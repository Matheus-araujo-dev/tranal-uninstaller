using System.IO;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// A familia de pasta pessoal do usuario.
/// </summary>
public enum PersonalFileKind
{
    /// <summary>Documentos.</summary>
    Documento,

    /// <summary>Imagens.</summary>
    Imagem,

    /// <summary>Videos.</summary>
    Video,

    /// <summary>Musicas.</summary>
    Musica,

    /// <summary>A area de trabalho.</summary>
    AreaDeTrabalho,

    /// <summary>A pasta de downloads.</summary>
    Downloads,

    /// <summary>Os favoritos do Windows.</summary>
    Favoritos,
}

/// <summary>
/// Um item que está em uma pasta pessoal do usuario.
/// </summary>
public sealed class PersonalFileGuardItem
{
    /// <summary>O caminho do item.</summary>
    public required string Path { get; init; }

    /// <summary>A familia da pasta em que o item esta.</summary>
    public required PersonalFileKind Kind { get; init; }

    /// <summary>O nome do arquivo.</summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// A guarda que impede que um arquivo pessoal do usuario seja apagado sem
/// que ele saiba.
///
/// O IObit Uninstall tem "confirm before deleting personal files": um
/// documento, uma foto ou um video do usuario nao e sobra do programa, e
/// apagar isso sem perguntar seria perder dado de quem nao pediu por isso.
///
/// A REGRA mora aqui, em um lugar so: o motor de remocao consulta esta
/// guarda e nao reimplementa a lista de pastas pessoais.
/// </summary>
public static class PersonalFileGuard
{
    /// <summary>As familias de pasta pessoal, na ordem de apresentacao.</summary>
    public static IReadOnlyList<PersonalFileKind> Kinds { get; } = new[]
    {
        PersonalFileKind.Documento,
        PersonalFileKind.Imagem,
        PersonalFileKind.Video,
        PersonalFileKind.Musica,
        PersonalFileKind.AreaDeTrabalho,
        PersonalFileKind.Downloads,
        PersonalFileKind.Favoritos,
    };

    /// <summary>
    /// A pasta raiz de uma familia pessoal, ou null quando o Windows nao
    /// informa (a conta nao tem essa pasta).
    /// </summary>
    /// <param name="kind">A familia.</param>
    public static string? RootOf(PersonalFileKind kind)
    {
        switch (kind)
        {
            case PersonalFileKind.Documento:
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            case PersonalFileKind.Imagem:
                return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            case PersonalFileKind.Video:
                return Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            case PersonalFileKind.Musica:
                return Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            case PersonalFileKind.AreaDeTrabalho:
                return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            case PersonalFileKind.Favoritos:
                return Environment.GetFolderPath(Environment.SpecialFolder.Favorites);
            case PersonalFileKind.Downloads:
                var perfil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return perfil.Length == 0 ? null : System.IO.Path.Combine(perfil, "Downloads");
            default:
                return null;
        }
    }

    /// <summary>
    /// A familia pessoal de um caminho, ou null quando o caminho nao esta em
    /// nenhuma delas.
    ///
    /// A comparacao e por prefixo de pasta: o arquivo que esta DENTRO de
    /// Documentos e pessoal, e a pasta Documentos em si tambem.
    /// </summary>
    /// <param name="path">O caminho a inspecionar.</param>
    public static PersonalFileKind? Classify(string? path)
    {
        var full = PathUtil.Normalize(path);
        if (full.Length == 0) return null;

        foreach (var kind in Kinds)
        {
            var root = RootOf(kind);
            if (string.IsNullOrWhiteSpace(root)) continue;

            var rootFull = PathUtil.Normalize(root);
            if (rootFull.Length == 0) continue;

            if (string.Equals(full, rootFull, StringComparison.OrdinalIgnoreCase)) return kind;

            if (full.StartsWith(rootFull + System.IO.Path.DirectorySeparatorChar,
                                StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        return null;
    }

    /// <summary>True quando o caminho esta em uma pasta pessoal do usuario.</summary>
    /// <param name="path">O caminho a inspecionar.</param>
    public static bool IsPersonal(string? path) => Classify(path) is not null;

    /// <summary>
    /// Os itens pessoais de uma lista de caminhos.
    /// </summary>
    /// <param name="paths">Os caminhos a inspecionar.</param>
    public static IReadOnlyList<PersonalFileGuardItem> Find(IEnumerable<string>? paths)
    {
        var items = new List<PersonalFileGuardItem>();
        if (paths is null) return items;

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;

            var kind = Classify(path);
            if (kind is null) continue;

            items.Add(new PersonalFileGuardItem
            {
                Path = path,
                Kind = kind.Value,
                Name = System.IO.Path.GetFileName(path),
            });
        }

        return items;
    }
}
