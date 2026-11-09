using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using Tranal.Uninstaller.Core.RegistryEditing;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// O estado de uma entrada do Windows que foi escondida da lista de
/// "Adicionar e Remover Programas".
/// </summary>
public sealed class HiddenEntry
{
    /// <summary>O caminho original da chave (com o nome verdadeiro).</summary>
    public string OriginalPath { get; init; } = string.Empty;

    /// <summary>O caminho com o nome escondido.</summary>
    public string HiddenPath { get; init; } = string.Empty;

    /// <summary>O nome do programa.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>A data em que a entrada foi escondida.</summary>
    public string HiddenAt { get; init; } = string.Empty;
}

/// <summary>
/// Esconde (ou remove) a entrada do programa na lista de "Adicionar e Remover
/// Programas" do Windows, sem desinstalar o programa.
///
/// O ashampoo tem "Remove entry from list of installed programs" e o HiBit
/// tem "Ocultar entrada de desinstalação". O Revo também esconde o programa
/// da lista do Windows.
///
/// A REGRA mora aqui, em um lugar so: o Windows so mostra a entrada quando o
/// nome da chave parece um identificador de produto. Renomear a chave com um
/// sufixo faz o Windows deixar de mostra-la — e como a renomeacao e
/// reversivel, a entrada volta quando o usuario quiser.
///
/// O estado das entradas escondidas fica em um arquivo, para que a operacao
/// seja reversivel mesmo depois de fechar o programa.
/// </summary>
public static class WindowsListEntry
{
    /// <summary>O sufixo que faz o Windows parar de mostrar a entrada.</summary>
    public const string HiddenSuffix = ".TranalHidden";

    /// <summary>O nome do arquivo com as entradas escondidas.</summary>
    private const string StateFileName = "entradas-ocultas.json";

    /// <summary>
    /// Esconde a entrada do programa na lista do Windows.
    /// </summary>
    /// <param name="program">O programa cuja entrada sera escondida.</param>
    /// <returns>True quando a entrada foi escondida agora.</returns>
    public static bool Hide(InstalledProgram? program)
    {
        if (program is null) return false;

        var original = PathUtil.Normalize(program.RegistryPath);
        if (original.Length == 0) return false;
        if (original.EndsWith(HiddenSuffix, StringComparison.OrdinalIgnoreCase)) return false;

        var escondido = original + HiddenSuffix;
        var resultado = RegistryEditor.RenameKey(original, UltimoSegmento(original) + HiddenSuffix);
        if (!resultado.Success) return false;

        Save(new HiddenEntry
        {
            OriginalPath = original,
            HiddenPath = escondido,
            Name = program.DisplayName,
            HiddenAt = DateTime.Now.ToString("O"),
        });

        return true;
    }

    /// <summary>
    /// Devolve a entrada escondida para a lista do Windows.
    /// </summary>
    /// <param name="program">O programa cuja entrada sera restaurada.</param>
    /// <returns>True quando a entrada voltou.</returns>
    public static bool Restore(InstalledProgram? program)
    {
        if (program is null) return false;

        var escondido = PathUtil.Normalize(program.RegistryPath);
        if (escondido.Length == 0) return false;

        var original = escondido.EndsWith(HiddenSuffix, StringComparison.OrdinalIgnoreCase)
            ? escondido[..^HiddenSuffix.Length]
            : escondido;

        var resultado = RegistryEditor.RenameKey(escondido, UltimoSegmento(original));
        if (!resultado.Success) return false;

        Forget(original);
        return true;
    }

    /// <summary>
    /// Remove a entrada do programa da lista do Windows, sem desinstalar o
    /// programa.
    /// </summary>
    /// <param name="program">O programa cuja entrada sera removida.</param>
    /// <returns>True quando a entrada foi removida.</returns>
    public static bool Remove(InstalledProgram? program)
    {
        if (program is null) return false;

        var caminho = PathUtil.Normalize(program.RegistryPath);
        if (caminho.Length == 0) return false;

        var resultado = RegistryEditor.DeleteKey(caminho);
        return resultado.Success;
    }

    /// <summary>
    /// As entradas escondidas pela ultima vez.
    /// </summary>
    public static IReadOnlyList<HiddenEntry> List()
    {
        try
        {
            var path = StatePath();
            if (!File.Exists(path)) return Array.Empty<HiddenEntry>();

            var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
            return JsonSerializer.Deserialize<List<HiddenEntry>>(json) ?? new List<HiddenEntry>();
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler as entradas escondidas: {ex.Message}");
            return Array.Empty<HiddenEntry>();
        }
    }

    /// <summary>
    /// O ultimo segmento de um caminho de registro (o nome da chave).
    /// </summary>
    /// <param name="path">O caminho completo.</param>
    public static string UltimoSegmento(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        var normalizado = path.Replace('/', '\\').TrimEnd('\\');
        var pos = normalizado.LastIndexOf('\\');
        return pos < 0 ? normalizado : normalizado[(pos + 1)..];
    }

    // ---------------------------------------------------------------------
    // Estado das entradas escondidas
    // ---------------------------------------------------------------------

    /// <summary>
    /// O caminho do arquivo com as entradas escondidas.
    /// </summary>
    private static string StatePath() =>
        Path.Combine(RegistryBackup.DataDirectory, StateFileName);

    /// <summary>
    /// Grava a entrada escondida no arquivo de estado.
    /// </summary>
    private static void Save(HiddenEntry entry)
    {
        try
        {
            var lista = List().ToList();
            lista.RemoveAll(e => string.Equals(e.OriginalPath, entry.OriginalPath, StringComparison.OrdinalIgnoreCase));
            lista.Add(entry);

            Write(lista);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao gravar o estado das entradas escondidas: {ex.Message}");
        }
    }

    /// <summary>
    /// Tira a entrada do arquivo de estado.
    /// </summary>
    private static void Forget(string originalPath)
    {
        try
        {
            var lista = List().ToList();
            var rest = lista
                .Where(e => !string.Equals(e.OriginalPath, originalPath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (rest.Count == lista.Count) return;

            Write(rest);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao limpar o estado das entradas escondidas: {ex.Message}");
        }
    }

    /// <summary>
    /// Grava a lista de entradas escondidas.
    /// </summary>
    private static void Write(IReadOnlyList<HiddenEntry> lista)
    {
        try
        {
            var path = StatePath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(lista, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao gravar as entradas escondidas: {ex.Message}");
        }
    }
}
