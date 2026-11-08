using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Security.Principal;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Uma falha agrupada de um programa.
/// </summary>
public sealed class CrashEntry
{
    /// <summary>O nome do programa que falhou.</summary>
    public string ProgramName { get; init; } = string.Empty;

    /// <summary>O executavel que falhou, quando o evento diz.</summary>
    public string Executable { get; init; } = string.Empty;

    /// <summary>Quantas falhas houve.</summary>
    public int Count { get; init; }

    /// <summary>A falha mais recente.</summary>
    public DateTime LastSeen { get; init; }

    /// <summary>A primeira falha do periodo.</summary>
    public DateTime FirstSeen { get; init; }

    /// <summary>O codigo do evento.</summary>
    public int EventId { get; init; }

    /// <summary>
    /// O nome legivel do evento ("Aplicativo com falha").
    /// </summary>
    public string EventName { get; init; } = string.Empty;
}

/// <summary>
/// Agrupa as falhas do Windows Event Log por programa.
///
/// O Ashampoo mede "Crash Analyzer: relatorios de falha do Windows Event Log,
/// agrupados por programa, com export e busca online".
///
/// E o painel que responde "esse programa esta caindo?". O Tranal ja LIMPA
/// Event Logs (a ferramenta de privacidade); este aqui e o lado oposto: ler o
/// que ja aconteceu.
///
/// A REGRA de como o nome do programa e extraido mora aqui: o evento traz o
/// caminho do executavel que falhou, e o nome vem do arquivo — nao de
/// adivinhar pelo texto da mensagem.
///
/// O log de aplicacao exige permissao de leitura; quando falta, a lista
/// fica vazia e a tela diz isso, em vez de fingir que nao houve falha.
/// </summary>
public static class CrashAnalyzer
{
    /// <summary>
    /// As falhas agrupadas por programa.
    /// </summary>
    /// <param name="dias">Quantos dias olhar para tras (0 = todos).</param>
    /// <param name="limite">Quantos grupos devolver (0 = todos).</param>
    public static IReadOnlyList<CrashEntry> Scan(int dias = 30, int limite = 0)
    {
        var desde = dias > 0 ? DateTime.Now.AddDays(-dias) : DateTime.MinValue;

        var entradas = new List<CrashEntry>();

        try
        {
            // A consulta ja traz o filtro de nivel (erro) e o periodo. Sem
            // permissao para o log, o EventLogReader lanca — e a lista fica
            // vazia, com a tela avisando.
            var filtro = desde > DateTime.MinValue
                ? "*[System[(Level=2) and TimeCreated[@SystemTime>='" + desde.ToString("yyyy-MM-ddTHH:mm:ss") + "']]]"
                : "*[System[(Level=2)]]";

            var consulta = new EventLogQuery("Application", PathType.LogName, filtro)
            {
                ReverseDirection = true,
                TolerateQueryErrors = true,
            };

            using var leitor = new EventLogReader(consulta);

            EventRecord? evento;
            while ((evento = leitor.ReadEvent()) is not null)
            {
                using (evento)
                {
                    var entrada = Converter(evento);
                    if (entrada is not null) entradas.Add(entrada);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Sem permissao para o Event Log: a lista fica vazia.
            return Array.Empty<CrashEntry>();
        }
        catch (EventLogNotFoundException)
        {
            return Array.Empty<CrashEntry>();
        }
        catch (Exception)
        {
            return Array.Empty<CrashEntry>();
        }

        var agrupado = entradas
            .GroupBy(e => e.ProgramName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CrashEntry
            {
                ProgramName = g.Key,
                Executable = g.First().Executable,
                Count = g.Count(),
                LastSeen = g.Max(e => e.LastSeen),
                FirstSeen = g.Min(e => e.FirstSeen),
                EventId = g.First().EventId,
                EventName = g.First().EventName,
            })
            .OrderByDescending(e => e.Count)
            .ThenByDescending(e => e.LastSeen)
            .ToList();

        if (limite > 0 && agrupado.Count > limite) agrupado = agrupado.Take(limite).ToList();

        return agrupado;
    }

    /// <summary>
    /// Converte um evento do log em uma falha de programa, quando o evento
    /// for de um programa (e nao do proprio Windows).
    /// </summary>
    private static CrashEntry? Converter(EventRecord evento)
    {
        try
        {
            var idNumero = evento.Id;

            // Os eventos de falha de aplicacao sao o 1000 (Application Error),
            // o 1001 (Application Hang) e o 1002 (AppCrash).
            if (idNumero is not (1000 or 1001 or 1002)) return null;

            var executavel = ExtrairExecutavel(evento);
            if (executavel.Length == 0) return null;

            var nome = NomeDoPrograma(executavel);
            if (nome.Length == 0) return null;

            var momento = evento.TimeCreated ?? DateTime.Now;

            return new CrashEntry
            {
                ProgramName = nome,
                Executable = executavel,
                Count = 1,
                LastSeen = momento,
                FirstSeen = momento,
                EventId = idNumero,
                EventName = idNumero switch
                {
                    1000 => "Aplicativo com falha",
                    1001 => "Aplicativo travado",
                    _ => "Aplicativo encerrado",
                },
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extrai o caminho do executavel do texto do evento.
    /// </summary>
    private static string ExtrairExecutavel(EventRecord evento)
    {
        var texto = evento.FormatDescription();
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        // O caminho comeca com uma letra de unidade e tem ":\" depois.
        var partes = texto.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var parte in partes)
        {
            var limpa = parte.Trim('"', ',', '.', ';', ':');
            if (limpa.Length < 4) continue;

            if (limpa.Length > 2
                && char.IsLetter(limpa[0])
                && limpa[1] == ':'
                && (limpa[2] == '\\' || limpa[2] == '/'))
            {
                return limpa;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// O nome do programa a partir do executavel.
    /// </summary>
    private static string NomeDoPrograma(string executavel)
    {
        try
        {
            if (System.IO.File.Exists(executavel))
            {
                var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(executavel);
                if (!string.IsNullOrWhiteSpace(info.ProductName)) return info.ProductName.Trim();
            }

            return System.IO.Path.GetFileNameWithoutExtension(executavel);
        }
        catch
        {
            return System.IO.Path.GetFileNameWithoutExtension(executavel);
        }
    }
}
