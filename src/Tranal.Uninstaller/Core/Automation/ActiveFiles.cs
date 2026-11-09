using Tranal.Uninstaller.Core.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// Um arquivo que algum processo segura aberto.
/// </summary>
public sealed class LockedFile
{
    /// <summary>O caminho do arquivo.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Os processos que seguram o arquivo.</summary>
    public IReadOnlyList<string> Processes { get; init; } = Array.Empty<string>();

    /// <summary>True quando algum processo segura o arquivo.</summary>
    public bool IsLocked => Processes.Count > 0;

    /// <summary>Os nomes dos processos, separados por virgula.</summary>
    public string DescribeProcesses() => string.Join(", ", Processes);
}

/// <summary>
/// Os arquivos que estao em uso: o painel de "Active Files".
///
/// E o que responde a pergunta "por que aquele arquivo nao sumiu?" — a
/// resposta e quase sempre "porque um programa ainda esta usando ele".
///
/// A REGRA mora aqui, em um lugar so: quem pergunta usa
/// <see cref="RestartManager"/> (a API do proprio Windows) e nunca chama a
/// API do Agendador de Reinicio por conta propria.
/// </summary>
public static class ActiveFiles
{
    /// <summary>
    /// Os processos que seguram um arquivo.
    ///
    /// Devolve lista vazia quando o arquivo nao existe, quando ninguem o
    /// segura ou quando a consulta falha — nunca lanca: um arquivo em uso
    /// nao pode derrubar a tela que o lista.
    /// </summary>
    /// <param name="path">O caminho do arquivo.</param>
    public static IReadOnlyList<string> ProcessesHolding(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return Array.Empty<string>();

        try
        {
            if (!File.Exists(path)) return Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }

        try
        {
            return RestartManager.WhoIsLocking(path)
                .Select(p => p.ProcessName)
                .Where(nome => !string.IsNullOrWhiteSpace(nome))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao consultar quem usa '{path}': {ex.Message}");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// True quando algum processo segura o arquivo.
    /// </summary>
    /// <param name="path">O caminho do arquivo.</param>
    public static bool IsLocked(string? path) => ProcessesHolding(path).Count > 0;

    /// <summary>
    /// Os arquivos em uso de uma lista de caminhos.
    ///
    /// Só os arquivos que algem processo segura sao devolvidos: a lista e
    /// sobre o que esta em uso, e nao sobre tudo que existe.
    /// </summary>
    /// <param name="paths">Os caminhos a consultar.</param>
    public static IReadOnlyList<LockedFile> Scan(IEnumerable<string>? paths)
    {
        if (paths is null) return Array.Empty<LockedFile>();

        var result = new List<LockedFile>();

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;

            var processes = ProcessesHolding(path);
            if (processes.Count == 0) continue;

            result.Add(new LockedFile
            {
                Path = path,
                Processes = processes,
            });
        }

        return result;
    }
}
