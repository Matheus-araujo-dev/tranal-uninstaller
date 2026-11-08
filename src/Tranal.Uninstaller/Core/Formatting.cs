namespace Tranal.Uninstaller.Core;

/// <summary>Formatacao de tamanhos e tamanhos de arquivo em texto legivel.</summary>
public static class ByteFormatter
{
    /// <summary>
    /// Formata bytes como B/KB/MB/GB/TB (mesmo padrao da coluna "Tamanho" do Revo).
    ///
    /// A REGRA mora aqui, em um lugar so: quem mostra tamanho na tela, no
    /// relatorio ou na linha de comando consome este metodo e nunca formata
    /// byte por conta propria.
    ///
    /// A formatacao e sempre invariante de cultura: o mesmo numero sai
    /// "1.5 MB" na tela, no relatorio e no log, seja qual for a lingua do
    /// Windows que estiver rodando.
    /// </summary>
    /// <param name="bytes">O tamanho em bytes.</param>
    public static string Format(long bytes)
    {
        if (bytes <= 0) return "0 B";

        string[] unidades = { "B", "KB", "MB", "GB", "TB" };
        double valor = bytes;
        var indice = 0;

        while (valor >= 1024 && indice < unidades.Length - 1)
        {
            valor /= 1024;
            indice++;
        }

        return indice == 0
            ? $"{bytes} B"
            : $"{valor.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} {unidades[indice]}";
    }
}
