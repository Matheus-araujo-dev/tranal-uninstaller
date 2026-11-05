using System.Windows;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// A janela de comentario do usuario sobre um programa.
///
/// Corresponde ao "Add Comment" (15216), "Edit Comment" (15217) e ao
/// "Enter your comment:" (15281) do Revo.
/// </summary>
public sealed partial class CommentWindow : Window
{
    /// <summary>O comentario escrito (vazio quando o usuario apagou tudo).</summary>
    public string Comment { get; private set; } = string.Empty;

    public CommentWindow(string initial = "")
    {
        InitializeComponent();

        Title = Loc.Get(string.IsNullOrWhiteSpace(initial) ? "Comment.AddTitle" : "Comment.EditTitle");
        PromptText.Text = Loc.Get("Comment.Prompt");
        CommentBox.Text = initial ?? string.Empty;
        CommentBox.Focus();
        CommentBox.SelectAll();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Comment = (CommentBox.Text ?? string.Empty).Trim();
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
