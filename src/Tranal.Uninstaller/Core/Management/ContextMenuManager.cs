using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Tools;

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

    /// <summary>
    /// O mesmo local, relativo a <c>Software\Classes</c> — e por ele que a
    /// chave e aberta para remover a entrada.
    /// </summary>
    public string RelativeLocation { get; init; } = string.Empty;

    /// <summary>Identificador da classe COM (o <c>{...}</c>).</summary>
    public string Clsid { get; init; } = string.Empty;

    /// <summary>Nome exibido da entrada, quando o CLSID declara um.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// O nome da chave do registro — e por ele que a entrada e apagada.
    /// Nem sempre e igual ao nome exibido: as duas convencoes do Explorer
    /// convivem (ver <see cref="ContextMenuManager"/>).
    /// </summary>
    public string HandlerName { get; init; } = string.Empty;

    /// <summary>Caminho da DLL (ou do executavel) que implementa a entrada.</summary>
    public string ServerPath { get; init; } = string.Empty;

    /// <summary>Chave de registro que registra a entrada.</summary>
    public string HandlerKeyPath { get; init; } = string.Empty;

    /// <summary>Vista do registro em que a entrada foi encontrada.</summary>
    public string RegistryView { get; init; } = string.Empty;

    /// <summary>True quando a entrada pertence a todos os usuarios.</summary>
    public bool IsMachineWide { get; init; }

    /// <summary>
    /// True quando a entrada e do proprio Windows (a DLL esta na pasta do
    /// Windows, ou nao ha DLL conhecida).
    ///
    /// Entrada do Windows nao e sobra de programa nenhum: remove-la quebra o
    /// Explorer. Por isso o gerenciador nao oferece a remocao delas.
    /// </summary>
    public bool IsSystem { get; init; }
}

/// <summary>
/// Um manipulador de menu de contexto encontrado no registro, antes de
/// virar uma <see cref="ContextMenuEntry"/>.
///
/// E o que separa a LEITURA (que depende do registro) da REGRA (que nao):
/// a suite monta esta lista a mao e confere a regra sem tocar no registro.
/// </summary>
/// <param name="Location">Onde o manipulador foi encontrado (relativo a Software\Classes).</param>
/// <param name="Name">O nome da chave do manipulador (e o que se apaga).</param>
/// <param name="DisplayName">O nome que aparece no menu.</param>
/// <param name="Clsid">A classe COM que implementa o manipulador.</param>
/// <param name="IsMachineWide">True quando vale para todos os usuarios.</param>
/// <param name="View">A vista do registro em que foi encontrado.</param>
public sealed record ShellHandlerRef(
    string Location,
    string Name,
    string DisplayName,
    string Clsid,
    bool IsMachineWide,
    RegistryView View);

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
    ///
    /// Os caminhos sao **relativos** a <c>Software\Classes</c>: a chave ja
    /// aberta e essa, e um caminho que repetisse o prefixo procuraria a
    /// chave duas vezes — e nao encontraria nada.
    /// </summary>
    private static readonly string[] HandlerLocations =
    {
        @"*\shellex\ContextMenuHandlers",
        @"AllFilesystemObjects\shellex\ContextMenuHandlers",
        @"Directory\shellex\ContextMenuHandlers",
        @"Directory\Background\shellex\ContextMenuHandlers",
        @"Folder\shellex\ContextMenuHandlers",
        @"Drive\shellex\ContextMenuHandlers",
        @"Folder\shellex\ContextMenuHandlers\Directory\Background",
    };

    /// <summary>
    /// O prefixo das chaves de classe, dentro do hive.
    /// </summary>
    public const string ClassesRelative = @"Software\Classes";

    /// <summary>Todas as entradas de menu de contexto de terceiros.</summary>
    public static IReadOnlyList<ContextMenuEntry> List()
    {
        var handlers = new List<ShellHandlerRef>();

        // A mesma chave aparece nas duas vistas do registro (64 e 32 bits).
        // A entrada e uma so: o que muda de vista nao muda de entrada.
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                    using var classes = baseKey.OpenSubKey(ClassesRelative);
                    if (classes is null) continue;

                    foreach (var location in HandlerLocations)
                    {
                        using var subkey = classes.OpenSubKey(location);
                        if (subkey is null) continue;

                        foreach (var name in subkey.GetSubKeyNames())
                        {
                            using var handler = subkey.OpenSubKey(name);
                            if (handler is null) continue;

                            var valor = handler.GetValue(null) as string ?? string.Empty;

                            // O Explorer usa duas convencoes e elas convivem:
                            // as vezes o NOME da chave e o CLSID e o valor padrao
                            // e o nome bonitinho ("Taskband Pin"); as vezes e o
                            // contrario ("Open With" -> o valor padrao e o
                            // CLSID). O CLSID e o dos dois que parece um CLSID.
                            var clsid = valor;
                            var exibido = name;

                            if (!PareceClsid(valor) && PareceClsid(name))
                            {
                                clsid = name;
                                exibido = valor.Length > 0 ? valor : name;
                            }
                            else if (PareceClsid(valor))
                            {
                                exibido = name;
                            }
                            else if (valor.Length > 0)
                            {
                                exibido = valor;
                            }

                            if (clsid.Length == 0) continue;

                            var marca = $"{(isMachine ? "LM" : "CU")}|{location}|{name}";
                            if (!vistos.Add(marca)) continue;

                            handlers.Add(new ShellHandlerRef(location, name, exibido, clsid, isMachine, view));
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

        return Build(handlers, ServerPathOf);
    }

    /// <summary>
    /// As entradas de menu de contexto cujo servidor esta dentro do caminho.
    ///
    /// E o filtro que amarra a entrada ao programa instalado.
    /// </summary>
    /// <param name="installPath">A pasta de instalacao do programa.</param>
    public static IReadOnlyList<ContextMenuEntry> ListFor(string installPath)
    {
        var root = PathUtil.Normalize(installPath);
        if (string.IsNullOrEmpty(root)) return Array.Empty<ContextMenuEntry>();

        return List()
            .Where(e => ServiceManager.BelongsTo(e.ServerPath, root))
            .ToList();
    }

    /// <summary>
    /// Monta as entradas a partir dos manipuladores ja lidos.
    ///
    /// A leitura vem do registro; a montagem nao. Separar as duas coisas e o
    /// que permite conferir a regra com uma lista montada a mao, sem
    /// precisar escrever no registro de quem roda o teste.
    /// </summary>
    /// <param name="handlers">Os manipuladores lidos.</param>
    /// <param name="serverPathOf">Quem descobre o caminho da DLL de um manipulador.</param>
    public static IReadOnlyList<ContextMenuEntry> Build(
        IEnumerable<ShellHandlerRef> handlers,
        Func<ShellHandlerRef, string> serverPathOf)
    {
        var entries = new List<ContextMenuEntry>();

        if (handlers is null) return entries;

        foreach (var handler in handlers)
        {
            if (handler is null) continue;
            if (string.IsNullOrWhiteSpace(handler.Clsid)) continue;

            var serverPath = serverPathOf is null ? string.Empty : serverPathOf(handler);
            var completo = ClassesRelative + @"\" + handler.Location;

            entries.Add(new ContextMenuEntry
            {
                Location = completo,
                RelativeLocation = handler.Location,
                Clsid = handler.Clsid,
                DisplayName = handler.DisplayName,
                HandlerName = handler.Name,
                ServerPath = serverPath,
                HandlerKeyPath = $@"{RootPathOf(handler.IsMachineWide)}\{completo}\{handler.Name}",
                RegistryView = handler.View.ToString(),
                IsMachineWide = handler.IsMachineWide,
                IsSystem = IsWindowsEntry(serverPath),
            });
        }

        return entries;
    }

    /// <summary>
    /// O texto parece um CLSID (no formato <c>{...}</c>)?
    /// </summary>
    /// <param name="value">O texto a conferir.</param>
    public static bool PareceClsid(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var texto = value.Trim();
        return texto.Length > 2 && texto[0] == '{' && texto[^1] == '}';
    }

    /// <summary>
    /// A entrada e do proprio Windows?
    ///
    /// Duas situacoes contam como do Windows: a DLL que implementa a entrada
    /// esta na pasta do Windows, e a entrada nao tem DLL conhecida (as
    /// classes internas do shell, como o "Fixar na barra de tarefas", nao
    /// declaram caminho). Nenhuma das duas e sobra de programa: remover a
    /// entrada quebra o Explorer.
    /// </summary>
    /// <param name="serverPath">O caminho da DLL que implementa a entrada.</param>
    public static bool IsWindowsEntry(string serverPath)
    {
        if (string.IsNullOrWhiteSpace(serverPath)) return true;

        try
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (string.IsNullOrWhiteSpace(windows)) return false;

            return serverPath.StartsWith(windows, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // Sem a pasta do Windows: nao da para afirmar que e do Windows.
            return false;
        }
    }

    /// <summary>
    /// Remove a entrada de menu de contexto.
    ///
    /// A chave e guardada em um arquivo .reg antes de sumir: a entrada do
    /// menu de contexto de um programa e o jeito de o usuario chegar nele,
    /// e remove-la sem backup e perder o caminho de volta.
    /// </summary>
    /// <param name="entry">A entrada a remover.</param>
    /// <param name="backup">False quando quem chama ja guardou o backup.</param>
    public static ManagementResult Remove(ContextMenuEntry entry, bool backup = true)
    {
        if (entry is null) return ManagementResult.Fail("Entrada nao informada.");

        if (entry.IsSystem)
        {
            return ManagementResult.Fail(
                $"A entrada '{entry.DisplayName}' e do Windows e nao pode ser removida daqui.");
        }

        try
        {
            var hive = entry.IsMachineWide ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
            var view = string.Equals(entry.RegistryView, "Registry32", StringComparison.OrdinalIgnoreCase)
                ? RegistryView.Registry32
                : RegistryView.Registry64;

            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var classes = baseKey.OpenSubKey(ClassesRelative);
            if (classes is null) return ManagementResult.Fail("A chave de classes nao pode ser aberta.");

            // O caminho relativo e o que vale dentro de Software\Classes:
            // abrir pelo caminho completo procuraria a chave duas vezes.
            var location = entry.RelativeLocation.Length > 0
                ? entry.RelativeLocation
                : entry.Location;

            using var handlers = classes.OpenSubKey(location, writable: true);
            if (handlers is null)
                return ManagementResult.Fail($"A entrada '{entry.DisplayName}' nao existe mais.");

            if (backup && entry.HandlerKeyPath.Length > 0)
            {
                try { RegistryBackup.Backup(new[] { entry.HandlerKeyPath }); }
                catch (Exception ex) { ToolLog.Error($"Falha ao guardar a entrada antes de remove-la: {ex.Message}"); }
            }

            // O nome da chave e o que se apaga: pode ser diferente do nome
            // exibido (as duas convencoes do Explorer).
            var chave = entry.HandlerName.Length > 0 ? entry.HandlerName : entry.DisplayName;

            handlers.DeleteSubKeyTree(chave, throwOnMissingSubKey: false);
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
    ///
    /// A busca acontece no mesmo hive e vista em que o manipulador foi
    /// encontrado: e de la que vem o CLSID.
    /// </summary>
    /// <param name="handler">O manipulador lido.</param>
    private static string ServerPathOf(ShellHandlerRef handler)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(
                handler.IsMachineWide ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
                handler.View);

            using var classes = baseKey.OpenSubKey(ClassesRelative);
            if (classes is null) return string.Empty;

            using var clsidKey = classes.OpenSubKey($@"CLSID\{handler.Clsid}");
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
        catch (IOException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// O caminho completo da chave, com a raiz do registro.
    /// </summary>
    /// <param name="isMachineWide">True quando a chave vale para todos os usuarios.</param>
    private static string RootPathOf(bool isMachineWide) => isMachineWide
        ? @"HKEY_LOCAL_MACHINE"
        : @"HKEY_CURRENT_USER";
}
