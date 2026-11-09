using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;
using Tranal.Cli;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O lote 21 fecha duas lacunas medidas contra os concorrentes:
///
/// <list type="bullet">
/// <item><b>T4#47 (Ashampoo StartUp Tuner)</b>: faltavam <i>adicionar</i> e
/// <i>editar</i>. Habilitar, desabilitar e remover ja existiam.</item>
/// <item><b>T4#51 (Ashampoo Crash Analyzer)</b>: faltavam o <i>export</i> do
/// relatorio e a <i>busca online</i>.</item>
/// </list>
///
/// <para>A regra mora no nucleo
/// (<c>Core/Tools/AutorunManager.cs</c> e <c>Core/Tools/CrashAnalyzer.cs</c>)
/// e a suite testa esse modulo. A ligacao com a interface (a tela e a linha
/// de comando) e conferida sobre o fonte, pelo mesmo motivo das outras
/// paridades: a suite nao referencia o projeto grafico (WPF).</para>
///
/// <para>Os testes de edicao escrevem em uma chave descartavel do HKCU
/// (<c>Software\Tranal\Tests\Lote21</c>), que e removida no fim de cada
/// teste. A lista de exclusoes e trocada por uma vazia durante o teste: a
/// edicao respeta a lista, e um teste nao pode depender do que o usuario
/// excluiu na maquina dele.</para>
/// </summary>
public class Lote21FeatureTests
{
    /// <summary>A chave de teste: nunca fica no registro.</summary>
    private const string TestRoot = @"Software\Tranal\Tests\Lote21";

    // ==================================================================
    // A natureza do item: a regra que decide
    // ==================================================================

    [Fact]
    public void SemLocalOItemEChaveDeRegistro()
    {
        Assert.Equal(AutorunItemKind.RunKey, AutorunManager.ResolveKind(string.Empty));
        Assert.Equal(AutorunItemKind.RunKey, AutorunManager.ResolveKind(null));
    }

    [Fact]
    public void UmaChaveDeRegistroNoLocalEChaveDeRegistro()
    {
        Assert.Equal(
            AutorunItemKind.RunKey,
            AutorunManager.ResolveKind(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"));
    }

    [Fact]
    public void UmaPastaNoLocalEItemDePastaDeInicializacao()
    {
        Assert.Equal(
            AutorunItemKind.StartupFolder,
            AutorunManager.ResolveKind(@"C:\Users\Teste\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup"));
    }

    // ==================================================================
    // Editar o item de inicializacao
    // ==================================================================

    [Fact]
    public void EditarRenomeiaOValorERequardaOComando()
    {
        ComChaveDeTeste("Antigo", @"C:\Exemplo\exemplo.exe", item =>
        {
            Assert.True(AutorunManager.Update(item, "Novo", string.Empty));

            using var relida = Registry.CurrentUser.OpenSubKey(TestRoot);
            Assert.Null(relida?.GetValue("Antigo"));
            Assert.Equal(@"C:\Exemplo\exemplo.exe", relida?.GetValue("Novo")?.ToString());
        });
    }

    [Fact]
    public void EditarTrocaOComandoEguardaONome()
    {
        ComChaveDeTeste("Antigo", @"C:\Exemplo\exemplo.exe", item =>
        {
            Assert.True(AutorunManager.Update(item, string.Empty, @"C:\Outro\outro.exe"));

            using var relida = Registry.CurrentUser.OpenSubKey(TestRoot);
            Assert.Equal(@"C:\Outro\outro.exe", relida?.GetValue("Antigo")?.ToString());
            Assert.Equal(1, relida?.GetValueNames().Length);
        });
    }

    [Fact]
    public void EditarTrocaONomeEOComandoJuntos()
    {
        ComChaveDeTeste("Antigo", @"C:\Exemplo\exemplo.exe", item =>
        {
            Assert.True(AutorunManager.Update(item, "Novo", @"C:\Outro\outro.exe"));

            using var relida = Registry.CurrentUser.OpenSubKey(TestRoot);
            Assert.Null(relida?.GetValue("Antigo"));
            Assert.Equal(@"C:\Outro\outro.exe", relida?.GetValue("Novo")?.ToString());
        });
    }

    [Fact]
    public void EditarGuardaOPrefixoDoItemDesabilitado()
    {
        ComChaveDeTeste("DisabledName-Antigo", @"C:\Exemplo\exemplo.exe", item =>
        {
            Assert.True(AutorunManager.Update(item, "Novo", string.Empty));

            using var relida = Registry.CurrentUser.OpenSubKey(TestRoot);
            Assert.Null(relida?.GetValue("DisabledName-Antigo"));
            Assert.Equal(@"C:\Exemplo\exemplo.exe", relida?.GetValue("DisabledName-Novo")?.ToString());
        });
    }

    [Fact]
    public void EditarAtualizaOLocalDoItem()
    {
        ComChaveDeTeste("Antigo", @"C:\Exemplo\exemplo.exe", item =>
        {
            Assert.True(AutorunManager.Update(item, "Novo", string.Empty));

            Assert.Equal(@"HKEY_CURRENT_USER\" + TestRoot + @"\Novo", item.Location);
            Assert.Equal("Novo", item.Name);
        });
    }

    [Fact]
    public void EditarCancelaQuandoJaExisteUmValorComOMesmoNome()
    {
        ComChaveDeTeste("Antigo", @"C:\Exemplo\exemplo.exe", item =>
        {
            using (var raiz = Registry.CurrentUser.OpenSubKey(TestRoot, writable: true))
            {
                raiz?.SetValue("Novo", @"C:\Outro\outro.exe", RegistryValueKind.String);
            }

            Assert.False(AutorunManager.Update(item, "Novo", string.Empty));

            using var relida = Registry.CurrentUser.OpenSubKey(TestRoot);
            Assert.Equal(@"C:\Exemplo\exemplo.exe", relida?.GetValue("Antigo")?.ToString());
        });
    }

    [Fact]
    public void ItemDeSistemaNaoEEditado()
    {
        var item = new AutorunItem
        {
            Kind = AutorunItemKind.RunKey,
            Name = "SecurityHealth",
            Command = @"C:\Windows\System32\SecurityHealthSystray.exe",
            Location = @"HKEY_CURRENT_USER\" + TestRoot + @"\SecurityHealth",
            IsSystem = true,
        };

        Assert.False(AutorunManager.Update(item, "Outro", string.Empty));
    }

    [Fact]
    public void EditarSemNomeECOMandoNaoFazNada()
    {
        ComChaveDeTeste("Antigo", @"C:\Exemplo\exemplo.exe", item =>
        {
            Assert.False(AutorunManager.Update(item, string.Empty, string.Empty));

            using var relida = Registry.CurrentUser.OpenSubKey(TestRoot);
            Assert.Equal(@"C:\Exemplo\exemplo.exe", relida?.GetValue("Antigo")?.ToString());
        });
    }

    // ==================================================================
    // O relatorio do Crash Analyzer
    // ==================================================================

    [Fact]
    public void ORelatorioMostraCadaFalhaAgrupada()
    {
        var html = CrashAnalyzer.ToHtmlReport(new[] { Falha() });

        Assert.Contains("Exemplo", html);
        Assert.Contains("Aplicativo com falha (1000)", html);
        Assert.Contains("21/11/2026 10:30", html);
        Assert.Contains(@"C:\Exemplo\exemplo.exe", html);
    }

    [Fact]
    public void ORelatorioAceitaOsTextosDeQuemChama()
    {
        var html = CrashAnalyzer.ToHtmlReport(
            new[] { Falha() },
            new CrashReportLabels { Program = "Programa", Count = "Falhas" });

        Assert.Contains("Programa", html);
        Assert.Contains("Falhas", html);
    }

    [Fact]
    public void ORelatorioDeUmaListaVaziaAindaEValido()
    {
        var html = CrashAnalyzer.ToHtmlReport(Array.Empty<CrashEntry>());

        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("</html>", html);
    }

    [Fact]
    public void GravarORelatorioEscreveOArquivo()
    {
        var caminho = Path.Combine(Path.GetTempPath(), "tranal-lote21-crash.html");

        try
        {
            Assert.True(CrashAnalyzer.WriteReport(caminho, new[] { Falha() }));
            Assert.True(File.Exists(caminho));

            var html = File.ReadAllText(caminho);
            Assert.Contains("Exemplo", html);
        }
        finally
        {
            if (File.Exists(caminho)) File.Delete(caminho);
        }
    }

    // ==================================================================
    // A busca online do Crash Analyzer
    // ==================================================================

    [Fact]
    public void OTermoDaBuscaTrazOProgramaEOTipoDaFalha()
    {
        Assert.Equal("\"Exemplo\" crash", CrashAnalyzer.BuildSearchTerm(Falha()));
    }

    [Fact]
    public void OTermoDaBuscaDeUmTravamentoFalaDeHang()
    {
        var falha = new CrashEntry
        {
            ProgramName = "Travado",
            Count = 1,
            EventId = 1001,
            EventName = "Aplicativo travado",
        };

        Assert.Equal("\"Travado\" hang", CrashAnalyzer.BuildSearchTerm(falha));
    }

    [Fact]
    public void OTermoDaBuscaDeUmProgramaSemNomeEVazio()
    {
        Assert.Equal(string.Empty, CrashAnalyzer.BuildSearchTerm(new CrashEntry()));
        Assert.Equal(string.Empty, CrashAnalyzer.BuildSearchTerm(null!));
    }

    [Fact]
    public void OEnderecoDaBuscaOnlineUsaOMotorConfigurado()
    {
        var url = CrashAnalyzer.BuildSearchUrl(Falha(), new AppSettings());

        Assert.StartsWith("http", url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Exemplo", url);
    }

    // ==================================================================
    // A linha de comando
    // ==================================================================

    [Fact]
    public void ACliLeOsValoresNaPosicaoPedida()
    {
        var args = new[] { "/autorun", "/add", "nome", "comando", "local" };

        Assert.Equal("nome", CliArgs.ValueAfter(args, "/add"));
        Assert.Equal("comando", CliArgs.ValuesAfter(args, "/add", 1));
        Assert.Equal("local", CliArgs.ValuesAfter(args, "/add", 2));
    }

    [Fact]
    public void ACliParaNaOpcaoSeguinte()
    {
        var args = new[] { "/autorun", "/add", "nome", "/export", "arquivo" };

        Assert.Null(CliArgs.ValuesAfter(args, "/add", 1));
        Assert.Null(CliArgs.ValuesAfter(args, "/add", 2));
    }

    [Fact]
    public void ACliNaoAchaValorDeOpcaoInexistente()
    {
        Assert.Null(CliArgs.ValuesAfter(new[] { "/autorun" }, "/add", 0));
    }

    // ==================================================================
    // A interface (conferida sobre o fonte)
    // ==================================================================

    [Fact]
    public void ATelaDoAutorunTemAdicionarEEditar()
    {
        var fonte = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Tools", "ToolViews.cs");

        Assert.Contains("\"AutorunAdd\"", fonte);
        Assert.Contains("\"AutorunEdit\"", fonte);
        Assert.Contains("AutorunManager.Update", fonte);
        Assert.Contains("AutorunManager.ResolveKind", fonte);
    }

    [Fact]
    public void ATelaDoCrashAnalyzerTemExportarEBuscaOnline()
    {
        var fonte = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Tools", "InsightViews.cs");

        Assert.Contains("\"CrashAnalyzerExport\"", fonte);
        Assert.Contains("\"CrashAnalyzerSearch\"", fonte);
        Assert.Contains("CrashAnalyzer.WriteReport", fonte);
        Assert.Contains("CrashAnalyzer.BuildSearchUrl", fonte);
    }

    [Fact]
    public void ACliTemAsAcoesDoAutorunEOCrashAnalyzer()
    {
        var fonte = RepoFiles.Read("src", "Tranal.Cli", "ExtraCommands.cs");

        Assert.Contains("RunAutorun(args, flags)", fonte);
        Assert.Contains("RunCrashAnalyzer(args, flags)", fonte);
        Assert.Contains("AutorunManager.Update", fonte);
        Assert.Contains("CrashAnalyzer.WriteReport", fonte);
    }

    [Fact]
    public void OsTextosNovosEstaoNosDoisIdiomas()
    {
        var chaves = new[]
        {
            "Tool.Autorun.Edit",
            "Tool.Autorun.EditName",
            "Tool.Autorun.EditCommand",
            "Tool.Autorun.LocationOptional",
            "Tool.Autorun.EditMissing",
            "Tool.Autorun.Added",
            "Tool.Autorun.Edited",
            "Tool.Autorun.NotEditable",
            "Insight.Crash.Export",
            "Insight.Crash.Exported",
            "Insight.Crash.Search",
            "Insight.Crash.SearchTerm",
            "Insight.Crash.Column.Count",
            "Insight.Crash.Column.First",
            "Insight.Crash.Column.Last",
            "Insight.Crash.Column.Event",
            "Insight.Crash.Column.Executable",
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
        using var documento = JsonDocument.Parse(texto);

        var dicionario = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var propriedade in documento.RootElement.EnumerateObject())
        {
            dicionario[propriedade.Name] = propriedade.Value.GetString() ?? string.Empty;
        }

        return dicionario;
    }

    private static CrashEntry Falha() => new()
    {
        ProgramName = "Exemplo",
        Executable = @"C:\Exemplo\exemplo.exe",
        Count = 3,
        FirstSeen = new DateTime(2026, 11, 19, 8, 0, 0),
        LastSeen = new DateTime(2026, 11, 21, 10, 30, 0),
        EventId = 1000,
        EventName = "Aplicativo com falha",
    };

    /// <summary>
    /// Cria a chave de teste com um valor, chama a acao e apaga tudo no fim.
    /// A lista de exclusoes e trocada por uma vazia: a edicao a respeita, e
    /// um teste nao pode depender do que o usuario excluiu na maquina dele.
    /// </summary>
    private static void ComChaveDeTeste(string nomeValor, string comando, Action<AutorunItem> acao)
    {
        RemoverChave();

        var listaAnterior = ExcludeList.Current;
        ExcludeList.SetCurrent(new ExcludeList());

        try
        {
            using var raiz = Registry.CurrentUser.CreateSubKey(TestRoot);
            raiz.SetValue(nomeValor, comando, RegistryValueKind.String);

            var item = new AutorunItem
            {
                Kind = AutorunItemKind.RunKey,
                Name = nomeValor.StartsWith("DisabledName-", StringComparison.Ordinal)
                    ? nomeValor["DisabledName-".Length..]
                    : nomeValor,
                Command = comando,
                Location = @"HKEY_CURRENT_USER\" + TestRoot + @"\" + nomeValor,
            };

            acao(item);
        }
        finally
        {
            ExcludeList.SetCurrent(listaAnterior);
            RemoverChave();
        }
    }

    private static void RemoverChave()
    {
        try
        {
            using var raiz = Registry.CurrentUser.OpenSubKey(@"Software\Tranal\Tests", writable: true);
            raiz?.DeleteSubKeyTree("Lote21", throwOnMissingSubKey: false);
        }
        catch
        {
            // A chave de teste ja foi removida.
        }
    }
}
