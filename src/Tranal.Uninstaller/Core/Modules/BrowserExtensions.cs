using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// Uma extensão instalada em um navegador (Chrome, Edge, Firefox ou Opera).
/// </summary>
public sealed class BrowserExtension
{
    /// <summary>Nome da extensão, como o navegador a exibe.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Identificador do navegador: "chrome", "edge", "firefox" ou "opera".</summary>
    public string BrowserId { get; init; } = string.Empty;

    /// <summary>Nome do navegador: "Google Chrome", "Microsoft Edge"...</summary>
    public string BrowserName { get; init; } = string.Empty;

    /// <summary>Identificador da extensão dentro do navegador.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Versao da extensão.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Perfil do navegador onde a extensão esta instalada.</summary>
    public string Profile { get; init; } = string.Empty;

    /// <summary>Pasta (Chromium) ou arquivo .xpi (Firefox) da extensão.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>False quando a extensão esta desativada no navegador.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// True quando a extensão e embarcada no navegador (componente interno):
    /// essas nao podem ser removidas pelo Tranal.
    /// </summary>
    public bool IsBuiltIn { get; init; } = false;
}

/// <summary>
/// Modulo "Extensoes do Navegador" do Revo: lista e remove extensoes do Chrome,
/// Edge, Firefox e Opera.
///
/// Layouts monitorados:
///   Chromium: %LOCALAPPDATA%\&lt;navegador&gt;\User Data\&lt;perfil&gt;\Extensions\&lt;id&gt;\&lt;versao&gt;\manifest.json
///   Firefox:  %APPDATA%\Mozilla\Firefox\Profiles\&lt;perfil&gt;\extensions.json (+ &lt;id&gt;.xpi)
/// </summary>
public static class BrowserExtensions
{
    /// <summary>Identificador do Firefox (formato diferente dos navegadores Chromium).</summary>
    private const string FirefoxId = "firefox";

    /// <summary>
    /// Extensoes de componente (embarcadas) conhecidas dos navegadores Chromium.
    /// A lista e conservadora: marcar uma extensao comum como "embarcada" apenas
    /// impede a remocao dela, nunca apaga algo do usuario.
    /// </summary>
    private static readonly HashSet<string> BuiltInChromiumIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "cjpalhdlnbpafiamejdnhcphjbkeiagm", // Visualizador de PDF do Chrome
        "mhjfbmdgcfjbbpaeojofohoefgiehjai", // Google Docs Offline
        "eoopicbdnlegpjbcjnjnelhbbolbodj", // Google Network Speech
        "lcoigidnojbydpblkcnnegkpbkkkkc", // Chrome Media Router
        "fcoeoabgfenejglbffodgkkbkcdhcgfn", // CryptoToken Extension
    };

    /// <summary>Navegadores baseados em Chromium e suas pastas de perfil.</summary>
    private sealed record ChromiumBrowser(string Id, string Name, string RelativePath);

    /// <summary>
    /// Navegadores baseados em Chromium e suas pastas de perfil.
    ///
    /// A lista cobre os onze navegadores que o gerenciador de extensoes do
    /// Wise lista, com os baseados em Chromium que usam a mesma estrutura de
    /// pasta ("User Data" e "Extensions"). Os que usam estrutura propria —
    /// Firefox, que usa arquivos .xpi — tem caminho proprio.
    /// </summary>
    private static readonly ChromiumBrowser[] ChromiumBrowsers =
    {
        new("chrome", "Google Chrome", @"Google\Chrome\User Data"),
        new("edge", "Microsoft Edge", @"Microsoft\Edge\User Data"),
        new("edgebeta", "Microsoft Edge Beta", @"Microsoft\Edge Beta\User Data"),
        new("edgedev", "Microsoft Edge Dev", @"Microsoft\Edge Dev\User Data"),
        new("brave", "Brave", @"BraveSoftware\Brave-Browser\User Data"),
        new("vivaldi", "Vivaldi", @"Vivaldi\User Data"),
        new("yandex", "Yandex Browser", @"Yandex\YandexBrowser\User Data"),
        new("chromium", "Chromium", @"Chromium\User Data"),
        new("opera", "Opera", @"Opera Software\Opera Stable\User Data"),
        new("operagx", "Opera GX", @"Opera Software\Opera GX Stable\User Data"),
        new("comodo", "Comodo Dragon", @"Comodo\Dragon\User Data"),
        new("torch", "Torch Browser", @"TorchSoftware\Torch\User Data"),
        new("coccoc", "CocCoc", @"CocCoc\Browser\User Data"),
        new("cent", "Cent Browser", @"CentBrowser\User Data"),
        new("chedot", "Chedot Browser", @"Chedot\User Data"),
        new("iridium", "Iridium", @"Iridium\User Data"),
    };

    /// <summary>
    /// Lista as extensoes de todos os navegadores. Nunca lanca: um navegador
    /// inacessivel apenas nao aparece na listagem.
    /// </summary>
    public static IReadOnlyList<BrowserExtension> List()
    {
        var result = new List<BrowserExtension>();

        try
        {
            foreach (var extension in ListChromium()) result.Add(extension);
        }
        catch { }

        try
        {
            foreach (var extension in ListFirefox()) result.Add(extension);
        }
        catch { }

        return result
            .OrderBy(e => e.BrowserName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Remove a extensao. Extensoes embarcadas (IsBuiltIn) nunca sao removidas.
    /// A remocao do arquivo/pasta passa pela rotina canonica
    /// <see cref="UninstallEngine.RemoveLeftovers"/>.
    /// </summary>
    public static bool Remove(BrowserExtension extension)
    {
        try
        {
            if (extension is null) return false;
            if (extension.IsBuiltIn) return false;
            if (string.IsNullOrWhiteSpace(extension.Path)) return false;

            return string.Equals(extension.BrowserId, FirefoxId, StringComparison.OrdinalIgnoreCase)
                ? RemoveFirefox(extension)
                : RemoveChromium(extension);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Extensoes dos navegadores Chromium.</summary>
    private static IEnumerable<BrowserExtension> ListChromium()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData)) yield break;

        foreach (var browser in ChromiumBrowsers)
        {
            string basePath;
            try { basePath = Path.Combine(localAppData, browser.RelativePath); }
            catch { continue; }

            if (!Directory.Exists(basePath)) continue;

            string[] profiles;
            try { profiles = Directory.GetDirectories(basePath); }
            catch { continue; }

            foreach (var profile in profiles)
            {
                var profileName = Path.GetFileName(profile);
                if (!IsChromiumProfile(profileName)) continue;

                var extensionsDir = Path.Combine(profile, "Extensions");
                if (!Directory.Exists(extensionsDir)) continue;

                string[] ids;
                try { ids = Directory.GetDirectories(extensionsDir); }
                catch { continue; }

                foreach (var idDir in ids)
                {
                    var extension = ReadChromiumExtension(browser, profileName, idDir);
                    if (extension is not null) yield return extension;
                }
            }
        }
    }

    private static BrowserExtension? ReadChromiumExtension(
        ChromiumBrowser browser, string profileName, string idDir)
    {
        try
        {
            var id = Path.GetFileName(idDir);
            if (string.IsNullOrWhiteSpace(id)) return null;

            string[] versions;
            try { versions = Directory.GetDirectories(idDir); }
            catch { return null; }

            // A versao mais recente e a ultima pasta em ordem alfabetica; usa-se a
            // primeira que tiver manifest.json legivel.
            foreach (var versionDir in versions.OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase))
            {
                var manifestPath = Path.Combine(versionDir, "manifest.json");
                if (!File.Exists(manifestPath)) continue;

                var manifest = ReadManifest(manifestPath);
                var name = ResolveExtensionName(manifestPath);
                if (string.IsNullOrWhiteSpace(name)) name = id;

                return new BrowserExtension
                {
                    Name = name,
                    BrowserId = browser.Id,
                    BrowserName = browser.Name,
                    Id = id,
                    Version = manifest.Version,
                    Profile = profileName,
                    Path = PathUtil.Normalize(idDir),
                    IsEnabled = true,
                    IsBuiltIn = BuiltInChromiumIds.Contains(id),
                };
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Le nome, versao, default_locale e manifest_version do manifest.json.</summary>
    private static ManifestInfo ReadManifest(string manifestPath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;
            return new ManifestInfo(
                GetString(root, "name"),
                GetString(root, "version"),
                GetString(root, "default_locale"));
        }
        catch
        {
            return new ManifestInfo(string.Empty, string.Empty, string.Empty);
        }
    }

    /// <summary>Dados do manifest.json usados pela listagem.</summary>
    private sealed record ManifestInfo(string Name, string Version, string DefaultLocale);

    /// <summary>
    /// Resolve o nome de exibicao de uma extensao a partir do manifest.json.
    ///
    /// Nomes no formato "__MSG_chave__" sao resolvidos pelos arquivos de locale
    /// da propria extensao. Se a chave nao existir em nenhum locale, devolve o
    /// valor cru do manifesto (e nao uma string vazia), para nunca esconder o
    /// nome real da extensao.
    /// </summary>
    public static string ResolveExtensionName(string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifestPath)) return string.Empty;

        var manifest = ReadManifest(manifestPath);
        var resolved = ResolveLocalizedName(manifestPath, manifest.Name, manifest.DefaultLocale);
        return string.IsNullOrWhiteSpace(resolved) ? manifest.Name : resolved;
    }

    /// <summary>
    /// Resolve nomes no formato "__MSG_chave__" usando os arquivos de locale da
    /// propria extensao.
    ///
    /// Medido nos pacotes reais desta maquina:
    ///   - a extensao do Chrome "Chrome Web Store Payments" declara
    ///     "name": "__MSG_APP_NAME__" com "default_locale": "en", mas o
    ///     messages.json usa a chave "app_name" (minusculas). O navegador
    ///     resolve sem diferenciar caixa, e o Tranal precisa fazer o mesmo —
    ///     senao o nome cru "__MSG_APP_NAME__" aparece na lista.
    ///   - a extensao "Google Docs Offline" declara "__MSG_extName__" com
    ///     "default_locale": "en_US" e a chave "extName" (com caixa).
    ///
    /// Ordem de resolucao: o locale padrao declarado pelo manifesto, depois
    /// pt_BR/en_US/en, depois qualquer outro locale (a chave pode estar
    /// grafada com outra caixa).
    /// </summary>
    private static string ResolveLocalizedName(string manifestPath, string name, string defaultLocale)
    {
        if (!name.StartsWith("__MSG_", StringComparison.Ordinal) || !name.EndsWith("__", StringComparison.Ordinal))
            return name;

        var key = name[6..^2];
        try
        {
            var extensionDir = Path.GetDirectoryName(manifestPath);
            if (string.IsNullOrEmpty(extensionDir)) return string.Empty;

            var localesDir = Path.Combine(extensionDir, "_locales");
            if (!Directory.Exists(localesDir)) return string.Empty;

            // 1) locale padrao do manifesto
            if (!string.IsNullOrWhiteSpace(defaultLocale))
            {
                var text = ReadLocaleMessage(localesDir, defaultLocale, key);
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }

            // 2) idiomas de fallback (ordem estavel)
            foreach (var locale in new[] { "pt_BR", "en_US", "en" })
            {
                var text = ReadLocaleMessage(localesDir, locale, key);
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }

            // 3) qualquer outro locale, em ordem alfabetica (deterministico)
            string[] others;
            try { others = Directory.GetDirectories(localesDir); }
            catch { return string.Empty; }

            foreach (var dir in others.OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var text = ReadLocaleMessage(localesDir, Path.GetFileName(dir), key);
                if (!string.IsNullOrWhiteSpace(text)) return text;
            }
        }
        catch { }

        return string.Empty;
    }

    /// <summary>
    /// Le a mensagem de um locale. A chave e procurada de forma exata e, em
    /// seguida, sem diferenciar caixa (e o que o navegador faz).
    /// </summary>
    private static string ReadLocaleMessage(string localesDir, string locale, string key)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(locale)) return string.Empty;

            var messagesPath = Path.Combine(localesDir, locale, "messages.json");
            if (!File.Exists(messagesPath)) return string.Empty;

            using var document = JsonDocument.Parse(File.ReadAllText(messagesPath));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return string.Empty;

            if (root.TryGetProperty(key, out var exact))
            {
                var text = exact.TryGetProperty("message", out var message)
                    ? message.GetString()
                    : null;
                if (!string.IsNullOrWhiteSpace(text)) return text!;
            }

            foreach (var entry in root.EnumerateObject())
            {
                if (!string.Equals(entry.Name, key, StringComparison.OrdinalIgnoreCase)) continue;

                var text = entry.Value.TryGetProperty("message", out var m) ? m.GetString() : null;
                if (!string.IsNullOrWhiteSpace(text)) return text!;
            }
        }
        catch { }

        return string.Empty;
    }

    /// <summary>Perfis de usuario do Chromium: "Default" e "Profile N".</summary>
    private static bool IsChromiumProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName)) return false;
        if (string.Equals(profileName, "Default", StringComparison.OrdinalIgnoreCase)) return true;
        return profileName.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Extensoes do Firefox, lidas do extensions.json de cada perfil.</summary>
    private static IEnumerable<BrowserExtension> ListFirefox()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData)) yield break;

        string profilesRoot;
        try { profilesRoot = Path.Combine(appData, "Mozilla", "Firefox", "Profiles"); }
        catch { yield break; }

        if (!Directory.Exists(profilesRoot)) yield break;

        string[] profiles;
        try { profiles = Directory.GetDirectories(profilesRoot); }
        catch { yield break; }

        foreach (var profile in profiles)
        {
            var profileName = Path.GetFileName(profile);
            var extensionsJson = Path.Combine(profile, "extensions.json");
            if (!File.Exists(extensionsJson)) continue;

            JsonNode? root;
            try { root = JsonNode.Parse(File.ReadAllText(extensionsJson)); }
            catch { continue; }

            if (root is not JsonObject obj) continue;
            if (obj["addons"] is not JsonArray addons) continue;

            foreach (var node in addons)
            {
                var extension = ReadFirefoxAddon(node, profileName, profile);
                if (extension is not null) yield return extension;
            }
        }
    }

    private static BrowserExtension? ReadFirefoxAddon(JsonNode? node, string profileName, string profile)
    {
        try
        {
            if (node is null) return null;

            var id = NodeString(node["id"]);
            if (string.IsNullOrWhiteSpace(id)) return null;

            var type = NodeString(node["type"]) ?? string.Empty;
            var location = NodeString(node["location"]) ?? string.Empty;

            var name = NodeString(node["defaultLocale"]?["name"])
                       ?? NodeString(node["name"])
                       ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)) name = id;

            var version = NodeString(node["version"]) ?? string.Empty;
            var active = NodeBool(node["active"]);
            var userDisabled = NodeBool(node["userDisabled"]);
            var appDisabled = NodeBool(node["appDisabled"]);

            return new BrowserExtension
            {
                Name = name,
                BrowserId = FirefoxId,
                BrowserName = "Mozilla Firefox",
                Id = id,
                Version = version,
                Profile = profileName,
                Path = FindXpi(profile, id),
                IsEnabled = active && !userDisabled && !appDisabled,
                IsBuiltIn = string.Equals(location, "app-builtin", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(type, "builtinwebextension", StringComparison.OrdinalIgnoreCase),
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Localiza o arquivo .xpi de uma extensao do Firefox.</summary>
    private static string FindXpi(string profile, string id)
    {
        try
        {
            var dir = Path.Combine(profile, "extensions");
            if (!Directory.Exists(dir)) return string.Empty;

            var files = Directory.GetFiles(dir, "*.xpi");
            foreach (var file in files)
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(file), id, StringComparison.OrdinalIgnoreCase))
                    return PathUtil.Normalize(file);
            }

            foreach (var file in files)
            {
                if (Path.GetFileNameWithoutExtension(file).StartsWith(id, StringComparison.OrdinalIgnoreCase))
                    return PathUtil.Normalize(file);
            }

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Remove a pasta da extensao (navegadores Chromium).</summary>
    private static bool RemoveChromium(BrowserExtension extension)
    {
        try
        {
            var items = new List<LeftoverItem>
            {
                new()
                {
                    Kind = "Pasta",
                    Path = extension.Path,
                    Detail = $"Extensão {extension.Name} ({extension.BrowserName})",
                },
            };

            var report = UninstallEngine.RemoveLeftovers(items);
            return report.Outcomes.Count > 0 && report.Outcomes.All(o => o.Gone);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Remove a extensao do Firefox: apaga o .xpi (pela rotina canonica) e tira a
    /// entrada do extensions.json.
    /// </summary>
    private static bool RemoveFirefox(BrowserExtension extension)
    {
        try
        {
            var fileOk = true;
            if (!string.IsNullOrWhiteSpace(extension.Path))
            {
                var items = new List<LeftoverItem>
                {
                    new()
                    {
                        Kind = "Arquivo",
                        Path = extension.Path,
                        Detail = $"Extensão {extension.Name} (Mozilla Firefox)",
                    },
                };

                var report = UninstallEngine.RemoveLeftovers(items);
                fileOk = report.Outcomes.Count > 0 && report.Outcomes.All(o => o.Gone);
            }

            var jsonOk = RemoveFirefoxEntry(extension);
            return fileOk && jsonOk;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Retira a entrada da extensao do extensions.json do perfil.</summary>
    private static bool RemoveFirefoxEntry(BrowserExtension extension)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(extension.Path)) return true;

            var extensionsDir = Path.GetDirectoryName(extension.Path);
            if (string.IsNullOrEmpty(extensionsDir)) return true;

            var profile = Path.GetDirectoryName(extensionsDir);
            if (string.IsNullOrEmpty(profile)) return true;

            var jsonPath = Path.Combine(profile, "extensions.json");
            if (!File.Exists(jsonPath)) return true;

            var root = JsonNode.Parse(File.ReadAllText(jsonPath));
            if (root is not JsonObject obj) return false;
            if (obj["addons"] is not JsonArray addons) return true;

            var kept = new JsonArray();
            foreach (var node in addons)
            {
                var id = NodeString(node?["id"]);
                if (!string.IsNullOrEmpty(id)
                    && string.Equals(id, extension.Id, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                kept.Add(node?.DeepClone());
            }

            obj["addons"] = kept;

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
            File.WriteAllText(jsonPath, obj.ToJsonString(options), new UTF8Encoding(false));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? NodeString(JsonNode? node)
    {
        try
        {
            if (node is null) return null;
            return node.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    private static bool NodeBool(JsonNode? node)
    {
        try
        {
            return node is not null && node.GetValue<bool>();
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        try
        {
            if (element.ValueKind != JsonValueKind.Object) return false;
            if (element.TryGetProperty(name, out value)) return true;

            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            return false;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    private static string GetString(JsonElement element, string name)
    {
        try
        {
            if (!TryGetProperty(element, name, out var value)) return string.Empty;
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.ToString(),
                _ => string.Empty,
            };
        }
        catch
        {
            return string.Empty;
        }
    }
}
