using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// Um processo que esta segurando um arquivo.
/// </summary>
public sealed class LockingProcess
{
    /// <summary>O identificador do processo.</summary>
    public int ProcessId { get; init; }

    /// <summary>O nome do executavel do processo.</summary>
    public string ProcessName { get; init; } = string.Empty;

    /// <summary>True quando o processo tem uma janela aberta.</summary>
    public bool HasWindow { get; init; }
}

/// <summary>
/// Descobre quem esta segurando um arquivo — o Restart Manager do Windows.
///
/// Um desinstalador trava na maioria das vezes porque o proprio programa
/// continua rodando e segura o arquivo que o desinstalador quer apagar. O
/// Windows tem uma API feita para isso: o **Restart Manager** (<c>rmsrch.dll</c>),
/// que devolve a lista de processos que travam um arquivo.
///
/// Item de alto impacto na auditoria binaria (Soft Organizer: "Restart Manager
/// para liberar arquivos em uso (RmStartSession/RmGetList/RmRegisterResources)").
///
/// Esta classe **pergunta**; quem encerra o processo e o motor canonico
/// (<c>Core/ProcessTerminator.cs</c>), que ja sabe respeitar o que o
/// Windows nao deixa matar.
/// </summary>
public static class RestartManager
{
    private const int RmRebootReasonNone = 0;
    private const int CchRmSessionKey = 32;
    private const int CchRmMaxAppName = 255;
    private const int CchRmMaxSvcName = 63;
    private const int ErrorMoreData = 234;

    [StructLayout(LayoutKind.Sequential)]
    private struct RmSessionInfo
    {
        public RmUniqueProcess Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxAppName + 1)]
        public string AppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxSvcName + 1)]
        public string ServiceShortName;
        public RmAppType ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RmUniqueProcess
    {
        public uint dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    private enum RmAppType
    {
        Unknown = 0,
        MainWindow = 1,
        OtherWindow = 2,
        Service = 3,
        Explorer = 4,
        Console = 5,
        Critical = 1000,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RmProcessInfo
    {
        public RmUniqueProcess Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxAppName + 1)]
        public string AppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxSvcName + 1)]
        public string ServiceShortName;
        public RmAppType ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint pSessionHandle);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmRegisterResources(uint pSessionHandle, uint nFiles, string[] rgsFilenames,
        uint nApplications, RmUniqueProcess[]? rgApplications, uint nServices, string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded,
        ref uint pnProcInfo, [In, Out] RmProcessInfo[] rgAffectedApps, ref uint lpdwRebootReasons);

    /// <summary>
    /// Os processos que estao segurando o arquivo informado.
    /// </summary>
    /// <param name="filePath">O caminho do arquivo.</param>
    public static IReadOnlyList<LockingProcess> WhoIsLocking(string? filePath)
    {
        var processos = new List<LockingProcess>();

        if (string.IsNullOrWhiteSpace(filePath)) return processos;

        uint session = 0;
        var chave = Guid.NewGuid().ToString("N");

        try
        {
            var erro = RmStartSession(out session, 0, chave);
            if (erro != 0) return processos;

            var arquivos = new[] { filePath };
            erro = RmRegisterResources(session, 1, arquivos, 0, null, 0, null);
            if (erro != 0) return processos;

            uint needed = 0;
            uint reasons = RmRebootReasonNone;
            uint count = 0;

            erro = RmGetList(session, out needed, ref count, null!, ref reasons);
            if (erro != ErrorMoreData || needed == 0) return processos;

            var informacoes = new RmProcessInfo[needed];
            count = needed;

            erro = RmGetList(session, out needed, ref count, informacoes, ref reasons);
            if (erro != 0) return processos;

            foreach (var info in informacoes.Take((int)count))
            {
                var id = (int)info.Process.dwProcessId;

                processos.Add(new LockingProcess
                {
                    ProcessId = id,
                    ProcessName = NomeDoProcesso(id),
                    HasWindow = info.ApplicationType == RmAppType.MainWindow,
                });
            }
        }
        catch (DllNotFoundException)
        {
            // Windows sem o Restart Manager: sem lista, sem drama.
        }
        catch (EntryPointNotFoundException)
        {
            // Versao antiga da API: mesma coisa.
        }
        finally
        {
            if (session != 0)
            {
                try { RmEndSession(session); } catch { /* encerramento best-effort */ }
            }
        }

        return processos;
    }

    /// <summary>
    /// True quando o arquivo esta sendo usado por algum processo.
    /// </summary>
    /// <param name="filePath">O caminho do arquivo.</param>
    public static bool IsLocked(string? filePath) => WhoIsLocking(filePath).Count > 0;

    private static string NomeDoProcesso(int id)
    {
        try
        {
            using var processo = System.Diagnostics.Process.GetProcessById(id);
            return processo.ProcessName;
        }
        catch
        {
            return "(desconhecido)";
        }
    }
}
