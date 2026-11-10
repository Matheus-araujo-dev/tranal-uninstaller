using System.Collections.Generic;
using System.Linq;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Reporting;
using Tranal.Uninstaller.Core.Tracing;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O lote 27 fecha a arvore de alteracoes detectadas (TOTALUNINSTALL T2#22)
/// e a busca dentro dela (T2#11).
///
/// A comparacao ja existia e era testada; o que faltava era a **apresentacao
/// em arvore** e a **busca** dentro dela. A comparacao aparecia como texto
/// numa caixa, e quem quuisse saber o que mudou precisava ler.
///
/// A REGRA mora no nucleo (`Core/Reporting/ChangeTreeNodes.cs`, e a
/// classificacao em `Core/Reporting/ChangeTreeReport.cs`) e a suite testa
/// esses modulos. A ligacao com a interface e conferida sobre o fonte, pelo
/// mesmo motivo das outras paridades: a suite nao referencia o projeto
/// grafico (WPF).
/// </summary>
public class Lote27FeatureTests
{
    /// <summary>
    /// Um retrato com arquivos.
    /// </summary>
    private static SnapshotState Retrato(params (string Caminho, long Tamanho)[] arquivos) => new()
    {
        Files = arquivos
            .Select(a => new SnapshotFileState { Path = a.Caminho, SizeBytes = a.Tamanho })
            .ToList(),
    };

    /// <summary>
    /// Um retrato com chaves e valores de registro.
    /// </summary>
    private static SnapshotState RetratoComRegistro(
        IReadOnlyList<string> chaves,
        IReadOnlyList<(string Chave, string Nome, string Valor)> valores) => new()
    {
        RegistryKeys = chaves,
        RegistryValues = valores
            .Select(v => new SnapshotRegistryValueState { Key = v.Chave, ValueName = v.Nome, Value = v.Valor })
            .ToList(),
    };

    /// <summary>
    /// Uma comparacao com os quatro tipos de item: arquivo, pasta, chave de
    /// registro e valor de registro.
    /// </summary>
    private static SnapshotComparison ComparacaoCompleta()
    {
        var antes = new SnapshotState
        {
            Files = new List<SnapshotFileState> { new() { Path = "C:\\antes.txt", SizeBytes = 10 } },
            Folders = new List<SnapshotFileState> { new() { Path = "C:\\Pasta Velha", IsDirectory = true } },
            RegistryKeys = new List<string> { "HKCU\\Software\\Velha" },
            RegistryValues = new List<SnapshotRegistryValueState>
            {
                new() { Key = "HKCU\\Software\\Velha", ValueName = "Versao", Value = "1.0" },
            },
        };

        var depois = new SnapshotState
        {
            Files = new List<SnapshotFileState> { new() { Path = "C:\\depois.txt", SizeBytes = 20 } },
            Folders = new List<SnapshotFileState> { new() { Path = "C:\\Pasta Nova", IsDirectory = true } },
            RegistryKeys = new List<string> { "HKCU\\Software\\Nova" },
            RegistryValues = new List<SnapshotRegistryValueState>
            {
                new() { Key = "HKCU\\Software\\Nova", ValueName = "Valor", Value = "x" },
                new() { Key = "HKCU\\Software\\Velha", ValueName = "Versao", Value = "2.0" },
            },
        };

        return SnapshotComparer.Compare(antes, depois);
    }

    // ==================================================================
    // A arvore
    // ==================================================================

    [Fact]
    public void A_arvore_tem_as_quatro_categorias()
    {
        var nos = ChangeTreeNodes.Build(ComparacaoCompleta());

        Assert.Equal(4, nos.Count);
        Assert.Equal(
            new[] { ChangeTreeCategory.Files, ChangeTreeCategory.Folders, ChangeTreeCategory.Registry, ChangeTreeCategory.Values },
            nos.Select(n => n.Category!.Value).ToArray());
    }

    [Fact]
    public void A_arvore_agrupa_por_mudanca()
    {
        var nos = ChangeTreeNodes.Build(ComparacaoCompleta());

        var grupos = nos.SelectMany(n => n.Children).Select(g => g.Change!.Value).Distinct().ToList();

        Assert.Contains(ChangeTreeChange.Added, grupos);
        Assert.Contains(ChangeTreeChange.Modified, grupos);
        Assert.Contains(ChangeTreeChange.Removed, grupos);
    }

    [Fact]
    public void A_arvore_e_a_tabela_sao_a_mesma_coisa()
    {
        // A arvore e a tabela sao as mesmas alteracoes em duas formas. Se a
        // contagem divergisse, o que a pessoa le na tela deixaria de ser o
        // que sai no arquivo exportado.
        var comparacao = ComparacaoCompleta();

        var linhas = ChangeTreeReport.Rows(comparacao);
        var nos = ChangeTreeNodes.Build(comparacao);

        Assert.Equal(linhas.Count, ChangeTreeNodes.Total(nos));
    }

    [Fact]
    public void A_arvore_tem_o_mesmo_conteudo_da_tabela()
    {
        var comparacao = ComparacaoCompleta();

        var linhas = ChangeTreeReport.Rows(comparacao);
        var itens = ChangeTreeReport.Items(comparacao);

        Assert.Equal(linhas.Count, itens.Count);

        for (var i = 0; i < linhas.Count; i++)
        {
            Assert.Equal(itens[i].Kind, linhas[i][0]);
            Assert.Equal(itens[i].ChangeText, linhas[i][1]);
            Assert.Equal(itens[i].Item, linhas[i][2]);
            Assert.Equal(itens[i].Detail, linhas[i][3]);
        }
    }

    [Fact]
    public void O_cabecalho_do_no_mostra_a_contagem()
    {
        var nos = ChangeTreeNodes.Build(ComparacaoCompleta());

        var arquivos = nos.First(n => n.Category == ChangeTreeCategory.Files);
        Assert.Equal(2, arquivos.Count);
        Assert.Equal("files", arquivos.LabelKey);
    }

    [Fact]
    public void A_arvore_vazia_nao_tem_no()
    {
        var comparacao = SnapshotComparer.Compare(SnapshotState.Empty, SnapshotState.Empty);

        Assert.Empty(ChangeTreeNodes.Build(comparacao));
    }

    // ==================================================================
    // A busca
    // ==================================================================

    [Fact]
    public void A_busca_filtra_dentro_da_arvore()
    {
        var nos = ChangeTreeNodes.Build(ComparacaoCompleta());

        var filtrados = ChangeTreeNodes.Filter(nos, "depois");

        Assert.Equal(1, ChangeTreeNodes.Total(filtrados));
        Assert.Contains("depois.txt", filtrados[0].Children[0].Children[0].Item);
    }

    [Fact]
    public void A_busca_preserva_a_hierarquia()
    {
        // Um no so continua na arvore quando ele tem um filho que casa: sem
        // isso, a busca acharia o item e esconderia o caminho dele.
        var nos = ChangeTreeNodes.Build(ComparacaoCompleta());

        var filtrados = ChangeTreeNodes.Filter(nos, "depois");

        Assert.Single(filtrados);
        Assert.Equal(ChangeTreeCategory.Files, filtrados[0].Category);
        Assert.Single(filtrados[0].Children);
    }

    [Fact]
    public void A_busca_vazia_mostra_tudo()
    {
        var nos = ChangeTreeNodes.Build(ComparacaoCompleta());

        Assert.Equal(ChangeTreeNodes.Total(nos), ChangeTreeNodes.Total(ChangeTreeNodes.Filter(nos, "   ")));
        Assert.Equal(ChangeTreeNodes.Total(nos), ChangeTreeNodes.Total(ChangeTreeNodes.Filter(nos, null)));
    }

    [Fact]
    public void A_busca_que_nao_acha_nada_esvazia_a_arvore()
    {
        var nos = ChangeTreeNodes.Build(ComparacaoCompleta());

        var filtrados = ChangeTreeNodes.Filter(nos, "nao-existe-nada");

        Assert.Empty(filtrados);
        Assert.Equal(0, ChangeTreeNodes.Total(filtrados));
    }

    [Fact]
    public void A_busca_acha_pelo_detalhe_tambem()
    {
        // O detalhe (o valor antigo e o novo de um valor de registro) faz
        // parte da busca: e por ele que se acha a mudanca.
        var nos = ChangeTreeNodes.Build(ComparacaoCompleta());

        var filtrados = ChangeTreeNodes.Filter(nos, "2.0");

        Assert.Equal(1, ChangeTreeNodes.Total(filtrados));
    }

    [Fact]
    public void A_busca_ignora_as_maiusculas()
    {
        var nos = ChangeTreeNodes.Build(ComparacaoCompleta());

        Assert.Equal(1, ChangeTreeNodes.Total(ChangeTreeNodes.Filter(nos, "DEPOIS")));
    }

    // ==================================================================
    // O texto da arvore
    // ==================================================================

    [Fact]
    public void A_arvore_em_texto_mostra_a_hierarquia()
    {
        var nos = ChangeTreeNodes.Build(ComparacaoCompleta());

        var texto = ChangeTreeNodes.Describe(nos, no => no.LabelKey.Length == 0 ? no.Item : no.LabelKey + " (" + no.Count + ")");

        Assert.Contains("files (2)", texto);
        Assert.Contains("  added (1)", texto);
        Assert.Contains("    C:\\depois.txt", texto);
    }

    // ==================================================================
    // Os rotulos e a ligacao com a interface
    // ==================================================================

    [Fact]
    public void Os_rotulos_da_arvore_estao_nos_dois_idiomas()
    {
        foreach (var idioma in new[] { "pt-BR", "en-US" })
        {
            var catalogo = Loc.Load(idioma);

            foreach (var chave in new[]
            {
                "ChangeTree.Category.Files",
                "ChangeTree.Category.Folders",
                "ChangeTree.Category.Registry",
                "ChangeTree.Category.Values",
                "ChangeTree.Change.Added",
                "ChangeTree.Change.Modified",
                "ChangeTree.Change.Removed",
                "ChangeTree.Search",
                "ChangeTree.SearchHint",
            })
            {
                Assert.True(catalogo.ContainsKey(chave), $"{idioma} sem a chave {chave}");
                Assert.False(string.IsNullOrWhiteSpace(catalogo[chave]), $"a chave {chave} esta vazia em {idioma}");
            }
        }
    }

    [Fact]
    public void O_rotulo_do_no_vem_do_catalogo()
    {
        var nos = ChangeTreeNodes.Build(ComparacaoCompleta());

        var arquivos = nos.First(n => n.Category == ChangeTreeCategory.Files);
        Assert.Equal(Loc.Get("ChangeTree.Category.Files"), ChangeTreeLabels.Label(arquivos));

        var folha = arquivos.Children[0].Children[0];
        Assert.Equal(folha.Item, ChangeTreeLabels.Label(folha));
    }

    [Fact]
    public void A_interface_tem_a_arvore_e_a_busca()
    {
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Tracing", "TracingWindow.xaml");
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Tracing", "TracingWindow.xaml.cs");

        Assert.Contains("ChangeTreeView", xaml);
        Assert.Contains("ChangeTreeSearchBox", xaml);
        Assert.Contains("TracingChangeTree", xaml);

        Assert.Contains("ChangeTreeNodes.Build", codigo);
        Assert.Contains("ChangeTreeNodes.Filter", codigo);
    }

    [Fact]
    public void A_cli_tem_a_arvore_e_a_busca()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Cli", "ExtraCommands.cs");

        Assert.Contains("/busca", codigo);
        Assert.Contains("ChangeTreeNodes.Build", codigo);
        Assert.Contains("ChangeTreeNodes.Describe", codigo);
    }
}
