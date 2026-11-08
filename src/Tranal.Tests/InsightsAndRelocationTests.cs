using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O painel de informacoes do sistema, a data relativa, o total agregado, o
/// ranking de impacto, o pais do programa, a analise de falhas e a mudanca de
/// local de instalacao.
/// </summary>
public class InsightsAndRelocationTests
{
    // ------------------------------------------------------------------
    // Data relativa
    // ------------------------------------------------------------------

    [Fact]
    public void DataRelativaMostraHojeEOntem()
    {
        var hoje = new DateTime(2026, 11, 8, 14, 0, 0);

        Assert.Equal("Hoje", RelativeDate.Format(hoje, hoje));
        Assert.Equal("Ontem", RelativeDate.Format(hoje.AddDays(-1), hoje));
    }

    [Fact]
    public void DataRelativaUsaSemanasMesesEAnos()
    {
        var hoje = new DateTime(2026, 11, 8, 14, 0, 0);

        Assert.Equal("3 dias atras", RelativeDate.Format(hoje.AddDays(-3), hoje));
        Assert.Equal("1 semana atras", RelativeDate.Format(hoje.AddDays(-8), hoje));
        Assert.Equal("2 semanas atras", RelativeDate.Format(hoje.AddDays(-14), hoje));
        Assert.Equal("1 mes atras", RelativeDate.Format(hoje.AddDays(-40), hoje));
        Assert.Equal("1 ano atras", RelativeDate.Format(hoje.AddDays(-400), hoje));
        Assert.Equal("2 anos atras", RelativeDate.Format(hoje.AddDays(-800), hoje));
    }

    [Fact]
    public void DataRelativaDeDataFuturaMostraADataAbsoluta()
    {
        var hoje = new DateTime(2026, 11, 8, 14, 0, 0);
        var futuro = hoje.AddDays(5);

        // Um relogo errado nao pode virar "daqui a 5 dias".
        Assert.Equal("13/11/2026", RelativeDate.Format(futuro, hoje));
    }

    [Fact]
    public void DataRelativaDeDataNulaFicaVazia()
    {
        Assert.Equal(string.Empty, RelativeDate.Format(null));
    }

    [Fact]
    public void DataRelativaInterpretaOFormatoDoWindows()
    {
        Assert.True(RelativeDate.TryParse("08/11/2026", out var data));
        Assert.Equal(2026, data.Year);
        Assert.Equal(11, data.Month);
        Assert.Equal(8, data.Day);

        Assert.True(RelativeDate.TryParse("20261108", out var data2));
        Assert.Equal(2026, data2.Year);

        Assert.False(RelativeDate.TryParse("nao e data", out _));
        Assert.False(RelativeDate.TryParse(null, out _));
    }

    [Fact]
    public void DataRelativaDoTextoUsaADataAbsoluta()
    {
        var hoje = new DateTime(2026, 11, 8, 14, 0, 0);

        Assert.Equal("Ontem", RelativeDate.FormatFromText("07/11/2026", hoje));
        Assert.Equal(string.Empty, RelativeDate.FormatFromText(""));
    }

    // ------------------------------------------------------------------
    // Total agregado
    // ------------------------------------------------------------------

    [Fact]
    public void TotalAgregadoSomaOsProgramas()
    {
        var programas = new List<InstalledProgram>
        {
            new() { DisplayName = "A", SizeBytes = 1024 },
            new() { DisplayName = "B", SizeBytes = 2048 },
            new() { DisplayName = "C" },
        };

        var total = ProgramTotalsCalculator.Compute(programas);

        Assert.Equal(3, total.Count);
        Assert.Equal(3072, total.SizeBytes);
        Assert.Contains("3 programa(s)", total.Text);
    }

    [Fact]
    public void TotalAgregadoDeListaVazia()
    {
        var total = ProgramTotalsCalculator.Compute(null);

        Assert.Equal(0, total.Count);
        Assert.Equal(0, total.SizeBytes);
    }

    // ------------------------------------------------------------------
    // Painel do sistema
    // ------------------------------------------------------------------

    [Fact]
    public void PainelDoSistemaTemPeloMenosOSistemaOperacional()
    {
        var itens = SystemInfoPanel.Collect(incluirGpu: false);

        Assert.NotEmpty(itens);
        Assert.Contains(itens, i => i.Label == "Sistema operacional");
        Assert.All(itens, i => Assert.False(string.IsNullOrWhiteSpace(i.Label)));
        Assert.All(itens, i => Assert.False(string.IsNullOrWhiteSpace(i.Value)));
    }

    [Fact]
    public void PainelDoSistemaMostraProcessadorEMemoria()
    {
        var itens = SystemInfoPanel.Collect(incluirGpu: false);

        // Processador e memoria existem em qualquer Windows moderno.
        Assert.Contains(itens, i => i.Label == "Processador");
        Assert.Contains(itens, i => i.Label == "Memoria");
    }

    [Fact]
    public void FormatarBytesUsaAUnidadeCerta()
    {
        // A formatacao de bytes mora em um lugar so (ByteFormatter).
        Assert.Equal("512 B", ByteFormatter.Format(512));
        Assert.Equal("1 KB", ByteFormatter.Format(1024));
        Assert.Equal("1 MB", ByteFormatter.Format(1024 * 1024));
        Assert.Equal("1 GB", ByteFormatter.Format(1024L * 1024 * 1024));
        Assert.Equal("0 B", ByteFormatter.Format(-1));
        Assert.Equal("1.5 KB", ByteFormatter.Format(1536));
    }

    [Fact]
    public void FormatarDuracaoUsaAUnidadeCerta()
    {
        Assert.Equal("1 dia(s) e 2 hora(s)", SystemInfoPanel.FormatarDuracao(TimeSpan.FromDays(1) + TimeSpan.FromHours(2)));
        Assert.Equal("3 hora(s) e 5 minuto(s)", SystemInfoPanel.FormatarDuracao(TimeSpan.FromHours(3) + TimeSpan.FromMinutes(5)));
        Assert.Equal("7 minuto(s)", SystemInfoPanel.FormatarDuracao(TimeSpan.FromMinutes(7)));
    }

    // ------------------------------------------------------------------
    // Impacto
    // ------------------------------------------------------------------

    [Fact]
    public void NotaDeImpactoSobeComMemoriaECpu()
    {
        var semNada = ProgramImpactAnalyzer.CalcularNota(0, 0);
        var soMemoria = ProgramImpactAnalyzer.CalcularNota(500L * 1024 * 1024, 0);
        var soCpu = ProgramImpactAnalyzer.CalcularNota(0, 60);
        var muito = ProgramImpactAnalyzer.CalcularNota(8L * 1024 * 1024 * 1024, 600);

        Assert.Equal(0, semNada);
        Assert.True(soMemoria > 0);
        Assert.True(soCpu > 0);
        Assert.True(muito > soMemoria);
        Assert.InRange(muito, 0, 100);
    }

    [Fact]
    public void NotaDeImpactoNuncaPassaDeCem()
    {
        Assert.InRange(ProgramImpactAnalyzer.CalcularNota(long.MaxValue, 100000), 0, 100);
    }

    [Fact]
    public void RankingDeImpactoDeListaVaziaNaoQuebra()
    {
        var ranking = ProgramImpactAnalyzer.Rank(null);

        Assert.NotNull(ranking);
        Assert.Empty(ranking);
    }

    // ------------------------------------------------------------------
    // Pais
    // ------------------------------------------------------------------

    [Fact]
    public void BandeiraVemDoCodigoIso()
    {
        Assert.Equal("🇧🇷", CountryDetector.BandeiraDe("BR"));
        Assert.Equal("🇺🇸", CountryDetector.BandeiraDe("US"));
        Assert.Equal("🇵🇹", CountryDetector.BandeiraDe("pt"));
        Assert.Equal(string.Empty, CountryDetector.BandeiraDe("BRX"));
        Assert.Equal(string.Empty, CountryDetector.BandeiraDe(""));
    }

    [Fact]
    public void PaisPeloCodigoTemNomeEbandeira()
    {
        var pais = CountryDetector.FromCode("BR");

        Assert.NotNull(pais);
        Assert.Equal("BR", pais!.Code);
        Assert.Contains("Brasil", pais.Name);
        Assert.Equal("🇧🇷", pais.Flag);
        Assert.Contains("Brasil", pais.Label);
    }

    [Fact]
    public void PaisDeCodigoInvalidoFicaNulo()
    {
        Assert.Null(CountryDetector.FromCode("XX"));
        Assert.Null(CountryDetector.FromCode("BRX"));
        Assert.Null(CountryDetector.FromCode(null));
    }

    [Fact]
    public void PaisDeArquivoInexistenteFicaNulo()
    {
        Assert.Null(CountryDetector.Detect(@"C:\caminho\que\não\existe.exe"));
        Assert.Null(CountryDetector.Detect(null));
    }

    // ------------------------------------------------------------------
    // Analise de falhas
    // ------------------------------------------------------------------

    [Fact]
    public void AnaliseDeFalhasNaoQuebraEmMaquinaSemPermissao()
    {
        // Sem permissao no Event Log, a lista fica vazia — nunca lanca.
        var entradas = CrashAnalyzer.Scan(dias: 1, limite: 5);

        Assert.NotNull(entradas);
        Assert.True(entradas.Count <= 5);
        Assert.All(entradas, e => Assert.False(string.IsNullOrWhiteSpace(e.ProgramName)));
    }

    // ------------------------------------------------------------------
    // Mudanca de local
    // ------------------------------------------------------------------

    [Fact]
    public void MudancaDeLocalRejeitaPastaInexistente()
    {
        var programa = new InstalledProgram
        {
            DisplayName = "Programa",
            InstallLocation = @"C:\caminho\que\não\existe",
        };

        var resultado = RelocationManager.Move(programa, @"C:\destino");

        Assert.False(resultado.Success);
        Assert.Contains("nao existe", resultado.Message);
    }

    [Fact]
    public void MudancaDeLocalRejeitaProgramaSemPasta()
    {
        var resultado = RelocationManager.Move(new InstalledProgram { DisplayName = "X" }, @"C:\destino");

        Assert.False(resultado.Success);
        Assert.Contains("pasta de instalacao", resultado.Message);
    }

    [Fact]
    public void MudancaDeLocalMoveEVolta()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "tranal-reloc-" + Guid.NewGuid().ToString("N")[..8]);
        var origem = Path.Combine(raiz, "Programa");
        var destino = Path.Combine(raiz, "NovoDisco");

        try
        {
            Directory.CreateDirectory(origem);
            Directory.CreateDirectory(destino);
            File.WriteAllText(Path.Combine(origem, "app.exe"), "conteudo");

            var programa = new InstalledProgram
            {
                DisplayName = "Programa Teste",
                InstallLocation = origem,
            };

            var movido = RelocationManager.Move(programa, destino);

            Assert.True(movido.Success);
            Assert.True(Directory.Exists(Path.Combine(destino, "Programa")));
            Assert.False(Directory.Exists(origem));
            Assert.True(File.Exists(Path.Combine(destino, "Programa", "app.exe")));

            var devolvido = RelocationManager.Revert("Programa Teste");

            Assert.True(devolvido.Success);
            Assert.True(Directory.Exists(origem));
            Assert.True(File.Exists(Path.Combine(origem, "app.exe")));
        }
        finally
        {
            try
            {
                if (Directory.Exists(raiz)) Directory.Delete(raiz, true);
            }
            catch
            {
                // A pasta temporaria fica para o sistema limpar.
            }
        }
    }

    [Fact]
    public void ReverterSemHistoricoFalaQueNaoHaRegistro()
    {
        var resultado = RelocationManager.Revert("Programa Que Nunca Foi Movido");

        Assert.False(resultado.Success);
        Assert.Contains("Nao ha mudanca registrada", resultado.Message);
    }
}
