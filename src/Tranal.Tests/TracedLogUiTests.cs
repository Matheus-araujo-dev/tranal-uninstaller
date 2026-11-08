using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// A casca grafica das funcionalidades de log.
///
/// O nucleo (renomear, mesclar, editar entrada a entrada, exportar, o
/// Custom Uninstall e o Advanced Uninstall) ja estava pronto e testado; o que
/// faltava era a tela. Estes testes cobrem a tela — a suite nao referencia o
/// projeto WPF, entao a verificacao e feita sobre o fonte em disco.
/// </summary>
public class TracedLogUiTests
{
    private static readonly XNamespace XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    // "AutomationProperties.AutomationId" e um atributo sem prefixo: em XAML ele
    // nao pertence a nenhum namespace XML.
    private static readonly XName AutomationIdAttr = "AutomationProperties.AutomationId";

    private static string LogWindowXaml() => RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "TracedLogWindow.xaml");
    private static string LogWindowCode() => RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "TracedLogWindow.xaml.cs");
    private static string ReviewWindowXaml() => RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "TracedReviewWindow.xaml");
    private static string ReviewWindowCode() => RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "TracedReviewWindow.xaml.cs");
    private static string ModuleViewsCode() => RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Modules", "ModuleViews.cs");

    [Fact]
    public void AJanelaDeLogMostraAsEntradasDoLog()
    {
        var xaml = LogWindowXaml();
        var codigo = LogWindowCode();

        Assert.Contains("TracedLogEntries", xaml);
        Assert.Contains("LogOperations.Entries", codigo);

        // As colunas que o dono pediu: tipo, caminho, valor, operacao e confianca.
        Assert.Contains("\"Tipo\"", codigo);
        Assert.Contains("\"Caminho\"", codigo);
        Assert.Contains("\"Valor\"", codigo);
        Assert.Contains("\"Operacao\"", codigo);
        Assert.Contains("\"Confianca\"", codigo);
    }

    [Fact]
    public void AJanelaDeLogEditaIgnoraRemoveEAcrescentaEntradas()
    {
        var codigo = LogWindowCode();

        Assert.Contains("LogOperations.EditEntry", codigo);
        Assert.Contains("LogOperations.IgnoreEntry", codigo);
        Assert.Contains("LogOperations.RemoveEntry", codigo);
        Assert.Contains("LogOperations.AddEntry", codigo);

        // As quatro acoes precisam de botao na tela (e nao so de codigo).
        var xaml = LogWindowXaml();
        Assert.Contains("TracedLogEdit", xaml);
        Assert.Contains("TracedLogIgnore", xaml);
        Assert.Contains("TracedLogRemoveEntry", xaml);
        Assert.Contains("TracedLogAddEntry", xaml);
    }

    [Fact]
    public void AJanelaDeLogRenomeaExportaEMesclaOsLogs()
    {
        var codigo = LogWindowCode();

        Assert.Contains("LogOperations.Rename", codigo);
        Assert.Contains("TraceLogExporter.Write", codigo);
        Assert.Contains("LogOperations.Merge", codigo);

        var xaml = LogWindowXaml();
        Assert.Contains("TracedLogRename", xaml);
        Assert.Contains("TracedLogExport", xaml);
        Assert.Contains("TracedLogMerge", xaml);
    }

    [Fact]
    public void AMesclagemEscolheOsLogsMarcados()
    {
        var xaml = LogWindowXaml();
        var codigo = LogWindowCode();

        // Os logs que entram na mesclagem sao os marcados na lista.
        Assert.Contains("IsChecked=\"{Binding Selected, Mode=TwoWay}\"", xaml);
        Assert.Contains("TracedLogMergeList", xaml);
        Assert.Contains("l.Selected", codigo);
    }

    [Fact]
    public void AJanelaDeLogAbreACustomEoAdvancedUninstall()
    {
        var codigo = LogWindowCode();
        var xaml = LogWindowXaml();

        Assert.Contains("TracedLogCustom", xaml);
        Assert.Contains("TracedLogAdvanced", xaml);
        Assert.Contains("new TracedReviewWindow(_log)", codigo);
        Assert.Contains("new TracedReviewWindow(_log, candidates)", codigo);
    }

    [Fact]
    public void AJanelaDeRevisaoTemOCustomEOAdvancedUninstall()
    {
        var codigo = ReviewWindowCode();

        Assert.Contains("CustomUninstallReviewBuilder.Build", codigo);
        Assert.Contains("AdvancedUninstallReviewBuilder.Build", codigo);

        var xaml = ReviewWindowXaml();
        Assert.Contains("TracedReviewItems", xaml);
    }

    [Fact]
    public void AJanelaDeRevisaoUsaOMotorCanonicoDeRemocao()
    {
        var codigo = ReviewWindowCode();

        // A tela nao apaga nada por conta propria: ela entrega o plano ao motor.
        Assert.Contains("UninstallEngine.RemoveLeftovers", codigo);
    }

    [Fact]
    public void AJanelaDeRevisaoMontaOPlanoComADecisaoDoUsuario()
    {
        var codigo = ReviewWindowCode();

        // O plano e remontado pelo nucleo com a decisao marcada na tela.
        Assert.Contains("CustomUninstallReviewBuilder.Build(_log, entry =>", codigo);
        Assert.Contains("Mod.Review.Keep", codigo);
    }

    [Fact]
    public void OsBotoesDoModuloDeLogsAparecemNaInterface()
    {
        var codigo = ModuleViewsCode();

        Assert.Contains("\"LogsOpen\"", codigo);
        Assert.Contains("\"LogsCustom\"", codigo);
        Assert.Contains("\"LogsAdvanced\"", codigo);
        Assert.Contains("Loc.Get(\"Mod.Logs.Open\")", codigo);
        Assert.Contains("Loc.Get(\"Mod.Logs.Custom\")", codigo);
        Assert.Contains("Loc.Get(\"Mod.Logs.Advanced\")", codigo);
    }

    [Fact]
    public void OModuloDeWindowsAppsExportaOScriptPowerShell()
    {
        var codigo = ModuleViewsCode();

        Assert.Contains("\"WindowsAppsExportScript\"", codigo);
        Assert.Contains("StoreAppScriptWriter.Write", codigo);
        Assert.Contains("Loc.Get(\"Mod.Apps.ExportScript\")", codigo);
    }

    [Theory]
    [InlineData("TracedLogWindow.xaml")]
    [InlineData("TracedReviewWindow.xaml")]
    public void TodaJanelaNovaTemAutomationIdEmTodosOsControles(string arquivo)
    {
        var caminho = RepoFiles.Find("src", "Tranal.Uninstaller", "Ui", arquivo);
        Assert.NotNull(caminho);

        var documento = XDocument.Load(caminho!);

        var ids = new List<string>();
        foreach (var elemento in documento.Descendants())
        {
            var nome = elemento.Attribute(XamlNs + "Name");
            if (nome is null) continue;

            var id = elemento.Attribute(AutomationIdAttr);
            Assert.True(id is not null, $"controle '{nome.Value}' sem AutomationProperties.AutomationId");
            Assert.False(string.IsNullOrWhiteSpace(id!.Value));

            ids.Add(id.Value);
        }

        Assert.NotEmpty(ids);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("TracedLogWindow.xaml")]
    [InlineData("TracedReviewWindow.xaml")]
    public void OsControlesDeInteracaoDasJanelasNovasTemAutomationId(string arquivo)
    {
        var caminho = RepoFiles.Find("src", "Tranal.Uninstaller", "Ui", arquivo);
        Assert.NotNull(caminho);

        var documento = XDocument.Load(caminho!);

        foreach (var elemento in documento.Descendants())
        {
            if (elemento.Name.LocalName is not ("Button" or "TextBox" or "ComboBox" or "ListView")) continue;

            // O conteudo de um template de item repete o controle em cada linha:
            // um id igual em varias linhas nao identifica nada.
            if (elemento.Ancestors().Any(a => a.Name.LocalName == "DataTemplate")) continue;

            var id = elemento.Attribute(AutomationIdAttr);
            Assert.True(id is not null, $"controle '{elemento.Name.LocalName}' sem AutomationProperties.AutomationId");
        }
    }

    [Theory]
    [InlineData("TracedLogWindow.xaml")]
    [InlineData("TracedReviewWindow.xaml")]
    public void AsJanelasNovasUsamOsBrushesDoTema(string arquivo)
    {
        var caminho = RepoFiles.Find("src", "Tranal.Uninstaller", "Ui", arquivo);
        Assert.NotNull(caminho);

        var xaml = File.ReadAllText(caminho!);

        Assert.Contains("Background=\"{DynamicResource PanelBrush}\"", xaml);
        Assert.Contains("Style=\"{StaticResource FlatButton}\"", xaml);

        // Cor fixa em XAML quebra o tema.
        Assert.DoesNotMatch(new System.Text.RegularExpressions.Regex("#[0-9A-Fa-f]{6,8}"), xaml);
    }

    [Fact]
    public void OsTextosDasJanelasNovasVemDoCatalogoDeIdioma()
    {
        var log = LogWindowCode();
        var revisao = ReviewWindowCode();

        Assert.Contains("Loc.Get(\"Mod.Logs.WindowTitle\")", log);
        Assert.Contains("Loc.Get(\"Mod.Logs.Rename\")", log);
        Assert.Contains("Loc.Get(\"Mod.Logs.ExportData\")", log);
        Assert.Contains("Loc.Get(\"Mod.Logs.Merge\")", log);
        Assert.Contains("Loc.Get(\"Mod.Review.CustomTitle\")", revisao);
        Assert.Contains("Loc.Get(\"Mod.Review.AdvancedTitle\")", revisao);

        // O titulo da janela tambem vem do catalogo.
        Assert.Contains("Title = Loc.Get(", log);
        Assert.Contains("Title = Loc.Get(", revisao);
    }

    [Fact]
    public void OLogMescladoTemUmNomeEscolhidoPeloUsuario()
    {
        var codigo = LogWindowCode();

        // O nome do log mesclado e digitado pelo usuario, nao inventado pela tela.
        Assert.Contains("MergeNameBox.Text", codigo);
        Assert.Contains("LogOperations.Merge(marked, name)", codigo);
    }

    [Fact]
    public void AJanelaDeRevisaoPedeConfirmacaoAntesDeRemover()
    {
        var codigo = ReviewWindowCode();

        Assert.Contains("UserMessages.Confirm", codigo);
    }
}
