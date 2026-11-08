using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Ui.Tools;

namespace Tranal.Uninstaller.Ui.Appearance;

/// <summary>
/// O painel lateral de configuracoes da lista: filtros e realces.
///
/// O painel so EDITA o estado: o conjunto de filtros (<see cref="ProgramFilterSet"/>)
/// mora no nucleo, e a janela principal consome o mesmo objeto. Assim o filtro
/// aplicado na lista e o filtro mostrado no painel nunca podem divergir.
///
/// O painel nao toca em disco, registro ou rede.
/// </summary>
public partial class SettingsSidebar : UserControl
{
    private ProgramFilterSet _filters = new();
    private readonly DispatcherTimer _searchTimer;
    private readonly Dictionary<HighlightRule, Border> _swatches = new();
    private bool _ready;

    public SettingsSidebar()
    {
        InitializeComponent();

        // A digitacao avisa com 250 ms de atraso: redesenhar a lista a cada
        // tecla trava a janela. O conjunto, esse sim, e atualizado a cada
        // tecla (veja SearchBox_TextChanged) - quem consome ve o texto novo.
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _searchTimer.Tick += SearchTimer_Tick;

        PopulateDrives(null);

        // Os rotulos vem do catalogo de idiomas: quando o idioma muda, o
        // painel se redesenha.
        Loc.LanguageChanged += OnLanguageChanged;

        _ready = true;
    }

    /// <summary>
    /// O conjunto de filtros que o painel edita. Depois de Load() e o mesmo
    /// objeto que a janela principal possui.
    /// </summary>
    public ProgramFilterSet Filters => _filters;

    /// <summary>
    /// Disparado quando o usuario muda um filtro, um realce ou clica em
    /// "limpar". A janela principal reaplica o filtro e redesenha a lista.
    /// </summary>
    public event EventHandler? FiltersChanged;

    /// <summary>
    /// Assume um conjunto de filtros e redesenha os controles.
    /// </summary>
    /// <param name="filters">O conjunto que o painel passa a editar.</param>
    public void Load(ProgramFilterSet filters)
    {
        _filters = filters ?? new ProgramFilterSet();
        RefreshControls();
    }

    /// <summary>
    /// Limpa todos os filtros e todos os realces (e dispara FiltersChanged).
    /// </summary>
    public void ClearAll()
    {
        _filters.Clear();
        RefreshControls();
        RaiseChanged();
    }

    /// <summary>
    /// Passa o texto da busca para o painel, sem mexer no resto dos
    /// controles.
    ///
    /// E o que mantem a busca da janela principal e a do painel showing o
    /// mesmo texto: as duas escrevem no mesmo conjunto de filtros.
    /// </summary>
    /// <param name="text">O texto da busca.</param>
    /// <summary>
    /// Leva o foco para o campo de busca. E o que o atalho Ctrl+F faz.
    /// </summary>
    public void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }
    public void SyncSearch(string? text)
    {
        _ready = false;
        try { SearchBox.Text = text ?? string.Empty; }
        finally { _ready = true; }
    }

    // ------------------------------------------------------------------
    // Redesenho dos controles
    // ------------------------------------------------------------------

    /// <summary>
    /// Passa o estado do conjunto para os controles, sem que isso valha como
    /// mudanca do usuario.
    /// </summary>
    private void RefreshControls()
    {
        _ready = false;

        try
        {
            SearchBox.Text = _filters.SearchText;
        RefreshExclusions();
        UpdateFilterDiagnostic();
            PublisherBox.Text = _filters.PublisherFilter;

            SelectByTag(TypeCombo, _filters.TypeFilter);
            SelectByTag(SizeCombo, _filters.SizeFilter);

            PopulateDrives(_filters.DriveFilter);

            OnlyCommentsCheck.IsChecked = _filters.OnlyWithComments;
            OnlyGroupsCheck.IsChecked = _filters.OnlyWithGroups;
            OnlyRarelyUsedCheck.IsChecked = _filters.OnlyRarelyUsed;

            // Os rotulos com chave nova no catalogo vem do codigo, para que
            // a troca de idioma tambem os atualize.
            OnlyRarelyUsedCheck.Content = Loc.Get("Sidebar.Filter.OnlyRarelyUsed");
            CustomToolsButton.Content = Loc.Get("Options.ManageCustomTools");

            _swatches.Clear();
            HighlightList.ItemsSource = _filters.Highlights;
        }
        finally
        {
            _ready = true;
        }
    }

    /// <summary>Escolhe o item do combo cujo valor e o indicado.</summary>
    private static void SelectByTag(ComboBox combo, string valor)
    {
        foreach (var item in combo.Items.OfType<ComboBoxItem>())
        {
            if (!string.Equals(item.Tag as string, valor, StringComparison.Ordinal)) continue;

            combo.SelectedItem = item;
            return;
        }
    }

    /// <summary>
    /// Monta o combo de unidades: "Todas" mais as unidades fixas do disco.
    /// </summary>
    private void PopulateDrives(string? selecionada)
    {
        DriveCombo.Items.Clear();

        var todas = new ComboBoxItem { Content = Loc.Get("Sidebar.All"), Tag = string.Empty };
        DriveCombo.Items.Add(todas);

        foreach (var drive in FixedDrives())
        {
            DriveCombo.Items.Add(new ComboBoxItem { Content = drive, Tag = drive });
        }

        var escolhida = DriveCombo.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(i => string.Equals(i.Tag as string, selecionada ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            ?? todas;

        DriveCombo.SelectedItem = escolhida;
    }

    /// <summary>As unidades fixas de disco, ordenadas.</summary>
    private static IEnumerable<string> FixedDrives()
    {
        try
        {
            return DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .Select(d => d.Name)
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            // Uma unidade que falha ao ser lida nao pode derrubar o painel:
            // o filtro de unidade simplesmente fica vazio.
            return Array.Empty<string>();
        }
    }

    // ------------------------------------------------------------------
    // Filtros
    // ------------------------------------------------------------------

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;

        // O conjunto e atualizado na hora; so o aviso espera a digitacao.
        _filters.SearchText = SearchBox.Text;

        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void PublisherBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;

        _filters.PublisherFilter = PublisherBox.Text;
        RaiseChanged();
    }

    private void TypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;

        _filters.TypeFilter = TagOf(sender) ?? "All";
        RaiseChanged();
    }

    private void SizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;

        _filters.SizeFilter = TagOf(sender) ?? "all";
        RaiseChanged();
    }

    private void DriveCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;

        var tag = TagOf(sender);
        _filters.DriveFilter = string.IsNullOrEmpty(tag) ? null : tag;
        RaiseChanged();
    }

    private void OnlyCommentsCheck_Click(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        _filters.OnlyWithComments = OnlyCommentsCheck.IsChecked == true;
        RaiseChanged();
    }

    private void OnlyGroupsCheck_Click(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        _filters.OnlyWithGroups = OnlyGroupsCheck.IsChecked == true;
        RaiseChanged();
    }

    /// <summary>
    /// Filtra a lista pelos programas usados raramente.
    ///
    /// A regra do que conta como "raro" mora no nucleo
    /// (<see cref="UsageSignals"/>); o painel so entrega o pedido.
    /// </summary>
    private void OnlyRarelyUsedCheck_Click(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;

        _filters.OnlyRarelyUsed = OnlyRarelyUsedCheck.IsChecked == true;
        RaiseChanged();
    }

    /// <summary>
    /// Abre o gerenciador das ferramentas personalizadas.
    ///
    /// A tela mora em <see cref="ToolViews"/>; o painel e so a porta de
    /// entrada.
    /// </summary>
    private void CustomToolsButton_Click(object sender, RoutedEventArgs e)
    {
        ToolViews.ShowTool(Window.GetWindow(this),
            ToolViews.CreateCustomTools(),
            Loc.Get("Module.CustomTools"),
            "ToolCustomToolsWindow");
    }

    /// <summary>
    /// Acrescenta um termo de exclusao ao filtro.
    ///
    /// A regra mora no <see cref="ProgramFilterSet"/>: a tela so entrega o
    /// termo que o usuario escreveu.
    /// </summary>
    private void AddExclusionButton_Click(object sender, RoutedEventArgs e)
    {
        var termo = (ExclusionBox.Text ?? string.Empty).Trim();
        if (termo.Length == 0) return;

        _filters.AddExclusion(termo);
        ExclusionBox.Text = string.Empty;

        RefreshExclusions();
        RaiseChanged();
    }

    /// <summary>
    /// Tira um termo de exclusao do filtro.
    /// </summary>
    private void RemoveExclusionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button botao) return;
        var termo = botao.Tag as string;
        if (string.IsNullOrEmpty(termo)) return;

        _filters.ExcludedTerms.Remove(termo);

        RefreshExclusions();
        RaiseChanged();
    }

    /// <summary>
    /// Atualiza a lista de exclusoes mostrada na tela.
    /// </summary>
    private void RefreshExclusions()
    {
        ExclusionList.Items.Clear();
        foreach (var termo in _filters.ExclusionTerms)
        {
            ExclusionList.Items.Add(termo);
        }
    }

    /// <summary>
    /// Mostra o aviso quando a busca usa uma expressao regular invalida.
    ///
    /// A regex invalida e ignorada pelo filtro: a lista continua
    /// funcionando, mas o usuario precisa saber que o filtro dele nao esta
    /// valendo.
    /// </summary>
    private void UpdateFilterDiagnostic()
    {
        if (_filters.HasInvalidRegex)
        {
            FilterDiagnosticText.Text = Loc.Get("Sidebar.Filter.RegexInvalid");
            FilterDiagnosticText.Visibility = Visibility.Visible;
            return;
        }

        FilterDiagnosticText.Text = string.Empty;
        FilterDiagnosticText.Visibility = Visibility.Collapsed;
    }
    private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        ClearAll();
    }

    // ------------------------------------------------------------------
    // Realces
    // ------------------------------------------------------------------

    private void AddHighlightButton_Click(object sender, RoutedEventArgs e)
    {
        _filters.Highlights.Add(new HighlightRule
        {
            Pattern = string.Empty,
            Scope = HighlightScope.Name,
            ColorKey = HighlightPalette.Warning,
            Enabled = true,
        });

        RaiseChanged();
    }

    private void RemoveHighlight_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        if (element.DataContext is not HighlightRule rule) return;

        _swatches.Remove(rule);
        _filters.Highlights.Remove(rule);

        RaiseChanged();
    }

    private void HighlightEnabled_Click(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        if (sender is not CheckBox check) return;
        if (check.DataContext is not HighlightRule rule) return;

        rule.Enabled = check.IsChecked == true;
        RaiseChanged();
    }

    private void HighlightPattern_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        if (sender is not TextBox box) return;
        if (box.DataContext is not HighlightRule rule) return;

        rule.Pattern = box.Text;

        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void HighlightScope_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        if (sender is not ComboBox combo) return;
        if (combo.DataContext is not HighlightRule rule) return;
        if (combo.SelectedItem is not HighlightScopeOption option) return;

        rule.Scope = option.Value;
        RaiseChanged();
    }

    private void HighlightColor_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        if (sender is not ComboBox combo) return;
        if (combo.DataContext is not HighlightRule rule) return;
        if (combo.SelectedItem is not HighlightColorOption option) return;

        rule.ColorKey = option.Value;
        UpdateSwatch(rule);
        RaiseChanged();
    }

    /// <summary>
    /// O quadradinho que mostra a cor do realce. A cor vem da paleta do
    /// nucleo - a mesma que a lista usa para pintar o programa.
    /// </summary>
    private void HighlightSwatch_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.DataContext is not HighlightRule rule) return;

        _swatches[rule] = border;
        UpdateSwatch(rule);
    }

    private void UpdateSwatch(HighlightRule rule)
    {
        if (!_swatches.TryGetValue(rule, out var border)) return;

        var hex = HighlightPalette.HexOf(rule.ColorKey);

        try
        {
            border.Background = hex is null
                ? Brushes.Transparent
                : (Brush)new BrushConverter().ConvertFromString(hex)!;
        }
        catch
        {
            // Cor invalida: o quadrado fica neutro. A cor do realce na lista
            // continua vindo da paleta do nucleo.
            border.Background = Brushes.Transparent;
        }
    }

    // ------------------------------------------------------------------
    // Avisos
    // ------------------------------------------------------------------

    private void SearchTimer_Tick(object? sender, EventArgs e)
    {
        _searchTimer.Stop();

        // O aviso da regex invalida entra aqui: e quando a busca para de
        // mudar que a expressao pode estar errada.
        UpdateFilterDiagnostic();

        RaiseChanged();
    }

    private void RaiseChanged() => FiltersChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>O valor (Tag) do item escolhido no combo.</summary>
    private static string? TagOf(object sender)
    {
        if (sender is not ComboBox combo) return null;
        if (combo.SelectedItem is not ComboBoxItem item) return null;

        return item.Tag as string;
    }

    /// <summary>
    /// O idioma mudou: o combo de unidades e os rotulos das opcoes de realce
    /// sao refeitos.
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        PopulateDrives(_filters.DriveFilter);
        RefreshControls();
    }
}

/// <summary>
/// Uma opcao de escopo de realce, com o rotulo tirado do catalogo de idiomas.
/// </summary>
public sealed class HighlightScopeOption : INotifyPropertyChanged
{
    public HighlightScopeOption(HighlightScope value, string labelKey)
    {
        Value = value;
        LabelKey = labelKey;
    }

    /// <summary>O escopo escolhido.</summary>
    public HighlightScope Value { get; }

    /// <summary>A chave de idioma do rotulo.</summary>
    public string LabelKey { get; }

    /// <summary>O rotulo no idioma ativo.</summary>
    public string Label => Loc.Get(LabelKey);

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Raise() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
}

/// <summary>
/// Uma opcao de cor de realce, com o rotulo tirado do catalogo de idiomas.
/// </summary>
public sealed class HighlightColorOption : INotifyPropertyChanged
{
    public HighlightColorOption(string value, string labelKey)
    {
        Value = value;
        LabelKey = labelKey;
    }

    /// <summary>A chave da cor (veja <see cref="HighlightPalette"/>).</summary>
    public string Value { get; }

    /// <summary>A chave de idioma do rotulo.</summary>
    public string LabelKey { get; }

    /// <summary>O rotulo no idioma ativo.</summary>
    public string Label => Loc.Get(LabelKey);

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Raise() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
}

/// <summary>
/// As opcoes de escopo e de cor dos realces.
///
/// Os rotulos vem do catalogo de idiomas: quando o idioma muda, as opcoes
/// avisam e a tela se atualiza sem recriar o painel.
/// </summary>
public static class HighlightOptions
{
    private static readonly List<HighlightScopeOption> ScopeItems = new()
    {
        new HighlightScopeOption(HighlightScope.Name, "Sidebar.Highlight.Scope.Name"),
        new HighlightScopeOption(HighlightScope.Publisher, "Sidebar.Highlight.Scope.Publisher"),
        new HighlightScopeOption(HighlightScope.InstallLocation, "Sidebar.Highlight.Scope.InstallLocation"),
        new HighlightScopeOption(HighlightScope.UninstallString, "Sidebar.Highlight.Scope.UninstallString"),
    };

    private static readonly List<HighlightColorOption> ColorItems = new()
    {
        new HighlightColorOption(HighlightPalette.Warning, HighlightPalette.LabelKeyOf(HighlightPalette.Warning)),
        new HighlightColorOption(HighlightPalette.Danger, HighlightPalette.LabelKeyOf(HighlightPalette.Danger)),
        new HighlightColorOption(HighlightPalette.Success, HighlightPalette.LabelKeyOf(HighlightPalette.Success)),
        new HighlightColorOption(HighlightPalette.Accent, HighlightPalette.LabelKeyOf(HighlightPalette.Accent)),
    };

    static HighlightOptions()
    {
        // O rotulo das opcoes e do catalogo: a troca de idioma precisa
        // aparecer nelas tambem.
        Loc.LanguageChanged += (_, _) =>
        {
            foreach (var item in ScopeItems) item.Raise();
            foreach (var item in ColorItems) item.Raise();
        };
    }

    /// <summary>Os escopos de realce, na ordem de exibicao.</summary>
    public static IReadOnlyList<HighlightScopeOption> Scopes => ScopeItems;

    /// <summary>As cores de realce, na ordem de exibicao.</summary>
    public static IReadOnlyList<HighlightColorOption> Colors => ColorItems;
}
