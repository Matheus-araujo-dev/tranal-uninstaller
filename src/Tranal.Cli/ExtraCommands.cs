using System.Text;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Cli;

/// <summary>
/// Comandos adicionales da CLI, com paridade de contrato com o Revo:
///
///   tranal /winapps                      - lista os aplicativos do Windows (RevoCmd /winapps)
///   tranal /wa "App1;App2"               - desinstala aplicativos do Windows (RevoUninPro /wa)
///   tranal /be                           - lista as extensoes de navegador (RevoUninPro /be)
///   tranal /be "Ext1;Ext2" /remove      - remove extensoes de navegador
///   tranal /autorun                      - lista os itens de inicializacao automatica
///   tranal /tools                        - lista as ferramentas nativas do Windows
///   tranal /export "caminho" [/txt|/html|/csv|/xml]  - exporta a listagem
///   tranal /hunter "caminho"             - resolve um icone para o programa instalado
///   tranal /lang "pt-BR"                 - define o idioma da interface
///   tranal /theme "dark|light"           - define o tema da interface
/// </summary>
public static class ExtraCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando nenhum dos comandos extras
    /// foi pedido (o fluxo normal segue no Program).
    /// </summary>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (flags.Contains("/winapps")) return RunWindowsApps(args, flags);
        if (flags.Contains("/wa")) return RunWindowsApps(args, flags, uninstall: true);
        if (flags.Contains("/be")) return RunBrowserExtensions(args, flags);
        if (flags.Contains("/autorun")) return RunAutorun();
        if (flags.Contains("/tools")) return RunWindowsTools();
        if (flags.Contains("/export")) return RunExport(args, flags);
        if (flags.Contains("/hunter")) return RunHunter(args);
        if (flags.Contains("/lang")) return RunLanguage(args);
        if (flags.Contains("/theme")) return RunTheme(args);
        if (flags.Contains("/new")) return RunNewPrograms();
        return null;
    }

    private static string? ValueAfter(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static string[] SplitNames(string raw) =>
        raw.Split(';')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

    // ------------------------------------------------------------------
    // Windows Apps
    // ------------------------------------------------------------------
    private static int RunWindowsApps(string[] args, HashSet<string> flags, bool uninstall = false)
    {
        var apps = WindowsApps.List();

        if (!uninstall)
        {
            Console.WriteLine($"Aplicativos do Windows instalados: {apps.Count}");
            Console.WriteLine();
            foreach (var app in apps)
                Console.WriteLine($"{app.Name} - {app.Version}");
            return 0;
        }

        var raw = ValueAfter(args, "/wa");
        if (string.IsNullOrWhiteSpace(raw))
        {
            Console.Error.WriteLine("Uso: tranal /wa \"App1;App2\"");
            return 1;
        }

        var wanted = SplitNames(raw);
        var removed = 0;
        var failed = 0;

        foreach (var name in wanted)
        {
            var target = apps.FirstOrDefault(a =>
                a.Name.Contains(name, StringComparison.CurrentCultureIgnoreCase));

            if (target is null)
            {
                Console.WriteLine($"Não encontrado: {name}");
                failed++;
                continue;
            }

            var ok = WindowsApps.Uninstall(target);
            Console.WriteLine($"{(ok ? "Removido" : "Falhou")}: {target.Name} ({target.PackageFullName})");
            if (ok) removed++; else failed++;
        }

        Console.WriteLine();
        Console.WriteLine($"Removidos: {removed} | Falhas: {failed}");
        return failed == 0 ? 0 : 2;
    }

    // ------------------------------------------------------------------
    // Extensoes de navegador
    // ------------------------------------------------------------------
    private static int RunBrowserExtensions(string[] args, HashSet<string> flags)
    {
        var extensions = BrowserExtensions.List();

        var raw = ValueAfter(args, "/be");
        var remove = flags.Contains("/remove") || flags.Contains("/rm");

        if (string.IsNullOrWhiteSpace(raw) || !remove)
        {
            Console.WriteLine($"Extensões de navegador encontradas: {extensions.Count}");
            Console.WriteLine();
            foreach (var extension in extensions)
                Console.WriteLine($"{extension.Name} - {extension.Version} ({extension.BrowserName}/{extension.Profile})");
            return 0;
        }

        var wanted = SplitNames(raw);
        var removed = 0;
        var failed = 0;

        foreach (var name in wanted)
        {
            var target = extensions.FirstOrDefault(e =>
                e.Name.Contains(name, StringComparison.CurrentCultureIgnoreCase));

            if (target is null)
            {
                Console.WriteLine($"Não encontrada: {name}");
                failed++;
                continue;
            }

            var ok = BrowserExtensions.Remove(target);
            Console.WriteLine($"{(ok ? "Removida" : "Falhou")}: {target.Name} ({target.BrowserName})");
            if (ok) removed++; else failed++;
        }

        Console.WriteLine();
        Console.WriteLine($"Removidas: {removed} | Falhas: {failed}");
        return failed == 0 ? 0 : 2;
    }

    // ------------------------------------------------------------------
    // Autorun
    // ------------------------------------------------------------------
    private static int RunAutorun()
    {
        var items = AutorunManager.Scan();
        Console.WriteLine($"Itens de inicialização automática: {items.Count}");
        Console.WriteLine();

        foreach (var item in items)
        {
            var state = item.Enabled ? "ativo " : "inativo";
            Console.WriteLine($"[{state}] {item.Name}");
            Console.WriteLine($"         {item.Source} :: {item.Command}");
        }

        return 0;
    }

    // ------------------------------------------------------------------
    // Ferramentas do Windows
    // ------------------------------------------------------------------
    private static int RunWindowsTools()
    {
        var tools = WindowsToolsCatalog.All;
        Console.WriteLine($"Ferramentas nativas do Windows: {tools.Count}");
        Console.WriteLine();

        foreach (var tool in tools)
            Console.WriteLine($"{tool.Name} ({tool.CategoryId}) -> {tool.Command} {tool.Arguments}");

        return 0;
    }

    // ------------------------------------------------------------------
    // Exportacao da listagem
    // ------------------------------------------------------------------
    private static int RunExport(string[] args, HashSet<string> flags)
    {
        var destination = ValueAfter(args, "/export");
        if (string.IsNullOrWhiteSpace(destination))
        {
            Console.Error.WriteLine("Uso: tranal /export \"caminho\" [/txt|/html|/csv|/xml]");
            return 1;
        }

        var programs = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = true,
            IncludeMsiProducts = false,
        });

        string content;
        if (flags.Contains("/html")) content = Exporters.ToHtml(programs);
        else if (flags.Contains("/csv")) content = Exporters.ToCsv(programs);
        else if (flags.Contains("/xml")) content = Exporters.ToXml(programs);
        else content = Exporters.ToText(programs);

        File.WriteAllText(destination, content, Encoding.UTF8);
        Console.WriteLine($"Lista exportada: {destination} ({programs.Count} entradas)");
        return 0;
    }

    // ------------------------------------------------------------------
    // Modo caçador
    // ------------------------------------------------------------------
    private static int RunHunter(string[] args)
    {
        var path = ValueAfter(args, "/hunter");
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.Error.WriteLine("Uso: tranal /hunter \"caminho\"");
            return 1;
        }

        var resolution = HunterMode.Resolve(path);

        if (!resolution.Found || resolution.Program is null)
        {
            Console.WriteLine($"Não foi possível associar o item a um programa instalado: {resolution.Message}");
            return 1;
        }

        Console.WriteLine($"Programa identificado: {resolution.Program.DisplayName}");
        Console.WriteLine($"Versão: {resolution.Program.VersionOrNoVersion}");
        Console.WriteLine($"Executável: {resolution.ResolvedPath}");
        Console.WriteLine($"Chave de registro: {resolution.Program.RegistryPath}");
        Console.WriteLine();
        Console.WriteLine($"Use: tranal /mu \"{resolution.Program.DisplayName}\" /mode advanced");
        return 0;
    }

    // ------------------------------------------------------------------
    // Grupo "Novos Programas"
    // ------------------------------------------------------------------
    /// <summary>
    /// Lista o grupo "Novos Programas" usando a MESMA cadeia de datas do
    /// Tranal (e, portanto, a mesma da GUI). Serve para conferir a paridade
    /// com o Revo sem depender de captura de tela.
    /// </summary>
    private static int RunNewPrograms()
    {
        var programs = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = false,
        });

        var days = Math.Max(1, AppSettingsStore.Current.NewProgramsDays);
        var baseDate = DateTime.Now.AddDays(-days);
        var cutoff = new DateTime(baseDate.Year, baseDate.Month, baseDate.Day);

        var novos = programs
            .Where(p => p.InstallDateValue is { } date && date >= cutoff)
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Console.WriteLine($"\"Novos Programas\" (janela de {days} dias, corte {cutoff:dd/MM/yyyy}): {novos.Count}");
        Console.WriteLine();
        foreach (var p in novos)
            Console.WriteLine($"  {p.DisplayName} - {p.InstallDate}");
        Console.WriteLine();
        Console.WriteLine($"\"Outros Programas\": {programs.Count - novos.Count}");
        return 0;
    }

    // ------------------------------------------------------------------
    // Idioma e tema
    // ------------------------------------------------------------------
    private static int RunLanguage(string[] args)
    {
        var code = ValueAfter(args, "/lang");
        if (string.IsNullOrWhiteSpace(code))
        {
            Console.WriteLine($"Idioma atual: {Loc.Current}");
            Console.WriteLine($"Disponíveis ({LanguageCatalog.Count}): " +
                              string.Join(", ", LanguageCatalog.All.Select(l => l.Code)));
            return 0;
        }

        if (!LanguageCatalog.Exists(code))
        {
            Console.Error.WriteLine($"Idioma desconhecido: {code}");
            return 1;
        }

        AppSettingsStore.Update(s => s.Language = code);
        Loc.SetLanguage(code);
        Console.WriteLine($"Idioma definido: {code} ({LanguageCatalog.NativeName(code)})");
        return 0;
    }

    private static int RunTheme(string[] args)
    {
        var theme = ValueAfter(args, "/theme");
        if (string.IsNullOrWhiteSpace(theme))
        {
            Console.WriteLine($"Tema atual: {AppSettingsStore.Current.Theme}");
            Console.WriteLine("Disponíveis: Dark, Light");
            return 0;
        }

        var normalized = theme.Trim().ToLowerInvariant() switch
        {
            "light" or "claro" or "classico" => "Light",
            _ => "Dark",
        };

        AppSettingsStore.Update(s => s.Theme = normalized);
        Console.WriteLine($"Tema definido: {normalized}");
        return 0;
    }
}
