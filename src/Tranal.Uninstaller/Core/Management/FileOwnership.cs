using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// Um item e o estado da propriedade dele.
/// </summary>
public sealed class OwnershipItem
{
    /// <summary>O caminho do item.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>O nome do item.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>True quando o caminho e uma pasta.</summary>
    public bool IsDirectory { get; init; }

    /// <summary>True quando o usuario atual consegue gravar no item.</summary>
    public bool CanWrite { get; init; }

    /// <summary>
    /// True quando o item esta numa area do sistema (Program Files, Windows,
    /// ProgramData). Assumir a propriedade nesses lugares exige
    /// administrador.
    /// </summary>
    public bool IsSystem { get; init; }

    /// <summary>
    /// True quando o item precisa de intervencao: existe, o usuario nao
    /// consegue gravar e o caminho e do sistema.
    /// </summary>
    public bool NeedsOwnership => !CanWrite && IsSystem;

    /// <summary>True quando o caminho existe.</summary>
    public bool Exists { get; init; }
}

/// <summary>
/// Assumir a propriedade de um arquivo ou de uma pasta.
///
/// E o "take ownership" do Bullseye (BCU) e a acao "assumir a propriedade"
/// do Explorer do Windows: quando um item foi instalado por outro usuario
/// ou com permissao restrita, o Windows recusa a remocao ate que a
/// propriedade volte para a conta que esta usando o Tranal.
///
/// A REGRA mora aqui, em um lugar so: quem mostra o item e quem age
/// consomem este modulo. O que age sao as ferramentas do proprio Windows
/// (<c>takeown</c> e <c>icacls</c>), que e o que o Explorer usa — nao uma
/// reimplementacao da ACL.
///
/// A operacao precisa de administrador. O modulo **diz** isso, em vez de
/// fingir que deu certo.
/// </summary>
public static class FileOwnership
{
    /// <summary>
    /// Le o estado da propriedade dos caminhos informados.
    ///
    /// "Pode gravar" e MEDIDO: o modulo tenta gravar e ve se o Windows
    /// deixa. Nao e deduzido do caminho — e o que o sistema responde que
    /// conta.
    /// </summary>
    /// <param name="paths">Os caminhos a olhar.</param>
    public static IReadOnlyList<OwnershipItem> Scan(IEnumerable<string>? paths)
    {
        var itens = new List<OwnershipItem>();

        foreach (var caminho in paths ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(caminho)) continue;

            var existe = File.Exists(caminho) || Directory.Exists(caminho);
            var pasta = Directory.Exists(caminho);

            itens.Add(new OwnershipItem
            {
                Path = caminho,
                Name = Path.GetFileName(caminho.TrimEnd('\\', '/')),
                IsDirectory = pasta,
                Exists = existe,
                CanWrite = existe && CanWrite(caminho, pasta),
                IsSystem = IsSystemPath(caminho),
            });
        }

        return itens;
    }

    /// <summary>
    /// Assume a propriedade do caminho.
    ///
    /// Sao os dois comandos que o proprio Windows usa: o <c>takeown</c>
    /// passa a propriedade para a conta, e o <c>icacls</c> grava o dono.
    /// </summary>
    /// <param name="path">O caminho.</param>
    /// <returns>True quando a propriedade foi assumida.</returns>
    public static (bool Ok, string Message) TakeOwnership(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return (false, "o caminho nao foi informado.");
        if (!File.Exists(path) && !Directory.Exists(path))
            return (false, $"o caminho '{path}' nao existe.");

        var (code, saida) = Run("takeown.exe", "/f \"" + path + "\" /r /d y");
        if (code != 0)
            return (false, saida.Length > 0 ? saida : "o takeown nao conseguiu assumir a propriedade.");

        var usuario = Environment.UserName;
        var (code2, saida2) = Run("icacls.exe", "\"" + path + "\" /setowner \"" + usuario + "\" /t /c");

        return code2 == 0
            ? (true, $"a propriedade de '{path}' agora e do usuario atual.")
            : (false, saida2.Length > 0 ? saida2 : "a propriedade foi assumida, mas o dono nao foi gravado.");
    }

    /// <summary>
    /// Da controle total ao usuario atual sobre o caminho.
    /// </summary>
    /// <param name="path">O caminho.</param>
    /// <returns>True quando a permissao foi dada.</returns>
    public static (bool Ok, string Message) GrantFullControl(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return (false, "o caminho nao foi informado.");
        if (!File.Exists(path) && !Directory.Exists(path))
            return (false, $"o caminho '{path}' nao existe.");

        var (code, saida) = Run("icacls.exe", "\"" + path + "\" /grant \"" + Environment.UserName + "\":(OI)(CI)F /t /c");

        return code == 0
            ? (true, $"o usuario atual agora tem controle total sobre '{path}'.")
            : (false, saida.Length > 0 ? saida : "nao foi possivel dar o controle total.");
    }

    /// <summary>
    /// True quando o caminho esta numa area do sistema.
    /// </summary>
    /// <param name="path">O caminho.</param>
    public static bool IsSystemPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        string normalizado;
        try
        {
            normalizado = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return false;
        }

        foreach (var pasta in SystemFolders())
        {
            if (string.IsNullOrWhiteSpace(pasta)) continue;
            if (normalizado.StartsWith(pasta, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>
    /// As pastas do sistema que exigem administrador para mexer.
    /// </summary>
    private static IEnumerable<string> SystemFolders()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    }

    /// <summary>
    /// True quando o usuario atual consegue gravar no item.
    ///
    /// A pergunta e feita ao Windows: o modulo tenta gravar e apaga o que
    /// escreveu. O que importa e a resposta do sistema, nao o caminho.
    /// </summary>
    private static bool CanWrite(string path, bool isDirectory)
    {
        try
        {
            if (!isDirectory)
            {
                if (!File.Exists(path)) return false;

                using var stream = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                return true;
            }

            if (!Directory.Exists(path)) return false;

            var teste = Path.Combine(path, ".tranal-escrita-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (var stream = File.Create(teste))
            {
                stream.WriteByte(0);
            }

            File.Delete(teste);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Roda uma ferramenta do Windows e devolve o codigo e a saida.
    /// </summary>
    private static (int Code, string Output) Run(string fileName, string arguments)
    {
        try
        {
            var info = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using var process = Process.Start(info);
            if (process is null) return (-1, $"nao foi possivel iniciar o {fileName}.");

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(60_000);

            var texto = string.IsNullOrWhiteSpace(error) ? output : error;
            return (process.ExitCode, texto.Trim());
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return (-1, ex.Message);
        }
    }
}
