using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Reporting;

namespace Tranal.Uninstaller.Ui.Reporting;

/// <summary>
/// A janela de relatorio: mostra a lista como ela vai ser impressa e
/// imprime.
///
/// O nucleo nao pode usar WPF nem <c>System.Drawing</c> (e compartilhado
/// entre a interface grafica e a CLI), entao quem decide o que entra em
/// cada pagina, onde comeca a quebra e o que vai no cabecalho e o
/// <see cref="PrintLayout"/>, no nucleo. A janela so desenha o que ele
/// entrega e entrega o desenho a impressora.
///
/// A janela tem duas acoes: a visualizacao (que e o que aparece ao abrir) e
/// a impressao. A visualizacao mostra exatamente o que sera impresso —
/// as mesmas linhas, na mesma ordem, com o mesmo cabecalho.
/// </summary>
public sealed partial class ReportingWindow : Window
{
    /// <summary>
    /// Uma pagina do layout, como a tela mostra.
    /// </summary>
    public sealed class PageView
    {
        /// <summary>O titulo do documento.</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>O cabecalho do documento.</summary>
        public string Header { get; init; } = string.Empty;

        /// <summary>As linhas da pagina.</summary>
        public IReadOnlyList<string> Rows { get; init; } = Array.Empty<string>();

        /// <summary>O rodape da pagina.</summary>
        public string Footer { get; init; } = string.Empty;
    }

    private readonly PrintLayout _layout;
    private readonly ObservableCollection<PageView> _pages = new();

    /// <summary>
    /// Abre a janela de impressao para a lista de programas.
    /// </summary>
    /// <param name="programs">Os programas a listar.</param>
    /// <param name="columns">
    /// As colunas. Nulo usa o conjunto padrao do exportador — a impressao
    /// mostra a mesma lista que a exportacao mostra.
    /// </param>
    public ReportingWindow(IReadOnlyList<InstalledProgram> programs, IReadOnlyList<ProgramColumn>? columns = null)
    {
        InitializeComponent();

        _layout = PrintLayout.Build(programs ?? Array.Empty<InstalledProgram>(), columns);

        Title = Text("Reporting.Title", "Imprimir");
        PrintButton.Content = Text("Reporting.Print", "Imprimir");
        CloseButton.Content = Text("Reporting.Close", "Fechar");
        HeaderText.Text = _layout.Header;

        foreach (var page in _layout.Pages)
        {
            _pages.Add(new PageView
            {
                Title = _layout.Title,
                Header = _layout.Header,
                Rows = page.Rows,
                Footer = page.Footer,
            });
        }

        PagesList.ItemsSource = _pages;
    }

    /// <summary>
    /// O layout que sera impresso. Serve para quem chama conferir o
    /// resultado sem precisar imprimir.
    /// </summary>
    public PrintLayout Layout => _layout;

    /// <summary>
    /// Imprime a lista. O dialogo do Windows aparece antes: quem imprime
    /// escolhe a impressora, as paginas e a quantidade de copias.
    /// </summary>
    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialogo = new System.Windows.Controls.PrintDialog();
            if (dialogo.ShowDialog() != true) return;

            using var documento = new System.Drawing.Printing.PrintDocument();
            documento.DocumentName = _layout.Title;

            var indice = 0;

            documento.PrintPage += (_, args) =>
            {
                if (indice >= _layout.Pages.Count)
                {
                    args.HasMorePages = false;
                    return;
                }

                var grafico = args.Graphics;
                if (grafico is null)
                {
                    args.HasMorePages = false;
                    return;
                }

                DesenharPagina(grafico, _layout.Pages[indice], args.MarginBounds);

                indice++;
                args.HasMorePages = indice < _layout.Pages.Count;
            };

            documento.Print();
        }
        catch (Exception ex)
        {
            UserMessages.Warning(this, Text("Reporting.Error", "Não foi possível imprimir.") + " " + ex.Message);
        }
    }

    /// <summary>
    /// Fecha a janela.
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Desenha uma pagina do layout na folha.
    /// </summary>
    /// <param name="g">O destino do desenho.</param>
    /// <param name="pagina">A pagina.</param>
    /// <param name="area">A area imprimivel da folha.</param>
    private void DesenharPagina(System.Drawing.Graphics g, PrintPage pagina, System.Drawing.Rectangle area)
    {
        using var fonteTitulo = new System.Drawing.Font("Segoe UI", 13f, System.Drawing.FontStyle.Bold);
        using var fonte = new System.Drawing.Font("Consolas", 8f);
        using var fonteRodape = new System.Drawing.Font("Segoe UI", 9f);
        using var caneta = new System.Drawing.Pen(System.Drawing.Color.Black);

        var y = area.Top + 4f;

        // O titulo so na primeira pagina: e o titulo do documento.
        if (pagina.Number == 1)
        {
            g.DrawString(_layout.Title, fonteTitulo, System.Drawing.Brushes.Black, area.Left, y);
            y += fonteTitulo.GetHeight() + 4f;
            g.DrawString(_layout.Header, fonteRodape, System.Drawing.Brushes.Black, area.Left, y);
            y += fonteRodape.GetHeight() + 6f;
        }

        var alturaLinha = fonte.GetHeight();

        foreach (var linha in pagina.Rows)
        {
            // A pagina acabou: o que nao coube fica de fora, e a pagina
            // seguinte comeca com o cabecalho das colunas de novo.
            if (y + alturaLinha > area.Bottom - fonteRodape.GetHeight() - 4f) break;

            g.DrawString(Recortar(g, linha, fonte, area.Width), fonte, System.Drawing.Brushes.Black, area.Left, y);
            y += alturaLinha;
        }

        g.DrawString(pagina.Footer, fonteRodape, System.Drawing.Brushes.Black, area.Left, area.Bottom - fonteRodape.GetHeight());
    }

    /// <summary>
    /// Corta a linha no ponto em que ela deixa de caber na folha.
    /// </summary>
    /// <param name="g">O destino do desenho (para medir).</param>
    /// <param name="linha">A linha.</param>
    /// <param name="fonte">A fonte do texto.</param>
    /// <param name="largura">A largura disponivel.</param>
    private static string Recortar(System.Drawing.Graphics g, string linha, System.Drawing.Font fonte, int largura)
    {
        if (string.IsNullOrEmpty(linha)) return string.Empty;
        if (g.MeasureString(linha, fonte).Width <= largura) return linha;

        var tamanho = linha.Length;
        while (tamanho > 0 && g.MeasureString(linha[..tamanho] + "…", fonte).Width > largura)
        {
            tamanho--;
        }

        return tamanho <= 0 ? string.Empty : linha[..tamanho] + "…";
    }

    /// <summary>
    /// O texto de uma chave do catalogo de idiomas, com o texto de reserva
    /// quando a chave ainda nao existe no catalogo.
    ///
    /// O padrao e o mesmo de <see cref="ProgramColumns.Label"/>: a chave vem
    /// do catalogo, e o texto de reserva cobre o tempo em que a chave ainda
    /// nao foi traduzida.
    /// </summary>
    /// <param name="chave">A chave do catalogo.</param>
    /// <param name="reserva">O texto de reserva.</param>
    private static string Text(string chave, string reserva)
    {
        var texto = Loc.Get(chave);
        return texto == chave ? reserva : texto;
    }
}
