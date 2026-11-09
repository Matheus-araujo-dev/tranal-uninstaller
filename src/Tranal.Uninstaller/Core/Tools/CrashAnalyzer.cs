using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Security.Principal;
using Tranal.Uninstaller.Core.Reporting;
using Tranal.Uninstaller.Core.Settings;

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
/// <summary>
/// Os textos do relatorio de falhas.
///
/// O relatorio e escrito em dois lugares (a tela e a CLI) e cada um fala o
/// seu idioma: por isso os rotulos entram por parametro. A regra de COMO uma
/// falha vira uma linha mora no <see cref="CrashAnalyzer"/>.
/// </summary>
public sealed class CrashReportLabels
{
    /// <summary>O titulo do relatorio.</summary>
    public string Title { get; init; } = "Crash Analyzer";

    /// <summary>A coluna do nome do programa.</summary>
    public string Program { get; init; } = "Programa";

    /// <summary>A coluna da quantidade de falhas.</summary>
    public string Count { get; init; } = "Falhas";

    /// <summary>A coluna da primeira falha do periodo.</summary>
    public string First { get; init; } = "Primeira";

    /// <summary>A coluna da ultima falha do periodo.</summary>
    public string Last { get; init; } = "Ultima";

    /// <summary>A coluna do evento.</summary>
    public string Event { get; init; } = "Evento";

    /// <summary>A coluna do executavel que falhou.</summary>
    public string Executable { get; init; } = "Executavel";

    /// <summary>O subtitulo, com o total de programas.</summary>
    public string SubtitleFormat { get; init; } = "{0} programa(s) com falha registrada.";
}


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
    /// <summary>
    /// O relatorio em HTML das falhas agrupadas.
    ///
    /// E o "export" do Crash Analyzer: o que a tela mostra, em um arquivo que
    /// abre em qualquer navegador.
    ///
    /// A REGRA de COMO uma falha vira uma linha mora aqui, no nucleo. Os
    /// TEXTOS sao de quem chama: a tela fala o idioma da interface e a CLI
    /// fala o dela.
    /// </summary>
    /// <param name="entries">As falhas agrupadas.</param>
    /// <param name="labels">Os textos do relatorio (pt-BR quando omitido).</param>
    public static string ToHtmlReport(IReadOnlyList<CrashEntry> entries, CrashReportLabels? labels = null)
    {
        var textos = labels ?? new CrashReportLabels();
        var lista = entries ?? Array.Empty<CrashEntry>();

        var columns = new[]
        {
            new ToolReportColumn { Header = textos.Program, Width = 240 },
            new ToolReportColumn { Header = textos.Count, Width = 80 },
            new ToolReportColumn { Header = textos.First, Width = 150 },
            new ToolReportColumn { Header = textos.Last, Width = 150 },
            new ToolReportColumn { Header = textos.Event, Width = 240 },
            new ToolReportColumn { Header = textos.Executable, Width = 420 },
        };

        var rows = new List<ToolReportRow>();
        foreach (var entrada in lista)
        {
            rows.Add(new ToolReportRow
            {
                Cells = new[]
                {
                    entrada.ProgramName,
                    entrada.Count.ToString(CultureInfo.CurrentCulture),
                    FormatarData(entrada.FirstSeen),
                    FormatarData(entrada.LastSeen),
                    $"{entrada.EventName} ({entrada.EventId})",
                    entrada.Executable,
                },
            });
        }

        var subtitle = string.Format(CultureInfo.CurrentCulture, textos.SubtitleFormat, lista.Count);
        return ToolReportWriter.ToHtml(textos.Title, subtitle, columns, rows);
    }

    /// <summary>
    /// <summary>
    /// Grava o relatorio em HTML no arquivo informado.
    ///
    /// Quem grava o arquivo e o modulo (a regra fica em um lugar so); a tela
    /// e a CLI so escolhem o caminho.
    /// </summary>
    /// <param name="path">O caminho do arquivo.</param>
    /// <param name="entries">As falhas agrupadas.</param>
    /// <param name="labels">Os textos do relatorio.</param>
    public static bool WriteReport(string path, IReadOnlyList<CrashEntry> entries, CrashReportLabels? labels = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            File.WriteAllText(path, ToHtmlReport(entries, labels));
            return true;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao gravar o relatorio '{path}': {ex.Message}");
            return false;
        }
    }


    /// O termo de busca de uma falha: o nome do programa com a pista do tipo
    /// de evento. E o que a "busca online" do Crash Analyzer procura.
    /// </summary>
    /// <param name="entry">A falha.</param>
    public static string BuildSearchTerm(CrashEntry entry)
    {
        if (entry is null) return string.Empty;

        var nome = (entry.ProgramName ?? string.Empty).Trim();
        if (nome.Length == 0) return string.Empty;

        // 1001 = "Aplicativo travado" (hang); 1000 e 1002 = "Aplicativo com
        // falha" (crash). A pista em ingles traz mais resultados.
        var pista = entry.EventId == 1001 ? "hang" : "crash";

        return $"\"{nome}\" {pista}";
    }

    /// <summary>
    /// O endereco da busca online de uma falha, pelo motor configurado.
    /// </summary>
    /// <param name="entry">A falha.</param>
    /// <param name="settings">As preferencias do usuario (o motor de busca).</param>
    public static string BuildSearchUrl(CrashEntry entry, AppSettings settings)
    {
        var termo = BuildSearchTerm(entry);
        if (termo.Length == 0) return string.Empty;

        if (settings is null) return WebSearch.BuildUrl(termo);

        return WebSearch.BuildUrl(termo, settings);
    }

    /// <summary>
    /// A data no formato do relatorio (vazio quando nao ha data).
    /// </summary>
    private static string FormatarData(DateTime momento) =>
        momento == default
            ? string.Empty
            : momento.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);


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
                var produto = Discovery.ExecutableMetadataReader.ProductOf(executavel);
                if (!string.IsNullOrWhiteSpace(produto)) return produto;
            }

            return System.IO.Path.GetFileNameWithoutExtension(executavel);
        }
        catch
        {
            return System.IO.Path.GetFileNameWithoutExtension(executavel);
        }
    }
}
