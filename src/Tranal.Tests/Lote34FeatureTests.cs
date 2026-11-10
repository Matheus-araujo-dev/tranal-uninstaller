using System.IO;
using System.Text.RegularExpressions;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Lote 34: o que o nucleo ja sabia e a tela nao dizia.
///
/// Tres das linhas da auditoria estavam measurecidas contra um codigo que ja
/// tinha mudado, e uma delas era um bug de verdade:
///
/// - a remocao principal usava <c>File.Delete</c> direto, e um arquivo
///   somente-leitura nao saia (o Windows recusa). Agora a marcacao e tirada
///   pela REGRA do <c>StubbornRemover</c>, que ja existia;
/// - o desregistro de componentes COM existia como comando separado, sem
///   nenhum lugar no fluxo de desinstalacao — agora ele acontece antes de
///   apagar os arquivos, que e o que evita o registro orfao;
/// - a varredura do assistente nao tinha acao de exportar o resultado, ainda
///   que o exportador em XML existisse (a linha de comando usava).
///
/// Este arquivo mede os tres.
/// </summary>
public sealed class Lote34FeatureTests : IDisposable
{
    private readonly string _temporaria;

    /// <summary>Cria a pasta temporaria do teste.</summary>
    public Lote34FeatureTests()
    {
        _temporaria = Path.Combine(Path.GetTempPath(), "tranal-lote34-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_temporaria);
    }

    /// <summary>Apaga a pasta temporaria do teste.</summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_temporaria)) Directory.Delete(_temporaria, recursive: true);
        }
        catch
        {
            // A pasta temporaria e do proprio teste: se o Windows seguro o
            // arquivo, ela fica para o limpador do sistema.
        }
    }

    /// <summary>
    /// O corpo de um metodo do fonte, qualquer que seja o tipo de retorno.
    ///
    /// O helper <see cref="RepoFiles.Metodo"/> so reconhece alguns tipos; aqui
    /// o tipo do metodo nao importa (e <c>async Task</c> nao esta na lista).
    ///
    /// A busca e pela DEFINICAO: uma chamada (<c>await X();</c>) e recusada,
    /// porque depois dos parametros vem ponto e virgula, e nao o corpo.
    /// </summary>
    /// <param name="codigo">O fonte inteiro.</param>
    /// <param name="nome">O nome do metodo.</param>
    private static string Corpo(string codigo, string nome)
    {
        var definicao = new Regex(Regex.Escape(nome) + @"\s*\(", RegexOptions.CultureInvariant);

        foreach (Match achado in definicao.Matches(codigo))
        {
            var abre = codigo.IndexOf('(', achado.Index);
            if (abre < 0) continue;

            var fecha = -1;
            var profundidade = 0;
            for (var i = abre; i < codigo.Length; i++)
            {
                if (codigo[i] == '(') profundidade++;
                else if (codigo[i] == ')')
                {
                    profundidade--;
                    if (profundidade == 0)
                    {
                        fecha = i;
                        break;
                    }
                }
            }

            if (fecha < 0) continue;

            // Apos os parametros vem a assinatura do tipo de retorno e o
            // corpo. Uma chamada tem ponto e virgula aqui.
            var j = fecha + 1;
            while (j < codigo.Length && char.IsWhiteSpace(codigo[j])) j++;
            if (j < codigo.Length && codigo[j] == ':')
            {
                while (j < codigo.Length && codigo[j] != '{' && codigo[j] != ';') j++;
            }

            if (j >= codigo.Length || codigo[j] != '{') continue;

            var nivel = 0;
            for (var k = j; k < codigo.Length; k++)
            {
                if (codigo[k] == '{') nivel++;
                else if (codigo[k] == '}')
                {
                    nivel--;
                    if (nivel == 0) return codigo[achado.Index..(k + 1)];
                }
            }

            return codigo[achado.Index..];
        }

        Assert.Fail($"definicao de '{nome}' nao encontrada");
        return string.Empty;
    }

    /// <summary>
    /// A remocao principal apaga um arquivo somente-leitura.
    ///
    /// Antes do lote 34 o <c>File.Delete</c> direto falhava, e o item
    /// voltava como falha (ou ficava agendado para o proximo reinicio).
    /// </summary>
    [Fact]
    public void ARemocaoApagaArquivoSomenteLeitura()
    {
        var arquivo = Path.Combine(_temporaria, "somente-leitura.bin");
        File.WriteAllText(arquivo, "conteudo");
        File.SetAttributes(arquivo, FileAttributes.ReadOnly);

        var item = new LeftoverItem
        {
            Kind = "Arquivo",
            Path = arquivo,
            Selected = true,
            Confidence = LeftoverConfidence.High,
        };

        var opcoes = new UninstallRemovalOptions
        {
            Backup = false,
            UseRecycleBin = false,
            DeferToNextRestart = false,

            // O guarda de arquivo pessoal e outra regra: o teste mede a
            // remocao do somente-leitura, e nao o guarda de documento.
            ProtectPersonalFiles = false,
        };

        var relatorio = UninstallEngine.RemoveLeftovers(new[] { item }, opcoes);

        Assert.Single(relatorio.Outcomes);
        Assert.True(relatorio.Outcomes[0].Removed,
            $"o arquivo nao saiu: {relatorio.Outcomes[0].Status} - {relatorio.Outcomes[0].Message}");
        Assert.False(File.Exists(arquivo));
    }

    /// <summary>
    /// A regra do somente-leitura mora no StubbornRemover: ela tira a
    /// marcacao do arquivo e da pasta.
    /// </summary>
    [Fact]
    public void ARegraDoSomenteLeituraLimpaArquivoEPasta()
    {
        var arquivo = Path.Combine(_temporaria, "marcado.txt");
        File.WriteAllText(arquivo, "conteudo");
        File.SetAttributes(arquivo, FileAttributes.ReadOnly);

        Assert.True(StubbornRemover.ClearReadOnly(arquivo));
        Assert.False((File.GetAttributes(arquivo) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly);

        var pasta = Path.Combine(_temporaria, "pasta-marcada");
        Directory.CreateDirectory(pasta);
        File.SetAttributes(pasta, FileAttributes.ReadOnly);

        Assert.True(StubbornRemover.ClearReadOnly(pasta));
        Assert.False((File.GetAttributes(pasta) & FileAttributes.ReadOnly) == FileAttributes.ReadOnly);

        // Um caminho que nao existe nao e erro: ele ja nao esta protegido.
        Assert.True(StubbornRemover.ClearReadOnly(Path.Combine(_temporaria, "nao-existe.txt")));

        // Caminho vazio nao tem o que limpar.
        Assert.False(StubbornRemover.ClearReadOnly(""));
    }

    /// <summary>
    /// O desregistro de COM por pasta existe e devolve a lista dos
    /// componentes registrados a partir dela.
    /// </summary>
    [Fact]
    public void ODesregistroDeComPorPastaExiste()
    {
        // Pasta vazia: nada esta registrado a partir dela.
        Assert.Empty(ComUnregister.UnregisterFolder(_temporaria));

        // Pasta que nao existe: o mesmo.
        Assert.Empty(ComUnregister.UnregisterFolder(Path.Combine(_temporaria, "nao-existe")));

        // Caminho vazio: o mesmo.
        Assert.Empty(ComUnregister.UnregisterFolder(null));
        Assert.Empty(ComUnregister.UnregisterFolder(""));
    }

    /// <summary>
    /// O assistente tem a acao de exportar a lista de sobras em XML.
    /// </summary>
    [Fact]
    public void OAssistenteTemOAcaoDeExportarXml()
    {
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml");

        Assert.Contains("WizardExportLeftoversButton", xaml, System.StringComparison.Ordinal);
        Assert.Contains("ExportLeftoversButton_Click", xaml, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// A exportacao usa o exportador do nucleo — e nao uma segunda
    /// implementacao na tela.
    /// </summary>
    [Fact]
    public void AExportacaoUsaOExportadorDoNucleo()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml.cs");

        var metodo = Corpo(codigo, "ExportLeftoversButton_Click");

        Assert.Contains("LeftoverReportExporter.WriteXml", metodo, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// O desregistro de COM acontece no fluxo de desinstalacao, e nao em uma
    /// ferramenta solta.
    /// </summary>
    [Fact]
    public void ODesregistroDeComEstaNoFluxoDeDesinstalacao()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml.cs");

        var metodo = Corpo(codigo, "RemoveAsync");
        Assert.Contains("DesregistrarCom()", metodo, System.StringComparison.Ordinal);

        // A regra mora no nucleo: a tela so consome o metodo de alto nivel.
        var consulta = Corpo(codigo, "DesregistrarCom");
        Assert.Contains("ComUnregister.UnregisterFolder", consulta, System.StringComparison.Ordinal);

        var nucleo = RepoFiles.Read("src", "Tranal.Uninstaller", "Core", "Automation", "ComUnregister.cs");
        Assert.Contains("public static IReadOnlyList<KeyValuePair<string, ManagementResult>> UnregisterFolder",
            nucleo, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// O caminho principal de remocao consome a regra do somente-leitura —
    /// ele nao repete o <c>File.SetAttributes</c> por conta propria.
    /// </summary>
    [Fact]
    public void OCaminhoPrincipalDeRemocaoConsomeARegra()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Core", "UninstallSupport.cs");

        var metodo = Corpo(codigo, "RemoveFileSafely");

        Assert.Contains("StubbornRemover.ClearReadOnly(file)", metodo, System.StringComparison.Ordinal);
        Assert.DoesNotContain("FileAttributes.ReadOnly", metodo, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// O caminho principal da remocao do item Arquivo tambem consome a
    /// regra do somente-leitura.
    ///
    /// Este e o caminho que o usuario ve: um item de sobra marcado como
    /// arquivo. Antes do lote 34 ele usava o <c>File.Delete</c> direto e
    /// um arquivo somente-leitura nao saia - o Windows recusa.
    /// </summary>
    [Fact]
    public void OItemDeSobraArquivoTambemConsomeARegra()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Core", "UninstallSupport.cs");

        var metodo = Corpo(codigo, "RemoveOne");

        Assert.Contains("StubbornRemover.ClearReadOnly(full)", metodo, System.StringComparison.Ordinal);
        Assert.DoesNotContain("FileAttributes.ReadOnly", metodo, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// A varredura que nao remove nada tambem exporta em XML.
    /// </summary>
    ///
    /// A tela do assistente so chega na etapa da varredura DEPOIS do
    /// desinstalador nativo. A varredura que a pessoa faz sem remover
    /// nada e a da desinstalacao personalizada - e ali so havia CSV.
    /// </summary>
    [Fact]
    public void AVarreduraSemRemocaoTambemExportaXml()
    {
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "ManualRemovalWindow.xaml");

        Assert.Contains("ManualExportXmlButton", xaml, System.StringComparison.Ordinal);
        Assert.Contains("ExportXmlButton_Click", xaml, System.StringComparison.Ordinal);

        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "ManualRemovalWindow.xaml.cs");

        var metodo = Corpo(codigo, "ExportXmlButton_Click");

        // A regra mora no nucleo: a tela so pergunta onde gravar.
        Assert.Contains("LeftoverReportExporter.WriteXml", metodo, System.StringComparison.Ordinal);
        Assert.Contains("ExportXmlButton.IsEnabled = _sobras.Count > 0", codigo, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// As chaves novas do catalogo existem nos dois idiomas.
    /// </summary>
    [Fact]
    public void AsChavesNovasEstaoNosDoisIdiomas()
    {
        var chaves = new[]
        {
            "Wizard.Step4.ExportXml",
            "Wizard.ExportLeftoversTitle",
            "Wizard.ExportLeftoversFilter",
            "Wizard.ExportNothing",
            "Wizard.Exported",
            "Wizard.ExportFailed",
            "Wizard.ComUnregistered",
            "Wizard.ComUnregisterFailed",
            "Manual.ExportXml",
            "Manual.ExportXmlTitle",
            "Manual.ExportXmlFilter",
            "Manual.ExportedXml",
            "Manual.ExportXmlFailed",
        };

        foreach (var arquivo in new[] { "pt-BR.json", "en-US.json" })
        {
            var caminho = RepoFiles.Find("src", "Tranal.Uninstaller", "Lang", arquivo);
            Assert.NotNull(caminho);

            var texto = File.ReadAllText(caminho!);
            foreach (var chave in chaves)
            {
                Assert.True(texto.Contains("\"" + chave + "\"", System.StringComparison.Ordinal),
                    $"a chave '{chave}' falta em {arquivo}");
            }
        }
    }
}
