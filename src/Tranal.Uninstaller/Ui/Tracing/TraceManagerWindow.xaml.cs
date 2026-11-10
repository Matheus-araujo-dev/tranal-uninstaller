using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Reporting;

namespace Tranal.Uninstaller.Ui.Tracing;

/// <summary>
/// O gerenciador do log de rastreio.
///
/// <para>O log de instalacao e a fonte do que o instalador gravou no
/// computador — e e ele que a desinstalacao dirigida pelo log remove. Esta
/// tela deixa a pessoa corrigir esse log ANTES da remocao: excluir o item que
/// nao era do programa e acrescentar o que o rastreio nao viu.</para>
///
/// <para>A REGRA mora no nucleo: <c>TraceLogEditor</c> (o que muda no log) e
/// <c>LogOperations</c> (o que e uma entrada, o que e ignorar, o que e
/// acrescentar a mao). A gravacao do arquivo e do <c>TraceStore</c>, que
/// grava de forma atomica e deixa a versao anterior em <c>.bak</c>.</para>
///
/// <para>Esta tela nao decide o que e seguro apagar: isso e do motor, com a
/// confianca que ele mede. Ela so organiza o que o log vai dizer.</para>
/// </summary>
public sealed partial class TraceManagerWindow : Window
{
    private TracedInstall _log;

    private readonly ObservableCollection<ItemRow> _itens = new();

    /// <summary>
    /// Abre o gerenciador de um log.
    /// </summary>
    /// <param name="log">O log a gerenciar.</param>
    public TraceManagerWindow(TracedInstall log)
    {
        InitializeComponent();

        _log = log;

        Title = Loc.Get("Mod.TraceManager.Title");
        ItemsView.ItemsSource = _itens;

        BuildKindChoices();
        BuildColumns();
        ApplyTexts();

        Reload();
    }

    /// <summary>Rotulos dos tipos de item que o log aceita.</summary>
    private static string KindText(LogEntryKind kind, bool isFolder) => kind switch
    {
        LogEntryKind.RegistryKey => Loc.Get("Mod.TraceManager.Kind.RegistryKey"),
        _ => isFolder ? Loc.Get("Mod.TraceManager.Kind.Folder") : Loc.Get("Mod.TraceManager.Kind.File"),
    };

    /// <summary>Rotulo do estado da entrada no log.</summary>
    private static string StateText(LogEntry entry)
    {
        if (entry.Ignored) return Loc.Get("Mod.TraceManager.State.Ignored");
        if (entry.Added) return Loc.Get("Mod.TraceManager.State.Added");
        return Loc.Get("Mod.TraceManager.State.Log");
    }

    /// <summary>Escreve os textos da janela a partir do catalogo de idioma.</summary>
    private void ApplyTexts()
    {
        TitleText.Text = Loc.Get("Mod.TraceManager.Title");
        SubtitleText.Text = Loc.Get("Mod.TraceManager.Hint");

        AddButton.Content = Loc.Get("Mod.TraceManager.Add");
        RemoveButton.Content = Loc.Get("Mod.TraceManager.Remove");
        CloseButton.Content = Loc.Get("Common.Close");

        KindCombo.ToolTip = Loc.Get("Mod.TraceManager.Kind");
        PathBox.ToolTip = Loc.Get("Mod.TraceManager.Path");
    }

    /// <summary>Preenche a lista de tipos que o item pode ter.</summary>
    private void BuildKindChoices()
    {
        KindCombo.Items.Add(new KindChoice(Loc.Get("Mod.TraceManager.Kind.File"), ItemKind.File));
        KindCombo.Items.Add(new KindChoice(Loc.Get("Mod.TraceManager.Kind.Folder"), ItemKind.Folder));
        KindCombo.Items.Add(new KindChoice(Loc.Get("Mod.TraceManager.Kind.RegistryKey"), ItemKind.RegistryKey));
        KindCombo.SelectedIndex = 0;
    }

    /// <summary>Monta as colunas da lista de itens.</summary>
    private void BuildColumns()
    {
        if (ItemsView.View is not GridView view) return;

        view.Columns.Clear();

        view.Columns.Add(new GridViewColumn { Header = Loc.Get("Mod.TraceManager.Kind"), Width = 150, DisplayMemberBinding = new System.Windows.Data.Binding("Tipo") });
        view.Columns.Add(new GridViewColumn { Header = Loc.Get("Mod.TraceManager.Path"), Width = 540, DisplayMemberBinding = new System.Windows.Data.Binding("Caminho") });
        view.Columns.Add(new GridViewColumn { Header = Loc.Get("Common.Status"), Width = 120, DisplayMemberBinding = new System.Windows.Data.Binding("Estado") });

        foreach (var column in view.Columns)
        {
            if (TryFindResource("ListViewHeaderStyle") is Style headerStyle)
                column.Header = new GridViewColumnHeader { Content = column.Header, Style = headerStyle };
        }
    }

    /// <summary>Relê o log e refaz a lista de itens.</summary>
    private void Reload()
    {
        _itens.Clear();

        foreach (var entrada in LogOperations.Entries(_log))
        {
            _itens.Add(new ItemRow
            {
                Tipo = KindText(entrada.Kind, entrada.IsFolder),
                Caminho = entrada.Path,
                Estado = StateText(entrada),
            });
        }

        SubtitleText.Text = Loc.Get("Mod.TraceManager.Hint") + "  —  "
            + string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0}: {1}",
                Loc.Get("Mod.TraceManager.Items"), _itens.Count);
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var caminho = (PathBox.Text ?? string.Empty).Trim();
        if (caminho.Length == 0)
        {
            StatusText.Text = Loc.Get("Mod.TraceManager.NeedPath");
            return;
        }

        var tipo = KindCombo.SelectedItem is KindChoice escolha ? escolha.Kind : ItemKind.File;

        var resultado = tipo switch
        {
            ItemKind.Folder => TraceLogEditor.AddFolder(_log, caminho),
            ItemKind.RegistryKey => TraceLogEditor.AddRegistryKey(_log, caminho),
            _ => TraceLogEditor.AddFile(_log, caminho),
        };

        if (resultado is null)
        {
            StatusText.Text = Loc.Get("Mod.TraceManager.Failed");
            return;
        }

        _log = resultado;
        PathBox.Text = string.Empty;
        StatusText.Text = Loc.Get("Mod.TraceManager.Added");

        Reload();
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (ItemsView.SelectedItem is not ItemRow item)
        {
            StatusText.Text = Loc.Get("Mod.TraceManager.NeedItem");
            return;
        }

        var resultado = TraceLogEditor.Remove(_log, item.Caminho);
        if (resultado is null)
        {
            StatusText.Text = Loc.Get("Mod.TraceManager.Failed");
            return;
        }

        _log = resultado;
        StatusText.Text = Loc.Get("Mod.TraceManager.Removed");

        Reload();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>O tipo de item que o gerenciador trabalha.</summary>
    private enum ItemKind
    {
        /// <summary>Um arquivo.</summary>
        File,

        /// <summary>Uma pasta.</summary>
        Folder,

        /// <summary>Uma chave de registro.</summary>
        RegistryKey,
    }

    /// <summary>Uma escolha da lista de tipos.</summary>
    private sealed class KindChoice
    {
        public KindChoice(string texto, ItemKind kind)
        {
            Text = texto;
            Kind = kind;
        }

        /// <summary>O texto traduzido do tipo.</summary>
        public string Text { get; }

        /// <summary>O tipo que a escolha representa.</summary>
        public ItemKind Kind { get; }

        /// <summary>O texto que a lista mostra.</summary>
        public override string ToString() => Text;
    }

    /// <summary>Uma linha da lista de itens do log.</summary>
    private sealed class ItemRow
    {
        /// <summary>O tipo do item (arquivo, pasta ou chave).</summary>
        public string Tipo { get; init; } = string.Empty;

        /// <summary>O caminho do item.</summary>
        public string Caminho { get; init; } = string.Empty;

        /// <summary>O estado da entrada no log.</summary>
        public string Estado { get; init; } = string.Empty;
    }
}
