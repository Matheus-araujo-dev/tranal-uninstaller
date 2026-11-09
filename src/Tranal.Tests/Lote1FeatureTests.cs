using System;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Reporting;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O lote 1 das lacunas medidas: o total no rodape, o reset de padroes do
/// navegador, o cache MRU do Windows, a exportacao das sobras em XML e o
/// aviso do item compartilhado.
///
/// Cada teste aqui prende uma capacidade que a auditoria mediu como
/// faltando. Sem eles, a mesma lacuna volta sem que ninguem perceba.
/// </summary>
public class Lote1FeatureTests : IDisposable
{
    private readonly string _raiz;

    public Lote1FeatureTests()
    {
        _raiz = Path.Combine(Path.GetTempPath(), "tranal-lote1-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_raiz);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_raiz)) Directory.Delete(_raiz, true);
        }
        catch (IOException)
        {
            // O temporario do teste nao pode derrubar a suite.
        }
    }

    [Fact]
    public void OTotalAgregadoSomaOTamanhoDosProgramas()
    {
        var programas = new[]
        {
            new InstalledProgram { DisplayName = "A", SizeBytes = 1000 },
            new InstalledProgram { DisplayName = "B", SizeBytes = 2000 },
            new InstalledProgram { DisplayName = "C" },
        };

        var total = ProgramTotalsCalculator.Compute(programas);

        Assert.Equal(3, total.Count);
        Assert.Equal(3000, total.SizeBytes);
    }

    [Fact]
    public void OTextoDoTotalVemDoCatalogoDeIdioma()
    {
        var total = ProgramTotalsCalculator.Compute(new[]
        {
            new InstalledProgram { DisplayName = "A", SizeBytes = 1024 },
        });

        // O texto nao e escrito a mao no nucleo: ele vem do catalogo, e por
        // isso a tela traduzida mostra o total no idioma da interface.
        Assert.Contains("1", total.Text);
        Assert.NotEqual("1 programa(s) ocupam", total.Text.Trim());
    }

    [Fact]
    public void OResetDePadroesSemNavegadorValemParaTodos()
    {
        // Antes, o metodo saia cedo com o navegador vazio — o botao da tela
        // nunca fazia nada.
        var todas = BrowserCleanup.ChavesDePolitica(null);

        Assert.Equal(4, todas.Count);
        Assert.Contains(todas, c => c.Contains("Google\\Chrome"));
        Assert.Contains(todas, c => c.Contains("Microsoft\\Edge"));
    }

    [Fact]
    public void OResetDePadroesFiltraPeloNavegadorInformado()
    {
        var chrome = BrowserCleanup.ChavesDePolitica("Chrome");
        var edge = BrowserCleanup.ChavesDePolitica("Edge");

        Assert.Equal(2, chrome.Count);
        Assert.All(chrome, c => Assert.Contains("Google\\Chrome", c));

        Assert.Equal(2, edge.Count);
        Assert.All(edge, c => Assert.Contains("Microsoft\\Edge", c));

        Assert.Empty(BrowserCleanup.ChavesDePolitica("Firefox"));
    }

    [Fact]
    public void ACacheMrCacheDoWindowsEstaNaLimpezaDoMru()
    {
        // A chave que o Geek limpa: e o cache MRU do proprio Windows.
        Assert.Contains(MruCleaner.KnownKeys,
            k => k.EndsWith(@"Local Settings\MrtCache", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AExportacaoDeSobrasEmXmlGravaOArquivo()
    {
        // O exportador existia e nao tinha consumidor: o resultado era
        // gerado e nunca guardado.
        var destino = Path.Combine(_raiz, "sobras.xml");

        var gravado = LeftoverReportExporter.WriteXml(destino, Array.Empty<LeftoverItem>());

        Assert.False(string.IsNullOrWhiteSpace(gravado));
        Assert.True(File.Exists(destino));

        var texto = File.ReadAllText(destino);
        Assert.Contains("<", texto);
    }

    [Fact]
    public void AExportacaoEmXmlFalhaHonestamente()
    {
        var gravado = LeftoverReportExporter.WriteXml(string.Empty, Array.Empty<LeftoverItem>());

        Assert.Equal(string.Empty, gravado);
    }

    [Fact]
    public void OAvisoDoItemCompartilhadoExisteNoCatalogoDeIdioma()
    {
        foreach (var idioma in new[] { "pt-BR", "en-US" })
        {
            var catalogo = Loc.Load(idioma);

            Assert.True(catalogo.ContainsKey("Tool.Shared.ConfirmRemove"),
                $"falta a chave do aviso de item compartilhado em {idioma}");
            Assert.False(string.IsNullOrWhiteSpace(catalogo["Tool.Shared.ConfirmRemove"]));
        }
    }

    [Fact]
    public void OAvisoDeDesinstalacaoSilenciosaExisteNoCatalogoDeIdioma()
    {
        foreach (var idioma in new[] { "pt-BR", "en-US" })
        {
            var catalogo = Loc.Load(idioma);

            Assert.True(catalogo.ContainsKey("Wizard.NoSilentUninstall"),
                $"falta a chave do aviso de desinstalacao silenciosa em {idioma}");
        }
    }

    [Fact]
    public void AacaoModificarExisteNoCatalogoDeIdioma()
    {
        foreach (var idioma in new[] { "pt-BR", "en-US" })
        {
            var catalogo = Loc.Load(idioma);

            Assert.True(catalogo.ContainsKey("Cmd.ModifyProgram"),
                $"falta a chave da acao Modificar em {idioma}");
            Assert.True(catalogo.ContainsKey("Cmd.ModifyProgramUnavailable"));
            Assert.True(catalogo.ContainsKey("Cmd.ModifyProgramFailed"));
        }
    }

    [Fact]
    public void OsDoisCatalogosDeIdiomaTemAsMesmasChaves()
    {
        var pt = Loc.Load("pt-BR");
        var en = Loc.Load("en-US");

        var faltando = pt.Keys.Where(k => !en.ContainsKey(k)).ToList();
        var sobrando = en.Keys.Where(k => !pt.ContainsKey(k)).ToList();

        Assert.True(faltando.Count == 0, $"faltando em en-US: {string.Join(", ", faltando)}");
        Assert.True(sobrando.Count == 0, $"sobrando em en-US: {string.Join(", ", sobrando)}");
    }
}
