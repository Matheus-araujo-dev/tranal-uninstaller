using System;
using System.Collections.Generic;
using System.IO;
using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Por que um programa foi entendido como atualizacao de outro aplicativo.
/// </summary>
public enum UpdateMarkerReason
{
    /// <summary>Nenhum sinal de atualizacao.</summary>
    None,

    /// <summary>O nome do programa diz que e uma atualizacao ("Atualizacao do X").</summary>
    NameSaysUpdate,

    /// <summary>A pasta de instalacao e a de atualizacao do programa.</summary>
    FolderSaysUpdate,

    /// <summary>O comando de desinstalacao e o de um atualizador.</summary>
    CommandSaysUpdate,

    /// <summary>O editor do programa e o mesmo de outro programa da lista.</summary>
    PublisherSaysUpdate,
}

/// <summary>
/// O que o Tranal entendeu sobre um programa que parece uma atualizacao.
/// </summary>
/// <param name="IsUpdate">True quando o programa e uma atualizacao.</param>
/// <param name="BaseAppName">O nome do aplicativo atualizado, quando da para saber.</param>
/// <param name="Reason">O sinal que marcou a atualizacao.</param>
/// <param name="Detail">A explicacao em texto do sinal.</param>
public sealed record UpdateMarkerInfo(
    bool IsUpdate,
    string BaseAppName,
    UpdateMarkerReason Reason,
    string Detail);

/// <summary>
/// Marca um programa que e **atualizacao de outro aplicativo**.
///
/// O registro do Windows ja traz o valor <c>IsUpgrade</c>, mas ele so
/// existe para os produtos do Windows Installer. Fora do MSI, a atualizacao
/// aparece no nome ("Atualizacao do Chrome"), na pasta ("...\App\Updates") ou
/// no comando ("...\App\update.exe") — e e ai que este detector age.
///
/// A regra mora aqui, em um lugar so: a lista e o relatorio consomem o
/// resultado e nunca montam a marcacao por conta propria.
/// </summary>
public static class UpdateMarker
{
    /// <summary>
    /// Palavras que, no nome, indicam uma atualizacao.
    /// </summary>
    private static readonly string[] UpdateWords =
    {
        "update", "updater", "hotfix", "patch", "service pack", "security update",
        "atualizacao", "atualização", "atualizador", "correcao", "correção",
    };

    /// <summary>
    /// Detecta se o programa e uma atualizacao de outro aplicativo.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    public static UpdateMarkerInfo Detect(InstalledProgram? program)
    {
        if (program is null)
        {
            return new UpdateMarkerInfo(false, string.Empty, UpdateMarkerReason.None,
                "Programa não informado.");
        }

        // O proprio registro ja declara a atualizacao: e o sinal mais forte
        // que existe, e nao precisa de interpretacao.
        if (program.IsUpgrade)
        {
            return new UpdateMarkerInfo(true, BaseNameOf(program.DisplayName),
                UpdateMarkerReason.NameSaysUpdate,
                "O registro do Windows marca este programa como atualização.");
        }

        var name = program.DisplayName ?? string.Empty;
        var baseName = BaseNameFromName(name);

        if (baseName.Length > 0)
        {
            return new UpdateMarkerInfo(true, baseName, UpdateMarkerReason.NameSaysUpdate,
                $"O nome do programa indica que ele é uma atualização de \"{baseName}\".");
        }

        var folder = PathUtil.Normalize(program.InstallLocation);
        if (folder.Length > 0)
        {
            foreach (var segment in folder.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!IsUpdateWord(segment)) continue;

                return new UpdateMarkerInfo(true, string.Empty, UpdateMarkerReason.FolderSaysUpdate,
                    $"A pasta de instalação está em \"{segment}\", que costuma guardar atualizações.");
            }
        }

        var command = program.UninstallString ?? string.Empty;
        if (command.Length > 0)
        {
            var exe = Path.GetFileNameWithoutExtension(command.Trim().Trim('"'));
            var commandName = exe.Length > 0 ? exe : command;

            if (IsUpdateWord(commandName) || command.Contains("/update", StringComparison.OrdinalIgnoreCase)
                || command.Contains(" --update", StringComparison.OrdinalIgnoreCase)
                || command.Contains(" -update", StringComparison.OrdinalIgnoreCase))
            {
                return new UpdateMarkerInfo(true, string.Empty, UpdateMarkerReason.CommandSaysUpdate,
                    "O comando de desinstalação é o de um atualizador.");
            }
        }

        return new UpdateMarkerInfo(false, string.Empty, UpdateMarkerReason.None,
            "Nenhum sinal de atualização.");
    }

    /// <summary>
    /// True quando o programa e uma atualizacao de outro aplicativo da
    /// propria lista.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    /// <param name="allPrograms">Todos os programas instalados.</param>
    public static bool IsUpdateOf(InstalledProgram? program, IEnumerable<InstalledProgram>? allPrograms)
    {
        var info = Detect(program);
        if (!info.IsUpdate) return false;
        if (allPrograms is null) return false;
        if (program is null) return false;

        foreach (var other in allPrograms)
        {
            if (other is null) continue;
            if (ReferenceEquals(other, program)) continue;

            if (info.BaseAppName.Length > 0
                && other.DisplayName.Contains(info.BaseAppName, StringComparison.OrdinalIgnoreCase))
                return true;

            // Sem nome derivavel: a mesma pasta de instalacao e o mesmo
            // editor indicam que os dois sao do mesmo aplicativo.
            if (info.BaseAppName.Length > 0) continue;

            var myFolder = PathUtil.Normalize(program.InstallLocation);
            var theirFolder = PathUtil.Normalize(other.InstallLocation);

            if (myFolder.Length > 0
                && string.Equals(myFolder, theirFolder, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(program.Publisher)
                && string.Equals(program.Publisher, other.Publisher, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// O nome do aplicativo atualizado, extraido do nome do programa.
    ///
    /// "Atualizacao do Chrome 120" -> "Chrome"; "Chrome Update" -> "Chrome";
    /// "Patch do Java 8" -> "Java".
    /// </summary>
    /// <param name="displayName">O nome exibido do programa.</param>
    public static string BaseNameFromName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return string.Empty;

        var name = displayName.Trim();

        // "Atualização do X" / "Update for X" / "Patch do X": o aplicativo vem
        // depois da palavra.
        foreach (var word in UpdateWords)
        {
            var index = name.IndexOf(word, StringComparison.OrdinalIgnoreCase);
            if (index < 0) continue;

            var tail = name[(index + word.Length)..].Trim();
            tail = tail.TrimStart('-', '–', '—', ':', ' ');
            tail = RemoveLeadingArticle(tail);

            if (tail.Length >= 3) return StripVersion(tail);
        }

        // "X Update" / "X Updater" / "X Hotfix": o aplicativo vem antes.
        for (var position = name.Length - 1; position >= 0; position--)
        {
            if (position > 0 && name[position - 1] != ' ') continue;

            var head = name[..position].Trim();
            if (head.Length < 3) continue;

            var word = name[position..].Trim();
            if (!IsUpdateWord(word)) continue;

            return StripVersion(head);
        }

        return string.Empty;
    }

    /// <summary>True quando o texto e uma palavra de atualizacao.</summary>
    private static bool IsUpdateWord(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        foreach (var word in UpdateWords)
        {
            if (text.Contains(word, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>Remove o artigo no comeco do nome ("o Chrome" -> "Chrome").</summary>
    private static string RemoveLeadingArticle(string text)
    {
        foreach (var article in new[] { "do ", "da ", "de ", "for ", "of ", "the " })
        {
            if (text.StartsWith(article, StringComparison.OrdinalIgnoreCase))
                return text[article.Length..].Trim();
        }

        return text;
    }

    /// <summary>Remove a versao no fim do nome ("Chrome 120" -> "Chrome").</summary>
    private static string StripVersion(string text)
    {
        var cut = text.LastIndexOf(' ');
        if (cut <= 0) return text;

        var tail = text[(cut + 1)..].Trim();
        if (tail.Length == 0) return text;

        foreach (var ch in tail)
        {
            if (!char.IsDigit(ch) && ch != '.' && ch != '-' && ch != '_') return text;
        }

        return text[..cut].Trim();
    }

    /// <summary>
    /// O nome do programa sem a versao: usado quando o registro ja marcou a
    /// atualizacao e nenhum nome derivado e necessario.
    /// </summary>
    /// <param name="displayName">O nome exibido do programa.</param>
    private static string BaseNameOf(string? displayName) =>
        string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
}
