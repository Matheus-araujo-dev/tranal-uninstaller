using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Partida a frio: o que acontece na primeira execucao, sem arquivo de
/// preferencias.
///
/// Medido: sem <c>settings.json</c> o aplicativo NAO abria. Foram dois
/// defeitos de ordenacao, um atras do outro:
///  1. O <c>Checked</c> do <c>RadioButton</c> dispara durante o
///     <c>InitializeComponent</c>, antes de o botao existir — o handler
///     estourava <c>NullReferenceException</c>.
///  2. A janela principal ainda nao tinha sido exibida, e o WPF nao aceita
///     uma janela nao exibida como <c>Owner</c> — o <c>Owner</c> do dialogo
///     de primeiro lancamento estourava <c>InvalidOperationException</c>.
///
/// A suite nao referencia o projeto WPF (o nucleo e compilado tambem na CLI;
/// referenciar os dois criaria dois tipos "Core" diferentes), por isso a
/// verificacao da camada de interface e feita sobre o fonte em disco. O
/// comportamento de ponta a ponta e conferido por
/// <c>tools/test-cold-start.ps1</c>, que sobe o aplicativo de verdade.
/// </summary>
public class ColdStartTests
{
    /// <summary>Le um arquivo do projeto grafico.</summary>
    private static string Read(params string[] parts) => RepoFiles.Read(parts);

    /// <summary>
    /// O dialogo de primeiro lancamento nao pode ter uma janela proprietaria
    /// que ainda nao foi exibida: o WPF recusa isso com
    /// <c>InvalidOperationException</c> e o aplicativo nao abre.
    /// </summary>
    [Fact]
    public void ODialogoDePrimeiroLancamentoNaoUsaUmaJanelaNaoExibidaComoProprietaria()
    {
        var code = Read("src", "Tranal.Uninstaller", "App.xaml.cs");
        var pergunta = RepoFiles.Metodo(code, "PerguntarTemaNoPrimeiroLancamento");

        // A janela principal e criada, mas so e EXIBIDA depois da pergunta de
        // tema. Usa-la como Owner nesse intervalo e o que quebrava a partida a
        // frio.
        Assert.DoesNotContain("Owner", pergunta, StringComparison.Ordinal);
        Assert.Contains("new FirstRunWindow()", pergunta, StringComparison.Ordinal);
    }

    /// <summary>
    /// Durante a pergunta de tema o dialogo e a unica janela do app. Com o
    /// modo "encerrar na ultima janela fechada" o aplicativo terminaria no
    /// fim da pergunta — o modo e trocado durante a pergunta e restaurado
    /// depois.
    /// </summary>
    [Fact]
    public void APerguntaDeTemaNaoEncerraOAppAoFechar()
    {
        var code = Read("src", "Tranal.Uninstaller", "App.xaml.cs");
        var pergunta = RepoFiles.Metodo(code, "PerguntarTemaNoPrimeiroLancamento");

        Assert.Contains("ShutdownMode.OnExplicitShutdown", pergunta, StringComparison.Ordinal);
        Assert.Contains("ShutdownMode = modoAnterior", pergunta, StringComparison.Ordinal);

        // O modo so volta ao normal DEPOIS do dialogo: inverter a ordem
        // devolveria o encerramento indevido.
        var troca = pergunta.IndexOf("ShutdownMode.OnExplicitShutdown", StringComparison.Ordinal);
        var dialogo = pergunta.IndexOf("new FirstRunWindow()", StringComparison.Ordinal);
        var restaura = pergunta.IndexOf("ShutdownMode = modoAnterior", StringComparison.Ordinal);

        Assert.True(troca >= 0 && dialogo >= 0 && restaura >= 0);
        Assert.True(troca < dialogo, "o modo de encerramento precisa ser trocado antes do dialogo");
        Assert.True(dialogo < restaura, "o modo de encerramento precisa ser restaurado depois do dialogo");
    }

    /// <summary>
    /// O tema e aplicado antes de a janela principal ser exibida: assim a
    /// primeira pintura ja sai com o tema escolhido, sem "flash".
    /// </summary>
    [Fact]
    public void OTemaEAplicadoAntesDaPrimeiraPinturaDaJanelaPrincipal()
    {
        var code = Read("src", "Tranal.Uninstaller", "App.xaml.cs");
        var startup = RepoFiles.Metodo(code, "OnStartup");

        var pergunta = startup.IndexOf("PerguntarTemaNoPrimeiroLancamento()", StringComparison.Ordinal);
        var mostra = startup.IndexOf("window.Show()", StringComparison.Ordinal);

        Assert.True(pergunta >= 0, "a pergunta de tema do primeiro lancamento nao acontece");
        Assert.True(mostra >= 0, "a janela principal nao e exibida");
        Assert.True(pergunta < mostra, "o tema precisa ser escolhido antes de a janela principal ser exibida");
    }

    /// <summary>
    /// O <c>Checked</c> do <c>RadioButton</c> dispara durante o
    /// <c>InitializeComponent</c>, antes de o botao existir. Sem a guarda, a
    /// partida a frio terminava em <c>NullReferenceException</c>.
    /// </summary>
    [Fact]
    public void OHandlerDeTemaSobreviveAoDisparoDuranteOCarregamentoDoXaml()
    {
        var code = Read("src", "Tranal.Uninstaller", "Ui", "FirstRunWindow.xaml.cs");
        var handler = RepoFiles.Metodo(code, "ThemeRadio_Changed");

        Assert.Contains("OkButton is null", handler, StringComparison.Ordinal);
    }

    /// <summary>
    /// Sem <c>Owner</c>, o dialogo de primeiro lancamento se centraliza na
    /// tela: <c>CenterOwner</c> nao teria sobre quem se centralizar.
    /// </summary>
    [Fact]
    public void ODialogoDePrimeiroLancamentoSeCentralizaNaTela()
    {
        var xaml = Read("src", "Tranal.Uninstaller", "Ui", "FirstRunWindow.xaml");

        Assert.Contains("WindowStartupLocation=\"CenterScreen\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("WindowStartupLocation=\"CenterOwner\"", xaml, StringComparison.Ordinal);
    }
}
