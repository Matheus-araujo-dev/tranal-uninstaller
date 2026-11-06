namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// Um item do mapa de area: o que ele representa, como ele se chama na tela
/// e qual e o seu tamanho.
/// </summary>
/// <param name="Key">A identidade do item (a chave do registro do programa).</param>
/// <param name="Label">O nome que aparece desenhado no retangulo.</param>
/// <param name="Size">O tamanho do item, em bytes. Zero quando o tamanho e desconhecido.</param>
public sealed record TreemapEntry(string Key, string Label, long Size);

/// <summary>
/// Um retangulo do mapa de area: onde ele fica e o tamanho dele.
///
/// A posicao e a medida do retangulo ja prontas para desenhar — quem desenha
/// (a tela) nao precisa saber nada de algoritmo.
/// </summary>
/// <param name="Key">A identidade do item que ocupa o retangulo.</param>
/// <param name="X">A distancia da borda esquerda, em pixels.</param>
/// <param name="Y">A distancia da borda superior, em pixels.</param>
/// <param name="Width">A largura do retangulo, em pixels.</param>
/// <param name="Height">A altura do retangulo, em pixels.</param>
public readonly record struct TreemapRect(string Key, double X, double Y, double Width, double Height)
{
    /// <summary>A area do retangulo, em pixels quadrados.</summary>
    public double Area => Width * Height;
}

/// <summary>
/// O mapa de area dos programas: cada programa e um retangulo, e o retangulo
/// e grande conforme o programa ocupa espaco.
///
/// O algoritmo e o <em>squarified treemap</em> de Bruls, Huizing e van Wijk:
/// os itens sao ordenados do maior para o menor e encaixados em faixas, de
/// modo que cada retangulo fique o mais quadrado possivel. Um mapa de area
/// com retangulos fininhos e ilegivel, e o algoritmo existe justamente para
/// evitar isso.
///
/// O modelo e de nucleo de proposito: o mesmo desenho serve para a tela e para
/// a CLI, e o teste confere a propriedade que importa — a area do retangulo e
/// proporcional ao tamanho do programa.
/// </summary>
public sealed class TreemapModel
{
    private TreemapModel(
        IReadOnlyList<TreemapRect> rectangles,
        IReadOnlyList<TreemapEntry> entries,
        long totalSize,
        double width,
        double height)
    {
        Rectangles = rectangles;
        Entries = entries;
        TotalSize = totalSize;
        Width = width;
        Height = height;
    }

    /// <summary>Os retangulos, ja posicionados dentro da area do mapa.</summary>
    public IReadOnlyList<TreemapRect> Rectangles { get; }

    /// <summary>
    /// Os itens que entraram no mapa, do maior para o menor. Os itens sem
    /// tamanho conhecido ficam de fora: eles nao tem area a desenhar.
    /// </summary>
    public IReadOnlyList<TreemapEntry> Entries { get; }

    /// <summary>
    /// A soma dos tamanhos dos itens. E o que o mapa inteiro representa.
    /// </summary>
    public long TotalSize { get; }

    /// <summary>A largura do mapa, em pixels.</summary>
    public double Width { get; }

    /// <summary>A altura do mapa, em pixels.</summary>
    public double Height { get; }

    /// <summary>
    /// A area total ocupada pelos retangulos. Quando ha tamanho a medir, ela
    /// bate com a area do mapa (<see cref="Width"/> x <see cref="Height"/>).
    /// </summary>
    public double TotalArea
    {
        get
        {
            var total = 0.0;
            foreach (var rect in Rectangles) total += rect.Area;
            return total;
        }
    }

    /// <summary>
    /// Monta o mapa dos itens informados.
    ///
    /// Itens com tamanho zero (ou negativo) nao entram: sem medida nao ha
    /// area proporcional para desenhar, e um retangulo de area zero seria uma
    /// mentira. A ordem dos itens com o mesmo tamanho e a ordem de entrada —
    /// o desenho e deterministico.
    /// </summary>
    /// <param name="entries">Os itens a desenhar.</param>
    /// <param name="width">A largura do mapa, em pixels.</param>
    /// <param name="height">A altura do mapa, em pixels.</param>
    public static TreemapModel Build(IReadOnlyList<TreemapEntry>? entries, double width, double height)
    {
        var items = new List<TreemapEntry>();
        long total = 0;

        if (entries is not null)
        {
            foreach (var entry in entries)
            {
                if (entry is null) continue;
                if (entry.Size <= 0) continue;

                items.Add(entry);
                total += entry.Size;
            }
        }

        // Do maior para o menor. A ordenacao e estavel: empate de tamanho
        // mantem a ordem de entrada, e o desenho nao muda entre execucoes.
        var ordenados = items
            .OrderByDescending(e => e.Size)
            .ToList();

        var rectangles = new List<TreemapRect>();

        if (ordenados.Count > 0 && total > 0 && width > 0 && height > 0)
        {
            var areaTotal = width * height;
            var areas = new List<KeyValuePair<string, double>>(ordenados.Count);
            foreach (var entry in ordenados)
                areas.Add(new KeyValuePair<string, double>(entry.Key, (double)entry.Size / total * areaTotal));

            rectangles = Layout(areas, width, height);
        }

        return new TreemapModel(rectangles, ordenados, total, width, height);
    }

    /// <summary>
    /// Monta o mapa a partir da lista de programas: o tamanho de cada um e o
    /// tamanho estimado (em bytes) e o nome e o nome exibido.
    /// </summary>
    /// <param name="programs">Os programas a desenhar.</param>
    /// <param name="width">A largura do mapa, em pixels.</param>
    /// <param name="height">A altura do mapa, em pixels.</param>
    public static TreemapModel FromPrograms(IEnumerable<InstalledProgram>? programs, double width, double height)
    {
        var entries = new List<TreemapEntry>();

        if (programs is not null)
        {
            foreach (var program in programs)
            {
                if (program is null) continue;

                var key = program.RegistryPath;
                if (string.IsNullOrWhiteSpace(key)) key = program.DisplayName;

                entries.Add(new TreemapEntry(key, program.DisplayName, program.SizeBytes));
            }
        }

        return Build(entries, width, height);
    }

    /// <summary>
    /// O encaixe dos retangulos: o algoritmo de Bruls, Huizing e van Wijk.
    ///
    /// A cada passo, uma faixa de itens e montada enquanto a proporcao
    /// (aspecto) dos retangulos melhorar; a faixa ocupa uma tira da lado
    /// mais curto da area que sobra, e a area que sobra e reavaliada. O
    /// resultado e deterministico e cobre a area inteira sem sobrepor
    /// retangulo nenhum.
    /// </summary>
    private static List<TreemapRect> Layout(
        List<KeyValuePair<string, double>> areas, double width, double height)
    {
        var result = new List<TreemapRect>(areas.Count);

        var x = 0.0;
        var y = 0.0;
        var w = width;
        var h = height;

        var index = 0;
        while (index < areas.Count)
        {
            var shortSide = Math.Min(w, h);
            if (shortSide <= 0) break;

            var row = new List<KeyValuePair<string, double>>();
            var rowArea = 0.0;

            while (index < areas.Count)
            {
                var candidate = areas[index];
                var candidateArea = rowArea + candidate.Value;

                if (row.Count == 0)
                {
                    row.Add(candidate);
                    rowArea = candidateArea;
                    index++;
                    continue;
                }

                var current = Worst(row, rowArea, shortSide);
                var next = Worst(row, candidateArea, shortSide, candidate.Value);

                // A faixa so cresce enquanto o item novo deixa a proporcao
                // dos retangulos melhor (ou igual).
                if (next > current) break;

                row.Add(candidate);
                rowArea = candidateArea;
                index++;
            }

            if (row.Count == 0) break;

            // A espessura da tira: a area da faixa dividida pelo lado que ela
            // ocupa. Os itens da faixa se enchem ao longo desse lado.
            var thickness = rowArea / shortSide;

            if (w >= h)
            {
                var used = 0.0;
                foreach (var item in row)
                {
                    var itemHeight = item.Value / thickness;
                    result.Add(new TreemapRect(item.Key, x, y + used, thickness, itemHeight));
                    used += itemHeight;
                }

                x += thickness;
                w -= thickness;
            }
            else
            {
                var used = 0.0;
                foreach (var item in row)
                {
                    var itemWidth = item.Value / thickness;
                    result.Add(new TreemapRect(item.Key, x + used, y, itemWidth, thickness));
                    used += itemWidth;
                }

                y += thickness;
                h -= thickness;
            }
        }

        return result;
    }

    /// <summary>
    /// A pior proporcao (aspecto) de uma faixa: o quadrado mais desproporcional
    /// que ela contem. E a medida que o algoritmo quer manter baixa — quanto
    /// menor, mais quadrados sao os retangulos.
    /// </summary>
    private static double Worst(
        List<KeyValuePair<string, double>> row, double rowArea, double shortSide, double? extraArea = null)
    {
        // O maior e o menor item da faixa — com o item candidato incluido
        // quando a chamada e a da faixa com o proximo item.
        var maxArea = 0.0;
        var minArea = double.MaxValue;

        foreach (var item in row)
        {
            if (item.Value > maxArea) maxArea = item.Value;
            if (item.Value < minArea) minArea = item.Value;
        }

        if (extraArea.HasValue)
        {
            if (extraArea.Value > maxArea) maxArea = extraArea.Value;
            if (extraArea.Value < minArea) minArea = extraArea.Value;
        }

        if (maxArea <= 0 || minArea <= 0 || minArea == double.MaxValue || rowArea <= 0)
            return double.PositiveInfinity;

        var lado = shortSide * shortSide;
        return Math.Max(lado * maxArea / (rowArea * rowArea), (rowArea * rowArea) / (lado * minArea));
    }
}
