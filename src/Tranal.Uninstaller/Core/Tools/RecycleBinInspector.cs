using System.IO;
using System.Runtime.InteropServices;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Um item que esta na Lixeira do Windows.
/// </summary>
public sealed class RecycleBinItem
{
    /// <summary>O caminho original do arquivo.</summary>
    public string OriginalPath { get; init; } = string.Empty;

    /// <summary>O nome do arquivo.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>O tamanho do arquivo, em bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>A data em que o arquivo foi enviado para a Lixeira.</summary>
    public DateTime DeletedAt { get; init; }

    /// <summary>A pasta em que o arquivo estava.</summary>
    public string Folder { get; init; } = string.Empty;
}

/// <summary>
/// Consulta a Lixeira do Windows.
///
/// O Soft Organizer tem um "consultar Lixeira" e o ashampoo tambem: a Lixeira
/// guarda o que o usuario apagou sem querer, e por isso e o primeiro lugar a
/// procurar quando um sumiu.
///
/// A REGRA mora aqui, em um lugar so: a leitura da Lixeira (a API do Windows
/// que lista os itens pelo caminho especial <c>$Recycle.Bin</c>) fica aqui, e
/// quem mostra consome o veredito.
/// </summary>
public static class RecycleBinInspector
{
    /// <summary>
    /// A raiz da Lixeira de todos os usuarios da maquina.
    /// </summary>
    private const string RecycleBinFolder = @"$Recycle.Bin";

    /// <summary>
    /// Os itens da Lixeira do usuario atual.
    /// </summary>
    public static IReadOnlyList<RecycleBinItem> List()
    {
        var itens = new List<RecycleBinItem>();

        try
        {
            var raiz = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "..",
                RecycleBinFolder);

            var caminhoReal = Path.GetFullPath(raiz);
            if (!Directory.Exists(caminhoReal)) return itens;

            // A Lixeira do usuario e a pasta cujo nome e o SID do usuario.
            var meuSid = CurrentUserSid();
            if (meuSid.Length == 0) return itens;

            var pastaUsuario = Path.Combine(caminhoReal, meuSid);
            if (!Directory.Exists(pastaUsuario)) return itens;

            foreach (var arquivo in Directory.EnumerateFiles(pastaUsuario, "*.$I", SearchOption.TopDirectoryOnly))
            {
                var item = ReadItem(arquivo);
                if (item is not null) itens.Add(item);
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler a Lixeira: {ex.Message}");
        }

        return itens.OrderByDescending(i => i.DeletedAt).ToList();
    }

    /// <summary>
    /// O total ocupado pela Lixeira do usuario.
    /// </summary>
    public static long TotalSize() => List().Sum(i => i.SizeBytes);

    /// <summary>
    /// Le um arquivo <c>$I</c> da Lixeira: ele traz o caminho original, o
    /// tamanho e a data em que o arquivo foi apagado.
    /// </summary>
    /// <param name="infoPath">O caminho do arquivo <c>$I</c>.</param>
    public static RecycleBinItem? ReadItem(string? infoPath)
    {
        var caminho = PathUtil.Normalize(infoPath);
        if (caminho.Length == 0) return null;

        try
        {
            if (!File.Exists(caminho)) return null;

            var dados = File.ReadAllBytes(caminho);
            if (dados.Length < 24) return null;

            // O formato e o do Windows 10/11: versao (8 bytes), tamanho
            // (8 bytes), data FILETIME (8 bytes) e o caminho em UTF-16.
            var versao = BitConverter.ToInt64(dados, 0);
            if (versao != 2) return null;

            var tamanho = BitConverter.ToInt64(dados, 8);
            var fileTime = BitConverter.ToInt64(dados, 16);

            var texto = dados.Length > 24
                ? System.Text.Encoding.Unicode.GetString(dados, 24, dados.Length - 24)
                : string.Empty;

            var original = texto.Split('\0')[0].Trim();
            if (original.Length == 0) return null;

            var apagadoEm = fileTime > 0 ? DateTime.FromFileTime(fileTime) : DateTime.MinValue;

            return new RecycleBinItem
            {
                OriginalPath = original,
                Name = Path.GetFileName(original),
                SizeBytes = tamanho,
                DeletedAt = apagadoEm,
                Folder = Path.GetDirectoryName(original) ?? string.Empty,
            };
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler o item da Lixeira '{caminho}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// O identificador de seguranca do usuario atual.
    /// </summary>
    private static string CurrentUserSid()
    {
        try
        {
            using var identidade = System.Security.Principal.WindowsIdentity.GetCurrent();
            return identidade.User?.Value ?? string.Empty;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler o SID do usuario: {ex.Message}");
            return string.Empty;
        }
    }
}
