using System;
using System.Collections.Generic;
using System.Linq;

namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// A largura que a pessoa deixou em uma coluna.
/// </summary>
/// <param name="Column">
/// O nome da coluna (o identificador fixo do catalogo, em ingles: o
/// arquivo de preferencias continua legivel em qualquer idioma).
/// </param>
/// <param name="Width">A largura, em pixels.</param>
public sealed record ProgramColumnWidth(string Column, double Width);

/// <summary>
/// A memoria da grade: a ordem das colunas, a largura de cada uma e a
/// ordenacao escolhida.
///
/// A REGRA mora aqui: quem aplica a memoria na tela e quem grava o que a
/// pessoa mudou consomem este modulo. A lista de colunas visiveis continua
/// vindo do catalogo (<c>ProgramListLayout</c>); o que a pessoa ajustou e
/// uma camada por cima, nunca uma segunda lista de colunas.
///
/// A diferenca em relacao aos concorrentes: o Uninstall Tool memoriza a
/// largura e a posicao das colunas; o Revo Pro nao. Aqui a memoria existe,
/// e o que a pessoa deixa e o que volta quando o programa abre de novo.
/// </summary>
public static class ProgramColumnMemory
{
    /// <summary>A largura minima: abaixo disso o texto da coluna nao cabe.</summary>
    public const double MinWidth = 24;

    /// <summary>A largura maxima: acima disso a coluna come a janela.</summary>
    public const double MaxWidth = 1600;

    /// <summary>
    /// A ordem das colunas: a que a pessoa deixou vale, e as colunas que
    /// apareceram depois vao para o fim, na ordem do catalogo.
    ///
    /// Uma coluna que a pessoa escondeu e depois mostrou de novo nao perde a
    /// posicao: o nome dela continua na memoria, e ela volta para o lugar
    /// que tinha.
    /// </summary>
    /// <param name="visible">As colunas visiveis, na ordem do catalogo.</param>
    /// <param name="saved">A ordem guardada (o nome de cada coluna).</param>
    public static IReadOnlyList<ProgramColumn> Order(
        IReadOnlyList<ProgramColumn> visible,
        IEnumerable<string>? saved)
    {
        if (visible is null || visible.Count == 0) return visible ?? Array.Empty<ProgramColumn>();
        if (saved is null) return visible;

        var restantes = new List<ProgramColumn>(visible);
        var resultado = new List<ProgramColumn>();

        foreach (var nome in saved)
        {
            if (string.IsNullOrWhiteSpace(nome)) continue;

            // O indice e a busca: o enum nao aceita "null", e o valor
            // padrao do enum seria uma coluna valida (o nome).
            var indice = restantes.FindIndex(c =>
                string.Equals(c.ToString(), nome, StringComparison.OrdinalIgnoreCase));
            if (indice < 0) continue;

            resultado.Add(restantes[indice]);
            restantes.RemoveAt(indice);
        }

        resultado.AddRange(restantes);
        return resultado;
    }

    /// <summary>
    /// O identificador da coluna que fica na posicao dada da grade.
    ///
    /// A posicao conta as colunas de comando (a das caixas de selecao): elas
    /// nao sao colunas do catalogo, e por isso que o identificador delas e
    /// nulo. A primeira coluna do catalogo e a que vem depois delas.
    ///
    /// </summary>
    /// <param name="position">A posicao da coluna na grade.</param>
    /// <param name="visible">As colunas do catalogo que a grade mostra.</param>
    /// <param name="commandColumns">Quantas colunas de comando vem antes delas.</param>
    public static ProgramColumn? IdAt(
        int position,
        IReadOnlyList<ProgramColumn> visible,
        int commandColumns)
    {
        if (visible is null) return null;

        var indice = position - Math.Max(0, commandColumns);
        if (indice < 0 || indice >= visible.Count) return null;

        return visible[indice];
    }

    /// <summary>
    /// As larguras guardadas, por coluna.
    ///
    /// A largura invalida e descartada: um arquivo de preferencia editado a
    /// mao nao pode deixar a coluna com 3 pixels, nem com 40.000.
    /// </summary>
    /// <param name="widths">As larguras guardadas.</param>
    public static Dictionary<ProgramColumn, double> Read(IEnumerable<ProgramColumnWidth>? widths)
    {
        var resultado = new Dictionary<ProgramColumn, double>();
        if (widths is null) return resultado;

        foreach (var largura in widths)
        {
            if (largura is null) continue;
            if (!Enum.TryParse(largura.Column, true, out ProgramColumn id)) continue;
            if (largura.Width < MinWidth || largura.Width > MaxWidth) continue;

            resultado[id] = largura.Width;
        }

        return resultado;
    }

    /// <summary>
    /// A largura final da coluna: a que a pessoa deixou, ou o padrao.
    /// </summary>
    /// <param name="column">A coluna.</param>
    /// <param name="widths">As larguras guardadas.</param>
    /// <param name="padrao">A largura padrao da coluna.</param>
    public static double WidthOf(
        ProgramColumn column,
        IReadOnlyDictionary<ProgramColumn, double>? widths,
        double padrao) =>
        widths is not null && widths.TryGetValue(column, out var largura) ? largura : padrao;

    /// <summary>
    /// A ordem das colunas, como texto (o que vai para o arquivo de
    /// preferencias).
    /// </summary>
    /// <param name="columns">As colunas, na ordem escolhida.</param>
    public static List<string> NamesOf(IEnumerable<ProgramColumn>? columns) =>
        (columns ?? Array.Empty<ProgramColumn>())
            .Select(c => c.ToString())
            .ToList();

    /// <summary>
    /// As larguras, como texto (o que vai para o arquivo de preferencias).
    /// A largura invalida e descartada: ela nao chega ao arquivo.
    /// </summary>
    /// <param name="widths">As larguras, por coluna.</param>
    public static List<ProgramColumnWidth> WidthsOf(
        IReadOnlyDictionary<ProgramColumn, double>? widths) =>
        (widths ?? new Dictionary<ProgramColumn, double>())
            .Where(w => w.Value >= MinWidth && w.Value <= MaxWidth)
            .Select(w => new ProgramColumnWidth(w.Key.ToString(), w.Value))
            .ToList();
}
