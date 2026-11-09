using System;
using System.Linq;
using Tranal.Uninstaller.Core.Recovery;

namespace Tranal.Cli;

/// <summary>
/// Os comandos dos snapshots (a pasta de backups das sobras).
///
///   tranal /snapshots            - os backups que existem
///   tranal /snapshots /prune     - aplica o limite de quantidade e tamanho
///
/// A REGRA mora no nucleo: cada comando aqui consome o veredito de uma
/// classe do <c>Core</c> e nao repete a regra de negocio.
/// </summary>
public static class SnapshotCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/snapshots")) return Executar(args);
        return null;
    }

    /// <summary>
    /// Os backups e o limite da pasta.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    private static int Executar(string[] args)
    {
        var podar = CliArgs.Has(args.ToHashSet(StringComparer.OrdinalIgnoreCase), "/prune");

        if (podar)
        {
            var resultado = SnapshotLimits.Enforce();
            Console.WriteLine($"Removidos: {resultado.RemovedCount} "
                            + $"(liberados: {resultado.FreedBytes / 1024 / 1024} MB)");
            Console.WriteLine($"Restantes: {resultado.TotalCount} snapshot(s), "
                            + $"{resultado.TotalBytes / 1024 / 1024} MB");
            return 0;
        }

        var snapshots = LeftoverBackup.List();
        var tamanhos = SnapshotLimits.Measure();

        Console.WriteLine($"Snapshots: {snapshots.Count} "
                        + $"({tamanhos.Sum(t => t.Bytes) / 1024 / 1024} MB)");
        Console.WriteLine();

        foreach (var tamanho in tamanhos)
        {
            Console.WriteLine($"{tamanho.CreatedUtc:yyyy-MM-dd HH:mm}  {tamanho.Id,-40} "
                            + $"{tamanho.Bytes / 1024} KB");
        }

        return 0;
    }
}
