using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Ui.Shared;

/// <summary>
/// A arvore das sobras: as categorias (com a contagem) e, debaixo de cada
/// uma, os itens.
///
/// E a tela de sobras do Geek e do Revo: "File System (%d items)" e
/// "Registry (%d items)". A REGRA do agrupamento mora no nucleo
/// (<see cref="LeftoverGroups"/>); aqui a arvore e montada e nada mais.
///
/// O mesmo controle serve as duas telas que mostram sobras: o assistente de
/// desinstalacao e a desinstalacao forcada.
/// </summary>
public partial class LeftoversTree : UserControl
{
    private IReadOnlyList<LeftoverItem> _itens = Array.Empty<LeftoverItem>();

    /// <summary>Cria a arvore das sobras.</summary>
    public LeftoversTree()
    {
        InitializeComponent();
    }

    /// <summary>
    /// O menu de contexto da arvore.
    ///
    /// Quem monta o menu e quem sabe o que fazer com o item selecionado; a
    /// arvore so o usa.
    /// </summary>
    public ContextMenu? ContextMenuSource
    {
        get => Tree.ContextMenu;
        set => Tree.ContextMenu = value;
    }

    /// <summary>A sobra selecionada (a folha da arvore).</summary>
    public LeftoverItem? SelectedItem =>
        (Tree.SelectedItem as TreeViewItem)?.Tag as LeftoverItem;

    /// <summary>
    /// Mostra as sobras, agrupadas por categoria.
    /// </summary>
    /// <param name="itens">As sobras encontradas.</param>
    public void SetItems(IEnumerable<LeftoverItem>? itens)
    {
        _itens = (itens ?? Array.Empty<LeftoverItem>()).Where(i => i is not null).ToList();
        Reconstruir();
    }

    /// <summary>
    /// Refaz a arvore. E o que a chamadora usa quando a marcacao das sobras
    /// muda: a sobra nao avisa mudanca de propriedade, entao a caixa de
    /// marcacao so acompanha a escolha depois de a arvore ser refeita.
    /// </summary>
    public void Refresh() => Reconstruir();

    /// <summary>
    /// Monta a arvore: um no por categoria (com a contagem) e uma folha por
    /// item, filtrada pelo texto da busca.
    /// </summary>
    private void Reconstruir()
    {
        Tree.Items.Clear();

        var filtrados = LeftoverGroups.Filter(_itens, SearchBox.Text);
        var grupos = LeftoverGroups.Build(filtrados);

        foreach (var grupo in grupos)
        {
            var no = new TreeViewItem
            {
                Header = $"{grupo.Title} ({grupo.Count})",
                IsExpanded = true,
                Tag = grupo,
            };

            foreach (var item in grupo.Items)
            {
                no.Items.Add(FolhaDe(item));
            }

            Tree.Items.Add(no);
        }

        BrowseButton.IsEnabled = SelectedItem is not null;
    }

    /// <summary>
    /// A folha de uma sobra: a caixa de marcacao, o caminho e o detalhe
    /// (tipo, tamanho, confianca e o motivo do julgamento).
    /// </summary>
    /// <param name="item">A sobra.</param>
    private static TreeViewItem FolhaDe(LeftoverItem item)
    {
        var folha = new TreeViewItem { Tag = item, IsExpanded = true };

        var painel = new Grid();
        painel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        painel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var caixa = new CheckBox
        {
            IsChecked = item.Selected,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
        };
        caixa.Checked += (_, _) => item.Selected = true;
        caixa.Unchecked += (_, _) => item.Selected = false;
        Grid.SetColumn(caixa, 0);

        var textos = new StackPanel();

        var caminho = new TextBlock
        {
            Text = item.Path,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = item.Path,
            FontWeight = item.IsHighConfidence ? FontWeights.Bold : FontWeights.Normal,
        };

        var detalhe = new TextBlock
        {
            Text = DetalheDe(item),
            FontSize = 10,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = CorDoTextoFraco(),
        };

        textos.Children.Add(caminho);
        textos.Children.Add(detalhe);
        Grid.SetColumn(textos, 1);

        painel.Children.Add(caixa);
        painel.Children.Add(textos);

        folha.Header = painel;
        return folha;
    }

    /// <summary>
    /// O detalhe de uma sobra: tipo, tamanho, confianca e o motivo do
    /// julgamento (o motivo e o que diz POR QUE o item e sobra).
    /// </summary>
    /// <param name="item">A sobra.</param>
    private static string DetalheDe(LeftoverItem item)
    {
        var partes = new List<string>();

        if (!string.IsNullOrWhiteSpace(item.Kind)) partes.Add(item.Kind);
        if (!string.IsNullOrWhiteSpace(item.Detail)) partes.Add(item.Detail);

        partes.Add(LeftoverLabels.Confidence(item.Confidence));

        var motivo = LeftoverLabels.Reason(item.Reason);
        if (!string.IsNullOrWhiteSpace(motivo)) partes.Add(motivo);

        return string.Join(" · ", partes);
    }

    /// <summary>
    /// A cor do texto fraco do tema. Quando o recurso nao existe (a tela
    /// aberta antes do tema), o cinza do sistema e o plano B.
    /// </summary>
    private static Brush CorDoTextoFraco() =>
        Application.Current?.TryFindResource("TextDimBrush") as Brush ?? Brushes.Gray;

    /// <summary>
    /// A busca filtra as sobras pelo caminho, pelo detalhe e pelo tipo.
    /// </summary>
    /// <param name="sender">A caixa de busca.</param>
    /// <param name="e">A mudanca do texto.</param>
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Reconstruir();

    /// <summary>
    /// A sobra selecionada mudou: o comando "Explorar" so vale com uma
    /// folha selecionada.
    /// </summary>
    /// <param name="sender">A arvore.</param>
    /// <param name="e">A mudanca de selecao.</param>
    private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        BrowseButton.IsEnabled = SelectedItem is not null;
    }

    /// <summary>
    /// Abre o item selecionado no lugar onde ele vive: a pasta no Explorer e
    /// a chave de registro no editor de registro.
    ///
    /// E o "Browse..." do Geek: a navegacao dentro das sobras encontradas.
    /// </summary>
    /// <param name="sender">O botao.</param>
    /// <param name="e">O clique.</param>
    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var item = SelectedItem;
        if (item is null) return;

        if (string.Equals(item.Kind, "Registro", StringComparison.OrdinalIgnoreCase))
        {
            SystemNavigator.Open(new SystemNavigationTarget(
                "Cmd.OpenRegistry", "regedit.exe", $"\"{item.Path}\""));
            return;
        }

        ExternalLaunchers.OpenFolder(item.Path);
    }
}
