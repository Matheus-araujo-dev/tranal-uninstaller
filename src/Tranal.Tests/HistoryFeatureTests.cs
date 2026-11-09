using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.History;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O historico de desinstalacoes: a linha que a tela mostra e o botao que
/// devolve as coisas.
///
/// A lacuna medida era dupla: o historico era gravado e nunca lido (nao
/// havia tela nem comando), e o "Recuperar" nao existia. Estes testes
/// prendem as duas metades.
/// </summary>
public class HistoryFeatureTests : IDisposable
{
    private readonly string _raiz;

    public HistoryFeatureTests()
    {
        _raiz = Path.Combine(Path.GetTempPath(), "tranal-historio-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_raiz);

        UninstallHistory.FilePathOverride = Path.Combine(_raiz, "history.jsonl");
    }

    public void Dispose()
    {
        UninstallHistory.FilePathOverride = null;

        try
        {
            if (Directory.Exists(_raiz)) Directory.Delete(_raiz, true);
        }
        catch (IOException)
        {
            // O temporario do teste nao pode derrubar a suite.
        }
    }

    [Fact]
    public void OHistoricoVazioNaoDevolveLinha()
    {
        Assert.Empty(HistoryReport.Read());
    }

    [Fact]
    public void AEntradaGravadaViraLinhaComDataRelativaEResiduos()
    {
        UninstallHistory.Append(new UninstallHistoryEntry
        {
            Programs = "Programa de Teste",
            Level = "Seguro",
            Removed = 7,
            Failed = 1,
            Uninstalled = true,
            Timestamp = DateTime.Now.AddDays(-2),
            RemovedItems = new List<string> { @"C:\sobra\a.exe", @"C:\sobra\b.exe" },
        });

        var linhas = HistoryReport.Read();
        var linha = Assert.Single(linhas);

        Assert.Equal("Programa de Teste", linha.Programs);
        Assert.Equal("Seguro", linha.Level);
        Assert.Equal(7, linha.Removed);
        Assert.Equal(1, linha.Failed);
        Assert.True(linha.Uninstalled);

        // A coluna de residuos: os itens de sobra que sairam junto.
        Assert.Equal(2, linha.ResidueCount);
        Assert.Contains(@"C:\sobra\a.exe", linha.Residues);

        // A data em tempo relativo: o concorrente mostra "ontem", nao um
        // numero de calendario.
        Assert.False(string.IsNullOrWhiteSpace(linha.When));
        Assert.Contains("atras", linha.When, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UmaLinhaSemBackupNaoSeDizRecuperavel()
    {
        UninstallHistory.Append(new UninstallHistoryEntry { Programs = "Sem Backup" });

        var linha = Assert.Single(HistoryReport.Read());
        Assert.False(linha.Recoverable);
    }

    [Fact]
    public void UmaLinhaComBackupSeDizRecuperavel()
    {
        UninstallHistory.Append(new UninstallHistoryEntry
        {
            Programs = "Com Backup",
            RecoveryId = "backup-123",
        });

        var linha = Assert.Single(HistoryReport.Read());
        Assert.True(linha.Recoverable);
        Assert.Equal("backup-123", linha.RecoveryId);
    }

    [Fact]
    public void RecuperarSemIdentificadorDevolveFalhaHonesta()
    {
        var (ok, mensagem) = HistoryReport.Recover((string?)null);

        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(mensagem));
    }

    [Fact]
    public void RecuperarComIdentificadorDesconhecidoDevolveFalha()
    {
        var (ok, mensagem) = HistoryReport.Recover("backup-que-nao-existe");

        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(mensagem));
    }

    [Fact]
    public void ALinhaRuimNaoEstragaOHistoricoInteiro()
    {
        UninstallHistory.Append(new UninstallHistoryEntry { Programs = "Antes" });
        File.AppendAllText(UninstallHistory.FilePath, "{ linha quebrada" + Environment.NewLine);
        UninstallHistory.Append(new UninstallHistoryEntry { Programs = "Depois" });

        var linhas = HistoryReport.Read();

        Assert.Equal(2, linhas.Count);
        Assert.Equal("Depois", linhas[0].Programs);
        Assert.Equal("Antes", linhas[1].Programs);
    }

    [Fact]
    public void AHistoricoLimpoSome()
    {
        UninstallHistory.Append(new UninstallHistoryEntry { Programs = "Algum" });
        Assert.NotEmpty(HistoryReport.Read());

        Assert.True(UninstallHistory.Clear());
        Assert.Empty(HistoryReport.Read());
    }

    [Fact]
    public void ADataRelativaVemDoNucleoEmUmLugarSo()
    {
        // A data que a tela mostra e a mesma que o nucleo formata: a tela
        // consome o veredito, nao recalcula a data por conta propria.
        var entrada = new UninstallHistoryEntry
        {
            Programs = "Teste",
            Timestamp = new DateTime(2026, 11, 9, 10, 0, 0),
        };

        var linha = Assert.Single(HistoryReport.Build(new[] { entrada }));
        Assert.Equal(linha.When, Tranal.Uninstaller.Core.Modules.RelativeDate.Format(entrada.Timestamp));
        Assert.StartsWith("2026-11-09", linha.WhenExact);
    }
}
