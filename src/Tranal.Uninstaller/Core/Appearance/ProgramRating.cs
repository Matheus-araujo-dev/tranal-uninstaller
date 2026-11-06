namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// A classificacao do usuario sobre um programa: de 0 a 5 estrelas.
///
/// O valor vive nas preferencias por programa
/// (<see cref="ProgramCustomization.Rating"/>) e a regra fica aqui: quantas
/// estrelas existem, como um valor fora da faixa e tratado e como a estrela
/// e desenhada. A coluna da lista, a tela e a exportacao consomem esta
/// mesma regra — quando cada uma tivesse a sua, a lista mostraria "3" e a
/// tela mostraria "★★★".
/// </summary>
public static class ProgramRating
{
    /// <summary>A menor classificacao possivel: o programa nao foi classificado.</summary>
    public const int Min = 0;

    /// <summary>A maior classificacao possivel: cinco estrelas.</summary>
    public const int Max = 5;

    /// <summary>A estrela cheia.</summary>
    public const string FilledStar = "★";

    /// <summary>A estrela vazia (a que falta para completar as cinco).</summary>
    public const string EmptyStar = "☆";

    /// <summary>
    /// True quando o valor cabe na escala de 0 a 5 estrelas.
    /// </summary>
    /// <param name="rating">O valor a conferir.</param>
    public static bool IsValid(int rating) => rating >= Min && rating <= Max;

    /// <summary>
    /// Ajusta o valor para a escala. Um valor fora da faixa e um dado
    /// invalido: ele e cortado, nunca propagado.
    /// </summary>
    /// <param name="rating">O valor informado.</param>
    public static int Clamp(int rating) => rating < Min ? Min : rating > Max ? Max : rating;

    /// <summary>
    /// O texto da estrela, como a coluna mostra.
    ///
    /// Sem classificacao (0) o texto e vazio: um programa que o usuario nao
    /// classificou nao pode aparecer com estrelas. Com classificacao, as cinco
    /// posicoes aparecem — as cheias e as vazias — para que o usuario leia
    /// "tres de cinco" sem contar.
    /// </summary>
    /// <param name="rating">A classificacao, de 0 a 5.</param>
    public static string Stars(int rating)
    {
        var value = Clamp(rating);
        if (value == Min) return string.Empty;

        var filled = new string(FilledStar[0], value);
        var empty = new string(EmptyStar[0], Max - value);
        return filled + empty;
    }
}
