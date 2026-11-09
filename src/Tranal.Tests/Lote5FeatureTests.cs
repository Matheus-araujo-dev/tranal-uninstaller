using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Recovery;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// As features do lote 5: o File Associator, o Centro de Mudancas, o filtro
/// de tipo de arquivo e os modos de execucao.
///
/// O que estes testes cobrem ja aconteceu de verdade: uma feature sem
/// consumidor e um modulo orfao (que nao conta), e o filtro de tipo que
/// nao entrava na remocao (a preferencia existia, o efeito nao).
/// </summary>
public class Lote5FeatureTests
{
    // ------------------------------------------------------------------
    // Filtro de tipo de arquivo
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("C:\\Program Files\\App\\app.ico", RemovalFileKind.Icon)]
    [InlineData("C:\\Program Files\\App\\app.dll", RemovalFileKind.Library)]
    [InlineData("C:\\Program Files\\App\\ajuda.htm", RemovalFileKind.WebPage)]
    [InlineData("C:\\Program Files\\App\\ajuda.html", RemovalFileKind.WebPage)]
    [InlineData("C:\\Users\\Zé\\Desktop\\App.lnk", RemovalFileKind.Shortcut)]
    [InlineData("C:\\Program Files\\App\\app.exe", RemovalFileKind.Executable)]
    [InlineData("C:\\Program Files\\App\\leia-me.txt", RemovalFileKind.Other)]
    public void OClassificadorReconheceAFamiliaDoArquivo(string caminho, RemovalFileKind esperado)
    {
        Assert.Equal(esperado, FileTypeFilter.Classify(caminho));
    }

    [Fact]
    public void OClassificadorNaoQuebraComCaminhoDeVasoura()
    {
        Assert.Equal(RemovalFileKind.Other, FileTypeFilter.Classify(null));
        Assert.Equal(RemovalFileKind.Other, FileTypeFilter.Classify(string.Empty));
    }

    [Fact]
    public void OFiltroDesmarcaOQueEstaPreservado()
    {
        var ico = new LeftoverItem { Kind = "Arquivo", Path = @"C:\App\app.ico", Selected = true };
        var exe = new LeftoverItem { Kind = "Arquivo", Path = @"C:\App\app.exe", Selected = true };
        var pasta = new LeftoverItem { Kind = "Pasta", Path = @"C:\App\app.ico", Selected = true };

        var preservados = new FileTypeFilter(new[] { RemovalFileKind.Icon }).Apply(new[] { ico, exe, pasta });

        Assert.Equal(1, preservados);
        Assert.False(ico.Selected);
        Assert.True(exe.Selected);
        Assert.True(pasta.Selected);
    }

    [Fact]
    public void OFiltroVazioNaoDesmarcaNada()
    {
        var item = new LeftoverItem { Kind = "Arquivo", Path = @"C:\App\app.ico", Selected = true };

        Assert.Equal(0, FileTypeFilter.Empty.Apply(new[] { item }));
        Assert.True(item.Selected);
    }

    [Fact]
    public void OFiltroSeMontaPeloNomeDaFamilia()
    {
        var filtro = FileTypeFilter.FromNames(new[] { "Icon", "Library", "Desconhecida" });

        Assert.True(filtro.IsPreserved(RemovalFileKind.Icon));
        Assert.True(filtro.IsPreserved(RemovalFileKind.Library));
        Assert.False(filtro.IsPreserved(RemovalFileKind.Executable));
        Assert.Contains("Icon", filtro.Names());
    }

    // ------------------------------------------------------------------
    // Modos de execucao e de exibicao
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(ExecutionPreference.System, "system")]
    [InlineData(ExecutionPreference.Always, "always")]
    [InlineData(ExecutionPreference.Never, "never")]
    [InlineData(ExecutionPreference.Ask, "ask")]
    public void OModoFazIdaEVolta(ExecutionPreference modo, string texto)
    {
        Assert.Equal(texto, ExecutionPreferences.Text(modo));
        Assert.Equal(modo, ExecutionPreferences.Parse(texto));
    }

    [Fact]
    public void ModoDesconhecidoViraOSistema()
    {
        Assert.Equal(ExecutionPreference.System, ExecutionPreferences.Parse(null));
        Assert.Equal(ExecutionPreference.System, ExecutionPreferences.Parse(string.Empty));
        Assert.Equal(ExecutionPreference.System, ExecutionPreferences.Parse("modo do ano passado"));
        Assert.Equal("system", ExecutionPreferences.Normalize("modo do ano passado"));
    }

    [Fact]
    public void APreferenciaGuardaOModoComoTexto()
    {
        var settings = new AppSettings { RunPreference = "always", DisplayPreference = "never" };

        Assert.Equal(ExecutionPreference.Always, settings.RunMode);
        Assert.Equal(ExecutionPreference.Never, settings.DisplayMode);
    }

    // ------------------------------------------------------------------
    // Centro de mudancas
    // ------------------------------------------------------------------

    [Fact]
    public void OCentroDeMudancasLeSemQuebrar()
    {
        // A leitura e sobre o backup de verdade do usuario: nao escreve nada,
        // e precisa responder mesmo quando nao ha nada guardado.
        var report = UndoChangesCenter.Scan();

        Assert.NotNull(report.Items);
        Assert.Equal(report.Items.Count, report.Total);
        Assert.All(report.Items, i => Assert.NotEqual(string.Empty, i.Id));
    }

    [Fact]
    public void DesfazerSemIdentificadorNaoFazNada()
    {
        var (ok, _) = UndoChangesCenter.Undo(null);

        Assert.False(ok);
    }

    [Fact]
    public void DesfazerComIdentificadorInexistenteNaoFazNada()
    {
        var (ok, mensagem) = UndoChangesCenter.Undo("identificador-que-nao-existe-tranal");

        Assert.False(ok);
        Assert.NotEqual(string.Empty, mensagem);
    }

    // ------------------------------------------------------------------
    // File Associator
    // ------------------------------------------------------------------

    [Fact]
    public void OFileAssociatorVarreORegistro()
    {
        // A varredura e de leitura: as associacoes do Windows tem de estar
        // la, e a varredura nao pode falhar por causa de uma chave malformada.
        var report = FileAssociator.Scan();

        Assert.NotNull(report.Items);
        Assert.All(report.Items, i => Assert.StartsWith(".", i.Extension));
    }

    [Fact]
    public void DefinirExtensaoInexistenteNaoGravaNada()
    {
        var (ok, _) = FileAssociator.SetDefault(".tranal-nao-existe", @"C:\nao-existe\programa.exe");

        Assert.False(ok);
    }

    [Fact]
    public void DesfazerSemAlteracaoGuardadaNaoFazNada()
    {
        var (ok, _) = FileAssociator.Undo(".tranal-extensao-que-nunca-foi-variada");

        Assert.False(ok);
    }
}
