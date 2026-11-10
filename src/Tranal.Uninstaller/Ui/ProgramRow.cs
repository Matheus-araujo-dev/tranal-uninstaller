using System.ComponentModel;
using System.Windows.Media;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;

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

    /// <summary>
    /// As marcas do programa (32 bits, atualizacao, orfao, invalido,
    /// protegido).
    /// </summary>
    public ProgramMarkInfo Marks => ProgramMarks.Detect(Program);

    /// <summary>
    /// O texto da coluna "Marcas".
    /// </summary>
    public string MarksText => ProgramMarks.Text(Program);

    /// <summary>
    /// O nome **exibido**: o nome do programa com a marca de 32 bits
    /// embutida. E o que a pessoa le na lista.
    ///
    /// O <see cref="DisplayName"/> continua sendo o nome cru: ele e a
    /// identidade do programa (a selecao, a copia e a exportacao casam por
    /// ele), e mudar a identidade para caber na tela seria trocar uma coisa
    /// certa por outra errada.
    /// </summary>
    public string Label => ProgramMarks.WithBits(DisplayName, Marks);
    public string DisplayVersion => ProgramColumns.Value(ProgramColumn.Version, Program, Custom);
    public string Publisher => ProgramColumns.Value(ProgramColumn.Company, Program, Custom);
    public string UrlInfoAbout => ProgramColumns.Value(ProgramColumn.HomePage, Program, Custom);
    public string Comments => ProgramColumns.Value(ProgramColumn.Comments, Program, Custom);
    public string UninstallString => ProgramColumns.Value(ProgramColumn.UninstallString, Program, Custom);
    public string UninstallerKindText => ProgramColumns.Value(ProgramColumn.UninstallerKind, Program, Custom);
    public string ProductCodeText => ProgramColumns.Value(ProgramColumn.ProductCode, Program, Custom);
    public string QuietUninstallText => ProgramColumns.Value(ProgramColumn.QuietUninstall, Program, Custom);
    public string InstallDate => ProgramColumns.Value(ProgramColumn.Date, Program, Custom);
    public string Bits => ProgramColumns.Value(ProgramColumn.Type, Program, Custom);
    public string SizeText => ProgramColumns.Value(ProgramColumn.Size, Program, Custom);

    // As seis colunas novas. Todas delegam ao catalogo de colunas: a linha
    // nao recalcula nada, e a coluna e a fonte do valor.
    public string UninstallerLocationText => ProgramColumns.Value(ProgramColumn.UninstallerLocation, Program, Custom);
    public string SourceText => ProgramColumns.Value(ProgramColumn.Source, Program, Custom);
    public string SystemComponentText => ProgramColumns.Value(ProgramColumn.SystemComponent, Program, Custom);
    public string ProtectedText => ProgramColumns.Value(ProgramColumn.Protected, Program, Custom);
    public string StartupText => ProgramColumns.Value(ProgramColumn.Startup, Program, Custom);
    public string RatingText => ProgramColumns.Value(ProgramColumn.Rating, Program, Custom);

    /// <summary>Os grupos que o usuario criou para este programa (15072: "Group").</summary>
    public string UserGroup => ProgramColumns.Value(ProgramColumn.Group, Program, Custom);

    /// <summary>A anotacao do usuario sobre este programa (15216: "Add Comment").</summary>
    public string UserNote => ProgramColumns.Value(ProgramColumn.Note, Program, Custom);

    /// <summary>A chave do registro de desinstalacao do programa.</summary>
    public string RegistryKeyText => ProgramColumns.Value(ProgramColumn.RegistryKey, Program, Custom);

    /// <summary>
    /// O pais do editor, com a bandeira ("Brasil 🇧🇷"). Vazio quando o
    /// executavel nao diz de onde e — a celula vazia e melhor que um palpite.
    /// </summary>
    public string CountryText => ProgramColumns.Value(ProgramColumn.Country, Program, Custom);

    /// <summary>
    /// A data da instalacao em tempo relativo ("Hoje", "Ontem", "3 semanas
    /// atras").
    /// </summary>
    public string RelativeDateText => ProgramColumns.Value(ProgramColumn.RelativeDate, Program, Custom);

    /// <summary>
    /// A plataforma de origem do programa (Steam, Epic, Chocolatey...). Vazio
    /// quando o programa nao veio de nenhuma loja ou gerenciador conhecido.
    /// </summary>
    public string PlatformText => ProgramColumns.Value(ProgramColumn.Platform, Program, Custom);

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

    private string _highlightColorKey = string.Empty;

    /// <summary>
    /// A chave de cor do realce desta linha (vazia quando nenhum realce
    /// casa). Quem decide a cor e o nucleo
    /// (<see cref="HighlightPalette"/>); a linha so guarda a chave.
    /// </summary>
    public string HighlightColorKey
    {
        get => _highlightColorKey;
        set
        {
            if (string.Equals(_highlightColorKey, value, StringComparison.Ordinal)) return;

            _highlightColorKey = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HighlightColorKey)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HighlightTextBrush)));
        }
    }

    /// <summary>
    /// O pincel do texto quando a linha esta realçada. Nulo: o nome usa a cor
    /// do tema.
    /// </summary>
    public Brush? HighlightTextBrush
    {
        get
        {
            var hex = HighlightPalette.HexOf(_highlightColorKey);
            if (hex is null || hex.Length != 9) return null;

            try
            {
                var a = Convert.ToByte(hex.Substring(1, 2), 16);
                var r = Convert.ToByte(hex.Substring(3, 2), 16);
                var g = Convert.ToByte(hex.Substring(5, 2), 16);
                var b = Convert.ToByte(hex.Substring(7, 2), 16);

                return new SolidColorBrush(Color.FromArgb(a, r, g, b));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ImageSource? Icon { get; }
}
