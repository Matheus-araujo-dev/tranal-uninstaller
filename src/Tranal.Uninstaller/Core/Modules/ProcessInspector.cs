using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// Um processo que roda agora no computador.
/// </summary>
public sealed class RunningProcess
{
    /// <summary>O identificador do processo.</summary>
    public int Id { get; init; }

    /// <summary>O nome do executavel, sem o caminho.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>O caminho do executavel. Vazio quando o Windows nao deixa ler.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>A descricao do arquivo ("Windows Media Player").</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>A empresa do arquivo ("Microsoft Corporation").</summary>
    public string Company { get; init; } = string.Empty;

    /// <summary>A versao do arquivo.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Quem abriu a sessao do processo.</summary>
    public string User { get; init; } = string.Empty;

    /// <summary>A memoria ocupada, em bytes.</summary>
    public long WorkingSetBytes { get; init; }

    /// <summary>Quantas threads o processo tem.</summary>
    public int ThreadCount { get; init; }

    /// <summary>Quando o processo comecou.</summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// O programa instalado que contem este executavel.
    ///
    /// E a pergunta que o painel responde: "o que e esse processo?" — e a
    /// resposta que decide qual programa desinstalar.
    /// </summary>
    public string InstalledProgram { get; init; } = string.Empty;

    /// <summary>A memoria, em texto legivel.</summary>
    public string WorkingSetText => WorkingSetBytes switch
    {
        < 1024 => $"{WorkingSetBytes} B",
        < 1024 * 1024 => $"{WorkingSetBytes / 1024.0:0.#} KB",
        < 1024L * 1024 * 1024 => $"{WorkingSetBytes / (1024.0 * 1024):0.#} MB",
        _ => $"{WorkingSetBytes / (1024.0 * 1024 * 1024):0.##} GB",
    };
}

/// <summary>
/// Os processos que estao rodando, com o programa dono de cada um.
///
/// O HiBit mede um "Painel de processos": a lista do que esta rodando, com a
/// informacao de qual programa e dono do executavel. E o que responde a
/// pergunta que surge quando o desinstalador falha — "o que esta segurando
/// estes arquivos?".
///
/// A REGRA do "dono" mora em um lugar so: <see cref="ExecutableLocator"/> e
/// quem sabe onde o Windows procura um executavel, e a correlacao com os
/// programas instalados usa a mesma pasta de instalacao.
/// </summary>
public static class ProcessInspector
{
    /// <summary>
    /// Os processos que estao rodando agora.
    ///
    /// Um processo que termina no meio da listagem simplesmente nao entra: e o
    /// que acontece o tempo todo no Windows.
    /// </summary>
    public static IReadOnlyList<RunningProcess> List() => List(null);

    /// <summary>
    /// Os processos que estao rodando agora, ja com o programa dono.
    ///
    /// </summary>
    /// <param name="programs">
    /// Os programas instalados. Quando nulo, a correlacao nao e feita — o
    /// painel ainda funciona, so sem a coluna do dono.
    /// </param>
    public static IReadOnlyList<RunningProcess> List(IEnumerable<InstalledProgram>? programs)
    {
        var resultado = new List<RunningProcess>();

        Process[] processos;
        try
        {
            processos = Process.GetProcesses();
        }
        catch (Exception ex)
        {
            Core.Tools.ToolLog.Error($"Falha ao listar os processos: {ex.Message}");
            return resultado;
        }
        foreach (var processo in processos)
        {
            try
            {
                resultado.Add(Describe(processo, programs));
            }
            catch
            {
                // O processo morreu no meio da leitura: segue para o proximo.
            }
            finally
            {
                processo.Dispose();
            }
        }

        return resultado
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Id)
            .ToList();
    }

    /// <summary>
    /// Monta a descricao de um processo.
    /// </summary>
    private static RunningProcess Describe(Process processo, IEnumerable<InstalledProgram>? programs)
    {
        var caminho = string.Empty;
        var descricao = string.Empty;
        var empresa = string.Empty;
        var versao = string.Empty;

        try
        {
            // O modulo principal de um processo de outro usuario exige
            // administrador; sem ele, o Windows devolve null em vez de erro.
            caminho = processo.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            // Sem permissao: o processo entra na lista sem o caminho.
        }

        if (caminho.Length > 0)
        {
            try
            {
                var info = Discovery.ExecutableMetadataReader.Read(caminho);
                descricao = info?.FileDescription ?? string.Empty;
                empresa = info?.CompanyName ?? string.Empty;
                versao = info?.FileVersion ?? string.Empty;
            }
            catch
            {
                // Arquivo sem metadados ou inacessivel: o processo entra
                // sem descricao.
            }
        }

        long memoria = 0;
        var threads = 0;
        var inicio = default(DateTime);

        try { memoria = processo.WorkingSet64; } catch { }
        try { threads = processo.Threads.Count; } catch { }
        try { inicio = processo.StartTime; } catch { }

        var usuario = string.Empty;
        try
        {
            var dono = processo.StartInfo.UserName;
            if (!string.IsNullOrEmpty(dono)) usuario = dono;
        }
        catch
        {
            // O processo ja pode ter terminado.
        }

        // O nome vem do arquivo quando ele pode ser lido; quando nao pode,
        // vem do proprio Windows. Um processo sem nome nenhum (um processo
        // morto no meio da leitura) entra com o identificador: a lista sem
        // nome nenhum nao ajuda ninguem.
        var nome = string.Empty;
        if (caminho.Length > 0) nome = Path.GetFileName(caminho);

        if (nome.Length == 0)
        {
            try { nome = processo.ProcessName ?? string.Empty; }
            catch { nome = string.Empty; }
        }

        if (nome.Length == 0) nome = $"PID {processo.Id}";

        return new RunningProcess
        {
            Id = processo.Id,
            Name = nome,
            Path = caminho,
            Description = descricao,
            Company = empresa,
            Version = versao,
            User = usuario,
            WorkingSetBytes = memoria,
            ThreadCount = threads,
            StartTime = inicio,
            InstalledProgram = Dono(caminho, programs),
        };
    }

    /// <summary>
    /// O programa instalado que contem o executavel do processo.
    ///
    /// A correlacao e pela pasta de instalacao: e a mesma regra que
    /// <see cref="Core.Management.ExecutableLocator"/> usa para saber se um
    /// programa esta instalado.
    /// </summary>
    private static string Dono(string caminho, IEnumerable<InstalledProgram>? programs)
    {
        if (caminho.Length == 0 || programs is null) return string.Empty;

        var pasta = Path.GetDirectoryName(caminho);
        if (string.IsNullOrEmpty(pasta)) return string.Empty;

        string melhor = string.Empty;
        var melhorTamanho = int.MaxValue;

        foreach (var programa in programs)
        {
            var local = (programa.InstallLocation ?? string.Empty).Trim().Trim('"');
            if (local.Length == 0) continue;

            string normalizadoLocal;
            try { normalizadoLocal = Path.GetFullPath(local); }
            catch { continue; }

            normalizadoLocal = normalizadoLocal.TrimEnd('\\');

            if (!caminho.StartsWith(normalizadoLocal + "\\", StringComparison.OrdinalIgnoreCase)) continue;

            // A pasta mais especifica vence: o dono do executavel e o
            // programa instalado na pasta mais proxima dele.
            if (normalizadoLocal.Length >= melhorTamanho) continue;

            melhorTamanho = normalizadoLocal.Length;
            melhor = programa.DisplayName;
        }

        return melhor;
    }
}
