using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.RegistryEditing;
using Tranal.Uninstaller.Core.Reporting;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// A remocao em lote de entradas de registro e o relatorio em HTML por
/// ferramenta.
///
/// O teste de remocao trabalha em <c>HKEY_CURRENT_USER\Software\Tranal\Tests</c>,
/// uma chave que o proprio teste cria e apaga: nada de registro do sistema e
/// tocado.
/// </summary>
public class BatchRegistryAndReportTests : IDisposable
{
    private const string BaseTeste = @"Software\Tranal\Tests\Lote";

    public void Dispose()
    {
        try
        {
            using var raiz = Registry.CurrentUser.OpenSubKey(@"Software\Tranal\Tests", writable: true);
            raiz?.DeleteSubKeyTree("Lote", throwOnMissingSubKey: false);
        }
        catch
        {
            // A chave de teste fica para o sistema limpar.
        }
    }

    private static void CriarChave(string nome, params (string Nome, string Dado)[] valores)
    {
        using var raiz = Registry.CurrentUser.CreateSubKey(BaseTeste + "\\" + nome);
        foreach (var (nomeValor, dado) in valores)
            raiz.SetValue(nomeValor, dado);
    }

    [Fact]
    public void AlvoDeRegistroLeOCaminhoCompleto()
    {
        var alvo = RegistryTarget.FromPath(@"HKEY_CURRENT_USER\Software\Tranal\Tests\Alvo");

        Assert.NotNull(alvo);
        Assert.Equal(RegistryHive.CurrentUser, alvo!.Hive);
        Assert.Equal(@"Software\Tranal\Tests\Alvo", alvo.RelativePath);
        Assert.False(alvo.IsValue);
        Assert.Equal(@"HKEY_CURRENT_USER\Software\Tranal\Tests\Alvo", alvo.DisplayPath);
    }

    [Fact]
    public void AlvoDeRegistroAceitaOCaminhoSolto()
    {
        var alvo = RegistryTarget.FromPath(@"SOFTWARE\Tranal\Tests\Alvo");

        Assert.NotNull(alvo);
        Assert.Equal(RegistryHive.LocalMachine, alvo!.Hive);
        Assert.Equal(@"SOFTWARE\Tranal\Tests\Alvo", alvo.RelativePath);
    }

    [Fact]
    public void AlvoDeRegistroRejeitaCaminhoVazio()
    {
        Assert.Null(RegistryTarget.FromPath(null));
        Assert.Null(RegistryTarget.FromPath("   "));
    }

    [Fact]
    public void AlvoDeValorMostraONomeDoValorNoCaminho()
    {
        var alvo = RegistryTarget.FromPath(@"HKEY_CURRENT_USER\Software\Tranal\Tests\Alvo", "Valor1");

        Assert.NotNull(alvo);
        Assert.True(alvo!.IsValue);
        Assert.Equal(@"HKEY_CURRENT_USER\Software\Tranal\Tests\Alvo\Valor1", alvo.DisplayPath);
    }

    [Fact]
    public void RemocaoEmLoteApagaAChaveInteira()
    {
        CriarChave("LoteA", ("Valor1", "dado"));
        using (var confere = Registry.CurrentUser.OpenSubKey(BaseTeste + "\\LoteA"))
            Assert.NotNull(confere);

        var relatorio = RegistryBatchRemover.Remove(new[]
        {
            RegistryTarget.FromPath(@"HKEY_CURRENT_USER\" + BaseTeste + @"\LoteA")!,
        });

        Assert.Single(relatorio.Outcomes);
        Assert.Equal(RemovalStatus.Removed, relatorio.Outcomes[0].Status);
        using var depois = Registry.CurrentUser.OpenSubKey(BaseTeste + @"\LoteA");
        Assert.Null(depois);
    }

    [Fact]
    public void RemocaoEmLoteApagaOValorEMantemAChave()
    {
        CriarChave("LoteB", ("Valor1", "dado"), ("Valor2", "outro"));

        var relatorio = RegistryBatchRemover.Remove(new[]
        {
            RegistryTarget.FromPath(@"HKEY_CURRENT_USER\" + BaseTeste + @"\LoteB", "Valor1")!,
        });

        Assert.Single(relatorio.Outcomes);
        Assert.Equal(RemovalStatus.Removed, relatorio.Outcomes[0].Status);
        Assert.Equal("Valor de Registro", relatorio.Outcomes[0].Item.Kind);

        using var chave = Registry.CurrentUser.OpenSubKey(BaseTeste + @"\LoteB");
        Assert.NotNull(chave);
        Assert.Null(chave!.GetValue("Valor1"));
        Assert.Equal("outro", chave.GetValue("Valor2"));
    }

    [Fact]
    public void RemocaoEmLoteMarcaInexistenteComoAusente()
    {
        var relatorio = RegistryBatchRemover.Remove(new[]
        {
            RegistryTarget.FromPath(@"HKEY_CURRENT_USER\" + BaseTeste + @"\NaoExiste")!,
        });

        Assert.Single(relatorio.Outcomes);
        Assert.Equal(RemovalStatus.AlreadyAbsent, relatorio.Outcomes[0].Status);
    }

    [Fact]
    public void RemocaoEmLoteNaoApagaChaveProtegida()
    {
        // A regra de protecao e a mesma da remocao avulsa: nada de lote pula
        // o que a remocao avulsa respeitaria.
        //
        // A chave usada e uma chave de TESTE cujo ultimo segmento e um nome
        // protegido ("SOFTWARE"): mesmo que a regra falhe, o estrago e
        // confinedo a HKCU\Software\Tranal\Tests.
        CriarChave("SOFTWARE", ("Valor1", "dado"));

        var protegido = RegistryTarget.FromPath(@"HKEY_CURRENT_USER\" + BaseTeste + @"\SOFTWARE");

        var relatorio = RegistryBatchRemover.Remove(new[] { protegido! });

        Assert.Single(relatorio.Outcomes);
        Assert.Equal(RemovalStatus.Protected, relatorio.Outcomes[0].Status);

        using var conferida = Registry.CurrentUser.OpenSubKey(BaseTeste + @"\SOFTWARE");
        Assert.NotNull(conferida);
    }

    [Fact]
    public void RemocaoEmLoteAceitaListaVazia()
    {
        var relatorio = RegistryBatchRemover.Remove(null);

        Assert.Empty(relatorio.Outcomes);
    }

    [Fact]
    public void RelatorioEmHtmlEscapaAsCelulas()
    {
        var html = ToolReportWriter.ToHtml(
            "Servicos <teste>",
            "sub & titulo",
            new[] { new ToolReportColumn { Header = "Nome", Width = 120 } },
            new[] { new ToolReportRow { Cells = new[] { "<script>alert(1)</script>", "a & b" } } });

        Assert.Contains("<title>Servicos &lt;teste&gt;</title>", html);
        Assert.Contains("sub &amp; titulo", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("<th style=\"width:120px\">Nome</th>", html);
    }

    [Fact]
    public void RelatorioEmHtmlTemUmaLinhaPorItem()
    {
        var html = ToolReportWriter.ToHtml(
            "Tarefas",
            null,
            new[] { new ToolReportColumn { Header = "A" }, new ToolReportColumn { Header = "B" } },
            new[]
            {
                new ToolReportRow { Cells = new[] { "1", "2" } },
                new ToolReportRow { Cells = new[] { "3", "4" } },
                new ToolReportRow { Cells = new[] { "5" } },
            });

        // A terceira linha tem celula faltando: a coluna vazia e preenchida.
        Assert.Contains("<td>5</td>", html);
        Assert.Contains("<td></td>", html);
        Assert.Contains("3 item(ns)", html);
    }

    [Fact]
    public void RelatorioEmHtmlGravaOArquivo()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "tranal-rel-" + Guid.NewGuid().ToString("N")[..8]);
        var arquivo = Path.Combine(pasta, "relatorio.html");

        try
        {
            ToolReportWriter.Write(
                arquivo,
                "Servicos",
                "Lista de servicos",
                new[] { new ToolReportColumn { Header = "Nome" } },
                new[] { new ToolReportRow { Cells = new[] { "Spooler" } } });

            Assert.True(File.Exists(arquivo));
            var texto = File.ReadAllText(arquivo);
            Assert.Contains("Spooler", texto);
            Assert.Contains("<!DOCTYPE html>", texto);
        }
        finally
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, true);
        }
    }

    [Fact]
    public void RelatorioEmHtmlNaoDependeDeRede()
    {
        var html = ToolReportWriter.ToHtml(
            "Drivers",
            null,
            new[] { new ToolReportColumn { Header = "Nome" } },
            Array.Empty<ToolReportRow>());

        Assert.DoesNotContain("http://", html);
        Assert.DoesNotContain("https://", html);
        Assert.DoesNotContain("<link", html);
        Assert.DoesNotContain("<script", html);
    }
}
