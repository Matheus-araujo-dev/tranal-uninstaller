using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Tranal.Uninstaller.Core;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Testes dos atalhos de teclado.
///
/// O item 25 da secao 7.1 do REVO_SPEC (atalhos de teclado completos) estava
/// marcado como pronto sem que existisse qualquer atalho no codigo. Estos
/// testes existem para que a tabela de atalhos nao volte a ser so um
///Titulo: cada atalho precisa ter uma acao, e cada acao precisa estar
/// traduzida nos dois catalogos.
/// </summary>
public class ShortcutTests
{
    [Fact]
    public void ACombinaoEhMontadaNaOremCerta()
    {
        Assert.Equal("Ctrl+Shift+A", ShortcutMap.GestureOf("A", ctrl: true, shift: true));
        Assert.Equal("F5", ShortcutMap.GestureOf("F5"));
        Assert.Equal("Ctrl+OemComma", ShortcutMap.GestureOf("OemComma", ctrl: true));
    }

    [Fact]
    public void CadaAtalhoEncontradoTemAAcaoCerta()
    {
        var atalho = ShortcutMap.Find("Ctrl+F");

        Assert.NotNull(atalho);
        Assert.Equal(ShortcutAction.FocusSearch, atalho!.Action);
    }

    [Fact]
    public void CombinacaoQueNaoExisteNaoDevolveAtalho()
    {
        Assert.Null(ShortcutMap.Find("Ctrl+Shift+F12"));
        Assert.Null(ShortcutMap.Find(null));
        Assert.Null(ShortcutMap.Find("  "));
    }

    [Fact]
    public void NaoHaAtalhosRepetidos()
    {
        var repetidos = ShortcutMap.All
            .GroupBy(s => s.Gesture, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(repetidos.Count == 0,
            $"atalho repetido: {string.Join(", ", repetidos)}");
    }

    [Fact]
    public void TodoAtalhoTemDescricaoNosDoisCatalogos()
    {
        foreach (var catalogo in new[] { "pt-BR", "en-US" })
        {
            var caminho = RepoFiles.Find("src", "Tranal.Uninstaller", "Lang", $"{catalogo}.json");
            Assert.NotNull(caminho);

            using var document = JsonDocument.Parse(File.ReadAllText(caminho!));
            var chaves = document.RootElement.EnumerateObject()
                .Select(p => p.Name)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var atalho in ShortcutMap.All)
            {
                Assert.True(chaves.Contains(atalho.DescriptionKey),
                    $"{catalogo}: falta a chave {atalho.DescriptionKey} do atalho {atalho.Gesture}");
            }
        }
    }

    [Fact]
    public void AJanelaPrincipalTrataOsAtalhos()
    {
        // A tela e a unica que pode executar a acao; o nucleo so descreve.
        var fonte = RepoFiles.Find("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");
        Assert.NotNull(fonte);

        var texto = File.ReadAllText(fonte!);
        Assert.Contains("Window_PreviewKeyDown", texto);
        Assert.Contains("ShortcutMap.Find", texto);

        foreach (var acao in Enum.GetValues<ShortcutAction>())
        {
            Assert.Contains($"ShortcutAction.{acao}", texto);
        }
    }
}
