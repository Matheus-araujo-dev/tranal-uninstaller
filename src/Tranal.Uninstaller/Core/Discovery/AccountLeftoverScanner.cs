using System.IO;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// O tipo do item de sobra encontrado dentro de uma conta do Windows.
/// </summary>
public enum AccountItemKind
{
    /// <summary>Pasta.</summary>
    Folder,

    /// <summary>Arquivo.</summary>
    File,

    /// <summary>Chave de registro da conta.</summary>
    RegistryKey,
}

/// <summary>
/// Opcoes da varredura de sobras por conta do Windows.
///
/// Correspondem as opcoes do Revo Uninstaller Pro 15345 ("Show the option to
/// scan all Windows accounts for leftovers"), 15352 ("Scan all Windows
/// accounts for leftovers") e 15353 ("Uninstall from all Windows accounts").
/// </summary>
public sealed record AccountLeftoverScanOptions
{
    /// <summary>
    /// Inclui a conta que esta usando o Tranal na varredura. Por padrao a
    /// varredura multi-conta cuida das OUTRAS contas: a conta atual ja e
    /// coberta pela varredura normal do programa.
    /// </summary>
    public bool IncludeCurrentAccount { get; init; }

    /// <summary>
    /// Deixa marcados para remocao os itens que pertencem a outras contas.
    /// Sem esta opcao, a varredura em todas as contas e apenas informativa.
    /// Corresponde a opcao "Uninstall from all Windows accounts" do Revo.
    /// </summary>
    public bool SelectAllAccounts { get; init; }

    /// <summary>
    /// Quando <c>true</c> (padrao), so as contas com o hive carregado em
    /// HKEY_USERS sao varridas no registro: as chaves das contas com o hive
    /// desmontado nao existem de fato no sistema.
    /// </summary>
    public bool RequireLoadedHive { get; init; } = true;

    /// <summary>Opcoes padrao: varredura multi-conta, sem marcar para remocao.</summary>
    public static AccountLeftoverScanOptions Default { get; } = new();
}

/// <summary>
/// Uma sobra encontrada dentro de uma conta do Windows.
/// </summary>
public sealed class AccountLeftover
{
    /// <summary>
    /// A conta dona do item. <c>null</c> quando o item e compartilhado
    /// (ProgramData e companhia) e nao pertence a uma conta.
    /// </summary>
    public UserProfile? Profile { get; init; }

    /// <summary>Caminho completo do item encontrado.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>O tipo do item.</summary>
    public AccountItemKind Kind { get; init; }

    /// <summary>
    /// True quando o item vem marcado para remocao. Itens de outra conta so
    /// vem marcados quando o usuario pediu para desinstalar de todas as contas.
    /// </summary>
    public bool Selected { get; init; }

    /// <summary>De onde veio o item (a conta, ou "compartilhado").</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>True quando o item e compartilhado entre as contas.</summary>
    public bool IsShared => Profile is null;

    /// <summary>
    /// O nome da conta dona do item; "compartilhado" para os itens sem conta.
    /// </summary>
    public string AccountName => Profile?.UserName ?? "compartilhado";
}

/// <summary>
/// Varredura de sobras POR CONTA do Windows.
///
/// As contas do Windows sao os perfis cadastrados no ProfileList do registro
/// (veja <see cref="UserProfiles"/>). Uma conta que nao esta logada tem o
/// seu registro em <c>HKEY_USERS\&lt;sid&gt;</c> e as suas pastas em
/// <c>C:\Users\&lt;conta&gt;\AppData</c> — e neither fica no
/// <c>HKEY_CURRENT_USER</c> nem no <c>%LOCALAPPDATA%</c> de quem esta usando
/// o Tranal.
///
/// A regra mora aqui e em um lugar so:
///
/// <list type="bullet">
/// <item>quais as subpastas do perfil varridas em cada conta;</item>
/// <item>quais as contas entram na varredura (as outras, e a atual quando
/// pedida);</item>
/// <item>quando o item pode ser marcado para remocao.</item>
/// </list>
///
/// O resto do projeto so consome.
/// </summary>
public static class AccountLeftoverScanner
{
    /// <summary>
    /// As subpastas do perfil varridas em cada conta. O <c>AppData</c> e onde
    /// um programa instalado guarda o que e do usuario.
    /// </summary>
    public static readonly IReadOnlyList<string> ProfileRoots = new[]
    {
        @"AppData\Local",
        @"AppData\Roaming",
        @"AppData\LocalLow",
    };

    /// <summary>
    /// As contas varridas, conforme as opcoes: as outras contas do Windows, e
    /// a conta atual quando ela foi pedida.
    /// </summary>
    public static IReadOnlyList<UserProfile> Accounts(AccountLeftoverScanOptions options) =>
        options.IncludeCurrentAccount ? UserProfiles.Enumerate() : UserProfiles.Others();

    /// <summary>
    /// As pastas compartilhadas entre as contas (o ProgramData). O que esta
    /// la pertence a maquina, nao a uma conta.
    /// </summary>
    public static IReadOnlyList<string> SharedRootPaths()
    {
        var result = new List<string>();

        try
        {
            var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrWhiteSpace(common)) result.Add(PathUtil.Normalize(common));
        }
        catch
        {
            // Sem o ProgramData: nada a compartilhar.
        }

        return result;
    }

    /// <summary>
    /// Sobras de pasta e arquivo em cada conta, a partir de nomes RELATIVOS
    /// ao perfil (ex.: <c>AppData\Local\Meu Programa</c>).
    ///
    /// Caminho com raiz (ex.: "C:\Programas") e ignorado: quem e compartilhado
    /// ou absoluto e tratado por <see cref="ScanShared"/>.
    /// </summary>
    public static IReadOnlyList<AccountLeftover> ScanFolders(
        IReadOnlyCollection<string> relativePaths,
        AccountLeftoverScanOptions options) =>
        ScanFolders(Accounts(options), relativePaths, options);

    /// <summary>
    /// Sobras de pasta e arquivo em cada uma das contas informadas.
    /// </summary>
    public static IReadOnlyList<AccountLeftover> ScanFolders(
        IEnumerable<UserProfile>? profiles,
        IReadOnlyCollection<string>? relativePaths,
        AccountLeftoverScanOptions options)
    {
        var result = new List<AccountLeftover>();
        if (relativePaths is null || relativePaths.Count == 0) return result;

        foreach (var profile in profiles ?? Array.Empty<UserProfile>())
        {
            if (profile is null) continue;
            if (string.IsNullOrWhiteSpace(profile.LocalPath)) continue;

            foreach (var relative in relativePaths)
            {
                if (string.IsNullOrWhiteSpace(relative)) continue;

                string full;
                try
                {
                    // Um caminho com raiz nao pertence ao perfil: seria o
                    // mesmo caminho para todas as contas.
                    if (Path.IsPathRooted(relative)) continue;
                    full = PathUtil.Normalize(Path.Combine(profile.LocalPath, relative));
                }
                catch
                {
                    continue;
                }

                if (full.Length == 0) continue;

                if (Directory.Exists(full))
                    result.Add(Make(profile, full, AccountItemKind.Folder, options));
                else if (File.Exists(full))
                    result.Add(Make(profile, full, AccountItemKind.File, options));
            }
        }

        return result;
    }

    /// <summary>
    /// Sobras de registro em cada conta, a partir de subcaminhos do hive da
    /// conta (ex.: <c>Software\Classes\Meu Programa</c>).
    ///
    /// O caminho exibido e o canonico do regedit:
    /// <c>HKEY_USERS\&lt;sid&gt;\&lt;subcaminho&gt;</c>.
    /// </summary>
    public static IReadOnlyList<AccountLeftover> ScanRegistry(
        IReadOnlyCollection<string> registrySubPaths,
        AccountLeftoverScanOptions options) =>
        ScanRegistry(Accounts(options), registrySubPaths, options);

    /// <summary>
    /// Sobras de registro em cada uma das contas informadas.
    /// </summary>
    public static IReadOnlyList<AccountLeftover> ScanRegistry(
        IEnumerable<UserProfile>? profiles,
        IReadOnlyCollection<string>? registrySubPaths,
        AccountLeftoverScanOptions options)
    {
        var result = new List<AccountLeftover>();
        if (registrySubPaths is null || registrySubPaths.Count == 0) return result;

        foreach (var profile in profiles ?? Array.Empty<UserProfile>())
        {
            if (profile is null) continue;
            if (string.IsNullOrWhiteSpace(profile.Sid)) continue;

            // Sem o hive carregado nao ha chave para ler: o perfil existe no
            // ProfileList, mas o registro dele nao esta montado.
            if (options.RequireLoadedHive && !profile.IsLoaded) continue;

            foreach (var sub in registrySubPaths)
            {
                if (string.IsNullOrWhiteSpace(sub)) continue;

                if (!RegistryKeyExists(profile.Sid, sub)) continue;

                result.Add(new AccountLeftover
                {
                    Profile = profile,
                    Path = $@"HKEY_USERS\{profile.Sid}\{sub}",
                    Kind = AccountItemKind.RegistryKey,
                    Selected = options.SelectAllAccounts || profile.IsCurrent,
                    Detail = DescribeProfile(profile),
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Sobras em pastas compartilhadas entre as contas (o ProgramData).
    /// O item pertence a maquina: ele nao vem de uma conta especifica.
    /// </summary>
    public static IReadOnlyList<AccountLeftover> ScanShared(IReadOnlyCollection<string>? absolutePaths)
    {
        var result = new List<AccountLeftover>();
        if (absolutePaths is null) return result;

        foreach (var path in absolutePaths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;

            var full = PathUtil.Normalize(path);
            if (full.Length == 0) continue;

            if (Directory.Exists(full))
            {
                result.Add(new AccountLeftover
                {
                    Profile = null,
                    Path = full,
                    Kind = AccountItemKind.Folder,
                    Selected = true,
                    Detail = "Pasta compartilhada",
                });
            }
            else if (File.Exists(full))
            {
                result.Add(new AccountLeftover
                {
                    Profile = null,
                    Path = full,
                    Kind = AccountItemKind.File,
                    Selected = true,
                    Detail = "Arquivo compartilhado",
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Monta um item de sobra de pasta ou arquivo de uma conta.
    /// </summary>
    private static AccountLeftover Make(
        UserProfile profile, string path, AccountItemKind kind, AccountLeftoverScanOptions options) =>
        new()
        {
            Profile = profile,
            Path = path,
            Kind = kind,
            Selected = options.SelectAllAccounts || profile.IsCurrent,
            Detail = DescribeProfile(profile),
        };

    /// <summary>De onde vem o item, para o usuario entender de quem ele e.</summary>
    private static string DescribeProfile(UserProfile profile) =>
        profile.IsCurrent ? "Conta atual" : $"Conta {profile.UserName}";

    /// <summary>
    /// True quando a chave existe no hive da conta.
    /// </summary>
    private static bool RegistryKeyExists(string sid, string subPath)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default);
            using var key = baseKey.OpenSubKey($"{sid}\\{subPath}");
            return key is not null;
        }
        catch
        {
            return false;
        }
    }
}
