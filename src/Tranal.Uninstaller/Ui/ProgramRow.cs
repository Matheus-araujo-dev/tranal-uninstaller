using System.IO;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// Uma linha da lista de programas: o modelo exibido + o icone extraido do campo
/// DisplayIcon do registro (com fallback para um icone generico).
///
/// Implementa <see cref="INotifyPropertyChanged"/> porque a caixa de marcacao
/// e ligada em modo bidirecional: sem a notificacao, uma alteracao feita por
/// codigo deixa a tela mostrando o estado antigo.
/// </summary>
public sealed class ProgramRow : INotifyPropertyChanged
{
    public InstalledProgram Program { get; }

    public ProgramRow(InstalledProgram program)
    {
        Program = program;
        Icon = LoadIcon(program.IconPath);
    }

    public string DisplayName => Program.DisplayName;
    public string DisplayVersion => Program.DisplayVersion;
    public string Publisher => Program.Publisher;
    public string UrlInfoAbout => Program.UrlInfoAbout;
    public string Comments => Program.Comments;
    public string UninstallString => Program.UninstallString;
    public string InstallDate => Program.InstallDate;
    public string Bits => Program.Bits;
    public string SizeText => Program.SizeText;

    /// <summary>
    /// Janela de "programas novos". O Revo usa o par "New programs type" / "New Days"
    /// em HKCU\Software\VS Revo Group\Revo Uninstaller\Uninstaller, com o padrao de 7 dias.
    /// O corte conta o dia inteiro: a partir da meia-noite de (hoje - 7 dias).
    /// </summary>
    public static int NewProgramsDays =>
        Math.Max(1, Core.Settings.AppSettingsStore.Current.NewProgramsDays);

    /// <summary>"Novos Programas" (instalados na janela de 7 dias) ou "Outros Programas".</summary>
    public string Group => Program.InstallDateValue is { } date && date >= NewProgramsCutoff
        ? Core.Localization.Loc.Get("Group.New")
        : Core.Localization.Loc.Get("Group.Other");

    private static DateTime NewProgramsCutoff
    {
        get
        {
            var cutoff = DateTime.Now.AddDays(-NewProgramsDays);
            return new DateTime(cutoff.Year, cutoff.Month, cutoff.Day);
        }
    }

    private bool _isChecked = true;

    /// <summary>Caixa de marcacao da linha (indicador visual, como no Revo).</summary>
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;

            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ImageSource? Icon { get; }

    private static ImageSource? LoadIcon(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            var uri = new Uri(path, UriKind.Absolute);
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = uri;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 32;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch { return null; }
    }
}
