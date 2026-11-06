using System;
using System.Collections.Generic;
using System.Text;

public static class FastStrings
{
    /// <summary>
    /// Extrai cadeias ASCII e UTF-16LE imprimiveis de um binario.
    /// Equivalente funcional do modo "strings" do tools/auditoria/coleta.ps1,
    /// porem vetorizado (o lao PowerShell byte-a-byte levava minutos em binarios de 28 MB).
    /// </summary>
    public static string[] Extract(byte[] b, int min)
    {
        var res = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // --- ASCII ---
        int i = 0;
        int n = b.Length;
        while (i < n)
        {
            int start = i;
            while (i < n && b[i] >= 0x20 && b[i] <= 0x7E) i++;
            if (i - start >= min)
            {
                string s = Encoding.ASCII.GetString(b, start, i - start);
                if (seen.Add(s)) res.Add(s);
            }
            i++;
        }

        // --- UTF-16LE (duas alineacoes) ---
        for (int align = 0; align <= 1; align++)
        {
            i = align;
            while (i + 1 < n)
            {
                var sb = new StringBuilder();
                int start = i;
                while (i + 1 < n && b[i + 1] == 0 && b[i] >= 0x20 && b[i] <= 0x7E)
                {
                    sb.Append((char)b[i]);
                    i += 2;
                }
                if (sb.Length >= min)
                {
                    string s = sb.ToString();
                    if (seen.Add(s)) res.Add(s);
                }
                i += 2;
            }
        }

        return res.ToArray();
    }
}
