using System;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.History;

namespace Tranal.Cli;

/// <summary>
/// Os comandos do historico de desinstalacoes.
///
///   tranal /history                       - o que saiu do computador
///   tranal /history /recover "id"         - devolve as sobras de uma operacao
///   tranal /history /clear                - apaga o historico
///
/// A REGRA mora no nucleo: o historico e lido e restaurado por
/// <see cref="HistoryReport"/>, que e o mesmo que a interface grafica usa.
/// O comando aqui nao decide nada — ele mostra e devolve o veredito.
/// </summary>
public static class HistoryCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (!CliArgs.Has(flags, "/history")) return null;

        if (CliArgs.Has(flags, "/clear"))
        {
            UninstallHistory.Clear();
            Console.WriteLine("Historico apagado.");
            return 0;
        }

        var id = CliArgs.ValueAfter(args, "/recover");
        if (!string.IsNullOrWhiteSpace(id))
        {
            var (ok, mensagem) = HistoryReport.Recover(id);
            Console.WriteLine(mensagem);
            return ok ? 0 : 1;
        }

        return Executar();
    }

    /// <summary>
    /// Lista o historico de desinstalacoes.
    /// </summary>
    public static int Executar()
    {
        var linhas = HistoryReport.Read();

        if (linhas.Count == 0)
        {
            Console.WriteLine("Nenhuma desinstalacao no historico.");
            return 0;
        }

        Console.WriteLine($"Historico: {linhas.Count} desinstalacao(oes)");
        Console.WriteLine();

        foreach (var linha in linhas)
        {
            var quando = linha.WhenExact.Length > 0 ? linha.WhenExact : linha.When;
            Console.WriteLine($"    {quando}  {linha.Programs}");

            var detalhe = $"        removidos: {linha.Removed} | falhas: {linha.Failed}";
            if (linha.ResidueCount > 0) detalhe += $" | residuos: {linha.ResidueCount}";
            if (linha.Recoverable) detalhe += $" | recuperavel ({linha.RecoveryId})";
            Console.WriteLine(detalhe);
        }

        return 0;
    }
}
