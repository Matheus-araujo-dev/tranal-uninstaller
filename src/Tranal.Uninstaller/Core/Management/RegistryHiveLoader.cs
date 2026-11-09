using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// O estado de uma chave carregada.
/// </summary>
/// <param name="Name">O nome da chave carregada.</param>
/// <param name="Path">O caminho onde ela foi montada.</param>
/// <param name="SourceFile">O arquivo .hiv de origem.</param>
/// <param name="IsLoaded">Ela esta carregada agora?</param>
public sealed record LoadedHive(
    string Name,
    string Path,
    string SourceFile,
    bool IsLoaded);

/// <summary>
/// A carga e a descarga de chaves de registro.
///
/// O Windows sabe montar uma chave de registro a partir de um arquivo
/// (`.hiv`) — e e assim que se olha uma chave que veio de outro computador,
/// ou de um backup.
///
/// A API e <c>RegLoadKey</c> e <c>RegUnLoadKey</c>: a primeira precisa de
/// privilegio administrativo, e a segunda e a que devolve a chave.
///
/// A regra esta em um lugar so: este modulo sabe carregar, descarregar e
/// substituir chaves.
/// </summary>
public static class RegistryHiveLoader
{
    [DllImport("advapi32.dll", EntryPoint = "RegLoadKeyW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int RegLoadKey(IntPtr hKey, string lpSubKey, string lpFile);

    [DllImport("advapi32.dll", EntryPoint = "RegUnLoadKeyW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int RegUnLoadKey(IntPtr hKey, string lpSubKey);

    /// <summary>
    /// Carrega uma chave de registro a partir de um arquivo .hiv.
    /// </summary>
    /// <param name="name">O nome da chave (o que aparecera na arvore).</param>
    /// <param name="hiveFile">O arquivo .hiv.</param>
    /// <param name="targetPath">
    /// Onde a chave sera montada. Vazio monta em
    /// <c>HKLM\SOFTWARE\Tranal\Loaded\</c>.
    /// </param>
    /// <returns>Verdadeiro quando a chave ficou carregada.</returns>
    public static bool Load(string? name, string? hiveFile, string? targetPath = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (string.IsNullOrWhiteSpace(hiveFile)) return false;

        try
        {
            if (!File.Exists(hiveFile)) return false;

            var destino = string.IsNullOrWhiteSpace(targetPath)
                ? DefaultPathFor(name)
                : targetPath;

            using var hive = Registry.LocalMachine;
            using var key = hive.OpenSubKey(destino, writable: true);
            if (key is null) return false;

            return RegLoadKey(key.Handle.DangerousGetHandle(), name, hiveFile!) == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Descarrega uma chave montada.
    /// </summary>
    /// <param name="name">O nome da chave.</param>
    /// <param name="targetPath">Onde ela estava montada.</param>
    public static bool Unload(string? name, string? targetPath = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        try
        {
            var destino = string.IsNullOrWhiteSpace(targetPath)
                ? DefaultPathFor(name)
                : targetPath;

            using var hive = Registry.LocalMachine;
            using var key = hive.OpenSubKey(destino, writable: true);
            if (key is null) return false;

            return RegUnLoadKey(key.Handle.DangerousGetHandle(), name) == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Substitui a chave montada por outra: descarrega e carrega de novo.
    /// </summary>
    /// <param name="name">O nome da chave.</param>
    /// <param name="hiveFile">O novo arquivo .hiv.</param>
    /// <param name="targetPath">Onde ela esta montada.</param>
    public static bool Replace(string? name, string? hiveFile, string? targetPath = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        Unload(name, targetPath);
        return Load(name, hiveFile, targetPath);
    }

    /// <summary>
    /// As chaves montadas agora.
    /// </summary>
    public static IReadOnlyList<LoadedHive> List()
    {
        var root = DefaultRoot();

        try
        {
            using var hive = Registry.LocalMachine;
            using var key = hive.OpenSubKey(root);
            if (key is null) return Array.Empty<LoadedHive>();

            var entries = new List<LoadedHive>();

            foreach (var name in key.GetSubKeyNames())
            {
                using var sub = key.OpenSubKey(name);
                if (sub is null) continue;

                entries.Add(new LoadedHive(
                    name,
                    root + "\\" + name,
                    sub.GetValue("TranalHiveFile") as string ?? string.Empty,
                    true));
            }

            return entries;
        }
        catch
        {
            return Array.Empty<LoadedHive>();
        }
    }

    /// <summary>A raiz onde as chaves montadas ficam.</summary>
    public static string DefaultRoot() => @"SOFTWARE\Tranal\Loaded";

    /// <summary>
    /// O caminho padrao de montagem de uma chave.
    /// </summary>
    /// <param name="name">O nome da chave.</param>
    public static string DefaultPathFor(string name) => DefaultRoot() + "\\" + name;
}
