using System;
using System.Collections.Generic;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Modules;

namespace Tranal.Cli;

/// <summary>
/// Os comandos de log de instalacao rastreada.
///
/// Correspondem ao que o Revo Pro faz com a "Base de Logs": listar, ver as
/// entradas, renomear, mesclar, editar entrada a entrada e montar a revisao
/// do "Custom Uninstall" e do "Advanced Uninstall".
/// </summary>
public static class LogCommands
{
    /// <summary>
    /// Tenta tratar um comando de log. Devolve null quando nenhum comando de
    /// log foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (flags.Contains("/logs")) return RunList();
        if (flags.Contains("/log-entries")) return RunEntries(args);
        if (flags.Contains("/log-rename")) return RunRename(args);
        if (flags.Contains("/log-merge")) return RunMerge(args);
        if (flags.Contains("/log-ignore")) return RunEntryAction(args, ignore: true);
        if (flags.Contains("/log-remove")) return RunEntryAction(args, ignore: false);
        if (flags.Contains("/custom-uninstall")) return RunCustomUninstall(args);
        if (flags.Contains("/advanced-uninstall")) return RunAdvancedUninstall(args);
        return null;
    }

    private static string? ValueAfter(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static TracedInstall? Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        return TraceStore.List().FirstOrDefault(l =>
            l.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase) ||
            l.ProgramName.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static int RunList()
    {
        var logs = TraceStore.List();
        Console.WriteLine($"Logs de instalacao rastreada: {logs.Count}");
        Console.WriteLine();

        foreach (var log in logs)
        {
            Console.WriteLine($"{log.Id}  {log.ProgramName}");
            Console.WriteLine($"    arquivos: {log.FileCount}  pastas: {log.FolderCount}  chaves: {log.RegistryKeyCount}");
            Console.WriteLine($"    data: {log.Timestamp:yyyy-MM-dd HH:mm}  instalador: {log.InstallerPath}");
        }

        return 0;
    }

    private static int RunEntries(string[] args)
    {
        var id = ValueAfter(args, "/log-entries");
        var log = Find(id ?? string.Empty);

        if (log is null)
        {
            Console.Error.WriteLine("Log nao encontrado. Use /logs para ver a lista.");
            return 1;
        }

        var entries = LogOperations.Entries(log);
        Console.WriteLine($"Entradas do log {log.ProgramName}: {entries.Count}");
        Console.WriteLine();

        foreach (var entry in entries)
        {
            var marca = entry.Ignored ? " (ignorada)" : string.Empty;
            Console.WriteLine($"{entry.Id}  {entry.Kind}  {entry.Operation}  {entry.Confidence}{marca}");
            Console.WriteLine($"    {entry.Path}");
            if (!string.IsNullOrEmpty(entry.ValueName))
                Console.WriteLine($"    {entry.ValueName} = {entry.Value}");
        }

        return 0;
    }

    private static int RunRename(string[] args)
    {
        var id = ValueAfter(args, "/log-rename");
        var novoNome = ValueAfter(args, "/name");

        var log = Find(id ?? string.Empty);
        if (log is null)
        {
            Console.Error.WriteLine("Log nao encontrado. Use /logs para ver a lista.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(novoNome))
        {
            Console.Error.WriteLine("Uso: tranal /log-rename <id> /nome \"Novo nome\"");
            return 1;
        }

        if (!LogOperations.Rename(log, novoNome!))
        {
            Console.Error.WriteLine("Nao foi possivel renomear o log.");
            return 1;
        }

        Console.WriteLine($"Log renomeado para: {novoNome}");
        return 0;
    }

    private static int RunMerge(string[] args)
    {
        var ids = ValueAfter(args, "/log-merge");
        var novoNome = ValueAfter(args, "/name");

        if (string.IsNullOrWhiteSpace(ids) || string.IsNullOrWhiteSpace(novoNome))
        {
            Console.Error.WriteLine("Uso: tranal /log-merge \"id1;id2\" /nome \"Nome do log\"");
            return 1;
        }

        var logs = new List<TracedInstall>();
        foreach (var parte in ids!.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var log = Find(parte.Trim());
            if (log is null)
            {
                Console.Error.WriteLine($"Log nao encontrado: {parte.Trim()}");
                return 1;
            }

            logs.Add(log);
        }

        var merged = LogOperations.Merge(logs, novoNome!);
        if (merged is null)
        {
            Console.Error.WriteLine("Nao foi possivel mesclar os logs.");
            return 1;
        }

        Console.WriteLine($"Logs mesclados em: {merged.ProgramName} ({merged.Id})");
        return 0;
    }

    private static int RunEntryAction(string[] args, bool ignore)
    {
        var id = ValueAfter(args, ignore ? "/log-ignore" : "/log-remove");
        var entryId = ValueAfter(args, "/entrada");

        var log = Find(id ?? string.Empty);
        if (log is null)
        {
            Console.Error.WriteLine("Log nao encontrado. Use /logs para ver a lista.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(entryId))
        {
            Console.Error.WriteLine("Uso: tranal /log-ignore <id> /entrada <id da entrada>");
            return 1;
        }

        var entry = LogOperations.Entries(log).FirstOrDefault(e =>
            e.Id.Equals(entryId!.Trim(), StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            Console.Error.WriteLine("Entrada nao encontrada. Use /log-entries para ver a lista.");
            return 1;
        }

        var ok = ignore
            ? LogOperations.IgnoreEntry(log, entry)
            : LogOperations.RemoveEntry(log, entry);

        if (!ok)
        {
            Console.Error.WriteLine("Nao foi possivel gravar o log.");
            return 1;
        }

        Console.WriteLine(ignore ? "Entrada ignorada." : "Entrada removida do log.");
        return 0;
    }

    private static int RunCustomUninstall(string[] args)
    {
        var id = ValueAfter(args, "/custom-uninstall");
        var log = Find(id ?? string.Empty);

        if (log is null)
        {
            Console.Error.WriteLine("Log nao encontrado. Use /logs para ver a lista.");
            return 1;
        }

        var review = CustomUninstallReviewBuilder.Build(log);

        Console.WriteLine($"Custom Uninstall de {log.ProgramName}");
        Console.WriteLine($"itens: {review.Items.Count}  remover: {review.RemoveCount}  manter: {review.KeepCount}");
        Console.WriteLine();

        foreach (var item in review.Items)
        {
            var acao = item.WillRemove ? "REMOVER" : "manter ";
            Console.WriteLine($"{acao}  {item.Entry.Kind}  {item.Entry.Path}");
            Console.WriteLine($"        {item.Reason}");
        }

        return 0;
    }

    private static int RunAdvancedUninstall(string[] args)
    {
        var id = ValueAfter(args, "/advanced-uninstall");
        var log = Find(id ?? string.Empty);

        if (log is null)
        {
            Console.Error.WriteLine("Log nao encontrado. Use /logs para ver a lista.");
            return 1;
        }

        // Os candidatos sao as sobras do proprio programa: e o cruzamento do
        // log com a varredura que revela o que NAO foi registrado.
        var program = ProgramScanner.Scan(new ProgramScanner.ScanOptions { RequireUninstallString = false })
            .FirstOrDefault(p => p.DisplayName.Equals(log.ProgramName, StringComparison.CurrentCultureIgnoreCase));

        var leftovers = program is null
            ? new List<LeftoverItem>()
            : LeftoverScanner.Scan(program, LeftoverScanOptions.Default);
        var review = AdvancedUninstallReviewBuilder.Build(log, leftovers);

        Console.WriteLine($"Advanced Uninstall de {log.ProgramName}");
        Console.WriteLine($"fora do log: {review.UnloggedCount}  dentro do log: {review.Logged.Count}");
        Console.WriteLine();

        foreach (var candidate in review.Unlogged)
        {
            Console.WriteLine($"FORA DO LOG  {candidate.Item.Kind}  {candidate.Item.Path}");
            Console.WriteLine($"        {candidate.Detail}");
        }

        return 0;
    }
}
