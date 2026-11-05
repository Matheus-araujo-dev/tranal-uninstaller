using System.IO;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Settings;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Modo portavel do Tranal Uninstaller Pro.
///
/// Corresponde as opcoes "Usar pasta de armazenamento de dados personalizada" e
/// "Ativar modo de carregamento rapido" do Revo Uninstaller Pro.
///
/// Quando existe uma pasta configurada e utilizavel, todos os dados do Tranal
/// (preferencias, logs, backups e pontos de restauracao) ficam nela; sem
/// configuracao, os dados ficam em
/// <c>%LOCALAPPDATA%\Tranal Group\Tranal Uninstaller Pro</c>.
/// </summary>
public static class PortableMode
{
    /// <summary>
    /// Pasta de dados padrao do Tranal:
    /// %LOCALAPPDATA%\Tranal Group\Tranal Uninstaller Pro
    /// </summary>
    public static string DefaultDataDirectory => RegistryBackup.DataDirectory;

    /// <summary>
    /// True quando o modo de carregamento rapido esta ligado
    /// (<see cref="AppSettings.FastLoadingMode"/>).
    /// </summary>
    public static bool FastLoadingEnabled
    {
        get
        {
            try { return AppSettingsStore.Current.FastLoadingMode; }
            catch { return false; }
        }
    }

    /// <summary>
    /// Devolve a pasta de dados efetiva: a pasta configurada, quando ela e
    /// acessivel (ou pode ser criada); caso contrario, a pasta padrao.
    ///
    /// A pasta configurada nunca e devolvida sem verificacao: uma pasta
    /// inacessivel deixaria o aplicativo sem preferencias, sem logs e sem
    /// backups. Nesse caso o motivo fica registrado em
    /// <see cref="TryResolveDataDirectory"/>.
    /// </summary>
    public static string ResolveDataDirectory(string? configuredFolder)
    {
        TryResolveDataDirectory(configuredFolder, out var directory, out _);
        return directory;
    }

    /// <summary>
    /// True quando existe uma pasta de dados personalizada em uso (o modo
    /// portavel esta ativo). Uma pasta configurada mas inacessivel NAO conta:
    /// nesse caso o Tranal volta a usar a pasta padrao.
    /// </summary>
    public static bool IsPortable(string? configuredFolder)
    {
        if (string.IsNullOrWhiteSpace(configuredFolder)) return false;

        TryResolveDataDirectory(configuredFolder, out _, out var error);
        return error is null;
    }

    /// <summary>
    /// Resolve a pasta de dados informada e devolve tambem o motivo da falha
    /// (quando a pasta configurada nao pode ser usada).
    /// </summary>
    /// <param name="configuredFolder">Pasta configurada nas preferencias (pode ser vazia).</param>
    /// <param name="directory">Pasta de dados efetiva.</param>
    /// <param name="error">Texto do erro (<c>Portable.FolderInaccessible</c>) ou null.</param>
    public static bool TryResolveDataDirectory(
        string? configuredFolder, out string directory, out string? error)
    {
        directory = DefaultDataDirectory;
        error = null;

        if (string.IsNullOrWhiteSpace(configuredFolder))
        {
            EnsureDirectory(directory);
            return true;
        }

        var candidate = PathUtil.Normalize(configuredFolder);
        if (candidate.Length == 0)
        {
            error = Loc.Get("Portable.FolderInaccessible", configuredFolder, directory);
            EnsureDirectory(directory);
            return false;
        }

        if (EnsureDirectory(candidate))
        {
            directory = candidate;
            return true;
        }

        error = Loc.Get("Portable.FolderInaccessible", configuredFolder, directory);
        EnsureDirectory(directory);
        return false;
    }

    /// <summary>
    /// Resumo legivel do estado do modo portavel (usado no relatorio e na
    /// interface).
    /// </summary>
    public static string DescribePortableState(string? configuredFolder)
    {
        var sb = new System.Text.StringBuilder();

        var hasCustom = !string.IsNullOrWhiteSpace(configuredFolder);
        var resolved = TryResolveDataDirectory(configuredFolder, out var directory, out var error);

        sb.Append("Modo portátil: ").Append(hasCustom && resolved ? "SIM" : "NÃO");
        sb.AppendLine();
        sb.Append("Pasta de dados: ").AppendLine(directory);
        sb.Append("Carregamento rápido: ").AppendLine(FastLoadingEnabled ? "SIM" : "NÃO");

        if (error is not null)
            sb.AppendLine(error);

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Garante que a pasta existe (cria quando necessario). Devolve false
    /// quando a pasta nao pode ser usada.
    /// </summary>
    private static bool EnsureDirectory(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }
}
