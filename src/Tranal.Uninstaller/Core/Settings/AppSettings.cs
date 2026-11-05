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
    /// <summary>Tema ativo: "Dark" ou "Light".</summary>
    public string Theme { get; set; } = "Dark";

    /// <summary>Codigo de idioma ativo (ver <see cref="Localization.LanguageCatalog"/>).</summary>
    public string Language { get; set; } = "pt-BR";

    /// <summary>Exibe componentes do sistema (SystemComponent=1) na listagem.</summary>
    public bool ShowSystemComponents { get; set; }

    /// <summary>Exibe aplicativos do Windows (MSIX) na listagem de programas.</summary>
    public bool ShowWindowsApps { get; set; }

    /// <summary>Janela de "Novos Programas" em dias (medido: 7 no Revo).</summary>
    public int NewProgramsDays { get; set; } = 7;

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
        + "showWindowsApps, newProgramsDays, recentPrograms[], firstRunCompleted, "
        + "createRestorePointBeforeUninstall, onlyRunBuiltInUninstaller, disableUninstallHistory, "
        + "showWebPageShortcuts, advancedMode, checkForUpdateOnStartup, updateAutomatically, "
        + "sendFilesToRecycleBin, deferRemovalToNextRestart, backupBeforeRemoval, "
        + "portableDataFolder, fastLoadingMode, webSearchEngine";
}
