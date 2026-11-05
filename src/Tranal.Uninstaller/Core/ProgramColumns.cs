using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Core;

/// <summary>As colunas da lista de programas.</summary>
public enum ProgramColumn
{
    /// <summary>Nome do programa (sempre a primeira coluna).</summary>
    Name,

    /// <summary>Tamanho estimado.</summary>
    Size,

    /// <summary>Versao.</summary>
    Version,

    /// <summary>32 ou 64 bits.</summary>
    Type,

    /// <summary>Data da instalacao.</summary>
    Date,

    /// <summary>Empresa.</summary>
    Company,

    /// <summary>Pagina inicial.</summary>
    HomePage,

    /// <summary>Comentarios do instalador (vem do registro).</summary>
    Comments,

    /// <summary>Comando de desinstalacao.</summary>
    UninstallString,

    /// <summary>
    /// Tipo do desinstalador (Windows Installer, Inno Setup, NSIS...).
    /// O valor e o identificador fixo do tipo, independente do idioma.
    /// </summary>
    UninstallerKind,

    /// <summary>Codigo do produto (GUID) do Windows Installer.</summary>
    ProductCode,

    /// <summary>Grupo criado pelo usuario (15072: "Group").</summary>
    Group,

    /// <summary>Anotacao do usuario sobre o programa (15216: "Add Comment").</summary>
    Note,

    /// <summary>Chave do registro de desinstalacao (a identidade do programa).</summary>
    RegistryKey,
}

/// <summary>
/// A descricao de uma coluna: como ela se chama na tela e como ela se chama
/// no arquivo exportado.
///
/// O rotulo da tela vem do catalogo de idiomas (a interface e traduzida); o
/// nome da exportacao e fixo, para que o arquivo exportado continue legivel
/// por qualquer ferramenta, independentemente do idioma da interface.
/// </summary>
public sealed record ProgramColumnInfo(
    ProgramColumn Id,
    string LabelKey,
    string FallbackLabel,
    string ExportName);

/// <summary>
/// O catalogo de colunas da lista de programas.
///
/// Este e o unico lugar que sabe quais colunas existem, como elas se chamam
/// e que valor elas tem. A lista da tela e a exportacao consomem este
/// catalogo — nao existe uma segunda lista de colunas em cada um dos dois
/// lugares.
/// </summary>
public static class ProgramColumns
{
    private static readonly ProgramColumnInfo[] Catalogo =
    {
        new(ProgramColumn.Name, "Common.Program", "Programa", "Program"),
        new(ProgramColumn.Size, "Common.Size", "Tamanho", "Size"),
        new(ProgramColumn.Version, "Common.Version", "Versão", "Version"),
        new(ProgramColumn.Type, "Common.Type", "Tipo", "Type"),
        new(ProgramColumn.Date, "Common.Date", "Data", "Date"),
        new(ProgramColumn.Company, "Common.Publisher", "Empresa", "Company"),
        new(ProgramColumn.HomePage, "Common.Homepage", "Página inicial", "HomePage"),
        new(ProgramColumn.Comments, "Common.Comments", "Comentários", "Comments"),
        new(ProgramColumn.UninstallString, "Common.UninstallString", "Desinstalador", "UninstallString"),
        new(ProgramColumn.UninstallerKind, "Common.UninstallerKind", "Tipo de desinstalador", "UninstallerKind"),
        new(ProgramColumn.ProductCode, "Common.ProductCode", "Código do produto", "ProductCode"),
        new(ProgramColumn.Group, "Common.Group", "Grupo", "Group"),
        new(ProgramColumn.Note, "Common.Note", "Anotação", "Note"),
        new(ProgramColumn.RegistryKey, "Common.RegistryKey", "Chave do registro", "RegistryKey"),
    };

    /// <summary>Todas as colunas, na ordem em que aparecem na lista.</summary>
    public static IReadOnlyList<ProgramColumnInfo> All => Catalogo;

    /// <summary>A descricao de uma coluna.</summary>
    public static ProgramColumnInfo Info(ProgramColumn id) =>
        Catalogo.FirstOrDefault(c => c.Id == id) ?? Catalogo[0];

    /// <summary>
    /// O rotulo traduzido da coluna. O catalogo de idiomas e a fonte: quando
    /// a chave nao existe no idioma ativo, o rotulo de reserva entra.
    /// </summary>
    public static string Label(ProgramColumn id)
    {
        var info = Info(id);
        var text = Loc.Get(info.LabelKey);
        return text == info.LabelKey ? info.FallbackLabel : text;
    }

    /// <summary>
    /// O valor da coluna para um programa. E o mesmo valor que a lista
    /// mostra e que a exportacao grava — uma coluna, uma interpretacao.
    /// </summary>
    public static string Value(ProgramColumn id, InstalledProgram program, ProgramCustomization? custom)
    {
        switch (id)
        {
            case ProgramColumn.Name: return program.DisplayName;
            case ProgramColumn.Size: return program.SizeText;
            case ProgramColumn.Version: return program.VersionOrNoVersion;
            case ProgramColumn.Type: return program.Bits;
            case ProgramColumn.Date: return program.InstallDate;
            case ProgramColumn.Company: return program.Publisher;
            case ProgramColumn.HomePage: return program.UrlInfoAbout;
            case ProgramColumn.Comments: return program.Comments;
            case ProgramColumn.UninstallString: return program.UninstallString;
            case ProgramColumn.UninstallerKind: return program.Kind.ToString();
            case ProgramColumn.ProductCode: return program.ProductCode;
            case ProgramColumn.Group: return GroupOf(custom);
            case ProgramColumn.Note: return custom?.Comment ?? string.Empty;
            case ProgramColumn.RegistryKey: return program.RegistryKey;
            default: return string.Empty;
        }
    }

    /// <summary>Os grupos do programa, separados por virgula.</summary>
    public static string GroupOf(ProgramCustomization? custom)
    {
        if (custom is null || custom.Groups.Count == 0) return string.Empty;
        return string.Join(", ", custom.Groups);
    }

    /// <summary>
    /// As colunas pedidas por nome.
    ///
    /// O nome aceito é o do arquivo exportado (<see cref="ProgramColumnInfo.ExportName"/>),
    /// fixo e independente do idioma: é o que torna o comando roteirizável.
    /// A ordem do catálogo manda — o arquivo sai sempre na mesma ordem.
    ///
    /// Devolve false quando algum nome não existe no catálogo.
    /// </summary>
    public static bool TryParse(
        IEnumerable<string> names,
        out IReadOnlyList<ProgramColumn> columns,
        out string unknown)
    {
        columns = Array.Empty<ProgramColumn>();
        unknown = string.Empty;

        var escolhidas = new List<ProgramColumn>();

        foreach (var nome in names)
        {
            var texto = (nome ?? string.Empty).Trim();
            if (texto.Length == 0) continue;

            var coluna = Catalogo.FirstOrDefault(c =>
                string.Equals(c.ExportName, texto, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Id.ToString(), texto, StringComparison.OrdinalIgnoreCase));

            if (coluna is null)
            {
                unknown = texto;
                return false;
            }

            if (!escolhidas.Contains(coluna.Id)) escolhidas.Add(coluna.Id);
        }

        if (escolhidas.Count == 0)
        {
            unknown = string.Empty;
            return false;
        }

        columns = Catalogo.Where(c => escolhidas.Contains(c.Id)).Select(c => c.Id).ToList();
        return true;
    }

    /// <summary>
    /// As colunas que aparecem na lista: todas menos as escondidas nas
    /// preferencias. A coluna do nome nunca some — sem ela a lista perde o
    /// sentido.
    /// </summary>
    public static IReadOnlyList<ProgramColumn> Visible(IReadOnlyCollection<string>? hidden)
    {
        var result = new List<ProgramColumn>();
        foreach (var info in Catalogo)
        {
            if (info.Id == ProgramColumn.Name) { result.Add(info.Id); continue; }

            // A comparacao ignora a caixa: a preferencia pode ter sido
            // editada a mao, e "size" tem de valer como "Size".
            if (hidden is not null
                && hidden.Any(h => string.Equals(h, info.Id.ToString(), StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            result.Add(info.Id);
        }

        return result;
    }
}
