using System.IO;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Settings;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// As opcoes do Revo Uninstaller Pro que o Tranal ainda nao tinha na
/// interface: manter as preferencias ao desinstalar (15364), abrir o
/// assistente maximizado (15170), focar "Traced Programs" no arranque
/// (15129) e as caixas de selecao na visualizacao de detalhes (15334).
///
/// A suite nao referencia o WPF: a parte de interface e verificada sobre o
/// fonte, e a parte de regra e verificada sobre o nucleo.
/// </summary>
[Collection("preferencias")]
public class StartupPreferencesTests : IDisposable
{
    private readonly string _file;
    private readonly InstalledProgram _programa;

    public StartupPreferencesTests()
    {
        // As preferencias do teste ficam num arquivo temporario: sem isso, o
        // teste escreveria no arquivo real do usuario.
        _file = Path.Combine(Path.GetTempPath(), "tranal-program-data-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        ProgramCustomizationStore.FilePathOverride = _file;
        ProgramCustomizationStore.Reload();

        _programa = new InstalledProgram
        {
            DisplayName = "Programa de Teste",
            RegistryKey = "{chave-de-teste}",
            RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{chave-de-teste}",
        };
    }

    public void Dispose()
    {
        ProgramCustomizationStore.ResetForTests();
        AppSettingsStore.FilePathOverride = null;
        try { if (File.Exists(_file)) File.Delete(_file); }
        catch { /* o temporario e descartavel */ }
    }

    // ---------------------------------------------------------------
    // 15364 — "Do you want to keep your preferences?"
    // ---------------------------------------------------------------

    [Fact]
    public void OsIdentificadoresDasPreferenciasSaoEstaveis()
    {
        ProgramCustomizationStore.CreateGroup("Trabalho");
        ProgramCustomizationStore.SetComment(_programa, "minha anotacao");
        ProgramCustomizationStore.AddToGroup(_programa, "Trabalho");
        ProgramCustomizationStore.Save();

        var ids = PreferenceRetention.ItemIds(_programa);

        Assert.Contains("comment", ids);
        Assert.Contains("groups", ids);
        Assert.DoesNotContain("icon", ids);
    }

    [Fact]
    public void NaoManterAsPreferenciasApagaOComentarioEOGrupo()
    {
        ProgramCustomizationStore.CreateGroup("Trabalho");
        ProgramCustomizationStore.SetComment(_programa, "minha anotacao");
        ProgramCustomizationStore.AddToGroup(_programa, "Trabalho");
        ProgramCustomizationStore.Save();

        var apagado = PreferenceRetention.ApplyAfterUninstall(_programa, keep: false);

        Assert.True(apagado);
        Assert.True(string.IsNullOrWhiteSpace(ProgramCustomizationStore.For(_programa).Comment));
        Assert.Empty(ProgramCustomizationStore.For(_programa).Groups);
    }

    [Fact]
    public void ManterAsPreferenciasNaoApagaNada()
    {
        ProgramCustomizationStore.SetComment(_programa, "minha anotacao");
        ProgramCustomizationStore.Save();

        var apagado = PreferenceRetention.ApplyAfterUninstall(_programa, keep: true);

        Assert.False(apagado);
        Assert.Equal("minha anotacao", ProgramCustomizationStore.For(_programa).Comment);
    }

    [Fact]
    public void AOpcaoDeManterVemDaPreferenciaDoUsuario()
    {
        var temp = Path.Combine(Path.GetTempPath(), "tranal-settings-" + Guid.NewGuid().ToString("N")[..8] + ".json");

        try
        {
            AppSettingsStore.FilePathOverride = temp;
            AppSettingsStore.Save(new AppSettings { KeepPreferencesOnUninstall = false });

            // Sem parametro, vale a preferencia do usuario.
            Assert.False(UninstallRemovalOptions.FromSettings().KeepPreferences);

            // Com parametro, vale a escolha da operacao (a caixa do assistente).
            Assert.True(UninstallRemovalOptions.FromSettings("nome", keepPreferences: true).KeepPreferences);
        }
        finally
        {
            AppSettingsStore.FilePathOverride = null;
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch { /* o temporario e descartavel */ }
        }
    }

    [Fact]
    public void OAssistenteAbreMaximizado()
    {
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml");

        Assert.Contains("WindowState=\"Maximized\"", xaml);
    }

    [Fact]
    public void OAssistenteTemACaixaDeManterPreferencias()
    {
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml");

        Assert.Contains("KeepPreferencesCheck", xaml);
        Assert.Contains("loc:Tr Wizard.KeepPreferences", xaml);
    }

    [Fact]
    public void ADecisaoSobreAsPreferenciasRodaDepoisDoDesinstaladorNativo()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml.cs");

        var desinstalador = codigo.IndexOf("await RunBuiltInAsync();", StringComparison.Ordinal);
        var decisao = codigo.IndexOf("ApplyPreferenceDecision();", StringComparison.Ordinal);

        Assert.True(desinstalador >= 0, "o desinstalador nativo nao foi encontrado");
        Assert.True(decisao > desinstalador,
            "a decisao sobre as preferencias precisa rodar depois do desinstalador nativo");
    }

    // ---------------------------------------------------------------
    // 15129 — focar "Traced Programs" no arranque
    // ---------------------------------------------------------------

    [Fact]
    public void AFuncaoFocaOsProgramasMonitoradosNoArranque()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        Assert.Contains("FocusStartupModule", codigo);
        Assert.Contains("FocusTracedProgramsOnStartup", codigo);
        Assert.Contains("\"TracedPrograms\"", codigo);
    }

    // ---------------------------------------------------------------
    // 15334 — "Show check boxes in Details view"
    // ---------------------------------------------------------------

    [Fact]
    public void AGradeTemAColunaDeCaixasDeSelecao()
    {
        var layout = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "ProgramColumnLayout.cs");
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml");

        Assert.Contains("CheckColumnWidth", layout);
        Assert.Contains("ProgramCheckTemplate", xaml);
    }

    [Fact]
    public void AsPreferenciasNovasLigamPorPadrao()
    {
        var settings = new AppSettings();

        // O Revo foca os programas monitorados no arranque e mostra as caixas
        // de selecao na visualizacao de detalhes: e o padrao do original.
        Assert.True(settings.FocusTracedProgramsOnStartup);
        Assert.True(settings.ShowCheckBoxesInDetailsView);
    }

    // ---------------------------------------------------------------
    // As chaves novas existem nos dois catálogos
    // ---------------------------------------------------------------

    [Fact]
    public void AsChavesNovasExisteNosDoisCatalogos()
    {
        var pt = Loc.Load("pt-BR");
        var en = Loc.Load("en-US");

        var chaves = new[]
        {
            "Wizard.KeepPreferences",
            "Wizard.KeepPreferencesNote",
            "Wizard.Preference.Comment",
            "Wizard.Preference.Icon",
            "Wizard.Preference.Groups",
            "Options.FocusTracedProgramsOnStartup",
            "Options.ShowCheckBoxesInDetailsView",
        };

        foreach (var chave in chaves)
        {
            Assert.True(pt.ContainsKey(chave), $"falta a chave em pt-BR: {chave}");
            Assert.True(en.ContainsKey(chave), $"falta a chave em en-US: {chave}");
        }
    }
}
