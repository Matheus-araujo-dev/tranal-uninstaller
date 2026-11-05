using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Settings;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// Assistente de desinstalacao em 5 etapas, no mesmo fluxo do Revo:
/// Analisar -&gt; Executar desinstalador nativo -&gt; Verificar sobras -&gt;
/// Remocao -&gt; Resumo.
///
/// O usuario escolhe o MODO de desinstalacao (Somente, Seguro, Moderado ou
/// Avancado) e ele e a unica opcao da operacao: define o que e varrido e se
/// ha varredura. Nao existe mais um "nivel de varredura" separado.
/// </summary>
public partial class UninstallWizardWindow : Window
{
    /// <summary>
    /// Mensagem da etapa 3. O catalogo ainda fala em "nivel de varredura",
    /// que deixou de existir: o modo escolhido e a unica opcao da operacao.
    /// </summary>
    private static string ScanStepMessage => Loc.Get("Wizard.ScanStepMessage");

    /// <summary>Informa quantas sobras ficaram no sistema e quantas ja existiam antes.</summary>
    private static string LeftoversAfterUninstall => Loc.Get("Wizard.LeftoversAfterUninstall");

    /// <summary>Instrucao de recuperacao do conteudo removido.</summary>
    private static string RestorePointHint => Loc.Get("Wizard.RestorePointHint");

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
    private string _backupError = string.Empty;
    private bool _restorePointCreated;
    private string _restorePointMessage = string.Empty;
    private UninstallMode _mode = UninstallMode.Moderate;
    private int _survivors;
    private RemovalReport? _report;
    private string _summary = string.Empty;

    /// <summary>
    /// Opcoes da operacao, resolvidas uma vez no inicio (etapa 2) para que
    /// a etapa 4 remova exatamente o que foi pedido.
    /// </summary>
    private UninstallRemovalOptions _removalOptions = UninstallRemovalOptions.Default;

    /// <summary>
    /// Backup completo do registro, criado ANTES do desinstalador nativo.
    /// Vazio quando a opcao nao foi pedida.
    /// </summary>
    private FullRegistryBackupResult? _fullRegistryBackup;

    /// <summary>
    /// Opcao "marcar todos os itens restantes por padrao" do Revo: quando
    /// ligada, a varredura marca tambem os itens de confianca media e baixa.
    /// Lida das preferencias no momento da varredura.
    /// </summary>
    private static bool CheckAllLeftovers =>
        AppSettingsStore.Current.CheckAllLeftoversByDefault;

    /// <summary>Cria o assistente para a lista de programas informada.</summary>
    /// <param name="programs">Programas a desinstalar.</param>
    /// <param name="quick">True quando a desinstalacao foi pedido em modo rapido.</param>
    public UninstallWizardWindow(List<InstalledProgram> programs, bool quick)
    {
        InitializeComponent();

        // Window.Title e GridViewColumn.Header nao aceitam binding: texto via codigo.
        Title = Loc.Get("Wizard.Title");
        BuildLeftoversView();

        _programs = programs;
        _quick = quick;
        StepsBar.ItemsSource = StepNames;
        LeftoversList.ItemsSource = _leftovers;

        ApplyPreferences();

        // Desmarcar a varredura em todas as contas desmarca tambem a
        // desinstalacao de todas as contas: as duas opcoes andam juntas.
        ScanAllAccountsCheck.Checked += (_, _) => SyncAccountChecks();
        ScanAllAccountsCheck.Unchecked += (_, _) => SyncAccountChecks();

        ShowStep(0);
    }

    private InstalledProgram Target => _programs[0];

    /// <summary>
    /// Modo escolhido pelo usuario. E a unica opcao da operacao: ela define
    /// o que e varrido e se ha varredura.
    /// </summary>
    private UninstallMode SelectedMode
    {
        get
        {
            if (ModeBuiltInRadio.IsChecked == true) return UninstallMode.BuiltIn;
            if (ModeSafeRadio.IsChecked == true) return UninstallMode.Safe;
            if (ModeAdvancedRadio.IsChecked == true) return UninstallMode.Advanced;
            return UninstallMode.Moderate;
        }
    }

    /// <summary>
    /// Aplica as preferencias do usuario ao assistente: o ponto de restauracao
    /// segue a preferencia das Opcoes e a opcao "executar apenas o
    /// desinstalador nativo" trava o modo em "Somente".
    /// </summary>
    private void ApplyPreferences()
    {
        var settings = AppSettingsStore.Current;

        RestorePointCheck.IsChecked = settings.CreateRestorePointBeforeUninstall;
        RestorePointNote.Text = Loc.Get("Wizard.RestorePointDisabled");
        RestorePointNote.Visibility = settings.CreateRestorePointBeforeUninstall
            ? Visibility.Collapsed
            : Visibility.Visible;

        FullRegistryBackupCheck.IsChecked = settings.CreateFullRegistryBackupBeforeUninstall;

        // As opcoes de conta do Windows so aparecem quando o interruptor
        // mestre das Opcoes esta ligado — como no original, onde a opcao
        // existe mas fica oculta ate o usuario habilita-la.
        var showAccounts = settings.ShowScanAllAccountsOption;

        ScanAllAccountsCheck.IsChecked = settings.ScanAllWindowsAccounts;
        ScanAllAccountsCheck.Visibility = showAccounts ? Visibility.Visible : Visibility.Collapsed;

        UninstallAllAccountsCheck.IsChecked = settings.UninstallFromAllWindowsAccounts;
        UninstallAllAccountsCheck.Visibility = showAccounts ? Visibility.Visible : Visibility.Collapsed;

        ScanAllAccountsNote.Text = Loc.Get("Wizard.ScanAllAccountsNote");
        ScanAllAccountsNote.Visibility = Visibility.Collapsed;

        SyncAccountChecks();

        if (settings.OnlyRunBuiltInUninstaller)
        {
            ModeBuiltInRadio.IsChecked = true;
            ModeSafeRadio.IsEnabled = false;
            ModeModerateRadio.IsEnabled = false;
            ModeAdvancedRadio.IsEnabled = false;
        }
        else
        {
            ModeModerateRadio.IsChecked = true;
        }
    }

    /// <summary>
    /// Garante a invariante entre as duas opcoes de conta: marcar para
    /// desinstalar de todas as contas so faz sentido quando a varredura em
    /// todas as contas esta ligada. Sem isso, a marcacao ficaria valendo sem
    /// nenhum item de outra conta na lista.
    /// </summary>
    private void SyncAccountChecks()
    {
        var scanAll = ScanAllAccountsCheck.IsChecked == true;

        if (!scanAll)
            UninstallAllAccountsCheck.IsChecked = false;

        UninstallAllAccountsCheck.IsEnabled = scanAll;
        ScanAllAccountsNote.Visibility = scanAll ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Opcoes de varredura do assistente, ja normalizadas.</summary>
    private LeftoverScanOptions BuildScanOptions() => new()
    {
        Mode = _mode,
        CheckAllLeftovers = CheckAllLeftovers,
        ScanAllUserAccounts = ScanAllAccountsCheck.IsChecked == true,
        SelectOtherAccountItems =
            (ScanAllAccountsCheck.IsChecked == true) && (UninstallAllAccountsCheck.IsChecked == true),
    };

    /// <summary>Mostra uma das 5 etapas do fluxo.</summary>
    private void ShowStep(int step)
    {
        _step = step;
        ModePanel.Visibility = Visibility.Collapsed;
        OptionsPanel.Visibility = Visibility.Collapsed;
        ModeInfoText.Visibility = Visibility.Collapsed;
        RemovalPanel.Visibility = Visibility.Collapsed;
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
                ModePanel.Visibility = Visibility.Visible;
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
                MessageText.Text = UninstallModes.Scans(_mode)
                    ? ScanStepMessage
                    : Loc.Get("Wizard.ScanSkipped");
                ModeInfoText.Text = $"{Loc.Get("Wizard.Step3.Mode")}: {UninstallModes.ShortName(_mode)}";
                ModeInfoText.Visibility = Visibility.Visible;
                StatusText.Text = UninstallModes.Scans(_mode)
                    ? Loc.Get("Wizard.ScanNotice")
                    : Loc.Get("Wizard.ScanSkipped");
                break;

            case 3:
                SubtitleText.Text = Loc.Get("Wizard.StepCounter", 4, Loc.Get("Wizard.Step4.Title"));
                MessageText.Text = Loc.Get("Wizard.RemovalIntro");
                RemovalPanel.Visibility = Visibility.Visible;
                StatusText.Text = string.Format(
                    CultureInfo.CurrentCulture, LeftoversAfterUninstall, _leftovers.Count, _survivors);
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
            UserMessages.Warning(this, ex.Message);
        }
    }

    /// <summary>
    /// Grava as opcoes escolhidas no painel da etapa 2. A interface e a linha
    /// de comando leem as mesmas preferencias: o que o usuario marca aqui
    /// vale para as duas.
    /// </summary>
    private void PersistWizardOptions()
    {
        var scanAll = ScanAllAccountsCheck.IsChecked == true;
        var uninstallAll = scanAll && UninstallAllAccountsCheck.IsChecked == true;

        AppSettingsStore.Update(s =>
        {
            s.CreateFullRegistryBackupBeforeUninstall = FullRegistryBackupCheck.IsChecked == true;
            s.ScanAllWindowsAccounts = scanAll;
            s.UninstallFromAllWindowsAccounts = uninstallAll;
        });
    }

    /// <summary>
    /// Etapa 1: registra quais rastros do programa existem ANTES da
    /// desinstalacao. Esse conjunto e comparado com a varredura da etapa 3
    /// para dizer o que sobrou.
    /// </summary>
    private async Task AnalyzeAsync()
    {
        StatusText.Text = Loc.Get("Wizard.Analyzing");
        _mode = SelectedMode;

        _analysis = await Task.Run(() => _programs
            .SelectMany(p => LeftoverScanner.Scan(p, BuildScanOptions()))
            .ToList());

        StatusText.Text = Loc.Get("Wizard.Analyzed", _analysis.Count);
    }

    /// <summary>
    /// Etapa 2: cria o backup do Registro e o ponto de restauracao do sistema
    /// (quando a preferencia das Opcoes esta ligada) e executa o desinstalador
    /// nativo.
    /// </summary>
    private async Task RunBuiltInAsync()
    {
        PersistWizardOptions();

        var name = string.Join(", ", _programs.Select(p => p.DisplayName));
        _removalOptions = UninstallRemovalOptions.FromSettings($"Desinstalacao de {name}");

        // O backup completo do registro vem ANTES do desinstalador nativo: e
        // depois dele que o desinstalador do proprio programa apaga as chaves
        // dele, e um .reg do registro ja limpo nao serve para nada.
        if (_removalOptions.FullRegistryBackup)
        {
            StatusText.Text = Loc.Get("Wizard.CreatingFullRegistryBackup");
            _fullRegistryBackup = UninstallEngine.PrepareFullRegistryBackup(_removalOptions);
        }

        StatusText.Text = Loc.Get("Wizard.CreatingBackup");
        if (BackupRegistryCheck.IsChecked == true)
        {
            try
            {
                var paths = _programs.Select(p => p.RegistryPath).ToList();
                _backupFile = RegistryBackup.Backup(paths);
            }
            catch (Exception ex)
            {
                // O backup e um extra: uma falha aqui nao pode derrubar a desinstalacao.
                _backupFile = string.Empty;
                _backupError = ex.Message;
            }
        }

        if (RestorePointCheck.IsChecked == true)
        {
            StatusText.Text = Loc.Get("Wizard.CreatingRestorePoint");
            _restorePointCreated = UninstallEngine.TryCreateRestorePoint("Tranal Uninstaller Pro");
            _restorePointMessage = _restorePointCreated
                ? Loc.Get("Wizard.BackupPointSaved", "Tranal Uninstaller Pro")
                : Loc.Get("Wizard.RestorePointFailed");
        }
        else
        {
            _restorePointCreated = false;
            _restorePointMessage = Loc.Get("Wizard.RestorePointDisabled");
        }

        StatusText.Text = Loc.Get("Wizard.RunningBuiltIn");
        foreach (var program in _programs)
            _builtInResults.Add(await UninstallEngine.RunBuiltInUninstaller(program));
    }

    /// <summary>
    /// Etapa 3: varre as sobras DEPOIS do desinstalador nativo, no modo
    /// escolhido. No modo "Somente" nao ha varredura alguma.
    /// </summary>
    private async Task ScanAsync()
    {
        StatusText.Text = Loc.Get("Wizard.Scanning");

        var items = UninstallModes.Scans(_mode)
            ? await Task.Run(() => _programs
                .SelectMany(p => LeftoverScanner.Scan(p, BuildScanOptions()))
                .ToList())
            : new List<LeftoverItem>();

        _leftovers.Clear();
        foreach (var item in items) _leftovers.Add(item);

        // O Revo lista TUDO que sobrou relacionado ao programa. O conjunto
        // analisado antes da desinstalacao serve para dizer quantos desses
        // itens ja existiam antes (ou seja, o que o desinstalador nativo
        // deixou para tras).
        var before = new HashSet<string>(_analysis.Select(ItemKey), StringComparer.OrdinalIgnoreCase);
        _survivors = items.Count(i => before.Contains(ItemKey(i)));

        StatusText.Text = Loc.Get("Wizard.ItemsFound", _leftovers.Count);
    }

    /// <summary>
    /// Etapa 4: remove somente as sobras marcadas pelo usuario. As opcoes
    /// (backup automatico, Lixeira e adiamento para o proximo reinicio) vem
    /// das preferencias do usuario.
    /// </summary>
    private async Task RemoveAsync()
    {
        StatusText.Text = Loc.Get("Wizard.Removing");

        var name = string.Join(", ", _programs.Select(p => p.DisplayName));
        var options = _removalOptions;

        _report = await Task.Run(() =>
            UninstallEngine.RemoveLeftovers(_leftovers, options, _fullRegistryBackup));
        _summary = BuildSummary(_report);

        if (!AppSettingsStore.Current.DisableUninstallHistory)
        {
            UninstallHistory.Append(new UninstallHistoryEntry
            {
                Programs = name,
                Level = _mode.ToString(),
                BuiltIn = string.Join(" | ", _builtInResults.Select(r => r.Describe())),
                Report = _summary,
                Removed = _report.RemovedCount,
                Failed = _report.FailedCount + _report.ProtectedCount,
            });
        }

        // "Apagar o log do programa monitorado apos a desinstalacao": o log da
        // base de logs que corresponde aos programas desinstalados deixa de
        // existir quando a preferencia esta ligada.
        if (AppSettingsStore.Current.DeleteTracedLogAfterUninstall)
        {
            DeleteTracedLogsOf(name);
        }
    }

    /// <summary>
    /// Apaga os logs da base de logs cujo nome do programa casa com os
    /// programas desinstalados.
    /// </summary>
    private void DeleteTracedLogsOf(string programNames)
    {
        try
        {
            var names = programNames
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(n => n.Trim())
                .Where(n => n.Length > 0)
                .ToList();

            if (names.Count == 0) return;

            foreach (var install in TracedPrograms.List())
            {
                if (string.IsNullOrWhiteSpace(install.ProgramName)) continue;

                var matches = names.Any(n =>
                    string.Equals(n, install.ProgramName, StringComparison.OrdinalIgnoreCase));

                if (matches) TraceStore.Delete(install);
            }
        }
        catch { /* apagar log nunca pode derrubar a desinstalacao */ }
    }

    /// <summary>
    /// Resumo final: contagens por categoria, igual ao painel de resumo do
    /// Revo, mais o adiamento para o proximo reinicio, os itens enviados para
    /// a Lixeira e o ponto de restauracao do que foi removido.
    /// </summary>
    private string BuildSummary(RemovalReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Loc.Get("Wizard.SummaryTitle", string.Join(", ", _programs.Select(p => p.DisplayName))));
        sb.AppendLine();

        foreach (var result in _builtInResults)
            sb.AppendLine($"• {result.Describe()}");

        if (_restorePointMessage.Length > 0)
            sb.AppendLine($"• {_restorePointMessage}");

        sb.AppendLine($"• {Loc.Get("Wizard.Mode.Title")}: {UninstallModes.ShortName(_mode)}");
        sb.AppendLine($"• {Loc.Get("Wizard.ItemsFound", _leftovers.Count)}");

        sb.AppendLine();
        sb.AppendLine(report.Describe());
        sb.AppendLine();
        sb.AppendLine(Loc.Get("Wizard.SummaryTotal", report.Total));
        sb.AppendLine(Loc.Get("Wizard.SummaryRemoved", report.RemovedCount));
        sb.AppendLine(Loc.Get("Wizard.SummaryAbsent", report.AbsentCount));
        sb.AppendLine(Loc.Get("Wizard.SummaryFailed", report.FailedCount));
        sb.AppendLine(Loc.Get("Wizard.SummaryProtected", report.ProtectedCount));
        sb.AppendLine(Loc.Get("Wizard.SummaryDeferred", report.DeferredCount));
        sb.AppendLine(Loc.Get("Wizard.SummaryRecycleBin", CountRecycleBin(report)));

        if (report.RestorePointId is not null)
        {
            sb.AppendLine();
            sb.AppendLine(Loc.Get("Wizard.BackupPointSaved", report.RestorePointId));
            sb.AppendLine(RestorePointHint);
        }

        if (_backupFile.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine(Loc.Get("Wizard.BackupSaved", _backupFile));
            sb.AppendLine(Loc.Get("Wizard.BackupHint"));
        }
        else if (_backupError.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"• {_backupError}");
        }

        // O backup completo do registro nao e um item removido, entao nao
        // entra nas contagens: ele aparece aqui, com o caminho onde ficou.
        if (!string.IsNullOrWhiteSpace(report.FullRegistryBackupPath))
        {
            sb.AppendLine();
            sb.AppendLine(report.FullRegistryBackupOk
                ? Loc.Get("Wizard.FullRegistryBackupSaved", report.FullRegistryBackupPath)
                : Loc.Get("Wizard.FullRegistryBackupFailed", report.FullRegistryBackupPath));
        }

        // Processos que nao puderam ser encerrados: os arquivos deles
        // continuam em uso, e o relatorio de itens nao os mostra.
        var processosFalhos = report.CountOf("Processo", RemovalStatus.Failed);
        if (processosFalhos > 0)
        {
            sb.AppendLine();
            sb.AppendLine(Loc.Get("Wizard.ProcessesNotTerminated", processosFalhos));
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Quantos itens foram enviados para a Lixeira. O nucleo sinaliza a
    /// Lixeira pela mensagem do resultado (as chaves de registro nunca vao
    /// para ela).
    /// </summary>
    private static int CountRecycleBin(RemovalReport report) =>
        report.Outcomes.Count(o => o.Message.Contains("Lixeira", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Chave canonica de um item (tipo + caminho normalizado). E o que permite
    /// comparar o que existia antes da desinstalacao com o que existe agora.
    /// </summary>
    private static string ItemKey(LeftoverItem item) =>
        item.Kind + "|" + PathUtil.Normalize(item.Path);

    /// <summary>Marca todas as sobras da lista para remocao.</summary>
    private void SelectAllButton_Click(object sender, RoutedEventArgs e) => SetAllSelected(true);

    /// <summary>Desmarca todas as sobras da lista.</summary>
    private void DeselectAllButton_Click(object sender, RoutedEventArgs e) => SetAllSelected(false);

    /// <summary>Altera a marcacao de todos os itens da lista de sobras.</summary>
    private void SetAllSelected(bool selected)
    {
        foreach (var item in _leftovers)
            item.Selected = selected;

        // LeftoverItem nao avisa mudancas de propriedade: o refresh refaz as
        // ligacoes da lista para as caixas de marcacao acompanharem a escolha.
        LeftoversList.Items.Refresh();

        StatusText.Text = Loc.Get("Wizard.ItemsFound", _leftovers.Count);
    }

    /// <summary>
    /// Monta as colunas da lista de sobras. Os cabecalhos nao aceitam binding,
    /// por isso o texto vem do catalogo por codigo.
    /// </summary>
    private void BuildLeftoversView()
    {
        var grid = new GridView();

        grid.Columns.Add(new GridViewColumn
        {
            Header = string.Empty,
            Width = 34,
            CellTemplate = BuildCheckTemplate(),
        });

        grid.Columns.Add(new GridViewColumn
        {
            Header = Loc.Get("Common.Type"),
            Width = 90,
            DisplayMemberBinding = new Binding("Kind"),
        });

        grid.Columns.Add(new GridViewColumn
        {
            Header = Loc.Get("Common.Path"),
            Width = 430,
            CellTemplate = BuildBoldPathTemplate(),
        });

        grid.Columns.Add(new GridViewColumn
        {
            Header = Loc.Get("Common.Size"),
            Width = 90,
            DisplayMemberBinding = new Binding("Detail"),
        });

        grid.Columns.Add(new GridViewColumn
        {
            Header = Loc.Get("Common.Confidence"),
            Width = 90,
            DisplayMemberBinding = new Binding("Confidence") { Converter = new ConfidenceTextConverter() },
        });

        // A coluna do motivo: a confianca sozinha diz o quanto o Tranal
        // acredita no item, mas nao diz por que. O motivo acompanha sempre.
        grid.Columns.Add(new GridViewColumn
        {
            Header = Loc.Get("Common.Reason"),
            Width = 250,
            DisplayMemberBinding = new Binding("Reason") { Converter = new ReasonTextConverter() },
        });

        ApplyHeaderStyle(grid);
        LeftoversList.View = grid;
        BuildLeftoversContextMenu();
    }

    /// <summary>
    /// Menu de contexto da lista de sobras. A entrada "Adicionar a lista de
    /// exclusoes" protege o item contra qualquer remocao futura — e o
    /// equivalente do "Add to Exclude List" do original.
    /// </summary>
    private void BuildLeftoversContextMenu()
    {
        var menu = new ContextMenu();

        var addToExclude = new MenuItem { Header = Loc.Get("ContextMenu.AddToExcludeList") };
        AutomationProperties.SetAutomationId(addToExclude, "CtxAddToExcludeList");
        addToExclude.Click += (_, _) => AddSelectedToExcludeList();
        menu.Items.Add(addToExclude);

        LeftoversList.ContextMenu = menu;
    }

    /// <summary>
    /// Protege a sobra selecionada contra qualquer remocao. O item continua
    /// na lista desta execucao (a lista ja foi montada), mas a partir de
    /// agora ele e devolvido pela varredura como "nao encontrado" e o motor
    /// recusa remove-lo.
    /// </summary>
    private void AddSelectedToExcludeList()
    {
        if (LeftoversList.SelectedItem is not LeftoverItem item)
        {
            StatusText.Text = Loc.Get("ExcludeList.NothingSelected");
            return;
        }

        if (!ExcludeList.Current.AddItem(item))
        {
            StatusText.Text = Loc.Get("ExcludeList.AlreadyExcluded");
            return;
        }

        ExcludeList.Current.Save();
        StatusText.Text = Loc.Get("ContextMenu.AddedToExcludeList");
    }

    /// <summary>Aplica o cabecalho no estilo do tema (o padrao do WPF destoa).</summary>
    private static void ApplyHeaderStyle(GridView grid)
    {
        foreach (var column in grid.Columns)
        {
            var header = new GridViewColumnHeader { Content = column.Header, Tag = column };
            if (Application.Current.TryFindResource("ListViewHeaderStyle") is Style style)
                header.Style = style;
            column.Header = header;
        }
    }

    /// <summary>Template da coluna de marcacao: a caixa liga no Selected da sobra.</summary>
    private static DataTemplate BuildCheckTemplate()
    {
        var xaml =
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>"
            + "<CheckBox IsChecked='{Binding Selected, Mode=TwoWay}' VerticalAlignment='Center' />"
            + "</DataTemplate>";
        return (DataTemplate)XamlReader.Parse(xaml);
    }

    /// <summary>
    /// Template da coluna de caminho: os itens de confianca alta ficam em
    /// negrito, como no Revo (so eles vem marcados para remocao).
    /// </summary>
    private static DataTemplate BuildBoldPathTemplate()
    {
        var xaml =
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>"
            + "<TextBlock Text='{Binding Path}' TextTrimming='CharacterEllipsis' ToolTip='{Binding Path}'>"
            + "<TextBlock.Style>"
            + "<Style TargetType='TextBlock'>"
            + "<Setter Property='Foreground' Value='{DynamicResource TextBrush}' />"
            + "<Setter Property='FontFamily' Value='{DynamicResource UiFont}' />"
            + "<Setter Property='FontSize' Value='11' />"
            + "<Style.Triggers>"
            + "<DataTrigger Binding='{Binding IsHighConfidence}' Value='True'>"
            + "<Setter Property='FontWeight' Value='Bold' />"
            + "</DataTrigger>"
            + "</Style.Triggers>"
            + "</Style>"
            + "</TextBlock.Style>"
            + "</TextBlock>"
            + "</DataTemplate>";
        return (DataTemplate)XamlReader.Parse(xaml);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_step > 0) ShowStep(_step - 1);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
}

/// <summary>
/// Converte o nivel de confianca de uma sobra no texto localizado exibido na
/// coluna "Confianca" da lista de sobras do assistente.
/// </summary>
public sealed class ConfidenceTextConverter : IValueConverter
{
    /// <summary>Devolve "Alta", "Media" ou "Baixa" conforme a confianca do item.</summary>
    /// <param name="value">Valor da propriedade <c>Confidence</c> da sobra.</param>
    /// <param name="targetType">Tipo de destino da conversao.</param>
    /// <param name="parameter">Parametro do conversor, nao usado.</param>
    /// <param name="culture">Cultura do conversor, nao usada.</param>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is LeftoverConfidence confidence
            ? LeftoverLabels.Confidence(confidence)
            : string.Empty;

    /// <summary>Conversao de volta nao e suportada: a coluna e somente leitura.</summary>
    /// <param name="value">Valor de origem.</param>
    /// <param name="targetType">Tipo de destino da conversao.</param>
    /// <param name="parameter">Parametro do conversor, nao usado.</param>
    /// <param name="culture">Cultura do conversor, nao usada.</param>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>
/// Converte o motivo do julgamento de uma sobra no texto localizado exibido
/// na coluna "Por que" da lista de sobras do assistente.
///
/// O motivo e o que o Bulk Crap Uninstaller chama de "confidence part": la,
/// cada nivel de confianca vem acompanhado da razao que o produziu.
/// </summary>
public sealed class ReasonTextConverter : IValueConverter
{
    /// <summary>Devolve a frase que explica por que o item e sobra do programa.</summary>
    /// <param name="value">Valor da propriedade <c>Reason</c> da sobra.</param>
    /// <param name="targetType">Tipo de destino da conversao.</param>
    /// <param name="parameter">Parametro do conversor, nao usado.</param>
    /// <param name="culture">Cultura do conversor, nao usada.</param>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is LeftoverReason reason
            ? LeftoverLabels.Reason(reason)
            : string.Empty;

    /// <summary>Conversao de volta nao e suportada: a coluna e somente leitura.</summary>
    /// <param name="value">Valor de origem.</param>
    /// <param name="targetType">Tipo de destino da conversao.</param>
    /// <param name="parameter">Parametro do conversor, nao usado.</param>
    /// <param name="culture">Cultura do conversor, nao usada.</param>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
