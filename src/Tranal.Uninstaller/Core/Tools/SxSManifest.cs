using System.IO;
using System.Text;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// O manifesto lado a lado (SxS) do Tranal.
///
/// O Windows permite que um programa declare quais DLLs de sistema ele usa e
/// em que versao. E o manifesto: um XML que o Windows le antes de carregar o
/// programa.
///
/// As DLLs declaradas aqui sao as que o Windows costuma entregar por uma
/// versao diferente da que o programa espera: <c>comctl32</c> (os controles
/// comuns), <c>mpr</c> (as chamadas de rede), <c>netapi32</c> e
/// <c>netutils</c> (a API de rede), <c>textshaping</c> (o desenho de texto) e
/// <c>version</c> (a informacao de versao do arquivo).
///
/// A regra esta em um lugar so: este modulo sabe o que o manifesto declara e
/// o escreve. O arquivo que o Windows le sai daqui.
/// </summary>
public static class SxSManifest
{
    /// <summary>
    /// A identidade do programa no manifesto.
    /// </summary>
    public const string AssemblyName = "Tranal.Uninstaller.Pro";

    /// <summary>
    /// As DLLs de sistema que o programa declara.
    ///
    /// A lista e **fixa** e vem do proprio Windows: sao as DLLs que o sistema
    /// entrega em varias versoes, e por isso precisam ser declaradas.
    /// </summary>
    public static IReadOnlyList<string> SystemDlls { get; } = new[]
    {
        "comctl32",
        "mpr",
        "netapi32",
        "netutils",
        "textshaping",
        "version",
    };

    /// <summary>
    /// Gera o manifesto.
    /// </summary>
    /// <param name="version">A versao do programa.</param>
    public static string Build(string version = "1.4.0.0")
    {
        var sb = new StringBuilder();

        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<assembly xmlns=\"urn:schemas-microsoft-com:asm.v1\" manifestVersion=\"1.0\">");
        sb.AppendLine($"  <assemblyIdentity type=\"win32\" name=\"{AssemblyName}\" version=\"{version}\" />");
        sb.AppendLine();
        sb.AppendLine("  <trustInfo xmlns=\"urn:schemas-microsoft-com:asm.v3\">");
        sb.AppendLine("    <security>");
        sb.AppendLine("      <requestedPrivileges>");
        sb.AppendLine("        <requestedExecutionLevel level=\"requireAdministrator\" uiAccess=\"false\" />");
        sb.AppendLine("      </requestedPrivileges>");
        sb.AppendLine("    </security>");
        sb.AppendLine("  </trustInfo>");
        sb.AppendLine();
        sb.AppendLine("  <dependency>");
        sb.AppendLine("    <dependentAssembly>");
        sb.AppendLine("      <assemblyIdentity type=\"win32\" name=\"Microsoft.Windows.Common-Controls\"");
        sb.AppendLine("                        version=\"6.0.0.0\" processorArchitecture=\"*\"");
        sb.AppendLine("                        publicKeyToken=\"6595b64144ccf1df\" language=\"*\" />");
        sb.AppendLine("    </dependentAssembly>");
        sb.AppendLine("  </dependency>");
        sb.AppendLine();

        foreach (var dll in SystemDlls)
        {
            sb.AppendLine($"  <!-- A DLL de sistema {dll}: a versao que o Windows entrega e declarada aqui. -->");
            sb.AppendLine("  <file name=\"" + dll + ".dll\" />");
        }

        sb.AppendLine("</assembly>");

        return sb.ToString();
    }

    /// <summary>
    /// Grava o manifesto no caminho informado.
    /// </summary>
    /// <param name="destinationPath">O arquivo a ser gravado.</param>
    /// <param name="version">A versao do programa.</param>
    /// <returns>O caminho gravado, ou uma string vazia.</returns>
    public static string Write(string? destinationPath, string version = "1.4.0.0")
    {
        if (string.IsNullOrWhiteSpace(destinationPath)) return string.Empty;

        try
        {
            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(destinationPath, Build(version));
            return destinationPath;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// O manifesto e valido como XML?
    ///
    /// E o que a verificacao do instalador faz antes de embaralhar o setup:
    /// um manifesto quebrado faz o Windows recusar o programa.
    /// </summary>
    /// <param name="manifest">O texto do manifesto.</param>
    public static bool IsValidXml(string? manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest)) return false;

        try
        {
            var documento = System.Xml.Linq.XDocument.Parse(manifest);
            return documento.Root is not null;
        }
        catch
        {
            return false;
        }
    }
}
