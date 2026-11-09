using System.IO;
using System.Text.Json;

namespace Tranal.Uninstaller.Core.Settings;

/// <summary>
/// As operacoes do Tranal que podem ter um perfil proprio.
/// </summary>
public enum OperationKind
{
    /// <summary>A varredura dos programas instalados.</summary>
    Scanning,

    /// <summary>A analise profunda de um programa.</summary>
    Analysis,

    /// <summary>A desinstalacao.</summary>
    Uninstall,

    /// <summary>A comparacao entre duas varreduras.</summary>
    Comparing,

    /// <summary>A restauracao do que foi removido.</summary>
    Restore,

    /// <summary>A limpeza das sobras.</summary>
    Cleaner,

    /// <summary>A tela de inicializacao automatica.</summary>
    Autorun,
}

/// <summary>
/// Um perfil de operacao: as preferencias que valem para uma operacao
/// especifica.
///
/// A ideia e do Total Uninstall: a varredura, a analise e a desinstalacao
/// podem ter coisas diferentes ligadas. Por exemplo, a varredura pode incluir
/// os componentes do sistema, e a desinstalacao nao.
///
/// Este e o **unico** lugar que sabe o que um perfil guarda. A tela e a
/// linha de comando usam este mesmo perfil.
/// </summary>
public sealed class OperationProfile
{
    /// <summary>A operacao.</summary>
    public OperationKind Kind { get; set; }

    /// <summary>Incluir os componentes do sistema.</summary>
    public bool IncludeSystemComponents { get; set; }

    /// <summary>Incluir os aplicativos empacotados (WindowsApps).</summary>
    public bool IncludeWindowsApps { get; set; }

    /// <summary>Verificar todas as sobras, sem perguntar.</summary>
    public bool CheckAllLeftovers { get; set; }

    /// <summary>Criar um ponto de restauracao antes de remover.</summary>
    public bool CreateRestorePoint { get; set; } = true;

    /// <summary>Guardar o log das mudancas detectadas.</summary>
    public bool KeepChangesLog { get; set; } = true;

    /// <summary>Parar o Tranal quando a desinstalacao terminar.</summary>
    public bool CloseWhenFinished { get; set; }

    /// <summary>
    /// As visoes de mudanca: o que a tela mostra depois de uma desinstalacao.
    /// </summary>
    public List<ChangeViewKind> ChangeViews { get; set; } = new();
}

/// <summary>
/// O que a tela de mudancas mostra.
/// </summary>
public enum ChangeViewKind
{
    /// <summary>Os arquivos que foram removidos.</summary>
    Files,

    /// <summary>As chaves de registro que foram removidas.</summary>
    Registry,

    /// <summary>As entradas do menu de contexto que foram removidas.</summary>
    ContextMenu,

    /// <summary>Os servicos que foram removidos.</summary>
    Services,

    /// <summary>As tarefas agendadas que foram removidas.</summary>
    ScheduledTasks,
}

/// <summary>
/// O arquivo dos perfis.
/// </summary>
internal sealed class ProfileStore
{
    public List<OperationProfile> Profiles { get; set; } = new();
}

/// <summary>
/// Os perfis por operacao e as visoes de mudanca.
///
/// Os dois ficam aqui porque sao a mesma coisa: o perfil e o conjunto de
/// preferencias de uma operacao, e a visao de mudanca e uma dessas
/// preferencias.
///
/// A regra esta em um lugar so: este modulo sabe o que um perfil guarda e
/// onde ele fica. A tela e a linha de comando consomem o mesmo perfil.
/// </summary>
public static class OperationProfiles
{
    /// <summary>
    /// Substitui a pasta de dados (so a suite de testes). Nulo em uso normal.
    /// </summary>
    internal static string? FolderOverride { get; set; }

    /// <summary>A pasta onde ficam os perfis.</summary>
    public static string Folder =>
        FolderOverride ?? Path.Combine(RegistryBackup.DataDirectory, "profiles");

    /// <summary>O arquivo dos perfis.</summary>
    public static string FilePath => Path.Combine(Folder, "profiles.json");

    /// <summary>
    /// O perfil de uma operacao. Quando o perfil nao existe, nasce um com os
    /// valores da preferencia global — e nao com valores inventados.
    /// </summary>
    /// <param name="kind">A operacao.</param>
    public static OperationProfile ProfileOf(OperationKind kind)
    {
        var guardado = Read().FirstOrDefault(p => p.Kind == kind);
        if (guardado is not null) return guardado;

        var settings = AppSettingsStore.Current;

        return new OperationProfile
        {
            Kind = kind,
            IncludeSystemComponents = settings.ShowSystemComponents,
            IncludeWindowsApps = settings.ShowWindowsApps,
            CheckAllLeftovers = settings.CheckAllLeftoversByDefault,
            CreateRestorePoint = settings.CreateRestorePointBeforeUninstall,
            KeepChangesLog = settings.KeepDetectedChangesLog,
        };
    }

    /// <summary>Todos os perfis guardados.</summary>
    public static IReadOnlyList<OperationProfile> List() => Read();

    /// <summary>
    /// Guarda o perfil de uma operacao.
    /// </summary>
    /// <param name="profile">O perfil.</param>
    public static bool Save(OperationProfile profile)
    {
        if (profile is null) return false;

        try
        {
            var perfis = Read().ToList();
            perfis.RemoveAll(p => p.Kind == profile.Kind);
            perfis.Add(profile);

            Directory.CreateDirectory(Folder);
            var json = JsonSerializer.Serialize(new ProfileStore { Profiles = perfis }, new JsonSerializerOptions
            {
                WriteIndented = true,
            });

            File.WriteAllText(FilePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// As visoes de mudanca ligadas de uma operacao.
    /// </summary>
    /// <param name="kind">A operacao.</param>
    public static IReadOnlyList<ChangeViewKind> ChangeViewsOf(OperationKind kind) =>
        ProfileOf(kind).ChangeViews;

    private static IReadOnlyList<OperationProfile> Read()
    {
        try
        {
            if (!File.Exists(FilePath)) return Array.Empty<OperationProfile>();

            var json = File.ReadAllText(FilePath);
            var loaded = JsonSerializer.Deserialize<ProfileStore>(json);
            return loaded?.Profiles ?? (IReadOnlyList<OperationProfile>)Array.Empty<OperationProfile>();
        }
        catch
        {
            return Array.Empty<OperationProfile>();
        }
    }
}
