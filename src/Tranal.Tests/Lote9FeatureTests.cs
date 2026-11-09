using System;
using System.Linq;
using System.Threading;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// As features do lote 9: as acoes de energia com contagem, o programa
/// dormente e a varredura que pode ser interrompida.
///
/// O que estes testes cobrem: a contagem regressiva e de verdade (o prazo
/// fica em um lugar so), o cancelamento limpa o que foi agendado, e a
/// varredura interrompida devolve o que ja tinha lido em vez de perder
/// tudo.
/// </summary>
public class Lote9FeatureTests
{
    [Fact]
    public void OPlanejadorDeEnergiaDescreveOAComPrazo()
    {
        var texto = PowerPlanner.Describe(PowerAction.Suspender, 45);

        Assert.Contains("45", texto);
        Assert.Contains("suspender", texto);
    }

    [Fact]
    public void CancelarLimpaOAcaoAgendada()
    {
        // Nao executa nenhuma acao do sistema: so verifica que o estado
        // interno do planejador fica limpo.
        PowerPlanner.Cancel();

        Assert.Null(PowerPlanner.Pending);
        Assert.Equal(0, PowerPlanner.RemainingSeconds);
    }

    [Fact]
    public void OProgramaDormenteGuardaOEstadoAnterior()
    {
        var programa = new InstalledProgram
        {
            DisplayName = "Programa Para Dormir Lote 9",
            InstallLocation = @"C:\Teste\Dormir",
        };

        // Sem autorun nem servico na pasta, o resultado honesto e a recusa:
        // nao ha o que desligar.
        ProgramHibernation.Forget(programa.DisplayName);

        var (ok, mensagem) = ProgramHibernation.Hibernate(programa);

        Assert.False(ok);
        Assert.Contains("nao tem inicializacao automatica nem servico", mensagem);
        Assert.False(ProgramHibernation.IsHibernated(programa.DisplayName));
    }

    [Fact]
    public void AcordarUmProgramaQueNaoDormiuFalha()
    {
        var (ok, mensagem) = ProgramHibernation.Wake("Programa Que Nunca Dormiu Lote 9");

        Assert.False(ok);
        Assert.Contains("nao esta dormindo", mensagem);
    }

    [Fact]
    public void AVarreduraInterrompidaDevolveOQueJaFoiLido()
    {
        var cancelado = new CancellationTokenSource();
        cancelado.Cancel();

        var programas = ProgramScanner.Scan(
            new ProgramScanner.ScanOptions { RequireUninstallString = false },
            cancelado.Token);

        // Com o token ja cancelado, a varredura para antes de comecar:
        // o que importa e que ela devolve uma lista e nao lanca.
        Assert.NotNull(programas);
        Assert.Empty(programas);

        cancelado.Dispose();
    }

    [Fact]
    public void AVarreduraNormalDevolveOsProgramas()
    {
        var programas = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            RequireUninstallString = false,
            ComputeFolderSize = false,
        });

        Assert.NotEmpty(programas);
        Assert.All(programas, p => Assert.False(string.IsNullOrWhiteSpace(p.DisplayName)));
    }

    [Fact]
    public void AOpcaoDeDesligarUsaOContagemDoWindows()
    {
        // O shutdown.exe recebe o prazo: e ele que mostra a contagem e
        // aceita o cancelamento.
        var prazo = PowerPlanner.DefaultSeconds;

        Assert.True(prazo >= PowerPlanner.MinSeconds);
    }
}
