using System.IO;
using System.Xml.Linq;
using Microsoft.Win32;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Um item de inicializacao de um aplicativo empacotado (MSIX / Store).
/// </summary>
public sealed class StoreStartupEntry
{
    /// <summary>O identificador da task, como o Windows registra.</summary>
    public string TaskId { get; init; } = string.Empty;

    /// <summary>O nome exibido da task.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>O pacote ao qual a task pertence.</summary>
    public string PackageName { get; init; } = string.Empty;

    /// <summary>O comando que a task executa.</summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>True quando a task esta habilitada para iniciar com o Windows.</summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// True quando o Windows registra a task como desabilitada.
    ///
    /// O estado fica em
    /// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder</c>:
    /// o valor 2 e "desabilitada pelo usuario" e o valor 1 e "habilitada".
    /// A task que nao aparece no registro segue o padrao do manifesto.
    /// </summary>
    public bool DisabledByUser { get; init; }
}

/// <summary>
/// Os itens de inicializacao dos aplicativos da Microsoft Store.
///
/// O Uninstall Tool lista os "Startup items" de aplicativos UWP: são as
/// <c>StartupTask</c> declaradas no manifesto do pacote MSIX. No Tranal
/// esses itens NAO apareciam em lugar nenhum — o Boot Center so via autorun
/// classico (Run, RunOnce, pasta de inicializacao) e servicos.
///
/// A REGRA mora aqui, em um lugar so: o manifesto do pacote e lido aqui, o
/// registro de aprovacao e consultado aqui, e quem mostra consome o
/// veredito.
/// </summary>
public static class StoreStartupEntries
{
    /// <summary>
    /// A chave em que o Windows registra quais tasks o usuario desabilitou.
    /// </summary>
    public const string ApprovedKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    /// <summary>
    /// As tasks de inicializacao de todos os pacotes da Store.
    /// </summary>
    public static IReadOnlyList<StoreStartupEntry> List()
    {
        var entradas = new List<StoreStartupEntry>();

        foreach (var pacote in WindowsApps.List())
        {
            foreach (var task in TasksOf(pacote))
            {
                var desabilitada = IsDisabledByUser(task.TaskId);

                entradas.Add(new StoreStartupEntry
                {
                    TaskId = task.TaskId,
                    DisplayName = task.DisplayName.Length > 0 ? task.DisplayName : task.TaskId,
                    PackageName = pacote.DisplayName,
                    Command = task.Command,
                    Enabled = !desabilitada,
                    DisabledByUser = desabilitada,
                });
            }
        }

        return entradas
            .OrderBy(e => e.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// True quando o Windows registra a task como desabilitada pelo usuario.
    /// </summary>
    /// <param name="taskId">O identificador da task.</param>
    public static bool IsDisabledByUser(string? taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId)) return false;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            using var key = baseKey.OpenSubKey(ApprovedKeyPath);
            if (key is null) return false;

            var valor = key.GetValue(taskId);
            if (valor is not int numero) return false;

            return numero == 2;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler o estado da task '{taskId}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// As tasks declaradas no manifesto do pacote.
    /// </summary>
    private static IReadOnlyList<(string TaskId, string DisplayName, string Command)> TasksOf(WindowsApp pacote)
    {
        var tasks = new List<(string, string, string)>();

        if (string.IsNullOrWhiteSpace(pacote.InstallLocation)) return tasks;

        try
        {
            var manifesto = Path.Combine(pacote.InstallLocation, "AppxManifest.xml");
            if (!File.Exists(manifesto)) return tasks;

            var documento = XDocument.Load(manifesto);

            foreach (var elemento in documento.Descendants())
            {
                // O prefixo do XML muda entre as versoes do manifesto; o que
                // identifica a task e o NOME LOCAL do elemento.
                if (!string.Equals(elemento.Name.LocalName, "StartupTask", StringComparison.OrdinalIgnoreCase))
                    continue;

                var taskId = (string?)elemento.Attribute("TaskId") ?? string.Empty;
                if (taskId.Length == 0) continue;

                var display = (string?)elemento.Attribute("DisplayName") ?? string.Empty;
                var comando = (string?)elemento.Attribute("Executable") ?? string.Empty;

                tasks.Add((taskId, display, comando));
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler o manifesto de '{pacote.PackageFullName}': {ex.Message}");
        }

        return tasks;
    }
}
