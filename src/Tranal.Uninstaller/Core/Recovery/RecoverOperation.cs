using System.Text.Json;

namespace Tranal.Uninstaller.Core.Recovery;

/// <summary>
/// Uma operacao de remocao que pode ser desfeita.
///
/// E o "Undo Changes Center" do Soft Organizer, o "Recuperar" do Wise e o
/// "reverter a instalacao rastreada" do Uninstall Tool: tres nomes, uma so
/// peca.
///
/// A operacao guarda ONDE o conteudo foi parar (a pasta do backup) e o que
/// era (itens e valores de registro). Desfazer devolve; refazer apaga de
/// novo.
/// </summary>
public sealed class RecoverOperation
{
    /// <summary>O identificador da operacao.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>A descricao curta (o que aconteceu).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Quando a operacao aconteceu (UTC).</summary>
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    /// <summary>O nome do programa.</summary>
    public string ProgramName { get; set; } = string.Empty;

    /// <summary>A pasta do backup com o conteudo guardado.</summary>
    public string BackupFolder { get; set; } = string.Empty;

    /// <summary>Os itens que foram removidos (com o conteudo guardado).</summary>
    public List<LeftoverBackupItem> Items { get; set; } = new();

    /// <summary>True quando a operacao ja foi desfeida.</summary>
    public bool Undone { get; set; }

    /// <summary>
    /// Quantos itens a operacao removeu (so os que contam: arquivo, pasta e
    /// registro).
    /// </summary>
    public int ItemCount => Items.Count(i =>
        i.Kind is "Arquivo" or "Pasta" or "Registro");

    /// <summary>Uma linha que descreve a operacao.</summary>
    public string Describe() =>
        $"{TimestampUtc.ToLocalTime():yyyy-MM-dd HH:mm} — {Description}"
        + (ProgramName.Length > 0 ? $" ({ProgramName})" : string.Empty);

    /// <summary>
    /// A mesma operacao em JSON: e assim que ela sobrevive ao fechar o
    /// aplicativo.
    ///
    /// O texto sai numa linha só: o arquivo das operacoes e JSON Lines (uma
    /// operacao por linha), e um JSON com quebras estragaria a leitura da
    /// linha seguinte.
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>
    /// Le uma operacao do JSON.
    /// </summary>
    /// <param name="json">O texto JSON.</param>
    public static RecoverOperation? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<RecoverOperation>(json, JsonOptions);
        }
        catch (JsonException) { return null; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
    };
}
