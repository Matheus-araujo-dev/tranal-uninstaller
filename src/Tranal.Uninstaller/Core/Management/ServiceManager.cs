using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// Um servico do Windows instalado na maquina.
///
/// A informacao vem do WMI (<c>Win32_Service</c>), que e a fonte que o proprio
/// Windows usa para listar servicos — e por isso nao depende do idioma do
/// sistema, ao contrario de <c>sc.exe</c> e do painel de servicos.
/// </summary>
public sealed class WindowsService
{
    /// <summary>Nome interno do servico (o que o <c>sc.exe</c> exige).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Nome que aparece no painel de servicos.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Linha de comando completa do binario do servico.</summary>
    public string PathName { get; init; } = string.Empty;

    /// <summary>Estado atual, como o Windows o nomeia ("Running", "Stopped"…).</summary>
    public string State { get; init; } = string.Empty;

    /// <summary>Modo de inicializacao, como o Windows o nomeia.</summary>
    public string StartMode { get; init; } = string.Empty;

    /// <summary>Descricao do servico, quando existe.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>True quando o servico esta em execucao.</summary>
    public bool Started { get; init; }

    /// <summary>
    /// O binario do servico, sem os argumentos.
    ///
    /// Um caminho de binario costuma vir com argumentos grudados
    /// (<c>"C:\Pasta\app.exe" --servico</c>); para casar o servico com um
    /// programa instalado, so o executavel interessa.
    /// </summary>
    public string ExecutablePath => ExecutablePathResolver.Extract(PathName);
}

/// <summary>
/// Resultado de uma acao sobre o sistema (desativar, parar, remover).
///
/// O motivo importa: um servico protegido pelo Windows falha com "acesso
/// negado", e um servico que o Windows recusa por ser dependente de outro
/// falha de outro jeito. O usuario precisa saber a diferenca.
/// </summary>
public sealed class ManagementResult
{
    /// <summary>True quando a acao foi executada.</summary>
    public bool Success { get; init; }

    /// <summary>Texto que explica o resultado (sucesso ou recusa).</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Codigo de erro do Windows, quando a recusa veio do sistema.</summary>
    public int ErrorCode { get; init; }

    /// <summary>Resultado de sucesso.</summary>
    public static ManagementResult Ok(string message) => new() { Success = true, Message = message };

    /// <summary>Resultado de recusa, com o motivo.</summary>
    public static ManagementResult Fail(string message, int errorCode = 0) =>
        new() { Success = false, Message = message, ErrorCode = errorCode };
}

/// <summary>
/// Gerenciador proprio de servicos do Windows.
///
/// Hoje o Tranal apenas **abre** o painel de servicos
/// (<c>Core/Tools/WindowsToolsCatalog.cs</c>): o usuario sai do aplicativo e
/// volta. Este gerenciador lista, correlaciona e age — sem sair do Tranal.
///
/// A listagem vem do WMI (<c>Win32_Service</c>), nao do <c>sc.exe</c>: a saida
/// do <c>sc.exe</c> esta em portugues nesta maquina, e um catalogo que so
/// funciona em ingles e um catalogo quebrado.
/// </summary>
public static class ServiceManager
{
    /// <summary>
    /// Nomes de servico que o Windows nao permite remover por ferramenta.
    ///
    /// A lista nao e uma politica arbitraria: sao os servicos dos quais o
    /// proprio Windows depende para subir a sessao, a rede, a agenda e a
    /// protecao do sistema. Remover um deles deixa a maquina sem rede ou sem
    /// agendar tarefas — e a ferramenta recusa antes de tentar.
    /// </summary>
    private static readonly HashSet<string> CriticalServices = new(StringComparer.OrdinalIgnoreCase)
    {
        "RpcSs", "RpcEptMapper", "DcomLaunch", "Power", "PlugPlay", "ProfSvc", "ProfSvcStart",
        "Winlogon", "UserManager", "CryptSvc", "KeyIso", "SamSs", "LSM", "EventLog", "Schedule",
        "BFE", "MpsSvc", "WinDefend", "SecurityHealthService", "TrustedInstaller", "wuauserv",
        "Audiosrv", "AudioEndpointBuilder", "gpsvc", "nsi", "Dhcp", "Dnscache", "LanmanWorkstation",
        "LanmanServer", "Winmgmt", "NlaSvc", "ntexe", "SystemEventsBroker", "BrokerInfrastructure",
        "CoreMessagingRegistrar", "DcomLaunch", "UnistoreSvc", "Wcmsvc", "StateRepository",
        "PcaSvc", "WerSvc", "TabletInputService", "TimeBrokerSvc", "UserDataSvc", "WaaSMedicSvc",
        "XblAuthManager", "XblGameSave", "XboxGipSvc", "XboxNetApiSvc", "LicenseManager",
        "InstallService", "TokenBroker", "AppXSvc", "ClipSVC", "BrokerInfrastructure",
        "ConsentUxUserSvc", "DiagTrack", "dmwappushservice", "MapsBroker",
    };

    /// <summary>Todos os servicos instalados, com estado e modo de inicializacao.</summary>
    public static IReadOnlyList<WindowsService> List()
    {
        var services = new List<WindowsService>();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Service");
            using var results = searcher.Get();

            foreach (ManagementObject item in results)
            {
                services.Add(Read(item));
            }
        }
        catch (ManagementException)
        {
            // Sem acesso ao WMI (ou servico desabilitado): devolve o que deu
            // para ler e nao derruba a tela — o painel de servicos do Windows
            // continua sendo o caminho alternativo.
        }
        catch (UnauthorizedAccessException)
        {
            // Sem permissao para o WMI local: o catalogo fica vazio, e a GUI
            // avisa que a listagem precisa de administrador.
        }

        return services;
    }

    /// <summary>
    /// Os servicos cujo binario esta dentro de um caminho de instalacao.
    ///
    /// E o que amarra o servico a um programa instalado: o servico do
    /// Photoshop roda o Photoshop, e o caminho do executavel diz isso.
    /// </summary>
    public static IReadOnlyList<WindowsService> ListFor(string installPath)
    {
        var root = PathUtil.Normalize(installPath);
        if (string.IsNullOrEmpty(root)) return Array.Empty<WindowsService>();

        return List()
            .Where(s => BelongsTo(s.ExecutablePath, root))
            .ToList();
    }

    /// <summary>
    /// True quando um caminho de executavel esta dentro da pasta informada.
    ///
    /// O casamento e por **prefixo com separador**: <c>C:\App</c> casa com
    /// <c>C:\App\app.exe</c> e nao casa com <c>C:\App2\app.exe</c>.
    /// </summary>
    internal static bool BelongsTo(string executablePath, string normalizedRoot)
    {
        if (string.IsNullOrEmpty(executablePath) || string.IsNullOrEmpty(normalizedRoot)) return false;

        var candidate = PathUtil.Normalize(executablePath);
        if (candidate.Length == 0) return false;

        if (candidate.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)) return true;

        var prefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;

        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True quando o servico e protegido: o Windows precisa dele para a
    /// maquina funcionar de forma basica.
    /// </summary>
    public static bool IsCritical(WindowsService service)
    {
        if (service is null) return false;
        if (CriticalServices.Contains(service.Name)) return true;

        // Servico cujo binario mora na pasta de sistema tambem e protegido:
        // a Microsoft coloca ali o que o Windows precisa para funcionar.
        var system = PathUtil.Normalize(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        if (!string.IsNullOrEmpty(system) && BelongsTo(service.ExecutablePath, system)) return true;

        return false;
    }

    /// <summary>
    /// Cria um servico do Windows.
    ///
    /// Item de alto impacto na auditoria binaria (HiBit: "Gerenciador de
    /// Servicos que cria servico"): os concorrentes nao apenas gerenciam o
    /// que existe, eles criam.
    ///
    /// Criar servico e uma operacao de administrador: o Windows recusa sem
    /// permissao, e a recusa volta com o codigo de erro do sistema.
    /// </summary>
    /// <param name="serviceName">O nome interno do servico (o que o sc.exe exige).</param>
    /// <param name="displayName">O nome que aparece no painel de servicos.</param>
    /// <param name="executablePath">O executavel do servico.</param>
    /// <param name="arguments">Os argumentos do executavel.</param>
    /// <param name="description">A descricao do servico.</param>
    /// <param name="startMode">"Automatic", "Manual" ou "Disabled".</param>
    public static ManagementResult Create(
        string serviceName,
        string displayName,
        string executablePath,
        string arguments = "",
        string description = "",
        string startMode = "Manual")
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            return ManagementResult.Fail("Informe o nome do servico.");

        if (string.IsNullOrWhiteSpace(executablePath))
            return ManagementResult.Fail("Informe o executavel do servico.");

        var tipo = ServiceStartMode(startMode);

        if (tipo == uint.MaxValue)
            return ManagementResult.Fail("Modo de inicializacao invalido (use Automatic, Manual ou Disabled).");

        var linha = arguments.Trim().Length > 0
            ? $"{executablePath.Trim()} {arguments.Trim()}"
            : executablePath.Trim();

        IntPtr gerenciador = IntPtr.Zero;
        IntPtr servico = IntPtr.Zero;

        try
        {
            gerenciador = ServiceControlNative.OpenSCManager(null, null, ServiceControlNative.SC_MANAGER_CREATE_SERVICE);
            if (gerenciador == IntPtr.Zero)
                return ManagementResult.Fail("Sem permissao para abrir o gerenciador de servicos (administrador).", Marshal.GetLastWin32Error());

            if (!ServiceControlNative.CreateService(
                gerenciador,
                serviceName.Trim(),
                string.IsNullOrWhiteSpace(displayName) ? serviceName.Trim() : displayName.Trim(),
                ServiceControlNative.SERVICE_ALL_ACCESS,
                ServiceControlNative.SERVICE_WIN32_OWN_PROCESS,
                tipo,
                linha,
                null,
                IntPtr.Zero,
                null,
                null,
                null,
                0,
                0,
                out servico))

            if (servico == IntPtr.Zero)
                return ManagementResult.Fail("O Windows recusou criar o servico.", Marshal.GetLastWin32Error());

            if (!string.IsNullOrWhiteSpace(description))
            {
                var texto = Marshal.StringToHGlobalUni(description);
                var informacao = Marshal.AllocHGlobal(Marshal.SizeOf<ServiceControlNative.ServiceDescription>());

                try
                {
                    Marshal.StructureToPtr(
                        new ServiceControlNative.ServiceDescription { lpDescription = texto },
                        informacao,
                        false);

                    ServiceControlNative.ChangeServiceConfig2(
                        servico,
                        ServiceControlNative.SERVICE_CONFIG_DESCRIPTION,
                        informacao);
                }
                finally
                {
                    Marshal.FreeHGlobal(informacao);
                    Marshal.FreeHGlobal(texto);
                }
            }

            return ManagementResult.Ok($"Servico criado: {serviceName.Trim()}");
        }
        catch (DllNotFoundException)
        {
            return ManagementResult.Fail("Este Windows nao expoe o gerenciador de servicos.");
        }
        catch (EntryPointNotFoundException)
        {
            return ManagementResult.Fail("Este Windows nao expoe o gerenciador de servicos.");
        }
        finally
        {
            if (servico != IntPtr.Zero) ServiceControlNative.CloseServiceHandle(servico);
            if (gerenciador != IntPtr.Zero) ServiceControlNative.CloseServiceHandle(gerenciador);
        }
    }

    /// <summary>
    /// O codigo do modo de inicializacao, ou <c>uint.MaxValue</c> quando o
    /// texto nao corresponde a nenhum modo valido.
    /// </summary>
    /// <param name="startMode">"Automatic", "Manual" ou "Disabled".</param>
    public static uint ServiceStartMode(string? startMode)
    {
        var texto = (startMode ?? string.Empty).Trim();

        if (texto.Equals("Automatic", StringComparison.OrdinalIgnoreCase) ||
            texto.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            return ServiceControlNative.SERVICE_AUTO_START;

        if (texto.Equals("Manual", StringComparison.OrdinalIgnoreCase))
            return ServiceControlNative.SERVICE_DEMAND_START;

        if (texto.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
            return ServiceControlNative.SERVICE_DISABLED;

        return uint.MaxValue;
    }
    /// <summary>Desativa a inicializacao automatica do servico.</summary>
    public static ManagementResult Disable(WindowsService service)
    {
        if (service is null) return ManagementResult.Fail("Servico nao informado.");
        if (IsCritical(service))
            return ManagementResult.Fail($"O servico '{service.Name}' e protegido pelo Windows e nao pode ser desativado aqui.");

        return ChangeStartMode(service, "Disabled", "desativado");
    }

    /// <summary>Reativa a inicializacao automatica do servico.</summary>
    public static ManagementResult Enable(WindowsService service)
    {
        if (service is null) return ManagementResult.Fail("Servico nao informado.");

        return ChangeStartMode(service, "Auto", "reativado");
    }

    /// <summary>Para o servico, se ele aceitar.</summary>
    public static ManagementResult Stop(WindowsService service)
    {
        if (service is null) return ManagementResult.Fail("Servico nao informado.");
        if (IsCritical(service))
            return ManagementResult.Fail($"O servico '{service.Name}' e protegido pelo Windows e nao pode ser parado aqui.");

        try
        {
            using var wmi = Open(service.Name);
            var result = Convert.ToUInt32(wmi.InvokeMethod("Stop", null, null), CultureInfo.InvariantCulture);
            if (result is 0 or 2)
                return ManagementResult.Ok($"O servico '{service.DisplayName}' foi parado.");

            return ManagementResult.Fail(
                $"O Windows recusou parar o servico '{service.DisplayName}' (codigo {result}).");
        }
        catch (ManagementException ex)
        {
            return ManagementResult.Fail(
                $"Nao foi possivel parar o servico '{service.DisplayName}': {ex.Message}",
                RemovalOutcome.ErrorCodeOf(ex));
        }
    }

    /// <summary>
    /// Define o modo de inicializacao do servico.
    ///
    /// E o que permite devolver o servico ao modo que ele tinha: o
    /// "acordar" de um programa dormente precisa devolver o Automatic para
    /// Manual, e nao deixar tudo como Automatic.
    /// </summary>
    public static ManagementResult SetStartMode(WindowsService service, string startMode)
    {
        if (service is null) return ManagementResult.Fail("Servico nao informado.");
        if (IsCritical(service))
            return ManagementResult.Fail($"O servico '{service.Name}' e protegido pelo Windows e nao pode ser alterado aqui.");

        var modo = (startMode ?? string.Empty).Trim();
        if (modo.Length == 0)
            return ManagementResult.Fail("Modo de inicializacao nao informado.");

        if (ServiceStartMode(modo) == uint.MaxValue)
            return ManagementResult.Fail($"Modo de inicializacao invalido: '{modo}'.");

        return ChangeStartMode(service, modo, $"definido como {modo}");
    }

    /// <summary>Inicia o servico, se ele aceitar.</summary>
    public static ManagementResult Start(WindowsService service)
    {
        if (service is null) return ManagementResult.Fail("Servico nao informado.");
        if (IsCritical(service))
            return ManagementResult.Fail($"O servico '{service.Name}' e protegido pelo Windows e nao pode ser iniciado aqui.");

        try
        {
            using var wmi = Open(service.Name);
            var result = Convert.ToUInt32(wmi.InvokeMethod("Start", null, null), CultureInfo.InvariantCulture);
            if (result is 0 or 2)
                return ManagementResult.Ok($"O servico '{service.DisplayName}' foi iniciado.");

            return ManagementResult.Fail(
                $"O Windows recusou iniciar o servico '{service.DisplayName}' (codigo {result}).");
        }
        catch (ManagementException ex)
        {
            return ManagementResult.Fail(
                $"Nao foi possivel iniciar o servico '{service.DisplayName}': {ex.Message}",
                RemovalOutcome.ErrorCodeOf(ex));
        }
    }

    /// <summary>Remove o servico do sistema.</summary>
    public static ManagementResult Delete(WindowsService service)
    {
        if (service is null) return ManagementResult.Fail("Servico nao informado.");
        if (IsCritical(service))
            return ManagementResult.Fail($"O servico '{service.Name}' e protegido pelo Windows e nao pode ser removido aqui.");

        try
        {
            using var wmi = Open(service.Name);
            var result = Convert.ToUInt32(wmi.InvokeMethod("Delete", null, null), CultureInfo.InvariantCulture);
            if (result == 0)
                return ManagementResult.Ok($"O servico '{service.DisplayName}' foi removido.");

            return ManagementResult.Fail(
                $"O Windows recusou remover o servico '{service.DisplayName}' (codigo {result}).");
        }
        catch (ManagementException ex)
        {
            return ManagementResult.Fail(
                $"Nao foi possivel remover o servico '{service.DisplayName}': {ex.Message}",
                RemovalOutcome.ErrorCodeOf(ex));
        }
    }

    private static ManagementResult ChangeStartMode(WindowsService service, string mode, string acao)
    {
        try
        {
            using var wmi = Open(service.Name);
            var result = Convert.ToUInt32(wmi.InvokeMethod("ChangeStartMode", new object?[] { mode }), CultureInfo.InvariantCulture);
            if (result == 0)
                return ManagementResult.Ok($"O servico '{service.DisplayName}' foi {acao}.");

            return ManagementResult.Fail(
                $"O Windows recusou alterar o servico '{service.DisplayName}' (codigo {result}).");
        }
        catch (ManagementException ex)
        {
            return ManagementResult.Fail(
                $"Nao foi possivel alterar o servico '{service.DisplayName}': {ex.Message}",
                RemovalOutcome.ErrorCodeOf(ex));
        }
    }

    private static ManagementObject Open(string name)
    {
        var wmi = new ManagementObject($"Win32_Service.Name='{name.Replace("'", "''")}'");
        wmi.Get();
        return wmi;
    }

    private static WindowsService Read(ManagementObject item)
    {
        return new WindowsService
        {
            Name = Text(item, "Name"),
            DisplayName = Text(item, "DisplayName"),
            PathName = Text(item, "PathName"),
            State = Text(item, "State"),
            StartMode = Text(item, "StartMode"),
            Description = Text(item, "Description"),
            Started = Text(item, "Started").Equals("true", StringComparison.OrdinalIgnoreCase),
        };
    }

    private static string Text(ManagementObject item, string property)
    {
        try
        {
            var value = item[property];
            return value is null ? string.Empty : value.ToString() ?? string.Empty;
        }
        catch (ManagementException)
        {
            // A propriedade pode nao existir em um servico malformado; a
            // listagem continua com o campo vazio em vez de perder o servico.
            return string.Empty;
        }
    }
}
