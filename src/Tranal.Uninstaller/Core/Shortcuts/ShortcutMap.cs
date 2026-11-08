using System;
using System.Collections.Generic;
using System.Linq;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// O que um atalho de teclado faz.
/// </summary>
public enum ShortcutAction
{
    /// <summary>Vai para o campo de busca.</summary>
    FocusSearch,

    /// <summary>Refaz a varredura da lista de programas.</summary>
    Rescan,

    /// <summary>Seleciona todos os programas da lista.</summary>
    SelectAll,

    /// <summary>Desinstala os programas selecionados.</summary>
    Uninstall,

    /// <summary>Desinstala os programas selecionados sem perguntar.</summary>
    QuickUninstall,

    /// <summary>Abre a exportacao da lista.</summary>
    ExportList,

    /// <summary>Abre as opcoes.</summary>
    Options,

    /// <summary>Abre o modulo de descoberta (o que o Windows esconde).</summary>
    Discovery,

    /// <summary>Abre a ajuda.</summary>
    Support,

    /// <summary>Limpa a busca e o filtro.</summary>
    ClearFilter,
}

/// <summary>
/// Um atalho de teclado: a combinação de teclas e o que ela faz.
/// </summary>
/// <param name="Gesture">A combinação, no formato "Ctrl+F".</param>
/// <param name="Action">O que o atalho faz.</param>
/// <param name="DescriptionKey">A chave de idioma do texto que descreve o atalho.</param>
public sealed record ShortcutBinding(string Gesture, ShortcutAction Action, string DescriptionKey);

/// <summary>
/// Os atalhos de teclado do Tranal.
///
/// A tabela mora aqui, em um lugar so: a janela principal so traduz a tecla
/// pressionada para o nome da combinacao e pergunta o que fazer. Assim a
/// lista de atalhos pode ser conferida por teste e mostrada na ajuda sem
/// duplicar regra em dois lugares.
/// </summary>
public static class ShortcutMap
{
    /// <summary>
    /// Todos os atalhos, na ordem em que aparecem na ajuda.
    /// </summary>
    public static IReadOnlyList<ShortcutBinding> All { get; } = new[]
    {
        new ShortcutBinding("Ctrl+F", ShortcutAction.FocusSearch, "Shortcut.FocusSearch"),
        new ShortcutBinding("F5", ShortcutAction.Rescan, "Shortcut.Rescan"),
        new ShortcutBinding("Ctrl+A", ShortcutAction.SelectAll, "Shortcut.SelectAll"),
        new ShortcutBinding("Delete", ShortcutAction.Uninstall, "Shortcut.Uninstall"),
        new ShortcutBinding("Shift+Delete", ShortcutAction.QuickUninstall, "Shortcut.QuickUninstall"),
        new ShortcutBinding("Ctrl+E", ShortcutAction.ExportList, "Shortcut.ExportList"),
        new ShortcutBinding("Ctrl+OemComma", ShortcutAction.Options, "Shortcut.Options"),
        new ShortcutBinding("Ctrl+D", ShortcutAction.Discovery, "Shortcut.Discovery"),
        new ShortcutBinding("F1", ShortcutAction.Support, "Shortcut.Support"),
        new ShortcutBinding("Escape", ShortcutAction.ClearFilter, "Shortcut.ClearFilter"),
    };

    /// <summary>
    /// Monta o nome da combinação a partir da tecla e dos modificadores.
    ///
    /// A ordem e sempre a mesma (Ctrl, Shift, Alt e depois a tecla), porque
    /// e assim que o usuario le o atalho na ajuda.
    /// </summary>
    /// <param name="key">O nome da tecla.</param>
    /// <param name="ctrl">True quando o Ctrl esta pressionado.</param>
    /// <param name="shift">True quando o Shift esta pressionado.</param>
    /// <param name="alt">True quando o Alt esta pressionado.</param>
    public static string GestureOf(string? key, bool ctrl = false, bool shift = false, bool alt = false)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;

        var partes = new List<string>(3);
        if (ctrl) partes.Add("Ctrl");
        if (shift) partes.Add("Shift");
        if (alt) partes.Add("Alt");
        partes.Add(key.Trim());

        return string.Join("+", partes);
    }

    /// <summary>
    /// O atalho que corresponde a uma combinação, ou null quando a
    /// combinação nao esta na tabela.
    /// </summary>
    /// <param name="gesture">A combinação, no formato devolvido por
    /// <see cref="GestureOf"/>.</param>
    public static ShortcutBinding? Find(string? gesture)
    {
        if (string.IsNullOrWhiteSpace(gesture)) return null;

        return All.FirstOrDefault(b => string.Equals(b.Gesture, gesture, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// O atalho de uma combinação, procurando tambem a variante sem
    /// modificadores extras. Serve para o "Delete" valer tanto sozinho
    /// quanto com o Shift (desinstalacao rapida).
    /// </summary>
    /// <param name="gesture">A combinação pressionada.</param>
    /// <param name="action">A ação pedida.</param>
    public static bool Matches(string? gesture, ShortcutAction action)
    {
        var direto = Find(gesture);
        if (direto is not null) return direto.Action == action;

        return false;
    }
}
