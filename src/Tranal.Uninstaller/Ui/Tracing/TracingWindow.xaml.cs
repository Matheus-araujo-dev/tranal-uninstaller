using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using Tranal.Uninstaller.Core.Reporting;
using Tranal.Uninstaller.Core.Tracing;
using Tranal.Uninstaller.Ui.Reporting;

namespace Tranal.Uninstaller.Ui.Tracing;

/// <summary>
/// Os textos da janela de rastreio.
///
/// Eles vivem aqui, em um lugar so, para que a troca pelo catalogo de
/// idiomas (que pertence a outro dono de arquivo) seja uma troca de uma
/// unica linha: todo texto da tela passa a sair de <c>{loc:Tr ...}</c>.
/// </summary>
public static class TracingTexts
{
    /// <summary>Titulo da janela.</summary>
    public const string Titulo = "Rastreio e comparacao A x B";

    /// <summary>Aba do rastreio em tempo real.</summary>
    public const string AbaRastreio = "Rastreio";

    /// <summary>Aba da politica de processos.</summary>
    public const string AbaProcessos = "Processos";

    /// <summary>Aba da biblioteca de snapshots.</summary>
    public const string AbaSnapshots = "Snapshots";

    /// <summary>Botao que comeca a observar a pasta.</summary>
    public const string Observar = "Observar";

    /// <summary>Botao que interrompe a observacao.</summary>
    public const string Parar = "Parar";

    /// <summary>Botao que grava o log atual.</summary>
    public const string SalvarLog = "Salvar log";

    /// <summary>Botao que carrega os logs gravados.</summary>
    public const string CarregarLogs = "Carregar logs";

    /// <summary>Botao que une os logs selecionados.</summary>
    public const string UnirLogs = "Unir logs";

    /// <summary>Botao que inclui um processo pelo nome.</summary>
    public const string IncluirNome = "Incluir nome";

    /// <summary>Botao que exclui um processo pelo nome.</summary>
    public const string ExcluirNome = "Excluir nome";

    /// <summary>Botao que inclui uma pasta de executaveis.</summary>
    public const string IncluirPasta = "Incluir pasta";

    /// <summary>Botao que exclui uma pasta de executaveis.</summary>
    public const string ExcluirPasta = "Excluir pasta";

    /// <summary>Botao que limpa a politica.</summary>
    public const string Limpar = "Limpar";

    /// <summary>Botao que fotografa o sistema e grava um snapshot.</summary>
    public const string CapturarSnapshot = "Capturar snapshot";

    /// <summary>Botao que compara os dois snapshots selecionados.</summary>
    public const string Comparar = "Comparar A x B";

    /// <summary>Botao que apaga o snapshot selecionado.</summary>
    public const string Apagar = "Apagar";

    /// <summary>Cabecalho da coluna do tipo da mudanca.</summary>
    public const string ColunaTipo = "Tipo";

    /// <summary>Cabecalho da coluna do caminho.</summary>
    public const string ColunaCaminho = "Caminho";

    /// <summary>Cabecalho da coluna do nome anterior.</summary>
    public const string ColunaNomeAntigo = "Nome anterior";

    /// <summary>Cabecalho da coluna do dado anterior.</summary>
    public const string ColunaDadoAntigo = "Dado anterior";

    /// <summary>Cabecalho da coluna do instante.</summary>
    public const string ColunaQuando = "Quando";

    /// <summary>Cabecalho da coluna do programa, na lista de logs.</summary>
    public const string ColunaPrograma = "Programa";

    /// <summary>Cabecalho da coluna da quantidade de entradas.</summary>
    public const string ColunaEntradas = "Entradas";

    /// <summary>Cabecalho da coluna da regra, na politica de processos.</summary>
    public const string ColunaRegra = "Regra";

    /// <summary>Cabecalho da coluna do valor, na politica de processos.</summary>
    public const string ColunaValor = "Valor";

    /// <summary>Cabecalho da coluna do nome, na lista de snapshots.</summary>
    public const string ColunaNome = "Nome";

    /// <summary>Cabecalho da coluna do rotulo, na lista de snapshots.</summary>
    public const string ColunaRotulo = "Rotulo";

    /// <summary>Cabecalho da coluna das contagens, na lista de snapshots.</summary>
    public const string ColunaContagens = "Arquivos / Pastas / Chaves";

    /// <summary>A explicacao da lista de logs.</summary>
    public const string HintLogs =
        "Selecione dois ou mais logs e use \"Unir logs\" para juntar o rastreio do mesmo programa num log so, sem duplicar entrada.";

    /// <summary>Mensagem quando a pasta informada esta vazia.</summary>
    public const string InformePasta = "Informe a pasta que sera observada.";

    /// <summary>Mensagem quando a pasta nao pode ser observada.</summary>
    public const string NaoFoiPossivelObservar = "Nao foi possivel observar a pasta informada.";

    /// <summary>Mensagem quando o watcher esta ativo.</summary>
    public const string Observando = "Observando as escritas em ";

    /// <summary>Mensagem quando o log foi gravado.</summary>
    public const string LogGravado = "Log gravado em ";

    /// <summary>Mensagem quando nao ha log para gravar.</summary>
    public const string NadaParaGravar = "Nao ha entrada observada para gravar.";

    /// <summary>Mensagem quando o log foi unido.</summary>
    public const string LogsUnidos = "Logs unidos em ";

    /// <summary>Mensagem quando nenhum log foi selecionado para unir.</summary>
    public const string SelecioneLogs = "Selecione ao menos um log para unir.";

    /// <summary>Mensagem quando a captura do snapshot terminou.</summary>
    public const string SnapshotCapturado = "Snapshot gravado: ";

    /// <summary>Mensagem quando a captura do snapshot falhou.</summary>
    public const string SnapshotFalhou = "Nao foi possivel capturar o snapshot.";

    /// <summary>Mensagem quando dois snapshots foram comparados.</summary>
    public const string ComparacaoPronta = "Comparacao concluida.";

    /// <summary>Mensagem quando faltam snapshots para comparar.</summary>
    public const string SelecioneDoisSnapshots = "Selecione dois snapshots para comparar.";

    /// <summary>Mensagem quando um snapshot foi apagado.</summary>
    public const string SnapshotApagado = "Snapshot apagado.";

    /// <summary>Mensagem quando nada foi apagado.</summary>
    public const string NadaParaApagar = "Selecione o snapshot que deseja apagar.";

    /// <summary>Botao que exporta a arvore de alteracoes.</summary>
    public const string ExportarArvore = "Exportar arvore...";

    /// <summary>Botao que imprime a arvore de alteracoes.</summary>
    public const string ImprimirArvore = "Imprimir arvore...";

    /// <summary>Mensagem quando a arvore foi exportada.</summary>
    public const string ArvoreExportada = "Arvore exportada: ";

    /// <summary>Mensagem quando a arvore nao pode ser exportada.</summary>
    public const string ArvoreExportFalhou = "Nao foi possivel exportar a arvore.";

    /// <summary>Mensagem quando ainda nao houve comparacao.</summary>
    public const string SemComparacao = "Compare dois snapshots antes de exportar ou imprimir.";
}

/// <summary>
/// A janela de rastreio: observa as escritas de uma pasta em tempo real,
/// mostra o log de rastreio, a politica de processos observados e a
/// biblioteca de snapshots com a comparacao A x B.
/// </summary>
public partial class TracingWindow : Window
{
    private readonly TraceProcessPolicy _politica = new();
    private readonly ObservableCollection<LinhaEntrada> _entradas = new();
    private readonly ObservableCollection<LinhaLog> _logs = new();
    private readonly ObservableCollection<LinhaPolitica> _regras = new();
    private readonly ObservableCollection<LinhaSnapshot> _snapshots = new();

    private WriteWatcher? _watcher;

    /// <summary>
    /// A ultima comparacao feita. E ela que a arvore de alteracoes mostra,
    /// exporta e imprime.
    /// </summary>
    private SnapshotComparison? _comparacao;

    /// <summary>O nome do snapshot de antes da ultima comparacao.</summary>
    private string _comparacaoAntes = string.Empty;

    /// <summary>O nome do snapshot de depois da ultima comparacao.</summary>
    private string _comparacaoDepois = string.Empty;

    /// <summary>
    /// Abre a janela de rastreio.
    /// </summary>
    public TracingWindow()
    {
        InitializeComponent();

        Title = TracingTexts.Titulo;

        EntriesView.ItemsSource = _entradas;
        LogsView.ItemsSource = _logs;
        PolicyView.ItemsSource = _regras;
        SnapshotsView.ItemsSource = _snapshots;

        MontarColunas();
        AtualizarPolitica();
        CarregarLogs();
        CarregarSnapshots();
    }

    // ---------------------------------------------------------------------
    // Rastreio
    // ---------------------------------------------------------------------

    /// <summary>
    /// Comeca a observar a pasta informada.
    /// </summary>
    private void WatchButton_Click(object sender, RoutedEventArgs e)
    {
        var pasta = WatchFolderBox.Text.Trim();
        if (pasta.Length == 0)
        {
            StatusText.Text = TracingTexts.InformePasta;
            return;
        }

        PararWatcher();

        var watcher = WriteWatcher.Iniciar(pasta, _politica, entrada =>
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() => AdicionarEntrada(entrada)));
            }
            catch
            {
                // A janela pode ter sido fechada enquanto o watcher ainda
                // estava vivo: a entrada se perde, mas o watcher nao quebra.
            }
        });

        if (watcher is null)
        {
            StatusText.Text = TracingTexts.NaoFoiPossivelObservar;
            return;
        }

        _watcher = watcher;
        WatchButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        StatusText.Text = TracingTexts.Observando + pasta;
    }

    /// <summary>
    /// Interrompe a observacao.
    /// </summary>
    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        PararWatcher();
        StatusText.Text = string.Empty;
    }

    /// <summary>
    /// Grava o log das entradas observadas.
    /// </summary>
    private void SaveLogButton_Click(object sender, RoutedEventArgs e)
    {
        if (_entradas.Count == 0)
        {
            StatusText.Text = TracingTexts.NadaParaGravar;
            return;
        }

        var dialogo = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Log de rastreio (*.trace.json)|*.trace.json|Todos os arquivos (*.*)|*.*",
            FileName = "rastreio" + TraceLogV2.FileExtension,
        };

        if (dialogo.ShowDialog(this) != true) return;

        var entradas = _entradas.Select(l => l.Entrada).ToList();
        var log = TraceLogV2.Create("Rastreio manual", entradas);

        var caminho = log.Save(dialogo.FileName);
        if (caminho.Length == 0)
        {
            StatusText.Text = TracingTexts.NadaParaGravar;
            return;
        }

        StatusText.Text = TracingTexts.LogGravado + caminho;
        CarregarLogs();
    }

    /// <summary>
    /// Carrega os logs gravados no disco.
    /// </summary>
    private void LoadLogsButton_Click(object sender, RoutedEventArgs e) => CarregarLogs();

    /// <summary>
    /// Une os logs selecionados num log so, sem duplicar entrada.
    /// </summary>
    private void MergeLogsButton_Click(object sender, RoutedEventArgs e)
    {
        var selecionados = LogsView.SelectedItems.OfType<LinhaLog>().ToList();
        if (selecionados.Count == 0)
        {
            StatusText.Text = TracingTexts.SelecioneLogs;
            return;
        }

        var unido = TraceLogV2.Merge(selecionados.Select(l => l.Log));
        var caminho = unido.Save();

        if (caminho.Length == 0)
        {
            StatusText.Text = TracingTexts.NadaParaGravar;
            return;
        }

        StatusText.Text = TracingTexts.LogsUnidos + caminho;
        CarregarLogs();
    }

    // ---------------------------------------------------------------------
    // Processos
    // ---------------------------------------------------------------------

    /// <summary>Inclui um processo pelo nome.</summary>
    private void IncludeNameButton_Click(object sender, RoutedEventArgs e)
    {
        _politica.Include(ProcessNameBox.Text);
        AtualizarPolitica();
    }

    /// <summary>Exclui um processo pelo nome.</summary>
    private void ExcludeNameButton_Click(object sender, RoutedEventArgs e)
    {
        _politica.Exclude(ProcessNameBox.Text);
        AtualizarPolitica();
    }

    /// <summary>Inclui uma pasta de executaveis.</summary>
    private void IncludePathButton_Click(object sender, RoutedEventArgs e)
    {
        _politica.IncludePath(ProcessPathBox.Text);
        AtualizarPolitica();
    }

    /// <summary>Exclui uma pasta de executaveis.</summary>
    private void ExcludePathButton_Click(object sender, RoutedEventArgs e)
    {
        _politica.ExcludePath(ProcessPathBox.Text);
        AtualizarPolitica();
    }

    /// <summary>Limpa a politica de processos.</summary>
    private void ClearPolicyButton_Click(object sender, RoutedEventArgs e)
    {
        _politica.Clear();
        AtualizarPolitica();
    }

    // ---------------------------------------------------------------------
    // Snapshots
    // ---------------------------------------------------------------------

    /// <summary>
    /// Fotografa o sistema e grava um snapshot nomeado. A captura e lenta (o
    /// sistema inteiro e varrido), por isso roda fora da thread da interface.
    /// </summary>
    private async void CaptureSnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        var nome = SnapshotNameBox.Text.Trim();
        if (nome.Length == 0) nome = "Snapshot";

        var rotulo = SnapshotLabelBox.Text.Trim();

        StatusText.Text = TracingTexts.SnapshotCapturado + nome + "...";

        var metadados = await Task.Run(() => SnapshotLibrary.Capture(nome, rotulo));

        CarregarSnapshots();

        StatusText.Text = metadados is null
            ? TracingTexts.SnapshotFalhou
            : TracingTexts.SnapshotCapturado + metadados.Name;
    }

    /// <summary>
    /// Compara os dois snapshots selecionados.
    /// </summary>
    private void CompareSnapshotsButton_Click(object sender, RoutedEventArgs e)
    {
        var selecionados = SnapshotsView.SelectedItems.OfType<LinhaSnapshot>().ToList();
        if (selecionados.Count < 2)
        {
            StatusText.Text = TracingTexts.SelecioneDoisSnapshots;
            return;
        }

        var antes = selecionados[0].Metadados;
        var depois = selecionados[1].Metadados;

        var comparacao = SnapshotLibrary.Compare(antes.Id, depois.Id);
        if (comparacao is null)
        {
            StatusText.Text = TracingTexts.SnapshotFalhou;
            return;
        }

        _comparacao = comparacao;
        _comparacaoAntes = antes.Name;
        _comparacaoDepois = depois.Name;

        ComparisonText.Text = DescreverComparacao(antes.Name, depois.Name, comparacao);
        StatusText.Text = TracingTexts.ComparacaoPronta;
    }

    /// <summary>
    /// Exporta a arvore de alteracoes da ultima comparacao.
    ///
    /// O formato sai da extensao do arquivo escolhido: .csv vira planilha,
    /// .html vira pagina, o resto vira texto.
    /// </summary>
    private void ExportChangeTreeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_comparacao is null)
        {
            StatusText.Text = TracingTexts.SemComparacao;
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = TracingTexts.ExportarArvore,
            Filter = "CSV (*.csv)|*.csv|HTML (*.html)|*.html|Texto (*.txt)|*.txt",
            FileName = "arvore-alteracoes.csv",
            AddExtension = true,
        };

        if (dialog.ShowDialog(this) != true) return;

        var gravado = ChangeTreeReport.Write(
            _comparacao,
            _comparacaoAntes,
            _comparacaoDepois,
            dialog.FileName,
            FormatoDe(dialog.FileName));

        StatusText.Text = gravado.Length > 0
            ? TracingTexts.ArvoreExportada + gravado
            : TracingTexts.ArvoreExportFalhou;
    }

    /// <summary>
    /// Abre a pre-visualizacao de impressao da arvore de alteracoes.
    ///
    /// E a mesma janela de impressao da lista de programas: a regra de como
    /// uma tabela vira folha mora no nucleo (<c>PrintTable</c>).
    /// </summary>
    private void PrintChangeTreeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_comparacao is null)
        {
            StatusText.Text = TracingTexts.SemComparacao;
            return;
        }

        var tabela = ChangeTreeReport.Table(_comparacao, _comparacaoAntes, _comparacaoDepois);

        var janela = new ReportingWindow(tabela);
        janela.Owner = this;
        janela.ShowDialog();
    }

    /// <summary>
    /// O formato de exportacao, pela extensao do arquivo.
    /// </summary>
    /// <param name="caminho">O caminho do arquivo.</param>
    private static ChangeTreeFormat FormatoDe(string caminho)
    {
        if (caminho.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return ChangeTreeFormat.Csv;
        if (caminho.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            || caminho.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)) return ChangeTreeFormat.Html;
        return ChangeTreeFormat.Text;
    }

    /// <summary>
    /// Apaga os snapshots selecionados.
    /// </summary>
    private void DeleteSnapshotButton_Click(object sender, RoutedEventArgs e)
    {
        var selecionados = SnapshotsView.SelectedItems.OfType<LinhaSnapshot>().ToList();
        if (selecionados.Count == 0)
        {
            StatusText.Text = TracingTexts.NadaParaApagar;
            return;
        }

        foreach (var linha in selecionados) SnapshotLibrary.Delete(linha.Metadados.Id);

        CarregarSnapshots();
        StatusText.Text = TracingTexts.SnapshotApagado;
    }

    // ---------------------------------------------------------------------
    // Apoio
    // ---------------------------------------------------------------------

    /// <summary>
    /// Monta as colunas das grades. O texto do cabecalho vai por codigo (a
    /// regra do projeto: <c>GridViewColumn.Header</c> nao e uma propriedade
    /// de dependencia).
    /// </summary>
    private void MontarColunas()
    {
        var entradas = new GridView();
        entradas.Columns.Add(Coluna(TracingTexts.ColunaTipo, "Tipo", 90));
        entradas.Columns.Add(Coluna(TracingTexts.ColunaCaminho, "Caminho", 320));
        entradas.Columns.Add(Coluna(TracingTexts.ColunaNomeAntigo, "NomeAntigo", 130));
        entradas.Columns.Add(Coluna(TracingTexts.ColunaDadoAntigo, "DadoAntigo", 130));
        entradas.Columns.Add(Coluna(TracingTexts.ColunaQuando, "Quando", 140));
        EntriesView.View = entradas;

        var logs = new GridView();
        logs.Columns.Add(Coluna(TracingTexts.ColunaPrograma, "Programa", 160));
        logs.Columns.Add(Coluna(TracingTexts.ColunaEntradas, "Entradas", 70));
        logs.Columns.Add(Coluna(TracingTexts.ColunaQuando, "Quando", 140));
        LogsView.View = logs;

        var politicas = new GridView();
        politicas.Columns.Add(Coluna(TracingTexts.ColunaRegra, "Regra", 130));
        politicas.Columns.Add(Coluna(TracingTexts.ColunaValor, "Valor", 320));
        PolicyView.View = politicas;

        var snapshots = new GridView();
        snapshots.Columns.Add(Coluna(TracingTexts.ColunaNome, "Nome", 160));
        snapshots.Columns.Add(Coluna(TracingTexts.ColunaRotulo, "Rotulo", 180));
        snapshots.Columns.Add(Coluna(TracingTexts.ColunaContagens, "Contagens", 150));
        snapshots.Columns.Add(Coluna(TracingTexts.ColunaQuando, "Quando", 140));
        SnapshotsView.View = snapshots;
    }

    /// <summary>
    /// Cria uma coluna da grade.
    /// </summary>
    private static GridViewColumn Coluna(string cabecalho, string propriedade, double largura) =>
        new GridViewColumn
        {
            Header = cabecalho,
            Width = largura,
            DisplayMemberBinding = new Binding(propriedade),
        };

    /// <summary>
    /// Acrescenta uma entrada observada a lista.
    /// </summary>
    private void AdicionarEntrada(TraceEntry entrada) => _entradas.Add(ConverterEntrada(entrada));

    /// <summary>
    /// Converte a entrada do nucleo na linha da grade.
    /// </summary>
    private static LinhaEntrada ConverterEntrada(TraceEntry entrada) => new LinhaEntrada
    {
        Entrada = entrada,
        Tipo = DescreverTipo(entrada.Kind),
        Caminho = entrada.Path,
        NomeAntigo = entrada.OldName ?? string.Empty,
        DadoAntigo = entrada.OldValue ?? string.Empty,
        Quando = entrada.Timestamp.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// O nome do tipo da mudanca, em portugues.
    /// </summary>
    private static string DescreverTipo(TraceChangeKind tipo) => tipo switch
    {
        TraceChangeKind.Created => "Criado",
        TraceChangeKind.Deleted => "Removido",
        TraceChangeKind.Renamed => "Renomeado",
        TraceChangeKind.Overwritten => "Sobrescrito",
        _ => "Desconhecido",
    };

    /// <summary>
    /// Recarrega a lista de logs gravados.
    /// </summary>
    private void CarregarLogs()
    {
        _logs.Clear();

        foreach (var log in TraceLogV2.List())
        {
            _logs.Add(new LinhaLog
            {
                Programa = log.ProgramName,
                Entradas = log.Entries.Count.ToString(CultureInfo.InvariantCulture),
                Quando = log.Timestamp.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                Log = log,
            });
        }
    }

    /// <summary>
    /// Recarrega a lista de snapshots.
    /// </summary>
    private void CarregarSnapshots()
    {
        _snapshots.Clear();

        foreach (var metadados in SnapshotLibrary.List())
        {
            _snapshots.Add(new LinhaSnapshot
            {
                Nome = metadados.Name,
                Rotulo = metadados.Label,
                Contagens = metadados.FileCount + " / " + metadados.FolderCount + " / " + metadados.RegistryKeyCount,
                Quando = metadados.CreatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                Metadados = metadados,
            });
        }
    }

    /// <summary>
    /// Atualiza a lista de regras da politica de processos.
    /// </summary>
    private void AtualizarPolitica()
    {
        _regras.Clear();

        foreach (var nome in _politica.IncludedNames)
            _regras.Add(new LinhaPolitica { Regra = "Nome incluido", Valor = nome });

        foreach (var nome in _politica.ExcludedNames)
            _regras.Add(new LinhaPolitica { Regra = "Nome excluido", Valor = nome });

        foreach (var caminho in _politica.IncludedPaths)
            _regras.Add(new LinhaPolitica { Regra = "Pasta incluida", Valor = caminho });

        foreach (var caminho in _politica.ExcludedPaths)
            _regras.Add(new LinhaPolitica { Regra = "Pasta excluida", Valor = caminho });

        StatusText.Text = _politica.Describe();
    }

    /// <summary>
    /// Encerra o watcher, se houver um.
    /// </summary>
    private void PararWatcher()
    {
        if (_watcher is null) return;

        _watcher.Dispose();
        _watcher = null;

        WatchButton.IsEnabled = true;
        StopButton.IsEnabled = false;
    }

    /// <summary>
    /// O texto da comparacao A x B.
    /// </summary>
    private static string DescreverComparacao(string antes, string depois, SnapshotComparison comparacao)
    {
        var linhas = new List<string>
        {
            "Antes: " + antes,
            "Depois: " + depois,
            string.Empty,
            "Novos: " + comparacao.NewFiles.Count + " arquivos, " + comparacao.NewFolders.Count + " pastas, " +
                comparacao.NewRegistryKeys.Count + " chaves",
            "Removidos: " + comparacao.RemovedFiles.Count + " arquivos, " + comparacao.RemovedFolders.Count + " pastas, " +
                comparacao.RemovedRegistryKeys.Count + " chaves",
            "Modificados: " + comparacao.ModifiedFiles.Count + " arquivos, " + comparacao.ModifiedRegistryValues.Count + " valores",
            "Inalterados: " + comparacao.UnchangedFiles.Count + " arquivos",
        };

        foreach (var arquivo in comparacao.ModifiedFiles)
            linhas.Add("  modificado: " + arquivo.Path);

        foreach (var arquivo in comparacao.RemovedFiles)
            linhas.Add("  removido: " + arquivo.Path);

        foreach (var arquivo in comparacao.NewFiles)
            linhas.Add("  novo: " + arquivo.Path);

        return string.Join(Environment.NewLine, linhas);
    }

    /// <summary>
    /// Encerra o watcher quando a janela fecha.
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        PararWatcher();
        base.OnClosed(e);
    }

    /// <summary>Uma entrada do log mostrada na grade.</summary>
    private sealed class LinhaEntrada
    {
        public TraceEntry Entrada { get; init; } = new();
        public string Tipo { get; init; } = string.Empty;
        public string Caminho { get; init; } = string.Empty;
        public string NomeAntigo { get; init; } = string.Empty;
        public string DadoAntigo { get; init; } = string.Empty;
        public string Quando { get; init; } = string.Empty;
    }

    /// <summary>Um log carregado, mostrado na grade.</summary>
    private sealed class LinhaLog
    {
        public string Programa { get; init; } = string.Empty;
        public string Entradas { get; init; } = string.Empty;
        public string Quando { get; init; } = string.Empty;
        public TraceLogV2 Log { get; init; } = new();
    }

    /// <summary>Uma regra da politica de processos.</summary>
    private sealed class LinhaPolitica
    {
        public string Regra { get; init; } = string.Empty;
        public string Valor { get; init; } = string.Empty;
    }

    /// <summary>Um snapshot da biblioteca.</summary>
    private sealed class LinhaSnapshot
    {
        public string Nome { get; init; } = string.Empty;
        public string Rotulo { get; init; } = string.Empty;
        public string Contagens { get; init; } = string.Empty;
        public string Quando { get; init; } = string.Empty;
        public SnapshotMetadata Metadados { get; init; } = new();
    }
}
