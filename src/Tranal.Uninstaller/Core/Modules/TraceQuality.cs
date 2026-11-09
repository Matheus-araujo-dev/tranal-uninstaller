using System.IO;
using System.Text.Json;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// O estado do aviso sobre a qualidade do rastreio.
/// </summary>
/// <param name="ShouldAsk">O aviso ainda deve ser mostrado?</param>
/// <param name="TimesAsked">Quantas vezes o aviso ja foi mostrado.</param>
/// <param name="TraceCount">Quantos rastros existem.</param>
/// <param name="Detail">O texto do aviso.</param>
public sealed record TraceQualityState(
    bool ShouldAsk,
    int TimesAsked,
    int TraceCount,
    string Detail);

/// <summary>
/// O arquivo do aviso.
/// </summary>
internal sealed class TraceQualityFile
{
    public int TimesAsked { get; set; }
    public bool AskDisabled { get; set; }
}

/// <summary>
/// O aviso para melhorar a qualidade do rastreio.
///
/// O rastreio do Tranal e melhor quando o programa roda com os logs do
/// Windows ligados — e o usuario nem sempre sabe disso. O aviso existe para
/// explicar isso uma vez, e nao todas as vezes.
///
/// A regra esta em um lugar so: este modulo sabe quando o aviso deve
/// aparecer e o que ele diz.
/// </summary>
public static class TraceQuality
{
    /// <summary>
    /// Quantas vezes o aviso pode aparecer antes de o Tranal parar de
    /// perguntar.
    /// </summary>
    public const int MaxTimes = 3;

    /// <summary>
    /// Substitui a pasta de dados (so a suite de testes). Nulo em uso normal.
    /// </summary>
    internal static string? FolderOverride { get; set; }

    /// <summary>A pasta onde fica o estado do aviso.</summary>
    public static string Folder =>
        FolderOverride ?? Path.Combine(RegistryBackup.DataDirectory, "trace-quality");

    /// <summary>O arquivo do estado do aviso.</summary>
    public static string FilePath => Path.Combine(Folder, "quality.json");

    /// <summary>
    /// O estado do aviso: se ele ainda deve aparecer, quantas vezes ja
    /// apareceu e quantos rastros existem.
    /// </summary>
    public static TraceQualityState State()
    {
        var arquivo = Read();
        var rastros = TraceStore.List().Count;
        var vezes = arquivo?.TimesAsked ?? 0;
        var desativado = arquivo?.AskDisabled ?? false;

        var deveMostrar = !desativado && vezes < MaxTimes && rastros > 0;

        var detalhe = deveMostrar
            ? "Ligue o rastreio detalhado do Windows para o Tranal reconhecer melhor o que cada programa instalou."
            : rastros == 0
                ? "Nenhum rastreio de instalacao ainda."
                : "O aviso sobre a qualidade do rastreio nao aparece mais.";

        return new TraceQualityState(deveMostrar, vezes, rastros, detalhe);
    }

    /// <summary>
    /// Marca que o aviso foi mostrado.
    /// </summary>
    public static bool MarkAsked()
    {
        try
        {
            var arquivo = Read() ?? new TraceQualityFile();
            arquivo.TimesAsked++;

            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(arquivo, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// O usuario nao quer mais ver o aviso.
    /// </summary>
    public static bool Disable()
    {
        try
        {
            var arquivo = Read() ?? new TraceQualityFile();
            arquivo.AskDisabled = true;

            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(arquivo, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static TraceQualityFile? Read()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<TraceQualityFile>(json);
        }
        catch
        {
            return null;
        }
    }
}
