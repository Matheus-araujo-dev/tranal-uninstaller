using System.IO;
using System.Text.RegularExpressions;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Paridade entre o catalogo de ferramentas, a barra lateral e as telas.
///
/// O defeito que estes testes cobrem ja aconteceu: o botao "Otimizador de
/// Registro" estava na barra lateral, mas a tela nao estava registrada — o
/// clique abria um painel vazio. A captura de tela mostrava o item destacado
/// e o titulo certo, o que mascarava o problema.
///
/// O catalogo de ferramentas mora no nucleo (<see cref="ToolCatalog"/>), que e
/// compilado tambem na CLI: e a fonte unica que a barra lateral, o registro de
/// telas e estes testes usam.
/// </summary>
public class UiParityTests
{
    /// <summary>Localiza um arquivo do repositorio a partir da pasta de saida do teste.</summary>
    private static string? FindRepoFile(params string[] parts)
    {
        var relative = Path.Combine(parts);

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate)) return candidate;

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>Ids das ferramentas declaradas na barra lateral (MainWindow.xaml).</summary>
    private static List<string> SidebarToolIds()
    {
        var xaml = FindRepoFile("src", "Tranal.Uninstaller", "MainWindow.xaml");
        Assert.NotNull(xaml);

        var text = File.ReadAllText(xaml!);

        return Regex.Matches(text, @"AutomationProperties\.AutomationId=""Tool([A-Za-z]+)""")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public void CatalogoNaoTemFerramentaRepetida()
    {
        Assert.Equal(ToolCatalog.All.Count, ToolCatalog.All.Distinct(StringComparer.Ordinal).Count());
        Assert.NotEmpty(ToolCatalog.All);
    }

    [Fact]
    public void BarraLateralEFerramentasDoCatalogoSaoOsMesmos()
    {
        var naBarra = SidebarToolIds();
        var noCatalogo = ToolCatalog.All;

        var faltandoNaBarra = noCatalogo.Where(id => !naBarra.Contains(id, StringComparer.Ordinal)).ToList();
        var faltandoNoCatalogo = naBarra.Where(id => !noCatalogo.Contains(id, StringComparer.Ordinal)).ToList();

        Assert.True(faltandoNaBarra.Count == 0,
            $"Ferramentas do catalogo que nao estao na barra lateral: {string.Join(", ", faltandoNaBarra)}");
        Assert.True(faltandoNoCatalogo.Count == 0,
            $"Ferramentas na barra lateral que nao estao no catalogo: {string.Join(", ", faltandoNoCatalogo)}");
    }

    [Fact]
    public void TodaFerramentaTemTelaImplementada()
    {
        // A fabrica de telas fica no projeto grafico (WPF), que a suite nao
        // referencia de proposito: o nucleo e compilado tambem na CLI e
        // referenciar os dois criaria dois tipos "Core" diferentes.
        // A verificacao e feita sobre o fonte, que e a unica forma de
        // conferir a cobertura sem acoplar a suite ao WPF.
        var source = FindRepoFile("src", "Tranal.Uninstaller", "Ui", "Tools", "ToolViews.cs");
        Assert.NotNull(source);

        var text = File.ReadAllText(source!);

        // O id pode aparecer como literal ou pela constante do catalogo.
        var constantNames = ToolCatalogConstantNames();

        foreach (var id in ToolCatalog.All)
        {
            var viaConstant = constantNames.TryGetValue(id, out var name)
                && text.Contains("ToolCatalog." + name, StringComparison.Ordinal);

            var viaLiteral = text.Contains($"\"{id}\"", StringComparison.Ordinal);

            Assert.True(viaLiteral || viaConstant,
                $"A ferramenta '{id}' esta no catalogo mas nao tem tela em ToolViews.cs.");
        }
    }

    /// <summary>Mapeia o valor de cada constante de <see cref="ToolCatalog"/> para o nome dela.</summary>
    private static Dictionary<string, string> ToolCatalogConstantNames()
    {
        var source = FindRepoFile("src", "Tranal.Uninstaller", "Core", "ToolCatalog.cs");
        Assert.NotNull(source);

        var text = File.ReadAllText(source!);

        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Match match in Regex.Matches(text, @"public const string (\w+)\s*=\s*""([^""]+)"""))
        {
            map[match.Groups[2].Value] = match.Groups[1].Value;
        }

        return map;
    }

    [Fact]
    public void TodaFerramentaTemTituloNoCatalogoDeIdioma()
    {
        // O catalogo e carregado sob demanda. O teste nao pode depender de
        // outra classe ter carregado antes: a ordem de execucao dos testes
        // nao e garantida, e no CI esta classe rodou antes das demais.
        var catalog = Loc.Load(Loc.DefaultLanguage);

        foreach (var id in ToolCatalog.All)
        {
            var key = "Module." + id;
            Assert.True(catalog.ContainsKey(key), $"Falta a chave '{key}' no catalogo de idioma.");
        }
    }

    [Fact]
    public void OsModulosDoTracksCleanerEstaoNoCatalogo()
    {
        // Os tres limpadores separados do Revo ("Browsers Cleaner",
        // "Microsoft Office Cleaner" e "Windows Cleaner").
        foreach (var id in new[] { ToolCatalog.TracksBrowsers, ToolCatalog.TracksOffice, ToolCatalog.TracksWindows })
        {
            Assert.Contains(id, ToolCatalog.All);
        }
    }

    [Fact]
    public void AListaDeFerramentasNotificaAMarcacao()
    {
        // O menu de contexto ("marcar todos" / "desmarcar todos") altera a
        // marcacao por codigo. Sem INotifyPropertyChanged, a caixa de
        // marcacao da tela continua mostrando o estado antigo, mesmo com o
        // modelo ja alterado — foi o que aconteceu na medicao.
        var source = FindRepoFile("src", "Tranal.Uninstaller", "Ui", "Tools", "ToolViews.cs");
        Assert.NotNull(source);

        var text = File.ReadAllText(source!);

        Assert.Contains("INotifyPropertyChanged", text);
        Assert.Contains("PropertyChangedEventArgs(nameof(Selected))", text);
    }

    [Fact]
    public void AInterfaceAtendeOAlvoDoMenuDeContexto()
    {
        // O menu de contexto do Explorer chama o executavel grafico com
        // "--uninstall-target <caminho>". Se a interface nao tratar o
        // argumento, o clique apenas abre a janela principal e joga o alvo
        // fora.
        var source = FindRepoFile("src", "Tranal.Uninstaller", "App.xaml.cs");
        Assert.NotNull(source);

        var text = File.ReadAllText(source!);

        Assert.Contains("UninstallTargetArgument.Extract", text);
    }

    [Fact]
    public void OAlvoDoMenuDeContextoEExtraidoDoArgumento()
    {
        Assert.Equal(string.Empty, UninstallTargetArgument.Extract(new[] { "--outro" }));
        Assert.Equal(string.Empty, UninstallTargetArgument.Extract(new[] { "--uninstall-target" }));
        Assert.Equal(string.Empty, UninstallTargetArgument.Extract(null));

        Assert.Equal(
            @"C:\App\app.exe",
            UninstallTargetArgument.Extract(new[] { "--uninstall-target", @"C:\App\app.exe" }));

        // As aspas que o Explorer coloca no caminho nao podem sobrar.
        Assert.Equal(
            @"C:\App\app.exe",
            UninstallTargetArgument.Extract(new[] { "--uninstall-target", "\"C:\\App\\app.exe\"" }));
    }
}
