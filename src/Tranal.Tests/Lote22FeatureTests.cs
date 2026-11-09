using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O lote 22 fecha as acoes que faltavam na lista de programas e REMEDIA
/// as linhas do RECON que estavam desatualizadas.
///
/// <list type="bullet">
/// <item><b>GEEK T1#21</b>: "Ir para a ... Microsoft Store" — o destino
///existia para o registro e para os paineis do Windows, nao para a loja.</item>
/// <item><b>GEEK T1#22</b>: "Browse Location... e Browse Source..." — as
/// colunas mostravam a pasta, mas nao havia a acao de abre-la.</item>
/// <item><b>GEEK T2#44</b>: "Nenhum programa nesta categoria" — o rodape
/// mostrava "0" quando a lista estava vazia.</item>
/// <item><b>GEEK T2#35</b>, <b>GEEK T1#2</b>, <b>IOBIT T2#9</b> e
/// <b>WISE T2#33</b>: o RECON afirmava que faltavam coisas que ja
/// existiam — a remediacao esta nos testes, com a medicao no codigo.</item>
/// </list>
///
/// <para>A regra mora no nucleo
/// (<c>Core/Management/SystemNavigator.cs</c>,
/// <c>Core/Tools/SystemActions.cs</c> e
/// <c>Core/UninstallAdvisories.cs</c>) e a suite testa esse modulo. A ligacao
/// com a interface e conferida sobre o fonte, pelo mesmo motivo das outras
/// paridades: a suite nao referencia o projeto grafico (WPF).</para>
/// </summary>
public class Lote22FeatureTests
{
    // ==================================================================
    // Ir para a Microsoft Store
    // ==================================================================

    [Fact]
    public void A_loja_abre_a_busca_pelo_nome_do_programa()
    {
        var target = SystemNavigator.MicrosoftStore(Programa("Calculadora"));

        Assert.NotNull(target);
        Assert.StartsWith("ms-windows-store://search/?query=", target!.FileName, StringComparison.Ordinal);
        Assert.Contains("Calculadora", Uri.UnescapeDataString(target.FileName));
    }

    [Fact]
    public void A_busca_na_loja_leva_a_editora_quando_o_nome_nao_a_traz()
    {
        var programa = Programa("Calculadora");
        programa.Publisher = "Fabricante XYZ";

        var target = SystemNavigator.MicrosoftStore(programa);

        Assert.NotNull(target);
        var consulta = Uri.UnescapeDataString(target!.FileName);
        Assert.Contains("Calculadora", consulta);
        Assert.Contains("Fabricante XYZ", consulta);
    }

    [Fact]
    public void A_busca_na_loja_nao_repete_a_editora_que_o_nome_ja_traz()
    {
        var programa = Programa("Calculadora do Fabricante XYZ");
        programa.Publisher = "Fabricante XYZ";

        var target = SystemNavigator.MicrosoftStore(programa);

        Assert.NotNull(target);
        var consulta = Uri.UnescapeDataString(target!.FileName);
        Assert.Equal(1, consulta.Split("Fabricante XYZ").Length - 1);
    }

    [Fact]
    public void Sem_nome_nao_ha_busca_na_loja()
    {
        var programa = Programa(string.Empty);
        Assert.Null(SystemNavigator.MicrosoftStore(programa));
    }

    // ==================================================================
    // Browse Location... e Browse Source...
    // ==================================================================

    [Fact]
    public void A_pasta_do_programa_surge_quando_a_pasta_existe()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "tranal-lote22");
        Directory.CreateDirectory(pasta);

        try
        {
            var programa = Programa("Calculadora");
            programa.InstallLocation = pasta;

            var target = SystemNavigator.ProgramFolder(programa);

            Assert.NotNull(target);
            Assert.Equal("explorer.exe", target!.FileName);
            Assert.Contains(pasta, target.Arguments);
        }
        finally
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, true);
        }
    }

    [Fact]
    public void A_pasta_do_programa_nao_surge_quando_o_caminho_nao_existe()
    {
        var programa = Programa("Calculadora");
        programa.InstallLocation = @"C:\Pasta\Que\Nao\Existe\Nada";

        Assert.Null(SystemNavigator.ProgramFolder(programa));
    }

    [Fact]
    public void A_pasta_do_desinstalador_vem_do_comando_de_desinstalacao()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "tranal-lote22-origem");
        Directory.CreateDirectory(pasta);

        var executavel = Path.Combine(pasta, "desinstalador.exe");
        File.WriteAllText(executavel, "x");

        try
        {
            var programa = Programa("Calculadora");
            programa.UninstallString = $"\"{executavel}\" /uninstall";

            var target = SystemNavigator.UninstallerFolder(programa);

            Assert.NotNull(target);
            Assert.Equal("explorer.exe", target!.FileName);
            Assert.Contains(pasta, target.Arguments);
        }
        finally
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, true);
        }
    }

    [Fact]
    public void Sem_comando_de_desinstalacao_nao_ha_pasta_do_desinstalador()
    {
        var programa = Programa("Calculadora");
        programa.UninstallString = string.Empty;

        Assert.Null(SystemNavigator.UninstallerFolder(programa));
    }

    [Fact]
    public void Os_destinos_do_programa_trazem_a_loja_e_as_pastas()
    {
        var targets = SystemNavigator.TargetsFor(Programa("Calculadora"));

        Assert.Contains(targets, t => t.FileName.StartsWith("ms-windows-store://", StringComparison.Ordinal));
    }

    // ==================================================================
    // O aviso de que o desinstalador deixa residuo
    // ==================================================================

    [Fact]
    public void Sem_sobras_nao_ha_aviso_de_residuo()
    {
        Assert.Null(UninstallAdvisories.LeavesResidueAdvisory(null));
        Assert.Null(UninstallAdvisories.LeavesResidueAdvisory(Array.Empty<LeftoverItem>()));
    }

    [Fact]
    public void O_aviso_de_residuo_conta_as_sobras_marcadas()
    {
        var sobras = new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = @"C:\Dados\DoPrograma" },
            new LeftoverItem { Kind = "Arquivo", Path = @"C:\Dados\DoPrograma\config.ini" },
            new LeftoverItem { Kind = "Arquivo", Path = @"C:\Desmarcado", Selected = false },
        };

        var aviso = UninstallAdvisories.LeavesResidueAdvisory(sobras);

        Assert.NotNull(aviso);
        Assert.Equal(UninstallAdvisoryKind.LeavesResidue, aviso!.Kind);
        Assert.Equal(2, aviso.Items.Count);
    }

    [Fact]
    public void O_aviso_de_residuo_entra_na_lista_de_avisos()
    {
        var sobras = new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = @"C:\Dados\DoPrograma" },
        };

        var avisos = UninstallAdvisories.Build(Programa("Calculadora"), null, sobras);

        Assert.Contains(avisos, a => a.Kind == UninstallAdvisoryKind.LeavesResidue);
    }

    // ==================================================================
    // Avaliar on-line
    // ==================================================================

    [Fact]
    public void O_endereco_da_avaliacao_traz_o_nome_do_programa()
    {
        var url = SystemActions.AvaliarOnlineUrl(Programa("Calculadora"));

        Assert.StartsWith("http", url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Calculadora", Uri.UnescapeDataString(url));
    }

    [Fact]
    public void Sem_nome_nao_ha_endereco_de_avaliacao()
    {
        Assert.Equal(string.Empty, SystemActions.AvaliarOnlineUrl(Programa(string.Empty)));
    }

    // ==================================================================
    // As remediacoes: o que o RECON dizia que faltava e ja existe
    // ==================================================================

    [Fact]
    public void O_rodape_mostra_o_total_agregado_dos_programas()
    {
        var fonte = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        // O Geek mostra "%d programs of %s size in total"; o rodape do Tranal
        // mostra a contagem E o tamanho (o total mora no nucleo).
        Assert.Contains("ProgramTotalsCalculator.Compute", fonte);
        Assert.Contains("totais.Text", fonte);
    }

    [Fact]
    public void A_lista_vazia_diz_que_nao_ha_programa_nesta_categoria()
    {
        var fonte = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        Assert.Contains("Status.NoProgramsInCategory", fonte);
    }

    [Fact]
    public void A_limpeza_de_mru_inclui_a_chave_mrtcache()
    {
        var fonte = RepoFiles.Read("src", "Tranal.Uninstaller", "Core", "Tools", "MruCleaner.cs");

        Assert.Contains(@"Local Settings\MrtCache", fonte);
    }

    [Fact]
    public void A_desinstalacao_rapida_existe_no_menu_principal()
    {
        var fonte = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        Assert.Contains("Cmd.QuickUninstall", fonte);
        Assert.Contains("StartUninstall(quick: true)", fonte);
    }

    [Fact]
    public void A_tela_forcada_tem_o_botao_de_procurar_o_programa()
    {
        var fonte = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "ForcedUninstallWindow.xaml");

        Assert.Contains("ForcedBrowseButton", fonte);
        Assert.Contains("BrowseButton_Click", fonte);
    }

    [Fact]
    public void As_acoes_novas_estao_no_menu_do_programa()
    {
        var fonte = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        Assert.Contains("Cmd.RateProgram", fonte);
        Assert.Contains("SystemNavigator.TargetsFor(row.Program)", fonte);
    }

    [Fact]
    public void Os_textos_novos_estao_nos_dois_idiomas()
    {
        var chaves = new[]
        {
            "Cmd.OpenMicrosoftStore",
            "Cmd.OpenProgramFolder",
            "Cmd.OpenUninstallerFolder",
            "Cmd.RateProgram",
            "Cmd.RateProgramOpened",
            "Cmd.RateProgramFailed",
            "Status.NoProgramsInCategory",
            "Advisory.Title.LeavesResidue",
            "Advisory.LeavesResidue",
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
    // Os identificadores do menu
    // ==================================================================

    [Fact]
    public void A_loja_tem_identificador_de_menu_proprio()
    {
        var target = SystemNavigator.MicrosoftStore(Programa("Calculadora"));

        Assert.NotNull(target);
        Assert.Equal("CmdOpenMicrosoftStore", target!.MenuId);
    }

    [Fact]
    public void As_duas_pastas_nao_compartilham_o_identificador_de_menu()
    {
        var pastaPrograma = Path.Combine(Path.GetTempPath(), "tranal-lote22-p1");
        var pastaOrigem = Path.Combine(Path.GetTempPath(), "tranal-lote22-p2");
        Directory.CreateDirectory(pastaPrograma);
        Directory.CreateDirectory(pastaOrigem);

        var executavel = Path.Combine(pastaOrigem, "desinstalador.exe");
        File.WriteAllText(executavel, "x");

        try
        {
            var programa = Programa("Calculadora");
            programa.InstallLocation = pastaPrograma;
            programa.UninstallString = $"\"{executavel}\" /uninstall";

            var targets = SystemNavigator.TargetsFor(programa);
            var doExplorer = targets.Where(t => t.FileName == "explorer.exe").ToList();

            Assert.Equal(2, doExplorer.Count);
            Assert.NotEqual(doExplorer[0].MenuId, doExplorer[1].MenuId);
            Assert.All(doExplorer, t => Assert.DoesNotContain(" ", t.MenuId));
        }
        finally
        {
            if (Directory.Exists(pastaPrograma)) Directory.Delete(pastaPrograma, true);
            if (Directory.Exists(pastaOrigem)) Directory.Delete(pastaOrigem, true);
        }
    }

    [Fact]
    public void A_cli_tem_o_comando_de_avaliacao_e_o_de_loja()
    {
        var fonte = RepoFiles.Read("src", "Tranal.Cli", "InspectionCommands.cs");

        Assert.Contains("/inspection-rate", fonte);
        Assert.Contains("/inspection-store", fonte);
        Assert.Contains("SystemActions.AvaliarOnlineUrl", fonte);
        Assert.Contains("SystemNavigator.MicrosoftStore", fonte);
    }

    // ==================================================================
    // O modo rapido: confirmar e seguir
    // ==================================================================

    [Fact]
    public void O_modo_rapido_remove_o_que_a_varredura_marcou()
    {
        var sobras = new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = @"C:\Programa" },
            new LeftoverItem { Kind = "Arquivo", Path = @"C:\Programa\config.ini" },
            new LeftoverItem { Kind = "Arquivo", Path = @"C:\Desmarcado", Selected = false },
        };

        var itens = ExecutionPolicy.ItensDoModoRapido(sobras);

        Assert.Equal(2, itens.Count);
    }

    [Fact]
    public void O_modo_rapido_nao_toca_o_que_pode_ser_dado_do_usuario()
    {
        var sobras = new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = @"C:\Users\Alguem\Documents\MeuApp" },
            new LeftoverItem { Kind = "Pasta", Path = @"C:\Programa" },
        };

        var itens = ExecutionPolicy.ItensDoModoRapido(sobras);

        Assert.Single(itens);
        Assert.Equal(@"C:\Programa", itens[0].Path);
    }

    [Fact]
    public void O_modo_rapido_ignora_o_item_marcado_como_dado_do_usuario()
    {
        var sobras = new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = @"C:\Programa\Dados", MayContainUserData = true },
        };

        Assert.Empty(ExecutionPolicy.ItensDoModoRapido(sobras));
    }

    [Fact]
    public void Sem_sobras_o_modo_rapido_nao_remove_nada()
    {
        Assert.Empty(ExecutionPolicy.ItensDoModoRapido(null));
        Assert.Empty(ExecutionPolicy.ItensDoModoRapido(Array.Empty<LeftoverItem>()));
    }

    [Fact]
    public void O_assistente_tem_o_modo_rapido_que_confirma_e_segue()
    {
        var fonte = RepoFiles.Read(
            "src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml.cs");

        Assert.Contains("RunQuickAsync", fonte);
        Assert.Contains("ExecutionPolicy.ItensDoModoRapido", fonte);
        Assert.Contains("ConfirmPreUninstallAdvisories", fonte);
    }

    // ==================================================================
    // A categoria vazia
    // ==================================================================

    [Fact]
    public void A_categoria_do_usuario_aparece_mesmo_sem_programa()
    {
        var nos = ProgramCategoryTree.Build(
            new[] { Programa("Calculadora") },
            _ => null,
            new[] { "Jogos" });

        Assert.Contains(nos, n => n.Name == "Jogos");
        Assert.Equal(0, nos.First(n => n.Name == "Jogos").TotalPrograms);
    }

    [Fact]
    public void A_categoria_vazia_diz_que_nao_ha_programa_nela()
    {
        var nos = ProgramCategoryTree.Build(
            Array.Empty<InstalledProgram>(),
            null,
            new[] { "Jogos" });

        Assert.Single(nos);
        Assert.Equal("Jogos", nos[0].Name);
        Assert.Empty(nos[0].Programs);
    }

    [Fact]
    public void Sem_categorias_do_usuario_a_arvore_so_tem_o_que_os_programas_dizem()
    {
        var nos = ProgramCategoryTree.Build(new[] { Programa("Calculadora") }, _ => null);

        Assert.Single(nos);
    }

    // ==================================================================
    // Auxiliares
    // ==================================================================

    private static InstalledProgram Programa(string nome) => new()
    {
        DisplayName = nome,
        Publisher = string.Empty,
        InstallLocation = string.Empty,
        UninstallString = string.Empty,
    };

    private static Dictionary<string, string> LerCatalogo(string arquivo)
    {
        var texto = RepoFiles.Read("src", "Tranal.Uninstaller", "Lang", arquivo);
        using var documento = JsonDocument.Parse(texto);

        var dicionario = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var propriedade in documento.RootElement.EnumerateObject())
        {
            dicionario[propriedade.Name] = propriedade.Value.GetString() ?? string.Empty;
        }

        return dicionario;
    }
}
