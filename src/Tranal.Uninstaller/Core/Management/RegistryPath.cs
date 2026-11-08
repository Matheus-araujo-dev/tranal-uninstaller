using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// Converte um caminho de registro no formato do Windows
/// ("HKEY_LOCAL_MACHINE\SOFTWARE\...") no par (hive, caminho relativo).
/// </summary>
/// <remarks>
/// O caminho que o Tranal guarda em <see cref="InstalledProgram.RegistryPath"/>
/// e sempre esse texto completo. Toda leitura de registro que precisa voltar
/// a chave do programa passa por aqui, para que a traducao do prefixo exista
/// em um lugar so.
/// </remarks>
public static class RegistryPath
{
    /// <summary>
    /// Traduz o prefixo do hive: "HKEY_LOCAL_MACHINE" vira
    /// <see cref="RegistryHive.LocalMachine"/> e assim por diante.
    /// </summary>
    /// <param name="registryPath">O caminho completo da chave.</param>
    /// <returns>O hive e o caminho relativo, ou null quando o texto nao e um
    /// caminho de registro reconhecivel.</returns>
    public static (RegistryHive Hive, string RelativePath)? Parse(string? registryPath)
    {
        if (string.IsNullOrWhiteSpace(registryPath)) return null;

        var text = registryPath.Trim();

        var hive = RegistryHive.LocalMachine;
        string relative;

        if (text.StartsWith(@"HKEY_LOCAL_MACHINE\", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.LocalMachine;
            relative = text[@"HKEY_LOCAL_MACHINE\".Length..];
        }
        else if (text.StartsWith(@"HKEY_CURRENT_USER\", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.CurrentUser;
            relative = text[@"HKEY_CURRENT_USER\".Length..];
        }
        else if (text.StartsWith(@"HKEY_CLASSES_ROOT\", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.ClassesRoot;
            relative = text[@"HKEY_CLASSES_ROOT\".Length..];
        }
        else if (text.StartsWith(@"HKEY_USERS\", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.Users;
            relative = text[@"HKEY_USERS\".Length..];
        }
        else if (text.StartsWith("LocalMachine\\", StringComparison.OrdinalIgnoreCase))
        {
            // O Tranal guarda o caminho do programa como
            // "LocalMachine\SOFTWARE\...\Uninstall\{GUID}" (o nome do hive,
            // sem o prefixo HKEY_). As duas formas valem.
            hive = RegistryHive.LocalMachine;
            relative = text["LocalMachine\\".Length..];
        }
        else if (text.StartsWith("CurrentUser\\", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.CurrentUser;
            relative = text["CurrentUser\\".Length..];
        }
        else if (text.StartsWith("ClassesRoot\\", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.ClassesRoot;
            relative = text["ClassesRoot\\".Length..];
        }
        else if (text.StartsWith("Users\\", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.Users;
            relative = text["Users\\".Length..];
        }
        else
        {
            // Sem prefixo conhecido: o caminho ja e relativo a um hive.
            relative = text;
        }

        relative = relative.Trim('\\');
        if (relative.Length == 0) return null;

        return (hive, relative);
    }

    /// <summary>
    /// Devolve o nome do hive como o Windows mostra
    /// ("HKEY_LOCAL_MACHINE").
    ///
    /// E a traducao inversa de <see cref="Parse"/>, e mora aqui para que
    /// ninguem monte o caminho na mao: e o mesmo par, nas duas direcoes.
    /// </summary>
    /// <param name="hive">O hive.</param>
    public static string HiveName(RegistryHive hive) => hive switch
    {
        RegistryHive.CurrentUser => "HKEY_CURRENT_USER",
        RegistryHive.LocalMachine => "HKEY_LOCAL_MACHINE",
        RegistryHive.ClassesRoot => "HKEY_CLASSES_ROOT",
        RegistryHive.Users => "HKEY_USERS",
        RegistryHive.CurrentConfig => "HKEY_CURRENT_CONFIG",
        RegistryHive.PerformanceData => "HKEY_PERFORMANCE_DATA",
        _ => "HKEY_LOCAL_MACHINE",
    };

    /// <summary>
    /// Abre a chave do registro e devolve o nome de um valor, ou null quando
    /// a chave ou o valor nao existem.
    /// </summary>
    /// <param name="registryPath">O caminho completo da chave.</param>
    /// <param name="valueName">O nome do valor lido.</param>
    /// <param name="view">A visao de 32 ou 64 bits.</param>
    public static string? ReadValue(string? registryPath, string valueName, RegistryView view)
    {
        var parsed = Parse(registryPath);
        if (parsed is null) return null;

        try
        {
            using var root = RegistryKey.OpenBaseKey(parsed.Value.Hive, view);
            using var key = root.OpenSubKey(parsed.Value.RelativePath);
            return key?.GetValue(valueName)?.ToString();
        }
        catch
        {
            // Chave protegida ou inexistente: o valor e desconhecido.
            return null;
        }
    }

    /// <summary>
    /// True quando a chave existe no registro.
    /// </summary>
    /// <param name="registryPath">O caminho completo da chave.</param>
    /// <param name="view">A visao de 32 ou 64 bits.</param>
    public static bool KeyExists(string? registryPath, RegistryView view)
    {
        var parsed = Parse(registryPath);
        if (parsed is null) return false;

        try
        {
            using var root = RegistryKey.OpenBaseKey(parsed.Value.Hive, view);
            using var key = root.OpenSubKey(parsed.Value.RelativePath);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }
}
