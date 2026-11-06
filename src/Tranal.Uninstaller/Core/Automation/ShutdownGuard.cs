using System.Runtime.InteropServices;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// Impede que o Windows desligue ou reinicie enquanto a tarefa roda.
///
/// Uma desinstalacao longa que o sistema apaga no meio deixa a maquina em
/// estado quebrado. Este guarda usa a API de execucao do proprio Windows
/// (<c>SetThreadExecutionState</c>), que e o mecanismo oficial para isso.
///
/// A guarda e descartavel: ao soltar, a solicitacao e revogada e o Windows
/// volta a poder desligar normalmente.
///
/// O Revo Uninstaller Pro 5.5.4 tem o mesmo cuidado — a evidencia esta no
/// binario dele, na secao "[Uninstaller Options]" da tabela de idioma.
/// </summary>
public sealed class ShutdownGuard : IDisposable
{
    private const uint ES_CONTINUOUS = 0x80000000;
    private const uint ES_SYSTEM_REQUIRED = 0x00000001;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;
    private const uint ES_AWAYMODE_REQUIRED = 0x00000040;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);

    private bool _released;

    private ShutdownGuard(uint flags)
    {
        Flags = flags;
    }

    /// <summary>As flags que esta guarda pediu ao Windows.</summary>
    public uint Flags { get; }

    /// <summary>
    /// True enquanto a guarda esta valendo.
    /// </summary>
    public bool IsActive => !_released;

    /// <summary>
    /// Pede ao Windows que nao desligue nem reinicie.
    /// </summary>
    /// <param name="keepDisplayOn">
    /// Quando true, tambem mantem a tela acesa. Desligar a tela e uma coisa
    /// diferente de desligar a maquina: em uma desinstalacao longa, a tela
    /// pode apagar sozinha sem atrapalhar.
    /// </param>
    /// <param name="apply">
    /// Como aplicar as flags. Por padrao, a API do Windows. Existe para os
    /// testes nao dependerem do sistema.
    /// </param>
    /// <returns>A guarda, ou nulo quando o Windows recusou a solicitacao.</returns>
    public static ShutdownGuard? Acquire(
        bool keepDisplayOn = false, Func<uint, uint>? apply = null)
    {
        var flags = ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_AWAYMODE_REQUIRED;
        if (keepDisplayOn) flags |= ES_DISPLAY_REQUIRED;

        var setter = apply ?? SetThreadExecutionState;
        uint result;
        try
        {
            result = setter(flags);
        }
        catch (DllNotFoundException)
        {
            return null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }

        // O Windows devolve o estado anterior. Zero significa que a
        // solicitacao foi recusada.
        if (result == 0) return null;

        return new ShutdownGuard(flags);
    }

    /// <summary>
    /// Revoga a solicitacao: o Windows volta a poder desligar e reiniciar.
    /// </summary>
    public void Dispose()
    {
        if (_released) return;
        _released = true;

        try
        {
            SetThreadExecutionState(ES_CONTINUOUS);
        }
        catch (DllNotFoundException)
        {
            // Nada a fazer: sem a API nao havia guarda nenhuma.
        }
        catch (EntryPointNotFoundException)
        {
            // Idem.
        }
    }
}
