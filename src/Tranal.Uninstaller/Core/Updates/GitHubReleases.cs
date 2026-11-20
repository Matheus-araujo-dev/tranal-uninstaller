using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tranal.Uninstaller.Core.Updates;

/// <summary>
/// A leitura da lista de releases do GitHub (a resposta de
/// <c>GET /repos/{dono}/{repo}/releases</c>).
///
/// E a regra da DEC-APPS-DISTRIBUICAO-001, em um lugar so: a versao
/// publicada e a <b>maior tag <c>vX.Y.Z</c></b> entre as releases, e nao a
/// primeira da lista nem a release "latest" (que e uma tag movel, sem numero).
/// Tag que nao e versao (<c>latest</c>, <c>v1.0-beta</c>), rascunho e
/// pre-release sao ignorados. O instalador e o asset com o nome do Setup, e o
/// tamanho dele viaja junto para o download ser conferido.
///
/// A funcao e pura (texto entra, resultado sai): e o que a suite testa sem
/// rede.
/// </summary>
public static class GitHubReleases
{
    /// <summary>A forma de uma tag de versao: <c>v</c> + tres numeros.</summary>
    private static readonly Regex TagDeVersao = new(@"^v(\d+)\.(\d+)\.(\d+)$", RegexOptions.CultureInvariant);

    /// <summary>
    /// A resposta parece a lista de releases do GitHub? (lista de objetos com
    /// <c>tag_name</c>). Quem le um feed generico usa isto para escolher a
    /// regra certa.
    /// </summary>
    /// <param name="root">A raiz do JSON.</param>
    public static bool IsReleaseList(JsonElement root) =>
        root.ValueKind == JsonValueKind.Array
        && root.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("tag_name", out _));

    /// <summary>
    /// A versao de uma tag <c>vX.Y.Z</c>, ou null quando a tag nao e versao.
    /// </summary>
    /// <param name="tag">O nome da tag.</param>
    public static Version? VersionOfTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;

        var m = TagDeVersao.Match(tag.Trim());
        if (!m.Success) return null;

        try
        {
            return new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
        }
        catch (OverflowException)
        {
            // Numero que nao cabe em int nao e versao do Tranal.
            return null;
        }
    }

    /// <summary>
    /// Escolhe a release com a maior tag de versao e o instalador dela.
    /// </summary>
    /// <param name="root">A lista de releases (JSON).</param>
    /// <param name="setupAssetName">O nome do arquivo do instalador.</param>
    /// <returns>
    /// O resultado com a versao (sem o "v"), o endereco e o tamanho do
    /// instalador. Sem release versionada, devolve falha.
    /// </returns>
    public static UpdateCheckResult PickLatest(JsonElement root, string setupAssetName)
    {
        Version? maior = null;
        JsonElement escolhida = default;

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var release in root.EnumerateArray())
            {
                if (release.ValueKind != JsonValueKind.Object) continue;
                if (Flag(release, "draft") || Flag(release, "prerelease")) continue;

                var tag = release.TryGetProperty("tag_name", out var t) && t.ValueKind == JsonValueKind.String
                    ? t.GetString()
                    : null;
                var versao = VersionOfTag(tag);
                if (versao is null) continue;

                if (maior is null || versao > maior)
                {
                    maior = versao;
                    escolhida = release;
                }
            }
        }

        if (maior is null)
        {
            return new UpdateCheckResult
            {
                Success = false,
                ErrorMessage = "Nenhuma release com tag de versao (vX.Y.Z).",
            };
        }

        string? url = null;
        long? tamanho = null;

        if (escolhida.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.ValueKind != JsonValueKind.Object) continue;
                if (!asset.TryGetProperty("name", out var nome) || nome.ValueKind != JsonValueKind.String) continue;
                if (!string.Equals(nome.GetString(), setupAssetName, StringComparison.OrdinalIgnoreCase)) continue;

                if (asset.TryGetProperty("browser_download_url", out var u) && u.ValueKind == JsonValueKind.String)
                    url = u.GetString();
                if (asset.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.Number && s.TryGetInt64(out var n) && n > 0)
                    tamanho = n;
                break;
            }
        }

        var notas = escolhida.TryGetProperty("html_url", out var h) && h.ValueKind == JsonValueKind.String
            ? h.GetString()
            : null;

        return new UpdateCheckResult
        {
            Success = true,
            LatestVersion = maior.ToString(3),
            DownloadUrl = string.IsNullOrWhiteSpace(url) ? null : url,
            ExpectedSize = tamanho,
            ReleaseNotes = string.IsNullOrWhiteSpace(notas) ? null : notas,
        };
    }

    /// <summary>Le um campo booleano (ausente = false).</summary>
    private static bool Flag(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
}
