using System;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// As features do lote 12: a bandeja do sistema, os programas recem-
/// instalados, a arvore de categorias automaticas, o icone no painel de
/// controle, as ferramentas externas e a lista compacta.
///
/// O que estes testes cobrem: as regras moram em um lugar so (o nucleo), os
/// arquivos de estado ficam isolados em pasta temporaria (nada toca o
/// ambiente de quem roda os testes) e a arvore de categorias devolve a
/// classificacao que a regra promete.
/// </summary>
public sealed class Lote12FeatureTests : IDisposable
{
    private readonly string _temporaria = Path.Combine(
        Path.GetTempPath(),
        "tranal-lote12-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string? _preferenciasAntigas;

    public Lote12FeatureTests()
    {
        Directory.CreateDirectory(_temporaria);

        _preferenciasAntigas = AppSettingsStore.FilePathOverride;
        AppSettingsStore.FilePathOverride = Path.Combine(_temporaria, "settings.json");

        NewInstallsTracker.FolderOverride = Path.Combine(_temporaria, "new-installs");
        ProgramCategoryTree.FolderOverride = Path.Combine(_temporaria, "categories");
    }

    public void Dispose()
    {
        AppSettingsStore.FilePathOverride = _preferenciasAntigas;
        NewInstallsTracker.FolderOverride = null;
        ProgramCategoryTree.FolderOverride = null;

        try
        {
            if (Directory.Exists(_temporaria)) Directory.Delete(_temporaria, recursive: true);
        }
        catch
        {
            // A pasta temporaria que sobrou nao quebra o teste seguinte.
        }
    }

    // ------------------------------------------------------------------
    // Bandeja do sistema
    // ------------------------------------------------------------------

    [Fact]
    public void AsRegrasDaBandejaVemDasPreferencias()
    {
        AppSettingsStore.Save(new AppSettings { ShowTrayIcon = false, StartMinimizedToTray = true });

        Assert.False(SystemTray.ShowIcon);
        Assert.True(SystemTray.StartMinimized);
        Assert.Equal(4, SystemTray.Rules.Count);
        Assert.Contains(SystemTray.Rules, r => r.Id == "ShowIcon" && !r.Enabled);
        Assert.Contains(SystemTray.Rules, r => r.Id == "StartMinimized" && r.Enabled);
    }

    [Fact]
    public void ADescricaoDaBandejaDizSeOIconeEstaLigado()
    {
        AppSettingsStore.Save(new AppSettings { ShowTrayIcon = false });
        Assert.Contains("desativada", SystemTray.Describe());

        AppSettingsStore.Save(new AppSettings { ShowTrayIcon = true, StartMinimizedToTray = true });
        Assert.Contains("minimizado", SystemTray.Describe());
    }

    [Fact]
    public void OIconeDaBandejaVemDoExecutavelDoTranal()
    {
        var caminho = SystemTray.ExecutablePath;

        Assert.False(string.IsNullOrWhiteSpace(caminho));
        Assert.EndsWith(".exe", caminho, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // Lista compacta
    // ------------------------------------------------------------------

    [Fact]
    public void AListaCompactaTemAsColunasEssenciais()
    {
        Assert.Equal(4, ProgramListLayout.CompactColumns.Count);
        Assert.Contains(ProgramColumn.Name, ProgramListLayout.CompactColumns);
        Assert.Equal(ProgramListLayout.CompactColumns, ProgramListLayout.ColumnsFor(true));

        var completa = ProgramListLayout.ColumnsFor(false);
        Assert.True(completa.Count > ProgramListLayout.CompactColumns.Count);
        Assert.Equal(ProgramColumns.All.Count, completa.Count);
    }

    [Fact]
    public void OModoCompacoFicaNasPreferencias()
    {
        AppSettingsStore.Save(new AppSettings { CompactProgramList = true });
        Assert.True(ProgramListLayout.IsCompact);

        ProgramListLayout.SetCompact(false);
        Assert.False(ProgramListLayout.IsCompact);
    }

    // ------------------------------------------------------------------
    // Programas recem-instalados
    // ------------------------------------------------------------------

    [Fact]
    public void ALinhaDeBaseMarcaOQueApareceuDepois()
    {
        var antes = new InstalledProgram { DisplayName = "Antigo", RegistryKey = "chave-antiga" };
        var depois = new InstalledProgram { DisplayName = "Novo", RegistryKey = "chave-nova" };

        NewInstallsTracker.SaveBaseline(new[] { antes });
        Assert.True(NewInstallsTracker.HasBaseline);
        Assert.NotNull(NewInstallsTracker.BaselineAt);

        var novos = NewInstallsTracker.Detect(new[] { antes, depois });

        Assert.Single(novos);
        Assert.Equal("Novo", novos[0].DisplayName);
        Assert.True(novos[0].FromBaseline);
        Assert.True(NewInstallsTracker.IsNew(depois));
        Assert.False(NewInstallsTracker.IsNew(antes));
    }

    [Fact]
    public void SemLinhaDeBaseOSinalVemDaDataDeInstalacao()
    {
        NewInstallsTracker.ClearBaseline();

        var recente = new InstalledProgram
        {
            DisplayName = "Recente",
            RegistryKey = "chave-recente",
            InstallDateValue = DateTime.Now.AddDays(-1),
        };

        var antigo = new InstalledProgram
        {
            DisplayName = "Antigo",
            RegistryKey = "chave-antiga",
            InstallDateValue = DateTime.Now.AddDays(-400),
        };

        var novos = NewInstallsTracker.Detect(new[] { recente, antigo }, days: 7);

        Assert.Single(novos);
        Assert.Equal("Recente", novos[0].DisplayName);
        Assert.False(novos[0].FromBaseline);
    }

    [Fact]
    public void ApagarALinhaDeBaseVoltaAoSinalPorData()
    {
        NewInstallsTracker.SaveBaseline(new[]
        {
            new InstalledProgram { DisplayName = "A", RegistryKey = "a" },
        });

        Assert.True(NewInstallsTracker.ClearBaseline());
        Assert.False(NewInstallsTracker.HasBaseline);
        Assert.Null(NewInstallsTracker.BaselineAt);
    }

    // ------------------------------------------------------------------
    // Categorias automaticas
    // ------------------------------------------------------------------

    [Fact]
    public void ACategoriaUsaOGrupoQueOUsuarioCriou()
    {
        var programa = new InstalledProgram { DisplayName = "Jogo", RegistryKey = "chave-jogo" };
        var custom = new ProgramCustomization { Groups = { "Jogos" } };

        Assert.Equal("Jogos", ProgramCategoryTree.CategoryOf(programa, custom));
    }

    [Fact]
    public void ResetarApagaAsAtribuicoesGuardadas()
    {
        Assert.True(ProgramCategoryTree.Assign("chave-jogo", "Jogos"));
        Assert.Single(ProgramCategoryTree.Assignments);

        var apagadas = ProgramCategoryTree.ResetToUnsorted();

        Assert.Equal(1, apagadas);
        Assert.Empty(ProgramCategoryTree.Assignments);
    }

    [Fact]
    public void AArvoreAgrupaOsProgramosPorCategoria()
    {
        var a = new InstalledProgram { DisplayName = "A", RegistryKey = "a" };
        var b = new InstalledProgram { DisplayName = "B", RegistryKey = "b" };

        var nos = ProgramCategoryTree.Build(new[] { a, b }, _ => null);

        Assert.Single(nos);
        Assert.Equal(2, nos[0].TotalPrograms);
        Assert.Equal(ProgramCategoryTree.UnsortedId, nos[0].Id);
    }

    [Fact]
    public void AArvoreAceitaUmaPreferenciaPorPrograma()
    {
        var a = new InstalledProgram { DisplayName = "A", RegistryKey = "a" };
        var b = new InstalledProgram { DisplayName = "B", RegistryKey = "b" };

        var nos = ProgramCategoryTree.Build(
            new[] { a, b },
            p => p.RegistryKey == "b"
                ? new ProgramCustomization { Groups = { "Ferramentas" } }
                : null);

        Assert.Equal(2, nos.Count);
        Assert.Contains(nos, n => n.Name == "Ferramentas" && n.TotalPrograms == 1);
    }

    // ------------------------------------------------------------------
    // Painel de controle e ferramentas externas
    // ------------------------------------------------------------------

    [Fact]
    public void OIconeDoPainelDeControleEcreatedERemovido()
    {
        // Se a entrada ja existe (o Tranal instalado), o teste nao mexe:
        // apagar seria mexer no ambiente de quem roda a suite.
        if (ControlPanelIcon.IsRegistered()) return;

        try
        {
            Assert.True(ControlPanelIcon.Register(
                "Tranal Uninstaller",
                "\"tranal.exe\"",
                ControlPanelIcon.IconPath));

            Assert.True(ControlPanelIcon.IsRegistered());
            Assert.Contains("painel de controle", ControlPanelIcon.Describe());
        }
        finally
        {
            // Mesmo quando a verificacao falha, a entrada nao fica para tras
            // na maquina de quem roda a suite.
            ControlPanelIcon.Unregister();
        }

        Assert.False(ControlPanelIcon.IsRegistered());
    }

    [Fact]
    public void AsFerramentasExternasSaoConhecidas()
    {
        var ferramentas = ExternalLaunchers.Known();

        Assert.Equal(2, ferramentas.Count);
        Assert.Contains(ferramentas, f => f.Id == "TotalCommander");
        Assert.Contains(ferramentas, f => f.Id == "CredentialManager");
        Assert.All(ferramentas, f => Assert.False(string.IsNullOrWhiteSpace(f.DisplayName)));
    }

    [Fact]
    public void OTotalCommanderNaoEInventado()
    {
        var caminho = ExternalLaunchers.FindTotalCommander();

        if (caminho is null)
        {
            // Nao esta instalado: o metodo devolve nulo, e nao um caminho
            // inventado que a tela prometeria abrir.
            Assert.Null(caminho);
            return;
        }

        Assert.True(File.Exists(caminho));
    }

    // ------------------------------------------------------------------
    // O catalogo de ferramentas
    // ------------------------------------------------------------------

    [Fact]
    public void AsFerramentasNovasEstaoNoCatalogo()
    {
        Assert.Contains(ToolCatalog.TrayIcon, ToolCatalog.All);
        Assert.Contains(ToolCatalog.NewInstalls, ToolCatalog.All);
        Assert.Contains(ToolCatalog.ProgramCategories, ToolCatalog.All);
        Assert.Contains(ToolCatalog.WindowsIntegration, ToolCatalog.All);
    }
}
