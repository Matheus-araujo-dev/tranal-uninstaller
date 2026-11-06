using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Tranal.Uninstaller.Core.Discovery;

namespace Tranal.Uninstaller.Ui.Discovery;

/// <summary>
/// Janela da descoberta ampliada (Lote E).
///
/// A janela apenas **mostra** o que os catalogos do nucleo encontram:
/// produtos MSI, recursos opcionais do Windows, atualizacoes, itens de
/// gerenciadores de pacotes, executaveis que parecem desinstalador e pastas
/// de programas sobrando.
///
/// Nada e removido aqui. A varredura de drives e lenta, por isso ela so
/// acontece quando o usuario pede — e nunca no arranque do aplicativo.
///
/// Os textos especificos desta tela sao literais em portugues porque o
/// catalogo de idiomas pertence a outro lote; a lista exata das chaves
/// sugeridas esta no RELATORIO-W4.md.
/// </summary>
public partial class DiscoveryWindow : Window
{
    /// <summary>
    /// Cria a janela ja com as categorias de busca e a grade de resultados
    /// montadas.
    /// </summary>
    public DiscoveryWindow()
    {
        InitializeComponent();

        // O titulo da janela nao e DependencyProperty: o texto vai por codigo.
        Title = "Descoberta ampliada";

        CategoryBox.ItemsSource = new List<CategoryOption>
        {
            new("Produtos MSI (Windows Installer)", DiscoveryCategory.MsiProducts),
            new("Recursos opcionais do Windows", DiscoveryCategory.WindowsFeatures),
            new("Atualizacoes do Windows", DiscoveryCategory.WindowsUpdates),
            new("Steam, Chocolatey e Scoop", DiscoveryCategory.ManagedPackages),
            new("Executaveis que parecem desinstalador", DiscoveryCategory.OrphanUninstallers),
            new("Pastas de programa vazias ou pela metade", DiscoveryCategory.EmptyFolders),
        };
        CategoryBox.SelectedIndex = 0;

        BuildGrid();
        StatusText.Text = "Escolha o que procurar e clique em Verificar.";
    }

    /// <summary>
    /// Monta as colunas da grade em codigo (o cabecalho do
    /// <c>GridViewColumn</c> nao e DependencyProperty).
    /// </summary>
    private void BuildGrid()
    {
        var view = new GridView();

        view.Columns.Add(new GridViewColumn { Header = "Item", DisplayMemberBinding = new Binding("Name") });
        view.Columns.Add(new GridViewColumn { Header = "Detalhe", DisplayMemberBinding = new Binding("Detail") });
        view.Columns.Add(new GridViewColumn { Header = "Situacao", DisplayMemberBinding = new Binding("Status") });

        ResultsList.View = view;
    }

    /// <summary>
    /// Roda a busca pedida. A varredura e pesada, por isso roda fora da
    /// thread da interface e o resultado volta para a tela.
    /// </summary>
    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        ScanButton.IsEnabled = false;
        StatusText.Text = "Procurando...";

        try
        {
            var category = (CategoryBox.SelectedItem as CategoryOption)?.Category
                ?? DiscoveryCategory.MsiProducts;

            var rows = await Task.Run(() => Collect(category));

            ResultsList.ItemsSource = rows;
            StatusText.Text = rows.Count == 0
                ? "Nada encontrado."
                : $"{rows.Count} item(ns) encontrado(s).";
        }
        catch (Exception ex)
        {
            ResultsList.ItemsSource = new List<DiscoveryRow>();
            StatusText.Text = "Falha na busca: " + ex.Message;
        }
        finally
        {
            ScanButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Fecha a janela.
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Coleta os itens de uma categoria.
    /// </summary>
    private static List<DiscoveryRow> Collect(DiscoveryCategory category)
    {
        var rows = new List<DiscoveryRow>();

        switch (category)
        {
            case DiscoveryCategory.MsiProducts:
                foreach (var product in MsiProductCatalog.ListInstalled())
                {
                    rows.Add(new DiscoveryRow
                    {
                        Name = product.DisplayName,
                        Detail = product.InstallLocation.Length > 0
                            ? product.InstallLocation
                            : product.ProductCode,
                        Status = product.QuietUninstallCommand,
                    });
                }
                break;

            case DiscoveryCategory.WindowsFeatures:
                var features = WindowsFeatureCatalog.List();
                foreach (var feature in features.Items)
                {
                    rows.Add(new DiscoveryRow
                    {
                        Name = feature.Name,
                        Detail = feature.State,
                        Status = feature.Availability == DiscoveryAvailability.Installed
                            ? "Instalado"
                            : "Disponivel",
                    });
                }
                break;

            case DiscoveryCategory.WindowsUpdates:
                var updates = WindowsUpdateCatalog.ListInstalled();
                foreach (var update in updates.Items)
                {
                    rows.Add(new DiscoveryRow
                    {
                        Name = update.Title,
                        Detail = update.KbArticles,
                        Status = update.InstalledOn?.ToString("dd/MM/yyyy") ?? "Instalada",
                    });
                }
                break;

            case DiscoveryCategory.ManagedPackages:
                foreach (var package in PackageManagerCatalog.Discover(PackageManagerRoots.Detect()))
                {
                    rows.Add(new DiscoveryRow
                    {
                        Name = package.Name,
                        Detail = package.InstallPath,
                        Status = package.HasOwnUninstaller
                            ? "Desinstalador proprio"
                            : "Sem desinstalador proprio",
                    });
                }
                break;

            case DiscoveryCategory.OrphanUninstallers:
                var scan = DriveScanner.Scan(DriveScanOptions.Default());
                foreach (var orphan in scan.Found)
                {
                    rows.Add(new DiscoveryRow
                    {
                        Name = orphan.FileName,
                        Detail = orphan.FolderPath,
                        Status = "Candidato: confirme se o Painel de Controle nao conhece",
                    });
                }
                break;

            case DiscoveryCategory.EmptyFolders:
                foreach (var candidate in EmptyFolderCleaner.Find(EmptyFolderCleaner.DefaultRoots()))
                {
                    rows.Add(new DiscoveryRow
                    {
                        Name = candidate.Path,
                        Detail = candidate.Reason,
                        Status = candidate.IsEmpty ? "Vazia" : "Pela metade",
                    });
                }
                break;
        }

        return rows;
    }

    /// <summary>
    /// As categorias de busca da janela.
    /// </summary>
    private enum DiscoveryCategory
    {
        MsiProducts,
        WindowsFeatures,
        WindowsUpdates,
        ManagedPackages,
        OrphanUninstallers,
        EmptyFolders,
    }

    /// <summary>
    /// Uma opcao da lista de categorias.
    /// </summary>
    private sealed class CategoryOption
    {
        public CategoryOption(string text, DiscoveryCategory category)
        {
            Text = text;
            Category = category;
        }

        /// <summary>Texto mostrado na lista.</summary>
        public string Text { get; }

        /// <summary>Categoria escondida por tras do texto.</summary>
        public DiscoveryCategory Category { get; }
    }

    /// <summary>
    /// Uma linha da grade de resultados.
    /// </summary>
    private sealed class DiscoveryRow
    {
        /// <summary>Nome do item encontrado.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Detalhe (caminho, estado, versao).</summary>
        public string Detail { get; init; } = string.Empty;

        /// <summary>Situacao do item.</summary>
        public string Status { get; init; } = string.Empty;
    }
}
