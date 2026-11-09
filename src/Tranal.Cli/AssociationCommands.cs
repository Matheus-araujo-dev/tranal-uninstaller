using System;
using System.Linq;
using Tranal.Uninstaller.Core.Recovery;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Cli;

/// <summary>
/// Os comandos do File Associator e do Centro de Mudancas.
///
///   tranal /file-assoc                          - as associacoes de arquivo
///   tranal /file-assoc /set ".pdf" "app.exe"   - define o programa da extensao
///   tranal /file-assoc /undo ".pdf"             - desfaz a ultima mudanca
///   tranal /undo-center                         - o que pode ser desfeito
///   tranal /undo-center /undo "id"              - desfaz
///
/// A REGRA mora no nucleo: cada comando aqui consome o veredito de uma
/// classe do <c>Core</c> e nao repete a regra de negocio.
/// </summary>
public static class AssociationCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/file-assoc")) return ExecutarAssociacoes(args);
        if (CliArgs.Has(flags, "/undo-center")) return ExecutarCentroDeMudancas(args);
        return null;
    }

    /// <summary>
    /// As associacoes de arquivo e as acoes sobre elas.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    private static int ExecutarAssociacoes(string[] args)
    {
        var definir = CliArgs.ValueAfter(args, "/set");
        var desfazer = CliArgs.ValueAfter(args, "/undo");

        if (definir is not null)
        {
            var extensao = definir.Trim();
            // "/set" vem seguido da extensao e do executavel.
            var indice = Array.FindIndex(args, a => a.Equals("/set", StringComparison.OrdinalIgnoreCase));
            var executavel = indice >= 0 && indice + 2 < args.Length ? args[indice + 2] : null;

            if (string.IsNullOrWhiteSpace(executavel))
            {
                Console.WriteLine("Uso: tranal /file-assoc /set \".pdf\" \"C:\\...\\app.exe\"");
                return 2;
            }

            var (ok, mensagem) = FileAssociator.SetDefault(extensao, executavel);
            Console.WriteLine(mensagem);
            return ok ? 0 : 1;
        }

        if (desfazer is not null)
        {
            var (ok, mensagem) = FileAssociator.Undo(desfazer.Trim());
            Console.WriteLine(mensagem);
            return ok ? 0 : 1;
        }

        var report = FileAssociator.Scan();

        Console.WriteLine($"Associacoes: {report.Items.Count}");
        Console.WriteLine($"Sem programa: {report.UnassociatedCount}");
        Console.WriteLine($"Quebradas:   {report.BrokenCount}");
        Console.WriteLine();

        foreach (var item in report.Items
                     .Where(i => !i.HasProgram || i.IsBroken)
                     .OrderBy(i => i.Extension, StringComparer.OrdinalIgnoreCase))
        {
            var estado = item.HasProgram ? item.ProgramName : "(sem programa)";
            Console.WriteLine($"{item.Extension,-10} {estado}");
        }

        return 0;
    }

    /// <summary>
    /// O centro de mudancas: o que o Tranal mudou e como voltar atras.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    private static int ExecutarCentroDeMudancas(string[] args)
    {
        var desfazer = CliArgs.ValueAfter(args, "/undo");

        if (desfazer is not null)
        {
            var (ok, mensagem) = UndoChangesCenter.Undo(desfazer.Trim());
            Console.WriteLine(mensagem);
            return ok ? 0 : 1;
        }

        var report = UndoChangesCenter.Scan();

        Console.WriteLine($"Mudancas: {report.Total} ({report.UndoneCount} ja desfeita(s))");
        Console.WriteLine();

        foreach (var item in report.Items)
        {
            Console.WriteLine(
                $"{item.Timestamp:yyyy-MM-dd HH:mm}  {item.ProgramName,-24} {item.Description} ({item.ItemCount} item(ns))");
        }

        return 0;
    }
}
