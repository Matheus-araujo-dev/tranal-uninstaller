using System.Security.Cryptography;
using System.IO;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// O conteudo de um arquivo em forma de linhas hexadecimais.
/// </summary>
public sealed class HexDump
{
    /// <summary>O endereco do primeiro byte da linha.</summary>
    public long Offset { get; init; }

    /// <summary>A parte hexadecimal da linha.</summary>
    public string Hex { get; init; } = string.Empty;

    /// <summary>A parte em texto (o que da para ler).</summary>
    public string Text { get; init; } = string.Empty;
}

/// <summary>
/// O visor hexadecimal e a soma de verificacao de um arquivo.
///
/// O Total Uninstall tem um "visor hexadecimal" e mostra a "soma MD5 dos
/// arquivos novos" — a soma serve para comparar o arquivo com o original e
/// saber se ele foi alterado.
///
/// A REGRA mora aqui, em um lugar so: a leitura em hexadecimal e o calculo
/// do hash ficam aqui, e quem mostra consome o veredito.
/// </summary>
public static class HexViewer
{
    /// <summary>
    /// O numero de bytes por linha no visor.
    /// </summary>
    public const int BytesPerLine = 16;

    /// <summary>
    /// Monta o visor hexadecimal de um trecho do arquivo.
    /// </summary>
    /// <param name="path">O caminho do arquivo.</param>
    /// <param name="offset">O deslocamento inicial, em bytes.</param>
    /// <param name="length">Quantos bytes ler.</param>
    public static IReadOnlyList<HexDump> Dump(string? path, long offset = 0, int length = 4096)
    {
        var linhas = new List<HexDump>();

        var arquivo = PathUtil.Normalize(path);
        if (arquivo.Length == 0) return linhas;
        if (length <= 0) return linhas;

        try
        {
            using var stream = new FileStream(arquivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (offset > 0) stream.Seek(offset, SeekOrigin.Begin);

            var buffer = new byte[Math.Min(length, 1024 * 1024)];
            var lidos = stream.Read(buffer, 0, buffer.Length);

            var pos = 0;
            while (pos < lidos)
            {
                var tamanho = Math.Min(BytesPerLine, lidos - pos);

                var hex = new System.Text.StringBuilder();
                var texto = new System.Text.StringBuilder();

                for (var i = 0; i < BytesPerLine; i++)
                {
                    if (i >= tamanho)
                    {
                        hex.Append("   ");
                        texto.Append(' ');
                        continue;
                    }

                    var b = buffer[pos + i];
                    hex.Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)).Append(' ');
                    texto.Append(b >= 32 && b < 127 ? (char)b : '.');
                }

                linhas.Add(new HexDump
                {
                    Offset = offset + pos,
                    Hex = hex.ToString().TrimEnd(),
                    Text = texto.ToString().TrimEnd(),
                });

                pos += BytesPerLine;
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler o arquivo '{arquivo}': {ex.Message}");
        }

        return linhas;
    }

    /// <summary>
    /// A soma MD5 de um arquivo, em hexadecimal maiusculo.
    ///
    /// A leitura e feita em blocos: um instalador grande tem milhares de
    /// arquivos, e somar o arquivo inteiro de uma vez carrega tudo na
    /// memoria. O valor e o mesmo MD5 do arquivo.
    /// </summary>
    /// <param name="path">O caminho do arquivo.</param>
    public static string Md5Of(string? path)
    {
        var arquivo = PathUtil.Normalize(path);
        if (arquivo.Length == 0) return string.Empty;

        try
        {
            if (!File.Exists(arquivo)) return string.Empty;

            using var stream = new FileStream(arquivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var md5 = MD5.Create();

            var buffer = new byte[64 * 1024];
            int lidos;

            while ((lidos = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                md5.TransformBlock(buffer, 0, lidos, null, 0);
            }

            md5.TransformFinalBlock(buffer, 0, 0);

            return System.Convert.ToHexString(md5.Hash ?? Array.Empty<byte>());
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao calcular o MD5 de '{arquivo}': {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// A soma SHA256 de um arquivo, em hexadecimal maiusculo.
    /// </summary>
    /// <param name="path">O caminho do arquivo.</param>
    public static string Sha256Of(string? path)
    {
        var arquivo = PathUtil.Normalize(path);
        if (arquivo.Length == 0) return string.Empty;

        try
        {
            if (!File.Exists(arquivo)) return string.Empty;

            using var stream = new FileStream(arquivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sha = SHA256.Create();

            var hash = sha.ComputeHash(stream);
            return System.Convert.ToHexString(hash);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao calcular o SHA256 de '{arquivo}': {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Compara a soma de um arquivo com a soma informada.
    /// </summary>
    /// <param name="path">O caminho do arquivo.</param>
    /// <param name="expectedMd5">A soma esperada, em hexadecimal.</param>
    public static bool MatchesMd5(string? path, string? expectedMd5)
    {
        if (string.IsNullOrWhiteSpace(expectedMd5)) return false;

        var atual = Md5Of(path);
        if (atual.Length == 0) return false;

        return string.Equals(atual, expectedMd5.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
