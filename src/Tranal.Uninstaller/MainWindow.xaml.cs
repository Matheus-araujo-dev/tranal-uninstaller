using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Theming;
using Tranal.Uninstaller.Ui;

namespace Tranal.Uninstaller;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ProgramRow> _all = new();
    private readonly ICollectionView _view;
    private string _module = "AllPrograms";

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

        SetupColumnHeaders();

        // Telas de modulos e ferramentas registradas uma unica vez.
        ViewSetup.RegisterAll(this);

        // Visibilidade dos paineis conforme as preferencias.
        ApplyLayout();

        // Textos do codigo-behind tambem mudam com o idioma.
        Loc.LanguageChanged += (_, _) => RefreshLocalizedTexts();
        Loaded += async (_, _) => await ReloadAsync();
    }

    // ------------------------------------------------------------------
    // Carga dos dados
    // ------------------------------------------------------------------
    private async Task ReloadAsync()
    {
        StatusText.Text = Loc.Get("Status.Loading");
        var options = new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = AppSettingsStore.Current.ShowSystemComponents,
            IncludeMsiProducts = false,
        };

        var programs = await Task.Run(() => ProgramScanner.Scan(options));

        _all.Clear();
        foreach (var program in programs)
            _all.Add(new ProgramRow(program));

        ApplyFilter();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var total = _all.Count;
        var visible = _view.Cast<ProgramRow>().Count();
        StatusText.Text = $"{Loc.Get("Status.Installations")}: {total}";
        if (visible != total) StatusText.Text += $"   ({Loc.Get("Status.Showing")} {visible})";
    }

    // ------------------------------------------------------------------
    // Busca em tempo real (nome, editora ou comentarios)
    // ------------------------------------------------------------------
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
        UpdateStatus();
    }

    private void ApplyFilter() => _view.Refresh();

    private bool FilterRow(object item)
    {
        if (item is not ProgramRow row) return false;
        return MatchesSearch(row) && MatchesType(row) && MatchesSize(row) && MatchesDrive(row);
    }

    private bool MatchesSearch(ProgramRow row)
    {
        var term = SearchBox.Text.Trim();
        if (term.Length == 0) return true;

        return Contains(row.DisplayName, term)
            || Contains(row.Publisher, term)
            || Contains(row.Comments, term);
    }

    private static bool Contains(string haystack, string term) =>
        haystack.Contains(term, StringComparison.CurrentCultureIgnoreCase);

    // ------------------------------------------------------------------
    // Filtros: por tipo, por tamanho e por unidade
    // ------------------------------------------------------------------
    private string _filterType = "All";
    private string _filterSize = "all";
    private string? _filterDrive;

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        PopulateDriveFilters();
        FilterPopup.IsOpen = !FilterPopup.IsOpen;
    }

    private void PopulateDriveFilters()
    {
        DriveFilterPanel.Children.Clear();

        var all = new RadioButton { Content = Loc.Get("Sidebar.All"), Margin = new Thickness(0, 0, 0, 2) };
        all.IsChecked = _filterDrive is null;
        all.Tag = "drive:*";
        all.Checked += FilterOption_Changed;
        DriveFilterPanel.Children.Add(all);

        foreach (var drive in DriveInfo.GetDrives()
                     .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                     .Select(d => d.Name).OrderBy(d => d))
        {
            var radio = new RadioButton { Content = drive, Margin = new Thickness(0, 0, 0, 2) };
            radio.IsChecked = _filterDrive == drive;
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
            _filterDrive = value == "*" ? null : value;
        }
        else if (tag is "All" or "32" or "64" or "msi")
        {
            _filterType = tag;
        }
        else
        {
            _filterSize = tag;
        }

        ApplyFilter();
        UpdateStatus();
    }

    private bool MatchesType(ProgramRow row) => _filterType switch
    {
        "32" => row.Bits == "32-bits",
        "64" => row.Bits == "64-bits",
        "msi" => row.Program.IsWindowsInstaller,
        _ => true,
    };

    private bool MatchesSize(ProgramRow row)
    {
        const long mb = 1024L * 1024L;
        const long gb = 1024L * mb;
        var size = row.Program.SizeBytes;

        return _filterSize switch
        {
            "small" => size > 0 && size < 100 * mb,
            "medium" => size >= 100 * mb && size <= gb,
            "large" => size > gb,
            _ => true,
        };
    }

    private bool MatchesDrive(ProgramRow row)
    {
        if (_filterDrive is null) return true;
        var source = row.Program.InstallLocation.Length > 0
            ? row.Program.InstallLocation
            : row.Program.UninstallString;
        return source.StartsWith(_filterDrive, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // Cabecalho: ordenacao por clique e menu de contexto (colunas/alinhamento)
    // ------------------------------------------------------------------
    private readonly Dictionary<GridViewColumn, string> _columnProperties = new();

    /// <summary>
    /// Propriedade ordenavel de cada coluna, na MESMA ordem das colunas do XAML.
    /// O cabecalho e localizado e nao serve como chave estavel; o indice e.
    /// </summary>
    private static readonly string[] ColumnSortProperties =
    {
        nameof(ProgramRow.DisplayName),
        nameof(ProgramRow.SizeText),
        nameof(ProgramRow.DisplayVersion),
        nameof(ProgramRow.Bits),
        nameof(ProgramRow.InstallDate),
        nameof(ProgramRow.Publisher),
        nameof(ProgramRow.UrlInfoAbout),
        nameof(ProgramRow.Comments),
        nameof(ProgramRow.UninstallString),
    };

    /// <summary>
    /// Chave de traducao do cabecalho de cada coluna, na MESMA ordem das colunas
    /// do XAML. GridViewColumn.Header NAO e uma DependencyProperty: nao aceita
    /// binding, entao o texto e atribuido em codigo.
    /// </summary>
    private static readonly string[] ColumnHeaderKeys =
    {
        "Common.Program", "Common.Size", "Common.Version", "Common.Type", "Common.Date",
        "Common.Publisher", "Common.Homepage", "Common.Comments", "Common.UninstallString",
    };

    /// <summary>
    /// A propriedade ordenavel vem do indice da coluna (o cabecalho e localizado e
    /// nao serve como chave estavel).
    /// </summary>
    private void SetupColumnHeaders()
    {
        if (ProgramsList.View is not GridView grid) return;

        for (var index = 0; index < grid.Columns.Count; index++)
        {
            var column = grid.Columns[index];
            if (index < ColumnSortProperties.Length)
                _columnProperties[column] = ColumnSortProperties[index];

            // Cabecalho traduzido (propriedade CLR simples, sem binding).
            if (index < ColumnHeaderKeys.Length)
                column.Header = Loc.Get(ColumnHeaderKeys[index]);

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
        _columnMenu ??= new ContextMenu();
        _columnMenu.Opened += (_, _) => PopulateColumnMenu(_columnMenu);
        PopulateColumnMenu(_columnMenu);
        return _columnMenu;
    }

    private void PopulateColumnMenu(ContextMenu menu)
    {
        menu.Items.Clear();

        if (ProgramsList.View is GridView grid)
        {
            var index = 0;
            foreach (var column in grid.Columns)
            {
                var title = _columnProperties.TryGetValue(column, out var property)
                    ? TitleForProperty(property)
                    : column.Header?.ToString() ?? string.Empty;

                // AutomationId estavel por coluna: "ColDisplayName", "ColSizeText"...
                var automationId = property is not null ? $"Col{property}" : $"Col{index}";

                var item = new MenuItem
                {
                    Header = title,
                    IsCheckable = true,
                    IsChecked = column.Width > 0,
                };
                AutomationProperties.SetAutomationId(item, automationId);

                var target = column;
                var originalWidth = column.Width;
                item.Click += (_, _) =>
                {
                    target.Width = item.IsChecked ? originalWidth : 0;
                };
                menu.Items.Add(item);
                index++;
            }
        }

        menu.Items.Add(new Separator());
        var align = new MenuItem { Header = "Realinhar texto" };
        align.Items.Add(MenuItemFor("Esquerda", "AlignLeft", () => SetAlignment(HorizontalAlignment.Left)));
        align.Items.Add(MenuItemFor("Centro", "AlignCenter", () => SetAlignment(HorizontalAlignment.Center)));
        align.Items.Add(MenuItemFor("Direita", "AlignRight", () => SetAlignment(HorizontalAlignment.Right)));
        menu.Items.Add(align);
    }

    private static string TitleForProperty(string property) => property switch
    {
        nameof(ProgramRow.DisplayName) => Loc.Get("Common.Program"),
        nameof(ProgramRow.SizeText) => Loc.Get("Common.Size"),
        nameof(ProgramRow.DisplayVersion) => Loc.Get("Common.Version"),
        nameof(ProgramRow.Bits) => Loc.Get("Common.Type"),
        nameof(ProgramRow.InstallDate) => Loc.Get("Common.Date"),
        nameof(ProgramRow.Publisher) => Loc.Get("Common.Publisher"),
        nameof(ProgramRow.UrlInfoAbout) => Loc.Get("Common.Homepage"),
        nameof(ProgramRow.Comments) => Loc.Get("Common.Comments"),
        nameof(ProgramRow.UninstallString) => Loc.Get("Common.UninstallString"),
        _ => property,
    };

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

        var uninstall = CommandItem(Loc.Get("Cmd.Uninstall"), "CmdUninstall", () => StartUninstall(quick: false));
        menu.Items.Add(uninstall);

        var quick = CommandItem(Loc.Get("Cmd.QuickUninstall"), "CmdQuickUninstall", () => StartUninstall(quick: true));
        menu.Items.Add(quick);

        var forced = CommandItem(Loc.Get("Cmd.ForcedUninstall"), "CmdForcedUninstall", ShowForcedUninstall);
        menu.Items.Add(forced);

        menu.Items.Add(new Separator());

        var viewMenu = new MenuItem { Header = Loc.Get("Cmd.View") };
        viewMenu.Items.Add(MenuItemFor(Loc.Get("Cmd.ViewDetails"), "CmdViewDetails", () => ApplyViewMode("Details")));
        viewMenu.Items.Add(MenuItemFor(Loc.Get("Cmd.ViewList"), "CmdViewList", () => ApplyViewMode("List")));
        viewMenu.Items.Add(MenuItemFor(Loc.Get("Cmd.ViewIcons"), "CmdViewIcons", () => ApplyViewMode("Icons")));
        menu.Items.Add(viewMenu);

        var exportMenu = new MenuItem { Header = Loc.Get("Cmd.ExportList") };
        foreach (var format in new[] { "TXT", "CSV", "HTML", "XML" })
        {
            var item = CommandItem(format, $"CmdExport{format}", () => ExportList(format));
            exportMenu.Items.Add(item);
        }
        menu.Items.Add(exportMenu);

        menu.Items.Add(new Separator());
        var options = CommandItem(Loc.Get("Cmd.Options"), "CmdOptions", () => ShowOptions());
        menu.Items.Add(options);

        var backups = CommandItem(Loc.Get("Cmd.BackupManager"), "CmdBackupManager", () => ShowBackupManager());
        menu.Items.Add(backups);

        menu.PlacementTarget = MoreCommandsButton;
        menu.IsOpen = true;
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

    private void ApplyViewMode(string mode)
    {
        var grid = ProgramsList.View as GridView;
        if (grid is null) return;

        // List/Icons usam a mesma grade, apenas com menos colunas visiveis.
        var widths = mode switch
        {
            "List" => new[] { 320d, 95d, 130d },
            _ => new[] { 90d, 90d, 90d },
        };
        for (var i = 0; i < grid.Columns.Count; i++)
            grid.Columns[i].Width = i < widths.Length ? widths[i] : 0;
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

        var topmost = CommandItem(Loc.Get("Cmd.TopmostWindow"), "CmdTopmostWindow", () => ShowTopmostWindow());
        menu.Items.Add(topmost);

        menu.Items.Add(BuildPanelsMenu());

        var themeMenu = new MenuItem { Header = Loc.Get("Cmd.Theme") };
        themeMenu.Items.Add(MenuItemFor(Loc.Get("Cmd.ThemeDark"), "CmdThemeDark",
            () => ThemeManager.Apply(ThemeManager.Dark)));
        themeMenu.Items.Add(MenuItemFor(Loc.Get("Cmd.ThemeLight"), "CmdThemeLight",
            () => ThemeManager.Apply(ThemeManager.Light)));
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
    private void ExportList(string format)
    {
        var save = new SaveFileDialog
        {
            Filter = $"{format} (*.{format.ToLowerInvariant()})|*.{format.ToLowerInvariant()}",
            FileName = $"tranal-lista.{format.ToLowerInvariant()}",
        };
        if (save.ShowDialog(this) != true) return;

        var rows = _view.Cast<ProgramRow>().ToList();
        var content = format switch
        {
            "CSV" => Exporters.ToCsv(rows.Select(r => r.Program)),
            "HTML" => Exporters.ToHtml(rows.Select(r => r.Program)),
            "XML" => Exporters.ToXml(rows.Select(r => r.Program)),
            _ => Exporters.ToText(rows.Select(r => r.Program)),
        };

        File.WriteAllText(save.FileName, content, System.Text.Encoding.UTF8);
        StatusText.Text = $"{Loc.Get("Status.Exported")}: {save.FileName}";
    }

    // ------------------------------------------------------------------
    // Desinstalacao
    // ------------------------------------------------------------------
    /// <summary>
    /// Programas alvo da desinstalacao: SOMENTE os itens selecionados na lista.
    /// As caixas de marcacao sao apenas indicador visual (como no Revo) e nunca
    /// definem o alvo — caso contrario uma unica acao removeria todos os programas.
    /// </summary>
    private List<ProgramRow> selectedRows =>
        ProgramsList.SelectedItems.Cast<ProgramRow>().ToList();

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
            var confirm = MessageBox.Show(this,
                $"Deseja desinstalar {targets.Count} programas?\n\n"
                + string.Join("\n", targets.Select(t => "• " + t.DisplayName)),
                "Tranal Uninstaller", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
        }

        var wizard = new Ui.UninstallWizardWindow(targets.Select(t => t.Program).ToList(), quick)
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
    public void StartUninstallFor(InstalledProgram program)
    {
        var wizard = new Ui.UninstallWizardWindow(new List<InstalledProgram> { program }, quick: false)
        {
            Owner = this,
        };
        wizard.ShowDialog();
        _ = ReloadAsync();
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
