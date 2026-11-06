using System.IO;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// O gerenciador de pacotes que instalou um aplicativo.
/// </summary>
public enum PackageManagerKind
{
    /// <summary>Loja Steam.</summary>
    Steam,

    /// <summary>Chocolatey.</summary>
    Chocolatey,

    /// <summary>Scoop.</summary>
    Scoop,
}

/// <summary>
/// Um item instalado por um gerenciador de pacotes.
///
/// Nem todo item gerenciado tem desinstalador proprio: o jogo da Steam e o
/// pacote do Scoop sao removidos pelo proprio gerenciador. O campo
/// <see cref="HasOwnUninstaller"/> diz qual e o caso, para a tela poder
/// oferecer o caminho certo.
/// </summary>
public sealed class ManagedPackage
{
    /// <summary>Nome do item, como o gerenciador mostra.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gerenciador responsavel.</summary>
    public PackageManagerKind Manager { get; init; }

    /// <summary>Pasta de instalacao do item.</summary>
    public string InstallPath { get; init; } = string.Empty;

    /// <summary>Versao declarada pelo gerenciador (vazia quando ele nao diz).</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>True quando o item tem desinstalador proprio.</summary>
    public bool HasOwnUninstaller { get; init; }

    /// <summary>Caminho do desinstalador proprio (vazio quando nao ha).</summary>
    public string UninstallerPath { get; init; } = string.Empty;

    /// <summary>Entrada do item no gerenciador (shim, manifesto ou manifest).</summary>
    public string ManagerEntry { get; init; } = string.Empty;

    /// <summary>Observacao em portugues sobre o caminho de remocao.</summary>
    public string Note { get; init; } = string.Empty;
}

/// <summary>
/// Onde ficam as raizes dos gerenciadores de pacotes.
///
/// Os caminhos sao injetaveis de proposito: a suite monta uma arvore
/// sintetica e prova a regra sem depender da maquina de quem roda.
/// </summary>
public sealed class PackageManagerRoots
{
    /// <summary>Pasta do Steam (a que contem <c>steamapps</c>).</summary>
    public string? SteamPath { get; init; }

    /// <summary>Pasta do Chocolatey (a que contem <c>lib</c> e <c>bin</c>).</summary>
    public string? ChocolateyPath { get; init; }

    /// <summary>Pasta do Scoop (a que contem <c>apps</c> e <c>shims</c>).</summary>
    public string? ScoopPath { get; init; }

    /// <summary>
    /// Descobre as raizes nesta maquina, pelos caminhos conhecidos e pelo
    /// registro. Devolve raizes vazias para o que nao existir.
    /// </summary>
    public static PackageManagerRoots Detect()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return new PackageManagerRoots
        {
            SteamPath = FirstExisting(
                Combine(programFilesX86, "Steam"),
                Combine(programFiles, "Steam"),
                Combine(programFilesX86, "Steam"),
                ReadSteamPathFromRegistry()),
            ChocolateyPath = FirstExisting(Combine(programData, "chocolatey")),
            ScoopPath = FirstExisting(Combine(userProfile, "scoop")),
        };
    }

    /// <summary>
    /// O caminho do Steam declarado no registro do usuario.
    /// </summary>
    private static string? ReadSteamPathFromRegistry()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var path = key?.GetValue("SteamPath")?.ToString();
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        catch
        {
            // Sem registro do Steam: o caminho segue desconhecido.
            return null;
        }
    }

    /// <summary>
    /// Junta duas partes de caminho.
    /// </summary>
    private static string Combine(string root, string child) =>
        Path.Combine(root, child);

    /// <summary>
    /// O primeiro caminho que existe, ou nulo.
    /// </summary>
    private static string? FirstExisting(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                if (Directory.Exists(candidate)) return candidate;
            }
            catch
            {
                // Caminho inacessivel: tenta o proximo.
            }
        }

        return null;
    }
}

/// <summary>
/// Uma entrada do formato VDF (o texto que a Steam usa): o caminho da chave
/// e o valor, por exemplo <c>libraryfolders/0/path</c>.
/// </summary>
public sealed class VdfEntry
{
    /// <summary>Caminho da chave, separado por barra.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Valor da chave.</summary>
    public string Value { get; init; } = string.Empty;
}

/// <summary>
/// Catalogo dos aplicativos instalados por gerenciadores de pacotes
/// (Steam, Chocolatey e Scoop).
///
/// A leitura e apenas leitura: o catalogo diz o que existe e se o item tem
/// desinstalador proprio. Quem decide remover e o Tranal, com o pedido do
/// usuario.
/// </summary>
public static class PackageManagerCatalog
{
    /// <summary>
    /// Descobre os itens dos tres gerenciadores. Gerenciador ausente nao
    /// produz nada — e nao erro.
    /// </summary>
    public static IReadOnlyList<ManagedPackage> Discover(PackageManagerRoots roots)
    {
        var packages = new List<ManagedPackage>();

        if (roots is null) return packages;

        if (!string.IsNullOrWhiteSpace(roots.SteamPath))
            packages.AddRange(DiscoverSteam(roots.SteamPath));

        if (!string.IsNullOrWhiteSpace(roots.ChocolateyPath))
            packages.AddRange(DiscoverChocolatey(roots.ChocolateyPath));

        if (!string.IsNullOrWhiteSpace(roots.ScoopPath))
            packages.AddRange(DiscoverScoop(roots.ScoopPath));

        return packages
            .OrderBy(p => p.Manager)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Descobre os aplicativos das bibliotecas Steam.
    ///
    /// A Steam pode ter bibliotecas em outros discos: por isso a lista de
    /// bibliotecas vem do <c>libraryfolders.vdf</c>, e cada biblioteca tem os
    /// seus <c>appmanifest_*.acf</c>.
    /// </summary>
    public static IReadOnlyList<ManagedPackage> DiscoverSteam(string steamPath)
    {
        var packages = new List<ManagedPackage>();
        if (string.IsNullOrWhiteSpace(steamPath)) return packages;

        foreach (var library in ReadSteamLibraryPaths(steamPath))
        {
            var steamapps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamapps)) continue;

            string[] manifests;
            try { manifests = Directory.GetFiles(steamapps, "appmanifest_*.acf"); }
            catch { continue; }

            foreach (var manifest in manifests)
            {
                var entries = ParseVdf(ReadText(manifest));

                var appid = Value(entries, "AppState/appid");
                var name = Value(entries, "AppState/name");
                var installDir = Value(entries, "AppState/installdir");

                if (name.Length == 0 && appid.Length == 0) continue;

                var installPath = installDir.Length > 0
                    ? Path.Combine(steamapps, "common", installDir)
                    : string.Empty;

                var uninstaller = FindOwnUninstaller(installPath);

                packages.Add(new ManagedPackage
                {
                    Name = name.Length > 0 ? name : $"App {appid}",
                    Manager = PackageManagerKind.Steam,
                    InstallPath = installPath,
                    Version = string.Empty,
                    HasOwnUninstaller = uninstaller.Length > 0,
                    UninstallerPath = uninstaller,
                    ManagerEntry = manifest,
                    Note = uninstaller.Length > 0
                        ? "O jogo tem desinstalador proprio."
                        : "Jogo sem desinstalador proprio: a remocao e feita pela Steam.",
                });
            }
        }

        return packages;
    }

    /// <summary>
    /// Descobre os pacotes do Chocolatey (pasta <c>lib</c> e shim em <c>bin</c>).
    /// </summary>
    public static IReadOnlyList<ManagedPackage> DiscoverChocolatey(string chocolateyPath)
    {
        var packages = new List<ManagedPackage>();
        if (string.IsNullOrWhiteSpace(chocolateyPath)) return packages;

        var lib = Path.Combine(chocolateyPath, "lib");
        if (!Directory.Exists(lib)) return packages;

        string[] folders;
        try { folders = Directory.GetDirectories(lib); }
        catch { return packages; }

        foreach (var folder in folders)
        {
            var name = Path.GetFileName(folder);
            var version = ReadNuspecVersion(folder);

            // O Chocolatey roda a desinstalacao pelo script do pacote.
            var uninstallScript = Path.Combine(folder, "tools", "chocolateyUninstall.ps1");
            var hasUninstaller = File.Exists(uninstallScript);

            var shim = Path.Combine(chocolateyPath, "bin", name + ".exe");

            packages.Add(new ManagedPackage
            {
                Name = name,
                Manager = PackageManagerKind.Chocolatey,
                InstallPath = folder,
                Version = version,
                HasOwnUninstaller = hasUninstaller,
                UninstallerPath = hasUninstaller ? uninstallScript : string.Empty,
                ManagerEntry = File.Exists(shim) ? shim : folder,
                Note = hasUninstaller
                    ? "O pacote tem script de desinstalacao proprio."
                    : "Pacote sem desinstalador proprio: a remocao e feita pelo Chocolatey.",
            });
        }

        return packages;
    }

    /// <summary>
    /// Descobre os aplicativos do Scoop (pasta <c>apps</c> e shim em <c>shims</c>).
    /// </summary>
    public static IReadOnlyList<ManagedPackage> DiscoverScoop(string scoopPath)
    {
        var packages = new List<ManagedPackage>();
        if (string.IsNullOrWhiteSpace(scoopPath)) return packages;

        var apps = Path.Combine(scoopPath, "apps");
        if (!Directory.Exists(apps)) return packages;

        string[] folders;
        try { folders = Directory.GetDirectories(apps); }
        catch { return packages; }

        foreach (var folder in folders)
        {
            var name = Path.GetFileName(folder);

            // O Scoop guarda uma pasta por versao; a mais recente e a instalada.
            string[] versions;
            try { versions = Directory.GetDirectories(folder); }
            catch { versions = Array.Empty<string>(); }

            var installPath = folder;
            var version = string.Empty;

            if (versions.Length > 0)
            {
                var ordered = versions.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList();
                installPath = ordered[^1];
                version = Path.GetFileName(installPath);
            }

            var uninstallScript = Path.Combine(installPath, "uninstall.ps1");
            var hasUninstaller = File.Exists(uninstallScript);

            var shim = Path.Combine(scoopPath, "shims", name + ".exe");

            packages.Add(new ManagedPackage
            {
                Name = name,
                Manager = PackageManagerKind.Scoop,
                InstallPath = installPath,
                Version = version,
                HasOwnUninstaller = hasUninstaller,
                UninstallerPath = hasUninstaller ? uninstallScript : string.Empty,
                ManagerEntry = File.Exists(shim) ? shim : folder,
                Note = hasUninstaller
                    ? "O aplicativo tem script de desinstalacao proprio."
                    : "Aplicativo sem desinstalador proprio: a remocao e feita pelo Scoop.",
            });
        }

        return packages;
    }

    /// <summary>
    /// Lista as pastas de biblioteca do Steam: a do proprio Steam e as
    /// declaradas no <c>libraryfolders.vdf</c> (que podem estar em outros
    /// discos).
    /// </summary>
    public static IReadOnlyList<string> ReadSteamLibraryPaths(string steamPath)
    {
        var libraries = new List<string>();
        if (string.IsNullOrWhiteSpace(steamPath)) return libraries;

        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        var text = ReadText(vdf);

        if (text.Length > 0)
        {
            foreach (var entry in ParseVdf(text))
            {
                if (!entry.Path.EndsWith("/path", StringComparison.OrdinalIgnoreCase)) continue;
                if (entry.Value.Length == 0) continue;
                libraries.Add(entry.Value);
            }
        }

        // A propria pasta do Steam e a raiz da biblioteca do Steam: os
        // manifestos moram em <Steam>\steamapps. O VDF declara as outras
        // bibliotecas pela raiz do disco, e os manifestos delas tambem
        // moram em <raiz>\steamapps.
        libraries.Add(steamPath);

        var unique = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var library in libraries)
        {
            if (seen.Add(library)) unique.Add(library);
        }

        return unique;
    }

    /// <summary>
    /// True quando o nome do arquivo parece o desinstalador de um programa
    /// (a regra do BCU): comeca com <c>unins</c> e termina com <c>.exe</c>.
    /// </summary>
    public static bool IsUninstallerFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        return fileName.StartsWith("unins", StringComparison.OrdinalIgnoreCase)
            && fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Procura um desinstalador proprio dentro da pasta do item.
    /// </summary>
    public static string FindOwnUninstaller(string installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath)) return string.Empty;
        if (!Directory.Exists(installPath)) return string.Empty;

        try
        {
            foreach (var file in Directory.GetFiles(installPath, "*.exe", SearchOption.TopDirectoryOnly))
            {
                if (IsUninstallerFileName(Path.GetFileName(file))) return file;
            }
        }
        catch
        {
            // Pasta inacessivel: o item entra sem desinstalador proprio.
        }

        return string.Empty;
    }

    /// <summary>
    /// Interpreta o texto VDF da Steam (usado no <c>libraryfolders.vdf</c> e
    /// nos <c>appmanifest_*.acf</c>).
    /// </summary>
    public static IReadOnlyList<VdfEntry> ParseVdf(string? text)
    {
        var entries = new List<VdfEntry>();
        if (string.IsNullOrEmpty(text)) return entries;

        var tokens = Tokenize(text);
        var stack = new List<string>();
        string? key = null;

        foreach (var token in tokens)
        {
            if (token == "{")
            {
                stack.Add(key ?? string.Empty);
                key = null;
                continue;
            }

            if (token == "}")
            {
                if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                key = null;
                continue;
            }

            if (key is null)
            {
                key = token;
                continue;
            }

            var path = stack.Count > 0
                ? string.Join("/", stack) + "/" + key
                : key;

            entries.Add(new VdfEntry { Path = path, Value = token });
            key = null;
        }

        return entries;
    }

    /// <summary>
    /// Quebra o texto VDF em tokens: chaves e valores entre aspas, chaves
    /// soltas e as chaves de estrutura.
    /// </summary>
    private static IEnumerable<string> Tokenize(string text)
    {
        var index = 0;

        while (index < text.Length)
        {
            var c = text[index];

            if (char.IsWhiteSpace(c))
            {
                index++;
                continue;
            }

            // Comentario de linha: o DISM e a Steam nao usam, mas o VDF aceita.
            if (c == '/' && index + 1 < text.Length && text[index + 1] == '/')
            {
                while (index < text.Length && text[index] != '\n') index++;
                continue;
            }

            if (c == '{' || c == '}')
            {
                yield return c.ToString();
                index++;
                continue;
            }

            if (c == '"')
            {
                index++;
                var value = new System.Text.StringBuilder();
                while (index < text.Length && text[index] != '"')
                {
                    if (text[index] == '\\' && index + 1 < text.Length)
                    {
                        index++;
                        value.Append(text[index] switch
                        {
                            'n' => '\n',
                            't' => '\t',
                            var other => other,
                        });
                    }
                    else
                    {
                        value.Append(text[index]);
                    }

                    index++;
                }

                // Fecha a aspa, se houver.
                if (index < text.Length) index++;
                yield return value.ToString();
                continue;
            }

            var start = index;
            while (index < text.Length && !char.IsWhiteSpace(text[index]) && text[index] != '{' && text[index] != '}')
                index++;

            if (index > start) yield return text[start..index];
        }
    }

    /// <summary>
    /// O valor de uma chave do VDF (ex.: <c>AppState/name</c>).
    /// </summary>
    private static string Value(IReadOnlyList<VdfEntry> entries, string path)
    {
        foreach (var entry in entries)
        {
            if (string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase)) return entry.Value;
        }

        return string.Empty;
    }

    /// <summary>
    /// Le a versao declarada no <c>.nuspec</c> do pacote Chocolatey.
    /// </summary>
    private static string ReadNuspecVersion(string folder)
    {
        try
        {
            foreach (var nuspec in Directory.GetFiles(folder, "*.nuspec", SearchOption.TopDirectoryOnly))
            {
                var text = ReadText(nuspec);
                var open = text.IndexOf("<version>", StringComparison.OrdinalIgnoreCase);
                if (open < 0) continue;

                var close = text.IndexOf("</version>", open, StringComparison.OrdinalIgnoreCase);
                if (close < 0) continue;

                return text[(open + "<version>".Length)..close].Trim();
            }
        }
        catch
        {
            // Nuspec ausente: a versao fica desconhecida.
        }

        return string.Empty;
    }

    /// <summary>
    /// Le um arquivo de texto sem lancar excecao.
    /// </summary>
    private static string ReadText(string path)
    {
        try
        {
            if (!File.Exists(path)) return string.Empty;
            return File.ReadAllText(path);
        }
        catch
        {
            // Arquivo inacessivel: o item entra sem o dado que nao veio.
            return string.Empty;
        }
    }
}
