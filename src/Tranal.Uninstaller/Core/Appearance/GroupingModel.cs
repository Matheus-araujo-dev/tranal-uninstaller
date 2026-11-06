using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// Um grupo de programas, formado por uma coluna da lista.
///
/// O grupo e uma **classificacao**, nao uma decisao: ele junta programas que
/// compartilham o mesmo valor numa coluna (o mesmo tipo de desinstalador, a
/// mesma empresa, a mesma data) e nao mexe em nada.
/// </summary>
public sealed class ProgramGroup
{
    internal ProgramGroup(ProgramColumn column, string name, IReadOnlyList<InstalledProgram> programs)
    {
        Column = column;
        Name = name;
        Programs = programs;

        long total = 0;
        foreach (var program in programs) total += program.SizeBytes;
        TotalSize = total;
    }

    /// <summary>A coluna que formou o grupo.</summary>
    public ProgramColumn Column { get; }

    /// <summary>
    /// O nome do grupo: o valor da coluna. Quando o programa nao tem dado
    /// nessa coluna, o nome e o texto de "sem valor" — nunca um palpite.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Os programas do grupo, na ordem da lista original: agrupar nao pode
    /// baguncar a ordem que o usuario conhece.
    /// </summary>
    public IReadOnlyList<InstalledProgram> Programs { get; }

    /// <summary>Quantos programas tem no grupo.</summary>
    public int Count => Programs.Count;

    /// <summary>
    /// O tamanho somado dos programas do grupo, em bytes. E o que ordena os
    /// grupos: o grupo que ocupa mais espaco vem primeiro.
    /// </summary>
    public long TotalSize { get; }
}

/// <summary>
/// O agrupamento automatico por coluna.
///
/// O modelo recebe a lista de programas e a coluna escolhida e devolve os
/// grupos. Ele nao conhece os valores: eles vem de
/// <see cref="ProgramColumns.Value"/> — a mesma fonte que a lista e a
/// exportacao usam. Se o agrupamento calculasse o valor por conta propria, a
/// lista e o agrupamento diriam coisas diferentes sobre o mesmo programa.
///
/// A ordenacao dos grupos e por tamanho total, decrescente: o grupo que
/// ocupa mais espaco vem primeiro. Dentro do grupo, a ordem e a da lista
/// original.
/// </summary>
public static class GroupingModel
{
    /// <summary>
    /// O texto de reserva do grupo sem valor. O catalogo de idiomas e a
    /// fonte; este texto cobre o idioma que ainda nao tem a chave.
    /// </summary>
    private const string SemValor = "(sem valor)";

    /// <summary>
    /// Agrupa os programas pela coluna escolhida.
    /// </summary>
    /// <param name="programs">Os programas, na ordem em que a lista mostra.</param>
    /// <param name="column">A coluna do agrupamento.</param>
    /// <param name="customization">
    /// Como achar a preferencia do usuario de cada programa. Nulo usa a loja
    /// de preferencias (o mesmo caminho que a lista usa).
    /// </param>
    public static IReadOnlyList<ProgramGroup> Group(
        IReadOnlyList<InstalledProgram>? programs,
        ProgramColumn column,
        Func<InstalledProgram, ProgramCustomization?>? customization = null)
    {
        if (programs is null || programs.Count == 0) return Array.Empty<ProgramGroup>();

        var resolver = customization ?? DefaultCustomization;

        // A ordem de aparicao do grupo e a ordem da lista: ela desempatra os
        // grupos do mesmo tamanho, sem inventar uma ordem nova.
        var ordem = new List<string>();
        var membros = new Dictionary<string, List<InstalledProgram>>(StringComparer.OrdinalIgnoreCase);

        foreach (var program in programs)
        {
            if (program is null) continue;

            var raw = ProgramColumns.Value(column, program, resolver(program));
            var key = (raw ?? string.Empty).Trim();

            if (!membros.TryGetValue(key, out var lista))
            {
                lista = new List<InstalledProgram>();
                membros[key] = lista;
                ordem.Add(key);
            }

            lista.Add(program);
        }

        var grupos = new List<ProgramGroup>(ordem.Count);
        foreach (var key in ordem)
        {
            var nome = key.Length == 0 ? TextoSemValor() : key;
            grupos.Add(new ProgramGroup(column, nome, membros[key]));
        }

        // OrderByDescending e estavel: grupos do mesmo tamanho mantem a ordem
        // de aparicao.
        return grupos
            .OrderByDescending(g => g.TotalSize)
            .ToList();
    }

    /// <summary>
    /// A preferencia do usuario do programa, pela loja de preferencias.
    /// </summary>
    private static ProgramCustomization? DefaultCustomization(InstalledProgram program) =>
        ProgramCustomizationStore.For(program);

    /// <summary>
    /// O texto do grupo sem valor, traduzido quando o catalogo tem a chave.
    /// </summary>
    private static string TextoSemValor()
    {
        const string key = "Common.EmptyValue";
        var text = Loc.Get(key);
        return text == key ? SemValor : text;
    }
}
