using System.IO;
using System.Text.Json;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Core.Notifications;

/// <summary>De onde veio a notificacao.</summary>
public enum NotificationKind
{
    /// <summary>Uma dica sobre o uso do programa.</summary>
    Tip,

    /// <summary>Um aviso sobre a maquina.</summary>
    Warning,

    /// <summary>Uma informacao sobre o que o Tranal fez.</summary>
    Info,
}

/// <summary>
/// Uma notificacao interna do Tranal.
/// </summary>
/// <param name="Id">O identificador fixo da notificacao.</param>
/// <param name="Kind">De onde ela veio.</param>
/// <param name="Title">O titulo (chave do catalogo de idiomas).</param>
/// <param name="Message">O texto (chave do catalogo de idiomas).</param>
/// <param name="CreatedAt">Quando ela foi criada.</param>
/// <param name="IsRead">O usuario ja leu?</param>
/// <param name="ScheduledFor">Quando ela deve aparecer. Nulo = ja pode aparecer.</param>
/// <param name="Sender">Quem mandou a notificacao.</param>
public sealed record InternalNotification(
    string Id,
    NotificationKind Kind,
    string Title,
    string Message,
    DateTime CreatedAt,
    bool IsRead,
    DateTime? ScheduledFor,
    string Sender = "Tranal Uninstaller");

/// <summary>
/// O arquivo onde ficam as notificacoes.
/// </summary>
internal sealed class NotificationStore
{
    public List<InternalNotification> Items { get; set; } = new();
}

/// <summary>
/// As notificacoes internas do Tranal: o "sino" da aplicacao.
///
/// Sao tres coisas que andam juntas: as **dicas** (o "voce sabia que..."), os
/// **avisos** sobre a maquina e os avisos **agendados**, que so aparecem
/// depois da hora marcada.
///
/// A regra esta em um lugar so: este modulo sabe o que e uma notificacao,
/// quando ela aparece e onde ela fica. A tela do sino, a linha de comando e
/// o agendamento consomem este mesmo modulo.
///
/// O texto das notificacoes vem do catalogo de idiomas: e texto de tela, e a
/// tela e traduzida.
/// </summary>
public static class InternalNotifications
{
    /// <summary>
    /// Substitui a pasta de dados (so a suite de testes). Nulo em uso normal.
    /// </summary>
    internal static string? FolderOverride { get; set; }

    /// <summary>A pasta onde ficam as notificacoes.</summary>
    public static string Folder =>
        FolderOverride ?? Path.Combine(RegistryBackup.DataDirectory, "notifications");

    /// <summary>O arquivo das notificacoes.</summary>
    public static string FilePath => Path.Combine(Folder, "notifications.json");

    /// <summary>
    /// As dicas que o Tranal conhece. A lista e **fixa**: e o centro de
    /// dicas, e nao muda a cada execucao.
    /// </summary>
    public static IReadOnlyList<InternalNotification> Tips { get; } = new[]
    {
        new InternalNotification("tip.drag", NotificationKind.Tip, "Tip.Drag", "Tip.DragText", DateTime.MinValue, false, null),
        new InternalNotification("tip.external", NotificationKind.Tip, "Tip.External", "Tip.ExternalText", DateTime.MinValue, false, null),
        new InternalNotification("tip.update", NotificationKind.Tip, "Tip.Update", "Tip.UpdateText", DateTime.MinValue, false, null),
        new InternalNotification("tip.language", NotificationKind.Tip, "Tip.Language", "Tip.LanguageText", DateTime.MinValue, false, null),
    };

    /// <summary>
    /// Todas as notificacoes: as guardadas, mais as dicas que ainda nao
    /// foram vistas.
    /// </summary>
    public static IReadOnlyList<InternalNotification> List()
    {
        var guardadas = Read().ToList();

        var vistas = guardadas
            .Where(n => !string.IsNullOrWhiteSpace(n.Id))
            .Select(n => n.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var dica in Tips)
        {
            if (vistas.Contains(dica.Id)) continue;
            guardadas.Add(dica with { CreatedAt = DateTime.MinValue });
        }

        return guardadas;
    }

    /// <summary>
    /// As notificacoes que devem aparecer agora: as agendadas cuja hora ja
    /// chegou, e as que nao tem hora marcada.
    /// </summary>
    public static IReadOnlyList<InternalNotification> Due()
    {
        var agora = DateTime.Now;

        return List()
            .Where(n => n.ScheduledFor is null || n.ScheduledFor <= agora)
            .ToList();
    }

    /// <summary>Quantas notificacoes nao foram lidas.</summary>
    public static int UnreadCount() => List().Count(n => !n.IsRead);

    /// <summary>
    /// Grava (ou atualiza) uma notificacao.
    /// </summary>
    /// <param name="notification">A notificacao.</param>
    public static bool Save(InternalNotification notification)
    {
        if (notification is null) return false;

        try
        {
            var itens = Read().ToList();
            itens.RemoveAll(n => string.Equals(n.Id, notification.Id, StringComparison.OrdinalIgnoreCase));
            itens.Add(notification);

            Directory.CreateDirectory(Folder);
            var json = JsonSerializer.Serialize(new NotificationStore { Items = itens }, new JsonSerializerOptions
            {
                WriteIndented = true,
            });

            File.WriteAllText(FilePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Marca a notificacao como lida.</summary>
    /// <param name="id">O identificador da notificacao.</param>
    public static bool MarkAsRead(string id)
    {
        var item = List().FirstOrDefault(n => string.Equals(n.Id, id, StringComparison.OrdinalIgnoreCase));
        if (item is null) return false;

        return Save(item with { IsRead = true });
    }

    /// <summary>
    /// Agenda uma notificacao para depois.
    /// </summary>
    /// <param name="id">O identificador da notificacao.</param>
    /// <param name="when">Quando ela deve aparecer.</param>
    /// <param name="title">A chave do titulo.</param>
    /// <param name="message">A chave do texto.</param>
    public static bool Schedule(string id, DateTime when, string title, string message)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        return Save(new InternalNotification(
            id,
            NotificationKind.Info,
            title,
            message,
            DateTime.Now,
            false,
            when));
    }

    /// <summary>Apaga as notificacoes que ja foram lidas.</summary>
    /// <returns>Quantas foram apagadas.</returns>
    public static int ClearRead()
    {
        var itens = Read().ToList();
        var lidas = itens.Where(n => n.IsRead).ToList();

        foreach (var item in lidas)
        {
            itens.Remove(item);
        }

        try
        {
            Directory.CreateDirectory(Folder);
            var json = JsonSerializer.Serialize(new NotificationStore { Items = itens }, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            return 0;
        }

        return lidas.Count;
    }

    /// <summary>
    /// O titulo e o texto traduzidos de uma notificacao.
    /// </summary>
    /// <param name="notification">A notificacao.</param>
    public static (string Title, string Message) TextOf(InternalNotification notification)
    {
        if (notification is null) return (string.Empty, string.Empty);

        return (Translate(notification.Title), Translate(notification.Message));
    }

    private static string Translate(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;

        var text = Loc.Get(key);
        return text == key ? key : text;
    }

    private static IReadOnlyList<InternalNotification> Read()
    {
        try
        {
            if (!File.Exists(FilePath)) return Array.Empty<InternalNotification>();

            var json = File.ReadAllText(FilePath);
            var loaded = JsonSerializer.Deserialize<NotificationStore>(json);
            return loaded?.Items ?? (IReadOnlyList<InternalNotification>)Array.Empty<InternalNotification>();
        }
        catch
        {
            return Array.Empty<InternalNotification>();
        }
    }
}
