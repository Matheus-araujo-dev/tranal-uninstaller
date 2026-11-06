using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Tranal.Uninstaller.Core.Tracing;

/// <summary>
/// Os dados de um processo em execucao, como a politica de rastreio os
/// enxerga.
/// </summary>
public sealed class TraceProcessInfo
{
    /// <summary>Nome do executavel (com a extensao).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Caminho completo do executavel, quando deu para ler.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Identificador do processo.</summary>
    public int Id { get; init; }
}

/// <summary>
/// A politica que decide quais processos sao observados pelo rastreio.
///
/// A regra e uma so e vale para nome e para caminho:
///
///   1. <b>A exclusao vence a inclusao.</b> Um processo excluido nunca e
///      observado, mesmo que o nome dele tambem esteja na inclusao.
///   2. Se ha inclusao (por nome ou por caminho), so e observado o que casa
///      com alguma delas.
///   3. Nao havendo inclusao, tudo e observado — menos o que foi excluido.
///
/// O casamento por caminho e por prefixo COM separador: <c>C:\App</c>
/// protege <c>C:\App\app.exe</c> e nao <c>C:\App2\app.exe</c>. O casamento
/// por nome ignora a extensao e nao diferencia maiuscula de minuscula.
/// </summary>
public sealed class TraceProcessPolicy
{
    private readonly List<string> _incluidosNome = new();
    private readonly List<string> _excluidosNome = new();
    private readonly List<string> _incluidosCaminho = new();
    private readonly List<string> _excluidosCaminho = new();

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// A politica que observa tudo (nenhuma inclusao, nenhuma exclusao).
    /// </summary>
    public static TraceProcessPolicy Default => new();

    /// <summary>Nomes incluidos no rastreio.</summary>
    public IReadOnlyList<string> IncludedNames => _incluidosNome;

    /// <summary>Nomes excluidos do rastreio.</summary>
    public IReadOnlyList<string> ExcludedNames => _excluidosNome;

    /// <summary>Caminhos incluidos no rastreio.</summary>
    public IReadOnlyList<string> IncludedPaths => _incluidosCaminho;

    /// <summary>Caminhos excluidos do rastreio.</summary>
    public IReadOnlyList<string> ExcludedPaths => _excluidosCaminho;

    /// <summary>
    /// Limpa a politica: volta a observar todos os processos.
    /// </summary>
    public void Clear()
    {
        _incluidosNome.Clear();
        _excluidosNome.Clear();
        _incluidosCaminho.Clear();
        _excluidosCaminho.Clear();
    }

    /// <summary>
    /// True quando a politica restringe o rastreio a algum processo (isto e,
    /// quando ha alguma inclusao).
    /// </summary>
    public bool HasRestriction => _incluidosNome.Count > 0 || _incluidosCaminho.Count > 0;

    /// <summary>
    /// Inclui um processo pelo nome. O nome pode vir com ou sem extensao.
    /// </summary>
    /// <param name="name">Nome do executavel (ex.: "setup.exe").</param>
    public void Include(string? name)
    {
        var normalizado = NormalizeName(name);
        if (normalizado.Length == 0) return;
        if (_incluidosNome.Contains(normalizado, StringComparer.OrdinalIgnoreCase)) return;
        _incluidosNome.Add(normalizado);
    }

    /// <summary>
    /// Exclui um processo pelo nome. A exclusao vence qualquer inclusao.
    /// </summary>
    /// <param name="name">Nome do executavel (ex.: "setup.exe").</param>
    public void Exclude(string? name)
    {
        var normalizado = NormalizeName(name);
        if (normalizado.Length == 0) return;
        if (_excluidosNome.Contains(normalizado, StringComparer.OrdinalIgnoreCase)) return;
        _excluidosNome.Add(normalizado);
    }

    /// <summary>
    /// Inclui um processo pelo caminho do executavel (a pasta onde ele roda).
    /// </summary>
    /// <param name="path">Caminho da pasta do executavel.</param>
    public void IncludePath(string? path)
    {
        var normalizado = NormalizePath(path);
        if (normalizado.Length == 0) return;
        if (_incluidosCaminho.Contains(normalizado, StringComparer.OrdinalIgnoreCase)) return;
        _incluidosCaminho.Add(normalizado);
    }

    /// <summary>
    /// Exclui um processo pelo caminho do executavel. A exclusao vence
    /// qualquer inclusao.
    /// </summary>
    /// <param name="path">Caminho da pasta do executavel.</param>
    public void ExcludePath(string? path)
    {
        var normalizado = NormalizePath(path);
        if (normalizado.Length == 0) return;
        if (_excluidosCaminho.Contains(normalizado, StringComparer.OrdinalIgnoreCase)) return;
        _excluidosCaminho.Add(normalizado);
    }

    /// <summary>
    /// Decide se o processo deve ser observado pela politica.
    /// </summary>
    /// <param name="processName">Nome do executavel do processo.</param>
    /// <param name="executablePath">Caminho do executavel do processo.</param>
    public bool ShouldObserve(string? processName, string? executablePath)
    {
        var nome = NormalizeName(processName);
        var caminho = NormalizePath(executablePath);

        // 1) A exclusao vence a inclusao — e ela e conferida primeiro.
        if (nome.Length > 0 && _excluidosNome.Contains(nome, StringComparer.OrdinalIgnoreCase))
            return false;

        if (caminho.Length > 0 && EstaSobAlgum(caminho, _excluidosCaminho))
            return false;

        // 2) Havendo inclusao, so entra o que casa com ela.
        if (HasRestriction)
        {
            if (nome.Length > 0 && _incluidosNome.Contains(nome, StringComparer.OrdinalIgnoreCase))
                return true;

            if (caminho.Length > 0 && EstaSobAlgum(caminho, _incluidosCaminho))
                return true;

            return false;
        }

        // 3) Sem inclusao, tudo e observado.
        return true;
    }

    /// <summary>
    /// Os processos em execucao que a politica manda observar.
    /// </summary>
    public IReadOnlyList<TraceProcessInfo> ObservedProcesses()
    {
        var resultado = new List<TraceProcessInfo>();
        try
        {
            foreach (var processo in System.Diagnostics.Process.GetProcesses())
            {
                using (processo)
                {
                    var nome = string.Empty;
                    var caminho = string.Empty;

                    try { nome = processo.ProcessName; }
                    catch { /* o processo pode ter morrido no meio da leitura */ }

                    try
                    {
                        var modulo = processo.MainModule;
                        if (modulo is not null) caminho = modulo.FileName ?? string.Empty;
                    }
                    catch { /* sem permissao para ler o modulo: segue sem o caminho */ }

                    if (!ShouldObserve(nome, caminho)) continue;

                    resultado.Add(new TraceProcessInfo
                    {
                        Name = nome,
                        Path = caminho,
                        Id = processo.Id,
                    });
                }
            }
        }
        catch
        {
            // Sem a lista de processos, a politica continua valendo: o que
            // importa e a regra, nao a leitura da lista.
        }

        return resultado;
    }

    /// <summary>
    /// Texto que resume a politica (para a tela). Tudo em portugues.
    /// </summary>
    public string Describe()
    {
        if (!HasRestriction && _excluidosNome.Count == 0 && _excluidosCaminho.Count == 0)
            return "Observa todos os processos.";

        var partes = new List<string>();

        if (_incluidosNome.Count > 0)
            partes.Add("Incluidos: " + string.Join(", ", _incluidosNome));

        if (_incluidosCaminho.Count > 0)
            partes.Add("Pastas incluidas: " + string.Join(", ", _incluidosCaminho));

        if (_excluidosNome.Count > 0)
            partes.Add("Excluidos: " + string.Join(", ", _excluidosNome));

        if (_excluidosCaminho.Count > 0)
            partes.Add("Pastas excluidas: " + string.Join(", ", _excluidosCaminho));

        return string.Join(" | ", partes);
    }

    /// <summary>
    /// Serializa a politica em JSON (para a tela guardar e a integracao
    /// levar para as preferencias).
    /// </summary>
    public string ToJson()
    {
        try
        {
            return JsonSerializer.Serialize(new PoliticaJson
            {
                IncludedNames = _incluidosNome,
                ExcludedNames = _excluidosNome,
                IncludedPaths = _incluidosCaminho,
                ExcludedPaths = _excluidosCaminho,
            }, Opcoes);
        }
        catch
        {
            return "{}";
        }
    }

    /// <summary>
    /// Le a politica de um JSON. Um JSON invalido devolve a politica que
    /// observa tudo — nunca uma excecao para quem chamou.
    /// </summary>
    /// <param name="json">O JSON serializado por <see cref="ToJson"/>.</param>
    public static TraceProcessPolicy FromJson(string? json)
    {
        var politica = new TraceProcessPolicy();
        if (string.IsNullOrWhiteSpace(json)) return politica;

        try
        {
            var dados = JsonSerializer.Deserialize<PoliticaJson>(json, Opcoes);
            if (dados is null) return politica;

            foreach (var nome in dados.IncludedNames ?? Array.Empty<string>()) politica.Include(nome);
            foreach (var nome in dados.ExcludedNames ?? Array.Empty<string>()) politica.Exclude(nome);
            foreach (var caminho in dados.IncludedPaths ?? Array.Empty<string>()) politica.IncludePath(caminho);
            foreach (var caminho in dados.ExcludedPaths ?? Array.Empty<string>()) politica.ExcludePath(caminho);
        }
        catch
        {
            // JSON invalido: a politica padrao (observa tudo) e a unica segura.
        }

        return politica;
    }

    /// <summary>
    /// O nome do processo, sem extensao e sem diferenca de caixa.
    /// </summary>
    private static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        try
        {
            var texto = name.Trim().Trim('"');
            var posicao = texto.LastIndexOfAny(new[] { '\\', '/' });
            if (posicao >= 0) texto = texto[(posicao + 1)..];

            var ponto = texto.LastIndexOf('.');
            if (ponto > 0) texto = texto[..ponto];

            return texto.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// O caminho normalizado, sem barra final.
    /// </summary>
    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        try
        {
            var normalizado = PathUtil.Normalize(path.Trim());
            return normalizado.TrimEnd('\\');
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// True quando o caminho esta sob um dos caminhos informados. O casamento
    /// e por prefixo COM separador, como em todo o resto do Tranal.
    /// </summary>
    private static bool EstaSobAlgum(string caminho, IEnumerable<string> raizes)
    {
        foreach (var raiz in raizes)
        {
            if (raiz.Length == 0) continue;

            if (string.Equals(caminho, raiz, StringComparison.OrdinalIgnoreCase)) return true;

            if (caminho.Length <= raiz.Length) continue;
            if (!caminho.StartsWith(raiz, StringComparison.OrdinalIgnoreCase)) continue;

            var separador = caminho[raiz.Length];
            if (separador == '\\' || separador == '/') return true;
        }

        return false;
    }

    /// <summary>
    /// O formato guardado no disco.
    /// </summary>
    private sealed class PoliticaJson
    {
        public IReadOnlyList<string>? IncludedNames { get; set; }
        public IReadOnlyList<string>? ExcludedNames { get; set; }
        public IReadOnlyList<string>? IncludedPaths { get; set; }
        public IReadOnlyList<string>? ExcludedPaths { get; set; }
    }
}
