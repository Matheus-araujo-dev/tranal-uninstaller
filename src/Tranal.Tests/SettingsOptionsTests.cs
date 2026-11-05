using System.IO;
using Microsoft.Win32;
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
public class SettingsOptionsTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();
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
        var program = CreateFakeProgram(out var keyName);

        try
        {
            var items = LeftoverScanner.Scan(program, UninstallMode.Advanced);

            // A varredura precisa achar alguma coisa: um teste que roda sobre
            // uma lista vazia passa sem provar nada.
            Assert.NotEmpty(items);

            var baixa = items.Where(i => i.Confidence != LeftoverConfidence.High).ToList();
            Assert.NotEmpty(baixa);

            // Sem a opcao "marcar todas as sobras", so a confianca alta vem
            // marcada: e a paridade com o "apagar apenas os itens em negrito"
            // do Revo.
            foreach (var item in baixa)
            {
                Assert.False(item.Selected,
                    $"Item de confianca {item.Confidence} veio marcado sem a opcao: {item.Path}");
            }
        }
        finally
        {
            RemoveFakeRegistryKey(keyName);
        }
    }

    [Fact]
    public void MarcarTodasAsSobrasMarcaTodosOsItens()
    {
        var program = CreateFakeProgram(out var keyName);

        try
        {
            var items = LeftoverScanner.Scan(program, UninstallMode.Advanced, checkAllLeftovers: true);

            Assert.NotEmpty(items);
            Assert.Contains(items, i => i.Confidence != LeftoverConfidence.High);

            // A confianca continua sendo exibida; o que muda e a marcacao.
            Assert.All(items, item => Assert.True(item.Selected));
        }
        finally
        {
            RemoveFakeRegistryKey(keyName);
        }
    }

    [Fact]
    public void ForcarDesinstalacaoAceitaMarcarTodasAsSobras()
    {
        var program = CreateFakeProgram(out var keyName);

        try
        {
            var target = Path.Combine(program.InstallLocation, "app.exe");

            var items = LeftoverScanner.ScanForced(target, UninstallMode.Advanced, checkAllLeftovers: true);

            Assert.NotEmpty(items);
            Assert.All(items, item => Assert.True(item.Selected));
        }
        finally
        {
            RemoveFakeRegistryKey(keyName);
        }
    }

    // ---------------------------------------------------------------
    // Fixture: um programa falso com sobra de verdade
    // ---------------------------------------------------------------

    private const string UninstallRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    /// <summary>
    /// Cria um programa falso com sobra real:
    ///
    /// - uma pasta que existe (confianca alta, "Pasta de instalacao");
    /// - uma chave de desinstalacao descartavel com o nome parecido (confianca
    ///   media).
    ///
    /// Sem a chave, a varredura acharia so a pasta de alta confianca, e o
    /// teste de "marcar todas as sobras" nao provaria nada.
    /// </summary>
    private InstalledProgram CreateFakeProgram(out string registryKeyName)
    {
        var folder = _temp.CreateSubFolder("Programa de Teste Tranal");
        File.WriteAllText(Path.Combine(folder, "app.exe"), "conteudo");

        registryKeyName = "TranalTeste_" + Guid.NewGuid().ToString("N")[..8];

        using var root = Registry.CurrentUser.CreateSubKey(UninstallRoot);
        using var key = root.CreateSubKey(registryKeyName);

        // Nome parecido, mas nao igual: e o que produz confianca media.
        key.SetValue("DisplayName", "Programa de Teste Tranal Helper");
        key.SetValue("UninstallString", $"\"{Path.Combine(folder, "app.exe")}\" /x");

        return new InstalledProgram
        {
            DisplayName = "Programa de Teste Tranal",
            InstallLocation = folder,
        };
    }

    /// <summary>Apaga a chave de registro descartavel do fixture.</summary>
    private static void RemoveFakeRegistryKey(string keyName)
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(UninstallRoot, writable: true);
            root?.DeleteSubKeyTree(keyName, throwOnMissingSubKey: false);
        }
        catch { /* chave de teste ja removida */ }
    }
}
