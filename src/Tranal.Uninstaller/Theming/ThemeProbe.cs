using System.IO;
using System.Windows;
using System.Windows.Media;

namespace Tranal.Uninstaller.Theming;

/// <summary>
/// Sonda de diagnostico dos recursos de tema (so com TRANAL_THEME_PROBE=1).
/// Grava o resultado em %TEMP%\tranal-theme-probe.txt.
/// </summary>
internal static class ThemeProbe
{
    public static void Run()
    {
        try
        {
            var lines = new List<string>();
            var app = Application.Current;
            lines.Add($"app={app?.GetType().FullName}");

            var res = app?.Resources;
            lines.Add($"resources null? {res is null}");
            lines.Add($"merged count: {res?.MergedDictionaries.Count}");
            for (var i = 0; i < (res?.MergedDictionaries.Count ?? 0); i++)
            {
                var d = res!.MergedDictionaries[i];
                lines.Add($"  merged[{i}] source='{d.Source?.OriginalString}' chaves={d.Count} temThemeDark={d.Contains("ThemeDark")}");
            }

            var catalog = res?["ThemeCatalog"] as ResourceDictionary;
            lines.Add($"catalogo null? {catalog is null}");
            if (catalog is not null)
            {
                lines.Add($"  catalogo.Keys = {string.Join(", ", catalog.Keys.Cast<object>())}");
                foreach (var key in new[] { "Dark", "Light" })
                {
                    var theme = catalog[key] as ResourceDictionary;
                    lines.Add($"  catalogo['{key}'] null? {theme is null} chaves={theme?.Count}");
                    if (theme is not null)
                    {
                        var sample = theme.Keys.Cast<object>().Take(5).Select(k => k + "=" + theme[k]?.GetType().Name);
                        lines.Add($"     amostra: {string.Join(", ", sample)}");
                    }
                }
            }

            foreach (var key in new[] { "PanelBrush", "SidebarBrush", "TextBrush", "TitleTextBrush", "FlatButton" })
            {
                var value = res?[key];
                var color = value is SolidColorBrush b ? b.Color.ToString() : value?.GetType().Name ?? "null";
                lines.Add($"  app['{key}'] = {color}");
            }

            File.WriteAllLines(Path.Combine(Path.GetTempPath(), "tranal-theme-probe.txt"), lines);
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(Path.GetTempPath(), "tranal-theme-probe.txt"),
                    "probe falhou: " + ex);
            }
            catch { }
        }
    }
}
