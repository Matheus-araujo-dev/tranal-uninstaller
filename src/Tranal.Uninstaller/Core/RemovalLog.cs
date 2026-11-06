using System.Globalization;
using System.Text;
using System.Text.Json;
using Tranal.Uninstaller.Core.Modules;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Uma linha do log de remocao: o que saiu da maquina, o que nao saiu, e o
/// codigo de erro quando algo falhou.
/// </summary>
public sealed class RemovalLogEntry
{
    /// <summary>O tipo do item: Arquivo, Pasta, Registro ou Processo.</summary>
    public required string Kind { get; init; }

    /// <summary>O caminho do item.</summary>
    public required string Path { get; init; }

    /// <summary>O resultado: Removed, AlreadyAbsent, Failed, Protected ou Deferred.</summary>
    public required string Status { get; init; }

    /// <summary>O codigo de erro. Zero quando deu certo.</summary>
    public required int ErrorCode { get; init; }

    /// <summary>A explicacao, em portugues.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Itens removidos dentro da pasta.</summary>
    public int RemovedInside { get; init; }

    /// <summary>Itens protegidos mantidos dentro da pasta.</summary>
    public int ProtectedInside { get; init; }

    /// <summary>Itens que falharam dentro da pasta.</summary>
    public int FailedInside { get; init; }

    /// <summary>Itens adiados para o proximo reinicio dentro da pasta.</summary>
    public int DeferredInside { get; init; }
}

/// <summary>
/// O log de remocao por item.
///
/// O Uninstall Tool grava um log detalhado de remocao com codigo de erro, e
/// a auditoria do Geek/MSI pede o mesmo. Ate aqui o Tranal contava: sabia
/// quantos itens sairam, mas nao deixava rastro de QUAL item e POR QUE nao
/// saiu.
///
/// A regra mora aqui: o codigo de erro, a formatacao do CSV e o nome do
/// arquivo sao decididos num lugar so, e a interface grafica e a CLI consomem
/// o mesmo resultado.
/// </summary>
public static class RemovalLog
{
    /// <summary>
    /// Codigo usado quando a remocao foi barrada pelas regras de seguranca do
    /// Tranal. Nao e um codigo do Windows: 403 e o "acesso negado" de todo
    /// mundo, e o log precisa distinguir "o Windows recusou" de "o Tranal
    /// protegeu".
    /// </summary>
    public const int BlockedByPolicy = 403;

    /// <summary>
    /// Adiado para o proximo reinicio. E o mesmo numero que o instalador do
    /// Windows usa para "deu certo, mas precisa reiniciar".
    /// </summary>
    public const int DeferredToReboot = 3010;

    /// <summary>
    /// O codigo de erro de uma linha do log.
    ///
    /// O codigo do Windows tem prioridade. Quando o Windows nao disse nada
    /// (a remocao foi barrada pelo Tranal, ou foi adiada), o codigo vem do
    /// estado.
    /// </summary>
    /// <param name="outcome">O resultado da remocao.</param>
    public static int ErrorCodeFor(RemovalOutcome outcome) =>
        outcome.ErrorCode != 0 ? outcome.ErrorCode : outcome.Status switch
        {
            RemovalStatus.Protected => BlockedByPolicy,
            RemovalStatus.Deferred => DeferredToReboot,
            _ => 0,
        };

    /// <summary>
    /// Converte o relatorio de remocao nas linhas do log.
    /// </summary>
    /// <param name="report">O relatorio da operacao.</param>
    public static IReadOnlyList<RemovalLogEntry> Build(RemovalReport report) =>
        report.Outcomes
            .Select(o => new RemovalLogEntry
            {
                Kind = o.Item.Kind,
                Path = o.Item.Path,
                Status = o.Status.ToString(),
                ErrorCode = ErrorCodeFor(o),
                Message = o.Message,
                RemovedInside = o.RemovedInside,
                ProtectedInside = o.ProtectedInside,
                FailedInside = o.FailedInside,
                DeferredInside = o.DeferredInside,
            })
            .ToList();

    /// <summary>
    /// O log em CSV, com uma linha por item.
    /// </summary>
    /// <param name="entries">As linhas do log.</param>
    public static string ToCsv(IEnumerable<RemovalLogEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Tipo;Caminho;Resultado;Codigo;Mensagem;RemovidosDentro;ProtegidosDentro;FalharamDentro;AdiadosDentro");

        foreach (var e in entries)
        {
            sb.Append(Csv(e.Kind)).Append(';')
              .Append(Csv(e.Path)).Append(';')
              .Append(Csv(e.Status)).Append(';')
              .Append(e.ErrorCode.ToString(CultureInfo.InvariantCulture)).Append(';')
              .Append(Csv(e.Message)).Append(';')
              .Append(e.RemovedInside.ToString(CultureInfo.InvariantCulture)).Append(';')
              .Append(e.ProtectedInside.ToString(CultureInfo.InvariantCulture)).Append(';')
              .Append(e.FailedInside.ToString(CultureInfo.InvariantCulture)).Append(';')
              .Append(e.DeferredInside.ToString(CultureInfo.InvariantCulture))
              .AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// O log em JSON, com o resumo e as linhas.
    /// </summary>
    /// <param name="report">O relatorio da operacao.</param>
    /// <param name="programName">O nome do programa desinstalado.</param>
    public static string ToJson(RemovalReport report, string programName)
    {
        var payload = new
        {
            Programa = programName,
            Quando = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
            Total = report.Total,
            Removidos = report.RemovedCount,
            JaInexistentes = report.AbsentCount,
            Falharam = report.FailedCount,
            Protegidos = report.ProtectedCount,
            Adiados = report.DeferredCount,
            RemovidosDentro = report.RemovedInsideCount,
            Itens = Build(report),
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// A pasta onde os logs de remocao ficam gravados.
    ///
    /// E a mesma pasta dos logs de rastreio: e o mesmo produto, e o dono
    /// espera achar tudo em um lugar so.
    ///
    /// O nome nao e "Directory" de proposito: uma propriedade chamada assim
    /// esconderia o <see cref="System.IO.Directory"/> dentro desta classe, e
    /// "Directory.CreateDirectory(...)" passaria a tentar criar uma pasta
    /// chamada "CreateDirectory".
    /// </summary>
    public static string LogDirectory => Path.Combine(
        Path.GetDirectoryName(TraceStore.Directory) ?? TraceStore.Directory,
        "removal-logs");

    /// <summary>
    /// Grava o log de remocao e devolve o caminho do arquivo.
    /// </summary>
    /// <param name="report">O relatorio da operacao.</param>
    /// <param name="programName">O nome do programa desinstalado.</param>
    /// <param name="path">Onde gravar. Vazio usa a pasta padrao com nome automatico.</param>
    public static string Save(RemovalReport report, string programName, string? path = null)
    {
        var nome = string.IsNullOrWhiteSpace(programName) ? "programa" : programName;

        if (string.IsNullOrWhiteSpace(path))
        {
            System.IO.Directory.CreateDirectory(LogDirectory);
            var seguro = string.Join("-", nome.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
            if (seguro.Length == 0) seguro = "programa";
            path = Path.Combine(LogDirectory, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{seguro}.json");
        }
        else
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir)) System.IO.Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, ToJson(report, nome), new UTF8Encoding(false));
        return path;
    }

    /// <summary>Escapa um campo para CSV separado por ponto e virgula.</summary>
    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.IndexOf(';') < 0 && text.IndexOf('"') < 0 && text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0)
            return text;

        return '"' + text.Replace("\"", "\"\"") + '"';
    }
}
