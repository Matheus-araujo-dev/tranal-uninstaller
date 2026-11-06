using System.IO;
using Tranal.Uninstaller.Core.Modules;

namespace Tranal.Uninstaller.Core.Tracing;

/// <summary>
/// De onde vem a informacao de uma entrada.
///
/// E a distincao que separa "eu vi" de "eu deduzi". Uma entrada medida veio
/// de um log de rastreio (o Tranal observou a mudanca). Uma entrada inferida
/// veio de uma deducao (o registro do Windows, o nome do programa) — e por
/// isso ela nunca pode ser apresentada como medida.
/// </summary>
public enum TraceProvenance
{
    /// <summary>
    /// O Tranal MEDIU a mudanca: ela saiu de um log de rastreio ou do log do
    /// instalador.
    /// </summary>
    Measured,

    /// <summary>
    /// O Tranal INFERIU a mudanca: ela saiu de outra fonte (o registro do
    /// Windows, o nome do programa) e nao foi observada.
    /// </summary>
    Inferred,
}

/// <summary>
/// Uma entrada do log de desinstalacao de um programa ja instalado.
/// </summary>
public sealed class ForensicEntry
{
    /// <summary>O caminho do item.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>"Arquivo", "Pasta" ou "Registro".</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>De onde veio a informacao.</summary>
    public TraceProvenance Provenance { get; init; }

    /// <summary>
    /// A confianca do vinculo entre o item e o programa.
    /// </summary>
    public LeftoverConfidence Confidence { get; init; }

    /// <summary>O motivo do julgamento.</summary>
    public LeftoverReason Reason { get; init; }

    /// <summary>
    /// True quando o item e apenas um CANDIDATO: houve um parecido de nome,
    /// mas nenhuma prova de que ele seja do programa. O candidato entra com a
    /// confianca mais baixa — e nunca como medido.
    /// </summary>
    public bool IsCandidate { get; init; }

    /// <summary>A explicacao, em portugues, do porque do item.</summary>
    public string Detail { get; init; } = string.Empty;
}

/// <summary>
/// O log de desinstalacao de um programa ja instalado, com a proveniencia de
/// cada entrada.
/// </summary>
public sealed class ForensicReport
{
    /// <summary>O nome do programa analisado.</summary>
    public string ProgramName { get; init; } = string.Empty;

    /// <summary>As entradas do relatorio.</summary>
    public IReadOnlyList<ForensicEntry> Entries { get; init; } = Array.Empty<ForensicEntry>();

    /// <summary>Quantas entradas foram medidas (observadas de verdade).</summary>
    public int MeasuredCount => Entries.Count(e => e.Provenance == TraceProvenance.Measured);

    /// <summary>Quantas entradas foram inferidas (deduzidas).</summary>
    public int InferredCount => Entries.Count(e => e.Provenance == TraceProvenance.Inferred);

    /// <summary>Quantas entradas sao apenas candidatos (sem prova de vinculo).</summary>
    public int CandidateCount => Entries.Count(e => e.IsCandidate);

    /// <summary>
    /// True quando o relatorio tem alguma entrada medida. Um relatorio de
    /// programa ja instalado (sem log de rastreio) NUNCA tem: o que ele tem
    /// sao deducoes.
    /// </summary>
    public bool HasMeasuredEntries => MeasuredCount > 0;
}

/// <summary>
/// Um item do plano de remocao.
/// </summary>
public sealed class RemovalPlanItem
{
    /// <summary>"Arquivo", "Pasta" ou "Registro".</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>O caminho do item.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>A explicacao do porque do item.</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>De onde veio a informacao.</summary>
    public TraceProvenance Provenance { get; init; }
}

/// <summary>
/// O plano de remocao derivado de um log.
///
/// REGRA DURA: <b>somente o que esta no log entra no plano</b>. O plano nao
/// e uma varredura com filtro de nome: e a transcricao do que o instalador
/// gravou. Um item que o log nao menciona nunca entra, por mais que o nome
/// dele se pareca com o do programa.
/// </summary>
public sealed class RemovalPlan
{
    /// <summary>O nome do programa a que o plano se refere.</summary>
    public string ProgramName { get; init; } = string.Empty;

    /// <summary>De onde veio o plano (o log que o originou).</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Os itens do plano.</summary>
    public IReadOnlyList<RemovalPlanItem> Items { get; init; } = Array.Empty<RemovalPlanItem>();

    /// <summary>
    /// Transforma o plano nos itens de sobra que o motor de remocao consome.
    /// </summary>
    public IReadOnlyList<LeftoverItem> ToLeftoverItems()
    {
        var itens = new List<LeftoverItem>();

        foreach (var item in Items)
        {
            itens.Add(new LeftoverItem
            {
                Kind = item.Kind,
                Path = item.Path,
                Detail = item.Detail,
                Confidence = item.Provenance == TraceProvenance.Measured
                    ? LeftoverConfidence.High
                    : LeftoverConfidence.Low,
                Reason = LeftoverReason.InstallerLog,
                Selected = item.Provenance == TraceProvenance.Measured,
            });
        }

        return itens;
    }
}

/// <summary>
/// A analise forense de um programa: o que o Tranal pode dizer sobre ele sem
/// ter visto a instalacao.
///
/// A distincao central e a proveniencia. Um programa <b>ja instalado</b> nao
/// tem log de rastreio: o que o Tranal produz e uma DEDUCAO, e por isso cada
/// entrada sai marcada como <see cref="TraceProvenance.Inferred"/> — nunca
/// como medida. O que nao tem prova de vinculo com o programa fica como
/// <b>candidato</b>, com a confianca mais baixa.
///
/// Alem disso, o modulo expoe o plano de remocao dirigido pelo log
/// (itens 32 e 42): a saida do log e a FONTE da remocao, e a correlacao do
/// log com o programa e uma opcao oferecida ao usuario, nunca uma imposicao.
/// </summary>
public static class ForensicAnalyzer
{
    /// <summary>
    /// Monta o log de desinstalacao de um programa ja instalado.
    ///
    /// Sem log de rastreio, TODAS as entradas sao inferidas: e a natureza do
    /// que se sabe de um programa que nao foi observado. Com log de
    /// rastreio, as entradas do log entram como medidas (o Tranal viu).
    /// </summary>
    /// <param name="programa">O programa analisado.</param>
    /// <param name="logs">Logs de rastreio que podem ser correlacionados.</param>
    public static ForensicReport Analyze(InstalledProgram? programa, IReadOnlyList<TraceLogV2>? logs = null)
    {
        var entradas = new List<ForensicEntry>();
        if (programa is null) return new ForensicReport();

        var nome = programa.DisplayName ?? string.Empty;

        // 1) O que o proprio Windows registra do programa: pasta de
        // instalacao, executavel do desinstalador e chave de desinstalacao.
        // Isso e informacao registrada — mas nao foi MEDIDA pelo Tranal, e
        // por isso a proveniencia continua sendo "inferido".
        var instalacao = Normalizar(programa.InstallLocation);
        if (instalacao.Length > 0)
        {
            entradas.Add(new ForensicEntry
            {
                Path = instalacao,
                Kind = "Pasta",
                Provenance = TraceProvenance.Inferred,
                Confidence = LeftoverConfidence.High,
                Reason = LeftoverReason.InstallFolder,
                Detail = "Pasta de instalacao registrada pelo Windows",
            });
        }

        var desinstalador = ExecutavelDoDesinstalador(programa.UninstallString);
        if (desinstalador.Length > 0)
        {
            entradas.Add(new ForensicEntry
            {
                Path = desinstalador,
                Kind = "Arquivo",
                Provenance = TraceProvenance.Inferred,
                Confidence = LeftoverConfidence.High,
                Reason = LeftoverReason.InstallFolder,
                Detail = "Executavel do desinstalador registrado pelo Windows",
            });
        }

        if (!string.IsNullOrWhiteSpace(programa.RegistryPath))
        {
            entradas.Add(new ForensicEntry
            {
                Path = programa.RegistryPath,
                Kind = "Registro",
                Provenance = TraceProvenance.Inferred,
                Confidence = LeftoverConfidence.High,
                Reason = LeftoverReason.InstallFolder,
                Detail = "Chave de desinstalacao do programa",
            });
        }

        // 2) Os candidatos: parecidos de nome, sem prova de vinculo. Entram
        // com a confianca mais baixa e nunca como medido.
        foreach (var candidato in Candidatos(nome, instalacao))
        {
            entradas.Add(new ForensicEntry
            {
                Path = candidato,
                Kind = "Pasta",
                Provenance = TraceProvenance.Inferred,
                Confidence = LeftoverConfidence.Low,
                Reason = LeftoverReason.SimilarNameMatch,
                IsCandidate = true,
                Detail = "Candidato: parecido de nome, sem prova de vinculo com o programa",
            });
        }

        // 3) O que o Tranal MEDIU num log de rastreio.
        if (logs is not null)
        {
            foreach (var log in logs)
            {
                if (log is null) continue;

                foreach (var entrada in log.Entries)
                {
                    if (entrada is null) continue;
                    if (entrada.Kind == TraceChangeKind.Deleted) continue;

                    entradas.Add(new ForensicEntry
                    {
                        Path = entrada.Path,
                        Kind = entrada.IsDirectory ? "Pasta" : "Arquivo",
                        Provenance = TraceProvenance.Measured,
                        Confidence = LeftoverConfidence.High,
                        Reason = LeftoverReason.InstallerLog,
                        Detail = "Registrado pelo rastreio",
                    });
                }
            }
        }

        return new ForensicReport
        {
            ProgramName = nome,
            Entries = entradas,
        };
    }

    /// <summary>
    /// Monta o plano de remocao a partir do log de instalacao do Windows
    /// Installer.
    ///
    /// REGRA DURA: so o que esta no log entra no plano. Nem a pasta do
    /// programa, nem um item parecido pelo nome: o plano e a transcricao do
    /// que o instalador gravou.
    /// </summary>
    /// <param name="log">O log de instalacao.</param>
    public static RemovalPlan BuildPlan(MsiInstallLog? log)
    {
        var itens = new List<RemovalPlanItem>();
        if (log is null) return new RemovalPlan { Source = "Log do instalador" };

        foreach (var arquivo in log.Files)
        {
            if (string.IsNullOrWhiteSpace(arquivo)) continue;

            itens.Add(new RemovalPlanItem
            {
                Kind = "Arquivo",
                Path = arquivo,
                Provenance = TraceProvenance.Measured,
                Detail = "Instalado pelo pacote do Windows Installer",
            });
        }

        foreach (var pasta in log.Directories)
        {
            if (string.IsNullOrWhiteSpace(pasta)) continue;

            itens.Add(new RemovalPlanItem
            {
                Kind = "Pasta",
                Path = pasta,
                Provenance = TraceProvenance.Measured,
                Detail = "Criada pelo pacote do Windows Installer",
            });
        }

        foreach (var entrada in log.RegistryEntries)
        {
            if (string.IsNullOrWhiteSpace(entrada.Key)) continue;

            itens.Add(new RemovalPlanItem
            {
                Kind = "Registro",
                Path = entrada.Key,
                Provenance = TraceProvenance.Measured,
                Detail = entrada.ValueName.Length == 0
                    ? "Valor padrao gravado pelo pacote do Windows Installer"
                    : $"Valor '{entrada.ValueName}' gravado pelo pacote do Windows Installer",
            });
        }

        return new RemovalPlan
        {
            ProgramName = log.ProductCode,
            Source = "Log do instalador (Windows Installer)",
            Items = itens,
        };
    }

    /// <summary>
    /// Monta o plano de remocao a partir de um log de rastreio.
    ///
    /// REGRA DURA: so o que esta no log entra no plano. Entradas de remocao
    /// (o que deixou de existir) e entradas sem tipo conhecido ficam de
    /// fora: o plano e o que o instalador CRIOU.
    /// </summary>
    /// <param name="log">O log de rastreio.</param>
    public static RemovalPlan BuildPlan(TraceLogV2? log)
    {
        var itens = new List<RemovalPlanItem>();
        if (log is null) return new RemovalPlan { Source = "Log de rastreio" };

        foreach (var entrada in log.Entries)
        {
            if (entrada is null) continue;
            if (entrada.Kind == TraceChangeKind.Deleted) continue;
            if (entrada.Kind == TraceChangeKind.Unknown) continue;

            itens.Add(new RemovalPlanItem
            {
                Kind = entrada.IsDirectory ? "Pasta" : "Arquivo",
                Path = entrada.Path,
                Provenance = TraceProvenance.Measured,
                Detail = "Registrado pelo rastreio",
            });
        }

        return new RemovalPlan
        {
            ProgramName = log.ProgramName,
            Source = "Log de rastreio",
            Items = itens,
        };
    }

    /// <summary>
    /// Correlaciona os logs de rastreio com o programa: por nome do programa
    /// e por caminho de instalacao.
    ///
    /// A correlacao devolve CANDIDATOS. A escolha e do usuario: o log e uma
    /// opcao oferecida, nunca uma imposicao.
    /// </summary>
    /// <param name="programa">O programa que esta sendo desinstalado.</param>
    /// <param name="logs">Os logs de rastreio disponiveis.</param>
    public static IReadOnlyList<TraceLogV2> Correlate(
        InstalledProgram? programa,
        IEnumerable<TraceLogV2>? logs)
    {
        var resultado = new List<TraceLogV2>();
        if (programa is null || logs is null) return resultado;

        var variantes = TraceStore.TraceText.NameVariants(programa.DisplayName);
        var instalacao = Normalizar(programa.InstallLocation);

        foreach (var log in logs)
        {
            if (log is null) continue;

            if (CasaPeloNome(log.ProgramName, variantes) || CasaPeloCaminho(log, instalacao))
                resultado.Add(log);
        }

        return resultado;
    }

    /// <summary>
    /// A escolha do log pelo usuario: devolve o log escolhido entre os
    /// candidatos, ou null quando o identificador nao casa com nenhum.
    ///
    /// O metodo nao escolhe sozinho: e a tela (ou a CLI) que apresenta os
    /// candidatos e chama este metodo com o que o usuario escolheu.
    /// </summary>
    /// <param name="candidatos">Os logs correlacionados.</param>
    /// <param name="id">O identificador do log escolhido.</param>
    public static TraceLogV2? SelectLog(IReadOnlyList<TraceLogV2>? candidatos, string? id)
    {
        if (candidatos is null || string.IsNullOrWhiteSpace(id)) return null;

        foreach (var log in candidatos)
        {
            if (log is null) continue;
            if (string.Equals(log.Id, id, StringComparison.OrdinalIgnoreCase)) return log;
        }

        return null;
    }

    /// <summary>
    /// True quando o nome do log casa com o nome do programa.
    /// </summary>
    private static bool CasaPeloNome(string? nomeDoLog, IReadOnlyList<string> variantes)
    {
        if (variantes.Count == 0) return false;

        var normalizado = TraceStore.TraceText.Normalize(nomeDoLog);
        if (normalizado.Length == 0) return false;

        foreach (var variante in variantes)
        {
            if (string.Equals(normalizado, variante, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    /// <summary>
    /// True quando alguma entrada do log esta sob a pasta de instalacao do
    /// programa.
    /// </summary>
    private static bool CasaPeloCaminho(TraceLogV2 log, string instalacao)
    {
        if (instalacao.Length == 0) return false;

        foreach (var entrada in log.Entries)
        {
            if (entrada is null) continue;
            if (string.IsNullOrEmpty(entrada.Path)) continue;

            if (entrada.Path.StartsWith(instalacao + "\\", StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>
    /// Os candidatos: pastas cujo nome se parece com o do programa, sem
    /// prova de vinculo.
    /// </summary>
    private static IEnumerable<string> Candidatos(string nome, string instalacao)
    {
        var variantes = TraceStore.TraceText.NameVariants(nome);
        if (variantes.Count == 0) yield break;

        var raizes = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        };

        foreach (var raiz in raizes)
        {
            if (string.IsNullOrWhiteSpace(raiz)) continue;

            string[] pastas;
            try { pastas = System.IO.Directory.GetDirectories(raiz); }
            catch { continue; }

            foreach (var pasta in pastas)
            {
                if (string.IsNullOrWhiteSpace(pasta)) continue;

                // A pasta de instalacao que o Windows ja registro tem prova:
                // ela ja entrou acima, como deducao com confianca alta.
                if (instalacao.Length > 0
                    && string.Equals(pasta.TrimEnd('\\'), instalacao.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string folha;
                try { folha = System.IO.Path.GetFileName(pasta.TrimEnd('\\')); }
                catch { continue; }

                if (!TraceStore.TraceText.ContainsAnyWholeWord(folha, variantes)) continue;

                yield return pasta;
            }
        }
    }

    /// <summary>
    /// O executavel do comando de desinstalacao (a parte antes do primeiro
    /// espaco, sem as aspas).
    /// </summary>
    private static string ExecutavelDoDesinstalador(string? comando)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(comando)) return string.Empty;

            var texto = comando.Trim();
            var espaco = texto.IndexOf(' ');
            if (espaco > 0) texto = texto[..espaco];

            texto = texto.Trim('"');
            if (texto.Length == 0) return string.Empty;

            return Normalizar(texto);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// O caminho normalizado (string vazia quando nao da).
    /// </summary>
    private static string Normalizar(string? caminho)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(caminho)) return string.Empty;
            return PathUtil.Normalize(caminho.Trim()).TrimEnd('\\');
        }
        catch
        {
            return string.Empty;
        }
    }
}
