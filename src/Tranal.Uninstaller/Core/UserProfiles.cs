using System.IO;
using System.Security.Principal;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Perfil de usuario do Windows — uma "conta" do sistema.
/// </summary>
public sealed class UserProfile
{
    /// <summary>Identificador de seguranca (SID) da conta.</summary>
    public string Sid { get; init; } = string.Empty;

    /// <summary>Pasta local do perfil (ex.: "C:\Users\Joao").</summary>
    public string LocalPath { get; init; } = string.Empty;

    /// <summary>Nome de exibicao da conta.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>True quando e a conta que esta usando o Tranal agora.</summary>
    public bool IsCurrent { get; init; }

    /// <summary>True quando o hive da conta esta carregado no sistema.</summary>
    public bool IsLoaded { get; init; }

    /// <summary>Raiz do registro da conta.</summary>
    public string RegistryRoot => $@"HKEY_USERS\{Sid}";

    /// <summary>Pasta "AppData" da conta.</summary>
    public string AppDataPath => Path.Combine(LocalPath, "AppData");
}

/// <summary>
/// Enumera as contas (perfis de usuario) do Windows.
///
/// Corresponde as opcoes do Revo Uninstaller Pro "Show the option to scan all
/// Windows accounts for leftovers" (15345), "Scan all Windows accounts for
/// leftovers" (15352) e "Uninstall from all Windows accounts" (15353).
///
/// A fonte de verdade e o ProfileList do registro
/// (HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList): e o catalogo
/// real de perfis da maquina, e nao uma varredura de pastas que inventaria
/// perfis de contas apagadas.
/// </summary>
public static class UserProfiles
{
    private const string ProfileListPath =
        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList";

    /// <summary>SIDs das contas de servico do Windows: nao sao perfis de usuario.</summary>
    private static readonly string[] SystemSids =
    {
        "S-1-5-18", // conta local do sistema
        "S-1-5-19", // conta local de servicos
        "S-1-5-20", // conta de servico de rede
    };

    private static string? _currentSid;

    /// <summary>
    /// SID da conta que esta usando o Tranal agora. Vazio quando o Windows
    /// nao consegue informar (nunca acontece em uso normal).
    /// </summary>
    public static string? CurrentSid
    {
        get
        {
            if (_currentSid is not null) return _currentSid;

            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                _currentSid = identity.User?.Value;
            }
            catch
            {
                _currentSid = null;
            }

            return _currentSid;
        }
    }

    /// <summary>
    /// Todas as contas do Windows desta maquina, com a conta atual em primeiro.
    /// </summary>
    public static IReadOnlyList<UserProfile> Enumerate()
    {
        var profiles = new List<UserProfile>();

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(ProfileListPath);
            if (key is null) return profiles;

            foreach (var sid in key.GetSubKeyNames())
            {
                // ".DEFAULT" e "_Classes" aparecem no ProfileList em algumas
                // configuracoes, mas nao sao contas.
                if (sid.StartsWith('.') || sid.StartsWith('_')) continue;
                if (SystemSids.Contains(sid, StringComparer.OrdinalIgnoreCase)) continue;

                string? imagePath = null;
                var loaded = false;

                try
                {
                    using var sub = key.OpenSubKey(sid);
                    imagePath = sub?.GetValue("ProfileImagePath")?.ToString();

                    // RefCount > 0 significa que o perfil esta carregado nesta sessao.
                    var refCount = sub?.GetValue("RefCount");
                    loaded = refCount is not null && Convert.ToInt32(refCount) > 0;
                }
                catch { /* perfil inacessivel: segue sem a pasta, mas com o SID */ }

                var full = PathUtil.Normalize(imagePath);
                if (full.Length == 0) continue;

                profiles.Add(new UserProfile
                {
                    Sid = sid,
                    LocalPath = full,
                    UserName = Path.GetFileName(full.TrimEnd('\\')),
                    IsCurrent = IsCurrentProfile(sid, full),
                    IsLoaded = loaded || IsHiveLoaded(sid),
                });
            }
        }
        catch { /* ProfileList inacessivel: nenhuma conta alternativa */ }

        return profiles
            .OrderByDescending(p => p.IsCurrent)
            .ThenBy(p => p.UserName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// True quando o perfil e o da conta que esta usando o Tranal agora.
    ///
    /// A comparacao pelo SID e a precisa. A comparacao pela pasta do perfil e
    /// a reserva: quando o Windows nao entrega o SID, sem ela a conta atual
    /// seria tratada como "outra conta" e a varredura em todas as contas
    /// listaria HKEY_USERS da conta atual ao lado de HKEY_CURRENT_USER — com o
    /// mesmo conteudo, duas vezes.
    /// </summary>
    private static bool IsCurrentProfile(string sid, string localPath)
    {
        if (!string.IsNullOrEmpty(CurrentSid)
            && string.Equals(sid, CurrentSid, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var current = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(current)) return false;

        return string.Equals(
            PathUtil.Normalize(localPath),
            PathUtil.Normalize(current),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Contas do Windows diferentes da conta atual.
    /// </summary>
    public static IReadOnlyList<UserProfile> Others() =>
        Enumerate().Where(p => !p.IsCurrent).ToList();

    /// <summary>
    /// True quando o hive da conta esta montado em HKEY_USERS. Sem o hive
    /// carregado nao ha como ler (nem remover) as chaves da conta.
    /// </summary>
    public static bool IsHiveLoaded(string sid)
    {
        if (string.IsNullOrWhiteSpace(sid)) return false;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default);
            using var key = baseKey.OpenSubKey(sid);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }
}
