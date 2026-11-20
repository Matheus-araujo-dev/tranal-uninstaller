using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Settings;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// A interface leve (o estilo do Tranal Sentinel): a lista de programas na
/// frente, as ferramentas em uma pagina propria e cada ferramenta com o guia
/// "o que faz / o que voce ganha / quando usar / cuidado".
///
/// O guia so funciona se estiver completo: uma ferramenta sem guia volta a ser
/// um botao que ninguem sabe para que serve. Os testes conferem a regra do
/// nucleo (<see cref="ToolGuide"/>), os dois catalogos de idioma e a tela
/// (sobre o fonte, porque a suite nao referencia o projeto WPF).
/// </summary>
public sealed class InterfaceLeveTests
{
    private static Dictionary<string, string> Catalogo(string idioma)
    {
        var texto = RepoFiles.Read("src", "Tranal.Uninstaller", "Lang", idioma + ".json");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(texto)!;
    }

    private static IEnumerable<string> TelasComGuia() => ToolCatalog.All.Concat(ToolGuide.Modules);

    [Fact]
    public void TodaFerramentaTemUmaCategoria()
    {
        var semCategoria = ToolCatalog.All.Where(id => ToolGuide.CategoryOf(id) is null).ToList();
        Assert.True(semCategoria.Count == 0, "Ferramentas sem categoria: " + string.Join(", ", semCategoria));

        // Toda ferramenta aparece em exatamente uma categoria da pagina.
        var naPagina = Enum.GetValues<ToolCategory>().SelectMany(ToolGuide.ToolsOf).ToList();
        Assert.Equal(ToolCatalog.All.Count, naPagina.Count);
        Assert.Equal(ToolCatalog.All.OrderBy(x => x), naPagina.OrderBy(x => x));
    }

    [Fact]
    public void Toda_categoria_tem_ferramenta_e_nome_nos_dois_idiomas()
    {
        foreach (var idioma in new[] { "pt-BR", "en-US" })
        {
            var cat = Catalogo(idioma);
            foreach (var categoria in Enum.GetValues<ToolCategory>())
            {
                Assert.NotEmpty(ToolGuide.ToolsOf(categoria));
                Assert.True(cat.TryGetValue(ToolGuide.LabelKeyOf(categoria), out var nome) && nome.Length > 0,
                    $"{idioma}: falta {ToolGuide.LabelKeyOf(categoria)}");
            }
        }
    }

    [Fact]
    public void TodaTelaTemOGuiaCompletoNosDoisIdiomas()
    {
        var faltando = new List<string>();

        foreach (var idioma in new[] { "pt-BR", "en-US" })
        {
            var cat = Catalogo(idioma);
            foreach (var id in TelasComGuia())
            {
                Assert.True(ToolGuide.HasGuide(id));

                foreach (var parte in ToolGuide.Parts)
                {
                    var chave = ToolGuide.KeyOf(id, parte);
                    if (!cat.TryGetValue(chave, out var texto) || string.IsNullOrWhiteSpace(texto))
                        faltando.Add($"{idioma}: {chave}");
                }

                if (!cat.ContainsKey($"Module.{id}")) faltando.Add($"{idioma}: Module.{id}");
            }

            foreach (var parte in ToolGuide.Parts)
            {
                if (!cat.ContainsKey($"Guide.Label.{parte}")) faltando.Add($"{idioma}: Guide.Label.{parte}");
            }
        }

        Assert.True(faltando.Count == 0, "Guia incompleto:\n" + string.Join("\n", faltando));
    }

    [Fact]
    public void OGuiaExplicaComPalavrasProprias()
    {
        // As quatro partes dizem coisas diferentes: copiar o "o que faz" no
        // "quando usar" e nao explicar nada.
        var cat = Catalogo("pt-BR");
        foreach (var id in TelasComGuia())
        {
            var partes = ToolGuide.Parts.Select(p => cat[ToolGuide.KeyOf(id, p)]).ToList();
            Assert.Equal(partes.Count, partes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    [Fact]
    public void TelaComGuiaETelaSemGuia()
    {
        Assert.False(ToolGuide.HasGuide("AllPrograms"));
        Assert.False(ToolGuide.HasGuide(""));
        Assert.True(ToolGuide.HasGuide(ToolCatalog.JunkCleaner));
        Assert.True(ToolGuide.HasGuide("WindowsApps"));
        Assert.Equal("Guide.JunkCleaner.What", ToolGuide.KeyOf(ToolCatalog.JunkCleaner, "What"));
    }

    [Fact]
    public void APaginaDeFerramentasConsomeAsCategoriasDoNucleo()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");
        Assert.Contains("ToolGuide.ToolsOf(categoria)", codigo, StringComparison.Ordinal);
        Assert.Contains("ToolGuide.KeyOf(id, \"What\")", codigo, StringComparison.Ordinal);

        // O guia aparece no topo da ferramenta aberta.
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml");
        Assert.Contains("x:Name=\"ToolGuidePanel\"", xaml, StringComparison.Ordinal);
        foreach (var parte in ToolGuide.Parts)
            Assert.Contains($"Guide.Label.{parte}", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ADesinstalacaoEstaNaFrente()
    {
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml");
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        // O botao principal e os dois cliques levam ao mesmo fluxo do menu.
        Assert.Contains("x:Name=\"UninstallButton\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MouseDoubleClick=\"ProgramsList_MouseDoubleClick\"", xaml, StringComparison.Ordinal);
        Assert.Contains("private void UninstallButton_Click(object sender, RoutedEventArgs e) => StartUninstall(quick: false);",
            codigo, StringComparison.Ordinal);

        // A busca da lista nao roda mais a varredura da inicializacao a cada letra.
        var status = codigo[codigo.IndexOf("private void UpdateStatus()", StringComparison.Ordinal)..];
        status = status[..status.IndexOf("private void SearchBox_TextChanged", StringComparison.Ordinal)];
        Assert.DoesNotContain("AutorunManager.Scan", status, StringComparison.Ordinal);
    }

    [Fact]
    public void OMenuNaoUsaOTemplatePadraoDoWindows()
    {
        // O "branco no hover do menu": o template padrao do WPF pinta o
        // submenu com cor fixa. O tema tem template proprio para o item de
        // menu, com o popup do submenu nas brushes do tema.
        var estilos = RepoFiles.Read("src", "Tranal.Uninstaller", "Themes", "Styles.xaml");
        Assert.Contains("<Style TargetType=\"MenuItem\">", estilos, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PART_Popup\"", estilos, StringComparison.Ordinal);
        Assert.Contains("MenuItem.SeparatorStyleKey", estilos, StringComparison.Ordinal);
        Assert.Contains("<Style TargetType=\"ComboBoxItem\">", estilos, StringComparison.Ordinal);
        Assert.Contains("<Style TargetType=\"ToolTip\">", estilos, StringComparison.Ordinal);
    }

    [Fact]
    public void AMigracaoTrocaOsPadroesAntigosUmaVez()
    {
        // Arquivo de antes da interface nova: os padroes antigos gravados.
        var antigo = JsonSerializer.Deserialize<AppSettings>(
            """{ "FocusTracedProgramsOnStartup": true, "ShowCheckBoxesInDetailsView": true, "CompactProgramList": false }""")!;
        var migrado = AppSettingsStore.Normalize(antigo);
        Assert.False(migrado.FocusTracedProgramsOnStartup);
        Assert.False(migrado.ShowCheckBoxesInDetailsView);

        // A lista completa de quem ja a usava continua: a migracao nao tira
        // colunas que a pessoa escolheu.
        Assert.False(migrado.CompactProgramList);
        Assert.Equal(AppSettingsStore.UiCurrentRevision, migrado.UiRevision);

        // Depois da migracao, a escolha do usuario vale.
        var escolhido = JsonSerializer.Deserialize<AppSettings>(
            """{ "FocusTracedProgramsOnStartup": true, "ShowCheckBoxesInDetailsView": true, "CompactProgramList": false, "UiRevision": 1 }""")!;
        var normalizado = AppSettingsStore.Normalize(escolhido);
        Assert.True(normalizado.FocusTracedProgramsOnStartup);
        Assert.True(normalizado.ShowCheckBoxesInDetailsView);
        Assert.False(normalizado.CompactProgramList);
    }

    [Fact]
    public void ODesenhoPorSoftwareEOPadrao()
    {
        // MEDIDO: o desenho por hardware soma ~45 MB de memoria privada.
        Assert.False(new AppSettings().UseHardwareRendering);

        var app = RepoFiles.Read("src", "Tranal.Uninstaller", "App.xaml.cs");
        Assert.Contains("RenderMode.SoftwareOnly", app, StringComparison.Ordinal);
        Assert.Contains("AppSettingsStore.Current.UseHardwareRendering", app, StringComparison.Ordinal);
    }
}
