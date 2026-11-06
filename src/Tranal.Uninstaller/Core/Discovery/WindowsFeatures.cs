using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// De onde veio a informacao.
///
/// A distincao importa porque o Tranal promete "medido vale, inferido nao
/// vale": o que o proprio Windows reportou e medida; o que o Tranal deduziu
/// e inferido. A tela mostra a origem ao lado do item.
/// </summary>
public enum DiscoveryOrigin
{
    /// <summary>Lido direto do relato do proprio Windows (DISM, COM do Windows Update).</summary>
    Measured,

    /// <summary>Deduzido pelo Tranal a partir de fonte secundaria.</summary>
    Inferred,
}

/// <summary>
/// Se o item esta instalado, se pode ser instalado, ou se ficou desconhecido.
/// </summary>
public enum DiscoveryAvailability
{
    /// <summary>Presente na maquina.</summary>
    Installed,

    /// <summary>Conhecido pelo Windows, mas nao instalado.</summary>
    Available,

    /// <summary>O sistema nao respondeu sobre o item.</summary>
    Unknown,
}

/// <summary>
/// Um recurso opcional do Windows (as "Windows Features" do item #11 do
/// backlog).
/// </summary>
public sealed class WindowsFeature
{
    /// <summary>Nome tecnico do recurso (ex.: <c>Microsoft-Hyper-V-All</c>).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Nome exibido pelo Windows.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Estado cru relatado pelo Windows (ex.: <c>Enabled</c>).</summary>
    public string State { get; init; } = string.Empty;

    /// <summary>Instalado, disponivel ou desconhecido.</summary>
    public DiscoveryAvailability Availability { get; init; }

    /// <summary>Origem da informacao.</summary>
    public DiscoveryOrigin Origin { get; init; }
}

/// <summary>
/// Uma atualizacao do Windows (item #11 do backlog).
/// </summary>
public sealed class WindowsUpdate
{
    /// <summary>Titulo da atualizacao.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Artigos de base de conhecimento (ex.: <c>KB5031356</c>).</summary>
    public string KbArticles { get; init; } = string.Empty;

    /// <summary>Data da instalacao (quando o Windows sabe).</summary>
    public DateTime? InstalledOn { get; init; }

    /// <summary>Instalada, disponivel ou desconhecida.</summary>
    public DiscoveryAvailability Availability { get; init; }

    /// <summary>Origem da informacao.</summary>
    public DiscoveryOrigin Origin { get; init; }
}

/// <summary>
/// Resultado de uma consulta de recursos opcionais: os itens e a origem
/// declarada do conjunto.
/// </summary>
public sealed class WindowsFeatureCatalogResult
{
    /// <summary>Recursos encontrados.</summary>
    public IReadOnlyList<WindowsFeature> Items { get; init; } = Array.Empty<WindowsFeature>();

    /// <summary>Origem declarada do conjunto.</summary>
    public DiscoveryOrigin Origin { get; init; }

    /// <summary>True quando a fonte respondeu.</summary>
    public bool SourceAvailable { get; init; }

    /// <summary>Nome da fonte usada (ex.: <c>DISM</c>).</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Explicacao do resultado, em portugues.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Resultado de uma consulta de atualizacoes do Windows.
/// </summary>
public sealed class WindowsUpdateCatalogResult
{
    /// <summary>Atualizacoes encontradas.</summary>
    public IReadOnlyList<WindowsUpdate> Items { get; init; } = Array.Empty<WindowsUpdate>();

    /// <summary>Origem declarada do conjunto.</summary>
    public DiscoveryOrigin Origin { get; init; }

    /// <summary>True quando a fonte respondeu.</summary>
    public bool SourceAvailable { get; init; }

    /// <summary>Explicacao do resultado, em portugues.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Catalogo dos recursos opcionais do Windows ("Windows Features").
///
/// A fonte e o proprio DISM (<c>dism.exe /online /get-features /format:table</c>),
/// que e o mesmo caminho que o Painel de Controle usa. A leitura e apenas
/// leitura: o Tranal nao habilita nem desabilita recurso nenhum aqui.
/// </summary>
public static class WindowsFeatureCatalog
{
    /// <summary>Executavel do DISM.</summary>
    public const string DismCommand = "dism.exe";

    /// <summary>Argumentos da listagem de recursos opcionais.</summary>
    public const string DismArguments = "/online /get-features /format:table";

    /// <summary>Timeout da consulta (segundos). O DISM e lento em maquina grande.</summary>
    private const int TimeoutSeconds = 180;

    /// <summary>
    /// Lista os recursos opcionais do Windows, com o estado de cada um.
    /// Nunca lanca: quando o DISM nao responde, o resultado vem com a origem
    /// declarada e a fonte marcada como indisponivel.
    /// </summary>
    public static WindowsFeatureCatalogResult List()
    {
        try
        {
            var (exitCode, output) = Run(DismCommand, DismArguments, TimeoutSeconds);
            if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return new WindowsFeatureCatalogResult
                {
                    Items = Array.Empty<WindowsFeature>(),
                    Origin = DiscoveryOrigin.Inferred,
                    SourceAvailable = false,
                    Source = "DISM",
                    Message = "O DISM nao respondeu; os recursos opcionais nao puderam ser lidos.",
                };
            }

            var items = ParseDismTable(output);

            return new WindowsFeatureCatalogResult
            {
                Items = items,
                Origin = DiscoveryOrigin.Measured,
                SourceAvailable = true,
                Source = "DISM",
                Message = $"{items.Count} recursos opcionais lidos pelo DISM.",
            };
        }
        catch (Exception ex)
        {
            return new WindowsFeatureCatalogResult
            {
                Items = Array.Empty<WindowsFeature>(),
                Origin = DiscoveryOrigin.Inferred,
                SourceAvailable = false,
                Source = "DISM",
                Message = $"Falha ao ler os recursos opcionais: {ex.Message}",
            };
        }
    }

    /// <summary>
    /// Interpreta a saida tabular do DISM.
    ///
    /// O DISM escreve a coluna como <c>Nome : Estado</c> em uma versao e
    /// como colunas alinhadas por espaco em outra. As duas formas sao
    /// aceitas, porque o texto e o unico contrato estavel entre versoes.
    /// </summary>
    public static IReadOnlyList<WindowsFeature> ParseDismTable(string output)
    {
        var features = new List<WindowsFeature>();
        if (string.IsNullOrWhiteSpace(output)) return features;

        foreach (var rawLine in output.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("---", StringComparison.Ordinal)) continue;

            string name;
            string state;

            var colon = line.IndexOf(':');
            if (colon > 0)
            {
                name = line[..colon].Trim();
                state = line[(colon + 1)..].Trim();
            }
            else
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                state = parts[^1].Trim();
                if (!IsKnownState(state)) continue;

                name = string.Join(" ", parts[..^1]).Trim();
            }

            if (name.Length == 0) continue;
            if (string.Equals(name, "Feature Name", StringComparison.OrdinalIgnoreCase)) continue;
            if (!IsKnownState(state)) continue;

            features.Add(new WindowsFeature
            {
                Name = name,
                DisplayName = name,
                State = state,
                Availability = ClassifyState(state),
                Origin = DiscoveryOrigin.Measured,
            });
        }

        return features;
    }

    /// <summary>
    /// Traduz o estado do Windows para instalado / disponivel / desconhecido.
    ///
    /// <c>Enabled</c> e <c>EnablePending</c> sao instalados (o segundo esta
    /// esperando o reinicio); <c>Disabled</c> e <c>DisabledWithPayloadRemoved</c>
    /// sao recursos disponiveis para instalar.
    /// </summary>
    public static DiscoveryAvailability ClassifyState(string? state)
    {
        if (string.IsNullOrWhiteSpace(state)) return DiscoveryAvailability.Unknown;

        var value = state.Trim();

        if (value.Equals("Enabled", StringComparison.OrdinalIgnoreCase)
            || value.Equals("EnablePending", StringComparison.OrdinalIgnoreCase))
            return DiscoveryAvailability.Installed;

        if (value.Equals("Disabled", StringComparison.OrdinalIgnoreCase)
            || value.Equals("DisabledWithPayloadRemoved", StringComparison.OrdinalIgnoreCase))
            return DiscoveryAvailability.Available;

        return DiscoveryAvailability.Unknown;
    }

    /// <summary>
    /// True quando o texto e um estado de recurso que o Windows usa.
    /// </summary>
    private static bool IsKnownState(string state) =>
        state.Equals("Enabled", StringComparison.OrdinalIgnoreCase)
        || state.Equals("Disabled", StringComparison.OrdinalIgnoreCase)
        || state.Equals("DisabledWithPayloadRemoved", StringComparison.OrdinalIgnoreCase)
        || state.Equals("EnablePending", StringComparison.OrdinalIgnoreCase)
        || state.Equals("DisablePending", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Executa um comando e devolve o codigo de saida e a saida padrao.
    /// Nunca lanca.
    /// </summary>
    private static (int ExitCode, string Output) Run(string fileName, string arguments, int timeoutSeconds)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo);
        if (process is null) return (-1, string.Empty);

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(timeoutSeconds * 1000))
        {
            try { process.Kill(entireProcessTree: true); }
            catch { /* o processo ja morreu sozinho */ }
            return (-1, string.Empty);
        }

        var output = stdout.GetAwaiter().GetResult();
        _ = stderr.GetAwaiter().GetResult();
        return (process.ExitCode, output);
    }
}

/// <summary>
/// Um registro cru de atualizacao, como a fonte devolve.
/// </summary>
public sealed class WindowsUpdateRecord
{
    /// <summary>Titulo da atualizacao.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Artigos de base de conhecimento.</summary>
    public string KbArticles { get; init; } = string.Empty;

    /// <summary>Data do ultimo despliegue (quando o Windows sabe).</summary>
    public DateTime? Date { get; init; }
}

/// <summary>
/// Fonte de atualizacoes do Windows. A interface existe para que o catalogo
/// possa ser testado sem COM e sem depender da maquina de quem roda a suite.
/// </summary>
public interface IWindowsUpdateSource
{
    /// <summary>
    /// Pesquisa as atualizacoes. <paramref name="installedOnly"/> true traz
    /// as ja instaladas; false traz as disponiveis para instalacao.
    /// </summary>
    IReadOnlyList<WindowsUpdateRecord> Search(bool installedOnly);
}

/// <summary>
/// Catalogo das atualizacoes do Windows.
///
/// A fonte real e o COM <c>Microsoft.Update.Session</c>, o mesmo agente de
/// atualizacao que o Windows usa. A sessao separada por estado: as
/// atualizacoes ja instaladas vem do historico local; as disponiveis exigem
/// consulta ao servico do Windows Update e podem demorar.
/// </summary>
public static class WindowsUpdateCatalog
{
    /// <summary>
    /// Lista as atualizacoes ja instaladas.
    /// </summary>
    public static WindowsUpdateCatalogResult ListInstalled(IWindowsUpdateSource? source = null)
        => Build(source, installedOnly: true);

    /// <summary>
    /// Lista as atualizacoes disponiveis para instalacao. A consulta vai ao
    /// servico do Windows Update e pode demorar varios minutos.
    /// </summary>
    public static WindowsUpdateCatalogResult ListAvailable(IWindowsUpdateSource? source = null)
        => Build(source, installedOnly: false);

    /// <summary>
    /// Monta o resultado da consulta, declarando a origem da informacao.
    /// </summary>
    private static WindowsUpdateCatalogResult Build(IWindowsUpdateSource? source, bool installedOnly)
    {
        var effective = source ?? new WindowsUpdateSessionSource();

        try
        {
            var records = effective.Search(installedOnly);
            var items = records
                .Select(r => new WindowsUpdate
                {
                    Title = r.Title,
                    KbArticles = r.KbArticles,
                    InstalledOn = r.Date,
                    Availability = installedOnly ? DiscoveryAvailability.Installed : DiscoveryAvailability.Available,
                    Origin = DiscoveryOrigin.Measured,
                })
                .ToList();

            return new WindowsUpdateCatalogResult
            {
                Items = items,
                Origin = DiscoveryOrigin.Measured,
                SourceAvailable = true,
                Message = installedOnly
                    ? $"{items.Count} atualizacoes instaladas."
                    : $"{items.Count} atualizacoes disponiveis.",
            };
        }
        catch (Exception ex)
        {
            return new WindowsUpdateCatalogResult
            {
                Items = Array.Empty<WindowsUpdate>(),
                Origin = DiscoveryOrigin.Inferred,
                SourceAvailable = false,
                Message = installedOnly
                    ? $"Nao foi possivel ler as atualizacoes instaladas: {ex.Message}"
                    : $"Nao foi possivel consultar as atualizacoes disponiveis: {ex.Message}",
            };
        }
    }
}

/// <summary>
/// Fonte real das atualizacoes: o agente COM do Windows Update
/// (<c>Microsoft.Update.Session</c>).
///
/// O COM e chamado por ligacao tardia (reflexao) e nao por <c>dynamic</c>:
/// o nucleo e compilado com <c>Nullable</c> ligado e a ligacao tardia
/// escrita a mao nao deixa passar aviso de nulidade.
/// </summary>
public sealed class WindowsUpdateSessionSource : IWindowsUpdateSource
{
    /// <inheritdoc />
    public IReadOnlyList<WindowsUpdateRecord> Search(bool installedOnly)
    {
        var records = new List<WindowsUpdateRecord>();

        try
        {
            var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
            if (sessionType is null) return records;

            var session = Activator.CreateInstance(sessionType);
            if (session is null) return records;

            var searcher = Call(session, "CreateUpdateSearcher");
            if (searcher is null) return records;

            var query = installedOnly
                ? "IsInstalled=1 and Type='Software'"
                : "IsInstalled=0 and Type='Software'";

            var result = Call(searcher, "Search", query);
            if (result is null) return records;

            if (Get(result, "Updates") is not IEnumerable updates) return records;

            foreach (var item in updates)
            {
                if (item is null) continue;

                records.Add(new WindowsUpdateRecord
                {
                    Title = Convert.ToString(Get(item, "Title")) ?? string.Empty,
                    KbArticles = ReadKbArticles(item),
                    Date = ReadDate(item),
                });
            }
        }
        catch
        {
            // COM indisponivel ou negado: a lista fica vazia e o catalogo
            // declara a fonte como indisponivel.
        }

        return records;
    }

    /// <summary>
    /// Le os artigos KB da atualizacao (o COM devolve um vetor de int).
    /// </summary>
    private static string ReadKbArticles(object update)
    {
        try
        {
            if (Get(update, "KBArticleIDs") is not Array array) return string.Empty;

            var numbers = new List<string>();
            foreach (var value in array)
            {
                if (value is null) continue;
                numbers.Add("KB" + Convert.ToString(value));
            }

            return string.Join(", ", numbers);
        }
        catch
        {
            // Artigo ausente: o titulo ainda identifica a atualizacao.
            return string.Empty;
        }
    }

    /// <summary>
    /// Le a data do ultimo despliegue da atualizacao.
    /// </summary>
    private static DateTime? ReadDate(object update)
    {
        try
        {
            var value = Get(update, "LastDeploymentChangeTime");
            if (value is DateTime date) return date;
            return null;
        }
        catch
        {
            // Data desconhecida: o Windows nem sempre a expõe.
            return null;
        }
    }

    /// <summary>
    /// Chama um metodo do objeto COM por ligacao tardia.
    /// </summary>
    private static object? Call(object target, string name, params object?[] args)
        => target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, args);

    /// <summary>
    /// Le uma propriedade do objeto COM por ligacao tardia.
    /// </summary>
    private static object? Get(object target, string name)
        => target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null);
}
