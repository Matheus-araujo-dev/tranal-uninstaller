using System;
using System.Linq;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Management;

namespace Tranal.Cli;

/// <summary>
/// Os comandos das integracoes do Windows.
///
///   tranal /store-startup   - as StartupTask dos aplicativos da Store
///   tranal /msi-deps        - as dependencias entre pacotes MSI
///   tranal /appx            - o catalogo de pacotes Appx
///   tranal /system-dlls     - as DLLs em pastas de sistema
///   tranal /windows-updates  - as atualizacoes do Windows instaladas
///   tranal /windows-update-remove "KB5031356" - remove uma atualizacao
///
/// A REGRA mora no nucleo: cada comando aqui consome o veredito de uma
/// classe do <c>Core</c> e nao repete a regra de negocio.
/// </summary>
public static class IntegrationCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/store-startup")) return ExecutarStoreStartup();
        if (CliArgs.Has(flags, "/msi-deps")) return ExecutarMsiDeps();
        if (CliArgs.Has(flags, "/appx")) return ExecutarAppx();
        if (CliArgs.Has(flags, "/system-dlls")) return ExecutarSystemDlls();
        if (CliArgs.Has(flags, "/windows-updates")) return ExecutarWindowsUpdates();
        if (CliArgs.Has(flags, "/windows-update-remove")) return ExecutarWindowsUpdateRemove(args);
        return null;
    }

    /// <summary>
    /// As StartupTask dos aplicativos empacotados.
    /// </summary>
    private static int ExecutarStoreStartup()
    {
        var entradas = StoreStartupEntries.List();

        Console.WriteLine($"Tarefas de inicializacao da Store: {entradas.Count}");
        Console.WriteLine();

        foreach (var entrada in entradas)
        {
            Console.WriteLine($"{entrada.DisplayName,-28} {(entrada.Enabled ? "habilitada" : "desabilitada"),-12} "
                            + $"{entrada.PackageName}");
        }

        return 0;
    }

    /// <summary>
    /// As dependencias entre pacotes do Windows Installer.
    /// </summary>
    private static int ExecutarMsiDeps()
    {
        var dependencias = MsiDependencies.List();

        Console.WriteLine($"Dependencias entre pacotes MSI: {dependencias.Count}");
        Console.WriteLine();

        // A lista pode ter centenas de linhas: o total vem primeiro, e o
        // que importa (os primeiros) vem depois.
        foreach (var dependencia in dependencias.Take(30))
        {
            Console.WriteLine($"{dependencia.RequiredPackage,-44} e exigido por {dependencia.DependentPackage}");
        }

        if (dependencias.Count > 30)
        {
            Console.WriteLine($"... e mais {dependencias.Count - 30} dependencia(s).");
        }

        return 0;
    }

    /// <summary>
    /// O catalogo de pacotes Appx.
    /// </summary>
    private static int ExecutarAppx()
    {
        var pacotes = AppxCatalog.List();
        var preinstalados = pacotes.Count(p => p.NonRemovable);

        Console.WriteLine($"Pacotes Appx: {pacotes.Count} ({preinstalados} pre-instalado(s))");
        Console.WriteLine();

        foreach (var pacote in pacotes)
        {
            var origem = pacote.NonRemovable ? "sistema" : (pacote.IsFramework ? "framework" : "usuario");
            Console.WriteLine($"{pacote.DisplayName,-40} {pacote.Version,-12} {origem}");
        }

        return 0;
    }

    /// <summary>
    /// As DLLs em pastas de sistema.
    /// </summary>
    private static int ExecutarSystemDlls()
    {
        var dlls = SystemDllTracker.List();
        var candidatas = dlls.Count(d => !d.IsKnownDll);

        Console.WriteLine($"DLLs em pastas de sistema: {dlls.Count} ({candidatas} nao conhecidas pelo Windows)");
        Console.WriteLine();

        foreach (var dll in dlls.Where(d => !d.IsKnownDll).OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{dll.Name,-44} {dll.CompanyName}");
        }

        return 0;
    }

    /// <summary>
    /// As atualizacoes do Windows que estao instaladas.
    /// </summary>
    private static int ExecutarWindowsUpdates()
    {
        var atualizacoes = WindowsUpdateManager.ListInstalled();

        Console.WriteLine($"Atualizacoes do Windows instaladas: {atualizacoes.Count}");
        Console.WriteLine();

        foreach (var update in atualizacoes)
        {
            Console.WriteLine($"{update.KbArticles,-22} {update.Title}");
        }

        return 0;
    }

    /// <summary>
    /// Remove uma atualizacao do Windows.
    ///
    /// A remocao e do Windows e nao do Tranal: o comando e o
    /// <c>wusa.exe</c>, e o que o Windows responde e o que sai aqui.
    /// </summary>
    private static int ExecutarWindowsUpdateRemove(string[] args)
    {
        var artigo = CliArgs.ValueAfter(args, "/windows-update-remove");
        if (string.IsNullOrWhiteSpace(artigo))
        {
            Console.Error.WriteLine("Uso: tranal /windows-update-remove KB5031356");
            return 1;
        }

        var resultados = WindowsUpdateManager.RemoveMany(artigo);
        if (resultados.Count == 0)
        {
            Console.Error.WriteLine("Informe o artigo de base de conhecimento (ex.: KB5031356).");
            return 1;
        }

        var falhou = false;
        foreach (var (numero, resultado) in resultados)
        {
            Console.WriteLine($"{numero}: {resultado.Message}");
            if (!resultado.Success) falhou = true;
        }

        return falhou ? 1 : 0;
    }
}
