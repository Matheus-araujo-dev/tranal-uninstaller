using System.Diagnostics;
using System.IO;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// A acao de energia que o Windows pode executar ao fim de uma tarefa.
/// </summary>
public enum PowerAction
{
    /// <summary>Desligar a maquina.</summary>
    Desligar,

    /// <summary>Reiniciar a maquina.</summary>
    Reiniciar,

    /// <summary>Suspender a maquina (sleep).</summary>
    Suspender,

    /// <summary>Hibernar a maquina.</summary>
    Hibernar,

    /// <summary>Sair da sessao do usuario.</summary>
    SairDaSessao,
}

/// <summary>
/// As acoes de energia com contagem regressiva.
///
/// O BCU oferece "put the computer to sleep when the task is finished, with
/// a countdown". O Tranal faz a mesma coisa e mantem a REGRA em um lugar so:
/// o prazo, a acao e o cancelamento vivem aqui, e a interface e a CLI
/// apenas consomem o veredito.
///
/// Desligar, reiniciar, hibernar e sair da sessao vao pelo
/// <c>shutdown.exe</c>, que e a ferramenta do proprio Windows: ela mostra a
/// contagem, avisa o usuario e aceita o cancelamento. A suspensao nao tem
/// equivalente no <c>shutdown.exe</c> — por isso ela e feita aqui, com a
/// mesma contagem e o mesmo cancelamento.
/// </summary>
public static class PowerPlanner
{
    /// <summary>O prazo minimo, em segundos.</summary>
    public const int MinSeconds = 5;

    /// <summary>O prazo usado por padrao.</summary>
    public const int DefaultSeconds = 60;

    private static readonly object _lock = new();
    private static System.Threading.Timer? _timer;
    private static DateTime _due = DateTime.MinValue;
    private static PowerAction? _pending;

    /// <summary>A acao agendada, ou null quando nada esta agendado.</summary>
    public static PowerAction? Pending
    {
        get { lock (_lock) return _pending; }
    }

    /// <summary>Quantos segundos faltam para a acao agendada.</summary>
    public static int RemainingSeconds
    {
        get
        {
            lock (_lock)
            {
                if (_pending is null) return 0;

                var segundos = (int)Math.Ceiling((_due - DateTime.Now).TotalSeconds);
                return segundos > 0 ? segundos : 0;
            }
        }
    }

    /// <summary>
    /// True quando o Windows aceita hibernar nesta maquina.
    ///
    /// O indicador e o proprio arquivo de hibernacao do Windows: sem ele,
    /// o <c>shutdown /h</c> e recusado.
    /// </summary>
    public static bool SupportsHibernate()
    {
        try
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (windows.Length == 0) return false;

            return File.Exists(Path.Combine(windows, "hiberfil.sys"));
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao verificar o suporte a hibernacao: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Agenda a acao de energia com a contagem regressiva.
    /// </summary>
    /// <param name="action">A acao a executar.</param>
    /// <param name="seconds">Quanto tempo ate a acao.</param>
    public static bool Plan(PowerAction action, int seconds = DefaultSeconds)
    {
        if (seconds < MinSeconds) seconds = MinSeconds;

        Cancel();

        switch (action)
        {
            case PowerAction.Desligar:
                return ExecutarShutdown("/s", seconds);
            case PowerAction.Reiniciar:
                return ExecutarShutdown("/r", seconds);
            case PowerAction.Hibernar:
                return ExecutarShutdown("/h", seconds);
            case PowerAction.SairDaSessao:
                return ExecutarShutdown("/l", seconds);
            case PowerAction.Suspender:
                return AgendarSuspensao(seconds);
            default:
                return false;
        }
    }

    /// <summary>
    /// Cancela a acao agendada.
    ///
    /// O <c>shutdown.exe /a</c> aborta o que o Windows tem pendente; o
    /// timer interno e o caso da suspensao.
    /// </summary>
    public static bool Cancel()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            _pending = null;
            _due = DateTime.MinValue;
        }

        try
        {
            var info = new ProcessStartInfo("shutdown.exe", "/a")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var processo = Process.Start(info);
            return processo is not null;
        }
        catch (Exception ex)
        {
            // Cancelar e sempre possivel: se o shutdown.exe nao existe, o
            // cancelamento do timer ja foi feito acima.
            ToolLog.Error($"Falha ao cancelar a acao de energia: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// O texto que descreve o plano agendado.
    /// </summary>
    /// <param name="action">A acao.</param>
    /// <param name="seconds">O prazo.</param>
    public static string Describe(PowerAction action, int seconds)
    {
        var nome = action switch
        {
            PowerAction.Desligar => "desligar",
            PowerAction.Reiniciar => "reiniciar",
            PowerAction.Suspender => "suspender",
            PowerAction.Hibernar => "hibernar",
            PowerAction.SairDaSessao => "sair da sessao",
            _ => action.ToString(),
        };

        return $"{nome} em {seconds} segundo(s)";
    }

    private static bool ExecutarShutdown(string flag, int seconds)
    {
        try
        {
            var info = new ProcessStartInfo("shutdown.exe", $"{flag} /t {seconds}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var processo = Process.Start(info);
            if (processo is null) return false;

            lock (_lock)
            {
                _pending = flag switch
                {
                    "/s" => PowerAction.Desligar,
                    "/r" => PowerAction.Reiniciar,
                    "/h" => PowerAction.Hibernar,
                    _ => PowerAction.SairDaSessao,
                };
                _due = DateTime.Now.AddSeconds(seconds);
            }

            return true;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao agendar '{flag}': {ex.Message}");
            return false;
        }
    }

    private static bool AgendarSuspensao(int seconds)
    {
        try
        {
            lock (_lock)
            {
                _pending = PowerAction.Suspender;
                _due = DateTime.Now.AddSeconds(seconds);

                _timer = new System.Threading.Timer(
                    _ => AoVencer(),
                    null,
                    TimeSpan.FromSeconds(seconds),
                    TimeSpan.FromSeconds(1));
            }

            return true;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao agendar a suspensao: {ex.Message}");
            return false;
        }
    }

    private static void AoVencer()
    {
        PowerAction? acao;
        lock (_lock)
        {
            acao = _pending;
            _pending = null;
            _timer?.Dispose();
            _timer = null;
        }

        if (acao != PowerAction.Suspender) return;

        try
        {
            // A suspensao e a unica das acoes que o shutdown.exe nao faz.
            // O metodo oficial e o SetSuspendState da powrprof.dll.
            var info = new ProcessStartInfo(
                "rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var processo = Process.Start(info);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao suspender a maquina: {ex.Message}");
        }
    }
}
