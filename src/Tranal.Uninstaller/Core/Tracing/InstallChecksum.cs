using System.IO;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Tracing;

/// <summary>
/// A soma de verificacao de um arquivo instalado.
/// </summary>
public sealed class FileChecksum
{
    /// <summary>O caminho do arquivo.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>O tamanho do arquivo, em bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>A soma MD5, em hexadecimal.</summary>
    public string Md5 { get; init; } = string.Empty;

    /// <summary>A soma SHA256, em hexadecimal.</summary>
    public string Sha256 { get; init; } = string.Empty;

    /// <summary>A data de gravacao do arquivo.</summary>
    public DateTime ModifiedAt { get; init; }
}

/// <summary>
/// Calcula a soma de verificacao dos arquivos de um programa instalado.
///
/// O HiBit tem "verificacao de integridade de arquivo" e o Total Uninstall
/// mostra a "soma MD5 dos arquivos novos" — a soma serve para conferir se o
/// arquivo que esta no disco e o mesmo que o instalador trouxe.
///
/// A REGRA mora aqui, em um lugar so: o calculo do hash fica aqui (o mesmo
/// leitor de metadados do executavel calcula o hash do arquivo inteiro) e
/// quem compara consome o veredito.
///
/// O calculo e feito em partes: um instalador grande tem milhares de
/// arquivos, e a API do Windows so entrega o hash do arquivo fechado.
/// </summary>
public static class InstallChecksum
{
    /// <summary>
    /// A soma MD5 de um arquivo.
    /// </summary>
    /// <param name="path">O caminho do arquivo.</param>
    public static string Md5Of(string? path) => Tools.HexViewer.Md5Of(path);

    /// <summary>
    /// A soma SHA256 de um arquivo.
    /// </summary>
    /// <param name="path">O caminho do arquivo.</param>
    public static string Sha256Of(string? path) => Tools.HexViewer.Sha256Of(path);

    /// <summary>
    /// A soma de verificacao de um arquivo.
    /// </summary>
    /// <param name="path">O caminho do arquivo.</param>
    public static FileChecksum? Of(string? path)
    {
        var arquivo = PathUtil.Normalize(path);
        if (arquivo.Length == 0) return null;

        try
        {
            if (!File.Exists(arquivo)) return null;

            var info = new FileInfo(arquivo);

            return new FileChecksum
            {
                Path = arquivo,
                SizeBytes = info.Length,
                Md5 = Md5Of(arquivo),
                Sha256 = Sha256Of(arquivo),
                ModifiedAt = info.LastWriteTime,
            };
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao calcular a soma de '{arquivo}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// As somas de verificacao dos arquivos de um programa.
    /// </summary>
    /// <param name="program">O programa instalado.</param>
    /// <param name="limit">O numero maximo de arquivos.</param>
    public static IReadOnlyList<FileChecksum> OfProgram(InstalledProgram? program, int limit = 2000)
    {
        var resultado = new List<FileChecksum>();
        if (program is null) return resultado;

        var pasta = program.InstallLocation;
        if (string.IsNullOrWhiteSpace(pasta)) return resultado;

        try
        {
            if (!Directory.Exists(pasta)) return resultado;

            foreach (var arquivo in Directory.EnumerateFiles(pasta, "*", SearchOption.AllDirectories))
            {
                var soma = Of(arquivo);
                if (soma is not null) resultado.Add(soma);

                if (resultado.Count >= limit) break;
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao somar os arquivos de '{pasta}': {ex.Message}");
        }

        return resultado;
    }

    /// <summary>
    /// Confere se o arquivo no disco ainda tem a soma esperada.
    /// </summary>
    /// <param name="path">O caminho do arquivo.</param>
    /// <param name="expectedMd5">A soma MD5 esperada.</param>
    public static bool Verify(string? path, string? expectedMd5) =>
        Tools.HexViewer.MatchesMd5(path, expectedMd5);
}
