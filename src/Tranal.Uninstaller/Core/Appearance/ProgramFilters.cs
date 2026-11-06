using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// Onde a regra de realce olha.
/// </summary>
public enum HighlightScope
{
    /// <summary>O nome exibido do programa.</summary>
    Name,

    /// <summary>A editora do programa.</summary>
    Publisher,

    /// <summary>A pasta de instalacao do programa.</summary>
    InstallLocation,

    /// <summary>O comando de desinstalacao do programa.</summary>
    UninstallString,
}

/// <summary>
/// Uma regra de realce: quando o padrao casa, o programa recebe a cor.
///
/// A regra mora aqui (e nao na tela): o painel lateral so edita o estado e a
/// janela principal consome este mesmo objeto, para que o filtro e o realce
/// nunca divirjam entre a lista e o painel.
/// </summary>
public sealed class HighlightRule
{
    /// <summary>O texto procurado. Padrao vazio nunca casa.</summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>Onde o padrao e procurado.</summary>
    public HighlightScope Scope { get; set; } = HighlightScope.Name;

    /// <summary>A chave da cor do realce (veja <see cref="HighlightPalette"/>).</summary>
    public string ColorKey { get; set; } = HighlightPalette.Warning;

    /// <summary>False quando a regra esta desligada e nao pinta nada.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// O texto do programa no escopo da regra (string.Empty se nao houver).
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    public string SubjectOf(InstalledProgram program)
    {
        if (program is null) return string.Empty;

        return Scope switch
        {
            HighlightScope.Publisher => program.Publisher ?? string.Empty,
            HighlightScope.InstallLocation => program.InstallLocation ?? string.Empty,
            HighlightScope.UninstallString => program.UninstallString ?? string.Empty,
            _ => program.DisplayName ?? string.Empty,
        };
    }

    /// <summary>
    /// True quando o padrao casa com o texto do escopo. Padrao vazio nunca casa.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    public bool Matches(InstalledProgram program)
    {
        if (program is null) return false;
        if (string.IsNullOrEmpty(Pattern)) return false;

        var subject = SubjectOf(program);
        if (string.IsNullOrEmpty(subject)) return false;

        return subject.Contains(Pattern, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// As cores de realce: um conjunto fixo de chaves.
///
/// A paleta e fixa (sempre as mesmas quatro cores) e a cor de cada uma mora
/// aqui: a tela e a janela principal leem a mesma cor, para que o realce
/// mostrado no painel seja exatamente o aplicado na lista.
/// </summary>
public static class HighlightPalette
{
    /// <summary>Aviso: a cor usada para chamar atencao.</summary>
    public const string Warning = "warning";

    /// <summary>Perigo: a cor usada para marcar o que nao deve ficar.</summary>
    public const string Danger = "danger";

    /// <summary>Sucesso: a cor usada para marcar o que pode ficar.</summary>
    public const string Success = "success";

    /// <summary>Destaque: a cor usada para marcar o que interessa.</summary>
    public const string Accent = "accent";

    private static readonly string[] Ordered =
    {
        Warning,
        Danger,
        Success,
        Accent,
    };

    /// <summary>Todas as chaves, na ordem de exibicao.</summary>
    public static IReadOnlyList<string> All => Ordered;

    /// <summary>
    /// A chave de idioma do rotulo da cor (ex.: "Sidebar.Highlight.Warning").
    /// </summary>
    /// <param name="colorKey">A chave da cor.</param>
    public static string LabelKeyOf(string colorKey) => colorKey switch
    {
        Danger => "Sidebar.Highlight.Danger",
        Success => "Sidebar.Highlight.Success",
        Accent => "Sidebar.Highlight.Accent",
        _ => "Sidebar.Highlight.Warning",
    };

    /// <summary>
    /// O hexadecimal da cor, com alfa (#AARRGGBB). Chave desconhecida =&gt; null.
    /// </summary>
    /// <param name="colorKey">A chave da cor.</param>
    public static string? HexOf(string colorKey) => colorKey switch
    {
        Warning => "#FFE3B341",
        Danger => "#FFE05C5C",
        Success => "#FF4CAF50",
        Accent => "#FF4C9AFF",
        _ => null,
    };
}

/// <summary>
/// O estado completo dos filtros da lista. A REGRA mora aqui e so aqui.
///
/// Quem filtra e quem pinta o realce consomem este objeto: o painel lateral
/// edita o estado, a janela principal reaplica o filtro na lista.
/// </summary>
public sealed class ProgramFilterSet
{
    private const long Megabyte = 1024L * 1024L;
    private const long Gigabyte = 1024L * Megabyte;

    /// <summary>
    /// O texto da busca. Casa com o nome, a editora, o comentario do registro
    /// e o comentario que o usuario escreveu. Vazio passa tudo.
    /// </summary>
    public string SearchText { get; set; } = string.Empty;

    /// <summary>All | 32 | 64 | msi.</summary>
    public string TypeFilter { get; set; } = "All";

    /// <summary>all | small | medium | large.</summary>
    public string SizeFilter { get; set; } = "all";

    /// <summary>A unidade (ex.: "C:\"). Null = todas.</summary>
    public string? DriveFilter { get; set; }

    /// <summary>O texto procurado no nome da editora. Vazio passa tudo.</summary>
    public string PublisherFilter { get; set; } = string.Empty;

    /// <summary>True quando so entram programas com comentario do usuario.</summary>
    public bool OnlyWithComments { get; set; }

    /// <summary>True quando so entram programas que estao em algum grupo.</summary>
    public bool OnlyWithGroups { get; set; }

    /// <summary>As regras de realce, na ordem em que foram criadas.</summary>
    public IList<HighlightRule> Highlights { get; } = new ObservableCollection<HighlightRule>();

    /// <summary>
    /// True quando o programa passa por TODOS os filtros ativos.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    public bool Matches(InstalledProgram program)
    {
        if (program is null) return false;

        // A anotacao do usuario e lida uma vez: a loja e um estado global e
        // busca-la de novo custaria uma varredura da lista inteira.
        var anotacao = CustomizationOf(program);

        if (!MatchesSearch(program, anotacao)) return false;
        if (!MatchesType(program)) return false;
        if (!MatchesSize(program)) return false;
        if (!MatchesDrive(program)) return false;
        if (!MatchesPublisher(program)) return false;

        if (OnlyWithComments && string.IsNullOrWhiteSpace(anotacao?.Comment)) return false;
        if (OnlyWithGroups && (anotacao is null || anotacao.Groups.Count == 0)) return false;

        return true;
    }

    /// <summary>
    /// A chave de cor da primeira regra habilitada que casa; null quando
    /// nenhuma casa. A primeira regra da lista vence.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    public string? HighlightColorOf(InstalledProgram program)
    {
        if (program is null) return null;

        foreach (var regra in Highlights)
        {
            if (regra is null || !regra.Enabled) continue;
            if (!regra.Matches(program)) continue;

            return regra.ColorKey;
        }

        return null;
    }

    /// <summary>True quando nenhum filtro esta ativo (e nao ha realce).</summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(SearchText)
        && string.Equals(TypeFilter, "All", StringComparison.Ordinal)
        && string.Equals(SizeFilter, "all", StringComparison.Ordinal)
        && string.IsNullOrEmpty(DriveFilter)
        && string.IsNullOrWhiteSpace(PublisherFilter)
        && !OnlyWithComments
        && !OnlyWithGroups
        && Highlights.Count == 0;

    /// <summary>Volta ao estado inicial (filtros vazios e sem realces).</summary>
    public void Clear()
    {
        SearchText = string.Empty;
        TypeFilter = "All";
        SizeFilter = "all";
        DriveFilter = null;
        PublisherFilter = string.Empty;
        OnlyWithComments = false;
        OnlyWithGroups = false;
        Highlights.Clear();
    }

    /// <summary>
    /// A busca casa com o nome, a editora, o comentario do registro e o
    /// comentario do usuario. Vazio passa tudo.
    /// </summary>
    private bool MatchesSearch(InstalledProgram program, ProgramCustomization? anotacao)
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return true;

        var termo = SearchText.Trim();

        if (Contains(program.DisplayName, termo)) return true;
        if (Contains(program.Publisher, termo)) return true;
        if (Contains(program.Comments, termo)) return true;

        return Contains(anotacao?.Comment, termo);
    }

    /// <summary>O filtro de tipo: All passa tudo.</summary>
    private bool MatchesType(InstalledProgram program) => TypeFilter switch
    {
        "32" => program.Bits == "32-bits",
        "64" => program.Bits == "64-bits",
        "msi" => program.IsWindowsInstaller,
        _ => true,
    };

    /// <summary>
    /// O filtro de tamanho: small ate 100 MB (sem os 100 MB), medium de
    /// 100 MB ate 1 GB (com os dois extremos), large acima de 1 GB.
    /// </summary>
    private bool MatchesSize(InstalledProgram program)
    {
        var size = program.SizeBytes;

        return SizeFilter switch
        {
            "small" => size > 0 && size < 100 * Megabyte,
            "medium" => size >= 100 * Megabyte && size <= Gigabyte,
            "large" => size > Gigabyte,
            _ => true,
        };
    }

    /// <summary>
    /// O filtro de unidade: a origem do programa (pasta de instalacao ou
    /// comando de desinstalacao) precisa comecar com a unidade.
    /// </summary>
    private bool MatchesDrive(InstalledProgram program)
    {
        if (string.IsNullOrEmpty(DriveFilter)) return true;

        var origem = !string.IsNullOrEmpty(program.InstallLocation)
            ? program.InstallLocation
            : program.UninstallString;

        if (string.IsNullOrEmpty(origem)) return false;

        return origem.StartsWith(DriveFilter, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>O filtro de editora: o nome da editora contem o texto.</summary>
    private bool MatchesPublisher(InstalledProgram program)
    {
        if (string.IsNullOrWhiteSpace(PublisherFilter)) return true;

        return Contains(program.Publisher, PublisherFilter.Trim());
    }

    /// <summary>
    /// A anotacao do usuario sobre o programa, ou null quando o programa
    /// nunca foi anotado. A loja e consultada sem gravar nada.
    /// </summary>
    private static ProgramCustomization? CustomizationOf(InstalledProgram program)
    {
        if (!ProgramCustomizationStore.Has(program)) return null;

        return ProgramCustomizationStore.For(program);
    }

    /// <summary>Contem o texto, sem diferenciar maiusculas/minusculas.</summary>
    private static bool Contains(string? texto, string termo)
    {
        if (string.IsNullOrEmpty(texto)) return false;

        return texto.Contains(termo, StringComparison.OrdinalIgnoreCase);
    }
}
