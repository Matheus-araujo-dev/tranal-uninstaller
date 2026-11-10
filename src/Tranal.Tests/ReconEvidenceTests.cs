using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// A evidencia dos relatorios de reconciliacao (RECON) so vale se a citacao
/// continua apontando para o codigo.
///
/// Cada linha do RECON cita o lugar do codigo que sustenta o estado
/// declarado: <c>`arquivo.cs:linha`</c> e, as vezes, o simbolo que esta
/// nessa linha entre parenteses. Quando o codigo anda e a linha citada fica
/// para tras, a evidencia passa a mentir — e uma linha "OK" com evidencia
/// errada e pior que uma linha "AUSENTE" honesta.
///
/// Este teste confere a citacao contra o fonte em disco. A suite nao
/// referencia o projeto grafico: o nucleo e compilado tambem na CLI, e
/// referenciar os dois criaria dois tipos "Core" diferentes.
///
/// As duas regras:
/// - a citacao COM simbolo exige que o identificador final do simbolo
///   esteja na linha citada; quando o simbolo e qualificado
///   (<c>Classe.Metodo</c>), a classe tem que existir no arquivo, porque a
///   string <c>Classe.Metodo</c> nunca aparece literal no codigo. O simbolo
///   que e comando da linha de comando (<c>tranal /xxx</c>) nao e
///   identificador de codigo e nao se aplica;
/// - a citacao SEM simbolo exige que o arquivo exista, que a linha esteja
///   dentro dele e que ela nao seja uma linha em branco — nao ha codigo em
///   uma linha em branco.
/// </summary>
public class ReconEvidenceTests
{
    /// <summary>A citacao de um RECON: o arquivo, a linha e o simbolo.</summary>
    private sealed record Citacao(string Arquivo, int Linha, string Simbolo);

    /// <summary>
    /// O padrao de uma citacao: o arquivo e a linha entre crases.
    /// </summary>
    private static readonly Regex Padrao = new(
        @"`(?<arq>[^`]*?\.cs):(?<lin>\d+)`",
        RegexOptions.Compiled);

    /// <summary>
    /// O indice dos arquivos do codigo por nome: a citacao abreviada
    /// (<c>Arquivo.cs:linha</c>) nao traz o diretorio, e o nome precisa
    /// resolver para um unico arquivo.
    /// </summary>
    private static readonly Dictionary<string, string> ArquivosPorNome = MontarIndice();

    /// <summary>
    /// Monta o indice nome -> caminho, andando por <c>src/</c>.
    /// </summary>
    private static Dictionary<string, string> MontarIndice()
    {
        var indice = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var src = RepoFiles.FindDirectory("src");
        if (src is null) return indice;

        foreach (var arquivo in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            var nome = Path.GetFileName(arquivo);

            // O nome repetido e ambiguo: o índice guarda o primeiro e a
            // citacao ambigua e recusada na hora da conferencia.
            if (!indice.ContainsKey(nome)) indice[nome] = arquivo;
            else indice[nome] = string.Empty;
        }

        return indice;
    }

    /// <summary>
    /// Toda citacao dos RECON aponta para a linha do simbolo (quando ha
    /// simbolo) e para uma linha que existe e nao esta em branco.
    /// </summary>
    [Fact]
    public void TodaCitacaoDoReconApontaParaOCodigo()
    {
        var pasta = RepoFiles.FindDirectory("docs", "auditoria");
        Assert.NotNull(pasta);

        var arquivos = Directory.GetFiles(pasta!, "RECON-*.md");
        Assert.True(arquivos.Length > 0, "os relatorios de reconciliacao existem");

        var quebradas = new List<string>();
        var total = 0;
        var comSimbolo = 0;

        foreach (var arquivo in arquivos)
        {
            var nome = Path.GetFileName(arquivo);
            var texto = File.ReadAllText(arquivo);

            foreach (Match achado in Padrao.Matches(texto))
            {
                var arq = achado.Groups["arq"].Value;
                var num = int.Parse(achado.Groups["lin"].Value);

                // O simbolo, quando existe, vem logo depois da citacao.
                var resto = texto.Substring(achado.Index + achado.Length);
                var simbolo = LerSimbolo(resto);

                total++;
                if (simbolo.Length > 0) comSimbolo++;

                var fonte = Localizar(arq);
                if (fonte is null)
                {
                    quebradas.Add($"{nome}: o arquivo '{arq}' citado nao existe no codigo");
                    continue;
                }

                var linhas = File.ReadAllLines(fonte);
                if (num < 1 || num > linhas.Length)
                {
                    quebradas.Add(
                        $"{nome}: {arq}:{num} esta fora do arquivo ({linhas.Length} linhas)");
                    continue;
                }

                if (simbolo.Length == 0)
                {
                    // Sem simbolo, a exigencia e a mais fraca que ainda
                    // significa algo: a linha tem de ter codigo.
                    if (linhas[num - 1].Trim().Length == 0)
                    {
                        quebradas.Add($"{nome}: {arq}:{num} aponta para uma linha em branco");
                    }

                    continue;
                }

                if (EhComandoDeLinhaDeComando(simbolo)) continue;

                var identificador = IdentificadorFinal(simbolo);
                if (identificador.Length == 0) continue;

                if (linhas[num - 1].Contains(identificador, StringComparison.Ordinal)) continue;

                var classe = ClasseDe(simbolo);
                if (classe.Length > 0 && !ContemAlgum(linhas, classe))
                {
                    quebradas.Add(
                        $"{nome}: a classe '{classe}' de '{simbolo}' nao existe em {arq}");
                    continue;
                }

                quebradas.Add(
                    $"{nome}: '{simbolo}' nao esta em {arq}:{num} (esta em {Onde(linhas, identificador)})");
            }
        }

        Assert.True(total > 1000,
            $"os RECON tem citacoes para conferir (medido: {total})");
        Assert.True(comSimbolo > 500,
            $"os RECON tem citacoes com simbolo para conferir (medido: {comSimbolo})");

        Assert.True(quebradas.Count == 0,
            "citacao do RECON apontando para o codigo errado:\n  " +
            string.Join("\n  ", quebradas));
    }

    /// <summary>
    /// Le o simbolo que vem logo depois da citacao: <c>(`Simbolo`)</c>.
    /// Vazio quando a citacao nao tem simbolo.
    /// </summary>
    /// <param name="resto">O texto que vem depois da citacao.</param>
    private static string LerSimbolo(string resto)
    {
        var achado = Regex.Match(resto, @"^\s*\(`(?<sim>[^`]+?)`\)", RegexOptions.CultureInvariant);
        return achado.Success ? achado.Groups["sim"].Value.Trim() : string.Empty;
    }

    /// <summary>
    /// True quando o simbolo e um comando da linha de comando (e nao um
    /// identificador do codigo).
    /// </summary>
    /// <param name="simbolo">O simbolo citado.</param>
    private static bool EhComandoDeLinhaDeComando(string simbolo)
    {
        return simbolo.StartsWith("/", StringComparison.Ordinal)
            || simbolo.StartsWith("tranal ", StringComparison.Ordinal)
            || simbolo.Contains(" /", StringComparison.Ordinal);
    }

    /// <summary>
    /// O identificador final do simbolo: sem a assinatura e sem o nome da
    /// classe (<c>CheckAsync(feedUrl)</c> vira <c>CheckAsync</c>).
    /// </summary>
    /// <param name="simbolo">O simbolo citado.</param>
    private static string IdentificadorFinal(string simbolo)
    {
        var texto = simbolo.Trim().Trim('`');

        var abre = texto.IndexOf('(');
        if (abre >= 0) texto = texto.Substring(0, abre);

        var partes = texto.Split(new[] { '.', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return partes.Length == 0 ? string.Empty : partes[partes.Length - 1];
    }

    /// <summary>
    /// A classe de um simbolo qualificado (<c>Classe.Metodo</c>). Vazio
    /// quando o simbolo nao e qualificado.
    /// </summary>
    /// <param name="simbolo">O simbolo citado.</param>
    private static string ClasseDe(string simbolo)
    {
        var texto = simbolo.Trim().Trim('`');

        var abre = texto.IndexOf('(');
        if (abre >= 0) texto = texto.Substring(0, abre);

        var partes = texto.Split(new[] { '.', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return partes.Length >= 2 ? partes[0] : string.Empty;
    }

    /// <summary>
    /// True quando alguma linha do arquivo menciona o termo como palavra
    /// inteira.
    /// </summary>
    /// <param name="linhas">As linhas do arquivo.</param>
    /// <param name="termo">O termo procurado.</param>
    private static bool ContemAlgum(IReadOnlyList<string> linhas, string termo)
    {
        foreach (var linha in linhas)
        {
            var indice = linha.IndexOf(termo, StringComparison.Ordinal);
            while (indice >= 0)
            {
                var antes = indice == 0 ? ' ' : linha[indice - 1];
                var depois = indice + termo.Length >= linha.Length
                    ? ' '
                    : linha[indice + termo.Length];

                if (!char.IsLetterOrDigit(antes) && !char.IsLetterOrDigit(depois))
                {
                    return true;
                }

                indice = linha.IndexOf(termo, indice + 1, StringComparison.Ordinal);
            }
        }

        return false;
    }

    /// <summary>
    /// As linhas onde o simbolo aparece (para a mensagem de falha).
    /// </summary>
    /// <param name="linhas">As linhas do arquivo.</param>
    /// <param name="simbolo">O simbolo procurado.</param>
    private static string Onde(IReadOnlyList<string> linhas, string simbolo)
    {
        var achadas = new List<string>();

        for (var i = 0; i < linhas.Count && achadas.Count < 5; i++)
        {
            if (linhas[i].Contains(simbolo, StringComparison.Ordinal))
            {
                achadas.Add((i + 1).ToString());
            }
        }

        return achadas.Count == 0 ? "nenhuma linha" : string.Join(", ", achadas);
    }

    /// <summary>
    /// Localiza o arquivo citado: relativo a raiz do repositorio, ao projeto
    /// grafico (que e onde mora o nucleo) ou pelo nome, quando a citacao vem
    /// abreviada.
    /// </summary>
    /// <param name="relativo">O caminho citado.</param>
    private static string? Localizar(string relativo)
    {
        var separado = relativo.Replace('/', Path.DirectorySeparatorChar);

        var peloRepo = RepoFiles.Find(relativo.Replace('/', Path.DirectorySeparatorChar));
        if (peloRepo is not null) return peloRepo;

        var peloProjeto = RepoFiles.Find("src", "Tranal.Uninstaller", separado);
        if (peloProjeto is not null) return peloProjeto;

        var nome = Path.GetFileName(relativo.Replace('\\', '/'));
        if (nome.Length == 0) return null;

        return ArquivosPorNome.TryGetValue(nome, out var caminho) && caminho.Length > 0
            ? caminho
            : null;
    }
}
