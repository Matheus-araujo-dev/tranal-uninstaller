using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace Tranal.Uninstaller.Core.Localization;

/// <summary>
/// Motor de localizacao do Tranal Uninstaller Pro.
///
/// Os catálogos sao arquivos JSON embarcados no assembly (Lang/&lt;codigo&gt;.json).
/// A resolucao de uma chave segue a cadeia: idioma ativo -&gt; pt-BR -&gt; en-US -&gt;
/// a propria chave (para nunca devolver texto vazio).
/// </summary>
public static class Loc
{
    /// <summary>Idioma de referencia do projeto (portugues do Brasil).</summary>
    public const string DefaultLanguage = "pt-BR";

    /// <summary>Segundo idioma da cadeia de fallback.</summary>
    public const string SecondaryLanguage = "en-US";

    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Catalogs = new(StringComparer.OrdinalIgnoreCase);

    private static readonly List<LocalizedString> Proxies = new();
    private static readonly object ProxyLock = new();

    private static string _current = DefaultLanguage;

    /// <summary>Codigo do idioma ativo.</summary>
    public static string Current => _current;

    /// <summary>Dispara quando o idioma muda (a GUI reconstrói os textos).</summary>
    public static event EventHandler? LanguageChanged;

    /// <summary>True quando existe traducao para o idioma ativo.</summary>
    public static bool IsTranslated => Catalogs.ContainsKey(_current);

    /// <summary>Devolve o texto da chave no idioma ativo.</summary>
    public static string Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;

        if (Catalogs.TryGetValue(_current, out var active) && active.TryGetValue(key, out var value))
            return value;

        if (Catalogs.TryGetValue(DefaultLanguage, out var fallback) && fallback.TryGetValue(key, out value))
            return value;

        if (Catalogs.TryGetValue(SecondaryLanguage, out var secondary) && secondary.TryGetValue(key, out value))
            return value;

        return key;
    }

    /// <summary>Devolve o texto da chave aplicando formatacao posicional.</summary>
    public static string Get(string key, params object?[] args)
    {
        var text = Get(key);
        if (args.Length == 0) return text;
        try { return string.Format(System.Globalization.CultureInfo.CurrentCulture, text, args); }
        catch (FormatException) { return text; }
    }

    /// <summary>True quando a chave existe no catalogo do idioma ativo.</summary>
    public static bool HasKey(string key) =>
        Catalogs.TryGetValue(_current, out var active) && active.ContainsKey(key);

    /// <summary>Define o idioma ativo e avisa os assinantes.</summary>
    public static void SetLanguage(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        if (string.Equals(code, _current, StringComparison.OrdinalIgnoreCase) && Catalogs.ContainsKey(code))
            return;

        _current = code;
        Load(code);
        RaiseAll();
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Carrega (ou recarrega) o catalogo de um idioma.</summary>
    public static IReadOnlyDictionary<string, string> Load(string code)
    {
        return Catalogs.GetOrAdd(code, static c =>
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var suffix = $"Lang.{c}.json";
                var name = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
                if (name is null) return new Dictionary<string, string>(StringComparer.Ordinal);

                using var stream = assembly.GetManifestResourceStream(name);
                if (stream is null) return new Dictionary<string, string>(StringComparer.Ordinal);

                using var document = JsonDocument.Parse(stream);
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var property in document.RootElement.EnumerateObject())
                    map[property.Name] = property.Value.GetString() ?? string.Empty;
                return map;
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }
        });
    }

    /// <summary>Registra um proxy para receber atualizacoes quando o idioma muda.</summary>
    internal static void Register(LocalizedString proxy)
    {
        lock (ProxyLock) Proxies.Add(proxy);
    }

    private static void RaiseAll()
    {
        List<LocalizedString> snapshot;
        lock (ProxyLock) snapshot = Proxies.ToList();
        foreach (var proxy in snapshot) proxy.Raise();
    }
}

/// <summary>
/// Texto localizado usado por binding no XAML.
/// Mantem o controle em dia quando o idioma muda, sem recarregar a janela.
/// </summary>
public sealed class LocalizedString : INotifyPropertyChanged
{
    public LocalizedString(string key)
    {
        Key = key;
        Loc.Register(this);
    }

    public string Key { get; }

    public string Value => Loc.Get(Key);

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Raise() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
}
