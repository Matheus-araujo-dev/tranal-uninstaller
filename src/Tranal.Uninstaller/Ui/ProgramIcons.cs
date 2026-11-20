using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// O icone de um programa tirado do proprio executavel (o campo DisplayIcon
/// do registro costuma apontar para o .exe, "C:\App\app.exe,0").
///
/// POR QUE EXISTE: o carregador de imagens do WPF nao decodifica executavel —
/// e por isso a lista mostrava quase todos os programas sem icone. O icone e
/// o que a pessoa reconhece primeiro na lista.
///
/// MEMORIA: o icone sai do arquivo pelo <c>ExtractIconEx</c> (sem carregar as
/// extensoes do Shell no processo), e copiado para um bitmap pequeno
/// (<see cref="Size"/>) e congelado; o HICON e liberado na hora. O cache e por
/// caminho+indice: o mesmo executavel nunca e lido duas vezes.
/// </summary>
public static class ProgramIcons
{
    /// <summary>O lado do icone, em pixels. A lista desenha a 20 px.</summary>
    public const int Size = 32;

    private static readonly ConcurrentDictionary<string, ImageSource?> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int index, IntPtr[]? large, IntPtr[]? small, uint count);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    /// <summary>
    /// O icone do executavel apontado por um campo DisplayIcon
    /// ("caminho,indice"). Nulo quando o arquivo nao existe ou nao tem icone.
    /// </summary>
    /// <param name="displayIcon">O valor cru do campo DisplayIcon.</param>
    public static ImageSource? FromDisplayIcon(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon)) return null;

        var raw = displayIcon.Trim();
        var index = 0;
        var comma = raw.LastIndexOf(',');
        if (comma > 0 && int.TryParse(raw[(comma + 1)..].Trim(), out var parsed))
        {
            index = parsed;
            raw = raw[..comma];
        }

        return FromFile(raw.Trim().Trim('"'), index);
    }

    /// <summary>
    /// O icone de indice <paramref name="index"/> de um executavel ou DLL.
    /// Nulo quando nao da para extrair.
    /// </summary>
    /// <param name="path">O caminho do arquivo.</param>
    /// <param name="index">O indice do icone (negativo = id do recurso).</param>
    public static ImageSource? FromFile(string path, int index = 0)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var ext = Path.GetExtension(path);
        if (!ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            && !ext.Equals(".dll", StringComparison.OrdinalIgnoreCase)
            && !ext.Equals(".ico", StringComparison.OrdinalIgnoreCase))
            return null;

        return Cache.GetOrAdd(path + "|" + index, _ => Extract(path, index));
    }

    private static ImageSource? Extract(string path, int index)
    {
        if (!File.Exists(path)) return null;

        var large = new IntPtr[1];
        try
        {
            if (ExtractIconEx(path, index, large, null, 1) == 0 || large[0] == IntPtr.Zero)
            {
                // O indice pedido nao existe: o primeiro icone do arquivo
                // ainda e melhor que nenhum.
                if (index == 0 || ExtractIconEx(path, 0, large, null, 1) == 0 || large[0] == IntPtr.Zero)
                    return null;
            }

            var source = Imaging.CreateBitmapSourceFromHIcon(
                large[0], Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(Size, Size));
            source.Freeze();
            return source;
        }
        catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException or ExternalException)
        {
            // Um icone corrompido nao pode derrubar a lista: a linha fica sem
            // icone, como antes.
            return null;
        }
        finally
        {
            if (large[0] != IntPtr.Zero) DestroyIcon(large[0]);
        }
    }

    /// <summary>
    /// Esquece os icones carregados. A lista chama ao recarregar: um programa
    /// atualizado pode ter trocado o icone, e o cache nao deve crescer sem
    /// limite.
    /// </summary>
    public static void Clear() => Cache.Clear();
}
