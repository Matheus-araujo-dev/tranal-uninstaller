using System;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Cli;

/// <summary>
/// Os comandos de energia e de hibernacao.
///
///   tranal /power suspend 60        - suspender a maquina em 60 segundos
///   tranal /power shutdown 60      - desligar em 60 segundos
///   tranal /power restart 60       - reiniciar em 60 segundos
///   tranal /power hibernate 60     - hibernar em 60 segundos
///   tranal /power cancel           - cancelar a acao agendada
///   tranal /hibernate "programa"   - colocar o programa para dormir
///   tranal /wake "programa"        - acordar o programa
///
/// A REGRA mora no nucleo: cada comando aqui consome o veredito de uma
/// classe do <c>Core</c> e nao repete a regra de negocio.
/// </summary>
public static class PowerCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/power")) return ExecutarEnergia(args);
        if (CliArgs.Has(flags, "/hibernate")) return ExecutarDormir(args, false);
        if (CliArgs.Has(flags, "/wake")) return ExecutarDormir(args, true);
        return null;
    }

    /// <summary>
    /// As acoes de energia.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    private static int ExecutarEnergia(string[] args)
    {
        var acao = args.FirstOrDefault(a =>
            a.Equals("shutdown", StringComparison.OrdinalIgnoreCase)
            || a.Equals("restart", StringComparison.OrdinalIgnoreCase)
            || a.Equals("suspend", StringComparison.OrdinalIgnoreCase)
            || a.Equals("hibernate", StringComparison.OrdinalIgnoreCase)
            || a.Equals("logoff", StringComparison.OrdinalIgnoreCase));

        if (acao is null || acao.Equals("cancel", StringComparison.OrdinalIgnoreCase))
        {
            var pendente = PowerPlanner.Pending;
            if (pendente is null)
            {
                Console.WriteLine("Nenhuma acao de energia agendada.");
                return 0;
            }

            PowerPlanner.Cancel();
            Console.WriteLine($"Acao cancelada: {pendente}.");
            return 0;
        }

        var segundos = 60;
        var indice = Array.FindIndex(args, a => a.Equals(acao, StringComparison.OrdinalIgnoreCase));
        if (indice >= 0 && indice + 1 < args.Length
            && int.TryParse(args[indice + 1], out var informado))
        {
            segundos = informado;
        }

        var alvo = acao.ToLowerInvariant() switch
        {
            "shutdown" => PowerAction.Desligar,
            "restart" => PowerAction.Reiniciar,
            "suspend" => PowerAction.Suspender,
            "hibernate" => PowerAction.Hibernar,
            _ => PowerAction.SairDaSessao,
        };

        var ok = PowerPlanner.Plan(alvo, segundos);
        Console.WriteLine(ok
            ? $"Agendado: {PowerPlanner.Describe(alvo, segundos)}."
            : "Nao foi possivel agendar a acao.");

        return ok ? 0 : 1;
    }

    /// <summary>
    /// Colocar o programa para dormir ou acorda-lo.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="acordar">True para acordar, false para dormir.</param>
    private static int ExecutarDormir(string[] args, bool acordar)
    {
        var nome = args.FirstOrDefault(a =>
            !a.StartsWith("/", StringComparison.Ordinal) && !a.StartsWith("-", StringComparison.Ordinal));

        if (string.IsNullOrWhiteSpace(nome))
        {
            Console.WriteLine(acordar
                ? "Uso: tranal /wake \"nome do programa\""
                : "Uso: tranal /hibernate \"nome do programa\"");
            return 2;
        }

        if (acordar)
        {
            var (ok, mensagem) = ProgramHibernation.Wake(nome.Trim());
            Console.WriteLine(mensagem);
            return ok ? 0 : 1;
        }

        var programas = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = false,
        });

        var alvo = programas.FirstOrDefault(p =>
            p.DisplayName.Contains(nome.Trim(), StringComparison.OrdinalIgnoreCase));

        if (alvo is null)
        {
            Console.WriteLine($"Nenhum programa instalado com o nome '{nome}'.");
            return 1;
        }

        var (ok2, mensagem2) = ProgramHibernation.Hibernate(alvo);
        Console.WriteLine(mensagem2);
        return ok2 ? 0 : 1;
    }
}
