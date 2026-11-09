using System;
using System.Collections.Generic;
using System.Linq;
using Tranal.Uninstaller.Core.Management;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// A natureza do item do Boot Center.
/// </summary>
public enum BootCenterItemKind
{
    /// <summary>Item de inicializacao: chave de registro, pasta ou servico.</summary>
    Autorun,

    /// <summary>Tarefa agendada do Windows.</summary>
    ScheduledTask,
}

/// <summary>
/// Um item do Boot Center: o que sobe junto com a maquina.
/// </summary>
public sealed class BootCenterItem
{
    /// <summary>A natureza do item.</summary>
    public BootCenterItemKind Kind { get; init; }

    /// <summary>O nome exibido.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>O comando ou o caminho do executavel.</summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>Onde o item esta (chave, pasta ou caminho da tarefa).</summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>A editora do executavel, quando da para ler.</summary>
    public string Publisher { get; init; } = string.Empty;

    /// <summary>True quando o item esta habilitado.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>True para item de sistema: nunca pode ser removido.</summary>
    public bool IsSystem { get; init; }

    /// <summary>
    /// O item original do nucleo (<see cref="AutorunItem"/>). E ele quem e
    /// entregue as acoes — o Boot Center nao reescreve o item.
    /// </summary>
    public AutorunItem Source { get; init; } = new();
}

/// <summary>
/// O resumo do Boot Center.
/// </summary>
public sealed class BootCenterReport
{
    /// <summary>Quando a maquina subiu.</summary>
    public DateTime BootTime { get; init; }

    /// <summary>Ha quanto tempo a maquina esta de pe.</summary>
    public TimeSpan Uptime { get; init; }

    /// <summary>Os itens de inicializacao encontrados.</summary>
    public IReadOnlyList<BootCenterItem> Items { get; init; } = Array.Empty<BootCenterItem>();

    /// <summary>Quantos itens estao habilitados.</summary>
    public int EnabledCount => Items.Count(i => i.Enabled);

    /// <summary>Quantos itens sao do sistema (protegidos).</summary>
    public int SystemCount => Items.Count(i => i.IsSystem);
}

/// <summary>
/// O Boot Center: o resumo do que sobe junto com a maquina.
///
/// O Revo, o Ashampoo e o BCU tem o mesmo modulo com nomes diferentes — e
/// e o que o usuario procura quando a maquina demora para subir.
///
/// A REGRA mora aqui, em um lugar so: a tela do Boot Center e a linha de
/// comando consomem este resumo e estas acoes. Quem varre e quem executa
/// continua sendo o modulo de inicializacao automatica
/// (<see cref="AutorunManager"/>) — o Boot Center junta, nao duplica.
///
/// O tempo de boot e **medido**: sai do tempo de atividade do Windows, e
/// nao de uma estimativa. O Tranal nao inventa tempo de boot que ele nao
/// consegue medir.
/// </summary>
public static class BootCenter
{
    /// <summary>
    /// Ha quanto tempo a maquina esta de pe.
    ///
    /// Medido pelo proprio Windows (o tempo de atividade do processo), e nao
    /// por um relogio de parede: e o valor que o sistema conhece.
    /// </summary>
    public static TimeSpan Uptime() => TimeSpan.FromMilliseconds(Environment.TickCount64);

    /// <summary>
    /// Quando a maquina subiu.
    /// </summary>
    public static DateTime BootTime() => DateTime.Now - Uptime();

    /// <summary>
    /// O resumo do Boot Center: quando a maquina subiu e o que sobe com ela.
    /// </summary>
    public static BootCenterReport Scan()
    {
        var items = new List<BootCenterItem>();

        foreach (var item in AutorunManager.Scan())
        {
            items.Add(new BootCenterItem
            {
                Kind = item.Kind == AutorunItemKind.ScheduledTask
                    ? BootCenterItemKind.ScheduledTask
                    : BootCenterItemKind.Autorun,
                Name = item.Name,
                Command = item.Command,
                Location = string.IsNullOrWhiteSpace(item.Location) ? item.Source : item.Location,
                Publisher = item.Publisher,
                Enabled = item.Enabled,
                IsSystem = item.IsSystem,
                Source = item,
            });
        }

        return new BootCenterReport
        {
            BootTime = BootTime(),
            Uptime = Uptime(),
            Items = items,
        };
    }

    /// <summary>
    /// Habilita ou desabilita um item do Boot Center.
    ///
    /// A tarefa agendada e desabilitada pelo Agendador de Tarefas (e nao
    /// pela regra do autorun), porque desabilitar tarefa e ligar e desligar
    /// e o que o Windows oferece.
    /// </summary>
    /// <param name="item">O item.</param>
    /// <param name="enabled">True para habilitar.</param>
    /// <returns>True quando a mudanca foi feita.</returns>
    public static bool SetEnabled(BootCenterItem item, bool enabled)
    {
        if (item is null) return false;

        if (item.Kind == BootCenterItemKind.ScheduledTask)
        {
            var task = FindTask(item);
            if (task is null) return false;

            var result = enabled
                ? ScheduledTaskManager.Enable(task)
                : ScheduledTaskManager.Disable(task);

            return result.Success;
        }

        return AutorunManager.SetEnabled(item.Source, enabled);
    }

    /// <summary>
    /// Remove um item do Boot Center.
    ///
    /// Item de sistema nunca e removido. Tarefa agendada e apagada pelo
    /// Agendador de Tarefas; os demais itens usam a regra do modulo de
    /// inicializacao automatica.
    /// </summary>
    /// <param name="item">O item.</param>
    /// <returns>True quando a remocao foi feita.</returns>
    public static bool Remove(BootCenterItem item)
    {
        if (item is null) return false;
        if (item.IsSystem) return false;

        if (item.Kind == BootCenterItemKind.ScheduledTask)
        {
            var task = FindTask(item);
            if (task is null) return false;

            return ScheduledTaskManager.Delete(task).Success;
        }

        return AutorunManager.Remove(item.Source);
    }

    /// <summary>
    /// A tarefa agendada que corresponde ao item.
    ///
    /// O Boot Center nao guarda a tarefa: eleacha pelo caminho, porque o
    /// caminho e o identificador estavel da tarefa no Agendador.
    /// </summary>
    private static ScheduledTask? FindTask(BootCenterItem item)
    {
        foreach (var task in ScheduledTaskManager.List())
        {
            if (string.Equals(task.Name, item.Location, StringComparison.OrdinalIgnoreCase))
                return task;

            if (string.Equals(task.Name, item.Name, StringComparison.OrdinalIgnoreCase))
                return task;
        }

        return null;
    }
}
