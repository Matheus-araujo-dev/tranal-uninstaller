using System.IO;
using System.Xml.Linq;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// Uma tarefa agendada que a visao esconde.
///
/// O Uninstall Tool esconde por padrao as tarefas que pertencem a Microsoft —
/// a lista com mais de duzentas tarefas do sistema esconde a tarefa que o
/// usuario criou. O Windows tambem marca uma tarefa como oculta (o elemento
/// <c>Hidden</c> do XML), e essa e a que o usuario nem ve na lista de
/// tarefas.
/// </summary>
public sealed record HiddenMicrosoftTask
{
    /// <summary>Nome completo da tarefa, com as pastas.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>A pasta da tarefa.</summary>
    public string Folder { get; init; } = string.Empty;

    /// <summary>Caminho do arquivo XML que define a tarefa.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Executavel que a tarefa roda.</summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>Argumentos da tarefa.</summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>Estado declarado no XML ("Ready", "Running", "Disabled").</summary>
    public string State { get; init; } = string.Empty;

    /// <summary>True quando a tarefa esta habilitada no Agendador.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>True quando o Windows marca a tarefa como oculta.</summary>
    public bool IsHidden { get; init; }

    /// <summary>Descricao da tarefa, quando o XML traz.</summary>
    public string Description { get; init; } = string.Empty;
}

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
///
/// Esta classe nunca apaga nem desabilita tarefa: para mudar o que o
/// Windows mostra, ela escreve <c>&lt;Hidden&gt;false&lt;/Hidden&gt;</c> no
/// XML da definicao.
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
    /// As tarefas da Microsoft, no formato proprio desta visao.
    /// </summary>
    public static IReadOnlyList<HiddenMicrosoftTask> ListMicrosoftTasks() =>
        ListMicrosoft().Select(Of).ToList();

    /// <summary>
    /// As tarefas que o Windows marca como ocultas.
    ///
    /// Sao as tarefas que somem da lista do Agendador — e que o
    /// <see cref="Unhide"/> traz de volta, sem apagar nem desabilitar nada.
    /// </summary>
    public static IReadOnlyList<HiddenMicrosoftTask> ListHidden() =>
        ScheduledTaskManager.List()
            .Where(t => t.IsHidden)
            .Select(Of)
            .ToList();

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

    /// <summary>
    /// Deixa a tarefa visivel de novo.
    ///
    /// Escreve <c>&lt;Hidden&gt;false&lt;/Hidden&gt;</c> no XML da definicao da
    /// tarefa. Nao apaga a tarefa, nao a desabilita e nao muda o que ela
    /// roda — so tira a marca de "oculta".
    /// </summary>
    /// <param name="task">A tarefa a mostrar.</param>
    public static ManagementResult Unhide(HiddenMicrosoftTask? task)
    {
        if (task is null) return ManagementResult.Fail("Tarefa nao informada.");

        var arquivo = (task.FilePath ?? string.Empty).Trim();
        if (arquivo.Length == 0)
            return ManagementResult.Fail("A definicao da tarefa nao esta no disco.");

        try
        {
            if (!File.Exists(arquivo))
                return ManagementResult.Fail($"A definicao da tarefa '{arquivo}' nao existe.");

            var document = XDocument.Load(arquivo);
            var raiz = document.Root;
            if (raiz is null) return ManagementResult.Fail("A definicao da tarefa esta vazia.");

            var settings = raiz.Descendants().FirstOrDefault(e => e.Name.LocalName == "Settings");
            if (settings is null)
            {
                settings = new XElement(raiz.Name.Namespace + "Settings");
                raiz.Add(settings);
            }

            var hidden = settings.Elements().FirstOrDefault(e => e.Name.LocalName == "Hidden");
            if (hidden is null) settings.Add(new XElement(settings.Name.Namespace + "Hidden", "false"));
            else hidden.Value = "false";

            document.Save(arquivo);

            return ManagementResult.Ok($"A tarefa '{task.Name}' nao esta mais oculta.");
        }
        catch (UnauthorizedAccessException)
        {
            return ManagementResult.Fail(
                $"Sem permissao para alterar a definicao de '{task.Name}' (a pasta de tarefas do Windows exige administrador).");
        }
        catch (Exception ex)
        {
            return ManagementResult.Fail($"Nao foi possivel mostrar a tarefa '{task.Name}': {ex.Message}");
        }
    }

    /// <summary>
    /// Deixa a tarefa visivel de novo.
    /// </summary>
    /// <param name="task">A tarefa a mostrar.</param>
    public static ManagementResult Unhide(ScheduledTask? task) =>
        Unhide(task is null ? null : Of(task));

    /// <summary>
    /// Converte a tarefa do gerenciador no formato desta visao.
    /// </summary>
    private static HiddenMicrosoftTask Of(ScheduledTask task) => new()
    {
        Name = task.Name,
        Folder = task.Folder,
        FilePath = task.FilePath,
        Command = task.Command,
        Arguments = task.Arguments,
        State = task.State,
        Enabled = task.Enabled,
        IsHidden = task.IsHidden,
        Description = task.Description,
    };
}
