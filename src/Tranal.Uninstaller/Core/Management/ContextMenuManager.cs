using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// Uma entrada de menu de contexto (botao direito) instalada por um programa.
///
/// O Explorer nao guarda o caminho do programa: ele guarda um **CLSID**, e o
/// CLSID aponta para a DLL que faz o trabalho. Por isso a ligacao com o
/// programa instalado e feita pelo caminho da DLL — e nao pelo nome da
/// entrada, que o usuario pode ter renomeado.
/// </summary>
public sealed class ContextMenuEntry
{
    /// <summary>Onde a entrada aparece (ex.: "*", "Directory", "Folder").</summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>Identificador da classe COM (o <c>{...}</c>).</summary>
    public string Clsid { get; init; } = string.Empty;

    /// <summary>Nome exibido da entrada, quando o CLSID declara um.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Caminho da DLL (ou do executavel) que implementa a entrada.</summary>
    public string ServerPath { get; init; } = string.Empty;

    /// <summary>Chave de registro que registra a entrada.</summary>
    public string HandlerKeyPath { get; init; } = string.Empty;

    /// <summary>Vista do registro em que a entrada foi encontrada.</summary>
    public string RegistryView { get; init; } = string.Empty;

    /// <summary>True quando a entrada pertence a todos os usuarios.</summary>
    public bool IsMachineWide { get; init; }
}

/// <summary>
/// Gerenciador de entradas de menu de contexto.
///
/// O Tranal ja tem um gerenciador de menu de contexto **do proprio Tranal**
/// (<c>Core/Topmost/ExplorerContextMenu.cs</c>), que e outra coisa: este
/// gerencia o menu de contexto dos **outros** programas, para que a
/// desinstalacao de um programa remova a entrada que ele deixou.
/// </summary>
public static class ContextMenuManager
{
    /// <summary>
    /// Os pontos onde o Explorer aceita um manipulador de menu de contexto.
    ///
    /// Sao fixos no Windows; um programa que se registra em outro lugar nao
    /// aparece no menu de contexto e por isso nao entra aqui.
    /// </summary>
    private static readonly string[] HandlerLocations =
    {
        @"Software\Classes\*\shellex\ContextMenuHandlers",
        @"Software\Classes\AllFilesystemObjects\shellex\ContextMenuHandlers",
        @"Software\Classes\Directory\shellex\ContextMenuHandlers",
        @"Software\Classes\Directory\Background\shellex\ContextMenuHandlers",
        @"Software\Classes\Folder\shellex\ContextMenuHandlers",
        @"Software\Classes\Drive\shellex\ContextMenuHandlers",
        @"Software\Classes\Folder\shellex\ContextMenuHandlers\Directory\Background",
    };

    /// <summary>Todas as entradas de menu de contexto de terceiros.</summary>
    public static IReadOnlyList<ContextMenuEntry> List()
    {
        var entries = new List<ContextMenuEntry>();

        foreach (var (hive, isMachine) in new[]
                 {
                     (RegistryHive.CurrentUser, false),
                     (RegistryHive.LocalMachine, true),
                 })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var classes = baseKey.OpenSubKey(@"Software\Classes");
                    if (classes is null) continue;

                    foreach (var location in HandlerLocations)
                    {
                        using var handlers = classes.OpenSubKey(location);
                        if (handlers is null) continue;

                        foreach (var name in handlers.GetSubKeyNames())
                        {
                            using var handler = handlers.OpenSubKey(name);
                            if (handler is null) continue;

                            var clsid = handler.GetValue(null) as string ?? string.Empty;
                            if (clsid.Length == 0) continue;

                            entries.Add(new ContextMenuEntry
                            {
                                Location = location,
                                Clsid = clsid,
                                DisplayName = name,
                                ServerPath = ServerPathOf(classes, clsid),
                                HandlerKeyPath = $@"{RootPathOf(hive, view)}\{location}\{name}",
                                RegistryView = view.ToString(),
                                IsMachineWide = isMachine,
                            });
                        }
                    }
                }
                catch (System.Security.SecurityException)
                {
                    // Sem permissao para a vista do registro: segue para a outra.
                }
                catch (UnauthorizedAccessException)
                {
                    // Chave protegida: a listacao continua sem ela.
                }
                catch (IOException)
                {
                    // Registro momentaneamente indisponivel.
                }
            }
        }

        return entries;
    }

    /// <summary>
    /// As entradas de menu de contexto cujo servidor esta dentro do caminho.
    ///
    /// E o filtro que amarra a entrada ao programa instalado.
    /// </summary>
    public static IReadOnlyList<ContextMenuEntry> ListFor(string installPath)
    {
        var root = PathUtil.Normalize(installPath);
        if (string.IsNullOrEmpty(root)) return Array.Empty<ContextMenuEntry>();

        return List()
            .Where(e => ServiceManager.BelongsTo(e.ServerPath, root))
            .ToList();
    }

    /// <summary>Remove a entrada de menu de contexto.</summary>
    public static ManagementResult Remove(ContextMenuEntry entry)
    {
        if (entry is null) return ManagementResult.Fail("Entrada nao informada.");

        try
        {
            var hive = entry.IsMachineWide ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
            var view = string.Equals(entry.RegistryView, "Registry32", StringComparison.OrdinalIgnoreCase)
                ? RegistryView.Registry32
                : RegistryView.Registry64;

            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var classes = baseKey.OpenSubKey(@"Software\Classes");
            if (classes is null) return ManagementResult.Fail("A chave de classes nao pode ser aberta.");

            var location = entry.Location;
            using var handlers = classes.OpenSubKey(location, writable: true);
            if (handlers is null)
                return ManagementResult.Fail($"A entrada '{entry.DisplayName}' nao existe mais.");

            handlers.DeleteSubKeyTree(entry.DisplayName, throwOnMissingSubKey: false);
            return ManagementResult.Ok($"A entrada de menu de contexto '{entry.DisplayName}' foi removida.");
        }
        catch (System.Security.SecurityException ex)
        {
            return ManagementResult.Fail($"Nao foi possivel remover a entrada: {ex.Message}",
                RemovalOutcome.ErrorCodeOf(ex));
        }
        catch (UnauthorizedAccessException ex)
        {
            return ManagementResult.Fail($"Nao foi possivel remover a entrada: {ex.Message}",
                RemovalOutcome.ErrorCodeOf(ex));
        }
        catch (IOException ex)
        {
            return ManagementResult.Fail($"Nao foi possivel remover a entrada: {ex.Message}",
                RemovalOutcome.ErrorCodeOf(ex));
        }
    }

    /// <summary>
    /// O caminho do servidor que implementa o CLSID.
    ///
    /// O valor padrao de <c>InprocServer32</c> e o caminho da DLL; em alguns
    /// CLSID o valor vem no formato <c>C:\pasta\app.dll</c> com parametros
    /// depois, e o prefixo existente no disco e o que interessa.
    /// </summary>
    private static string ServerPathOf(RegistryKey classes, string clsid)
    {
        try
        {
            using var clsidKey = classes.OpenSubKey($@"CLSID\{clsid}");
            if (clsidKey is null) return string.Empty;

            using var server = clsidKey.OpenSubKey("InprocServer32");
            if (server is null) return string.Empty;

            var raw = server.GetValue(null) as string ?? string.Empty;
            return ExecutablePathResolver.Extract(raw);
        }
        catch (System.Security.SecurityException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static string RootPathOf(RegistryHive hive, RegistryView view) => hive switch
    {
        RegistryHive.CurrentUser => @"HKEY_CURRENT_USER",
        RegistryHive.LocalMachine => @"HKEY_LOCAL_MACHINE",
        _ => @"HKEY_CURRENT_USER",
    };
}
