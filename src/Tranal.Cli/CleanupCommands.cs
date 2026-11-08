using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Cli;

/// <summary>
/// Os comandos das ferramentas de limpeza e de diagnostico do Tranal:
///
///   tranal /unlock "C:\Programa\app.exe"          - mostra quem segura o arquivo
///   tranal /unlock "C:\Programa\app.exe" /force    - destrava o arquivo
///   tranal /unlock "..." /explorer                - destrava e reinicia o Explorer
///   tranal /internet                              - lista os dados dos navegadores
///   tranal /internet /clean                       - limpa os dados dos navegadores
///   tranal /internet /exception "site.com"        - protege um dominio (cookies seguros)
///   tranal /drive                                 - lista os arquivos desnecessarios
///   tranal /drive /clean                          - limpa os arquivos desnecessarios
///   tranal /drive /report "arquivo.html"          - grava o relatorio HTML
///   tranal /shortcuts                             - lista os atalhos quebrados
///   tranal /shortcuts /repair                     - repara os atalhos quebrados
///   tranal /shortcuts /remove                     - remove os atalhos quebrados
///   tranal /mru                                   - lista o cache de uso recente
///   tranal /mru /clean                            - limpa o cache de uso recente
///   tranal /shared-dlls                           - lista os componentes compartilhados
/// </summary>
public static class CleanupCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/unlock")) return RunUnlock(args, flags);
        if (CliArgs.Has(flags, "/internet")) return RunInternet(args, flags);
        if (CliArgs.Has(flags, "/drive")) return RunDrive(args, flags);
        if (CliArgs.Has(flags, "/shortcuts")) return RunShortcuts(args, flags);
        if (CliArgs.Has(flags, "/mru")) return RunMru(args, flags);
        if (CliArgs.Has(flags, "/shared-dlls")) return RunSharedDlls();
        return null;
    }

    // ------------------------------------------------------------------
    // Destravador
    // ------------------------------------------------------------------

    private static int RunUnlock(string[] args, HashSet<string> flags)
    {
        var caminho = CliArgs.ValueAfter(args, "/unlock");
        if (string.IsNullOrWhiteSpace(caminho))
        {
            Console.Error.WriteLine("Informe o arquivo a verificar.");
            return 1;
        }

        var item = Unlocker.Scan(caminho);
        if (item is null)
        {
            Console.WriteLine($"O arquivo '{caminho}' nao esta em uso.");
            return 0;
        }

        Console.WriteLine($"Em uso: {item.Path} ({ByteFormatter.Format(item.SizeBytes)})");

        if (item.Lockers.Count == 0)
        {
            Console.WriteLine("Nao foi possivel identificar quem segura o arquivo.");
        }
        else
        {
            foreach (var locker in item.Lockers)
            {
                Console.WriteLine($"    {locker.ProcessId,6}  {locker.ProcessName}");
            }
        }

        if (!CliArgs.Has(flags, "/force") && !CliArgs.Has(flags, "/explorer")) return 0;

        var reiniciar = CliArgs.Has(flags, "/explorer");
        var resultado = Unlocker.Unlock(caminho, reiniciar, reiniciar);

        Console.WriteLine(resultado.Message);

        foreach (var processo in resultado.TerminatedProcesses)
        {
            Console.WriteLine($"    encerrado: {processo}");
        }

        return resultado.Unlocked ? 0 : 1;
    }

    // ------------------------------------------------------------------
    // Limpador de internet
    // ------------------------------------------------------------------

    private static int RunInternet(string[] args, HashSet<string> flags)
    {
        var excecao = CliArgs.ValueAfter(args, "/exception");
        if (!string.IsNullOrWhiteSpace(excecao))
        {
            var added = InternetCleaner.AddException(excecao);
            Console.WriteLine(added
                ? $"Excecao adicionada: {excecao}"
                : $"O dominio '{excecao}' ja esta na lista de excecoes.");
            return 0;
        }

        var itens = InternetCleaner.Scan();

        if (!CliArgs.Has(flags, "/clean"))
        {
            Console.WriteLine($"Dados de navegador encontrados: {itens.Count}");
            Console.WriteLine();

            foreach (var item in itens)
            {
                var marca = item.IsProtected ? " [protegido]" : string.Empty;
                Console.WriteLine($"    {item.Browser,-10} {item.Kind,-12} {ByteFormatter.Format(item.SizeBytes),10}  {item.Path}{marca}");
            }

            return 0;
        }

        var marcados = itens.Where(i => i.Selected && !i.IsProtected).ToList();
        var relatorio = InternetCleaner.Clear(marcados);

        Console.WriteLine($"Removidos: {relatorio.Outcomes.Count(o => o.Removed)}");
        Console.WriteLine($"Adiados para o proximo reinicio: {relatorio.Outcomes.Count(o => o.Status == RemovalStatus.Deferred)}");
        Console.WriteLine($"Falhas: {relatorio.Outcomes.Count(o => o.Status == RemovalStatus.Failed)}");

        return 0;
    }

    // ------------------------------------------------------------------
    // Drive Cleaner
    // ------------------------------------------------------------------

    private static int RunDrive(string[] args, HashSet<string> flags)
    {
        var destino = CliArgs.ValueAfter(args, "/report");
        var itens = DriveCleaner.Scan();

        if (!string.IsNullOrWhiteSpace(destino))
        {
            var escrito = DriveCleaner.WriteReport(destino, itens, null);
            Console.WriteLine($"Relatorio gravado: {escrito}");
            return 0;
        }

        if (!CliArgs.Has(flags, "/clean"))
        {
            Console.WriteLine($"Arquivos desnecessarios encontrados: {itens.Count}");
            Console.WriteLine();

            foreach (var (categoria, bytes) in DriveCleaner.Summary(itens))
            {
                Console.WriteLine($"    {categoria,-40} {ByteFormatter.Format(bytes),12}");
            }

            return 0;
        }

        var relatorio = DriveCleaner.Clear(itens.Where(i => i.Selected).ToList());

        Console.WriteLine($"Removidos: {relatorio.Outcomes.Count(o => o.Removed)}");
        Console.WriteLine($"Adiados para o proximo reinicio: {relatorio.Outcomes.Count(o => o.Status == RemovalStatus.Deferred)}");
        Console.WriteLine($"Falhas: {relatorio.Outcomes.Count(o => o.Status == RemovalStatus.Failed)}");

        return 0;
    }

    // ------------------------------------------------------------------
    // Atalhos quebrados
    // ------------------------------------------------------------------

    private static int RunShortcuts(string[] args, HashSet<string> flags)
    {
        var instalados = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = false,
        });

        var problemas = ShortcutFixer.Scan(instalados);

        var reparar = CliArgs.Has(flags, "/repair");
        var remover = CliArgs.Has(flags, "/remove");

        if (!reparar && !remover)
        {
            Console.WriteLine($"Atalhos quebrados: {problemas.Count}");
            Console.WriteLine();

            foreach (var problema in problemas)
            {
                var reparavel = problema.CanRepair ? " [reparavel]" : string.Empty;
                Console.WriteLine($"    {problema.Kind,-14} {problema.Location,-22} {problema.ShortcutPath}{reparavel}");
            }

            return 0;
        }

        var reparados = 0;
        var removidos = 0;

        foreach (var problema in problemas)
        {
            var resultado = ShortcutFixer.Repair(problema, remover);
            if (resultado.Repaired) reparados++;
            if (resultado.Removed) removidos++;
        }

        if (remover && reparados == 0 && removidos == 0)
        {
            var relatorio = ShortcutFixer.Remove(problemas);
            Console.WriteLine($"Removidos: {relatorio.Outcomes.Count(o => o.Removed)}");
            return 0;
        }

        Console.WriteLine($"Reparados: {reparados}");
        Console.WriteLine($"Removidos: {removidos}");

        return 0;
    }

    // ------------------------------------------------------------------
    // Cache MRU
    // ------------------------------------------------------------------

    private static int RunMru(string[] args, HashSet<string> flags)
    {
        var itens = MruCleaner.Scan();

        if (!CliArgs.Has(flags, "/clean"))
        {
            Console.WriteLine($"Vestigios de uso recente: {itens.Count}");
            Console.WriteLine();

            foreach (var item in itens.Take(200))
            {
                Console.WriteLine($"    {item.RegistryPath}");
                Console.WriteLine($"        {item.ValueName} = {item.Content}");
            }

            if (itens.Count > 200) Console.WriteLine($"    ... e mais {itens.Count - 200} item(ns).");

            return 0;
        }

        var relatorio = MruCleaner.Clear(itens.Where(i => i.Selected).ToList());

        Console.WriteLine($"Removidos: {relatorio.Outcomes.Count(o => o.Removed)}");
        Console.WriteLine($"Falhas: {relatorio.Outcomes.Count(o => o.Status == RemovalStatus.Failed)}");

        return 0;
    }

    // ------------------------------------------------------------------
    // Componentes compartilhados
    // ------------------------------------------------------------------

    private static int RunSharedDlls()
    {
        var instalados = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = false,
        });

        var entradas = SharedDllCatalog.List(instalados);

        Console.WriteLine($"Componentes compartilhados: {entradas.Count}");
        Console.WriteLine();

        foreach (var entrada in entradas)
        {
            var usada = entrada.UsedBy.Count > 0
                ? string.Join(", ", entrada.UsedBy)
                : "(nenhum programa instalado usa)";

            Console.WriteLine($"    {entrada.Name,-34} refs {entrada.ReferenceCount,3}  {ByteFormatter.Format(entrada.SizeBytes),10}  {usada}");
        }

        return 0;
    }
}
