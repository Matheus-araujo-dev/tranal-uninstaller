using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// Formata a data de instalacao como tempo relativo ("Hoje", "Ontem", "3
/// semanas atras").
///
/// O HiBit mede "Data de instalacao com tempo relativo (Hoje, Ontem, 1 mes
/// atras, 2 meses antes, 1 ano atras)". E o que a lista mostra na coluna de
/// data: um numero absoluto obriga o usuario a fazer a conta, e a conta e o
/// que ele faz de olhos fechados.
///
/// A data ABSOLUTA continua acessivel: e o texto completo que aparece na
/// dica (tooltip) e no detalhe do programa.
/// </summary>
public static class RelativeDate
{
    /// <summary>
    /// O texto relativo da data.
    ///
    /// </summary>
    /// <param name="data">A data. Nulo quando desconhecida.</param>
    /// <param name="agora">O instante de referencia (o "agora").</param>
    public static string Format(DateTime? data, DateTime? agora = null)
    {
        if (data is null) return string.Empty;

        var referencia = agora ?? DateTime.Now;
        var d = data.Value;

        // A instalacao no futuro (relgio errado, entrada mal escrita) nao e
        // "daqui a 3 dias": e um dado invalido, e a data absoluta e melhor.
        if (d > referencia.AddMinutes(1)) return d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        var dias = (int)Math.Floor((referencia.Date - d.Date).TotalDays);

        if (dias <= 0) return "Hoje";
        if (dias == 1) return "Ontem";
        if (dias < 7) return $"{dias} dias atras";
        if (dias < 14) return "1 semana atras";
        if (dias < 31) return $"{dias / 7} semanas atras";
        if (dias < 60) return "1 mes atras";
        if (dias < 365) return $"{dias / 30} meses atras";
        if (dias < 730) return "1 ano atras";
        return $"{dias / 365} anos atras";
    }

    /// <summary>
    /// O texto relativo da data, a partir do texto dd/MM/yyyy do programa.
    /// </summary>
    /// <param name="dataTexto">A data em texto.</param>
    /// <param name="agora">O instante de referencia (o "agora").</param>
    public static string FormatFromText(string? dataTexto, DateTime? agora = null)
    {
        if (!TryParse(dataTexto, out var data)) return string.Empty;
        return Format(data, agora);
    }

    /// <summary>
    /// Interpreta a data no formato dd/MM/yyyy (o que o Windows usa nos
    /// valores de instalacao).
    /// </summary>
    /// <param name="dataTexto">O texto.</param>
    /// <param name="data">A data interpretada.</param>
    public static bool TryParse(string? dataTexto, out DateTime data)
    {
        data = default;

        if (string.IsNullOrWhiteSpace(dataTexto)) return false;

        var texto = dataTexto.Trim();

        // O formato compacto (yyyyMMdd) e testado ANTES dos ticks: um numero
        // de oito digitos como "20261108" e um numero valido de ticks, e o
        // .NET o converteria numa data de 1970 sem reclamar.
        if (texto.Length == 8 && DateTime.TryParseExact(
                texto, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var compacta))
        {
            data = compacta;
            return true;
        }

        // O Windows grava a data de instalacao como um numero de ticks desde
        // 1601. Esse formato aparece em algumas chaves.
        if (long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
        {
            try
            {
                if (ticks > 0 && ticks < 2_000_000_000_000_000L)
                {
                    data = DateTime.FromFileTimeUtc(ticks).ToLocalTime();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        return DateTime.TryParseExact(
            texto,
            new[] { "dd/MM/yyyy", "dd/MM/yy" },
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out data);
    }
}

/// <summary>
/// O total agregado da lista de programas.
/// </summary>
public sealed class ProgramTotals
{
    /// <summary>Quantos programas a lista tem.</summary>
    public int Count { get; init; }

    /// <summary>Quantos bytes ocupam, no total.</summary>
    public long SizeBytes { get; init; }

    /// <summary>
    /// O texto do total, no formato do Geek ("%d programs of %s size in
    /// total").
    /// </summary>
    public string Text => $"{Count} programa(s) ocupam {ByteFormatter.Format(SizeBytes)} no total";
}

/// <summary>
/// Calcula o total agregado da lista de programas.
///
/// O Geek mede "Total agregado no rodape (%d programs of %s size in total)" e
/// o HiBit mede "Contadores na barra de status: total de programas e tamanho
/// total ocupado".
///
/// E o mesmo numero em dois lugares: por isso a REGRA mora aqui, e a
/// interface e a CLI consomem este objeto.
/// </summary>
public static class ProgramTotalsCalculator
{
    /// <summary>
    /// O total dos programas informados.
    /// </summary>
    /// <param name="programas">Os programas.</param>
    public static ProgramTotals Compute(IEnumerable<InstalledProgram>? programas)
    {
        var lista = (programas ?? Enumerable.Empty<InstalledProgram>()).ToList();

        long total = 0;
        foreach (var p in lista)
        {
            if (p is null) continue;
            if (p.SizeBytes > 0) total += p.SizeBytes;
        }

        return new ProgramTotals { Count = lista.Count, SizeBytes = total };
    }
}
