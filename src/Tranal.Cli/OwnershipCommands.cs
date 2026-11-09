using System;
using System.Linq;
using Tranal.Uninstaller.Core.Management;

namespace Tranal.Cli;

/// <summary>
/// O comando de propriedade dos arquivos.
///
///   tranal /ownership "C:\caminho"            - o estado da propriedade
///   tranal /ownership "C:\caminho" /take      - assume a propriedade
///   tranal /ownership "C:\caminho" /grant     - da controle total
///
/// A REGRA mora no nucleo: o comando consome o veredito de
/// <see cref="FileOwnership"/> e nao repete a regra de negocio.
/// </summary>
public static class OwnershipCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/ownership")) return Executar(args, flags);
        return null;
    }

    /// <summary>
    /// Le ou altera a propriedade de um caminho.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    private static int Executar(string[] args, HashSet<string> flags)
    {
        var caminho = CliArgs.ValueAfter(args, "/ownership");
        if (string.IsNullOrWhiteSpace(caminho))
        {
            Console.WriteLine("Uso: tranal /ownership \"C:\\caminho\" [/take] [/grant]");
            return 2;
        }

        if (CliArgs.Has(flags, "/take"))
        {
            var (ok, mensagem) = FileOwnership.TakeOwnership(caminho);
            Console.WriteLine(mensagem);
            return ok ? 0 : 1;
        }

        if (CliArgs.Has(flags, "/grant"))
        {
            var (ok, mensagem) = FileOwnership.GrantFullControl(caminho);
            Console.WriteLine(mensagem);
            return ok ? 0 : 1;
        }

        foreach (var item in FileOwnership.Scan(new[] { caminho }))
        {
            Console.WriteLine($"{item.Path}");
            Console.WriteLine($"  tipo:      {(item.IsDirectory ? "pasta" : "arquivo")}");
            Console.WriteLine($"  existe:    {(item.Exists ? "sim" : "nao")}");
            Console.WriteLine($"  grava:     {(item.CanWrite ? "sim" : "nao")}");
            Console.WriteLine($"  do sistema:{(item.IsSystem ? "sim" : "nao")}");
        }

        return 0;
    }
}
