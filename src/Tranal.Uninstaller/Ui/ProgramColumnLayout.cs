using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// A grade (GridView) da lista de programas.
///
/// A grade e montada em codigo porque o usuario escolhe quais colunas quer
/// ver (o "Columns" do Revo, 15054): no XAML, a grade seria uma segunda
/// copia do catalogo de colunas, e as duas divergiriam na primeira
/// alteracao.
/// </summary>
public static class ProgramColumnLayout
{
    /// <summary>A largura padrao de cada coluna.</summary>
    public static double WidthOf(ProgramColumn column) => column switch
    {
        ProgramColumn.Name => 250,
        ProgramColumn.Size => 95,
        ProgramColumn.Version => 130,
        ProgramColumn.Type => 70,
        ProgramColumn.Date => 85,
        ProgramColumn.Company => 150,
        ProgramColumn.HomePage => 160,
        ProgramColumn.Comments => 170,
        ProgramColumn.UninstallString => 220,
        ProgramColumn.UninstallerKind => 150,
        ProgramColumn.ProductCode => 190,
        ProgramColumn.QuietUninstall => 240,
        ProgramColumn.Group => 120,
        ProgramColumn.Note => 180,
        ProgramColumn.RegistryKey => 220,
        _ => 120,
    };

    /// <summary>
    /// A propriedade da linha que a coluna mostra. Nulo quando a coluna nao
    /// vem de um campo da linha (a coluna do nome tem template proprio).
    /// </summary>
    public static string? PropertyOf(ProgramColumn column) => column switch
    {
        ProgramColumn.Size => nameof(ProgramRow.SizeText),
        ProgramColumn.Version => nameof(ProgramRow.DisplayVersion),
        ProgramColumn.Type => nameof(ProgramRow.Bits),
        ProgramColumn.Date => nameof(ProgramRow.InstallDate),
        ProgramColumn.Company => nameof(ProgramRow.Publisher),
        ProgramColumn.HomePage => nameof(ProgramRow.UrlInfoAbout),
        ProgramColumn.Comments => nameof(ProgramRow.Comments),
        ProgramColumn.UninstallString => nameof(ProgramRow.UninstallString),
        ProgramColumn.UninstallerKind => nameof(ProgramRow.UninstallerKindText),
        ProgramColumn.ProductCode => nameof(ProgramRow.ProductCodeText),
        ProgramColumn.QuietUninstall => nameof(ProgramRow.QuietUninstallText),
        ProgramColumn.Group => nameof(ProgramRow.UserGroup),
        ProgramColumn.Note => nameof(ProgramRow.UserNote),
        ProgramColumn.RegistryKey => nameof(ProgramRow.RegistryKeyText),
        _ => null,
    };

    /// <summary>
    /// A propriedade que ordena a coluna. A coluna do nome nao tem
    /// propriedade de binding (ela usa o proprio template), mas ordena pelo
    /// nome — como no Revo.
    /// </summary>
    public static string? SortPropertyOf(ProgramColumn column) => column switch
    {
        ProgramColumn.Name => nameof(ProgramRow.DisplayName),
        _ => PropertyOf(column),
    };

    /// <summary>Monta a grade com as colunas escolhidas, na ordem do catalogo.</summary>
    public static GridView Build(IReadOnlyList<ProgramColumn> columns, DataTemplate nameTemplate)
    {
        var grid = new GridView();

        foreach (var column in columns)
        {
            var gridColumn = new GridViewColumn
            {
                Header = ProgramColumns.Label(column),
                Width = WidthOf(column),
            };

            if (column == ProgramColumn.Name)
            {
                gridColumn.CellTemplate = nameTemplate;
            }
            else
            {
                var property = PropertyOf(column);
                if (property is not null) gridColumn.DisplayMemberBinding = new Binding(property);
            }

            grid.Columns.Add(gridColumn);
        }

        return grid;
    }
}
