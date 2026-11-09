using System.Runtime.InteropServices;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// Um privilegio do token do processo.
/// </summary>
/// <param name="Id">O identificador fixo do privilegio.</param>
/// <param name="Name">O nome que o Windows usa.</param>
/// <param name="Enabled">O privilegio esta ligado no token agora?</param>
public sealed record TokenPrivilegeInfo(string Id, string Name, bool Enabled);

/// <summary>
/// Os privilegios do token e a impersonacao.
///
/// Todo processo roda com um **token**: um conjunto de permissoes. Alguns
/// privilegios nem vem ligados — e o caso do "depurar programas", que
/// precisa ser ligado antes de o Tranal conseguir olhar o que outro processo
/// fez.
///
/// A API e <c>AdjustTokenPrivileges</c>, e a lista de privilegios vem da
/// propria conta do Windows.
///
/// A regra esta em um lugar so: este modulo sabe o que e um privilegio e
/// como liga-lo. A tela e a linha de comando consultam o mesmo modulo.
/// </summary>
public static class TokenPrivileges
{
    /// <summary>O privilegio de depurar programas.</summary>
    public const string SeDebugPrivilege = "SeDebugPrivilege";

    /// <summary>O privilegio de fazer backup do registro.</summary>
    public const string SeBackupPrivilege = "SeBackupPrivilege";

    /// <summary>O privilegio de restaurar o registro.</summary>
    public const string SeRestorePrivilege = "SeRestorePrivilege";

    /// <summary>O privilegio de assumir o identidade de um cliente.</summary>
    public const string SeImpersonatePrivilege = "SeImpersonatePrivilege";

    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint SE_PRIVILEGE_ENABLED = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID_AND_ATTRIBUTES Privilege;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr tokenHandle, bool disableAll, ref TOKEN_PRIVILEGES newState, uint bufferLength,
        IntPtr previousState, IntPtr returnLength);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool LookupPrivilegeValue(string? host, string name, out LUID luid);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    /// <summary>
    /// Os privilegios conhecidos e o estado de cada um no token atual.
    /// </summary>
    public static IReadOnlyList<TokenPrivilegeInfo> List()
    {
        var nomes = new[]
        {
            (SeDebugPrivilege, "SeDebugPrivilege"),
            (SeBackupPrivilege, "SeBackupPrivilege"),
            (SeRestorePrivilege, "SeRestorePrivilege"),
            (SeImpersonatePrivilege, "SeImpersonatePrivilege"),
        };

        var resultado = new List<TokenPrivilegeInfo>();

        foreach (var (id, nome) in nomes)
        {
            resultado.Add(new TokenPrivilegeInfo(id, nome, IsEnabled(nome)));
        }

        return resultado;
    }

    /// <summary>
    /// O privilegio esta ligado no token do processo?
    /// </summary>
    /// <param name="privilegeName">O nome do privilegio.</param>
    public static bool IsEnabled(string privilegeName)
    {
        if (!LookupPrivilegeValue(null, privilegeName, out var luid)) return false;

        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, out var token)) return false;

        try
        {
            var state = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privilege = new LUID_AND_ATTRIBUTES
                {
                    Luid = luid,
                    Attributes = 0,
                },
            };

            if (!AdjustTokenPrivileges(token, false, ref state, 0, IntPtr.Zero, IntPtr.Zero)) return false;

            // A funcao devolve zero quando o privilegio nao existia; quando
            // existia mas estava desligado, o ultimo erro e "not assigned".
            return Marshal.GetLastWin32Error() == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            CloseHandle(token);
        }
    }

    /// <summary>
    /// Liga um privilegio no token do processo.
    /// </summary>
    /// <param name="privilegeName">O nome do privilegio.</param>
    /// <returns>Verdadeiro quando o privilegio ficou ligado.</returns>
    public static bool Enable(string privilegeName)
    {
        if (!LookupPrivilegeValue(null, privilegeName, out var luid)) return false;

        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var token)) return false;

        try
        {
            var state = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privilege = new LUID_AND_ATTRIBUTES
                {
                    Luid = luid,
                    Attributes = SE_PRIVILEGE_ENABLED,
                },
            };

            if (!AdjustTokenPrivileges(token, false, ref state, 0, IntPtr.Zero, IntPtr.Zero)) return false;

            return Marshal.GetLastWin32Error() == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            CloseHandle(token);
        }
    }

    /// <summary>
    /// O Tranal esta rodando como administrador?
    ///
    /// O sinal e o proprio token: um administrador tem o privilegio de
    /// "impersonar cliente" ligado por padrao. E o que o Windows usa para
    /// dizer que a conta e administrativa.
    /// </summary>
    public static bool IsElevated() => IsEnabled(SeImpersonatePrivilege);
}
