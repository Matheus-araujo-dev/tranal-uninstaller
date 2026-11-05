using System.IO;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Travessia segura de arvores de pasta.
///
/// Um <em>reparse point</em> (junction, symbolic link, ponto de montagem) e um
/// atalho para outro lugar do disco. O .NET o trata como se fosse conteudo
/// da propria pasta: a enumeracao com
/// <see cref="SearchOption.AllDirectories"/> atravessa o atalho e traz os
/// arquivos do alvo como se fossem da pasta.
///
/// Medido nesta maquina (.NET 10, <c>mklink /J</c>):
///
/// <list type="bullet">
/// <item><c>Directory.EnumerateFiles(pasta, "*", AllDirectories)</c> lista os
/// arquivos do alvo do atalho — a travessia acontece.</item>
/// <item><c>Directory.Delete(pasta, true)</c> falha com
/// <c>UnauthorizedAccessException</c> ao chegar no atalho: nao apaga o alvo,
/// mas deixa a pasta pela metade e o erro nao diz nada sobre o atalho.</item>
/// <item><c>Directory.Delete(link, false)</c> apaga somente o link: o alvo
/// fica intacto.</item>
/// </list>
///
/// A regra do Tranal e uma so: <b>o motor nunca atravessa um atalho</b>. O
/// alvo do atalho esta fora da pasta que o usuario mandou remover, e apagar
/// arquivo alheio seria perda de dados nao pedida.
/// </summary>
public static class SafeTraversal
{
    /// <summary>
    /// True quando o caminho e um atalho para outro lugar do disco
    /// (junction, symbolic link ou ponto de montagem).
    ///
    /// Um caminho que nao pode ser lido (inexistente ou sem permissao) nao e
    /// tratado como atalho: nesse caso quem chama trata o item pelo erro
    /// original.
    /// </summary>
    public static bool IsReparsePoint(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            // Caminho inacessivel ou inexistente: nao da para afirmar que e
            // atalho, e o erro original aparece adiante na operacao.
            return false;
        }
    }

    /// <summary>
    /// Apaga uma pasta e todo o conteudo dela, sem atravessar atalhos.
    ///
    /// Atalho encontrado dentro da pasta e apagado como atalho: o alvo
    /// (<c>Directory.Delete(link, false)</c>) fica intacto.
    ///
    /// A raiz tambem e verificada: quando o proprio caminho e um atalho,
    /// so o link e apagado. Sem essa guarda, enumerar a raiz devolvia os
    /// arquivos do alvo — e a remocao apagava o conteudo de fora da pasta
    /// pedida, burlando a lista de exclusoes.
    /// </summary>
    /// <exception cref="IOException">Quando sobrou conteudo dentro da pasta.</exception>
    public static void DeleteDirectoryTree(string root)
    {
        var full = PathUtil.Normalize(root);
        if (full.Length == 0) throw new ArgumentException("Caminho vazio.", nameof(root));

        // A raiz e um atalho: apaga-se o link, nunca o alvo.
        if (IsReparsePoint(full))
        {
            Directory.Delete(full, recursive: false);
            return;
        }

        var failures = new List<string>();

        foreach (var file in EnumerateFiles(full))
        {
            try { File.Delete(file); }
            catch (Exception ex) { failures.Add($"{file}: {ex.Message}"); }
        }

        foreach (var dir in EnumerateDirectories(full))
        {
            if (IsReparsePoint(dir))
            {
                // Apaga-se o link, nunca o alvo.
                try { Directory.Delete(dir, recursive: false); }
                catch (Exception ex) { failures.Add($"{dir}: {ex.Message}"); }
                continue;
            }

            try { DeleteDirectoryTree(dir); }
            catch (Exception ex) { failures.Add($"{dir}: {ex.Message}"); }
        }

        try { Directory.Delete(full, recursive: false); }
        catch (Exception ex) { failures.Add($"{full}: {ex.Message}"); }

        if (failures.Count > 0)
            throw new IOException("Não foi possível remover: " + string.Join("; ", failures));
    }

    /// <summary>
    /// Soma o tamanho dos arquivos de uma pasta sem atravessar atalhos.
    ///
    /// O alvo de um atalho pode ser outra unidade inteira: conta-lo tornaria
    /// o numero do backup falso e a travessia poderia nunca terminar.
    /// </summary>
    public static long MeasureSize(string root)
    {
        var full = PathUtil.Normalize(root);
        if (full.Length == 0) return 0;

        var total = 0L;
        var pending = new Stack<string>();
        pending.Push(full);

        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly))
                {
                    try { total += new FileInfo(file).Length; } catch { /* o arquivo sumiu no meio da conta */ }
                }

                foreach (var sub in Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly))
                {
                    if (IsReparsePoint(sub)) continue;
                    pending.Push(sub);
                }
            }
            catch
            {
                // Pasta inacessivel: o tamanho dela nao entra na conta, e a
                // travessia continua pelas outras.
            }
        }

        return total;
    }

    /// <summary>
    /// Lista os arquivos de uma pasta e de suas subpastas, sem atravessar
    /// atalhos.
    ///
    /// O <see cref="SearchOption.AllDirectories"/> do .NET atravessa junction
    /// e symlink: os arquivos do alvo aparecem como se fossem da pasta. Um
    /// limpador que os removesse estaria apagando arquivos de fora do
    /// escopo — e de outra pessoa, se o alvo for o perfil de outro
    /// usuário.
    /// </summary>
    public static IEnumerable<string> EnumerateFilesDeep(string root, string pattern = "*")
    {
        var full = PathUtil.Normalize(root);
        if (full.Length == 0) return Array.Empty<string>();

        var result = new List<string>();
        var pending = new Stack<string>();
        pending.Push(full);

        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly))
                    result.Add(file);

                foreach (var sub in Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly))
                {
                    if (IsReparsePoint(sub)) continue;
                    pending.Push(sub);
                }
            }
            catch
            {
                // Pasta inacessivel: a travessia continua pelas outras.
            }
        }

        return result;
    }

    /// <summary>Arquivos de uma pasta, sem falhar quando ela esta inacessivel.</summary>
    private static IEnumerable<string> EnumerateFiles(string folder)
    {
        try { return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly).ToList(); }
        catch { return Array.Empty<string>(); }
    }

    /// <summary>Subpastas de uma pasta, sem falhar quando ela esta inacessivel.</summary>
    private static IEnumerable<string> EnumerateDirectories(string folder)
    {
        try { return Directory.EnumerateDirectories(folder, "*", SearchOption.TopDirectoryOnly).ToList(); }
        catch { return Array.Empty<string>(); }
    }
}
