using System;
using System.Collections.Generic;
using System.Linq;
using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// A decisao de manter ou apagar as preferencias de um programa que foi
/// desinstalado.
///
/// O Revo pergunta: "Do you want to keep your preferences? Icons, comments,
/// groups" — e a pergunta e justa: o comentario, o icone e o grupo sao
/// **do usuario**, nao do programa. Apagar o programa nao tem por que apagar
/// o que o usuario escreveu.
///
/// A decisao mora no nucleo, e a tela so pergunta e aplica. E a gravacao usa
/// a API publica das preferencias por programa — nada de mexer no arquivo
/// `program-data.json` por fora.
/// </summary>
public static class PreferenceRetention
{
    /// <summary>
    /// O que seria apagado se o usuario escolher nao manter.
    /// </summary>
    /// <param name="program">O programa desinstalado.</param>
    public static IReadOnlyList<string> WhatWouldBeRemoved(InstalledProgram program)
    {
        if (program is null) return Array.Empty<string>();

        var items = new List<string>();
        var customization = ProgramCustomizationStore.For(program);

        if (!string.IsNullOrWhiteSpace(customization.Comment)) items.Add("comentário");
        if (customization.IconIndex >= 0 || !string.IsNullOrWhiteSpace(customization.IconPath)) items.Add("ícone");
        if (customization.Groups.Count > 0) items.Add("grupos");

        return items;
    }

    /// <summary>
    /// True quando o programa tem alguma preferencia guardada.
    /// </summary>
    public static bool HasPreferences(InstalledProgram program) =>
        WhatWouldBeRemoved(program).Count > 0;

    /// <summary>
    /// Apaga as preferencias do programa.
    ///
    /// Cada parte e apagada pela API publica: o comentario fica vazio, o
    /// icone volta ao padrao e o programa sai de cada grupo. O arquivo das
    /// preferencias e gravado uma vez, no fim.
    /// </summary>
    /// <param name="program">O programa cujas preferencias serao apagadas.</param>
    /// <returns>True quando algo foi apagado e gravado.</returns>
    public static bool RemoveFor(InstalledProgram program)
    {
        if (program is null) return false;

        var customization = ProgramCustomizationStore.For(program);
        var changed = false;

        if (!string.IsNullOrWhiteSpace(customization.Comment))
        {
            if (ProgramCustomizationStore.SetComment(program, string.Empty)) changed = true;
        }

        if (customization.IconIndex >= 0 || !string.IsNullOrWhiteSpace(customization.IconPath))
        {
            if (ProgramCustomizationStore.SetIcon(program, -1, string.Empty)) changed = true;
        }

        foreach (var group in customization.Groups.ToList())
        {
            if (ProgramCustomizationStore.RemoveFromGroup(program, group)) changed = true;
        }

        if (!changed) return false;

        return ProgramCustomizationStore.Save();
    }

    /// <summary>
    /// A pergunta, montada com o que seria apagado.
    ///
    /// O texto mora no nucleo (a chave do catalogo) e a tela mostra. E o
    /// mesmo texto que a linha de comando imprimiria.
    /// </summary>
    public static string Question(InstalledProgram program)
    {
        var items = WhatWouldBeRemoved(program);
        if (items.Count == 0) return string.Empty;

        return items.Count switch
        {
            1 => $"Deseja manter o {items[0]} de {program.DisplayName}?",
            2 => $"Deseja manter o {items[0]} e o {items[1]} de {program.DisplayName}?",
            _ => $"Deseja manter as preferências ({string.Join(", ", items)}) de {program.DisplayName}?",
        };
    }
}
