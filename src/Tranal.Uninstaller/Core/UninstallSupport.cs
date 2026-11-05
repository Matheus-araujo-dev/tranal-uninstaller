using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Confianca da associacao entre uma sobra e o programa removido.
///
/// O Revo Uninstaller Pro destaca em negrito apenas os itens de confianca
/// alta e avisa: "somente os itens marcados em negrito e seus subitens serao
/// excluidos". No Tranal essa regra e aplicada de forma explicita: itens de
/// confianca alta vem marcados para remocao, os demais vem desmarcados.
/// </summary>
public enum LeftoverConfidence
{
    /// <summary>
    /// Alta: o item pertence de fato ao programa — a pasta de instalacao
    /// registrada, a chave de desinstalacao ou um item com o nome exato.
    /// </summary>
    High,

    /// <summary>Media: o nome casa com uma variante conhecida do programa.</summary>
    Medium,

    /// <summary>
    /// Baixa: o nome apenas se parece com o do programa (casamento por
    /// subpasta ou por segundo nivel da arvore de registro).
    /// </summary>
    Low,
}

public sealed class LeftoverItem
{
    public string Kind { get; init; } = string.Empty;      // "Arquivo" | "Pasta" | "Registro"
    public string Path { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public bool Selected { get; set; } = true;

    /// <summary>
    /// Confianca da associacao item/programa. Determina se o item entra
    /// marcado para remocao: apenas a confianca alta entra marcada.
    /// </summary>
    public LeftoverConfidence Confidence { get; init; } = LeftoverConfidence.High;

    /// <summary>True quando o item deve ser exibido em negrito na lista de sobras.</summary>
    public bool IsHighConfidence => Confidence == LeftoverConfidence.High;
}

/// <summary>Normalizacao canonica de caminhos de sistema.</summary>
public static class PathUtil
{
    /// <summary>
    /// Canonicaliza um caminho de sistema: remove aspas, resolve caminho relativo,
    /// remove barra final e normaliza as barras. "C:\App\" e "C:\App" viram a mesma chave.
    /// </summary>
    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        var value = path.Trim().Trim('"');
        try { value = Path.GetFullPath(value); }
        catch { /* caminho nao normalizavel (ex.: caminho de registro) fica como esta */ }

        if (value.Length > 3) value = value.TrimEnd('\\', '/');
        return value;
    }
}

/// <summary>Casamento de nomes: evita falsos positivos como "Steam" x "Steamworks".</summary>
public static class NameMatch
{
    /// <summary>
    /// Verdadeiro quando <paramref name="variant"/> aparece em <paramref name="candidate"/>
    /// sem ser seguido de uma letra. "Steam" casa em "Steam Tools", mas nao em "Steamworks".
    /// </summary>
    public static bool ContainsWord(string candidate, string variant)
    {
        if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(variant)) return false;

        var index = candidate.IndexOf(variant, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            var end = index + variant.Length;
            if (end >= candidate.Length || !char.IsLetter(candidate[end])) return true;
            index = candidate.IndexOf(variant, index + 1, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    /// <summary>
    /// Verdadeiro quando <paramref name="candidate"/> COMECA com <paramref name="variant"/>
    /// e o proximo caractere nao e uma letra. Usado para nomes de pasta, que seguem
    /// o padrao "&lt;Programa&gt; &lt;Complemento&gt;".
    /// </summary>
    public static bool StartsWithWord(string candidate, string variant)
    {
        if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(variant)) return false;
        if (!candidate.StartsWith(variant, StringComparison.OrdinalIgnoreCase)) return false;

        var end = variant.Length;
        if (end >= candidate.Length) return true;
        return !char.IsLetter(candidate[end]);
    }
}

/// <summary>
/// Regras de seguranca aplicadas ANTES de qualquer remocao.
/// Nenhuma remocao pode tocar chaves de registro ou pastas de sistema.
/// </summary>
public static class SafetyRules
{
    /// <summary>Nomes de chave de registro que nunca podem ser removidos.</summary>
    private static readonly HashSet<string> ProtectedRegistryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft", "Classes", "Wow6432Node", "Policies", "ODBC", "MauiFeeds",

        // Raiz dos hives criticos: um "sistema" chamado assim jamais pode ser
        // apagado por varredura de sobras (HKLM\SYSTEM, HKLM\SAM, HKLM\SECURITY...).
        "System", "SAM", "SECURITY", "SOFTWARE", "HARDWARE", "DEFAULT",
    };

    /// <summary>Nomes de pasta que nunca podem ser removidos (comparados com o ULTIMO segmento).</summary>
    private static readonly HashSet<string> ProtectedLeafNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "windows", "system32", "syswow64", "drivers", "boot", "recovery", "winsxs", "servicing",
        "windowsapps", "packages", "common files", "program files", "program files (x86)",
        "users", "$recycle.bin", "system volume information", "microsoft",
    };

    /// <summary>
    /// Verdadeiro quando a chave de registro e estrutural demais para ser removida.
    /// </summary>
    public static bool IsProtectedRegistryKey(string registryPath)
    {
        if (string.IsNullOrWhiteSpace(registryPath)) return true;

        var parts = registryPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return true;

        var name = parts[^1];
        if (ProtectedRegistryNames.Contains(name)) return true;

        // Uma chave com muitos filhos diretos e uma raiz compartilhada de um fornecedor
        // (ex.: "SOFTWARE\JavaSoft"): removeria programas nao relacionados.
        try
        {
            var (hive, sub) = SplitRegistryPath(registryPath);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(sub);
            if (key is not null && key.GetSubKeyNames().Length > 40) return true;
        }
        catch { /* chave inacessivel: nao e motivo para bloquear, o caso raro e seguro */ }

        return false;
    }

    /// <summary>
    /// Subpastas do Windows liberadas para limpeza: sao areas temporarias por
    /// definicao (cache de atualizacao, logs, prefetch) e e exatamente o que as
    /// ferramentas "Junk Cleaner" e "Evidence Remover" precisam apagar.
    /// Os caminhos sao RELATIVOS a pasta do Windows (Environment.SpecialFolder.Windows).
    /// A propria pasta "Windows" continua protegida — so as subpastas listadas
    /// sao liberadas.
    /// </summary>
    private static readonly string[] AllowedWindowsSubfolders =
    {
        @"Temp",
        @"Prefetch",
        @"Logs",
        @"Minidump",
        @"LiveKernelReports",
        @"Downloaded Program Files",
        @"SoftwareDistribution\Download",
        @"System32\winevt\Logs",
        @"Panther\Logs",
    };

    /// <summary>
    /// Verdadeiro quando a pasta e de sistema e nao pode ser removida.
    /// </summary>
    public static bool IsProtectedFolder(string folderPath)
    {
        var full = PathUtil.Normalize(folderPath);
        if (full.Length == 0) return true;

        // Nunca a raiz de uma unidade.
        try
        {
            var root = Path.GetPathRoot(full);
            if (!string.IsNullOrEmpty(root) && full.TrimEnd('\\').Length <= root.TrimEnd('\\').Length)
                return true;
        }
        catch { return true; }

        // Subpastas liberadas do Windows: verificadas ANTES do bloqueio geral,
        // senao a limpeza de cache do Windows Update seria sempre barrada.
        if (IsAllowedWindowsSubfolder(full)) return false;

        var windows = PathUtil.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        if (windows.Length > 0 && full.StartsWith(windows + "\\", StringComparison.OrdinalIgnoreCase))
            return true;

        var leaf = Path.GetFileName(full);
        return ProtectedLeafNames.Contains(leaf);
    }

    /// <summary>True quando a pasta esta dentro de uma das subpastas liberadas do Windows.</summary>
    private static bool IsAllowedWindowsSubfolder(string full)
    {
        var windows = PathUtil.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        if (windows.Length == 0) return false;

        foreach (var relative in AllowedWindowsSubfolders)
        {
            var candidate = PathUtil.Normalize(Path.Combine(windows, relative));
            if (candidate.Length == 0) continue;

            // A propria subpasta liberada conta (ex.: "C:\Windows\Temp"),
            #pragma warning disable CA1307 // StartsWith(string, StringComparison) exige cultura explicita
            if (string.Equals(full, candidate, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(candidate + "\\", StringComparison.OrdinalIgnoreCase))
                return true;
            #pragma warning restore CA1307
        }

        return false;
    }

    internal static (RegistryHive Hive, string SubPath) SplitRegistryPath(string path)
    {
        if (path.StartsWith(@"HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
            return (RegistryHive.CurrentUser, path[@"HKEY_CURRENT_USER".Length..].TrimStart('\\'));
        if (path.StartsWith(@"HKEY_CLASSES_ROOT", StringComparison.OrdinalIgnoreCase))
            return (RegistryHive.ClassesRoot, path[@"HKEY_CLASSES_ROOT".Length..].TrimStart('\\'));
        return (RegistryHive.LocalMachine, path[@"HKEY_LOCAL_MACHINE".Length..].TrimStart('\\'));
    }

    /// <summary>Nome canonico da raiz de registro, como exibido pelo regedit.</summary>
    public static string HiveName(RegistryHive hive) => hive switch
    {
        RegistryHive.LocalMachine => "HKEY_LOCAL_MACHINE",
        RegistryHive.CurrentUser => "HKEY_CURRENT_USER",
        RegistryHive.ClassesRoot => "HKEY_CLASSES_ROOT",
        RegistryHive.Users => "HKEY_USERS",
        RegistryHive.CurrentConfig => "HKEY_CURRENT_CONFIG",
        _ => "HKEY_LOCAL_MACHINE",
    };
}

/// <summary>Varre o sistema por restos de um programa removido.</summary>
public static class LeftoverScanner
{
    private static readonly string[] UninstallRoots =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    };

    private static readonly string[] NameRoots =
    {
        @"SOFTWARE",
        @"SOFTWARE\WOW6432Node",
    };

    private static readonly string[] FolderRoots =
    {
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Microsoft\Windows",
    };

    /// <summary>
    /// Variantes do nome do programa usadas para casar chaves e pastas.
    /// NUNCA inclui a editora: publishers como "Microsoft Corporation" casariam
    /// milhares de chaves alheias e a remocao massiva seria insegura.
    /// </summary>
    public static IReadOnlyList<string> NameVariants(InstalledProgram program)
    {
        var variants = new List<string>();
        var full = program.DisplayName.Trim();
        if (full.Length >= 3) variants.Add(full);

        // "Notepad++ (64-bit x64)" -> "Notepad++"; "DBeaver 26.2.0" -> "DBeaver"
        var trimmed = full;
        var paren = trimmed.IndexOf('(');
        if (paren > 0) trimmed = trimmed[..paren].Trim();
        var versioned = System.Text.RegularExpressions.Regex
            .Replace(trimmed, @"\s+[\d][\d\.\-]*$", "").Trim();
        if (versioned.Length >= 3 && !variants.Contains(versioned)) variants.Add(versioned);
        if (trimmed.Length >= 3 && !variants.Contains(trimmed)) variants.Add(trimmed);

        // Nomes muito genericos nao sao usados para casar (evita falsos positivos).
        var generic = new[] { "app", "tool", "setup", "installer", "program", "software", "update" };
        return variants
            .Where(v => v.Length >= 4 && !generic.Contains(v.ToLowerInvariant()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<LeftoverItem> Scan(InstalledProgram program, UninstallMode mode)
    {
        var items = new List<LeftoverItem>();

        // Modo "Somente": o desinstalador nativo faz o trabalho e nada mais
        // e procurado. Nenhuma sobra e reportada.
        if (!UninstallModes.Scans(mode)) return items;

        var all = NameVariants(program);
        // Safe usa apenas o nome completo; Moderate e Advanced ampliam.
        var variants = mode switch
        {
            UninstallMode.Safe => all.Take(1).ToList(),
            UninstallMode.Moderate => all.Take(2).ToList(),
            _ => all,
        };

        foreach (var variant in variants)
            ScanRegistry(variant, mode, items);

        foreach (var variant in variants)
            ScanFolders(variant, mode, items);

        // A propria pasta de instalacao e sempre candidata: o nome dela nem sempre
        // contem o nome do programa (ex.: "VS Revo Group" para "Revo Uninstaller Pro").
        foreach (var folder in CandidateInstallFolders(program))
        {
            try
            {
                if (!Directory.Exists(folder)) continue;
                items.Add(new LeftoverItem
                {
                    Kind = "Pasta",
                    Path = PathUtil.Normalize(folder),
                    Detail = "Pasta de instalação",
                    Confidence = LeftoverConfidence.High,
                    Selected = true,
                });
            }
            catch { }
        }

        // Deduplicacao canonica: a mesma pasta/registro descoberta por caminhos
        // diferentes (ex.: "C:\App" e "C:\App\") aparece uma unica vez.
        return Deduplicate(items);
    }

    /// <summary>
    /// Varredura forçada a partir de um caminho (arquivo ou pasta), sem depender
    /// de chave de desinstalacao no registro. E o "Forced Uninstall" do Revo:
    /// o usuario informa o executavel ou a pasta e o Tranal procura tudo que
    /// estiver relacionado ao nome informado.
    /// </summary>
    public static List<LeftoverItem> ScanForced(string path, UninstallMode mode)
    {
        var full = PathUtil.Normalize(path);
        if (full.Length == 0) return new List<LeftoverItem>();

        // No modo "Somente" nao ha varredura alguma — apenas o alvo informado.
        if (!UninstallModes.Scans(mode)) return new List<LeftoverItem>();

        string name;
        string installLocation;

        try
        {
            if (Directory.Exists(full))
            {
                name = Path.GetFileName(full.TrimEnd('\\'));
                installLocation = full;
            }
            else
            {
                name = Path.GetFileNameWithoutExtension(full);
                installLocation = Path.GetDirectoryName(full) ?? string.Empty;
            }
        }
        catch { return new List<LeftoverItem>(); }

        if (name.Length < 3) name = Path.GetFileName(full);

        var synthetic = new InstalledProgram
        {
            DisplayName = name,
            InstallLocation = installLocation,
        };

        var items = new List<LeftoverItem>(Scan(synthetic, mode));

        // O proprio alvo informado entra na lista (a protecao contra pastas de
        // sistema continua valendo em UninstallEngine.RemoveLeftovers).
        try
        {
            if (Directory.Exists(full))
            {
                items.Add(new LeftoverItem
                {
                    Kind = "Pasta",
                    Path = full,
                    Detail = "Alvo informado",
                    Confidence = LeftoverConfidence.High,
                    Selected = true,
                });
            }
            else if (File.Exists(full))
            {
                items.Add(new LeftoverItem
                {
                    Kind = "Arquivo",
                    Path = full,
                    Detail = "Alvo informado",
                    Confidence = LeftoverConfidence.High,
                    Selected = true,
                });
            }
        }
        catch { }

        return Deduplicate(items);
    }

    /// <summary>Deduplica por tipo + caminho canonico.</summary>
    private static List<LeftoverItem> Deduplicate(IEnumerable<LeftoverItem> items) =>
        items
            .Where(i => !string.IsNullOrWhiteSpace(i.Path))
            .GroupBy(i => i.Kind + "|" + PathUtil.Normalize(i.Path), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

    /// <summary>Pastas candidatas: a registrada e a pasta do executavel de desinstalacao.</summary>
    private static IEnumerable<string> CandidateInstallFolders(InstalledProgram program)    {
        var result = new List<string>();
        if (!string.IsNullOrWhiteSpace(program.InstallLocation))
            result.Add(program.InstallLocation.Trim().Trim('"'));

        var exe = ProgramScanner.ExtractExecutablePath(program.UninstallString);
        if (exe.Length > 0)
        {
            try
            {
                var dir = Path.GetDirectoryName(exe);
                if (!string.IsNullOrWhiteSpace(dir)) result.Add(dir);
            }
            catch { }
        }

        return result
            .Select(PathUtil.Normalize)
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static void ScanRegistry(string variant, UninstallMode mode, List<LeftoverItem> items)
    {
        // 1) Chaves de desinstalacao: o NOME da chave e um GUID, entao o casamento
        //    precisa ser pelo valor DisplayName dentro dela.
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                foreach (var root in UninstallRoots)
                {
                    try
                    {
                        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                        using var key = baseKey.OpenSubKey(root);
                        if (key is null) continue;

                        foreach (var name in key.GetSubKeyNames())
                        {
                            using var sub = key.OpenSubKey(name);
                            if (sub is null) continue;
                            var display = sub.GetValue("DisplayName")?.ToString();
                            if (display is null || !NameMatch.ContainsWord(display, variant)) continue;

                            items.Add(new LeftoverItem
                            {
                                Kind = "Registro",
                                Path = CanonicalRegistryPath(hive, root, name, view),
                                Detail = display,
                                Confidence = LeftoverConfidence.High,
                                Selected = true,
                            });
                        }
                    }
                    catch { }
                }
            }
        }

        // 2) Demais arvores: casamento pelo NOME da chave (evita varrer todo valor).
        //    No nivel Advanced tambem se olha o segundo nivel, onde ficam as chaves
        //    do proprio programa (ex.: SOFTWARE\VS Revo Group\Revo Uninstaller Pro).
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                foreach (var root in NameRoots)
                {
                    try
                    {
                        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                        using var key = baseKey.OpenSubKey(root);
                        if (key is null) continue;

                        foreach (var name in key.GetSubKeyNames())
                        {
                            if (NameMatch.ContainsWord(name, variant))
                                AddRegistryItem(hive, root, name, view, items, name,
                                    ConfidenceFor(name, variant));

                            if (mode != UninstallMode.Advanced) continue;

                            try
                            {
                                using var vendor = key.OpenSubKey(name);
                                if (vendor is null) continue;

                                foreach (var child in vendor.GetSubKeyNames())
                                {
                                    if (!NameMatch.ContainsWord(child, variant)) continue;
                                    AddRegistryItem(hive, root, $@"{name}\{child}", view, items, child,
                                        LeftoverConfidence.Low);
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
        }
    }

    /// <summary>
    /// Confianca de um item cujo nome casa com uma variante do programa.
    /// Nome exatamente igual e alta confianca; casamento parcial e media.
    /// </summary>
    private static LeftoverConfidence ConfidenceFor(string name, string variant) =>
        string.Equals(name, variant, StringComparison.OrdinalIgnoreCase)
            ? LeftoverConfidence.High
            : LeftoverConfidence.Medium;

    private static void AddRegistryItem(RegistryHive hive, string root, string relativePath,
        RegistryView view, List<LeftoverItem> items, string detail,
        LeftoverConfidence confidence)
    {
        var path = CanonicalRegistryPath(hive, root, relativePath, view);
        if (SafetyRules.IsProtectedRegistryKey(path)) return;

        items.Add(new LeftoverItem
        {
            Kind = "Registro",
            Path = path,
            Detail = detail,
            Confidence = confidence,
            Selected = confidence == LeftoverConfidence.High,
        });
    }

    /// <summary>
    /// Converte o caminho encontrado na visao de registro em um caminho canonico
    /// (o mesmo exibido pelo regedit). Sem isso, uma chave vista na visao de 32 bits
    /// seria removida da visao de 64 bits — ou seja, a chave ERRADA.
    /// </summary>
    internal static string CanonicalRegistryPath(RegistryHive hive, string root, string name, RegistryView view)
    {
        // O redirecionador WOW64 so existe sob HKLM\SOFTWARE: HKCU\Software e compartilhado.
        if (view == RegistryView.Registry32
            && hive == RegistryHive.LocalMachine
            && Environment.Is64BitOperatingSystem
            && root.StartsWith(@"SOFTWARE\", StringComparison.OrdinalIgnoreCase)
            && !root.StartsWith(@"SOFTWARE\WOW6432Node", StringComparison.OrdinalIgnoreCase))
        {
            root = @"SOFTWARE\WOW6432Node\" + root[@"SOFTWARE\".Length..];
        }

        return $@"{SafetyRules.HiveName(hive)}\{root}\{name}";
    }

    private static void ScanFolders(string variant, UninstallMode mode, List<LeftoverItem> items)
    {
        var roots = mode == UninstallMode.Advanced
            ? FolderRoots
            : FolderRoots.Take(4).ToArray();

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(dir);
                    if (NameMatch.StartsWithWord(name, variant))
                    {
                        AddFolderItem(dir, items, ConfidenceFor(name, variant));
                        continue;
                    }

                    // No modo Advanced o segundo nivel guarda a pasta real do programa
                    // (ex.: %LOCALAPPDATA%\VS Revo Group\Revo Uninstaller Pro).
                    if (mode != UninstallMode.Advanced) continue;

                    try
                    {
                        foreach (var sub in Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly))
                        {
                            if (NameMatch.StartsWithWord(Path.GetFileName(sub), variant))
                                AddFolderItem(sub, items, LeftoverConfidence.Low);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }

    private static void AddFolderItem(string dir, List<LeftoverItem> items, LeftoverConfidence confidence)
    {
        var full = PathUtil.Normalize(dir);
        if (SafetyRules.IsProtectedFolder(full)) return;

        items.Add(new LeftoverItem
        {
            Kind = "Pasta",
            Path = full,
            Detail = $"{ProgramScanner.MeasureFolder(dir) / 1024 / 1024} MB",
            Confidence = confidence,
            Selected = confidence == LeftoverConfidence.High,
        });
    }
}

/// <summary>Backup e restauracao de chaves de registro no formato do Revo.</summary>
public static class RegistryBackup
{
    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Tranal Group", "Tranal Uninstaller Pro");

    /// <summary>
    /// Cria o backup das chaves indicadas e devolve o caminho do arquivo .reg.
    /// O arquivo gerado e importavel por "reg import" (formato .reg UTF-16 com BOM).
    /// </summary>
    public static string Backup(IEnumerable<string> registryPaths, string? destination = null)
    {
        var dir = Path.Combine(DataDirectory, "RegBackup", "Last");
        Directory.CreateDirectory(dir);
        var file = destination ?? Path.Combine(dir, "TranalRegistryBackup.reg");

        var sb = new StringBuilder();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();

        var exported = new List<string>();
        foreach (var path in registryPaths)
        {
            if (TryExport(path, sb)) exported.Add(path);
        }

        File.WriteAllText(file, sb.ToString(), Encoding.Unicode);

        // Script de restauracao: importa o .reg gerado. Equivalente ao Restore.dat do Revo.
        var restore = Path.Combine(dir, "Restore.dat");
        File.WriteAllText(restore, BuildRestoreScript(file, exported), Encoding.ASCII);

        return file;
    }

    private static string BuildRestoreScript(string regFile, IEnumerable<string> exportedPaths)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("echo Restaurando o backup do Registro do Tranal Uninstaller...");
        sb.AppendLine($"reg import \"{regFile}\"");
        sb.AppendLine("echo Concluido.");
        return sb.ToString();
    }

    private static bool TryExport(string path, StringBuilder sb)
    {
        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(path);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(sub);
            if (key is null) return false;

            sb.AppendLine($"[{path}]");
            foreach (var valueName in key.GetValueNames())
            {
                var value = key.GetValue(valueName);
                if (value is null) continue;

                // "@" e o nome reservado do valor padrao (sem nome) no formato .reg.
                var label = valueName.Length == 0 ? "@" : valueName;
                sb.AppendLine($"\"{label}\"={FormatValue(value)}");
            }
            sb.AppendLine();

            foreach (var child in key.GetSubKeyNames())
                TryExport($"{path}\\{child}", sb);

            return true;
        }
        catch { return false; }
    }

    private static string FormatValue(object value) => value switch
    {
        int i => $"dword:{i:X8}",
        uint u => $"dword:{u:X8}",
        long l => $"\"qword\":{l:X16}",
        byte[] bytes => "hex(2):" + string.Join(",", bytes.Select(b => b.ToString("X2"))),
        string[] multi => "hex(7):" + string.Join(",", multi.SelectMany(s => Encoding.Unicode.GetBytes(s + "\0"))
                                                              .Select(b => b.ToString("X2"))),
        _ => $"\"{value}\"",
    };
}

/// <summary>Execucao do desinstalador nativo e criacao de ponto de restauracao.</summary>
public static class UninstallEngine
{
    /// <summary>Separa um comando de desinstalacao em executavel e argumentos.</summary>
    public static (string Exe, string Arguments) SplitCommand(string command)
    {
        var text = command.Trim();
        if (text.Length == 0) return (string.Empty, string.Empty);

        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            if (end > 0)
                return (text[1..end], text[(end + 1)..].Trim());
        }

        // Sem aspas: primeiro token e o executavel, o resto sao argumentos.
        // Cuida de casos como "C:\Program Files\App\uninst.exe" sem aspas,
        // em que o caminho tem espacos: procura o primeiro .exe/.com/.bat.
        var space = text.IndexOf(' ');
        if (space < 0) return (text, string.Empty);

        var head = text[..space];
        var exeExt = head.LastIndexOf('.');
        if (exeExt < 0) return (head, text[(space + 1)..].Trim());

        var firstArgs = text[(space + 1)..].Trim();
        var candidate = text[..(space + 1 + firstArgs.IndexOf(' ', StringComparison.Ordinal))]
            .TrimEnd();
        if (candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return (candidate, firstArgs);

        return (head, firstArgs);
    }

    /// <summary>Executa o comando de desinstalacao nativo e aguarda o processo.</summary>
    public static async Task<BuiltInUninstallResult> RunBuiltInUninstaller(InstalledProgram program)
    {
        var command = !string.IsNullOrWhiteSpace(program.QuietUninstallString)
            ? program.QuietUninstallString
            : program.UninstallString;

        if (string.IsNullOrWhiteSpace(command))
            return new BuiltInUninstallResult
            {
                ProgramName = program.DisplayName,
                Command = string.Empty,
                Started = false,
                Message = "o programa não possui desinstalador nativo",
            };

        var (exe, arguments) = SplitCommand(command);
        if (exe.Length == 0)
            return new BuiltInUninstallResult
            {
                ProgramName = program.DisplayName,
                Command = command,
                Started = false,
                Message = "comando de desinstalação inválido",
            };

        // Executaveis sem caminho completo (MsiExec.exe) sao resolvidos pelo sistema.
        var fullPath = exe.Contains('\\') ? exe : null;
        if (fullPath is not null && !File.Exists(fullPath))
            return new BuiltInUninstallResult
            {
                ProgramName = program.DisplayName,
                Command = command,
                Started = false,
                Message = $"executável não encontrado: {fullPath}",
            };

        var startInfo = new ProcessStartInfo(exe, arguments) { UseShellExecute = true };
        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
                return new BuiltInUninstallResult
                {
                    ProgramName = program.DisplayName,
                    Command = command,
                    Started = false,
                    Message = "o processo não pôde ser criado",
                };

            await process.WaitForExitAsync();
            return new BuiltInUninstallResult
            {
                ProgramName = program.DisplayName,
                Command = command,
                Started = true,
                ExitCode = process.ExitCode,
            };
        }
        catch (Exception ex)
        {
            return new BuiltInUninstallResult
            {
                ProgramName = program.DisplayName,
                Command = command,
                Started = false,
                Message = ex.Message,
            };
        }
    }

    /// <summary>Cria um ponto de restauracao do sistema (best effort, requer admin).</summary>
    public static bool TryCreateRestorePoint(string description)
    {
        try
        {
            var scope = new System.Management.ManagementScope(@"\\.\root\cimv2");
            scope.Connect();
            using var restore = new System.Management.ManagementClass(
                scope, new System.Management.ManagementPath("SystemRestore"), null);

            var parameters = restore.GetMethodParameters("Create");
            parameters["Description"] = description;
            parameters["RestorePointType"] = 12; // MODIFIED_SETTINGS

            var result = restore.InvokeMethod("Create", parameters, null);
            return Convert.ToUInt32(result["ReturnValue"]) == 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// Remove os restos selecionados e devolve o relatorio detalhado.
    /// Itens bloqueados pelas regras de seguranca sao marcados como Protected,
    /// nunca como remocao tentada.
    /// </summary>
    /// <param name="items">Todos os itens encontrados; apenas os marcados sao removidos.</param>
    /// <param name="options">Opcoes da remocao. Quando nulo, usa <see cref="UninstallRemovalOptions.Default"/>.</param>
    public static RemovalReport RemoveLeftovers(IEnumerable<LeftoverItem> items, UninstallRemovalOptions? options = null)
    {
        var opts = options ?? UninstallRemovalOptions.Default;
        var selected = items.Where(i => i.Selected).ToList();
        var outcomes = new List<RemovalOutcome>();

        // O backup e feito ANTES de qualquer remocao: e o que permite devolver
        // ao sistema tudo o que for excluido (equivalente ao Gerenciador de
        // Backups do Revo Uninstaller Pro).
        RestorePoint? point = null;
        if (opts.Backup && selected.Count > 0)
        {
            point = CreateRestorePoint(selected, opts);
        }

        foreach (var item in selected)
        {
            outcomes.Add(RemoveOne(item, opts));
        }

        return new RemovalReport(outcomes) { RestorePointId = point?.Id };
    }

    /// <summary>
    /// Cria o ponto de restauracao com o conteudo de tudo o que sera removido.
    /// O ponto so e criado quando pelo menos um item pode ser capturado.
    /// </summary>
    private static RestorePoint? CreateRestorePoint(List<LeftoverItem> selected, UninstallRemovalOptions options)
    {
        try
        {
            var name = string.IsNullOrWhiteSpace(options.RestorePointName)
                ? "Desinstalação"
                : options.RestorePointName;

            var operation = selected.All(i => i.Kind == "Registro") ? BackupManager.OperationRegistry
                : selected.All(i => i.Kind != "Registro") ? BackupManager.OperationFiles
                : BackupManager.OperationMixed;

            var point = BackupManager.Create(name, operation);

            foreach (var item in selected)
            {
                switch (item.Kind)
                {
                    case "Registro":
                        BackupManager.AddRegistryKey(point, item.Path);
                        break;
                    case "Arquivo":
                        BackupManager.AddFile(point, item.Path);
                        break;
                    case "Pasta":
                        BackupManager.AddFolder(point, item.Path);
                        break;
                }
            }

            BackupManager.Save(point);
            return point;
        }
        catch { return null; }
    }

    private static RemovalOutcome RemoveOne(LeftoverItem item, UninstallRemovalOptions options)
    {
        try
        {
            switch (item.Kind)
            {
                case "Pasta":
                {
                    var full = PathUtil.Normalize(item.Path);
                    if (SafetyRules.IsProtectedFolder(full))
                        return Fail(item, RemovalStatus.Protected, "pasta de sistema protegida");

                    if (!Directory.Exists(full))
                        return new RemovalOutcome { Item = item, Status = RemovalStatus.AlreadyAbsent, Message = "já não existia" };

                    // O Revo Pro envia as sobras de arquivos para a Lixeira.
                    if (options.UseRecycleBin && RecycleBin.CanAccept(full) && RecycleBin.Send(full))
                        return new RemovalOutcome { Item = item, Status = RemovalStatus.Removed, Message = "enviada para a Lixeira" };

                    try
                    {
                        Directory.Delete(full, true);
                        return new RemovalOutcome { Item = item, Status = RemovalStatus.Removed, Message = "removida" };
                    }
                    catch (Exception inner)
                    {
                        if (options.DeferToNextRestart && DeferredDelete.Schedule(full))
                            return new RemovalOutcome
                            {
                                Item = item,
                                Status = RemovalStatus.Deferred,
                                Message = "agendada para o próximo reinício do sistema",
                            };

                        return Fail(item, RemovalStatus.Failed, inner.Message);
                    }
                }

                case "Arquivo":
                {
                    var full = PathUtil.Normalize(item.Path);
                    var leaf = Path.GetFileName(full);
                    if (SafetyRules.IsProtectedFolder(Path.GetDirectoryName(full) ?? string.Empty))
                        return Fail(item, RemovalStatus.Protected, "caminho de sistema protegido");

                    if (!File.Exists(full))
                        return new RemovalOutcome { Item = item, Status = RemovalStatus.AlreadyAbsent, Message = "já não existia" };

                    if (options.UseRecycleBin && RecycleBin.CanAccept(full) && RecycleBin.Send(full))
                        return new RemovalOutcome { Item = item, Status = RemovalStatus.Removed, Message = "enviado para a Lixeira" };

                    try
                    {
                        File.Delete(full);
                        return new RemovalOutcome { Item = item, Status = RemovalStatus.Removed, Message = "removido" };
                    }
                    catch (Exception inner)
                    {
                        if (options.DeferToNextRestart && DeferredDelete.Schedule(full))
                            return new RemovalOutcome
                            {
                                Item = item,
                                Status = RemovalStatus.Deferred,
                                Message = "agendado para o próximo reinício do sistema",
                            };

                        return Fail(item, RemovalStatus.Failed, inner.Message);
                    }
                }

                case "Registro":
                {
                    if (SafetyRules.IsProtectedRegistryKey(item.Path))
                        return Fail(item, RemovalStatus.Protected, "chave de registro protegida");

                    var (hive, sub) = SafetyRules.SplitRegistryPath(item.Path);
                    using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                    using var key = baseKey.OpenSubKey(sub);
                    if (key is null)
                        return new RemovalOutcome { Item = item, Status = RemovalStatus.AlreadyAbsent, Message = "já não existia" };

                    baseKey.DeleteSubKeyTree(sub, throwOnMissingSubKey: false);
                    return new RemovalOutcome { Item = item, Status = RemovalStatus.Removed, Message = "removida" };
                }

                default:
                    return Fail(item, RemovalStatus.Failed, $"tipo desconhecido: {item.Kind}");
            }
        }
        catch (Exception ex)
        {
            return Fail(item, RemovalStatus.Failed, ex.Message);
        }
    }

    private static RemovalOutcome Fail(LeftoverItem item, RemovalStatus status, string message) =>
        new() { Item = item, Status = status, Message = message };
}
