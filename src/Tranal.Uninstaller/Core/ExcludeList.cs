using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Lista de exclusoes: caminhos e chaves de registro que o Tranal nunca toca.
///
/// Corresponde ao original nas strings "Add to Exclude List" (15102),
/// "Folder to exclude" (15315), "Files and Folders to Exclude" (15316) e
/// "Registry Keys to Exclude" (15317).
///
/// A regra e simples e previsivel: excluir <c>C:\Programas\App</c> protege o
/// proprio e tudo que estiver dentro dele. O item excluido nem aparece na
/// lista de sobras, e — o que importa — o motor de remocao recusa qualquer
/// item que esteja na lista, mesmo que ele chegue marcado por outro caminho.
/// </summary>
public sealed class ExcludeList
{
    /// <summary>Pastas e arquivos protegidos.</summary>
    public List<string> Paths { get; init; } = new();

    /// <summary>Chaves de registro protegidas (caminho canonico HKEY_...).</summary>
    public List<string> RegistryKeys { get; init; } = new();

    /// <summary>True quando a lista nao tem nenhum item.</summary>
    [JsonIgnore]
    public bool IsEmpty => Paths.Count == 0 && RegistryKeys.Count == 0;

    // -----------------------------------------------------------------
    // Casamento
    // -----------------------------------------------------------------

    /// <summary>
    /// True quando o caminho informado esta protegido pela lista.
    /// O casamento e por prefixo de pasta: excluir uma pasta protege o
    /// conteudo dela.
    /// </summary>
    public bool ContainsPath(string? path)
    {
        var candidate = PathUtil.Normalize(path);
        if (candidate.Length == 0) return false;

        foreach (var entry in Paths)
        {
            var normalized = PathUtil.Normalize(entry);
            if (normalized.Length == 0) continue;

            if (string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase)) return true;

            if (candidate.Length > normalized.Length
                && candidate.StartsWith(normalized, StringComparison.OrdinalIgnoreCase)
                && candidate[normalized.Length] == '\\')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True quando a chave de registro informada esta protegida.
    /// O casamento e por prefixo de subchave.
    /// </summary>
    public bool ContainsRegistryKey(string? registryPath)
    {
        var candidate = NormalizeRegistryPath(registryPath);
        if (candidate.Length == 0) return false;

        foreach (var entry in RegistryKeys)
        {
            var normalized = NormalizeRegistryPath(entry);
            if (normalized.Length == 0) continue;

            if (string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase)) return true;

            if (candidate.Length > normalized.Length
                && candidate.StartsWith(normalized, StringComparison.OrdinalIgnoreCase)
                && candidate[normalized.Length] == '\\')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True quando o item (pasta, arquivo ou chave) esta protegido.
    /// </summary>
    public bool Contains(string kind, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        if (string.Equals(kind, "Registro", StringComparison.OrdinalIgnoreCase))
            return ContainsRegistryKey(path);

        return ContainsPath(path);
    }

    /// <summary>
    /// Remove da lista os itens protegidos. E o filtro aplicado na varredura:
    /// um item excluido nem chega a aparecer para o usuario.
    /// </summary>
    public List<LeftoverItem> ApplyTo(IEnumerable<LeftoverItem> items) =>
        items.Where(i => !Contains(i.Kind, i.Path)).ToList();

    /// <summary>
    /// Adiciona um item de sobra a lista, escolhendo a lista pelo tipo do item.
    /// Devolve false quando o item ja estava na lista.
    /// </summary>
    public bool AddItem(LeftoverItem? item)
    {
        if (item is null) return false;

        return string.Equals(item.Kind, "Registro", StringComparison.OrdinalIgnoreCase)
            ? AddRegistryKey(item.Path)
            : AddPath(item.Path);
    }

    // -----------------------------------------------------------------
    // Alteracoes
    // -----------------------------------------------------------------

    /// <summary>Adiciona um caminho. Devolve false quando ja estava na lista.</summary>
    public bool AddPath(string? path)
    {
        var normalized = PathUtil.Normalize(path);
        if (normalized.Length == 0) return false;

        if (Paths.Any(p => string.Equals(PathUtil.Normalize(p), normalized, StringComparison.OrdinalIgnoreCase)))
            return false;

        Paths.Add(normalized);
        return true;
    }

    /// <summary>Adiciona uma chave de registro. Devolve false quando ja estava.</summary>
    public bool AddRegistryKey(string? registryPath)
    {
        var normalized = NormalizeRegistryPath(registryPath);
        if (normalized.Length == 0) return false;

        if (RegistryKeys.Any(k => string.Equals(NormalizeRegistryPath(k), normalized, StringComparison.OrdinalIgnoreCase)))
            return false;

        RegistryKeys.Add(normalized);
        return true;
    }

    /// <summary>Remove um caminho da lista. Devolve false quando nao estava.</summary>
    public bool RemovePath(string? path)
    {
        var normalized = PathUtil.Normalize(path);
        if (normalized.Length == 0) return false;

        var removed = Paths.RemoveAll(p =>
            string.Equals(PathUtil.Normalize(p), normalized, StringComparison.OrdinalIgnoreCase));

        return removed > 0;
    }

    /// <summary>Remove uma chave da lista. Devolve false quando nao estava.</summary>
    public bool RemoveRegistryKey(string? registryPath)
    {
        var normalized = NormalizeRegistryPath(registryPath);
        if (normalized.Length == 0) return false;

        var removed = RegistryKeys.RemoveAll(k =>
            string.Equals(NormalizeRegistryPath(k), normalized, StringComparison.OrdinalIgnoreCase));

        return removed > 0;
    }

    /// <summary>Esvazia a lista.</summary>
    public void Clear()
    {
        Paths.Clear();
        RegistryKeys.Clear();
    }

    /// <summary>
    /// Normaliza um caminho de registro para comparacao.
    ///
    /// Os nomes curtos de hive (HKLM, HKCU, HKU, HKCR, HKCC) sao expandidos
    /// para o nome canonico: o usuario digita "HKLM\SOFTWARE\X" na lista e o
    /// Tranal encontra o item cujo caminho e "HKEY_LOCAL_MACHINE\SOFTWARE\X".
    /// Sem essa equivalencia, a lista pareceria nao funcionar.
    /// </summary>
    private static string NormalizeRegistryPath(string? registryPath)
    {
        // A barra invertida inicial e aceita: o usuario pode digitar
        // "\HKLM\SOFTWARE\MeuApp", e o motor tambem aceita essa forma
        // (SplitRegistryPath faz TrimStart). Duas interpretacoes do mesmo
        // caminho seria a lista "parecer nao funcionar".
        var value = (registryPath ?? string.Empty).Trim().Trim('"').Replace('/', '\\').Trim('\\');
        if (value.Length == 0) return string.Empty;

        var separator = value.IndexOf('\\');
        var hive = separator < 0 ? value : value[..separator];
        var rest = separator < 0 ? string.Empty : value[separator..];

        var canonical = hive.ToUpperInvariant() switch
        {
            "HKLM" => "HKEY_LOCAL_MACHINE",
            "HKCU" => "HKEY_CURRENT_USER",
            "HKCR" => "HKEY_CLASSES_ROOT",
            "HKU" => "HKEY_USERS",
            "HKCC" => "HKEY_CURRENT_CONFIG",
            _ => hive.ToUpperInvariant(),
        };

        return canonical + rest;
    }

    // -----------------------------------------------------------------
    // Persistencia
    // -----------------------------------------------------------------

    /// <summary>
    /// Caminho do arquivo da lista. Existe para os testes: sem ele, um teste
    /// que mexe na lista escreveria no arquivo real do usuario.
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    /// <summary>Caminho do arquivo que guarda a lista.</summary>
    public static string FilePath =>
        FilePathOverride ?? Path.Combine(RegistryBackup.DataDirectory, "exclude-list.json");

    /// <summary>Le a lista do disco. Devolve uma lista vazia quando nao existe.</summary>
    public static ExcludeList Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new ExcludeList();

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<ExcludeList>(json) ?? new ExcludeList();
        }
        catch
        {
            return new ExcludeList();
        }
    }

    /// <summary>Grava a lista no disco. Devolve false quando nao deu para gravar.</summary>
    public bool Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() },
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });

            File.WriteAllText(FilePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// A lista em uso. Carregada uma unica vez e mantida em memoria para que
    /// o motor de remocao nao leia o disco a cada item.
    /// </summary>
    private static ExcludeList? _current;

    /// <summary>A lista de exclusoes ativa.</summary>
    public static ExcludeList Current => _current ??= Load();

    /// <summary>Substitui a lista ativa (usado pelo gerenciador e pelos testes).</summary>
    public static void SetCurrent(ExcludeList list) => _current = list;

    /// <summary>
    /// Esquece a lista ativa em memoria, para que a proxima leitura volte do
    /// disco. Existe para os testes: sem ele, um teste que trocasse a lista
    /// deixaria o estado alterado para os testes seguintes.
    /// </summary>
    internal static void ResetCurrent() => _current = null;

    /// <summary>Recarrega a lista ativa do disco.</summary>
    public static void Reload() => _current = Load();
}
