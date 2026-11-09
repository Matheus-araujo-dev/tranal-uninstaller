
namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Um grupo de programas com o mesmo nome, instalados em lugares diferentes.
/// </summary>
public sealed class DuplicateGroup
{
    /// <summary>O nome normalizado que agrupou os programas.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>O nome exibido (o do primeiro programa do grupo).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Os programas do grupo.</summary>
    public IReadOnlyList<InstalledProgram> Programs { get; init; } = Array.Empty<InstalledProgram>();

    /// <summary>Quantas copias existem.</summary>
    public int Count => Programs.Count;
}

/// <summary>
/// Detecta instalacoes duplicadas: o mesmo programa instalado em dois
/// lugares.
///
/// O Uninstalr e o Uninstall Tool mostram as duplicatas, porque so uma das
/// copias costuma ser a "certa" — as outras sao sobras de uma instalacao
/// antiga, ou a versao de teste de um.
///
/// A REGRA mora aqui, em um lugar so: a normalizacao do nome (que decide o
/// que e o mesmo programa) e o agrupamento ficam aqui.
///
/// O que conta como "mesmo programa" e o nome sem o numero da versao, sem o
/// sufixo do fabricante e sem acentos: "Adobe Acrobat Reader DC (x64)" e
/// "Adobe Acrobat Reader" sao o mesmo programa para o usuario.
/// </summary>
public static class DuplicateInstallDetector
{
    /// <summary>
    /// Os grupos de instalacoes duplicadas.
    /// </summary>
    /// <param name="programs">Os programas instalados.</param>
    public static IReadOnlyList<DuplicateGroup> Detect(IEnumerable<InstalledProgram>? programs)
    {
        var grupos = new List<DuplicateGroup>();
        if (programs is null) return grupos;

        var porChave = new Dictionary<string, List<InstalledProgram>>(StringComparer.OrdinalIgnoreCase);

        foreach (var programa in programs)
        {
            if (programa is null) continue;

            var chave = Normalize(programa.DisplayName);
            if (chave.Length < 3) continue;

            if (!porChave.TryGetValue(chave, out var lista))
            {
                lista = new List<InstalledProgram>();
                porChave[chave] = lista;
            }

            lista.Add(programa);
        }

        foreach (var par in porChave)
        {
            if (par.Value.Count < 2) continue;

            grupos.Add(new DuplicateGroup
            {
                Key = par.Key,
                Name = par.Value[0].DisplayName,
                Programs = par.Value,
            });
        }

        return grupos.OrderByDescending(g => g.Count).ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Normaliza o nome de um programa para comparar com outro.
    /// </summary>
    /// <param name="name">O nome do programa.</param>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var texto = name.Trim().ToLowerInvariant();

        // Tira o acento.
        var decomposto = texto.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        texto = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);

        // Tira o numero da versao: "(1.2.3)", "- 1.2.3", "v1.2.3".
        texto = System.Text.RegularExpressions.Regex.Replace(texto, @"[\(\[]\s*v?\d+(\.\d+)*\s*[\)\]]", " ");
        texto = System.Text.RegularExpressions.Regex.Replace(texto, @"\s+-\s+v?\d+(\.\d+)*", " ");

        // Tira a versao no fim do nome: "Meu Programa 1.0".
        texto = System.Text.RegularExpressions.Regex.Replace(texto, @"\s+v?\d+(\.\d+)+$", " ");

        // Tira o sufixo do fabricante e o da arquitetura.
        texto = System.Text.RegularExpressions.Regex.Replace(texto, @"\((x86|x64|64-bit|32-bit|amd64)\)", " ");
        texto = System.Text.RegularExpressions.Regex.Replace(texto, @"\s+-\s+\S+\s+(inc|corporation|corp\.?|ltda|llc|gmbh|s\.a\.)\s*$", " ");

        // Ajusta o espaco.
        texto = System.Text.RegularExpressions.Regex.Replace(texto, @"\s+", " ").Trim();

        return texto;
    }
}
