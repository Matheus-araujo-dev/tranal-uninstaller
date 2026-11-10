using System.IO;
using System.Text.Json;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Um programa que apareceu na maquina depois da ultima linha de base.
/// </summary>
/// <param name="DisplayName">O nome do programa.</param>
/// <param name="RegistryKey">A chave do registro que o identifica.</param>
/// <param name="Publisher">A empresa que registrou o programa.</param>
/// <param name="InstallLocation">A pasta de instalacao.</param>
/// <param name="InstallDate">A data de instalacao, quando o Windows a informa.</param>
/// <param name="FromBaseline">
/// Verdadeiro quando o programa foi detectado por comparacao com a linha de
/// base; falso quando o sinal veio da data de instalacao.
/// </param>
public sealed record NewInstallEntry(
    string DisplayName,
    string RegistryKey,
    string Publisher,
    string InstallLocation,
    DateTime? InstallDate,
    bool FromBaseline);

/// <summary>
/// O arquivo da linha de base: a lista dos programas que ja estavam na
/// maquina quando o Tranal olhou pela ultima vez.
/// </summary>
internal sealed class InstallBaseline
{
    public DateTime At { get; set; } = DateTime.MinValue;
    public List<string> Keys { get; set; } = new();
}

/// <summary>
/// Os programas que apareceram depois da ultima vez que o Tranal olhou.
///
/// A regra e uma so: um programa e **novo** quando a chave dele nao estava
/// na linha de base gravada na ultima varredura. Sem linha de base, o sinal
/// que sobra e a data de instalacao — e o texto do programa diz de onde veio
/// o sinal, para ninguem confundir uma coisa com a outra.
///
/// A linha de base fica em disco, na pasta de dados do Tranal. E ela que
/// permite dizer "isto foi instalado ontem", em vez de adivinhar pela data.
/// </summary>
public static class NewInstallsTracker
{
    /// <summary>
    /// Substitui a pasta de dados (so a suite de testes). Nulo em uso normal.
    /// </summary>
    internal static string? FolderOverride { get; set; }

    /// <summary>A pasta onde ficam a linha de base e os dados dos novos.</summary>
    public static string Folder =>
        FolderOverride ?? Path.Combine(RegistryBackup.DataDirectory, "new-installs");

    /// <summary>O arquivo da linha de base.</summary>
    public static string BaselineFilePath => Path.Combine(Folder, "baseline.json");

    /// <summary>Existe linha de base gravada?</summary>
    public static bool HasBaseline => File.Exists(BaselineFilePath);

    /// <summary>
    /// Quando a linha de base foi gravada. Nulo quando nao existe linha de
    /// base — sem dado, sem data.
    /// </summary>
    public static DateTime? BaselineAt
    {
        get
        {
            var baseline = ReadBaseline();
            if (baseline is null) return null;
            return baseline.At == DateTime.MinValue ? null : baseline.At;
        }
    }

    /// <summary>
    /// Grava a linha de base: a lista de programas que estao na maquina
    /// agora passa a ser a referencia do "ja existia".
    /// </summary>
    /// <param name="programs">Os programas varridos.</param>
    /// <returns>Quantos programas entraram na linha de base.</returns>
    public static int SaveBaseline(IEnumerable<InstalledProgram>? programs)
    {
        var lista = (programs ?? Array.Empty<InstalledProgram>())
            .Where(p => p is not null)
            .Select(KeyOf)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var baseline = new InstallBaseline
        {
            At = DateTime.Now,
            Keys = lista,
        };

        try
        {
            Directory.CreateDirectory(Folder);
            var json = JsonSerializer.Serialize(baseline, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            File.WriteAllText(BaselineFilePath, json);
        }
        catch
        {
            // Sem linha de base gravada, o rastreio continua funcionando por
            // data de instalacao. Falhar aqui nao pode parar o Tranal.
        }

        return lista.Count;
    }

    /// <summary>
    /// Os programas novos, em relacao a linha de base.
    ///
    /// Com linha de base, o sinal e a comparacao das chaves. Sem linha de
    /// base, o sinal e a data de instalacao dentro da janela configurada — e
    /// <see cref="NewInstallEntry.FromBaseline"/> diz qual dos dois foi.
    /// </summary>
    /// <param name="programs">Os programas varridos.</param>
    /// <param name="days">
    /// A janela de dias usada quando nao ha linha de base. Nulo usa a janela
    /// das preferencias.
    /// </param>
    public static IReadOnlyList<NewInstallEntry> Detect(
        IEnumerable<InstalledProgram>? programs,
        int? days = null)
    {
        var lista = (programs ?? Array.Empty<InstalledProgram>()).Where(p => p is not null).ToList();
        if (lista.Count == 0) return Array.Empty<NewInstallEntry>();

        var baseline = ReadBaseline();

        if (baseline is not null)
        {
            var conhecidos = new HashSet<string>(baseline.Keys, StringComparer.OrdinalIgnoreCase);
            return lista
                .Where(p => !conhecidos.Contains(KeyOf(p)))
                .Select(p => ToEntry(p, fromBaseline: true))
                .ToList();
        }

        var corte = Cutoff(days);

        return lista
            .Where(p => p.InstallDateValue is { } data && data >= corte)
            .Select(p => ToEntry(p, fromBaseline: false))
            .ToList();
    }

    /// <summary>
    /// O programa e novo? Mesma regra de <see cref="Detect"/>, para um
    /// programa so.
    ///
    /// O sinal e a **linha de base**: o que apareceu depois da ultima vez que
    /// o Tranal olhou. E o sinal mais forte que existe — mas ele e do
    /// rastreio de instalacoes, nao da lista.
    /// </summary>
    /// <param name="program">O programa.</param>
    public static bool IsNew(InstalledProgram? program)
    {
        if (program is null) return false;

        var conhecidos = KnownKeys();
        if (conhecidos is not null) return !conhecidos.Contains(KeyOf(program));

        return IsRecent(program);
    }

    /// <summary>
    /// O corte da janela de "Novos Programas": a meia-noite de (hoje - janela).
    ///
    /// O dia inteiro conta: um programa instalado ontem continua novo hoje
    /// de manha. E a mesma janela que o Revo usa (medido).
    /// </summary>
    /// <param name="days">
    /// A janela em dias. Nulo usa a janela das preferencias.
    /// </param>
    public static DateTime Cutoff(int? days = null)
    {
        var janela = Math.Max(1, days ?? Settings.AppSettingsStore.Current.NewProgramsDays);
        var corte = DateTime.Now.AddDays(-janela);
        return new DateTime(corte.Year, corte.Month, corte.Day);
    }

    /// <summary>
    /// O programa foi instalado dentro da janela de "Novos Programas"?
    ///
    /// E a REGRA da janela, em um lugar so: a coluna "Grupo" da lista e o
    /// realce do programa novo consomem esta resposta, e nunca discordam
    /// sobre o que e "novo".
    ///
    /// A linha de base (<see cref="IsNew"/>) e um sinal mais forte, do
    /// rastreio de instalacoes; a janela e o que a lista mostra.
    /// </summary>
    /// <param name="program">O programa.</param>
    /// <param name="days">
    /// A janela em dias. Nulo usa a janela das preferencias.
    /// </param>
    public static bool IsRecent(InstalledProgram? program, int? days = null)
    {
        if (program is null) return false;

        return program.InstallDateValue is { } data && data >= Cutoff(days);
    }

    /// <summary>Apaga a linha de base: depois disso, o sinal volta a ser a data.</summary>
    public static bool ClearBaseline()
    {
        try
        {
            if (File.Exists(BaselineFilePath)) File.Delete(BaselineFilePath);

            _baseline = null;
            _baselineKeys = null;

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// A chave que identifica o programa. E a chave do registro quando ela
    /// existe; sem ela, o caminho de instalacao; sem ele, o nome.
    /// </summary>
    private static string KeyOf(InstalledProgram program)
    {
        if (!string.IsNullOrWhiteSpace(program.RegistryKey)) return program.RegistryKey;
        if (!string.IsNullOrWhiteSpace(program.RegistryPath)) return program.RegistryPath;
        if (!string.IsNullOrWhiteSpace(program.InstallLocation)) return program.InstallLocation;
        return program.DisplayName;
    }

    private static NewInstallEntry ToEntry(InstalledProgram program, bool fromBaseline) => new(
        program.DisplayName,
        program.RegistryKey,
        program.Publisher,
        program.InstallLocation,
        program.InstallDateValue,
        fromBaseline);

    /// <summary>
    /// A linha de base em memoria, e a hora em que o arquivo foi escrito.
    ///
    /// A lista pergunta o sinal do "programa novo" para cada linha: reler o
    /// arquivo a cada linha travaria a janela. O cache e invalidado quando
    /// o arquivo muda.
    /// </summary>
    private static InstallBaseline? _baseline;

    private static DateTime _baselineWrite;

    private static HashSet<string>? _baselineKeys;

    /// <summary>
    /// As chaves conhecidas da linha de base (null quando nao ha linha de
    /// base).
    /// </summary>
    private static HashSet<string>? KnownKeys()
    {
        ReadBaseline();
        return _baselineKeys;
    }

    private static InstallBaseline? ReadBaseline()
    {
        try
        {
            if (!File.Exists(BaselineFilePath))
            {
                _baseline = null;
                _baselineKeys = null;
                return null;
            }

            var escrita = File.GetLastWriteTimeUtc(BaselineFilePath);
            if (_baseline is not null && escrita == _baselineWrite) return _baseline;

            var json = File.ReadAllText(BaselineFilePath);
            var loaded = JsonSerializer.Deserialize<InstallBaseline>(json);
            if (loaded is null) return null;

            loaded.Keys ??= new List<string>();

            _baseline = loaded;
            _baselineWrite = escrita;
            _baselineKeys = new HashSet<string>(loaded.Keys, StringComparer.OrdinalIgnoreCase);

            return loaded;
        }
        catch
        {
            // Uma linha de base corrompida e um arquivo ilegivel: o sinal
            // volta a ser a data de instalacao, sem travar o Tranal.
            return null;
        }
    }
}
