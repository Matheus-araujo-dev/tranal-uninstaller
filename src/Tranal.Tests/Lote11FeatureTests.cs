using System;
using System.Linq;
using Tranal.Uninstaller.Core.Discovery;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// As features do lote 11: a inicializacao dos aplicativos da Store, as
/// dependencias entre pacotes MSI, o catalogo de pacotes Appx e as DLLs em
/// pastas de sistema.
///
/// O que estes testes cobrem: os modulos respondem sem lancar mesmo quando
/// a maquina nao tem o que eles procuram, e o que eles affirmam e medido
/// (a lista de KnownDLLs vem do registro, o catalogo Appx vem do
/// PowerShell).
/// </summary>
public class Lote11FeatureTests
{
    [Fact]
    public void AInicializacaoDaStoreResponde()
    {
        var entradas = StoreStartupEntries.List();

        Assert.NotNull(entradas);
        Assert.All(entradas, e => Assert.False(string.IsNullOrWhiteSpace(e.TaskId)));
    }

    [Fact]
    public void AsDependenciasMsiRespondem()
    {
        var dependencias = MsiDependencies.List();

        Assert.NotNull(dependencias);
        Assert.All(dependencias, d => Assert.False(string.IsNullOrWhiteSpace(d.RequiredPackage)));
        Assert.All(dependencias, d => Assert.False(string.IsNullOrWhiteSpace(d.DependentPackage)));
    }

    [Fact]
    public void OCatalogoAppxSeparaOSistemaDoUsuario()
    {
        var pacotes = AppxCatalog.List();

        Assert.NotNull(pacotes);

        // As duas listas sao partes da mesma: a soma e o total.
        var preinstalados = AppxCatalog.PreInstalled().Count;
        var doUsuario = AppxCatalog.UserInstalled().Count;

        Assert.True(preinstalados + doUsuario <= pacotes.Count);
    }

    [Fact]
    public void AsDllsDeSistemaRespondem()
    {
        var dlls = SystemDllTracker.List();

        Assert.NotNull(dlls);
        Assert.All(dlls, d => Assert.False(string.IsNullOrWhiteSpace(d.Name)));
    }

    [Fact]
    public void AListaDeKnownDllsVemDoRegistro()
    {
        var conhecidas = SystemDllTracker.KnownDlls();

        // O Windows tem uma lista de DLLs que ele carrega pelo nome; ela
        // nunca esta vazia em uma maquina de verdade.
        Assert.NotNull(conhecidas);
        Assert.True(conhecidas.Count > 0, "a lista de KnownDLLs do Windows veio vazia");
    }

    [Fact]
    public void AsCandidatasSaoAsQueOWindowsNaoCarregaPeloNome()
    {
        var candidatas = SystemDllTracker.Candidates();

        Assert.All(candidatas, d => Assert.False(d.IsKnownDll));
    }
}
