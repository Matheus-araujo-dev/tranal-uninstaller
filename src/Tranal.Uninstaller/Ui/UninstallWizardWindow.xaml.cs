using Tranal.Uninstaller.Core.Tools;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Ui.Automation;

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

    /// <summary>
    /// Os programas que ficaram de fora do lote por causa do teto de itens.
    ///
    /// Eles continuam instalados: sao adiados, nunca descartados em silencio.
    /// </summary>
    private readonly List<InstalledProgram> _deferred = new();

    /// <summary>
    /// Todos os programas instalados. E o que permite avisar que outro
    /// programa divide a pasta de instalacao — e o aviso que o original da
    /// antes de rodar o desinstalador.
    /// </summary>
    private readonly IReadOnlyList<InstalledProgram>? _allPrograms;

    private readonly bool _quick;
    private readonly ObservableCollection<LeftoverItem> _leftovers = new();
    private int _step;

    /// <summary>
    /// O painel de progresso da desinstalacao (modelo de nucleo).
    ///
    /// E ele quem traduz os tres comandos do usuario — pular, encerrar e rodar
    /// agora — em acoes do assistente. O modelo e o unico que sabe o que
    /// "pular" significa; a tela so mostra.
    /// </summary>
    private RemovalProgress? _progress;

    /// <summary>
    /// A janela do painel de progresso. E **sem dono modal**: o assistente
    /// continua vivo enquanto ela esta aberta, porque e o assistente que
    /// avanca as etapas e fecha a janela no fim.
    /// </summary>
    private AutomationWindow? _progressWindow;

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

    /// <summary>
    /// O log do instalador de cada programa, capturado antes do desinstalador
    /// nativo. E o que permite dizer que um item e sobra de verdade.
    /// </summary>
    private Dictionary<InstalledProgram, MsiInstallLog> _installLogs = new();
    private string _backupFile = string.Empty;
    private string _backupError = string.Empty;
    private bool _restorePointCreated;
    private string _restorePointMessage = string.Empty;
    private UninstallMode _mode = UninstallMode.Moderate;
    private int _survivors;
    private RemovalReport? _report;
    private string _summary = string.Empty;

    /// <summary>
    /// True quando o usuario mandou encerrar enquanto o desinstalador nativo
    /// rodava. O desinstalador nativo nao foi morto: ele terminou sozinho, e
    /// o que veio depois (varredura e remocao) nao aconteceu.
    /// </summary>
    private bool _abortedAfterNativeUninstaller;

    /// <summary>
    /// Caminho do log de remocao gravado nesta operacao. Vazio quando o log
    /// nao pôde ser gravado.
    /// </summary>
    private string _removalLogPath = string.Empty;

    /// <summary>
    /// O identificador do backup das sobras desta operacao. Vazio quando o
    /// backup nao foi feito (desligado nas preferencias ou sem sobras).
    /// </summary>
    private string _leftoverBackupId = string.Empty;

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
    /// <param name="allPrograms">
    /// Todos os programas instalados. E o que permite avisar que outro
    /// programa divide a pasta de instalacao. Pode ser nulo: sem a lista, o
    /// aviso de pasta compartilhada simplesmente nao existe.
    /// </param>
    public UninstallWizardWindow(
        List<InstalledProgram> programs, bool quick, IReadOnlyList<InstalledProgram>? allPrograms = null)
    {
        InitializeComponent();

        // O painel de progresso e uma janela a parte: se o assistente fechar
        // (cancelar, ou a janela principal fechar), a janela do painel vai
        // junto. Uma janela orfa na area de trabalho e pior do que nenhuma.
        Closed += (_, _) => CloseProgressPanel();

        // Window.Title e GridViewColumn.Header nao aceitam binding: texto via codigo.
        Title = Loc.Get("Wizard.Title");
        BuildLeftoversView();

        // A ordem inteligente e a do Revo: os desinstaladores que abrem janela
        // vao primeiro, enquanto o usuario ainda esta attentive; os
        // silenciosos depois, em lote.
        // O teto de lote mora na REGRA (ExecutionPolicy.ApplyBatchLimit): o
        // que passa do teto e adiado e nomeado, nunca descartado em silencio.
        var plan = ExecutionPolicy.ApplyBatchLimit(
            SmartOrdering.Order(programs).ToList(),
            AppSettingsStore.Current.BatchItemLimit);

        _programs = plan.Accepted.ToList();
        _deferred = plan.Deferred.ToList();
        _allPrograms = allPrograms;
        _quick = quick;
        StepsBar.ItemsSource = StepNames;
        LeftoversTreeControl.SetItems(_leftovers);

        ApplyPreferences();

        // Desmarcar a varredura em todas as contas desmarca tambem a
        // desinstalacao de todas as contas: as duas opcoes andam juntas.
        ScanAllAccountsCheck.Checked += (_, _) => SyncAccountChecks();
        ScanAllAccountsCheck.Unchecked += (_, _) => SyncAccountChecks();

        ShowStep(0);

        // O lote foi cortado no teto: o usuario precisa saber o que ficou de
        // fora, porque um programa que sai da fila sem aviso e um programa
        // que o usuario achou que foi removido.
        if (_deferred.Count > 0)
        {
            MessageBox.Show(this,
                Loc.Get("Wizard.BatchTruncated",
                    _deferred.Count,
                    AppSettingsStore.Current.BatchItemLimit,
                    string.Join(", ", _deferred.Select(p => p.DisplayName))),
                Loc.Get("Wizard.BatchTruncatedTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
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

        // A pergunta do Revo sobre as preferencias (15364): "Do you want to
        // keep your preferences? Icons, comments, groups". A opcao so aparece
        // quando ha mesmo alguma coisa para manter — perguntar sobre o nada
        // e ruido, e o usuario nao teria o que responder.
        var preferences = _programs
            .SelectMany(p => PreferenceRetention.ItemIds(p))
            .Distinct()
            .ToList();

        KeepPreferencesCheck.IsChecked = settings.KeepPreferencesOnUninstall;
        KeepPreferencesCheck.Visibility = preferences.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        KeepPreferencesNote.Text = Loc.Get("Wizard.KeepPreferencesNote",
            string.Join(", ", preferences.Select(PreferenceLabel)));
        KeepPreferencesNote.Visibility = preferences.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

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
    /// O rotulo traduzido de uma preferencia (comentario, icone, grupos).
    /// O identificador vem do nucleo; o texto vem do catalogo de idioma.
    /// </summary>
    private static string PreferenceLabel(string id) => id switch
    {
        "comment" => Loc.Get("Wizard.Preference.Comment"),
        "icon" => Loc.Get("Wizard.Preference.Icon"),
        "groups" => Loc.Get("Wizard.Preference.Groups"),
        _ => id,
    };

    /// <summary>
    /// Aplica a decisao sobre as preferencias do usuario.
    ///
    /// A regra mora no nucleo (<see cref="PreferenceRetention"/>): aqui so digo
    /// se o usuario quer manter. Roda logo depois do desinstalador nativo,
    /// que e quando o programa ja saiu do computador.
    /// </summary>
    private void ApplyPreferenceDecision()
    {
        var keep = _removalOptions.KeepPreferences;
        foreach (var program in _programs)
            PreferenceRetention.ApplyAfterUninstall(program, keep);
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
        NextButton.Content = Loc.Get(step == 4
            ? "Wizard.Finish"
            : (_quick ? "Wizard.QuickStart" : "Wizard.Next"));

        var name = string.Join(", ", _programs.Select(p => p.DisplayName));
        TitleText.Text = _programs.Count == 1
            ? Loc.Get("Wizard.UninstallTitle", name)
            : Loc.Get("Wizard.UninstallTitleMany", name, _programs.Count);

        switch (step)
        {
            case 0:
                SubtitleText.Text = Loc.Get("Wizard.StepCounter", 1, Loc.Get("Wizard.Step1.Title"));

                // No modo rapido nao ha escolha de modo nem etapas para
                // revisar: a mensagem diz o que vai acontecer.
                MessageText.Text = _quick
                    ? Loc.Get("Wizard.QuickIntro", name)
                    : Loc.Get("Wizard.AnalyzeIntro", name);

                ModePanel.Visibility = _quick ? Visibility.Collapsed : Visibility.Visible;
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
                OpenProgressPanel();
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
            // O modo rapido e o "confirmar e seguir" do IObit: uma
            // confirmacao e a cadeia inteira, sem etapa no meio. Ele so
            // comeca na primeira etapa — no fim, o botao e o "Concluir".
            if (_quick && _step == 0)
            {
                await RunQuickAsync();
                return;
            }

            switch (_step)
            {
                case 0:
                    await AnalyzeAsync();
                    if (!ConfirmPreUninstallAdvisories()) return;
                    ShowStep(1);
                    break;

                case 1:
                    await RunBuiltInAsync();
                    ApplyPreferenceDecision();
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
                    ApplyCompletion();
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
    /// O modo rapido: uma confirmacao e a cadeia inteira.
    ///
    /// E o "Quick Uninstall" do IObit — confirmar e seguir. A diferenca para
    /// o concorrente e que o Tranal tambem limpa o que sobrar, sem pedir que
    /// o usuario revise a lista item por item.
    ///
    /// O que sai e o que a REGRA decide
    /// (<see cref="ExecutionPolicy.ItensDoModoRapido"/>): as sobras marcadas,
    /// menos as que podem conter dado do usuario.
    /// </summary>
    private async Task RunQuickAsync()
    {
        NextButton.IsEnabled = false;
        BackButton.IsEnabled = false;

        await AnalyzeAsync();

        // A confirmacao e o "confirmar": sem ela, nada comeca.
        if (!ConfirmPreUninstallAdvisories())
        {
            StatusText.Text = Loc.Get("Wizard.Ready");
            NextButton.IsEnabled = true;
            return;
        }

        await RunBuiltInAsync();
        ApplyPreferenceDecision();

        await ScanAsync();

        // A regra do que sai mora no nucleo.
        var itens = ExecutionPolicy.ItensDoModoRapido(_leftovers);
        _leftovers.Clear();
        foreach (var item in itens) _leftovers.Add(item);
        LeftoversTreeControl.SetItems(_leftovers);

        await RemoveAsync();

        // O resumo fica na tela: e o resultado da operacao, e o usuario
        // precisa poder ler antes de a janela fechar. O "Concluir" fecha.
        NextButton.IsEnabled = true;
        ShowStep(4);
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

        // O log do instalador e capturado AQUI, antes do desinstalador nativo:
        // o pacote .msi sai do cache assim que a desinstalacao termina, e
        // depois disso nao ha mais o que ler. E ele que permite dizer que um
        // item e sobra de verdade, e nao apenas uma pasta de nome parecido.
        if (UninstallModes.Scans(_mode))
        {
            _installLogs = await Task.Run(() => _programs
                .Select(p => (Program: p, Log: MsiInstallLogReader.Capture(p)))
                .Where(x => x.Log is not null)
                .ToDictionary(x => x.Program, x => x.Log!));
        }

        _analysis = await Task.Run(() => _programs
            .SelectMany(p => LeftoverScanner.Scan(p, BuildScanOptions()))
            .ToList());

        StatusText.Text = Loc.Get("Wizard.Analyzed", _analysis.Count);
    }

    /// <summary>
    /// Os avisos que precedem a desinstalacao: outro programa divide a pasta
    /// de instalacao, o programa e um componente do sistema, o registro o
    /// marca como protegido, o desinstalador registrado esta ausente ou
    /// corrompido, e ha programas que costumam sair junto.
    ///
    /// A confirmacao NUNCA e omitida pelas preferencias de mensagem: e a
    /// pergunta que antecede a remocao, nao uma mensagem informativa. E o
    /// original faz a mesma coisa antes de rodar o desinstalador nativo.
    ///
    /// A regra mora no nucleo (<see cref="UninstallAdvisories.Build"/>): a
    /// tela nao monta aviso por conta propria.
    /// </summary>
    private bool ConfirmPreUninstallAdvisories()
    {
        // As guardas vem ANTES dos avisos: uma recusa (desinstalar o proprio
        // Tranal, ou ja ter uma desinstalacao em andamento) nao e um aviso que
        // o usuario pode aceitar — e uma operacao que nao deve comecar.
        foreach (var program in _programs)
        {
            var guarda = UninstallGuards.Check(program, _mode == UninstallMode.Advanced);
            if (guarda.Allowed) continue;

            UserMessages.Information(this, guarda.Reason, Loc.Get("Wizard.GuardTitle"));
            return false;
        }

        // O aviso de forcada antes da segura: a guarda diz que a operacao
        // pode seguir, mas pede um aviso. A interface mostrava a recusa e
        // ignorava o aviso — e o aviso e a parte que o usuario precisa ler.
        foreach (var program in _programs)
        {
            var guarda = UninstallGuards.Check(program, _mode == UninstallMode.Advanced);
            if (!guarda.RequiresWarning) continue;

            UserMessages.Information(this, guarda.Warning, Loc.Get("Guard.Title"));
            break;
        }

        // O aviso de desinstalacao silenciosa: um programa que tem
        // desinstalador mas nao aceita o modo silencioso vai abrir a
        // interface dele — e o usuario precisa saber disso antes de
        // confirmar.
        foreach (var program in _programs)
        {
            if (string.IsNullOrWhiteSpace(program.UninstallString)) continue;
            if (QuietUninstall.Supports(program)) continue;

            UserMessages.Information(this,
                string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    Loc.Get("Wizard.NoSilentUninstall"), program.DisplayName),
                Loc.Get("Guard.Title"));
            break;
        }

        var advisories = new List<UninstallAdvisory>();

        foreach (var program in _programs)
        {
            advisories.AddRange(UninstallAdvisories.Build(program, _allPrograms, null, _mode));
        }

        if (advisories.Count == 0) return true;

        var text = string.Join(
            Environment.NewLine + Environment.NewLine,
            advisories.Select(a => a.Title + Environment.NewLine + a.Message));

        return UserMessages.Confirm(this, text);
    }

    /// <summary>
    /// Mostra o aviso de pastas que podem conter dados do usuario e
    /// documentos. Ele fica na etapa da lista de sobras, que e onde o
    /// usuario revisa o que vai ser apagado.
    /// </summary>
    private void ShowLeftoverAdvisory()
    {
        // Os avisos sao montados no nucleo (LeftoverWarnings) e nao aqui: a
        // tela so mostra. O aviso de dado do usuario ja vem com um texto
        // mais detalhado, entao o aviso do nucleo nao e repetido.
        var advisory = UninstallAdvisories.UserDataAdvisory(_leftovers);
        var avisos = LeftoverWarnings.Build(_leftovers);

        var blocos = new List<string>();
        if (advisory is not null) blocos.Add(advisory.Message);

        foreach (var aviso in avisos)
        {
            if (advisory is not null && aviso.Kind == LeftoverWarningKind.UserData) continue;

            var texto = Loc.Get(aviso.MessageKey);

            if (aviso.Items.Count > 0)
            {
                texto += Environment.NewLine +
                    string.Join(Environment.NewLine, aviso.Items.Take(8).Select(p => "• " + p));

                if (aviso.Items.Count > 8)
                    texto += Environment.NewLine + $"... (+{aviso.Items.Count - 8})";
            }

            blocos.Add(texto);
        }

        if (blocos.Count == 0)
        {
            AdvisoryPanel.Visibility = Visibility.Collapsed;
            return;
        }

        AdvisoryTitleText.Text = advisory?.Title ?? Loc.Get("Wizard.LeftoverWarningTitle");
        AdvisoryText.Text = string.Join(Environment.NewLine + Environment.NewLine, blocos);
        AdvisoryPanel.Visibility = Visibility.Visible;
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
        _removalOptions = UninstallRemovalOptions.FromSettings(
            $"Desinstalacao de {name}",
            KeepPreferencesCheck.IsChecked == true);

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
        var silent = SilentUninstallCheck.IsChecked == true;

        // Quantas vezes vale tentar de novo o que falhou: a REGRA mora no
        // nucleo (ExecutionPolicy.ShouldRetry) e so o que FALHOU volta.
        var maximo = Math.Max(1, AppSettingsStore.Current.RetryFailedAttempts);

        foreach (var program in _programs)
        {
            var tentativa = 0;
            BuiltInUninstallResult resultado;

            while (true)
            {
                tentativa++;
                resultado = await UninstallEngine.RunBuiltInUninstaller(program, silent);

                var outcome = resultado.Uninstalled
                    ? UninstallQueueOutcome.Completed
                    : UninstallQueueOutcome.Failed;

                if (!ExecutionPolicy.ShouldRetry(outcome, tentativa, maximo))
                    break;
            }

            _builtInResults.Add(resultado);

            // O usuario mandou encerrar enquanto o desinstalador nativo
            // rodava. Ele NAO foi morto: matar o desinstalador no meio
            // deixa o programa meio desinstalado, que e pior do que
            // terminar a desinstalacao e parar depois. A operacao encerra
            // aqui, com o desinstalador nativo ja terminado.
            if (_progress?.StopRequested == true)
            {
                _abortedAfterNativeUninstaller = true;
                break;
            }
        }
    }

    /// <summary>
    /// Etapa 3: varre as sobras DEPOIS do desinstalador nativo, no modo
    /// escolhido. No modo "Somente" nao ha varredura alguma.
    /// </summary>
    private async Task ScanAsync()
    {
        StatusText.Text = Loc.Get("Wizard.Scanning");

        // "Encerrar" no painel de progresso aborta a operacao: o assistente
        // fecha e nada mais e removido.
        if (_progress?.StopRequested == true)
        {
            // O usuario mandou encerrar. Quando o desinstalador nativo estava
            // rodando, ele NAO foi morto: ele terminou e o que veio depois
            // nao aconteceu. O usuario precisa saber disso.
            if (_abortedAfterNativeUninstaller)
            {
                MessageBox.Show(this,
                    Loc.Get("Wizard.AbortedAfterNativeUninstaller"),
                    Loc.Get("Wizard.Aborted"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            StatusText.Text = Loc.Get("Wizard.Aborted");
            Close();
            return;
        }

        // "Pular" no painel de progresso pula a **varredura**: o que for
        // removido e o que a analise inicial ja conhecia, e nada mais. E a
        // acao mais segura que existe — remove menos, nunca mais.
        var skipScan = _progress?.ConsumeSkip() ?? false;
        if (skipScan)
            StatusText.Text = Loc.Get("Wizard.ScanSkipped");

        // O log do instalador so vale depois que o desinstalador nativo rodou
        // e deu certo: antes disso o programa continua instalado e o log
        // descreveria o programa inteiro como sobra.
        var uninstalled = _builtInResults.Count > 0 && _builtInResults.All(r => r.Uninstalled);

        var items = (!skipScan && UninstallModes.Scans(_mode))
            ? await Task.Run(() => _programs
                .SelectMany(p => LeftoverScanner.Scan(
                    p,
                    BuildScanOptions(),
                    MsiInstallLogReader.UsableAfter(
                        _installLogs.TryGetValue(p, out var log) ? log : null,
                        uninstalled)))
                .ToList())
            : new List<LeftoverItem>();

        // A pasta da atualizacao do Windows 11 (paridade com o Revo Pro):
        // ela nao e sobra do programa, e sobra do sistema. O Tranal pergunta
        // antes de colocar na lista.
        var pastasWin11 = Windows11ProblemFolders.KnownFolders();
        if (pastasWin11.Count > 0)
        {
            var resposta = MessageBox.Show(this,
                string.Format(
                    Loc.Get("Windows11.ProblemFolder.Question"),
                    string.Join(", ", pastasWin11.Select(p => p.Path))),
                Loc.Get("Windows11.ProblemFolder.Title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (resposta == MessageBoxResult.Yes)
            {
                foreach (var pasta in pastasWin11)
                {
                    items.Add(new LeftoverItem
                    {
                        Kind = "Pasta",
                        Path = pasta.Path,
                        Detail = pasta.Description,
                        Confidence = LeftoverConfidence.High,
                        Reason = LeftoverReason.WindowsUpdateFolder,
                        Selected = true,
                    });
                }
            }
        }
        _leftovers.Clear();
        foreach (var item in items) _leftovers.Add(item);
        LeftoversTreeControl.SetItems(_leftovers);

        // O Revo lista TUDO que sobrou relacionado ao programa. O conjunto
        // analisado antes da desinstalacao serve para dizer quantos desses
        // itens ja existiam antes (ou seja, o que o desinstalador nativo
        // deixou para tras).
        var before = new HashSet<string>(_analysis.Select(ItemKey), StringComparer.OrdinalIgnoreCase);
        _survivors = items.Count(i => before.Contains(ItemKey(i)));

        StatusText.Text = Loc.Get("Wizard.ItemsFound", _leftovers.Count);

        ShowLeftoverAdvisory();
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

        // "Rodar agora" no painel de progresso tira o adiamento para o
        // proximo reinicio: o que estiver em uso e tentado agora. As demais
        // opcoes permanecem as que o usuario escolheu.
        var options = _progress is not null
            ? _progress.ApplyRunNow(_removalOptions)
            : _removalOptions;

        // As sobras vao para o backup ANTES de sumirem: e o que permite devolver
        // o conteudo apagado ("Recuperar").
        var leftoverBackup = UninstallEngine.BackupLeftovers(_leftovers, name, options);
        _leftoverBackupId = leftoverBackup.Ok ? leftoverBackup.Id : string.Empty;

        _report = await Task.Run(() =>
            UninstallEngine.RemoveLeftovers(_leftovers, options, _fullRegistryBackup));
        _summary = BuildSummary(_report);

        // O painel de progresso cumpriu o seu papel: a janela fecha aqui.
        CloseProgressPanel();

        // O log de remocao por item: o que saiu, o que nao saiu e o codigo de
        // erro de cada um. E a resposta para "por que aquele arquivo nao foi
        // apagado" — e o Uninstall Tool grava o mesmo.
        try
        {
            // O log das mudancas detectadas pode ser desligado nas
            // preferencias (o "Don't use detected changes log" do Total
            // Uninstall). A REGRA esta na preferencia; aqui e so a consulta.
            if (AppSettingsStore.Current.KeepDetectedChangesLog)
            {
                _removalLogPath = RemovalLog.Save(_report, name);
                _summary += Environment.NewLine + Environment.NewLine
                    + Loc.Get("Wizard.RemovalLogSaved", _removalLogPath);
            }
        }
        catch (Exception ex)
        {
            _summary += Environment.NewLine + Environment.NewLine
                + Loc.Get("Wizard.RemovalLogFailed", ex.Message);
        }

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
                RecoveryId = _leftoverBackupId,
                RemovedItems = _report.Outcomes.Where(o => o.Removed).Select(o => o.Item.Path).ToList(),
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
    /// Abre o painel de progresso da desinstalacao.
    ///
    /// O painel mostra as etapas do plano e devolve os tres comandos do
    /// usuario — pular, encerrar e rodar agora. A janela e **sem dono
    /// modal**: o assistente continua vivo enquanto ela esta aberta, porque
    /// e o assistente que avanca as etapas e fecha a janela no fim.
    ///
    /// Na desinstalacao rapida o painel nao abre: ali o usuario pediu
    /// que a desinstalacao fosse direto, sem painel nenhum.
    /// </summary>
    private void OpenProgressPanel()
    {
        if (_quick || _programs.Count == 0) return;
        if (_progressWindow is not null) return;

        var plan = UninstallPlanBuilder.Build(_programs[0], _mode);

        _progress = new RemovalProgress();
        _progressWindow = new AutomationWindow(_progress, plan)
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        _progressWindow.Show();
    }

    /// <summary>
    /// Fecha o painel de progresso, se estiver aberto.
    /// </summary>
    private void CloseProgressPanel()
    {
        if (_progressWindow is null) return;

        _progressWindow.Close();
        _progressWindow = null;
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
    /// O que fazer quando a desinstalacao acaba: reiniciar a maquina ou
    /// fechar o aplicativo.
    ///
    /// A REGRA mora no nucleo (<see cref="ExecutionPolicy.Completion"/>): a
    /// tela pergunta ao usuario e executa o que a regra decidiu — ela nunca
    /// decide sozinha.
    /// </summary>
    private void ApplyCompletion()
    {
        var settings = AppSettingsStore.Current;
        var pendente = (_report?.DeferredCount ?? 0) > 0
            || _builtInResults.Any(r => r.NeedsRestart);

        var acao = ExecutionPolicy.Completion(settings, pendente);

        if (acao == CompletionAction.RestartMachine)
        {
            var resposta = MessageBox.Show(this,
                Loc.Get("Wizard.RestartQuestion", _report?.DeferredCount ?? 0),
                Loc.Get("Wizard.RestartTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            // Recusar o reinicio nao fecha o aplicativo: o usuario recusou
            // justamente a maquina voltar, e fechar a tela nao e a mesma
            // coisa.
            if (resposta == MessageBoxResult.Yes) SystemActions.AgentarReinicio();

            return;
        }

        if (acao == CompletionAction.CloseApplication)
        {
            Application.Current.Shutdown();
        }
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
        LeftoversTreeControl.Refresh();

        StatusText.Text = Loc.Get("Wizard.ItemsFound", _leftovers.Count);
    }

    /// <summary>
    /// Prepara a tela de sobras: o menu de contexto da arvore.
    ///
    /// A arvore mostra o caminho e o detalhe de cada sobra, e o agrupamento
    /// por categoria mora no nucleo (<see cref="LeftoverGroups"/>).
    /// </summary>
    private void BuildLeftoversView()
    {
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

        LeftoversTreeControl.ContextMenuSource = menu;
    }

    /// <summary>
    /// Protege a sobra selecionada contra qualquer remocao. O item continua
    /// na lista desta execucao (a lista ja foi montada), mas a partir de
    /// agora ele e devolvido pela varredura como "nao encontrado" e o motor
    /// recusa remove-lo.
    /// </summary>
    private void AddSelectedToExcludeList()
    {
        if (LeftoversTreeControl.SelectedItem is not LeftoverItem item)
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
