using System;
using System.Runtime.InteropServices;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// A API do Windows que cria e configura servicos (o "Service Control
/// Manager").
///
/// Fica em um arquivo so, com os nomes em ingles do API do Windows, porque
/// e a traducao literal da documentacao da Microsoft. Quem usa e o
/// <see cref="ServiceManager"/>.
///
/// A API e documentada em "Service Control Manager" (Windows API): as
/// constantes abaixo tem os valores fixos do sistema.
/// </summary>
internal static class ServiceControlNative
{
    /// <summary>Permite criar servicos.</summary>
    internal const uint SC_MANAGER_CREATE_SERVICE = 0x0002;

    /// <summary>Servico que roda no seu proprio processo.</summary>
    internal const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;

    /// <summary>Servico que roda em um processo compartilhado.</summary>
    internal const uint SERVICE_WIN32_SHARE_PROCESS = 0x00000020;

    /// <summary>Inicia automaticamente.</summary>
    internal const uint SERVICE_AUTO_START = 0x00000002;

    /// <summary>Inicia sob demanda.</summary>
    internal const uint SERVICE_DEMAND_START = 0x00000003;

    /// <summary>Fica desativado.</summary>
    internal const uint SERVICE_DISABLED = 0x00000004;

    /// <summary>Recuperacao: nao reiniciar em caso de falha.</summary>
    internal const uint SERVICE_ERROR_NORMAL = 0x00000001;

    /// <summary>Permissao completa sobre o servico.</summary>
    internal const uint SERVICE_ALL_ACCESS = 0x000F01FF;

    /// <summary>Identificador da configuracao de descricao do servico.</summary>
    internal const int SERVICE_CONFIG_DESCRIPTION = 1;

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool CreateService(
        IntPtr scManager,
        string serviceName,
        string displayName,
        uint desiredAccess,
        uint serviceType,
        uint startType,
        string binaryPathName,
        string? loadOrderGroup,
        IntPtr tagId,
        string? dependencies,
        string? serviceStartName,
        string? password,
        uint passwordSize,
        uint serviceStartTimeout,
        out IntPtr serviceHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool ChangeServiceConfig2(
        IntPtr service,
        int infoLevel,
        IntPtr lpInfo);

    /// <summary>
    /// A descricao do servico — o que <c>ChangeServiceConfig2</c> espera
    /// quando o nivel e <see cref="SERVICE_CONFIG_DESCRIPTION"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct ServiceDescription
    {
        public IntPtr lpDescription;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseServiceHandle(IntPtr handle);
}
