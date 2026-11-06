using System.Windows;
using System.Windows.Controls;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Theming;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// Janela de opcoes do Tranal Uninstaller Pro (paridade com "Menu -&gt; Options"
/// do Revo): tema, idioma, comportamento da listagem e as preferencias de
/// desinstalacao, armazenamento e atualizacao.
/// </summary>
public partial class OptionsWindow : Window
{
    /// <summary>Titulo do grupo de preferencias de desinstalacao.</summary>
    private static string UninstallSection => Loc.Get("Options.UninstallSection");

    /// <summary>Titulo do grupo de preferencias de armazenamento.</summary>
    private static string StorageSection => Loc.Get("Options.StorageSection");

    /// <summary>Titulo do grupo de preferencias de layout.</summary>
    private static string LayoutSection => Loc.Get("Options.LayoutSection");

    /// <summary>Cria a janela de opcoes e carrega os valores atuais.</summary>
    public OptionsWindow()
    {
        InitializeComponent();

        // Window.Title nao aceita binding: texto via codigo.
        Title = Loc.Get("Options.Title");

        // Titulos de grupo que ainda nao existem no catalogo de idiomas.
        UninstallSectionText.Text = UninstallSection;
        StorageSectionText.Text = StorageSection;
        LayoutSectionText.Text = LayoutSection;

        // A lista de temas vem do catalogo do nucleo: e a mesma que a linha de
        // comando e o painel de configuracao listam. Um tema que existe no
        // XAML e nao esta no catalogo nao aparece para o usuario.
        BuildThemeCombo();
        BuildColorRows();

        foreach (var language in LanguageCatalog.All)
        {
            var item = new ComboBoxItem
            {
                Content = $"{language.NativeName} ({language.EnglishName})",                Tag = language.Code,
            };
            LanguageCombo.Items.Add(item);
        }

        var settings = AppSettingsStore.Current;
        SelectByTag(ThemeCombo, ThemeManager.Normalize(settings.Theme));
        SelectByTag(LanguageCombo, settings.Language);

        SystemComponentsCheck.IsChecked = settings.ShowSystemComponents;
        WindowsAppsCheck.IsChecked = settings.ShowWindowsApps;
        NewDaysBox.Text = settings.NewProgramsDays.ToString();

        ShowWebShortcutsCheck.IsChecked = settings.ShowWebPageShortcuts;
        DisableTextAnimationsCheck.IsChecked = settings.DisableTextAnimations;
        ShowSplashScreenCheck.IsChecked = settings.ShowSplashScreen;
        KeepPreferencesCheck.IsChecked = settings.KeepPreferencesOnUninstall;
        FocusTracedProgramsCheck.IsChecked = settings.FocusTracedProgramsOnStartup;
        ShowCheckBoxesCheck.IsChecked = settings.ShowCheckBoxesInDetailsView;
        AdvancedModeCheck.IsChecked = settings.AdvancedMode;
        FastLoadingCheck.IsChecked = settings.FastLoadingMode;
        CheckUpdateCheck.IsChecked = settings.CheckForUpdateOnStartup;
        AutoUpdateCheck.IsChecked = settings.UpdateAutomatically;
        SkipInformationCheck.IsChecked = settings.SkipInformationMessages;
        SkipWarningCheck.IsChecked = settings.SkipWarningMessages;
        ShowContextMenuCommandsCheck.IsChecked = settings.ShowContextMenuCommands;
        DeleteTracedLogCheck.IsChecked = settings.DeleteTracedLogAfterUninstall;

        ShowMainToolbarCheck.IsChecked = settings.ShowMainToolbar;
        ShowDescriptionPanelCheck.IsChecked = settings.ShowDescriptionPanel;
        ShowStatusBarCheck.IsChecked = settings.ShowStatusBar;
        ShowToolsToolbarCheck.IsChecked = settings.ShowToolsToolbar;

        CreateRestorePointCheck.IsChecked = settings.CreateRestorePointBeforeUninstall;
        OnlyBuiltInCheck.IsChecked = settings.OnlyRunBuiltInUninstaller;
        DisableHistoryCheck.IsChecked = settings.DisableUninstallHistory;
        RecycleBinCheck.IsChecked = settings.SendFilesToRecycleBin;
        DeferRemovalCheck.IsChecked = settings.DeferRemovalToNextRestart;
        BackupBeforeRemovalCheck.IsChecked = settings.BackupBeforeRemoval;
        CheckAllLeftoversCheck.IsChecked = settings.CheckAllLeftoversByDefault;
        ForciblyStopExecutablesCheck.IsChecked = settings.ForciblyStopRunningExecutables;
        ShowScanAllAccountsCheck.IsChecked = settings.ShowScanAllAccountsOption;

        PortableFolderBox.Text = settings.PortableDataFolder;
        WebSearchBox.Text = settings.WebSearchEngine;
    }

    /// <summary>
    /// Monta a lista de temas a partir do catalogo do nucleo.
    ///
    /// A lista e a mesma que a linha de comando e o painel de configuracao
    /// mostram: um tema novo entra no catalogo do nucleo e aparece aqui
    /// sem precisar tocar nesta tela.
    /// </summary>
    private void BuildThemeCombo()
    {
        foreach (var skin in ThemeSkinCatalog.All)
        {
            ThemeCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{skin.DisplayName} — {skin.Description}",
                Tag = skin.Id,
            });
        }
    }

    /// <summary>
    /// Monta as linhas das cores personalizadas.
    ///
    /// Cada linha e um papel de cor do nucleo, e o texto inicial e a cor
    /// que o tema escolhido ja usa. O usuario edita o hexadecimal e ve a
    /// amostra mudar; a cor so e gravada se for valida.
    /// </summary>
    private void BuildColorRows()
    {
        CustomColorsTitleText.Text = Loc.Get("Options.CustomColors");
        CustomColorsPanel.Items.Clear();

        var palette = ThemePalettes.Of(AppSettingsStore.Current.Theme);
        var custom = AppSettingsStore.Current.CustomThemeColors;

        foreach (var role in ThemePalettes.Roles)
        {
            var hex = palette.ColorOf(role);
            if (custom.TryGetValue(role.ToString(), out var chosen) && chosen.Length > 0)
                hex = chosen;

            CustomColorsPanel.Items.Add(new ColorRow
            {
                Role = role,
                Label = ThemePalettes.DescribeRole(role),
                Hex = hex,
                BoxId = "OptColor" + role,
            });
        }
    }

    /// <summary>
    /// As cores personalizadas escolhidas, com as invalidas descartadas.
    ///
    /// Uma cor invalida nao e gravada: e melhor a tela ficar com a cor do
    /// tema do que ficar sem cor nenhuma.
    /// </summary>
    private Dictionary<string, string> ReadCustomColors()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in CustomColorsPanel.Items.OfType<ColorRow>())
        {
            if (ThemePalette.TryNormalizeHex(row.Hex, out var hex))
                result[row.Role.ToString()] = hex;
        }

        return result;
    }

    /// <summary>
    /// Uma linha de cor personalizada: o rotulo, a amostra e o hexadecimal.
    /// </summary>
    private sealed class ColorRow : System.ComponentModel.INotifyPropertyChanged
    {
        private string _hex = string.Empty;

        /// <summary>O papel da cor no tema.</summary>
        public ThemeColorRole Role { get; init; }

        /// <summary>O nome do papel, como o usuario ve.</summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>O identificador da caixa de texto, para a automacao.</summary>
        public string BoxId { get; init; } = string.Empty;

        /// <summary>O hexadecimal que o usuario digitou.</summary>
        public string Hex
        {
            get => _hex;
            set
            {
                _hex = value ?? string.Empty;
                Brush = MakeBrush(_hex);
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Hex)));
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Brush)));
            }
        }

        /// <summary>A amostra da cor, para o usuario ver o que digitou.</summary>
        public System.Windows.Media.Brush Brush { get; private set; } = System.Windows.Media.Brushes.Transparent;

        /// <summary>Avisa que a linha mudou.</summary>
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// A cor da amostra. Uma cor invalida deixa a amostra transparente:
        /// e o jeito de dizer "isso aqui nao e uma cor" sem travar a digitacao.
        /// </summary>
        private static System.Windows.Media.Brush MakeBrush(string hex)
        {
            if (!ThemePalettes.TryParseColor(hex, out var argb))
                return System.Windows.Media.Brushes.Transparent;

            return new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(
                    (byte)((argb >> 16) & 0xFF),
                    (byte)((argb >> 8) & 0xFF),
                    (byte)(argb & 0xFF)));
        }
    }
    private static void SelectByTag(ComboBox combo, string tag)    {
        foreach (var item in combo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }

        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    /// <summary>Grava as preferencias escolhidas e aplica tema e idioma.</summary>
    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var theme = (ThemeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? ThemeManager.Dark;
        var language = (LanguageCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? Loc.DefaultLanguage;

        var days = int.TryParse(NewDaysBox.Text, out var parsed) && parsed > 0 ? parsed : 7;

        AppSettingsStore.Update(s =>
        {
            s.Theme = theme;
            s.Language = language;
            s.ShowSystemComponents = SystemComponentsCheck.IsChecked == true;
            s.ShowWindowsApps = WindowsAppsCheck.IsChecked == true;
            s.NewProgramsDays = days;

            s.ShowWebPageShortcuts = ShowWebShortcutsCheck.IsChecked == true;
            s.AdvancedMode = AdvancedModeCheck.IsChecked == true;
            s.FastLoadingMode = FastLoadingCheck.IsChecked == true;
            s.CheckForUpdateOnStartup = CheckUpdateCheck.IsChecked == true;
            s.UpdateAutomatically = AutoUpdateCheck.IsChecked == true;
            s.SkipInformationMessages = SkipInformationCheck.IsChecked == true;
            s.SkipWarningMessages = SkipWarningCheck.IsChecked == true;
            s.ShowContextMenuCommands = ShowContextMenuCommandsCheck.IsChecked == true;
            s.DeleteTracedLogAfterUninstall = DeleteTracedLogCheck.IsChecked == true;

            s.ShowMainToolbar = ShowMainToolbarCheck.IsChecked == true;
            s.ShowDescriptionPanel = ShowDescriptionPanelCheck.IsChecked == true;
            s.ShowStatusBar = ShowStatusBarCheck.IsChecked == true;
            s.ShowToolsToolbar = ShowToolsToolbarCheck.IsChecked == true;

            s.CreateRestorePointBeforeUninstall = CreateRestorePointCheck.IsChecked == true;
            s.OnlyRunBuiltInUninstaller = OnlyBuiltInCheck.IsChecked == true;
            s.DisableUninstallHistory = DisableHistoryCheck.IsChecked == true;
            s.SendFilesToRecycleBin = RecycleBinCheck.IsChecked == true;
            s.DeferRemovalToNextRestart = DeferRemovalCheck.IsChecked == true;
            s.BackupBeforeRemoval = BackupBeforeRemovalCheck.IsChecked == true;
            s.CheckAllLeftoversByDefault = CheckAllLeftoversCheck.IsChecked == true;
            s.ForciblyStopRunningExecutables = ForciblyStopExecutablesCheck.IsChecked == true;
            s.ShowScanAllAccountsOption = ShowScanAllAccountsCheck.IsChecked == true;

            s.CustomThemeColors = ReadCustomColors();
            s.DisableTextAnimations = DisableTextAnimationsCheck.IsChecked == true;
            s.ShowSplashScreen = ShowSplashScreenCheck.IsChecked == true;
            s.KeepPreferencesOnUninstall = KeepPreferencesCheck.IsChecked == true;
            s.FocusTracedProgramsOnStartup = FocusTracedProgramsCheck.IsChecked == true;
            s.ShowCheckBoxesInDetailsView = ShowCheckBoxesCheck.IsChecked == true;
            s.PortableDataFolder = PortableFolderBox.Text.Trim();
            s.WebSearchEngine = WebSearchBox.Text.Trim();
        });

        ThemeManager.Apply(theme);
        Loc.SetLanguage(language);

        // A entrada no menu de contexto do Windows entra em vigor na hora.
        Core.Topmost.ExplorerContextMenu.Apply(AppSettingsStore.Current.ShowContextMenuCommands);

        DialogResult = true;
        Close();
    }

    /// <summary>Fecha a janela sem gravar as preferencias.</summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
