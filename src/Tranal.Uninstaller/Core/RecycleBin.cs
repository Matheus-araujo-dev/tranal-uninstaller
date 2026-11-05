using System.IO;
using System.Runtime.InteropServices;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Envio de arquivos e pastas para a Lixeira do Windows.
///
/// O Revo Uninstaller Pro envia as sobras de arquivos para a Lixeira (e nao
/// as apaga de forma definitiva) e apaga chaves de registro em definitivo.
/// O comportamento esta medido no binario do Revo, que importa
/// <c>SHFileOperation</c> — a mesma API usada aqui.
///
/// Quando a Lixeira nao esta disponivel (volume sem recycle bin, policy de
/// grupo, modo de inicializacao), a operacao falha de forma explicita e o
/// chamador decide o destino: apagar em definitivo ou adiar para o proximo
/// reinicio.
/// </summary>
public static class RecycleBin
{
    private const int FoDelete = 0x0003;
    private const ushort FofSilent = 0x0004;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoConfirmMkDir = 0x0200;
    private const ushort FofNoErrorUi = 0x0400;

    /// <summary>Cancelado pelo usuario.</summary>
    public const int Aborted = 0x41;

    /// <summary>A operacao falhou.</summary>
    public const int Failed = 0x72;

    /// <summary>Nao ha Lixeira disponivel no volume de destino.</summary>
    public const int RecycleBinUnavailable = 0x75;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        public short fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHFileOperation(ref ShFileOpStruct lpFileOp);

    /// <summary>
    /// Envia um arquivo ou pasta para a Lixeira.
    /// </summary>
    /// <param name="path">Caminho absoluto do arquivo ou pasta.</param>
    /// <returns>
    /// <c>true</c> quando o item foi enviado para a Lixeira.
    /// <c>false</c> quando a Lixeira nao esta disponivel ou a operacao falhou.
    /// </returns>
    public static bool Send(string path)
    {
        var full = PathUtil.Normalize(path);
        if (full.Length == 0) return false;

        var op = new ShFileOpStruct
        {
            wFunc = FoDelete,
            pFrom = full + "\0\0",
            pTo = null,
            fFlags = FofSilent | FofNoConfirmation | FofAllowUndo | FofNoConfirmMkDir | FofNoErrorUi,
        };

        try
        {
            var code = SHFileOperation(ref op);
            return code == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Envia varios itens para a Lixeira em uma unica operacao da shell.
    /// Devolve a quantidade de itens efetivamente enviados.
    /// </summary>
    public static int SendMany(IEnumerable<string> paths)
    {
        var list = paths
            .Select(PathUtil.Normalize)
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (list.Count == 0) return 0;

        var op = new ShFileOpStruct
        {
            wFunc = FoDelete,
            pFrom = string.Join("\0", list) + "\0\0",
            pTo = null,
            fFlags = FofSilent | FofNoConfirmation | FofAllowUndo | FofNoConfirmMkDir | FofNoErrorUi,
        };

        try
        {
            var code = SHFileOperation(ref op);
            return code == 0 ? list.Count : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// True quando o caminho pode ser enviado para a Lixeira: existe, nao e
    /// raiz de unidade e esta em um volume que aceita Lixeira.
    /// </summary>
    public static bool CanAccept(string path)
    {
        var full = PathUtil.Normalize(path);
        if (full.Length == 0) return false;

        try
        {
            var root = Path.GetPathRoot(full);
            if (!string.IsNullOrEmpty(root) && full.TrimEnd('\\').Length <= root.TrimEnd('\\').Length)
                return false;
        }
        catch { return false; }

        return File.Exists(full) || Directory.Exists(full);
    }
}
