using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tranal.Uninstaller.Core.Settings;

/// <summary>
/// Preferencias persistentes do Tranal Uninstaller Pro.
/// Gravadas em %LOCALAPPDATA%\Tranal Group\Tranal Uninstaller Pro\settings.json.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Tema ativo: "Dark", "Slate", "Contrast" ou "Light".</summary>
    public string Theme { get; set; } = "Dark";

    /// <summary>
    /// As cores personalizadas do usuario, por papel.
    ///
    /// O papel e o nome do enum <c>ThemeColorRole</c>; o valor e a cor em
    /// hexadecimal. Uma cor invalida e ignorada na hora de aplicar — um
    /// arquivo de preferencia corrompido nao pode deixar a tela sem cor.
    /// </summary>
    public Dictionary<string, string> CustomThemeColors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// O modo de visao de cores escolhido pelo usuario: "Standard",
    /// "Protanopia", "Deuteranopia" ou "Tritanopia".
    ///
    /// A paleta adaptada mora no nucleo
    /// (<c>Core/Appearance/ColorBlindPalette.cs</c>); aqui mora somente o
    /// nome do modo, para que o arquivo de preferencias continue legivel.
    /// </summary>
    public string ColorVisionMode { get; set; } = "Standard";

    /// <summary>
    /// Desativa as animacoes de texto da interface.
    ///
    /// Corresponde a opcao de desactivar as animacoes do Revo Uninstaller Pro.
    /// Padrao: <c>false</c>.
    /// </summary>
    public bool DisableTextAnimations { get; set; }

    /// <summary>
    /// Mostra a tela de inicializacao (splash) ao abrir o aplicativo.
    /// Padrao: <c>true</c>.
    /// </summary>
    public bool ShowSplashScreen { get; set; } = true;

    /// <summary>
    /// Oferece manter as preferencias do programa ao desinstalar.
    ///
    /// Corresponde a pergunta do Revo ("Do you want to keep your
    /// preferences? Icons, comments, groups"). Padrao: <c>true</c>.
    /// </summary>
    public bool KeepPreferencesOnUninstall { get; set; } = true;

    /// <summary>
    /// Foca a tela "Traced Programs" (Programas Monitorados) no arranque.
    ///
    /// Corresponde a opcao 15129 do Revo ("Set the focus on Traced Programs on
    /// startup"). Padrao: <c>true</c>.
    /// </summary>
    public bool FocusTracedProgramsOnStartup { get; set; } = true;

    /// <summary>
    /// Mostra as caixas de selecao na visualizacao de detalhes da lista.
    ///
    /// Corresponde a opcao 15334 do Revo ("Show check boxes in Details view").
    /// Padrao: <c>true</c>.
    /// </summary>
    public bool ShowCheckBoxesInDetailsView { get; set; } = true;

    /// <summary>Codigo de idioma ativo (ver <see cref="Localization.LanguageCatalog"/>).</summary>
    public string Language { get; set; } = "pt-BR";

    /// <summary>Exibe componentes do sistema (SystemComponent=1) na listagem.</summary>
    public bool ShowSystemComponents { get; set; }

    /// <summary>Exibe aplicativos do Windows (MSIX) na listagem de programas.</summary>
    public bool ShowWindowsApps { get; set; }

    /// <summary>Janela de "Novos Programas" em dias (medido: 7 no Revo).</summary>
    public int NewProgramsDays { get; set; } = 7;

    /// <summary>
    /// Colunas escondidas na lista de programas.
    ///
    /// Corresponde ao "Columns" (15054) e "Remove This Column" (15055) do
    /// Revo: o usuario escolhe quais colunas quer ver. A coluna do nome
    /// nunca some — sem ela a lista perde o sentido.
    /// </summary>
    public List<string> HiddenColumns { get; set; } = new();

    /// <summary>Ultimos programas desinstalados (menu de acesso rapido).</summary>
    public List<string> RecentPrograms { get; set; } = new();

    /// <summary>True depois que a escolha de tema do primeiro lancamento foi feita.</summary>
    public bool FirstRunCompleted { get; set; }

    /// <summary>
    /// Cria um ponto de restauracao do sistema antes de desinstalar.
    /// Corresponde a opcao "Criar um Ponto de Restauracao do Sistema antes
    /// da desinstalacao" do Revo Uninstaller Pro. Padrao: <c>true</c>.
    /// </summary>
    public bool CreateRestorePointBeforeUninstall { get; set; } = true;

    /// <summary>
    /// Executa somente o desinstalador nativo, sem varredura de sobras.
    /// Corresponde a opcao "Executar apenas o desinstalador interno" do Revo.
    /// Padrao: <c>false</c>.
    /// </summary>
    public bool OnlyRunBuiltInUninstaller { get; set; }

    /// <summary>
    /// Desativa o registro de historico de desinstalacoes.
    /// Corresponde a opcao "Desativar o recurso Historico de Desinstalacao".
    /// </summary>
    public bool DisableUninstallHistory { get; set; }

    /// <summary>
    /// Exibe atalhos de paginas web na listagem de programas.
    /// Corresponde a opcao "Mostrar atalhos de paginas web" do Revo.
    /// </summary>
    public bool ShowWebPageShortcuts { get; set; }

    /// <summary>
    /// Modo avancado: exibe opcoes e ferramentas que ficam ocultas por padrao.
    /// Corresponde a opcao "Modo avancado" do Revo.
    /// </summary>
    public bool AdvancedMode { get; set; }

    /// <summary>Verifica por atualizacoes ao iniciar. Padrao: <c>false</c>.</summary>
    public bool CheckForUpdateOnStartup { get; set; }

    /// <summary>Atualiza automaticamente em segundo plano. Padrao: <c>false</c>.</summary>
    public bool UpdateAutomatically { get; set; }

    /// <summary>
    /// Envia arquivos e pastas removidos para a Lixeira em vez de apaga-los
    /// de forma definitiva. Medido no Revo Pro: os arquivos vão para a Lixeira
    /// e as chaves de registro sao apagadas em definitivo. Padrao: <c>true</c>.
    /// </summary>
    public bool SendFilesToRecycleBin { get; set; } = true;

    /// <summary>
    /// Adia para o proximo reinicio os itens que estao em uso.
    /// Padrao: <c>true</c>.
    /// </summary>
    public bool DeferRemovalToNextRestart { get; set; } = true;

    /// <summary>
    /// Cria um backup automatico (ponto de restauracao) antes de remover itens.
    /// Padrao: <c>true</c>.
    /// </summary>
    public bool BackupBeforeRemoval { get; set; } = true;

    /// <summary>
    /// Pasta de dados propria do aplicativo (modo portatil). Vazia quando o
    /// Tranal usa a pasta padrao em %LOCALAPPDATA%.
    /// Corresponde a opcao "Usar pasta de armazenamento de dados personalizada".
    /// </summary>
    public string PortableDataFolder { get; set; } = string.Empty;

    /// <summary>
    /// Modo de carregamento rapido: pula etapas de inicializacao nao essenciais.
    /// Corresponde a opcao "Ativar modo de carregamento rapido" do Revo.
    /// </summary>
    public bool FastLoadingMode { get; set; }

    /// <summary>
    /// Mecanismo de busca usado quando o usuario pesquisa um programa que
    /// ainda nao esta instalado. Vazio significa busca na web padrao.
    ///
    /// Este e o valor **antigo** da preferencia: ele continua valendo e e
    /// lido quando a lista nova (<see cref="WebSearchEngines"/>) esta vazia.
    /// </summary>
    public string WebSearchEngine { get; set; } = string.Empty;

    /// <summary>
    /// Os motores de busca configurados, na ordem em que aparecem.
    ///
    /// Cada item e o identificador de um motor do catalogo ("Google",
    /// "Bing", "DuckDuckGo", "Yandex", "Yahoo") ou a definicao de um motor
    /// personalizado ("MeuBusca|https://...?q={0}").
    ///
    /// O **primeiro** da lista e o motor usado na busca. Lista vazia
    /// significa "usa o valor antigo" (<see cref="WebSearchEngine"/>).
    /// </summary>
    public List<string> WebSearchEngines { get; set; } = new();

    /// <summary>
    /// Exporta a lista de programas pelo Excel instalado (automacao COM),
    /// em vez do formato SpreadsheetML (XML).
    ///
    /// O SpreadsheetML funciona sem o Office instalado; o Excel instalado
    /// grava um .xlsx de verdade. Padrao: <c>false</c> — a exportacao nao
    /// pode depender de um programa que talvez nao esteja na maquina.
    /// </summary>
    public bool ExportToExcelInstalled { get; set; }

    /// <summary>
    /// Marca todos os itens de sobra por padrao, inclusive os de confianca
    /// media e baixa. Corresponde a opcao "Marcar todos os itens restantes por
    /// padrao" do Revo Uninstaller Pro.
    ///
    /// Padrao: <c>false</c> — como no Revo, so a confianca alta vem marcada.
    /// A confianca de cada item continua sendo exibida: apenas a marcacao
    /// inicial muda.
    /// </summary>
    public bool CheckAllLeftoversByDefault { get; set; }

    /// <summary>
    /// Nao exibe as mensagens de informacao do aplicativo.
    /// Corresponde a opcao "Ocultar mensagens de informacao" do Revo.
    /// </summary>
    public bool SkipInformationMessages { get; set; }

    /// <summary>
    /// Nao exibe as mensagens de aviso do aplicativo.
    /// Corresponde a opcao "Ocultar mensagens de aviso" do Revo.
    /// </summary>
    public bool SkipWarningMessages { get; set; }

    /// <summary>Mostra o cabecalho principal da janela. Padrao: <c>true</c>.</summary>
    public bool ShowMainToolbar { get; set; } = true;

    /// <summary>Mostra o painel de descricao. Padrao: <c>true</c>.</summary>
    public bool ShowDescriptionPanel { get; set; } = true;

    /// <summary>Mostra a barra de status. Padrao: <c>true</c>.</summary>
    public bool ShowStatusBar { get; set; } = true;

    /// <summary>Mostra o grupo de ferramentas na barra lateral. Padrao: <c>true</c>.</summary>
    public bool ShowToolsToolbar { get; set; } = true;

    /// <summary>
    /// Mostra os comandos do Tranal no menu de contexto do Windows.
    /// Corresponde a opcao "Mostrar comandos no menu de contexto do Windows"
    /// do Revo.
    ///
    /// Padrao: <c>false</c> — registrar uma entrada para todos os arquivos do
    /// Explorer e uma mudanca visivel no ambiente do usuario: ela so acontece
    /// quando o usuario liga a opcao.
    /// </summary>
    public bool ShowContextMenuCommands { get; set; }

    /// <summary>
    /// Apaga o registro do programa monitorado depois da desinstalacao.
    /// Corresponde a opcao "Apagar o log do programa monitorado apos a
    /// desinstalacao" do Revo.
    /// </summary>
    public bool DeleteTracedLogAfterUninstall { get; set; }

    /// <summary>
    /// Exibe a opcao de procurar sobras em todas as contas do Windows na tela
    /// de desinstalacao. Corresponde a opcao "Show the option to scan all
    /// Windows accounts for leftovers" do Revo.
    ///
    /// Padrao: <c>false</c> — como no Revo, a opcao existe mas fica oculta
    /// ate o usuario ligar este interruptor nas Opcoes.
    /// </summary>
    public bool ShowScanAllAccountsOption { get; set; }

    /// <summary>
    /// Procura sobras nas outras contas do Windows (chaves em HKEY_USERS e
    /// pastas dos outros perfis). Corresponde a opcao "Scan all Windows
    /// accounts for leftovers" do Revo.
    /// </summary>
    public bool ScanAllWindowsAccounts { get; set; }

    /// <summary>
    /// Deixa marcados para remocao os itens que pertencem a outras contas do
    /// Windows. Corresponde a opcao "Uninstall from all Windows accounts" do
    /// Revo.
    ///
    /// Sem esta opcao, a varredura em todas as contas e apenas informativa:
    /// os itens das outras contas aparecem na lista, mas nao vem marcados.
    /// </summary>
    public bool UninstallFromAllWindowsAccounts { get; set; }

    /// <summary>
    /// Encerra os executaveis do programa que esta sendo removido antes de
    /// apagar os arquivos deles. Corresponde a opcao "Forcibly stop running
    /// executable files during their uninstall" do Revo.
    /// </summary>
    public bool ForciblyStopRunningExecutables { get; set; }

    /// <summary>
    /// Cria um backup completo do registro (todos os hives) antes de remover
    /// as sobras. Corresponde a opcao "Create a full Registry Backup before
    /// uninstall" do Revo.
    /// </summary>
    public bool CreateFullRegistryBackupBeforeUninstall { get; set; }

    /// <summary>
    /// Endereco do feed de atualizacoes do proprio Tranal. Vazio usa o
    /// endereco padrao (<see cref="Updates.UpdateService.FeedDoTranalPadrao"/>).
    /// </summary>
    public string UpdateFeedUrl { get; set; } = string.Empty;

    /// <summary>
    /// Endereco do feed de atualizacoes dos programas instalados. Vazio usa o
    /// endereco padrao (<see cref="Updates.UpdateService.FeedDeProgramasPadrao"/>).
    /// </summary>
    public string SoftwareUpdateFeedUrl { get; set; } = string.Empty;

    /// <summary>
    /// A lista negra de atualizacoes: chaves do feed ou nomes de programa que
    /// nunca podem ser atualizados.
    /// </summary>
    public List<string> UpdateBlacklist { get; set; } = new();

    /// <summary>
    /// Teto de itens de um lote de desinstalacao (0 = sem teto).
    ///
    /// Medido no Uninstall Tool: um lote nao roda o programa inteiro de uma
    /// vez — ele avisa e corta. O corte e sempre do fim da fila, e o que
    /// ficou de fora e mostrado ao usuario.
    /// </summary>
    public int BatchItemLimit { get; set; }

    /// <summary>
    /// Numero maximo de tentativas para um silencioso que falhou (1 = sem
    /// repeticao). Medido no BCU ("tentar novamente").
    /// </summary>
    public int RetryFailedAttempts { get; set; } = 1;

    /// <summary>
    /// Reinicia a maquina quando a desinstalacao termina, para concluir o
    /// que ficou pendente para o proximo reinicio.
    ///
    /// Medido no Uninstalr ("reiniciar automaticamente para finalizar").
    /// So acontece quando sobrou item pendente: sem pendencia, reiniciar nao
    /// adianta.
    /// </summary>
    public bool RestartWhenFinished { get; set; }

    /// <summary>
    /// Fecha o proprio aplicativo quando a desinstalacao termina.
    /// Medido no Uninstalr ("fechar o proprio aplicativo ao terminar").
    /// </summary>
    public bool CloseAppWhenFinished { get; set; }

    /// <summary>
    /// Data e hora da ultima varredura da lista de programas.
    ///
    /// E o "registro de ultima analise" do Wise: saber quando a maquina foi
    /// vista pela ultima vez e o que foi encontrado. Nulo = nunca varreu.
    /// </summary>
    public DateTime? LastScanAt { get; set; }

    /// <summary>
    /// Quantos programas a ultima varredura encontrou.
    /// </summary>
    public int LastScanCount { get; set; }

    /// <summary>
    /// O modo de execucao: como o Tranal age quando a desinstalacao comeca.
    ///
    /// "system" (o padrao), "always", "never" ou "ask". Medido no
    /// Uninstall Tool 3.8.2 ("sempre rodar / nunca rodar / perguntar").
    ///
    /// O valor e texto, e nao enumerado: e assim que a preferencia gravada
    /// continua legivel no arquivo e nao depende da ordem do enumerado. A
    /// regra de leitura esta em
    /// <see cref="ExecutionPreferences.Parse"/>.
    /// </summary>
    public string RunPreference { get; set; } = "system";

    /// <summary>
    /// O modo de exibicao: "system", "always" ou "never".
    ///
    /// Medido no Uninstalr ("modo de exibicao do sistema"). O padrao e o
    /// sistema: o Tranal nao muda o que o Windows decide.
    /// </summary>
    public string DisplayPreference { get; set; } = "system";

    /// <summary>
    /// As familias de arquivo que a remocao PRESERVA (o filtro de tipo).
    ///
    /// Vazio = preserva nada, que e o comportamento de sempre. Os nomes sao
    /// os do enumerado <see cref="Automation.RemovalFileKind"/>.
    /// </summary>
    public List<string> PreservedFileKinds { get; set; } = new();

    /// <summary>
    /// Preserva os arquivos que estao em pastas pessoais do usuario.
    ///
    /// E a regra que impede que uma desinstalacao apague um documento, uma
    /// foto ou um video do usuario sem que ele saiba. Padrao: <c>true</c>.
    /// </summary>
    public bool ProtectPersonalFiles { get; set; } = true;

    /// <summary>
    /// Comprime os arquivos guardados no backup com Brotli.
    ///
    /// O BCU usa Brotli nos backups que gera. O ganho e real em arquivos de
    /// texto e de configuracao, que e o que sobra de programa. Padrao:
    /// <c>true</c>.
    /// </summary>
    public bool CompressBackups { get; set; } = true;

    /// <summary>
    /// Quantos snapshots a pasta de backups guarda.
    ///
    /// O limite existe porque a pasta cresce sem parar: sem ele, o backup
    /// que existe para devolver as sobras acaba sendo o que enche o disco.
    /// Padrao: 20.
    /// </summary>
    public int MaxSnapshotCount { get; set; } = 20;

    /// <summary>
    /// Quantos megabytes a pasta de backups pode ocupar. Padrao: 512.
    /// </summary>
    public int MaxSnapshotSizeMb { get; set; } = 512;

    /// <summary>
    /// Mantem o log das mudancas detectadas na desinstalacao.
    ///
    /// E o registro do que a desinstalacao encontrou e removeu. O Total
    /// Uninstall tem a opcao "Don't use detected changes log". Padrao:
    /// <c>true</c>.
    /// </summary>
    public bool KeepDetectedChangesLog { get; set; } = true;

    /// <summary>
    /// Mostra o icone do Tranal na bandeja do sistema.
    ///
    /// A bandeja e a forma de o Tranal continuar vivo sem ocupar a tela.
    /// Padrao: <c>true</c>.
    /// </summary>
    public bool ShowTrayIcon { get; set; } = true;

    /// <summary>
    /// Comeca minimizado na bandeja. Sem esta preferencia, o Tranal sempre
    /// abre a janela na tela. Padrao: <c>false</c>.
    /// </summary>
    public bool StartMinimizedToTray { get; set; }

    /// <summary>
    /// Minimiza na bandeja enquanto a desinstalacao roda: e o que atrapalha
    /// menos o proprio desinstalador. Padrao: <c>true</c>.
    /// </summary>
    public bool MinimizeToTrayDuringUninstall { get; set; } = true;

    /// <summary>
    /// Um clique no icone da bandeja devolve a janela para a tela.
    /// Padrao: <c>true</c>.
    /// </summary>
    public bool RestoreTrayOnClick { get; set; } = true;

    /// <summary>
    /// A lista de programas em modo compacto: so as colunas essenciais.
    /// Padrao: <c>false</c>.
    /// </summary>
    public bool CompactProgramList { get; set; }

    /// <summary>
    /// Avisa quando aparece um programa novo. Padrao: <c>true</c>.
    /// </summary>
    public bool ShowNewProgramsPopup { get; set; } = true;

    /// <summary>
    /// Liga o agente de fundo: um processo leve que fica de olho na maquina
    /// e avisa quando algo muda. Padrao: <c>false</c>.
    /// </summary>
    public bool BackgroundAgent { get; set; }

    /// <summary>O modo de execucao, ja lido pela regra.</summary>
    public ExecutionPreference RunMode => ExecutionPreferences.Parse(RunPreference);

    /// <summary>O modo de exibicao, ja lido pela regra.</summary>
    public ExecutionPreference DisplayMode => ExecutionPreferences.Parse(DisplayPreference);
}

/// <summary>Leitura e gravacao das <see cref="AppSettings"/>.</summary>
public static class AppSettingsStore
{
    /// <summary>
    /// Caminho alternativo do arquivo de preferencias. Existe para os testes:
    /// sem ele, um teste que grava preferencias escreveria no arquivo real do
    /// usuario e perderia o estado se o processo morresse no meio.
    /// Nulo em uso normal.
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    public static string FilePath =>
        FilePathOverride ?? Path.Combine(RegistryBackup.DataDirectory, "settings.json");

    private static AppSettings? _current;

    /// <summary>Preferencias em memoria (carregadas uma unica vez).</summary>
    public static AppSettings Current => _current ??= Load();

    /// <summary>Dispara quando as preferencias mudam em disco.</summary>
    public static event EventHandler? Saved;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded is not null) return Normalize(loaded);
            }
        }
        catch { /* preferencias corrompidas nao podem impedir a execucao */ }

        return new AppSettings();
    }

    /// <summary>Grava as preferencias em disco (best effort).</summary>
    public static void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });

            File.WriteAllText(FilePath, json);
            _current = settings;
            Saved?.Invoke(null, EventArgs.Empty);
        }
        catch { }
    }

    /// <summary>Salva alteracoes pontuais preservando o resto do arquivo.</summary>
    public static void Update(Action<AppSettings> mutate)
    {
        var settings = Current;
        mutate(settings);
        Save(settings);
    }

    /// <summary>
    /// Registra que a maquina foi varrida agora, e quantos programas foram
    /// encontrados.
    ///
    /// E o "registro de ultima analise" do Wise: a interface grafica e a
    /// linha de comando gravam por este mesmo metodo, para que o historico
    /// diga a verdade independente de quem varreu.
    /// </summary>
    /// <param name="count">Quantos programas foram encontrados.</param>
    public static void RecordScan(int count)
    {
        Update(s =>
        {
            s.LastScanAt = DateTime.Now;
            s.LastScanCount = count < 0 ? 0 : count;
        });
    }

    /// <summary>
    /// Completa as preferencias com os valores padrao das chaves que
    /// faltam ou chegaram vazias.
    ///
    /// E a **unica** regra de normalizacao: quem carrega do disco
    /// (<see cref="Load"/>) e quem importa de um arquivo
    /// (<see cref="SettingsTransfer.Import"/>) usam esta mesma funcao, para
    /// que um arquivo importado e um arquivo gravado nunca divirjam.
    /// </summary>
    internal static AppSettings Normalize(AppSettings settings)
    {
        if (settings.NewProgramsDays <= 0) settings.NewProgramsDays = 7;
        if (string.IsNullOrWhiteSpace(settings.Theme)) settings.Theme = "Dark";
        if (string.IsNullOrWhiteSpace(settings.Language)) settings.Language = "pt-BR";
        if (string.IsNullOrWhiteSpace(settings.ColorVisionMode)) settings.ColorVisionMode = "Standard";
        settings.RecentPrograms ??= new List<string>();
        settings.PortableDataFolder ??= string.Empty;
        settings.WebSearchEngine ??= string.Empty;
        settings.WebSearchEngines ??= new List<string>();
        if (settings.BatchItemLimit < 0) settings.BatchItemLimit = 0;
        if (settings.RetryFailedAttempts < 1) settings.RetryFailedAttempts = 1;

        // Os modos passam pela regra: um texto desconhecido (ou de uma
        // versao futura) vira o padrao do sistema, e nao um valor invalido.
        settings.RunPreference = ExecutionPreferences.Normalize(settings.RunPreference);
        settings.DisplayPreference = ExecutionPreferences.Normalize(settings.DisplayPreference);
        settings.PreservedFileKinds ??= new List<string>();

        return settings;
    }
}

/// <summary>Formato do arquivo de preferencias em disco (documentacao em disco).</summary>
public static class SettingsFileFormat
{
    public static string Describe() =>
        "settings.json (JSON UTF-8): theme, language, showSystemComponents, "
        + "colorVisionMode, "
        + "showWindowsApps, newProgramsDays, hiddenColumns[], recentPrograms[], "
        + "firstRunCompleted, "
        + "createRestorePointBeforeUninstall, onlyRunBuiltInUninstaller, disableUninstallHistory, "
        + "showWebPageShortcuts, advancedMode, checkForUpdateOnStartup, updateAutomatically, "
        + "sendFilesToRecycleBin, deferRemovalToNextRestart, backupBeforeRemoval, "
        + "portableDataFolder, fastLoadingMode, webSearchEngine, webSearchEngines[], "
        + "exportToExcelInstalled, "
        + "checkAllLeftoversByDefault, skipInformationMessages, skipWarningMessages, "
        + "showMainToolbar, showDescriptionPanel, showStatusBar, showToolsToolbar, "
        + "showContextMenuCommands, deleteTracedLogAfterUninstall, "
        + "showScanAllAccountsOption, scanAllWindowsAccounts, uninstallFromAllWindowsAccounts, "
        + "forciblyStopRunningExecutables, createFullRegistryBackupBeforeUninstall, "
        + "batchItemLimit, retryFailedAttempts, restartWhenFinished, closeAppWhenFinished, "
        + "lastScanAt, lastScanCount, "
        + "runPreference, displayPreference, preservedFileKinds[]";
}
