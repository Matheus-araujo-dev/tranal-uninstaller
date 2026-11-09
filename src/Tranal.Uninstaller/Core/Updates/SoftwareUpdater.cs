using System.IO;
using System.Net.Http;
using System.Text.Json;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Updates;

/// <summary>
/// Um programa instalado que tem uma versão mais nova publicada.
///
/// É o cruzamento entre o que está na máquina (<see cref="InstalledProgram"/>)
/// e o que o feed de atualizações publicou.
/// </summary>
/// <param name="ProgramName">Nome do programa instalado.</param>
/// <param name="Publisher">Editor do programa.</param>
/// <param name="InstalledVersion">Versão que está instalada.</param>
/// <param name="AvailableVersion">Versão mais nova publicada.</param>
/// <param name="FeedKey">Chave da entrada no feed (é o identificador da lista negra).</param>
/// <param name="Source">Endereço de onde a atualização é obtida.</param>
public sealed record UpdateCandidate(
    string ProgramName,
    string Publisher,
    string InstalledVersion,
    string AvailableVersion,
    string FeedKey,
    string Source);

/// <summary>
/// Uma entrada do feed de atualizações de programas instalados.
/// </summary>
/// <param name="Key">Chave da entrada (identificador da lista negra).</param>
/// <param name="ProgramName">Nome do programa.</param>
/// <param name="Publisher">Editor do programa.</param>
/// <param name="Version">Versão publicada.</param>
/// <param name="Url">Endereço do instalador ou da página de atualização.</param>
/// <param name="Notes">Notas da versão.</param>
public sealed record UpdateFeedEntry(
    string Key,
    string ProgramName,
    string Publisher,
    string Version,
    string Url,
    string Notes);

/// <summary>
/// Origem da lista de atualizações publicadas.
///
/// A interface é o que separa "de onde vêm as atualizações" de "o que fazer
/// com elas": o mesmo motor de atualização serve para um feed HTTP e para um
/// arquivo local, e nenhum dos dois conhece o plano.
/// </summary>
public interface IUpdateFeedProvider
{
    /// <summary>
    /// As entradas do feed. Nunca devolve <c>null</c>; uma falha devolve lista
    /// vazia (e é registrada em log), porque um feed fora do ar não pode
    /// derrubar a varredura de atualizações.
    /// </summary>
    /// <param name="ct">Token de cancelamento.</param>
    Task<IReadOnlyList<UpdateFeedEntry>> ObterAsync(CancellationToken ct);
}

/// <summary>
/// Feed de atualizações lido de um arquivo JSON local.
///
/// O formato do arquivo é o mesmo do feed HTTP:
/// <code>
/// { "atualizacoes": [ { "chave": "...", "programa": "...", "editor": "...",
///                      "versao": "...", "url": "...", "notas": "..." } ] }
/// </code>
/// Um arquivo inválido devolve lista vazia — nunca uma exceção: o feed é uma
/// fonte externa, e uma fonte externa quebrada não pode derrubar o Tranal.
/// </summary>
public sealed class LocalUpdateFeedProvider : IUpdateFeedProvider
{
    /// <summary>Cria o provedor para o arquivo informado.</summary>
    /// <param name="caminho">Caminho do arquivo JSON do feed.</param>
    public LocalUpdateFeedProvider(string caminho)
    {
        Caminho = caminho ?? string.Empty;
    }

    /// <summary>Caminho do arquivo JSON lido pelo provedor.</summary>
    public string Caminho { get; }

    /// <summary>Lê o feed do disco.</summary>
    /// <param name="ct">Token de cancelamento.</param>
    public async Task<IReadOnlyList<UpdateFeedEntry>> ObterAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Caminho)) return Array.Empty<UpdateFeedEntry>();

        try
        {
            if (!File.Exists(Caminho)) return Array.Empty<UpdateFeedEntry>();

            var texto = await File.ReadAllTextAsync(Caminho, ct).ConfigureAwait(false);
            return UpdateFeedReader.Parse(texto);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Feed local invalido: lista vazia, sem log — o arquivo esta sob o
            // controle do usuario e o problema e visivel na propria leitura.
            return Array.Empty<UpdateFeedEntry>();
        }
    }
}

/// <summary>
/// Feed de atualizações lido de uma URL.
///
/// O tempo limite e curto e nao ha repeticao: um feed lento ou fora do ar
/// devolve lista vazia e registra o motivo em log, em vez de travar a
/// varredura de atualizações.
/// </summary>
public sealed class HttpUpdateFeedProvider : IUpdateFeedProvider
{
    /// <summary>Tempo maximo de espera da resposta (segundos).</summary>
    public const int TimeoutSeconds = 10;

    private static readonly HttpClient Client = CreateClient();

    /// <summary>Cria o provedor para a URL informada.</summary>
    /// <param name="url">Endereco do feed JSON.</param>
    public HttpUpdateFeedProvider(string url)
    {
        Url = url ?? string.Empty;
    }

    /// <summary>Endereco do feed lido pelo provedor.</summary>
    public string Url { get; }

    /// <summary>Baixa e interpreta o feed.</summary>
    /// <param name="ct">Token de cancelamento.</param>
    public async Task<IReadOnlyList<UpdateFeedEntry>> ObterAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Url)) return Array.Empty<UpdateFeedEntry>();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Url);
            request.Headers.TryAddWithoutValidation("User-Agent", AutoUpdateClient.UserAgent);

            using var response = await Client
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                ToolLog.Error($"Feed de atualizacoes indisponivel (HTTP {(int)response.StatusCode}): {Url}");
                return Array.Empty<UpdateFeedEntry>();
            }

            var corpo = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return UpdateFeedReader.Parse(corpo);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler o feed de atualizacoes '{Url}': {ex.Message}");
            return Array.Empty<UpdateFeedEntry>();
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(TimeoutSeconds),
        };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", AutoUpdateClient.UserAgent);
        return client;
    }
}

/// <summary>
/// Le o JSON do feed de atualizações.
///
/// A leitura mora aqui para que o feed local e o feed HTTP interpretaem o
/// mesmo formato: um só lugar decide o que é uma entrada válida.
/// </summary>
internal static class UpdateFeedReader
{
    /// <summary>
    /// Interpreta o JSON do feed. Texto invalido devolve lista vazia.
    /// </summary>
    /// <param name="texto">Conteudo do arquivo ou da resposta HTTP.</param>
    public static IReadOnlyList<UpdateFeedEntry> Parse(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return Array.Empty<UpdateFeedEntry>();

        try
        {
            using var document = JsonDocument.Parse(texto);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return Array.Empty<UpdateFeedEntry>();

            if (!document.RootElement.TryGetProperty("atualizacoes", out var lista)
                || lista.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<UpdateFeedEntry>();
            }

            var entradas = new List<UpdateFeedEntry>();

            foreach (var item in lista.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;

                var programa = Texto(item, "programa");
                if (programa.Length == 0) continue;

                entradas.Add(new UpdateFeedEntry(
                    Texto(item, "chave"),
                    programa,
                    Texto(item, "editor"),
                    Texto(item, "versao"),
                    Texto(item, "url"),
                    Texto(item, "notas")));
            }

            return entradas;
        }
        catch (JsonException ex)
        {
            ToolLog.Error($"Feed de atualizacoes com JSON invalido: {ex.Message}");
            return Array.Empty<UpdateFeedEntry>();
        }
    }

    private static string Texto(JsonElement item, string nome) =>
        item.TryGetProperty(nome, out var valor) && valor.ValueKind == JsonValueKind.String
            ? (valor.GetString() ?? string.Empty).Trim()
            : string.Empty;
}

/// <summary>
/// Uma entrada do plano com a decisão tomada sobre ela.
/// </summary>
/// <param name="Candidate">O programa e a versão nova.</param>
/// <param name="Pending">
/// True quando a atualização deve ser executada. False quando o update está
/// na lista negra — nesse caso <paramref name="Reason"/> diz por quê.
/// </param>
/// <param name="Reason">O motivo da recusa (vazio quando a atualização segue).</param>
public sealed record UpdatePlanItem(UpdateCandidate Candidate, bool Pending, string Reason);

/// <summary>
/// O plano de atualização de programas instalados.
///
/// O plano é montado **antes** de qualquer execução: é ele que mostra o que
/// será atualizado e o que foi deixado de fora, e por quê.
/// </summary>
public sealed class UpdatePlan
{
    /// <summary>Cria o plano com as entradas decididas.</summary>
    /// <param name="items">Entradas do plano.</param>
    public UpdatePlan(IReadOnlyList<UpdatePlanItem> items)
    {
        Items = items ?? Array.Empty<UpdatePlanItem>();
    }

    /// <summary>Todas as entradas do plano, na ordem em que foram decididas.</summary>
    public IReadOnlyList<UpdatePlanItem> Items { get; }

    /// <summary>As atualizações que devem ser executadas.</summary>
    public IReadOnlyList<UpdateCandidate> Pending =>
        Items.Where(i => i.Pending).Select(i => i.Candidate).ToList();

    /// <summary>As atualizações deixadas de fora, com o motivo.</summary>
    public IReadOnlyList<UpdatePlanItem> Skipped =>
        Items.Where(i => !i.Pending).ToList();
}

/// <summary>
/// O resultado de tentar abrir a atualização de um programa.
/// </summary>
/// <param name="ProgramName">Nome do programa.</param>
/// <param name="Success">True quando a atualização foi aberta com sucesso.</param>
/// <param name="Reason">O motivo da falha (vazio em caso de sucesso).</param>
public sealed record UpdateExecutionResult(string ProgramName, bool Success, string Reason);

/// <summary>
/// Abre a página ou o instalador de uma atualização.
///
/// A interface existe para que o lote seja testável sem rede e sem abrir
/// navegador: o teste injeta um abridor falso e confere o resultado por item.
/// </summary>
public interface IUpdateOpener
{
    /// <summary>Abre a atualização. Devolve true quando deu certo.</summary>
    /// <param name="url">Endereco a abrir.</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task<bool> OpenAsync(string url, CancellationToken ct);
}

/// <summary>
/// Abre a atualização no navegador padrão do Windows.
/// </summary>
public sealed class ShellUpdateOpener : IUpdateOpener
{
    /// <summary>Abre o endereco informado no navegador padrão.</summary>
    /// <param name="url">Endereco a abrir.</param>
    /// <param name="ct">Token de cancelamento.</param>
    public async Task<bool> OpenAsync(string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        try
        {
            var info = new System.Diagnostics.ProcessStartInfo(url)
            {
                UseShellExecute = true,
            };

            using var processo = System.Diagnostics.Process.Start(info);
            if (processo is null) return false;

            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao abrir a atualizacao '{url}': {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// Atualizador de programas instalados, em lote, com lista negra.
///
/// O que o concorrente medido faz: "atualizar programas instalados, em lote,
/// com lista negra de atualizacoes". As tres partes moram aqui:
/// a comparacao de versoes (<see cref="VersionComparer"/>), o plano (com o
/// motivo das recusas) e a execucao em lote (com o resultado por item).
/// </summary>
public sealed class SoftwareUpdater
{
    private readonly IUpdateFeedProvider _feed;
    private readonly IUpdateOpener _opener;
    private readonly HashSet<string> _blacklist;

    /// <summary>
    /// Cria o atualizador.
    /// </summary>
    /// <param name="feed">Origem das atualizações publicadas.</param>
    /// <param name="blacklist">
    /// Chaves (ou nomes de programa) que nunca podem ser atualizados. A
    /// comparação ignora a caixa.
    /// </param>
    /// <param name="opener">Abridor da atualização (o navegador, por padrão).</param>
    public SoftwareUpdater(
        IUpdateFeedProvider feed,
        IEnumerable<string>? blacklist = null,
        IUpdateOpener? opener = null)
    {
        _feed = feed ?? throw new ArgumentNullException(nameof(feed));
        _opener = opener ?? new ShellUpdateOpener();
        _blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in blacklist ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(item)) _blacklist.Add(item.Trim());
        }
    }

    /// <summary>
    /// As chaves da lista negra. Uma atualização cuja chave (ou nome de
    /// programa) esteja aqui é ignorada, e o motivo fica no plano.
    /// </summary>
    public IReadOnlyCollection<string> Blacklist => _blacklist;

    /// <summary>
    /// Monta o plano: cruza o feed com os programas instalados e decide o que
    /// será atualizado.
    ///
    /// Só entram no plano as atualizações com versão **mais nova** que a
    /// instalada — a comparação é a semântica de <see cref="VersionComparer"/>.
    /// </summary>
    /// <param name="programs">Os programas instalados.</param>
    /// <param name="entries">As entradas do feed.</param>
    public UpdatePlan BuildPlan(
        IReadOnlyList<InstalledProgram> programs,
        IReadOnlyList<UpdateFeedEntry> entries)
    {
        var itens = new List<UpdatePlanItem>();

        foreach (var entry in entries ?? Array.Empty<UpdateFeedEntry>())
        {
            var programa = Match(programs, entry);
            if (programa is null) continue;

            // Sem versao instalada nao ha como dizer que a publicada e mais
            // nova; sem versao publicada nao ha o que instalar.
            if (string.IsNullOrWhiteSpace(entry.Version)) continue;
            if (!VersionComparer.IsNewer(entry.Version, programa.DisplayVersion)) continue;

            var candidate = new UpdateCandidate(
                programa.DisplayName,
                string.IsNullOrWhiteSpace(entry.Publisher) ? programa.Publisher : entry.Publisher,
                programa.DisplayVersion,
                entry.Version,
                entry.Key,
                entry.Url);

            if (EstaNaListaNegra(candidate))
            {
                itens.Add(new UpdatePlanItem(
                    candidate,
                    Pending: false,
                    Reason: $"lista negra: {candidate.FeedKey}"));
                continue;
            }

            itens.Add(new UpdatePlanItem(candidate, Pending: true, Reason: string.Empty));
        }

        return new UpdatePlan(itens);
    }

    /// <summary>
    /// Lê o feed e monta o plano em uma operação só.
    /// </summary>
    /// <param name="programs">Os programas instalados.</param>
    /// <param name="ct">Token de cancelamento.</param>
    public async Task<UpdatePlan> MontarPlanoAsync(
        IReadOnlyList<InstalledProgram> programs, CancellationToken ct = default)
    {
        var entries = await _feed.ObterAsync(ct).ConfigureAwait(false);
        return BuildPlan(programs, entries);
    }

    /// <summary>
    /// Executa o plano em lote, devolvendo o resultado de cada item.
    ///
    /// Uma atualização que falha não interrompe as demais: o lote inteiro é
    /// tentado e o resultado de cada item é reportado separadamente.
    /// </summary>
    /// <param name="plan">O plano a executar.</param>
    /// <param name="ct">Token de cancelamento.</param>
    public async Task<IReadOnlyList<UpdateExecutionResult>> ExecutarAsync(
        UpdatePlan plan, CancellationToken ct = default)
    {
        var resultados = new List<UpdateExecutionResult>();
        if (plan is null) return resultados;

        foreach (var item in plan.Items)
        {
            if (!item.Pending) continue;

            ct.ThrowIfCancellationRequested();

            var url = item.Candidate.Source;
            if (string.IsNullOrWhiteSpace(url))
            {
                resultados.Add(new UpdateExecutionResult(
                    item.Candidate.ProgramName, false, "a entrada do feed nao tem endereco de download"));
                continue;
            }

            bool ok;
            string motivo;

            try
            {
                ok = await _opener.OpenAsync(url, ct).ConfigureAwait(false);
                motivo = ok ? string.Empty : "nao foi possivel abrir a atualizacao";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                ok = false;
                motivo = ex.Message;
            }

            resultados.Add(new UpdateExecutionResult(item.Candidate.ProgramName, ok, motivo));
        }

        return resultados;
    }

    /// <summary>
    /// True quando o candidato está na lista negra, pela chave do feed ou
    /// pelo nome do programa.
    /// </summary>
    private bool EstaNaListaNegra(UpdateCandidate candidate)
    {
        if (_blacklist.Count == 0) return false;

        if (!string.IsNullOrWhiteSpace(candidate.FeedKey) && _blacklist.Contains(candidate.FeedKey))
            return true;

        return !string.IsNullOrWhiteSpace(candidate.ProgramName)
               && _blacklist.Contains(candidate.ProgramName);
    }

    /// <summary>
    /// O programa instalado que corresponde à entrada do feed.
    ///
    /// O nome é o que casa; o editor só é conferido quando os dois o
    /// declaram — assim duas entradas de mesmo nome e editores diferentes não
    /// se misturam.
    /// </summary>
    private static InstalledProgram? Match(
        IReadOnlyList<InstalledProgram> programs, UpdateFeedEntry entry)
    {
        InstalledProgram? encontrado = null;

        foreach (var programa in programs ?? Array.Empty<InstalledProgram>())
        {
            if (programa is null) continue;

            if (!string.Equals(programa.DisplayName?.Trim(), entry.ProgramName?.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (encontrado is null)
            {
                encontrado = programa;
                continue;
            }

            // Mesmo nome: o editor decide, quando os dois o declaram.
            var meu = programa.Publisher?.Trim() ?? string.Empty;
            var dele = entry.Publisher?.Trim() ?? string.Empty;

            if (meu.Length > 0 && dele.Length > 0
                && string.Equals(meu, dele, StringComparison.OrdinalIgnoreCase))
            {
                return programa;
            }
        }

        return encontrado;
    }
}
