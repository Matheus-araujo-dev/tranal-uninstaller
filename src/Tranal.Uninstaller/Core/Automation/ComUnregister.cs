using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// Desregistro de componentes COM antes de remover os arquivos do programa.
///
/// Um componente COM e uma DLL registrada no registro: o Windows so carrega
/// a DLL depois que o <c>regsvr32</c> registra as classes dela. Apagar a
/// DLL sem desregistrar deixa o registro apontando para um arquivo que nao
/// existe — e e essa a sobra que o <see cref="Core.Tools.RegistryCleaner"/>
/// depois encontra e limpa.
///
/// O Geek Uninstaller mede esse cuidado: a remocao dele desregistra os
/// componentes COM do programa antes de apagar a pasta.
///
/// A REGRA do que precisa ser desregistrado mora em um lugar so:
/// <see cref="RegisteredDlls"/> — uma DLL so entra na lista quando o registro
/// aponta para ela. O <see cref="Unregister"/> apenas executa.
/// </summary>
public static class ComUnregister
{
    /// <summary>O executavel que registra e desregistra componentes COM.</summary>
    public const string RegSvrCommand = "regsvr32.exe";

    /// <summary>
    /// Desregistra uma DLL: <c>regsvr32 /u</c>.
    ///
    /// O codigo de saida 0 significa que o desregistro rodou. O codigo 1
    /// significa que o Windows recusou (falta de permissao ou DLL sem
    /// registro). O codigo 2 significa que o Windows nao conseguiu abrir a
    /// DLL (arquivo ausente).
    ///
    /// </summary>
    /// <param name="dllPath">O caminho da DLL.</param>
    public static ManagementResult Unregister(string? dllPath)
    {
        var caminho = (dllPath ?? string.Empty).Trim().Trim('"');
        if (caminho.Length == 0)
            return ManagementResult.Fail("Informe o caminho da DLL a desregistrar.");

        if (!File.Exists(caminho))
            return ManagementResult.Fail($"A DLL '{caminho}' nao existe mais.");

        try
        {
            var startInfo = new ProcessStartInfo(RegSvrCommand)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("/u");
            startInfo.ArgumentList.Add(caminho);

            using var process = Process.Start(startInfo);
            if (process is null)
                return ManagementResult.Fail("Nao foi possivel iniciar o regsvr32.");

            process.WaitForExit();

            var exitCode = process.ExitCode;
            if (exitCode == 0)
                return ManagementResult.Ok($"Componente COM desregistrado: {caminho}");

            return ManagementResult.Fail(
                $"O regsvr32 terminou com o codigo {exitCode} ao desregistrar '{caminho}'.",
                exitCode);
        }
        catch (Exception ex)
        {
            return ManagementResult.Fail($"Nao foi possivel desregistrar '{caminho}': {ex.Message}", ex.HResult & 0xFFFF);
        }
    }

    /// <summary>
    /// Desregistra varias DLLs e devolve o resultado de cada uma.
    ///
    /// A ordem importa: uma DLL que outra depende continua valendo enquanto a
    /// dependida ainda existe. Por isso a lista vem ordenada do mais dependente
    /// para o menos, e o erro de uma nao interrompe as outras.
    ///
    /// </summary>
    /// <param name="dllPaths">Os caminhos das DLLs.</param>
    public static IReadOnlyList<KeyValuePair<string, ManagementResult>> UnregisterAll(
        IEnumerable<string>? dllPaths)
    {
        var resultados = new List<KeyValuePair<string, ManagementResult>>();
        if (dllPaths is null) return resultados;

        foreach (var dll in dllPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var resultado = Unregister(dll);
            resultados.Add(new KeyValuePair<string, ManagementResult>(dll, resultado));
        }

        return resultados;
    }

    /// <summary>
    /// As DLLs de uma pasta do programa que tem registro de componente COM.
    ///
    /// A lista vem do REGISTRO, e nao da pasta: o que decide se a DLL precisa
    /// ser desregistrada e o registro apontar para ela. Uma DLL qualquer da
    /// pasta, sem registro, nao precisa de <c>regsvr32 /u</c> — e rodar o
    /// comando nela seria so perda de tempo.
    ///
    /// </summary>
    /// <param name="folder">A pasta do programa.</param>
    public static IReadOnlyList<string> RegisteredDlls(string? folder)
    {
        var registradas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(folder)) return Array.Empty<string>();

        var prefixo = NormalizarPasta(folder);
        if (prefixo.Length == 0) return Array.Empty<string>();

        foreach (var dll in LerServidoresCom())
        {
            if (!dll.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)) continue;
            registradas.Add(dll);
        }

        return registradas.OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Todas as DLLs registradas como servidor COM (InprocServer32).
    /// </summary>
    private static IReadOnlyList<string> LerServidoresCom()
    {
        var dlls = new List<string>();

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry64);
            using var clsid = baseKey.OpenSubKey("CLSID");
            if (clsid is null) return dlls;

            foreach (var guid in clsid.GetSubKeyNames())
            {
                try
                {
                    using var classe = clsid.OpenSubKey(guid);
                    if (classe is null) continue;

                    using var inproc = classe.OpenSubKey("InprocServer32");
                    if (inproc is null) continue;

                    var caminho = inproc.GetValue(string.Empty) as string;
                    if (string.IsNullOrWhiteSpace(caminho)) continue;

                    var limpo = caminho.Trim().Trim('"');
                    if (limpo.Length == 0) continue;

                    dlls.Add(limpo);
                }
                catch
                {
                    // Classe inacessivel: segue para a proxima.
                }
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler as classes COM registradas: {ex.Message}");
        }

        return dlls;
    }

    /// <summary>
    /// Normaliza a pasta com a barra final, que e o que a comparacao de
    /// prefixo precisa.
    /// </summary>
    private static string NormalizarPasta(string folder)
    {
        try
        {
            var full = Path.GetFullPath(folder.Trim().Trim('"'));
            return full.TrimEnd('\\') + "\\";
        }
        catch
        {
            return string.Empty;
        }
    }
}
