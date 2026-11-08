using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// A tag <c>NoRemove</c>: a protecao que o proprio instalador grava na entrada
/// de desinstalacao para dizer "esta entrada nao sai".
///
/// O Windows Installer usa essa tag em produto que nao quer que a entrada de
/// desinstalacao desapareca quando o produto e reparado. O Bulk Crap
/// Uninstaller mede a mesma coisa: ele marca os desinstaladores protegidos
/// e oferece a opcao de desligar a protecao por item.
///
/// A REGRA esta em um lugar so: e esta classe que decide se a entrada esta
/// protegida, e e ela que apaga a tag quando o usuario pede. Nenhuma outra
/// parte do Tranal le o valor diretamente.
/// </summary>
public static class ProtectedUninstallTag
{
    /// <summary>O nome do valor que carrega a protecao.</summary>
    public const string TagName = "NoRemove";

    /// <summary>
    /// True quando a entrada de desinstalacao do programa esta protegida
    /// pela tag <c>NoRemove</c>.
    ///
    /// </summary>
    /// <param name="program">O programa instalado.</param>
    public static bool IsProtected(InstalledProgram? program)
    {
        if (program is null) return false;

        var (hive, relativo) = Resolve(program);
        if (relativo.Length == 0) return false;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, program.View);
            using var chave = baseKey.OpenSubKey(relativo);
            if (chave is null) return false;

            var valor = chave.GetValue(TagName);
            if (valor is null) return false;

            // O valor e um numero: 1 protege, 0 nao protege. Qualquer valor
            // diferente de zero conta como protecao.
            if (valor is int numero) return numero != 0;
            if (valor is string texto) return texto.Trim() is "0" ? false : true;

            return true;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler a tag '{TagName}' de '{program.DisplayName}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Os programas instalados cuja entrada de desinstalacao esta protegida.
    ///
    /// </summary>
    /// <param name="programs">Os programas instalados.</param>
    public static IReadOnlyList<InstalledProgram> ListProtected(IEnumerable<InstalledProgram>? programs)
    {
        if (programs is null) return Array.Empty<InstalledProgram>();

        return programs.Where(p => IsProtected(p)).ToList();
    }

    /// <summary>
    /// Desliga a protecao do item: apaga a tag <c>NoRemove</c> da entrada de
    /// desinstalacao.
    ///
    /// E o que o Bulk Crap Uninstaller oferece quando o desinstalador esta
    /// marcado como protegido: "desabilitar a protecao deste item".
    ///
    /// </summary>
    /// <param name="program">O programa protegido.</param>
    public static ManagementResult Clear(InstalledProgram? program)
    {
        if (program is null) return ManagementResult.Fail("Informe o programa.");

        var (hive, relativo) = Resolve(program);
        if (relativo.Length == 0)
            return ManagementResult.Fail($"O programa '{program.DisplayName}' nao tem chave de registro conhecida.");

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, program.View);
            using var chave = baseKey.OpenSubKey(relativo, writable: true);
            if (chave is null)
                return ManagementResult.Fail($"A chave de '{program.DisplayName}' nao existe mais.");

            if (chave.GetValue(TagName) is null)
                return ManagementResult.Ok($"O programa '{program.DisplayName}' nao esta protegido.");

            chave.DeleteValue(TagName, throwOnMissingValue: false);
            return ManagementResult.Ok($"A protecao de '{program.DisplayName}' foi desligada.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return ManagementResult.Fail(
                $"Sem permissao para alterar a chave de '{program.DisplayName}': {ex.Message}",
                ex.HResult & 0xFFFF);
        }
        catch (System.Security.SecurityException ex)
        {
            return ManagementResult.Fail(
                $"Sem permissao para alterar a chave de '{program.DisplayName}': {ex.Message}",
                ex.HResult & 0xFFFF);
        }
        catch (Exception ex)
        {
            return ManagementResult.Fail(
                $"Nao foi possivel desligar a protecao de '{program.DisplayName}': {ex.Message}",
                ex.HResult & 0xFFFF);
        }
    }

    /// <summary>
    /// Descobre o hive e o caminho da entrada de desinstalacao do programa.
    ///
    /// </summary>
    private static (RegistryHive Hive, string RelativePath) Resolve(InstalledProgram program)
    {
        var caminho = (program.RegistryPath ?? string.Empty).Trim();
        if (caminho.Length == 0) caminho = (program.RegistryKey ?? string.Empty).Trim();
        if (caminho.Length == 0) return (RegistryHive.LocalMachine, string.Empty);

        var parsed = RegistryPath.Parse(caminho);
        if (parsed is null) return (RegistryHive.LocalMachine, string.Empty);

        return (parsed.Value.Hive, parsed.Value.RelativePath);
    }
}
