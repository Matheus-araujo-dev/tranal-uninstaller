using System.IO;
using System.Runtime.InteropServices;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// A captura da tela.
///
/// O Windows tem uma API para isso: <c>BitBlt</c> copia um retangulo de uma
/// superficie para outra. E a mesma que o proprio Windows usa quando o
/// usuario aperta "Print Screen".
///
/// A captura e gravada como BMP — um formato que qualquer sistema abre, e que
/// nao precisa de nenhuma biblioteca para ser escrito.
///
/// <para><b>Seguranca primeiro.</b> A captura so acontece quando existe uma
/// area de trabalho: um processo rodando sem sessao grafica (um servico, por
/// exemplo) nao tem o que capturar. Nesses casos o modulo devolve uma string
/// vazia em vez de chamar uma API com um device context invalido — uma
/// chamada invalida derruba o processo, e um modulo de captura nao pode
/// derrubar o Tranal.</para>
///
/// <para><b>O formato do DIB e imposto por quem chama.</b> A API do Windows
/// devolve o pixels no formato pedido pelo cabecalho: aqui, 24 bits por
/// pixel, de cima para baixo. O passo de cada linha do BMP e preenchido
/// ate um multipliplo de 4 bytes — e o que o formato do arquivo exige, e o
/// que a gravacao usa.</para>
///
/// A regra esta em um lugar so: este modulo sabe capturar. A tela de suporte
/// e a linha de comando usam a mesma captura.
/// </summary>
public static class ScreenCapture
{
    private const int SRCCOPY = 0x00CC0020;
    private const int CAPTUREBLT = 0x40000000;
    private const int DIB_RGB_COLORS = 0;
    private const int BITS_PER_PIXEL = 24;
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll", EntryPoint = "SelectObject")]
    private static extern IntPtr SelectObjectGdi(IntPtr hdc, IntPtr bmp);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(
        IntPtr hdcDest, int xDest, int yDest, int w, int h,
        IntPtr hdcSrc, int xSrc, int ySrc, int rop);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(
        IntPtr hdc, IntPtr hbm, uint start, uint cLines, byte[] lpvBits,
        ref BITMAPINFOHEADER lpbmi, int usage);

    /// <summary>
    /// Onde a ultima captura parou. Vazio quando a captura deu certo.
    ///
    /// Existe para o diagnostico: quando a captura falha, o motivo fica
    /// escrito, em vez de a API falhar em silencio.
    /// </summary>
    public static string LastError { get; private set; } = string.Empty;

    /// <summary>
    /// Ha uma area de trabalho para capturar?
    ///
    /// Quando a resposta e nao, a captura e recusada — e isso e dito, em vez
    /// de a API ser chamada e derrubar o processo.
    /// </summary>
    public static bool HasDesktop()
    {
        var (largura, altura) = ScreenSize();
        return largura > 0 && altura > 0;
    }

    /// <summary>
    /// O tamanho da tela (em pixels). Zero quando nao ha area de trabalho.
    /// </summary>
    public static (int Width, int Height) ScreenSize()
    {
        try
        {
            var largura = GetSystemMetrics(SM_CXSCREEN);
            var altura = GetSystemMetrics(SM_CYSCREEN);

            if (largura <= 0 || altura <= 0) return (0, 0);

            return (largura, altura);
        }
        catch
        {
            return (0, 0);
        }
    }

    /// <summary>
    /// O passo de uma linha do BMP: o numero de bytes ate a proxima linha,
    /// sempre multiplu de 4.
    /// </summary>
    /// <param name="width">A largura em pixels.</param>
    public static int RowStride(int width) => ((width * 3) + 3) / 4 * 4;

    /// <summary>
    /// Captura a tela inteira e grava a imagem no caminho informado.
    /// </summary>
    /// <param name="destinationPath">O arquivo .bmp a ser gravado.</param>
    /// <returns>O caminho gravado, ou uma string vazia quando a captura falhou.</returns>
    public static string Capture(string? destinationPath)
    {
        LastError = string.Empty;

        if (string.IsNullOrWhiteSpace(destinationPath)) return string.Empty;

        var (largura, altura) = ScreenSize();
        if (largura <= 0 || altura <= 0) { LastError = "sem area de trabalho"; return string.Empty; }

        IntPtr hdcTela = IntPtr.Zero;
        IntPtr hdcMemoria = IntPtr.Zero;
        IntPtr bitmap = IntPtr.Zero;
        IntPtr antigo = IntPtr.Zero;

        try
        {
            // O device context da tela inteira: sem ele, nao ha o que copiar.
            hdcTela = GetDC(IntPtr.Zero);
            if (hdcTela == IntPtr.Zero) { LastError = "GetDC"; return string.Empty; }

            hdcMemoria = CreateCompatibleDC(hdcTela);
            if (hdcMemoria == IntPtr.Zero) { LastError = "CreateCompatibleDC"; return string.Empty; }

            bitmap = CreateCompatibleBitmap(hdcTela, largura, altura);
            if (bitmap == IntPtr.Zero) { LastError = "CreateCompatibleBitmap"; return string.Empty; }

            antigo = SelectObjectGdi(hdcMemoria, bitmap);

            if (!BitBlt(hdcMemoria, 0, 0, largura, altura, hdcTela, 0, 0, SRCCOPY | CAPTUREBLT))
            {
                LastError = "BitBlt";
                return string.Empty;
            }

            // O passo de cada linha e o que o formato do BMP exige.
            var passo = RowStride(largura);

            // O cabecalho e imposto por quem chama: 24 bits por pixel e
            // altura negativa (de cima para baixo), como o Windows espera.
            //
            // O tamanho da imagem e informado de proposito: com a altura
            // negativa, a API nao consegue deduzir o tamanho do buffer, e
            // recusaria a leitura.
            var cabecalho = new BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = largura,
                biHeight = -altura,
                biPlanes = 1,
                biBitCount = BITS_PER_PIXEL,
                biCompression = 0,
                biSizeImage = passo * altura,
            };

            var dados = new byte[passo * altura];

            var lidas = GetDIBits(hdcMemoria, bitmap, 0, (uint)altura, dados, ref cabecalho, DIB_RGB_COLORS);
            if (lidas == 0) { LastError = "GetDIBits"; return string.Empty; }

            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            WriteBmp(destinationPath, largura, altura, passo, dados);
            return destinationPath;
        }
        catch (Exception ex)
        {
            LastError = "excecao: " + ex.Message;
            return string.Empty;
        }
        finally
        {
            if (antigo != IntPtr.Zero && hdcMemoria != IntPtr.Zero) SelectObjectGdi(hdcMemoria, antigo);
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            if (hdcMemoria != IntPtr.Zero) DeleteDC(hdcMemoria);
            if (hdcTela != IntPtr.Zero) ReleaseDC(IntPtr.Zero, hdcTela);
        }
    }

    /// <summary>
    /// Grava o BMP. O arquivo guarda as linhas de baixo para cima — e o
    /// formato do BMP —, enquanto a API devolveu de cima para baixo.
    /// </summary>
    private static void WriteBmp(string path, int largura, int altura, int passo, byte[] dados)
    {
        var tamanhoImagem = passo * altura;
        var tamanhoArquivo = 14 + 40 + tamanhoImagem;

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write((uint)tamanhoArquivo);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((uint)(14 + 40));

        writer.Write(40);
        writer.Write(largura);
        writer.Write(altura);
        writer.Write((short)1);
        writer.Write((short)BITS_PER_PIXEL);
        writer.Write(0);
        writer.Write(tamanhoImagem);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        for (var y = altura - 1; y >= 0; y--)
        {
            writer.Write(dados, y * passo, passo);
        }
    }
}
