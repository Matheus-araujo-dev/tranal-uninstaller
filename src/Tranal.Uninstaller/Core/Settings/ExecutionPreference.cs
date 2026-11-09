using System;
using System.Collections.Generic;

namespace Tranal.Uninstaller.Core.Settings;

/// <summary>
/// O modo de execucao e de exibicao do Tranal.
///
/// A REGRA mora aqui, em um lugar so: o enumerado, a conversao para texto e
/// a leitura do texto vivem juntos. Quem mostra o seletor, quem guarda a
/// preferencia e quem executa consomem estes metodos — e nao cada um a sua
/// propria traducao, que e como os modos comecam a divergir.
///
/// Os quatro estados vem de tres concorrentes medidos:
/// - "always / never / ask" (Uninstall Tool 3.8.2): o que fazer quando a
///   desinstalacao comeca.
/// - "system / always / never" (Uninstalr): o modo de exibicao.
/// - O padrao do sistema, que e o estado <see cref="System"/>.
/// </summary>
public enum ExecutionPreference
{
    /// <summary>
    /// O sistema decide (o padrao). O Tranal nao muda o que o Windows faz
    /// por conta propria.
    /// </summary>
    System = 0,

    /// <summary>Sempre: o Tranal age sem perguntar.</summary>
    Always = 1,

    /// <summary>Nunca: o Tranal nao age sozinho.</summary>
    Never = 2,

    /// <summary>Pergunta antes de agir.</summary>
    Ask = 3,
}

/// <summary>
/// A conversao entre o modo e o texto guardado na preferencia.
///
/// A regra e uma so: o texto e o nome do estado em minusculas. O que nao
/// for reconhecido vira <see cref="ExecutionPreference.System"/>, que e o
/// comportamento medido do Windows quando o Tranal nao diz nada.
/// </summary>
public static class ExecutionPreferences
{
    /// <summary>Todos os modos, na ordem em que aparecem na interface.</summary>
    public static IReadOnlyList<ExecutionPreference> All { get; } = new[]
    {
        ExecutionPreference.System,
        ExecutionPreference.Always,
        ExecutionPreference.Never,
        ExecutionPreference.Ask,
    };

    /// <summary>
    /// O texto do modo (como vai para a preferencia e para a interface).
    /// </summary>
    /// <param name="preference">O modo.</param>
    public static string Text(ExecutionPreference preference) => preference switch
    {
        ExecutionPreference.Always => "always",
        ExecutionPreference.Never => "never",
        ExecutionPreference.Ask => "ask",
        _ => "system",
    };

    /// <summary>
    /// O modo a partir do texto. Texto desconhecido vira
    /// <see cref="ExecutionPreference.System"/>.
    /// </summary>
    /// <param name="text">O texto guardado.</param>
    public static ExecutionPreference Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return ExecutionPreference.System;

        return text.Trim().ToLowerInvariant() switch
        {
            "always" or "sempre" => ExecutionPreference.Always,
            "never" or "nunca" => ExecutionPreference.Never,
            "ask" or "perguntar" => ExecutionPreference.Ask,
            _ => ExecutionPreference.System,
        };
    }

    /// <summary>
    /// O texto normalizado de um modo guardado.
    /// </summary>
    /// <param name="text">O texto guardado.</param>
    public static string Normalize(string? text) => Text(Parse(text));
}
