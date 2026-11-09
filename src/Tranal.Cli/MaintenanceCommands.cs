using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Tools;
using Tranal.Uninstaller.Core.Tracing;

namespace Tranal.Cli;

/// <summary>
/// Os comandos de manutencao do Tranal:
///
///   tranal /bloatware                      - programas que vieram com o Windows
///   tranal /duplicates                     - instalacoes duplicadas
///   tranal /printers                       - impressoras e fila
///   tranal /dumps [/clean]                 - dumps de falha
///   tranal /browser-clean [/clean]         - limpeza do navegador
///   tranal /checksum "arquivo"             - MD5 e SHA256 de um arquivo
///   tranal /tasks                          - tarefas agendadas
///   tranal /install "instalador"           - instalacao silenciosa
/// </summary>
public static class MaintenanceCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/bloatware")) return RunBloatware();
        if (CliArgs.Has(flags, "/duplicates")) return RunDuplicates();
        if (CliArgs.Has(flags, "/printers")) return RunPrinters();
        if (CliArgs.Has(flags, "/dumps")) return RunDumps(flags);
        if (CliArgs.Has(flags, "/browser-clean")) return RunBrowserClean(flags);
        if (CliArgs.Has(flags, "/checksum")) return RunChecksum(args);
        if (CliArgs.Has(flags, "/tasks")) return RunTasks();
        if (CliArgs.Has(flags, "/install")) return RunInstall(args);
        return null;
    }

    private static int RunBloatware()
    {
        var instalados = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = false,
        });

        var encontrados = BundlewareDetector.Detect(instalados);

        Console.WriteLine($"Programas que vieram com o Windows: {encontrados.Count}");
        Console.WriteLine();

        foreach (var entrada in encontrados)
        {
            Console.WriteLine($"    {entrada.Program.DisplayName}");
            Console.WriteLine($"        {entrada.Category.Name} — {entrada.Category.Reason}");
        }

        return 0;
    }

    private static int RunDuplicates()
    {
        var instalados = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = false,
        });

        var grupos = DuplicateInstallDetector.Detect(instalados);

        Console.WriteLine($"Grupos de instalacao duplicada: {grupos.Count}");
        Console.WriteLine();

        foreach (var grupo in grupos)
        {
            Console.WriteLine($"    {grupo.Name} ({grupo.Count} copias)");
            foreach (var programa in grupo.Programs)
            {
                Console.WriteLine($"        {programa.InstallLocation}");
            }
        }

        return 0;
    }

    private static int RunPrinters()
    {
        var impressoras = PrinterEnumerator.ListWithQueue();

        Console.WriteLine($"Impressoras: {impressoras.Count}");
        Console.WriteLine();

        foreach (var impressora in impressoras)
        {
            var fila = impressora.QueuedJobs > 0 ? $"  fila: {impressora.QueuedJobs}" : string.Empty;
            Console.WriteLine($"    {impressora.Name}  [{impressora.Port}]{fila}");
        }

        var trabalhos = PrinterEnumerator.ListQueue();
        if (trabalhos.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"Trabalhos na fila: {trabalhos.Count}");
            foreach (var trabalho in trabalhos)
            {
                Console.WriteLine($"    {trabalho.Document}  ({trabalho.Owner})");
            }
        }

        return 0;
    }

    private static int RunDumps(HashSet<string> flags)
    {
        var dumps = CrashDumpCollector.List();

        if (!CliArgs.Has(flags, "/clean"))
        {
            Console.WriteLine($"Dumps de falha: {dumps.Count}");
            Console.WriteLine();

            foreach (var dump in dumps)
            {
                Console.WriteLine($"    {dump.Name,-46} {ByteFormatter.Format(dump.SizeBytes),10}  {dump.CreatedAt:yyyy-MM-dd}");
            }

            return 0;
        }

        var relatorio = CrashDumpCollector.Clear(dumps);

        Console.WriteLine($"Removidos: {relatorio.Outcomes.Count(o => o.Removed)}");
        Console.WriteLine($"Adiados para o proximo reinicio: {relatorio.Outcomes.Count(o => o.Status == RemovalStatus.Deferred)}");

        return 0;
    }

    private static int RunBrowserClean(HashSet<string> flags)
    {
        var orfas = BrowserCleanup.FindOrphanExtensions();

        Console.WriteLine($"Extensoes orfas: {orfas.Count}");
        foreach (var orfa in orfas)
        {
            Console.WriteLine($"    {orfa.Browser,-10} {orfa.Id}  {orfa.Path}");
        }

        if (CliArgs.Has(flags, "/clean") && orfas.Count > 0)
        {
            var resultado = BrowserCleanup.RemoveOrphanExtensions(orfas);
            Console.WriteLine();
            Console.WriteLine(resultado.Message);
        }

        if (CliArgs.Has(flags, "/reset"))
        {
            var reset = BrowserCleanup.ResetDefaults(null);
            Console.WriteLine();
            Console.WriteLine(reset.Message);
        }

        return 0;
    }

    private static int RunChecksum(string[] args)
    {
        var arquivo = CliArgs.ValueAfter(args, "/checksum");
        if (string.IsNullOrWhiteSpace(arquivo))
        {
            Console.Error.WriteLine("Informe o arquivo.");
            return 1;
        }

        var md5 = InstallChecksum.Md5Of(arquivo);
        var sha = InstallChecksum.Sha256Of(arquivo);

        if (md5.Length == 0)
        {
            Console.Error.WriteLine($"Nao foi possivel ler '{arquivo}'.");
            return 1;
        }

        Console.WriteLine($"MD5    {md5}");
        Console.WriteLine($"SHA256 {sha}");

        return 0;
    }

    private static int RunTasks()
    {
        var tarefas = ScheduledTaskVisibility.List(includeMicrosoft: false);

        Console.WriteLine($"Tarefas agendadas: {tarefas.Count}");
        Console.WriteLine($"(ocultas da Microsoft: {ScheduledTaskVisibility.HiddenCount()})");
        Console.WriteLine();

        foreach (var tarefa in tarefas)
        {
            Console.WriteLine($"    {tarefa.Name}");
            if (tarefa.Command.Length > 0) Console.WriteLine($"        {tarefa.Command} {tarefa.Arguments}");
        }

        return 0;
    }

    private static int RunInstall(string[] args)
    {
        var instalador = CliArgs.ValueAfter(args, "/install");
        if (string.IsNullOrWhiteSpace(instalador))
        {
            Console.Error.WriteLine("Informe o instalador.");
            return 1;
        }

        var log = CliArgs.ValueAfter(args, "/log");
        var resultado = SilentInstaller.Install(instalador, log);

        Console.WriteLine(resultado.Message);
        if (resultado.Output.Length > 0) Console.WriteLine(resultado.Output);

        return resultado.Success ? 0 : 1;
    }
}
