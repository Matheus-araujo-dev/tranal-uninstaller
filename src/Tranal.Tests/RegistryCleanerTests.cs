using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O Limpador de Registro por categoria, o desregistro de componentes COM, o
/// contador de referencia de itens compartilhados e a tag de protecao do
/// desinstalador.
///
/// Todas as regras testadas aqui foram medidas nesta maquina: o MuiCache
/// guarda o caminho no NOME do valor, a TypeLib guarda o arquivo em
/// "0\win32", as DLLs compartilhadas guardam o caminho no NOME do valor, e as
/// regras de firewall gravam "%SystemRoot%\..." no valor.
///
/// Os testes nao dependem do estado do registro da maquina: eles exercitam
/// as regras puras (extracao de caminho, contagem de referencia, decisao de
/// proteger) e verificam que a varredura real roda sem lancar.
/// </summary>
public class RegistryCleanerTests
{
    // ---------------------------------------------------------------
    // As categorias
    // ---------------------------------------------------------------

    [Fact]
    public void AsCategoriasSaoDezesseisComIdUnico()
    {
        Assert.Equal(16, RegistryCleaner.Categories.Count);
        Assert.Equal(
            RegistryCleaner.Categories.Count,
            RegistryCleaner.Categories.Select(c => c.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void CadaCategoriaTemNomeEDescricao()
    {
        foreach (var categoria in RegistryCleaner.Categories)
        {
            Assert.False(string.IsNullOrWhiteSpace(categoria.Id), "categoria sem id");
            Assert.False(string.IsNullOrWhiteSpace(categoria.Name), $"categoria '{categoria.Id}' sem nome");
            Assert.False(string.IsNullOrWhiteSpace(categoria.Description), $"categoria '{categoria.Id}' sem descricao");
        }
    }

    [Fact]
    public void OsIdsDasCategoriasSaoOsQueAInterfaceEACliConhecem()
    {
        var ids = RegistryCleaner.Categories.Select(c => c.Id).ToList();

        Assert.Contains(RegistryCleaner.CategoryActiveX, ids);
        Assert.Contains(RegistryCleaner.CategoryAppPaths, ids);
        Assert.Contains(RegistryCleaner.CategoryComClasses, ids);
        Assert.Contains(RegistryCleaner.CategoryFileTypes, ids);
        Assert.Contains(RegistryCleaner.CategoryFirewallRules, ids);
        Assert.Contains(RegistryCleaner.CategoryFonts, ids);
        Assert.Contains(RegistryCleaner.CategoryIfeo, ids);
        Assert.Contains(RegistryCleaner.CategoryInstallerProducts, ids);
        Assert.Contains(RegistryCleaner.CategoryMuiCache, ids);
        Assert.Contains(RegistryCleaner.CategoryOpenWith, ids);
        Assert.Contains(RegistryCleaner.CategoryProgIds, ids);
        Assert.Contains(RegistryCleaner.CategoryRunEntries, ids);
        Assert.Contains(RegistryCleaner.CategorySharedDlls, ids);
        Assert.Contains(RegistryCleaner.CategoryShellExtensions, ids);
        Assert.Contains(RegistryCleaner.CategoryTypeLib, ids);
        Assert.Contains(RegistryCleaner.CategoryUninstallEntries, ids);
    }

    // ---------------------------------------------------------------
    // A varredura
    // ---------------------------------------------------------------

    [Fact]
    public void AVarreduraDeUmaCategoriaNaoLanca()
    {
        var entradas = RegistryCleaner.Scan(new[] { RegistryCleaner.CategoryMuiCache });

        Assert.NotNull(entradas);
        Assert.All(entradas, e => Assert.Equal(RegistryCleaner.CategoryMuiCache, e.CategoryId));
    }

    [Fact]
    public void AVarreduraDeTodasAsCategoriasNaoLanca()
    {
        var entradas = RegistryCleaner.Scan();

        Assert.NotNull(entradas);
    }

    [Fact]
    public void TodaEntradaTemCaminhoECategoriaConhecida()
    {
        var ids = RegistryCleaner.Categories.Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entrada in RegistryCleaner.Scan())
        {
            Assert.False(string.IsNullOrWhiteSpace(entrada.Path), "entrada sem caminho");
            Assert.Contains(entrada.CategoryId, ids);
            Assert.False(string.IsNullOrWhiteSpace(entrada.CategoryName), "entrada sem nome de categoria");
        }
    }

    [Fact]
    public void UmaEntradaComValorTemONomeDoValorEOValorPadraoEChave()
    {
        var entradas = RegistryCleaner.Scan(new[] { RegistryCleaner.CategoryMuiCache });

        foreach (var entrada in entradas.Where(e => e.IsValue))
        {
            Assert.False(string.IsNullOrWhiteSpace(entrada.ValueName), "entrada de valor sem nome de valor");
            Assert.StartsWith("HKEY_", entrada.Path, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void RemoverUmaListaVaziaNaoFazNada()
    {
        var relatorio = RegistryCleaner.Remove(Array.Empty<RegistryCleanerEntry>());

        Assert.Equal(0, relatorio.Total);
        Assert.Equal(0, relatorio.RemovedCount);
        Assert.Equal(0, relatorio.FailedCount);
    }

    // ---------------------------------------------------------------
    // MuiCache: o caminho esta no NOME do valor
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(@"C:\Program Files\App\app.exe.FriendlyAppName", @"C:\Program Files\App\app.exe")]
    [InlineData(@"C:\Program Files\App\app.exe.ApplicationCompany", @"C:\Program Files\App\app.exe")]
    [InlineData(@"C:\Program Files\App\app.exe,-12345", @"C:\Program Files\App\app.exe")]
    [InlineData("C:\\App\\app.exe", "C:\\App\\app.exe")]
    [InlineData("C:/App/app.exe", "C:/App/app.exe")]
    public void OMuiCacheTiraOSufixoDoNomeDoValor(string nome, string esperado)
    {
        Assert.Equal(esperado, MuiCacheLeftovers.ExtrairCaminho(nome));
    }

    [Theory]
    [InlineData("LangID")]
    [InlineData("SteelSeries A/S")]
    [InlineData("")]
    [InlineData(null)]
    public void OMuiCacheIgnoraNomeQueNaoEUmCaminho(string? nome)
    {
        Assert.Equal(string.Empty, MuiCacheLeftovers.ExtrairCaminho(nome));
    }

    [Fact]
    public void AListagemDoMuiCacheNaoLanca()
    {
        var entradas = MuiCacheLeftovers.List();

        Assert.NotNull(entradas);
        Assert.All(entradas, e => Assert.True(e.Path.Length > 0));
    }

    // ---------------------------------------------------------------
    // TypeLib: o arquivo esta em "0\win32", e a descricao nao e caminho
    // ---------------------------------------------------------------

    [Fact]
    public void AListagemDeTypeLibNaoConfundeDescricaoComCaminho()
    {
        foreach (var entrada in TypeLibLeftovers.List())
        {
            // A descricao da biblioteca ("OLE Automation") e um texto, e nao
            // um caminho: quando o valor default da chave de versao era lido
            // como caminho, as 428 bibliotecas do Windows viravam sobra.
            Assert.False(entrada.Path.Contains("Library", StringComparison.OrdinalIgnoreCase));
            Assert.True(entrada.Path.Contains('\\'), $"caminho de TypeLib sem barra: {entrada.Path}");
        }
    }

    [Fact]
    public void AListagemDeTypeLibNaoLanca()
    {
        Assert.NotNull(TypeLibLeftovers.List());
    }

    // ---------------------------------------------------------------
    // Desregistro de componentes COM
    // ---------------------------------------------------------------

    [Fact]
    public void DesregistrarUmaDllInexistenteFalhaComMensagem()
    {
        var resultado = ComUnregister.Unregister(@"C:\Caminho\Que\Nao\Existe.dll");

        Assert.False(resultado.Success);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Message));
    }

    [Fact]
    public void DesregistrarSemCaminhoFalha()
    {
        var resultado = ComUnregister.Unregister("   ");

        Assert.False(resultado.Success);
    }

    [Fact]
    public void UmaPastaSemComponentesRegistradosNaoTemDll()
    {
        var dlls = ComUnregister.RegisteredDlls(@"C:\Caminho\Que\Nao\Existe");

        Assert.Empty(dlls);
    }

    [Fact]
    public void DesregistrarVariasDllsDevolveUmResultadoPorDll()
    {
        var resultados = ComUnregister.UnregisterAll(new[]
        {
            @"C:\Um.dll",
            @"C:\Dois.dll",
        });

        Assert.Equal(2, resultados.Count);
        Assert.All(resultados, r => Assert.False(r.Value.Success));
    }

    // ---------------------------------------------------------------
    // Contador de referencia de itens compartilhados
    // ---------------------------------------------------------------

    [Fact]
    public void UmCaminhoQueNaoEUsadoPorNenhumProgramaEOrfao()
    {
        var analise = SharedReferenceCounter.Analyze(@"C:\Pasta\De\Teste", new[]
        {
            new InstalledProgram { DisplayName = "Outro", InstallLocation = @"C:\Program Files\Outro" },
        });

        Assert.True(analise.IsOrphan);
        Assert.False(analise.IsShared);
        Assert.Equal(0, analise.ReferenceCount);
    }

    [Fact]
    public void UmCaminhoUsadoPorDoisProgramasECompartilhado()
    {
        var analise = SharedReferenceCounter.Analyze(@"C:\Program Files\Common Files\Shared", new[]
        {
            new InstalledProgram { DisplayName = "Alpha", InstallLocation = @"C:\Program Files\Common Files\Shared" },
            new InstalledProgram { DisplayName = "Beta", InstallLocation = @"C:\Program Files\Common Files\Shared" },
        });

        Assert.True(analise.IsShared);
        Assert.False(analise.IsOrphan);
        Assert.Equal(2, analise.ReferenceCount);
        Assert.Contains("Alpha", analise.Owners);
        Assert.Contains("Beta", analise.Owners);
    }

    [Fact]
    public void ODesinstaladorDoProgramaTambemContaComoReferencia()
    {
        var analise = SharedReferenceCounter.Analyze(@"C:\Program Files\Ferramenta", new[]
        {
            new InstalledProgram
            {
                DisplayName = "Ferramenta",
                InstallLocation = @"C:\Program Files\Ferramenta",
                UninstallString = @"""C:\Program Files\Ferramenta\desinstalar.exe"" /S",
            },
        });

        Assert.Equal(1, analise.ReferenceCount);
        Assert.Equal("Ferramenta", analise.Owners.Single());
    }

    [Fact]
    public void AListaDeCompartilhadosFiltraPeloContador()
    {
        var caminhos = new[] { @"C:\Pasta\Compartilhada", @"C:\Pasta\Sozinha" };

        var compartilhados = SharedReferenceCounter.Shared(caminhos, new[]
        {
            new InstalledProgram { DisplayName = "A", InstallLocation = @"C:\Pasta\Compartilhada" },
            new InstalledProgram { DisplayName = "B", InstallLocation = @"C:\Pasta\Compartilhada" },
        });

        Assert.Single(compartilhados);
        Assert.Equal(@"C:\Pasta\Compartilhada", compartilhados[0].Path);
    }

    // ---------------------------------------------------------------
    // A tag de protecao do desinstalador
    // ---------------------------------------------------------------

    [Fact]
    public void UmProgramaSemChaveDeRegistroNaoEstaProtegido()
    {
        Assert.False(ProtectedUninstallTag.IsProtected(new InstalledProgram { DisplayName = "Sem chave" }));
        Assert.False(ProtectedUninstallTag.IsProtected(null));
    }

    [Fact]
    public void DesligarAProtecaoDeUmProgramaInexistenteFalha()
    {
        var resultado = ProtectedUninstallTag.Clear(new InstalledProgram { DisplayName = "Sem chave" });

        Assert.False(resultado.Success);
    }

    [Fact]
    public void AListaDeProtegidosAceitaListaVazia()
    {
        Assert.Empty(ProtectedUninstallTag.ListProtected(Array.Empty<InstalledProgram>()));
        Assert.Empty(ProtectedUninstallTag.ListProtected(null));
    }

    // ---------------------------------------------------------------
    // Onde o Windows procura um executavel
    // ---------------------------------------------------------------

    [Fact]
    public void OLocalizadorAchaOExecutavelDoSistema()
    {
        var caminho = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "cmd.exe");

        if (!File.Exists(caminho)) return; // maquina sem o cmd.exe: nada a provar

        Assert.True(ExecutableLocator.Exists(caminho));
    }

    [Fact]
    public void OLocalizadorAceitaOCaminhoSemExtensao()
    {
        var caminho = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "cmd");

        if (!File.Exists(caminho + ".exe")) return; // maquina sem o cmd.exe

        Assert.True(ExecutableLocator.Exists(caminho));
    }

    [Fact]
    public void OLocalizadorNegaUmCaminhoQueNaoExiste()
    {
        Assert.False(ExecutableLocator.Exists(@"C:\Caminho\Que\Nao\Existe\programa.exe"));
        Assert.False(ExecutableLocator.Exists(""));
        Assert.False(ExecutableLocator.Exists(null));
    }

    [Fact]
    public void OLocalizadorNegaUmNomeQueNaoExiste()
    {
        Assert.False(ExecutableLocator.Exists("programa-que-nao-existe-987654.exe"));
    }
}
