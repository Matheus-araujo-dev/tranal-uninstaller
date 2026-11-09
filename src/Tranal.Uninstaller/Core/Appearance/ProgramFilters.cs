using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;

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
    /// Os sinais de uso lidos do Windows, guardados para nao reler o
    /// registro e a pasta Prefetch a cada programa da lista.
    /// </summary>
    private IReadOnlyList<UsageSignal>? _signals;

    /// <summary>
    /// Os sinais de uso ja lidos. A leitura acontece uma unica vez: o
    /// filtro de uso raro precisa do dado de todos os programas, e reler o
    /// registro a cada linha da lista travaria a janela.
    /// </summary>
    private IReadOnlyList<UsageSignal> Signals => _signals ??= UsageSignals.Collect();

    /// <summary>
    /// O texto da busca. Casa com o nome, a editora, o comentario do registro
    /// e o comentario que o usuario escreveu. Vazio passa tudo.
    /// </summary>
    public string SearchText { get; set; } = string.Empty;

    /// <summary>
    /// True quando a busca tambem casa por aproximacao (o termo "acrobat"
    /// acha "Adobe Acrobat Reader").
    ///
    /// Vale so para a busca; as exclusoes continuam casando pelo texto exato.
    /// </summary>
    public bool FuzzySearch { get; set; } = true;

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

    /// <summary>
    /// True quando so entram programas usados raramente.
    ///
    /// A regra do que conta como "uso raro" mora no nucleo
    /// (<see cref="UsageSignals"/>): o filtro so consome o veredito, sem
    /// repetir a janela de tempo nem a leitura dos sinais do Windows.
    /// </summary>
    public bool OnlyRarelyUsed { get; set; }

    /// <summary>
    /// O prefixo que marca o texto de busca como expressao regular.
    ///
    /// O texto "regex:^Steam" filtra por expressao regular; sem o prefixo,
    /// o texto continua sendo a busca literal de sempre.
    /// </summary>
    public const string RegexPrefix = "regex:";

    /// <summary>
    /// Os nomes dos programas que o usuario selecionou, quando a selecao
    /// foi transformada em filtro.
    ///
    /// Vazio = o filtro de selecao esta desligado.
    /// </summary>
    public ISet<string> SelectedNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>True quando a selecao virou filtro.</summary>
    public bool HasSelectionFilter => SelectedNames.Count > 0;

    /// <summary>
    /// Transforma a selecao atual em filtro: a lista passa a mostrar so os
    /// programas escolhidos.
    ///
    /// E o "Add the selected as filter" do BCU: a escolha deixa de ser uma
    /// marcacao e passa a ser um criterio.
    /// </summary>
    /// <param name="names">Os nomes dos programas escolhidos.</param>
    public void UseSelectionAsFilter(IEnumerable<string>? names)
    {
        SelectedNames.Clear();

        foreach (var nome in names ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(nome)) continue;
            SelectedNames.Add(nome.Trim());
        }
    }

    /// <summary>Desliga o filtro de selecao.</summary>
    public void ClearSelectionFilter() => SelectedNames.Clear();

    /// <summary>
    /// Termos de exclusao (negacoes): um programa que casa com um deles
    /// fica fora da lista, mesmo que case com a busca.
    ///
    /// O termo pode vir do proprio texto de busca ("jogo -demo") ou desta
    /// lista, que a tela lateral usa.
    /// </summary>
    public IList<string> ExcludedTerms { get; } = new ObservableCollection<string>();

    /// <summary>
    /// Os diagnosticos do filtro atual.
    ///
    /// Uma expressao regular invalida NAO derruba a lista: ela e ignorada e
    /// o problema fica registrado aqui, para a tela mostrar.
    /// </summary>
    public IList<string> Diagnostics { get; } = new ObservableCollection<string>();

    /// <summary>
    /// A expressao regular em uso, ou string vazia quando o filtro nao usa
    /// regex.
    /// </summary>
    public string RegexExpression => Query().Pattern;

    /// <summary>True quando a expressao regular do filtro e invalida.</summary>
    public bool HasInvalidRegex => Query().InvalidRegex;

    /// <summary>Os termos de exclusao em uso (busca e lista juntas).</summary>
    public IReadOnlyList<string> ExclusionTerms => Query().Exclusions;

    /// <summary>
    /// Coloca a busca em modo de expressao regular.
    /// </summary>
    /// <param name="pattern">A expressao regular.</param>
    public void SetRegex(string? pattern)
    {
        SearchText = string.IsNullOrWhiteSpace(pattern)
            ? string.Empty
            : RegexPrefix + pattern!.Trim();

        InvalidateQuery();
    }

    /// <summary>
    /// Acrescenta um termo de exclusao (negacao).
    /// </summary>
    /// <param name="term">O termo a excluir (o "-" inicial e dispensavel).</param>
    public void AddExclusion(string? term)
    {
        var text = (term ?? string.Empty).Trim().TrimStart('-');
        if (text.Length == 0) return;

        foreach (var existing in ExcludedTerms)
        {
            if (string.Equals(existing, text, StringComparison.OrdinalIgnoreCase)) return;
        }

        ExcludedTerms.Add(text);
        InvalidateQuery();
    }

    /// <summary>Remove todos os termos de exclusao.</summary>
    public void ClearExclusions()
    {
        ExcludedTerms.Clear();
        InvalidateQuery();
    }

    /// <summary>
    /// Valida o filtro: devolve false quando a expressao regular e invalida.
    /// O diagnostico fica em <see cref="Diagnostics"/>.
    /// </summary>
    public bool Validate() => !Query().InvalidRegex;

    /// <summary>
    /// A consulta montada a partir do texto de busca e dos termos de
    /// exclusao.
    ///
    /// E o unico lugar onde a busca e interpretada: a tela e a janela
    /// principal consomem este mesmo objeto.
    /// </summary>
    private sealed record FilterQuery(
        Regex? Regex,
        string Pattern,
        bool InvalidRegex,
        string[] Terms,
        string[] Exclusions);

    private FilterQuery? _query;
    private string? _queryKey;

    /// <summary>Descarta a consulta memorizada (o filtro mudou).</summary>
    private void InvalidateQuery()
    {
        _query = null;
        _queryKey = null;
    }

    /// <summary>
    /// A consulta em uso. Ela e calculada uma vez e reaproveitada: a lista
    /// filtra centenas de programas com a mesma busca.
    /// </summary>
    private FilterQuery Query()
    {
        var key = (SearchText ?? string.Empty) + "\u0001" + string.Join("\u0002", ExcludedTerms);
        if (_query is not null && string.Equals(_queryKey, key, StringComparison.Ordinal)) return _query;

        _queryKey = key;
        _query = BuildQuery();
        return _query;
    }

    /// <summary>
    /// Interpreta o texto de busca e os termos de exclusao.
    ///
    /// Tres formatos convivem:
    /// <list type="bullet">
    /// <item>"regex:expressao" — a busca e uma expressao regular;</item>
    /// <item>"palavra -termo" — "termo" e exclusao, e o resto e a busca;</item>
    /// <item>"palavra" — a busca literal de sempre.</item>
    /// </list>
    /// </summary>
    private FilterQuery BuildQuery()
    {
        Diagnostics.Clear();

        var text = (SearchText ?? string.Empty).Trim();
        var exclusions = new List<string>();

        foreach (var term in ExcludedTerms)
        {
            var value = (term ?? string.Empty).Trim().TrimStart('-');
            if (value.Length == 0) continue;
            if (exclusions.Contains(value, StringComparer.OrdinalIgnoreCase)) continue;

            exclusions.Add(value);
        }

        Regex? regex = null;
        var pattern = string.Empty;
        var invalid = false;
        string[] terms;

        if (text.StartsWith(RegexPrefix, StringComparison.OrdinalIgnoreCase))
        {
            pattern = text[RegexPrefix.Length..].Trim();
            terms = Array.Empty<string>();

            if (pattern.Length > 0)
            {
                try
                {
                    regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                }
                catch (ArgumentException ex)
                {
                    // Uma regex invalida e IGNORADA: ela nunca pode derrubar a
                    // lista inteira. O diagnostico fica para a tela mostrar.
                    invalid = true;
                    Diagnostics.Add($"Expressão regular ignorada (inválida): {ex.Message}");
                }
            }
        }
        else
        {
            var tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var positives = new List<string>();

            foreach (var token in tokens)
            {
                if (token.Length > 1 && token[0] == '-')
                {
                    var value = token[1..].Trim();
                    if (value.Length > 0 && !exclusions.Contains(value, StringComparer.OrdinalIgnoreCase))
                        exclusions.Add(value);

                    continue;
                }

                positives.Add(token);
            }

            // Sem exclusao, o texto inteiro continua sendo a busca: e o
            // comportamento de sempre, e mudar aqui quebraria o filtro que o
            // usuario ja conhece.
            terms = exclusions.Count == 0
                ? (text.Length > 0 ? new[] { text } : Array.Empty<string>())
                : positives.ToArray();
        }

        return new FilterQuery(regex, pattern, invalid, terms, exclusions.ToArray());
    }

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

        // As negacoes vem depois da busca: um programa que casa com um termo
        // de exclusao fica fora da lista mesmo tendo casado com a busca.
        if (!MatchesExclusions(program, anotacao)) return false;

        if (OnlyWithComments && string.IsNullOrWhiteSpace(anotacao?.Comment)) return false;
        if (OnlyWithGroups && (anotacao is null || anotacao.Groups.Count == 0)) return false;

        if (!MatchesRareUsage(program)) return false;

        // O filtro de selecao vem por ultimo: ele e a escolha do usuario
        // sobre a propria lista, e vale acima dos demais criterios.
        if (SelectedNames.Count > 0 && !SelectedNames.Contains(program.DisplayName ?? string.Empty))
            return false;

        return true;
    }

    /// <summary>
    /// True quando o programa passa pelo filtro de uso raro.
    ///
    /// A regra do que conta como "raro" mora no nucleo: o veredito e do
    /// <see cref="UsageSignal"/>, e aqui so se procura o sinal do
    /// executavel do programa.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    private bool MatchesRareUsage(InstalledProgram program)
    {
        if (!OnlyRarelyUsed) return true;

        var executavel = UsageSignals.ExecutableOf(program);
        if (executavel.Length == 0) return false;

        var sinal = Signals.FirstOrDefault(s =>
            string.Equals(s.ExecutablePath, executavel, StringComparison.OrdinalIgnoreCase));

        // Sem dado do Windows nao ha prova de uso raro: o programa fica de
        // fora do filtro, como o veredito do nucleo faz.
        if (sinal is null || !sinal.LastUsed.HasValue) return false;

        return sinal.IsRarelyUsed(UsageSignals.DefaultRarelyUsedWindow);
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
        && !OnlyRarelyUsed
        && ExcludedTerms.Count == 0
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
        OnlyRarelyUsed = false;
        ExcludedTerms.Clear();
        Highlights.Clear();
        InvalidateQuery();
    }

    /// <summary>
    /// A busca: por expressao regular, por termo, ou pelos dois.
    ///
    /// A busca casa com o nome, a editora, o comentario do registro e o
    /// comentario do usuario.
    /// </summary>
    private bool MatchesSearch(InstalledProgram program, ProgramCustomization? anotacao)
    {
        var query = Query();

        if (query.Regex is not null)
        {
            return MatchesRegex(query.Regex, program, anotacao);
        }

        // Regex invalida: e IGNORADA. Um texto mal digitado nao pode
        // derrubar a lista inteira — o diagnostico ja ficou registrado.
        if (query.InvalidRegex) return true;

        foreach (var termo in query.Terms)
        {
            if (!MatchesTerm(termo, program, anotacao) && !MatchesFuzzyTerm(termo, program, anotacao)) return false;
        }

        return true;
    }

    /// <summary>
    /// A busca difusa: o termo casa com o campo por aproximacao.
    ///
    /// Ela so vale para a busca, nunca para as exclusoes: um termo de
    /// exclusao que casasse por aproximacao tiraria da lista programas que
    /// o usuario pediu para manter.
    /// </summary>
    /// <param name="termo">O termo digitado.</param>
    /// <param name="program">O programa inspecionado.</param>
    /// <param name="anotacao">A anotacao do usuario sobre o programa.</param>
    private bool MatchesFuzzyTerm(string termo, InstalledProgram program, ProgramCustomization? anotacao)
    {
        if (!FuzzySearch) return false;
        if (termo.Length < Search.FuzzyMatch.MinimumTermLength) return false;

        return Search.FuzzyMatch.IsMatch(program.DisplayName, termo)
            || Search.FuzzyMatch.IsMatch(program.Publisher, termo)
            || Search.FuzzyMatch.IsMatch(program.Comments, termo)
            || Search.FuzzyMatch.IsMatch(anotacao?.Comment, termo);
    }

    /// <summary>
    /// As negacoes: um programa que casa com um termo de exclusao fica fora
    /// da lista, mesmo que case com a busca.
    /// </summary>
    private bool MatchesExclusions(InstalledProgram program, ProgramCustomization? anotacao)
    {
        var query = Query();

        foreach (var termo in query.Exclusions)
        {
            if (MatchesTerm(termo, program, anotacao)) return false;
        }

        return true;
    }

    /// <summary>True quando o texto casa com algum dos campos do programa.</summary>
    private static bool MatchesTerm(string termo, InstalledProgram program, ProgramCustomization? anotacao)
    {
        if (Contains(program.DisplayName, termo)) return true;
        if (Contains(program.Publisher, termo)) return true;
        if (Contains(program.Comments, termo)) return true;

        return Contains(anotacao?.Comment, termo);
    }

    /// <summary>True quando a expressao regular casa com algum campo.</summary>
    private static bool MatchesRegex(Regex regex, InstalledProgram program, ProgramCustomization? anotacao)
    {
        return regex.IsMatch(program.DisplayName ?? string.Empty)
            || regex.IsMatch(program.Publisher ?? string.Empty)
            || regex.IsMatch(program.Comments ?? string.Empty)
            || regex.IsMatch(anotacao?.Comment ?? string.Empty);
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
