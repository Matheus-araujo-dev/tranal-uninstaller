using System;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Reporting;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// As features do lote 14: a limpeza de cache, a captura de tela, o
/// relatorio de falha, o script de remocao simples, o processo de 32 bits, a
/// carga de chaves, a estacao de rede, os privilegios do token, o aviso de
/// sincronizacao do Chrome e o agente de fundo.
///
/// O que estes testes cobrem: o que apaga esta isolado em pasta temporaria,
/// o que grava no registro usa uma chave de teste, e o resto responde sem
/// lancar.
/// </summary>
public sealed class Lote14FeatureTests : IDisposable
{
    private readonly string _temporaria = Path.Combine(
        Path.GetTempPath(),
        "tranal-lote14-" + Guid.NewGuid().ToString("N")[..8]);

    public Lote14FeatureTests()
    {
        Directory.CreateDirectory(_temporaria);
        CrashReport.FolderOverride = Path.Combine(_temporaria, "crash-reports");
    }

    public void Dispose()
    {
        CrashReport.FolderOverride = null;

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
    // A limpeza de cache
    // ------------------------------------------------------------------

    [Fact]
    public void OCacheDosProgramasEhMedido()
    {
        var programa = new InstalledProgram
        {
            DisplayName = "Programa de Teste",
            Publisher = "Editora de Teste",
        };

        var entradas = AppCacheCleaner.ScanOf(programa);

        Assert.NotNull(entradas);
        Assert.All(entradas, e => Assert.True(e.SizeBytes >= 0));
    }

    [Fact]
    public void AMedidaDoCacheDevolveZeroParaPastaInexistente()
    {
        Assert.Equal(0, AppCacheCleaner.Measure(Path.Combine(_temporaria, "nao-existe")));
    }

    // ------------------------------------------------------------------
    // A captura de tela
    // ------------------------------------------------------------------

    [Fact]
    public void ATelaTemTamanhoConhecido()
    {
        var (largura, altura) = ScreenCapture.ScreenSize();

        // Um processo sem sessao grafica (um servico, um agente de CI) nao
        // tem area de trabalho: o tamanho e zero e a captura e recusada.
        if (largura == 0)
        {
            Assert.Equal(0, altura);
            Assert.False(ScreenCapture.HasDesktop());
            return;
        }

        Assert.True(largura > 0);
        Assert.True(altura > 0);
        Assert.True(ScreenCapture.HasDesktop());
    }

    [Fact]
    public void ACapturaGravaUmArquivo()
    {
        var destino = Path.Combine(_temporaria, "captura.bmp");

        var gravado = ScreenCapture.Capture(destino);

        if (!ScreenCapture.HasDesktop())
        {
            // Sem area de trabalho, a captura e recusada — e nada e gravado.
            Assert.Empty(gravado);
            Assert.False(File.Exists(destino));
            return;
        }

        Assert.Equal(destino, gravado);
        Assert.True(File.Exists(destino));

        // O arquivo BMP comeca com a assinatura "BM".
        var cabecalho = File.ReadAllBytes(destino).Take(2).ToArray();
        Assert.Equal((byte)'B', cabecalho[0]);
        Assert.Equal((byte)'M', cabecalho[1]);
    }

    // ------------------------------------------------------------------
    // O relatorio de falha
    // ------------------------------------------------------------------

    [Fact]
    public void ORelatorioDeFalhaEGravaEmDisco()
    {
        var relatorio = CrashReport.Write("desinstalando", new InvalidOperationException("falha de teste"));

        Assert.NotNull(relatorio);
        Assert.True(File.Exists(relatorio!.ReportPath));
        Assert.Contains("falha de teste", relatorio.Message);
        Assert.Contains("desinstalando", CrashReport.Describe(relatorio));
        Assert.Contains(relatorio.ReportPath, CrashReport.List());
    }

    [Fact]
    public void ORelatorioAceitaUmaFalhaNula()
    {
        var relatorio = CrashReport.Write("iniciando", null);

        Assert.NotNull(relatorio);
        Assert.Empty(relatorio!.Message);
    }

    // ------------------------------------------------------------------
    // O script de remocao simples
    // ------------------------------------------------------------------

    [Fact]
    public void OScriptDeRemocaoSimplesTrazAPastaEAsChaves()
    {
        var script = SimpleDeleteWriter.Build(
            @"C:\Program Files\App",
            new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\App" },
            "App");

        Assert.Contains("rmdir /s /q", script);
        Assert.Contains(@"C:\Program Files\App", script);
        Assert.Contains("reg delete", script);
        Assert.Contains("App", script);
    }

    [Fact]
    public void OScriptDeRemocaoSimplesGravaEmDisco()
    {
        var destino = Path.Combine(_temporaria, SimpleDeleteWriter.FileName);

        var gravado = SimpleDeleteWriter.Write(@"C:\App", new[] { @"HKLM\SOFTWARE\App" }, "App", destino);

        Assert.Equal(destino, gravado);
        Assert.True(File.Exists(destino));
    }

    // ------------------------------------------------------------------
    // O processo de 32 bits
    // ------------------------------------------------------------------

    [Fact]
    public void APastaDe32BitsExisteNoWindows()
    {
        Assert.True(Directory.Exists(Wow64Process.SysWow64Folder));
        Assert.True(Wow64Process.X86PathOf("cmd.exe") is not null);
    }

    [Fact]
    public void OProcessoDe32BitsNaoEInventado()
    {
        Assert.Null(Wow64Process.X86PathOf(@"C:\nao-existe\nao-existe.exe"));
    }

    // ------------------------------------------------------------------
    // A carga de chaves
    // ------------------------------------------------------------------

    [Fact]
    public void AsChavesMontadasSaoListadas()
    {
        var chaves = RegistryHiveLoader.List();

        Assert.NotNull(chaves);
        Assert.All(chaves, c => Assert.False(string.IsNullOrWhiteSpace(c.Name)));
    }

    [Fact]
    public void OCarregamentoDeUmaChaveInexistenteFalha()
    {
        Assert.False(RegistryHiveLoader.Load("TesteTranal", Path.Combine(_temporaria, "nao-existe.hiv")));
    }

    // ------------------------------------------------------------------
    // A estacao de rede
    // ------------------------------------------------------------------

    [Fact]
    public void AEstacaoDeRedeSeApresenta()
    {
        var estacao = NetworkStationInfo.Current();

        Assert.False(string.IsNullOrWhiteSpace(estacao.UserName));
        Assert.False(string.IsNullOrWhiteSpace(estacao.ComputerName));
        Assert.NotNull(estacao.MacAddress);
    }

    // ------------------------------------------------------------------
    // Os privilegios do token
    // ------------------------------------------------------------------

    [Fact]
    public void OsPrivilegiosDoTokenRespondem()
    {
        var privilegios = TokenPrivileges.List();

        Assert.Equal(4, privilegios.Count);
        Assert.Contains(privilegios, p => p.Id == TokenPrivileges.SeDebugPrivilege);
        Assert.Contains(privilegios, p => p.Id == TokenPrivileges.SeImpersonatePrivilege);
    }

    // ------------------------------------------------------------------
    // O aviso de sincronizacao do Chrome
    // ------------------------------------------------------------------

    [Fact]
    public void OChromeSyncResponde()
    {
        Assert.False(ChromeSyncGuard.WarningFor(null).Length > 0);
        Assert.False(ChromeSyncGuard.WarningFor("").Length > 0);
        Assert.NotNull(ChromeSyncGuard.ProfilesFolder);
    }

    // ------------------------------------------------------------------
    // O agente de fundo
    // ------------------------------------------------------------------

    [Fact]
    public void OAgenteDeFundoResponde()
    {
        Assert.NotNull(BackgroundAgent.StateFilePath);
        Assert.False(string.IsNullOrWhiteSpace(BackgroundAgent.AgentArgument));
    }
}
