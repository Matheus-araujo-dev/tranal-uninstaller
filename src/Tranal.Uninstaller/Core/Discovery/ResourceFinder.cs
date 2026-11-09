using System.IO;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Um achado do localizador de arquivos e chaves de registro.
/// </summary>
public sealed class FoundItem
{
    /// <summary>"Arquivo", "Pasta" ou "Registro".</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>O caminho completo do achado.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>O nome do achado.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>O tamanho do arquivo, quando ele existe.</summary>
    public long SizeBytes { get; init; }

    /// <summary>A data de gravacao do arquivo, quando ela existe.</summary>
    public DateTime? ModifiedAt { get; init; }
}

/// <summary>
/// O localizador de arquivos e chaves de registro.
///
/// O HiBit tem um "Localizador de Arquivos e Registros": da para digitar um
/// nome e ver onde o Windows guarda aquele arquivo ou aquela chave. E a forma
/// mais direta de responder "onde foi parar aquele XML que o instalador
/// criou?".
///
/// A REGRA mora aqui, em um lugar so: a busca por nome (com curinga) e a
/// varredura do registro ficam aqui. Quem mostra consome o veredito.
///
/// A busca e sempre dentro de uma pasta declarada pelo usuario: varrer o
/// disco inteiro sem pedido seria lento e mostraria coisa que o usuario nao
/// pediu.
/// </summary>
public static class ResourceFinder
{
    /// <summary>
    /// Procura arquivos e pastas pelo nome, dentro da pasta informada.
    ///
    /// O nome aceita curinga do Windows ("*.log") e barra invertida como
    /// separador de pasta.
    /// </summary>
    /// <param name="root">A pasta onde procurar.</param>
    /// <param name="pattern">O nome (ou curinga) a procurar.</param>
    /// <param name="recursive">True para descer nas subpastas.</param>
    /// <param name="limit">O numero maximo de achados.</param>
    public static IReadOnlyList<FoundItem> FindFiles(
        string? root,
        string? pattern,
        bool recursive = true,
        int limit = 500)
    {
        var achados = new List<FoundItem>();

        var pasta = PathUtil.Normalize(root);
        if (pasta.Length == 0) return achados;
        if (!Directory.Exists(pasta)) return achados;

        var busca = string.IsNullOrWhiteSpace(pattern) ? "*" : pattern.Trim();

        try
        {
            var opcao = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            foreach (var arquivo in Directory.EnumerateFiles(pasta, busca, opcao))
            {
                var info = SafeInfo(arquivo);
                achados.Add(new FoundItem
                {
                    Kind = "Arquivo",
                    Path = PathUtil.Normalize(arquivo),
                    Name = Path.GetFileName(arquivo),
                    SizeBytes = info?.Length ?? 0,
                    ModifiedAt = info?.LastWriteTime,
                });

                if (achados.Count >= limit) return achados;
            }

            foreach (var subpasta in Directory.EnumerateDirectories(pasta, busca, opcao))
            {
                achados.Add(new FoundItem
                {
                    Kind = "Pasta",
                    Path = PathUtil.Normalize(subpasta),
                    Name = Path.GetFileName(subpasta),
                });

                if (achados.Count >= limit) return achados;
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao procurar '{busca}' em '{pasta}': {ex.Message}");
        }

        return achados;
    }

    /// <summary>
    /// Procura chaves de registro pelo nome, dentro da raiz informada.
    /// </summary>
    /// <param name="root">A raiz da busca (ex.: "HKEY_LOCAL_MACHINE\SOFTWARE").</param>
    /// <param name="pattern">O nome (ou curinga) a procurar.</param>
    /// <param name="limit">O numero maximo de achados.</param>
    public static IReadOnlyList<FoundItem> FindRegistryKeys(string? root, string? pattern, int limit = 500)
    {
        var achados = new List<FoundItem>();

        var raiz = PathUtil.Normalize(root);
        if (raiz.Length == 0) return achados;

        var busca = string.IsNullOrWhiteSpace(pattern) ? "*" : pattern.Trim();

        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(raiz);
            using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, Microsoft.Win32.RegistryView.Default);
            using var key = baseKey.OpenSubKey(sub);
            if (key is null) return achados;

            foreach (var nome in key.GetSubKeyNames())
            {
                if (!Casa(nome, busca)) continue;

                achados.Add(new FoundItem
                {
                    Kind = "Registro",
                    Path = raiz.TrimEnd('\\') + "\\" + nome,
                    Name = nome,
                });

                if (achados.Count >= limit) return achados;
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao procurar a chave '{busca}' em '{raiz}': {ex.Message}");
        }

        return achados;
    }

    /// <summary>
    /// True quando o nome casa com o padrao (com curinga do Windows).
    /// </summary>
    /// <param name="name">O nome testado.</param>
    /// <param name="pattern">O padrao.</param>
    public static bool Casa(string? name, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (string.IsNullOrWhiteSpace(pattern)) return true;
        if (pattern == "*") return true;

        if (pattern.IndexOf('*') < 0 && pattern.IndexOf('?') < 0)
        {
            return name.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(
                name, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao comparar o nome com o padrao: {ex.Message}");
            return false;
        }
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
