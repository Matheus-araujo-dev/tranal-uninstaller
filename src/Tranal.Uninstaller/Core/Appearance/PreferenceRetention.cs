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
    ///
    /// O texto e em portugues porque e o que o usuario le; a tela traduz o
    /// equivalente pela chave <see cref="ItemIds"/>.
    /// </summary>
    /// <param name="program">O programa desinstalado.</param>
    public static IReadOnlyList<string> WhatWouldBeRemoved(InstalledProgram program) =>
        Items(program).Select(i => i.Text).ToList();

    /// <summary>
    /// Os identificadores do que seria apagado: "comment", "icon", "groups".
    ///
    /// A tela usa estes identificadores para montar o texto traduzido da
    /// pergunta; o texto em portugues e <see cref="WhatWouldBeRemoved"/>.
    /// </summary>
    /// <param name="program">O programa desinstalado.</param>
    public static IReadOnlyList<string> ItemIds(InstalledProgram program) =>
        Items(program).Select(i => i.Id).ToList();

    /// <summary>
    /// A lista unica do que existe para manter. Tudo que pergunta sobre as
    /// preferencias do programa passa por aqui.
    /// </summary>
    private static (string Id, string Text)[] Items(InstalledProgram program)
    {
        if (program is null) return Array.Empty<(string, string)>();

        var items = new List<(string, string)>();
        var customization = ProgramCustomizationStore.For(program);

        if (!string.IsNullOrWhiteSpace(customization.Comment)) items.Add(("comment", "comentário"));
        if (customization.IconIndex >= 0 || !string.IsNullOrWhiteSpace(customization.IconPath)) items.Add(("icon", "ícone"));
        if (customization.Groups.Count > 0) items.Add(("groups", "grupos"));

        return items.ToArray();
    }

    /// <summary>
    /// Aplica a decisao depois da desinstalacao.
    ///
    /// Quando o usuario nao quer manter, o comentario, o icone e os grupos do
    /// programa sao apagados. Quando quer, nada acontece: o que o usuario
    /// escreveu continua la para o caso de o programa voltar.
    ///
    /// E este o metodo que a interface e a linha de comando chamam: a regra
    /// fica aqui e os dois lados so dizem "mantem" ou "nao mantem".
    /// </summary>
    /// <param name="program">O programa que foi desinstalado.</param>
    /// <param name="keep">True para manter as preferencias.</param>
    /// <returns>True quando algo foi apagado.</returns>
    public static bool ApplyAfterUninstall(InstalledProgram program, bool keep)
    {
        if (keep) return false;
        return RemoveFor(program);
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
