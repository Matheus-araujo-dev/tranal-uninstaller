using System.Windows;
using System.Windows.Controls;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Theming;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// Janela de opcoes do Tranal Uninstaller Pro (paridade com "Menu -&gt; Options"
/// do Revo): tema, idioma e comportamento da listagem.
/// </summary>
public partial class OptionsWindow : Window
{
    public OptionsWindow()
    {
        InitializeComponent();

        // Window.Title nao aceita binding: texto via codigo.
        Title = Loc.Get("Options.Title");

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
        });

        ThemeManager.Apply(theme);
        Loc.SetLanguage(language);

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
