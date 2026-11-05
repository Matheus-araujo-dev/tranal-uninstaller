using System.IO;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Settings;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// As preferencias que traduzem as opcoes do Revo Uninstaller Pro que o
/// Tranal ainda nao tinha: marcacao das sobras, mensagens, visibilidade dos
/// paineis, menu de contexto e log do programa monitorado.
/// </summary>
[Collection("preferencias")]
public class SettingsOptionsTests
{
    [Fact]
    public void PadroesDasPreferenciasNovas()
    {
        var settings = new AppSettings();

        // O layout aparece por padrao.
        Assert.True(settings.ShowMainToolbar);
        Assert.True(settings.ShowDescriptionPanel);
        Assert.True(settings.ShowStatusBar);
        Assert.True(settings.ShowToolsToolbar);

        // Marcar todas as sobras muda o padrao do Revo: so a confianca alta
        // vem marcada.
        Assert.False(settings.CheckAllLeftoversByDefault);

        // As mensagens continuam aparecendo.
        Assert.False(settings.SkipInformationMessages);
        Assert.False(settings.SkipWarningMessages);

        // Registrar uma entrada para todos os arquivos do Explorer e uma
        // mudanca visivel no ambiente do usuario: ela e opt-in.
        Assert.False(settings.ShowContextMenuCommands);

        Assert.False(settings.DeleteTracedLogAfterUninstall);
    }

    [Fact]
    public void GravarERecarregarPreservaAsPreferenciasNovas()
    {
        var temp = Path.Combine(
            Path.GetTempPath(),
            "tranal-settings-" + Guid.NewGuid().ToString("N")[..8] + ".json");

        try
        {
            AppSettingsStore.FilePathOverride = temp;

            AppSettingsStore.Save(new AppSettings
            {
                CheckAllLeftoversByDefault = true,
                SkipInformationMessages = true,
                SkipWarningMessages = true,
                ShowMainToolbar = false,
                ShowDescriptionPanel = false,
                ShowStatusBar = false,
                ShowToolsToolbar = false,
                ShowContextMenuCommands = true,
                DeleteTracedLogAfterUninstall = true,
            });

            var loaded = AppSettingsStore.Load();

            Assert.True(loaded.CheckAllLeftoversByDefault);
            Assert.True(loaded.SkipInformationMessages);
            Assert.True(loaded.SkipWarningMessages);
            Assert.False(loaded.ShowMainToolbar);
            Assert.False(loaded.ShowDescriptionPanel);
            Assert.False(loaded.ShowStatusBar);
            Assert.False(loaded.ShowToolsToolbar);
            Assert.True(loaded.ShowContextMenuCommands);
            Assert.True(loaded.DeleteTracedLogAfterUninstall);
        }
        finally
        {
            AppSettingsStore.FilePathOverride = null;
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch { /* o temporario e descartavel */ }
        }
    }

    [Fact]
    public void ArquivoDePreferenciasAntigoMantemOLayoutVisivel()
    {
        // Um settings.json gravado antes das opcoes de layout nao tem as
        // chaves novas: elas precisam continuar valendo (visivel), e nao
        // virar "oculto" por omissao.
        var temp = Path.Combine(
            Path.GetTempPath(),
            "tranal-settings-" + Guid.NewGuid().ToString("N")[..8] + ".json");

        try
        {
            AppSettingsStore.FilePathOverride = temp;
            File.WriteAllText(temp, """{ "Theme": "Dark", "Language": "pt-BR" }""");

            var loaded = AppSettingsStore.Load();

            Assert.True(loaded.ShowMainToolbar);
            Assert.True(loaded.ShowDescriptionPanel);
            Assert.True(loaded.ShowStatusBar);
            Assert.True(loaded.ShowToolsToolbar);
            Assert.False(loaded.ShowContextMenuCommands);
        }
        finally
        {
            AppSettingsStore.FilePathOverride = null;
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch { /* o temporario e descartavel */ }
        }
    }

    [Fact]
    public void VarreduraNormalSoMarcaAConfiancaAlta()
    {
        var program = new InstalledProgram
        {
            DisplayName = "Programa de Teste Tranal",
            InstallLocation = @"C:\Arquivos de Programas\Programa de Teste Tranal",
        };

        var items = LeftoverScanner.Scan(program, UninstallMode.Advanced);

        // Sem a opcao "marcar todas as sobras", so a confianca alta vem
        // marcada: e a paridade com o "apagar apenas os itens em negrito" do
        // Revo.
        foreach (var item in items)
        {
            if (item.Confidence == LeftoverConfidence.High) continue;

            Assert.False(item.Selected,
                $"Item de confianca {item.Confidence} veio marcado sem a opcao: {item.Path}");
        }
    }

    [Fact]
    public void MarcarTodasAsSobrasMarcaTodosOsItens()
    {
        var program = new InstalledProgram
        {
            DisplayName = "Programa de Teste Tranal",
            InstallLocation = @"C:\Arquivos de Programas\Programa de Teste Tranal",
        };

        var items = LeftoverScanner.Scan(program, UninstallMode.Advanced, checkAllLeftovers: true);

        // A confianca continua sendo exibida; o que muda e a marcacao.
        Assert.All(items, item => Assert.True(item.Selected));
    }

    [Fact]
    public void ForcarDesinstalacaoAceitaMarcarTodasAsSobras()
    {
        var items = LeftoverScanner.ScanForced(
            @"C:\Arquivos de Programas\Programa de Teste Tranal\app.exe",
            UninstallMode.Advanced,
            checkAllLeftovers: true);

        Assert.All(items, item => Assert.True(item.Selected));
    }
}
