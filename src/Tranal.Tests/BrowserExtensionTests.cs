using Tranal.Uninstaller.Core.Modules;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Modulo "Extensoes do Navegador".
///
/// Regressao medida nesta maquina: a extensao "Chrome Web Store Payments"
/// declara <c>"name": "__MSG_APP_NAME__"</c> com <c>"default_locale": "en"</c>,
/// mas o messages.json do locale usa a chave <c>app_name</c> (minusculas).
/// O navegador resolve sem diferenciar caixa; o Tranal nao, e a lista
/// mostrava o token cru "__MSG_APP_NAME__".
///
/// Os testes usam uma arvore de extensao sintetica em pasta temporaria —
/// nada do perfil real do usuario e tocado.
/// </summary>
public class BrowserExtensionTests : IDisposable
{
    private readonly string _root;

    public BrowserExtensionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "tranal-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Limpeza nao pode derrubar o teste.
        }
    }

    [Fact]
    public void ResolveChaveDeMensagemSemDiferenciarCaixa()
    {
        // Caso real: "__MSG_APP_NAME__" -> chave "app_name".
        var manifest = CreateExtension(
            name: "__MSG_APP_NAME__",
            defaultLocale: "en",
            locales: new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["app_name"] = "Chrome Web Store Payments" },
                ["pt_BR"] = new() { ["app_name"] = "Pagamentos da Chrome Web Store" },
            });

        var resolved = BrowserExtensions.ResolveExtensionName(manifest);

        Assert.Equal("Chrome Web Store Payments", resolved);
        Assert.DoesNotContain("__MSG_", resolved);
    }

    [Fact]
    public void ResolveChaveDeMensagemComACaixaExata()
    {
        // Caso real: "Google Docs Offline" -> "__MSG_extName__" / "extName".
        var manifest = CreateExtension(
            name: "__MSG_extName__",
            defaultLocale: "en_US",
            locales: new Dictionary<string, Dictionary<string, string>>
            {
                ["en_US"] = new() { ["extName"] = "Google Docs Offline" },
                ["pt_BR"] = new() { ["extname"] = "Google Docs off-line" },
            });

        var resolved = BrowserExtensions.ResolveExtensionName(manifest);

        Assert.Equal("Google Docs Offline", resolved);
    }

    [Fact]
    public void UsaODefaultLocaleDoManifestoAntesDosFallbacks()
    {
        // O default_locale vence: aqui o locale "en" e o unico com a chave.
        var manifest = CreateExtension(
            name: "__MSG_app_name__",
            defaultLocale: "en",
            locales: new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["app_name"] = "Pelo default locale" },
                ["pt_BR"] = new() { ["app_name"] = "Pelo fallback" },
            });

        var resolved = BrowserExtensions.ResolveExtensionName(manifest);

        Assert.Equal("Pelo default locale", resolved);
    }

    [Fact]
    public void SemLocalesDevolveONomeCruDoManifesto()
    {
        var manifest = CreateExtension(
            name: "__MSG_APP_NAME__",
            defaultLocale: "en",
            locales: new Dictionary<string, Dictionary<string, string>>());

        var resolved = BrowserExtensions.ResolveExtensionName(manifest);

        Assert.Equal("__MSG_APP_NAME__", resolved);
    }

    [Fact]
    public void NomeComumNaoPassaPorTraducao()
    {
        var manifest = CreateExtension(
            name: "Edge relevant text changes",
            defaultLocale: null,
            locales: new Dictionary<string, Dictionary<string, string>>());

        var resolved = BrowserExtensions.ResolveExtensionName(manifest);

        Assert.Equal("Edge relevant text changes", resolved);
    }

    [Fact]
    public void ProcuraEmQualquerLocaleQuandoOsPreferidosNaoTemAChave()
    {
        var manifest = CreateExtension(
            name: "__MSG_ONLY_IN_DE__",
            defaultLocale: "en",
            locales: new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["outro"] = "x" },
                ["de"] = new() { ["only_in_de"] = "Nur auf Deutsch" },
            });

        var resolved = BrowserExtensions.ResolveExtensionName(manifest);

        Assert.Equal("Nur auf Deutsch", resolved);
    }

    /// <summary>
    /// Monta uma arvore de extensao sintetica:
    /// &lt;raiz&gt;/&lt;id&gt;/&lt;versao&gt;/manifest.json e _locales/&lt;locale&gt;/messages.json
    /// </summary>
    private string CreateExtension(
        string name,
        string? defaultLocale,
        Dictionary<string, Dictionary<string, string>> locales)
    {
        var versionDir = Path.Combine(_root, "abcdefghijklmnop", "1.0.0_0");
        Directory.CreateDirectory(versionDir);

        var manifest = new Dictionary<string, string?>
        {
            ["manifest_version"] = "3",
            ["name"] = name,
            ["version"] = "1.0.0",
        };
        if (!string.IsNullOrEmpty(defaultLocale)) manifest["default_locale"] = defaultLocale;

        var manifestPath = Path.Combine(versionDir, "manifest.json");
        File.WriteAllText(manifestPath, System.Text.Json.JsonSerializer.Serialize(manifest));

        foreach (var (locale, messages) in locales)
        {
            var localeDir = Path.Combine(versionDir, "_locales", locale);
            Directory.CreateDirectory(localeDir);

            var payload = messages.ToDictionary(
                m => m.Key,
                m => (object)new Dictionary<string, string> { ["message"] = m.Value });

            File.WriteAllText(
                Path.Combine(localeDir, "messages.json"),
                System.Text.Json.JsonSerializer.Serialize(payload));
        }

        return manifestPath;
    }
}
