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
/// O <see cref="TraceStore"/> grava, importa, exporta e apaga o log inteiro
/// — mas nao edita entrada. Aqui o usuario adiciona ou remove um caminho de
/// arquivo, uma pasta ou uma chave de registro do log, para corrigir o que o
/// rastreio nao viu (ou para tirar de la o que nao era do programa).
///
/// A gravacao e **atomica**: o conteudo novo vai para um arquivo temporario
/// e so entao substitui o original, deixando a versao anterior em
/// <c>.bak</c>. Um log pela metade derrubaria a desinstalacao dirigida pelo
/// log — e o log e a unica fonte do que o instalador gravou.
///
/// O envelope do arquivo (o cabecalho, o id, o nome do programa) e o que
/// estava la: so o bloco <c>payload</c> e as contagens sao reescritos, e
/// eles sao montados pelo proprio <see cref="TraceStore"/>. Nao ha uma
/// segunda interpretacao do formato do log.
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

        return Editar(install, trace =>
        {
            if (trace.Files.Any(f => string.Equals(f.Path, alvo, StringComparison.OrdinalIgnoreCase)))
                return;

            trace.Files.Add(new TraceStore.TraceFileEntry
            {
                Path = alvo,
                SizeBytes = sizeBytes,
                ModifiedUtc = modifiedUtc ?? default,
            });
        });
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

        return Editar(install, trace =>
        {
            if (trace.Folders.Any(f => string.Equals(f, alvo, StringComparison.OrdinalIgnoreCase)))
                return;

            trace.Folders.Add(alvo);
        });
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

        return Editar(install, trace =>
        {
            if (trace.RegistryKeys.Any(k => string.Equals(k, alvo, StringComparison.OrdinalIgnoreCase)))
                return;

            trace.RegistryKeys.Add(alvo);
        });
    }

    /// <summary>
    /// Remove do log o item com o caminho informado — seja arquivo, pasta ou
    /// chave de registro.
    /// </summary>
    /// <param name="install">O log.</param>
    /// <param name="path">O caminho do item.</param>
    /// <returns>O log ja atualizado, ou nulo quando nao deu para gravar.</returns>
    public static TracedInstall? Remove(TracedInstall install, string path)
    {
        var alvo = (path ?? string.Empty).Trim();
        if (alvo.Length == 0) return null;

        return Editar(install, trace =>
        {
            trace.Files.RemoveAll(f => string.Equals(f.Path, alvo, StringComparison.OrdinalIgnoreCase));
            trace.Folders.RemoveAll(f => string.Equals(f, alvo, StringComparison.OrdinalIgnoreCase));
            trace.RegistryKeys.RemoveAll(k => string.Equals(k, alvo, StringComparison.OrdinalIgnoreCase));
        });
    }

    /// <summary>
    /// Aplica a mudanca no log e grava o arquivo de forma atomica.
    /// </summary>
    /// <param name="install">O log.</param>
    /// <param name="mudanca">A mudanca a aplicar no conteudo do log.</param>
    /// <returns>O log ja atualizado, ou nulo quando nao deu para gravar.</returns>
    private static TracedInstall? Editar(TracedInstall install, Action<TraceStore.TracePayload> mudanca)
    {
        try
        {
            if (install is null) return null;

            var caminho = ResolverCaminho(install);
            if (caminho.Length == 0 || !File.Exists(caminho)) return null;

            var texto = File.ReadAllText(caminho);

            JsonNode? raiz;
            try { raiz = JsonNode.Parse(texto); }
            catch { return null; }

            if (raiz is not JsonObject documento) return null;

            JsonElement payload = default;
            if (documento["payload"] is JsonNode noPayload)
            {
                try { payload = noPayload.Deserialize<JsonElement>(); }
                catch { payload = default; }
            }

            var trace = TraceStore.ReadPayload(payload);
            mudanca(trace);

            var novoPayload = TraceStore.SerializePayload(trace);
            documento["payload"] = JsonNode.Parse(novoPayload.GetRawText());

            documento["counts"] = new JsonObject
            {
                ["files"] = trace.Files.Count,
                ["folders"] = trace.Folders.Count,
                ["registryKeys"] = trace.RegistryKeys.Count,
            };

            var saida = documento.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            if (!GravarAtomicamente(caminho, saida)) return null;

            return new TracedInstall
            {
                Id = install.Id,
                ProgramName = install.ProgramName,
                Timestamp = install.Timestamp,
                InstallerPath = install.InstallerPath,
                LogPath = caminho,
                FileCount = trace.Files.Count,
                FolderCount = trace.Folders.Count,
                RegistryKeyCount = trace.RegistryKeys.Count,
                Imported = install.Imported,
            };
        }
        catch
        {
            // Falha ao ler ou gravar: o log fica como estava. Quem chama
            // precisa avisar o usuario.
            return null;
        }
    }

    /// <summary>
    /// O arquivo do log: o caminho que o log traz, quando ele existe; senao o
    /// caminho canonico do identificador.
    /// </summary>
    /// <param name="install">O log.</param>
    private static string ResolverCaminho(TracedInstall install)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(install.LogPath))
            {
                var candidato = PathUtil.Normalize(install.LogPath);
                if (candidato.Length > 0 && File.Exists(candidato)) return candidato;
            }

            if (!string.IsNullOrWhiteSpace(install.Id)) return TraceStore.PathForId(install.Id);
            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Grava o arquivo de forma atomica: o conteudo novo vai para um
    /// temporario, o original vira <c>.bak</c> e o temporario assume o lugar.
    /// </summary>
    /// <param name="caminho">O caminho do arquivo.</param>
    /// <param name="conteudo">O conteudo novo.</param>
    private static bool GravarAtomicamente(string caminho, string conteudo)
    {
        var temporario = caminho + ".tmp";

        try
        {
            File.WriteAllText(temporario, conteudo, new UTF8Encoding(false));

            if (File.Exists(caminho))
            {
                try { File.Replace(temporario, caminho, caminho + ".bak", true); }
                catch (PlatformNotSupportedException) { SubstituirComBackup(temporario, caminho); }
                catch (IOException) { SubstituirComBackup(temporario, caminho); }
            }
            else
            {
                File.Move(temporario, caminho);
            }

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            // O temporario nao pode sobrar na pasta de logs.
            try { if (File.Exists(temporario)) File.Delete(temporario); }
            catch { /* o temporario fica para a proxima gravacao sobrescrever */ }
        }
    }

    /// <summary>
    /// Substitui o arquivo quando <c>File.Replace</c> nao esta disponivel
    /// (volume sem suporte a ReplaceFile). A copia para <c>.bak</c> vem
    /// ANTES da substituicao: e melhor avisar do que fingir que houve
    /// backup.
    /// </summary>
    /// <param name="temporario">O arquivo temporario.</param>
    /// <param name="caminho">O arquivo original.</param>
    private static void SubstituirComBackup(string temporario, string caminho)
    {
        File.Copy(caminho, caminho + ".bak", true);
        File.Move(temporario, caminho, true);
    }
}
