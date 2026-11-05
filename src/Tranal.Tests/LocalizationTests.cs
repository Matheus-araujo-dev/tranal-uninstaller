using System.Text.RegularExpressions;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Settings;
using Xunit;

namespace Tranal.Tests;

/// <summary>Motor de localizacao: fallback, cobertura de chaves e catalogo de idiomas.</summary>
public class LocalizationTests
{
    [Fact]
    public void ChaveDesconhecidaDevolveAPropriaChave()
    {
        var original = Loc.Current;
        try
        {
            Loc.SetLanguage("pt-BR");
            Assert.Equal("Chave.Inexistente.Teste", Loc.Get("Chave.Inexistente.Teste"));
        }
        finally { Loc.SetLanguage(original); }
    }

    [Fact]
    public void CatalogoPtBrEEnUsTemAsMesmasChaves()
    {
        var pt = Loc.Load("pt-BR");
        var en = Loc.Load("en-US");

        Assert.NotEmpty(pt);
        Assert.NotEmpty(en);

        var faltandoEmEn = pt.Keys.Where(k => !en.ContainsKey(k)).OrderBy(k => k).ToList();
        var faltandoEmPt = en.Keys.Where(k => !pt.ContainsKey(k)).OrderBy(k => k).ToList();

        Assert.True(faltandoEmEn.Count == 0,
            $"Chaves ausentes em en-US: {string.Join(", ", faltandoEmEn)}");
        Assert.True(faltandoEmPt.Count == 0,
            $"Chaves ausentes em pt-BR: {string.Join(", ", faltandoEmPt)}");
    }

    [Fact]
    public void CatalogoTemOs48IdiomasDoRevo()
    {
        Assert.Equal(48, LanguageCatalog.Count);
        Assert.Equal(48, LanguageCatalog.All.Select(l => l.Code).Distinct().Count());
        Assert.True(LanguageCatalog.Exists("pt-BR"));
        Assert.True(LanguageCatalog.Exists("zh-Hans"));
        Assert.False(LanguageCatalog.Exists("xx-YY"));
    }

    [Fact]
    public void TrocaDeIdiomaDisparaEvento()
    {
        var original = Loc.Current;
        var fired = false;
        EventHandler handler = (_, _) => fired = true;
        Loc.LanguageChanged += handler;
        try
        {
            Loc.SetLanguage(original == "en-US" ? "pt-BR" : "en-US");
            Assert.True(fired);
        }
        finally
        {
            Loc.LanguageChanged -= handler;
            Loc.SetLanguage(original);
        }
    }

    /// <summary>
    /// Verificacao adversarial: toda chave usada no XAML precisa existir no catalogo.
    /// Sem isso, uma traducao faltando aparece na tela como "App.Title".
    /// </summary>
    [Fact]
    public void TodaChaveUsadaNoXamlExisteNoCatalogo()
    {
        var root = FindRepositoryRoot();
        Assert.NotNull(root);

        var catalog = Loc.Load("pt-BR");
        var pattern = new Regex(@"\{(?:loc:Tr|loc:Loc)\s+([A-Za-z0-9_.]+)\s*\}", RegexOptions.Compiled);

        var missing = new List<string>();

        foreach (var xaml in Directory.EnumerateFiles(root!, "*.xaml", SearchOption.AllDirectories))
        {
            if (xaml.Contains(@"\obj\") || xaml.Contains(@"\bin\")) continue;

            var text = File.ReadAllText(xaml);
            foreach (Match match in pattern.Matches(text))
            {
                var key = match.Groups[1].Value;
                if (!catalog.ContainsKey(key))
                    missing.Add($"{Path.GetFileName(xaml)}: {key}");
            }
        }

        Assert.True(missing.Count == 0,
            "Chaves sem traducao no catalogo pt-BR:\n" + string.Join("\n", missing.Distinct()));
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

/// <summary>
/// Preferencias persistentes.
///
/// A classe esta na colecao "preferencias" porque grava e restaura o
/// <see cref="AppSettingsStore"/> global — o mesmo que o app usa. Testes que
/// tocam nesse estado compartilhado nao podem rodar em paralelo.
/// </summary>
[Collection("preferencias")]
public class AppSettingsTests
{
    [Fact]
    public void ArquivoDePreferenciasTemOCaminhoCanonico()
    {
        var path = AppSettingsStore.FilePath;
        Assert.Contains("Tranal Group", path);
        Assert.EndsWith("settings.json", path);
    }

    [Fact]
    public void ValorPadraoDeNovosProgramasEh7Dias()
    {
        var settings = new AppSettings();
        Assert.Equal(7, settings.NewProgramsDays);
    }

    [Fact]
    public void GravarEOLerPreservaAsPreferencias()
    {
        // O arquivo de preferencias do teste e um temporario: gravar no
        // settings.json real perderia o estado do usuario se o processo
        // morresse no meio do teste.
        var temp = Path.Combine(Path.GetTempPath(), "tranal-settings-" + Guid.NewGuid().ToString("N")[..8] + ".json");

        try
        {
            AppSettingsStore.FilePathOverride = temp;

            AppSettingsStore.Save(new AppSettings
            {
                Theme = "Light",
                Language = "en-US",
                NewProgramsDays = 14,
                ShowSystemComponents = true,
            });

            var loaded = AppSettingsStore.Load();
            Assert.Equal("Light", loaded.Theme);
            Assert.Equal("en-US", loaded.Language);
            Assert.Equal(14, loaded.NewProgramsDays);
            Assert.True(loaded.ShowSystemComponents);
        }
        finally
        {
            AppSettingsStore.FilePathOverride = null;
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch { /* o temporario e descartavel */ }
        }
    }
}
