using System.IO;
using System.Text;

namespace Tranal.Uninstaller.Core.Reporting;

/// <summary>
/// O gerador do "Simple Delete": um script que apaga um programa que nao se
/// registrou no Windows.
///
/// Quando um programa nao tem entrada de desinstalacao, nao existe "desinstalar"
/// dele — existe so a pasta dele e as sobras. O BCU resolve isso gerando um
/// script que o usuario pode rodar.
///
/// O script e um arquivo .cmd: ele apaga a pasta de instalacao e as chaves de
/// registro que o Tranal identificou. Nada e apagado pela GUI — o script e
/// gerado, e quem executa e o usuario.
///
/// A regra esta em um lugar so: este modulo sabe o que entra no script e
/// como ele e escrito.
/// </summary>
public static class SimpleDeleteWriter
{
    /// <summary>
    /// O nome do arquivo gerado.
    /// </summary>
    public const string FileName = "simple-delete.cmd";

    /// <summary>
    /// Gera o script de remocao simples de um programa.
    /// </summary>
    /// <param name="installPath">A pasta de instalacao do programa.</param>
    /// <param name="registryPaths">As chaves de registro do programa.</param>
    /// <param name="programName">O nome do programa (so para o titulo do script).</param>
    /// <returns>O texto do script.</returns>
    public static string Build(
        string? installPath,
        IEnumerable<string>? registryPaths = null,
        string? programName = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine("@echo off");
        sb.AppendLine("REM Script de remocao simples gerado pelo Tranal Uninstaller.");
        sb.AppendLine("REM Ele apaga a pasta do programa e as chaves de registro indicadas.");
        sb.AppendLine("REM Revise o script antes de executar.");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(programName))
        {
            sb.AppendLine("REM Programa: " + programName);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(installPath))
        {
            sb.AppendLine("if exist \"" + installPath.Trim() + "\" (");
            sb.AppendLine("  rmdir /s /q \"" + installPath.Trim() + "\"");
            sb.AppendLine("  echo Pasta removida.");
            sb.AppendLine(")");
        }

        foreach (var chave in registryPaths ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(chave)) continue;

            sb.AppendLine("reg delete \"" + chave.Trim() + "\" /f >nul 2>&1");
        }

        sb.AppendLine();
        sb.AppendLine("echo Remocao simples concluida.");
        sb.AppendLine("pause");

        return sb.ToString();
    }

    /// <summary>
    /// Gera o script e grava no caminho informado.
    /// </summary>
    /// <param name="installPath">A pasta de instalacao.</param>
    /// <param name="registryPaths">As chaves de registro.</param>
    /// <param name="programName">O nome do programa.</param>
    /// <param name="destinationPath">O arquivo a ser gravado.</param>
    /// <returns>O caminho gravado, ou uma string vazia.</returns>
    public static string Write(
        string? installPath,
        IEnumerable<string>? registryPaths,
        string? programName,
        string? destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath)) return string.Empty;

        try
        {
            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(destinationPath, Build(installPath, registryPaths, programName));
            return destinationPath;
        }
        catch
        {
            return string.Empty;
        }
    }
}
