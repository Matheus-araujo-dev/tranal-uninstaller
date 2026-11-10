using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O lote 23 fecha a tela de sobras em arvore (GEEK T1#11), a busca dentro
/// das sobras (TOTALUNINSTALL T2#11) e o "Browse..." das sobras (GEEK T1#15).
///
/// A regra do agrupamento mora no nucleo
/// (<c>Core/LeftoverGroups.cs</c>) e a suite testa esse modulo. A ligacao
/// com a interface e conferida sobre o fonte, pelo mesmo motivo das outras
/// paridades: a suite nao referencia o projeto grafico (WPF).
/// </summary>
public class Lote23FeatureTests
{
    // ==================================================================
    // O agrupamento por categoria
    // ==================================================================

    [Fact]
    public void As_sobras_vem_em_sistema_de_arquivos_e_registro()
    {
        var grupos = LeftoverGroups.Build(new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = @"C:\App" },
            new LeftoverItem { Kind = "Arquivo", Path = @"C:\App\x.ini" },
            new LeftoverItem { Kind = "Registro", Path = @"HKCU\Software\App" },
        });

        Assert.Equal(2, grupos.Count);

        Assert.Equal(LeftoverGroups.FileSystemKey, grupos[0].Key);
        Assert.Equal(2, grupos[0].Count);

        Assert.Equal(LeftoverGroups.RegistryKey, grupos[1].Key);
        Assert.Equal(1, grupos[1].Count);
    }

    [Fact]
    public void O_sistema_de_arquivos_vem_primeiro()
    {
        var grupos = LeftoverGroups.Build(new[]
        {
            new LeftoverItem { Kind = "Registro", Path = @"HKCU\Software\App" },
            new LeftoverItem { Kind = "Pasta", Path = @"C:\App" },
        });

        Assert.Equal(LeftoverGroups.FileSystemKey, grupos[0].Key);
        Assert.Equal(LeftoverGroups.RegistryKey, grupos[1].Key);
    }

    [Fact]
    public void Uma_categoria_desconhecida_aparece_com_o_nome_do_tipo()
    {
        var grupos = LeftoverGroups.Build(new[]
        {
            new LeftoverItem { Kind = "Atalho", Path = @"C:\App\App.lnk" },
        });

        Assert.Single(grupos);
        Assert.Equal("Atalho", grupos[0].Title);
        Assert.Equal(1, grupos[0].Count);
    }

    [Fact]
    public void A_categoria_sem_item_nao_aparece()
    {
        var grupos = LeftoverGroups.Build(new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = @"C:\App" },
        });

        Assert.Single(grupos);
        Assert.Equal(LeftoverGroups.FileSystemKey, grupos[0].Key);
    }

    [Fact]
    public void Sem_sobras_nao_ha_grupo()
    {
        Assert.Empty(LeftoverGroups.Build(null));
        Assert.Empty(LeftoverGroups.Build(Array.Empty<LeftoverItem>()));
    }

    [Fact]
    public void O_titulo_do_grupo_vem_do_catalogo()
    {
        var grupos = LeftoverGroups.Build(new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = @"C:\App" },
        });

        // O texto traduzido nao pode ser o nome cru da chave: a chave e o
        // que o codigo usa, o titulo e o que o usuario le.
        Assert.NotEqual("Leftovers.Group.FileSystem", grupos[0].Title);
        Assert.False(string.IsNullOrWhiteSpace(grupos[0].Title));
    }

    // ==================================================================
    // A busca dentro das sobras
    // ==================================================================

    [Fact]
    public void A_busca_filtra_pelo_caminho()
    {
        var filtrados = LeftoverGroups.Filter(new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = @"C:\Jogos\A" },
            new LeftoverItem { Kind = "Pasta", Path = @"C:\Programas\B" },
        }, "jogos");

        Assert.Single(filtrados);
        Assert.Equal(@"C:\Jogos\A", filtrados[0].Path);
    }

    [Fact]
    public void A_busca_olha_o_detalhe_e_o_tipo()
    {
        var sobras = new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = @"C:\A", Detail = "12 KB" },
            new LeftoverItem { Kind = "Registro", Path = @"HKCU\Software\B" },
        };

        Assert.Single(LeftoverGroups.Filter(sobras, "12 KB"));
        Assert.Single(LeftoverGroups.Filter(sobras, "registro"));
    }

    [Fact]
    public void A_busca_vazia_devolve_tudo()
    {
        var sobras = new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = @"C:\A" },
            new LeftoverItem { Kind = "Pasta", Path = @"C:\B" },
        };

        Assert.Equal(2, LeftoverGroups.Filter(sobras, string.Empty).Count);
        Assert.Equal(2, LeftoverGroups.Filter(sobras, "   ").Count);
        Assert.Equal(2, LeftoverGroups.Filter(sobras, null).Count);
    }

    [Fact]
    public void A_busca_que_nao_casa_nao_devolve_nada()
    {
        var filtrados = LeftoverGroups.Filter(new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = @"C:\A" },
        }, "nao-existe-xyz");

        Assert.Empty(filtrados);
    }

    [Fact]
    public void Sem_sobras_a_busca_nao_devolve_nada()
    {
        Assert.Empty(LeftoverGroups.Filter(null, "x"));
        Assert.Empty(LeftoverGroups.Filter(Array.Empty<LeftoverItem>(), "x"));
    }

    // ==================================================================
    // A ligacao com as telas
    // ==================================================================

    [Fact]
    public void O_assistente_mostra_a_arvore_das_sobras()
    {
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml");
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml.cs");

        Assert.Contains("LeftoversTreeControl", xaml);
        Assert.Contains("LeftoversTreeControl.SetItems", codigo);
        Assert.Contains("LeftoversTreeControl.Refresh", codigo);
    }

    [Fact]
    public void A_desinstalacao_forcada_mostra_a_mesma_arvore()
    {
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "ForcedUninstallWindow.xaml");
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "ForcedUninstallWindow.xaml.cs");

        Assert.Contains("LeftoversTreeControl", xaml);
        Assert.Contains("LeftoversTreeControl.SetItems", codigo);
    }

    [Fact]
    public void A_arvore_tem_a_busca_e_o_comando_de_explorar()
    {
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Shared", "LeftoversTree.xaml");
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Shared", "LeftoversTree.xaml.cs");

        Assert.Contains("LeftoversSearchBox", xaml);
        Assert.Contains("LeftoversBrowseButton", xaml);
        Assert.Contains("LeftoversTree", xaml);
        Assert.Contains("LeftoverGroups.Filter", codigo);
        Assert.Contains("LeftoverGroups.Build", codigo);
    }

    [Fact]
    public void Os_textos_novos_estao_nos_dois_idiomas()
    {
        var chaves = new[]
        {
            "Leftovers.Group.FileSystem",
            "Leftovers.Group.Registry",
            "Wizard.Step4.Search",
            "Wizard.Step4.Browse",
        };

        var pt = LerCatalogo("pt-BR.json");
        var en = LerCatalogo("en-US.json");

        foreach (var chave in chaves)
        {
            Assert.True(pt.ContainsKey(chave), $"chave ausente no pt-BR: {chave}");
            Assert.True(en.ContainsKey(chave), $"chave ausente no en-US: {chave}");

            Assert.False(string.IsNullOrWhiteSpace(pt[chave]));
            Assert.False(string.IsNullOrWhiteSpace(en[chave]));
        }
    }

    // ==================================================================
    // Auxiliares
    // ==================================================================

    private static Dictionary<string, string> LerCatalogo(string arquivo)
    {
        var texto = RepoFiles.Read("src", "Tranal.Uninstaller", "Lang", arquivo);
        using var documento = System.Text.Json.JsonDocument.Parse(texto);

        var dicionario = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var propriedade in documento.RootElement.EnumerateObject())
        {
            dicionario[propriedade.Name] = propriedade.Value.GetString() ?? string.Empty;
        }

        return dicionario;
    }
}
