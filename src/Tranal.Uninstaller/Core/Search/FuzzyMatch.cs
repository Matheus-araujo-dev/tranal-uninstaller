using System.Globalization;
using System.Text;

namespace Tranal.Uninstaller.Core.Search;

/// <summary>
/// Correspondencia difusa entre dois textos.
///
/// O Geek Uninstaller e o Uninstalr casam a busca por aproximacao: digitar
/// "adobe" acha "Adobe Acrobat Reader" mesmo com a palavra faltando, e
/// digitar "acrobat" acha "Acrobat Reader DC".
///
/// A REGRA mora aqui, em um lugar so: a similaridade entre dois textos e o
/// limiar que decide o que e "caso" sao definidos nesta classe, e quem busca
/// consome o veredito.
/// </summary>
public static class FuzzyMatch
{
    /// <summary>
    /// O limiar padrao de similaridade para considerar que dois textos casam.
    /// </summary>
    public const double DefaultThreshold = 0.72;

    /// <summary>
    /// O menor numero de letras de um termo para a busca difusa fazer sentido.
    /// Com menos que isso, qualquer texto parecido vira falso positivo.
    /// </summary>
    public const int MinimumTermLength = 3;

    /// <summary>
    /// Normaliza um texto para a comparacao: sem acento, em minusculas e sem
    /// pontuacao nos separadores.
    /// </summary>
    /// <param name="text">O texto de origem.</param>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }

        return sb.ToString()
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant()
            .Trim();
    }

    /// <summary>
    /// A distancia de Levenshtein entre dois textos normalizados.
    /// </summary>
    /// <param name="a">O primeiro texto.</param>
    /// <param name="b">O segundo texto.</param>
    public static int Distance(string? a, string? b)
    {
        var left = Normalize(a);
        var right = Normalize(b);

        if (left.Length == 0) return right.Length;
        if (right.Length == 0) return left.Length;
        if (left == right) return 0;

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var j = 0; j <= right.Length; j++) previous[j] = j;

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= right.Length; j++)
            {
                var custo = left[i - 1] == right[j - 1] ? 0 : 1;

                var insercao = current[j - 1] + 1;
                var remocao = previous[j] + 1;
                var substituicao = previous[j - 1] + custo;

                current[j] = Math.Min(Math.Min(insercao, remocao), substituicao);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    /// <summary>
    /// A similaridade entre dois textos, de 0 (nada em comum) a 1 (iguais).
    /// </summary>
    /// <param name="a">O primeiro texto.</param>
    /// <param name="b">O segundo texto.</param>
    public static double Similarity(string? a, string? b)
    {
        var left = Normalize(a);
        var right = Normalize(b);

        if (left.Length == 0 && right.Length == 0) return 1;
        if (left.Length == 0 || right.Length == 0) return 0;
        if (left == right) return 1;

        var maior = Math.Max(left.Length, right.Length);
        return 1.0 - (double)Distance(left, right) / maior;
    }

    /// <summary>
    /// True quando os dois textos casam por aproximacao.
    ///
    /// O termo curto demais nunca casa por aproximacao: com duas letras, a
    /// chance de falso positivo e alta demais.
    /// </summary>
    /// <param name="text">O texto do programa.</param>
    /// <param name="term">O termo digitado.</param>
    /// <param name="threshold">O limiar de similaridade.</param>
    public static bool IsMatch(string? text, string? term, double threshold = DefaultThreshold)
    {
        var busca = Normalize(term);
        if (busca.Length < MinimumTermLength) return false;

        var alvo = Normalize(text);
        if (alvo.Length == 0) return false;

        // Casa exata por palavra: "acrobat" dentro de "Adobe Acrobat Reader".
        if (alvo.Contains(busca, StringComparison.Ordinal)) return true;

        // Casa por palavra: cada palavra do termo bate com alguma palavra do
        // texto, por aproximacao.
        var palavrasTermo = busca.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var palavrasAlvo = alvo.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (palavrasTermo.Length > 1 && palavrasAlvo.Length > 0)
        {
            var todas = palavrasTermo.All(pt =>
                palavrasAlvo.Any(pa => Similarity(pa, pt) >= threshold));

            if (todas) return true;
        }

        return Similarity(alvo, busca) >= threshold;
    }
}
