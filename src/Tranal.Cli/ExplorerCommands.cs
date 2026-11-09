using System;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Notifications;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Core.Tools;
using Tranal.Uninstaller.Core.Topmost;

namespace Tranal.Cli;

/// <summary>
/// Os comandos do Explorer, das notificacoes, dos perfis e do sistema.
///
///   tranal /shell-menu             - as entradas do Tranal no Explorer
///   tranal /shell-menu /on         - registra as entradas no Explorer
///   tranal /shell-menu /off        - remove as entradas do Explorer
///   tranal /hotkeys                - os atalhos globais
///   tranal /notifications          - as notificacoes internas
///   tranal /notifications /clear   - apaga as notificacoes lidas
///   tranal /notification-blocker   - o estado dos avisos do Windows
///   tranal /notification-blocker /off - bloqueia os avisos na tela
///   tranal /profiles               - os perfis por operacao
///   tranal /registry-table         - os caminhos de registro por programa
///   tranal /geolocation            - a geolocalizacao desta maquina
///
/// A REGRA mora no nucleo: cada comando aqui consome o veredito de uma
/// classe do <c>Core</c> e nao repete a regra de negocio.
/// </summary>
public static class ExplorerCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/shell-menu")) return ExecutarShellMenu(flags);
        if (CliArgs.Has(flags, "/hotkeys")) return ExecutarHotkeys();
        if (CliArgs.Has(flags, "/notifications")) return ExecutarNotificacoes(flags);
        if (CliArgs.Has(flags, "/notification-blocker")) return ExecutarBloqueio(flags);
        if (CliArgs.Has(flags, "/profiles")) return ExecutarPerfis();
        if (CliArgs.Has(flags, "/registry-table")) return ExecutarTabelaRegistro();
        if (CliArgs.Has(flags, "/geolocation")) return ExecutarGeolocalizacao();
        return null;
    }

    /// <summary>As entradas do Tranal no menu de contexto do Explorer.</summary>
    private static int ExecutarShellMenu(HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/on") || CliArgs.Has(flags, "/off"))
        {
            var ligar = CliArgs.Has(flags, "/on");
            var aplicadas = ExplorerShellMenu.Apply(ligar);
            Console.WriteLine(ligar
                ? $"Entradas do Tranal registradas no Explorer: {aplicadas}."
                : $"Entradas do Tranal removidas do Explorer: {aplicadas}.");
        }

        foreach (var entrada in ExplorerShellMenu.Entries)
        {
            var registrada = ExplorerShellMenu.IsRegistered(entrada.Id);
            Console.WriteLine($"{entrada.DisplayName,-40} {(registrada ? "registrada" : "ausente"),-12} {entrada.ShellKeyRelative}");
        }

        return 0;
    }

    /// <summary>Os atalhos globais do Tranal.</summary>
    private static int ExecutarHotkeys()
    {
        foreach (var atalho in GlobalHotkeys.All)
        {
            Console.WriteLine($"{atalho.DisplayName,-24} {GlobalHotkeys.Describe(atalho)}");
        }

        return 0;
    }

    /// <summary>As notificacoes internas.</summary>
    private static int ExecutarNotificacoes(HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/clear"))
        {
            var apagadas = InternalNotifications.ClearRead();
            Console.WriteLine($"Notificacoes lidas apagadas: {apagadas}");
        }

        var notificacoes = InternalNotifications.Due();
        Console.WriteLine($"Notificacoes: {notificacoes.Count} (nao lidas: {InternalNotifications.UnreadCount()})");
        Console.WriteLine();

        foreach (var notificacao in notificacoes)
        {
            var (titulo, mensagem) = InternalNotifications.TextOf(notificacao);
            Console.WriteLine($"{titulo,-24} {mensagem}");
        }

        return 0;
    }

    /// <summary>O estado dos avisos do Windows.</summary>
    private static int ExecutarBloqueio(HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/off"))
        {
            NotificationBlocker.SetToastEnabled(false);
            NotificationBlocker.SetSoundEnabled(false);
            Console.WriteLine("Os avisos do Windows foram bloqueados.");
        }

        if (CliArgs.Has(flags, "/on"))
        {
            NotificationBlocker.SetToastEnabled(true);
            NotificationBlocker.SetSoundEnabled(true);
            Console.WriteLine("Os avisos do Windows voltaram a aparecer.");
        }

        Console.WriteLine(NotificationBlocker.Describe());
        return 0;
    }

    /// <summary>Os perfis por operacao.</summary>
    private static int ExecutarPerfis()
    {
        foreach (var operacao in Enum.GetValues<OperationKind>())
        {
            var perfil = OperationProfiles.ProfileOf(operacao);
            Console.WriteLine($"{operacao,-14} sistema={perfil.IncludeSystemComponents,-5} "
                            + $"store={perfil.IncludeWindowsApps,-5} "
                            + $"restauracao={perfil.CreateRestorePoint,-5} "
                            + $"log={perfil.KeepChangesLog}");
        }

        return 0;
    }

    /// <summary>Os caminhos de registro por programa.</summary>
    private static int ExecutarTabelaRegistro()
    {
        var instalados = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = false,
        });

        var linhas = RegistryPathTable.Build(instalados);

        Console.WriteLine($"Caminhos de registro: {linhas.Count}");
        Console.WriteLine();

        foreach (var linha in linhas)
        {
            var marca = linha.IsRegistryKey ? "(Reg)" : "     ";
            Console.WriteLine($"{linha.ProgramName,-26} {marca} {linha.Path}");
        }

        return 0;
    }

    /// <summary>A geolocalizacao desta maquina.</summary>
    private static int ExecutarGeolocalizacao()
    {
        var estado = GeolocationProbe.State();

        Console.WriteLine(estado.Detail);
        Console.WriteLine($"API presente: {(estado.ApiAvailable ? "sim" : "nao")}");
        Console.WriteLine($"Servico presente: {(estado.ServicePresent ? "sim" : "nao")}");

        return 0;
    }
}
