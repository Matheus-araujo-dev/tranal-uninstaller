namespace Tranal.Uninstaller.Core;

/// <summary>Formatacao de tamanhos e tamanhos de arquivo em texto legivel.</summary>
public static class ByteFormatter
{
    /// <summary>
    /// Formata bytes como KB/MB/GB (mesmo padrao da coluna "Tamanho" do Revo).
    /// </summary>
    public static string Format(long bytes)
    {
        if (bytes <= 0) return string.Empty;

        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:0.##} KB";

        double mb = kb / 1024.0;
        if (mb < 1024) return $"{mb:0.##} MB";

        return $"{mb / 1024.0:0.##} GB";
    }
}
