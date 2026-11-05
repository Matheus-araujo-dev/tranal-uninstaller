using System.IO;
using System.Net.Http;
using System.Text.Json;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Updates;

/// <summary>
/// Resultado de uma verificacao de atualizacao.
/// </summary>
public sealed class UpdateCheckResult
{
    /// <summary>True quando a verificacao foi feita com sucesso.</summary>
    public bool Success { get; init; }

    /// <summary>Versao mais recente publicada (string vazia quando desconhecida).</summary>
    public string? LatestVersion { get; init; }

    /// <summary>Endereco do pacote de atualizacao (string vazia quando desconhecido).</summary>
    public string? DownloadUrl { get; init; }

    /// <summary>Notas de versao publicadas (string vazia quando inexistentes).</summary>
    public string? ReleaseNotes { get; init; }

    /// <summary>Motivo da falha (null quando a verificacao deu certo).</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Progresso do download de uma atualizacao.
/// </summary>
public sealed class DownloadProgress
{
    /// <summary>Bytes ja baixados.</summary>
    public long BytesDownloaded { get; init; }

    /// <summary>Total de bytes da atualizacao (null quando o servidor nao informa).</summary>
    public long? TotalBytes { get; init; }

    /// <summary>Fracao concluida (0 a 1). Null quando o total nao e conhecido.</summary>
    public double? Fraction { get; init; }
}

/// <summary>
/// Cliente de verificacao e download de atualizacoes.
///
/// Correspondente as opcoes "Verificar por atualizacoes ao iniciar" e
/// "Atualizar automaticamente em segundo plano" do Revo Uninstaller Pro.
///
/// O endereco do feed NAO e fixo no codigo: quem chama informa a URL. O
/// cliente aceita tanto um feed JSON quanto um arquivo de texto simples com a
/// versao, e le o campo de versao de chaves usuais ("latestVersion",
/// "version", "tag_name").
/// </summary>
public sealed class AutoUpdateClient
{
    /// <summary>Tempo maximo de espera das respostas HTTP (segundos).</summary>
    public const int TimeoutSeconds = 15;

    /// <summary>Tamanho do bloco de leitura do download (64 KB).</summary>
    private const int BufferSize = 64 * 1024;

    /// <summary>User-Agent enviado nas requisicoes.</summary>
    public const string UserAgent = "TranalUninstaller/1.0 (+https://www.tranal.com.br)";

    private static readonly HttpClient Client = CreateClient();

    /// <summary>
    /// Verifica se existe uma versao mais recente no feed informado.
    /// Nunca lanca: devolve <see cref="UpdateCheckResult.Success"/> false com o
    /// motivo da falha.
    /// </summary>
    public static async Task<UpdateCheckResult> CheckAsync(string feedUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            return Failed(Loc.Get("Update.CheckFailed"));
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, feedUrl);
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

            using var response = await Client
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return Failed($"{Loc.Get("Update.CheckFailed")} (HTTP {(int)response.StatusCode})");

            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseFeed(body);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            // Tempo limite esgotado (ou rede interrompida).
            return Failed($"{Loc.Get("Update.ConnectionError")} ({ex.Message})");
        }
        catch (HttpRequestException ex)
        {
            return Failed($"{Loc.Get("Update.ConnectionError")} ({ex.Message})");
        }
        catch (Exception ex)
        {
            return Failed($"{Loc.Get("Update.CheckFailed")} ({ex.Message})");
        }
    }

    /// <summary>
    /// Baixa a atualizacao para <paramref name="destination"/>.
    ///
    /// O arquivo e baixado para um temporario ao lado do destino e movido para
    /// o lugar so quando a transferencia termina com sucesso (um download
    /// interrompido nunca substitui um pacote existente). Devolve true em caso
    /// de sucesso. O cancelamento e propagado como
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    public static async Task<bool> DownloadAsync(
        string url, string destination, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(destination)) return false;

        var temporary = string.Empty;

        try
        {
            var folder = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            temporary = destination + ".download";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

            using var response = await Client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode) return false;

            var total = response.Content.Headers.ContentLength;

            await using var source = await response.Content
                .ReadAsStreamAsync(ct)
                .ConfigureAwait(false);

            await using (var target = new FileStream(
                             temporary, FileMode.Create, FileAccess.Write, FileShare.None,
                             BufferSize, useAsync: true))
            {
                var buffer = new byte[BufferSize];
                long downloaded = 0;
                int read;

                while ((read = await source.ReadAsync(buffer.AsMemory(0, BufferSize), ct).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    downloaded += read;

                    progress?.Report(new DownloadProgress
                    {
                        BytesDownloaded = downloaded,
                        TotalBytes = total,
                        Fraction = total.HasValue && total.Value > 0
                            ? Math.Clamp((double)downloaded / total.Value, 0d, 1d)
                            : null,
                    });
                }
            }

            File.Move(temporary, destination, overwrite: true);
            temporary = string.Empty;
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao baixar a atualizacao de '{url}': {ex.Message}");
            return false;
        }
        finally
        {
            if (temporary.Length > 0)
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch { /* temporario orfao: sera limpo pelo sistema */ }
            }
        }
    }

    // ---------------------------------------------------------------------
    // Auxiliares
    // ---------------------------------------------------------------------

    /// <summary>Cria o HttpClient compartilhado (tempo limite de 15 segundos).</summary>
    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(TimeoutSeconds),
        };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
        return client;
    }

    /// <summary>
    /// Interpreta o conteudo do feed: JSON com os campos de versao/URL/notas ou
    /// texto simples com a versao.
    /// </summary>
    internal static UpdateCheckResult ParseFeed(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return Failed(Loc.Get("Update.CheckFailed"));

        var text = body.Trim();

        if (text.StartsWith('{') || text.StartsWith('['))
        {
            try
            {
                using var document = JsonDocument.Parse(text);
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    root = root[0];

                if (root.ValueKind != JsonValueKind.Object) return Failed(Loc.Get("Update.CheckFailed"));

                var version = ReadString(root, "latestVersion", "version", "tag_name", "latest_version");
                var url = ReadString(root, "downloadUrl", "download_url", "url", "browser_download_url", "assetUrl");
                var notes = ReadString(root, "releaseNotes", "release_notes", "notes", "body", "changelog");

                if (version.Length == 0 && url.Length == 0) return Failed(Loc.Get("Update.CheckFailed"));

                return new UpdateCheckResult
                {
                    Success = true,
                    LatestVersion = version.Length == 0 ? null : version,
                    DownloadUrl = url.Length == 0 ? null : url,
                    ReleaseNotes = notes.Length == 0 ? null : notes,
                };
            }
            catch (JsonException ex)
            {
                ToolLog.Error($"Feed de atualizacao com JSON invalido: {ex.Message}");
                return Failed(Loc.Get("Update.CheckFailed"));
            }
        }

        // Feed em texto simples: a primeira linha e a versao.
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var firstLine = lines.Length > 0 ? lines[0].Trim() : string.Empty;
        if (firstLine.Length == 0) return Failed(Loc.Get("Update.CheckFailed"));

        return new UpdateCheckResult
        {
            Success = true,
            LatestVersion = firstLine,
        };
    }

    /// <summary>Le o primeiro campo de texto encontrado entre os nomes informados.</summary>
    private static string ReadString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind != JsonValueKind.String) continue;

            var text = value.GetString();
            if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
        }

        return string.Empty;
    }

    /// <summary>Resultado de falha com o texto informado.</summary>
    private static UpdateCheckResult Failed(string message) => new()
    {
        Success = false,
        ErrorMessage = message,
    };
}
