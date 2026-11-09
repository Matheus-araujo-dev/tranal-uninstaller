using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// A familia do arquivo, para o filtro de tipo na operacao de remocao.
/// </summary>
public enum RemovalFileKind
{
    /// <summary>Qualquer arquivo que nao caia nas outras familias.</summary>
    Other = 0,

    /// <summary>Icone (".ico").</summary>
    Icon = 1,

    /// <summary>Biblioteca (".dll", ".ocx", ".sys").</summary>
    Library = 2,

    /// <summary>Pagina web (".htm", ".html").</summary>
    WebPage = 3,

    /// <summary>Atalho (".lnk", ".url").</summary>
    Shortcut = 4,

    /// <summary>Executavel (".exe", ".com", ".bat", ".cmd").</summary>
    Executable = 5,
}

/// <summary>
/// O filtro de tipo de arquivo da operacao de remocao.
///
/// E o "file type filters" do Total Uninstall (".ico, .dll, .htm/.html,
/// .lnk/.exe") e o filtro de extensao do Uninstall Tool: o usuario escolhe
/// quais familias de arquivo a operacao PODE apagar.
///
/// A REGRA mora aqui, em um lugar so: a lista de sobras passa por este
/// filtro antes de ser removida, e o filtro diz o que e preservado. Nao ha
/// segunda copia da lista de extensoes em outro arquivo.
///
/// O padrao e NAO preservar nada: sem escolha do usuario, o comportamento e
/// o de sempre — tudo o que esta marcado e removido.
/// </summary>
public sealed class FileTypeFilter
{
    private readonly HashSet<RemovalFileKind> _preserved;

    /// <summary>
    /// Cria o filtro a partir das familias a preservar.
    /// </summary>
    /// <param name="preserved">As familias que nao podem ser removidas.</param>
    public FileTypeFilter(IEnumerable<RemovalFileKind>? preserved)
    {
        _preserved = preserved is null
            ? new HashSet<RemovalFileKind>()
            : new HashSet<RemovalFileKind>(preserved);
    }

    /// <summary>O filtro que nao preserva nada (o comportamento padrao).</summary>
    public static FileTypeFilter Empty { get; } = new(Array.Empty<RemovalFileKind>());

    /// <summary>As familias preservadas.</summary>
    public IReadOnlyCollection<RemovalFileKind> Preserved => _preserved;

    /// <summary>
    /// A familia do arquivo, pelo caminho.
    /// </summary>
    /// <param name="path">O caminho do arquivo.</param>
    public static RemovalFileKind Classify(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return RemovalFileKind.Other;

        string extensao;
        try
        {
            extensao = Path.GetExtension(path);
        }
        catch (ArgumentException)
        {
            return RemovalFileKind.Other;
        }

        return extensao.ToLowerInvariant() switch
        {
            ".ico" or ".icns" => RemovalFileKind.Icon,
            ".dll" or ".ocx" or ".sys" => RemovalFileKind.Library,
            ".htm" or ".html" or ".xhtml" => RemovalFileKind.WebPage,
            ".lnk" or ".url" => RemovalFileKind.Shortcut,
            ".exe" or ".com" or ".bat" or ".cmd" => RemovalFileKind.Executable,
            _ => RemovalFileKind.Other,
        };
    }

    /// <summary>
    /// True quando a familia do arquivo esta preservada.
    /// </summary>
    /// <param name="path">O caminho do arquivo.</param>
    public bool IsPreserved(string? path) => _preserved.Contains(Classify(path));

    /// <summary>
    /// True quando a familia esta preservada.
    /// </summary>
    /// <param name="kind">A familia.</param>
    public bool IsPreserved(RemovalFileKind kind) => _preserved.Contains(kind);

    /// <summary>
    /// Aplica o filtro a uma lista de sobras: o que esta preservado deixa de
    /// estar marcado para remocao.
    ///
    /// Devolve quantos itens foram preservados, para a interface poder dizer
    /// quantos ficaram de fora — em vez de sumir com eles em silencio.
    /// </summary>
    /// <param name="items">A lista de sobras.</param>
    /// <returns>Quantos itens foram preservados.</returns>
    public int Apply(IEnumerable<LeftoverItem>? items)
    {
        if (items is null || _preserved.Count == 0) return 0;

        var preservados = 0;

        foreach (var item in items)
        {
            if (item is null) continue;
            if (!string.Equals(item.Kind, "Arquivo", StringComparison.Ordinal)) continue;
            if (!IsPreserved(item.Path)) continue;

            if (item.Selected)
            {
                item.Selected = false;
                preservados++;
            }
        }

        return preservados;
    }

    /// <summary>
    /// Monta o filtro a partir dos nomes das familias guardados na
    /// preferencia.
    /// </summary>
    /// <param name="nomes">Os nomes das familias.</param>
    public static FileTypeFilter FromNames(IEnumerable<string>? nomes)
    {
        if (nomes is null) return Empty;

        var familias = new List<RemovalFileKind>();

        foreach (var nome in nomes)
        {
            if (string.IsNullOrWhiteSpace(nome)) continue;

            if (Enum.TryParse<RemovalFileKind>(nome.Trim(), ignoreCase: true, out var familia)
                && familia != RemovalFileKind.Other)
            {
                familias.Add(familia);
            }
        }

        return new FileTypeFilter(familias);
    }

    /// <summary>
    /// Os nomes das familias preservadas (para a preferencia).
    /// </summary>
    public IReadOnlyList<string> Names() =>
        _preserved.OrderBy(k => (int)k).Select(k => k.ToString()).ToList();
}
