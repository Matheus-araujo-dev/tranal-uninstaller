using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// Assistente de desinstalacao em 5 etapas, no mesmo fluxo do Revo:
/// Analyze -> Run Built-in Uninstaller -> Scan -> Removal -> Summary.
/// </summary>
public partial class UninstallWizardWindow : Window
{
    private readonly List<InstalledProgram> _programs;
    private readonly bool _quick;
    private readonly ObservableCollection<LeftoverItem> _leftovers = new();
    private int _step;

    /// <summary>Nomes das 5 etapas, no mesmo ordem do Revo.</summary>
    private static string[] StepNames => new[]
    {
        $"1. {Loc.Get("Wizard.Step1.Title")}",
        $"2. {Loc.Get("Wizard.Step2.Title")}",
        $"3. {Loc.Get("Wizard.Step3.Title")}",
        $"4. {Loc.Get("Wizard.Step4.Title")}",
        $"5. {Loc.Get("Wizard.Step5.Title")}",
    };

    // Estado acumulado do fluxo, consumido pela etapa 5 (resumo).
    private List<LeftoverItem> _analysis = new();
    private List<BuiltInUninstallResult> _builtInResults = new();
    private string _backupFile = string.Empty;
    private bool _restorePointCreated;
    private string _restorePointMessage = string.Empty;
    private UninstallMode _level = UninstallMode.Moderate;
    private RemovalReport? _report;
    private string _summary = string.Empty;

    public UninstallWizardWindow(List<InstalledProgram> programs, bool quick)
    {
        InitializeComponent();

        // Window.Title e GridViewColumn.Header nao aceitam binding: texto via codigo.
        Title = Loc.Get("Wizard.Title");
        if (LeftoversList.View is GridView grid && grid.Columns.Count >= 3)
        {
            grid.Columns[0].Header = Loc.Get("Common.Type");
            grid.Columns[1].Header = Loc.Get("Common.Path");
            grid.Columns[2].Header = Loc.Get("Common.Size");
        }

        _programs = programs;
        _quick = quick;
        StepsBar.ItemsSource = StepNames;
        LeftoversList.ItemsSource = _leftovers;
        ShowStep(0);
    }

    private InstalledProgram Target => _programs[0];

    private void ShowStep(int step)
    {
        _step = step;
        OptionsPanel.Visibility = Visibility.Collapsed;
        LeftoversList.Visibility = Visibility.Collapsed;
        SummaryText.Visibility = Visibility.Collapsed;
        MessageText.Visibility = Visibility.Visible;
        BackButton.IsEnabled = step > 0;
        NextButton.Content = Loc.Get(step == 4 ? "Wizard.Finish" : "Wizard.Next");

        var name = string.Join(", ", _programs.Select(p => p.DisplayName));
        TitleText.Text = _programs.Count == 1
            ? Loc.Get("Wizard.UninstallTitle", name)
            : Loc.Get("Wizard.UninstallTitleMany", name, _programs.Count);

        switch (step)
        {
            case 0:
                SubtitleText.Text = Loc.Get("Wizard.StepCounter", 1, Loc.Get("Wizard.Step1.Title"));
                MessageText.Text = Loc.Get("Wizard.AnalyzeIntro", name);
                StatusText.Text = Loc.Get("Wizard.Ready");
                break;

            case 1:
                SubtitleText.Text = Loc.Get("Wizard.StepCounter", 2, Loc.Get("Wizard.Step2.Title"));
                MessageText.Text = Loc.Get("Wizard.BuiltInIntro", name, Target.UninstallString);
                OptionsPanel.Visibility = Visibility.Visible;
                StatusText.Text = Loc.Get("Wizard.BackupNotice");
                break;

            case 2:
                SubtitleText.Text = Loc.Get("Wizard.StepCounter", 3, Loc.Get("Wizard.Step3.Title"));
                MessageText.Text = Loc.Get("Wizard.ScanIntro");
                OptionsPanel.Visibility = Visibility.Visible;
                StatusText.Text = Loc.Get("Wizard.ScanNotice");
                break;

            case 3:
                SubtitleText.Text = Loc.Get("Wizard.StepCounter", 4, Loc.Get("Wizard.Step4.Title"));
                MessageText.Text = Loc.Get("Wizard.RemovalIntro");
                LeftoversList.Visibility = Visibility.Visible;
                StatusText.Text = Loc.Get("Wizard.ItemsFound", _leftovers.Count);
                break;

            case 4:
                SubtitleText.Text = Loc.Get("Wizard.StepCounter", 5, Loc.Get("Wizard.Step5.Title"));
                MessageText.Visibility = Visibility.Collapsed;
                SummaryText.Visibility = Visibility.Visible;
                SummaryText.Text = _summary;
                StatusText.Text = Loc.Get("Wizard.Done");
                break;
        }
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            switch (_step)
            {
                case 0:
                    await AnalyzeAsync();
                    ShowStep(1);
                    break;

                case 1:
                    await RunBuiltInAsync();
                    ShowStep(2);
                    break;

                case 2:
                    await ScanAsync();
                    ShowStep(3);
                    break;

                case 3:
                    await RemoveAsync();
                    ShowStep(4);
                    break;

                case 4:
                    Close();
                    break;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Tranal Uninstaller", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Etapa 1: analise previa dos rastros do programa.</summary>
    private async Task AnalyzeAsync()
    {
        StatusText.Text = Loc.Get("Wizard.Analyzing");

        _analysis = await Task.Run(() => _programs
            .SelectMany(p => LeftoverScanner.Scan(p, UninstallMode.Advanced))
            .ToList());

        StatusText.Text = Loc.Get("Wizard.Analyzed", _analysis.Count);
    }

    private async Task RunBuiltInAsync()
    {
        StatusText.Text = Loc.Get("Wizard.CreatingBackup");
        if (BackupRegistryCheck.IsChecked == true)
        {
            var paths = _programs.Select(p => p.RegistryPath).ToList();
            _backupFile = RegistryBackup.Backup(paths);
        }

        if (RestorePointCheck.IsChecked == true)
        {
            StatusText.Text = Loc.Get("Wizard.CreatingRestorePoint");
            _restorePointCreated = UninstallEngine.TryCreateRestorePoint("Tranal Uninstaller Pro");
            _restorePointMessage = _restorePointCreated
                ? Loc.Get("Wizard.RestorePointCreated")
                : Loc.Get("Wizard.RestorePointFailed");
        }

        StatusText.Text = Loc.Get("Wizard.RunningBuiltIn");
        foreach (var program in _programs)
            _builtInResults.Add(await UninstallEngine.RunBuiltInUninstaller(program));
    }

    private async Task ScanAsync()
    {
        StatusText.Text = Loc.Get("Wizard.Scanning");
        _level = SafeRadio.IsChecked == true ? UninstallMode.Safe
            : ModerateRadio.IsChecked == true ? UninstallMode.Moderate
            : UninstallMode.Advanced;

        var items = await Task.Run(() =>
            _programs.SelectMany(p => LeftoverScanner.Scan(p, _level)).ToList());

        _leftovers.Clear();
        foreach (var item in items) _leftovers.Add(item);

        if (AutoDeleteCheck.IsChecked == true)
        {
            StatusText.Text = Loc.Get("Wizard.AutoRemoving");
            _report = UninstallEngine.RemoveLeftovers(_leftovers);
            _summary = BuildSummary(_report);
        }
        else
        {
            StatusText.Text = Loc.Get("Wizard.ItemsFound", _leftovers.Count);
        }
    }

    private async Task RemoveAsync()
    {
        StatusText.Text = Loc.Get("Wizard.Removing");

        // Quando a remocao automatica ja ocorreu na etapa 3, o relatorio e o mesmo:
        // nao ha dupla remocao nem dupla contagem.
        _report ??= await Task.Run(() => UninstallEngine.RemoveLeftovers(_leftovers));

        _summary = BuildSummary(_report);

        UninstallHistory.Append(new UninstallHistoryEntry
        {
            Programs = string.Join(", ", _programs.Select(p => p.DisplayName)),
            Level = _level.ToString(),
            BuiltIn = string.Join(" | ", _builtInResults.Select(r => r.Describe())),
            Report = _summary,
            Removed = _report.RemovedCount,
            Failed = _report.FailedCount + _report.ProtectedCount,
        });
    }

    /// <summary>Resumo final: contagens por categoria, igual ao painel de resumo do Revo.</summary>
    private string BuildSummary(RemovalReport report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(Loc.Get("Wizard.SummaryTitle", string.Join(", ", _programs.Select(p => p.DisplayName))));
        sb.AppendLine();

        foreach (var result in _builtInResults)
            sb.AppendLine($"• {result.Describe()}");

        if (_restorePointMessage.Length > 0)
            sb.AppendLine($"• {_restorePointMessage}");

        sb.AppendLine();
        sb.AppendLine(report.Describe());
        sb.AppendLine();
        sb.AppendLine(Loc.Get("Wizard.SummaryTotal", report.Total));
        sb.AppendLine(Loc.Get("Wizard.SummaryRemoved", report.RemovedCount));
        sb.AppendLine(Loc.Get("Wizard.SummaryAbsent", report.AbsentCount));
        sb.AppendLine(Loc.Get("Wizard.SummaryFailed", report.FailedCount + report.ProtectedCount));

        if (_backupFile.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine(Loc.Get("Wizard.BackupSaved", _backupFile));
            sb.AppendLine(Loc.Get("Wizard.BackupHint"));
        }

        return sb.ToString().TrimEnd();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_step > 0) ShowStep(_step - 1);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
}
