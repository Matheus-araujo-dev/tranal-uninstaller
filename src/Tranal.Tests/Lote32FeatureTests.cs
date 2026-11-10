using System.IO;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Recovery;
using Tranal.Uninstaller.Core.Settings;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Lote 32: os modulos orfaos do nucleo ganharam dono de produto.
///
/// Antes deste lote, sete modulos do nucleo (ProgramListLayout, PortableMode,
/// ForcedRemovalOptions, ManualRemoval, PendingOperations, DailyRegistryBackup
/// e InstallMonitor) eram chamados apenas pelos testes. Este arquivo mede o
/// que eles ganharam: a lista compacta que passou a valer, a pasta de dados
/// que passou a ter efeito, a tela de desinstalacao personalizada, a fila de
/// operacoes pendentes, o backup diario e o monitor de instalacao.
/// </summary>
public sealed class Lote32FeatureTests : IDisposable
{
    private const string ChaveDoRegistro = @"Software\Tranal Group\Tranal Uninstaller Pro";
    private const string ValorDoRegistro = "DataFolder";

    private readonly string _temporaria;
    private readonly string? _preferenciasAntigas;

    public Lote32FeatureTests()
    {
        _temporaria = Path.Combine(Path.GetTempPath(), "tranal-lote32-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_temporaria);

        _preferenciasAntigas = AppSettingsStore.FilePathOverride;
        AppSettingsStore.FilePathOverride = Path.Combine(_temporaria, "settings.json");
        AppSettingsStore.ResetCurrent();
    }

    public void Dispose()
    {
        AppSettingsStore.FilePathOverride = _preferenciasAntigas;
        AppSettingsStore.ResetCurrent();

        InstallMonitor.FileRootsOverride = null;
        InstallMonitor.RegistryRootsOverride = null;
        TraceStore.DirectoryOverride = null;

        DailyRegistryBackup.RootDirectoryOverride = null;
        DailyRegistryBackup.MarkerPathOverride = null;
        DailyRegistryBackup.DestinationOverride = null;
        FullRegistryBackup.HivesOverride = null;

        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(@"Software\Tranal\Testes", writable: true);
            root?.DeleteSubKeyTree("Lote32", throwOnMissingSubKey: false);
        }
        catch { /* o teste nao pode falhar por causa da limpeza */ }

        try
        {
            if (Directory.Exists(_temporaria)) Directory.Delete(_temporaria, true);
        }
        catch { /* idem */ }
    }

    // ------------------------------------------------------------------
    // O formato da lista (ProgramListLayout)
    // ------------------------------------------------------------------

    [Fact]
    public void AListaCompactaTemAsColunasEssenciais()
    {
        Assert.Equal(4, ProgramListLayout.CompactColumns.Count);
        Assert.Contains(ProgramColumn.Name, ProgramListLayout.CompactColumns);
        Assert.Contains(ProgramColumn.Size, ProgramListLayout.CompactColumns);
        Assert.Contains(ProgramColumn.Version, ProgramListLayout.CompactColumns);
        Assert.Contains(ProgramColumn.Company, ProgramListLayout.CompactColumns);
    }

    [Fact]
    public void AListaCompletaRespeitaAsColunasEscondidas()
    {
        AppSettingsStore.Save(new AppSettings
        {
            HiddenColumns = new List<string> { ProgramColumn.Size.ToString() },
        });

        var visiveis = ProgramListLayout.ColumnsFor(false);

        Assert.DoesNotContain(ProgramColumn.Size, visiveis);
        Assert.Contains(ProgramColumn.Name, visiveis);
    }

    [Fact]
    public void OModoCompatoMudaAsColunasQueAVistaMostra()
    {
        AppSettingsStore.Save(new AppSettings { CompactProgramList = true });

        Assert.True(ProgramListLayout.IsCompact);
        Assert.Equal(ProgramListLayout.CompactColumns, ProgramListLayout.VisibleColumns());

        ProgramListLayout.SetCompact(false);

        Assert.False(ProgramListLayout.IsCompact);
    }

    [Fact]
    public void OModoCompactoNaoDesobedeceAColunaEscondida()
    {
        AppSettingsStore.Save(new AppSettings
        {
            CompactProgramList = true,
            HiddenColumns = new List<string> { ProgramColumn.Size.ToString() },
        });

        var visiveis = ProgramListLayout.VisibleColumns();

        Assert.DoesNotContain(ProgramColumn.Size, visiveis);
        Assert.Contains(ProgramColumn.Name, visiveis);
    }

    [Fact]
    public void AJanelaPrincipalConsomeOFormatoDaLista()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        Assert.Contains("ProgramListLayout.VisibleColumns()", codigo);
    }

    // ------------------------------------------------------------------
    // A pasta de dados (PortableMode)
    // ------------------------------------------------------------------

    [Fact]
    public void APastaDeDadosConfiguradaFicaNoRegistro()
    {
        var pasta = Path.Combine(_temporaria, "dados");

        object? anterior = null;
        using (var chave = Registry.CurrentUser.OpenSubKey(ChaveDoRegistro))
            anterior = chave?.GetValue(ValorDoRegistro);

        try
        {
            Assert.True(PortableMode.SetConfiguredFolder(pasta));

            Assert.Equal(pasta, PortableMode.ConfiguredFolder);
            Assert.Equal(pasta, PortableMode.EffectiveDataDirectory);
            Assert.True(PortableMode.IsPortableMode);
            Assert.True(Directory.Exists(pasta));
        }
        finally
        {
            using var chave = Registry.CurrentUser.OpenSubKey(ChaveDoRegistro, writable: true);
            if (anterior is null)
                chave?.DeleteValue(ValorDoRegistro, throwOnMissingValue: false);
            else
                chave?.SetValue(ValorDoRegistro, anterior);
        }
    }

    [Fact]
    public void APastaVaziaVoltaParaOPadrao()
    {
        var pasta = Path.Combine(_temporaria, "dados");

        object? anterior = null;
        using (var chave = Registry.CurrentUser.OpenSubKey(ChaveDoRegistro))
            anterior = chave?.GetValue(ValorDoRegistro);

        try
        {
            PortableMode.SetConfiguredFolder(pasta);
            Assert.True(PortableMode.SetConfiguredFolder(string.Empty));

            Assert.Equal(string.Empty, PortableMode.ConfiguredFolder);
            Assert.Equal(PortableMode.DefaultDataDirectory, PortableMode.EffectiveDataDirectory);
            Assert.False(PortableMode.IsPortableMode);
        }
        finally
        {
            using var chave = Registry.CurrentUser.OpenSubKey(ChaveDoRegistro, writable: true);
            if (anterior is not null)
                chave?.SetValue(ValorDoRegistro, anterior);
            else
                chave?.DeleteValue(ValorDoRegistro, throwOnMissingValue: false);
        }
    }

    [Fact]
    public void OsDadosDoNucleoVemDaPastaDoModoPortavel()
    {
        // A regra mora em um lugar so: quem guarda os dados do Tranal consome
        // o modo portavel.
        Assert.Equal(PortableMode.EffectiveDataDirectory, RegistryBackup.DataDirectory);
    }

    [Fact]
    public void APreferenciaDaPastaDeDadosSaiuDoArquivoDePreferencias()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Core", "Settings", "AppSettings.cs");

        Assert.DoesNotContain("PortableDataFolder", codigo);
    }

    // ------------------------------------------------------------------
    // A desinstalacao personalizada (ManualRemoval e ForcedRemovalOptions)
    // ------------------------------------------------------------------

    [Fact]
    public void ASelecaoPorPalavraChaveCasaPorPalavraInteira()
    {
        var programas = new[]
        {
            new InstalledProgram { DisplayName = "Adobe Reader" },
            new InstalledProgram { DisplayName = "AdobeFresco" },
            new InstalledProgram { DisplayName = "Reader Adobe" },
            new InstalledProgram { DisplayName = "Paint" },
        };

        var selecionados = ManualRemoval.SelectByKeyword(programas, "Adobe");

        Assert.Equal(2, selecionados.Count);
        Assert.DoesNotContain(selecionados, p => p.DisplayName == "AdobeFresco");
        Assert.DoesNotContain(selecionados, p => p.DisplayName == "Paint");
    }

    [Fact]
    public void AExportacaoDaListaDeSobrasTemOCabecalho()
    {
        var csv = ManualRemoval.Export(Array.Empty<LeftoverItem>());

        Assert.Contains("Tipo;Caminho", csv);
    }

    [Fact]
    public void AListaDeSobrasGravaComMarcadorDeByteOrder()
    {
        var arquivo = Path.Combine(_temporaria, "sobras.csv");

        Assert.True(ManualRemoval.SaveToFile(arquivo, Array.Empty<LeftoverItem>()));

        var bytes = File.ReadAllBytes(arquivo);
        Assert.True(bytes.Length >= 3);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }

    [Fact]
    public void ADesinstalacaoForcadaUsaARegraDoNucleo()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "ForcedUninstallWindow.xaml.cs");

        Assert.Contains("ForcedRemovalOptions.ForTarget(path).Resolve(", codigo);
    }

    [Fact]
    public void ADesinstalacaoPorPalavraChavePrecisaDoPrograma()
    {
        Assert.Throws<ArgumentNullException>(() => ForcedRemovalOptions.ForKeyword("Adobe").Resolve(null));
    }

    [Fact]
    public void ATelaDeDesinstalacaoPersonalizadaExiste()
    {
        var janela = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "ManualRemovalWindow.xaml.cs");

        Assert.Contains("ManualRemoval.SelectByKeyword", janela);
        Assert.Contains("ManualRemoval.Candidates", janela);
        Assert.Contains("ManualRemoval.SaveToFile", janela);

        var menu = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");
        Assert.Contains("ShowManualRemoval", menu);
    }

    // ------------------------------------------------------------------
    // A fila de operacoes pendentes (PendingOperations)
    // ------------------------------------------------------------------

    [Fact]
    public void AOperacaoPendenteEstaNoCatalogoDeFerramentas()
    {
        Assert.Contains(ToolCatalog.PendingOperations, ToolCatalog.All);
        Assert.Contains(ToolCatalog.DailyRegistryBackup, ToolCatalog.All);
    }

    [Fact]
    public void AsFerramentasNovasTemBotaoNaBarraLateralETela()
    {
        var barra = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml");

        Assert.Contains("Tag=\"PendingOperations\"", barra);
        Assert.Contains("Tag=\"DailyRegistryBackup\"", barra);

        var telas = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Tools", "RecoveryViews.cs");
        Assert.Contains("CreatePendingOperations", telas);
        Assert.Contains("CreateDailyBackup", telas);
    }

    [Fact]
    public void AOperacaoPendenteEhLidaDaFilaDoWindows()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Tools", "RecoveryViews.cs");

        Assert.Contains("PendingOperations.List()", codigo);
        Assert.Contains("PendingOperations.Apply(", codigo);
        Assert.Contains("PendingOperations.Remove(", codigo);
    }

    // ------------------------------------------------------------------
    // O backup diario do registro (DailyRegistryBackup)
    // ------------------------------------------------------------------

    [Fact]
    public void OBackupDiarioRodaAGuardandoOMarcador()
    {
        const string raiz = @"Software\Tranal\Testes\Lote32";

        DailyRegistryBackup.RootDirectoryOverride = _temporaria;
        DailyRegistryBackup.MarkerPathOverride = Path.Combine(_temporaria, "diario.txt");
        FullRegistryBackup.HivesOverride = new[] { @"HKCU\" + raiz };

        try
        {
            using (var chave = Registry.CurrentUser.CreateSubKey(raiz))
                chave?.SetValue("Marca", "valor de teste");

            var resultado = DailyRegistryBackup.Run();

            Assert.True(resultado.Ran, resultado.Message);
            Assert.True(resultado.Success, resultado.Message);
            Assert.NotNull(DailyRegistryBackup.LastRunUtc);
            Assert.False(DailyRegistryBackup.IsDue);
        }
        finally
        {
            FullRegistryBackup.HivesOverride = null;
        }
    }

    [Fact]
    public void AListaDeBackupsDiariosEncontraOBBackupFeito()
    {
        const string raiz = @"Software\Tranal\Testes\Lote32";

        DailyRegistryBackup.RootDirectoryOverride = _temporaria;
        DailyRegistryBackup.MarkerPathOverride = Path.Combine(_temporaria, "diario.txt");
        FullRegistryBackup.HivesOverride = new[] { @"HKCU\" + raiz };

        try
        {
            using (var chave = Registry.CurrentUser.CreateSubKey(raiz))
                chave?.SetValue("Marca", "valor de teste");

            DailyRegistryBackup.Run();

            var backups = DailyRegistryBackup.List();

            Assert.NotEmpty(backups);
            Assert.Contains(backups, b => b.Files.Count > 0);
            Assert.Contains(backups, b => b.Date != DateTime.MinValue);
        }
        finally
        {
            FullRegistryBackup.HivesOverride = null;
        }
    }

    [Fact]
    public void OBackupDiarioRodaSoNoArranqueDoAplicativo()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "App.xaml.cs");

        Assert.Contains("DailyRegistryBackup.RunIfDue()", codigo);
    }

    // ------------------------------------------------------------------
    // O monitor de instalacao (InstallMonitor)
    // ------------------------------------------------------------------

    [Fact]
    public void OMonitorDeInstalacaoEncontraOQueApareceuNaPasta()
    {
        var pasta = Path.Combine(_temporaria, "instalado");
        Directory.CreateDirectory(pasta);

        InstallMonitor.FileRootsOverride = new[] { pasta };
        InstallMonitor.RegistryRootsOverride = new[] { @"HKEY_CURRENT_USER\Software\Tranal\Testes\Lote32" };
        TraceStore.DirectoryOverride = Path.Combine(_temporaria, "logs");

        var monitor = InstallMonitor.Start("Programa", 3600);
        Assert.NotNull(monitor);

        var arquivo = Path.Combine(pasta, "novo.txt");
        File.WriteAllText(arquivo, "conteudo");

        var install = monitor!.StopAndSave();

        Assert.NotNull(install);
        Assert.Equal(1, install!.FileCount);
        Assert.Contains(monitor.Files, f => f.Path == arquivo);
    }

    [Fact]
    public void AJanelaDoMonitorDeInstalacaoExisteEChamaOModulo()
    {
        var janela = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "InstallMonitorWindow.xaml.cs");

        Assert.Contains("InstallMonitor.Start(nome)", janela);
        Assert.Contains("monitor.StopAndSave()", janela);

        var menu = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");
        Assert.Contains("ShowInstallMonitor", menu);
    }

    // ------------------------------------------------------------------
    // O modo rapido
    // ------------------------------------------------------------------

    [Fact]
    public void OModoRapidoPulaAMedicaoDoTamanho()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Core", "UninstallSupport.cs");

        Assert.Contains("AccessibilityOptions.SkipSlowChecks", codigo);
    }

    // ------------------------------------------------------------------
    // O catalogo de idiomas
    // ------------------------------------------------------------------

    [Fact]
    public void OsDoisCatalogosTrazemAsChavesDoLote()
    {
        var chaves = new[]
        {
            "Cmd.ManualRemoval",
            "Cmd.InstallMonitor",
            "Manual.Title",
            "Manual.Description",
            "Manual.Search",
            "Manual.Export",
            "Manual.Remove",
            "Manual.Programs",
            "Manual.NoPrograms",
            "Manual.Found",
            "Manual.NothingSelected",
            "Manual.ExportTitle",
            "Manual.ExportFilter",
            "Manual.Exported",
            "Manual.ExportFailed",
            "Manual.Removed",
            "Module.PendingOperations",
            "Module.DailyRegistryBackup",
            "Tool.Pending.Type",
            "Tool.Pending.Destination",
            "Tool.Pending.State",
            "Tool.Pending.Apply",
            "Tool.Pending.Remove",
            "Tool.Pending.Start",
            "Tool.Pending.Empty",
            "Tool.Pending.Count",
            "Tool.Pending.Delete",
            "Tool.Pending.Rename",
            "Tool.Pending.Waiting",
            "Tool.Pending.Missing",
            "Tool.Pending.Applied",
            "Tool.Pending.Failed",
            "Tool.Pending.Removed",
            "Tool.Pending.FailedRemove",
            "Tool.DailyBackup.When",
            "Tool.DailyBackup.Files",
            "Tool.DailyBackup.Folder",
            "Tool.DailyBackup.Run",
            "Tool.DailyBackup.Empty",
            "Tool.DailyBackup.Count",
            "Tool.DailyBackup.Unknown",
            "Tool.DailyBackup.LastRun",
            "Tool.DailyBackup.Done",
            "Tool.DailyBackup.Failed",
            "Monitor.Title",
            "Monitor.Description",
            "Monitor.Start",
            "Monitor.Stop",
            "Monitor.Waiting",
            "Monitor.NameRequired",
            "Monitor.StartFailed",
            "Monitor.Running",
            "Monitor.NothingFound",
            "Monitor.Saved",
            "Monitor.Count",
            "Monitor.KindFile",
            "Monitor.KindFolder",
            "Monitor.KindRegistry",
            "Monitor.Column.Type",
            "Monitor.Column.Path",
            "Monitor.Column.Size",
            "Options.PortableFolderRestart",
            "Options.PortableFolderFailed",
            "Leftover.SizeNotMeasured",
        };

        var pt = Loc.Load("pt-BR");
        var en = Loc.Load("en-US");

        foreach (var chave in chaves)
        {
            Assert.True(pt.ContainsKey(chave), $"chave ausente no pt-BR: {chave}");
            Assert.True(en.ContainsKey(chave), $"chave ausente no en-US: {chave}");
        }
    }

    [Fact]
    public void OsDoisCatalogosTrazemAsMesmasChaves()
    {
        var pt = Loc.Load("pt-BR");
        var en = Loc.Load("en-US");

        var soPt = pt.Keys.Where(k => !en.ContainsKey(k)).OrderBy(k => k).ToList();
        var soEn = en.Keys.Where(k => !pt.ContainsKey(k)).OrderBy(k => k).ToList();

        Assert.True(soPt.Count == 0, $"so no pt-BR: {string.Join(", ", soPt)}");
        Assert.True(soEn.Count == 0, $"so no en-US: {string.Join(", ", soEn)}");
    }
}
