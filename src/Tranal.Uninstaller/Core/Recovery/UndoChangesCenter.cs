using System;
using System.Collections.Generic;
using System.Linq;

namespace Tranal.Uninstaller.Core.Recovery;

/// <summary>
/// Uma mudanca que pode ser desfeita.
/// </summary>
public sealed class UndoChangeItem
{
    /// <summary>O identificador da mudanca.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Quando a mudanca aconteceu.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>O programa que foi mexido.</summary>
    public string ProgramName { get; init; } = string.Empty;

    /// <summary>O que aconteceu ("Remocao", "Backup de sobras").</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Quantos itens foram guardados.</summary>
    public int ItemCount { get; init; }

    /// <summary>True quando a mudanca ja foi desfeita.</summary>
    public bool Undone { get; init; }

    /// <summary>A origem da mudanca ("Programa" ou "Sobras").</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Quantos arquivos o programa gravou (o perfil do backup).</summary>
    public int ProfileFiles { get; init; }

    /// <summary>Quantas pastas o programa gravou (o perfil do backup).</summary>
    public int ProfileFolders { get; init; }

    /// <summary>Quantos valores de registro o programa gravou.</summary>
    public int ProfileValues { get; init; }
}

/// <summary>
/// O resultado da leitura do centro de mudancas.
/// </summary>
public sealed class UndoChangesReport
{
    /// <summary>As mudancas, da mais recente para a mais antiga.</summary>
    public IReadOnlyList<UndoChangeItem> Items { get; init; } = Array.Empty<UndoChangeItem>();

    /// <summary>Quantas mudancas ha.</summary>
    public int Total => Items.Count;

    /// <summary>Quantas ja foram desfeitas.</summary>
    public int UndoneCount => Items.Count(i => i.Undone);
}

/// <summary>
/// O centro de mudancas: o que o Tranal mudou e como voltar atras.
///
/// E o "Undo Changes Center" do Soft Organizer, o "Recuperar" do Wise e o
/// "reverter a instalacao rastreada" do Uninstall Tool: tres nomes, uma so
/// peca.
///
/// A REGRA mora aqui, em um lugar so: quem mostra a lista e quem desfaz
/// consomem este modulo. O que sabe desfazer continua sendo quem sabe
/// restaurar — <see cref="ProgramBackup"/> para o programa e
/// <see cref="LeftoverBackup"/> para as sobras. O centro junta as duas
/// listas; ele nao reescreve a restauracao.
///
/// E o aviso que o Soft Organizer da antes de apagar ("use o Undo Changes
/// Center para cancelar") sai daqui: so ha o que desfazer quando o backup
/// foi feito.
/// </summary>
public static class UndoChangesCenter
{
    /// <summary>
    /// As mudancas que podem ser desfeitas, da mais recente para a mais
    /// antiga.
    /// </summary>
    public static UndoChangesReport Scan()
    {
        var itens = new List<UndoChangeItem>();
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // O perfil de cada programa: o que ele gravou no computador. A
        // REGRA de onde o perfil vem mora em <see cref="ProgramBackup"/>; aqui
        // ele so entra na linha.
        var perfis = new Dictionary<string, ProgramProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var perfil in ProgramBackup.ListProfiles())
        {
            if (string.IsNullOrWhiteSpace(perfil.ProgramName)) continue;
            perfis[perfil.ProgramName] = perfil;
        }

        foreach (var operacao in ProgramBackup.Operations())
        {
            var id = string.IsNullOrWhiteSpace(operacao.Id) ? operacao.BackupFolder : operacao.Id;
            if (!string.IsNullOrWhiteSpace(id)) vistos.Add(id);

            itens.Add(new UndoChangeItem
            {
                Id = id,
                Timestamp = operacao.TimestampUtc.ToLocalTime(),
                ProgramName = operacao.ProgramName,
                Description = string.IsNullOrWhiteSpace(operacao.Description)
                    ? "Remocao"
                    : operacao.Description,
                ItemCount = operacao.ItemCount,
                Undone = operacao.Undone,
                Kind = "Programa",
                ProfileFiles = PerfilDe(perfis, operacao.ProgramName, p => p.Files.Count),
                ProfileFolders = PerfilDe(perfis, operacao.ProgramName, p => p.Folders.Count),
                ProfileValues = PerfilDe(perfis, operacao.ProgramName, p => p.RegistryValues.Count),
            });
        }

        foreach (var manifesto in LeftoverBackup.List())
        {
            if (string.IsNullOrWhiteSpace(manifesto.Id)) continue;
            if (vistos.Contains(manifesto.Id)) continue;

            itens.Add(new UndoChangeItem
            {
                Id = manifesto.Id,
                Timestamp = manifesto.CreatedUtc.ToLocalTime(),
                ProgramName = manifesto.ProgramName,
                Description = "Backup de sobras",
                ItemCount = manifesto.Items.Count,
                Undone = false,
                Kind = "Sobras",
            });
        }

        return new UndoChangesReport
        {
            Items = itens.OrderByDescending(i => i.Timestamp).ToList(),
        };
    }

    /// <summary>
    /// Um numero do perfil do programa (zero quando o perfil nao existe).
    /// </summary>
    /// <param name="perfis">Os perfis, por nome de programa.</param>
    /// <param name="programName">O nome do programa.</param>
    /// <param name="contador">O que contar no perfil.</param>
    private static int PerfilDe(
        Dictionary<string, ProgramProfile> perfis,
        string? programName,
        Func<ProgramProfile, int> contador)
    {
        if (string.IsNullOrWhiteSpace(programName)) return 0;
        if (!perfis.TryGetValue(programName!, out var perfil)) return 0;
        return contador(perfil);
    }

    /// <summary>
    /// Desfaz a mudanca.
    /// </summary>
    /// <param name="id">O identificador da mudanca.</param>
    /// <returns>True quando a mudanca foi desfeita.</returns>
    public static (bool Ok, string Message) Undo(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return (false, "nenhuma mudanca escolhida.");

        foreach (var operacao in ProgramBackup.Operations())
        {
            var operacaoId = string.IsNullOrWhiteSpace(operacao.Id) ? operacao.BackupFolder : operacao.Id;
            if (!string.Equals(operacaoId, id, StringComparison.OrdinalIgnoreCase)) continue;

            return ProgramBackup.Undo(operacao);
        }

        foreach (var manifesto in LeftoverBackup.List())
        {
            if (!string.Equals(manifesto.Id, id, StringComparison.OrdinalIgnoreCase)) continue;

            return LeftoverBackup.Restore(manifesto.Id);
        }

        return (false, $"nao ha mudanca guardada com o identificador '{id}'.");
    }

    /// <summary>
    /// Refaz a mudanca (apaga de novo o que tinha sido desfeito).
    /// </summary>
    /// <param name="id">O identificador da mudanca.</param>
    /// <returns>True quando a mudanca foi refeita.</returns>
    public static (bool Ok, string Message) Redo(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return (false, "nenhuma mudanca escolhida.");

        foreach (var operacao in ProgramBackup.Operations())
        {
            var operacaoId = string.IsNullOrWhiteSpace(operacao.Id) ? operacao.BackupFolder : operacao.Id;
            if (!string.Equals(operacaoId, id, StringComparison.OrdinalIgnoreCase)) continue;

            return ProgramBackup.Redo(operacao);
        }

        return (false, $"a mudanca '{id}' nao pode ser refeita: o backup de sobras nao guarda o estado inverso.");
    }
}
