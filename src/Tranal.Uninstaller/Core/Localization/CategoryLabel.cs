namespace Tranal.Uninstaller.Core.Localization;

/// <summary>
/// Rotulo localizado de uma categoria de item (lixo, evidencia, historico,
/// ferramenta do Windows). O identificador da categoria e estavel
/// ("temp-user", "wucache", "jumplists"...); o texto exibido vem do catalogo.
/// </summary>
public static class CategoryLabel
{
    /// <summary>Prefixo das chaves de categoria no catalogo de idiomas.</summary>
    public const string KeyPrefix = "Cat.";

    /// <summary>
    /// Devolve o rotulo traduzido da categoria. Quando nao existe traducao,
    /// devolve o proprio identificador (nunca vazio).
    /// </summary>
    public static string For(string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId)) return string.Empty;

        var key = KeyPrefix + ToPascalCase(categoryId);
        var text = Loc.Get(key);
        return text == key ? categoryId : text;
    }

    /// <summary>"temp-user" -> "TempUser"; "wucache" -> "Wucache".</summary>
    public static string ToPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var sb = new System.Text.StringBuilder(value.Length);
        var upperNext = true;

        foreach (var c in value)
        {
            if (c is '-' or '_' or ' ')
            {
                upperNext = true;
                continue;
            }

            sb.Append(upperNext ? char.ToUpperInvariant(c) : c);
            upperNext = false;
        }

        return sb.ToString();
    }
}
