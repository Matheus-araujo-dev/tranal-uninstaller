using System.IO;
using System.Runtime.InteropServices;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Um fluxo alternativo (Alternate Data Stream, ADS) de um arquivo NTFS.
///
/// Os fluxos alternativos sao os "streams escondidos" que o Windows anexa a um
/// arquivo sem que ele apareca no Explorer: o <c>Zone.Identifier</c> (a marca
/// de "baixado da internet"), o <c>Zone.Transfer</c>, o <c>SmartScreen</c> e
/// afins. Eles sobrevivem a renomear, mover e copiar o arquivo, e por isso
/// sao rastros do programa — o item 4 da fila binaria.
/// </summary>
public sealed class AlternateDataStream
{
    /// <summary>
    /// Nome do fluxo, sem o prefixo ":" e sem o sufixo de tipo.
    /// Ex.: "Zone.Identifier". O fluxo principal e o proprio conteudo do
    /// arquivo, e aparece como "$DATA".
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Tipo do fluxo: "$DATA" (dados do usuario) ou "$RESOURCE" (recurso).
    /// </summary>
    public string Type { get; init; } = "$DATA";

    /// <summary>
    /// Caminho completo do stream, como o Explorer o entende:
    /// "C:\arquivo.txt:Zone.Identifier". No fluxo principal, o caminho do
    /// proprio arquivo.
    /// </summary>
    public string StreamPath { get; init; } = string.Empty;

    /// <summary>Tamanho do stream em bytes.</summary>
    public long Size { get; init; }

    /// <summary>
    /// True quando este e o fluxo principal — o proprio conteudo do arquivo.
    /// </summary>
    public bool IsDefault { get; init; }
}

/// <summary>
/// Descobre os fluxos alternativos (ADS) dos arquivos de um programa.
///
/// Somente leitura: nada e escrito, nada e apagado.
///
/// A enumeracao usa <c>GetFileInformationByHandleEx</c> com a classe
/// <c>FileStreamInfo</c>, que devolve exatamente a lista de streams do
/// arquivo: o fluxo principal, como "::$DATA" (nome vazio), e os escondidos,
/// como ":Zone.Identifier:$DATA".
///
/// A API primeira chamada para isto — <c>FindFirstStreamW</c> — foi MEDIDA e
/// descartada: nesta build do Windows 11 ela devolve <c>ERROR_INVALID_PARAMETER</c>
/// para qualquer arquivo e qualquer tamanho de buffer. A classe
/// <c>FileStreamInfo</c> e a mesma informacao, pela mesma familia de APIs do
/// kernel, e funciona.
///
/// Em volume que nao e NTFS (FAT, exFAT, rede) nao existem fluxos
/// alternativos: a varredura devolve uma lista vazia, que e a resposta
/// correta.
/// </summary>
public static class AlternateDataStreamScanner
{
    /// <summary>FileStreamInfo: a lista de streams do arquivo.</summary>
    private const int FileStreamInfo = 7;

    /// <summary>GENERIC_READ.</summary>
    private const uint GenericRead = 0x80000000;

    /// <summary>FILE_READ_ATTRIBUTES: leitura minima, para arquivo protegido.</summary>
    private const uint FileReadAttributes = 0x00000080;

    /// <summary>Leitura, escrita e remocao compartilhadas: o arquivo pode estar em uso.</summary>
    private const uint FileShareReadWriteDelete = 0x00000007;

    /// <summary>OPEN_EXISTING.</summary>
    private const uint OpenExisting = 3;

    /// <summary>FILE_ATTRIBUTE_NORMAL.</summary>
    private const uint FileAttributeNormal = 0x00000080;

    /// <summary>
    /// Tamanho do buffer da consulta. A lista de streams cabe em poucos
    /// kilobytes; o buffer cresce quando a consulta diz que nao coube.
    /// </summary>
    private static readonly uint[] BufferSizes = { 64 * 1024, 512 * 1024, 4 * 1024 * 1024 };

    /// <summary>Deslocamento do nome do stream dentro da entrada.</summary>
    private const int StreamNameOffset = 24;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        IntPtr file,
        int infoClass,
        IntPtr info,
        uint bufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    /// <summary>
    /// Fluxos alternativos de um arquivo.
    /// </summary>
    /// <param name="filePath">Caminho do arquivo.</param>
    /// <param name="includeDefaultStream">
    /// Quando <c>true</c>, inclui tambem o fluxo principal (o proprio conteudo
    /// do arquivo). O padrao e <c>false</c>: o interessa sao os streams
    /// escondidos.
    /// </param>
    /// <returns>
    /// Os fluxos encontrados. Lista vazia quando o arquivo nao existe, quando
    /// o volume nao suporta streams alternativos ou quando a leitura falhou.
    /// </returns>
    public static IReadOnlyList<AlternateDataStream> Scan(string filePath, bool includeDefaultStream = false)
    {
        var result = new List<AlternateDataStream>();

        var full = PathUtil.Normalize(filePath);
        if (full.Length == 0) return result;
        if (!File.Exists(full)) return result;

        var handle = OpenFile(full);
        if (handle == IntPtr.Zero) return result;

        try
        {
            foreach (var (name, size) in ReadStreams(handle))
                AddStream(result, full, name, size, includeDefaultStream);
        }
        catch
        {
            // Arquivo protegido, volume sem streams, erro de API: a varredura
            // devolve o que deu para ler, sem estourar.
        }
        finally
        {
            CloseHandle(handle);
        }

        return result;
    }

    /// <summary>
    /// Fluxos alternativos de todos os arquivos de uma pasta.
    /// </summary>
    /// <param name="folderPath">Pasta a varrer.</param>
    /// <param name="recursive">Quando <c>true</c>, desce nas subpastas.</param>
    public static IReadOnlyList<AlternateDataStream> ScanFolder(string folderPath, bool recursive = false)
    {
        var result = new List<AlternateDataStream>();

        var full = PathUtil.Normalize(folderPath);
        if (full.Length == 0) return result;
        if (!Directory.Exists(full)) return result;

        try
        {
            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var file in Directory.EnumerateFiles(full, "*", option))
                result.AddRange(Scan(file));
        }
        catch
        {
            // Pasta sem permissao no meio da varredura: o que deu para ler
            // continua valendo.
        }

        return result;
    }

    /// <summary>
    /// Nomes dos fluxos escondidos de um arquivo, sem o fluxo principal.
    /// Atalho para quem so precisa do nome (ex.: "Zone.Identifier").
    /// </summary>
    public static IReadOnlyList<string> HiddenStreamNames(string filePath) =>
        Scan(filePath).Select(s => s.Name).ToList();

    /// <summary>
    /// Abre o arquivo para leitura. Tenta a leitura plena e, quando o
    /// arquivo proibe, cai para a leitura de atributos — que ja basta para
    /// listar os streams.
    /// </summary>
    private static IntPtr OpenFile(string path)
    {
        var handle = CreateFileW(
            path, GenericRead, FileShareReadWriteDelete, IntPtr.Zero,
            OpenExisting, FileAttributeNormal, IntPtr.Zero);

        if (!IsInvalid(handle)) return handle;

        handle = CreateFileW(
            path, FileReadAttributes, FileShareReadWriteDelete, IntPtr.Zero,
            OpenExisting, FileAttributeNormal, IntPtr.Zero);

        return IsInvalid(handle) ? IntPtr.Zero : handle;
    }

    private static bool IsInvalid(IntPtr handle) =>
        handle == IntPtr.Zero || handle == new IntPtr(-1);

    /// <summary>
    /// Le a lista de streams do arquivo aberto.
    /// </summary>
    private static List<(string Name, long Size)> ReadStreams(IntPtr handle)
    {
        var result = new List<(string, long)>();

        foreach (var size in BufferSizes)
        {
            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (!GetFileInformationByHandleEx(handle, FileStreamInfo, buffer, size)) continue;

                var offset = 0;
                while (offset >= 0)
                {
                    var next = Marshal.ReadInt32(buffer, offset);
                    var nameLength = Marshal.ReadInt32(buffer, offset + 4);
                    var streamSize = Marshal.ReadInt64(buffer, offset + 8);

                    var name = nameLength > 0
                        ? Marshal.PtrToStringUni(buffer + offset + StreamNameOffset, nameLength / 2)
                        : string.Empty;

                    if (!string.IsNullOrEmpty(name)) result.Add((name, streamSize));

                    if (next <= 0) break;
                    offset += next;
                }

                break;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return result;
    }

    /// <summary>
    /// Acrescenta o stream encontrado a lista, conforme o tipo.
    /// </summary>
    private static void AddStream(
        List<AlternateDataStream> result, string filePath, string rawName, long size, bool includeDefaultStream)
    {
        var (name, type) = SplitStreamName(rawName);

        // O fluxo principal vem sem nome ("::$DATA"): ele e o proprio conteudo
        // do arquivo, nao um rastro.
        var isDefault = name.Length == 0 || name.Equals("$DATA", StringComparison.OrdinalIgnoreCase);
        if (isDefault) name = "$DATA";

        if (isDefault && !includeDefaultStream) return;

        result.Add(new AlternateDataStream
        {
            Name = name,
            Type = type,
            StreamPath = isDefault ? filePath : $"{filePath}:{name}",
            Size = size,
            IsDefault = isDefault,
        });
    }

    /// <summary>
    /// O nome vem da API no formato ":Nome:Tipo". O fluxo principal vem como
    /// "::$DATA" — nome vazio, tipo "$DATA" — e os escondidos como
    /// ":Zone.Identifier:$DATA".
    /// </summary>
    private static (string Name, string Type) SplitStreamName(string rawName)
    {
        var value = rawName.Trim();

        var index = value.LastIndexOf(':');
        var type = index >= 0 ? value[(index + 1)..] : "$DATA";
        var name = index >= 0 ? value[..index] : value;

        // O ":" inicial marca o inicio do nome do stream.
        name = name.TrimStart(':');

        return (name, type.Length > 0 ? type : "$DATA");
    }
}
