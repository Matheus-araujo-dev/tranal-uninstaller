using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Updates;

namespace Tranal.Cli;

/// <summary>
/// Os comandos de atualizacao do Tranal:
///
///   tranal /updates                - lista as atualizacoes disponiveis dos programas
///   tranal /update-check           - verifica se ha versao nova do proprio Tranal
///
/// Os dois consomem o nucleo (<see cref="UpdateService"/>): a CLI nao monta
/// feed nem compara versao por conta propria.
/// </summary>
public static class UpdateCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/updates")) return RunUpdates();
        if (CliArgs.Has(flags, "/update-check")) return RunUpdateCheck();
        return null;
    }

    /// <summary>
    /// Lista as atualizacoes disponiveis dos programas instalados.
    /// </summary>
    private static int RunUpdates()
    {
        var instalados = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = true,
        });

        var atualizador = UpdateService.AtualizadorDeProgramas();
        var plano = atualizador.MontarPlanoAsync(instalados).GetAwaiter().GetResult();

        Console.WriteLine($"Atualizacoes disponiveis: {plano.Pending.Count} de {plano.Items.Count} item(ns)");
        Console.WriteLine($"Feed: {UpdateService.UrlDeProgramas()}");
        Console.WriteLine();

        foreach (var item in plano.Items)
        {
            var marca = item.Pending ? "+" : "-";
            Console.WriteLine($"  {marca} {item.Candidate.ProgramName}  ({item.Candidate.Publisher})");
            Console.WriteLine($"      instalada: {item.Candidate.InstalledVersion}   publicada: {item.Candidate.AvailableVersion}");

            if (!item.Pending)
            {
                Console.WriteLine($"      motivo: {item.Reason}");
            }
        }

        return 0;
    }

    /// <summary>
    /// Verifica se ha versao nova do proprio Tranal.
    /// </summary>
    private static int RunUpdateCheck()
    {
        var atual = UpdateService.VersaoAtual();
        Console.WriteLine($"Versao atual: {atual}");
        Console.WriteLine($"Feed: {UpdateService.UrlDoTranal()}");

        var resultado = UpdateService.VerificarTranalAsync().GetAwaiter().GetResult();

        if (!resultado.Success)
        {
            Console.WriteLine($"Falha: {resultado.ErrorMessage}");
            return 1;
        }

        var publicada = resultado.LatestVersion ?? string.Empty;
        Console.WriteLine($"Versao publicada: {(publicada.Length == 0 ? "(desconhecida)" : publicada)}");

        if (!string.IsNullOrWhiteSpace(resultado.ReleaseNotes))
        {
            Console.WriteLine();
            Console.WriteLine(resultado.ReleaseNotes);
        }

        if (!UpdateService.TemVersaoNova(atual, publicada))
        {
            Console.WriteLine();
            Console.WriteLine("O Tranal esta atualizado.");
            return 0;
        }

        Console.WriteLine();
        Console.WriteLine("Ha uma versao nova disponivel.");

        if (!string.IsNullOrWhiteSpace(resultado.DownloadUrl))
        {
            Console.WriteLine($"Download: {resultado.DownloadUrl}");
        }

        return 0;
    }
}
