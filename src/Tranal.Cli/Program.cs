using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Tranal.Uninstaller.Core;

namespace Tranal.Cli;

/// <summary>
/// tranal.exe — linha de comando do Tranal Uninstaller.
/// Mantem o contrato do RevoCmd.exe:
///   tranal /m "programa" [/i] [/u] [/p] [/t]
///   tranal /winapps [/i] [/p]
///   tranal /?
/// E reproduz as operacoes do RevoUninPro.exe:
///   tranal /mu "programa" /mode safe|moderate|advanced
///   tranal /scan "programa" [/level ...] [/remove]
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // Marcador interno: o processo ja foi relancado com elevacao.
        var elevated = args.Contains("/elevated", StringComparer.OrdinalIgnoreCase);
        var effective = args.Where(a => !a.Equals("/elevated", StringComparison.OrdinalIgnoreCase)).ToArray();

        if (effective.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        var flags = effective.Select(a => a.ToLowerInvariant()).ToHashSet();
        if (flags.Contains("/?") || flags.Contains("/help") || flags.Contains("-h"))
        {
            PrintHelp();
            return 0;
        }

        // Varredura de sobras: /scan "programa" [/level safe|moderate|advanced] [/remove]
        if (flags.Contains("/scan"))
        {
            return RunScan(effective, flags, elevated);
        }

        // Desinstalacao completa: /mu "programa" [/mode ...] — paridade com RevoUninPro.exe /mu
        if (flags.Contains("/mu"))
        {
            return RunUninstall(effective, flags, elevated);
        }

        // Comandos com paridade de contrato com o RevoCmd/RevoUninPro:
        // /winapps, /wa, /be, /autorun, /tools, /export, /hunter, /lang, /theme
        var extra = ExtraCommands.TryRun(effective, flags);
        if (extra.HasValue) return extra.Value;

        var includeLocation = flags.Contains("/i");
        var includeUninstall = flags.Contains("/u");
        var asTable = flags.Contains("/t");
        var pause = flags.Contains("/p");

        string? pattern = null;
        for (var i = 0; i < effective.Length - 1; i++)
        {
            if (effective[i].Equals("/m", StringComparison.OrdinalIgnoreCase))
            {
                pattern = effective[i + 1];
                break;
            }
        }

        var options = new ProgramScanner.ScanOptions
        {
            // A CLI do Revo lista tambem os componentes de sistema (SystemComponent=1).
            IncludeSystemComponents = true,
            IncludeMsiProducts = false,
        };

        var programs = ProgramScanner.Scan(options);

        if (pattern is not null)
        {
            var regex = WildcardToRegex(pattern);
            programs = programs
                .Where(p => regex.IsMatch(p.DisplayName))
                .ToList();
        }

        if (asTable)
            PrintTable(programs, includeLocation, includeUninstall);
        else
            PrintPlain(programs, includeLocation, includeUninstall);

        if (pause) Pause();
        return 0;
    }

    /// <summary>
    /// Varre sobras de um programa. Sem /remove e apenas relatorio (dry-run).
    /// </summary>
    private static int RunScan(string[] args, HashSet<string> flags, bool elevated)
    {
        string? name = null;
        var level = UninstallMode.Moderate;
        var remove = flags.Contains("/remove");

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("/scan", StringComparison.OrdinalIgnoreCase))
            {
                name = args[i + 1];
                continue;
            }
            if (args[i].Equals("/level", StringComparison.OrdinalIgnoreCase))
            {
                level = ParseLevel(args[i + 1]);
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("Uso: tranal /scan \"nome do programa\" [/level safe|moderate|advanced] [/remove]");
            return 1;
        }

        // Remover chaves HKLM e arquivos em Program Files exige o token administrativo.
        if (remove && !elevated && !IsElevated())
        {
            Console.WriteLine("A remoção exige privilégios de administrador: relançando...");
            return RelaunchElevated(args);
        }

        var target = FindProgram(name);
        if (target is null)
        {
            Console.Error.WriteLine($"Programa não encontrado: {name}");
            return 1;
        }

        Console.WriteLine($"Alvo: {target.DisplayName}  ({target.VersionOrNoVersion})");
        Console.WriteLine($"Desinstalador: {target.UninstallString}");
        Console.WriteLine($"Nível: {level}");
        Console.WriteLine();

        var items = LeftoverScanner.Scan(target, level);
        if (items.Count == 0)
        {
            Console.WriteLine("Nenhuma sobra encontrada.");
            return 0;
        }

        foreach (var item in items)
            Console.WriteLine($"  [{item.Kind}] {item.Path}   ({item.Detail})");

        Console.WriteLine();
        Console.WriteLine($"Total: {items.Count} item(ns).");

        if (!remove)
        {
            Console.WriteLine("(dry-run: nada foi removido; use /remove para apagar)");
            return 0;
        }

        var report = UninstallEngine.RemoveLeftovers(items);
        Console.WriteLine();
        Console.WriteLine(report.Describe());
        Console.WriteLine();
        Console.WriteLine($"Removidos: {report.RemovedCount} | Já inexistentes: {report.AbsentCount} | "
                        + $"Falhas: {report.FailedCount} | Protegidos: {report.ProtectedCount}");

        return report.FailedCount + report.ProtectedCount == 0 ? 0 : 2;
    }

    /// <summary>
    /// Desinstalacao completa: desinstalador nativo -> varredura -> remocao.
    /// Equivale ao fluxo do assistente (5 etapas) nao-interativo.
    /// </summary>
    private static int RunUninstall(string[] args, HashSet<string> flags, bool elevated)
    {
        string? name = null;
        var level = UninstallMode.Moderate;
        var runBuiltIn = !flags.Contains("/nobuiltin");
        var remove = !flags.Contains("/noremove");

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("/mu", StringComparison.OrdinalIgnoreCase))
            {
                name = args[i + 1];
                continue;
            }
            if (args[i].Equals("/mode", StringComparison.OrdinalIgnoreCase))
            {
                level = ParseLevel(args[i + 1]);
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("Uso: tranal /mu \"nome do programa\" [/mode safe|moderate|advanced] "
                                  + "[/nobuiltin] [/noremove]");
            return 1;
        }

        if (!elevated && !IsElevated())
        {
            Console.WriteLine("A desinstalação exige privilégios de administrador: relançando...");
            return RelaunchElevated(args);
        }

        var target = FindProgram(name);
        if (target is null)
        {
            Console.Error.WriteLine($"Programa não encontrado: {name}");
            return 1;
        }

        Console.WriteLine($"Alvo: {target.DisplayName}  ({target.VersionOrNoVersion})");
        Console.WriteLine($"Nível de varredura: {level}");
        Console.WriteLine();

        var results = new List<BuiltInUninstallResult>();
        if (runBuiltIn)
        {
            Console.WriteLine("Executando o desinstalador nativo...");
            results.Add(UninstallEngine.RunBuiltInUninstaller(target).GetAwaiter().GetResult());
            foreach (var result in results) Console.WriteLine("  " + result.Describe());
            Console.WriteLine();
        }

        var items = LeftoverScanner.Scan(target, level);
        Console.WriteLine($"Sobras encontradas: {items.Count}");
        foreach (var item in items)
            Console.WriteLine($"  [{item.Kind}] {item.Path}   ({item.Detail})");
        Console.WriteLine();

        if (!remove)
        {
            Console.WriteLine("(dry-run: nada foi removido)");
            return 0;
        }

        var report = UninstallEngine.RemoveLeftovers(items);
        Console.WriteLine(report.Describe());
        Console.WriteLine();
        Console.WriteLine($"Removidos: {report.RemovedCount} | Já inexistentes: {report.AbsentCount} | "
                        + $"Falhas: {report.FailedCount} | Protegidos: {report.ProtectedCount}");

        UninstallHistory.Append(new UninstallHistoryEntry
        {
            Programs = target.DisplayName,
            Level = level.ToString(),
            BuiltIn = string.Join(" | ", results.Select(r => r.Describe())),
            Report = report.Describe(),
            Removed = report.RemovedCount,
            Failed = report.FailedCount + report.ProtectedCount,
        });

        return report.FailedCount + report.ProtectedCount == 0 ? 0 : 2;
    }

    private static InstalledProgram? FindProgram(string name)
    {
        var programs = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = true,
            IncludeMsiProducts = false,
        });

        return programs.FirstOrDefault(p =>
            p.DisplayName.Contains(name, StringComparison.CurrentCultureIgnoreCase))
            ?? programs.FirstOrDefault(p =>
                p.DisplayName.Equals(name, StringComparison.CurrentCultureIgnoreCase));
    }

    private static UninstallMode ParseLevel(string value) => value.ToLowerInvariant() switch
    {
        "builtin" => UninstallMode.BuiltIn,
        "safe" => UninstallMode.Safe,
        "advanced" => UninstallMode.Advanced,
        _ => UninstallMode.Moderate,
    };

    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    /// <summary>Relanca a propria CLI com o token administrativo (elevacao UAC).</summary>
    private static int RelaunchElevated(string[] args)
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return 3;

            var startInfo = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                Verb = "runas",
            };
            foreach (var arg in args) startInfo.ArgumentList.Add(arg);
            startInfo.ArgumentList.Add("/elevated");

            using var process = Process.Start(startInfo);
            if (process is null) return 3;

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine("A elevação foi cancelada ou negada: a remoção exige administrador.");
            return 3;
        }
    }

    private static void PrintPlain(
        List<InstalledProgram> programs, bool includeLocation, bool includeUninstall)
    {
        foreach (var p in programs)
        {
            var line = $"{p.DisplayName} - {p.VersionOrNoVersion}";
            if (includeLocation && !string.IsNullOrWhiteSpace(p.InstallLocation))
                line += $" - {p.InstallLocation}";
            if (includeUninstall && !string.IsNullOrWhiteSpace(p.UninstallString))
                line += $" - {p.UninstallString}";
            Console.WriteLine(line);
            Console.WriteLine();
        }
    }

    private static void PrintTable(
        List<InstalledProgram> programs, bool includeLocation, bool includeUninstall)
    {
        var headers = new List<string> { "Program", "Version" };
        if (includeLocation) headers.Add("Location");
        if (includeUninstall) headers.Add("Uninstall");
        Console.WriteLine(string.Join("\t", headers));
        foreach (var p in programs)
        {
            var cells = new List<string> { p.DisplayName, p.VersionOrNoVersion };
            if (includeLocation) cells.Add(p.InstallLocation);
            if (includeUninstall) cells.Add(p.UninstallString);
            Console.WriteLine(string.Join("\t", cells));
        }
    }

    /// <summary>Converte um padrao com curingas * e ? para regex.</summary>
    internal static Regex WildcardToRegex(string pattern)
    {
        var escaped = System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".");
        return new Regex($"^{escaped}$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.Singleline);
    }

    private static void Pause()
    {
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey(true);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("tranal v1.0 - List installed programs on local computer");
        Console.WriteLine("Tranal Uninstaller");
        Console.WriteLine();
        Console.WriteLine("Usage: tranal /m \"program name\" [/i] [/u] [/p] [/t]");
        Console.WriteLine("/m - Match program name using wildcard characters - * and ?");
        Console.WriteLine("     The asterisk (*) wildcard represents any collection of characters");
        Console.WriteLine("     The question mark (?) wildcard represents any single character");
        Console.WriteLine("     Example: tranal /m Microso*");
        Console.WriteLine("     Shows all programs beginning with the name Microso");
        Console.WriteLine();
        Console.WriteLine("/i - Include Install Location if exists");
        Console.WriteLine("/u - Include Uninstall command");
        Console.WriteLine("/p - Pause on each page");
        Console.WriteLine("/t - Print the results in a table");
        Console.WriteLine("/winapps - List installed Windows Apps");
        Console.WriteLine("/? | /help - This help");
        Console.WriteLine();
        Console.WriteLine("Operations:");
        Console.WriteLine("tranal /mu \"program name\" [/mode safe|moderate|advanced] [/nobuiltin] [/noremove]");
        Console.WriteLine("     Uninstall a program (built-in uninstaller, leftover scan and removal)");
        Console.WriteLine("tranal /scan \"program name\" [/level safe|moderate|advanced] [/remove]");
        Console.WriteLine("     List leftover files, folders and registry keys (dry-run unless /remove)");
        Console.WriteLine();
        Console.WriteLine("Modules and tools:");
        Console.WriteLine("tranal /winapps              - List installed Windows Apps");
        Console.WriteLine("tranal /wa \"App1;App2\"       - Uninstall Windows Apps by name");
        Console.WriteLine("tranal /be                   - List browser extensions");
        Console.WriteLine("tranal /be \"Ext1;Ext2\" /remove - Remove browser extensions by name");
        Console.WriteLine("tranal /autorun              - List auto-start items (run keys, services, tasks)");
        Console.WriteLine("tranal /tools                - List the native Windows tools");
        Console.WriteLine("tranal /export \"path\" [/txt|/html|/csv|/xml] - Export the program list");
        Console.WriteLine("tranal /hunter \"path\"       - Resolve a shortcut/executable to an installed program");
        Console.WriteLine("tranal /lang [code]          - Show or set the interface language");
        Console.WriteLine("tranal /theme [dark|light]   - Show or set the interface theme");
    }
}
