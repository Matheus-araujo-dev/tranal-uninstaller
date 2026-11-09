using System;
using System.Diagnostics;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// As guardas de operação (o navegador aberto, a forçada antes da segura),
/// as ações que o Tranal abre no Windows e a afinidade de processador.
///
/// A REGRA é que o aviso nasce no núcleo, com o código, e a tradução decide
/// como ele se lê. Estes testes medem o código e o conteúdo do aviso — não o
/// texto, que é da tradução.
/// </summary>
public class GuardsAndActionsTests
{
    [Fact]
    public void ForcadaAntesDaSeguraAvisaSoQuandoFazSentido()
    {
        // Sem desinstalador registrado, a forçada é o único caminho: não há
        // plano A, e não há o que avisar.
        var semDesinstalador = new InstalledProgram { DisplayName = "Sem desinstalador" };
        Assert.Null(OperationGuards.AvisoDeForcadaAntesDaSegura(semDesinstalador, seguraJaTentada: false));
        Assert.Null(OperationGuards.AvisoDeForcadaAntesDaSegura(null, seguraJaTentada: false));

        // Com desinstalador registrado, o aviso existe.
        var comDesinstalador = new InstalledProgram
        {
            DisplayName = "Com desinstalador",
            UninstallString = @"C:\Program Files\App\unins000.exe",
        };

        var aviso = OperationGuards.AvisoDeForcadaAntesDaSegura(comDesinstalador, seguraJaTentada: false);

        Assert.NotNull(aviso);
        Assert.Equal(OperationGuards.CodeForcadaAntesDaSegura, aviso!.Code);
        Assert.False(aviso.Blocking);
        Assert.Contains("Com desinstalador", aviso.Message, StringComparison.Ordinal);

        // A segura já tentada cala o aviso.
        Assert.Null(OperationGuards.AvisoDeForcadaAntesDaSegura(comDesinstalador, seguraJaTentada: true));
    }

    [Fact]
    public void NavegadoresRodandoNaoQuebraEOnlynavegador()
    {
        var avisos = OperationGuards.NavegadoresRodando();

        Assert.NotNull(avisos);
        Assert.All(avisos, a => Assert.Equal(OperationGuards.CodeNavegadorRodando, a.Code));
        Assert.All(avisos, a => Assert.False(a.Blocking));
    }

    [Fact]
    public void AAfinidadedeProcessadorEExpandidaEmTexto()
    {
        Assert.Equal("(nenhum)", ProcessAffinity.Expandir(0));
        Assert.Equal("0-3", ProcessAffinity.Expandir(0b1111));
        Assert.Equal("0-1, 3", ProcessAffinity.Expandir(0b1011));
        Assert.Equal("0", ProcessAffinity.Expandir(1));
        Assert.Equal("8", ProcessAffinity.Expandir(1L << 8));

        Assert.Equal(0, ProcessAffinity.Contar(0));
        Assert.Equal(4, ProcessAffinity.Contar(0b1111));
        Assert.Equal(3, ProcessAffinity.Contar(0b1011));
    }

    [Fact]
    public void AAfinidadeDoProcessoAtualELeida()
    {
        using var atual = Process.GetCurrentProcess();

        var info = ProcessAffinity.Of(atual.Id, "teste");

        Assert.NotNull(info);
        Assert.Equal(atual.Id, info!.ProcessId);
        Assert.Equal("teste", info.ProcessName);
        Assert.True(info.Mask > 0);
        Assert.False(string.IsNullOrWhiteSpace(info.Text));
        Assert.Equal(Environment.ProcessorCount, info.ProcessorCount);
    }

    [Fact]
    public void AListaDeAfinidadesNaoQuebraComProcessoInexistente()
    {
        // Processo que nao existe: a lista ignora, em vez de derrubar.
        Assert.Null(ProcessAffinity.Of(999999));

        var lista = ProcessAffinity.List();
        Assert.NotNull(lista);
        Assert.All(lista, a => Assert.True(a.Mask > 0));
    }

    [Fact]
    public void APageDoFabricanteSoAbreComEnderecoDeclarado()
    {
        // Sem pagina declarada, o Tranal nao inventa um endereco.
        Assert.False(SystemActions.AbrirPaginaDoFabricante(null));
        Assert.False(SystemActions.AbrirPaginaDoFabricante(new InstalledProgram { DisplayName = "Sem pagina" }));
    }
}
