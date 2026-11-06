using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// Um icone da galeria: o caminho do arquivo e o nome do programa que o
/// traz.
/// </summary>
public sealed class IconChoice
{
    public string Path { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public ImageSource? Image { get; init; }
}

/// <summary>
/// A janela de escolha de icone.
///
/// Corresponde ao "Change Icon" (15079), "Choose Icon" (15282), "Select an
/// icon from the list:" (15284), "File path:" (15283) e "Icon index"
/// (15240) do Revo.
///
/// A galeria e montada com os icones que ja existem na maquina — os dos
/// programas instalados. Copiar uma galeria de icones de terceiros seria
/// distribuir material de terceiros; a galeria do Tranal usa o que o
/// proprio Windows ja tem.
/// </summary>
public sealed partial class IconPickerWindow : Window
{
    private readonly List<IconChoice> _choices = new();

    /// <summary>O caminho do icone escolhido.</summary>
    public string SelectedIconPath { get; private set; } = string.Empty;

    /// <summary>
    /// A posicao do icone escolhido na galeria (15240: "Icon index").
    /// -1 quando o icone veio de um arquivo escolhido pelo usuario.
    /// </summary>
    public int SelectedIconIndex { get; private set; } = -1;

    /// <param name="choices">A galeria de icones.</param>
    /// <param name="current">O icone que o programa ja usa.</param>
    public IconPickerWindow(IEnumerable<IconChoice> choices, string? current = null)
    {
        InitializeComponent();

        Title = Loc.Get("Icon.Title");
        PromptText.Text = Loc.Get("Icon.Prompt");

        _choices.AddRange(choices);
        GalleryList.ItemsSource = _choices;

        if (!string.IsNullOrWhiteSpace(current)) PathBox.Text = current;
    }

    private void GalleryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GalleryList.SelectedItem is not IconChoice choice) return;

        PathBox.Text = choice.Path;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.Get("Icon.BrowseTitle"),
            Filter = Loc.Get("Icon.BrowseFilter"),
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) == true) PathBox.Text = dialog.FileName;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var path = (PathBox.Text ?? string.Empty).Trim();

        // Um caminho que nao existe nao serve: o programa voltaria a ficar
        // sem icone, e o usuario nao entenderia o porque.
        if (path.Length > 0 && !File.Exists(path))
        {
            PathBox.Focus();
            PathBox.SelectAll();
            return;
        }

        // Um arquivo que existe mas nao e imagem tambem nao serve: aceitar
        // deixaria o programa sem icone nenhum, porque o caminho do usuario
        // tem precedencia sobre o icone do proprio programa.
        if (path.Length > 0 && LoadIcon(path) is null)
        {
            MessageBox.Show(this,
                Loc.Get("Icon.NotAnImage"),
                Loc.Get("Icon.Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            PathBox.Focus();
            PathBox.SelectAll();
            return;
        }

        SelectedIconPath = path;
        SelectedIconIndex = _choices.FindIndex(c => string.Equals(c.Path, path, StringComparison.OrdinalIgnoreCase));

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>Carrega um icone de disco. Devolve null quando nao da para.</summary>
    public static ImageSource? LoadIcon(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        try
        {
            // O campo DisplayIcon do registro costuma apontar para o proprio
            // executavel do programa. O WPF nao decodifica executavel: ele
            // copia o arquivo inteiro para a memoria antes de descobrir que
            // ele nao e uma imagem (um Chrome de 4 MB por linha da lista).
            // Reconhecer a imagem pela assinatura evita essa copia, e o
            // resultado continua o mesmo: executavel nao vira icone.
            if (!EhImagem(path)) return null;

            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 32;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// True quando o arquivo comeca pela assinatura de uma imagem que o WPF
    /// decodifica (PNG, JPEG, GIF, BMP, ICO, TIFF, WMF, EMF).
    /// </summary>
    private static bool EhImagem(string path)
    {
        Span<byte> assinatura = stackalloc byte[12];

        int lidos;
        try
        {
            using var fluxo = File.OpenRead(path);
            lidos = fluxo.Read(assinatura);
        }
        catch
        {
            return false;
        }

        if (lidos >= 8 && assinatura[0] == 0x89 && assinatura[1] == 'P' && assinatura[2] == 'N' && assinatura[3] == 'G')
            return true; // PNG

        if (lidos >= 3 && assinatura[0] == 0xFF && assinatura[1] == 0xD8 && assinatura[2] == 0xFF)
            return true; // JPEG

        if (lidos >= 6 && assinatura[0] == 'G' && assinatura[1] == 'I' && assinatura[2] == 'F' && assinatura[3] == '8')
            return true; // GIF

        if (lidos >= 2 && assinatura[0] == 'B' && assinatura[1] == 'M')
            return true; // BMP

        if (lidos >= 4 && assinatura[0] == 0x00 && assinatura[1] == 0x00 && assinatura[2] == 0x01 && assinatura[3] == 0x00)
            return true; // ICO

        if (lidos >= 4 && assinatura[0] == 'I' && assinatura[1] == 'I' && assinatura[2] == 0x2A && assinatura[3] == 0x00)
            return true; // TIFF

        if (lidos >= 4 && assinatura[0] == 'M' && assinatura[1] == 'M' && assinatura[2] == 0x00 && assinatura[3] == 0x2A)
            return true; // TIFF

        if (lidos >= 4 && assinatura[0] == 0xD7 && assinatura[1] == 0xCD && assinatura[2] == 0xC6 && assinatura[3] == 0x9A)
            return true; // WMF

        if (lidos >= 4 && assinatura[0] == 0x01 && assinatura[1] == 0x00 && assinatura[2] == 0x00 && assinatura[3] == 0x00)
            return true; // EMF

        return false;
    }
}
