using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Um aplicativo instalado em pasta de usuario (fora do "Program Files").
/// </summary>
/// <param name="Name">O nome do aplicativo.</param>
/// <param name="Path">A pasta onde ele esta instalado.</param>
/// <param name="Root">A pasta raiz que foi varrida.</param>
/// <param name="HasUninstaller">True quando a pasta tem um desinstalador.</param>
/// <param name="UninstallerPath">O caminho do desinstalador, quando existe.</param>
public sealed record UserInstallApp(
    string Name,
    string Path,
    string Root,
    bool HasUninstaller,
    string UninstallerPath);

/// <summary>
/// As pastas de instalacao personalizadas do usuario.
///
/// O Revo Uninstaller Pro mostra, na lista de programas, os aplicativos que
/// o usuario instalou em pasta propria — "%LOCALAPPDATA%\Programs",
/// "%APPDATA%" e "%USERPROFILE%\AppData\Local" — que nao aparecem nos
/// "Program Files" e por isso escapam da varredura usual.
///
/// A regra mora aqui, em um lugar so: a lista e a CLI consomem esta
/// enumeracao e nunca montam a sua.
/// </summary>
public static class UserInstallFolders
{
    /// <summary>Nomes de desinstalador conhecidos.</summary>
    private static readonly string[] UninstallerNames =
    {
        "unins000.exe", "unins001.exe", "uninstall.exe", "uninstaller.exe",
        "uninstall", "uninst.exe", "remove.exe", "remover.exe", "app_uninstaller.exe",
    };

    /// <summary>
    /// As pastas de usuario onde aplicativos costumam ser instalados.
    /// </summary>
    public static IReadOnlyList<string> Roots
    {
        get
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var roots = new List<string>();

            // "%LOCALAPPDATA%\Programs": a pasta que o Windows mesmo usa para
            // os aplicativos do usuario ( installers "por usuario").
            if (local.Length > 0) roots.Add(Path.Combine(local, "Programs"));

            // "%APPDATA%": aplicativos portateis e instaladores velhos.
            if (roaming.Length > 0) roots.Add(roaming);

            // "%USERPROFILE%\AppData\Local" (= %LOCALAPPDATA%).
            if (local.Length > 0) roots.Add(local);

            // "%USERPROFILE%\AppData\Local\Programs" de novo, por seguranca.
            if (profile.Length > 0)
            {
                var programs = Path.Combine(profile, "AppData", "Local", "Programs");
                if (!roots.Contains(programs, StringComparer.OrdinalIgnoreCase)) roots.Add(programs);
            }

            return roots
                .Select(PathUtil.Normalize)
                .Where(r => r.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    /// <summary>
    /// True quando o caminho esta dentro de uma pasta de instalacao do
    /// usuario (e nao em "Program Files").
    /// </summary>
    /// <param name="path">O caminho a conferir.</param>
    public static bool IsUserInstallPath(string? path)
    {
        var full = PathUtil.Normalize(path);
        if (full.Length == 0) return false;

        foreach (var root in Roots)
        {
            if (full.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>
    /// Enumera os aplicativos instalados em pasta de usuario.
    ///
    /// Varre o primeiro nivel de cada raiz: cada pasta e candidata a
    /// aplicativo, e vira item quando tem desinstalador, manifesto de pacote
    /// ou um executavel com o proprio nome.
    /// </summary>
    public static IReadOnlyList<UserInstallApp> Scan()
    {
        var result = new List<UserInstallApp>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in Roots)
        {
            if (!Directory.Exists(root)) continue;

            IEnumerable<string> folders;
            try
            {
                folders = Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly).ToList();
            }
            catch
            {
                continue;
            }

            foreach (var folder in folders)
            {
                var full = PathUtil.Normalize(folder);
                if (full.Length == 0) continue;
                if (!seen.Add(full)) continue;

                // A propria pasta "Programs" dentro de %LOCALAPPDATA% e a raiz
                // do outro item: ela entra como raiz, e nao como aplicativo.
                if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase)) continue;

                var app = Describe(folder, full, root);
                if (app is not null) result.Add(app);
            }
        }

        return result
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Descreve a pasta como aplicativo, ou devolve nulo quando ela nao e um
    /// aplicativo (e so uma pasta qualquer do usuario).
    /// </summary>
    private static UserInstallApp? Describe(string folder, string full, string root)
    {
        string uninstaller = string.Empty;
        var hasManifest = false;
        string displayName = string.Empty;

        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(file);

                if (string.Equals(fileName, "AppxManifest.xml", StringComparison.OrdinalIgnoreCase))
                {
                    hasManifest = true;
                    continue;
                }

                if (uninstaller.Length > 0) continue;

                if (IsUninstallerName(fileName)) uninstaller = file;
            }
        }
        catch
        {
            // Pasta inacessivel: nao e aplicativo conhecido.
            return null;
        }

        // Sem desinstalador e sem manifesto: so e aplicativo se tiver um
        // executavel com o nome da propria pasta (portateis).
        if (uninstaller.Length == 0 && !hasManifest && !HasExecutableNamedLike(folder, full))
            return null;

        if (hasManifest) displayName = ReadManifestDisplayName(folder);

        var name = displayName.Length > 0 ? displayName : Path.GetFileName(full.TrimEnd('\\', '/'));

        return new UserInstallApp(
            name,
            full,
            root,
            uninstaller.Length > 0,
            uninstaller.Length > 0 ? PathUtil.Normalize(uninstaller) : string.Empty);
    }

    /// <summary>True quando o nome do arquivo e o de um desinstalador conhecido.</summary>
    private static bool IsUninstallerName(string fileName)
    {
        var lower = fileName.ToLowerInvariant();

        foreach (var known in UninstallerNames)
        {
            if (lower.Equals(known, StringComparison.OrdinalIgnoreCase)) return true;
        }

        // "Uninstall App.exe", "App Uninstaller.exe" e variantes.
        return lower.StartsWith("uninstall", StringComparison.Ordinal)
            || lower.EndsWith("uninstaller.exe", StringComparison.Ordinal);
    }

    /// <summary>True quando a pasta tem um executavel com o nome da propria pasta.</summary>
    private static bool HasExecutableNamedLike(string folder, string full)
    {
        var leaf = Path.GetFileName(full.TrimEnd('\\', '/'));
        if (leaf.Length < 3) return false;

        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.exe", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(file), leaf,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Le o nome exibido do manifesto de um aplicativo empacotado
    /// (AppxManifest.xml). Devolve string vazia quando nao da para ler.
    /// </summary>
    private static string ReadManifestDisplayName(string folder)
    {
        try
        {
            var manifest = Path.Combine(folder, "AppxManifest.xml");
            if (!File.Exists(manifest)) return string.Empty;

            var text = File.ReadAllText(manifest);
            var match = Regex.Match(text, @"DisplayName\s*=\s*""(?<name>[^""]+)""",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            return match.Success ? match.Groups["name"].Value : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
