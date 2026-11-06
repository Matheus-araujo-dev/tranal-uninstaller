using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// O tipo de permissao que um programa deixa no sistema.
/// </summary>
public enum AppPermissionKind
{
    /// <summary>Servico do Windows (inicializa com o sistema, roda em segundo plano).</summary>
    Service,

    /// <summary>Entrada de inicializacao automatica.</summary>
    Autorun,

    /// <summary>Entrada de menu de contexto (botao direito).</summary>
    ContextMenu,

    /// <summary>Tarefa agendada.</summary>
    ScheduledTask,
}

/// <summary>
/// Uma permissao que um programa instalado deixa no sistema.
///
/// O Revo e o Wise tratam esses quatro tipos como paineis separados; o Tranal
/// os trata como **a mesma coisa vista de quatro lados**: e o conjunto do que
/// o programa deixa rodar sozinho depois que o usuario o desinstalou.
/// </summary>
public sealed class AppPermissionEntry
{
    /// <summary>De que tipo e a permissao.</summary>
    public AppPermissionKind Kind { get; init; }

    /// <summary>Nome da entrada, como o usuario a reconhece.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Detalhe tecnico (caminho, comando, local).</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>True quando a entrada esta ativa.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>True quando a entrada pode ser desativada.</summary>
    public bool CanDisable { get; init; }

    /// <summary>True quando a entrada pode ser removida.</summary>
    public bool CanRemove { get; init; }

    /// <summary>
    /// O objeto por tras da entrada (o servico, a tarefa, o item de
    /// inicializacao). Guarda a referencia para que a acao happena sobre o
    /// objeto real e nao sobre uma copia reconstruida.
    /// </summary>
    public object? Source { get; init; }
}

/// <summary>
/// Gerenciador de permissao de aplicativo: servico, inicializacao, menu de
/// contexto e tarefa agendada de um mesmo programa.
///
/// O item 25 do backlog e exatamente isto: o Wise mostra, para cada programa,
/// os quatro tipos de "concessao" que ele pediu ao Windows. O Tranal reunia
/// as quatro coisas em lugares diferentes (o gerenciador de inicializacao, o
/// painel de servicos, o catalogo de ferramentas) e o usuario nao conseguia
/// ver o quadro inteiro de um programa.
///
/// Esta classe e o **unico** lugar que faz a correlacao; a tela e a CLI
/// consomem a lista.
/// </summary>
public static class AppPermissionManager
{
    /// <summary>
    /// As permissoes de um programa, a partir da pasta de instalacao.
    /// </summary>
    public static IReadOnlyList<AppPermissionEntry> For(string installPath)
    {
        var root = PathUtil.Normalize(installPath);
        if (string.IsNullOrEmpty(root)) return Array.Empty<AppPermissionEntry>();

        var entries = new List<AppPermissionEntry>();

        foreach (var service in ServiceManager.ListFor(root))
        {
            var critical = ServiceManager.IsCritical(service);
            entries.Add(new AppPermissionEntry
            {
                Kind = AppPermissionKind.Service,
                Name = string.IsNullOrEmpty(service.DisplayName) ? service.Name : service.DisplayName,
                Detail = service.PathName,
                Enabled = service.Started,
                CanDisable = !critical,
                CanRemove = !critical,
                Source = service,
            });
        }

        foreach (var item in AutorunManager.Scan())
        {
            var executable = ExecutablePathResolver.Extract(item.Command);
            if (!ServiceManager.BelongsTo(executable, root)) continue;

            entries.Add(new AppPermissionEntry
            {
                Kind = AppPermissionKind.Autorun,
                Name = string.IsNullOrEmpty(item.Name) ? executable : item.Name,
                Detail = item.Location,
                Enabled = item.Enabled,
                CanDisable = !item.IsSystem,
                CanRemove = !item.IsSystem,
                Source = item,
            });
        }

        foreach (var entry in ContextMenuManager.ListFor(root))
        {
            entries.Add(new AppPermissionEntry
            {
                Kind = AppPermissionKind.ContextMenu,
                Name = entry.DisplayName,
                Detail = entry.ServerPath,
                Enabled = true,
                CanDisable = false,
                CanRemove = true,
                Source = entry,
            });
        }

        foreach (var task in ScheduledTaskManager.ListFor(root))
        {
            entries.Add(new AppPermissionEntry
            {
                Kind = AppPermissionKind.ScheduledTask,
                Name = task.Name,
                Detail = task.Command,
                Enabled = task.Enabled,
                CanDisable = true,
                CanRemove = true,
                Source = task,
            });
        }

        return entries;
    }

    /// <summary>
    /// As permissoes de um programa instalado.
    ///
    /// A pasta de instalacao vem do registro; quando ela nao existe, o
    /// executavel do comando de desinstalacao e quem diz onde o programa mora.
    /// </summary>
    public static IReadOnlyList<AppPermissionEntry> For(InstalledProgram program)
    {
        if (program is null) return Array.Empty<AppPermissionEntry>();

        return For(InstallLocationOf(program));
    }

    /// <summary>
    /// A pasta onde o programa esta instalado.
    ///
    /// O registro e a fonte; o comando de desinstalacao e o plano B. Sem um
    /// dos dois nao ha correlacao possivel — e a lista volta vazia, em vez de
    /// adivinhar a partir do nome.
    ///
    /// A pasta do registro vale mesmo quando ela **nao existe mais**: um
    /// programa cujos arquivos foram apagados ainda tem a pasta registrada, e
    /// e por ela que se descobre o que o programa deixou no sistema. Exigir
    /// que a pasta exista perderia exatamente o caso que o painel precisa
    /// mostrar.
    /// </summary>
    public static string InstallLocationOf(InstalledProgram program)
    {
        if (program is null) return string.Empty;

        var declared = (program.InstallLocation ?? string.Empty).Trim();
        if (declared.Length > 0 && !declared.Contains("://", StringComparison.Ordinal))
        {
            var normalized = PathUtil.Normalize(declared);
            if (normalized.Length > 0) return normalized;
        }

        var (executable, _) = UninstallEngine.SplitCommand(program.UninstallString);
        if (string.IsNullOrEmpty(executable)) return string.Empty;

        var directory = Path.GetDirectoryName(executable);
        return string.IsNullOrEmpty(directory) ? string.Empty : PathUtil.Normalize(directory);
    }

    /// <summary>Desativa a permissao.</summary>
    public static ManagementResult Disable(AppPermissionEntry entry)
    {
        if (entry is null) return ManagementResult.Fail("Permissao nao informada.");
        if (!entry.CanDisable)
            return ManagementResult.Fail($"A permissao '{entry.Name}' nao pode ser desativada.");

        switch (entry.Kind)
        {
            case AppPermissionKind.Service:
                return ServiceManager.Disable((WindowsService)entry.Source!);

            case AppPermissionKind.Autorun:
            {
                var item = (AutorunItem)entry.Source!;
                return AutorunManager.SetEnabled(item, false)
                    ? ManagementResult.Ok($"A inicializacao automatica de '{entry.Name}' foi desativada.")
                    : ManagementResult.Fail($"Nao foi possivel desativar a inicializacao de '{entry.Name}'.");
            }

            case AppPermissionKind.ScheduledTask:
                return ScheduledTaskManager.Disable((ScheduledTask)entry.Source!);

            case AppPermissionKind.ContextMenu:
            default:
                return ManagementResult.Fail("Uma entrada de menu de contexto nao pode ser desativada: ela e removida.");
        }
    }

    /// <summary>Reativa a permissao.</summary>
    public static ManagementResult Enable(AppPermissionEntry entry)
    {
        if (entry is null) return ManagementResult.Fail("Permissao nao informada.");

        switch (entry.Kind)
        {
            case AppPermissionKind.Service:
                return ServiceManager.Enable((WindowsService)entry.Source!);

            case AppPermissionKind.Autorun:
            {
                var item = (AutorunItem)entry.Source!;
                return AutorunManager.SetEnabled(item, true)
                    ? ManagementResult.Ok($"A inicializacao automatica de '{entry.Name}' foi reativada.")
                    : ManagementResult.Fail($"Nao foi possivel reativar a inicializacao de '{entry.Name}'.");
            }

            case AppPermissionKind.ScheduledTask:
                return ScheduledTaskManager.Enable((ScheduledTask)entry.Source!);

            case AppPermissionKind.ContextMenu:
            default:
                return ManagementResult.Fail("Uma entrada de menu de contexto nao pode ser reativada: ela e removida.");
        }
    }

    /// <summary>Remove a permissao.</summary>
    public static ManagementResult Remove(AppPermissionEntry entry)
    {
        if (entry is null) return ManagementResult.Fail("Permissao nao informada.");
        if (!entry.CanRemove)
            return ManagementResult.Fail($"A permissao '{entry.Name}' nao pode ser removida.");

        switch (entry.Kind)
        {
            case AppPermissionKind.Service:
                return ServiceManager.Delete((WindowsService)entry.Source!);

            case AppPermissionKind.Autorun:
            {
                var item = (AutorunItem)entry.Source!;
                return AutorunManager.Remove(item)
                    ? ManagementResult.Ok($"A inicializacao automatica de '{entry.Name}' foi removida.")
                    : ManagementResult.Fail($"Nao foi possivel remover a inicializacao de '{entry.Name}'.");
            }

            case AppPermissionKind.ContextMenu:
                return ContextMenuManager.Remove((ContextMenuEntry)entry.Source!);

            case AppPermissionKind.ScheduledTask:
                return ScheduledTaskManager.Delete((ScheduledTask)entry.Source!);

            default:
                return ManagementResult.Fail("Permissao desconhecida.");
        }
    }
}
