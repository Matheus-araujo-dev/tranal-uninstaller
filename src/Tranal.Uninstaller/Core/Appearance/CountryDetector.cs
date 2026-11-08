using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;

namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// O pais de um programa, com a bandeira para mostrar.
/// </summary>
public sealed class CountryInfo
{
    /// <summary>O codigo de duas letras ("BR", "US").</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>O nome do pais, em pt-BR ("Brasil").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>O emoji da bandeira.</summary>
    public string Flag { get; init; } = string.Empty;

    /// <summary>O rotulo pronto para exibir ("Brasil 🇧🇷").</summary>
    public string Label => Flag.Length > 0 ? $"{Name} {Flag}" : Name;
}

/// <summary>
/// Descobre o pais de um programa.
///
/// O Ashampoo mede "Pais do editor com bandeira e nome" e o Uninstalr mede
/// "Pais de origem do app, com bandeira". E o detalhe que responde "de onde
/// veio isso?".
///
/// A REGRA de como o pais e deduzido mora aqui: primeiro a Resource do
/// proprio arquivo (o Windows grava o pais do produto no recurso de
/// versao), e so depois o idioma do sistema. Um instalador brasileiro em
/// maquina inglesa e brasileiro — e o recurso diz isso.
///
/// O emoji da bandeira e derivado do codigo ISO, e nao de um arquivo de
/// imagem: e o codigo que ja existe em todo lugar.
/// </summary>
public static class CountryDetector
{
    private static readonly Dictionary<string, CountryInfo> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// O pais do arquivo informado, ou null quando nao da para saber.
    /// </summary>
    /// <param name="caminhoExecutavel">O caminho do executavel.</param>
    public static CountryInfo? Detect(string? caminhoExecutavel)
    {
        if (string.IsNullOrWhiteSpace(caminhoExecutavel)) return null;

        var caminho = caminhoExecutavel.Trim();
        if (Cache.TryGetValue(caminho, out var emCache)) return emCache;

        CountryInfo? resultado = null;

        // O recurso de versao do arquivo traz o pais do produto.
        var doRecurso = DoResource(caminho);
        if (doRecurso is not null) resultado = doRecurso;

        // Sem recurso, o pais NAO e inventado a partir do idioma do
        // sistema: o programa e de uma empresaalekiana instalado em maquina
        // portuguesa continua sendo alemao. O idioma do sistema e uma pista
        // do USUARIO, nao do programa — e por isso ela nao vira resposta.

        Cache[caminho] = resultado!;
        return resultado;
    }

    /// <summary>
    /// O pais pelo codigo ISO de duas letras ("BR", "PT", "US").
    /// </summary>
    /// <param name="codigo">O codigo ("BR", "PT", "US").</param>
    public static CountryInfo? FromCode(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;

        var limpo = codigo.Trim().ToUpperInvariant();
        if (limpo.Length != 2) return null;

        if (Cache.TryGetValue($"__code__{limpo}", out var emCache)) return emCache;

        // O codigo de PAIS nao e o codigo de IDIOMA: "BR" e pais, e nao uma
        // cultura valida por si so. A cultura neutra ("pt") com a regiao
        // ("BR") e o que produz o nome certo.
        var nome = NomeDoPais(limpo);
        if (nome is null)
        {
            Cache[$"__code__{limpo}"] = null!;
            return null;
        }

        var info = new CountryInfo
        {
            Code = limpo,
            Name = nome,
            Flag = BandeiraDe(limpo),
        };

        Cache[$"__code__{limpo}"] = info;
        return info;
    }

    /// <summary>
    /// O nome do pais a partir do codigo ISO de duas letras.
    ///
    /// O codigo de pais e procurado em todas as culturas conhecidas: a que
    /// tem a regiao correspondente e a que tem o nome. E o que faz "BR"
    /// virar "Brasil" em vez de "bretão".
    /// </summary>
    private static string? NomeDoPais(string codigoPais)
    {
        try
        {
            foreach (var cultura in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
            {
                // A regiao vem do proprio nome da cultura: a ultima parte
                // depois do "-" (ou "_"). Uma cultura neutra ("pt") nao tem
                // regiao e por isso e ignorada.
                var partes = cultura.Name.Split('-', '_');
                if (partes.Length < 2) continue;

                var regiao = partes[^1];
                if (!string.Equals(regiao, codigoPais, StringComparison.OrdinalIgnoreCase)) continue;

                // O nome vem do recurso de idioma da propria cultura, e por
                // isso e "Brasil" e nao "bretao".
                return cultura.DisplayName;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    /// <summary>
    /// A bandeira, derivada do codigo ISO de duas letras.
    /// </summary>
    /// <param name="codigo">O codigo do pais.</param>
    public static string BandeiraDe(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo) || codigo.Length != 2) return string.Empty;

        const int deslocamento = 0x1F1E6;
        var a = char.ConvertFromUtf32(deslocamento + (char.ToUpperInvariant(codigo[0]) - 'A'));
        var b = char.ConvertFromUtf32(deslocamento + (char.ToUpperInvariant(codigo[1]) - 'A'));
        return a + b;
    }

    /// <summary>
    /// O pais declarado no recurso de versao do arquivo.
    /// </summary>
    private static CountryInfo? DoResource(string caminho)
    {
        try
        {
            if (!System.IO.File.Exists(caminho)) return null;

            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(caminho);

            // A Resource do arquivo traz o pais em formato numerico (a
            // regiao do Windows). O "ProductName" costuma trazer o pais por
            // extenso ("Brasil"), que e a pista mais direta.
            var produto = info.ProductName ?? string.Empty;
            var paisTexto = ExtrairPaisDoTexto(produto);
            if (paisTexto is not null) return paisTexto;

            // O "Language" do recurso e o codigo de idioma em hexadecimal
            // ("0419" = ingles dos EUA, "0416" = portugues do Brasil).
            var idioma = info.Language;
            if (!string.IsNullOrWhiteSpace(idioma))
            {
                var codigo = ExtrairRegiaoDoIdioma(idioma);
                if (codigo is not null)
                {
                    var porCodigo = FromCode(codigo);
                    if (porCodigo is not null) return porCodigo;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extrai o pais de um texto que o traz por extenso.
    /// </summary>
    private static CountryInfo? ExtrairPaisDoTexto(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        foreach (var cultura in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            var nome = cultura.DisplayName;
            if (nome.Length < 4) continue;
            if (texto.Contains(nome, StringComparison.OrdinalIgnoreCase))
            {
                return FromCode(cultura.TwoLetterISOLanguageName) ?? new CountryInfo { Name = nome };
            }
        }

        return null;
    }

    /// <summary>
    /// Extrai a regiao (a ultima parte do codigo de idioma) do valor de
    /// recurso do Windows.
    ///
    /// O valor e o codigo de idioma em hexadecimal, com quatro digitos: os
    /// dois primeiros sao o idioma (0x04 = pt-BR, 0x09 = en-US) e os dois
    /// ultimos, a regiao (0x16 = Brasil, 0x12 = Portugal).
    /// </summary>
    private static string? ExtrairRegiaoDoIdioma(string valor)
    {
        var texto = valor.Trim();
        if (texto.Length < 4) return null;

        if (!int.TryParse(texto[..4], System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var codigo))
        {
            return null;
        }

        // A regiao e o byte mais significativo. Os codigos de regiao mais
        // comuns estao listados aqui. Um codigo desconhecido nao vira pais:
        // o nome ficaria errado, e nome errado e pior que ausencia.
        var regiao = codigo & 0xFF;

        return regiao switch
        {
            0x02 => "BR",
            0x03 => "PT",
            0x01 => "US",
            0x0C => "FR",
            0x04 => "DE",
            0x10 => "IT",
            0x11 => "JP",
            0x12 => "KR",
            0x0A => "ES",
            0x13 => "NL",
            0x08 => "GB",
            0x05 => "CZ",
            0x0B => "FI",
            0x09 => "DK",
            0x0E => "HU",
            0x15 => "PL",
            0x0D => "SE",
            0x06 => "NO",
            0x1F => "TR",
            0x0F => "RU",
            0x14 => "AR",
            _ => null,
        };
    }
}
