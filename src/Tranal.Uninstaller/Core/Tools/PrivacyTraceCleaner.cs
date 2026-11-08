using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Os grupos de rastros de privacidade que o limpador limpa.
/// </summary>
public enum PrivacyTraceGroup
{
    /// <summary>Regras do firewall do Windows.</summary>
    Firewall,

    /// <summary>Historico UserAssist (os programas que o usuario abriu).</summary>
    UserAssist,

    /// <summary>Relatorios de erro do Windows (WER) e despejos de memoria.</summary>
    ErrorReports,

    /// <summary>Tracos de classes COM registradas por usuario (CLSID).</summary>
    Clsid,

    /// <summary>Tracos de reproducao de midia (audio e video).</summary>
    Audio,

    /// <summary>Eventos do Windows (log de Aplicacao).</summary>
    EventLogs,
}

/// <summary>
/// A descricao de um grupo de rastros: o que ele apaga e a chave de idioma
/// do rotulo.
/// </summary>
public sealed class PrivacyTraceGroupInfo
{
    /// <summary>O grupo.</summary>
    public PrivacyTraceGroup Group { get; init; }

    /// <summary>O identificador estavel do grupo.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>A explicacao do que o grupo apaga.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>A chave de idioma do rotulo do grupo.</summary>
    public string LabelKey { get; init; } = string.Empty;
}

/// <summary>
/// Um rastro encontrado em um dos grupos de privacidade.
/// </summary>
public sealed class PrivacyTraceItem
{
    /// <summary>O grupo ao qual o item pertence.</summary>
    public PrivacyTraceGroup Group { get; init; }

    /// <summary>"Pasta", "Arquivo" ou "Valor de registro".</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// O caminho do item. Para valor de registro, o caminho e a chave
    /// seguida do nome do valor.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>A explicacao do que o item e.</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>True quando o item esta marcado para limpeza.</summary>
    public bool Selected { get; set; } = true;
}

/// <summary>
/// Limpeza dos rastros de privacidade: regras de firewall, UserAssist,
/// relatorios de erro, CLSID, audio e eventos.
///
/// A regra mora aqui, em um lugar so: cada grupo e listado e limpo por este
/// metodo, e nada fora dele toca nesses rastros. Um grupo nunca invade o
/// outro — a limpeza do firewall nao apaga UserAssist, e vice-versa.
///
/// A remocao de arquivo e pasta passa pelo motor canonico
/// (<see cref="UninstallEngine.RemoveLeftovers"/>), que respeita a lista de
/// exclusoes e o backup. A remocao de VALOR de registro e feita aqui, porque
/// o motor canonico remove chaves inteiras — e apagar a chave das regras de
/// firewall inteira apagaria as regras de todo o sistema.
/// </summary>
public static class PrivacyTraceCleaner
{
    /// <summary>A chave de registro com as regras do firewall.</summary>
    private const string FirewallRulesKey =
        @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules";

    /// <summary>A chave de registro do UserAssist.</summary>
    private const string UserAssistKey =
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist";

    /// <summary>A chave de registro do contador de reproducao de midia.</summary>
    private const string PlayCountKey =
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\PlayCount";

    /// <summary>A chave de registro das classes COM registradas por usuario.</summary>
    private const string UserClsidKey =
        @"HKEY_CURRENT_USER\Software\Classes\CLSID";

    /// <summary>O rastro do ultimo CLSID visitado pelo shell.</summary>
    private const string LastVisitedKey =
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\LastVisited";

    /// <summary>
    /// Os grupos, na ordem em que devem ser apresentados.
    /// </summary>
    public static IReadOnlyList<PrivacyTraceGroupInfo> Groups { get; } = new[]
    {
        new PrivacyTraceGroupInfo
        {
            Group = PrivacyTraceGroup.Firewall,
            Id = "firewall",
            Description = "Regras do firewall do Windows que apontam para programas.",
            LabelKey = "Privacy.Firewall",
        },
        new PrivacyTraceGroupInfo
        {
            Group = PrivacyTraceGroup.UserAssist,
            Id = "userassist",
            Description = "Histórico dos programas abertos pelo usuário.",
            LabelKey = "Privacy.UserAssist",
        },
        new PrivacyTraceGroupInfo
        {
            Group = PrivacyTraceGroup.ErrorReports,
            Id = "error-reports",
            Description = "Relatórios de erro do Windows e despejos de memória.",
            LabelKey = "Privacy.ErrorReports",
        },
        new PrivacyTraceGroupInfo
        {
            Group = PrivacyTraceGroup.Clsid,
            Id = "clsid",
            Description = "Classes COM registradas no perfil do usuário.",
            LabelKey = "Privacy.Clsid",
        },
        new PrivacyTraceGroupInfo
        {
            Group = PrivacyTraceGroup.Audio,
            Id = "audio",
            Description = "Histórico de reprodução de mídia do Windows.",
            LabelKey = "Privacy.Audio",
        },
        new PrivacyTraceGroupInfo
        {
            Group = PrivacyTraceGroup.EventLogs,
            Id = "event-logs",
            Description = "Log de Aplicação do Windows, que registra quais programas rodaram.",
            LabelKey = "Privacy.EventLogs",
        },
    };

    /// <summary>
    /// As pastas de relatorio de erro do Windows (WER) e de despejo de
    /// memoria.
    /// </summary>
    private static readonly string[] ErrorReportFolders =
    {
        @"%LOCALAPPDATA%\Microsoft\Windows\WER\ReportArchive",
        @"%LOCALAPPDATA%\Microsoft\Windows\WER\ReportQueue",
        @"%PROGRAMDATA%\Microsoft\Windows\WER\ReportArchive",
        @"%PROGRAMDATA%\Microsoft\Windows\WER\ReportQueue",
        @"%LOCALAPPDATA%\CrashDumps",
    };

    /// <summary>
    /// Lista os rastros do grupo informado (ou de todos os grupos).
    ///
    /// A listagem nunca lanca: um grupo inacessivel devolve lista vazia, e
    /// os outros grupos continuam sendo listados.
    /// </summary>
    /// <param name="group">O grupo a listar; nulo lista todos.</param>
    public static IReadOnlyList<PrivacyTraceItem> Scan(PrivacyTraceGroup? group = null)
    {
        var items = new List<PrivacyTraceItem>();

        if (group is null || group == PrivacyTraceGroup.Firewall)
            ScanFirewall(items);

        if (group is null || group == PrivacyTraceGroup.UserAssist)
            ScanUserAssist(items);

        if (group is null || group == PrivacyTraceGroup.ErrorReports)
            ScanErrorReports(items);

        if (group is null || group == PrivacyTraceGroup.Clsid)
            ScanClsid(items);

        if (group is null || group == PrivacyTraceGroup.Audio)
            ScanAudio(items);

        if (group is null || group == PrivacyTraceGroup.EventLogs)
            ScanEventLogs(items);

        return items;
    }

    /// <summary>
    /// Limpa os rastros marcados e devolve o relatorio canonico de remocao.
    ///
    /// So o que esta marcado e dentro de um grupo conhecido e limpo: um
    /// item de fora dos grupos e ignorado, e a lista de exclusoes continua
    /// valendo.
    /// </summary>
    /// <param name="items">Os rastros a limpar.</param>
    public static RemovalReport Clean(IEnumerable<PrivacyTraceItem>? items)
    {
        var outcomes = new List<RemovalOutcome>();

        var lista = (items ?? Enumerable.Empty<PrivacyTraceItem>())
            .Where(i => i is not null)
            .ToList();

        var arquivos = new List<LeftoverItem>();
        var valores = new List<PrivacyTraceItem>();

        foreach (var item in lista)
        {
            if (!item.Selected) continue;
            if (!IsKnownGroup(item.Group)) continue;

            // A lista de exclusoes e a ultima palavra, como em qualquer
            // remocao do Tranal.
            if (ExcludeList.Current.Contains("Registro", item.Path)) continue;

            if (item.Kind == "Valor de registro")
            {
                valores.Add(item);
                continue;
            }

            arquivos.Add(new LeftoverItem
            {
                Kind = item.Kind,
                Path = item.Path,
                Detail = item.Detail,
                Selected = true,
            });
        }

        // Arquivos e pastas: pelo motor canonico (backup, exclusoes, lixeira).
        if (arquivos.Count > 0)
        {
            try
            {
                outcomes.AddRange(UninstallEngine.RemoveLeftovers(arquivos).Outcomes);
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha na limpeza dos rastros: {ex.Message}");
            }
        }

        // Valores de registro: pelo caminho do proprio grupo, para que a
        // limpeza nao apague a chave inteira.
        foreach (var item in valores)
        {
            outcomes.Add(RemoveRegistryValue(item));
        }

        return new RemovalReport(outcomes);
    }

    // ---------------------------------------------------------------------
    // Listagem por grupo
    // ---------------------------------------------------------------------

    /// <summary>Regras do firewall: um item por regra.</summary>
    private static void ScanFirewall(List<PrivacyTraceItem> items)
    {
        foreach (var (name, value) in RegistryValuesOf(FirewallRulesKey))
        {
            items.Add(new PrivacyTraceItem
            {
                Group = PrivacyTraceGroup.Firewall,
                Kind = "Valor de registro",
                Path = FirewallRulesKey + "\\" + name,
                Detail = DescribeFirewallRule(value),
            });
        }
    }

    /// <summary>UserAssist: um item por programa registrado.</summary>
    private static void ScanUserAssist(List<PrivacyTraceItem> items)
    {
        var root = RegistryKeyOf(UserAssistKey);
        if (root is null) return;

        string[] guids;
        try { guids = root.GetSubKeyNames(); }
        catch { return; }

        foreach (var guid in guids)
        {
            var countPath = UserAssistKey + "\\" + guid + "\\Count";

            foreach (var (name, _) in RegistryValuesOf(countPath))
            {
                items.Add(new PrivacyTraceItem
                {
                    Group = PrivacyTraceGroup.UserAssist,
                    Kind = "Valor de registro",
                    Path = countPath + "\\" + name,
                    Detail = DecodeUserAssist(name),
                });
            }
        }
    }

    /// <summary>Relatorios de erro: as pastas do WER e de despejo de memoria.</summary>
    private static void ScanErrorReports(List<PrivacyTraceItem> items)
    {
        foreach (var relative in ErrorReportFolders)
        {
            var folder = Expand(relative);
            if (folder.Length == 0) continue;
            if (!Directory.Exists(folder)) continue;

            items.Add(new PrivacyTraceItem
            {
                Group = PrivacyTraceGroup.ErrorReports,
                Kind = "Pasta",
                Path = folder,
                Detail = "Relatórios de erro do Windows",
            });
        }
    }

    /// <summary>
    /// CLSID: as classes COM registradas so no perfil do usuario — as que
    /// nao tem servidor registrado (so tem "shell"), que e o rastro que os
    /// instaladores deixam.
    /// </summary>
    private static void ScanClsid(List<PrivacyTraceItem> items)
    {
        var root = RegistryKeyOf(UserClsidKey);
        if (root is not null)
        {
            string[] classes;
            try { classes = root.GetSubKeyNames(); }
            catch { classes = Array.Empty<string>(); }

            foreach (var clsid in classes)
            {
                if (!IsShellOnlyClass(root, clsid)) continue;

                items.Add(new PrivacyTraceItem
                {
                    Group = PrivacyTraceGroup.Clsid,
                    Kind = "Registro",
                    Path = UserClsidKey + "\\" + clsid,
                    Detail = "Classe COM registrada no perfil do usuário",
                });
            }
        }

        // O ultimo CLSID visitado pelo shell tambem e rastro.
        foreach (var (name, _) in RegistryValuesOf(LastVisitedKey))
        {
            items.Add(new PrivacyTraceItem
            {
                Group = PrivacyTraceGroup.Clsid,
                Kind = "Valor de registro",
                Path = LastVisitedKey + "\\" + name,
                Detail = "Última classe COM visitada pelo shell",
            });
        }
    }

    /// <summary>Audio: o contador de reproducao de midia do Explorer.</summary>
    private static void ScanAudio(List<PrivacyTraceItem> items)
    {
        foreach (var (name, _) in RegistryValuesOf(PlayCountKey))
        {
            items.Add(new PrivacyTraceItem
            {
                Group = PrivacyTraceGroup.Audio,
                Kind = "Valor de registro",
                Path = PlayCountKey + "\\" + name,
                Detail = "Contador de reproduções de mídia",
            });
        }
    }

    /// <summary>
    /// Eventos: o log de Aplicacao do Windows.
    ///
    /// A regra de qual log conta como rastro de uso ja mora no
    /// <see cref="EvidenceRemover"/>: este grupo consome o catalogo de la em
    /// vez de repetir a lista de logs.
    /// </summary>
    private static void ScanEventLogs(List<PrivacyTraceItem> items)
    {
        foreach (var evidence in EvidenceRemover.Scan())
        {
            if (!string.Equals(evidence.CategoryId, "event-logs", StringComparison.OrdinalIgnoreCase))
                continue;

            items.Add(new PrivacyTraceItem
            {
                Group = PrivacyTraceGroup.EventLogs,
                Kind = evidence.Kind,
                Path = evidence.Path,
                Detail = "Log de Aplicação do Windows",
            });
        }
    }

    // ---------------------------------------------------------------------
    // Auxiliares
    // ---------------------------------------------------------------------

    /// <summary>True quando o grupo e um dos grupos conhecidos.</summary>
    private static bool IsKnownGroup(PrivacyTraceGroup group) =>
        Enum.IsDefined(typeof(PrivacyTraceGroup), group);

    /// <summary>
    /// Abre a chave de registro do caminho canonico, ou devolve nulo quando
    /// ela nao existe ou nao pode ser lida.
    /// </summary>
    private static RegistryKey? RegistryKeyOf(string keyPath)
    {
        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(keyPath);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            return baseKey.OpenSubKey(sub);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao abrir '{keyPath}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Os valores (nome e conteudo) de uma chave de registro. Devolve lista
    /// vazia quando a chave nao existe.
    /// </summary>
    private static List<(string Name, object? Value)> RegistryValuesOf(string keyPath)
    {
        var result = new List<(string, object?)>();

        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(keyPath);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(sub);
            if (key is null) return result;

            foreach (var name in key.GetValueNames())
            {
                object? value = null;
                try { value = key.GetValue(name); }
                catch { /* valor inacessivel: o nome ja basta para listar */ }

                result.Add((name, value));
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler '{keyPath}': {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// True quando a classe COM nao tem servidor registrado (nem local nem
    /// remoto): e uma classe que so existe no perfil do usuario, e por isso
    /// e rastro.
    /// </summary>
    private static bool IsShellOnlyClass(RegistryKey root, string clsid)
    {
        try
        {
            using var key = root.OpenSubKey(clsid);
            if (key is null) return false;

            var names = key.GetSubKeyNames();
            foreach (var name in names)
            {
                if (name.Equals("InprocServer32", StringComparison.OrdinalIgnoreCase)) return false;
                if (name.Equals("LocalServer32", StringComparison.OrdinalIgnoreCase)) return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Apaga o valor de registro do item e devolve o resultado.
    /// </summary>
    private static RemovalOutcome RemoveRegistryValue(PrivacyTraceItem item)
    {
        var leftover = new LeftoverItem
        {
            Kind = "Registro",
            Path = item.Path,
            Detail = item.Detail,
            Selected = true,
        };

        try
        {
            var cut = item.Path.LastIndexOf('\\');
            if (cut <= 0)
                return new RemovalOutcome { Item = leftover, Status = RemovalStatus.Failed, Message = "caminho de registro inválido" };

            var keyPath = item.Path[..cut];
            var valueName = item.Path[(cut + 1)..];

            var (hive, sub) = SafetyRules.SplitRegistryPath(keyPath);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(sub, writable: true);

            if (key is null)
                return new RemovalOutcome { Item = leftover, Status = RemovalStatus.AlreadyAbsent, Message = "já não existia" };

            if (key.GetValue(valueName) is null)
                return new RemovalOutcome { Item = leftover, Status = RemovalStatus.AlreadyAbsent, Message = "já não existia" };

            key.DeleteValue(valueName, throwOnMissingValue: false);

            return new RemovalOutcome { Item = leftover, Status = RemovalStatus.Removed, Message = "removido" };
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao remover '{item.Path}': {ex.Message}");
            return new RemovalOutcome { Item = leftover, Status = RemovalStatus.Failed, Message = ex.Message };
        }
    }

    /// <summary>
    /// A descricao curta de uma regra de firewall: o programa e o nome da
    /// regra, quando o valor da regra os traz.
    /// </summary>
    private static string DescribeFirewallRule(object? value)
    {
        var text = value?.ToString();
        if (string.IsNullOrWhiteSpace(text)) return "Regra de firewall";

        var app = ExtractField(text, "App=");
        var name = ExtractField(text, "Name=");

        if (app.Length == 0 && name.Length == 0) return "Regra de firewall";
        if (app.Length == 0) return name;
        if (name.Length == 0) return app;

        return $"{name} ({app})";
    }

    /// <summary>Le o campo "X=" de uma regra de firewall.</summary>
    private static string ExtractField(string text, string field)
    {
        var index = text.IndexOf(field, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return string.Empty;

        var rest = text[(index + field.Length)..];
        var end = rest.IndexOf('|');
        if (end >= 0) rest = rest[..end];

        return rest.Trim();
    }

    /// <summary>
    /// O nome gravado no UserAssist vem codificado em ROT13. Decodificar e
    /// mostrar o nome verdadeiro e o que torna o rastro legivel.
    /// </summary>
    private static string DecodeUserAssist(string name)
    {
        var sb = new StringBuilder(name.Length);

        foreach (var ch in name)
        {
            if (ch is >= 'A' and <= 'Z') sb.Append((char)('A' + (ch - 'A' + 13) % 26));
            else if (ch is >= 'a' and <= 'z') sb.Append((char)('a' + (ch - 'a' + 13) % 26));
            else sb.Append(ch);
        }

        return sb.ToString();
    }

    /// <summary>Expande as pastas do sistema escritas com "%VARIAVEL%".</summary>
    private static string Expand(string path)
    {
        try
        {
            return PathUtil.Normalize(Environment.ExpandEnvironmentVariables(path));
        }
        catch
        {
            return string.Empty;
        }
    }
}
