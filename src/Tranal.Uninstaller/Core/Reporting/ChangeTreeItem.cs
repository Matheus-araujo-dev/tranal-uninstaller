namespace Tranal.Uninstaller.Core.Reporting;

/// <summary>
/// A mudanca que uma alteracao representa.
/// </summary>
public enum ChangeTreeChange
{
    /// <summary>O item apareceu.</summary>
    Added,

    /// <summary>O item mudou de conteudo.</summary>
    Modified,

    /// <summary>O item sumiu.</summary>
    Removed,
}

/// <summary>
/// A categoria do item alterado.
/// </summary>
public enum ChangeTreeCategory
{
    /// <summary>Arquivos.</summary>
    Files,

    /// <summary>Pastas.</summary>
    Folders,

    /// <summary>Chaves de registro.</summary>
    Registry,

    /// <summary>Valores de registro.</summary>
    Values,
}

/// <summary>
/// Uma alteracao, de forma estruturada.
///
/// A tabela (o que sai exportado e impresso) e a arvore (o que a tela
/// mostra) sao **a mesma** alteracao em duas formas: por isso que o item
/// estruturado mora aqui, e que a tabela deriva dele. Sem isso, a arvore
/// teria a sua propria classificacao e as duas poderiam divergir.
/// </summary>
public sealed class ChangeTreeItem
{
    /// <summary>A categoria do item (arquivo, pasta, registro, valor).</summary>
    public required ChangeTreeCategory Category { get; init; }

    /// <summary>A mudanca (apareceu, mudou, sumiu).</summary>
    public required ChangeTreeChange Change { get; init; }

    /// <summary>
    /// O texto da mudanca, como a tabela mostra ("Novo", "Nova chave",
    /// "Modificado", "Chave removida"...).
    /// </summary>
    public required string ChangeText { get; init; }

    /// <summary>
    /// O tipo do item, como a tabela mostra ("Arquivo", "Pasta", "Registro",
    /// "Valor").
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>O caminho, a chave ou o valor.</summary>
    public required string Item { get; init; }

    /// <summary>O detalhe (o tamanho, o valor antigo e o novo).</summary>
    public string Detail { get; init; } = string.Empty;
}
