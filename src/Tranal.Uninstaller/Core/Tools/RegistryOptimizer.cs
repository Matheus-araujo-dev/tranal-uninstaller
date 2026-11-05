using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Uma entrada obsoleta do Menu Iniciar encontrada pelo Otimizador de Registro.
///
/// Equivale as entradas que o "Registry Optimizer" do Revo Uninstaller Pro
///remove do Menu Iniciar: atalhos (.lnk) de programas que ja foram
/// desinstalados e cujo alvo nao existe mais no disco.
/// </summary>
public sealed class RegistryOptimizerEntry
{
    /// <summary>
    /// Caminho da entrada. E a chave de registro do atalho (no formato do
    /// regedit, ex.: <c>HKEY_CURRENT_USER\Software\...\MenuOrder\Start Menu\Programs\Chrome</c>)
    /// quando o atalho tem counterpart de registro; caso contrario, o caminho
    /// completo do proprio arquivo .lnk.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Nome exibido da entrada.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Detalhe da entrada (origem e alvo inexistente).</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>True quando o caminho da entrada e uma chave de registro.</summary>
    public bool IsRegistryKey => Path.StartsWith("HKEY_", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Otimizador de Registro: procura entradas obsoletas do Menu Iniciar e
/// permite remove-las.
///
/// Varredura (mesma cobertura do Revo):
///   - chaves de MenuOrder do Menu Iniciar do usuario e do sistema
///     (<c>...\Explorer\MenuOrder\Start Menu\Programs</c>);
///   - Menu Iniciar do Windows 10/11, resolvido pelas chaves
///     <c>User Shell Folders</c> e <c>Shell Folders</c> (pastas "Programs",
///     "Start Menu", "Common Programs" e "Common Start Menu").
///
/// Criterio de obsolescencia: o alvo do atalho (.lnk) nao existe mais no disco
/// (o programa foi desinstalado) OU a pasta do programa foi removida.
///
/// Remocao: apaga APENAS a entrada (a chave de registro da entrada obsoleta ou
/// o arquivo .lnk). A pasta de programas do Menu Iniciar nunca e removida.
/// A remocao passa pelo motor canonico <see cref="UninstallEngine"/>, que
/// respeita <see cref="SafetyRules"/> e cria o backup antes de apagar.
/// </summary>
public sealed class RegistryOptimizer
{
    /// <summary>Caminho relativo das chaves de MenuOrder (dentro de HKCU e HKLM).</summary>
    private const string MenuOrderRelative =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\MenuOrder\Start Menu\Programs";

    /// <summary>Caminho relativo das chaves de pastas do shell.</summary>
    private const string ShellFoldersRelative =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer";

    /// <summary>Profundidade maxima da varredura recursiva do MenuOrder.</summary>
    private const int MaxMenuOrderDepth = 8;

    /// <summary>
    /// Procura as entradas obsoletas do Menu Iniciar.
    /// Nunca lanca: falhas parciais devolvem apenas o que foi lido com sucesso.
    /// </summary>
    public static IReadOnlyList<RegistryOptimizerEntry> ScanStartMenu()
    {
        var entries = new List<RegistryOptimizerEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Mapa atalho -> chave de registro (o MenuOrder guarda a ordem dos
        // itens do Menu Iniciar; quando a chave existe, a entrada e dela).
        var registryByShortcut = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Entradas cujo proprio atalho ja foi apagado (chave orfa).
        var orphans = new List<RegistryOptimizerEntry>();

        try { ScanMenuOrder(RegistryHive.CurrentUser, registryByShortcut, orphans); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer o MenuOrder do usuario: {ex.Message}"); }

        try { ScanMenuOrder(RegistryHive.LocalMachine, registryByShortcut, orphans); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer o MenuOrder do sistema: {ex.Message}"); }

        // 0) Chaves de MenuOrder cujo atalho nao existe mais.
        entries.AddRange(orphans);

        // 1) Atalhos com chave de registro: a entrada reportada e a CHAVE.
        foreach (var pair in registryByShortcut)
        {
            try
            {
                if (!seen.Add(pair.Key)) continue;
                if (!IsObsolete(pair.Key, out var target)) continue;

                entries.Add(new RegistryOptimizerEntry
                {
                    Path = pair.Value,
                    DisplayName = SafeFileNameWithoutExtension(pair.Key),
                    Detail = DescribeObsolete(target),
                });
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao avaliar o atalho '{pair.Key}': {ex.Message}");
            }
        }

        // 2) Atalhos das pastas do Menu Iniciar sem chave de registro.
        try { ScanStartMenuFolders(registryByShortcut, entries, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer as pastas do Menu Iniciar: {ex.Message}"); }

        return entries;
    }

    /// <summary>
    /// Remove as entradas obsoletas informadas.
    ///
    /// Cada entrada vira um item de sobra canonico (chave de registro ou
    /// arquivo .lnk) e a remocao e feita por
    /// <see cref="UninstallEngine.RemoveLeftovers"/>. Entradas protegidas pelas
    /// <see cref="SafetyRules"/> sao barradas antes da remocao.
    /// </summary>
    public static RemovalReport Remove(IEnumerable<RegistryOptimizerEntry> entries)
    {
        var items = new List<LeftoverItem>();

        foreach (var entry in entries ?? Array.Empty<RegistryOptimizerEntry>())
        {
            try
            {
                if (entry is null) continue;

                var path = (entry.Path ?? string.Empty).Trim();
                if (path.Length == 0) continue;

                if (path.StartsWith("HKEY_", StringComparison.OrdinalIgnoreCase))
                {
                    // Chave de registro: apenas a chave da entrada e apagada
                    // (nunca a pasta de programas inteira).
                    if (SafetyRules.IsProtectedRegistryKey(path))
                    {
                        ToolLog.Info($"Chave de registro protegida, ignorada: {path}");
                        continue;
                    }

                    items.Add(new LeftoverItem
                    {
                        Kind = "Registro",
                        Path = path,
                        Detail = string.IsNullOrWhiteSpace(entry.DisplayName) ? "Entrada obsoleta do Menu Iniciar" : entry.DisplayName,
                        Selected = true,
                        Confidence = LeftoverConfidence.High,
                    });
                }
                else
                {
                    // Atalho sem chave de registro: apaga-se apenas o .lnk.
                    items.Add(new LeftoverItem
                    {
                        Kind = "Arquivo",
                        Path = path,
                        Detail = string.IsNullOrWhiteSpace(entry.DisplayName) ? "Atalho obsoleto do Menu Iniciar" : entry.DisplayName,
                        Selected = true,
                        Confidence = LeftoverConfidence.High,
                    });
                }
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao preparar a remocao da entrada '{entry.Path}': {ex.Message}");
            }
        }

        if (items.Count == 0) return new RemovalReport(Array.Empty<RemovalOutcome>());

        return UninstallEngine.RemoveLeftovers(items);
    }

    // ---------------------------------------------------------------------
    // Varredura das chaves de MenuOrder
    // ---------------------------------------------------------------------

    /// <summary>
    /// Percorre as chaves de MenuOrder do Menu Iniciar e registra, para cada
    /// atalho encontrado, a chave de registro que o descreve. Chaves cujo
    /// atalho ja nao existe em disco viram entradas obsoletas.
    /// </summary>
    private static void ScanMenuOrder(
        RegistryHive hive,
        Dictionary<string, string> registryByShortcut,
        List<RegistryOptimizerEntry> orphans)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(MenuOrderRelative);
            if (key is null) return;

            WalkMenuOrder(key, hive, MenuOrderRelative, registryByShortcut, orphans, 0);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler {hive}\\{MenuOrderRelative}: {ex.Message}");
        }
    }

    private static void WalkMenuOrder(
        RegistryKey key, RegistryHive hive, string subPath,
        Dictionary<string, string> registryByShortcut,
        List<RegistryOptimizerEntry> orphans,
        int depth)
    {
        if (depth > MaxMenuOrderDepth) return;

        string[] names;
        try { names = key.GetSubKeyNames(); }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao listar as subchaves de '{subPath}': {ex.Message}");
            return;
        }

        foreach (var name in names)
        {
            try
            {
                var childPath = subPath + "\\" + name;
                using var child = key.OpenSubKey(name);
                if (child is null) continue;

                var hasChildren = child.GetSubKeyNames().Length > 0;
                if (hasChildren)
                {
                    // Pasta do Menu Iniciar: apenas os itens dentro dela sao entradas.
                    WalkMenuOrder(child, hive, childPath, registryByShortcut, orphans, depth + 1);
                    continue;
                }

                var link = ReadDefaultValue(child);
                var shortcut = ResolveShortcutPath(link, name);

                var canonical = "HKEY_LOCAL_MACHINE\\" + childPath;
                if (hive == RegistryHive.CurrentUser) canonical = "HKEY_CURRENT_USER\\" + childPath;

                if (shortcut is null)
                {
                    // A chave aponta para um .lnk que nao existe mais: a propria
                    // entrada do Menu Iniciar e obsoleta.
                    if (!string.IsNullOrWhiteSpace(link)
                        && link.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        orphans.Add(new RegistryOptimizerEntry
                        {
                            Path = canonical,
                            DisplayName = name,
                            Detail = $"Atalho do Menu Iniciar removido: {link}",
                        });
                    }

                    continue;
                }

                registryByShortcut[shortcut] = canonical;
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao ler a entrada '{name}': {ex.Message}");
            }
        }
    }

    // ---------------------------------------------------------------------
    // Varredura das pastas do Menu Iniciar (Windows 10/11)
    // ---------------------------------------------------------------------

    /// <summary>
    /// Percorre as pastas do Menu Iniciar resolvidas pelas chaves
    /// <c>User Shell Folders</c> e <c>Shell Folders</c> e reporta os atalhos
    /// cujo alvo nao existe mais.
    /// </summary>
    private static void ScanStartMenuFolders(
        Dictionary<string, string> registryByShortcut,
        List<RegistryOptimizerEntry> entries,
        HashSet<string> seen)
    {
        foreach (var folder in StartMenuFolders())
        {
            if (string.IsNullOrWhiteSpace(folder)) continue;
            if (!Directory.Exists(folder)) continue;

            foreach (var link in EnumerateShortcuts(folder))
            {
                try
                {
                    // Ja tratada pela varredura do MenuOrder.
                    if (registryByShortcut.ContainsKey(link)) continue;
                    if (!seen.Add(link)) continue;

                    if (!IsObsolete(link, out var target)) continue;

                    var name = SafeFileNameWithoutExtension(link);
                    entries.Add(new RegistryOptimizerEntry
                    {
                        Path = link,
                        DisplayName = name,
                        Detail = DescribeObsolete(target),
                    });
                }
                catch (Exception ex)
                {
                    ToolLog.Error($"Falha ao avaliar o atalho '{link}': {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// True quando o atalho aponta para um alvo que nao existe mais no disco
    /// (o programa foi desinstalado) ou para dentro de uma pasta removida.
    ///
    /// Atalhos sem alvo legivel (atalhos de rede, pastas virtuais) e atalhos
    /// para URLs NAO sao julgados: nao ha informacao suficiente.
    /// </summary>
    private static bool IsObsolete(string lnkPath, out string target)
    {
        target = ShellLinkReader.ResolveTarget(lnkPath) ?? string.Empty;
        if (target.Length == 0) return false;

        // Atalhos de rede/URL nao sao julgados.
        if (target.Contains("://", StringComparison.Ordinal)) return false;

        var expanded = Environment.ExpandEnvironmentVariables(target).Trim();
        if (expanded.Length == 0) return false;

        target = expanded;
        return !File.Exists(expanded) && !Directory.Exists(expanded);
    }

    /// <summary>
    /// Pastas do Menu Iniciar do usuario e do sistema, resolvidas pelas chaves
    /// <c>User Shell Folders</c> e <c>Shell Folders</c>.
    /// </summary>
    private static IReadOnlyList<string> StartMenuFolders()
    {
        var folders = new List<string>();

        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        {
            foreach (var keyName in new[] { "User Shell Folders", "Shell Folders" })
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                    using var key = baseKey.OpenSubKey(ShellFoldersRelative + "\\" + keyName);
                    if (key is null) continue;

                    foreach (var valueName in new[] { "Programs", "Start Menu", "Common Programs", "Common Start Menu" })
                    {
                        var raw = key.GetValue(valueName) as string;
                        if (string.IsNullOrWhiteSpace(raw)) continue;

                        var expanded = Environment.ExpandEnvironmentVariables(raw).Trim();
                        if (expanded.Length == 0) continue;

                        folders.Add(expanded);

                        // "Start Menu" (sem "Programs") tambem contem a pasta
                        // de programas do Windows 10/11 em um nivel abaixo.
                        var programs = Path.Combine(expanded, "Programs");
                        if (Directory.Exists(programs)) folders.Add(programs);
                    }
                }
                catch (Exception ex)
                {
                    ToolLog.Error($"Falha ao ler {hive}\\{keyName}: {ex.Message}");
                }
            }
        }

        return folders
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ---------------------------------------------------------------------
    // Auxiliares
    // ---------------------------------------------------------------------

    /// <summary>
    /// Descobre o caminho do atalho de uma entrada do MenuOrder: pelo valor
    /// padrao da chave ou, quando ela nao tem, pelo nome da entrada dentro das
    /// pastas do Menu Iniciar.
    /// </summary>
    private static string? ResolveShortcutPath(string defaultValue, string name)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(defaultValue))
            {
                var expanded = Environment.ExpandEnvironmentVariables(defaultValue).Trim();
                if (expanded.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && File.Exists(expanded))
                    return expanded;
            }

            foreach (var folder in StartMenuFolders())
            {
                var candidate = Path.Combine(folder, name + ".lnk");
                if (File.Exists(candidate)) return candidate;
            }
        }
        catch { }

        return null;
    }

    /// <summary>Le o valor padrao de uma chave de registro.</summary>
    private static string ReadDefaultValue(RegistryKey key)
    {
        try
        {
            var value = key.GetValue(null) as string;
            return value ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Enumera os arquivos .lnk de uma pasta, recursivamente.</summary>
    private static IEnumerable<string> EnumerateShortcuts(string folder)
    {
        var stack = new Stack<string>();
        stack.Push(folder);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            string[] files;
            try { files = Directory.GetFiles(current, "*.lnk"); }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao listar os atalhos de '{current}': {ex.Message}");
                continue;
            }

            foreach (var file in files) yield return file;

            try
            {
                foreach (var sub in Directory.GetDirectories(current)) stack.Push(sub);
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao listar as subpastas de '{current}': {ex.Message}");
            }
        }
    }

    /// <summary>Descreve o motivo da obsolescencia do atalho.</summary>
    private static string DescribeObsolete(string target)
    {
        try
        {
            var parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
                return $"Pasta do programa removida; alvo inexistente: {target}";

            return $"Alvo inexistente: {target}";
        }
        catch
        {
            return $"Alvo inexistente: {target}";
        }
    }

    private static string SafeFileNameWithoutExtension(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            return Path.GetFileNameWithoutExtension(path) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}

/// <summary>
/// Le o alvo de um atalho do Windows (.lnk).
///
/// ABORDAGEM (a mais confiavel foi adotada, com duas redes de seguranca):
///   1. COM <c>IShellLinkW</c> por late binding (Marshal.GetTypeFromCLSID +
///      Type.InvokeMember, sem referencia a Shell32). E o metodo oficial do
///      Windows e resolve atalhos com IDList, caminho relativo, UNC e MSI.
///   2. COM <c>WScript.Shell</c> por late binding (o mesmo metodo usado pelo
///      Modo Cacador do Tranal), quando o IShellLinkW nao responde.
///   3. Leitura binaria do proprio arquivo .lnk (bloco LinkInfo), usada
///      quando nenhum COM esta disponivel. O caminho local esta em
///      <c>LinkInfo + 0x10</c> (LocalBasePathOffset) e o sufixo comum em
///      <c>LinkInfo + 0x18</c> (CommonPathSuffixOffset); o bloco LinkInfo
///      comeca em 0x4C no arquivo, acrescido do tamanho do LinkTargetIDList.
///
/// A ordem importa: o COM conhece atalhos que o parser binario nao consegue
/// ler (por exemplo, os que usam apenas IDList), e o parser binario funciona
/// em ambientes sem COM algum.
/// </summary>
public static class ShellLinkReader
{
    /// <summary>CLSID da coclass ShellLink (IShellLinkW).</summary>
    private static readonly Guid ShellLinkClsid = new("00021401-0000-0000-C000-000000000046");

    /// <summary>STGM_READ.</summary>
    private const int StgmRead = 0x00000010;

    /// <summary>SLGP_RAWPATH: devolve o caminho bruto (sem resolver atalhos encadeados).</summary>
    private const int SlgpRawPath = 0x00000004;

    /// <summary>
    /// Devolve o alvo do atalho (o caminho do executavel apontado pelo .lnk)
    /// ou null quando o atalho nao pode ser lido.
    /// </summary>
    public static string? ResolveTarget(string lnkPath)
    {
        if (string.IsNullOrWhiteSpace(lnkPath)) return null;

        try
        {
            if (!File.Exists(lnkPath)) return null;

            var target = ResolveWithShellLink(lnkPath);
            if (!string.IsNullOrWhiteSpace(target)) return target.Trim();

            target = ResolveWithWScript(lnkPath);
            if (!string.IsNullOrWhiteSpace(target)) return target.Trim();

            target = ResolveFromBinary(lnkPath);
            if (!string.IsNullOrWhiteSpace(target)) return target.Trim();

            return null;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao resolver o atalho '{lnkPath}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// True quando o atalho aponta para um alvo que ainda existe no disco.
    /// Atalhos sem alvo legivel (atalhos de rede ou de pasta virtual) sao
    /// considerados existentes: nao ha informacao suficiente para julga-los.
    /// </summary>
    public static bool TargetExists(string lnkPath, out string target)
    {
        target = ResolveTarget(lnkPath) ?? string.Empty;
        if (target.Length == 0) return true;

        var expanded = Environment.ExpandEnvironmentVariables(target).Trim();
        try
        {
            return File.Exists(expanded) || Directory.Exists(expanded);
        }
        catch
        {
            return true;
        }
    }

    // ---------------------------------------------------------------------
    // 1) COM IShellLinkW (late binding)
    // ---------------------------------------------------------------------

    private static string? ResolveWithShellLink(string lnkPath)
    {
        object? instance = null;

        try
        {
            var type = Type.GetTypeFromCLSID(ShellLinkClsid);
            if (type is null) return null;

            instance = Activator.CreateInstance(type);
            if (instance is null) return null;

            // IPersistFile::Load(caminho, STGM_READ)
            type.InvokeMember("Load", BindingFlags.InvokeMethod, null, instance,
                new object[] { lnkPath, StgmRead });

            // IShellLinkW::GetPath(buffer, capacidade, pfd, SLGP_RAWPATH)
            var buffer = new StringBuilder(1024);
            type.InvokeMember("GetPath", BindingFlags.InvokeMethod, null, instance,
                new object?[] { buffer, buffer.Capacity, IntPtr.Zero, SlgpRawPath });

            var path = buffer.ToString();
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        catch (Exception ex)
        {
            ToolLog.Info($"IShellLinkW indisponivel para '{lnkPath}': {ex.Message}");
            return null;
        }
        finally
        {
            ReleaseComObject(instance);
        }
    }

    // ---------------------------------------------------------------------
    // 2) COM WScript.Shell (late binding)
    // ---------------------------------------------------------------------

    private static string? ResolveWithWScript(string lnkPath)
    {
        object? instance = null;

        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return null;

            instance = Activator.CreateInstance(shellType);
            if (instance is null) return null;

            dynamic shell = instance;
            dynamic shortcut = shell.CreateShortcut(lnkPath);

            var target = Convert.ToString(shortcut.TargetPath);
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch (Exception ex)
        {
            ToolLog.Info($"WScript.Shell indisponivel para '{lnkPath}': {ex.Message}");
            return null;
        }
        finally
        {
            ReleaseComObject(instance);
        }
    }

    // ---------------------------------------------------------------------
    // 3) Leitura binaria do arquivo .lnk
    // ---------------------------------------------------------------------

    /// <summary>
    /// Le o caminho do alvo no bloco LinkInfo do arquivo .lnk.
    /// Devolve null quando o atalho nao tem LinkInfo (so IDList) ou quando o
    /// arquivo esta corrompido.
    /// </summary>
    private static string? ResolveFromBinary(string lnkPath)
    {
        try
        {
            var bytes = File.ReadAllBytes(lnkPath);
            if (bytes.Length < 0x4C) return null;

            var headerSize = BitConverter.ToUInt32(bytes, 0x00);
            if (headerSize < 0x4C) return null;

            var flags = BitConverter.ToUInt32(bytes, 0x14);
            var hasIdList = (flags & 0x01) != 0;
            var hasLinkInfo = (flags & 0x02) != 0;
            var isUnicode = (flags & 0x80) != 0;

            var offset = 0x4C;

            // O LinkTargetIDList vem antes do LinkInfo, quando existe.
            if (hasIdList)
            {
                if (offset + 2 > bytes.Length) return null;
                var idListSize = BitConverter.ToUInt16(bytes, offset);
                if (idListSize < 2) return null;
                offset += idListSize;
            }

            if (!hasLinkInfo) return null;
            if (offset + 0x1C > bytes.Length) return null;

            var localBasePathOffset = BitConverter.ToUInt32(bytes, offset + 0x10);
            var commonPathSuffixOffset = BitConverter.ToUInt32(bytes, offset + 0x18);
            if (localBasePathOffset == 0) return null;

            var localBase = ReadNullTerminated(bytes, offset + (int)localBasePathOffset, isUnicode);
            if (string.IsNullOrWhiteSpace(localBase)) return null;

            var suffix = commonPathSuffixOffset == 0
                ? string.Empty
                : ReadNullTerminated(bytes, offset + (int)commonPathSuffixOffset, isUnicode);

            return localBase + suffix;
        }
        catch (Exception ex)
        {
            ToolLog.Info($"Leitura binaria do atalho falhou em '{lnkPath}': {ex.Message}");
            return null;
        }
    }

    /// <summary>Le uma string terminada em zero (UTF-16 ou Latin1).</summary>
    private static string ReadNullTerminated(byte[] bytes, int start, bool unicode)
    {
        if (start < 0 || start >= bytes.Length) return string.Empty;

        var encoding = unicode ? Encoding.Unicode : Encoding.Latin1;
        var step = unicode ? 2 : 1;
        var end = start;

        while (end + step <= bytes.Length)
        {
            var terminator = unicode ? bytes[end] == 0 && bytes[end + 1] == 0 : bytes[end] == 0;
            if (terminator) break;
            end += step;
        }

        return encoding.GetString(bytes, start, end - start);
    }

    /// <summary>Libera o objeto COM, quando houver.</summary>
    private static void ReleaseComObject(object? instance)
    {
        try
        {
            if (instance is not null && Marshal.IsComObject(instance)) Marshal.ReleaseComObject(instance);
        }
        catch
        {
            // Liberar o RCW e best effort.
        }
    }
}
