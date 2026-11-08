using System.IO;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// O resultado da limpeza de um fragmento recuperado pelo CHKDSK.
/// </summary>
public enum ChkdskCleanupStatus
{
    /// <summary>O fragmento existia e foi removido.</summary>
    Removed,

    /// <summary>O fragmento ja nao existia.</summary>
    AlreadyAbsent,

    /// <summary>Nao foi possivel remover.</summary>
    Failed,
}

/// <summary>
/// O resultado da limpeza de um fragmento recuperado pelo CHKDSK.
/// </summary>
public sealed class ChkdskCleanupResult
{
    /// <summary>Caminho do fragmento.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>O estado final da operacao.</summary>
    public ChkdskCleanupStatus Status { get; init; }

    /// <summary>O que aconteceu, em portugues.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Os fragmentos de arquivo recuperados pelo CHKDSK (o "Check Disk" do
/// Windows).
///
/// Quando o CHKDSK recupera um arquivo corrompido, ele grava o conteudo
/// recuperado em <c>FILE0000.CHK</c>, <c>FILE0001.CHK</c>… na RAIZ da unidade
/// onde a varredura foi feita. Esses arquivos sao o rastro mais direto que
/// existe de que o disco teve problema — e o Revo Uninstaller Pro os limpa no
/// item 725 do Windows Tracks Cleaner ("Delete Chkdsk (Check Disk) recovered
/// file fragments").
///
/// A regra mora aqui e em um lugar so: quais arquivos sao fragmentos, e como
/// eles sao removidos. O Tracks Cleaner so consome.
/// </summary>
public static class ChkdskFragmentCleaner
{
    /// <summary>
    /// A categoria usada pelo Tracks Cleaner — o prefixo "windows-" mantem o
    /// item dentro do limpador do Windows, como no Revo.
    /// </summary>
    public const string CategoryId = "windows-chkdsk";

    /// <summary>
    /// O padrao de nome dos fragmentos recuperados pelo CHKDSK.
    /// </summary>
    public const string FragmentPattern = "FILE*.CHK";

    /// <summary>
    /// Os fragmentos do CHKDSK na raiz de todas as unidades prontas.
    /// </summary>
    public static IReadOnlyList<string> FindFragments()
    {
        var result = new List<string>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;
                result.AddRange(FindFragmentsIn(drive.RootDirectory.FullName));
            }
            catch
            {
                // Unidade inacessivel: a varredura segue pelas outras.
            }
        }

        return result;
    }

    /// <summary>
    /// Os fragmentos do CHKDSK na raiz de uma unidade.
    /// </summary>
    /// <param name="driveRoot">A raiz da unidade (ex.: "C:\").</param>
    public static IReadOnlyList<string> FindFragmentsIn(string driveRoot)
    {
        var result = new List<string>();

        var root = PathUtil.Normalize(driveRoot);
        if (root.Length == 0) return result;

        try
        {
            // A raiz da unidade e o unico lugar onde o CHKDSK deixa os
            // fragmentos: eles nao vao para subpasta nenhuma.
            if (!Directory.Exists(root)) return result;

            foreach (var file in Directory.EnumerateFiles(root, FragmentPattern, SearchOption.TopDirectoryOnly))
            {
                if (!IsFragmentFileName(file)) continue;
                result.Add(PathUtil.Normalize(file));
            }
        }
        catch
        {
            // Unidade sem permissao: nada a listar.
        }

        return result;
    }

    /// <summary>
    /// True quando o nome do arquivo e o de um fragmento do CHKDSK
    /// ("FILE0000.CHK").
    /// </summary>
    public static bool IsFragmentFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        string name;
        try { name = Path.GetFileName(fileName); }
        catch { return false; }

        if (name.Length < 6) return false; // "F.CHK" nao e um fragmento
        if (!name.StartsWith("FILE", StringComparison.OrdinalIgnoreCase)) return false;
        if (!name.EndsWith(".CHK", StringComparison.OrdinalIgnoreCase)) return false;

        return true;
    }

    /// <summary>
    /// Limpa um fragmento: envia para a Lixeira, como o resto do projeto faz.
    /// </summary>
    /// <param name="fragmentPath">O caminho do fragmento.</param>
    public static ChkdskCleanupResult Clean(string fragmentPath)
    {
        var full = PathUtil.Normalize(fragmentPath);
        if (full.Length == 0)
        {
            return new ChkdskCleanupResult
            {
                Path = full,
                Status = ChkdskCleanupStatus.Failed,
                Message = "caminho inválido",
            };
        }

        // Regra de seguranca: so um fragmento do CHKDSK e removido por aqui.
        if (!IsFragmentFileName(full))
        {
            return new ChkdskCleanupResult
            {
                Path = full,
                Status = ChkdskCleanupStatus.Failed,
                Message = "não é um fragmento do CHKDSK",
            };
        }

        if (!File.Exists(full))
        {
            return new ChkdskCleanupResult
            {
                Path = full,
                Status = ChkdskCleanupStatus.AlreadyAbsent,
                Message = "já não existia",
            };
        }

        // A raiz de uma unidade jamais e alvo.
        try
        {
            var root = Path.GetPathRoot(full);
            if (!string.IsNullOrEmpty(root) && full.TrimEnd('\\').Length <= root.TrimEnd('\\').Length)
            {
                return new ChkdskCleanupResult
                {
                    Path = full,
                    Status = ChkdskCleanupStatus.Failed,
                    Message = "a raiz da unidade nunca é removida",
                };
            }
        }
        catch
        {
            return new ChkdskCleanupResult
            {
                Path = full,
                Status = ChkdskCleanupStatus.Failed,
                Message = "caminho inválido",
            };
        }

        // A Lixeira primeiro, como o resto do projeto faz.
        if (RecycleBin.Send(full))
        {
            return new ChkdskCleanupResult
            {
                Path = full,
                Status = ChkdskCleanupStatus.Removed,
                Message = "enviado para a Lixeira",
            };
        }

        // Sem Lixeira no volume, a shell apaga em definitivo — e "enviado para
        // a Lixeira" seria mentira.
        if (!File.Exists(full))
        {
            return new ChkdskCleanupResult
            {
                Path = full,
                Status = ChkdskCleanupStatus.Removed,
                Message = "removido em definitivo",
            };
        }

        try
        {
            File.Delete(full);
            return new ChkdskCleanupResult
            {
                Path = full,
                Status = ChkdskCleanupStatus.Removed,
                Message = "removido em definitivo",
            };
        }
        catch (Exception ex)
        {
            return new ChkdskCleanupResult
            {
                Path = full,
                Status = ChkdskCleanupStatus.Failed,
                Message = ex.Message,
            };
        }
    }

    /// <summary>
    /// Limpa todos os fragmentos encontrados nas unidades.
    /// </summary>
    public static IReadOnlyList<ChkdskCleanupResult> CleanAll()
    {
        var results = new List<ChkdskCleanupResult>();

        foreach (var fragment in FindFragments())
            results.Add(Clean(fragment));

        return results;
    }
}
