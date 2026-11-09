using System;
using System.Collections.Generic;
using System.Linq;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Recovery;

namespace Tranal.Uninstaller.Core.History;

/// <summary>
/// Uma linha do historico de desinstalacoes, ja pronta para a tela.
///
/// E o "Uninstall History" do Revo: o que saiu do computador, quando saiu,
/// o que sobrou e se ainda da para recuperar. A tela, a linha de comando e a
/// recuperacao leem esta mesma linha — nao existe uma segunda interpretacao
/// do que aconteceu.
/// </summary>
public sealed class HistoryRow
{
    /// <summary>Os programas desinstalados (varios, quando a operacao foi em lote).</summary>
    public string Programs { get; init; } = string.Empty;

    /// <summary>
    /// A data em texto relativo ("Hoje", "Ontem", "3 dias atras").
    ///
    /// O tempo relativo e o que o concorrente mostra: quem desinstalou ha
    /// dois dias lembra de "ontem", nao de uma data de calendario.
    /// </summary>
    public string When { get; init; } = string.Empty;

    /// <summary>A data exata, para quando o relativo nao basta.</summary>
    public string WhenExact { get; init; } = string.Empty;

    /// <summary>O modo da desinstalacao (o nivel do assistente).</summary>
    public string Level { get; init; } = string.Empty;

    /// <summary>Quantos itens foram removidos.</summary>
    public int Removed { get; init; }

    /// <summary>Quantos itens falharam ou ficaram protegidos.</summary>
    public int Failed { get; init; }

    /// <summary>
    /// Os residuos: os itens de sobra que sairam junto.
    ///
    /// E a coluna "Residuos" do historico do concorrente — o que o Windows
    /// guardou e que o Tranal removeu por conta propria.
    /// </summary>
    public string Residues { get; init; } = string.Empty;

    /// <summary>Quantos residuos foram removidos.</summary>
    public int ResidueCount { get; init; }

    /// <summary>True quando o desinstalador nativo rodou e o programa saiu.</summary>
    public bool Uninstalled { get; init; }

    /// <summary>
    /// True quando ainda existe backup das sobras e a operacao pode ser
    /// desfeita pelo botao "Recuperar".
    /// </summary>
    public bool Recoverable { get; init; }

    /// <summary>O identificador da entrada do historico.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>O identificador do backup das sobras (o que o "Recuperar" usa).</summary>
    public string? RecoveryId { get; init; }

    /// <summary>O relatorio da remocao, quando a entrada traz um.</summary>
    public string Report { get; init; } = string.Empty;

    /// <summary>A entrada original do historico.</summary>
    public UninstallHistoryEntry Source { get; init; } = new();
}

/// <summary>
/// O historico de desinstalacoes, visto como lista de linhas.
///
/// A REGRA mora aqui, em um lugar so: quem monta a linha e quem decide se
/// a operacao pode ser desfeita. A interface grafica e a linha de comando
/// consomem estas linhas — nao ha uma segunda versao do historico.
///
/// E o que resolve a lacuna medida: o registro de desinstalacoes existia
/// (<see cref="UninstallHistory"/>), mas nao tinha tela nem leitura — o dado
/// era gravado e nunca lido.
/// </summary>
public static class HistoryReport
{
    /// <summary>
    /// Monta as linhas do historico, das mais recentes para as mais antigas.
    /// </summary>
    /// <param name="entries">As entradas do historico.</param>
    /// <param name="take">Quantas linhas trazer (padrao: todas as que existirem).</param>
    public static IReadOnlyList<HistoryRow> Build(IEnumerable<UninstallHistoryEntry>? entries, int take = 200)
    {
        var linhas = new List<HistoryRow>();
        if (entries is null) return linhas;

        foreach (var entry in entries)
        {
            if (entry is null) continue;
            if (linhas.Count >= take) break;

            var residuos = entry.RemovedItems?.Where(r => !string.IsNullOrWhiteSpace(r)).ToList()
                          ?? new List<string>();

            linhas.Add(new HistoryRow
            {
                Programs = string.IsNullOrWhiteSpace(entry.Programs) ? "(sem nome)" : entry.Programs,
                When = RelativeDate.Format(entry.Timestamp),
                WhenExact = entry.Timestamp.ToString("yyyy-MM-dd HH:mm"),
                Level = entry.Level,
                Removed = entry.Removed,
                Failed = entry.Failed,
                Residues = string.Join("; ", residuos),
                ResidueCount = residuos.Count,
                Uninstalled = entry.Uninstalled,
                Recoverable = !string.IsNullOrWhiteSpace(entry.RecoveryId),
                Id = entry.Id,
                RecoveryId = entry.RecoveryId,
                Report = entry.Report,
                Source = entry,
            });
        }

        return linhas;
    }

    /// <summary>
    /// Le o historico e ja devolve as linhas.
    ///
    /// E o atalho que as duas superficies usam: a tela e a CLI nao montam a
    /// lista por conta propria.
    /// </summary>
    /// <param name="take">Quantas linhas trazer.</param>
    public static IReadOnlyList<HistoryRow> Read(int take = 200)
    {
        return Build(UninstallHistory.Read(int.MaxValue), take);
    }

    /// <summary>
    /// Desfaz a remocao: devolve as sobras guardadas no backup da entrada.
    ///
    /// Devolve o resultado honesto — entrou, nao entrou, ou nao ha backup.
    /// A entrada que nao tem backup nao pode ser recuperada, e o Tranal
    /// diz isso em vez de fingir que deu certo.
    /// </summary>
    /// <param name="recoveryId">O identificador do backup das sobras.</param>
    public static (bool Ok, string Message) Recover(string? recoveryId)
    {
        if (string.IsNullOrWhiteSpace(recoveryId))
            return (false, "Esta operacao nao tem backup das sobras para recuperar.");

        var (ok, mensagem) = LeftoverBackup.Restore(recoveryId);
        return (ok, ok ? "As sobras foram restauradas." : mensagem);
    }

    /// <summary>
    /// Desfaz a remocao a partir de uma linha do historico.
    /// </summary>
    /// <param name="linha">A linha do historico.</param>
    public static (bool Ok, string Message) Recover(HistoryRow? linha)
    {
        return Recover(linha?.RecoveryId);
    }
}
