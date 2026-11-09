using System.IO;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// Um arquivo de dump de falha.
/// </summary>
public sealed record CrashDump
{
    /// <summary>O caminho do arquivo .dmp.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>O nome do arquivo.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>O tamanho do arquivo, em bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>A data de gravacao do dump.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// O programa que gerou o dump, quando da para saber pelo nome do
    /// arquivo (o Windows grava o nome do processo no dump).
    /// </summary>
    public string Suspect { get; init; } = string.Empty;

    /// <summary>
    /// Quantas falhas do mesmo programa o Event Log registrou no periodo
    /// (zero quando o Event Log nao pode ser lido).
    /// </summary>
    public int CrashCount { get; init; }

    /// <summary>A data da ultima falha do programa, quando ha registro.</summary>
    public DateTime? LastCrashAt { get; init; }
}

/// <summary>
/// Um programa que deixou dumps de falha.
/// </summary>
public sealed record CrashDumpProgram
{
    /// <summary>O nome do programa.</summary>
    public string Program { get; init; } = string.Empty;

    /// <summary>Quantos dumps o programa deixou.</summary>
    public int Dumps { get; init; }

    /// <summary>Quanto os dumps ocupam, em bytes.</summary>
    public long Bytes { get; init; }

    /// <summary>Quantas falhas o Event Log registrou do mesmo programa.</summary>
    public int Crashes { get; init; }
}

/// <summary>
/// O resumo dos dumps de falha.
/// </summary>
public sealed record CrashDumpSummary
{
    /// <summary>Quantos dumps foram encontrados.</summary>
    public int Count { get; init; }

    /// <summary>Quanto os dumps ocupam, em bytes.</summary>
    public long TotalBytes { get; init; }

    /// <summary>Os programas que deixaram dumps, do maior para o menor.</summary>
    public IReadOnlyList<CrashDumpProgram> ByProgram { get; init; } = Array.Empty<CrashDumpProgram>();
}

/// <summary>
/// Coleta os dumps de falha do Windows.
///
/// O ashampoo tem um "Crash Analyzer" que le o Event Log; o IObit e o
/// ashampoo tambem limpam os dumps de falha (arquivos .dmp) que ocupam
/// espaco e nao servem para nada depois que o problema foi resolvido.
///
/// A REGRA mora aqui, em um lugar so: onde o Windows guarda os dumps
/// (MiniDump e FullDump) e a leitura desses arquivos ficam aqui. O
/// correlato com as falhas do Event Log vem do
/// <see cref="CrashAnalyzer"/> — quem mostra consome o veredito.
///
/// A limpeza dos dumps usa o motor canonico de remocao, que respeita as
/// regras de seguranca e sabe adiar o que estiver travado.
/// </summary>
public static class CrashDumpCollector
{
    /// <summary>
    /// As pastas onde o Windows grava os dumps de falha.
    /// </summary>
    public static IReadOnlyList<string> Folders()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        return new List<string>
        {
            Path.Combine(windows, "Minidump"),
            Path.Combine(windows, "LiveKernelReports"),
            Path.Combine(local, "CrashDumps"),
            Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportQueue"),
        };
    }

    /// <summary>
    /// Coleta os dumps de falha e o correlato com as falhas do Event Log.
    ///
    /// A pasta pode ser injetada: e o que permite medir a coleta em uma
    /// pasta de teste, sem depender do que a maquina tem gravado.
    /// </summary>
    /// <param name="dias">Quantos dias do Event Log olhar para tras.</param>
    /// <param name="pastas">As pastas varridas (vazio = as pastas do Windows).</param>
    /// <param name="limite">O numero maximo de dumps.</param>
    public static IReadOnlyList<CrashDump> Collect(
        int dias = 30,
        IReadOnlyList<string>? pastas = null,
        int limite = 500)
    {
        var alvo = pastas is null || pastas.Count == 0 ? Folders() : pastas;
        var dumps = Scan(alvo, limite);

        var falhas = CrashAnalyzer.Scan(dias);

        return dumps
            .Select(dump =>
            {
                var falha = FindCrash(falhas, dump.Suspect);

                return new CrashDump
                {
                    Path = dump.Path,
                    Name = dump.Name,
                    SizeBytes = dump.SizeBytes,
                    CreatedAt = dump.CreatedAt,
                    Suspect = dump.Suspect,
                    CrashCount = falha?.Count ?? 0,
                    LastCrashAt = falha?.LastSeen,
                };
            })
            .ToList();
    }

    /// <summary>
    /// Os dumps de falha de um programa.
    ///
    /// O nome do processo vem do proprio nome do arquivo do dump — e o que o
    /// Windows grava nele.
    /// </summary>
    /// <param name="programa">O nome do programa.</param>
    /// <param name="dias">Quantos dias do Event Log olhar para tras.</param>
    /// <param name="pastas">As pastas varridas (vazio = as pastas do Windows).</param>
    public static IReadOnlyList<CrashDump> FindFor(
        string? programa,
        int dias = 30,
        IReadOnlyList<string>? pastas = null)
    {
        var nome = (programa ?? string.Empty).Trim();
        if (nome.Length == 0) return Array.Empty<CrashDump>();

        return Collect(dias, pastas)
            .Where(dump => MesmaPrograma(dump.Suspect, nome))
            .ToList();
    }

    /// <summary>
    /// O resumo dos dumps: quantos sao, quanto ocupam e de quem sao.
    /// </summary>
    /// <param name="dumps">Os dumps a resumir (vazio = todos os do Windows).</param>
    public static CrashDumpSummary Summarize(IReadOnlyList<CrashDump>? dumps = null)
    {
        var alvo = dumps ?? Collect();

        var porPrograma = alvo
            .GroupBy(d => string.IsNullOrWhiteSpace(d.Suspect) ? "(sem programa)" : d.Suspect, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CrashDumpProgram
            {
                Program = g.Key,
                Dumps = g.Count(),
                Bytes = g.Sum(d => d.SizeBytes),
                Crashes = g.Max(d => d.CrashCount),
            })
            .OrderByDescending(p => p.Bytes)
            .ThenBy(p => p.Program, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CrashDumpSummary
        {
            Count = alvo.Count,
            TotalBytes = alvo.Sum(d => d.SizeBytes),
            ByProgram = porPrograma,
        };
    }

    /// <summary>
    /// O total ocupado pelos dumps de falha.
    /// </summary>
    public static long TotalSize() => Collect().Sum(d => d.SizeBytes);

    /// <summary>
    /// Apaga os dumps de falha e devolve o relatorio canonico.
    /// </summary>
    /// <param name="dumps">Os dumps a apagar (vazio = todos).</param>
    public static RemovalReport Clear(IEnumerable<CrashDump>? dumps = null)
    {
        var alvos = (dumps ?? Collect()).Where(d => d is not null).ToList();
        if (alvos.Count == 0) return new RemovalReport(new List<RemovalOutcome>());

        var sobras = alvos.Select(d => new LeftoverItem
        {
            Kind = "Arquivo",
            Path = d.Path,
            Detail = "Dump de falha",
            Selected = true,
        }).ToList();

        try
        {
            return UninstallEngine.RemoveLeftovers(sobras, new UninstallRemovalOptions
            {
                Backup = false,
                UseRecycleBin = true,
                DeferToNextRestart = true,
            });
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao apagar os dumps de falha: {ex.Message}");
            return new RemovalReport(new List<RemovalOutcome>());
        }
    }

    /// <summary>
    /// O nome do processo que gerou o dump, lido do nome do arquivo.
    ///
    /// O Windows grava o dump como "nome-do-programa.exe.1234.dmp" ou
    /// "WER-1234-abc.dump" — no primeiro caso o nome do processo esta no
    /// proprio arquivo.
    /// </summary>
    /// <param name="path">O caminho do dump.</param>
    public static string GuessProcess(string? path)
    {
        var nome = Path.GetFileNameWithoutExtension(PathUtil.Normalize(path));
        if (nome.Length == 0) return string.Empty;

        // "app.exe.1234" -> "app.exe"
        var ponto = nome.LastIndexOf('.');
        if (ponto > 0)
        {
            var parte = nome[..ponto];
            if (parte.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return parte;
            if (parte.EndsWith(".sys", StringComparison.OrdinalIgnoreCase)) return parte;
        }

        return nome;
    }

    /// <summary>
    /// Varre as pastas e devolve os dumps encontrados.
    /// </summary>
    private static IReadOnlyList<CrashDump> Scan(IReadOnlyList<string> pastas, int limite)
    {
        var dumps = new List<CrashDump>();

        foreach (var pasta in pastas)
        {
            try
            {
                if (!Directory.Exists(pasta)) continue;

                foreach (var arquivo in Directory.EnumerateFiles(pasta, "*.dmp", SearchOption.AllDirectories))
                {
                    var info = SafeInfo(arquivo);
                    if (info is null) continue;

                    dumps.Add(new CrashDump
                    {
                        Path = PathUtil.Normalize(arquivo),
                        Name = Path.GetFileName(arquivo),
                        SizeBytes = info.Length,
                        CreatedAt = info.LastWriteTime,
                        Suspect = GuessProcess(arquivo),
                    });

                    if (dumps.Count >= limite) return dumps;
                }
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao varrer os dumps de '{pasta}': {ex.Message}");
            }
        }

        return dumps.OrderByDescending(d => d.CreatedAt).ToList();
    }

    /// <summary>
    /// A entrada do Event Log do mesmo programa do dump.
    /// </summary>
    private static CrashEntry? FindCrash(IReadOnlyList<CrashEntry> falhas, string suspect)
    {
        if (suspect.Length == 0) return null;

        foreach (var falha in falhas)
        {
            if (MesmaPrograma(falha.ProgramName, suspect)) return falha;
        }

        return null;
    }

    /// <summary>
    /// True quando o nome do dump e o nome do programa são o mesmo programa.
    ///
    /// O dump traz o nome do arquivo do executavel ("app.exe") e o Event Log
    /// traz o nome do produto ("App") — a comparacao ignora a extensao e a
    /// caixa.
    /// </summary>
    private static bool MesmaPrograma(string? um, string? outro)
    {
        var a = (um ?? string.Empty).Trim();
        var b = (outro ?? string.Empty).Trim();
        if (a.Length == 0 || b.Length == 0) return false;

        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;

        var aSemExtensao = Path.GetFileNameWithoutExtension(a);
        var bSemExtensao = Path.GetFileNameWithoutExtension(b);

        return string.Equals(aSemExtensao, bSemExtensao, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Os dados do arquivo, quando ele pode ser lido.
    /// </summary>
    private static FileInfo? SafeInfo(string path)
    {
        try { return new FileInfo(path); }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler os dados de '{path}': {ex.Message}");
            return null;
        }
    }
}
