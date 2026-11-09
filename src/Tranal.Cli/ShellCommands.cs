using System;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Cli;

/// <summary>
/// Os comandos da bandeja, dos programas novos, das categorias e da
/// integracao com o Windows.
///
///   tranal /tray                 - as regras da bandeja do sistema
///   tranal /tray /on             - liga (ou desliga) o icone na bandeja
///   tranal /new-installs         - os programas que apareceram depois da base
///   tranal /new-installs /save   - grava a linha de base de agora
///   tranal /categories           - a arvore de categorias automaticas
///   tranal /categories /reset    - apaga as atribuicoes de categoria
///   tranal /windows-integration  - o icone no painel de controle e as
///                                  ferramentas externas
///
/// A REGRA mora no nucleo: cada comando aqui consome o veredito de uma
/// classe do <c>Core</c> e nao repete a regra de negocio.
/// </summary>
public static class ShellCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/tray")) return ExecutarTray(flags);
        if (CliArgs.Has(flags, "/new-installs")) return ExecutarNewInstalls(flags);
        if (CliArgs.Has(flags, "/categories")) return ExecutarCategorias(flags);
        if (CliArgs.Has(flags, "/windows-integration")) return ExecutarIntegracao(flags);
        return null;
    }

    /// <summary>As regras da bandeja do sistema.</summary>
    private static int ExecutarTray(HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/on") || CliArgs.Has(flags, "/off"))
        {
            var ligar = CliArgs.Has(flags, "/on");
            AppSettingsStore.Update(s => s.ShowTrayIcon = ligar);
            Console.WriteLine(ligar
                ? "Icone do Tranal na bandeja do sistema: ligado."
                : "Icone do Tranal na bandeja do sistema: desligado.");
        }

        Console.WriteLine(SystemTray.Describe());
        Console.WriteLine();

        foreach (var regra in SystemTray.Rules)
        {
            Console.WriteLine($"{regra.Id,-26} {(regra.Enabled ? "sim" : "nao")}");
        }

        return 0;
    }

    /// <summary>Os programas que apareceram depois da ultima linha de base.</summary>
    private static int ExecutarNewInstalls(HashSet<string> flags)
    {
        var instalados = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = false,
        });

        if (CliArgs.Has(flags, "/save"))
        {
            var gravados = NewInstallsTracker.SaveBaseline(instalados);
            Console.WriteLine($"Linha de base gravada com {gravados} programa(s).");
            return 0;
        }

        var novos = NewInstallsTracker.Detect(instalados);
        var base_ = NewInstallsTracker.BaselineAt;

        Console.WriteLine(base_ is { } quando
            ? $"Programas novos desde {quando:dd/MM/yyyy HH:mm}: {novos.Count}"
            : $"Programas novos (sem linha de base, por data de instalacao): {novos.Count}");
        Console.WriteLine();

        foreach (var novo in novos)
        {
            Console.WriteLine($"{novo.DisplayName,-28} {novo.Publisher,-22} {novo.InstallLocation}");
        }

        return 0;
    }

    /// <summary>A arvore de categorias automaticas.</summary>
    private static int ExecutarCategorias(HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/reset"))
        {
            var apagadas = ProgramCategoryTree.ResetToUnsorted();
            Console.WriteLine($"Atribuicoes de categoria apagadas: {apagadas}");
        }

        var instalados = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = false,
        });

        var nos = ProgramCategoryTree.Build(instalados);

        Console.WriteLine($"Categorias: {nos.Count}");
        Console.WriteLine();

        foreach (var no in nos)
        {
            Console.WriteLine($"{no.Name,-28} {no.TotalPrograms,4} programa(s)");
        }

        return 0;
    }

    /// <summary>O icone no painel de controle e as ferramentas externas.</summary>
    private static int ExecutarIntegracao(HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/register"))
        {
            var ok = ControlPanelIcon.Register(
                "Tranal Uninstaller",
                "\"" + SystemTray.ExecutablePath + "\"",
                ControlPanelIcon.IconPath);

            Console.WriteLine(ok
                ? "Icone do Tranal criado no painel de controle do Windows."
                : "Nao foi possivel criar o icone no painel de controle.");
        }

        if (CliArgs.Has(flags, "/unregister"))
        {
            var ok = ControlPanelIcon.Unregister();

            Console.WriteLine(ok
                ? "Icone do Tranal removido do painel de controle do Windows."
                : "Nao foi possivel remover o icone do painel de controle.");
        }

        Console.WriteLine(ControlPanelIcon.Describe());
        Console.WriteLine($"Chave: {ControlPanelIcon.RegistryPath}");
        Console.WriteLine();

        foreach (var ferramenta in ExternalLaunchers.Known())
        {
            var detalhe = ferramenta.Available
                ? (ferramenta.ExecutablePath.Length > 0 ? ferramenta.ExecutablePath : ferramenta.Arguments)
                : "nao encontrada nesta maquina";

            Console.WriteLine($"{ferramenta.DisplayName,-28} {detalhe}");
        }

        return 0;
    }
}
