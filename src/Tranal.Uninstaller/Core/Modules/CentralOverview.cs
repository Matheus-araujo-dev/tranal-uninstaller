using System;
using System.Collections.Generic;
using System.Linq;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Updates;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// O que a Central mostra.
/// </summary>
public enum CentralKind
{
    /// <summary>Atualizacao disponivel para um programa instalado.</summary>
    Update,

    /// <summary>Programa novo, ainda nao revisado por quem usa a maquina.</summary>
    NewProgram,

    /// <summary>
    /// Achado de saude: programa sem comando de desinstalacao, pasta
    /// compartilhada com outro programa, servico ou tarefa agendada.
    /// </summary>
    Health,

    /// <summary>Atualizacao do proprio Tranal.</summary>
    SelfUpdate,
}

/// <summary>
/// A prioridade do item: o que precisa de atencao primeiro.
/// </summary>
public enum CentralPriority
{
    /// <summary>Informativo: o usuario pode ignorar.</summary>
    Low,

    /// <summary>Convem olhar.</summary>
    Medium,

    /// <summary>Precisa de atencao agora.</summary>
    High,
}

/// <summary>
/// Um item da Central.
///
/// O item nao carrega a regra: ele carrega o que foi medido e **para onde**
/// ir quando o usuario quer agir. A regra de cada area continua no modulo
/// dela — a Central so junta o resultado.
/// </summary>
public sealed class CentralItem
{
    /// <summary>A area do item.</summary>
    public CentralKind Kind { get; init; }

    /// <summary>A prioridade do item.</summary>
    public CentralPriority Priority { get; init; }

    /// <summary>O que acontece, em uma frase.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>O detalhe que explica o item.</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>
    /// A tela que age sobre este item. Vazio quando o item nao tem acao
    /// (a propria Central e a acao).
    /// </summary>
    public string ToolId { get; init; } = string.Empty;

    /// <summary>
    /// O programa do item. Vazio quando o item e sobre a maquina inteira.
    /// </summary>
    public string Target { get; init; } = string.Empty;
}

/// <summary>
/// O resultado da Central.
/// </summary>
public sealed class CentralReport
{
    /// <summary>Os itens, do mais grave para o menos grave.</summary>
    public IReadOnlyList<CentralItem> Items { get; init; } = Array.Empty<CentralItem>();

    /// <summary>Quantos itens ha no total.</summary>
    public int Total => Items.Count;

    /// <summary>Quantos itens ha de uma area.</summary>
    /// <param name="kind">A area.</param>
    public int CountOf(CentralKind kind) => Items.Count(i => i.Kind == kind);

    /// <summary>Nada a fazer?</summary>
    public bool IsClear => Items.Count == 0;
}

/// <summary>
/// A Central: um resumo, em um lugar so, do que precisa de atencao.
///
/// A ideia vem do "Action Center" do IObit, medido no binario dele: um
/// painel que junta, em uma tela so, o estado de cada area (o que esta
/// desatualizado, o que precisa de limpeza, o que exige atencao) e a acao
/// de cada item.
///
/// <para><b>A regra fica em um lugar so.</b> Este modulo nao decide o que e
/// "atualizacao disponivel", o que e "programa novo" nem o que e "achado de
/// saude": ele consume os modulos que ja decidem isso
/// (<see cref="SoftwareUpdater"/>, <see cref="NewInstallsTracker"/> e
/// <see cref="SoftwareHealth"/>) e junta o resultado. Nenhuma area tem a
/// regra escrita duas vezes.</para>
///
/// <para>A diferenca em relacao ao concorrente: la o painel oferece os
/// produtos do proprio fabricante. Aqui os itens sao as **capacidades do
/// Tranal** — a limpeza, as atualizacoes, a saude — e a acao leva a tool
/// que faz o trabalho. O Tranal nao tem suite de produtos para vender e nao
/// vai fazer propaganda de nada.</para>
/// </summary>
public static class CentralOverview
{
    /// <summary>
    /// Monta a Central.
    /// </summary>
    /// <param name="programs">Os programas instalados.</param>
    /// <param name="updates">
    /// O plano de atualizacoes ja montado. Nulo quando o feed nao foi
    /// consultado (a Central funciona sem rede).
    /// </param>
    /// <param name="versaoPublicadaTranal">
    /// A versao mais nova do proprio Tranal, quando ela e conhecida.
    /// </param>
    /// <param name="incluirSaude">
    /// Quando falso, a area de saude fica de fora. A analise de saude le
    /// registro e servicos de cada programa: quem so quer a lista rapida
    /// desliga.
    /// </param>
    public static CentralReport Build(
        IReadOnlyList<InstalledProgram> programs,
        UpdatePlan? updates = null,
        string? versaoPublicadaTranal = null,
        bool incluirSaude = true)
    {
        var lista = programs ?? Array.Empty<InstalledProgram>();
        var itens = new List<CentralItem>();

        itens.AddRange(UpdateItems(updates));
        itens.AddRange(NewProgramItems(lista));
        itens.AddRange(SelfUpdateItem(versaoPublicadaTranal));
        if (incluirSaude) itens.AddRange(HealthItems(lista));

        // A ordem e a mesma em toda execucao: primeiro o que e mais grave.
        // Uma Central que troca a ordem a cada varredura treina o olho do
        // usuario a desconfiar dela.
        var ordenados = itens
            .OrderByDescending(i => i.Priority)
            .ThenBy(i => i.Kind)
            .ThenBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new CentralReport { Items = ordenados };
    }

    /// <summary>
    /// A area de atualizacoes: o que o <see cref="SoftwareUpdater"/> ja
    /// decidiu atualizar.
    ///
    /// O plano decide o que entra (versao mais nova, lista negra); a Central
    /// so le o resultado.
    /// </summary>
    private static IEnumerable<CentralItem> UpdateItems(UpdatePlan? updates)
    {
        if (updates is null) yield break;

        foreach (var item in updates.Items.Where(i => i.Pending))
        {
            var candidate = item.Candidate;

            yield return new CentralItem
            {
                Kind = CentralKind.Update,
                Priority = CentralPriority.Medium,
                Title = $"{candidate.ProgramName}: {candidate.InstalledVersion} -> {candidate.AvailableVersion}",
                Detail = candidate.Publisher,
                ToolId = ToolCatalog.SoftwareUpdates,
                Target = candidate.ProgramName,
            };
        }
    }

    /// <summary>
    /// A area de programas novos: o que apareceu na maquina e ainda nao foi
    /// revisado.
    ///
    /// A regra e a do <see cref="NewInstallsTracker"/>: comparacao com a
    /// linha de base, ou a data de instalacao quando nao ha linha de base.
    /// </summary>
    private static IEnumerable<CentralItem> NewProgramItems(IReadOnlyList<InstalledProgram> programs)
    {
        if (programs.Count == 0) yield break;

        foreach (var entry in NewInstallsTracker.Detect(programs))
        {
            yield return new CentralItem
            {
                Kind = CentralKind.NewProgram,
                Priority = CentralPriority.Medium,
                Title = $"{entry.DisplayName} foi instalado recentemente",
                Detail = entry.InstallDate?.ToString("d") ?? entry.Publisher,
                ToolId = ToolCatalog.NewInstalls,
                Target = entry.DisplayName,
            };
        }
    }

    /// <summary>
    /// A area do proprio Tranal: ha versao nova publicada?
    ///
    /// A comparacao e a do <see cref="UpdateService"/> — a mesma que decide a
    /// atualizacao automatica.
    /// </summary>
    private static IEnumerable<CentralItem> SelfUpdateItem(string? versaoPublicada)
    {
        if (string.IsNullOrWhiteSpace(versaoPublicada)) yield break;
        if (!UpdateService.TemVersaoNova(UpdateService.VersaoAtual(), versaoPublicada)) yield break;

        yield return new CentralItem
        {
            Kind = CentralKind.SelfUpdate,
            Priority = CentralPriority.High,
            Title = $"O Tranal tem versao nova: {versaoPublicada}",
            Detail = $"Versao instalada: {UpdateService.VersaoAtual()}",
            ToolId = ToolCatalog.AppUpdate,
        };
    }

    /// <summary>
    /// A area de saude: o que o <see cref="SoftwareHealth"/> encontrou.
    ///
    /// A Central nao roda a varredura de sobras nem a de duplicados: as duas
    /// sao caras e o usuario as pede na hora em que quer elas. O que entra
    /// aqui e o que sai do registro e dos servicos.
    /// </summary>
    private static IEnumerable<CentralItem> HealthItems(IReadOnlyList<InstalledProgram> programs)
    {
        if (programs.Count == 0) yield break;

        var report = SoftwareHealth.Analyze(programs, scanLeftovers: false, scanDuplicates: false);

        foreach (var finding in report.Findings)
        {
            yield return new CentralItem
            {
                Kind = CentralKind.Health,
                Priority = finding.Severity switch
                {
                    HealthSeverity.Critical => CentralPriority.High,
                    HealthSeverity.Warning => CentralPriority.Medium,
                    _ => CentralPriority.Low,
                },
                Title = finding.Title,
                Detail = finding.Detail,
                ToolId = ToolCatalog.SystemManagement,
                Target = finding.Target,
            };
        }
    }
}
