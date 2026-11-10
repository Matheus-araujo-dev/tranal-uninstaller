using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Um grupo de sobras: a categoria (o no da arvore) e os itens que ficam
/// debaixo dela.
/// </summary>
/// <param name="Key">O identificador da categoria.</param>
/// <param name="Title">O nome da categoria, no idioma ativo.</param>
/// <param name="Items">Os itens da categoria.</param>
public sealed record LeftoverGroup(
    string Key,
    string Title,
    IReadOnlyList<LeftoverItem> Items)
{
    /// <summary>Quantos itens tem na categoria (o "%d items" do Geek).</summary>
    public int Count => Items.Count;
}

/// <summary>
/// As sobras agrupadas por categoria — a arvore que o Geek e o Revo mostram.
///
/// O Geek mostra duas categorias ("File System (%d items)" e "Registry (%d
/// items)"); o Tranal mostra as mesmas e qualquer outra que apareca, para
/// que nenhum item encontrado suma da tela.
///
/// A REGRA mora aqui, em um lugar so: a interface grafica e a linha de
/// comando consomem estes grupos e nunca montam os seus.
/// </summary>
public static class LeftoverGroups
{
    /// <summary>A chave da categoria "Sistema de arquivos".</summary>
    public const string FileSystemKey = "filesystem";

    /// <summary>A chave da categoria "Registro".</summary>
    public const string RegistryKey = "registry";

    /// <summary>
    /// Os grupos das sobras, na ordem: sistema de arquivos, registro e as
    /// outras categorias, na ordem em que apareceram.
    ///
    /// So as categorias com item entram: uma categoria vazia nao e uma
    /// categoria, e a tela nao deve mostrar um no que nao tem nada dentro.
    /// </summary>
    /// <param name="items">As sobras encontradas.</param>
    public static IReadOnlyList<LeftoverGroup> Build(IEnumerable<LeftoverItem>? items)
    {
        if (items is null) return Array.Empty<LeftoverGroup>();

        var grupos = new List<(string Key, string Title, List<LeftoverItem> Items)>();

        foreach (var item in items)
        {
            if (item is null) continue;

            var (chave, titulo) = CategoryOf(item);

            var grupo = grupos.FirstOrDefault(g => string.Equals(g.Key, chave, StringComparison.OrdinalIgnoreCase));
            if (grupo.Items is null)
            {
                grupo = (chave, titulo, new List<LeftoverItem>());
                grupos.Add(grupo);
            }

            grupo.Items.Add(item);
        }

        // A ordem e a do Geek: o sistema de arquivos primeiro, o registro
        // depois, e as categorias novas no fim.
        return grupos
            .OrderBy(g => OrderOf(g.Key))
            .Select(g => new LeftoverGroup(g.Key, g.Title, g.Items.ToList()))
            .ToList();
    }

    /// <summary>
    /// As sobras que casam com o texto procurado.
    ///
    /// A busca olha o caminho e o detalhe do item, sem distinguir maiuscula
    /// de minuscula. Texto vazio devolve tudo: e o que a busca apagada
    /// significa.
    /// </summary>
    /// <param name="items">As sobras encontradas.</param>
    /// <param name="texto">O texto procurado.</param>
    public static IReadOnlyList<LeftoverItem> Filter(
        IEnumerable<LeftoverItem>? items, string? texto)
    {
        if (items is null) return Array.Empty<LeftoverItem>();

        var lista = items.Where(i => i is not null).ToList();

        var busca = (texto ?? string.Empty).Trim();
        if (busca.Length == 0) return lista;

        return lista
            .Where(i => i.Path.IndexOf(busca, StringComparison.OrdinalIgnoreCase) >= 0
                || i.Detail.IndexOf(busca, StringComparison.OrdinalIgnoreCase) >= 0
                || i.Kind.IndexOf(busca, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();
    }

    /// <summary>
    /// A categoria de uma sobra: a chave e o nome que a tela mostra.
    /// </summary>
    /// <param name="item">A sobra.</param>
    private static (string Key, string Title) CategoryOf(LeftoverItem item)
    {
        var tipo = (item.Kind ?? string.Empty).Trim();

        if (tipo.Equals("Registro", StringComparison.OrdinalIgnoreCase))
            return (RegistryKey, Loc.Get("Leftovers.Group.Registry"));

        if (tipo.Equals("Pasta", StringComparison.OrdinalIgnoreCase)
            || tipo.Equals("Arquivo", StringComparison.OrdinalIgnoreCase)
            || tipo.Equals("File", StringComparison.OrdinalIgnoreCase)
            || tipo.Equals("Folder", StringComparison.OrdinalIgnoreCase))
        {
            return (FileSystemKey, Loc.Get("Leftovers.Group.FileSystem"));
        }

        // Uma categoria que o Tranal ainda nao conhece aparece com o nome do
        // proprio tipo: melhor um nome cru do que um item sem categoria.
        return (tipo.Length == 0 ? "outros" : tipo, tipo);
    }

    /// <summary>
    /// A ordem das categorias: sistema de arquivos, registro, e as outras
    /// (pela ordem em que apareceram).
    /// </summary>
    /// <param name="key">A chave da categoria.</param>
    private static int OrderOf(string key)
    {
        if (string.Equals(key, FileSystemKey, StringComparison.OrdinalIgnoreCase)) return 0;
        if (string.Equals(key, RegistryKey, StringComparison.OrdinalIgnoreCase)) return 1;
        return 2;
    }
}
