using System.IO;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Updates;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// A Central: o resumo, em um lugar so, do que precisa de atencao.
///
/// E o "Action Center" do IObit, medido no binario dele: um painel que
/// junta o estado de cada area e a acao de cada item. Aqui cada item leva
/// a ferramenta do Tranal que age sobre ele.
///
/// <para>A regra mora no nucleo (<c>Core/Modules/CentralOverview.cs</c>) e a
/// suite testa esse modulo. A ligacao com a interface (o botao na barra
/// lateral, a tela e a linha de comando) e conferida sobre o fonte, pelo
/// mesmo motivo das outras paridades: a suite nao referencia o projeto
/// grafico (WPF).</para>
/// </summary>
[Collection("preferencias")]
public class CentralFeatureTests : IDisposable
{
    private readonly string _temporaria;
    private readonly string? _pastaAntiga;

    public CentralFeatureTests()
    {
        _temporaria = Path.Combine(Path.GetTempPath(), "tranal-central-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_temporaria);

        // A linha de base dos programas novos fica num arquivo proprio: o
        // teste nao pode mexer no arquivo real de quem roda.
        _pastaAntiga = NewInstallsTracker.FolderOverride;
        NewInstallsTracker.FolderOverride = Path.Combine(_temporaria, "new-installs");
    }

    public void Dispose()
    {
        NewInstallsTracker.FolderOverride = _pastaAntiga;

        try { if (Directory.Exists(_temporaria)) Directory.Delete(_temporaria, true); }
        catch { /* o temporario e descartavel */ }
    }

    // ------------------------------------------------------------------
    // A regra
    // ------------------------------------------------------------------

    [Fact]
    public void SemProgramasACentralEstaLimpa()
    {
        var report = CentralOverview.Build(Array.Empty<InstalledProgram>());

        Assert.True(report.IsClear);
        Assert.Equal(0, report.Total);
        Assert.Empty(report.Items);
    }

    [Fact]
    public void AtualizacaoPendenteViraItemDaCentral()
    {
        var plano = new UpdatePlan(new[]
        {
            new UpdatePlanItem(
                new UpdateCandidate("Editor", "Fabrica", "1.0", "2.0", "editor", "https://exemplo"),
                Pending: true,
                Reason: string.Empty),
        });

        var report = CentralOverview.Build(Array.Empty<InstalledProgram>(), plano);

        var item = Assert.Single(report.Items);
        Assert.Equal(CentralKind.Update, item.Kind);
        Assert.Equal(CentralPriority.Medium, item.Priority);
        Assert.Equal(ToolCatalog.SoftwareUpdates, item.ToolId);
        Assert.Contains("1.0", item.Title);
        Assert.Contains("2.0", item.Title);
        Assert.Equal("Editor", item.Target);
    }

    [Fact]
    public void AtualizacaoRecusadaNaoViraItem()
    {
        // O plano decidiu nao atualizar (lista negra): a Central nao
        // inventa o que o plano deixou de fora.
        var plano = new UpdatePlan(new[]
        {
            new UpdatePlanItem(
                new UpdateCandidate("Editor", "Fabrica", "1.0", "2.0", "editor", "https://exemplo"),
                Pending: false,
                Reason: "lista negra"),
        });

        var report = CentralOverview.Build(Array.Empty<InstalledProgram>(), plano);

        Assert.True(report.IsClear);
    }

    [Fact]
    public void ProgramaSemDesinstaladorViraItemDeSaude()
    {
        var programas = new[]
        {
            new InstalledProgram { DisplayName = "Sem Desinstalador", RegistryKey = "chave" },
        };

        var report = CentralOverview.Build(programas);

        var item = Assert.Single(report.Items);
        Assert.Equal(CentralKind.Health, item.Kind);
        Assert.Equal(ToolCatalog.SystemManagement, item.ToolId);
        Assert.Contains("Sem Desinstalador", item.Title);
    }

    [Fact]
    public void ProgramaNovoViraItem()
    {
        // A linha de base e gravada com um programa so: o outro apareceu
        // depois e e, portanto, novo.
        NewInstallsTracker.SaveBaseline(new[]
        {
            new InstalledProgram { DisplayName = "Antigo", RegistryKey = "antigo" },
        });

        var programas = new[]
        {
            new InstalledProgram { DisplayName = "Antigo", RegistryKey = "antigo" },
            new InstalledProgram { DisplayName = "Recem Chegado", RegistryKey = "novo" },
        };

        var report = CentralOverview.Build(programas, incluirSaude: false);

        var item = Assert.Single(report.Items);
        Assert.Equal(CentralKind.NewProgram, item.Kind);
        Assert.Equal(ToolCatalog.NewInstalls, item.ToolId);
        Assert.Contains("Recem Chegado", item.Title);
    }

    [Fact]
    public void VersaoNovaDoTranalViraItem()
    {
        var report = CentralOverview.Build(
            Array.Empty<InstalledProgram>(),
            versaoPublicadaTranal: "99.0.0");

        var item = Assert.Single(report.Items);
        Assert.Equal(CentralKind.SelfUpdate, item.Kind);
        Assert.Equal(CentralPriority.High, item.Priority);
        Assert.Equal(ToolCatalog.AppUpdate, item.ToolId);
        Assert.Contains("99.0.0", item.Title);
    }

    [Fact]
    public void VersaoIgualNaoViraItem()
    {
        var report = CentralOverview.Build(
            Array.Empty<InstalledProgram>(),
            versaoPublicadaTranal: "1.0.0");

        Assert.True(report.IsClear);
    }

    [Fact]
    public void AOrdemEConsistente()
    {
        // O mais grave vem primeiro: uma Central que troca a ordem a cada
        // varredura treina o olho do usuario a desconfiar dela.
        var plano = new UpdatePlan(new[]
        {
            new UpdatePlanItem(
                new UpdateCandidate("Editor", "Fabrica", "1.0", "2.0", "editor", "https://exemplo"),
                Pending: true,
                Reason: string.Empty),
        });

        var report = CentralOverview.Build(
            Array.Empty<InstalledProgram>(),
            plano,
            versaoPublicadaTranal: "99.0.0");

        Assert.Equal(2, report.Total);
        Assert.Equal(CentralPriority.High, report.Items[0].Priority);
        Assert.Equal(CentralKind.SelfUpdate, report.Items[0].Kind);
        Assert.Equal(CentralPriority.Medium, report.Items[1].Priority);
    }

    [Fact]
    public void TodoItemTemAcaoQueExisteNoCatalogo()
    {
        var plano = new UpdatePlan(new[]
        {
            new UpdatePlanItem(
                new UpdateCandidate("Editor", "Fabrica", "1.0", "2.0", "editor", "https://exemplo"),
                Pending: true,
                Reason: string.Empty),
        });

        var report = CentralOverview.Build(
            Array.Empty<InstalledProgram>(),
            plano,
            versaoPublicadaTranal: "99.0.0");

        Assert.NotEmpty(report.Items);

        foreach (var item in report.Items)
        {
            Assert.False(string.IsNullOrEmpty(item.ToolId));
            Assert.Contains(item.ToolId, ToolCatalog.All);
        }
    }

    [Fact]
    public void AContagemPorAreaConfere()
    {
        var plano = new UpdatePlan(new[]
        {
            new UpdatePlanItem(
                new UpdateCandidate("Editor", "Fabrica", "1.0", "2.0", "editor", "https://exemplo"),
                Pending: true,
                Reason: string.Empty),
        });

        var report = CentralOverview.Build(Array.Empty<InstalledProgram>(), plano);

        Assert.Equal(1, report.CountOf(CentralKind.Update));
        Assert.Equal(0, report.CountOf(CentralKind.Health));
    }

    // ------------------------------------------------------------------
    // A interface e a linha de comando consomem o mesmo modulo
    // ------------------------------------------------------------------

    [Fact]
    public void AJanelaTemOBotaoDaCentral()
    {
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml");
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        Assert.Contains("x:Name=\"BtnCentral\"", xaml);
        Assert.Contains("Tag=\"Central\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"ModuleCentral\"", xaml);

        Assert.Contains("public void OpenTool(string toolId)", codigo);
        Assert.Contains("SwitchView(toolId, tool: true);", codigo);
    }

    [Fact]
    public void ATelaDaCentralEstaRegistrada()
    {
        var setup = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "ViewSetup.cs");
        var tela = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Modules", "CentralView.cs");

        Assert.Contains("ViewRegistry.Register(\"Central\"", setup);
        Assert.Contains("CentralOverview.Build(", tela);
    }

    [Fact]
    public void ACliTemOComandoDaCentral()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Cli", "ExtraCommands.cs");

        Assert.Contains("flags.Contains(\"/central\")", codigo);
        Assert.Contains("CentralOverview.Build(", codigo);
    }

    [Fact]
    public void OsDoisCatalogosTrazemAsChavesDaCentral()
    {
        var chaves = new[]
        {
            "Module.Central",
            "Desc.Central",
            "Central.Column.Item",
            "Central.Column.Kind",
            "Central.Column.Priority",
            "Central.Column.Detail",
            "Central.Open",
            "Central.Empty",
            "Central.Summary",
            "Central.NoAction",
            "Central.Kind.Update",
            "Central.Kind.NewProgram",
            "Central.Kind.Health",
            "Central.Kind.SelfUpdate",
            "Central.Priority.High",
            "Central.Priority.Medium",
            "Central.Priority.Low",
        };

        foreach (var idioma in new[] { "pt-BR", "en-US" })
        {
            var catalogo = Loc.Load(idioma);

            foreach (var chave in chaves)
            {
                Assert.True(catalogo.ContainsKey(chave), $"{idioma} sem a chave {chave}");
            }
        }
    }
}
