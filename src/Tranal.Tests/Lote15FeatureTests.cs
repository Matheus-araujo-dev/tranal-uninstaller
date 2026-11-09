using System;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Core.Updates;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// As features do lote 15: o banco local embutido (SQLite do Windows), a
/// importacao dos logs antigos, a instalacao de uma versao nova, a ligacao
/// entre programa e log de instalacao, a versao do servico de rastreio, os
/// votos, o monitor de desinstaladores silenciosos e as opcoes de
/// acessibilidade e de velocidade.
///
/// O que estes testes cobrem: o banco usa o motor do proprio Windows (e o
/// teste grava e le uma linha de verdade), os arquivos de estado ficam
/// isolados em pasta temporaria e as regras da interface respondem.
/// </summary>
public sealed class Lote15FeatureTests : IDisposable
{
    private readonly string _temporaria = Path.Combine(
        Path.GetTempPath(),
        "tranal-lote15-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string? _preferenciasAntigas;

    public Lote15FeatureTests()
    {
        Directory.CreateDirectory(_temporaria);

        _preferenciasAntigas = AppSettingsStore.FilePathOverride;
        AppSettingsStore.FilePathOverride = Path.Combine(_temporaria, "settings.json");

        LocalSqlStore.FolderOverride = Path.Combine(_temporaria, "db");
        TraceAttachment.FolderOverride = Path.Combine(_temporaria, "attachments");
        TraceService.FolderOverride = Path.Combine(_temporaria, "trace-service");
        CommunityVotes.FolderOverride = Path.Combine(_temporaria, "votes");
    }

    public void Dispose()
    {
        AppSettingsStore.FilePathOverride = _preferenciasAntigas;
        LocalSqlStore.FolderOverride = null;
        TraceAttachment.FolderOverride = null;
        TraceService.FolderOverride = null;
        CommunityVotes.FolderOverride = null;

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
    // O banco local embutido
    // ------------------------------------------------------------------

    [Fact]
    public void OBancoLocalUsaOSqliteDoWindows()
    {
        Assert.True(LocalSqlStore.IsAvailable);
        Assert.True(File.Exists(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32", "winsqlite3.dll")));
    }

    [Fact]
    public void OBancoGravaELeUmaRemocao()
    {
        Assert.True(LocalSqlStore.Append("Programa de Teste", "Moderate", "Removido", "pasta apagada"));

        var linhas = LocalSqlStore.Recent(5);

        Assert.NotEmpty(linhas);
        Assert.Contains(linhas, l => l.Any(c => c == "Programa de Teste"));
        Assert.Contains(linhas, l => l.Any(c => c == "Removido"));
    }

    [Fact]
    public void OBancoIgnoraUmProgramaVazio()
    {
        Assert.False(LocalSqlStore.Append("   ", "Moderate", "Removido"));
    }

    // ------------------------------------------------------------------
    // A importacao dos logs antigos
    // ------------------------------------------------------------------

    [Fact]
    public void OLogAntigoEImportado()
    {
        var log = Path.Combine(_temporaria, "uninstaller4.log");

        File.WriteAllLines(log, new[]
        {
            "# log do UnInstaller 4",
            "2024-05-06 12:34:56;Programa Antigo;removido",
            "2024-05-07;Outro Programa;instalado",
            "linha sem formato",
        });

        var entradas = LegacyLogImporter.Import(log);

        Assert.Equal(2, entradas.Count);
        Assert.Equal("Programa Antigo", entradas[0].ProgramName);
        Assert.Equal("removido", entradas[0].Action);
        Assert.Equal(new DateTime(2024, 5, 6, 12, 34, 56), entradas[0].When);
        Assert.Equal("Outro Programa", entradas[1].ProgramName);
    }

    [Fact]
    public void OLogAntigoInexistenteNaoQuebra()
    {
        Assert.Empty(LegacyLogImporter.Import(Path.Combine(_temporaria, "nao-existe.log")));
        Assert.Empty(LegacyLogImporter.Import(null));
    }

    // ------------------------------------------------------------------
    // A instalacao das novas versoes
    // ------------------------------------------------------------------

    [Fact]
    public void OInstaladorSoRodaQuandoPode()
    {
        Assert.False(UpdateInstaller.CanRun(null));
        Assert.False(UpdateInstaller.CanRun(Path.Combine(_temporaria, "nao-existe.exe")));

        var resultado = UpdateInstaller.Run(Path.Combine(_temporaria, "nao-existe.exe"));

        Assert.False(resultado.Started);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Detail));
    }

    // ------------------------------------------------------------------
    // A ligacao entre programa e log
    // ------------------------------------------------------------------

    [Fact]
    public void OLogPodeSerAnexadoAoPrograma()
    {
        Assert.True(TraceAttachment.Attach("chave-1", "Programa", "trace-1", @"C:\logs\trace-1.json"));

        var ligacao = TraceAttachment.Of("chave-1");

        Assert.NotNull(ligacao);
        Assert.Equal("trace-1", ligacao!.TraceId);
        Assert.Single(TraceAttachment.List());
    }

    [Fact]
    public void ALigacaoPodeSerSolta()
    {
        TraceAttachment.Attach("chave-1", "Programa", "trace-1", @"C:\logs\trace-1.json");

        Assert.True(TraceAttachment.Detach("chave-1"));
        Assert.Empty(TraceAttachment.List());
    }

    // ------------------------------------------------------------------
    // A versao do servico de rastreio
    // ------------------------------------------------------------------

    [Fact]
    public void OServicoDeRastreioTemVersaoEAviso()
    {
        Assert.True(TraceService.NeedsUpdatePrompt());

        Assert.True(TraceService.SaveVersion());
        Assert.True(TraceService.State().IsUpToDate);
        Assert.False(TraceService.NeedsUpdatePrompt());
    }

    // ------------------------------------------------------------------
    // Os votos
    // ------------------------------------------------------------------

    [Fact]
    public void OVotoDoUsuarioEGravado()
    {
        Assert.True(CommunityVotes.Vote("chave-1", "Programa", 5, "Muito bom"));

        var voto = CommunityVotes.Of("chave-1");

        Assert.NotNull(voto);
        Assert.Equal(5, voto!.Score);
        Assert.Equal("Muito bom", voto.Comment);
    }

    [Fact]
    public void OVotoNaoSaiDaMaquinaSozinho()
    {
        CommunityVotes.Vote("chave-1", "Programa", 3);

        var exportado = CommunityVotes.Export(Path.Combine(_temporaria, "votos.json"));

        Assert.NotEmpty(exportado);
        Assert.True(File.Exists(exportado));
    }

    // ------------------------------------------------------------------
    // O monitor de desinstaladores
    // ------------------------------------------------------------------

    [Fact]
    public void OMonitorDeDesinstaladoresReconheceOsNomes()
    {
        Assert.True(UninstallerWatcher.IsUninstallerName("msiexec.exe"));
        Assert.True(UninstallerWatcher.IsUninstallerName("unins000.exe"));
        Assert.False(UninstallerWatcher.IsUninstallerName("notepad.exe"));
        Assert.False(UninstallerWatcher.IsUninstallerName(null));
    }

    [Fact]
    public void OMonitorDeDesinstaladoresResponde()
    {
        var processos = UninstallerWatcher.List();

        Assert.NotNull(processos);
        Assert.False(string.IsNullOrWhiteSpace(UninstallerWatcher.Describe()));
    }

    // ------------------------------------------------------------------
    // A acessibilidade e o modo rapido
    // ------------------------------------------------------------------

    [Fact]
    public void AsOpcoesDeAcessibilidadeVemDasPreferencias()
    {
        AppSettingsStore.Save(new AppSettings { AccessibilityMode = true, FastMode = true });

        Assert.True(AccessibilityOptions.IsEnabled);
        Assert.True(AccessibilityOptions.AnnounceChanges);
        Assert.True(AccessibilityOptions.ShowFocusVisuals);
        Assert.True(AccessibilityOptions.HighlightDefaultButton);
        Assert.True(AccessibilityOptions.SkipSlowChecks);
        Assert.True(AccessibilityOptions.FontScale > 1.0);
    }

    [Fact]
    public void OModoRapidoDesligadoMedeTudo()
    {
        AppSettingsStore.Save(new AppSettings { FastMode = false });

        Assert.False(AccessibilityOptions.SkipSlowChecks);
        Assert.Equal(1.0, AccessibilityOptions.FontScale);
    }
}
