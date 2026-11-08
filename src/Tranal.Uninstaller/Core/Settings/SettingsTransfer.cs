using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tranal.Uninstaller.Core.Settings;

/// <summary>
/// O arquivo de.exportacao das preferencias (1002 do Revo: "Export All
/// Settings").
///
/// E um JSON com um cabecalho — versao do formato, aplicacao e data da
/// exportacao — e as preferencias em si. O cabecalho existe para que uma
/// versao mais nova do aplicativo reconheca o arquivo e recuse, em vez de
/// importar preferences pela metade.
/// </summary>
public sealed class SettingsExportFile
{
    /// <summary>A versao do formato do arquivo.</summary>
    public int FormatVersion { get; set; } = SettingsTransfer.CurrentFormatVersion;

    /// <summary>O nome do aplicativo que exportou o arquivo.</summary>
    public string Application { get; set; } = SettingsTransfer.ApplicationName;

    /// <summary>A data da exportacao (UTC).</summary>
    public DateTime ExportedAtUtc { get; set; }

    /// <summary>As preferencias exportadas.</summary>
    public AppSettings Settings { get; set; } = new();
}

/// <summary>
/// Como terminou a importacao das preferencias.
/// </summary>
public enum SettingsImportStatus
{
    /// <summary>O arquivo foi lido (e aplicado, quando nao era previa).</summary>
    Ok,

    /// <summary>O caminho informed nao existe ou nao pode ser lido.</summary>
    FileNotFound,

    /// <summary>O arquivo existe, mas nao e um arquivo de preferencias valido.</summary>
    CorruptedFile,

    /// <summary>
    /// O arquivo e de outro formato, ou de uma versao do formato que este
    /// aplicativo nao conhece.
    /// </summary>
    IncompatibleFile,

    /// <summary>As preferencias foram lidas, mas nao deu para grava-las.</summary>
    WriteFailed,
}

/// <summary>
/// O resultado de uma importacao de preferencias (1003 do Revo: "Import All
/// Settings").
///
/// A **previa** e o mesmo resultado com <see cref="IsPreview"/> ligado: o
/// arquivo e lido e as preferences sao devolvidas, mas nada e gravado — e o
/// que a tela mostra antes de o usuario confirmar.
/// </summary>
public sealed class SettingsImportResult
{
    /// <summary>Como a operacao terminou.</summary>
    public SettingsImportStatus Status { get; init; } = SettingsImportStatus.Ok;

    /// <summary>True quando o arquivo foi lido com sucesso.</summary>
    public bool Success => Status == SettingsImportStatus.Ok;

    /// <summary>
    /// A explicacao, em portugues, do que aconteceu. Nunca fica vazia quando
    /// a operacao falha: a tela mostra este texto ao usuario.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>True quando nada foi gravado (apenas a previa).</summary>
    public bool IsPreview { get; init; }

    /// <summary>As preferencias lidas do arquivo.</summary>
    public AppSettings? Settings { get; init; }

    /// <summary>A data da exportacao original, quando o arquivo tem cabecalho.</summary>
    public DateTime? ExportedAtUtc { get; init; }

    /// <summary>A versao do formato do arquivo lido.</summary>
    public int FormatVersion { get; init; }

    /// <summary>
    /// As diferencas entre as preferencias atuais e as do arquivo, uma por
    /// linha. Vazio na previa de um arquivo igual ao que ja esta em uso.
    /// </summary>
    public IReadOnlyList<string> Changes { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Exporta e importa **todas** as preferencias do Tranal (paridade com
/// "Export All Settings" e "Import All Settings" do Revo Pro).
///
/// O arquivo e JSON UTF-8 e tem o mesmo conteudo das preferencias em disco,
/// mais um cabecalho de versao. A importacao valida o arquivo antes de
/// qualquer gravacao: um arquivo corrompido ou de outra versao do formato
/// **nao** pode deixar o aplicativo sem preferencias — e o caso comum (o
/// usuario escolheu o arquivo errado) nao pode derrubar o aplicativo.
/// </summary>
public static class SettingsTransfer
{
    /// <summary>A versao do formato do arquivo de exportacao.</summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>O nome do aplicativo gravado no cabecalho do arquivo.</summary>
    public const string ApplicationName = "Tranal Uninstaller Pro";

    /// <summary>A extensao sugerida para o arquivo de exportacao.</summary>
    public const string FileExtension = ".json";

    /// <summary>
    /// Exporta todas as preferencias para um arquivo JSON.
    ///
    /// Devolve false quando nao deu para gravar (caminho invalido, pasta
    /// sem permissao, disco cheio). O arquivo e gravado de forma atomica:
    /// o conteudo vai para um arquivo temporario e so entao substitui o
    /// destino — um arquivo pela metade nao serve para nada.
    /// </summary>
    /// <param name="path">O caminho do arquivo a gravar.</param>
    /// <param name="settings">
    /// As preferencias a exportar. Nulo usa as preferencias carregadas.
    /// </param>
    public static bool Export(string path, AppSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            var destino = Path.GetFullPath(path);
            var pasta = Path.GetDirectoryName(destino);
            if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

            var arquivo = new SettingsExportFile
            {
                FormatVersion = CurrentFormatVersion,
                Application = ApplicationName,
                ExportedAtUtc = DateTime.UtcNow,
                Settings = settings ?? AppSettingsStore.Current,
            };

            var json = JsonSerializer.Serialize(arquivo, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });

            var temporario = destino + ".tmp";
            try
            {
                File.WriteAllText(temporario, json, new UTF8Encoding(false));
                File.Move(temporario, destino, true);
            }
            finally
            {
                // O temporario nao pode sobrar na pasta escolhida pelo
                // usuario: ele carrega as preferencias em texto claro.
                try { if (File.Exists(temporario)) File.Delete(temporario); }
                catch { /* o temporario e sobrescrito na proxima exportacao */ }
            }

            return true;
        }
        catch
        {
            // Falha de gravacao: quem chama avisa o usuario. O arquivo do
            // usuario nunca e apagado por uma exportacao que falhou.
            return false;
        }
    }

    /// <summary>
    /// Importa as preferencias de um arquivo JSON.
    ///
    /// Com <paramref name="preview"/> ligado, o arquivo e lido e as
    /// preferences sao devolvidas, mas **nada e gravado**: e o que a tela
    /// mostra antes de o usuario confirmar. Sem a previa, as preferencias
    /// lidas substituem as atuais e sao gravadas em disco.
    /// </summary>
    /// <param name="path">O caminho do arquivo a ler.</param>
    /// <param name="preview">True para apenas ler e mostrar, sem gravar.</param>
    public static SettingsImportResult Import(string path, bool preview = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Falha(
                SettingsImportStatus.FileNotFound,
                "Informe o caminho do arquivo de preferencias.");
        }

        string json;
        try
        {
            if (!File.Exists(path))
            {
                return Falha(
                    SettingsImportStatus.FileNotFound,
                    $"O arquivo '{path}' nao existe.");
            }

            json = File.ReadAllText(path);
        }
        catch (IOException)
        {
            return Falha(
                SettingsImportStatus.FileNotFound,
                $"Nao foi possivel ler o arquivo '{path}'.");
        }
        catch (UnauthorizedAccessException)
        {
            return Falha(
                SettingsImportStatus.FileNotFound,
                $"O arquivo '{path}' nao pode ser lido.");
        }

        SettingsExportFile? arquivo;
        try
        {
            arquivo = JsonSerializer.Deserialize<SettingsExportFile>(json);
        }
        catch (JsonException)
        {
            return Falha(
                SettingsImportStatus.CorruptedFile,
                "O arquivo esta corrompido: o conteudo nao e um arquivo de preferencias valido.");
        }

        if (arquivo is null || arquivo.Settings is null)
        {
            return Falha(
                SettingsImportStatus.CorruptedFile,
                "O arquivo esta corrompido: o conteudo nao e um arquivo de preferencias valido.");
        }

        if (arquivo.FormatVersion <= 0 || arquivo.FormatVersion > CurrentFormatVersion)
        {
            return Falha(
                SettingsImportStatus.IncompatibleFile,
                $"O arquivo usa o formato {arquivo.FormatVersion}, que este aplicativo nao reconhece.");
        }

        var settings = AppSettingsStore.Normalize(arquivo.Settings);

        var resultado = new SettingsImportResult
        {
            Status = SettingsImportStatus.Ok,
            IsPreview = preview,
            Settings = settings,
            ExportedAtUtc = arquivo.ExportedAtUtc == default ? null : arquivo.ExportedAtUtc,
            FormatVersion = arquivo.FormatVersion,
            Message = preview
                ? $"Previa de {settings.Theme} ({settings.Language}) carregada do arquivo."
                : "Preferencias importadas com sucesso.",
            Changes = DescribeDifferences(AppSettingsStore.Current, settings),
        };

        if (preview) return resultado;

        AppSettingsStore.Save(settings);
        return resultado;
    }

    /// <summary>
    /// A diferenca entre as preferencias atuais e as do arquivo, uma por
    /// linha, no formato "Propriedade: valor antigo -&gt; valor novo".
    ///
    /// Serve para a tela mostrar ao usuario o que a importacao vai mudar.
    /// </summary>
    /// <param name="current">As preferencias em uso.</param>
    /// <param name="incoming">As preferencias do arquivo.</param>
    public static IReadOnlyList<string> DescribeDifferences(AppSettings current, AppSettings incoming)
    {
        var diferencas = new List<string>();

        foreach (var propriedade in typeof(AppSettings).GetProperties())
        {
            if (!propriedade.CanRead) continue;

            var antes = propriedade.GetValue(current);
            var depois = propriedade.GetValue(incoming);

            if (Valor(antes) == Valor(depois)) continue;

            diferencas.Add($"{propriedade.Name}: {Valor(antes)} -> {Valor(depois)}");
        }

        return diferencas;
    }

    /// <summary>
    /// O texto de um valor de preferencia: listas e dicionarios viram uma
    /// contagem (o conteudo inteiro nao cabe numa linha de resumo).
    /// </summary>
    /// <param name="valor">O valor.</param>
    private static string Valor(object? valor)
    {
        if (valor is null) return "(vazio)";
        if (valor is string texto) return texto.Length == 0 ? "(vazio)" : texto;
        if (valor is IEnumerable lista) return $"{lista.Cast<object?>().Count()} item(ns)";

        return valor.ToString() ?? string.Empty;
    }

    /// <summary>Um resultado de falha, com a mensagem pronta para a tela.</summary>
    private static SettingsImportResult Falha(SettingsImportStatus status, string mensagem) =>
        new()
        {
            Status = status,
            Message = mensagem,
        };
}
