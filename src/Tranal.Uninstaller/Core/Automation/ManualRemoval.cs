using System.IO;
using System.Text;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// A desinstalação manual: pular o desinstalador do programa e listar o que
/// o usuário quer apagar por conta própria.
///
/// A opção é a "eu cuido do resto" do Revo Uninstaller Pro: há programas
/// cujo desinstalador é quebrado, ou cujo desinstalador apaga coisa demais
/// (documentos, chaves de outros programas). Nesses casos o caminho útil é
/// mostrar a lista — com a confiança de cada item — e deixar a decisão com o
/// usuário.
///
/// Nada é removido aqui. A confiança e o motivo são os do núcleo
/// (<see cref="LeftoverConfidence"/> e <see cref="LeftoverReason"/>): a
/// desinstalação manual não inventa um segundo julgamento.
/// </summary>
public static class ManualRemoval
{
    /// <summary>
    /// A lista de candidatos do programa: o que sobrou dele no sistema,
    /// com a confiança e o motivo de cada item.
    ///
    /// A lista vem da varredura do núcleo, que já sabe o que é sobra de
    /// verdade (a pasta de instalação, a chave do desinstalador, o registro
    /// do instalador) e o que apenas se parece com o programa.
    /// </summary>
    /// <param name="program">O programa desinstalado.</param>
    /// <param name="options">As opções da varredura (profundidade e seleção).</param>
    public static IReadOnlyList<LeftoverItem> Candidates(
        InstalledProgram program,
        LeftoverScanOptions? options = null)
    {
        var opts = options ?? LeftoverScanOptions.Default;
        return LeftoverScanner.Scan(program, opts);
    }

    /// <summary>
    /// Monta o arquivo da lista de sobras, em CSV.
    ///
    /// O separador é o ponto e vírgula e a primeira linha é o cabeçalho: é o
    /// formato que o Excel em português abre sem perguntar nada. O texto da
    /// confiança e do motivo vem do catálogo de idiomas
    /// (<see cref="LeftoverLabels"/>), e não de uma segunda tradução que
    /// poderia divergir da tela.
    /// </summary>
    /// <param name="items">Os itens da lista.</param>
    public static string Export(IEnumerable<LeftoverItem> items)
    {
        // O catalogo e carregado aqui de proposito: a lista exportada e um
        // documento para o usuario, e ele tem de estar em portugues mesmo
        // quando quem exportou foi a linha de comando em outra situacao.
        Loc.Load(Loc.DefaultLanguage);

        var sb = new StringBuilder();
        sb.AppendLine("Tipo;Caminho;Detalhe;Confianca;Por que");

        foreach (var item in items)
        {
            sb.AppendLine(string.Join(';',
                Cell(item.Kind),
                Cell(item.Path),
                Cell(item.Detail),
                Cell(LeftoverLabels.Confidence(item.Confidence)),
                Cell(LeftoverLabels.Reason(item.Reason))));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Grava a lista de sobras em um arquivo.
    ///
    /// O arquivo sai em UTF-8 com marca de byte order, que é o que o Excel
    /// em português espera: sem ela, os acentos viram caracteres estranhos
    /// quando o usuário abre a lista.
    /// </summary>
    /// <param name="path">O caminho do arquivo.</param>
    /// <param name="items">Os itens da lista.</param>
    /// <returns><c>true</c> quando o arquivo foi gravado.</returns>
    public static bool SaveToFile(string path, IEnumerable<LeftoverItem> items)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(path, Export(items), new UTF8Encoding(true));
            return true;
        }
        catch
        {
            // Nao deu para gravar a lista: quem chama precisa avisar o
            // usuario, em vez de fingir que a lista foi salva.
            return false;
        }
    }

    /// <summary>
    /// Prepara uma célula de CSV: o ponto e vírgula e a aspa dupla são
    /// escapados, porque um caminho de programa pode conter os dois.
    /// </summary>
    private static string Cell(string? value)
    {
        var text = value ?? string.Empty;
        if (text.IndexOfAny(new[] { ';', '"', '\r', '\n' }) < 0) return text;

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
