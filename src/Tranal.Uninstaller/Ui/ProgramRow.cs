using System.ComponentModel;
using System.Windows.Media;
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

    /// <summary>
    /// A anotacao do usuario sobre o programa (comentario, icone, grupos).
    /// Nulo quando o programa ainda nao foi anotado.
    /// </summary>
    public ProgramCustomization? Custom { get; }

    public ProgramRow(InstalledProgram program)
    {
        Program = program;

        Custom = ProgramCustomizationStore.Has(program) ? ProgramCustomizationStore.For(program) : null;

        // O icone escolhido pelo usuario tem precedencia sobre o icone do
        // proprio programa (o "Change Icon" do Revo, 15079). Se o caminho
        // escolhido nao carregar, o icone do programa continua valendo: sem
        // icone nenhum, o usuario perderia a referencia do programa.
        var escolhido = string.IsNullOrWhiteSpace(Custom?.IconPath) ? null : Custom!.IconPath;
        Icon = (escolhido is not null ? IconPickerWindow.LoadIcon(escolhido) : null)
            ?? IconPickerWindow.LoadIcon(program.IconPath);
    }

    public string DisplayName => ProgramColumns.Value(ProgramColumn.Name, Program, Custom);
    public string DisplayVersion => ProgramColumns.Value(ProgramColumn.Version, Program, Custom);
    public string Publisher => ProgramColumns.Value(ProgramColumn.Company, Program, Custom);
    public string UrlInfoAbout => ProgramColumns.Value(ProgramColumn.HomePage, Program, Custom);
    public string Comments => ProgramColumns.Value(ProgramColumn.Comments, Program, Custom);
    public string UninstallString => ProgramColumns.Value(ProgramColumn.UninstallString, Program, Custom);
    public string UninstallerKindText => ProgramColumns.Value(ProgramColumn.UninstallerKind, Program, Custom);
    public string ProductCodeText => ProgramColumns.Value(ProgramColumn.ProductCode, Program, Custom);
    public string InstallDate => ProgramColumns.Value(ProgramColumn.Date, Program, Custom);
    public string Bits => ProgramColumns.Value(ProgramColumn.Type, Program, Custom);
    public string SizeText => ProgramColumns.Value(ProgramColumn.Size, Program, Custom);

    /// <summary>Os grupos que o usuario criou para este programa (15072: "Group").</summary>
    public string UserGroup => ProgramColumns.Value(ProgramColumn.Group, Program, Custom);

    /// <summary>A anotacao do usuario sobre este programa (15216: "Add Comment").</summary>
    public string UserNote => ProgramColumns.Value(ProgramColumn.Note, Program, Custom);

    /// <summary>A chave do registro de desinstalacao do programa.</summary>
    public string RegistryKeyText => ProgramColumns.Value(ProgramColumn.RegistryKey, Program, Custom);

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
}
