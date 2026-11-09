using System;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Settings;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O lote 3 das lacunas medidas: o teto de itens do lote, a repeticao do
/// que falhou e o que acontece quando a desinstalacao termina.
///
/// A REGRA mora no nucleo (<see cref="ExecutionPolicy"/>); a janela e a
/// linha de comando consomem — elas nunca montam a delas.
/// </summary>
public class Lote3FeatureTests
{
    private static InstalledProgram Programa(string nome) =>
        new InstalledProgram
        {
            DisplayName = nome,
            UninstallString = nome + ".exe",
        };

    // ---------------------------------------------------------------
    // O teto de lote
    // ---------------------------------------------------------------

    [Fact]
    public void OTetoDeLoteCortaOFimDaFila()
    {
        var programas = Enumerable.Range(1, 5).Select(i => Programa("Programa " + i)).ToList();

        var plano = ExecutionPolicy.ApplyBatchLimit(programas, 2);

        Assert.Equal(2, plano.Accepted.Count);
        Assert.Equal(3, plano.Deferred.Count);
        Assert.True(plano.WasTruncated);

        // O corte e do FIM: os primeiros entram, os ultimos ficam adiados.
        Assert.Equal("Programa 1", plano.Accepted[0].DisplayName);
        Assert.Equal("Programa 3", plano.Deferred[0].DisplayName);
    }

    [Fact]
    public void OTetoDeLoteSemTetoNaoCortaNada()
    {
        var programas = Enumerable.Range(1, 5).Select(i => Programa("Programa " + i)).ToList();

        var plano = ExecutionPolicy.ApplyBatchLimit(programas, ExecutionPolicy.SemTeto);

        Assert.Equal(5, plano.Accepted.Count);
        Assert.Empty(plano.Deferred);
        Assert.False(plano.WasTruncated);
    }

    [Fact]
    public void OTetoDeLoteMaiorQueAListaNaoCortaNada()
    {
        var programas = new[] { Programa("Programa 1"), Programa("Programa 2") };

        var plano = ExecutionPolicy.ApplyBatchLimit(programas, 10);

        Assert.Equal(2, plano.Accepted.Count);
        Assert.False(plano.WasTruncated);
    }

    [Fact]
    public void OTetoDeLoteComListaVaziaNaoFalha()
    {
        var vazio = ExecutionPolicy.ApplyBatchLimit(null, 3);

        Assert.Empty(vazio.Accepted);
        Assert.Empty(vazio.Deferred);
        Assert.False(vazio.WasTruncated);

        var nulo = ExecutionPolicy.ApplyBatchLimit(Array.Empty<InstalledProgram>(), 3);

        Assert.Empty(nulo.Accepted);
        Assert.False(nulo.WasTruncated);
    }

    // ---------------------------------------------------------------
    // A repeticao do que falhou
    // ---------------------------------------------------------------

    [Fact]
    public void OQueFalhouVoltaParaNovaTentativa()
    {
        Assert.True(ExecutionPolicy.ShouldRetry(UninstallQueueOutcome.Failed, 1, 2));
        Assert.True(ExecutionPolicy.ShouldRetry(UninstallQueueOutcome.Failed, 1, 3));
    }

    [Fact]
    public void ARepeticaoParaNoMaximo()
    {
        Assert.False(ExecutionPolicy.ShouldRetry(UninstallQueueOutcome.Failed, 2, 2));
        Assert.False(ExecutionPolicy.ShouldRetry(UninstallQueueOutcome.Failed, 5, 3));
    }

    [Fact]
    public void SemPedidoNaoHaRepeticao()
    {
        Assert.False(ExecutionPolicy.ShouldRetry(UninstallQueueOutcome.Failed, 1, 1));
    }

    [Fact]
    public void OQueSaiuDaTarefaNaoVolta()
    {
        // Saiu da tarefa e um item que o usuario trata: repetir seria rodar
        // o desinstalador de novo sem querer.
        Assert.False(ExecutionPolicy.ShouldRetry(UninstallQueueOutcome.RemovedFromTask, 1, 3));
    }

    [Fact]
    public void OQueConcluiuNaoVolta()
    {
        Assert.False(ExecutionPolicy.ShouldRetry(UninstallQueueOutcome.Completed, 1, 3));
        Assert.False(ExecutionPolicy.ShouldRetry(UninstallQueueOutcome.Skipped, 1, 3));
    }

    // ---------------------------------------------------------------
    // O fim da tarefa
    // ---------------------------------------------------------------

    [Fact]
    public void OReinicioTemPrioridadeSobreOFechamento()
    {
        var acao = ExecutionPolicy.Completion(reiniciar: true, fechar: true, pendente: true);

        Assert.Equal(CompletionAction.RestartMachine, acao);
    }

    [Fact]
    public void SemPendenciaOReinicioNaoAcontece()
    {
        // Sem item pendente, reiniciar a maquina nao adianta.
        var acao = ExecutionPolicy.Completion(reiniciar: true, fechar: true, pendente: false);

        Assert.Equal(CompletionAction.CloseApplication, acao);
    }

    [Fact]
    public void OFechamentoSoAconteceQuandoFoiPedido()
    {
        Assert.Equal(CompletionAction.CloseApplication,
            ExecutionPolicy.Completion(reiniciar: false, fechar: true, pendente: false));

        Assert.Equal(CompletionAction.Nothing,
            ExecutionPolicy.Completion(reiniciar: false, fechar: false, pendente: true));
    }

    [Fact]
    public void AFimUsaAsPreferenciasDoUsuario()
    {
        var preferences = new AppSettings
        {
            RestartWhenFinished = true,
            CloseAppWhenFinished = false,
        };

        Assert.Equal(CompletionAction.RestartMachine, ExecutionPolicy.Completion(preferences, pendente: true));
        Assert.Equal(CompletionAction.Nothing, ExecutionPolicy.Completion(preferences, pendente: false));

        Assert.Equal(CompletionAction.Nothing, ExecutionPolicy.Completion((AppSettings?)null, pendente: true));
    }

    // ---------------------------------------------------------------
    // A fila repete o que falhou
    // ---------------------------------------------------------------

    [Fact]
    public void AFilaTentaDeNovoOQueContinuaInstalado()
    {
        var tentativas = 0;

        var resultado = UninstallQueue.Run(
            new[] { Programa("Programa Teimoso") },
            _ =>
            {
                tentativas++;
                return false;
            },
            _ => true,
            maxAttempts: 3);

        var item = Assert.Single(resultado);

        Assert.Equal(3, tentativas);
        Assert.Equal(3, item.Attempts);

        // Acabadas as tentativas, o programa sai da tarefa para o usuario
        // tratar — como o Revo faz.
        Assert.Equal(UninstallQueueOutcome.RemovedFromTask, item.Outcome);
    }

    [Fact]
    public void AFilaNaoRepeteSemPedido()
    {
        var tentativas = 0;

        var resultado = UninstallQueue.Run(
            new[] { Programa("Programa Teimoso") },
            _ =>
            {
                tentativas++;
                return false;
            },
            _ => true);

        var item = Assert.Single(resultado);

        Assert.Equal(1, tentativas);
        Assert.Equal(1, item.Attempts);
        Assert.Equal(UninstallQueueOutcome.RemovedFromTask, item.Outcome);
    }

    [Fact]
    public void AFilaNaoRepeteOQueSaiuDoSistema()
    {
        var tentativas = 0;

        var resultado = UninstallQueue.Run(
            new[] { Programa("Programa Que Saiu") },
            _ =>
            {
                tentativas++;
                return false;
            },
            _ => false,
            maxAttempts: 3);

        var item = Assert.Single(resultado);

        Assert.Equal(1, tentativas);
        Assert.Equal(UninstallQueueOutcome.Completed, item.Outcome);
    }

    [Fact]
    public void AFilaRepeteOQueLancaExcecao()
    {
        var tentativas = 0;

        var resultado = UninstallQueue.Run(
            new[] { Programa("Programa Que Explode") },
            _ =>
            {
                tentativas++;
                throw new InvalidOperationException("falhou");
            },
            _ => true,
            maxAttempts: 2);

        var item = Assert.Single(resultado);

        Assert.Equal(2, tentativas);
        Assert.Equal(2, item.Attempts);
        Assert.Equal(UninstallQueueOutcome.Failed, item.Outcome);
        Assert.Equal("falhou", item.Detail);
    }

    [Fact]
    public void ADesinstalacaoQueDaCertoNaoERepetida()
    {
        var tentativas = 0;

        var resultado = UninstallQueue.Run(
            new[] { Programa("Programa Bom") },
            _ =>
            {
                tentativas++;
                return true;
            },
            _ => true,
            maxAttempts: 3);

        var item = Assert.Single(resultado);

        Assert.Equal(1, tentativas);
        Assert.Equal(UninstallQueueOutcome.Completed, item.Outcome);
    }
}

/// <summary>
/// O "registro de ultima analise": a interface grafica e a linha de comando
/// gravam pelo mesmo metodo, entao o historico diz a verdade.
/// </summary>
[Collection("preferencias")]
public class Lote3ScanRecordTests : IDisposable
{
    private readonly string _file;

    public Lote3ScanRecordTests()
    {
        _file = Path.Combine(Path.GetTempPath(),
            "tranal-lote3-" + Guid.NewGuid().ToString("N")[..8] + ".json");

        AppSettingsStore.FilePathOverride = _file;
    }

    public void Dispose()
    {
        AppSettingsStore.FilePathOverride = null;

        try { if (File.Exists(_file)) File.Delete(_file); }
        catch { /* o temporario e descartavel */ }
    }

    [Fact]
    public void AVarreduraFicaRegistrada()
    {
        AppSettingsStore.RecordScan(42);

        var loaded = AppSettingsStore.Load();

        Assert.NotNull(loaded.LastScanAt);
        Assert.Equal(42, loaded.LastScanCount);
    }

    [Fact]
    public void AVarreduraSubstituiARegistraAnterior()
    {
        AppSettingsStore.RecordScan(10);
        AppSettingsStore.RecordScan(7);

        var loaded = AppSettingsStore.Load();

        Assert.Equal(7, loaded.LastScanCount);
    }

    [Fact]
    public void ONumeroNegativoNaoViraContagemImpossivel()
    {
        AppSettingsStore.RecordScan(-3);

        var loaded = AppSettingsStore.Load();

        Assert.Equal(0, loaded.LastScanCount);
    }
}
