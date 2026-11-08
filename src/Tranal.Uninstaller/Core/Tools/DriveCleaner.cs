using Tranal.Uninstaller.Core.Reporting;
using System.IO;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Uma categoria de arquivos desnecessarios, como o ashampoo Drive Cleaner
/// organiza a limpeza.
/// </summary>
public sealed class DriveCleanerCategory
{
    /// <summary>Identificador estavel da categoria.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Nome exibido da categoria.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>True quando a categoria entra marcada na varredura.</summary>
    public bool SelectedByDefault { get; init; } = true;

    /// <summary>As pastas que a categoria varre.</summary>
    public IReadOnlyList<string> Folders { get; init; } = Array.Empty<string>();

    /// <summary>O padrao de arquivo dentro das pastas (vazio = tudo).</summary>
    public string Pattern { get; init; } = "*";

    /// <summary>True quando a categoria varre o CONTEUDO das pastas, sem apagar a pasta.</summary>
    public bool KeepsFolder { get; init; } = true;
}

/// <summary>
/// Um item encontrado pela varredura do Drive Cleaner.
/// </summary>
public sealed class DriveCleanerItem
{
    /// <summary>O identificador da categoria.</summary>
    public string CategoryId { get; init; } = string.Empty;

    /// <summary>O nome da categoria.</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>"Arquivo" ou "Pasta".</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>O caminho do item.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Tamanho ocupado, em bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>True quando o item esta marcado para limpeza.</summary>
    public bool Selected { get; set; } = true;
}

/// <summary>
/// Limpeza de arquivos desnecessarios do disco, organizada por categoria.
///
/// O ashampoo Drive Cleaner mostra a lista agrupada por categoria (temporarios,
/// cache, logs, miniaturas) com o tamanho de cada grupo e um relatorio do que
/// foi apagado. O IObit e o HiBit tem o mesmo nucleo com nomes diferentes.
///
/// A REGRA do que e "desnecessario" mora aqui, em um lugar so: as categorias
/// sao declaradas aqui e quem varre, limpa e relata consome esta lista.
///
/// Limpeza de temporario e definitiva: nao vai para a Lixeira e nao cria ponto
/// de restauracao. O que estiver travado pelo navegador ou pelo Explorer fica
/// agendado para o proximo reinicio, pelo motor canonico.
/// </summary>
public static class DriveCleaner
{
    /// <summary>
    /// As categorias de limpeza, com os caminhos resolvidos no momento do uso.
    /// </summary>
    private static IReadOnlyList<DriveCleanerCategory> BuildCategories()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        return new List<DriveCleanerCategory>
        {
            new()
            {
                Id = "temporarios-usuario",
                Label = "Temporarios do usuario",
                SelectedByDefault = true,
                Folders = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Path.GetTempPath(),
                },
                Pattern = "*.tmp",
            },
            new()
            {
                Id = "temporarios-windows",
                Label = "Temporarios do Windows",
                SelectedByDefault = true,
                Folders = new[] { Path.Combine(windows, "Temp") },
                Pattern = "*",
            },
            new()
            {
                Id = "cache-miniaturas",
                Label = "Cache de miniaturas",
                SelectedByDefault = true,
                Folders = new[] { Path.Combine(local, "Microsoft", "Windows", "Explorer") },
                Pattern = "thumbcache_*.db",
            },
            new()
            {
                Id = "cache-windows-update",
                Label = "Cache do Windows Update",
                SelectedByDefault = false,
                Folders = new[] { Path.Combine(windows, "SoftwareDistribution", "Download") },
                Pattern = "*",
                KeepsFolder = false,
            },
            new()
            {
                Id = "relatorios-de-erro",
                Label = "Relatorios de erro do Windows",
                SelectedByDefault = true,
                Folders = new[] { Path.Combine(local, "Microsoft", "Windows", "WER") },
                Pattern = "*",
                KeepsFolder = true,
            },
            new()
            {
                Id = "cache-directx",
                Label = "Cache de shaders do DirectX",
                SelectedByDefault = true,
                Folders = new[] { Path.Combine(local, "D3DSCache") },
                Pattern = "*",
            },
            new()
            {
                Id = "cache-instaladores",
                Label = "Cache dos instaladores",
                SelectedByDefault = false,
                Folders = new[] { Path.Combine(windows, "Installer") },
                Pattern = "*.tmp",
            },
        };
    }

    /// <summary>
    /// As categorias de limpeza.
    /// </summary>
    public static IReadOnlyList<DriveCleanerCategory> Categories() => BuildCategories();

    /// <summary>
    /// Varre o disco e devolve os itens por categoria.
    /// </summary>
    public static IReadOnlyList<DriveCleanerItem> Scan()
    {
        var itens = new List<DriveCleanerItem>();

        foreach (var categoria in BuildCategories())
        {
            foreach (var pasta in categoria.Folders)
            {
                try
                {
                    if (!Directory.Exists(pasta)) continue;
                    itens.AddRange(ScanFolder(categoria, pasta));
                }
                catch (Exception ex)
                {
                    ToolLog.Error($"Falha ao varrer '{pasta}': {ex.Message}");
                }
            }
        }

        return itens;
    }

    /// <summary>
    /// O total ocupado por cada categoria.
    /// </summary>
    public static IReadOnlyList<(string Category, long Bytes)> Summary(IEnumerable<DriveCleanerItem> items)
    {
        return items
            .GroupBy(i => i.Category, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Category: g.Key, Bytes: g.Sum(i => i.SizeBytes)))
            .OrderByDescending(p => p.Bytes)
            .ToList();
    }

    /// <summary>
    /// Limpa os itens marcados e devolve o relatorio canonico.
    /// </summary>
    /// <param name="items">Os itens marcados para limpeza.</param>
    public static RemovalReport Clear(IEnumerable<DriveCleanerItem> items)
    {
        var outcomes = new List<RemovalOutcome>();
        var sobras = new List<LeftoverItem>();

        foreach (var item in items)
        {
            if (!item.Selected) continue;

            sobras.Add(new LeftoverItem
            {
                Kind = item.Kind,
                Path = item.Path,
                Detail = item.Category,
                Selected = true,
            });
        }

        if (sobras.Count == 0) return new RemovalReport(outcomes);

        try
        {
            outcomes.AddRange(UninstallEngine.RemoveLeftovers(sobras, new UninstallRemovalOptions
            {
                Backup = false,
                UseRecycleBin = false,
                DeferToNextRestart = true,
            }).Outcomes);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha na limpeza do disco: {ex.Message}");
        }

        return new RemovalReport(outcomes);
    }

    /// <summary>
    /// Gera o relatorio em HTML da limpeza.
    /// </summary>
    /// <param name="path">O caminho do arquivo HTML.</param>
    /// <param name="items">Os itens varridos.</param>
    /// <param name="report">O relatorio da limpeza (pode ser nulo).</param>
    public static string WriteReport(string path, IReadOnlyList<DriveCleanerItem> items, RemovalReport? report)
    {
        var resumo = Summary(items);
        var total = resumo.Sum(p => p.Bytes);

        var subtitulo = report is null
            ? $"{items.Count} item(ns) encontrado(s) — {ByteFormatter.Format(total)}"
            : $"{items.Count} item(ns) — {ByteFormatter.Format(total)} — "
              + $"{report.Outcomes.Count(o => o.Removed)} removido(s), "
              + $"{report.Outcomes.Count(o => o.Status == RemovalStatus.Deferred)} adiado(s)";

        var linhas = new List<ToolReportRow>();
        foreach (var item in items)
        {
            linhas.Add(new ToolReportRow
            {
                Cells = new[] { item.Category, item.Kind, item.Path, ByteFormatter.Format(item.SizeBytes) },
            });
        }

        return ToolReportWriter.Write(
            path,
            "Drive Cleaner",
            subtitulo,
            new List<ToolReportColumn>
            {
                new() { Header = "Categoria", Width = 180 },
                new() { Header = "Tipo", Width = 80 },
                new() { Header = "Caminho", Width = 520 },
                new() { Header = "Tamanho", Width = 110 },
            },
            linhas);
    }


    /// <summary>
    /// Os itens de uma pasta, conforme a categoria.
    /// </summary>
    private static IEnumerable<DriveCleanerItem> ScanFolder(DriveCleanerCategory categoria, string pasta)
    {
        if (categoria.KeepsFolder)
        {
            foreach (var arquivo in Directory.EnumerateFiles(pasta, categoria.Pattern, SearchOption.TopDirectoryOnly))
            {
                yield return new DriveCleanerItem
                {
                    CategoryId = categoria.Id,
                    Category = categoria.Label,
                    Kind = "Arquivo",
                    Path = PathUtil.Normalize(arquivo),
                    SizeBytes = SafeLength(arquivo),
                };
            }

            yield break;
        }

        // A categoria remove a pasta inteira: o conteudo dela e o alvo.
        foreach (var arquivo in Directory.EnumerateFiles(pasta, categoria.Pattern, SearchOption.AllDirectories))
        {
            yield return new DriveCleanerItem
            {
                CategoryId = categoria.Id,
                Category = categoria.Label,
                Kind = "Arquivo",
                Path = PathUtil.Normalize(arquivo),
                SizeBytes = SafeLength(arquivo),
            };
        }
    }

    /// <summary>
    /// O tamanho de um arquivo, quando ele pode ser lido.
    /// </summary>
    private static long SafeLength(string path)
    {
        try { return new FileInfo(path).Length; }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao medir '{path}': {ex.Message}");
            return 0;
        }
    }
}
