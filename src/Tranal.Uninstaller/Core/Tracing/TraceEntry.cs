using System.Globalization;

namespace Tranal.Uninstaller.Core.Tracing;

/// <summary>
/// O tipo da mudanca observada no sistema durante o rastreio.
///
/// O log legado (<c>TraceStore</c>) guarda apenas a lista de arquivos, pastas e
/// chaves que surgiram: ele diz que algo nasceu, mas nao diz <b>como</b>. Sem
/// isso nao da para distinguir um arquivo novo de um arquivo sobrescrito, nem
/// saber o nome anterior de um item renomeado — e a desinstalacao dirigida
/// pelo log perde a informacao mais valiosa do rastreio.
///
/// Este enum e a fonte unica do significado: quem grava, quem le e quem
/// mostra consomem estes valores.
/// </summary>
public enum TraceChangeKind
{
    /// <summary>
    /// O item nasceu durante a janela de rastreio (arquivo, pasta ou chave).
    /// </summary>
    Created,

    /// <summary>
    /// O item existia e deixou de existir durante a janela de rastreio.
    /// </summary>
    Deleted,

    /// <summary>
    /// O item mudou de nome. O nome anterior fica em
    /// <see cref="TraceEntry.OldName"/>.
    /// </summary>
    Renamed,

    /// <summary>
    /// O item ja existia e teve o conteudo (ou o tamanho) trocado.
    /// </summary>
    Overwritten,

    /// <summary>
    /// O tipo da mudanca nao pode ser afirmar com prova. E o estado honesto
    /// quando o watcher perdeu a informacao (por exemplo, quando o buffer de
    /// eventos do sistema transbordou).
    /// </summary>
    Unknown,
}

/// <summary>
/// Uma entrada do log de rastreio: uma mudanca observada no sistema.
///
/// A entrada e a unidade atomica do log novo (o formato canonico do Lote B).
/// O log legado guarda listas de caminhos; este guarda o <b>que</b> aconteceu
/// com cada caminho — o tipo da mudanca, o nome anterior e o dado anterior.
/// </summary>
public sealed class TraceEntry
{
    /// <summary>
    /// Caminho absoluto do item que mudou. Para uma chave de registro, e o
    /// caminho canonico no formato do regedit.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// O tipo da mudanca observada.
    /// </summary>
    public TraceChangeKind Kind { get; init; }

    /// <summary>
    /// O nome anterior do item, quando a mudanca foi uma renomeacao.
    /// Vazio quando o item nao mudou de nome.
    /// </summary>
    public string? OldName { get; init; }

    /// <summary>
    /// O dado anterior do item: o caminho de origem em uma renomeacao, ou o
    /// tamanho medido antes da alteracao. Vazio quando nao ha dado anterior
    /// conhecido.
    /// </summary>
    public string? OldValue { get; init; }

    /// <summary>
    /// O dado novo do item: o caminho de destino em uma renomeacao, ou o
    /// tamanho medido depois da alteracao.
    /// </summary>
    public string? NewValue { get; init; }

    /// <summary>
    /// Tamanho do item em bytes no momento da observacao. Nulo quando o item
    /// nao tem tamanho (ou quando o tamanho nao pode ser lido).
    /// </summary>
    public long? Size { get; init; }

    /// <summary>
    /// Quando a mudanca foi observada (UTC).
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// O processo que fez a mudanca, quando deu para saber. Fica vazio quando
    /// o mecanismo de observacao nao informa o autor da escrita.
    /// </summary>
    public string? OwnerProcess { get; init; }

    /// <summary>
    /// True quando o item e uma pasta (e nao um arquivo).
    /// </summary>
    public bool IsDirectory { get; init; }

    /// <summary>
    /// A identidade da entrada: caminho + tipo de mudanca + carimbo.
    ///
    /// E a chave que decide se duas entradas sao a mesma mudanca, e por isso
    /// mora em um lugar so — e o que permite unir dois logs do mesmo programa
    /// sem duplicar entrada.
    /// </summary>
    public string Identity() => string.Concat(
        (Path ?? string.Empty).ToUpperInvariant(),
        "|",
        Kind.ToString(),
        "|",
        Timestamp.UtcTicks.ToString(CultureInfo.InvariantCulture));
}
