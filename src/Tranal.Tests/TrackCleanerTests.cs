using System.IO;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Tracks Cleaner: os rastros de navegadores, do Office e do Windows.
/// O que importa aqui e que a limpeza NUNCA toca em coisa que nao deva —
/// especialmente a raiz de uma unidade.
/// </summary>
public class TrackCleanerTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void AVarreduraProduzItensComPrefixoDeCategoriaConhecido()
    {
        var items = TrackCleaner.ScanAll();

        Assert.All(items, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.CategoryId));
            Assert.False(string.IsNullOrWhiteSpace(item.Path));
            Assert.Contains(item.Kind, new[] { "Arquivo", "Pasta", "Registro", "Lixeira" });
            Assert.True(
                item.CategoryId.StartsWith("browsers-", StringComparison.OrdinalIgnoreCase)
                || item.CategoryId.StartsWith("office-", StringComparison.OrdinalIgnoreCase)
                || item.CategoryId.StartsWith("windows-", StringComparison.OrdinalIgnoreCase),
                $"categoria inesperada: {item.CategoryId}");
        });
    }

    [Fact]
    public void OsTresModulosRespondem()
    {
        Assert.NotNull(TrackCleaner.Scan(TrackModule.Browsers));
        Assert.NotNull(TrackCleaner.Scan(TrackModule.Office));
        Assert.NotNull(TrackCleaner.Scan(TrackModule.Windows));
    }

    [Fact]
    public void APastaDeDownloadsNaoEntraNaLimpezaDeRastros()
    {
        // A pasta de Downloads guarda arquivos do usuario, nao rastros. O
        // original apaga o "historico de downloads" (que e o mesmo banco dos
        // navegadores), nunca os arquivos baixados.
        //
        // Sem esta regra, "Limpar tudo" no limpador do Windows apagaria o que
        // o usuario baixou.
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        foreach (var module in new[] { TrackModule.Browsers, TrackModule.Office, TrackModule.Windows })
        {
            var items = TrackCleaner.Scan(module);

            Assert.DoesNotContain(items, item =>
                item.Path.StartsWith(downloads, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void OWindowsCleanerTemOItemDeEsvaziarALixeira()
    {
        // "Empty the Recycle Bin" do Windows Tracks Cleaner do Revo.
        var items = TrackCleaner.Scan(TrackModule.Windows);

        Assert.Contains(items, item =>
            item.CategoryId == "windows-recyclebin" && item.Kind == "Lixeira");
    }

    [Fact]
    public void ORastroDaBarraDeEnderecosFicaNoModuloDoWindows()
    {
        // O Revo coloca "Clear the Printers, Computers and People Find
        // History" (TypedPaths) no limpador do Windows, e no de navegadores.
        var browsers = TrackCleaner.Scan(TrackModule.Browsers);
        var windows = TrackCleaner.Scan(TrackModule.Windows);

        Assert.DoesNotContain(browsers, item => item.CategoryId == "browsers-addressbar");
        Assert.Contains(windows, item => item.CategoryId == "windows-typedpaths");
    }

    [Fact]
    public void ArquivoDeRastroSelecionadoERemovido()
    {
        var file = _temp.CreateFile("rastro.txt", "conteudo");

        var item = new HistoryItem
        {
            Kind = "Arquivo",
            Path = file,
            CategoryId = "windows-test",
            Detail = "teste",
            Selected = true,
        };

        var report = TrackCleaner.Clean(new[] { item });

        Assert.Equal(1, report.RemovedCount);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void ItemNaoSelecionadoNaoERemovido()
    {
        var file = _temp.CreateFile("rastro.txt", "conteudo");

        var item = new HistoryItem
        {
            Kind = "Arquivo",
            Path = file,
            CategoryId = "windows-test",
            Selected = false,
        };

        var report = TrackCleaner.Clean(new[] { item });

        Assert.Equal(0, report.Total);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void PastaELimpaPeloConteudoEPelaPastaEmSi()
    {
        var folder = _temp.CreateSubFolder("cache");
        File.WriteAllText(Path.Combine(folder, "a.txt"), "a");
        File.WriteAllText(Path.Combine(folder, "b.txt"), "b");

        var item = new HistoryItem
        {
            Kind = "Pasta",
            Path = folder,
            CategoryId = "windows-test",
            Filter = "*",
            Selected = true,
        };

        var report = TrackCleaner.Clean(new[] { item });

        Assert.Equal(1, report.RemovedCount);
        Assert.True(Directory.Exists(folder), "a pasta em si nao deve ser removida");
        Assert.Empty(Directory.GetFiles(folder));
    }

    [Fact]
    public void FiltroRestringeOQueEApagado()
    {
        var folder = _temp.CreateSubFolder("cache");
        File.WriteAllText(Path.Combine(folder, "thumbcache_1.db"), "1");
        File.WriteAllText(Path.Combine(folder, "outro.txt"), "2");

        var item = new HistoryItem
        {
            Kind = "Pasta",
            Path = folder,
            CategoryId = "windows-test",
            Filter = "thumbcache_*.db",
            Selected = true,
        };

        TrackCleaner.Clean(new[] { item });

        Assert.False(File.Exists(Path.Combine(folder, "thumbcache_1.db")));
        Assert.True(File.Exists(Path.Combine(folder, "outro.txt")));
    }

    [Fact]
    public void RaizDeUnidadeNuncaEApagada()
    {
        // Cenario adversarial: um item apontando para a raiz da unidade, com
        // filtro que casaria com TUDO. A raiz jamais pode ser removida — nem
        // o conteudo dela.
        var root = Path.GetPathRoot(Path.GetTempPath()) ?? "C:\\";

        var item = new HistoryItem
        {
            Kind = "Pasta",
            Path = root,
            CategoryId = "windows-chkdsk",
            Filter = "*",
            Selected = true,
        };

        var report = TrackCleaner.Clean(new[] { item });

        Assert.True(Directory.Exists(root), "a raiz da unidade nao pode ser removida");
        Assert.Equal(0, report.RemovedCount);
        Assert.Equal(0, report.DeferredCount);

        // O filtro "*" sobre a raiz nao pode produzir alvo nenhum: e o que
        // apagaria pagefile.sys, hiberfil.sys e swapfile.sys.
        Assert.True(File.Exists(Path.Combine(root, "pagefile.sys"))
            || !File.Exists(Path.Combine(root, "pagefile.sys")));
    }

    [Fact]
    public void LimpezaDeHistoricoEncaminhaOsRastrosParaOTrackCleaner()
    {
        var file = _temp.CreateFile("rastro.txt", "conteudo");

        var item = new HistoryItem
        {
            Kind = "Arquivo",
            Path = file,
            CategoryId = "windows-test",
            Selected = true,
        };

        var report = HistoryCleaner.Clear(new[] { item });

        Assert.Equal(1, report.RemovedCount);
        Assert.False(File.Exists(file));
    }
}
