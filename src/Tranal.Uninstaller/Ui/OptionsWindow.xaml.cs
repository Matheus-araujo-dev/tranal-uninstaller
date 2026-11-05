using System.Windows;
using System.Windows.Controls;
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

    /// <summary>Cria a janela de opcoes e carrega os valores atuais.</summary>
    public OptionsWindow()
    {
        InitializeComponent();

        // Window.Title nao aceita binding: texto via codigo.
        Title = Loc.Get("Options.Title");

        // Titulos de grupo que ainda nao existem no catalogo de idiomas.
        UninstallSectionText.Text = UninstallSection;
        StorageSectionText.Text = StorageSection;

        foreach (var language in LanguageCatalog.All)
        {
            var item = new ComboBoxItem
            {
                Content = $"{language.NativeName} ({language.EnglishName})",
                Tag = language.Code,
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
        AdvancedModeCheck.IsChecked = settings.AdvancedMode;
        FastLoadingCheck.IsChecked = settings.FastLoadingMode;
        CheckUpdateCheck.IsChecked = settings.CheckForUpdateOnStartup;
        AutoUpdateCheck.IsChecked = settings.UpdateAutomatically;

        CreateRestorePointCheck.IsChecked = settings.CreateRestorePointBeforeUninstall;
        OnlyBuiltInCheck.IsChecked = settings.OnlyRunBuiltInUninstaller;
        DisableHistoryCheck.IsChecked = settings.DisableUninstallHistory;
        RecycleBinCheck.IsChecked = settings.SendFilesToRecycleBin;
        DeferRemovalCheck.IsChecked = settings.DeferRemovalToNextRestart;
        BackupBeforeRemovalCheck.IsChecked = settings.BackupBeforeRemoval;

        PortableFolderBox.Text = settings.PortableDataFolder;
        WebSearchBox.Text = settings.WebSearchEngine;
    }

    private static void SelectByTag(ComboBox combo, string tag)
    {
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

            s.CreateRestorePointBeforeUninstall = CreateRestorePointCheck.IsChecked == true;
            s.OnlyRunBuiltInUninstaller = OnlyBuiltInCheck.IsChecked == true;
            s.DisableUninstallHistory = DisableHistoryCheck.IsChecked == true;
            s.SendFilesToRecycleBin = RecycleBinCheck.IsChecked == true;
            s.DeferRemovalToNextRestart = DeferRemovalCheck.IsChecked == true;
            s.BackupBeforeRemoval = BackupBeforeRemovalCheck.IsChecked == true;

            s.PortableDataFolder = PortableFolderBox.Text.Trim();
            s.WebSearchEngine = WebSearchBox.Text.Trim();
        });

        ThemeManager.Apply(theme);
        Loc.SetLanguage(language);

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
