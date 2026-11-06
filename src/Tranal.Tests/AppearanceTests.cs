using System;
using System.Collections.Generic;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.RegistryEditing;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// A segunda metade do Lote H: tema e cores, copia para a area de
/// transferencia, editor de registro, suporte e retencao de preferencias.
///
/// Os testes nao tocam o registro do dono: o editor so e exercitado nos
/// casos em que a operacao **nao** chega a escrever (caminho invalido, chave
/// protegida), e a formatacao e testada com dados sinteticos.
/// </summary>
public class AppearanceTests
{
    // ---------------- Tema e cores ----------------

    [Fact]
    public void OHexDaCorAceitaAsFormasQueOUsuarioDigita()
    {
        Assert.True(ThemePalette.TryNormalizeHex("#ABC", out var curta));
        Assert.Equal("#AABBCC", curta);

        Assert.True(ThemePalette.TryNormalizeHex("#1e2a38", out var longa));
        Assert.Equal("#1E2A38", longa);

        Assert.True(ThemePalette.TryNormalizeHex("FF112233", out var comAlfa));
        Assert.Equal("#FF112233", comAlfa);
    }

    [Fact]
    public void OHexDaCorRecusaOQueNaoECor()
    {
        Assert.False(ThemePalette.TryNormalizeHex("nao e cor", out _));
        Assert.False(ThemePalette.TryNormalizeHex("#12345", out _));
        Assert.False(ThemePalette.TryNormalizeHex("#GGGGGG", out _));
        Assert.False(ThemePalette.TryNormalizeHex(null, out _));
    }

    [Fact]
    public void ACorPersonalizadaGanhaDoTema()
    {
        var baseTheme = ThemePalettes.Dark;
        var custom = new Dictionary<string, string> { ["Text"] = "#FF0000" };

        var merged = baseTheme.Merge(custom);

        Assert.Equal("#FF0000", merged.ColorOf(ThemeColorRole.Text));
        // Os papeis que o usuario nao tocou continuam com a cor do tema.
        Assert.Equal(baseTheme.ColorOf(ThemeColorRole.Panel), merged.ColorOf(ThemeColorRole.Panel));
    }

    [Fact]
    public void ACorPersonalizadaInvalidaEIgnoradaEOThemeContinuaNoLugar()
    {
        var baseTheme = ThemePalettes.Dark;
        var custom = new Dictionary<string, string> { ["Text"] = "nao e cor" };

        var merged = baseTheme.Merge(custom);

        Assert.Equal(baseTheme.ColorOf(ThemeColorRole.Text), merged.ColorOf(ThemeColorRole.Text));
    }

    [Fact]
    public void OThemeContinuaValendoQuandoACorPersonalizadaNaoExiste()
    {
        var merged = ThemePalettes.Dark.Merge(null);

        Assert.Equal(ThemePalettes.Dark.Colors.Count, merged.Colors.Count);
    }

    [Fact]
    public void OCatalogoDeTemasTemOsQuatroEGravaOPrimeiroComoPadrao()
    {
        Assert.Equal(4, ThemeSkinCatalog.All.Count);
        Assert.Equal("Dark", ThemeSkinCatalog.Default);
        Assert.True(ThemeSkinCatalog.Exists("Slate"));
        Assert.False(ThemeSkinCatalog.Exists(" inexistente"));
    }

    [Fact]
    public void ONomeDoThemeAceitaOApelidoAntigoDoClaro()
    {
        Assert.Equal("Light", ThemeSkinCatalog.Normalize("claro"));
        Assert.Equal("Light", ThemeSkinCatalog.Normalize("CLARO"));
        Assert.Equal("Light", ThemeSkinCatalog.Normalize("classico"));
        Assert.Equal("Dark", ThemeSkinCatalog.Normalize("desconhecido"));
        Assert.Equal("Dark", ThemeSkinCatalog.Normalize(null));
    }

    [Fact]
    public void CadaPapelDeCorTemASuaDescricao()
    {
        foreach (var role in ThemePalettes.Roles)
            Assert.False(string.IsNullOrWhiteSpace(ThemePalettes.DescribeRole(role)));
    }

    [Fact]
    public void ACorDoWPFVemDoHexComAlfaInteiro()
    {
        Assert.True(ThemePalettes.TryParseColor("#1E2A38", out var argb));
        Assert.Equal(0x1E, (int)((argb >> 16) & 0xFF));
        Assert.Equal(0x2A, (int)((argb >> 8) & 0xFF));
        Assert.Equal(0x38, (int)(argb & 0xFF));

        Assert.False(ThemePalettes.TryParseColor("nao e cor", out _));
    }

    // ---------------- Copia para a area de transferencia ----------------

    private static InstalledProgram Programa(string nome = "Exemplo") => new()
    {
        DisplayName = nome,
        DisplayVersion = "1.2.3",
        Publisher = "Fabricante",
        UninstallString = @"C:\Program Files\Exemplo\unins000.exe",
    };

    [Fact]
    public void ACopiaEmTextoSimplesTemNomeVersaoEEmpresa()
    {
        var texto = ProgramTextFormatter.Format(Programa(), ClipboardFormat.PlainText);

        Assert.Contains("Exemplo", texto);
        Assert.Contains("1.2.3", texto);
        Assert.Contains("Fabricante", texto);
    }

    [Fact]
    public void ACopiaEmColunasTemOCabecalhoEUmaLinhaPorPrograma()
    {
        var texto = ProgramTextFormatter.Format(
            new[] { Programa("A"), Programa("B") }, ClipboardFormat.TabSeparated);

        var linhas = texto.Split('\n');
        Assert.Equal(3, linhas.Length);
        Assert.Contains("Program", linhas[0]);
        Assert.Contains("A", linhas[1]);
        Assert.Contains("B", linhas[2]);
    }

    [Fact]
    public void ACopiaEmColunasNaoQuebraComValorQueTemTabulacao()
    {
        var programa = Programa();
        programa.Comments = "linha 1\tlinha 2";

        var texto = ProgramTextFormatter.Format(programa, ClipboardFormat.TabSeparated);

        // O comentario tem tabulacao dentro: se ela escapasse, a linha
        // ganhariam uma coluna a mais.
        foreach (var linha in texto.Split('\n'))
            Assert.True(linha.Split('\t').Length <= Exporters.DefaultColumns.Count + 1);
    }

    [Fact]
    public void ACopiaDosComandosMostraOComandoDeDesinstalacao()
    {
        var texto = ProgramTextFormatter.Format(Programa(), ClipboardFormat.UninstallCommand);

        Assert.Contains("unins000.exe", texto);
    }

    [Fact]
    public void ACopiaDosDetalhesTemRotuloEmCadaCampo()
    {
        var texto = ProgramTextFormatter.Format(Programa(), ClipboardFormat.FullDetails);

        Assert.Contains("Versão:", texto);
        Assert.Contains("Empresa:", texto);
    }

    [Fact]
    public void CopiarUmaListaVaziaNaoDevolveTextoNenhum()
    {
        Assert.Equal(string.Empty, ProgramTextFormatter.Format(
            Array.Empty<InstalledProgram>(), ClipboardFormat.PlainText));
    }

    // ---------------- Suporte ----------------

    [Fact]
    public void AMensagemDeSuporteTemOProgramaEOAmbiente()
    {
        var mensagem = SupportRequest.BuildMessage(Programa());

        Assert.Contains("Exemplo", mensagem);
        Assert.Contains("Windows:", mensagem);
        Assert.Contains("Versão do Tranal", mensagem);
    }

    [Fact]
    public void AURLDeSuporteTrazOTituloEOCorpoProntos()
    {
        var url = SupportRequest.BuildUrl(Programa(), "o programa nao desinstala");

        Assert.StartsWith(SupportRequest.SupportUrl, url);
        Assert.Contains("title=", url);
        Assert.Contains("body=", url);
    }

    [Fact]
    public void OEnderecoDeSuporteEORepositorioPublicoDoProjeto()
    {
        Assert.Contains("github.com/Matheus-araujo-dev/tranal-uninstaller", SupportRequest.SupportUrl);
    }

    // ---------------- Retencao de preferencias ----------------

    [Fact]
    public void AProgramaSemPreferenciaNaoTemNadaASerApagado()
    {
        Assert.False(PreferenceRetention.HasPreferences(Programa()));
        Assert.Empty(PreferenceRetention.WhatWouldBeRemoved(Programa()));
        Assert.Equal(string.Empty, PreferenceRetention.Question(Programa()));
    }

    // ---------------- Editor de registro ----------------

    [Fact]
    public void OEditorDeRegistroRecusaCaminhoQueNaoComecaComUmHive()
    {
        var resultado = RegistryEditor.RenameKey("SOFTWARE\\Exemplo", "Novo");

        Assert.False(resultado.Success);
        Assert.Contains("HKCU", resultado.Message);
    }

    [Fact]
    public void OEditorDeRegistroRecusaCaminhoVazio()
    {
        var resultado = RegistryEditor.DeleteKey("   ");

        Assert.False(resultado.Success);
    }

    [Fact]
    public void OEditorDeRegistroRecusaChaveProtegidaDoSistema()
    {
        var resultado = RegistryEditor.DeleteKey(@"HKLM\SOFTWARE\Microsoft\Windows");

        Assert.False(resultado.Success);
        Assert.Contains("protegida", resultado.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OEditorDeRegistroRecusaNomeDeChaveComBarra()
    {
        var resultado = RegistryEditor.RenameKey(@"HKCU\SOFTWARE\Exemplo", @"Invalido\Nome");

        Assert.False(resultado.Success);
        Assert.Contains("barra", resultado.Message, StringComparison.OrdinalIgnoreCase);
    }
}
