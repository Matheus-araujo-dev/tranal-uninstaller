using System.IO;
using Tranal.Uninstaller.Core.Modules;

namespace Tranal.Uninstaller.Core.Tracing;

/// <summary>
/// Um arquivo (ou uma pasta) no retrato de um sistema.
/// </summary>
public sealed class SnapshotFileState
{
    /// <summary>Caminho absoluto normalizado do item.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Tamanho em bytes no momento da captura.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Data da ultima modificacao (UTC) no momento da captura.</summary>
    public DateTime ModifiedUtc { get; init; }

    /// <summary>True quando o item e uma pasta.</summary>
    public bool IsDirectory { get; init; }
}

/// <summary>
/// Um valor de registro no retrato de um sistema.
/// </summary>
public sealed class SnapshotRegistryValueState
{
    /// <summary>
    /// Caminho canonico da chave, no mesmo formato do regedit.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Nome do valor. Vazio quando o valor e o padrao da chave.
    /// </summary>
    public string ValueName { get; init; } = string.Empty;

    /// <summary>
    /// O conteudo do valor, como texto. Vazio quando o valor nao e de texto.
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// O tipo do valor no registro (String, DWord, QWord, ...).
    /// </summary>
    public string ValueKind { get; init; } = string.Empty;
}

/// <summary>
/// O retrato de um sistema: arquivos, pastas, chaves e valores de registro.
///
/// E o insumo da comparacao A×B. Ao contrario da fotografia efemera
/// (<see cref="SystemSnapshot"/>), o retrato e serializavel e sobrevive a
/// operacao — e por isso que a biblioteca de snapshots nomeados existe.
/// </summary>
public sealed class SnapshotState
{
    /// <summary>Os arquivos capturados.</summary>
    public IReadOnlyList<SnapshotFileState> Files { get; init; } = Array.Empty<SnapshotFileState>();

    /// <summary>As pastas capturadas.</summary>
    public IReadOnlyList<SnapshotFileState> Folders { get; init; } = Array.Empty<SnapshotFileState>();

    /// <summary>As chaves de registro capturadas.</summary>
    public IReadOnlyList<string> RegistryKeys { get; init; } = Array.Empty<string>();

    /// <summary>Os valores de registro capturados.</summary>
    public IReadOnlyList<SnapshotRegistryValueState> RegistryValues { get; init; } = Array.Empty<SnapshotRegistryValueState>();

    /// <summary>
    /// O retrato vazio. Serve de "antes" quando nao ha fotografia anterior.
    /// </summary>
    public static SnapshotState Empty => new();

    /// <summary>
    /// Converte uma fotografia do sistema em retrato.
    ///
    /// A fotografia do sistema (<see cref="SystemSnapshot"/>) guarda o tamanho e
    /// a data dos arquivos, mas nao guarda os VALORES de registro — so o
    /// caminho das chaves. O retrato nasce com esses valores vazios; quem
    /// quiser comparar valores preenche <see cref="RegistryValues"/>.
    /// </summary>
    /// <param name="snapshot">A fotografia do sistema.</param>
    public static SnapshotState FromSnapshot(SystemSnapshot? snapshot)
    {
        if (snapshot is null) return Empty;

        var arquivos = new List<SnapshotFileState>();
        foreach (var par in snapshot.Files)
        {
            arquivos.Add(new SnapshotFileState
            {
                Path = par.Value.Path,
                SizeBytes = par.Value.SizeBytes,
                ModifiedUtc = par.Value.ModifiedUtc,
            });
        }

        var pastas = new List<SnapshotFileState>();
        foreach (var par in snapshot.Folders)
        {
            pastas.Add(new SnapshotFileState { Path = par.Value, IsDirectory = true });
        }

        var chaves = new List<string>();
        foreach (var par in snapshot.RegistryKeys) chaves.Add(par.Value.Path);

        return new SnapshotState
        {
            Files = arquivos,
            Folders = pastas,
            RegistryKeys = chaves,
        };
    }
}

/// <summary>
/// A mudanca de um valor de registro entre dois retratos.
/// </summary>
public sealed class RegistryValueChange
{
    /// <summary>Caminho canonico da chave.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Nome do valor (vazio quando e o valor padrao da chave).</summary>
    public string ValueName { get; init; } = string.Empty;

    /// <summary>O valor anterior (null quando o valor nao existia).</summary>
    public string? OldValue { get; init; }

    /// <summary>O valor novo (null quando o valor deixou de existir).</summary>
    public string? NewValue { get; init; }
}

/// <summary>
/// A comparacao completa entre dois retratos.
///
/// O <see cref="SnapshotDiff"/> legado so expõe o que NACEU. Esta comparacao
/// expoe tambem o que foi REMOVIDO e o que foi MODIFICADO — que e o que
/// permite dizer "o instalador trocou este arquivo" e nao apenas "o instalador
/// criou este arquivo".
/// </summary>
public sealed class SnapshotComparison
{
    /// <summary>Cria a comparacao a partir das listas classificadas.</summary>
    /// <param name="newFiles">Arquivos novos.</param>
    /// <param name="removedFiles">Arquivos removidos.</param>
    /// <param name="modifiedFiles">Arquivos modificados.</param>
    /// <param name="unchangedFiles">Arquivos inalterados.</param>
    /// <param name="newFolders">Pastas novas.</param>
    /// <param name="removedFolders">Pastas removidas.</param>
    /// <param name="newRegistryKeys">Chaves novas.</param>
    /// <param name="removedRegistryKeys">Chaves removidas.</param>
    /// <param name="newRegistryValues">Valores novos.</param>
    /// <param name="removedRegistryValues">Valores removidos.</param>
    /// <param name="modifiedRegistryValues">Valores alterados.</param>
    public SnapshotComparison(
        IReadOnlyList<SnapshotFileState> newFiles,
        IReadOnlyList<SnapshotFileState> removedFiles,
        IReadOnlyList<SnapshotFileState> modifiedFiles,
        IReadOnlyList<SnapshotFileState> unchangedFiles,
        IReadOnlyList<SnapshotFileState> newFolders,
        IReadOnlyList<SnapshotFileState> removedFolders,
        IReadOnlyList<string> newRegistryKeys,
        IReadOnlyList<string> removedRegistryKeys,
        IReadOnlyList<SnapshotRegistryValueState> newRegistryValues,
        IReadOnlyList<SnapshotRegistryValueState> removedRegistryValues,
        IReadOnlyList<RegistryValueChange> modifiedRegistryValues)
    {
        NewFiles = newFiles;
        RemovedFiles = removedFiles;
        ModifiedFiles = modifiedFiles;
        UnchangedFiles = unchangedFiles;
        NewFolders = newFolders;
        RemovedFolders = removedFolders;
        NewRegistryKeys = newRegistryKeys;
        RemovedRegistryKeys = removedRegistryKeys;
        NewRegistryValues = newRegistryValues;
        RemovedRegistryValues = removedRegistryValues;
        ModifiedRegistryValues = modifiedRegistryValues;
    }

    /// <summary>Arquivos que nao existiam antes.</summary>
    public IReadOnlyList<SnapshotFileState> NewFiles { get; }

    /// <summary>Arquivos que existiam antes e nao existem mais.</summary>
    public IReadOnlyList<SnapshotFileState> RemovedFiles { get; }

    /// <summary>Arquivos que existem nos dois, mas mudaram de tamanho ou de data.</summary>
    public IReadOnlyList<SnapshotFileState> ModifiedFiles { get; }

    /// <summary>Arquivos que existem nos dois sem nenhuma mudanca.</summary>
    public IReadOnlyList<SnapshotFileState> UnchangedFiles { get; }

    /// <summary>Pastas que nao existiam antes.</summary>
    public IReadOnlyList<SnapshotFileState> NewFolders { get; }

    /// <summary>Pastas que existiam antes e nao existem mais.</summary>
    public IReadOnlyList<SnapshotFileState> RemovedFolders { get; }

    /// <summary>Chaves de registro que nao existiam antes.</summary>
    public IReadOnlyList<string> NewRegistryKeys { get; }

    /// <summary>Chaves de registro que existiam antes e nao existem mais.</summary>
    public IReadOnlyList<string> RemovedRegistryKeys { get; }

    /// <summary>Valores de registro que nao existiam antes.</summary>
    public IReadOnlyList<SnapshotRegistryValueState> NewRegistryValues { get; }

    /// <summary>Valores de registro que existiam antes e nao existem mais.</summary>
    public IReadOnlyList<SnapshotRegistryValueState> RemovedRegistryValues { get; }

    /// <summary>Valores de registro que existem nos dois, mas com conteudo diferente.</summary>
    public IReadOnlyList<RegistryValueChange> ModifiedRegistryValues { get; }

    /// <summary>
    /// True quando nada mudou entre os dois retratos.
    /// </summary>
    public bool IsEmpty =>
        NewFiles.Count == 0 && RemovedFiles.Count == 0 && ModifiedFiles.Count == 0 &&
        NewFolders.Count == 0 && RemovedFolders.Count == 0 &&
        NewRegistryKeys.Count == 0 && RemovedRegistryKeys.Count == 0 &&
        NewRegistryValues.Count == 0 && RemovedRegistryValues.Count == 0 &&
        ModifiedRegistryValues.Count == 0;
}

/// <summary>
/// A comparacao A×B entre dois retratos do sistema.
///
/// E o motor que responde a pergunta do fluxo de snapshot: "o que o
/// instalador fez?". O <see cref="SystemSnapshot.Diff"/> legado responde
/// apenas "o que nasceu" — e nao cobre modificados nem removidos.
/// </summary>
public static class SnapshotComparer
{
    /// <summary>
    /// Compara dois retratos e classifica o que mudou.
    /// </summary>
    /// <param name="before">O retrato de antes.</param>
    /// <param name="after">O retrato de depois.</param>
    public static SnapshotComparison Compare(SnapshotState? before, SnapshotState? after)
    {
        var antes = before ?? SnapshotState.Empty;
        var depois = after ?? SnapshotState.Empty;

        var antesArquivos = IndexarArquivos(antes.Files);
        var depoisArquivos = IndexarArquivos(depois.Files);

        var novos = new List<SnapshotFileState>();
        var removidos = new List<SnapshotFileState>();
        var modificados = new List<SnapshotFileState>();
        var inalterados = new List<SnapshotFileState>();

        foreach (var par in depoisArquivos)
        {
            if (!antesArquivos.TryGetValue(par.Key, out var anterior))
            {
                novos.Add(par.Value);
                continue;
            }

            if (Mudou(anterior, par.Value)) modificados.Add(par.Value);
            else inalterados.Add(par.Value);
        }

        foreach (var par in antesArquivos)
        {
            if (depoisArquivos.ContainsKey(par.Key)) continue;
            removidos.Add(par.Value);
        }

        var antesPastas = IndexarArquivos(antes.Folders);
        var depoisPastas = IndexarArquivos(depois.Folders);

        var pastasNovas = new List<SnapshotFileState>();
        var pastasRemovidas = new List<SnapshotFileState>();

        foreach (var par in depoisPastas)
        {
            if (antesPastas.ContainsKey(par.Key)) continue;
            pastasNovas.Add(par.Value);
        }

        foreach (var par in antesPastas)
        {
            if (depoisPastas.ContainsKey(par.Key)) continue;
            pastasRemovidas.Add(par.Value);
        }

        var antesChaves = IndexarChaves(antes.RegistryKeys);
        var depoisChaves = IndexarChaves(depois.RegistryKeys);

        var chavesNovas = new List<string>();
        var chavesRemovidas = new List<string>();

        foreach (var par in depoisChaves)
        {
            if (antesChaves.ContainsKey(par.Key)) continue;
            chavesNovas.Add(par.Value);
        }

        foreach (var par in antesChaves)
        {
            if (depoisChaves.ContainsKey(par.Key)) continue;
            chavesRemovidas.Add(par.Value);
        }

        var (valoresNovos, valoresRemovidos, valoresAlterados) = CompararValores(
            antes.RegistryValues, depois.RegistryValues);

        return new SnapshotComparison(
            Ordenar(novos), Ordenar(removidos), Ordenar(modificados), Ordenar(inalterados),
            Ordenar(pastasNovas), Ordenar(pastasRemovidas),
            OrdenarTexto(chavesNovas), OrdenarTexto(chavesRemovidas),
            OrdenarValores(valoresNovos), OrdenarValores(valoresRemovidos), valoresAlterados);
    }

    /// <summary>
    /// Compara duas fotografias do sistema (o tipo que o fluxo de instalacao
    /// ja produz). E o mesmo motor, alimentado pelo retrato derivado.
    /// </summary>
    /// <param name="before">A fotografia de antes.</param>
    /// <param name="after">A fotografia de depois.</param>
    public static SnapshotComparison Compare(SystemSnapshot? before, SystemSnapshot? after) =>
        Compare(SnapshotState.FromSnapshot(before), SnapshotState.FromSnapshot(after));

    /// <summary>
    /// True quando o arquivo mudou de tamanho ou de data.
    /// </summary>
    private static bool Mudou(SnapshotFileState anterior, SnapshotFileState atual) =>
        anterior.SizeBytes != atual.SizeBytes || anterior.ModifiedUtc != atual.ModifiedUtc;

    /// <summary>
    /// Indexa os arquivos pelo caminho em MAIUSCULAS (a chave de comparacao
    /// do Tranal).
    /// </summary>
    private static Dictionary<string, SnapshotFileState> IndexarArquivos(IReadOnlyList<SnapshotFileState>? arquivos)
    {
        var indice = new Dictionary<string, SnapshotFileState>(StringComparer.Ordinal);
        if (arquivos is null) return indice;

        foreach (var arquivo in arquivos)
        {
            if (arquivo is null) continue;
            indice[arquivo.Path.ToUpperInvariant()] = arquivo;
        }

        return indice;
    }

    /// <summary>
    /// Indexa as chaves de registro pelo caminho canonico em MAIUSCULAS.
    /// </summary>
    private static Dictionary<string, string> IndexarChaves(IReadOnlyList<string>? chaves)
    {
        var indice = new Dictionary<string, string>(StringComparer.Ordinal);
        if (chaves is null) return indice;

        foreach (var chave in chaves)
        {
            if (string.IsNullOrWhiteSpace(chave)) continue;
            indice[chave.ToUpperInvariant()] = chave;
        }

        return indice;
    }

    /// <summary>
    /// Compara os valores de registro: novos, removidos e alterados.
    /// </summary>
    private static (List<SnapshotRegistryValueState> Novos,
                    List<SnapshotRegistryValueState> Removidos,
                    List<RegistryValueChange> Alterados) CompararValores(
        IReadOnlyList<SnapshotRegistryValueState>? antes,
        IReadOnlyList<SnapshotRegistryValueState>? depois)
    {
        var antesIndice = new Dictionary<string, SnapshotRegistryValueState>(StringComparer.Ordinal);
        if (antes is not null)
        {
            foreach (var valor in antes)
            {
                if (valor is null) continue;
                antesIndice[ChaveValor(valor)] = valor;
            }
        }

        var depoisIndice = new Dictionary<string, SnapshotRegistryValueState>(StringComparer.Ordinal);
        if (depois is not null)
        {
            foreach (var valor in depois)
            {
                if (valor is null) continue;
                depoisIndice[ChaveValor(valor)] = valor;
            }
        }

        var novos = new List<SnapshotRegistryValueState>();
        var removidos = new List<SnapshotRegistryValueState>();
        var alterados = new List<RegistryValueChange>();

        foreach (var par in depoisIndice)
        {
            if (!antesIndice.TryGetValue(par.Key, out var anterior))
            {
                novos.Add(par.Value);
                continue;
            }

            if (string.Equals(anterior.Value, par.Value.Value, StringComparison.Ordinal)) continue;

            alterados.Add(new RegistryValueChange
            {
                Key = par.Value.Key,
                ValueName = par.Value.ValueName,
                OldValue = anterior.Value,
                NewValue = par.Value.Value,
            });
        }

        foreach (var par in antesIndice)
        {
            if (depoisIndice.ContainsKey(par.Key)) continue;
            removidos.Add(par.Value);
        }

        return (novos, removidos, alterados);
    }

    /// <summary>
    /// A chave de um valor de registro: chave + nome do valor.
    /// </summary>
    private static string ChaveValor(SnapshotRegistryValueState valor) =>
        (valor.Key ?? string.Empty).ToUpperInvariant() + "|" + (valor.ValueName ?? string.Empty).ToUpperInvariant();

    /// <summary>
    /// Ordena por caminho, para que a comparacao seja sempre a mesma.
    /// </summary>
    private static List<SnapshotFileState> Ordenar(List<SnapshotFileState> itens) =>
        itens.OrderBy(i => i.Path, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// Ordena por caminho, para que a comparacao seja sempre a mesma.
    /// </summary>
    private static List<string> OrdenarTexto(List<string> itens) =>
        itens.OrderBy(i => i, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// Ordena os valores de registro por chave e nome, para que a comparacao
    /// seja sempre a mesma.
    /// </summary>
    private static List<SnapshotRegistryValueState> OrdenarValores(List<SnapshotRegistryValueState> itens) =>
        itens.OrderBy(i => i.Key + "|" + i.ValueName, StringComparer.OrdinalIgnoreCase).ToList();
}
