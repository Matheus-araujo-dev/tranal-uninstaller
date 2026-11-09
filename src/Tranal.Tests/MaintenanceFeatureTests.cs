using System;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Tools;
using Tranal.Uninstaller.Core.Tracing;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// A limpeza do navegador, o bloatware, a instalacao silenciosa, a soma de
/// verificacao, os dumps de falha, as impressoras, as duplicatas e a visao
/// das tarefas agendadas.
/// </summary>
public class MaintenanceFeatureTests : IDisposable
{
    private readonly string _temp;

    public MaintenanceFeatureTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "tranal-manutencao-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
        }
        catch (IOException)
        {
            // Temporario do sistema: o proprio Windows limpa depois.
        }
    }

    // ------------------------------------------------------------------
    // Limpeza do navegador
    // ------------------------------------------------------------------

    [Fact]
    public void ExtensoesOrfasNaoQuebram()
    {
        var orfas = BrowserCleanup.FindOrphanExtensions();

        Assert.NotNull(orfas);
    }

    [Fact]
    public void ResetDePadroesDevolveResultado()
    {
        var resultado = BrowserCleanup.ResetDefaults("Chrome");

        Assert.NotNull(resultado);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Message));
    }

    [Fact]
    public void ResetDePadroesSemNavegadorNaoFazNada()
    {
        var resultado = BrowserCleanup.ResetDefaults("   ");

        Assert.Equal(0, resultado.Removed);
    }

    // ------------------------------------------------------------------
    // Bloatware
    // ------------------------------------------------------------------

    [Fact]
    public void BloatwareDetectaJogoQueVemComOWindows()
    {
        var programas = new[]
        {
            new InstalledProgram { DisplayName = "Microsoft Solitaire Collection" },
            new InstalledProgram { DisplayName = "Bloco de Notas" },
        };

        var encontrados = BundlewareDetector.Detect(programas);

        Assert.Contains(encontrados, e => e.Program.DisplayName.Contains("Solitaire"));
        Assert.DoesNotContain(encontrados, e => e.Program.DisplayName.Contains("Bloco"));
    }

    [Fact]
    public void BloatwareMarcaOsDaMicrosoft()
    {
        var microsoft = new InstalledProgram { DisplayName = "App", Publisher = "Microsoft Corporation" };
        var outro = new InstalledProgram { DisplayName = "App", Publisher = "Outra Empresa" };

        Assert.True(BundlewareDetector.IsMicrosoft(microsoft));
        Assert.False(BundlewareDetector.IsMicrosoft(outro));
    }

    [Fact]
    public void CategoriasDeBloatwareNaoSaoVazias()
    {
        Assert.NotEmpty(BundlewareDetector.Categories);
        Assert.All(BundlewareDetector.Categories, c => Assert.NotEmpty(c.Markers));
    }

    // ------------------------------------------------------------------
    // Instalacao silenciosa
    // ------------------------------------------------------------------

    [Fact]
    public void InstaladorMsiRecebeOsParametrosSilenciosos()
    {
        var argumentos = SilentInstaller.ArgumentsFor(@"C:\Temp\pacote.msi");

        Assert.Contains("/qn", argumentos);
        Assert.Contains("/norestart", argumentos);
        Assert.Contains("msiexec.exe", SilentInstaller.BuildCommand(@"C:\Temp\pacote.msi"));
    }

    [Fact]
    public void InstaladorExeRecebeOsParametrosSilenciosos()
    {
        var argumentos = SilentInstaller.ArgumentsFor(@"C:\Temp\setup.exe");

        Assert.Contains("/VERYSILENT", argumentos);
        Assert.Contains("/SUPPRESSMSGBOXES", argumentos);
        Assert.Contains("/NORESTART", argumentos);
    }

    [Fact]
    public void InstaladorInexistenteNaoRoda()
    {
        var resultado = SilentInstaller.Install(Path.Combine(_temp, "nao-existe.exe"));

        Assert.False(resultado.Success);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Message));
    }

    // ------------------------------------------------------------------
    // Soma de verificacao
    // ------------------------------------------------------------------

    [Fact]
    public void SomaDeVerificacaoDoArquivo()
    {
        var arquivo = Path.Combine(_temp, "soma.bin");
        File.WriteAllText(arquivo, "tranal");

        var soma = InstallChecksum.Of(arquivo);

        Assert.NotNull(soma);
        Assert.Equal(32, soma!.Md5.Length);
        Assert.Equal(64, soma.Sha256.Length);
        Assert.True(InstallChecksum.Verify(arquivo, soma.Md5));
    }

    [Fact]
    public void SomaDeArquivoInexistenteENulo()
    {
        Assert.Null(InstallChecksum.Of(Path.Combine(_temp, "nao-existe.bin")));
    }

    // ------------------------------------------------------------------
    // Dumps de falha
    // ------------------------------------------------------------------

    [Fact]
    public void DumpsDeFalhaSaoListadosSemQuebrar()
    {
        var dumps = CrashDumpCollector.List();

        Assert.NotNull(dumps);
        Assert.All(dumps, d => Assert.False(string.IsNullOrWhiteSpace(d.Name)));
    }

    [Fact]
    public void ProcessoDoDumpEhAdivinhadoPeloNome()
    {
        Assert.Equal("app.exe", CrashDumpCollector.GuessProcess(@"C:\dumps\app.exe.1234.dmp"));
        Assert.Equal(string.Empty, CrashDumpCollector.GuessProcess(null));
    }

    // ------------------------------------------------------------------
    // Impressoras
    // ------------------------------------------------------------------

    [Fact]
    public void ImpressorasSaoEnumeradasSemQuebrar()
    {
        var impressoras = PrinterEnumerator.List();

        Assert.NotNull(impressoras);
    }

    [Fact]
    public void FilaDeImpressaoEEnumeradaSemQuebrar()
    {
        var fila = PrinterEnumerator.ListQueue();

        Assert.NotNull(fila);
    }

    // ------------------------------------------------------------------
    // Duplicatas
    // ------------------------------------------------------------------

    [Fact]
    public void DuplicatasAgrupamPeloNomeNormalizado()
    {
        var programas = new[]
        {
            new InstalledProgram { DisplayName = "Meu Programa 1.0", InstallLocation = @"C:\A" },
            new InstalledProgram { DisplayName = "Meu Programa 2.0", InstallLocation = @"C:\B" },
            new InstalledProgram { DisplayName = "Outro Programa", InstallLocation = @"C:\C" },
        };

        var grupos = DuplicateInstallDetector.Detect(programas);

        Assert.Single(grupos);
        Assert.Equal(2, grupos[0].Count);
    }

    [Fact]
    public void NormalizacaoTiraVersaoEEspaco()
    {
        Assert.Equal("meu programa", DuplicateInstallDetector.Normalize("Meu Programa (1.0)"));
        Assert.Equal("meu programa", DuplicateInstallDetector.Normalize("Meu Programa (x64)"));
        Assert.Equal(string.Empty, DuplicateInstallDetector.Normalize(null));
    }

    // ------------------------------------------------------------------
    // Tarefas agendadas
    // ------------------------------------------------------------------

    [Fact]
    public void TarefasDaMicrosoftSaoReconhecidas()
    {
        var microsoft = new ScheduledTask { Name = @"\Microsoft\Windows\Defrag\Defrag", Folder = @"\Microsoft\Windows\Defrag" };
        var doUsuario = new ScheduledTask { Name = @"\Tranal\Backup", Folder = @"\Tranal" };

        Assert.True(ScheduledTaskVisibility.IsMicrosoft(microsoft));
        Assert.False(ScheduledTaskVisibility.IsMicrosoft(doUsuario));
    }

    [Fact]
    public void VisaoDeTarefasNaoQuebra()
    {
        var tarefas = ScheduledTaskVisibility.List();

        Assert.NotNull(tarefas);
        Assert.All(tarefas, t => Assert.False(ScheduledTaskVisibility.IsMicrosoft(t)));
    }
}
