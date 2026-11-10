using System.IO;
using System.Globalization;

namespace Tranal.Uninstaller.Core.Recovery;

/// <summary>
/// O resultado do backup diario do registro.
/// </summary>
public sealed class DailyRegistryBackupResult
{
    /// <summary>True quando o backup foi feito agora.</summary>
    public bool Ran { get; init; }

    /// <summary>True quando o backup deu certo (quando rodou).</summary>
    public bool Success { get; init; }

    /// <summary>True quando ja havia um backup hoje e nada foi feito.</summary>
    public bool Skipped { get; init; }

    /// <summary>A pasta do backup criado.</summary>
    public string Directory { get; init; } = string.Empty;

    /// <summary>A explicacao.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Um backup diario ja feito.
/// </summary>
public sealed class DailyRegistryBackupEntry
{
    /// <summary>O dia do backup.</summary>
    public DateTime Date { get; init; }

    /// <summary>A pasta do backup.</summary>
    public string Directory { get; init; } = string.Empty;

    /// <summary>Os arquivos .reg do backup.</summary>
    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
}

/// <summary>
/// O backup completo do registro, todo dia.
///
/// O <see cref="FullRegistryBackup"/> ja exporta o registro inteiro, mas so
/// rodava sob demanda, antes de uma desinstalacao. O item 49 do backlog e
/// o outro: um backup diario, sem o usuario pedir nada.
///
/// A exportacao continua sendo a do <see cref="FullRegistryBackup"/> (o
/// <c>reg.exe</c> do Windows): este componente so orquestra a quando rodar —
/// nunca reescreve o formato do backup.
/// </summary>
public static class DailyRegistryBackup
{
    /// <summary>
    /// Substitui o caminho do marcador (so a suite de testes).
    /// </summary>
    internal static string? MarkerPathOverride { get; set; }

    /// <summary>
    /// Substitui a pasta de destino do backup (so a suite de testes).
    /// </summary>
    internal static string? DestinationOverride { get; set; }

    /// <summary>
    /// Substitui a pasta raiz dos backups diarios (so a suite de testes).
    /// </summary>
    internal static string? RootDirectoryOverride { get; set; }

    /// <summary>
    /// A pasta que guarda os backups diarios (um por dia).
    /// </summary>
    public static string RootDirectory =>
        RootDirectoryOverride ?? Path.Combine(RegistryBackup.DataDirectory, "RegBackup", "Full");

    /// <summary>
    /// O arquivo que guarda a data do ultimo backup diario.
    /// </summary>
    public static string MarkerPath =>
        MarkerPathOverride ?? Path.Combine(RegistryBackup.DataDirectory, "RegBackup", "diario.txt");

    /// <summary>
    /// Quando foi o ultimo backup diario (UTC). Nulo quando nunca houve.
    /// </summary>
    public static DateTime? LastRunUtc
    {
        get
        {
            try
            {
                var arquivo = MarkerPath;
                if (!File.Exists(arquivo)) return null;

                var texto = File.ReadAllText(arquivo).Trim();
                if (texto.Length == 0) return null;

                return DateTime.TryParse(texto, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var data)
                    ? data
                    : null;
            }
            catch (Exception) { return null; }
        }
    }

    /// <summary>
    /// True quando o backup diario ainda nao foi feito hoje.
    /// </summary>
    public static bool IsDue
    {
        get
        {
            var ultimo = LastRunUtc;
            if (ultimo is null) return true;

            return ultimo.Value.Date != DateTime.UtcNow.Date;
        }
    }

    /// <summary>
    /// Faz o backup diario, se ele ainda nao foi feito hoje.
    /// </summary>
    /// <param name="daysInterval">
    /// De quantos em quantos dias o backup deve ser feito. Padrao: 1 (todo
    /// dia).
    /// </param>
    public static DailyRegistryBackupResult RunIfDue(int daysInterval = 1)
    {
        if (daysInterval < 1) daysInterval = 1;

        var ultimo = LastRunUtc;
        if (ultimo is not null)
        {
            var passou = (DateTime.UtcNow - ultimo.Value).TotalDays;
            if (passou < daysInterval)
            {
                return new DailyRegistryBackupResult
                {
                    Ran = false,
                    Skipped = true,
                    Success = true,
                    Message = $"o backup diario ja foi feito ({ultimo.Value.ToLocalTime():yyyy-MM-dd})",
                };
            }
        }

        return Run();
    }

    /// <summary>
    /// Faz o backup diario agora, sem olhar a data do ultimo backup.

    /// E o que o botao "Executar agora" da tela chama: o usuario pediu,
    /// e o pedido manda.
    /// </summary>
    public static DailyRegistryBackupResult Run()
    {
        var destino = DestinationOverride ?? Path.Combine(
            RootDirectory, "diario-" + DateTime.Now.ToString("yyyy-MM-dd"));

        var resultado = FullRegistryBackup.Create(destino);

        if (resultado.Success)
        {
            GravarMarcador();
        }

        return new DailyRegistryBackupResult
        {
            Ran = true,
            Success = resultado.Success,
            Directory = resultado.Directory,
            Message = resultado.Success
                ? $"backup diario criado em {resultado.Directory}"
                : resultado.Message,
        };
    }

    /// <summary>
    /// Os backups diarios ja feitos, do mais novo para o mais antigo.
    /// </summary>
    public static IReadOnlyList<DailyRegistryBackupEntry> List()
    {
        var resultado = new List<DailyRegistryBackupEntry>();

        try
        {
            var raiz = RootDirectory;
            if (!Directory.Exists(raiz)) return resultado;

            foreach (var pasta in Directory.EnumerateDirectories(raiz, "diario-*"))
            {
                var nome = Path.GetFileName(pasta);
                var data = DateTime.MinValue;

                if (nome.Length > "diario-".Length)
                {
                    DateTime.TryParseExact(nome["diario-".Length..], "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out data);
                }

                resultado.Add(new DailyRegistryBackupEntry
                {
                    Date = data,
                    Directory = pasta,
                    Files = Directory.EnumerateFiles(pasta, "*.reg").ToList(),
                });
            }
        }
        catch (Exception)
        {
            return resultado;
        }

        return resultado.OrderByDescending(e => e.Date).ToList();
    }

    /// <summary>
    /// Apaga o marcador: o proximo <see cref="RunIfDue"/> roda de novo.
    /// </summary>
    public static bool Reset()
    {
        try
        {
            var arquivo = MarkerPath;
            if (File.Exists(arquivo)) File.Delete(arquivo);
            return true;
        }
        catch (Exception) { return false; }
    }

    private static void GravarMarcador()
    {
        try
        {
            var arquivo = MarkerPath;
            var pasta = Path.GetDirectoryName(arquivo);
            if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

            File.WriteAllText(arquivo, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        }
        catch (Exception) { /* o marcador e uma convenience: perder nao e desastre */ }
    }
}
