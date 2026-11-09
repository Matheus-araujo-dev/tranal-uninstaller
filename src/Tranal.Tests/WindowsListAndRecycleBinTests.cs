using System;
using System.IO;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// A entrada do programa na lista do Windows e a consulta da Lixeira.
/// </summary>
public class WindowsListAndRecycleBinTests
{
    // ------------------------------------------------------------------
    // Entrada na lista do Windows
    // ------------------------------------------------------------------

    [Fact]
    public void UltimoSegmentoPegaONomeDaChave()
    {
        Assert.Equal(
            "{1234-5678}",
            WindowsListEntry.UltimoSegmento(@"HKEY_LOCAL_MACHINE\SOFTWARE\Uninstall\{1234-5678}"));

        Assert.Equal("Uninstall", WindowsListEntry.UltimoSegmento(@"HKEY_LOCAL_MACHINE\SOFTWARE\Uninstall"));
        Assert.Equal(string.Empty, WindowsListEntry.UltimoSegmento(null));
    }

    [Fact]
    public void SufixoEscondidoNaoEhGUID()
    {
        // O Windows so mostra a entrada quando o nome da chave parece um
        // identificador de produto: o sufixo quebra essa aparencia.
        var nome = WindowsListEntry.UltimoSegmento(@"HKEY_LOCAL_MACHINE\SOFTWARE\Uninstall\{1234}") +
                   WindowsListEntry.HiddenSuffix;

        Assert.False(Guid.TryParse(nome, out _));
    }

    [Fact]
    public void ListaDeEntradasEscondidasNaoQuebra()
    {
        var entradas = WindowsListEntry.List();

        Assert.NotNull(entradas);
        Assert.All(entradas, e => Assert.False(string.IsNullOrWhiteSpace(e.OriginalPath)));
    }

    [Fact]
    public void EsconderProgramaNuloNaoFazNada()
    {
        Assert.False(WindowsListEntry.Hide(null));
        Assert.False(WindowsListEntry.Remove(null));
        Assert.False(WindowsListEntry.Restore(null));
    }

    [Fact]
    public void ProgramaSemCaminhoDeRegistroNaoEEscondido()
    {
        var programa = new InstalledProgram { DisplayName = "Exemplo" };

        Assert.False(WindowsListEntry.Hide(programa));
    }

    // ------------------------------------------------------------------
    // Lixeira
    // ------------------------------------------------------------------

    [Fact]
    public void ListarLixeiraNaoQuebra()
    {
        var itens = RecycleBinInspector.List();

        Assert.NotNull(itens);
        Assert.All(itens, i => Assert.False(string.IsNullOrWhiteSpace(i.Name)));
    }

    [Fact]
    public void TotalDaLixeiraNaoEhNegativo()
    {
        Assert.True(RecycleBinInspector.TotalSize() >= 0);
    }

    [Fact]
    public void ItemDeLixeiraInexistenteENulo()
    {
        var item = RecycleBinInspector.ReadItem(Path.Combine(Path.GetTempPath(), "nao-existe-$I"));

        Assert.Null(item);
        Assert.Null(RecycleBinInspector.ReadItem(null));
    }
}
