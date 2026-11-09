using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Topmost;

/// <summary>
/// Uma entrada de menu de contexto criada pelo usuario.
/// </summary>
/// <param name="Id">O identificador da entrada (o nome da chave).</param>
/// <param name="DisplayName">O texto que aparece no menu.</param>
/// <param name="Command">O comando que roda quando a entrada e acionada.</param>
public sealed record CustomShellEntry(string Id, string DisplayName, string Command);

/// <summary>
/// As entradas de menu de contexto que o usuario cria no gerenciador.
///
/// A entrada do Explorer e um verbo (uma chave em
/// <c>HKCU\Software\Classes\*\shell</c>): por isso ela fica ao lado das do
/// Tranal, e nao em <c>shellex\ContextMenuHandlers</c> (que e onde ficam os
/// manipuladores de terceiros).
///
/// A regra de gravar o verbo NAO mora aqui: quem escreve no registro e o
/// <see cref="ExplorerContextMenu"/>. Este modulo so sabe o que e uma
/// entrada do usuario, onde ela mora e como lista-la.
/// </summary>
public static class CustomShellMenu
{
    /// <summary>
    /// O prefixo do identificador das entradas do usuario.
    ///
    /// E o que permite saber quais entradas sao do usuario sem guardar uma
    /// lista em outro lugar: as chaves com esse prefixo, na pasta dos verbos
    /// do Explorer, sao as que o gerenciador criou.
    /// </summary>
    public const string IdPrefix = "Tranal";

    /// <summary>
    /// As entradas fixas do Tranal que ficam na mesma pasta e nao sao do
    /// usuario.
    /// </summary>
    private static readonly HashSet<string> FixasDoTranal = new(StringComparer.OrdinalIgnoreCase)
    {
        ExplorerContextMenu.ShellKeyName,
        ExplorerShellMenu.ShredId,
    };

    /// <summary>A pasta (relativa a HKCU) dos verbos de arquivo.</summary>
    public const string ShellFolder = @"Software\Classes\*\shell";

    /// <summary>
    /// O identificador de uma entrada a partir do nome escolhido.
    ///
    /// O nome vira identificador porque o Explorer usa a chave como nome do
    /// verbo: e o que o usuario ve no botao direito.
    /// </summary>
    /// <param name="displayName">O nome escolhido pelo usuario.</param>
    public static string IdFor(string displayName)
    {
        var limpo = new string((displayName ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .ToArray());

        if (limpo.Length == 0) limpo = "Comando";

        return IdPrefix + limpo;
    }

    /// <summary>
    /// Registra a entrada do usuario no Explorer.
    /// </summary>
    /// <param name="id">O identificador da entrada.</param>
    /// <param name="displayName">O texto que aparece no menu.</param>
    /// <param name="command">O comando que roda quando a entrada e acionada.</param>
    /// <param name="icon">O caminho do icone (opcional).</param>
    public static bool Create(string id, string displayName, string command, string icon = "")
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        if (string.IsNullOrWhiteSpace(displayName)) return false;
        if (string.IsNullOrWhiteSpace(command)) return false;

        return ExplorerContextMenu.WriteShellEntry(
            ShellFolder + @"\" + id,
            displayName.Trim(),
            command.Trim(),
            icon);
    }

    /// <summary>
    /// Remove a entrada do usuario.
    /// </summary>
    /// <param name="id">O identificador da entrada.</param>
    public static bool Remove(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            baseKey.DeleteSubKeyTree(ShellFolder + @"\" + id, throwOnMissingSubKey: false);
            return true;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao remover a entrada de menu de contexto '{id}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// As entradas do usuario, com o texto e o comando de cada uma.
    /// </summary>
    public static IReadOnlyList<CustomShellEntry> List()
    {
        var entradas = new List<CustomShellEntry>();

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            using var shell = baseKey.OpenSubKey(ShellFolder);
            if (shell is null) return entradas;

            foreach (var nome in shell.GetSubKeyNames())
            {
                if (!nome.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (FixasDoTranal.Contains(nome)) continue;

                var exibido = nome;
                var comando = string.Empty;

                using (var chave = shell.OpenSubKey(nome))
                {
                    if (chave is null) continue;

                    var muiverb = chave.GetValue("MUIVerb") as string;
                    if (!string.IsNullOrWhiteSpace(muiverb)) exibido = muiverb;

                    using (var command = chave.OpenSubKey("command"))
                    {
                        if (command is not null)
                            comando = command.GetValue(null) as string ?? string.Empty;
                    }
                }

                entradas.Add(new CustomShellEntry(nome, exibido, comando));
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao listar as entradas de menu de contexto do usuario: {ex.Message}");
        }

        return entradas;
    }
}
