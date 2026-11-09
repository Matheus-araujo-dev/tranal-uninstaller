using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Reporting;
using Tranal.Uninstaller.Core.Tracing;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// A arvore de alteracoes: exportar e imprimir o que foi detectado.
///
/// A comparacao entre dois snapshots ja existia e era testada. O que nao
/// existia era o destino dela: a comparacao aparecia como texto numa caixa
/// e morria ali. Estes testes cobrem a arvore — as linhas, a exportacao nos
/// tres formatos, o layout de impressao e a ligacao com a interface.
///
/// A suite nao referencia o projeto WPF: a verificacao da tela e feita
/// sobre o fonte em disco.
/// </summary>
public class Lote19FeatureTests
{
    private static SnapshotState Retrato(
        params (string Caminho, long Tamanho)[] arquivos)
    {
        return new SnapshotState
        {
            Files = arquivos
                .Select(a => new SnapshotFileState { Path = a.Caminho, SizeBytes = a.Tamanho })
                .ToList(),
        };
    }

    private static SnapshotState RetratoComRegistro(
        IReadOnlyList<string> chaves,
        IReadOnlyList<(string Chave, string Nome, string Valor)> valores)
    {
        return new SnapshotState
        {
            RegistryKeys = chaves,
            RegistryValues = valores
                .Select(v => new SnapshotRegistryValueState { Key = v.Chave, ValueName = v.Nome, Value = v.Valor })
                .ToList(),
        };
    }

    // ------------------------------------------------------------------
    // A tabela: a regra de impressao mora em um lugar so
    // ------------------------------------------------------------------

    [Fact]
    public void AImpressaoDaListaEDaArvoreUsamOMesmoNucleo()
    {
        // As duas sao tabelas: quem quebra a pagina e o mesmo codigo.
        var lista = PrintLayout.Build(new List<InstalledProgram>()).Table;
        var arvore = ChangeTreeReport.Table(null, "antes", "depois");

        Assert.IsType<PrintTable>(lista);
        Assert.IsType<PrintTable>(arvore);
    }

    [Fact]
    public void ACabecalhoDasColunasRepeteEmCadaPagina()
    {
        var linhas = Enumerable.Range(1, 25)
            .Select(i => (IReadOnlyList<string>)new[] { "Arquivo", "Novo", $"C:\\arquivo{i}.txt", "1 KB" })
            .ToList();

        var tabela = PrintTable.Build("Teste", "cabecalho", new[] { "Tipo", "Mudanca", "Item", "Detalhe" }, linhas, rowsPerPage: 10);

        Assert.Equal(3, tabela.TotalPages);
        Assert.All(tabela.Pages, pagina => Assert.StartsWith("Tipo", pagina.Rows[0]));
    }

    [Fact]
    public void ODocumentoVazioTemUmaPaginaComOCabecalho()
    {
        var tabela = PrintTable.Build("Teste", "cabecalho", new[] { "Tipo", "Item" }, new List<IReadOnlyList<string>>());

        Assert.Equal(1, tabela.TotalPages);
        Assert.Single(tabela.Pages[0].Rows);
        Assert.Equal(0, tabela.TotalRows);
    }

    [Fact]
    public void ARodapeDaPaginaDizQualPaginaE()
    {
        var linhas = Enumerable.Range(1, 12)
            .Select(i => (IReadOnlyList<string>)new[] { "Arquivo", $"C:\\a{i}" })
            .ToList();

        var tabela = PrintTable.Build("Teste", "cabecalho", new[] { "Tipo", "Item" }, linhas, rowsPerPage: 5);

        Assert.Equal(3, tabela.TotalPages);
        Assert.Contains("1", tabela.Pages[0].Footer);
        Assert.Contains("3", tabela.Pages[2].Footer);
    }

    // ------------------------------------------------------------------
    // A arvore: o que aparece
    // ------------------------------------------------------------------

    [Fact]
    public void AArvoreMostraOsArquivosNovosERemovidos()
    {
        var antes = Retrato(("C:\\antes.txt", 10));
        var depois = Retrato(("C:\\depois.txt", 20));

        var comparacao = SnapshotComparer.Compare(antes, depois);
        var linhas = ChangeTreeReport.Rows(comparacao);

        Assert.Contains(linhas, l => l[0] == "Arquivo" && l[1] == "Novo" && l[2] == "C:\\depois.txt");
        Assert.Contains(linhas, l => l[0] == "Arquivo" && l[1] == "Removido" && l[2] == "C:\\antes.txt");
    }

    [Fact]
    public void AArvoreMostraOValorAntigoEONovoDoRegistro()
    {
        var antes = RetratoComRegistro(
            new[] { "HKCU\\Software\\Teste" },
            new[] { ("HKCU\\Software\\Teste", "Versao", "1.0") });

        var depois = RetratoComRegistro(
            new[] { "HKCU\\Software\\Teste" },
            new[] { ("HKCU\\Software\\Teste", "Versao", "2.0") });

        var comparacao = SnapshotComparer.Compare(antes, depois);
        var linhas = ChangeTreeReport.Rows(comparacao);

        var alterado = linhas.First(l => l[1] == "Modificado");
        Assert.Equal("Valor", alterado[0]);
        Assert.Contains("HKCU\\Software\\Teste", alterado[2]);
        Assert.Contains("1.0", alterado[3]);
        Assert.Contains("2.0", alterado[3]);
    }

    [Fact]
    public void AArvoreMostraAsPastasEOsValores()
    {
        var antes = RetratoComRegistro(Array.Empty<string>(), Array.Empty<(string, string, string)>());
        var depois = new SnapshotState
        {
            Folders = new List<SnapshotFileState> { new() { Path = "C:\\Pasta Nova", IsDirectory = true } },
            RegistryKeys = new List<string> { "HKCU\\Software\\Novo" },
            RegistryValues = new List<SnapshotRegistryValueState>
            {
                new() { Key = "HKCU\\Software\\Novo", ValueName = "Valor", Value = "x" },
            },
        };

        var linhas = ChangeTreeReport.Rows(SnapshotComparer.Compare(antes, depois));

        Assert.Contains(linhas, l => l[0] == "Pasta" && l[1] == "Nova" && l[2] == "C:\\Pasta Nova");
        Assert.Contains(linhas, l => l[0] == "Registro" && l[1] == "Nova chave" && l[2] == "HKCU\\Software\\Novo");
        Assert.Contains(linhas, l => l[0] == "Valor" && l[1] == "Novo");
    }

    [Fact]
    public void AArvoreVaziaNaoTemLinha()
    {
        var comparacao = SnapshotComparer.Compare(SnapshotState.Empty, SnapshotState.Empty);

        Assert.Empty(ChangeTreeReport.Rows(comparacao));
    }

    // ------------------------------------------------------------------
    // A exportacao
    // ------------------------------------------------------------------

    [Fact]
    public void AArvoreExportaEmTextoCsvEHtml()
    {
        var antes = Retrato(("C:\\antes.txt", 10));
        var depois = Retrato(("C:\\depois.txt", 20));
        var comparacao = SnapshotComparer.Compare(antes, depois);

        var texto = ChangeTreeReport.ToText(comparacao, "antes", "depois");
        Assert.Contains("Arvore de alteracoes", texto);
        Assert.Contains("C:\\depois.txt", texto);

        var csv = ChangeTreeReport.ToCsv(comparacao, "antes", "depois");
        Assert.Contains("Tipo;Mudanca;Item;Detalhe", csv);
        Assert.Contains("C:\\depois.txt", csv);

        var html = ChangeTreeReport.ToHtml(comparacao, "antes", "depois");
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("C:\\depois.txt", html);
    }

    [Fact]
    public void AArvoreGravaOArquivoNoFormatoPedido()
    {
        var antes = Retrato(("C:\\antes.txt", 10));
        var depois = Retrato(("C:\\depois.txt", 20));
        var comparacao = SnapshotComparer.Compare(antes, depois);

        var pasta = Path.Combine(Path.GetTempPath(), "tranal-arvore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);

        try
        {
            var csv = Path.Combine(pasta, "arvore.csv");
            var html = Path.Combine(pasta, "arvore.html");
            var txt = Path.Combine(pasta, "arvore.txt");

            Assert.Equal(csv, ChangeTreeReport.Write(comparacao, "antes", "depois", csv, ChangeTreeFormat.Csv));
            Assert.Equal(html, ChangeTreeReport.Write(comparacao, "antes", "depois", html, ChangeTreeFormat.Html));
            Assert.Equal(txt, ChangeTreeReport.Write(comparacao, "antes", "depois", txt, ChangeTreeFormat.Text));

            Assert.Contains("C:\\depois.txt", File.ReadAllText(csv));
            Assert.Contains("<!DOCTYPE html>", File.ReadAllText(html));
            Assert.Contains("C:\\depois.txt", File.ReadAllText(txt));
        }
        finally
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, true);
        }
    }

    [Fact]
    public void AArvoreTemLayoutDeImpressao()
    {
        var antes = Retrato(("C:\\antes.txt", 10));
        var depois = Retrato(("C:\\depois.txt", 20));
        var comparacao = SnapshotComparer.Compare(antes, depois);

        var tabela = ChangeTreeReport.Table(comparacao, "antes", "depois");

        Assert.Equal("Arvore de alteracoes", tabela.Title);
        Assert.Contains("antes", tabela.Header);
        Assert.Contains("depois", tabela.Header);
        Assert.Single(tabela.Pages);
        Assert.Contains("C:\\depois.txt", tabela.Pages[0].Rows[1]);
    }

    // ------------------------------------------------------------------
    // A interface: a ligacao com o que o usuario ve
    // ------------------------------------------------------------------

    [Fact]
    public void OMenuPrincipalTemAImpressaoDaLista()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        Assert.Contains("Loc.Get(\"Cmd.Print\")", codigo);
        Assert.Contains("ShowPrintPreview", codigo);

        // A janela de impressao precisa de consumidor: antes ela existia e
        // ninguem a abria.
        Assert.Contains("new ReportingWindow(", codigo);
    }

    [Fact]
    public void OAtalhoDeImpressaoEstaLigado()
    {
        var mapa = ShortcutMap.All;

        var atalho = mapa.FirstOrDefault(s => s.Action == ShortcutAction.PrintList);
        Assert.NotNull(atalho);
        Assert.Equal("Ctrl+P", atalho!.Gesture);

        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");
        Assert.Contains("case ShortcutAction.PrintList:", codigo);
    }

    [Fact]
    public void AJanelaDeRastreioExportaEImprimeAArvore()
    {
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Tracing", "TracingWindow.xaml");
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Tracing", "TracingWindow.xaml.cs");

        Assert.Contains("TracingExportChangeTree", xaml);
        Assert.Contains("TracingPrintChangeTree", xaml);

        Assert.Contains("ChangeTreeReport.Write", codigo);
        Assert.Contains("ChangeTreeReport.Table", codigo);
    }

    [Fact]
    public void AJanelaDeImpressaoAceitaQualquerTabela()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Reporting", "ReportingWindow.xaml.cs");

        // A janela recebe a tabela pronta: e o que permite imprimir a lista
        // de programas e a arvore de alteracoes com a mesma janela.
        Assert.Contains("public ReportingWindow(PrintTable table)", codigo);
        Assert.Contains("public PrintTable Table => _table;", codigo);
    }

    [Fact]
    public void AJanelaDeImpressaoTemOsBotoesDeTeste()
    {
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Reporting", "ReportingWindow.xaml");

        Assert.Contains("ReportingPrint", xaml);
        Assert.Contains("ReportingClose", xaml);
        Assert.Contains("ReportingPages", xaml);
    }
}
