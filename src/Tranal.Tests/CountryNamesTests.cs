using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Localization;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O nome do país de um programa.
///
/// A REGRA é que o nome vem da tabela de países, no idioma do aplicativo —
/// nunca do "DisplayName" da cultura do Windows. Sem isso, uma máquina em
/// inglês dos EUA mostrava "Spanish (Brazil)" no lugar de "Brasil" e o
/// teste do CI quebrava só por causa do locale da máquina.
/// </summary>
public class CountryNamesTests
{
    [Fact]
    public void NomeDoPaisVeemDoIdiomaDoApp()
    {
        Assert.Equal("Brasil", CountryNames.Nome("BR", "pt-BR"));
        Assert.Equal("Estados Unidos", CountryNames.Nome("US", "pt-BR"));
        Assert.Equal("Alemanha", CountryNames.Nome("DE", "pt-BR"));
        Assert.Equal("Japan", CountryNames.Nome("JP", "en-US"));
    }

    [Fact]
    public void NomeDoPaisNaoDependeDaCulturaDoSistema()
    {
        var anterior = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            Assert.Equal("Brasil", CountryNames.Nome("BR", "pt-BR"));
            Assert.Equal("Brasil", CountryDetector.FromCode("BR")!.Name);
        }
        finally
        {
            CultureInfo.CurrentUICulture = anterior;
            Thread.CurrentThread.CurrentUICulture = anterior;
        }
    }

    [Fact]
    public void NomeDoPaisAcompanhaOIdiomaAtivoDoApp()
    {
        var anterior = Loc.Current;
        try
        {
            Loc.SetLanguage("en-US");
            Assert.Equal("Brazil", CountryDetector.FromCode("BR")!.Name);

            Loc.SetLanguage("pt-BR");
            Assert.Equal("Brasil", CountryDetector.FromCode("BR")!.Name);
        }
        finally
        {
            Loc.SetLanguage(anterior);
        }
    }

    [Fact]
    public void PaisProcuradoPeloNomeExtenso()
    {
        Assert.Equal("BR", CountryNames.CodigoNoTexto("7-Zip, Brasil"));
        Assert.Equal("BR", CountryNames.CodigoNoTexto("7-Zip, Brazil"));
        Assert.Equal("DE", CountryNames.CodigoNoTexto("ACME Tools Germany"));
        Assert.Null(CountryNames.CodigoNoTexto("NirvanaSoft Suite"));
        Assert.Null(CountryNames.CodigoNoTexto(null));
    }

    [Fact]
    public void TodosOsPaisesTecnomeNosDoisIdiomas()
    {
        var todos = CountryNames.Todos();

        Assert.True(todos.Count >= 70, $"A tabela tem {todos.Count} paises.");
        Assert.All(todos, p =>
        {
            Assert.Equal(2, p.Code.Length);
            Assert.False(string.IsNullOrWhiteSpace(p.Pt));
            Assert.False(string.IsNullOrWhiteSpace(p.En));
        });

        // Nenhum nome pode vir repetido com codigos diferentes: dois paises
        // com o mesmo nome quebram a busca por extenso.
        Assert.Equal(todos.Count, todos.Select(p => p.Pt).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(todos.Count, todos.Select(p => p.En).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void PaisForaDaTabelaTemNomeNativo()
    {
        // "ZW" (Zimbabwe) esta fora da tabela: o nome nativo da regiao
        // ainda responde, e nao fica vazio.
        var nome = CountryNames.Nome("ZW", "pt-BR");

        Assert.False(string.IsNullOrWhiteSpace(nome));
        Assert.Equal(string.Empty, CountryNames.Nome("XX", "pt-BR"));
        Assert.Equal(string.Empty, CountryNames.Nome(null, "pt-BR"));
    }
}
