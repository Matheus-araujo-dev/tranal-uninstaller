using System;
using System.Collections.Generic;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;

namespace Tranal.Cli;

/// <summary>
/// Retomar a desinstalacao interrompida.
///
/// Corresponde ao que o HiBit e o Uninstalr medem: quando o Windows reinicia
/// no meio da remocao, o trabalho que faltava pode ser retomado depois.
///
///   tranal /sessions            - lista as desinstalacoes interrompidas
///   tranal /resume "id"         - retoma uma delas
///   tranal /resume "id" /discard - descarta a retomada
/// </summary>
public static class ResumeCommands
{
    /// <summary>
    /// Tenta tratar um comando. Devolve null quando nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (flags.Contains("/sessions")) return RunList();
        if (flags.Contains("/resume")) return RunResume(args, flags);
        return null;
    }


    private static int RunList()
    {
        var pendentes = UninstallResume.Pending();

        if (pendentes.Count == 0)
        {
            Console.WriteLine("Nenhuma desinstalacao interrompida.");
            return 0;
        }

        Console.WriteLine($"Desinstalacoes interrompidas: {pendentes.Count}");
        Console.WriteLine();

        foreach (var pendente in pendentes)
        {
            Console.WriteLine($"    {pendente.Id}");
            Console.WriteLine($"        programa: {pendente.ProgramName}  ({pendente.Mode})");
            Console.WriteLine($"        etapas: {pendente.CompletedSteps}/{pendente.TotalSteps}");
            if (pendente.RemainingItems > 0)
                Console.WriteLine($"        itens que faltam sair: {pendente.RemainingItems}");
            if (pendente.NextStep.Length > 0)
                Console.WriteLine($"        proxima etapa: {pendente.NextStep}");
            Console.WriteLine();
        }

        Console.WriteLine("Use /resume \"id\" para retomar.");
        return 0;
    }

    private static int RunResume(string[] args, HashSet<string> flags)
    {
        var id = CliArgs.ValueAfter(args, "/resume");
        if (string.IsNullOrWhiteSpace(id))
        {
            Console.Error.WriteLine("Informe a identidade da sessao (veja /sessions).");
            return 1;
        }

        var sessao = UninstallSession.Load(id.Trim());
        if (sessao is null)
        {
            Console.Error.WriteLine($"Sessao '{id}' nao encontrada.");
            return 1;
        }

        if (flags.Contains("/discard"))
        {
            var descartou = UninstallResume.Discard(sessao.Id);
            Console.WriteLine(descartou
                ? $"A retomada de '{sessao.ProgramName}' foi descartada."
                : "Nao foi possivel descartar a retomada.");
            return descartou ? 0 : 1;
        }

        var itens = UninstallResume.ReadItems(sessao);
        if (itens.Count == 0)
        {
            Console.Error.WriteLine($"A sessao '{sessao.Id}' nao tem itens para retomar.");
            return 1;
        }

        Console.WriteLine($"Retomando: {sessao.ProgramName}  ({itens.Count} item(ns))");
        Console.WriteLine();

        var opcoes = new UninstallRemovalOptions
        {
            Backup = true,
            UseRecycleBin = !flags.Contains("/nobackup"),
            DeferToNextRestart = true,
        };

        var relatorio = UninstallEngine.RemoveLeftovers(itens, opcoes);

        Console.WriteLine($"Removidos: {relatorio.RemovedCount}  "
            + $"Já inexistentes: {relatorio.AbsentCount}  "
            + $"Falhas: {relatorio.FailedCount}  "
            + $"Protegidos: {relatorio.ProtectedCount}  "
            + $"Adiados para o próximo reinício: {relatorio.DeferredCount}");

        // A sessao so e descartada quando nao sobrou nada: com itens
        // adiados para o proximo reinicio, o trabalho ainda esta pela
        // metade e a retomada continua valendo.
        if (relatorio.FailedCount == 0 && relatorio.DeferredCount == 0 && relatorio.ProtectedCount == 0)
        {
            UninstallResume.Discard(sessao.Id);
            Console.WriteLine("A desinstalação foi concluída.");
        }
        else
        {
            Console.WriteLine($"A retomada continua pendente. Sessão: {sessao.Id}");
        }

        return relatorio.FailedCount > 0 ? 1 : 0;
    }
}
