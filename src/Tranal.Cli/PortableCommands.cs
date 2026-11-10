using System;
using System.Collections.Generic;
using System.IO;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Reporting;

namespace Tranal.Cli;

/// <summary>
/// O pacote portatil: levar para outra maquina o que o Tranal sabe do
/// sistema.
///
///   tranal /portable-export "pacote.zip"       - monta o pacote com os logs
///   tranal /portable-import "pacote.zip"       - traz o pacote de volta
///   tranal /portable-import-list "lista.csv"   - traz uma lista exportada
///
/// A REGRA esta no nucleo (<c>PortableArchive</c> e <c>ProgramListImporter</c>):
/// o manifesto e o que valida o <c>.zip</c>, e o casamento da lista com o que
/// esta instalado e feito pelo proprio nucleo. A linha de comando so pede e
/// mostra.
/// </summary>
public static class PortableCommands
{
    /// <summary>
    /// Tenta tratar um comando do pacote portatil. Devolve null quando
    /// nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/portable-export")) return RunExport(args);
        if (CliArgs.Has(flags, "/portable-import")) return RunImport(args);
        if (CliArgs.Has(flags, "/portable-import-list")) return RunImportList(args);
        return null;
    }

    private static int RunExport(string[] args)
    {
        var destino = CliArgs.ValueAfter(args, "/portable-export");
        if (string.IsNullOrWhiteSpace(destino))
        {
            Console.Error.WriteLine("Uso: tranal /portable-export \"pacote.zip\"");
            return 1;
        }

        var logs = TraceStore.List();
        var gravado = PortableArchive.Export(logs, destino!);

        if (string.IsNullOrWhiteSpace(gravado))
        {
            Console.Error.WriteLine("Nao foi possivel montar o pacote portatil.");
            return 1;
        }

        Console.WriteLine($"Pacote portatil gravado em: {gravado}");
        Console.WriteLine($"Logs no pacote: {logs.Count}");
        return 0;
    }

    private static int RunImport(string[] args)
    {
        var origem = CliArgs.ValueAfter(args, "/portable-import");
        if (string.IsNullOrWhiteSpace(origem))
        {
            Console.Error.WriteLine("Uso: tranal /portable-import \"pacote.zip\"");
            return 1;
        }

        if (!File.Exists(origem))
        {
            Console.Error.WriteLine($"Pacote nao encontrado: {origem}");
            return 1;
        }

        var resultado = PortableArchive.Import(origem!);

        if (resultado.Error.Length > 0)
        {
            Console.Error.WriteLine("Nao foi possivel importar o pacote: " + resultado.Error);
            return 1;
        }

        Console.WriteLine($"Logs importados: {resultado.Imported}");
        Console.WriteLine($"Logs recusados: {resultado.Skipped}");

        foreach (var log in resultado.Logs)
        {
            Console.WriteLine($"    {log.Id}  {log.ProgramName}");
        }

        foreach (var mensagem in resultado.Messages)
        {
            Console.WriteLine("    " + mensagem);
        }

        return 0;
    }

    private static int RunImportList(string[] args)
    {
        var origem = CliArgs.ValueAfter(args, "/portable-import-list");
        if (string.IsNullOrWhiteSpace(origem))
        {
            Console.Error.WriteLine("Uso: tranal /portable-import-list \"lista.csv\"");
            return 1;
        }

        if (!File.Exists(origem))
        {
            Console.Error.WriteLine($"Lista nao encontrada: {origem}");
            return 1;
        }

        var instalados = ProgramScanner.Scan(
            new ProgramScanner.ScanOptions { RequireUninstallString = false });

        var resultado = ProgramListImporter.Import(origem!, instalados);

        Console.WriteLine($"Entradas da lista: {resultado.Matched.Count + resultado.Unmatched.Count}");
        Console.WriteLine($"Casaram com o instalado: {resultado.Matched.Count}");
        Console.WriteLine($"Nao casaram: {resultado.Unmatched.Count}");
        Console.WriteLine();

        foreach (var item in resultado.Matched)
        {
            Console.WriteLine($"CASOU      {item.Name}  ->  {item.Program?.DisplayName ?? "(sem programa)"}");
        }

        foreach (var item in resultado.Unmatched)
        {
            Console.WriteLine($"NAO CASOU  {item.Name}  ({item.Reason})");
        }

        return 0;
    }
}
