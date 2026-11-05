using System.IO;
using System.Runtime.InteropServices;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Adia a remocao de um item para o proximo reinicio do Windows.
///
/// O Revo Uninstaller Pro informa que "os arquivos restantes da lista serao
/// excluidos durante a proxima reinicializacao do sistema" quando um item esta
/// em uso. O mesmo e feito aqui pela fila de remocao pendente do Windows
/// (<c>MoveFileEx</c> com <c>MOVEFILE_DELAY_UNTIL_REBOOT</c>), que e a
/// mechanismo oficial e sobrevive a reinicios sucessivos.
/// </summary>
public static class DeferredDelete
{
    private const int MovefileDelayUntilReboot = 0x0004;
    private const int MovefileReplaceExisting = 0x0001;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string existingFileName, string? newFileName, int flags);

    /// <summary>
    /// Agenda a remocao do arquivo ou pasta para o proximo reinicio.
    /// </summary>
    /// <returns><c>true</c> quando o item entrou na fila de remocao pendente.</returns>
    public static bool Schedule(string path)
    {
        var full = PathUtil.Normalize(path);
        if (full.Length == 0) return false;

        try
        {
            if (!File.Exists(full) && !Directory.Exists(full)) return false;
        }
        catch { return false; }

        // A propria fila de remocao pendente respeita as regras de protecao:
        // um item em uso dentro de pasta de sistema nao pode ser agendado
        // para sumir no proximo boot.
        if (SafetyRules.IsProtectedFolder(full)
            || SafetyRules.IsProtectedFolder(Path.GetDirectoryName(full) ?? string.Empty))
            return false;

        try
        {
            return MoveFileEx(full, null, MovefileDelayUntilReboot | MovefileReplaceExisting);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Agenda varios itens de uma vez. Devolve a quantidade agendada.
    /// </summary>
    public static int ScheduleMany(IEnumerable<string> paths)
    {
        var count = 0;
        foreach (var path in paths)
        {
            if (Schedule(path)) count++;
        }
        return count;
    }
}
