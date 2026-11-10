using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Management;

namespace Tranal.Cli;

/// <summary>
/// O comando "/remove-entries": tira entradas da lista "Adicionar e Remover
/// Programas" do Windows, sem desinstalar nada.
///
/// E a acao "Batch Remove Entries" do Geek Uninstaller e o "Remove entry" do
/// HiBit, do Wise e do UNINSTALLTOOL. O que se apaga e a chave de
/// desinstalacao — o programa continua instalado.
///
/// A REGRA mora em <see cref="WindowsListEntry.RemoveMany"/>: a CLI e a
/// interface grafica removem pelo mesmo codigo, e nao podem divergir.
/// </summary>
public static class EntryCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando o comando nao foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (!flags.Contains("/remove-entries")) return null;
        return Run(args, flags);
    }

    /// <summary>
    /// Executa a remocao das entradas pedidas.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    private static int Run(string[] args, HashSet<string> flags)
    {
        var pedidos = Valores(args);
        if (pedidos.Count == 0)
        {
            Console.Error.WriteLine("Informe o programa (ou a chave de registro) cujas entradas serao removidas.");
            return 1;
        }

        var instalados = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = false,

            // Uma entrada sem desinstalador e exatamente o caso em que o
            // usuario quer a entrada removida: e uma sobra do Windows.
            RequireUninstallString = false,
            IncludeEntriesWithoutUninstaller = true,
        });

        var alvos = new List<InstalledProgram>();
        var naoEncontrados = new List<string>();

        foreach (var pedido in pedidos)
        {
            if (EhCaminhoDeRegistro(pedido))
            {
                alvos.Add(new InstalledProgram
                {
                    DisplayName = pedido,
                    RegistryPath = pedido,
                });
                continue;
            }

            var achados = instalados
                .Where(p => p.DisplayName.Contains(pedido, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (achados.Count == 0)
            {
                naoEncontrados.Add(pedido);
                continue;
            }

            alvos.AddRange(achados);
        }

        foreach (var ausente in naoEncontrados)
            Console.Error.WriteLine($"Nao encontrei nenhum programa instalado com o nome '{ausente}'.");

        if (alvos.Count == 0) return 1;

        Console.WriteLine("Entradas que serao removidas da lista do Windows (o programa NAO e desinstalado):");
        foreach (var alvo in alvos)
            Console.WriteLine($"    {alvo.DisplayName}  [{alvo.RegistryPath}]");
        Console.WriteLine();

        if (!Confirmar($"Remover {alvos.Count} entrada(s) da lista do Windows?", flags))
        {
            Console.WriteLine("Cancelado. Nada foi removido.");
            return 1;
        }

        var relatorio = WindowsListEntry.RemoveMany(alvos);

        Console.WriteLine($"Removidas: {relatorio.RemovedCount}  "
            + $"Ja inexistentes: {relatorio.AbsentCount}  "
            + $"Protegidas: {relatorio.ProtectedCount}  "
            + $"Com falha: {relatorio.FailedCount}");

        foreach (var resultado in relatorio.Outcomes.Where(o => o.Status != RemovalStatus.Removed))
            Console.WriteLine($"    {resultado.Status}: {resultado.Message}");

        return relatorio.FailedCount > 0 || relatorio.ProtectedCount > 0 ? 1 : 0;
    }

    /// <summary>
    /// Os valores que vem depois da opcao, ate a proxima opcao.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    private static List<string> Valores(string[] args)
    {
        var resultado = new List<string>();
        if (args is null) return resultado;

        var indice = Array.FindIndex(
            args, a => a.Equals("/remove-entries", StringComparison.OrdinalIgnoreCase));
        if (indice < 0) return resultado;

        for (var i = indice + 1; i < args.Length; i++)
        {
            if (args[i].StartsWith("/", StringComparison.Ordinal)) break;
            resultado.Add(args[i]);
        }

        return resultado;
    }

    /// <summary>
    /// True quando o texto pedido e um caminho de registro, e nao um nome.
    /// </summary>
    /// <param name="texto">O texto pedido na linha de comando.</param>
    private static bool EhCaminhoDeRegistro(string texto) =>
        texto.Contains('\\') || texto.StartsWith("HKEY_", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Pede confirmacao explicita no console. A flag "/yes" dispensa a pergunta.
    /// </summary>
    /// <param name="pergunta">A pergunta feita ao usuario.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    private static bool Confirmar(string pergunta, HashSet<string> flags)
    {
        if (flags.Contains("/yes")) return true;

        Console.Write($"{pergunta} (sim/nao): ");
        var answer = Console.ReadLine();
        if (answer is null) return false;

        // A resposta pode chegar com a marca de ordem de bytes (BOM) do UTF-8
        // no inicio, quando o texto vem de um script ou de outro processo.
        var value = answer.Trim().Trim('\uFEFF').ToLowerInvariant();
        return value is "s" or "sim" or "y" or "yes";
    }
}
