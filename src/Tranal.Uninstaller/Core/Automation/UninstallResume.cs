using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// O que a sessao guarda para poder retomar: a lista de itens que ainda
/// precisam sair do computador.
///
/// A sessao sem esta carga serve para retomar um PLANO de etapas. A
/// desinstalacao, na pratica, precisa retomar uma REMOCAO: sao os arquivos
/// trancados por um processo que o Windows so libera no proximo reinicio.
/// Sem a lista, a retomada repetiria a varredura inteira e perderia o
/// trabalho devarredura.
/// </summary>
public sealed class RemovalPayload
{
    /// <summary>O nome do programa que estava sendo removido.</summary>
    public string ProgramName { get; set; } = string.Empty;

    /// <summary>O modo da desinstalacao ("Advanced", "Moderate"...).</summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>Os itens que a remocao pretendia apagar.</summary>
    public List<LeftoverItem> Items { get; set; } = new();
}

/// <summary>
/// Uma desinstalacao interrompida, vista de fora: o que falta fazer.
///
/// E a forma que a tela e a linha de comando mostram. O nucleo do Tranal
/// grava e le a sessao; a apresentacao desta e da conta de quem mostra.
/// </summary>
public sealed class PendingUninstall
{
    /// <summary>A identidade da sessao (o nome do arquivo).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>O nome do programa.</summary>
    public string ProgramName { get; init; } = string.Empty;

    /// <summary>O modo da desinstalacao.</summary>
    public string Mode { get; init; } = string.Empty;

    /// <summary>Quantas etapas o plano tinha.</summary>
    public int TotalSteps { get; init; }

    /// <summary>Quantas etapas ja foram concluidas.</summary>
    public int CompletedSteps { get; init; }

    /// <summary>A descricao da etapa que ficou pela metade.</summary>
    public string NextStep { get; init; } = string.Empty;

    /// <summary>Quantos itens da remocao ainda nao sairam do computador.</summary>
    public int RemainingItems { get; init; }

    /// <summary>Onde a sessao esta gravada.</summary>
    public string FilePath { get; init; } = string.Empty;
}

/// <summary>
/// Retomar a desinstalacao interrompida.
///
/// O HiBit e o Uninstalr medem essa capacidade: a desinstalacao que foi
/// interrompida pelo reinicio do Windows pode ser retomada depois, sem
/// comecar do zero.
///
/// A REGRA esta em um lugar so: a sessao e a fonte da verdade do que falta
/// fazer, e o <see cref="UninstallSession"/> e quem a guarda. Esta classe
/// apenas traduz a sessao para a forma que a tela e a linha de comando
/// entendem, e cria a sessao da remocao.
///
/// A retomada em si e o executor de plano
/// (<see cref="UninstallPlanExecutor"/>), que ja sabe pular as etapas
/// concluidas.
/// </summary>
public static class UninstallResume
{
    /// <summary>
    /// As opcoes de leitura e escrita da carga da sessao.
    /// </summary>
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// As desinstalacoes interrompidas que ainda tem trabalho pela frente.
    /// </summary>
    public static IReadOnlyList<PendingUninstall> Pending()
    {
        var result = new List<PendingUninstall>();

        foreach (var session in UninstallSession.Pending())
        {
            var items = ReadItems(session);

            var proxima = string.Empty;
            var indice = session.LastCompletedStep + 1;
            if (indice >= 0 && indice < session.Plan.Steps.Count)
                proxima = session.Plan.Steps[indice].Description;

            result.Add(new PendingUninstall
            {
                Id = session.Id,
                ProgramName = session.ProgramName,
                Mode = session.Plan.Mode.ToString(),
                TotalSteps = session.Plan.Steps.Count,
                CompletedSteps = session.LastCompletedStep + 1,
                NextStep = proxima,
                RemainingItems = items.Count,
                FilePath = session.FilePath,
            });
        }

        return result
            .OrderBy(p => p.ProgramName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Grava a sessao de uma remocao, para que ela possa ser retomada.
    ///
    /// A sessao e criada ANTES da remocao comecar: e a remocao que pode ser
    /// interrompida, e sem a sessao gravada antes nao haveria o que retomar.
    ///
    /// </summary>
    /// <param name="programName">O nome do programa.</param>
    /// <param name="mode">O modo da desinstalacao.</param>
    /// <param name="items">Os itens que a remocao vai apagar.</param>
    public static UninstallSession SaveRemoval(
        string programName,
        UninstallMode mode,
        IEnumerable<LeftoverItem> items)
    {
        var lista = items?.ToList() ?? new List<LeftoverItem>();

        var plano = new UninstallPlan
        {
            Id = UninstallSession.NewId(programName),
            ProgramName = programName,
            Mode = mode,
            Steps = new[]
            {
                new UninstallStep
                {
                    Kind = UninstallStepKind.Remove,
                    Description = $"Remover as sobras marcadas de {programName}.",
                },
            },
        };

        var sessao = UninstallSession.Start(plano, programName);

        var carga = new RemovalPayload
        {
            ProgramName = programName,
            Mode = mode.ToString(),
            Items = lista,
        };

        return UninstallSession.Save(sessao.WithPayload(JsonSerializer.Serialize(carga, Opcoes)));
    }

    /// <summary>
    /// Os itens que a sessao guardou para a retomada.
    ///
    /// Devolve uma lista vazia quando a sessao nao tem carga — uma sessao de
    /// plano (sem remocao) nao tem itens para retomar.
    /// </summary>
    /// <param name="session">A sessao.</param>
    public static IReadOnlyList<LeftoverItem> ReadItems(UninstallSession? session)
    {
        if (session is null) return Array.Empty<LeftoverItem>();
        if (string.IsNullOrWhiteSpace(session.Payload)) return Array.Empty<LeftoverItem>();

        try
        {
            var carga = JsonSerializer.Deserialize<RemovalPayload>(session.Payload, Opcoes);
            return carga?.Items ?? new List<LeftoverItem>();
        }
        catch
        {
            // Carga invalida: a sessao continua valendo para as etapas do
            // plano, mas nao tem itens para retomar.
            return Array.Empty<LeftoverItem>();
        }
    }

    /// <summary>
    /// Apaga a sessao de uma desinstalacao interrompida.
    ///
    /// E o que a tela faz quando o usuario descarta a retomada: o trabalho
    /// que faltava nao vai acontecer.
    /// </summary>
    /// <param name="id">A identidade da sessao.</param>
    public static bool Discard(string? id) => UninstallSession.Delete(id ?? string.Empty);
}
