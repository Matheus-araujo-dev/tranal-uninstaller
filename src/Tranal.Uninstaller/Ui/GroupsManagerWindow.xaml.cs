using System.Windows;
using System.Windows.Controls;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// O Gerenciador de grupos.
///
/// Corresponde ao "Groups Manager" (15001) do Revo, com o "Group name:"
/// (15070), o "Add" (15071) e o "Add new Group" (15173).
///
/// Renomear um grupo leva os programas junto: um grupo renomeado nao pode
/// deixar os programas orfaos.
/// </summary>
public sealed partial class GroupsManagerWindow : Window
{
    public GroupsManagerWindow()
    {
        InitializeComponent();

        // Window.Title nao aceita binding: o texto vai por codigo, para o
        // titulo acompanhar o idioma da interface.
        Title = Loc.Get("Groups.Title");

        Reload();
    }

    private void Reload()
    {
        GroupsList.ItemsSource = ProgramCustomizationStore.Groups.ToList();
        EmptyText.Visibility = ProgramCustomizationStore.Groups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private string SelectedGroup => GroupsList.SelectedItem as string ?? string.Empty;

    /// <summary>
    /// A mensagem de um resultado de operacao sobre grupos.
    ///
    /// "O nome ja existe" e "nao deu para gravar" sao coisas diferentes: a
    /// primeira e culpa do nome escolhido, a segunda e da pasta de dados.
    /// Mostrar a mesma mensagem para as duas esconde um problema real.
    /// </summary>
    private string MessageOf(GroupResult result) => result switch
    {
        GroupResult.AlreadyExists => Loc.Get("Groups.AlreadyExists"),
        GroupResult.NotFound => Loc.Get("Groups.SelectFirst"),
        GroupResult.InvalidName => Loc.Get("Groups.NameRequired"),
        GroupResult.SaveFailed => Loc.Get("ProgramData.SaveError"),
        _ => string.Empty,
    };

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var name = (NameBox.Text ?? string.Empty).Trim();

        if (name.Length == 0)
        {
            StatusText.Text = Loc.Get("Groups.NameRequired");
            return;
        }

        var resultado = ProgramCustomizationStore.CreateGroup(name);
        if (resultado != GroupResult.Ok)
        {
            StatusText.Text = MessageOf(resultado);
            return;
        }

        NameBox.Text = string.Empty;
        StatusText.Text = Loc.Get("Groups.Created", name);
        Reload();
    }

    private void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        var atual = SelectedGroup;
        if (atual.Length == 0)
        {
            StatusText.Text = Loc.Get("Groups.SelectFirst");
            return;
        }

        var novo = (NameBox.Text ?? string.Empty).Trim();
        if (novo.Length == 0)
        {
            // Renomear sem digitar nada mantem o nome: o usuario pode
            // querer so confirmar.
            StatusText.Text = Loc.Get("Groups.RenameHint");
            return;
        }

        var resultado = ProgramCustomizationStore.RenameGroup(atual, novo);
        if (resultado != GroupResult.Ok)
        {
            StatusText.Text = MessageOf(resultado);
            return;
        }

        NameBox.Text = string.Empty;
        StatusText.Text = Loc.Get("Groups.Renamed", atual, novo);
        Reload();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var group = SelectedGroup;
        if (group.Length == 0)
        {
            StatusText.Text = Loc.Get("Groups.SelectFirst");
            return;
        }

        // Apagar o grupo e uma classificacao, nao uma desinstalacao: os
        // programas continuam instalados, so deixam de estar no grupo.
        var resultado = ProgramCustomizationStore.DeleteGroup(group);
        if (resultado != GroupResult.Ok)
        {
            StatusText.Text = MessageOf(resultado);
            return;
        }

        StatusText.Text = Loc.Get("Groups.Deleted", group);
        Reload();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // O botao e o "Cancel" da janela: o ESC tambem fecha. O DialogResult
        // nao importa aqui — quem chama recarrega a lista de qualquer jeito.
        Close();
    }
}
