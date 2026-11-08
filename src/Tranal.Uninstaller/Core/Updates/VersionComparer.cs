namespace Tranal.Uninstaller.Core.Updates;

/// <summary>
/// Comparador de versoes **semantico** de software.
///
/// O Tranal compara versoes de duas naturezas diferentes: a versao que o
/// Windows Installer gravou no registro e a versao que o fabricante publicou
/// no feed de atualizacoes. As duas nao tem o mesmo formato — "1.2.3",
/// "1.2.3.4", "2024.1", "1.0-beta" aparecem na mesma lista de instalados.
///
/// A comparacao e numerica por componente ("2.0" e menor que "10.0", mesmo
/// que o texto diga o contrario), a parte que falta e tratada como zero
/// ("1.0" e igual a "1.0.0") e um sufixo de pre-lancamento ("1.0-beta") e
/// **menor** que a versao final ("1.0") — e assim que o versionamento
/// semantico define.
///
/// A regra mora aqui, em um lugar so: quem precisa comparar versoes consome
/// este metodo e nunca monta a comparacao por conta propria.
/// </summary>
public static class VersionComparer
{
    /// <summary>
    /// Compara duas versoes. Devolve um numero negativo quando a primeira e
    /// menor, zero quando sao equivalentes e positivo quando a primeira e
    /// maior.
    ///
    /// Duas versoes que nao podem ser lidas (vazias, texto solto) sao
    /// consideradas iguais: nao ha informacao suficiente para dizer que uma e
    /// mais nova, e tratar "desconhecido" como "mais novo" faria o Tranal
    /// oferecer atualizacao para tudo.
    /// </summary>
    /// <param name="esquerda">A primeira versao.</param>
    /// <param name="direita">A segunda versao.</param>
    public static int Compare(string? esquerda, string? direita)
    {
        var a = Parse(esquerda);
        var b = Parse(direita);

        // Sem nenhuma informacao util dos dois lados: sao iguais.
        if (!a.TemNumero && !b.TemNumero) return 0;
        if (!a.TemNumero) return -1;
        if (!b.TemNumero) return 1;

        var tamanho = Math.Max(a.Numeros.Count, b.Numeros.Count);
        for (var i = 0; i < tamanho; i++)
        {
            var va = i < a.Numeros.Count ? a.Numeros[i] : 0;
            var vb = i < b.Numeros.Count ? b.Numeros[i] : 0;
            if (va != vb) return va < vb ? -1 : 1;
        }

        // Numeros iguais: decide a pre-lancamento. "1.0-beta" < "1.0".
        if (a.PreRelease == b.PreRelease) return 0;
        if (string.IsNullOrEmpty(a.PreRelease)) return 1;
        if (string.IsNullOrEmpty(b.PreRelease)) return -1;

        return string.Compare(a.PreRelease, b.PreRelease, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True quando a versao informada e **mais nova** que a instalada.
    /// </summary>
    /// <param name="versaoNova">A versao oferecida.</param>
    /// <param name="versaoInstalada">A versao que ja esta na maquina.</param>
    public static bool IsNewer(string? versaoNova, string? versaoInstalada) =>
        Compare(versaoNova, versaoInstalada) > 0;

    /// <summary>
    /// As partes numericas e o sufixo de pre-lancamento de uma versao.
    /// </summary>
    private sealed class ParsedVersion
    {
        /// <summary>Os componentes numericos, na ordem em que aparecem.</summary>
        public List<int> Numeros { get; } = new();

        /// <summary>O sufixo de pre-lancamento ("beta", "rc1") ou string vazia.</summary>
        public string PreRelease { get; set; } = string.Empty;

        /// <summary>True quando a versao tem ao menos um numero utilizavel.</summary>
        public bool TemNumero => Numeros.Count > 0;
    }

    /// <summary>
    /// Le a versao: numeros separados por ponto e o sufixo depois do hifen.
    /// </summary>
    private static ParsedVersion Parse(string? texto)
    {
        var result = new ParsedVersion();

        if (string.IsNullOrWhiteSpace(texto)) return result;

        var limpo = texto.Trim();

        // O "v" inicial e uma convencao de controle de versao ("v1.2.3").
        if (limpo.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            limpo = limpo[1..];

        // O sufixo de pre-lancamento vem depois do primeiro hifen.
        var separador = limpo.IndexOf('-');
        var parteNumerica = limpo;
        if (separador >= 0)
        {
            result.PreRelease = limpo[(separador + 1)..].Trim();
            parteNumerica = limpo[..separador];
        }

        foreach (var parte in parteNumerica.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var digitos = 0;
            while (digitos < parte.Length && char.IsDigit(parte[digitos])) digitos++;

            // Um componente que nao comeca com digito ("1.x") vale zero: a
            // comparacao continua valendo e nao estoura.
            result.Numeros.Add(digitos > 0 && int.TryParse(parte[..digitos], out var valor)
                ? valor
                : 0);
        }

        return result;
    }
}
