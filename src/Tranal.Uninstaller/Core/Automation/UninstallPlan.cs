using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// A natureza de uma etapa do plano de desinstalação.
///
/// O plano é a sequência de trabalho de uma desinstalação: o que roda antes
/// do desinstalador do programa, o próprio desinstalador, a varredura das
/// sobras, a remoção delas, o que roda depois e — no modo manual — a simples
/// listagem do que o usuário vai apagar sozinho.
///
/// O enum existe para que a interface e a CLI falem a mesma língua sobre o
/// andamento: "está na varredura" e "está na remoção" são coisas diferentes,
/// e o usuário precisa saber em qual delas o programa está.
/// </summary>
public enum UninstallStepKind
{
    /// <summary>
    /// Comando do usuário que roda antes do desinstalador nativo.
    /// Serve para derrubar o programa, fechar o banco de dados ou liberar o
    /// arquivo antes que o desinstalador tente apaga-lo.
    /// </summary>
    PreCommand,

    /// <summary>
    /// Execução do desinstalador nativo do programa (o que o Windows
    /// registra em <c>UninstallString</c>).
    /// </summary>
    BuiltIn,

    /// <summary>
    /// Varredura do sistema em busca das sobras que o desinstalador deixou.
    /// </summary>
    Scan,

    /// <summary>
    /// Remoção das sobras marcadas na varredura.
    /// </summary>
    Remove,

    /// <summary>
    /// Comando do usuário que roda depois da desinstalação.
    /// Serve para refazer o que o desinstalador do programa desfez.
    /// </summary>
    PostCommand,

    /// <summary>
    /// Desinstalação manual: o desinstalador nativo é pulado e o Tranal
    /// apenas lista o que o usuário quer apagar por conta própria
    /// (a opção "eu cuido do resto").
    /// </summary>
    Manual,
}

/// <summary>
/// Uma etapa do plano de desinstalação.
///
/// A etapa é <em>dados</em>, não comportamento: ela descreve o que tem de
/// acontecer, e quem executa decide como. É o que permite o mesmo plano
/// rodar na interface e na linha de comando, e o que permite retomá-lo
/// depois de um reinício.
/// </summary>
public sealed class UninstallStep
{
    /// <summary>O tipo do trabalho que a etapa representa.</summary>
    public UninstallStepKind Kind { get; init; }

    /// <summary>
    /// A frase que descreve a etapa para o usuário. Aparece no painel de
    /// progresso e no relatório da operação.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// O comando do usuário, nas etapas de comando (antes e depois).
    /// Vazio nas outras etapas.
    /// </summary>
    public string? Command { get; init; }
}

/// <summary>
/// O plano completo de uma desinstalação.
///
/// Ele é criado antes de qualquer ação e por isso pode ser gravado em disco:
/// é o que permite retomar a operação depois de um reinício sem repetir o
/// que já deu certo.
/// </summary>
public sealed class UninstallPlan
{
    /// <summary>O nome do programa que está sendo desinstalado.</summary>
    public string ProgramName { get; init; } = string.Empty;

    /// <summary>
    /// O modo da desinstalação. Ele decide se o plano varre o sistema depois
    /// do desinstalador nativo — e a decisão mora no enum, não no plano.
    /// </summary>
    public UninstallMode Mode { get; init; }

    /// <summary>As etapas, na ordem em que devem acontecer.</summary>
    public IReadOnlyList<UninstallStep> Steps { get; init; } = Array.Empty<UninstallStep>();

    /// <summary>
    /// A identidade do plano. Serve para reencontrar o plano gravado depois
    /// de um reinício; quando vem vazia, a sessão que o carrega dá o nome.
    /// </summary>
    public string Id { get; init; } = string.Empty;
}

/// <summary>
/// O que o usuário pediu além da desinstalação em si: comandos nas pontas e
/// a opção de cuidar do resto manualmente.
///
/// As opções viajam juntas porque são sempre decididas na mesma tela: quem
/// escolhe "eu cuido do resto" não escolhe um comando para depois.
/// </summary>
public sealed class UninstallPlanOptions
{
    /// <summary>Comando a rodar antes do desinstalador nativo.</summary>
    public string? PreCommand { get; init; }

    /// <summary>Comando a rodar depois da desinstalação.</summary>
    public string? PostCommand { get; init; }

    /// <summary>
    /// Quando verdadeiro, o desinstalador nativo é pulado e o plano apenas
    /// lista o que apagar.
    /// </summary>
    public bool Manual { get; init; }

    /// <summary>
    /// A identidade do plano. Vazio quando o plano não precisa ser
    /// reencontrado em disco.
    /// </summary>
    public string Id { get; init; } = string.Empty;
}

/// <summary>
/// Monta o plano de desinstalação.
///
/// A ordem das etapas é a decisão central do motor: o comando do usuário
/// roda antes (para o desinstalador não encontrar o programa aberto), o
/// desinstalador nativo roda, a varredura vem depois (o log do instalador só
/// existe antes do desinstalador, mas as sobras só aparecem depois dele) e o
/// comando final fecha a operação.
/// </summary>
public static class UninstallPlanBuilder
{
    /// <summary>
    /// De onde vem o comando antes/depois que o usuario cadastrou para o
    /// programa.
    ///
    /// Por padrao, nulo: a suite de testes nao deve ler o arquivo de
    /// preferencias do usuario. O aplicativo aponta este gancho para o
    /// <c>ProgramCustomizationStore</c> no arranque.
    /// </summary>
    public static Func<InstalledProgram, (string? Pre, string? Post)?>? CustomizationProvider { get; set; }

    /// <summary>
    /// Junta as opcoes da chamada com o que o usuario cadastrou para o
    /// programa.
    ///
    /// A precedencia mora aqui, em um lugar so: o que a chamada passa
    /// vence; o que o usuario cadastrou entra na falta.
    /// </summary>
    /// <param name="program">O programa a desinstalar.</param>
    /// <param name="options">As opcoes da chamada.</param>
    public static UninstallPlanOptions ResolveOptions(
        InstalledProgram program, UninstallPlanOptions? options)
    {
        var opts = options ?? new UninstallPlanOptions();

        var provider = CustomizationProvider;
        if (provider is null) return opts;

        (string? Pre, string? Post)? saved;
        try
        {
            saved = provider(program);
        }
        catch
        {
            // A leitura das preferencias nunca derruba a desinstalacao.
            return opts;
        }

        if (saved is null) return opts;

        return new UninstallPlanOptions
        {
            PreCommand = string.IsNullOrWhiteSpace(opts.PreCommand) ? saved.Value.Pre : opts.PreCommand,
            PostCommand = string.IsNullOrWhiteSpace(opts.PostCommand) ? saved.Value.Post : opts.PostCommand,
            Manual = opts.Manual,
        };
    }

    /// <summary>
    /// Monta o plano do programa no modo informado.
    /// </summary>
    /// <param name="program">O programa a desinstalar.</param>
    /// <param name="mode">O modo da desinstalação.</param>
    /// <param name="options">Comandos nas pontas e a opção manual.</param>
    public static UninstallPlan Build(
        InstalledProgram program,
        UninstallMode mode,
        UninstallPlanOptions? options = null)
    {
        var opts = ResolveOptions(program, options);
        var steps = new List<UninstallStep>();
        var name = program.DisplayName.Trim();

        if (!string.IsNullOrWhiteSpace(opts.PreCommand))
        {
            steps.Add(new UninstallStep
            {
                Kind = UninstallStepKind.PreCommand,
                Description = $"Rodar o comando definido antes da desinstalação de {name}.",
                Command = opts.PreCommand.Trim(),
            });
        }

        if (opts.Manual)
        {
            // No modo manual o desinstalador nativo nao roda: ele e quem apaga
            // o programa, e a opcao "eu cuido do resto" significa exatamente
            // isso — o usuario apaga, nao o Tranal.
            steps.Add(new UninstallStep
            {
                Kind = UninstallStepKind.Manual,
                Description = $"Listar o que apagar de {name} sem remover nada.",
            });
        }
        else
        {
            steps.Add(new UninstallStep
            {
                Kind = UninstallStepKind.BuiltIn,
                Description = $"Executar o desinstalador nativo de {name}.",
            });

            // A varredura so existe nos modos que varrem: no modo "Somente",
            // o desinstalador nativo e a desinstalacao inteira.
            if (UninstallModes.Scans(mode))
            {
                steps.Add(new UninstallStep
                {
                    Kind = UninstallStepKind.Scan,
                    Description = $"Procurar as sobras que {name} deixou no sistema.",
                });

                steps.Add(new UninstallStep
                {
                    Kind = UninstallStepKind.Remove,
                    Description = $"Remover as sobras marcadas de {name}.",
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(opts.PostCommand))
        {
            steps.Add(new UninstallStep
            {
                Kind = UninstallStepKind.PostCommand,
                Description = $"Rodar o comando definido depois da desinstalação de {name}.",
                Command = opts.PostCommand.Trim(),
            });
        }

        return new UninstallPlan
        {
            Id = opts.Id,
            ProgramName = name,
            Mode = mode,
            Steps = steps,
        };
    }
}

/// <summary>
/// O resultado de uma etapa.
///
/// A distinção entre "deu certo", "falhou" e "foi pulada" é o que permite
/// que a falha de uma etapa não engula as outras: a etapa que falhou é
/// relatada, e o plano continua.
/// </summary>
public sealed class UninstallStepOutcome
{
    /// <summary>True quando a etapa foi cumprida.</summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// True quando a etapa foi pulada pelo usuário. O passo pulado conta
    /// como concluído: pular de propósito e recomeçar depois não é o mesmo
    /// que falhar.
    /// </summary>
    public bool Skipped { get; init; }

    /// <summary>A explicação do resultado, em português.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Etapa cumprida.</summary>
    public static UninstallStepOutcome Ok(string message = "") => new()
    {
        Succeeded = true,
        Message = message,
    };

    /// <summary>Etapa que falhou.</summary>
    public static UninstallStepOutcome Fail(string message) => new()
    {
        Succeeded = false,
        Message = message,
    };

    /// <summary>Etapa pulada a pedido do usuário.</summary>
    public static UninstallStepOutcome Skip(string message) => new()
    {
        Succeeded = true,
        Skipped = true,
        Message = message,
    };
}

/// <summary>
/// O resultado de uma etapa dentro da execução do plano, com a posição que
/// ela ocupava — é o que o painel de progresso mostra.
/// </summary>
public sealed class UninstallStepResult
{
    /// <summary>A posição da etapa no plano (começa em zero).</summary>
    public int Index { get; init; }

    /// <summary>O tipo da etapa.</summary>
    public UninstallStepKind Kind { get; init; }

    /// <summary>A descrição da etapa.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>True quando a etapa foi cumprida.</summary>
    public bool Succeeded { get; init; }

    /// <summary>True quando a etapa foi pulada.</summary>
    public bool Skipped { get; init; }

    /// <summary>A explicação do resultado.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// O resultado da execução de um plano inteiro.
/// </summary>
public sealed class UninstallRunResult
{
    /// <summary>O resultado de cada etapa executada.</summary>
    public IReadOnlyList<UninstallStepResult> Steps { get; init; } = Array.Empty<UninstallStepResult>();

    /// <summary>
    /// A última etapa concluída com sucesso. É o número que a sessão grava
    /// para que a retomada recomece pela etapa seguinte.
    /// </summary>
    public int LastCompletedStep { get; init; } = -1;

    /// <summary>True quando o usuário pediu para encerrar a operação.</summary>
    public bool Stopped { get; init; }

    /// <summary>True quando alguma etapa falhou.</summary>
    public bool HasFailures => Steps.Any(s => !s.Succeeded);

    /// <summary>Quantas etapas foram executadas.</summary>
    public int ExecutedCount => Steps.Count;
}

/// <summary>
/// Executa as etapas de um plano, uma a uma.
///
/// O executor é genérico de propósito: ele não sabe varre o registro nem
/// apagar arquivos — quem faz isso é o manipulador que o chamador entrega.
/// O que ele sabe, e o que importa, é a ordem, a retomada, o controle do
/// usuário e a regra de que uma etapa que falha não engole as outras.
/// </summary>
public sealed class UninstallPlanExecutor
{
    /// <summary>
    /// O manipulador de uma etapa: recebe a etapa e a posição dela, e devolve
    /// o resultado. É quem sabe fazer o trabalho de verdade.
    /// </summary>
    private readonly Func<UninstallStep, int, CancellationToken, Task<UninstallStepOutcome>> _handler;

    /// <summary>
    /// Cria o executor com o manipulador que faz o trabalho de cada etapa.
    /// </summary>
    /// <param name="handler">O manipulador das etapas.</param>
    public UninstallPlanExecutor(
        Func<UninstallStep, int, CancellationToken, Task<UninstallStepOutcome>> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    /// <summary>
    /// Percorre as etapas do plano, na ordem, gravando o andamento na sessão.
    ///
    /// A retomada é o que faz o item "continuar a desinstalação interrompida"
    /// existir: as etapas anteriores ao último passo concluído **não** são
    /// repetidas, e a que falhou pode ser tentada de novo.
    /// </summary>
    /// <param name="plan">O plano a executar.</param>
    /// <param name="session">A sessão onde o andamento é gravado (opcional).</param>
    /// <param name="progress">O painel de progresso (opcional).</param>
    /// <param name="cancellationToken">Permite cancelar a operação.</param>
    public async Task<UninstallRunResult> RunAsync(
        UninstallPlan plan,
        UninstallSession? session = null,
        RemovalProgress? progress = null,
        CancellationToken cancellationToken = default)
    {
        var steps = plan.Steps;
        var results = new List<UninstallStepResult>();

        var lastCompleted = session?.LastCompletedStep ?? -1;
        var current = session;

        progress?.BeginSteps(steps.Count);

        for (var index = 0; index < steps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Encerrar e o comando do usuario: o laço para aqui, e o que
            // ficou para tras continua valendo para a retomada.
            if (progress?.StopRequested == true) break;

            var step = steps[index];

            // A etapa ja foi concluida em uma execucao anterior (o computador
            // reiniciou no meio): ela nao e repetida.
            if (index <= lastCompleted) continue;

            progress?.BeginStep(index, step.Description);

            UninstallStepOutcome? outcome = null;
            if (progress?.ConsumeSkip() == true)
            {
                // Pular e um comando, nao uma falha: a etapa conta como
                // concluida e o plano segue.
                outcome = UninstallStepOutcome.Skip("Etapa pulada a pedido do usuário.");
            }
            else
            {
                try
                {
                    outcome = await _handler(step, index, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Uma etapa que estoura nao pode engolir as outras: o
                    // plano segue, e a falha aparece no resultado.
                    outcome = UninstallStepOutcome.Fail(ex.Message);
                }
            }

            outcome ??= UninstallStepOutcome.Fail("A etapa não devolveu resultado.");

            results.Add(new UninstallStepResult
            {
                Index = index,
                Kind = step.Kind,
                Description = step.Description,
                Succeeded = outcome.Succeeded,
                Skipped = outcome.Skipped,
                Message = outcome.Message,
            });

            progress?.EndStep(index, step.Description, outcome);

            // O passo so avanca quando a etapa deu certo (ou foi pulada) E
            // quando ela veio logo depois da ultima concluida.
            //
            // A segunda condicao e o que faz o desinstalador que pediu
            // reinicio funcionar: a etapa que falhou continua sendo a
            // primeira a ser tentada de novo na retomada, em vez de ser
            // pulada por causa de uma etapa posterior que deu certo.
            if (outcome.Succeeded && index == lastCompleted + 1)
            {
                lastCompleted = index;
                if (current is not null)
                {
                    current = current.WithCompletedStep(index);
                    UninstallSession.Save(current);
                }
            }
        }

        return new UninstallRunResult
        {
            Steps = results,
            LastCompletedStep = lastCompleted,
            Stopped = progress?.StopRequested == true,
        };
    }
}

/// <summary>
/// Executa o comando do usuário antes ou depois da desinstalação.
///
/// O comando é do usuário e roda pelo <c>cmd.exe</c>, com as permissões do
/// Tranal (que roda elevado). Por isso a regra é dura: o comando **não** é
/// montado por concatenação de valores do Tranal — quem precisar de um valor
/// do sistema dentro do comando usa <see cref="Quote"/>, que escapa aspas e o
/// sinal de porcento.
///
/// O comando em si chega ao <c>cmd.exe</c> como o usuário escreveu. Isso
/// significa que o interpretador continua expandindo <c>%VAR%</c>: quem
/// precisar passar um caminho com variável de ambiente deve escapar o
/// sinal de porcento na mão.
/// </summary>
public static class ShellCommand
{
    /// <summary>
    /// Escapa um valor para ser colocado dentro de um comando do
    /// <c>cmd.exe</c>.
    ///
    /// As aspas duplas fecham o argumento do interpretador: cada uma delas
    /// vira duas. O sinal de porcento é expandido pelo <c>cmd.exe</c> antes do
    /// comando rodar: ele vira dois.
    /// </summary>
    /// <param name="value">O valor a escapar.</param>
    public static string Quote(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length == 0) return "\"\"";

        var escaped = text.Replace("\"", "\"\"").Replace("%", "%%");
        return "\"" + escaped + "\"";
    }

    /// <summary>
    /// Roda o comando do usuário pelo <c>cmd.exe</c> e devolve o código de
    /// saída.
    ///
    /// A execução é silenciosa (sem janela) e espera o comando terminar: o
    /// comando "antes" só faz sentido se o programa realmente já tiver
    /// terminado quando a próxima etapa começar.
    /// </summary>
    /// <param name="command">O comando como o usuário escreveu.</param>
    /// <param name="cancellationToken">Permite cancelar a espera.</param>
    public static async Task<ShellCommandResult> RunAsync(
        string command,
        CancellationToken cancellationToken = default)
    {
        var text = (command ?? string.Empty).Trim();
        if (text.Length == 0)
            return new ShellCommandResult { ExitCode = -1, Message = "o comando está vazio" };

        try
        {
            var info = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            // ArgumentList escapa cada argumento por conta propria: o comando
            // do usuario chega ao cmd.exe sem passar por concatenacao de
            // string de shell.
            info.ArgumentList.Add("/d");
            info.ArgumentList.Add("/s");
            info.ArgumentList.Add("/c");
            info.ArgumentList.Add(text);

            using var process = Process.Start(info);
            if (process is null)
                return new ShellCommandResult { ExitCode = -1, Message = "o processo não pôde ser criado" };

            var saida = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var erro = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return new ShellCommandResult
            {
                ExitCode = process.ExitCode,
                Output = saida.Trim(),
                Error = erro.Trim(),
                Message = process.ExitCode == 0
                    ? "comando concluído"
                    : $"o comando terminou com o código {process.ExitCode}",
            };
        }
        catch (Exception ex)
        {
            return new ShellCommandResult { ExitCode = -1, Message = ex.Message };
        }
    }
}

/// <summary>
/// O resultado de um comando do usuário rodado pelo <c>cmd.exe</c>.
/// </summary>
public sealed class ShellCommandResult
{
    /// <summary>O código de saída do comando.</summary>
    public int ExitCode { get; init; } = -1;

    /// <summary>A saída padrão do comando.</summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>A saída de erro do comando.</summary>
    public string Error { get; init; } = string.Empty;

    /// <summary>A explicação do resultado.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>True quando o comando terminou com sucesso.</summary>
    public bool Succeeded => ExitCode == 0;
}
