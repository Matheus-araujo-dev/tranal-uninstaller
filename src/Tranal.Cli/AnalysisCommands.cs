using System;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Tracing;

namespace Tranal.Cli;

/// <summary>
/// O comando da analise profunda.
///
///   tranal /deep-analysis "nome do programa"
///
/// A REGRA mora no nucleo: o comando consome o veredito de
/// <see cref="ForensicAnalyzer"/> e nao repete a regra de negocio.
/// </summary>
public static class AnalysisCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/deep-analysis")) return Executar(args);
        return null;
    }

    /// <summary>
    /// Analisa um programa instalado e mostra o que o Tranal sabe sobre ele.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    private static int Executar(string[] args)
    {
        var nome = CliArgs.ValueAfter(args, "/deep-analysis");
        if (string.IsNullOrWhiteSpace(nome))
        {
            Console.WriteLine("Uso: tranal /deep-analysis \"nome do programa\"");
            return 2;
        }

        var instalados = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = false,
        });

        var alvo = instalados.FirstOrDefault(p =>
            p.DisplayName.Contains(nome.Trim(), StringComparison.OrdinalIgnoreCase));

        if (alvo is null)
        {
            Console.WriteLine($"Nenhum programa instalado com '{nome}'.");
            return 1;
        }

        var report = ForensicAnalyzer.Analyze(alvo);

        Console.WriteLine($"{report.ProgramName}: {report.Entries.Count} entrada(s)");
        Console.WriteLine($"  medidas:   {report.MeasuredCount}");
        Console.WriteLine($"  inferidas: {report.InferredCount}");
        Console.WriteLine($"  candidatas:{report.CandidateCount}");
        Console.WriteLine();

        foreach (var entrada in report.Entries)
        {
            Console.WriteLine($"{entrada.Kind,-10} {entrada.Provenance,-9} {entrada.Path}");
        }

        return 0;
    }
}
