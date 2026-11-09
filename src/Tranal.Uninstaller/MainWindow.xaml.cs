using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Reporting;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Core.Updates;
using Tranal.Uninstaller.Theming;
using Tranal.Uninstaller.Ui;
using Tranal.Uninstaller.Ui.Appearance;
using Tranal.Uninstaller.Ui.Discovery;

namespace Tranal.Uninstaller;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ProgramRow> _all = new();
    private readonly ICollectionView _view;

    /// <summary>
    /// A linha sobre a qual o menu de contexto foi aberto. A selecao da lista
    /// e multipla (desinstalacao em lote), entao "SelectedItem" nao serve:
    /// o menu tem de agir sobre a linha clicada.
    /// </summary>
    private ProgramRow? _contextRow;

    private string _module = "AllPrograms";

    /// <summary>
    /// Aviso sobre o arquivo de preferencias do usuario (comentarios,
    /// grupos, icones). Vazio quando esta tudo bem.
    ///
    /// O aviso fica na barra de status: e o lugar onde a janela ja diz o que
    /// esta acontecendo, e nao some sozinho.
    /// </summary>
    private string _avisoPreferencias = string.Empty;

    /// <summary>
    /// O estado dos filtros da lista.
    ///
    /// A REGRA mora no nucleo (<see cref="ProgramFilterSet"/>): o popup de
    /// filtros, a caixa de busca e o painel lateral editam este mesmo
    /// objeto, e a lista consome o julgamento dele. Nenhuma das telas
    /// reinterpreta o filtro por conta propria.
    /// </summary>
    private readonly ProgramFilterSet _filters = new();

    /// <summary>
    /// True quando o painel lateral de configuracoes esta aberto.
    /// </summary>
    private bool _settingsPanelOpen;

    public MainWindow()
    {
        InitializeComponent();

        // Window.Title nao aceita binding: o texto vem do catalogo.
        Title = Loc.Get("App.Title");

        _view = CollectionViewSource.GetDefaultView(_all);
        _view.Filter = FilterRow;
        ProgramsList.ItemsSource = _view;

        var group = new PropertyGroupDescription(nameof(ProgramRow.Group));
        _view.GroupDescriptions.Add(group);
        _view.SortDescriptions.Add(new SortDescription(nameof(ProgramRow.DisplayName),
            ListSortDirection.Ascending));

        // A grade nasce do catalogo de colunas: e o usuario que escolhe
        // quais colunas quer ver.
        BuildProgramColumns();

        // O painel lateral de configuracoes edita o mesmo conjunto de filtros
        // que o popup e a caixa de busca: e o que impede a lista e o painel de
        // discordar sobre o que esta filtrado.
        SettingsSidebarPanel.Load(_filters);
        SettingsSidebarPanel.FiltersChanged += SettingsSidebar_FiltersChanged;

        // Os atalhos de teclado entram pela tabela do nucleo
        // (Core/Shortcuts/ShortcutMap.cs): a janela so traduz a tecla
        // pressionada e pergunta o que fazer.
        PreviewKeyDown += Window_PreviewKeyDown;

        _checkBoxesInDetails = AppSettingsStore.Current.ShowCheckBoxesInDetailsView;

        // Telas de modulos e ferramentas registradas uma unica vez.
        ViewSetup.RegisterAll(this);

        // Visibilidade dos paineis conforme as preferencias.
        ApplyLayout();

        // As preferencias de layout tambem podem mudar pela janela de Opcoes:
        // o painel tem de refletir a mudanca sem esperar o proximo arranque.
        AppSettingsStore.Saved += OnSettingsSaved;
        Closed += (_, _) => AppSettingsStore.Saved -= OnSettingsSaved;

        // Textos do codigo-behind tambem mudam com o idioma.
        Loc.LanguageChanged += (_, _) => RefreshLocalizedTexts();
        Loaded += async (_, _) =>
        {
            await ReloadAsync();
            FocusStartupModule();
            _ = CheckForUpdateOnStartup();
        };
    }

    /// <summary>
    /// Verifica, ao iniciar, se ha versao nova do Tranal.
    ///
    /// Corresponde a opcao "Verificar por atualizacoes ao iniciar" do Revo
    /// (e a "Atualizar automaticamente em segundo plano", que baixa o pacote
    /// sem atrapalhar). A verificacao roda em segundo plano e nao bloqueia a
    /// janela: o resultado aparece na barra de status.
    ///
    /// A regra de "o que e versao nova" e do nucleo
    /// (<see cref="UpdateService.TemVersaoNova"/>), a mesma que a tela de
    /// atualizacao usa.
    /// </summary>
    private async Task CheckForUpdateOnStartup()
    {
        var settings = AppSettingsStore.Current;
        if (!settings.CheckForUpdateOnStartup) return;

        try
        {
            var resultado = await UpdateService.VerificarTranalAsync();
            if (!resultado.Success) return;

            var atual = UpdateService.VersaoAtual();
            if (!UpdateService.TemVersaoNova(atual, resultado.LatestVersion)) return;

            StatusText.Text = $"{Loc.Get("Update.LatestVersion")} {resultado.LatestVersion}";

            // "Atualizar automaticamente em segundo plano": o pacote e baixado
            // sem abrir nada. O usuario decide quando executa.
            if (!settings.UpdateAutomatically) return;
            if (string.IsNullOrWhiteSpace(resultado.DownloadUrl)) return;

            var destino = UpdateService.CaminhoDoPacote(resultado.DownloadUrl);
            var ok = await AutoUpdateClient.DownloadAsync(resultado.DownloadUrl!, destino, null, default);
            if (ok) StatusText.Text += " — " + Loc.Get("Update.Done");
        }
        catch
        {
            // Sem rede, a verificacao silenciosa falha e o Tranal continua
            // funcionando: a opcao e um extra, nunca um bloqueio.
        }
    }

    /// <summary>
    /// Foca a tela "Traced Programs" no arranque (opcao 15129 do Revo:
    /// "Set the focus on Traced Programs on startup").
    ///
    /// A preferencia desliga isso: nem todo mundo quer comecar pelo log de
    /// instalacao, e a varredura do painel custa tempo.
    /// </summary>
    private void FocusStartupModule()
    {
        if (!AppSettingsStore.Current.FocusTracedProgramsOnStartup) return;

        SwitchView("TracedPrograms", tool: false);
        ContentHost.Focus();
    }

    // ------------------------------------------------------------------
    // Carga dos dados
    // ------------------------------------------------------------------
    private CancellationTokenSource? _scanCancellation;

    private async Task ReloadAsync()
    {
        StatusText.Text = Loc.Get("Status.Loading");
        var options = new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = AppSettingsStore.Current.ShowSystemComponents,
            IncludeMsiProducts = false,
        };

        // A varredura pode ser interrompida (Wise: o botao "Stop"). O token
        // e do Tranal: e o que cancela o trabalho em andamento.
        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();

        var cancelamento = new CancellationTokenSource();
        _scanCancellation = cancelamento;

        var programs = await Task.Run(() => ProgramScanner.Scan(options, cancelamento.Token));

        var interrompida = cancelamento.IsCancellationRequested;
        cancelamento.Dispose();
        if (ReferenceEquals(_scanCancellation, cancelamento)) _scanCancellation = null;

        // O "registro de ultima analise" (Wise): quando a maquina foi vista
        // pela ultima vez e quantos programas foram encontrados. A regra
        // mora no nucleo — a interface e a linha de comando gravam do mesmo
        // jeito.
        AppSettingsStore.RecordScan(programs.Count);

        // O cache de programas (paridade com o Revo Pro, que mantem um cache
        // em disco) e gravado no fim da varredura. Ele e um resumo: a proxima
        // abertura parte dele em vez de varrer o Registro inteiro de novo.
        ProgramCacheStore.Save(programs);

        _all.Clear();
        foreach (var program in programs)
            _all.Add(new ProgramRow(program));

        // O arquivo de preferencias do usuario pode estar invalido (e foi
        // preservado) ou travado (e nao deu para ler). Nao avisar deixaria
        // comentarios e grupos sumirem sem explicacao.
        _avisoPreferencias = ProgramCustomizationStore.LastLoad switch
        {
            LoadState.Preserved => Loc.Get("ProgramData.Corrupted"),
            LoadState.Unreadable => Loc.Get("ProgramData.Unreadable"),
            _ => string.Empty,
        };

        ApplyFilter();
        UpdateStatus();

        if (interrompida)
        {
            StatusText.Text = Loc.Get("Status.ScanStopped", programs.Count);
        }
    }

    /// <summary>
    /// Interrompe a varredura em andamento.
    /// </summary>
    private void StopScan()
    {
        _scanCancellation?.Cancel();
    }

    private void UpdateStatus()
    {
        // A coluna "Inicializacao" so responde depois que a varredura de
        // inicializacao aconteceu: a varredura e cara e nao cabe por linha.
        // Ate la, a coluna fica vazia — nunca "Nao" por palpite.
        StartupEntryIndex.Refresh(Core.Tools.AutorunManager.Scan());

        var total = _all.Count;
        var visible = _view.Cast<ProgramRow>().Count();
        StatusText.Text = $"{Loc.Get("Status.Installations")}: {total}";
        if (visible != total) StatusText.Text += $"   ({Loc.Get("Status.Showing")} {visible})";

        // O filtro apagou tudo: o rodape diz isso, porque uma lista vazia
        // sem explicacao parece que a varredura falhou. E o aviso do Geek
        // ("no matching items, press ESC to reset").
        if (visible == 0 && total > 0)
        {
            StatusText.Text += $"   — {Loc.Get("Status.NoMatchingItems")}";
        }

        // O total agregado: quantos programas a lista tem e quanto eles
        // ocupam. O calculo mora no nucleo (ProgramTotalsCalculator) e o
        // texto vem do catalogo — o rodape nao monta nada por conta propria.
        var totais = Core.Modules.ProgramTotalsCalculator.Compute(
            _all.Select(r => r.Program).Where(p => p is not null)!);
        StatusText.Text += $"   — {totais.Text}";
        if (_avisoPreferencias.Length > 0) StatusText.Text += $"   — {_avisoPreferencias}";
    }

    // ------------------------------------------------------------------
    // Busca em tempo real (nome, editora ou comentarios)
    // ------------------------------------------------------------------
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _filters.SearchText = SearchBox.Text;
        ApplyFilter();
        UpdateStatus();
        SettingsSidebarPanel.SyncSearch(_filters.SearchText);
    }

    private void ApplyFilter()
    {
        _view.Refresh();
        ApplyHighlights();
    }

    /// <summary>
    /// Pinta as linhas que casam com alguma regra de realce.
    ///
    /// O realce nao filtra: ele so muda a cor do nome. Quem decide a cor e o
    /// nucleo (<see cref="ProgramFilterSet.HighlightColorOf"/>).
    /// </summary>
    private void ApplyHighlights()
    {
        foreach (var row in _all)
        {
            var cor = _filters.HighlightColorOf(row.Program);
            row.HighlightColorKey = cor ?? string.Empty;
        }
    }

    /// <summary>
    /// Abre e fecha o painel lateral de configuracoes (o item 52 do backlog:
    /// o "Properties Sidebar" do BCU, com filtros e realces).
    /// </summary>
    private void SettingsPanelButton_Click(object sender, RoutedEventArgs e)
    {
        _settingsPanelOpen = !_settingsPanelOpen;

        SettingsPanelBorder.Visibility = _settingsPanelOpen ? Visibility.Visible : Visibility.Collapsed;

        if (_settingsPanelOpen)
        {
            SettingsSidebarPanel.Load(_filters);
            ApplyFilter();
            UpdateStatus();
        }
    }

    /// <summary>
    /// O painel lateral mudou um filtro ou um realce: a lista e redesenhada.
    /// </summary>
    private void SettingsSidebar_FiltersChanged(object? sender, EventArgs e)
    {
        // A busca da janela principal e a do painel sao o mesmo campo.
        if (!string.Equals(SearchBox.Text, _filters.SearchText, StringComparison.Ordinal))
            SearchBox.Text = _filters.SearchText;

        ApplyFilter();
        UpdateStatus();
    }

    private bool FilterRow(object item)
    {
        if (item is not ProgramRow row) return false;
        return _filters.Matches(row.Program);
    }

    // ------------------------------------------------------------------
    // Filtros: por tipo, por tamanho e por unidade
    //
    // O estado mora no nucleo (ProgramFilterSet). O popup e o painel lateral
    // editam o mesmo objeto: e o que impede a lista e o painel de discordar.
    // ------------------------------------------------------------------

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        PopulateDriveFilters();
        SyncFilterRadios();

        // O interruptor "Hidden" mostra o mesmo estado das Opcoes: se ele
        // mudar la, aqui tambem muda.
        HiddenFilterCheck.IsChecked = AppSettingsStore.Current.ShowSystemComponents;

        FilterPopup.IsOpen = !FilterPopup.IsOpen;
    }

    /// <summary>
    /// O filtro "Hidden" (15362): liga ou desliga os componentes do sistema
    /// na listagem. E a preferencia de "Mostrar componentes do sistema" das
    /// Opcoes — as duas telas mexem no mesmo interruptor.
    /// </summary>
    private async void HiddenFilter_Changed(object sender, RoutedEventArgs e)
    {
        var mostrar = HiddenFilterCheck.IsChecked == true;

        var settings = AppSettingsStore.Current;
        if (settings.ShowSystemComponents == mostrar) return;

        settings.ShowSystemComponents = mostrar;
        AppSettingsStore.Save(settings);

        // A lista muda de conteudo: e preciso varrer de novo.
        await ReloadAsync();
    }

    private void PopulateDriveFilters()
    {
        DriveFilterPanel.Children.Clear();

        var all = new RadioButton { Content = Loc.Get("Sidebar.All"), Margin = new Thickness(0, 0, 0, 2) };
        all.IsChecked = _filters.DriveFilter is null;
        all.Tag = "drive:*";
        all.Checked += FilterOption_Changed;
        DriveFilterPanel.Children.Add(all);

        foreach (var drive in DriveInfo.GetDrives()
                     .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                     .Select(d => d.Name).OrderBy(d => d))
        {
            var radio = new RadioButton { Content = drive, Margin = new Thickness(0, 0, 0, 2) };
            radio.IsChecked = _filters.DriveFilter == drive;
            radio.Tag = "drive:" + drive;
            radio.Checked += FilterOption_Changed;
            DriveFilterPanel.Children.Add(radio);
        }
    }

    private void FilterOption_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton radio || radio.Tag is not string tag) return;

        if (tag.StartsWith("drive:", StringComparison.Ordinal))
        {
            var value = tag["drive:".Length..];
            _filters.DriveFilter = value == "*" ? null : value;
        }
        else if (tag is "All" or "32" or "64" or "msi")
        {
            _filters.TypeFilter = tag;
        }
        else
        {
            _filters.SizeFilter = tag;
        }

        ApplyFilter();
        UpdateStatus();

        // O painel lateral mostra o mesmo estado: os dois editam o mesmo
        // objeto, entao recarregar os controles dele mantem os dois na
        // mesma pagina.
        SettingsSidebarPanel.Load(_filters);
    }

    /// <summary>
    /// O popup de filtros mostra o estado do conjunto de filtros: quando o
    /// painel lateral muda o tipo ou o tamanho, o popup abre ja marcado no
    /// que vale.
    /// </summary>
    private void SyncFilterRadios()
    {
        TypeAllRadio.IsChecked = _filters.TypeFilter == "All";
        Type32Radio.IsChecked = _filters.TypeFilter == "32";
        Type64Radio.IsChecked = _filters.TypeFilter == "64";
        TypeMsiRadio.IsChecked = _filters.TypeFilter == "msi";

        SizeAllRadio.IsChecked = _filters.SizeFilter == "all";
        SizeSmallRadio.IsChecked = _filters.SizeFilter == "small";
        SizeMediumRadio.IsChecked = _filters.SizeFilter == "medium";
        SizeLargeRadio.IsChecked = _filters.SizeFilter == "large";
    }

    // ------------------------------------------------------------------
    // Cabecalho: ordenacao por clique e menu de contexto (colunas/alinhamento)
    // ------------------------------------------------------------------
    private readonly Dictionary<GridViewColumn, string> _columnProperties = new();

    /// <summary>
    /// A coluna das caixas de selecao, quando ela existe. Nao faz parte do
    /// catalogo de colunas: e um comando, nao um dado — nao tem cabecalho, nao
    /// ordena e nao pode ser removida.
    /// </summary>
    private GridViewColumn? _checkColumn;

    /// <summary>
    /// O ultimo valor da preferencia "caixas de selecao na visualizacao de
    /// detalhes". Serve para saber se a grade precisa ser refeita.
    /// </summary>
    private bool _checkBoxesInDetails;

    /// <summary>
    /// Qual coluna do catalogo cada coluna da grade representa. E o que liga
    /// a coluna da tela (montada em codigo) ao catalogo de colunas.
    /// </summary>
    private readonly Dictionary<GridViewColumn, ProgramColumn> _columnIds = new();
    /// <summary>
    /// Monta a grade da lista com as colunas escolhidas pelo usuario.
    ///
    /// O catalogo de colunas e a fonte: a grade nasce dele e nao de uma
    /// lista escrita no XAML — assim a lista da tela e a exportacao nunca
    /// discordam sobre o que e uma coluna.
    /// </summary>
    private void BuildProgramColumns()
    {
        var visiveis = ProgramColumns.Visible(AppSettingsStore.Current.HiddenColumns);

        var template = (DataTemplate)Resources["ProgramNameTemplate"];

        // A coluna das caixas de selecao e a "Show check boxes in Details
        // view" do Revo (15334): ela so entra quando a preferencia manda.
        DataTemplate? checkTemplate = null;
        if (AppSettingsStore.Current.ShowCheckBoxesInDetailsView)
            checkTemplate = (DataTemplate)Resources["ProgramCheckTemplate"];

        var grid = ProgramColumnLayout.Build(visiveis, template, checkTemplate);

        // A coluna das caixas e sempre a primeira: e por isso que ela pode
        // ser reconhecida sem mapa.
        _checkColumn = checkTemplate is not null ? grid.Columns[0] : null;

        // A grade anterior morre aqui: os mapas sao refeitos para nao
        // guardar colunas que ja nao existem.
        _columnIds.Clear();
        _columnProperties.Clear();

        for (var i = 0; i < grid.Columns.Count && i < visiveis.Count; i++)
            _columnIds[grid.Columns[i]] = visiveis[i];

        ProgramsList.View = grid;

        SetupColumnHeaders();

        // O modo de visualizacao e um estado da tela, nao uma preferencia de
        // colunas: ele e reaplicado depois da grade, para nao se perder quando
        // a grade e reconstruida (troca de idioma, escolha de colunas).
        ApplyViewMode(_viewMode);
    }

    /// <summary>A coluna do catalogo que uma coluna da grade representa.</summary>
    private ProgramColumn ColumnIdOf(GridViewColumn column) =>
        _columnIds.TryGetValue(column, out var id) ? id : ProgramColumn.Name;

    /// <summary>
    /// A propriedade ordenavel vem do indice da coluna (o cabecalho e localizado e
    /// nao serve como chave estavel).
    /// </summary>
    private void SetupColumnHeaders()
    {
        if (ProgramsList.View is not GridView grid) return;

        foreach (var column in grid.Columns)
        {
            // A coluna das caixas de selecao nao tem cabecalho: e um comando,
            // nao um dado.
            if (ReferenceEquals(column, _checkColumn)) continue;

            var property = ProgramColumnLayout.SortPropertyOf(ColumnIdOf(column));
            if (property is not null) _columnProperties[column] = property;

            // Cabecalho traduzido (propriedade CLR simples, sem binding).
            column.Header = ProgramColumns.Label(ColumnIdOf(column));

            var header = new GridViewColumnHeader { Content = column.Header, Tag = column };
            header.Style = (Style)FindResource("ListViewHeaderStyle");
            header.Click += ColumnHeader_Click;
            header.ContextMenu = BuildColumnMenu();
            column.Header = header;
        }
    }

    private void ColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not GridViewColumnHeader header) return;
        if (header.Tag is not GridViewColumn column) return;
        if (!_columnProperties.TryGetValue(column, out var property)) return;

        var existing = _view.SortDescriptions
            .FirstOrDefault(s => s.PropertyName == property);
        var ascending = existing.PropertyName == property
            && existing.Direction == ListSortDirection.Ascending;

        _view.SortDescriptions.Clear();
        _view.SortDescriptions.Add(new SortDescription(property,
            ascending ? ListSortDirection.Descending : ListSortDirection.Ascending));
    }

    private ContextMenu? _columnMenu;

    private ContextMenu BuildColumnMenu()
    {
        if (_columnMenu is null)
        {
            _columnMenu = new ContextMenu();

            // O menu e montado quando abre, porque o item depende da coluna
            // clicada: o mesmo menu serve a todos os cabecalhos. O handler e
            // registrado uma vez so — registrar a cada coluna faria o menu se
            // reconstruir varias vezes a cada abertura.
            _columnMenu.Opened += (_, _) => PopulateColumnMenu(_columnMenu);
        }

        return _columnMenu;
    }

    /// <summary>
    /// O menu do cabecalho de uma coluna: "Remove This Column" (15055) e o
    /// alinhamento (15056 a 15060).
    /// </summary>
    private void PopulateColumnMenu(ContextMenu menu)
    {
        menu.Items.Clear();

        // O cabecalho que abriu o menu diz qual coluna ele e.
        var header = menu.PlacementTarget as GridViewColumnHeader;
        if (header?.Tag is not GridViewColumn column) return;

        var id = ColumnIdOf(column);

        var remove = CommandItem(Loc.Get("Cmd.RemoveColumn"), "CmdRemoveColumn", () => HideColumn(column));
        // A coluna do nome nunca some: sem ela a lista perde o sentido.
        remove.IsEnabled = id != ProgramColumn.Name;
        menu.Items.Add(remove);

        menu.Items.Add(new Separator());

        // O auto-ajuste de todas as colunas: um comando, e nao um por coluna.
        var ajustarTodas = CommandItem(Loc.Get("Cmd.BestFitAll"), "CmdBestFitAll", BestFitAll);
        menu.Items.Add(ajustarTodas);

        menu.Items.Add(new Separator());

        var align = new MenuItem { Header = Loc.Get("Cmd.Alignment") };
        AutomationProperties.SetAutomationId(align, "CmdAlignment");
        align.Items.Add(MenuItemFor(Loc.Get("Cmd.AlignLeft"), "AlignLeft", () => SetAlignment(HorizontalAlignment.Left)));
        align.Items.Add(MenuItemFor(Loc.Get("Cmd.AlignCenter"), "AlignCenter", () => SetAlignment(HorizontalAlignment.Center)));
        align.Items.Add(MenuItemFor(Loc.Get("Cmd.AlignRight"), "AlignRight", () => SetAlignment(HorizontalAlignment.Right)));
        align.Items.Add(MenuItemFor(Loc.Get("Cmd.BestFit"), "BestFit", () => BestFit(column)));
        menu.Items.Add(align);
    }

    /// <summary>
    /// Esconde uma coluna da lista (15055). A escolha vai para as
    /// preferencias e a grade nasce de novo sem ela.
    /// </summary>
    private void HideColumn(GridViewColumn column)
    {
        var id = ColumnIdOf(column);
        if (id == ProgramColumn.Name) return;

        var visiveis = ProgramColumns.Visible(AppSettingsStore.Current.HiddenColumns)
            .Where(c => c != id);

        SetVisibleColumns(visiveis);
    }

    /// <summary>
    /// Grava quais colunas a lista mostra. E o unico lugar que decide isso: a
    /// janela de colunas e o "Remove This Column" passam por aqui.
    /// </summary>
    private void SetVisibleColumns(IEnumerable<ProgramColumn> visible)
    {
        var escolhidas = visible.ToHashSet();

        var settings = AppSettingsStore.Current;
        settings.HiddenColumns = ProgramColumns.All
            .Where(c => c.Id != ProgramColumn.Name && !escolhidas.Contains(c.Id))
            .Select(c => c.Id.ToString())
            .ToList();
        AppSettingsStore.Save(settings);

        BuildProgramColumns();
    }

    /// <summary>
    /// Ajusta a largura da coluna ao maior conteudo (15060: "Best Fit").
    /// A largura vale enquanto a janela estiver aberta: o Revo tambem nao
    /// grava a largura das colunas nas preferencias.
    /// </summary>
    private void BestFit(GridViewColumn column)
    {
        var id = ColumnIdOf(column);

        // A conta mora no nucleo: e a mesma que o auto-ajuste de todas as
        // colunas usa.
        var largura = ProgramColumns.AutoWidth(id, _view.Cast<ProgramRow>().Select(r => (r.Program, r.Custom)));
        column.Width = largura;

        // A largura fica guardada: voltar para o modo "Detalhes" reescrevia
        // todas as larguras com o valor padrao e o ajuste do usuario
        // sumia — ele acabara de apertar "Best Fit" e nao via nada acontecer.
        _columnWidths[id] = largura;
    }

    /// <summary>
    /// Ajusta a largura de TODAS as colunas de uma vez.
    ///
    /// E o "auto-ajustar a largura de todas as colunas" do BCU: o ajuste de
    /// uma coluna por vez obriga o usuario a passar por cada uma, e o
    /// resultado sai desigual.
    ///
    /// A conta e a mesma do "Best Fit" (<see cref="ProgramColumns.AutoWidth"/>):
    /// as duas telas medem o mesmo texto do mesmo jeito.
    /// </summary>
    private void BestFitAll()
    {
        if (ProgramsList.View is not GridView grid) return;

        var linhas = _view.Cast<ProgramRow>().Select(r => (r.Program, r.Custom)).ToList();

        foreach (var column in grid.Columns)
        {
            // A coluna das caixas de selecao nao tem texto: e um comando,
            // nao um dado.
            if (ReferenceEquals(column, _checkColumn)) continue;

            var id = ColumnIdOf(column);
            var largura = ProgramColumns.AutoWidth(id, linhas);

            column.Width = largura;
            _columnWidths[id] = largura;
        }
    }

    private void SetAlignment(HorizontalAlignment alignment)
    {
        var style = new Style(typeof(ListViewItem))
        {
            BasedOn = (Style)FindResource("ListViewItemBase"),
        };
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, alignment));
        ProgramsList.ItemContainerStyle = style;
    }

    // ------------------------------------------------------------------
    // Modulos e ferramentas
    // ------------------------------------------------------------------
    private void ModuleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        SwitchView(button.Tag?.ToString() ?? "AllPrograms", tool: false);
    }

    private void ToolButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        SwitchView(button.Tag?.ToString() ?? "AutorunManager", tool: true);
    }

    /// <summary>
    /// Troca o conteudo principal entre a lista de programas e o painel do
    /// modulo/ferramenta escolhido.
    /// </summary>
    private void SwitchView(string id, bool tool)
    {
        _module = id;

        foreach (var child in ModulePanel.Children.OfType<Button>())
        {
            var selected = !tool && (string)child.Tag == id;
            child.Style = (Style)FindResource(selected ? "ModuleButtonSelected" : "ModuleButton");
        }

        foreach (var child in ToolPanel.Children.OfType<Button>())
        {
            var selected = tool && (string)child.Tag == id;
            child.Style = (Style)FindResource(selected ? "ModuleButtonSelected" : "ModuleButton");
        }

        ModuleHeader.Text = Loc.Get($"Module.{id}");
        DescriptionText.Text = DescriptionFor(id);
        ActionTitleText.Text = Loc.Get(tool ? "Sidebar.Tools" : "Sidebar.Uninstall");

        if (id == "AllPrograms")
        {
            ProgramsView.Visibility = Visibility.Visible;
            ContentHost.Visibility = Visibility.Collapsed;
            ContentHost.Content = null;
            SearchBox.IsEnabled = true;
            FilterButton.IsEnabled = true;
            _ = ReloadAsync();
            return;
        }

        ProgramsView.Visibility = Visibility.Collapsed;
        ContentHost.Visibility = Visibility.Visible;
        SearchBox.IsEnabled = false;
        FilterButton.IsEnabled = false;

        var view = ViewRegistry.Create(id);
        ContentHost.Content = view;
        if (view is Ui.Shared.ItemListPanel panel) panel.PanelStatus = Loc.Get("Status.Loading");
    }

    /// <summary>Atualiza os textos fixos apos troca de idioma.</summary>
    private void RefreshLocalizedTexts()
    {
        ModuleHeader.Text = Loc.Get($"Module.{_module}");
        DescriptionText.Text = DescriptionFor(_module);
        ActionTitleText.Text = Loc.Get(_module == "AllPrograms" ? "Sidebar.Uninstall" : "Sidebar.Tools");

        // Os grupos ("Novos Programas"/"Outros Programas") tambem dependem do idioma.
        _view.Refresh();
        UpdateStatus();

        // Os cabecalhos das colunas sao traduzidos: a grade nasce de novo.
        BuildProgramColumns();

        // As telas de modulo/ferramenta sao recriadas: os cabecalhos das colunas
        // sao fixados no momento da criacao do painel.
        if (_module != "AllPrograms")
        {
            var view = ViewRegistry.Create(_module);
            if (view is not null) ContentHost.Content = view;
        }
    }

    /// <summary>
    /// Descricao do painel: o texto vem do catalogo de idiomas (chave "Desc.&lt;id&gt;").
    /// </summary>
    private static string DescriptionFor(string module)
    {
        var key = $"Desc.{module}";
        var text = Loc.Get(key);
        return text == key ? string.Empty : text;
    }

    // ------------------------------------------------------------------
    // Mais comandos
    // ------------------------------------------------------------------
    private void MoreCommandsButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();

        var refresh = CommandItem(Loc.Get("Cmd.Refresh"), "CmdRefresh", async () => await ReloadAsync());
        menu.Items.Add(refresh);

        // Parar a varredura (Wise: o botao "Stop").
        var parar = CommandItem(Loc.Get("Cmd.StopScan"), "CmdStopScan", () => StopScan());
        menu.Items.Add(parar);

        var uninstall = CommandItem(Loc.Get("Cmd.Uninstall"), "CmdUninstall", () => StartUninstall(quick: false));
        menu.Items.Add(uninstall);

        var quick = CommandItem(Loc.Get("Cmd.QuickUninstall"), "CmdQuickUninstall", () => StartUninstall(quick: true));
        menu.Items.Add(quick);

        var forced = CommandItem(Loc.Get("Cmd.ForcedUninstall"), "CmdForcedUninstall", ShowForcedUninstall);
        menu.Items.Add(forced);

        menu.Items.Add(new Separator());

        var viewMenu = new MenuItem { Header = Loc.Get("Cmd.View") };
        AutomationProperties.SetAutomationId(viewMenu, "CmdViewMenu");
        viewMenu.Items.Add(MenuItemFor(Loc.Get("Cmd.ViewDetails"), "CmdViewDetails", () => ApplyViewMode("Details")));
        viewMenu.Items.Add(MenuItemFor(Loc.Get("Cmd.ViewList"), "CmdViewList", () => ApplyViewMode("List")));
        viewMenu.Items.Add(MenuItemFor(Loc.Get("Cmd.ViewIcons"), "CmdViewIcons", () => ApplyViewMode("Icons")));
        menu.Items.Add(viewMenu);

        // As colunas da lista (15054) e o gerenciador de grupos (15001) ficam
        // no mesmo menu do Revo.
        var columns = CommandItem(Loc.Get("Cmd.Columns"), "CmdColumns", ShowColumns);
        menu.Items.Add(columns);

        var groups = CommandItem(Loc.Get("Cmd.GroupsManager"), "CmdGroupsManager", ShowGroupsManager);
        menu.Items.Add(groups);

        var exportMenu = new MenuItem { Header = Loc.Get("Cmd.ExportList") };
        AutomationProperties.SetAutomationId(exportMenu, "CmdExportMenu");
        foreach (var (format, extension, labelKey) in ExportFormats)
        {
            var item = CommandItem(Loc.Get(labelKey), $"CmdExport{format}", () => ExportList(format));
            exportMenu.Items.Add(item);
        }
        menu.Items.Add(exportMenu);

        // Copiar para a area de transferencia. E o comando geral do Revo
        // ("Copy to Clipboard") e a copia avancada do BCU: os dois sao o
        // mesmo lugar, com quatro formatos.
        var copyMenu = new MenuItem { Header = Loc.Get("Cmd.CopyToClipboard") };
        AutomationProperties.SetAutomationId(copyMenu, "CmdCopyToClipboardMenu");
        foreach (var format in ClipboardFormats)
        {
            var clipboardFormat = format.Format;
            copyMenu.Items.Add(MenuItemFor(format.Label, format.AutomationId,
                () => CopySelectionToClipboard(clipboardFormat)));
        }
        menu.Items.Add(copyMenu);

        // Suporte online: o repositorio publico do projeto. Nao ha servidor
        // de suporte do Tranal — a mensagem ja vai montada na URL.
        var support = CommandItem(Loc.Get("Cmd.Support"), "CmdSupport", ShowSupport);
        menu.Items.Add(support);

        // A descoberta do sistema: pacotes MSI, recursos do Windows, gerenciadores
        // de pacotes, varredura de drives, pastas vazias e assinatura do
        // desinstalador. E o que responde "o que mais esta instalado aqui".
        var discovery = CommandItem(Loc.Get("Cmd.Discovery"), "CmdDiscovery", ShowDiscovery);
        menu.Items.Add(discovery);

        menu.Items.Add(new Separator());
        var options = CommandItem(Loc.Get("Cmd.Options"), "CmdOptions", () => ShowOptions());
        menu.Items.Add(options);

        var backups = CommandItem(Loc.Get("Cmd.BackupManager"), "CmdBackupManager", () => ShowBackupManager());
        menu.Items.Add(backups);

        menu.PlacementTarget = MoreCommandsButton;
        menu.IsOpen = true;
    }

    /// <summary>
    /// Os formatos da copia para a area de transferencia.
    /// </summary>
    private static readonly (Core.Appearance.ClipboardFormat Format, string Label, string AutomationId)[] ClipboardFormats =
    {
        (Core.Appearance.ClipboardFormat.PlainText, "Cmd.CopyPlain", "CmdCopyPlain"),
        (Core.Appearance.ClipboardFormat.TabSeparated, "Cmd.CopyColumns", "CmdCopyColumns"),
        (Core.Appearance.ClipboardFormat.UninstallCommand, "Cmd.CopyCommands", "CmdCopyCommands"),
        (Core.Appearance.ClipboardFormat.FullDetails, "Cmd.CopyDetails", "CmdCopyDetails"),
    };

    /// <summary>
    /// Copia os programas selecionados para a area de transferencia.
    ///
    /// Sem selecao, a copia leva **todos** os programas da lista — e o que o
    /// usuario quer quando esta montando um relatorio da maquina.
    /// </summary>
    private void CopySelectionToClipboard(Core.Appearance.ClipboardFormat format)
    {
        var rows = selectedRows;
        var programs = rows.Count > 0
            ? rows.Select(r => r.Program).ToList()
            : _all.Select(r => r.Program).ToList();

        if (programs.Count == 0)
        {
            UserMessages.Information(this, Loc.Get("Status.NoSelection"));
            return;
        }

        if (!ProgramClipboard.Copy(programs, format))
        {
            UserMessages.Information(this, Loc.Get("Cmd.CopyFailed"));
            return;
        }

        StatusText.Text = Loc.Get("Common.Copied");
    }

    /// <summary>
    /// Abre o endereco de suporte com a mensagem ja montada.
    /// </summary>
    private void ShowSupport()
    {
        var program = selectedRows.FirstOrDefault()?.Program;
        var url = Core.Appearance.SupportRequest.BuildUrl(program);

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            UserMessages.Information(this, Core.Appearance.SupportRequest.SupportUrl);
        }
    }
    /// <summary>
    /// Abre a janela de descoberta do sistema.
    ///
    /// A janela **mostra**: nada e desinstalado, nada e apagado. E o lugar
    /// onde o usuario ve o que existe na maquina alem do Painel de Controle.
    /// </summary>
    private void ShowDiscovery()
    {
        var window = new DiscoveryWindow { Owner = this };
        window.ShowDialog();
    }

    /// <summary>
    /// Cria um item de menu com AutomationId proprio: e o seletor estavel para
    /// automacao (o texto do cabecalho "Desinstalar" colide com o titulo da barra lateral).
    /// </summary>
    private static MenuItem CommandItem(string header, string automationId, Action action)
    {
        var item = new MenuItem { Header = header };
        AutomationProperties.SetAutomationId(item, automationId);
        item.Click += (_, _) => action();
        return item;
    }

    private static MenuItem MenuItemFor(string header, string automationId, Action action) =>
        CommandItem(header, automationId, action);

    /// <summary>
    /// O modo de visualizacao da lista (Detalhes, Lista, Icones). E estado da
    /// tela, nao preferencia: as colunas escondidas continuam escondidas nas
    /// preferencias, e o modo e reaplicado quando a grade e reconstruida.
    /// </summary>
    private string _viewMode = "Details";

    /// <summary>
    /// A largura escolhida pelo usuario (Best Fit). E estado da tela, como o
    /// modo de visualizacao: vale enquanto a janela estiver aberta.
    /// </summary>
    private readonly Dictionary<ProgramColumn, double> _columnWidths = new();

    /// <summary>
    /// Aplica o modo de visualizacao.
    ///
    /// Detalhes mostra todas as colunas do catalogo; Lista mostra o nome e a
    /// versao; Icones mostra so o nome. O modo nao mexe nas preferencias: as
    /// colunas que o usuario escondeu continuam escondidas.
    /// </summary>
    private void ApplyViewMode(string mode)
    {
        var grid = ProgramsList.View as GridView;
        if (grid is null) return;

        _viewMode = mode;

        for (var i = 0; i < grid.Columns.Count; i++)
        {
            var column = grid.Columns[i];

            // As caixas de selecao so aparecem na visualizacao de detalhes:
            // e o que a opcao 15334 do Revo promete ("Show check boxes in
            // Details view").
            if (ReferenceEquals(column, _checkColumn))
            {
                column.Width = mode == "Details" ? ProgramColumnLayout.CheckColumnWidth : 0;
                continue;
            }

            var id = ColumnIdOf(column);

            grid.Columns[i].Width = mode switch
            {
                "List" => id switch
                {
                    ProgramColumn.Name => 320,
                    ProgramColumn.Version => 130,
                    _ => 0,
                },
                "Icons" => id == ProgramColumn.Name ? 220 : 0,
                // A largura que o usuario ajustou vale: o "Best Fit" nao pode
                // sumir quando o modo de visualizacao muda.
                _ => _columnWidths.TryGetValue(id, out var largura)
                    ? largura
                    : ProgramColumnLayout.WidthOf(id),
            };
        }
    }

    // ------------------------------------------------------------------
    // Menu principal (canto superior direito)
    // ------------------------------------------------------------------
    private void MainMenuButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();

        var options = CommandItem(Loc.Get("Cmd.Options"), "CmdOptions", () => ShowOptions());
        menu.Items.Add(options);

        var backups = CommandItem(Loc.Get("Cmd.BackupManager"), "CmdBackupManager", () => ShowBackupManager());
        menu.Items.Add(backups);

        var excludeList = CommandItem(Loc.Get("Cmd.ExcludeList"), "CmdExcludeList", () => ShowExcludeList());
        menu.Items.Add(excludeList);

        // "Columns" do Revo (15054): o usuario escolhe quais colunas a lista
        // mostra — e a escolha fica salva.
        var columns = CommandItem(Loc.Get("Cmd.Columns"), "CmdColumns", () => ShowColumns());
        menu.Items.Add(columns);

        // "Groups Manager" do Revo (15001).
        var groups = CommandItem(Loc.Get("Cmd.GroupsManager"), "CmdGroupsManager", () => ShowGroupsManager());
        menu.Items.Add(groups);

        var topmost = CommandItem(Loc.Get("Cmd.TopmostWindow"), "CmdTopmostWindow", () => ShowTopmostWindow());
        menu.Items.Add(topmost);

        menu.Items.Add(BuildPanelsMenu());

        // Os temas do menu sao os do catalogo do nucleo: a mesma lista que a
        // janela de opcoes mostra. Um tema novo entra no catalogo e aparece
        // nos dois lugares sem tocar em nenhum dos dois.
        var themeMenu = new MenuItem { Header = Loc.Get("Cmd.Theme") };
        foreach (var skin in Core.Appearance.ThemeSkinCatalog.All)
        {
            var id = skin.Id;
            themeMenu.Items.Add(MenuItemFor(skin.DisplayName, "CmdTheme" + id,
                () => ThemeManager.Apply(id)));
        }
        menu.Items.Add(themeMenu);

        var languageMenu = new MenuItem { Header = Loc.Get("Cmd.Language") };
        foreach (var language in Core.Localization.LanguageCatalog.All)
        {
            var code = language.Code;
            var item = new MenuItem
            {
                Header = $"{language.NativeName} ({language.EnglishName})",
                IsCheckable = true,
                IsChecked = string.Equals(Loc.Current, code, StringComparison.OrdinalIgnoreCase),
            };
            AutomationProperties.SetAutomationId(item, $"Lang{code}");
            item.Click += (_, _) => SetLanguage(code);
            languageMenu.Items.Add(item);
        }
        menu.Items.Add(languageMenu);

        menu.Items.Add(new Separator());
        var exit = CommandItem(Loc.Get("Cmd.Exit"), "CmdExit", () => Close());
        menu.Items.Add(exit);

        menu.PlacementTarget = MainMenuButton;
        menu.IsOpen = true;
    }

    // ------------------------------------------------------------------
    // Personalizacao do layout (visibilidade de paineis)
    // ------------------------------------------------------------------

    /// <summary>
    /// Monta o submenu "Paineis": mostrar ou esconder as partes da janela.
    /// Equivale ao menu "Exibir" do Revo Uninstaller Pro, que controla a
    /// barra de ferramentas principal, o painel de descricao, a barra de
    /// status e o grupo de ferramentas da barra lateral.
    /// </summary>
    private MenuItem BuildPanelsMenu()
    {
        var panels = new MenuItem { Header = Loc.Get("Cmd.Panels") };
        AutomationProperties.SetAutomationId(panels, "CmdPanels");

        panels.Items.Add(CheckableMenuItem(
            Loc.Get("Cmd.ShowMainToolbar"), "CmdShowMainToolbar",
            () => AppSettingsStore.Current.ShowMainToolbar,
            value => SetLayoutOption(s => s.ShowMainToolbar = value)));

        panels.Items.Add(CheckableMenuItem(
            Loc.Get("Cmd.ShowDescriptionPanel"), "CmdShowDescriptionPanel",
            () => AppSettingsStore.Current.ShowDescriptionPanel,
            value => SetLayoutOption(s => s.ShowDescriptionPanel = value)));

        panels.Items.Add(CheckableMenuItem(
            Loc.Get("Cmd.ShowStatusBar"), "CmdShowStatusBar",
            () => AppSettingsStore.Current.ShowStatusBar,
            value => SetLayoutOption(s => s.ShowStatusBar = value)));

        panels.Items.Add(CheckableMenuItem(
            Loc.Get("Cmd.ShowToolsToolbar"), "CmdShowToolsToolbar",
            () => AppSettingsStore.Current.ShowToolsToolbar,
            value => SetLayoutOption(s => s.ShowToolsToolbar = value)));

        panels.Items.Add(new Separator());
        panels.Items.Add(CommandItem(Loc.Get("Cmd.ResetLayout"), "CmdResetLayout", ResetLayout));

        return panels;
    }

    /// <summary>
    /// Aplica as preferencias de visibilidade dos paineis. E o equivalente ao
    /// item "Restaurar as configuracoes dos paineis da janela principal" do
    /// Revo quando o layout e redefinido.
    /// </summary>
    private void ApplyLayout()
    {
        var settings = AppSettingsStore.Current;

        MainToolbarBorder.Visibility = settings.ShowMainToolbar ? Visibility.Visible : Visibility.Collapsed;
        DescriptionPanelBorder.Visibility = settings.ShowDescriptionPanel ? Visibility.Visible : Visibility.Collapsed;
        StatusBarBorder.Visibility = settings.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;

        var toolsVisible = settings.ShowToolsToolbar ? Visibility.Visible : Visibility.Collapsed;
        ToolsToolbarHeaderBorder.Visibility = toolsVisible;
        ToolPanel.Visibility = toolsVisible;
    }

    /// <summary>Grava a preferencia de layout e reaplica na hora.</summary>
    private void SetLayoutOption(Action<AppSettings> mutate)
    {
        AppSettingsStore.Update(mutate);
        ApplyLayout();
    }

    /// <summary>
    /// Reaplica o layout quando as preferencias mudam fora do submenu
    /// "Paineis" — por exemplo, pela janela de Opcoes.
    /// </summary>
    private void OnSettingsSaved(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(ApplyLayout);
            return;
        }

        ApplyLayout();

        // A coluna das caixas de selecao depende da preferencia: quando ela
        // muda, a grade da lista nasce de novo.
        var settings = AppSettingsStore.Current;
        if (settings.ShowCheckBoxesInDetailsView != _checkBoxesInDetails)
        {
            _checkBoxesInDetails = settings.ShowCheckBoxesInDetailsView;
            BuildProgramColumns();
        }
    }

    /// <summary>Restaurou o layout padrao dos paineis.</summary>
    private void ResetLayout()
    {
        AppSettingsStore.Update(settings =>
        {
            settings.ShowMainToolbar = true;
            settings.ShowDescriptionPanel = true;
            settings.ShowStatusBar = true;
            settings.ShowToolsToolbar = true;
        });

        ApplyLayout();
    }

    /// <summary>
    /// Item de menu marcavel ligado a uma preferencia. O item e reconstruido a
    /// cada abertura do menu, entao o marcador reflete sempre o estado atual.
    /// </summary>
    private static MenuItem CheckableMenuItem(
        string header, string automationId, Func<bool> isChecked, Action<bool> set)
    {
        var item = new MenuItem
        {
            Header = header,
            IsCheckable = true,
            IsChecked = isChecked(),
        };

        AutomationProperties.SetAutomationId(item, automationId);
        item.Click += (_, _) => set(!isChecked());

        return item;
    }

    /// <summary>Troca o idioma e persiste a preferencia.</summary>
    private void SetLanguage(string code)
    {
        Loc.SetLanguage(code);
        AppSettingsStore.Update(s => s.Language = code);
        StatusText.Text = $"{Loc.Get("Options.Language")}: {Core.Localization.LanguageCatalog.NativeName(code)}";
    }

    private void ShowOptions()
    {
        var dialog = new OptionsWindow { Owner = this };

        // Reconstruir o cache de programas e uma acao da janela de opcoes:
        // quem refaz a varredura e a janela principal.
        dialog.CacheRebuildRequested += async (_, _) => await ReloadAsync();

        if (dialog.ShowDialog() == true)
            _ = ReloadAsync();
    }

    /// <summary>
    /// Abre o Gerenciador de Backups: a lista de pontos de restauracao
    /// criados a cada remocao, com restauracao integral ou item a item.
    /// </summary>
    private void ShowBackupManager()
    {
        var dialog = new BackupManagerWindow { Owner = this };
        dialog.ShowDialog();
    }

    /// <summary>
    /// O clique direito na lista seleciona a linha apontada.
    ///
    /// Sem isso, o menu de contexto age sobre a linha selecionada antes —
    /// e o usuario acabaria anotando o programa errado, sem nenhum aviso.
    ///
    /// A selecao e multipla (desinstalacao em lote): clicar com o botao
    /// direito em uma linha JA selecionada NAO descarta as outras. Uma linha
    /// que nao estava selecionada substitui a selecao inteira — que e o
    /// comportamento normal de lista com selecao multipla.
    /// </summary>
    private void ProgramsList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var origem = e.OriginalSource as DependencyObject;
        while (origem is not null and not ListViewItem)
        {
            origem = VisualTreeHelper.GetParent(origem);
        }

        if (origem is not ListViewItem item || item.DataContext is not ProgramRow row) return;

        // O menu de contexto age sobre a linha clicada, e nao sobre a
        // primeira linha selecionada.
        _contextRow = row;

        if (!ProgramsList.SelectedItems.Cast<ProgramRow>().Contains(row))
        {
            ProgramsList.SelectedItems.Clear();
            ProgramsList.SelectedItems.Add(row);
        }
    }

    /// <summary>
    /// O menu de contexto do programa: comentario, icone e grupos.
    ///
    /// Corresponde ao "Add Comment" (15216), "Edit Comment" (15217), "Change
    /// Icon" (15079), "Add to Group" (15080) e "Remove from Group" (15081)
    /// do Revo.
    /// </summary>
    private void ProgramContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        var menu = ProgramContextMenu;
        menu.Items.Clear();

        if ((_contextRow ?? ProgramsList.SelectedItem as ProgramRow) is not ProgramRow row) return;

        var custom = ProgramCustomizationStore.Has(row.Program) ? ProgramCustomizationStore.For(row.Program) : null;
        var temComentario = custom is not null && custom.Comment.Length > 0;

        // Renomear o programa na lista (paridade com o "Rename" do Revo Pro).
        var rename = CommandItem(Loc.Get("Cmd.RenameProgram"), "CmdRenameProgram", () => RenameProgram(row));
        menu.Items.Add(rename);

        // "Modificar": reaproveita o instalador do programa. So existe para
        // o que foi instalado pelo Windows Installer — para o resto, o
        // caminho e o desinstalador do fabricante.
        var modify = CommandItem(Loc.Get("Cmd.ModifyProgram"), "CmdModifyProgram", () => ModifyProgram(row));
        menu.Items.Add(modify);
        var comment = CommandItem(
            Loc.Get(temComentario ? "Cmd.EditComment" : "Cmd.AddComment"),
            "CmdAddComment",
            () => EditComment(row));
        menu.Items.Add(comment);

        var icon = CommandItem(Loc.Get("Cmd.ChangeIcon"), "CmdChangeIcon", () => ChangeIcon(row));
        menu.Items.Add(icon);

        // Selecao em massa por pais: marcar (ou desmarcar) todos os
        // programas de um mesmo pais de uma vez. O pais vem da tabela
        // unica no idioma da interface.
        var pais = ProgramColumns.CountryOf(row.Program);
        if (!string.IsNullOrWhiteSpace(pais))
        {
            var marcar = CommandItem(
                string.Format(System.Globalization.CultureInfo.CurrentCulture, Loc.Get("Cmd.MarkByCountry"), pais),
                "CmdMarkByCountry",
                () => MarcarPorPais(pais, marcado: true));
            menu.Items.Add(marcar);

            var desmarcar = CommandItem(
                string.Format(System.Globalization.CultureInfo.CurrentCulture, Loc.Get("Cmd.UnmarkByCountry"), pais),
                "CmdUnmarkByCountry",
                () => MarcarPorPais(pais, marcado: false));
            menu.Items.Add(desmarcar);
        }

        var commands = CommandItem(Loc.Get("Cmd.EditCommands"), "CmdEditCommands", () => EditCommands(row));
        menu.Items.Add(commands);

        // Onde a desinstalacao acontece: o registro do programa, o painel de
        // Programas e Recursos e a Recuperacao do Sistema. O destino vem do
        // nucleo, que e o unico lugar que sabe o comando de cada um.
        var abrir = new MenuItem { Header = Loc.Get("Cmd.OpenInWindows") };
        AutomationProperties.SetAutomationId(abrir, "CmdOpenInWindows");
        foreach (var target in SystemNavigator.TargetsFor(row.Program))
        {
            var alvo = target;
            abrir.Items.Add(CommandItem(
                alvo.Title,
                "CmdOpen" + alvo.FileName.Replace(".exe", string.Empty).Replace(".cpl", string.Empty),
                () => SystemNavigator.Open(alvo)));
        }
        menu.Items.Add(abrir);

        menu.Items.Add(new Separator());

        var grupos = ProgramCustomizationStore.Groups;

        var addGroup = new MenuItem { Header = Loc.Get("Cmd.AddToGroup") };
        AutomationProperties.SetAutomationId(addGroup, "CmdAddToGroup");

        var removeGroup = new MenuItem { Header = Loc.Get("Cmd.RemoveFromGroup") };
        AutomationProperties.SetAutomationId(removeGroup, "CmdRemoveFromGroup");

        if (grupos.Count == 0)
        {
            // Cada submenu recebe a sua propria instancia: um MenuItem nao
            // pode ter dois pais logicos, e a excecao apareceria no meio da
            // montagem do menu — antes dos submenus virarem parte dele.
            addGroup.Items.Add(NoGroupsItem());
            removeGroup.Items.Add(NoGroupsItem());
        }
        else
        {
            foreach (var group in grupos)
            {
                var dentro = custom is not null
                    && custom.Groups.Any(g => string.Equals(g, group, StringComparison.OrdinalIgnoreCase));

                var addItem = CommandItem(group, $"CmdAddGroup{group}", () => AddToGroup(row, group));
                addItem.IsCheckable = true;
                addItem.IsChecked = dentro;
                addGroup.Items.Add(addItem);

                var removeItem = CommandItem(group, $"CmdRemoveGroup{group}", () => RemoveFromGroup(row, group));
                removeItem.IsCheckable = true;
                removeItem.IsChecked = dentro;
                removeGroup.Items.Add(removeItem);
            }
        }

        menu.Items.Add(addGroup);
        menu.Items.Add(removeGroup);

        // Esconder (ou remover) a entrada do programa na lista do Windows
        // ("Adicionar e Remover Programas"), sem desinstalar o programa.
        var esconder = CommandItem(
            Loc.Get("Cmd.HideFromWindowsList"),
            "CmdHideFromWindowsList",
            () => HideFromWindowsList(row));
        menu.Items.Add(esconder);

        var removerDaLista = CommandItem(
            Loc.Get("Cmd.RemoveFromWindowsList"),
            "CmdRemoveFromWindowsList",
            () => RemoveFromWindowsList(row));
        menu.Items.Add(removerDaLista);
    }

    /// <summary>
    /// Esconde a entrada do programa na lista de "Adicionar e Remover
    /// Programas" do Windows. A operacao e reversivel: o programa continua
    /// instalado e a entrada volta quando o usuario pedir.
    /// </summary>
    /// <param name="row">O programa selecionado.</param>
    private async void HideFromWindowsList(ProgramRow row)
    {
        var escondido = Core.Management.WindowsListEntry.Hide(row.Program);
        if (escondido) await ReloadAsync();

        UserMessages.Information(this, escondido
            ? Loc.Get("Cmd.HideFromWindowsListDone")
            : Loc.Get("Cmd.HideFromWindowsListFailed"));
    }

    /// <summary>
    /// Remove a entrada do programa da lista do Windows, sem desinstalar o
    /// programa.
    /// </summary>
    /// <param name="row">O programa selecionado.</param>
    private async void RemoveFromWindowsList(ProgramRow row)
    {
        var removido = Core.Management.WindowsListEntry.Remove(row.Program);
        if (removido) await ReloadAsync();

        UserMessages.Information(this, removido
            ? Loc.Get("Cmd.RemoveFromWindowsListDone")
            : Loc.Get("Cmd.RemoveFromWindowsListFailed"));
    }

    /// <summary>
    /// O item que aparece no submenu de grupos quando ainda nao existe
    /// nenhum. Cada submenu recebe o seu: um item de menu nao pode ter dois
    /// pais logicos.
    /// </summary>
    private static MenuItem NoGroupsItem()
    {
        var item = new MenuItem { Header = Loc.Get("Cmd.NoGroups") };
        item.IsEnabled = false;
        return item;
    }

    /// <summary>Escreve (ou apaga) o comentario do usuario sobre o programa.</summary>
    /// <summary>
    /// Renomeia o programa na lista do Tranal.
    ///
    /// O nome que o Windows registra nao muda: o que muda e o nome que a
    /// lista do Tranal mostra.
    /// </summary>
    /// <param name="row">O programa do menu de contexto.</param>
    private void RenameProgram(ProgramRow row)
    {
        var custom = ProgramCustomizationStore.Has(row.Program) ? ProgramCustomizationStore.For(row.Program) : null;

        var dialog = new RenameWindow(custom?.DisplayName ?? string.Empty, row.Program.DisplayName)
        {
            Owner = this,
        };

        if (dialog.ShowDialog() != true) return;

        if (!ProgramCustomizationStore.SetName(row.Program, dialog.DisplayName))
        {
            ShowSaveError();
            return;
        }

        ReloadProgramRows();
    }
    private void EditComment(ProgramRow row)
    {
        var custom = ProgramCustomizationStore.Has(row.Program) ? ProgramCustomizationStore.For(row.Program) : null;

        var dialog = new CommentWindow(custom?.Comment ?? string.Empty) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        if (!ProgramCustomizationStore.SetComment(row.Program, dialog.Comment))
        {
            ShowSaveError();
            return;
        }

        ReloadProgramRows();
    }

    /// <summary>
    /// Escreve os comandos que o usuario manda rodar antes e depois da
    /// desinstalacao do programa.
    ///
    /// O plano de desinstalacao ja tinha as duas etapas; aqui mora o texto
    /// que as preenche.
    /// </summary>
    /// <param name="row">O programa do menu de contexto.</param>
    private void EditCommands(ProgramRow row)
    {
        var custom = ProgramCustomizationStore.Has(row.Program) ? ProgramCustomizationStore.For(row.Program) : null;

        var dialog = new UninstallCommandsWindow(
            custom?.PreCommand ?? string.Empty,
            custom?.PostCommand ?? string.Empty)
        {
            Owner = this,
        };

        if (dialog.ShowDialog() != true) return;

        if (!ProgramCustomizationStore.SetCommands(row.Program, dialog.PreCommand, dialog.PostCommand))
        {
            ShowSaveError();
            return;
        }

        ReloadProgramRows();
    }

    /// <summary>Escolhe o icone do programa.</summary>
    private void ChangeIcon(ProgramRow row)
    {
        var custom = ProgramCustomizationStore.Has(row.Program) ? ProgramCustomizationStore.For(row.Program) : null;

        var dialog = new IconPickerWindow(BuildIconChoices(), custom?.IconPath ?? row.Program.IconPath) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        if (!ProgramCustomizationStore.SetIcon(row.Program, dialog.SelectedIconIndex, dialog.SelectedIconPath))
        {
            ShowSaveError();
            return;
        }

        ReloadProgramRows();
    }

    /// <summary>
    /// Avisa que a preferencia nao pode ser gravada. Sem isto o usuario veria
    /// o comentario na lista e ele sumiria ao fechar o aplicativo.
    /// </summary>
    private void ShowSaveError()
    {
        StatusText.Text = Loc.Get("ProgramData.SaveError");
        MessageBox.Show(this,
            Loc.Get("ProgramData.SaveError"),
            Loc.Get("App.Title"),
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    /// <summary>
    /// A galeria de icones: os icones que ja existem na maquina — os dos
    /// programas instalados. Uma galeria de icones pronta seria material de
    /// terceiros; a do Tranal usa o que o proprio Windows ja tem.
    /// </summary>
    private List<IconChoice> BuildIconChoices()
    {
        var choices = new List<IconChoice>();
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in _all)
        {
            var path = row.Program.IconPath;
            if (string.IsNullOrWhiteSpace(path) || !vistos.Add(path)) continue;

            choices.Add(new IconChoice
            {
                Path = path,
                Name = row.DisplayName,
                Image = IconPickerWindow.LoadIcon(path),
            });
        }

        return choices;
    }

    private void AddToGroup(ProgramRow row, string group)
    {
        if (!ProgramCustomizationStore.AddToGroup(row.Program, group))
        {
            ShowSaveError();
            return;
        }

        ReloadProgramRows();
    }

    private void RemoveFromGroup(ProgramRow row, string group)
    {
        if (!ProgramCustomizationStore.RemoveFromGroup(row.Program, group))
        {
            ShowSaveError();
            return;
        }

        ReloadProgramRows();
    }

    /// <summary>
    /// Abre o Gerenciador da lista de exclusoes: as pastas, arquivos e chaves
    /// de registro que o Tranal nunca vai remover.
    /// </summary>
    private void ShowExcludeList()
    {
        var dialog = new ExcludeListWindow { Owner = this };
        dialog.ShowDialog();
    }

    /// <summary>
    /// Abre a janela de escolha de colunas da lista (o "Columns" do Revo,
    /// 15054). A escolha e salva: reabrir o Tranal mostra as mesmas colunas.
    /// </summary>
    private void ShowColumns()
    {
        var visiveis = ProgramColumns.Visible(AppSettingsStore.Current.HiddenColumns);

        var dialog = new ColumnsWindow(forExport: false, initial: visiveis) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        SetVisibleColumns(dialog.SelectedColumns);
    }

    /// <summary>
    /// Abre o Gerenciador de grupos (o "Groups Manager" do Revo, 15001).
    /// </summary>
    private void ShowGroupsManager()
    {
        var dialog = new GroupsManagerWindow { Owner = this };
        dialog.ShowDialog();

        // Os grupos podem ter mudado: a lista mostra o grupo do programa.
        ReloadProgramRows();
    }

    /// <summary>Recarrega as linhas da lista, preservando a selecao e os filtros.</summary>
    private void ReloadProgramRows()
    {
        // A selecao e multipla: TODAS as linhas escolhidas voltam escolhidas.
        var nomes = ProgramsList.SelectedItems
            .Cast<ProgramRow>()
            .Select(r => r.DisplayName)
            .ToList();

        var programs = _all.Select(r => r.Program).ToList();
        _all.Clear();
        foreach (var program in programs) _all.Add(new ProgramRow(program));

        ApplyFilter();
        UpdateStatus();

        ProgramsList.SelectedItems.Clear();
        foreach (var nome in nomes)
        {
            var row = _all.FirstOrDefault(r => string.Equals(r.DisplayName, nome, StringComparison.OrdinalIgnoreCase));
            if (row is not null) ProgramsList.SelectedItems.Add(row);
        }
    }

    /// <summary>
    /// Abre a Janela Principal Flutuante: um overlay arrastavel que resolve o
    /// alvo apontado (janela, icone ou item da barra de tarefas) e oferece as
    /// acoes sobre ele. Sem alvo, a janela espera um arrasto.
    /// </summary>
    private void ShowTopmostWindow()
    {
        var overlay = new Ui.Tools.TopmostOverlayWindow();
        overlay.Show();
    }

    // ------------------------------------------------------------------
    // Exportacao
    // ------------------------------------------------------------------

    /// <summary>
    /// Os formatos de exportacao. O rotulo vem do catalogo de idiomas
    /// (15062 a 15065) e a extensao do arquivo e fixa, para o arquivo
    /// exportado continuar legivel por qualquer ferramenta.
    /// </summary>
    private static readonly (string Format, string Extension, string LabelKey)[] ExportFormats =
    {
        ("TXT", "txt", "Cmd.ExportTxt"),
        ("CSV", "csv", "Cmd.ExportCsv"),
        ("HTML", "html", "Cmd.ExportHtml"),
        ("XML", "xml", "Cmd.ExportXml"),
        ("XLS", "xls", "Cmd.ExportExcel"),
        ("JSON", "json", "Cmd.ExportJson"),
    };

    private void ExportList(string format)
    {
        // O usuario escolhe as colunas da exportacao (15066: "Select the
        // columns you want to export:"). Sem nenhuma coluna, a exportacao
        // nao acontece (15298).
        var visiveis = ProgramColumns.Visible(AppSettingsStore.Current.HiddenColumns);
        var columns = new ColumnsWindow(forExport: true, initial: visiveis) { Owner = this };
        if (columns.ShowDialog() != true) return;

        var extensao = ExtensionOf(format);

        var save = new SaveFileDialog
        {
            Filter = $"{format} (*.{extensao})|*.{extensao}",
            FileName = $"tranal-lista.{extensao}",
        };
        if (save.ShowDialog(this) != true) return;

        var rows = _view.Cast<ProgramRow>().ToList();
        var selecionadas = columns.SelectedColumns;

        // O Excel e o unico formato que nao e um texto: quem grava o arquivo
        // e o proprio exportador, porque ele pode acionar o Excel instalado
        // (paridade com o Revo Pro) e, quando o Excel nao existe, cair no
        // formato que o Excel abre nativamente.
        if (format == "XLS")
        {
            var resultado = ExcelComExporter.ExportWithFallback(
                rows.Select(r => r.Program),
                save.FileName,
                selecionadas,
                AppSettingsStore.Current.ExportToExcelInstalled);

            StatusText.Text = resultado.Message;
            return;
        }

        var content = format switch
        {
            "CSV" => Exporters.ToCsv(rows.Select(r => r.Program), selecionadas),
            "HTML" => Exporters.ToHtml(rows.Select(r => r.Program), selecionadas),
            "XML" => Exporters.ToXml(rows.Select(r => r.Program), selecionadas),
            "JSON" => Exporters.ToJson(rows.Select(r => r.Program), selecionadas),
            _ => Exporters.ToText(rows.Select(r => r.Program), selecionadas),
        };

        // Falha ao gravar e avisada: a excecao engolida pelo
        // DispatcherUnhandledException deixaria o usuario sem saber por que
        // o arquivo nao apareceu.
        try
        {
            File.WriteAllText(save.FileName, content, System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"{Loc.Get("ProgramData.ExportError")} ({ex.Message})";
            return;
        }

        StatusText.Text = $"{Loc.Get("Status.Exported")}: {save.FileName}";
    }

    /// <summary>A extensao do arquivo de um formato de exportacao.</summary>
    private static string ExtensionOf(string format) =>
        ExportFormats.FirstOrDefault(f => f.Format == format).Extension ?? format.ToLowerInvariant();

    // ------------------------------------------------------------------
    // Desinstalacao
    // ------------------------------------------------------------------
    /// <summary>
    /// Programas alvo da desinstalacao: SOMENTE os itens selecionados na lista.
    ///
    /// A selecao e multipla (Ctrl/Shift, ou Ctrl+A): e o que permite a
    /// desinstalacao em lote. As caixas de marcacao sao apenas indicador visual
    /// (como no Revo) e nunca definem o alvo — caso contrario uma unica acao
    /// removeria todos os programas, porque todas as linhas comecam marcadas.
    /// </summary>
    private List<ProgramRow> selectedRows =>
        ProgramsList.SelectedItems.Cast<ProgramRow>().ToList();

    /// <summary>
    /// Trata os atalhos de teclado.
    ///
    /// A combinacao pressionada vira o nome usado pela tabela de atalhos
    /// (<see cref="ShortcutMap"/>). Quando a combinacao nao esta na tabela,
    /// o evento continua: e o Windows e os controles que tratam o resto
    /// (copiar, colar, navegar pela lista).
    /// </summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var gesture = ShortcutMap.GestureOf(
            e.Key.ToString(),
            (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control,
            (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift,
            (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt);

        var atalho = ShortcutMap.Find(gesture);
        if (atalho is null) return;

        switch (atalho.Action)
        {
            case ShortcutAction.FocusSearch:
                SettingsSidebarPanel.FocusSearch();
                e.Handled = true;
                break;

            case ShortcutAction.Rescan:
                _ = ReloadAsync();
                e.Handled = true;
                break;

            case ShortcutAction.SelectAll:
                ProgramsList.SelectedItems.Clear();
                foreach (var row in _view.Cast<ProgramRow>()) ProgramsList.SelectedItems.Add(row);
                e.Handled = true;
                break;

            case ShortcutAction.Uninstall:
                StartUninstall(quick: false);
                e.Handled = true;
                break;

            case ShortcutAction.QuickUninstall:
                StartUninstall(quick: true);
                e.Handled = true;
                break;

            case ShortcutAction.ExportList:
                ExportList("TXT");
                e.Handled = true;
                break;

            case ShortcutAction.Options:
                ShowOptions();
                e.Handled = true;
                break;

            case ShortcutAction.Discovery:
                ShowDiscovery();
                e.Handled = true;
                break;

            case ShortcutAction.Support:
                ShowSupport();
                e.Handled = true;
                break;

            case ShortcutAction.ClearFilter:
                SettingsSidebarPanel.ClearAll();
                e.Handled = true;
                break;

            case ShortcutAction.CopySelection:
                // Ctrl+C: a copia da selecao para a area de transferencia, no
                // mesmo formato de texto simples do menu.
                CopySelectionToClipboard(Core.Appearance.ClipboardFormat.PlainText);
                e.Handled = true;
                break;
        }
    }
    private void StartUninstall(bool quick)
    {
        var targets = selectedRows;
        if (targets.Count == 0)
        {
            UserMessages.Information(this, Loc.Get("Status.NoSelection"));
            return;
        }

        if (targets.Count > 1)
        {
            // A confirmacao de uma remocao em bloco NUNCA e omitida pelas
            // preferencias de mensagem: e a pergunta que antecede a acao
            // destrutiva, e o usuario precisa ver a lista inteira.
            var message = Loc.Get("Cmd.UninstallManyConfirm", targets.Count) + "\n\n"
                + string.Join("\n", targets.Select(t => "• " + t.DisplayName));

            if (!UserMessages.Confirm(this, message)) return;
        }

        var wizard = new Ui.UninstallWizardWindow(
            targets.Select(t => t.Program).ToList(), quick, _all.Select(r => r.Program).ToList())
        {
            Owner = this,
        };
        wizard.ShowDialog();
        _ = ReloadAsync();
    }

    /// <summary>
    /// Inicia o assistente de desinstalacao para um programa especifico.
    /// Usado pelo Modo Cacador, que ja identificou o programa pelo icone arrastado.
    /// </summary>
    /// <summary>
    /// Os programas instalados, como a janela principal os tem.
    ///
    /// As telas que precisam da lista (o mapa de area, por exemplo) consomem
    /// esta propriedade em vez de escanear de novo: o mesmo scan, uma vez
    /// so.
    /// </summary>
    public IReadOnlyList<InstalledProgram> ProgramasInstalados =>
        _all.Select(r => r.Program).ToList();

    public void StartUninstallFor(InstalledProgram program)
    {
        var wizard = new Ui.UninstallWizardWindow(
            new List<InstalledProgram> { program }, quick: false, _all.Select(r => r.Program).ToList())
        {
            Owner = this,
        };
        wizard.ShowDialog();
        _ = ReloadAsync();
    }

    /// <summary>
    /// Repara a instalacao do programa pelo Windows Installer.
    ///
    /// E a acao "Modificar" do Revo e do Uninstall Tool: em vez de
    /// desinstalar, o Tranal devolve o programa para o estado em que o
    /// Windows espera que ele esteja.
    /// </summary>
    /// <param name="row">A linha do programa.</param>
    private void ModifyProgram(ProgramRow row)
    {
        var programa = row.Program;
        var codigo = programa?.ProductCode ?? string.Empty;

        if (string.IsNullOrWhiteSpace(codigo))
        {
            UserMessages.Information(this, Loc.Get("Cmd.ModifyProgramUnavailable"), Loc.Get("Cmd.ModifyProgram"));
            return;
        }

        // Silencioso: o Tranal ja mostrou o que a acao faz, e a interface
        // do Windows Installer nao acrescenta nada aqui.
        var resultado = MsiProductCatalog.Repair(codigo, silent: true);

        UserMessages.Information(this,
            resultado.Success ? resultado.Message : Loc.Get("Cmd.ModifyProgramFailed"),
            Loc.Get("Cmd.ModifyProgram"));
    }

    /// <summary>
    /// Marca (ou desmarca) todos os programas de um mesmo pais.
    ///
    /// A regra do que e "o mesmo pais" mora no nucleo
    /// (<see cref="Core.Appearance.ProgramSelection"/>); aqui so se marca a
    /// lista.
    /// </summary>
    /// <param name="pais">O pais.</param>
    /// <param name="marcado">True para marcar, false para desmarcar.</param>
    private void MarcarPorPais(string pais, bool marcado)
    {
        var alvos = Core.Appearance.ProgramSelection.ByCountry(
            _all.Select(r => r.Program).Where(p => p is not null)!, pais);

        var quantos = 0;
        foreach (var row in _all)
        {
            if (!alvos.Contains(row.Program)) continue;
            row.IsChecked = marcado;
            quantos++;
        }

        StatusText.Text = string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            marcado ? Loc.Get("Cmd.MarkedByCountry") : Loc.Get("Cmd.UnmarkedByCountry"),
            quantos, pais);
    }

    private void ForcedUninstallButton_Click(object sender, RoutedEventArgs e) => ShowForcedUninstall();

    private void ShowForcedUninstall()
    {
        var dialog = new Ui.ForcedUninstallWindow { Owner = this };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            StatusText.Text = $"{Loc.Get("Cmd.ForcedUninstall")}: {dialog.SelectedPath}";
            _ = ReloadAsync();
        }
    }
}
