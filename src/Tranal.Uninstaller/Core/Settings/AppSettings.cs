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
    /// </summary>
    public string WebSearchEngine { get; set; } = string.Empty;

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

    private static AppSettings Normalize(AppSettings settings)
    {
        if (settings.NewProgramsDays <= 0) settings.NewProgramsDays = 7;
        if (string.IsNullOrWhiteSpace(settings.Theme)) settings.Theme = "Dark";
        if (string.IsNullOrWhiteSpace(settings.Language)) settings.Language = "pt-BR";
        settings.RecentPrograms ??= new List<string>();
        settings.PortableDataFolder ??= string.Empty;
        settings.WebSearchEngine ??= string.Empty;
        return settings;
    }
}

/// <summary>Formato do arquivo de preferencias em disco (documentacao em disco).</summary>
public static class SettingsFileFormat
{
    public static string Describe() =>
        "settings.json (JSON UTF-8): theme, language, showSystemComponents, "
        + "showWindowsApps, newProgramsDays, hiddenColumns[], recentPrograms[], "
        + "firstRunCompleted, "
        + "createRestorePointBeforeUninstall, onlyRunBuiltInUninstaller, disableUninstallHistory, "
        + "showWebPageShortcuts, advancedMode, checkForUpdateOnStartup, updateAutomatically, "
        + "sendFilesToRecycleBin, deferRemovalToNextRestart, backupBeforeRemoval, "
        + "portableDataFolder, fastLoadingMode, webSearchEngine, "
        + "checkAllLeftoversByDefault, skipInformationMessages, skipWarningMessages, "
        + "showMainToolbar, showDescriptionPanel, showStatusBar, showToolsToolbar, "
        + "showContextMenuCommands, deleteTracedLogAfterUninstall, "
        + "showScanAllAccountsOption, scanAllWindowsAccounts, uninstallFromAllWindowsAccounts, "
        + "forciblyStopRunningExecutables, createFullRegistryBackupBeforeUninstall";
}
