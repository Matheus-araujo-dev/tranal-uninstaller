using System.IO;
using System.Text.Json;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Um no da arvore de categorias.
/// </summary>
public sealed class ProgramCategoryNode
{
    internal ProgramCategoryNode(
        string id,
        string name,
        IReadOnlyList<ProgramCategoryNode> children,
        IReadOnlyList<InstalledProgram> programs)
    {
        Id = id;
        Name = name;
        Children = children;
        Programs = programs;
        TotalPrograms = programs.Count + children.Sum(c => c.TotalPrograms);
    }

    /// <summary>O identificador fixo da categoria.</summary>
    public string Id { get; }

    /// <summary>O nome da categoria.</summary>
    public string Name { get; }

    /// <summary>As subcategorias.</summary>
    public IReadOnlyList<ProgramCategoryNode> Children { get; }

    /// <summary>Os programas que estao direto nesta categoria.</summary>
    public IReadOnlyList<InstalledProgram> Programs { get; }

    /// <summary>Quantos programas tem nesta categoria, contando as subcategorias.</summary>
    public int TotalPrograms { get; }

    /// <summary>
    /// O no esta expandido? E o estado que o usuario ve: um no recolhido
    /// esconde os filhos.
    /// </summary>
    public bool IsExpanded { get; set; } = true;
}

/// <summary>
/// As atribuicoes de categoria que o usuario fez, por programa.
/// </summary>
internal sealed class CategoryAssignments
{
    public Dictionary<string, string> Assignments { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// A arvore de categorias automaticas dos programas.
///
/// A categoria de um programa e uma **classificacao**: ela vem do grupo que
/// o usuario criou, da loja de onde o programa veio (Steam, Epic...) ou do
/// que o proprio programa ja declara. Nunca e um palpite.
///
/// O que o Tranal acrescenta e a atribuicao automatica: o usuario pode
/// escrever a categoria de um programa uma vez, e ela fica guardada. O
/// "reset para sem categoria" apaga essas atribuicoes e devolve os
/// programas a classificacao automatica.
///
/// A regra do "o que e a categoria de um programa" esta em um lugar so, neste
/// modulo: a arvore, a tela e a linha de comando usam a mesma.
/// </summary>
public static class ProgramCategoryTree
{
    /// <summary>O identificador da categoria "sem categoria".</summary>
    public const string UnsortedId = "unsorted";

    /// <summary>
    /// Substitui a pasta de dados (so a suite de testes). Nulo em uso normal.
    /// </summary>
    internal static string? FolderOverride { get; set; }

    /// <summary>A pasta onde ficam as atribuicoes de categoria.</summary>
    public static string Folder =>
        FolderOverride ?? Path.Combine(RegistryBackup.DataDirectory, "categories");

    /// <summary>O arquivo das atribuicoes de categoria.</summary>
    public static string FilePath => Path.Combine(Folder, "assignments.json");

    /// <summary>
    /// O nome da categoria "sem categoria". Vem do catalogo de idiomas,
    /// porque e texto de tela.
    /// </summary>
    public static string UnsortedName
    {
        get
        {
            const string key = "Cat.Unsorted";
            var text = Localization.Loc.Get(key);
            return text == key ? "Sem categoria" : text;
        }
    }

    /// <summary>
    /// A categoria automatica de um programa.
    ///
    /// A ordem das regras: o grupo que o usuario criou tem a preferencia; a
    /// loja de origem vem depois; sem nenhuma das duas, o programa fica sem
    /// categoria.
    /// </summary>
    /// <param name="program">O programa.</param>
    /// <param name="customization">
    /// A preferencia do usuario do programa. Nulo usa a loja de preferencias.
    /// </param>
    public static string CategoryOf(
        InstalledProgram? program,
        ProgramCustomization? customization = null)
    {
        if (program is null) return UnsortedId;

        var custom = customization ?? ProgramCustomizationStore.For(program);

        if (custom is not null && custom.Groups.Count > 0)
        {
            var grupo = custom.Groups.FirstOrDefault(g => !string.IsNullOrWhiteSpace(g));
            if (!string.IsNullOrWhiteSpace(grupo)) return grupo!;
        }

        var atribuicao = Assignments.TryGetValue(ProgramCustomizationStore.KeyOf(program), out var escolhida)
            ? escolhida
            : null;

        if (!string.IsNullOrWhiteSpace(atribuicao)) return atribuicao!;

        var plataforma = ProgramColumns.PlatformOf(program);
        if (!string.IsNullOrWhiteSpace(plataforma)) return plataforma;

        return UnsortedId;
    }

    /// <summary>
    /// As atribuicoes guardadas. A chave e a identificacao do programa.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Assignments
    {
        get
        {
            try
            {
                if (!File.Exists(FilePath)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<CategoryAssignments>(json);
                if (loaded?.Assignments is null) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                return new Dictionary<string, string>(loaded.Assignments, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Guarda a categoria de um programa.
    /// </summary>
    /// <param name="programKey">A identificacao do programa.</param>
    /// <param name="categoryId">A categoria. A categoria "sem categoria" remove a atribuicao.</param>
    /// <returns>Verdadeiro quando a atribuicao ficou gravada.</returns>
    public static bool Assign(string programKey, string categoryId)
    {
        if (string.IsNullOrWhiteSpace(programKey)) return false;

        try
        {
            var atuais = new Dictionary<string, string>(Assignments, StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(categoryId) || categoryId == UnsortedId)
            {
                atuais.Remove(programKey);
            }
            else
            {
                atuais[programKey] = categoryId;
            }

            Directory.CreateDirectory(Folder);
            var json = JsonSerializer.Serialize(
                new CategoryAssignments { Assignments = atuais },
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(FilePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Apaga todas as atribuicoes: os programas voltam a classificacao
    /// automatica. E o "reset para sem categoria".
    /// </summary>
    /// <returns>Quantas atribuicoes foram apagadas.</returns>
    public static int ResetToUnsorted()
    {
        var quantas = Assignments.Count;

        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch
        {
            // O arquivo pode ficar para tras; o que importa e que a proxima
            // leitura nao encontra nenhuma atribuicao valida.
        }

        return quantas;
    }

    /// <summary>
    /// Monta a arvore de categorias.
    ///
    /// Os programas ficam na categoria que <see cref="CategoryOf"/> devolve. A
    /// ordem das categorias e a ordem de aparicao na lista — a lista que o
    /// usuario conhece nao muda de lugar por causa da arvore.
    /// </summary>
    /// <param name="programs">Os programas, na ordem da lista.</param>
    /// <param name="customization">
    /// Como achar a preferencia do usuario de cada programa. Nulo usa a loja
    /// de preferencias.
    /// </param>
    public static IReadOnlyList<ProgramCategoryNode> Build(
        IReadOnlyList<InstalledProgram>? programs,
        Func<InstalledProgram, ProgramCustomization?>? customization = null)
    {
        if (programs is null || programs.Count == 0) return Array.Empty<ProgramCategoryNode>();

        var resolver = customization ?? ProgramCustomizationStore.For;

        var ordem = new List<string>();
        var membros = new Dictionary<string, List<InstalledProgram>>(StringComparer.OrdinalIgnoreCase);

        foreach (var program in programs)
        {
            if (program is null) continue;

            var categoria = CategoryOf(program, resolver(program));
            if (string.IsNullOrWhiteSpace(categoria)) categoria = UnsortedId;

            if (!membros.TryGetValue(categoria, out var lista))
            {
                lista = new List<InstalledProgram>();
                membros[categoria] = lista;
                ordem.Add(categoria);
            }

            lista.Add(program);
        }

        var nos = new List<ProgramCategoryNode>(ordem.Count);
        foreach (var chave in ordem)
        {
            var nome = chave == UnsortedId ? UnsortedName : chave;
            nos.Add(new ProgramCategoryNode(chave, nome, Array.Empty<ProgramCategoryNode>(), membros[chave]));
        }

        return nos;
    }
}
