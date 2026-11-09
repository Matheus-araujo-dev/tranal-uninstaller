using Microsoft.Win32;
using System.IO;
using Tranal.Uninstaller.Core.Management;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Uma extensao de navegador que existe na pasta mas nao tem mais entrada no
/// registro.
/// </summary>
public sealed class OrphanExtension
{
    /// <summary>O navegador (Chrome, Edge...).</summary>
    public string Browser { get; init; } = string.Empty;

    /// <summary>O identificador da extensao (pasta).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>O nome da extensao, quando o manifesto diz.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>A pasta da extensao.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>O tamanho da pasta, em bytes.</summary>
    public long SizeBytes { get; init; }
}

/// <summary>
/// O resultado de uma limpeza de navegador.
/// </summary>
public sealed class BrowserCleanupResult
{
    /// <summary>Itens efetivamente removidos.</summary>
    public int Removed { get; init; }

    /// <summary>Itens que ficaram para o proximo reinicio.</summary>
    public int Deferred { get; init; }

    /// <summary>Itens que falharam.</summary>
    public int Failed { get; init; }

    /// <summary>A explicacao do resultado.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Limpeza de navegador: extensaoes orfas, reset de padroes e limpeza dos
/// dados.
///
/// O IObit tem "Browser Cleaner", que alem de limpar cookies e cache reseta
/// os padroes do navegador (pagina inicial, motor de busca) e remove as
/// extensoes que sobraram. O ashampoo tambem limpa as extensoes.
///
/// A REGRA mora aqui, em um lugar so: a lista de perfis de navegador e a
/// leitura do registro das extensoes ficam aqui. A limpeza dos cookies, do
/// historico e do cache e feita pelo modulo que ja existe
/// (<see cref="InternetCleaner"/>) — esta classe nao reimplementa a varredura.
///
/// Extensao orfa e a que continua na pasta mas perdeu a entrada do registro:
/// ela nao roda, mas ocupa espaco e continua aparecendo em alguns menus.
/// </summary>
public static class BrowserCleanup
{
    /// <summary>
    /// As extensoes orfas encontradas.
    /// </summary>
    public static IReadOnlyList<OrphanExtension> FindOrphanExtensions()
    {
        var orfas = new List<OrphanExtension>();

        foreach (var perfil in InternetCleaner.ListProfiles())
        {
            try
            {
                var basePath = Path.Combine(perfil.ProfilePath, "Extensions");
                if (!Directory.Exists(basePath)) continue;

                foreach (var pasta in Directory.EnumerateDirectories(basePath))
                {
                    var id = Path.GetFileName(pasta);
                    if (id.Length == 0) continue;

                    if (IsRegistered(perfil.Browser, id)) continue;

                    orfas.Add(new OrphanExtension
                    {
                        Browser = perfil.Browser,
                        Id = id,
                        Name = ReadName(pasta),
                        Path = PathUtil.Normalize(pasta),
                        SizeBytes = SafeSize(pasta),
                    });
                }
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao varrer as extensoes de '{perfil.ProfilePath}': {ex.Message}");
            }
        }

        return orfas;
    }

    /// <summary>
    /// Remove as extensoes orfas e devolve o resultado.
    /// </summary>
    /// <param name="extensions">As extensoes a remover.</param>
    public static BrowserCleanupResult RemoveOrphanExtensions(IEnumerable<OrphanExtension> extensions)
    {
        var removidos = 0;
        var adiados = 0;
        var falhas = 0;

        foreach (var extensao in extensions ?? Enumerable.Empty<OrphanExtension>())
        {
            if (extensao is null) continue;

            try
            {
                var relatorio = UninstallEngine.RemoveLeftovers(new List<LeftoverItem>
                {
                    new()
                    {
                        Kind = "Pasta",
                        Path = extensao.Path,
                        Detail = "Extensao orfa",
                        Selected = true,
                    },
                }, new UninstallRemovalOptions
                {
                    Backup = false,
                    UseRecycleBin = true,
                    DeferToNextRestart = true,
                });

                if (relatorio.Outcomes.Any(o => o.Removed)) removidos++;
                else if (relatorio.Outcomes.Any(o => o.Status == RemovalStatus.Deferred)) adiados++;
                else falhas++;
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao remover a extensao '{extensao.Path}': {ex.Message}");
                falhas++;
            }
        }

        return new BrowserCleanupResult
        {
            Removed = removidos,
            Deferred = adiados,
            Failed = falhas,
            Message = $"{removidos} removida(s), {adiados} adiada(s), {falhas} falha(s).",
        };
    }

    /// <summary>
    /// Reseta os padroes do navegador: pagina inicial, motor de busca e as
    /// politicas que os forcam.
    /// </summary>
    /// <param name="browser">O navegador (Chrome, Edge...).</param>
    public static BrowserCleanupResult ResetDefaults(string? browser)
    {
        var nome = (browser ?? string.Empty).Trim();
        if (nome.Length == 0) return new BrowserCleanupResult { Message = "Navegador nao informado." };

        var removidas = 0;
        var falhas = 0;

        // As politicas ficam na chave do navegador; e ela que forca a pagina
        // inicial e o motor de busca, e nao as preferencias do perfil.
        var chaves = new[]
        {
            @"HKEY_CURRENT_USER\Software\Policies\Google\Chrome",
            @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Edge",
            @"HKEY_LOCAL_MACHINE\Software\Policies\Google\Chrome",
            @"HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Edge",
        };

        foreach (var chave in chaves)
        {
            try
            {
                var (hive, sub) = SafetyRules.SplitRegistryPath(chave);
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var key = baseKey.OpenSubKey(sub, writable: true);
                if (key is null) continue;

                foreach (var valor in key.GetValueNames())
                {
                    try
                    {
                        key.DeleteValue(valor, throwOnMissingValue: false);
                        removidas++;
                    }
                    catch (Exception ex)
                    {
                        ToolLog.Error($"Falha ao remover o valor '{valor}' de '{chave}': {ex.Message}");
                        falhas++;
                    }
                }
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao resetar '{chave}': {ex.Message}");
                falhas++;
            }
        }

        return new BrowserCleanupResult
        {
            Removed = removidas,
            Failed = falhas,
            Message = $"{removidas} politica(s) removida(s).",
        };
    }

    /// <summary>
    /// Limpa os dados do navegador (cookies, historico, cache) usando o
    /// modulo que ja faz essa varredura.
    /// </summary>
    /// <param name="browser">O navegador (vazio = todos).</param>
    public static BrowserCleanupResult CleanData(string? browser)
    {
        var itens = string.IsNullOrWhiteSpace(browser)
            ? InternetCleaner.Scan()
            : InternetCleaner.Scan(browser);

        var relatorio = InternetCleaner.Clear(itens.Where(i => i.Selected && !i.IsProtected));

        return new BrowserCleanupResult
        {
            Removed = relatorio.Outcomes.Count(o => o.Removed),
            Deferred = relatorio.Outcomes.Count(o => o.Status == RemovalStatus.Deferred),
            Failed = relatorio.Outcomes.Count(o => o.Status == RemovalStatus.Failed),
            Message = "Limpeza dos dados do navegador concluida.",
        };
    }

    /// <summary>
    /// True quando a extensao ainda tem entrada no registro do navegador.
    /// </summary>
    /// <param name="browser">O navegador.</param>
    /// <param name="id">O identificador da extensao.</param>
    private static bool IsRegistered(string browser, string id)
    {
        try
        {
            var chaves = new[]
            {
                $@"HKEY_CURRENT_USER\Software\{browser}\Extensions\{id}",
                $@"HKEY_CURRENT_USER\Software\{browser}\NativeMessagingHosts\{id}",
            };

            foreach (var chave in chaves)
            {
                var (hive, sub) = SafetyRules.SplitRegistryPath(chave);
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var key = baseKey.OpenSubKey(sub);
                if (key is not null) return true;
            }

            // O Chrome guarda a extensao tambem dentro do proprio perfil.
            foreach (var perfil in InternetCleaner.ListProfiles())
            {
                if (!string.Equals(perfil.Browser, browser, StringComparison.OrdinalIgnoreCase)) continue;

                var preferencias = Path.Combine(perfil.ProfilePath, "Preferences");
                if (!File.Exists(preferencias)) continue;

                var texto = File.ReadAllText(preferencias);
                if (texto.Contains(id, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao consultar a extensao '{id}': {ex.Message}");
            return true;
        }
    }

    /// <summary>
    /// O nome da extensao, lido do manifesto.
    /// </summary>
    private static string ReadName(string pasta)
    {
        try
        {
            var manifesto = Path.Combine(pasta, "manifest.json");
            if (!File.Exists(manifesto)) return string.Empty;

            using var documento = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifesto));
            if (documento.RootElement.TryGetProperty("name", out var nome))
            {
                return nome.GetString() ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler o manifesto da extensao: {ex.Message}");
        }

        return string.Empty;
    }

    /// <summary>
    /// O tamanho de uma pasta, sem seguir os links.
    /// </summary>
    private static long SafeSize(string pasta)
    {
        try
        {
            long total = 0;
            foreach (var arquivo in Directory.EnumerateFiles(pasta, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(arquivo).Length; } catch { /* o arquivo sumiu no meio */ }
            }

            return total;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao medir '{pasta}': {ex.Message}");
            return 0;
        }
    }
}
