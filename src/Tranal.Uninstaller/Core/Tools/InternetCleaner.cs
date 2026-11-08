using System.Text;
using System.IO;
using System.Text.Json;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Um perfil de navegador encontrado no disco.
/// </summary>
public sealed class BrowserProfile
{
    /// <summary>Nome do navegador (Chrome, Edge, Firefox...).</summary>
    public string Browser { get; init; } = string.Empty;

    /// <summary>Nome do perfil (Default, Profile 1...).</summary>
    public string ProfileName { get; init; } = string.Empty;

    /// <summary>Caminho da pasta do perfil.</summary>
    public string ProfilePath { get; init; } = string.Empty;
}

/// <summary>
/// Um dado de navegador que o limpador pode apagar.
/// </summary>
public sealed class InternetCleanerItem
{
    /// <summary>O navegador a que o item pertence.</summary>
    public string Browser { get; init; } = string.Empty;

    /// <summary>O perfil a que o item pertence.</summary>
    public string ProfileName { get; init; } = string.Empty;

    /// <summary>
    /// O tipo do dado: "Cookies", "Historico", "Cache", "Sessoes",
    /// "Formularios" ou "Excecoes".
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>O caminho do arquivo (ou da pasta) que guarda o dado.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Tamanho ocupado pelo dado, em bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>True quando o item esta marcado para limpeza.</summary>
    public bool Selected { get; set; } = true;

    /// <summary>
    /// True quando o item e protegido por uma excecao do usuario (site na
    /// lista de excecoes) e por isso nao deve ser apagado.
    /// </summary>
    public bool IsProtected { get; init; }

    /// <summary>Os dominios de excecao encontrados dentro do arquivo.</summary>
    public IReadOnlyList<string> ExceptedHosts { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Limpeza de dados de navegador: cookies, historico, cache, sessoes e
/// formularios.
///
/// O ashampoo Internet Cleaner e o IObit Browser Cleaner fazem a mesma coisa:
/// uma lista de dados por navegador, com selecao por item e uma lista de
/// excecao ("cookies seguros") que protege os sites que o usuario nao quer
/// perder.
///
/// A REGRA do que e "dado de navegador" mora aqui, em um lugar so: a pasta de
/// perfil e a mesma em todos os navegadores derivados do Chromium, e o
/// Firefox usa os nomes proprios. Quem precisa limpar consome esta lista.
///
/// Limitacao medida e assumida: sem SQLite embarcado, a limpeza e por
/// arquivo — nao por cookie. Um site na lista de excecoes protege o arquivo
/// inteiro de cookies, e nao apenas o cookie daquele site.
/// </summary>
public static class InternetCleaner
{
    /// <summary>Nome do arquivo que guarda a lista de excecoes.</summary>
    private const string ExceptionsFileName = "internet-cleaner.json";

    /// <summary>Identificador da categoria de cookies.</summary>
    public const string KindCookies = "Cookies";

    /// <summary>Identificador da categoria de historico.</summary>
    public const string KindHistory = "Historico";

    /// <summary>Identificador da categoria de cache.</summary>
    public const string KindCache = "Cache";

    /// <summary>Identificador da categoria de sessoes.</summary>
    public const string KindSessions = "Sessoes";

    /// <summary>Identificador da categoria de formularios.</summary>
    public const string KindForms = "Formularios";

    /// <summary>
    /// Os navegadores baseados em Chromium e a pasta onde os perfis moram.
    /// O perfil real e uma subpasta ("Default", "Profile 1"...).
    /// </summary>
    private static readonly (string Browser, string BasePath)[] ChromiumBrowsers =
    {
        ("Chrome", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data")),
        ("Edge", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "User Data")),
        ("Brave", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BraveSoftware", "Brave-Browser", "User Data")),
        ("Vivaldi", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vivaldi", "User Data")),
    };

    /// <summary>
    /// Os navegadores com pasta propria (sem a estrutura "User Data").
    /// </summary>
    private static readonly (string Browser, string BasePath)[] OwnBrowsers =
    {
        ("Opera", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Opera Software", "Opera Stable")),
        ("Firefox", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla", "Firefox", "Profiles")),
    };

    /// <summary>
    /// Os perfis de navegador encontrados no disco.
    /// </summary>
    public static IReadOnlyList<BrowserProfile> ListProfiles()
    {
        var perfis = new List<BrowserProfile>();

        foreach (var (browser, basePath) in ChromiumBrowsers)
        {
            if (!Directory.Exists(basePath)) continue;

            foreach (var perfil in Directory.EnumerateDirectories(basePath))
            {
                var nome = Path.GetFileName(perfil);
                // "Crashpad", "ShaderCache" e "System Profile" nao sao perfis.
                if (nome.StartsWith("Crashpad", StringComparison.OrdinalIgnoreCase)) continue;
                if (nome.Equals("System Profile", StringComparison.OrdinalIgnoreCase)) continue;

                perfis.Add(new BrowserProfile
                {
                    Browser = browser,
                    ProfileName = nome,
                    ProfilePath = perfil,
                });
            }
        }

        foreach (var (browser, basePath) in OwnBrowsers)
        {
            if (!Directory.Exists(basePath)) continue;

            if (browser == "Firefox")
            {
                foreach (var perfil in Directory.EnumerateDirectories(basePath))
                {
                    perfis.Add(new BrowserProfile
                    {
                        Browser = browser,
                        ProfileName = Path.GetFileName(perfil),
                        ProfilePath = perfil,
                    });
                }

                continue;
            }

            perfis.Add(new BrowserProfile
            {
                Browser = browser,
                ProfileName = "Perfil unico",
                ProfilePath = basePath,
            });
        }

        return perfis;
    }

    /// <summary>
    /// Varre os dados de navegador e devolve a lista do que pode ser limpo.
    /// </summary>
    public static IReadOnlyList<InternetCleanerItem> Scan()
    {
        var itens = new List<InternetCleanerItem>();
        var excecoes = LoadExceptions();

        foreach (var perfil in ListProfiles())
        {
            try
            {
                itens.AddRange(ScanProfile(perfil, excecoes));
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao varrer o perfil '{perfil.ProfilePath}': {ex.Message}");
            }
        }

        return itens;
    }

    /// <summary>
    /// Varre apenas os dados de um navegador.
    /// </summary>
    /// <param name="browser">O nome do navegador (Chrome, Edge, Firefox...).</param>
    public static IReadOnlyList<InternetCleanerItem> Scan(string browser)
    {
        if (string.IsNullOrWhiteSpace(browser)) return Array.Empty<InternetCleanerItem>();

        return Scan()
            .Where(i => string.Equals(i.Browser, browser, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Limpa os itens marcados e devolve o relatorio canonico.
    /// </summary>
    /// <param name="items">Os itens marcados para limpeza.</param>
    public static RemovalReport Clear(IEnumerable<InternetCleanerItem> items)
    {
        var outcomes = new List<RemovalOutcome>();
        var sobras = new List<LeftoverItem>();

        foreach (var item in items)
        {
            if (!item.Selected) continue;
            if (item.IsProtected) continue;

            sobras.Add(new LeftoverItem
            {
                Kind = Directory.Exists(item.Path) ? "Pasta" : "Arquivo",
                Path = item.Path,
                Detail = $"{item.Browser} - {item.Kind}",
                Selected = true,
            });
        }

        if (sobras.Count == 0) return new RemovalReport(outcomes);

        try
        {
            // Limpeza de dado de navegador e definitiva: nao vai para a Lixeira
            // (o navegador ainda esta aberto) e pode ser adiada para o proximo
            // reinicio quando o processo segura o arquivo.
            outcomes.AddRange(UninstallEngine.RemoveLeftovers(sobras, new UninstallRemovalOptions
            {
                Backup = false,
                UseRecycleBin = false,
                DeferToNextRestart = true,
            }).Outcomes);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha na limpeza dos dados de navegador: {ex.Message}");
        }

        return new RemovalReport(outcomes);
    }

    // ---------------------------------------------------------------------
    // Excecoes
    // ---------------------------------------------------------------------

    /// <summary>
    /// A lista de dominios protegidos ("cookies seguros").
    /// </summary>
    public static IReadOnlyList<string> LoadExceptions()
    {
        try
        {
            var path = ExceptionsPath();
            if (!File.Exists(path)) return Array.Empty<string>();

            var json = File.ReadAllText(path, Encoding.UTF8);
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("dominios", out var dominios)) return Array.Empty<string>();

            var lista = new List<string>();
            foreach (var elemento in dominios.EnumerateArray())
            {
                var valor = elemento.GetString();
                if (!string.IsNullOrWhiteSpace(valor)) lista.Add(valor.Trim());
            }

            return lista;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler a lista de excecoes: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Acrescenta um dominio a lista de excecoes.
    /// </summary>
    /// <param name="domain">O dominio protegido.</param>
    public static bool AddException(string? domain)
    {
        var valor = NormalizeDomain(domain);
        if (valor.Length == 0) return false;

        var lista = LoadExceptions().ToList();
        if (lista.Any(d => string.Equals(d, valor, StringComparison.OrdinalIgnoreCase))) return false;

        lista.Add(valor);
        SaveExceptions(lista);
        return true;
    }

    /// <summary>
    /// Remove um dominio da lista de excecoes.
    /// </summary>
    /// <param name="domain">O dominio protegido.</param>
    public static bool RemoveException(string? domain)
    {
        var valor = NormalizeDomain(domain);
        if (valor.Length == 0) return false;

        var lista = LoadExceptions().ToList();
        var rest = lista.Where(d => !string.Equals(d, valor, StringComparison.OrdinalIgnoreCase)).ToList();
        if (rest.Count == lista.Count) return false;

        SaveExceptions(rest);
        return true;
    }

    /// <summary>
    /// True quando o dominio esta na lista de excecoes.
    /// </summary>
    /// <param name="host">O host a consultar.</param>
    public static bool IsExcepted(string? host)
    {
        var valor = NormalizeDomain(host);
        if (valor.Length == 0) return false;

        return LoadExceptions().Any(d => string.Equals(d, valor, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Grava a lista de excecoes no disco.
    /// </summary>
    private static void SaveExceptions(IReadOnlyList<string> dominios)
    {
        try
        {
            var path = ExceptionsPath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(new { dominios }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao gravar a lista de excecoes: {ex.Message}");
        }
    }

    /// <summary>
    /// Caminho alternativo do arquivo de excecoes. Os testes usam para nao
    /// tocar no ambiente de quem esta rodando a suite.
    /// </summary>
    internal static string? ExceptionsPathOverride { get; set; }

    /// <summary>
    /// O caminho do arquivo de excecoes.
    /// </summary>
    private static string ExceptionsPath() =>
        ExceptionsPathOverride ?? Path.Combine(RegistryBackup.DataDirectory, ExceptionsFileName);

    /// <summary>
    /// Normaliza um dominio: sem esquema, sem barra final e em minusculas.
    /// </summary>
    private static string NormalizeDomain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return string.Empty;

        var valor = domain.Trim().ToLowerInvariant();
        if (valor.StartsWith("https://", StringComparison.Ordinal)) valor = valor[8..];
        else if (valor.StartsWith("http://", StringComparison.Ordinal)) valor = valor[7..];
        if (valor.StartsWith("www.", StringComparison.Ordinal)) valor = valor[4..];

        var barra = valor.IndexOf('/');
        if (barra >= 0) valor = valor[..barra];

        return valor.Trim();
    }

    // ---------------------------------------------------------------------
    // Varredura por perfil
    // ---------------------------------------------------------------------

    /// <summary>
    /// Os dados de um perfil, ja com as excecoes aplicadas.
    /// </summary>
    private static IEnumerable<InternetCleanerItem> ScanProfile(BrowserProfile perfil, IReadOnlyList<string> excecoes)
    {
        if (string.Equals(perfil.Browser, "Firefox", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var item in ScanFirefox(perfil)) yield return item;
            yield break;
        }

        foreach (var (kind, relative) in new[]
                 {
                     (KindCookies, Path.Combine("Network", "Cookies")),
                     (KindHistory, "History"),
                     (KindCache, Path.Combine("Cache", "Cache_Data")),
                     (KindCache, "Code Cache"),
                     (KindCache, "GPUCache"),
                     (KindSessions, "Sessions"),
                     (KindForms, "Form Data"),
                     (KindForms, "Login Data"),
                 })
        {
            var path = Path.Combine(perfil.ProfilePath, relative);
            if (!File.Exists(path) && !Directory.Exists(path)) continue;

            IReadOnlyList<string> excepted = Array.Empty<string>();
            if (kind == KindCookies)
            {
                excepted = ExceptedHostsIn(path, excecoes);
            }

            yield return new InternetCleanerItem
            {
                Browser = perfil.Browser,
                ProfileName = perfil.ProfileName,
                Kind = kind,
                Path = path,
                SizeBytes = SafeSize(path),
                IsProtected = excepted.Count > 0,
                ExceptedHosts = excepted,
            };
        }
    }

    /// <summary>
    /// Os dados de um perfil do Firefox.
    /// </summary>
    private static IEnumerable<InternetCleanerItem> ScanFirefox(BrowserProfile perfil)
    {
        foreach (var (kind, file) in new[]
                 {
                     (KindCookies, "cookies.sqlite"),
                     (KindHistory, "places.sqlite"),
                     (KindHistory, "formhistory.sqlite"),
                     (KindCache, "cache2"),
                     (KindSessions, "sessionstore.jsonlz4"),
                     (KindSessions, "sessionstore-backups"),
                 })
        {
            var path = Path.Combine(perfil.ProfilePath, file);
            if (!File.Exists(path) && !Directory.Exists(path)) continue;

            yield return new InternetCleanerItem
            {
                Browser = perfil.Browser,
                ProfileName = perfil.ProfileName,
                Kind = kind,
                Path = path,
                SizeBytes = SafeSize(path),
            };
        }
    }

    /// <summary>
    /// Os dominios da lista de excecoes que aparecem dentro do arquivo de
    /// cookies.
    ///
    /// O arquivo de cookies e um banco SQLite: o dominio fica gravado como
    /// texto puro dentro do arquivo, entao a busca pelos bytes do dominio
    /// encontra o que existe de verdade — sem precisar do motor SQLite.
    /// </summary>
    private static IReadOnlyList<string> ExceptedHostsIn(string path, IReadOnlyList<string> excecoes)
    {
        if (excecoes.Count == 0) return Array.Empty<string>();

        var encontrados = new List<string>();

        try
        {
            if (!File.Exists(path)) return Array.Empty<string>();

            var dados = File.ReadAllBytes(path);
            var texto = Encoding.UTF8.GetString(dados);

            foreach (var dominio in excecoes)
            {
                if (dominio.Length == 0) continue;
                if (texto.Contains(dominio, StringComparison.OrdinalIgnoreCase))
                {
                    encontrados.Add(dominio);
                }
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao procurar dominios exceptados em '{path}': {ex.Message}");
        }

        return encontrados;
    }

    /// <summary>
    /// O tamanho ocupado pelo item, quando ele existe.
    /// </summary>
    private static long SafeSize(string path)
    {
        try
        {
            if (File.Exists(path)) return new FileInfo(path).Length;

            if (Directory.Exists(path))
            {
                long total = 0;
                foreach (var arquivo in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(arquivo).Length; } catch { /* arquivo sumiu no meio */ }
                }

                return total;
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao medir '{path}': {ex.Message}");
        }

        return 0;
    }
}
