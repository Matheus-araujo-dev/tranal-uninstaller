using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using Tranal.Uninstaller.Core.Automation;

namespace Tranal.Uninstaller.Ui.Automation;

/// <summary>
/// A janela do painel de progresso da automação.
///
/// Ela é a superfície do modelo (<see cref="RemovalProgress"/>): mostra a
/// etapa que está em andamento, o item que está sendo tratado e o total,
/// e devolve ao modelo os três comandos do usuário — pular a etapa,
/// encerrar a operação e rodar agora o que estava adiado para o próximo
/// reinício.
///
/// A janela não decide nada. Quem decide é o núcleo: é por isso que o mesmo
/// painel funciona na linha de comando, e que o modelo pode ser testado
/// sem abrir janela nenhuma.
/// </summary>
public partial class AutomationWindow : Window
{
    private readonly RemovalProgress? _progress;
    private readonly ObservableCollection<EtapaRow> _rows = new();

    /// <summary>
    /// Cria a janela sem modelo. Serve para o designer e para quem só quer
    /// a tela vazia.
    /// </summary>
    public AutomationWindow()
    {
        InitializeComponent();
        ProgramText.Text = "Nenhuma desinstalação em andamento.";
        ProgressText.Text = string.Empty;
        StatusText.Text = string.Empty;
    }

    /// <summary>
    /// Cria a janela ligada a um painel de progresso.
    /// </summary>
    /// <param name="progress">O modelo do painel.</param>
    /// <param name="plan">O plano que será mostrado na lista de etapas.</param>
    public AutomationWindow(RemovalProgress progress, UninstallPlan? plan = null) : this()
    {
        _progress = progress;
        _progress.Updated += Progress_Updated;

        if (plan is not null) MontarEtapas(plan);
    }

    /// <summary>Mostra as etapas do plano na lista.</summary>
    private void MontarEtapas(UninstallPlan plan)
    {
        _rows.Clear();
        foreach (var step in plan.Steps)
            _rows.Add(new EtapaRow { Descricao = step.Description });

        StepsList.ItemsSource = _rows;
        ProgramText.Text = $"Desinstalando {plan.ProgramName}.";
        ProgressTrack.Maximum = Math.Max(1, plan.Steps.Count);
    }

    /// <summary>
    /// Recebe o andamento do modelo e reflete na tela.
    /// </summary>
    private void Progress_Updated(ProgressUpdate update)
    {
        // O modelo roda fora da thread da interface.
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => Progress_Updated(update));
            return;
        }

        if (update.TotalSteps > 0) ProgressTrack.Maximum = update.TotalSteps;

        var etapa = update.StepIndex + 1;
        ProgressTrack.Value = Math.Min(etapa, ProgressTrack.Maximum);
        ProgressText.Text = update.TotalSteps > 0 ? $"{etapa}/{update.TotalSteps}" : string.Empty;
        StatusText.Text = update.Message;

        if (update.StepIndex < 0 || update.StepIndex >= _rows.Count) return;

        var linha = _rows[update.StepIndex];
        linha.Situacao = update.ItemName is { Length: > 0 }
            ? "Em andamento"
            : DescreverSituacao(update.Message);
    }

    /// <summary>
    /// O texto da situação da etapa, tirado da última mensagem do modelo.
    /// </summary>
    private static string DescreverSituacao(string mensagem)
    {
        if (mensagem.Contains("pulada", StringComparison.OrdinalIgnoreCase)) return "Pulada";
        if (mensagem.Contains("falhou", StringComparison.OrdinalIgnoreCase)) return "Falhou";
        if (mensagem.Contains("concluída", StringComparison.OrdinalIgnoreCase)) return "Concluída";
        return "Em andamento";
    }

    /// <summary>O usuário mandou pular a etapa atual.</summary>
    private void SkipButton_Click(object sender, RoutedEventArgs e) =>
        _progress?.RequestSkip();

    /// <summary>O usuário mandou encerrar a operação.</summary>
    private void StopButton_Click(object sender, RoutedEventArgs e) =>
        _progress?.RequestStop();

    /// <summary>
    /// O usuário mandou rodar agora o que estava adiado para o próximo
    /// reinício.
    /// </summary>
    private void RunNowButton_Click(object sender, RoutedEventArgs e) =>
        _progress?.RequestRunNow();

    /// <summary>Fecha a janela.</summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Uma linha da lista de etapas. É uma classe de tela, não de núcleo:
    /// o modelo do painel não sabe o que é uma linha.
    /// </summary>
    private sealed class EtapaRow : INotifyPropertyChanged
    {
        private string _situacao = "Aguardando";

        /// <summary>A descrição da etapa.</summary>
        public string Descricao { get; init; } = string.Empty;

        /// <summary>Em que pé a etapa está.</summary>
        public string Situacao
        {
            get => _situacao;
            set
            {
                _situacao = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Situacao)));
            }
        }

        /// <summary>Avisa a lista de que a situação mudou.</summary>
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
