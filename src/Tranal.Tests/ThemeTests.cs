using System.Text.RegularExpressions;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Paridade entre os dois temas.
///
/// O defeito que motivou estes testes: seis textos usavam
/// <c>Foreground="White"</c> fixo (titulo da barra lateral, cabecalho do
/// modulo, cabecalho de grupo, painel generico, modo cacador e assistente).
/// No tema escuro ficavam corretos; no tema claro o branco sobre superficie
/// clara deixava o texto invisivel. Como as duas cores sao fixas no XAML,
/// nenhum teste de cobertura de codigo pegaria isso — e a barra lateral
/// continuava clara, o que fez o defeito parecer outro.
/// </summary>
public class ThemeTests
{
    private static readonly Regex KeyPattern = new(
        "x:Key\\s*=\\s*\"(?<key>[^\"]+)\"", RegexOptions.Compiled);

    private static readonly Regex DynamicResourcePattern = new(
        "DynamicResource\\s+(?<key>[A-Za-z0-9_.]+)", RegexOptions.Compiled);

    private static readonly Regex StaticResourcePattern = new(
        "StaticResource\\s+(?<key>[A-Za-z0-9_.]+)", RegexOptions.Compiled);

    private static readonly Regex CommentPattern = new(
        "<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex HardColorPattern = new(
        "Foreground\\s*=\\s*\"(?<color>White|Black|Red|Lime|Blue|Yellow|Cyan|Magenta|Gray|Grey)\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void OsDoisTemasDeclaramAsMesmasChaves()
    {
        var dark = ReadThemeKeys("Dark.xaml");
        var light = ReadThemeKeys("Light.xaml");

        Assert.NotEmpty(dark);
        Assert.NotEmpty(light);

        var faltandoNoClaro = dark.Where(k => !light.Contains(k)).OrderBy(k => k).ToList();
        var faltandoNoEscuro = light.Where(k => !dark.Contains(k)).OrderBy(k => k).ToList();

        Assert.True(faltandoNoClaro.Count == 0,
            $"Chaves ausentes no tema claro: {string.Join(", ", faltandoNoClaro)}");
        Assert.True(faltandoNoEscuro.Count == 0,
            $"Chaves ausentes no tema escuro: {string.Join(", ", faltandoNoEscuro)}");
    }

    [Fact]
    public void TodaChaveDeRecursoUsadaNoXamlExisteNosDoisTemas()
    {
        var available = ReadResourceKeys();

        var faltando = new List<string>();

        foreach (var xaml in EnumerateXamlFiles())
        {
            var text = File.ReadAllText(xaml);
            text = CommentPattern.Replace(text, string.Empty);

            foreach (Match match in DynamicResourcePattern.Matches(text))
            {
                var key = match.Groups["key"].Value;
                if (!available.Contains(key)) faltando.Add($"{Path.GetFileName(xaml)}: DynamicResource {key}");
            }

            foreach (Match match in StaticResourcePattern.Matches(text))
            {
                var key = match.Groups["key"].Value;
                if (!available.Contains(key)) faltando.Add($"{Path.GetFileName(xaml)}: StaticResource {key}");
            }
        }

        Assert.True(faltando.Count == 0,
            "Recursos usados no XAML e inexistentes nos temas:\n" + string.Join("\n", faltando.Distinct()));
    }

    /// <summary>
    /// Fora dos dicionarios de tema, nenhuma cor de texto pode ser fixa:
    /// ela nao acompanha a troca de tema. O branco sobre azul (botao
    /// selecionado) vive dentro do proprio dicionario e continua valido.
    /// </summary>
    [Fact]
    public void NenhumaCorDeTextoFixaForaDosDicionariosDeTema()
    {
        var found = new List<string>();

        foreach (var xaml in EnumerateXamlFiles())
        {
            if (xaml.Contains(@"\Themes\")) continue;

            var text = File.ReadAllText(xaml);
            text = CommentPattern.Replace(text, string.Empty);
            foreach (Match match in HardColorPattern.Matches(text))
            {
                found.Add($"{Path.GetFileName(xaml)}: Foreground=\"{match.Groups["color"].Value}\"");
            }
        }

        Assert.True(found.Count == 0,
            "Cores de texto fixas (quebram no tema claro):\n" + string.Join("\n", found.Distinct()));
    }

    [Fact]
    public void OTituloDoPainelUsaBrushDeTitulo()
    {
        // Regressao do defeito corrigido: o titulo da barra lateral precisa
        // seguir o tema, e nao ficar branco fixo.
        var main = File.ReadAllText(FindProjectFile("MainWindow.xaml"));
        Assert.Contains("TitleTextBrush", main);
        Assert.DoesNotContain("Foreground=\"White\"", main);
    }

    /// <summary>
    /// Tudo o que a interface pode referenciar: as cores dos dois temas
    /// (Themes/Dark.xaml e Themes/Light.xaml) e os estilos compartilhados
    /// (Themes/Styles.xaml).
    /// </summary>
    private static HashSet<string> ReadResourceKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in new[] { "Dark.xaml", "Light.xaml", "Styles.xaml" })
            keys.UnionWith(ReadThemeKeys(file));
        return keys;
    }

    private static HashSet<string> ReadThemeKeys(string fileName)
    {
        var path = FindProjectFile(Path.Combine("Themes", fileName));
        var text = File.ReadAllText(path);

        return KeyPattern.Matches(text)
            .Select(m => m.Groups["key"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<string> EnumerateXamlFiles()
    {
        var root = FindRepositoryRoot();
        Assert.NotNull(root);

        return Directory.EnumerateFiles(root!, "*.xaml", SearchOption.AllDirectories)
            .Where(p => !p.Contains(@"\obj\") && !p.Contains(@"\bin\") && !p.Contains(@"\publish\"))
            .OrderBy(p => p, StringComparer.Ordinal);
    }

    private static string FindProjectFile(string relative)
    {
        var root = FindRepositoryRoot();
        Assert.NotNull(root);
        return Path.Combine(root!, "src", "Tranal.Uninstaller", relative);
    }

    private static string? FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Tranal.Uninstaller.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
