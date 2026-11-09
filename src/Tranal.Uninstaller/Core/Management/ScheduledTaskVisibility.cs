
namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// A visao das tarefas agendadas: as do Windows separately das do usuario.
///
/// O Uninstall Tool esconde por padrao as tarefas que pertencem a Microsoft —
/// a lista com mais de duzentas tarefas do sistema esconde a tarefa que o
/// usuario criou.
///
/// A REGRA mora aqui, em um lugar so: a leitura das tarefas fica no
/// <see cref="ScheduledTaskManager"/> (que ja sabe ler o XML do Windows e
/// agir no Agendador) e esta classe so decide o que entra na visao.
/// </summary>
public static class ScheduledTaskVisibility
{
    /// <summary>
    /// O prefixo das pastas de tarefa que pertencem a Microsoft.
    /// </summary>
    public const string MicrosoftFolderPrefix = "\\Microsoft";

    /// <summary>
    /// True quando a tarefa pertence a Microsoft.
    /// </summary>
    /// <param name="task">A tarefa inspecionada.</param>
    public static bool IsMicrosoft(ScheduledTask? task)
    {
        if (task is null) return false;

        var folder = (task.Folder ?? string.Empty).Trim();
        if (folder.Length == 0) return false;

        return folder.StartsWith(MicrosoftFolderPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// As tarefas agendadas, com a opcao de esconder as da Microsoft.
    /// </summary>
    /// <param name="includeMicrosoft">True para trazer as tarefas da Microsoft tambem.</param>
    public static IReadOnlyList<ScheduledTask> List(bool includeMicrosoft = false)
    {
        var todas = ScheduledTaskManager.List();
        if (includeMicrosoft) return todas;

        return todas.Where(t => !IsMicrosoft(t)).ToList();
    }

    /// <summary>
    /// As tarefas da Microsoft.
    /// </summary>
    public static IReadOnlyList<ScheduledTask> ListMicrosoft() =>
        ScheduledTaskManager.List().Where(IsMicrosoft).ToList();

    /// <summary>
    /// As tarefas que pertencem a um programa instalado.
    /// </summary>
    /// <param name="installPath">A pasta de instalacao do programa.</param>
    /// <param name="includeMicrosoft">True para trazer as tarefas da Microsoft tambem.</param>
    public static IReadOnlyList<ScheduledTask> ListFor(string? installPath, bool includeMicrosoft = false)
    {
        var tarefas = ScheduledTaskManager.ListFor(installPath ?? string.Empty);
        if (includeMicrosoft) return tarefas;

        return tarefas.Where(t => !IsMicrosoft(t)).ToList();
    }

    /// <summary>
    /// Quantas tarefas da Microsoft foram escondidas na visao.
    /// </summary>
    public static int HiddenCount() => ScheduledTaskManager.List().Count(IsMicrosoft);
}
