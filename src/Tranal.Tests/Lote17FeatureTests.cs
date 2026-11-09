using System.IO;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Settings;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O painel ancorado e a pre-visualizacao das ferramentas.
///
/// E o "push pin" e o "Close Preview" do Uninstall Tool: a ferramenta que o
/// usuario fixa continua na lateral enquanto ele navega pelas outras, e o
/// painel mostra o que ja encontrou.
///
/// <para>A feature vive no nucleo: a janela (que mostra) e a linha de comando
/// (que informa) consomem o mesmo modulo, e a suite testa esse modulo. A
/// verificacao da camada de interface e feita sobre o fonte em disco, pelo
/// mesmo motivo das outras paridades: a suite nao referencia o projeto
/// grafico (WPF).</para>
/// </summary>
[Collection("preferencias")]
public class Lote17FeatureTests : IDisposable
{
    private readonly string _arquivo;
    private readonly string? _preferenciasAntigas;

    public Lote17FeatureTests()
    {
        _arquivo = Path.Combine(Path.GetTempPath(), "tranal-lote17-" + Guid.NewGuid().ToString("N")[..8] + ".json");

        _preferenciasAntigas = AppSettingsStore.FilePathOverride;
        AppSettingsStore.FilePathOverride = _arquivo;

        // As preferencias em memoria precisam vir do arquivo temporario:
        // sem isto, o teste gravaria no arquivo real do usuario.
        AppSettingsStore.ResetCurrent();

        PanelPreview.Reset();
    }

    public void Dispose()
    {
        PanelPreview.Reset();

        AppSettingsStore.FilePathOverride = _preferenciasAntigas;
        AppSettingsStore.ResetCurrent();

        try { if (File.Exists(_arquivo)) File.Delete(_arquivo); }
        catch { /* o temporario e descartavel */ }
    }

    // ------------------------------------------------------------------
    // A pre-visualizacao
    // ------------------------------------------------------------------

    [Fact]
    public void PainelQueNaoCarregouNaoTemNumeroNaPreVisualizacao()
    {
        var texto = PanelPreview.Text("AutorunManager", "Gerenciador de execucao automatica", "Itens de inicializacao");

        Assert.Contains("Gerenciador de execucao automatica", texto);
        Assert.Contains("Itens de inicializacao", texto);

        // O numero so entra depois que o painel carregou: antes disso nao ha
        // nada a mostrar — e mostrar "0 itens" seria mentira.
        Assert.DoesNotContain("encontrado", texto);
    }

    [Fact]
    public void PainelQueCarregouMostraQuantosItensEncontrou()
    {
        PanelPreview.Publish("AutorunManager", 7);

        var texto = PanelPreview.Text("AutorunManager", "Autorun", "Itens de inicializacao");

        Assert.Contains("7 itens encontrados", texto);
    }

    [Fact]
    public void UMItemEncontradoFalaNoSingular()
    {
        PanelPreview.Publish("RecycleBin", 1);

        var texto = PanelPreview.Text("RecycleBin", "Lixeira", "Arquivos apagados");

        Assert.Contains("1 item encontrado", texto);
        Assert.DoesNotContain("1 itens", texto);
    }

    [Fact]
    public void ONumeroSoEntraDepoisQueOPainelCarregou()
    {
        Assert.Null(PanelPreview.Find("AutorunManager"));

        PanelPreview.Publish("AutorunManager", 3);

        var info = PanelPreview.Find("AutorunManager");

        Assert.NotNull(info);
        Assert.True(info!.HasCount);
        Assert.Equal(3, info.ItemCount);
        Assert.Equal("AutorunManager", info.ToolId);
    }

    [Fact]
    public void PainelRecriadoEsqueceONumeroAntigo()
    {
        PanelPreview.Publish("AutorunManager", 3);
        PanelPreview.Forget("AutorunManager");

        Assert.Null(PanelPreview.Find("AutorunManager"));
    }

    [Fact]
    public void PublicarComIdVazioNaoCriaNada()
    {
        PanelPreview.Publish("   ", 5);

        Assert.Empty(PanelPreview.All());
    }

    [Fact]
    public void ResetEsqueceTodasAsPreVisualizacoes()
    {
        PanelPreview.Publish("AutorunManager", 1);
        PanelPreview.Publish("RecycleBin", 2);

        Assert.Equal(2, PanelPreview.All().Count);

        PanelPreview.Reset();

        Assert.Empty(PanelPreview.All());
    }

    // ------------------------------------------------------------------
    // O painel ancorado
    // ------------------------------------------------------------------

    [Fact]
    public void FixarUmaFerramentaAncoraOPainelDela()
    {
        Assert.True(DockedPanel.Toggle("AutorunManager"));

        Assert.True(DockedPanel.HasPanel);
        Assert.Equal("AutorunManager", DockedPanel.ToolId);
        Assert.True(DockedPanel.IsDocked("AutorunManager"));
    }

    [Fact]
    public void FixarDeNovoSoltaOPainel()
    {
        DockedPanel.Toggle("AutorunManager");

        Assert.False(DockedPanel.Toggle("AutorunManager"));

        Assert.False(DockedPanel.HasPanel);
        Assert.Equal(string.Empty, DockedPanel.ToolId);
    }

    [Fact]
    public void ModuloNaoPodeSerAncorado()
    {
        // A lista de programas e a tela principal nao podem virar um painel
        // lateral: so uma ferramenta pode ser fixada.
        Assert.False(DockedPanel.Toggle("AllPrograms"));
        Assert.False(DockedPanel.HasPanel);
        Assert.Equal(string.Empty, DockedPanel.ToolId);
    }

    [Fact]
    public void OPainelAncoradoFicaGravadoNasPreferencias()
    {
        DockedPanel.Toggle("AutorunManager");

        Assert.Equal("AutorunManager", AppSettingsStore.Load().DockedToolId);
    }

    [Fact]
    public void SoltarOPainelTambemGrava()
    {
        DockedPanel.Toggle("AutorunManager");
        DockedPanel.Undock();

        Assert.Equal(string.Empty, AppSettingsStore.Load().DockedToolId);
        Assert.False(DockedPanel.HasPanel);
    }

    [Fact]
    public void ADescricaoDoPainelAncoradoVemDoCatalogo()
    {
        Assert.Equal(Loc.Get("Panel.NoneDocked"), DockedPanel.Describe());

        DockedPanel.Toggle("AutorunManager");

        Assert.Equal(Loc.Get("Panel.Docked", Loc.Get("Module.AutorunManager")), DockedPanel.Describe());
    }

    [Fact]
    public void TodaFerramentaDoCatalogoPodeSerAncorada()
    {
        foreach (var id in ToolCatalog.All)
        {
            Assert.True(DockedPanel.Toggle(id), $"Nao ancrou: {id}");
            Assert.True(DockedPanel.IsDocked(id));
        }
    }

    // ------------------------------------------------------------------
    // A interface e a linha de comando consomem o mesmo modulo
    // ------------------------------------------------------------------

    [Fact]
    public void AJanelaTemOBotaoEOPainelAncorado()
    {
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml");
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        Assert.Contains("x:Name=\"DockPanelButton\"", xaml);
        Assert.Contains("x:Name=\"DockPanelBorder\"", xaml);
        Assert.Contains("x:Name=\"DockPanelHost\"", xaml);
        Assert.Contains("DockPanelButton_Click", xaml);
        Assert.Contains("DockPanelCloseButton_Click", xaml);

        Assert.Contains("DockedPanel.Toggle(id);", codigo);
        Assert.Contains("DockedPanel.Undock();", codigo);
        Assert.Contains("PanelPreview.Publish(id, panel.ItemCount);", codigo);
        Assert.Contains("botao.ToolTip = PanelPreview.Text(", codigo);
    }

    [Fact]
    public void OPainelDaListaContaOsItensDaFonte()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Shared", "ItemListPanel.xaml.cs");

        Assert.Contains("ItemsList.ItemsSource", codigo);
        Assert.Contains("ItemsList.Items.Count", codigo);

        // A contagem antiga percorria "Items", que nao existe em um
        // UserControl: o numero da pre-visualizacao nunca saia.
        Assert.DoesNotContain("foreach (var _ in Items)", codigo);
    }

    [Fact]
    public void ACliTemOComandoDoPainel()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Cli", "ExtraCommands.cs");

        Assert.Contains("flags.Contains(\"/painel\")", codigo);
        Assert.Contains("DockedPanel.Toggle(id);", codigo);
        Assert.Contains("DockedPanel.Undock();", codigo);
        Assert.Contains("DockedPanel.Describe()", codigo);
    }

    [Fact]
    public void OsDoisCatalogosTrazemAsChavesDoPainelAncorado()
    {
        var pt = RepoFiles.Read("src", "Tranal.Uninstaller", "Lang", "pt-BR.json");
        var en = RepoFiles.Read("src", "Tranal.Uninstaller", "Lang", "en-US.json");

        var chaves = new[]
        {
            "Panel.Dock",
            "Panel.Undock",
            "Panel.Docked",
            "Panel.NoneDocked",
            "Panel.NoTool",
            "Preview.OneItem",
            "Preview.ManyItems",
        };

        foreach (var chave in chaves)
        {
            Assert.Contains("\"" + chave + "\"", pt);
            Assert.Contains("\"" + chave + "\"", en);
        }
    }
}
