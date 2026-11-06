using System.IO;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Opcoes da varredura de drives (item #13 do backlog).
///
/// A varredura e **lenta** e por isso e **opt-in**: ela nunca acontece no
/// arranque do aplicativo, so quando o usuario pede. Os limites sao
/// explicitos e configuraveis para que a varredura nunca vire uma volta
/// infinita pelo disco.
/// </summary>
public sealed class DriveScanOptions
{
    /// <summary>Pastas raiz da varredura (o drive inteiro, por exemplo).</summary>
    public IReadOnlyList<string> Roots { get; init; } = Array.Empty<string>();

    /// <summary>Profundidade maxima de pastas visitadas (a raiz e o nivel 0).</summary>
    public int MaxDepth { get; init; } = 4;

    /// <summary>
    /// Profundidade maxima dentro de <c>Program Files</c>. A pasta de
    /// programas de um drive inteiro nao e varrida recursivamente sem
    /// limite — este e o limite dela.
    /// </summary>
    public int ProgramFilesMaxDepth { get; init; } = 3;

    /// <summary>Quantidade maxima de pastas visitadas na varredura inteira.</summary>
    public int MaxDirectories { get; init; } = 20000;

    /// <summary>Quantidade maxima de arquivos lidos por pasta.</summary>
    public int MaxFilesPerDirectory { get; init; } = 4000;

    /// <summary>
    /// Nomes de pasta que a varredura pula na raiz do drive. Sao pastas do
    /// sistema, grandes e sem programa de usuario dentro.
    /// </summary>
    public IReadOnlyList<string> ExcludedFolderNames { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Opcoes padrao: as unidades fixas prontas, com os limites acima.
    /// Nenhuma varredura acontece aqui — so a preparacao.
    /// </summary>
    public static DriveScanOptions Default()
    {
        var roots = new List<string>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;
                if (drive.DriveType != DriveType.Fixed) continue;
                roots.Add(drive.RootDirectory.FullName);
            }
            catch
            {
                // Unidade inacessivel: a varredura segue pelas outras.
            }
        }

        return new DriveScanOptions
        {
            Roots = roots,
            ExcludedFolderNames = new[]
            {
                "$Recycle.Bin",
                "System Volume Information",
                "Windows",
                "Recovery",
                "$WinREAgent",
                "Documents and Settings",
            },
        };
    }
}

/// <summary>
/// Um executavel que parece desinstalador e que nao esta registrado no
/// Painel de Controle.
/// </summary>
public sealed class OrphanUninstaller
{
    /// <summary>Caminho completo do executavel.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Pasta onde o executavel esta.</summary>
    public string FolderPath { get; init; } = string.Empty;

    /// <summary>Nome do arquivo.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Profundidade da pasta em relacao a raiz da varredura.</summary>
    public int Depth { get; init; }
}

/// <summary>
/// Resultado de uma varredura de drives.
/// </summary>
public sealed class DriveScanResult
{
    /// <summary>Executaveis encontrados que nao estao registrados.</summary>
    public IReadOnlyList<OrphanUninstaller> Found { get; init; } = Array.Empty<OrphanUninstaller>();

    /// <summary>True quando a varredura parou por causa de um limite.</summary>
    public bool LimitReached { get; init; }

    /// <summary>Quantidade de pastas visitadas.</summary>
    public int DirectoriesVisited { get; init; }

    /// <summary>Quantidade de arquivos lidos.</summary>
    public int FilesExamined { get; init; }
}

/// <summary>
/// Indice dos desinstaladores que o Painel de Controle ja conhece.
///
/// A regra de "orfao" e esta: o executavel nao esta na lista de comandos de
/// desinstalacao registrados **e** nao esta dentro da pasta de instalacao de
/// um programa registrado. Um programa instalado de verdade nunca entra na
/// lista de orfaos.
/// </summary>
public sealed class RegisteredUninstallerIndex
{
    /// <summary>Caminhos dos executaveis de desinstalacao registrados.</summary>
    public HashSet<string> Executables { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Pastas de instalacao registradas.</summary>
    public HashSet<string> Folders { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True quando o caminho pertence a um programa registrado.
    /// </summary>
    public bool IsRegistered(string filePath)
    {
        var full = PathUtil.Normalize(filePath);
        if (full.Length == 0) return false;

        if (Executables.Contains(full)) return true;

        foreach (var folder in Folders)
        {
            if (folder.Length == 0) continue;

            // O casamento e por prefixo com separador: "C:\App" protege
            // "C:\App\app.exe" e nao protege "C:\App2".
            if (full.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Monta o indice a partir da lista de programas instalados.
    /// </summary>
    public static RegisteredUninstallerIndex From(IEnumerable<InstalledProgram> programs)
    {
        var index = new RegisteredUninstallerIndex();
        if (programs is null) return index;

        foreach (var program in programs)
        {
            if (program is null) continue;

            var executable = ExtractExecutable(program.UninstallString);
            if (executable.Length > 0)
                index.Executables.Add(PathUtil.Normalize(executable));

            var location = PathUtil.Normalize(program.InstallLocation);
            if (location.Length > 0) index.Folders.Add(location);
        }

        return index;
    }

    /// <summary>
    /// Extrai o executavel do comando de desinstalacao (o primeiro token que
    /// termina em <c>.exe</c>, com ou sem aspas).
    /// </summary>
    private static string ExtractExecutable(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return string.Empty;

        var text = command.Trim();

        if (text.StartsWith("\"", StringComparison.Ordinal))
        {
            var end = text.IndexOf('"', 1);
            if (end > 1) return text[1..end];
        }

        var space = text.IndexOf(' ');
        var candidate = space > 0 ? text[..space] : text;

        return candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? candidate.Trim('"')
            : string.Empty;
    }
}

/// <summary>
/// Varredura dos drives-fixedos atrás de desinstaladores que o Painel de
/// Controle nao conhece (item #13 do backlog).
///
/// Regras duras, todas aplicadas aqui:
///
/// <list type="bullet">
/// <item>e **opt-in**: nada disso acontece no arranque;</item>
/// <item>respeita o <see cref="SafeTraversal"/>: atalho (junction) nunca e
/// atravessado;</item>
/// <item>tem **limite explicito**: profundidade e quantidade de pastas, com
/// limite proprio para <c>Program Files</c>;</item>
/// <item>so <c>unins*.exe</c> e <c>uninstall*.exe</c> contam como
/// desinstalador.</item>
/// </list>
/// </summary>
public static class DriveScanner
{
    /// <summary>
    /// Varre as pastas das opcoes e devolve os executaveis que parecem
    /// desinstalador e nao estao registrados.
    /// </summary>
    public static DriveScanResult Scan(DriveScanOptions options, RegisteredUninstallerIndex? registered = null)
    {
        var found = new List<OrphanUninstaller>();
        if (options is null) return new DriveScanResult();

        var index = registered ?? new RegisteredUninstallerIndex();
        var visited = 0;
        var examined = 0;
        var limitReached = false;

        foreach (var root in options.Roots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;

            var queue = new Queue<(string Path, int Depth)>();
            queue.Enqueue((PathUtil.Normalize(root), 0));

            while (queue.Count > 0)
            {
                if (visited >= options.MaxDirectories)
                {
                    limitReached = true;
                    break;
                }

                var (folder, depth) = queue.Dequeue();
                visited++;

                // A pasta de programas tem limite proprio, menor que o geral.
                var limit = IsProgramFilesFolder(folder)
                    ? options.ProgramFilesMaxDepth
                    : options.MaxDepth;

                examined += InspectFolder(folder, depth, options, index, found, ref limitReached);

                if (depth + 1 > limit) continue;

                foreach (var child in EnumerateSubFolders(folder, options))
                {
                    // Atalho (junction, link simbolico, ponto de montagem):
                    // o motor nunca o atravessa.
                    if (SafeTraversal.IsReparsePoint(child)) continue;

                    if (depth == 0 && IsExcluded(child, options)) continue;

                    queue.Enqueue((child, depth + 1));
                }
            }

            if (limitReached) break;
        }

        return new DriveScanResult
        {
            Found = found,
            LimitReached = limitReached,
            DirectoriesVisited = visited,
            FilesExamined = examined,
        };
    }

    /// <summary>
    /// True quando o nome do arquivo parece o desinstalador de um programa.
    /// </summary>
    public static bool IsUninstallerFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        return fileName.StartsWith("unins", StringComparison.OrdinalIgnoreCase)
            && fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Le os arquivos de uma pasta e devolve quantos foram lidos.
    /// </summary>
    private static int InspectFolder(
        string folder,
        int depth,
        DriveScanOptions options,
        RegisteredUninstallerIndex index,
        List<OrphanUninstaller> found,
        ref bool limitReached)
    {
        var examined = 0;

        string[] files;
        try
        {
            files = Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            // Pasta inacessivel: a varredura continua pelas outras.
            return 0;
        }

        var count = 0;
        foreach (var file in files)
        {
            if (count >= options.MaxFilesPerDirectory)
            {
                limitReached = true;
                break;
            }

            count++;
            examined++;

            var name = Path.GetFileName(file);
            if (!IsUninstallerFileName(name)) continue;

            // Ja registrado no Painel de Controle: nao e orfao.
            if (index.IsRegistered(file)) continue;

            found.Add(new OrphanUninstaller
            {
                FilePath = file,
                FolderPath = folder,
                FileName = name,
                Depth = depth + 1,
            });
        }

        return examined;
    }

    /// <summary>
    /// As subpastas de uma pasta, sem falhar quando ela esta inacessivel.
    /// </summary>
    private static IEnumerable<string> EnumerateSubFolders(string folder, DriveScanOptions options)
    {
        try
        {
            var names = Directory.GetDirectories(folder, "*", SearchOption.TopDirectoryOnly);
            return names.Length > options.MaxFilesPerDirectory
                ? names.Take(options.MaxFilesPerDirectory)
                : names;
        }
        catch
        {
            // Pasta inacessivel: a varredura continua pelas outras.
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// True quando a pasta e a de programas do Windows.
    /// </summary>
    private static bool IsProgramFilesFolder(string folder)
    {
        var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));

        return name.Equals("Program Files", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Program Files (x86)", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True quando a pasta esta na lista de exclusao da raiz do drive.
    /// </summary>
    private static bool IsExcluded(string folder, DriveScanOptions options)
    {
        var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));

        foreach (var excluded in options.ExcludedFolderNames)
        {
            if (name.Equals(excluded, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }
}
