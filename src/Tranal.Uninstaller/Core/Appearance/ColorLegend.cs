using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// O que a cor de uma entrada da legenda significa.
/// </summary>
public enum ColorLegendSubject
{
    /// <summary>
    /// A confianca da sobra (<see cref="LeftoverConfidence"/>). E a cor que
    /// o usuario ve na lista de sobras.
    /// </summary>
    Confidence,

    /// <summary>
    /// O tipo do desinstalador (<see cref="UninstallerKind"/>). E a cor que
    /// identifica a familia do desinstalador do programa.
    /// </summary>
    UninstallerKind,
}

/// <summary>
/// Uma entrada da legenda: a cor, o rotulo e o que a cor significa.
///
/// A cor e um texto hexadecimal ("#RRGGBB") e nao um <c>Brush</c>: o nucleo
/// nao pode depender de WPF nem de <c>System.Drawing</c> — e a GUI e a CLI
/// compilam o mesmo codigo. Quem desenha converte o texto.
/// </summary>
/// <param name="Subject">A familia a que a entrada pertence.</param>
/// <param name="Key">A identidade da entrada, estavel e independente do idioma.</param>
/// <param name="Label">O rotulo, exatamente como a lista mostra.</param>
/// <param name="Meaning">A explicacao do que a cor significa.</param>
/// <param name="Color">A cor, em hexadecimal ("#RRGGBB").</param>
public sealed record ColorLegendEntry(
    ColorLegendSubject Subject,
    string Key,
    string Label,
    string Meaning,
    string Color);

/// <summary>
/// A legenda de cores: a lista que diz o que cada cor da tela significa.
///
/// A legenda e a **unica fonte** dessa leitura. A lista de sobras usa cores
/// para a confianca e o mapa de area usa cores para o tipo do desinstalador;
/// se a legenda fosse escrita a mao em cada um dos dois lugares, as duas
/// divergiriam na primeira cor trocada — e o usuario leria mal a tela.
///
/// Por isso as entradas **derivam das enums do nucleo**
/// (<see cref="LeftoverConfidence"/> e <see cref="UninstallerKind"/>): um
/// membro novo da enum aparece na legenda sozinho, e o rotulo vem do mesmo
/// metodo que a lista usa (<see cref="LeftoverLabels.Confidence"/>), para que
/// lista e legenda nunca discordem.
/// </summary>
public static class ColorLegend
{
    /// <summary>
    /// As cores da familia do desinstalador, na ordem. O tipo recebe a cor
    /// pela posicao dele na enum: o resultado e deterministico e nao depende
    /// de nenhuma lista paralela de cores.
    /// </summary>
    private static readonly string[] Palette =
    {
        "#4E79A7", "#F28E2B", "#59A14F", "#E15759", "#B07AA1", "#76B7B2",
        "#EDC948", "#9C755F", "#BAB0AC", "#8CD17D", "#D37295", "#A0CBE8",
    };

    /// <summary>
    /// A cor da confianca da sobra.
    ///
    /// Confianca alta e a unica que entra marcada para remocao — a cor
    /// vermelha alerta para isso. Confianca media e um palpite, e confianca
    /// baixa e um palpite fraco: as duas ficam em tom neutro para nao
    /// parecerem decisao.
    /// </summary>
    private static string ColorOf(LeftoverConfidence confidence) => confidence switch
    {
        LeftoverConfidence.High => "#D32F2F",
        LeftoverConfidence.Medium => "#F9A825",
        _ => "#9E9E9E",
    };

    /// <summary>Todas as entradas da legenda, na ordem: confianca e depois tipo.</summary>
    public static IReadOnlyList<ColorLegendEntry> Entries { get; } = Build();

    /// <summary>
    /// A entrada da legenda de um nivel de confianca.
    /// </summary>
    /// <param name="confidence">A confianca da sobra.</param>
    public static ColorLegendEntry ForConfidence(LeftoverConfidence confidence) =>
        Entries.First(e => e.Subject == ColorLegendSubject.Confidence
            && string.Equals(e.Key, KeyOf(confidence), StringComparison.Ordinal));

    /// <summary>
    /// A entrada da legenda de um tipo de desinstalador.
    /// </summary>
    /// <param name="kind">O tipo do desinstalador.</param>
    public static ColorLegendEntry ForKind(UninstallerKind kind) =>
        Entries.First(e => e.Subject == ColorLegendSubject.UninstallerKind
            && string.Equals(e.Key, KeyOf(kind), StringComparison.Ordinal));

    /// <summary>
    /// Monta as entradas a partir das enums do nucleo.
    ///
    /// Nao existe uma lista de meanings escrita a mao: cada entrada nasce de
    /// um membro da enum. Um membro novo entra na legenda sem ninguem ter de
    /// lembrar de adicionar a linha.
    /// </summary>
    private static IReadOnlyList<ColorLegendEntry> Build()
    {
        var entries = new List<ColorLegendEntry>();

        foreach (var confidence in Enum.GetValues<LeftoverConfidence>())
        {
            entries.Add(new ColorLegendEntry(
                ColorLegendSubject.Confidence,
                KeyOf(confidence),
                // O rotulo e o mesmo metodo que a lista usa: a lista e a
                // legenda nao podem discordar do texto.
                LeftoverLabels.Confidence(confidence),
                MeaningOfConfidence(confidence),
                ColorOf(confidence)));
        }

        foreach (var kind in Enum.GetValues<UninstallerKind>())
        {
            entries.Add(new ColorLegendEntry(
                ColorLegendSubject.UninstallerKind,
                KeyOf(kind),
                // A coluna "Tipo de desinstalador" mostra o identificador fixo
                // do tipo; a legenda mostra o mesmo texto.
                kind.ToString(),
                MeaningOfKind(kind),
                ColorOf(kind)));
        }

        return entries;
    }

    /// <summary>A identidade da entrada de uma confianca.</summary>
    private static string KeyOf(LeftoverConfidence confidence) =>
        "Confidence." + confidence.ToString();

    /// <summary>A identidade da entrada de um tipo de desinstalador.</summary>
    private static string KeyOf(UninstallerKind kind) =>
        "Kind." + kind.ToString();

    /// <summary>A cor de um tipo de desinstalador, pela posicao dele na enum.</summary>
    private static string ColorOf(UninstallerKind kind)
    {
        var index = (int)kind;
        if (index < 0) index = 0;
        return Palette[index % Palette.Length];
    }

    /// <summary>O que a cor da confianca significa.</summary>
    private static string MeaningOfConfidence(LeftoverConfidence confidence) => confidence switch
    {
        LeftoverConfidence.High => Text("Legend.Confidence.High", "Item marcado para remoção"),
        LeftoverConfidence.Medium => Text("Legend.Confidence.Medium", "Provavelmente sobra do programa"),
        _ => Text("Legend.Confidence.Low", "Pode ser sobra do programa"),
    };

    /// <summary>O que a cor do tipo de desinstalador significa.</summary>
    private static string MeaningOfKind(UninstallerKind kind) => kind switch
    {
        UninstallerKind.MsiExec => Text("Legend.Kind.MsiExec", "Desinstalado pelo Windows Installer"),
        UninstallerKind.InnoSetup => Text("Legend.Kind.InnoSetup", "Empacotado com Inno Setup"),
        UninstallerKind.Nsis => Text("Legend.Kind.Nsis", "Empacotado com NSIS"),
        UninstallerKind.InstallShield => Text("Legend.Kind.InstallShield", "Empacotado com InstallShield"),
        UninstallerKind.SdbInst => Text("Legend.Kind.SdbInst", "Pacote de driver do Windows Installer"),
        UninstallerKind.Steam => Text("Legend.Kind.Steam", "Instalado pela Steam"),
        UninstallerKind.StoreApp => Text("Legend.Kind.StoreApp", "Aplicativo da Microsoft Store"),
        UninstallerKind.Chocolatey => Text("Legend.Kind.Chocolatey", "Instalado pelo Chocolatey"),
        UninstallerKind.Oculus => Text("Legend.Kind.Oculus", "Aplicativo da Oculus Store"),
        UninstallerKind.PowerShell => Text("Legend.Kind.PowerShell", "Desinstalado por script PowerShell"),
        UninstallerKind.SimpleDelete => Text("Legend.Kind.SimpleDelete", "Desinstalado apagando os arquivos"),
        _ => Text("Legend.Kind.Unknown", "Tipo de desinstalador não identificado"),
    };

    /// <summary>
    /// O texto traduzido, com o texto de reserva quando a chave nao existe no
    /// catalogo do idioma ativo.
    /// </summary>
    private static string Text(string key, string fallback)
    {
        var text = Loc.Get(key);
        return text == key ? fallback : text;
    }
}
