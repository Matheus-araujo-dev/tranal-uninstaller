using System.IO;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Uma pasta problematica deixada pela atualizacao do Windows 11.
///
/// Corresponde ao item 15371 do Revo Uninstaller Pro: "If you have problems
/// updating to Windows 11, we recommend that you delete this folder %s. Would
/// you like to add it to the leftovers?".
///
/// A pasta e problematica porque o Windows 11 usa o espaco dela para preparar
/// a atualizacao; quando a atualizacao falha ou e cancelada, ela fica la,
/// ocupando gigabytes e podendo travar a proxima tentativa.
/// </summary>
public sealed class Windows11ProblemFolder
{
    /// <summary>Caminho completo da pasta.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Nome da pasta (ex.: "$WINDOWS.~BT").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Explicacao em portugues do que a pasta e.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Tamanho estimado do conteudo, em bytes.</summary>
    public long SizeBytes { get; init; }
}

/// <summary>
/// As pastas problematicas da atualizacao do Windows 11.
///
/// A regra mora aqui e em um lugar so: a lista dos nomes conhecidos, o que
/// cada nome significa e o teste "esta pasta e uma delas?".
///
/// So as pastas que existem de fato sao devolvidas — oferecer ao usuario a
/// limpeza de uma pasta inexistente seria ruido.
/// </summary>
public static class Windows11ProblemFolders
{
    /// <summary>
    /// Quantos arquivos sao contados para estimar o tamanho de uma pasta.
    /// Pasta de atualizacao tem gigabytes; a contagem e limitada para que a
    /// deteccao nunca vire uma varredura lenta.
    /// </summary>
    private const int SizeFileLimit = 5000;

    /// <summary>
    /// Os nomes conhecidos das pastas problematicas da atualizacao do
    /// Windows 11, com o texto que explica cada uma.
    /// </summary>
    private static readonly (string Name, string Description)[] Known =
    {
        ("$WINDOWS.~BT", "Arquivos de preparação da atualização do Windows 11"),
        ("$WINDOWS.~Q", "Restos da verificação de atualização do Windows 11"),
        ("$Windows.~WS", "Área de trabalho da atualização do Windows 11"),
        ("$SysReset", "Restos da redefinição do Windows (a atualização falhou)"),
        ("Windows.old", "Instalação anterior do Windows"),
    };

    /// <summary>
    /// Os nomes conhecidos, sem saber se existem na maquina.
    /// </summary>
    public static IReadOnlyList<string> KnownNames =>
        Known.Select(k => k.Name).ToList();

    /// <summary>
    /// As pastas problematicas que existem de fato nesta maquina.
    /// </summary>
    public static IReadOnlyList<Windows11ProblemFolder> KnownFolders()
    {
        var result = new List<Windows11ProblemFolder>();
        var root = SystemDriveRoot();
        if (root.Length == 0) return result;

        foreach (var (name, description) in Known)
        {
            var path = PathUtil.Normalize(Path.Combine(root, name));
            if (path.Length == 0) continue;
            if (!Directory.Exists(path)) continue;

            result.Add(new Windows11ProblemFolder
            {
                Path = path,
                Name = name,
                Description = description,
                SizeBytes = MeasureSize(path),
            });
        }

        return result;
    }

    /// <summary>
    /// Os caminhos das pastas problematicas que existem de fato.
    /// </summary>
    public static IReadOnlyList<string> KnownFolderPaths() =>
        KnownFolders().Select(f => f.Path).ToList();

    /// <summary>
    /// True quando o caminho informado e uma das pastas problematicas da
    /// atualizacao do Windows 11 (pelo nome da pasta, seja qual for a unidade
    /// em que ela estiver).
    /// </summary>
    public static bool IsKnownProblemFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        string name;
        try
        {
            name = Path.GetFileName(PathUtil.Normalize(path).TrimEnd('\\'));
        }
        catch
        {
            return false;
        }

        if (name.Length == 0) return false;

        return Known.Any(k => string.Equals(k.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A explicacao de uma pasta conhecida; vazio quando o caminho nao e uma
    /// delas.
    /// </summary>
    public static string Describe(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        string name;
        try
        {
            name = Path.GetFileName(PathUtil.Normalize(path).TrimEnd('\\'));
        }
        catch
        {
            return string.Empty;
        }

        foreach (var known in Known)
        {
            if (string.Equals(known.Name, name, StringComparison.OrdinalIgnoreCase))
                return known.Description;
        }

        return string.Empty;
    }

    /// <summary>
    /// A raiz da unidade do Windows (em geral "C:\"): e onde essas pastas
    /// nascem.
    /// </summary>
    private static string SystemDriveRoot()
    {
        try
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var root = Path.GetPathRoot(windows);
            if (!string.IsNullOrEmpty(root)) return root;

            root = Path.GetPathRoot(Environment.SystemDirectory);
            if (!string.IsNullOrEmpty(root)) return root;
        }
        catch
        {
            // Sem a raiz do Windows: nada aoffer.
        }

        return string.Empty;
    }

    /// <summary>
    /// Tamanho estimado do conteudo, com contagem limitada de arquivos.
    /// </summary>
    private static long MeasureSize(string path)
    {
        try
        {
            long total = 0;
            var count = 0;

            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(file).Length; }
                catch { /* arquivo sumiu no meio da varredura */ }

                count++;
                if (count >= SizeFileLimit) break;
            }

            return total;
        }
        catch
        {
            return 0;
        }
    }
}
