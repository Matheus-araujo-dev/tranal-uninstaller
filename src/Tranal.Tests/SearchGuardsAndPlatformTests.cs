using System;
using System.IO;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Search;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// A busca difusa, as guardas de desinstalacao, a deteccao de plataforma e a
/// leitura dos metadados do executavel.
/// </summary>
public class SearchGuardsAndPlatformTests
{
    // ------------------------------------------------------------------
    // Busca difusa
    // ------------------------------------------------------------------

    [Fact]
    public void NormalizacaoTiraAcentoEDeixaMinusculo()
    {
        Assert.Equal("asau", FuzzyMatch.Normalize("Asaú"));
        Assert.Equal("acucar", FuzzyMatch.Normalize("Açúcar"));
        Assert.Equal(string.Empty, FuzzyMatch.Normalize(null));
    }

    [Fact]
    public void DistanciaMedeQuantasLetrasFaltam()
    {
        Assert.Equal(0, FuzzyMatch.Distance("Adobe", "adobe"));
        Assert.Equal(1, FuzzyMatch.Distance("Adobe", "Adobo"));
        Assert.Equal(5, FuzzyMatch.Distance("Adobe", ""));
    }

    [Fact]
    public void SimilaridadeVaiDeZeroAUm()
    {
        Assert.Equal(1.0, FuzzyMatch.Similarity("Adobe", "adobe"));
        Assert.Equal(0.0, FuzzyMatch.Similarity("Adobe", string.Empty));
        Assert.True(FuzzyMatch.Similarity("Adobe", "Adobo") > 0.7);
    }

    [Fact]
    public void BuscaDifusaAchaPalavraDentroDoNome()
    {
        Assert.True(FuzzyMatch.IsMatch("Adobe Acrobat Reader DC", "acrobat"));
        Assert.True(FuzzyMatch.IsMatch("Adobe Acrobat Reader DC", "reader"));
    }

    [Fact]
    public void BuscaDifusaNaoCasaComTextoSemRelacao()
    {
        Assert.False(FuzzyMatch.IsMatch("Bloco de Notas", "acrobat"));
    }

    [Fact]
    public void BuscaDifusaIgnoraTermoCurtoDemais()
    {
        // Com duas letras, qualquer texto parecido vira falso positivo.
        Assert.False(FuzzyMatch.IsMatch("Bloco de Notas", "no"));
    }

    [Fact]
    public void FiltroDaListaUsaABuscaDifusa()
    {
        var filtros = new ProgramFilterSet { SearchText = "acrobat" };

        Assert.True(filtros.Matches(new InstalledProgram { DisplayName = "Adobe Acrobat Reader DC" }));
        Assert.False(filtros.Matches(new InstalledProgram { DisplayName = "Bloco de Notas" }));
    }

    [Fact]
    public void BuscaDifusaDesligadaVoltaAoTextoExato()
    {
        var filtros = new ProgramFilterSet { SearchText = "acrobt", FuzzySearch = false };

        Assert.True(filtros.Matches(new InstalledProgram { DisplayName = "Acrobt Reader DC" }));
        Assert.False(filtros.Matches(new InstalledProgram { DisplayName = "Adobe Reader DC" }));
    }

    [Fact]
    public void ExclusaoNaoCasaPorAproximacao()
    {
        // "acrobat" e parecido com "Acrobt", mas nao esta escrito no nome: a
        // exclusao casa pelo texto exato, entao o programa fica na lista.
        var filtros = new ProgramFilterSet { SearchText = "acrobt" };
        filtros.ExcludedTerms.Add("acrobat");

        Assert.True(filtros.Matches(new InstalledProgram { DisplayName = "Acrobt Reader DC" }));

        // Quando o termo esta escrito no nome, a exclusao vale.
        filtros.ExcludedTerms.Clear();
        filtros.ExcludedTerms.Add("Acrobt");
        Assert.False(filtros.Matches(new InstalledProgram { DisplayName = "Acrobt Reader DC" }));
    }

    // ------------------------------------------------------------------
    // Guardas de desinstalacao
    // ------------------------------------------------------------------

    [Fact]
    public void GuardaReconheceOProprioTranal()
    {
        var proprio = new InstalledProgram
        {
            DisplayName = "Tranal Uninstaller Pro",
            InstallLocation = @"C:\Program Files\TranalUninstaller",
        };

        Assert.True(UninstallGuards.IsSelf(proprio));

        var resultado = UninstallGuards.Check(proprio);
        Assert.False(resultado.Allowed);
        Assert.Contains("Tranal", resultado.Reason);
    }

    [Fact]
    public void GuardaDeixaPassarProgramaComum()
    {
        var comum = new InstalledProgram { DisplayName = "Bloco de Notas" };

        var resultado = UninstallGuards.Check(comum);

        Assert.True(resultado.Allowed);
        Assert.False(resultado.RequiresWarning);
    }

    [Fact]
    public void ModoForcadoPedeAviso()
    {
        // O aviso so faz sentido quando existe o caminho seguro: com
        // desinstalador registrado, a forcada e o plano B e o usuario
        // precisa saber disso antes de confirmar.
        var comum = new InstalledProgram
        {
            DisplayName = "Bloco de Notas",
            UninstallString = "\"C:\\Program Files\\Notas\\unins.exe\"",
        };

        var resultado = UninstallGuards.Check(comum, forcado: true);

        Assert.True(resultado.Allowed);
        Assert.True(resultado.RequiresWarning);
        Assert.Contains("Bloco de Notas", resultado.Warning);
    }

    [Fact]
    public void ModoForcadoSemDesinstaladorNaoAvisa()
    {
        // Sem desinstalador registrado, a forcada nao e um plano B: e o
        // unico caminho. Nao ha o que avisar.
        var semDesinstalador = new InstalledProgram { DisplayName = "Programa Sem Desinstalador" };

        var resultado = UninstallGuards.Check(semDesinstalador, forcado: true);

        Assert.True(resultado.Allowed);
        Assert.False(resultado.RequiresWarning);
        Assert.Equal(string.Empty, resultado.Warning);
    }

    [Fact]
    public void MarcaDeDesinstalacaoEmAndamentoEVisivel()
    {
        UninstallGuards.ClearRunning();

        var antes = UninstallGuards.CheckRunning();
        Assert.True(antes.Allowed);

        Assert.True(UninstallGuards.MarkRunning("teste"));

        var durante = UninstallGuards.CheckRunning();
        Assert.False(durante.Allowed);
        Assert.Contains("teste", durante.Reason);

        UninstallGuards.ClearRunning();

        var depois = UninstallGuards.CheckRunning();
        Assert.True(depois.Allowed);
    }

    // ------------------------------------------------------------------
    // Plataforma de origem
    // ------------------------------------------------------------------

    [Fact]
    public void PlataformaDetectaLojaDeJogos()
    {
        var steam = new InstalledProgram
        {
            DisplayName = "Counter-Strike",
            InstallLocation = @"C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike",
        };

        var plataforma = AppPlatformDetector.Detect(steam);

        Assert.NotNull(plataforma);
        Assert.Equal("Steam", plataforma!.Name);
        Assert.True(plataforma.IsGameStore);
    }

    [Fact]
    public void PlataformaDetectaGerenciadorDePacotes()
    {
        var choco = new InstalledProgram
        {
            DisplayName = "Ferramenta",
            UninstallString = @"C:\ProgramData\chocolatey\bin\ferramenta.exe",
        };

        var plataforma = AppPlatformDetector.Detect(choco);

        Assert.NotNull(plataforma);
        Assert.Equal("Chocolatey", plataforma!.Name);
        Assert.True(plataforma.IsPackageManager);
    }

    [Fact]
    public void ProgramaComumNaoTemPlataforma()
    {
        var comum = new InstalledProgram
        {
            DisplayName = "Bloco de Notas",
            InstallLocation = @"C:\Windows\System32",
        };

        Assert.Null(AppPlatformDetector.Detect(comum));
        Assert.Null(AppPlatformDetector.Detect(null));
    }

    [Fact]
    public void PlataformasConhecidasNaoSaoVazias()
    {
        var conhecidas = AppPlatformDetector.Known;

        Assert.NotEmpty(conhecidas);
        Assert.All(conhecidas, p => Assert.False(string.IsNullOrWhiteSpace(p.Name)));
    }

    // ------------------------------------------------------------------
    // Metadados do executavel
    // ------------------------------------------------------------------

    [Fact]
    public void MetadadosDeArquivoInexistenteSaoNulos()
    {
        var dados = ExecutableMetadataReader.Read(Path.Combine(Path.GetTempPath(), "nao-existe-xyz.exe"));

        Assert.Null(dados);
        Assert.Equal(string.Empty, ExecutableMetadataReader.CompanyOf(null));
    }

    [Fact]
    public void MetadadosDoProprioExecutavelSaoLidos()
    {
        var caminho = System.Reflection.Assembly.GetExecutingAssembly().Location;
        if (caminho.Length == 0) return;

        var dados = ExecutableMetadataReader.Read(caminho);

        Assert.NotNull(dados);
    }

    [Fact]
    public void EmpresaVemDoRegistroQuandoExiste()
    {
        var programa = new InstalledProgram
        {
            DisplayName = "Exemplo",
            Publisher = "Empresa Registrada",
        };

        Assert.Equal("Empresa Registrada", ExecutableMetadataReader.PublisherOf(programa));
    }
}
