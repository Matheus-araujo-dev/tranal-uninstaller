using System;
using System.Linq;
using Tranal.Uninstaller.Core.Automation;

namespace Tranal.Cli;

/// <summary>
/// Os comandos do Instal guard e das falhas de desinstalacao.
///
///   tranal /installer-guard                    - as regras que reconhecem instaladores
///   tranal /installer-guard "C:\...\setup.exe" - analisa um instalador
///   tranal /failed-uninstalls                  - as desinstalacoes que falharam
///   tranal /failed-uninstalls /cleanup "Prog"   - limpa a entrada do registro e os arquivos
///
/// A REGRA mora no nucleo: cada comando aqui consome o veredito de uma
/// classe do <c>Core</c> e nao repete a regra de negocio.
/// </summary>
public static class GuardCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/installer-guard")) return ExecutarInstalGuard(args);
        if (CliArgs.Has(flags, "/failed-uninstalls")) return ExecutarFalhas(args);
        return null;
    }

    /// <summary>
    /// As regras do Instal guard e a analise de um instalador.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    private static int ExecutarInstalGuard(string[] args)
    {
        var alvo = args.FirstOrDefault(a =>
            !a.StartsWith("/", StringComparison.Ordinal) &&
            a.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(alvo))
        {
            var achado = InstallerGuard.Detect(alvo);

            Console.WriteLine($"Arquivo:   {achado.Path}");
            Console.WriteLine($"Tipo:      {achado.Family}");
            Console.WriteLine($"Regra:     {(achado.MatchedRule.Length == 0 ? "-" : achado.MatchedRule)}");
            Console.WriteLine($"Assinatura: {(achado.SignatureFound ? "sim" : "nao")}");
            Console.WriteLine($"Propriedades: {achado.PropertiesText}");
            return achado.Detected ? 0 : 1;
        }

        Console.WriteLine($"Regras: {InstallerGuard.Rules.Count} "
                        + $"({InstallerGuard.BuiltinRules.Count} embutida(s), "
                        + $"{InstallerGuard.CustomRules.Count} personalizada(s))");
        Console.WriteLine();

        foreach (var regra in InstallerGuard.Rules)
        {
            var criterio = regra.Signature.Length > 0
                ? $"assinatura: {regra.Signature}"
                : $"{regra.Property}: {regra.PropertyValue}";

            Console.WriteLine($"{regra.Family,-20} {regra.Name,-20} {criterio}");
        }

        return 0;
    }

    /// <summary>
    /// As desinstalacoes que falharam e a limpeza que o usuario pode pedir.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    private static int ExecutarFalhas(string[] args)
    {
        var limpeza = CliArgs.ValueAfter(args, "/cleanup");

        if (limpeza is not null)
        {
            var nome = limpeza.Trim();
            var falha = UninstallFailureRecovery.List()
                .FirstOrDefault(f => string.Equals(f.ProgramName, nome, StringComparison.OrdinalIgnoreCase));

            if (falha is null)
            {
                Console.WriteLine($"Nenhuma falha registrada para '{nome}'.");
                return 1;
            }

            var (ok, mensagem) = UninstallFailureRecovery.Cleanup(falha);
            Console.WriteLine(mensagem);
            return ok ? 0 : 1;
        }

        var falhas = UninstallFailureRecovery.List();

        if (falhas.Count == 0)
        {
            Console.WriteLine("Nenhuma desinstalacao falhou.");
            return 0;
        }

        Console.WriteLine($"Falhas registradas: {falhas.Count}");
        Console.WriteLine();

        foreach (var falha in falhas)
        {
            Console.WriteLine($"{falha.FailedAt:yyyy-MM-dd HH:mm}  {falha.ProgramName,-24} "
                            + $"codigo={falha.ExitCode}  {falha.Message}");
        }

        return 0;
    }
}
