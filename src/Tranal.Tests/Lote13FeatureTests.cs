using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Notifications;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Core.Tools;
using Tranal.Uninstaller.Core.Topmost;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// As features do lote 13: as entradas do Tranal no Explorer, as notificacoes
/// internas, os perfis por operacao, o bloqueio dos avisos do Windows, os
/// atalhos globais, a tabela de caminhos de registro e a geolocalizacao.
///
/// O que estes testes cobrem: as chaves do Explorer sao as que o Windows
/// le, os arquivos de estado ficam isolados em pasta temporaria, o teste do
/// bloqueio de avisos usa uma chave de registro de teste (e nao a preferencia
/// real do Windows) e o perfil herda as preferencias globais quando nao
/// existe perfil guardado.
/// </summary>
public sealed class Lote13FeatureTests : IDisposable
{
    private readonly string _temporaria = Path.Combine(
        Path.GetTempPath(),
        "tranal-lote13-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string? _preferenciasAntigas;

    public Lote13FeatureTests()
    {
        Directory.CreateDirectory(_temporaria);

        _preferenciasAntigas = AppSettingsStore.FilePathOverride;
        AppSettingsStore.FilePathOverride = Path.Combine(_temporaria, "settings.json");

        InternalNotifications.FolderOverride = Path.Combine(_temporaria, "notifications");
        OperationProfiles.FolderOverride = Path.Combine(_temporaria, "profiles");
        NotificationBlocker.RootOverride = @"Software\Tranal\Tests\PushNotifications";
    }

    public void Dispose()
    {
        AppSettingsStore.FilePathOverride = _preferenciasAntigas;
        InternalNotifications.FolderOverride = null;
        OperationProfiles.FolderOverride = null;
        NotificationBlocker.RootOverride = null;

        try
        {
            using var hive = Registry.CurrentUser;
            hive.DeleteSubKeyTree(@"Software\Tranal\Tests", throwOnMissingSubKey: false);
        }
        catch
        {
            // A chave de teste que sobrou nao quebra o teste seguinte.
        }

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
    // As entradas do Explorer
    // ------------------------------------------------------------------

    [Fact]
    public void AsEntradasDoExplorerEstaoNasChavesQueOWindowsLe()
    {
        var entradas = ExplorerShellMenu.Entries;

        Assert.NotEmpty(entradas);
        Assert.All(entradas, e =>
        {
            Assert.StartsWith(@"Software\Classes\", e.ShellKeyRelative, StringComparison.Ordinal);
            Assert.Contains(@"\shell\", e.ShellKeyRelative, StringComparison.Ordinal);
            Assert.NotEmpty(e.DisplayName);
            Assert.NotEmpty(e.Arguments);
        });

        // O "Install and Trace" vale para executavel e para instalador.
        Assert.Equal(2, ExplorerShellMenu.EntriesOf(ExplorerShellMenu.InstallAndTraceId).Count);
        Assert.Single(ExplorerShellMenu.EntriesOf(ExplorerShellMenu.ShredId));
        Assert.Equal(2, ExplorerShellMenu.EntriesOf(ExplorerShellMenu.MonitorId).Count);
    }

    [Fact]
    public void AsEntradasDoExplorerPodemSerRegistradasERemovidas()
    {
        try
        {
            var aplicadas = ExplorerShellMenu.Apply(true);

            Assert.Equal(ExplorerShellMenu.Entries.Count, aplicadas);
            Assert.True(ExplorerShellMenu.IsRegistered(ExplorerShellMenu.InstallAndTraceId));
            Assert.True(ExplorerShellMenu.IsRegistered(ExplorerShellMenu.ShredId));
            Assert.True(ExplorerShellMenu.IsRegistered(ExplorerShellMenu.MonitorId));
        }
        finally
        {
            ExplorerShellMenu.Apply(false);
        }

        Assert.False(ExplorerShellMenu.IsRegistered(ExplorerShellMenu.InstallAndTraceId));
        Assert.False(ExplorerShellMenu.IsRegistered(ExplorerShellMenu.ShredId));
        Assert.False(ExplorerShellMenu.IsRegistered(ExplorerShellMenu.MonitorId));
    }

    // ------------------------------------------------------------------
    // Os atalhos globais
    // ------------------------------------------------------------------

    [Fact]
    public void OsAtalhosGlobaisTaoDescritos()
    {
        Assert.NotEmpty(GlobalHotkeys.All);
        Assert.Contains(GlobalHotkeys.All, h => h.Id == "open-tranal");

        var atalho = GlobalHotkeys.OpenTranal;
        var texto = GlobalHotkeys.Describe(atalho);

        Assert.Contains("Ctrl", texto, StringComparison.Ordinal);
        Assert.Contains("Alt", texto, StringComparison.Ordinal);
        Assert.Contains("T", texto, StringComparison.Ordinal);
        Assert.Equal(GlobalHotkeyModifiers(atalho), atalho.Modifiers);
    }

    private static HotkeyModifiers GlobalHotkeyModifiers(GlobalHotkey atalho) =>
        HotkeyModifiers.Control | HotkeyModifiers.Alt;

    // ------------------------------------------------------------------
    // As notificacoes internas
    // ------------------------------------------------------------------

    [Fact]
    public void AsNotificacoesGuardamELeem()
    {
        Assert.True(InternalNotifications.Schedule(
            "teste.1",
            DateTime.Now.AddMinutes(-1),
            "Tip.Drag",
            "Tip.DragText"));

        var notificacoes = InternalNotifications.List();
        Assert.Contains(notificacoes, n => n.Id == "teste.1");

        Assert.True(InternalNotifications.MarkAsRead("teste.1"));
        Assert.DoesNotContain(InternalNotifications.List(), n => n.Id == "teste.1" && !n.IsRead);
    }

    [Fact]
    public void AsDicasDoCentroDeDicasSaoFixas()
    {
        Assert.True(InternalNotifications.Tips.Count >= 4);
        Assert.Equal(InternalNotifications.Tips.Count, InternalNotifications.Tips.Select(t => t.Id).Distinct().Count());

        var (titulo, mensagem) = InternalNotifications.TextOf(InternalNotifications.Tips[0]);
        Assert.NotEmpty(titulo);
        Assert.NotEmpty(mensagem);
    }

    [Fact]
    public void ANotificacaoAgendadaSoApareceNaHora()
    {
        Assert.True(InternalNotifications.Schedule(
            "teste.futuro",
            DateTime.Now.AddHours(3),
            "Tip.Update",
            "Tip.UpdateText"));

        Assert.DoesNotContain(InternalNotifications.Due(), n => n.Id == "teste.futuro");
        Assert.Contains(InternalNotifications.List(), n => n.Id == "teste.futuro");
    }

    // ------------------------------------------------------------------
    // Os perfis por operacao
    // ------------------------------------------------------------------

    [Fact]
    public void OPerfilNasceDasPreferenciasGlobais()
    {
        AppSettingsStore.Save(new AppSettings
        {
            CreateRestorePointBeforeUninstall = false,
            ShowSystemComponents = false,
        });

        var perfil = OperationProfiles.ProfileOf(OperationKind.Uninstall);

        Assert.False(perfil.CreateRestorePoint);
        Assert.False(perfil.IncludeSystemComponents);
    }

    [Fact]
    public void OPerfilGuardadoTemPrevalencia()
    {
        var perfil = OperationProfiles.ProfileOf(OperationKind.Cleaner);
        perfil.IncludeWindowsApps = true;
        perfil.ChangeViews.Add(ChangeViewKind.Files);

        Assert.True(OperationProfiles.Save(perfil));

        var guardado = OperationProfiles.ProfileOf(OperationKind.Cleaner);
        Assert.True(guardado.IncludeWindowsApps);
        Assert.Contains(ChangeViewKind.Files, OperationProfiles.ChangeViewsOf(OperationKind.Cleaner));
    }

    // ------------------------------------------------------------------
    // O bloqueio dos avisos do Windows
    // ------------------------------------------------------------------

    [Fact]
    public void OBloqueioDeAvisosGravaELe()
    {
        Assert.True(NotificationBlocker.SetToastEnabled(false));
        Assert.False(NotificationBlocker.State().ToastEnabled);

        Assert.True(NotificationBlocker.SetToastEnabled(true));
        Assert.True(NotificationBlocker.State().ToastEnabled);
    }

    [Fact]
    public void OBloqueioDeAvisosExplicaOEstado()
    {
        NotificationBlocker.SetToastEnabled(false);
        Assert.Contains("bloqueados", NotificationBlocker.Describe());
    }

    // ------------------------------------------------------------------
    // A tabela de caminhos de registro
    // ------------------------------------------------------------------

    [Fact]
    public void ATabelaDeRegistroMarcaAChaveDoPrograma()
    {
        var programa = new InstalledProgram
        {
            DisplayName = "Programa",
            RegistryKey = "{chave}",
            RegistryPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\{chave}",
        };

        var linhas = RegistryPathTable.RowsOf(programa);

        Assert.NotEmpty(linhas);
        Assert.Contains(linhas, l => l.IsRegistryKey);
        Assert.All(linhas, l => Assert.False(string.IsNullOrWhiteSpace(l.Path)));
    }

    // ------------------------------------------------------------------
    // A geolocalizacao
    // ------------------------------------------------------------------

    [Fact]
    public void AGeolocalizacaoRespondeComUmVeredito()
    {
        var estado = GeolocationProbe.State();

        Assert.NotNull(estado);
        Assert.False(string.IsNullOrWhiteSpace(estado.Detail));
        Assert.False(string.IsNullOrWhiteSpace(GeolocationProbe.GeolocatorClass));
    }

    // ------------------------------------------------------------------
    // O catalogo de ferramentas
    // ------------------------------------------------------------------

    [Fact]
    public void AsFerramentasNovasEstaoNoCatalogo()
    {
        Assert.Contains(ToolCatalog.Notifications, ToolCatalog.All);
        Assert.Contains(ToolCatalog.OperationProfiles, ToolCatalog.All);
    }
}
