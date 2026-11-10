using System.IO;
using Microsoft.Win32;
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
///
/// A pasta configurada mora no registro, e nao no arquivo de preferencias: o
/// arquivo de preferencias fica dentro da pasta de dados, entao uma
/// preferencia que mora junto dele nunca conseguiria mudar o lugar do
/// proprio arquivo. O registro e o unico lugar que pode dizer "os dados
/// estao em outro sitio" sem depender do lugar que ele mesmo ocupa.
///
/// A REGRA mora aqui, em um lugar so: quem guarda os dados do Tranal
/// (<see cref="RegistryBackup.DataDirectory"/>) consome esta classe.
/// </summary>
public static class PortableMode
{
    /// <summary>O caminho da chave do registro onde a pasta fica.</summary>
    private const string ChaveDoRegistro = @"Software\Tranal Group\Tranal Uninstaller Pro";

    /// <summary>O nome do valor do registro onde a pasta fica.</summary>
    private const string ValorDoRegistro = "DataFolder";

    /// <summary>
    /// Pasta de dados padrao do Tranal:
    /// %LOCALAPPDATA%\Tranal Group\Tranal Uninstaller Pro
    /// </summary>
    public static string DefaultDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Tranal Group", "Tranal Uninstaller Pro");

    /// <summary>
    /// A pasta de dados configurada pelo usuario. Vazia quando o Tranal usa a
    /// pasta padrao.
    /// </summary>
    public static string ConfiguredFolder => ReadConfiguredFolder();

    /// <summary>
    /// A pasta de dados efetiva: a configurada, quando ela pode ser usada;
    /// caso contrario, a pasta padrao.
    ///
    /// E esta a pasta que todo o Tranal usa: preferencias, logs, backups,
    /// categorias, votos e sessoes de desinstalacao.
    /// </summary>
    public static string EffectiveDataDirectory => ResolveDataDirectory(ConfiguredFolder);

    /// <summary>
    /// True quando existe uma pasta de dados personalizada em uso (o modo
    /// portavel esta ativo).
    /// </summary>
    public static bool IsPortableMode => IsPortable(ConfiguredFolder);

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
    /// Grava (ou apaga, quando a pasta vem vazia) a pasta de dados
    /// configurada.
    ///
    /// Devolve false quando o registro nao pode ser gravado: nesse caso o
    /// Tranal continua usando a pasta padrao, e a interface avisa.
    /// </summary>
    /// <param name="folder">A pasta configurada (vazia para voltar ao padrao).</param>
    public static bool SetConfiguredFolder(string? folder)
    {
        var valor = (folder ?? string.Empty).Trim();

        try
        {
            if (valor.Length == 0)
            {
                using var chave = Registry.CurrentUser.OpenSubKey(ChaveDoRegistro, writable: true);
                chave?.DeleteValue(ValorDoRegistro, throwOnMissingValue: false);
                return true;
            }

            using var nova = Registry.CurrentUser.CreateSubKey(ChaveDoRegistro, writable: true);
            if (nova is null) return false;

            nova.SetValue(ValorDoRegistro, valor, RegistryValueKind.String);
            return true;
        }
        catch
        {
            // O registro pode estar bloqueado por politica do Windows: sem a
            // pasta configurada, o Tranal segue na pasta padrao.
            return false;
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
    /// Le a pasta configurada do registro. Devolve vazia quando o registro
    /// nao tem o valor (ou quando ele nao pode ser lido).
    /// </summary>
    private static string ReadConfiguredFolder()
    {
        try
        {
            using var chave = Registry.CurrentUser.OpenSubKey(ChaveDoRegistro);
            var valor = chave?.GetValue(ValorDoRegistro) as string;
            return valor?.Trim() ?? string.Empty;
        }
        catch
        {
            // Sem registro legivel o Tranal fica na pasta padrao — que e o
            // comportamento de sempre, e nao uma falha.
            return string.Empty;
        }
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
