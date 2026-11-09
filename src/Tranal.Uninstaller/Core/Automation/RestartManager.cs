using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Tranal.Uninstaller.Core.Tools;
using System.Text;

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

    /// <summary>
    /// O tamanho que a API do Windows escreve em cada <c>RM_PROCESS_INFO</c>.
    ///
    /// A estrutura nativa tem os nomes em UTF-16: 12 bytes do processo, 512 do
    /// nome do aplicativo, 128 do nome do servico e 16 dos quatro campos
    /// finais. Sao 668 bytes.
    /// </summary>
    private const int TamanhoRmProcessInfo = 668;

    /// <summary>
    /// O tamanho que a estrutura gerenciada ocupa hoje.
    ///
    /// A API do Windows escreve no tamanho dela. Se a estrutura gerenciada for
    /// menor, a escrita passa do fim do array e corrompe a memoria do
    /// processo. Por isso o modulo compara os dois tamanhos antes de chamar a
    /// API — e se recusa a chamar.
    /// </summary>
    internal static int TamanhoDaEstrutura => Marshal.SizeOf<RmProcessInfo>();

    /// <summary>
    /// A estrutura gerenciada esta no tamanho que o Windows escreve?
    /// </summary>
    internal static bool EstruturaNoTamanhoDoWindows =>
        TamanhoDaEstrutura == TamanhoRmProcessInfo;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
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

    /// <summary>
    /// Inicia uma sessao do Restart Manager.
    ///
    /// A chave da sessao e um buffer de **saida**: a API do Windows escreve
    /// nela a chave que ela gerou. Passar uma string (que e entrada) faz a
    /// chamada falhar com ERROR_BAD_ARGUMENTS, e o modulo inteiro devolvia
    /// lista vazia — nenhum arquivo em uso era encontrado.
    /// </summary>
    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, StringBuilder strSessionKey);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmEndSession(uint pSessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(uint pSessionHandle, uint nFiles, string[] rgsFilenames,
        uint nApplications, RmUniqueProcess[]? rgApplications, uint nServices, string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
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
        var chave = new StringBuilder(CchRmSessionKey + 1);

        try
        {
            var erro = RmStartSession(out session, 0, chave);
            if (erro != 0)
            {
                ToolLog.Error($"Restart Manager: nao foi possivel iniciar a sessao (codigo {erro}).");
                return processos;
            }

            var arquivos = new[] { filePath };
            erro = RmRegisterResources(session, 1, arquivos, 0, null, 0, null);
            if (erro != 0)
            {
                ToolLog.Error($"Restart Manager: falha ao registrar o recurso (codigo {erro}).");
                return processos;
            }

            uint needed = 0;
            uint reasons = RmRebootReasonNone;
            uint count = 0;

            erro = RmGetList(session, out needed, ref count, null!, ref reasons);
            if (erro != ErrorMoreData || needed == 0) return processos;

            // A API do Windows escreve a estrutura no tamanho dela (668
            // bytes). Uma estrutura gerenciada menor faria a escrita passar
            // do fim do array: o modulo se recusa a chamar a API.
            if (!EstruturaNoTamanhoDoWindows)
            {
                ToolLog.Error(
                    $"Restart Manager: a estrutura ocupa {TamanhoDaEstrutura} bytes e o Windows escreve {TamanhoRmProcessInfo}; a lista foi ignorada.");
                return processos;
            }

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
