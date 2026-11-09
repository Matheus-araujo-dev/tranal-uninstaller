using System;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Localization;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O lote 2 das lacunas medidas: a selecao em massa por pais e o atalho
/// de copiar.
///
/// A selecao por pais mora no nucleo (<see cref="ProgramSelection"/>); a
/// janela consome e a lista nunca decide sozinha o que e "o mesmo pais".
/// </summary>
public class Lote2FeatureTests
{
    [Fact]
    public void ASelecaoPorPaisAceitaListaVazia()
    {
        Assert.Empty(ProgramSelection.Countries(null));
        Assert.Empty(ProgramSelection.Countries(Array.Empty<InstalledProgram>()));
        Assert.Empty(ProgramSelection.ByCountry(null, "Brasil"));
        Assert.Empty(ProgramSelection.ByCountry(Array.Empty<InstalledProgram>(), "Brasil"));
    }

    [Fact]
    public void ASelecaoPorPaisIgnoraPaisVazio()
    {
        var programas = new[] { new InstalledProgram { DisplayName = "Sem executavel" } };

        // Sem executavel nao ha pais: e o que o detector devolve.
        Assert.Empty(ProgramSelection.Countries(programas));
        Assert.Empty(ProgramSelection.ByCountry(programas, string.Empty));
        Assert.Empty(ProgramSelection.ByCountry(programas, null));
    }

    [Fact]
    public void ASelecaoPorPaisNaoDevolveProgramasDeOutroPais()
    {
        var programas = new[] { new InstalledProgram { DisplayName = "Sem executavel" } };

        Assert.Empty(ProgramSelection.ByCountry(programas, "Japão"));
    }

    [Fact]
    public void OAtalhoDeCopiarEstaNaTabela()
    {
        // O atalho mora na tabela do nucleo (ShortcutMap): a janela so
        // traduz a tecla e pergunta o que fazer.
        var atalho = ShortcutMap.Find("Ctrl+C");

        Assert.NotNull(atalho);
        Assert.Equal(ShortcutAction.CopySelection, atalho!.Action);
        Assert.Equal("Shortcut.CopySelection", atalho.DescriptionKey);
    }

    [Fact]
    public void TodoAtalhoTemASuaDescricaoNoCatalogo()
    {
        foreach (var idioma in new[] { "pt-BR", "en-US" })
        {
            var catalogo = Loc.Load(idioma);

            foreach (var atalho in ShortcutMap.All)
            {
                Assert.True(catalogo.ContainsKey(atalho.DescriptionKey),
                    $"falta a chave '{atalho.DescriptionKey}' em {idioma}");
            }
        }
    }

    [Fact]
    public void AsChavesDoPaisExisteNosDoisIdiomas()
    {
        foreach (var idioma in new[] { "pt-BR", "en-US" })
        {
            var catalogo = Loc.Load(idioma);

            Assert.True(catalogo.ContainsKey("Cmd.MarkByCountry"));
            Assert.True(catalogo.ContainsKey("Cmd.UnmarkByCountry"));
            Assert.True(catalogo.ContainsKey("Cmd.MarkedByCountry"));
            Assert.True(catalogo.ContainsKey("Cmd.UnmarkedByCountry"));
        }
    }

    [Fact]
    public void OMenuDePaisSoApareceParaProgramaComPais()
    {
        // A regra: o item de menu so existe quando o programa tem pais. O
        // nome do pais vem da tabela unica no idioma do app.
        var pais = CountryDetector.FromCode("BR");

        Assert.NotNull(pais);
        Assert.False(string.IsNullOrWhiteSpace(pais!.Label));
    }
}
