using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O motor de desinstalação e a automação: o plano de etapas, o executor,
/// a retomada depois de um reinício, a lista de desinstalação em arquivo, a
/// seleção por palavra-chave e o painel de progresso.
///
/// Estes testes não tocam no computador de quem os roda: o executor recebe
/// um manipulador de etapas de mentira, a sessão grava num diretório
/// temporário e a varredura só olha uma pasta criada para o teste.
/// </summary>
public class AutomationTests
{
    /// <summary>
    /// Um programa de mentira, com o nome que a varredura vai procurar.
    /// </summary>
    private static InstalledProgram Programa(string nome) => new()
    {
        DisplayName = nome,
    };

    /// <summary>
    /// O plano tem de sair na ordem: comando do usuário, desinstalador
    /// nativo, varredura, remoção e comando final.
    ///
    /// A ordem não é detalhe: o comando antes existe para o desinstalador
    /// não encontrar o programa aberto, e a varredura só existe depois que o
    /// desinstalador rodou.
    /// </summary>
    [Fact]
    public void OPlanoMontaAsEtapasNaOrdem()
    {
        var plan = UninstallPlanBuilder.Build(
            Programa("Exemplo"),
            UninstallMode.Moderate,
            new UninstallPlanOptions
            {
                PreCommand = "echo antes",
                PostCommand = "echo depois",
            });

        Assert.Equal(
            new[]
            {
                UninstallStepKind.PreCommand,
                UninstallStepKind.BuiltIn,
                UninstallStepKind.Scan,
                UninstallStepKind.Remove,
                UninstallStepKind.PostCommand,
            },
            plan.Steps.Select(s => s.Kind).ToArray());

        Assert.Equal("Exemplo", plan.ProgramName);
        Assert.Equal(UninstallMode.Moderate, plan.Mode);
    }

    /// <summary>
    /// No modo "Somente" não há varredura nem remoção: o desinstalador
    /// nativo é a desinstalação inteira.
    /// </summary>
    [Fact]
    public void OPlanoDoModoSomenteNaoTemVarreduraNemRemocao()
    {
        var plan = UninstallPlanBuilder.Build(Programa("Exemplo"), UninstallMode.BuiltIn);

        Assert.Equal(
            new[] { UninstallStepKind.BuiltIn },
            plan.Steps.Select(s => s.Kind).ToArray());
    }

    /// <summary>
    /// Na desinstalação manual o desinstalador nativo não roda: a opção é
    /// "eu cuido do resto", e o Tranal só lista o que apagar.
    /// </summary>
    [Fact]
    public void OPlanoManualNaoRodaODesinstaladorNativo()
    {
        var plan = UninstallPlanBuilder.Build(
            Programa("Exemplo"),
            UninstallMode.Advanced,
            new UninstallPlanOptions { Manual = true });

        Assert.Equal(
            new[] { UninstallStepKind.Manual },
            plan.Steps.Select(s => s.Kind).ToArray());
    }

    /// <summary>
    /// O executor percorre todas as etapas, na ordem do plano.
    /// </summary>
    [Fact]
    public async Task OExecutorPercorreTodasAsEtapas()
    {
        var plan = UninstallPlanBuilder.Build(
            Programa("Exemplo"),
            UninstallMode.Advanced,
            new UninstallPlanOptions { PreCommand = "echo antes", PostCommand = "echo depois" });

        var executadas = new List<int>();
        var executor = new UninstallPlanExecutor((step, index, token) =>
        {
            executadas.Add(index);
            return Task.FromResult(UninstallStepOutcome.Ok());
        });

        var result = await executor.RunAsync(plan);

        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, executadas);
        Assert.Equal(plan.Steps.Count, result.ExecutedCount);
        Assert.False(result.HasFailures);
    }

    /// <summary>
    /// Uma etapa que falha não pode engolir as outras: o plano continua e a
    /// falha aparece no resultado.
    /// </summary>
    [Fact]
    public async Task UmaEtapaQueFalhaNaoImpedeAsSeguintes()
    {
        var plan = UninstallPlanBuilder.Build(Programa("Exemplo"), UninstallMode.Advanced);

        var executadas = new List<int>();
        var executor = new UninstallPlanExecutor((step, index, token) =>
        {
            executadas.Add(index);
            var outcome = index == 1
                ? UninstallStepOutcome.Fail("o desinstalador nativo recusou")
                : UninstallStepOutcome.Ok();

            return Task.FromResult(outcome);
        });

        var result = await executor.RunAsync(plan);

        Assert.Equal(new[] { 0, 1, 2 }, executadas);
        Assert.True(result.HasFailures);
        Assert.Equal(0, result.LastCompletedStep);
        Assert.Contains(result.Steps, s => !s.Succeeded && s.Message.Contains("recusou"));
    }

    /// <summary>
    /// Uma etapa que estoura não derruba a execução: o erro vira o
    /// resultado da etapa, e o plano segue.
    /// </summary>
    [Fact]
    public async Task UmaEtapaQueEstouraNaoDerrubaAExecucao()
    {
        var plan = UninstallPlanBuilder.Build(Programa("Exemplo"), UninstallMode.Advanced);

        var executadas = new List<int>();
        var executor = new UninstallPlanExecutor((step, index, token) =>
        {
            executadas.Add(index);
            if (index == 0) throw new InvalidOperationException("falhou por dentro");
            return Task.FromResult(UninstallStepOutcome.Ok());
        });

        var result = await executor.RunAsync(plan);

        Assert.Equal(new[] { 0, 1, 2 }, executadas);
        Assert.Contains(result.Steps, s => !s.Succeeded && s.Message.Contains("falhou por dentro"));
    }

    /// <summary>
    /// O comando do usuário roda pelo cmd.exe e devolve o código de saída.
    ///
    /// O comando de teste é o "echo": não instala, não desinstala e não
    /// mexe em nada do computador de quem roda a suíte.
    /// </summary>
    [Fact]
    public async Task OComandoDoUsuarioRodaPeloCmdDevolvendoOCodigoDeSaida()
    {
        var result = await ShellCommand.RunAsync("echo tranal");

        Assert.True(result.Succeeded, result.Message);
        Assert.Contains("tranal", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// O valor interpolado num comando tem de ser escapado: o caminho com
    /// espaço não pode virar dois argumentos.
    /// </summary>
    [Fact]
    public void OValorInterpoladoNoComandoEEscapado()
    {
        var citado = ShellCommand.Quote(@"C:\Program Files\App");

        Assert.StartsWith("\"", citado, StringComparison.Ordinal);
        Assert.EndsWith("\"", citado, StringComparison.Ordinal);
    }

    /// <summary>
    /// A seleção por regex casa com o nome do programa e devolve só os que
    /// casam.
    /// </summary>
    [Fact]
    public void OFiltroPorRegexCasaComONomeDoPrograma()
    {
        var leitura = BatchList.Parse("regex:^Adobe.*");

        Assert.True(leitura.Success, leitura.ErrorMessage);

        var programas = new[]
        {
            Programa("Adobe Reader"),
            Programa("Foxit Reader"),
            Programa("Adobe Premiere"),
        };

        var selecionados = leitura.List!.Match(programas);

        Assert.Equal(2, selecionados.Count);
        Assert.Contains(selecionados, p => p.DisplayName == "Adobe Reader");
        Assert.DoesNotContain(selecionados, p => p.DisplayName == "Foxit Reader");
    }

    /// <summary>
    /// A regex é insensível a maiúsculas: quem escreve "adobe" na lista
    /// quer o programa "Adobe".
    /// </summary>
    [Fact]
    public void OFiltroPorRegexIgnoraMaiusculasEMinusculas()
    {
        var leitura = BatchList.Parse("regex:^adobe reader$");

        Assert.True(leitura.Success, leitura.ErrorMessage);
        Assert.True(leitura.List!.Matches("Adobe Reader"));
    }

    /// <summary>
    /// Regex inválida é erro do usuário: tem de chegar como mensagem clara,
    /// com a linha do problema — e não como exceção crua.
    /// </summary>
    [Fact]
    public void AExpressaoRegularInvalidaERecebidaComMensagem()
    {
        var leitura = BatchList.Parse("regex:[");

        Assert.False(leitura.Success);
        Assert.Null(leitura.List);
        Assert.Contains("expressão regular inválida", leitura.ErrorMessage);
        Assert.Equal(1, leitura.LineNumber);
    }

    /// <summary>
    /// A lista vem de um arquivo em disco (é ela que torna a desinstalação
    /// em lote roteirizável) e casa contra o nome do programa.
    /// </summary>
    [Fact]
    public void AListaEmArquivoCasaComONomeDoPrograma()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "tranal-lote-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(pasta);
        var arquivo = Path.Combine(pasta, "lista.txt");

        try
        {
            File.WriteAllText(arquivo, "#commentario\nAdobe Reader\nregex:^Foxit.*\n");

            var leitura = BatchList.Load(arquivo);

            Assert.True(leitura.Success, leitura.ErrorMessage);
            Assert.Equal(2, leitura.List!.Patterns.Count);

            var selecionados = leitura.List.Match(new[]
            {
                Programa("Adobe Reader"),
                Programa("Foxit Reader"),
                Programa("Notepad++"),
            });

            Assert.Equal(2, selecionados.Count);
            Assert.DoesNotContain(selecionados, p => p.DisplayName == "Notepad++");
        }
        finally
        {
            try { Directory.Delete(pasta, true); }
            catch { /* a pasta é temporária */ }
        }
    }

    /// <summary>
    /// O casamento por palavra-chave é por palavra inteira: "Adobe" não é
    /// "AdobeFresco".
    /// </summary>
    [Fact]
    public void OCasamentoPorPalavraChaveNaoCasaPorSubstring()
    {
        Assert.False(ForcedRemovalOptions.MatchesKeyword("AdobeFresco", "Adobe"));
        Assert.True(ForcedRemovalOptions.MatchesKeyword("Adobe Reader", "Adobe"));
        Assert.True(ForcedRemovalOptions.MatchesKeyword("Reader Adobe", "Adobe"));
    }

    /// <summary>
    /// A seleção por palavra-chave devolve os programas escolhidos.
    /// </summary>
    [Fact]
    public void ASelecaoPorPalavraChaveDevolveOsProgramasEscolhidos()
    {
        var programas = new[]
        {
            Programa("Adobe Reader"),
            Programa("AdobeFresco"),
           Programa("Notepad++"),
        };

        var selecionados = ForcedRemovalOptions.SelectByKeyword(programas, "Adobe");

        Assert.Single(selecionados);
        Assert.Equal("Adobe Reader", selecionados[0].DisplayName);
    }

    /// <summary>
    /// O switch de CLI por ferramenta resolve o nome curto, o nome longo e a
    /// forma longa com o sufixo RUN.
    /// </summary>
    [Fact]
    public void OSwitchDeCliResolveONomeCurtoEOLongoComRun()
    {
        Assert.True(CliToolSwitchCatalog.TryResolve("-j", out var curto, out var runCurto, out var erroCurto),
            erroCurto);
        Assert.Equal(ToolCatalog.JunkCleaner, curto!.ToolId);
        Assert.False(runCurto);

        Assert.True(CliToolSwitchCatalog.TryResolve("--temporarios", out var longo, out var runLongo, out var erroLongo),
            erroLongo);
        Assert.Equal(ToolCatalog.JunkCleaner, longo!.ToolId);
        Assert.False(runLongo);

        Assert.True(CliToolSwitchCatalog.TryResolve("--temporarios-run", out var run, out var executar, out var erroRun),
            erroRun);
        Assert.Equal(ToolCatalog.JunkCleaner, run!.ToolId);
        Assert.True(executar);

        // A forma "--temporarios RUN" chega em dois pedaços.
        Assert.True(CliToolSwitchCatalog.TryResolve("--temporarios RUN", out var runSeparado, out var executarSeparado, out _));
        Assert.Equal(ToolCatalog.JunkCleaner, runSeparado!.ToolId);
        Assert.True(executarSeparado);
    }

    /// <summary>
    /// Ferramenta desconhecida é erro de digitação: a mensagem diz o que
    /// existe, em vez de estourar.
    /// </summary>
    [Fact]
    public void OSwitchDeCliDesconhecidoDevolveErroClaro()
    {
        Assert.False(CliToolSwitchCatalog.TryResolve("--nao-existe", out var tool, out _, out var erro));

        Assert.Null(tool);
        Assert.Contains("desconhecida", erro);
        Assert.Contains("--temporarios", erro);
    }

    /// <summary>
    /// Os switches do catálogo são únicos: dois switches com o mesmo nome
    /// fariam a linha de comando escolher a ferramenta errada.
    /// </summary>
    [Fact]
    public void OsSwitchesDoCatalogoSaoUnicos()
    {
        var curtos = CliToolSwitchCatalog.All.Select(t => t.ShortName).ToList();
        var longos = CliToolSwitchCatalog.All.Select(t => t.LongNameOnly).ToList();

        Assert.Equal(curtos.Count, curtos.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(longos.Count, longos.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// O painel emite os eventos na ordem em que as coisas aconteceram.
    /// </summary>
    [Fact]
    public void OProgressoEmiteOsEventosNaOrdem()
    {
        var progress = new RemovalProgress();
        var eventos = new List<string>();
        progress.Updated += update => eventos.Add(update.Message);

        progress.BeginSteps(2);
        progress.BeginStep(0, "Primeira etapa");
        progress.ReportItem(0, @"C:\Temp\arquivo", 3);
        progress.EndStep(0, "Primeira etapa", UninstallStepOutcome.Ok());
        progress.BeginStep(1, "Segunda etapa");

        Assert.Equal(5, eventos.Count);
        Assert.Contains("Primeira etapa", eventos[1]);
        Assert.Contains("arquivo", eventos[2]);
        Assert.Contains("Primeira etapa", progress.Log[1]);
        Assert.Contains("arquivo", progress.Log[2]);
    }

    /// <summary>
    /// "Encerrar" para o laço de execução: as etapas depois da que estava
    /// em andamento não rodam.
    /// </summary>
    [Fact]
    public async Task OEncerrarParaOLacoDeExecucao()
    {
        var plan = UninstallPlanBuilder.Build(Programa("Exemplo"), UninstallMode.Advanced);

        var progress = new RemovalProgress();
        var executadas = new List<int>();

        var executor = new UninstallPlanExecutor((step, index, token) =>
        {
            executadas.Add(index);
            if (index == 1) progress.RequestStop();
            return Task.FromResult(UninstallStepOutcome.Ok());
        });

        var result = await executor.RunAsync(plan, null, progress);

        Assert.Equal(new[] { 0, 1 }, executadas);
        Assert.True(result.Stopped);
    }

    /// <summary>
    /// "Pular" faz o executor pular a etapa que vem a seguir e seguir em
    /// frente: a etapa em andamento termina, a próxima é deixada de fora.
    /// </summary>
    [Fact]
    public async Task OPularPulaEtapaESegueEmFrente()
    {
        var plan = UninstallPlanBuilder.Build(Programa("Exemplo"), UninstallMode.Advanced);

        var progress = new RemovalProgress();
        var executadas = new List<int>();

        var executor = new UninstallPlanExecutor((step, index, token) =>
        {
            executadas.Add(index);
            if (index == 0) progress.RequestSkip();
            return Task.FromResult(UninstallStepOutcome.Ok());
        });

        var result = await executor.RunAsync(plan, null, progress);

        Assert.Equal(new[] { 0, 2 }, executadas);
        Assert.Contains(result.Steps, s => s.Index == 1 && s.Skipped);
        Assert.DoesNotContain(result.Steps, s => s.Index == 0 && s.Skipped);
    }

    /// <summary>
    /// "Rodar agora" é o comando que tira da fila do próximo reinício o
    /// que estava adiado.
    /// </summary>
    [Fact]
    public void ORodarAgoraMarcaOQueEstavaAdiado()
    {
        var progress = new RemovalProgress();

        Assert.False(progress.RunNowRequested);
        progress.RequestRunNow();
        Assert.True(progress.RunNowRequested);
    }

    /// <summary>
    /// "Rodar agora" muda a opção que adia a remoção para o próximo
    /// reinício: o item em uso é tentado agora, e não no próximo boot.
    /// </summary>
    [Fact]
    public void ORodarAgoraTiraAdiadoDoProximoReinicio()
    {
        var progress = new RemovalProgress();
        var options = new UninstallRemovalOptions { DeferToNextRestart = true };

        Assert.True(progress.ApplyRunNow(options).DeferToNextRestart);

        progress.RequestRunNow();

        Assert.False(progress.ApplyRunNow(options).DeferToNextRestart);
    }

    /// <summary>
    /// A desinstalação manual não remove nada: ela lista o que sobrou do
    /// programa, com a confiança de cada item, e a lista pode ser exportada
    /// para o usuário apagar por conta própria.
    /// </summary>
    [Fact]
    public void ADesinstalacaoManualNaoRemoveEExportaALista()
    {
        var pasta = Path.Combine(
            Path.GetTempPath(),
            "Alvo Tranal " + Guid.NewGuid().ToString("N")[..8]);

        Directory.CreateDirectory(pasta);
        var arquivo = Path.Combine(pasta, "programa.exe");
        File.WriteAllText(arquivo, "conteudo");

        try
        {
            var program = new InstalledProgram
            {
                DisplayName = "Alvo Tranal",
                InstallLocation = pasta,
            };

            var candidatos = ManualRemoval.Candidates(program, new LeftoverScanOptions
            {
                Mode = UninstallMode.Advanced,
            });

            // A pasta de instalacao e sobra de verdade do programa.
            Assert.Contains(candidatos, i =>
                string.Equals(i.Path, pasta, StringComparison.OrdinalIgnoreCase));

            // Nada foi removido: a opcao e "eu cuido do resto".
            Assert.True(Directory.Exists(pasta));
            Assert.True(File.Exists(arquivo));

            // E a lista pode ser exportada.
            var exportada = ManualRemoval.Export(candidatos);
            Assert.Contains("Tipo;Caminho", exportada);
            Assert.Contains(pasta, exportada);
        }
        finally
        {
            try { Directory.Delete(pasta, true); }
            catch { /* a pasta é temporária */ }
        }
    }
}

/// <summary>
/// A retomada da desinstalação interrompida.
///
/// A sessão grava em uma pasta temporária — a suíte não mexe no
/// %LOCALAPPDATA% de quem está rodando os testes.
/// </summary>
public class AutomationSessionTests : IDisposable
{
    private readonly string _pasta;
    private readonly string? _original;

    /// <summary>
    /// Prepara a pasta temporária das sessões.
    /// </summary>
    public AutomationSessionTests()
    {
        _original = UninstallSession.DirectoryOverride;
        _pasta = Path.Combine(Path.GetTempPath(), "tranal-sessoes-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_pasta);
        UninstallSession.DirectoryOverride = _pasta;
    }

    /// <summary>
    /// Devolve a pasta das sessões ao lugar original e apaga a de teste.
    /// </summary>
    public void Dispose()
    {
        UninstallSession.DirectoryOverride = _original;

        try { if (Directory.Exists(_pasta)) Directory.Delete(_pasta, true); }
        catch { /* a pasta é temporária */ }
    }

    /// <summary>
    /// Monta um plano de três etapas (desinstalador, varredura, remoção).
    /// </summary>
    private static UninstallPlan Plan() =>
        UninstallPlanBuilder.Build(
            new InstalledProgram { DisplayName = "Exemplo" },
            UninstallMode.Moderate);

    /// <summary>
    /// A sessão grava o passo concluído em disco, a cada etapa.
    /// </summary>
    [Fact]
    public async Task ASessaoGravaOPassoConcluido()
    {
        var plan = Plan();
        var session = UninstallSession.Start(plan);

        Assert.True(File.Exists(session.FilePath) == false);

        var executor = new UninstallPlanExecutor((step, index, token) =>
            Task.FromResult(UninstallStepOutcome.Ok()));

        await executor.RunAsync(plan, session);

        Assert.True(File.Exists(session.FilePath));

        var gravada = UninstallSession.Load(session.Id);
        Assert.NotNull(gravada);
        Assert.Equal(plan.Steps.Count - 1, gravada!.LastCompletedStep);
        Assert.Equal(plan.Steps.Count, gravada.Plan.Steps.Count);
    }

    /// <summary>
    /// A retomada começa pelo passo seguinte e não repete o que já deu
    /// certo: é o que promete o item "continuar a desinstalação interrompida
    /// por reinício".
    /// </summary>
    [Fact]
    public async Task ARetomadaComecaPeloPassoSeguinte()
    {
        var plan = Plan();
        var session = UninstallSession.Start(plan);

        // Primeira execução: a primeira etapa acontece e a segunda é
        // interrompida — o computador reinicia no meio dela.
        var progress = new RemovalProgress();
        var primeira = new UninstallPlanExecutor((step, index, token) =>
        {
            if (index == 1)
            {
                progress.RequestStop();
                return Task.FromResult(UninstallStepOutcome.Fail("o computador reiniciou"));
            }

            return Task.FromResult(UninstallStepOutcome.Ok());
        });

        await primeira.RunAsync(plan, session, progress);

        var gravada = UninstallSession.Load(session.Id);
        Assert.NotNull(gravada);
        Assert.Equal(0, gravada!.LastCompletedStep);

        // Retomada: a etapa zero não pode rodar de novo.
        var retomadas = new List<int>();
        var segunda = new UninstallPlanExecutor((step, index, token) =>
        {
            retomadas.Add(index);
            return Task.FromResult(UninstallStepOutcome.Ok());
        });

        await segunda.RunAsync(plan, gravada);

        Assert.Equal(new[] { 1, 2 }, retomadas);
    }

    /// <summary>
    /// A sessão pendente aparece na listagem — e uma sessão concluída
    /// some dela.
    /// </summary>
    [Fact]
    public void ASessaoPendenteApareceNaListagem()
    {
        var plan = Plan();
        var session = UninstallSession.Start(plan);
        UninstallSession.Save(session);

        Assert.Contains(UninstallSession.Pending(), s => s.Id == session.Id);

        UninstallSession.Save(session.WithCompletedStep(plan.Steps.Count - 1));

        Assert.DoesNotContain(UninstallSession.Pending(), s => s.Id == session.Id);
    }

    /// <summary>
    /// Apagar a sessão tira o arquivo do disco.
    /// </summary>
    [Fact]
    public void ApagarASessaoTiraOArquivoDoDisco()
    {
        var session = UninstallSession.Start(Plan());
        UninstallSession.Save(session);

        Assert.True(File.Exists(session.FilePath));
        Assert.True(UninstallSession.Delete(session.Id));
        Assert.False(File.Exists(session.FilePath));
    }
}
