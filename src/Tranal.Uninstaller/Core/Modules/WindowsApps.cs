using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// Um aplicativo empacotado do Windows (MSIX / Microsoft Store).
/// </summary>
public sealed class WindowsApp
{
    /// <summary>Nome tecnico do pacote (ex.: Microsoft.MicrosoftEdge_48.0).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Nome completo do pacote (inclui versao, arquitetura e publisher).</summary>
    public string PackageFullName { get; init; } = string.Empty;

    /// <summary>Nome exibido ao usuario.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Editor do pacote (certificado do publisher).</summary>
    public string Publisher { get; init; } = string.Empty;

    /// <summary>Versao do pacote.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Pasta de instalacao do pacote.</summary>
    public string InstallLocation { get; init; } = string.Empty;

    /// <summary>True quando o pacote e um framework (dependencia de outro app).</summary>
    public bool IsFramework { get; init; }

    /// <summary>
    /// True quando o Windows nao deixa remover o pacote.
    ///
    /// E o sinal medido de pacote pre-instalado: o proprio Windows marca
    /// o pacote como nao removivel, e e por isso que ele vem com o
    /// sistema.
    /// </summary>
    public bool NonRemovable { get; init; }
}

/// <summary>
/// Modulo "Windows Apps" do Revo: listar e remover aplicativos MSIX/Store.
/// A listagem usa o PowerShell (Get-AppxPackage), que e a unica API confiavel
/// para pacotes do usuario sem COM especifico.
/// </summary>
public static class WindowsApps
{
    /// <summary>Comando de listagem dos pacotes do usuario atual.</summary>
    private const string ListCommand =
        "Get-AppxPackage | Select-Object Name,PackageFullName,Publisher,Version,InstallLocation,IsFramework,NonRemovable | ConvertTo-Json -Compress";

    /// <summary>Timeout da listagem (segundos).</summary>
    private const int ListTimeoutSeconds = 60;

    /// <summary>Timeout da remocao (segundos).</summary>
    private const int UninstallTimeoutSeconds = 120;

    /// <summary>
    /// Lista os pacotes MSIX do usuario atual. Nunca lanca: se o PowerShell
    /// falhar, devolve uma lista vazia.
    /// </summary>
    public static IReadOnlyList<WindowsApp> List()
    {
        try
        {
            var (exitCode, output) = RunPowerShell(ListCommand, ListTimeoutSeconds);
            if (exitCode != 0 || string.IsNullOrWhiteSpace(output)) return Array.Empty<WindowsApp>();

            using var document = JsonDocument.Parse(output);
            var root = document.RootElement;

            var apps = new List<WindowsApp>();

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    var app = Read(item);
                    if (app is not null) apps.Add(app);
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                // Um unico pacote: o ConvertTo-Json devolve um objeto, nao um array.
                var app = Read(root);
                if (app is not null) apps.Add(app);
            }

            return apps
                .OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<WindowsApp>();
        }
    }

    /// <summary>
    /// Remove o pacote (Remove-AppxPackage). Devolve true quando o PowerShell
    /// termina com codigo 0.
    /// </summary>
    public static bool Uninstall(WindowsApp app)
    {
        try
        {
            if (app is null || string.IsNullOrWhiteSpace(app.PackageFullName)) return false;

            // Aspas simples duplicadas escapam o proprio caractere no PowerShell.
            var package = app.PackageFullName.Replace("'", "''");
            var script = $"Remove-AppxPackage -Package '{package}'";

            var (exitCode, _) = RunPowerShell(script, UninstallTimeoutSeconds);
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Le um pacote do JSON produzido pelo ConvertTo-Json.</summary>
    private static WindowsApp? Read(JsonElement element)
    {
        try
        {
            if (element.ValueKind != JsonValueKind.Object) return null;

            var name = GetString(element, "Name");
            var packageFullName = GetString(element, "PackageFullName");
            if (packageFullName.Length == 0 && name.Length == 0) return null;

            var display = GetString(element, "DisplayName");
            if (display.Length == 0) display = name;

            return new WindowsApp
            {
                Name = name,
                PackageFullName = packageFullName,
                DisplayName = display,
                Publisher = GetString(element, "Publisher"),
                Version = GetString(element, "Version"),
                InstallLocation = GetString(element, "InstallLocation"),
                IsFramework = GetBoolean(element, "IsFramework"),
                NonRemovable = GetBoolean(element, "NonRemovable"),
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Executa um script no PowerShell e devolve o codigo de saida e a saida
    /// padrao. Nunca lanca. Tenta o Windows PowerShell e, se ele nao existir,
    /// o PowerShell 7 (pwsh.exe).
    /// </summary>
    private static (int ExitCode, string Output) RunPowerShell(string script, int timeoutSeconds)
    {
        foreach (var shell in new[] { "powershell.exe", "pwsh.exe" })
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = shell,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-NonInteractive");
                startInfo.ArgumentList.Add("-Command");
                startInfo.ArgumentList.Add(script);

                using var process = new Process { StartInfo = startInfo };
                if (!process.Start()) continue;

                // A leitura e assincrona para nao travar quando o processo escreve
                // mais dados do que o buffer do pipe aceita.
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(timeoutSeconds * 1000))
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch { }
                    return (-1, string.Empty);
                }

                var output = stdout.GetAwaiter().GetResult();
                _ = stderr.GetAwaiter().GetResult();
                return (process.ExitCode, output);
            }
            catch
            {
                // PowerShell ausente ou indisponivel: tenta o proximo.
            }
        }

        return (-1, string.Empty);
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        try
        {
            if (element.ValueKind != JsonValueKind.Object) return false;
            if (element.TryGetProperty(name, out value)) return true;

            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            return false;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    private static string GetString(JsonElement element, string name)
    {
        try
        {
            if (!TryGetProperty(element, name, out var value)) return string.Empty;
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => string.Empty,
            };
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool GetBoolean(JsonElement element, string name)
    {
        try
        {
            if (!TryGetProperty(element, name, out var value)) return false;
            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
                JsonValueKind.Number => value.TryGetInt32(out var number) && number != 0,
                _ => false,
            };
        }
        catch
        {
            return false;
        }
    }
}
