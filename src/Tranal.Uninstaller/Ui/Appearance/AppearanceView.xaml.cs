using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Ui.Appearance;

/// <summary>
/// A tela de aparencia: o mapa de area dos programas, a legenda de cores, o
/// agrupamento por coluna e a classificacao por estrelas.
///
/// A tela **nao calcula nada**: ela consome os modelos do nucleo
/// (<see cref="TreemapModel"/>, <see cref="ColorLegend"/>,
/// <see cref="GroupingModel"/> e <see cref="ProgramRating"/>). Se a tela
/// tivesse a propria conta do tamanho ou a propria lista de cores, a lista e
/// a tela diriam coisas diferentes sobre o mesmo programa.
///
/// A tela nao depende da janela principal: quem integra e quem decide o
/// momento de mostrar e de carregar a lista.
/// </summary>
public partial class AppearanceView : UserControl
{
    /// <summary>As colunas que fazem sentido como agrupamento.</summary>
    private static readonly ProgramColumn[] ColunasDeAgrupamento =
    {
        ProgramColumn.Company,
        ProgramColumn.UninstallerKind,
        ProgramColumn.Source,
        ProgramColumn.Type,
        ProgramColumn.Date,
        ProgramColumn.Version,
        ProgramColumn.Size,
        ProgramColumn.Group,
        ProgramColumn.SystemComponent,
        ProgramColumn.Protected,
        ProgramColumn.Startup,
        ProgramColumn.Rating,
    };

    private IReadOnlyList<InstalledProgram> _programas = Array.Empty<InstalledProgram>();
    private IReadOnlyList<EstrelaItem> _estrelas = Array.Empty<EstrelaItem>();
    private InstalledProgram? _selecionado;
    private bool _atualizandoEstrela;

    /// <summary>
    /// Cria a tela. Tudo que ela mostra vem dos modelos do nucleo.
    /// </summary>
    public AppearanceView()
    {
        InitializeComponent();

        MontarLegenda();
        MontarColunasDeAgrupamento();
        MontarEstrelas();
    }

    /// <summary>
    /// Carrega a lista de programas: o mapa de area e o agrupamento sao
    /// refeitos com os dados recebidos.
    /// </summary>
    /// <param name="programs">Os programas instalados.</param>
    public void CarregarProgramas(IEnumerable<InstalledProgram>? programs)
    {
        _programas = programs is null
            ? Array.Empty<InstalledProgram>()
            : programs.Where(p => p is not null).ToList();

        DesenharTreemap();
        AtualizarAgrupamento();

        if (_selecionado is not null) SelecionarPrograma(_selecionado);
    }

    /// <summary>
    /// Escolhe o programa cujas estrelas serao mostradas (e alteradas).
    /// </summary>
    /// <param name="program">O programa escolhido.</param>
    public void SelecionarPrograma(InstalledProgram? program)
    {
        _selecionado = program;
        StatusText.Text = string.Empty;

        RatingProgramName.Text = program is null
            ? "Nenhum programa selecionado"
            : program.DisplayName;

        RatingBox.IsEnabled = program is not null;

        if (program is null)
        {
            RatingBox.SelectedItem = null;
            return;
        }

        var nota = ProgramCustomizationStore.RatingOf(program);

        // A troca de item dispara o evento de mudanca: sem esta guarda, a
        // tela gravaria no disco a mesma nota que acabou de ler.
        _atualizandoEstrela = true;
        try
        {
            RatingBox.SelectedItem = _estrelas.FirstOrDefault(i => i.Valor == nota);
        }
        finally
        {
            _atualizandoEstrela = false;
        }
    }

    // ---------------------------------------------------------------------
    // Legenda
    // ---------------------------------------------------------------------

    /// <summary>
    /// Monta a legenda a partir do modelo. A tela nao escreve o que a cor
    /// significa: ela mostra o que o modelo diz.
    /// </summary>
    private void MontarLegenda()
    {
        var linhas = new List<LegendaLinha>();

        foreach (var entrada in ColorLegend.Entries)
        {
            linhas.Add(new LegendaLinha
            {
                Rotulo = entrada.Label,
                Significado = entrada.Meaning,
                Cor = Cor(entrada.Color),
            });
        }

        LegendItems.ItemsSource = linhas;

        // O template e montado por codigo porque o tipo da linha e da tela:
        // o modelo do nucleo nao conhece WPF.
        var painel = new FrameworkElementFactory(typeof(StackPanel));
        painel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        painel.SetValue(StackPanel.MarginProperty, new Thickness(0, 2, 0, 2));

        var amostra = new FrameworkElementFactory(typeof(Border));
        amostra.SetValue(Border.WidthProperty, 12.0);
        amostra.SetValue(Border.HeightProperty, 12.0);
        amostra.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
        amostra.SetValue(Border.BackgroundProperty, new Binding(nameof(LegendaLinha.Cor)));
        amostra.SetValue(Border.MarginProperty, new Thickness(0, 0, 6, 0));
        amostra.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);

        var rotulo = new FrameworkElementFactory(typeof(TextBlock));
        rotulo.SetValue(TextBlock.TextProperty, new Binding(nameof(LegendaLinha.Rotulo)));
        rotulo.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        rotulo.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

        var significado = new FrameworkElementFactory(typeof(TextBlock));
        significado.SetValue(TextBlock.TextProperty, new Binding(nameof(LegendaLinha.Significado)));
        significado.SetValue(TextBlock.ForegroundProperty, FindBrush("TextDimBrush"));
        significado.SetValue(TextBlock.MarginProperty, new Thickness(6, 0, 0, 0));
        significado.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        significado.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

        painel.AppendChild(amostra);
        painel.AppendChild(rotulo);
        painel.AppendChild(significado);

        LegendItems.ItemTemplate = new DataTemplate { VisualTree = painel };
    }

    // ---------------------------------------------------------------------
    // Agrupamento
    // ---------------------------------------------------------------------

    /// <summary>
    /// Monta a lista de colunas que podem agrupar a lista.
    /// </summary>
    private void MontarColunasDeAgrupamento()
    {
        var colunas = new List<ColunaItem>();
        foreach (var coluna in ColunasDeAgrupamento)
            colunas.Add(new ColunaItem { Coluna = coluna, Rotulo = ProgramColumns.Label(coluna) });

        GroupColumnBox.ItemsSource = colunas;
        GroupColumnBox.SelectedIndex = 0;
    }

    /// <summary>
    /// Refaz o agrupamento com a coluna escolhida.
    /// </summary>
    private void AtualizarAgrupamento()
    {
        GroupTree.Items.Clear();

        if (GroupColumnBox.SelectedItem is not ColunaItem item) return;

        var grupos = GroupingModel.Group(_programas, item.Coluna);

        foreach (var grupo in grupos)
        {
            var cabecalho = new TreeViewItem
            {
                Header = $"{grupo.Name}  ({grupo.Count})",
                IsExpanded = false,
            };

            foreach (var programa in grupo.Programs)
            {
                var folha = new TreeViewItem { Header = programa.DisplayName };
                folha.Tag = programa;
                cabecalho.Items.Add(folha);
            }

            GroupTree.Items.Add(cabecalho);
        }
    }

    private void GroupColumnBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        AtualizarAgrupamento();
    }

    // ---------------------------------------------------------------------
    // Mapa de area
    // ---------------------------------------------------------------------

    /// <summary>
    /// Desenha o mapa de area dos programas. O desenho vem do modelo do
    /// nucleo: a tela so converte os retangulos em formas.
    /// </summary>
    private void DesenharTreemap()
    {
        TreemapCanvas.Children.Clear();

        var largura = TreemapCanvas.ActualWidth;
        var altura = TreemapCanvas.ActualHeight;
        if (largura <= 0 || altura <= 0) return;

        var modelo = TreemapModel.FromPrograms(_programas, largura, altura);
        var porChave = new Dictionary<string, InstalledProgram>(StringComparer.OrdinalIgnoreCase);

        foreach (var programa in _programas)
        {
            var chave = programa.RegistryPath;
            if (string.IsNullOrWhiteSpace(chave)) chave = programa.DisplayName;
            porChave[chave] = programa;
        }

        foreach (var rect in modelo.Rectangles)
        {
            porChave.TryGetValue(rect.Key, out var programa);

            var cor = Cor(ColorLegend.ForKind(programa?.Kind ?? UninstallerKind.Unknown).Color);

            var forma = new Rectangle
            {
                Width = Math.Max(0, rect.Width - 1),
                Height = Math.Max(0, rect.Height - 1),
                Fill = cor,
                Stroke = Brushes.Transparent,
                ToolTip = programa?.DisplayName ?? rect.Key,
            };

            Canvas.SetLeft(forma, rect.X);
            Canvas.SetTop(forma, rect.Y);
            TreemapCanvas.Children.Add(forma);

            // O nome so entra quando o retangulo tem espaco para ele: um
            // texto espremido e pior do que nenhum texto.
            if (rect.Width < 70 || rect.Height < 20) continue;

            var nome = new TextBlock
            {
                Text = programa?.DisplayName ?? rect.Key,
                Foreground = TextoSobre(cor),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = Math.Max(0, rect.Width - 8),
            };

            Canvas.SetLeft(nome, rect.X + 4);
            Canvas.SetTop(nome, rect.Y + 3);
            TreemapCanvas.Children.Add(nome);
        }

        TreemapSummary.Text = modelo.TotalSize > 0
            ? $"{modelo.Entries.Count} {Loc.Get("Common.Items")} — {modelo.TotalSize:N0} {Loc.Get("Common.Bytes")}"
            : "Nenhum programa com tamanho medido";
    }

    private void TreemapCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DesenharTreemap();
    }

    // ---------------------------------------------------------------------
    // Estrelas
    // ---------------------------------------------------------------------

    /// <summary>
    /// Monta a lista de classificacoes possiveis: de "sem classificacao" ate
    /// cinco estrelas.
    /// </summary>
    private void MontarEstrelas()
    {
        var itens = new List<EstrelaItem>();
        for (var valor = 0; valor <= ProgramRating.Max; valor++)
        {
            itens.Add(new EstrelaItem
            {
                Valor = valor,
                Texto = valor == 0 ? "Sem classificação" : ProgramRating.Stars(valor),
            });
        }

        _estrelas = itens;
        RatingBox.ItemsSource = itens;
        RatingBox.DisplayMemberPath = nameof(EstrelaItem.Texto);
        RatingBox.SelectedIndex = 0;
    }

    private void RatingBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (_atualizandoEstrela) return;
        if (_selecionado is null) return;
        if (RatingBox.SelectedItem is not EstrelaItem item) return;

        // A gravacao e atomica e devolve false quando falhou: a estrela sumiria
        // quando o aplicativo fechasse, e o usuario precisa saber.
        if (ProgramCustomizationStore.SetRating(_selecionado, item.Valor))
        {
            StatusText.Text = string.Empty;
            AtualizarAgrupamento();
            return;
        }

        StatusText.Text = "Não foi possível gravar a classificação.";
    }

    // ---------------------------------------------------------------------
    // Ajuda
    // ---------------------------------------------------------------------

    /// <summary>
    /// Converte a cor hexadecimal do modelo em pincel. A conversao fica aqui
    /// porque o nucleo nao pode depender de WPF.
    /// </summary>
    private static Brush Cor(string hexadecimal)
    {
        try
        {
            if (ColorConverter.ConvertFromString(hexadecimal) is Color cor)
                return new SolidColorBrush(cor);
        }
        catch (FormatException)
        {
            // Cor invalida no modelo: o cinza neutro evita que a tela quebre.
        }

        return new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));
    }

    /// <summary>
    /// O texto que contrasta com o fundo: claro em fundo escuro, escuro em
    /// fundo claro.
    /// </summary>
    private static Brush TextoSobre(Brush fundo)
    {
        if (fundo is SolidColorBrush solido)
        {
            var cor = solido.Color;
            var luma = (0.299 * cor.R + 0.587 * cor.G + 0.114 * cor.B) / 255.0;
            return new SolidColorBrush(luma > 0.6 ? Colors.Black : Colors.White);
        }

        return Brushes.Black;
    }

    /// <summary>
    /// O pincel do tema, quando ele existe.
    /// </summary>
    private static Brush FindBrush(string key) =>
        Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;

    /// <summary>Uma linha da legenda, como a tela mostra.</summary>
    private sealed class LegendaLinha
    {
        public string Rotulo { get; set; } = string.Empty;
        public string Significado { get; set; } = string.Empty;
        public Brush Cor { get; set; } = Brushes.Gray;
    }

    /// <summary>Uma coluna que pode agrupar a lista.</summary>
    private sealed class ColunaItem
    {
        public ProgramColumn Coluna { get; set; }
        public string Rotulo { get; set; } = string.Empty;
    }

    /// <summary>Uma classificacao possivel na lista de estrelas.</summary>
    private sealed class EstrelaItem
    {
        public int Valor { get; set; }
        public string Texto { get; set; } = string.Empty;
    }
}
