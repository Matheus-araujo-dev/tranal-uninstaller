using Tranal.Uninstaller.Core.Reporting;

namespace Tranal.Uninstaller.Core.Localization;

/// <summary>
/// Os rotulos localizados da arvore de alteracoes.
///
/// A interface grafica e a CLI consomem este metodo. Quando o texto da
/// categoria ou da mudanca vivesse em cada uma das duas, elas divergiriam na
/// primeira traducao esquecida — e a regra do projeto e que a interpretacao
/// fique em um lugar so.
/// </summary>
public static class ChangeTreeLabels
{
    /// <summary>
    /// O nome da categoria ("Arquivos", "Pastas", "Registro", "Valores").
    /// </summary>
    /// <param name="category">A categoria.</param>
    public static string Category(ChangeTreeCategory category) => Loc.Get(category switch
    {
        ChangeTreeCategory.Files => "ChangeTree.Category.Files",
        ChangeTreeCategory.Folders => "ChangeTree.Category.Folders",
        ChangeTreeCategory.Registry => "ChangeTree.Category.Registry",
        ChangeTreeCategory.Values => "ChangeTree.Category.Values",
        _ => "ChangeTree.Category.Files",
    });

    /// <summary>
    /// O rotulo de um no da arvore.
    ///
    /// A categoria e a mudanca sao traduzidas pelo catalogo; a alteracao em si
    /// (o caminho, a chave, o valor) e mostrada como e — um caminho nao se
    /// traduz.
    /// </summary>
    /// <param name="node">O no.</param>
    public static string Label(ChangeTreeNode node)
    {
        if (node is null) return string.Empty;
        if (node.LabelKey.Length == 0) return node.Item;

        var prefixo = node.Change.HasValue
            ? "ChangeTree.Change."
            : "ChangeTree.Category.";

        var nome = node.LabelKey switch
        {
            "folders" => "Folders",
            "registry" => "Registry",
            "values" => "Values",
            "added" => "Added",
            "modified" => "Modified",
            "removed" => "Removed",
            _ => "Files",
        };

        return Loc.Get(prefixo + nome);
    }

    /// <summary>
    /// O nome da mudanca ("Novo", "Modificado", "Removido").
    /// </summary>
    /// <param name="change">A mudanca.</param>
    public static string Change(ChangeTreeChange change) => Loc.Get(change switch
    {
        ChangeTreeChange.Added => "ChangeTree.Change.Added",
        ChangeTreeChange.Modified => "ChangeTree.Change.Modified",
        ChangeTreeChange.Removed => "ChangeTree.Change.Removed",
        _ => "ChangeTree.Change.Added",
    });
}
