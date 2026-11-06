namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// O que o usuário mandou fazer enquanto a operação está rodando.
///
/// São três comandos, e nenhum deles é um botão de "cancelar": pular é
/// "esta etapa eu não quero agora", encerrar é "chega de fazer isso" e
/// rodar agora é "não espera o próximo reinício".
/// </summary>
public enum ProgressCommand
{
    /// <summary>Pular a etapa que está em andamento.</summary>
    Skip,

    /// <summary>Encerrar a operação.</summary>
    Stop,

    /// <summary>
    /// Rodar agora o que estava adiado para o próximo reinício.
    /// </summary>
    RunNow,
}

/// <summary>
/// Uma fotografia do andamento da operação.
///
/// É um retrato, não um estado: quem recebe o evento sabe o que está
/// acontecendo agora, e o modelo continua guardando o estado completo.
/// </summary>
public sealed class ProgressUpdate
{
    /// <summary>A posição da etapa atual (começa em zero).</summary>
    public int StepIndex { get; init; }

    /// <summary>Quantas etapas o plano tem.</summary>
    public int TotalSteps { get; init; }

    /// <summary>A descrição da etapa atual.</summary>
    public string StepDescription { get; init; } = string.Empty;

    /// <summary>
    /// A posição do item atual dentro da etapa. Zero quando a etapa não
    /// trabalha item a item.
    /// </summary>
    public int ItemIndex { get; init; }

    /// <summary>Quantos itens a etapa tem para tratar.</summary>
    public int TotalItems { get; init; }

    /// <summary>O nome do item atual (o caminho do que está sendo tratado).</summary>
    public string? ItemName { get; init; }

    /// <summary>A frase que explica o que está acontecendo.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// O comando do usuário que deu origem a esta atualização, quando a
    /// atualização veio de um comando.
    /// </summary>
    public ProgressCommand? Command { get; init; }
}

/// <summary>
/// O modelo do painel de progresso.
///
/// Ele é testável sem interface: quem quiser assisti-lo (a janela de
/// automação, a linha de comando, um log) só precisa assinar o evento
/// <see cref="Updated"/>. E quem estiver executando consulta o estado a
/// cada etapa para saber se o usuário mandou pular ou encerrar.
///
/// O modelo não decide nada sobre a remoção: ele informa. A decisão de pular
/// uma etapa, encerrar ou rodar o adiado é do executor e do manipulador de
/// cada etapa.
/// </summary>
public sealed class RemovalProgress
{
    private readonly List<string> _log = new();

    /// <summary>
    /// Disparado a cada mudança de andamento: etapa que começou, item que
    /// mudou, etapa que terminou, comando do usuário.
    /// </summary>
    public event Action<ProgressUpdate>? Updated;

    /// <summary>
    /// Disparado quando o usuário emite um comando (pular, encerrar, rodar
    /// agora).
    /// </summary>
    public event Action<ProgressCommand>? CommandIssued;

    /// <summary>Quantas etapas o plano tem.</summary>
    public int TotalSteps { get; private set; }

    /// <summary>A posição da etapa atual (menos um antes de começar).</summary>
    public int CurrentStep { get; private set; } = -1;

    /// <summary>A descrição da etapa atual.</summary>
    public string CurrentDescription { get; private set; } = string.Empty;

    /// <summary>Quantos itens a etapa atual tem para tratar.</summary>
    public int TotalItems { get; private set; }

    /// <summary>A posição do item atual.</summary>
    public int CurrentItem { get; private set; }

    /// <summary>O nome do item atual.</summary>
    public string? CurrentItemName { get; private set; }

    /// <summary>True quando o usuário mandou pular a etapa atual.</summary>
    public bool SkipRequested { get; private set; }

    /// <summary>True quando o usuário mandou encerrar a operação.</summary>
    public bool StopRequested { get; private set; }

    /// <summary>
    /// True quando o usuário mandou rodar agora o que estava adiado para o
    /// próximo reinício.
    /// </summary>
    public bool RunNowRequested { get; private set; }

    /// <summary>
    /// O log do andamento, na ordem em que as coisas aconteceram.
    ///
    /// É o que a linha de comando mostra e o que um teste confere: a ordem
    /// das linhas é a ordem real das coisas.
    /// </summary>
    public IReadOnlyList<string> Log => _log;

    /// <summary>
    /// Informa quantas etapas o plano tem. Deve ser chamada antes da
    /// primeira etapa, para que o painel saiba o total.
    /// </summary>
    /// <param name="total">O número de etapas.</param>
    public void BeginSteps(int total)
    {
        TotalSteps = total;
        CurrentStep = -1;
        CurrentDescription = string.Empty;
        TotalItems = 0;
        CurrentItem = 0;
        CurrentItemName = null;

        Write($"Início: {total} etapa(s).");
        Publish(new ProgressUpdate
        {
            TotalSteps = total,
            Message = $"Início: {total} etapa(s).",
        });
    }

    /// <summary>
    /// Informa que uma etapa começou.
    /// </summary>
    /// <param name="index">A posição da etapa.</param>
    /// <param name="description">A descrição da etapa.</param>
    public void BeginStep(int index, string description)
    {
        CurrentStep = index;
        CurrentDescription = description;
        CurrentItem = 0;
        CurrentItemName = null;
        TotalItems = 0;

        Write($"Etapa {index + 1}/{TotalSteps}: {description}");
        Publish(new ProgressUpdate
        {
            StepIndex = index,
            TotalSteps = TotalSteps,
            StepDescription = description,
            Message = description,
        });
    }

    /// <summary>
    /// Informa qual item da etapa está sendo tratado agora.
    /// </summary>
    /// <param name="index">A posição do item.</param>
    /// <param name="name">O nome do item (o caminho, na maioria das vezes).</param>
    /// <param name="total">Quantos itens a etapa tem.</param>
    public void ReportItem(int index, string? name, int total)
    {
        CurrentItem = index;
        CurrentItemName = name;
        TotalItems = total;

        var message = total > 0
            ? $"Item {index + 1}/{total}: {name}"
            : $"Item {index + 1}: {name}";

        Write(message);
        Publish(new ProgressUpdate
        {
            StepIndex = CurrentStep,
            TotalSteps = TotalSteps,
            StepDescription = CurrentDescription,
            ItemIndex = index,
            TotalItems = total,
            ItemName = name,
            Message = message,
        });
    }

    /// <summary>
    /// Informa que uma etapa terminou, com o resultado dela.
    /// </summary>
    /// <param name="index">A posição da etapa.</param>
    /// <param name="description">A descrição da etapa.</param>
    /// <param name="outcome">O resultado da etapa.</param>
    public void EndStep(int index, string description, UninstallStepOutcome outcome)
    {
        var situacao = outcome.Skipped ? "pulada" : outcome.Succeeded ? "concluída" : "falhou";
        var texto = outcome.Message.Length > 0 ? $" — {outcome.Message}" : string.Empty;

        Write($"Etapa {index + 1}/{TotalSteps} {situacao}: {description}{texto}");
        Publish(new ProgressUpdate
        {
            StepIndex = index,
            TotalSteps = TotalSteps,
            StepDescription = description,
            Message = $"{situacao}: {description}{texto}",
        });
    }

    /// <summary>
    /// O usuário mandou pular a etapa que vem a seguir.
    ///
    /// A etapa em andamento termina (não dá para desfazer o que ela já
    /// fez); a próxima é que é deixada de fora.
    /// </summary>
    public void RequestSkip()
    {
        SkipRequested = true;
        Issue(ProgressCommand.Skip, "Pular a etapa atual.");
    }

    /// <summary>
    /// O usuário mandou encerrar a operação. O laço de execução para na
    /// próxima volta.
    /// </summary>
    public void RequestStop()
    {
        StopRequested = true;
        Issue(ProgressCommand.Stop, "Encerrar a operação.");
    }

    /// <summary>
    /// O usuário mandou rodar agora o que estava adiado para o próximo
    /// reinício do Windows.
    /// </summary>
    public void RequestRunNow()
    {
        RunNowRequested = true;
        Issue(ProgressCommand.RunNow, "Rodar agora o que estava adiado.");
    }

    /// <summary>
    /// Consome o pedido de pular: devolve <c>true</c> uma única vez e
    /// limpa o pedido, para que a etapa seguinte não seja pulada por
    /// acidente.
    /// </summary>
    public bool ConsumeSkip()
    {
        if (!SkipRequested) return false;
        SkipRequested = false;
        return true;
    }

    /// <summary>
    /// As opções de remoção com o pedido de "rodar agora" aplicado.
    ///
    /// "Rodar agora" é o inverso de "adiar para o próximo reinício": o item
    /// em uso deixa de esperar o próximo boot e é tentado agora. É esta
    /// única função que traduz o comando do usuário em opção de remoção —
    /// a etapa de remoção não decide sozinha o que fazer com o pedido.
    ///
    /// Atenção: as opções são copiadas campo a campo porque
    /// <see cref="UninstallRemovalOptions"/> não é um <c>record</c>. Se uma
    /// opção nova entrar lá, ela tem de entrar também aqui — esta é a única
    /// cópia das opções em todo o código.
    /// </summary>
    /// <param name="options">As opções da operação.</param>
    public UninstallRemovalOptions ApplyRunNow(UninstallRemovalOptions options)
    {
        if (!RunNowRequested) return options;

        return new UninstallRemovalOptions
        {
            Backup = options.Backup,
            UseRecycleBin = options.UseRecycleBin,
            DeferToNextRestart = false,
            KillRunningProcesses = options.KillRunningProcesses,
            FullRegistryBackup = options.FullRegistryBackup,
            RestorePointName = options.RestorePointName,
        };
    }

    /// <summary>
    /// Zera o modelo para uma nova operação.
    /// </summary>
    public void Reset()
    {
        TotalSteps = 0;
        CurrentStep = -1;
        CurrentDescription = string.Empty;
        TotalItems = 0;
        CurrentItem = 0;
        CurrentItemName = null;
        SkipRequested = false;
        StopRequested = false;
        RunNowRequested = false;
        _log.Clear();
    }

    /// <summary>Registra uma linha no log e avisa quem está assistindo.</summary>
    private void Issue(ProgressCommand command, string message)
    {
        Write(message);
        CommandIssued?.Invoke(command);
        Publish(new ProgressUpdate
        {
            StepIndex = CurrentStep,
            TotalSteps = TotalSteps,
            StepDescription = CurrentDescription,
            ItemIndex = CurrentItem,
            TotalItems = TotalItems,
            ItemName = CurrentItemName,
            Message = message,
            Command = command,
        });
    }

    /// <summary>Acrescenta uma linha ao log do andamento.</summary>
    private void Write(string line) => _log.Add(line);

    /// <summary>Avisa quem está assistindo ao andamento.</summary>
    private void Publish(ProgressUpdate update) => Updated?.Invoke(update);
}
