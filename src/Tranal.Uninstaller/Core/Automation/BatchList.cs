using System.IO;
using System.Text.RegularExpressions;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// Um padrão da lista de desinstalação.
///
/// O padrão pode ser um texto exato (o nome do programa, inteiro) ou uma
/// expressão regular. A distinção é escrita no arquivo, com o prefixo
/// <c>regex:</c> — sem ele, o padrão é texto exato.
///
/// A expressão regular é sempre insensível a maiúsculas e minúsculas: quem
/// escreve "adobe" numa lista de desinstalação quer o programa "Adobe", e
/// uma lista que quase casa é pior do que uma lista que não casa.
/// </summary>
public sealed class BatchListPattern
{
    /// <summary>O padrão como ele foi escrito no arquivo.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>True quando o padrão é uma expressão regular.</summary>
    public bool IsRegex { get; init; }

    /// <summary>
    /// A expressão regular compilada. Vazio quando o padrão é texto exato.
    /// </summary>
    public Regex? Compiled { get; init; }

    /// <summary>
    /// True quando o nome do programa casa com este padrão.
    /// </summary>
    /// <param name="programName">O nome do programa.</param>
    public bool Matches(string? programName)
    {
        var name = (programName ?? string.Empty).Trim();
        if (name.Length == 0) return false;

        if (Compiled is not null) return Compiled.IsMatch(name);

        return string.Equals(name, Text, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// A lista de desinstalação lida de um arquivo.
///
/// É o que torna a desinstalação em lote roteirizável: o usuário mantém um
/// arquivo com os programas que quer tirar de todo mundo, e o Tranal lê o
/// arquivo em vez de a lista de programas ser digitada de novo a cada vez.
///
/// A lista casa contra o **nome** do programa. O modo da desinstalação não
/// muda o que casa: a lista escolhe quais programas, e o modo decide o que
/// acontece com eles.
/// </summary>
public sealed class BatchList
{
    private BatchList(IReadOnlyList<BatchListPattern> patterns, string sourcePath)
    {
        Patterns = patterns;
        SourcePath = sourcePath;
    }

    /// <summary>Os padrões da lista, na ordem em que foram lidos.</summary>
    public IReadOnlyList<BatchListPattern> Patterns { get; }

    /// <summary>
    /// O arquivo de onde a lista veio. Vazio quando a lista foi montada em
    /// memória.
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    /// Lê a lista de um arquivo.
    ///
    /// Devolve o resultado da leitura em vez de estourar exceção: uma
    /// expressão regular inválida é um erro do usuário, e erro do usuário
    /// chega como mensagem, não como pilha de exceção.
    /// </summary>
    /// <param name="path">O caminho do arquivo (.txt ou .csv).</param>
    public static BatchListParseResult Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return BatchListParseResult.Falha(0, "nenhum arquivo informado");

        string content;
        try
        {
            if (!File.Exists(path))
                return BatchListParseResult.Falha(0, $"arquivo não encontrado: {path}");

            content = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            return BatchListParseResult.Falha(0, $"não foi possível ler o arquivo: {ex.Message}");
        }

        return Parse(content, path);
    }

    /// <summary>
    /// Monta a lista a partir do conteúdo de um arquivo.
    ///
    /// Cada linha é um padrão. São aceitos:
    /// <list type="bullet">
    /// <item><c>regex:Adobe.*</c> — expressão regular, insensível a caixa.</item>
    /// <item><c>exact:Adobe Reader</c> — texto exato (é o padrão).</item>
    /// <item><c>Adobe Reader</c> — texto exato (é o padrão).</item>
    /// </list>
    /// Linhas em branco e linhas que começam com <c>#</c> são comentários.
    /// Em arquivo <c>.csv</c>, o padrão é o primeiro campo da linha.
    /// </summary>
    /// <param name="content">O conteúdo do arquivo.</param>
    /// <param name="sourcePath">O caminho do arquivo (só para a mensagem de erro).</param>
    public static BatchListParseResult Parse(string content, string sourcePath = "")
    {
        var patterns = new List<BatchListPattern>();
        var lines = (content ?? string.Empty).Replace("\r\n", "\n").Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("#", StringComparison.Ordinal)) continue;

            var ehCsv = sourcePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
            var texto = ehCsv ? PrimeiroCampo(line) : line;

            // O cabecalho do CSV nao e um padrao: "nome" e "programa" nao
            // nomeiam programa nenhum.
            if (ehCsv && index == 0 && EhCabecalho(texto)) continue;

            if (texto.Length == 0) continue;

            var ehRegex = texto.StartsWith("regex:", StringComparison.OrdinalIgnoreCase);
            var valor = ehRegex ? texto["regex:".Length..].Trim() : texto;

            if (valor.StartsWith("exact:", StringComparison.OrdinalIgnoreCase))
                valor = valor["exact:".Length..].Trim();

            if (valor.Length == 0) continue;

            if (ehRegex)
            {
                try
                {
                    var compiled = new Regex(
                        valor,
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

                    patterns.Add(new BatchListPattern
                    {
                        Text = valor,
                        IsRegex = true,
                        Compiled = compiled,
                    });
                }
                catch (ArgumentException ex)
                {
                    return BatchListParseResult.Falha(
                        index + 1,
                        $"expressão regular inválida: '{valor}' ({ex.Message.TrimEnd('.')})");
                }
            }
            else
            {
                patterns.Add(new BatchListPattern { Text = valor, IsRegex = false });
            }
        }

        return BatchListParseResult.Ok(new BatchList(patterns, sourcePath));
    }

    /// <summary>
    /// Os programas da lista cujo nome casa com algum padrão.
    ///
    /// É o retorno da seleção: quem chamou a lista quer saber **quais**
    /// programas entram na desinstalação em lote.
    /// </summary>
    /// <param name="programs">Os programas instalados.</param>
    public IReadOnlyList<InstalledProgram> Match(IEnumerable<InstalledProgram> programs)
    {
        if (Patterns.Count == 0) return Array.Empty<InstalledProgram>();

        return programs.Where(p => Patterns.Any(pattern => pattern.Matches(p.DisplayName))).ToList();
    }

    /// <summary>
    /// True quando o nome casa com algum padrão da lista.
    /// </summary>
    /// <param name="programName">O nome do programa.</param>
    public bool Matches(string? programName) =>
        Patterns.Any(pattern => pattern.Matches(programName));

    /// <summary>
    /// O primeiro campo de uma linha de CSV. O separador pode ser ponto e
    /// vírgula (padrão do Excel em português) ou vírgula.
    /// </summary>
    private static string PrimeiroCampo(string line)
    {
        var separador = line.IndexOf(';') >= 0 ? ';' : ',';
        var posicao = line.IndexOf(separador);
        return posicao < 0 ? line : line[..posicao];
    }

    /// <summary>True quando a primeira linha do CSV é um cabeçalho.</summary>
    private static bool EhCabecalho(string texto)
    {
        var normalizado = texto.Trim().Trim('"').ToLowerInvariant();
        return normalizado is "nome" or "programa" or "programas" or "pattern"
            or "padrao" or "padrão" or "name";
    }
}

/// <summary>
/// O resultado da leitura de uma lista de desinstalação.
///
/// Ele existe para que uma expressão regular inválida (ou um arquivo que não
/// dá para ler) chegue ao usuário como mensagem clara — com a linha do
/// problema — e não como exceção crua.
/// </summary>
public sealed class BatchListParseResult
{
    private BatchListParseResult(bool success, BatchList? list, string errorMessage, int lineNumber)
    {
        Success = success;
        List = list;
        ErrorMessage = errorMessage;
        LineNumber = lineNumber;
    }

    /// <summary>True quando a lista foi lida sem erro.</summary>
    public bool Success { get; }

    /// <summary>A lista lida. Nulo quando a leitura falhou.</summary>
    public BatchList? List { get; }

    /// <summary>
    /// A mensagem do erro, em português. Vazia quando a leitura deu certo.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// A linha do problema (começa em um). Zero quando o problema não é de
    /// uma linha específica (arquivo inexistente, por exemplo).
    /// </summary>
    public int LineNumber { get; }

    /// <summary>Uma lista lida com sucesso.</summary>
    public static BatchListParseResult Ok(BatchList list) =>
        new(true, list, string.Empty, 0);

    /// <summary>Uma lista que não pôde ser lida.</summary>
    public static BatchListParseResult Falha(int lineNumber, string message) =>
        new(false, null, message, lineNumber);

    /// <summary>
    /// A mensagem do erro com o número da linha, quando ela existe.
    /// </summary>
    public override string ToString() => LineNumber > 0
        ? $"linha {LineNumber}: {ErrorMessage}"
        : ErrorMessage;
}
