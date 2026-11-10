using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tranal.Uninstaller.Core.Modules;

namespace Tranal.Uninstaller.Core.Reporting;

/// <summary>
/// O formato em que o log de instalacao monitorada e exportado.
/// </summary>
public enum TraceLogExportFormat
{
    /// <summary>Texto simples, para ler no Bloco de Notas.</summary>
    Text,

    /// <summary>CSV, para abrir em planilha.</summary>
    Csv,

    /// <summary>HTML, para abrir no navegador.</summary>
    Html,
}

/// <summary>
/// A exportacao do log de instalacao monitorada.
///
/// Ate aqui o log so saia em JSON — o formato que o proprio Tranal lê.
/// Um JSON serve para o programa; nao serve para quem precisa conferir o
/// que o instalador gravou. Por isso o mesmo log sai tambem em texto, CSV e
/// HTML.
///
/// O conteudo vem sempre do log canonico (<see cref="TraceStore"/>): os
/// arquivos, as pastas e as chaves de registro que o instalador gravou. Nao
/// existe uma segunda leitura do log.
/// </summary>
public static class TraceLogExporter
{
    /// <summary>
    /// Exporta o log de instalacao em texto simples.
    /// </summary>
    /// <param name="install">O log.</param>
    public static string ToText(TracedInstall install)
    {
        var trace = TraceOf(install);
        var sb = new StringBuilder();

        sb.AppendLine("Log de instalacao monitorada");
        sb.AppendLine("===========================");
        sb.AppendLine();
        sb.AppendLine("Programa:           " + (install?.ProgramName ?? string.Empty));
        sb.AppendLine("Data:               " + DataDe(install));
        sb.AppendLine("Instalador:         " + (install?.InstallerPath ?? string.Empty));
        sb.AppendLine("Arquivos:           " + trace.Files.Count);
        sb.AppendLine("Pastas:             " + trace.Folders.Count);
        sb.AppendLine("Chaves de registro: " + trace.RegistryKeys.Count);
        sb.AppendLine();
        sb.AppendLine("ARQUIVOS");
        sb.AppendLine("--------");

        foreach (var file in trace.Files)
        {
            sb.AppendLine(file.Path + "\t" + Tamanho(file.SizeBytes) + "\t" + Momento(file.ModifiedUtc));
        }

        sb.AppendLine();
        sb.AppendLine("PASTAS");
        sb.AppendLine("------");

        foreach (var folder in trace.Folders)
        {
            sb.AppendLine(folder);
        }

        sb.AppendLine();
        sb.AppendLine("CHAVES DE REGISTRO");
        sb.AppendLine("------------------");

        foreach (var key in trace.RegistryKeys)
        {
            sb.AppendLine(key);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Exporta o log de instalacao em CSV.
    ///
    /// O CSV tem uma linha por item, com o tipo do item ("Arquivo",
    /// "Pasta", "Registro"), o caminho e o detalhe. O nome do programa vem
    /// em cada linha: e o que torna o arquivo auto-suficiente quando ele e
    /// aberto sozinho, fora do log.
    /// </summary>
    /// <param name="install">O log.</param>
    public static string ToCsv(TracedInstall install)
    {
        var trace = TraceOf(install);
        var programa = install?.ProgramName ?? string.Empty;
        var sb = new StringBuilder();

        sb.AppendLine("Programa;Tipo;Item;Detalhe;Modificado");

        foreach (var file in trace.Files)
        {
            sb.AppendLine(string.Join(";", new[]
            {
                Csv(programa),
                Csv("Arquivo"),
                Csv(file.Path),
                Csv(Tamanho(file.SizeBytes)),
                Csv(Momento(file.ModifiedUtc)),
            }));
        }

        foreach (var folder in trace.Folders)
        {
            sb.AppendLine(string.Join(";", new[]
            {
                Csv(programa),
                Csv("Pasta"),
                Csv(folder),
                Csv(string.Empty),
                Csv(string.Empty),
            }));
        }

        foreach (var key in trace.RegistryKeys)
        {
            sb.AppendLine(string.Join(";", new[]
            {
                Csv(programa),
                Csv("Registro"),
                Csv(key),
                Csv(string.Empty),
                Csv(string.Empty),
            }));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Exporta o log de instalacao em HTML.
    /// </summary>
    /// <param name="install">O log.</param>
    public static string ToHtml(TracedInstall install)
    {
        var trace = TraceOf(install);
        var programa = install?.ProgramName ?? string.Empty;
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"pt-BR\"><head><meta charset=\"utf-8\">");
        sb.AppendLine("<title>Tranal Uninstaller - Log de instalacao</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,Arial;background:#525252;color:#e6e6e6}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin-bottom:24px}th,td{border:1px solid #2e2e2e;padding:4px 8px;text-align:left}");
        sb.AppendLine("th{background:#3c3c3c}tr:nth-child(even){background:#5a5a5a}h2{margin:18px 0 6px}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine("<h1>Log de instalacao monitorada</h1>");
        sb.AppendLine("<table><tr><th>Campo</th><th>Valor</th></tr>");
        sb.AppendLine("<tr><td>Programa</td><td>" + Html(programa) + "</td></tr>");
        sb.AppendLine("<tr><td>Data</td><td>" + Html(DataDe(install)) + "</td></tr>");
        sb.AppendLine("<tr><td>Instalador</td><td>" + Html(install?.InstallerPath ?? string.Empty) + "</td></tr>");
        sb.AppendLine("<tr><td>Arquivos</td><td>" + trace.Files.Count + "</td></tr>");
        sb.AppendLine("<tr><td>Pastas</td><td>" + trace.Folders.Count + "</td></tr>");
        sb.AppendLine("<tr><td>Chaves de registro</td><td>" + trace.RegistryKeys.Count + "</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>Arquivos</h2><table><tr><th>Arquivo</th><th>Tamanho</th><th>Modificado</th></tr>");
        foreach (var file in trace.Files)
        {
            sb.AppendLine("<tr><td>" + Html(file.Path) + "</td><td>" + Html(Tamanho(file.SizeBytes))
                + "</td><td>" + Html(Momento(file.ModifiedUtc)) + "</td></tr>");
        }
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>Pastas</h2><table><tr><th>Pasta</th></tr>");
        foreach (var folder in trace.Folders)
        {
            sb.AppendLine("<tr><td>" + Html(folder) + "</td></tr>");
        }
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>Chaves de registro</h2><table><tr><th>Chave</th></tr>");
        foreach (var key in trace.RegistryKeys)
        {
            sb.AppendLine("<tr><td>" + Html(key) + "</td></tr>");
        }
        sb.AppendLine("</table>");

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    /// <summary>
    /// Exporta o log de instalacao no formato pedido e grava no destino.
    /// </summary>
    /// <param name="install">O log.</param>
    /// <param name="path">O caminho do arquivo a gravar.</param>
    /// <param name="format">O formato.</param>
    /// <returns>O caminho gravado, ou string vazia quando falhou.</returns>
    public static string Write(TracedInstall install, string path, TraceLogExportFormat format)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            var destino = Path.GetFullPath(path);
            var pasta = Path.GetDirectoryName(destino);
            if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

            var conteudo = format switch
            {
                TraceLogExportFormat.Csv => ToCsv(install),
                TraceLogExportFormat.Html => ToHtml(install),
                _ => ToText(install),
            };

            File.WriteAllText(destino, conteudo, new UTF8Encoding(false));
            return destino;
        }
        catch
        {
            // Falha de gravacao: quem chama precisa avisar o usuario, e o
            // destino fica vazio para isso.
            return string.Empty;
        }
    }

    /// <summary>
    /// As chaves de registro do log como alteracoes prontas para o
    /// formato <c>.reg</c>.
    ///
    /// O log de instalacao guarda as chaves que o instalador CRIOU. O
    /// <c>.reg</c> correspondente recria essas chaves — e e o que serve
    /// para conferir a alteracao do registro fora do Tranal.
    /// </summary>
    /// <param name="install">O log.</param>
    public static IReadOnlyList<RegistryChange> ToRegistryChanges(TracedInstall install)
    {
        var trace = TraceOf(install);
        var result = new List<RegistryChange>();

        foreach (var key in trace.RegistryKeys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;

            result.Add(new RegistryChange
            {
                KeyPath = key,
                Operation = RegistryChangeOperation.CreateKey,
            });
        }

        return result;
    }

    /// <summary>
    /// O conteudo do log, lido pelo leitor canonico.
    /// </summary>
    /// <param name="install">O log.</param>
    private static TraceStore.TracePayload TraceOf(TracedInstall install)
    {
        try
        {
            var payload = TraceStore.LoadPayload(install);
            if (payload is null) return new TraceStore.TracePayload();
            return TraceStore.ReadPayload(payload.Value);
        }
        catch
        {
            // Log corrompido: a exportacao sai vazia em vez de derrubar a
            // tela de quem so queria ver o log.
            return new TraceStore.TracePayload();
        }
    }

    /// <summary>A data do log, ja formatada.</summary>
    /// <param name="install">O log.</param>
    private static string DataDe(TracedInstall? install)
    {
        if (install is null || install.Timestamp == default) return string.Empty;
        return install.Timestamp.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture) + " (UTC)";
    }

    /// <summary>O tamanho de um arquivo, no mesmo formato da tela.</summary>
    /// <param name="bytes">O tamanho em bytes.</param>
    private static string Tamanho(long bytes)
    {
        if (bytes <= 0) return string.Empty;
        var kb = bytes / 1024.0;
        if (kb < 1024) return kb.ToString("0.#", CultureInfo.CurrentCulture) + " KB";
        var mb = kb / 1024.0;
        if (mb < 1024) return mb.ToString("0.#", CultureInfo.CurrentCulture) + " MB";
        return (mb / 1024.0).ToString("0.#", CultureInfo.CurrentCulture) + " GB";
    }

    /// <summary>A data de modificacao de um arquivo.</summary>
    /// <param name="momento">A data (UTC).</param>
    private static string Momento(DateTime momento)
    {
        if (momento == default) return string.Empty;
        return momento.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
    }

    /// <summary>Um valor de CSV: entre aspas quando traz separador ou aspas.</summary>
    /// <param name="value">O valor.</param>
    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        return text.Contains(';') || text.Contains('"')
            ? '"' + text.Replace("\"", "\"\"") + '"'
            : text;
    }

    /// <summary>Um valor de HTML.</summary>
    /// <param name="value">O valor.</param>
    private static string Html(string? value)
    {
        var text = value ?? string.Empty;
        return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}

/// <summary>
/// A edicao de uma entrada do log de instalacao.
///
/// O <see cref="TraceStore"/> grava, importa, exporta e apaga o log inteiro.
/// Aqui o usuario acrescenta ou remove um caminho de arquivo, uma pasta ou uma
/// chave de registro do log, para corrigir o que o rastreio nao viu (ou para
/// tirar de la o que nao era do programa).
///
/// <para><b>A REGRA esta em um lugar so.</b> O que muda no log e o
/// <see cref="LogOperations"/> (que sabe o que e uma entrada, o que e
/// ignorar, o que e acrescentar a mao); a GRAVACAO do arquivo e o
/// <see cref="TraceStore"/>, que grava de forma atomica e deixa a versao
/// anterior em <c>.bak</c>. Esta classe e a porta que fala "por caminho" —
/// que e o jeito que a pessoa pensa.</para>
///
/// <para>Um log pela metade derrubaria a desinstalacao dirigida pelo log — e o
/// log e a unica fonte do que o instalador gravou.</para>
/// </summary>
public static class TraceLogEditor
{
    /// <summary>
    /// Acrescenta um arquivo ao log.
    /// </summary>
    /// <param name="install">O log.</param>
    /// <param name="path">O caminho do arquivo.</param>
    /// <param name="sizeBytes">O tamanho do arquivo, quando conhecido.</param>
    /// <param name="modifiedUtc">A data de modificacao, quando conhecida.</param>
    /// <returns>O log ja atualizado, ou nulo quando nao deu para gravar.</returns>
    public static TracedInstall? AddFile(TracedInstall install, string path, long sizeBytes = 0, DateTime? modifiedUtc = null)
    {
        var alvo = (path ?? string.Empty).Trim();
        if (alvo.Length == 0) return null;

        return Aplicar(install, () => LogOperations.AddEntry(
            install,
            new LogEntry { Kind = LogEntryKind.File, Path = alvo },
            sizeBytes,
            modifiedUtc));
    }

    /// <summary>
    /// Acrescenta uma pasta ao log.
    /// </summary>
    /// <param name="install">O log.</param>
    /// <param name="path">O caminho da pasta.</param>
    /// <returns>O log ja atualizado, ou nulo quando nao deu para gravar.</returns>
    public static TracedInstall? AddFolder(TracedInstall install, string path)
    {
        var alvo = (path ?? string.Empty).Trim();
        if (alvo.Length == 0) return null;

        return Aplicar(install, () => LogOperations.AddEntry(
            install,
            new LogEntry { Kind = LogEntryKind.File, IsFolder = true, Path = alvo }));
    }

    /// <summary>
    /// Acrescenta uma chave de registro ao log.
    /// </summary>
    /// <param name="install">O log.</param>
    /// <param name="keyPath">O caminho da chave.</param>
    /// <returns>O log ja atualizado, ou nulo quando nao deu para gravar.</returns>
    public static TracedInstall? AddRegistryKey(TracedInstall install, string keyPath)
    {
        var alvo = (keyPath ?? string.Empty).Trim();
        if (alvo.Length == 0) return null;

        return Aplicar(install, () => LogOperations.AddEntry(
            install,
            new LogEntry { Kind = LogEntryKind.RegistryKey, Path = alvo }));
    }

    /// <summary>
    /// Remove do log o item com o caminho informado — seja arquivo, pasta ou
    /// chave de registro.
    ///
    /// <para>Remove **todas** as entradas com aquele caminho: uma chave de
    /// registro e os valores dentro dela compartilham o caminho da chave, e
    /// tirar so a chave deixaria os valores no log.</para>
    /// </summary>
    /// <param name="install">O log.</param>
    /// <param name="path">O caminho do item.</param>
    /// <returns>O log ja atualizado, ou nulo quando nao deu para gravar.</returns>
    public static TracedInstall? Remove(TracedInstall install, string path)
    {
        var alvo = (path ?? string.Empty).Trim();
        if (alvo.Length == 0) return null;

        return Aplicar(install, () =>
        {
            var removido = false;

            // A lista e relida a cada volta: a remocao muda o log, e a entrada
            // seguinte so e achada no log ja gravado.
            while (true)
            {
                var entrada = LogOperations.Entries(install).FirstOrDefault(e =>
                    string.Equals(e.Path, alvo, StringComparison.OrdinalIgnoreCase));

                if (entrada is null) break;
                if (!LogOperations.RemoveEntry(install, entrada)) break;

                removido = true;
            }

            return removido;
        });
    }

    /// <summary>
    /// Aplica a mudanca no log e devolve o log ja gravado.
    ///
    /// A gravacao e a do <see cref="TraceStore"/> (atomica, com copia
    /// <c>.bak</c>); aqui so o que muda no conteudo.
    /// </summary>
    /// <param name="install">O log.</param>
    /// <param name="mudanca">A mudanca a aplicar no conteudo do log.</param>
    /// <returns>O log ja atualizado, ou nulo quando nao deu para gravar.</returns>
    private static TracedInstall? Aplicar(TracedInstall install, Func<bool> mudanca)
    {
        try
        {
            if (install is null) return null;
            if (!mudanca()) return null;

            // As contagens sao relidas do log gravado: quem contou e o leitor
            // canonico, e nao a conta que a tela fez.
            var payload = TraceStore.LoadPayload(install);
            if (payload is null) return null;

            var trace = TraceStore.ReadPayload(payload.Value);

            return new TracedInstall
            {
                Id = install.Id,
                ProgramName = install.ProgramName,
                Timestamp = install.Timestamp,
                InstallerPath = install.InstallerPath,
                LogPath = string.IsNullOrWhiteSpace(install.LogPath)
                    ? TraceStore.PathForId(install.Id)
                    : install.LogPath,
                FileCount = trace.Files.Count,
                FolderCount = trace.Folders.Count,
                RegistryKeyCount = trace.RegistryKeys.Count,
                Imported = install.Imported,
            };
        }
        catch
        {
            // Falha ao gravar: o log fica como estava. Quem chama precisa
            // avisar o usuario.
            return null;
        }
    }
}
