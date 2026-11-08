using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using Tranal.Uninstaller.Core.Management;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// O que foi observed sobre um programa que o Tranal conhece, mas que ja nao
/// esta mais registrado.
/// </summary>
public sealed class ExternalUninstallEvent
{
    /// <summary>O programa que sumiu do registro.</summary>
    public InstalledProgram Program { get; init; } = null!;

    /// <summary>A explicacao, em texto, do que foi encontrado.</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>Quando o Tranal percebeu a ausencia.</summary>
    public DateTime DetectedAt { get; init; } = DateTime.Now;

    /// <summary>True quando a chave de desinstalacao do registro sumiu.</summary>
    public bool RegistryEntryGone { get; init; }

    /// <summary>True quando a pasta de instalacao tambem sumiu.</summary>
    public bool InstallFolderGone { get; init; }

    /// <summary>
    /// True quando o programa foi desinstalado **fora** do Tranal: a chave do
    /// registro desapareceu sem que o Tranal tenha sido quem a removeu.
    /// </summary>
    public bool WasUninstalledElsewhere => RegistryEntryGone;
}

/// <summary>
/// Descobre programas que foram desinstalados fora do Tranal.
///
/// Item 5 da fila da auditoria binaria: o Revo e os concorrentes percebem
/// quando o usuario desinstala algo por fora (o "Painel de Controle", o
/// instalador de novo, o "Programas e Recursos" do Windows) e marcam o
/// programa como removido em vez de oferecer a desinstalacao de novo.
///
/// A deteccao e por evidencia, nao por adivinhacao: a chave de desinstalacao
/// do registro e o lugar onde o Windows registra que o programa existe. Se a
/// chave sumiu, o Windows nao considera o programa instalado — mesmo que a
/// pasta no disco ainda esteja la.
/// </summary>
public static class ExternalUninstallWatcher
{
    /// <summary>
    /// True quando o programa ainda esta registrado no Windows.
    ///
    /// Programa sem caminho de registro (Store App, por exemplo) nao pode ser
    /// verificado por aqui: nesse caso a resposta e <c>true</c>, para que o
    /// programa nao seja marcado como removido por engano.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    public static bool IsStillRegistered(InstalledProgram program)
    {
        if (program is null) return false;
        if (string.IsNullOrWhiteSpace(program.RegistryPath)) return true;

        // A chave pode estar na visao de 32 ou de 64 bits: o Windows registra
        // o programa em uma das duas, conforme o instalador. Conferir so a
        // visao em que ele foi lido acusaria "desinstalado fora" para tudo
        // que mora do outro lado.
        if (RegistryPath.KeyExists(program.RegistryPath, program.View)) return true;

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            if (view == program.View) continue;
            if (RegistryPath.KeyExists(program.RegistryPath, view)) return true;
        }

        return false;
    }
    /// <summary>
    /// Inspeciona um programa e devolve o que mudou, ou null quando ele
    /// continua instalado.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    public static ExternalUninstallEvent? Inspect(InstalledProgram program)
    {
        if (program is null) return null;
        if (string.IsNullOrWhiteSpace(program.RegistryPath)) return null;

        var registered = IsStillRegistered(program);
        if (registered) return null;

        var folder = program.InstallLocation ?? string.Empty;
        var folderGone = folder.Length == 0 || !Directory.Exists(folder);

        var detalhe = folderGone
            ? "a chave de desinstalacao e a pasta de instalacao sumiram: desinstalado fora do Tranal"
            : "a chave de desinstalacao sumiu, mas a pasta continua no disco: sobrou pasta";

        return new ExternalUninstallEvent
        {
            Program = program,
            Detail = detalhe,
            RegistryEntryGone = true,
            InstallFolderGone = folderGone,
        };
    }

    /// <summary>
    /// Inspeciona uma lista de programas e devolve os que sumiram do registro.
    /// </summary>
    /// <param name="programs">Os programas conhecidos.</param>
    public static IReadOnlyList<ExternalUninstallEvent> Inspect(IEnumerable<InstalledProgram> programs)
    {
        var events = new List<ExternalUninstallEvent>();
        if (programs is null) return events;

        foreach (var program in programs)
        {
            var ev = Inspect(program);
            if (ev is not null) events.Add(ev);
        }

        return events;
    }

    /// <summary>
    /// Os programas que continuam instalados, depois de remover os que
    /// sumiram. E o que a lista da interface mostra.
    /// </summary>
    /// <param name="programs">Os programas conhecidos.</param>
    public static IReadOnlyList<InstalledProgram> StillInstalled(IEnumerable<InstalledProgram> programs)
    {
        var restantes = new List<InstalledProgram>();
        if (programs is null) return restantes;

        foreach (var program in programs)
        {
            if (IsStillRegistered(program)) restantes.Add(program);
        }

        return restantes;
    }
}
