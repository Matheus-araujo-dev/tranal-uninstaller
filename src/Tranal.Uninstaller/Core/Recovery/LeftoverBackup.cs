using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using Tranal.Uninstaller.Core.Settings;

namespace Tranal.Uninstaller.Core.Recovery;

/// <summary>
/// Um item guardado no backup das sobras.
/// </summary>
public sealed class LeftoverBackupItem
{
    /// <summary>"Arquivo", "Pasta" ou "Registro".</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>O caminho original do item.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Onde o conteudo ficou guardado, dentro da pasta do backup.
    /// Vazio para itens de registro: o valor fica no proprio manifesto.
    /// </summary>
    public string StoredPath { get; set; } = string.Empty;

    /// <summary>O valor do registro, quando o item e de registro.</summary>
    public RegistryValueBackup? Value { get; set; }
}

/// <summary>
/// Um valor de registro guardado para poder ser devolvido.
/// </summary>
public sealed class RegistryValueBackup
{
    /// <summary>O caminho canonico da chave.</summary>
    public string KeyPath { get; set; } = string.Empty;

    /// <summary>O nome do valor. Vazio: o valor padrao da chave.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>O tipo real do valor (o <c>GetValueKind</c>, nao o do objeto).</summary>
    public string Kind { get; set; } = "String";

    /// <summary>
    /// O conteudo do valor, como texto. Para binario, em hexadecimal.
    /// </summary>
    public string Data { get; set; } = string.Empty;
}

/// <summary>
/// O manifesto de um backup de sobras.
/// </summary>
public sealed class LeftoverBackupManifest
{
    /// <summary>O identificador do backup.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>O nome do programa que gerou as sobras.</summary>
    public string ProgramName { get; set; } = string.Empty;

    /// <summary>Quando o backup foi feito (UTC).</summary>
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Os itens guardados.</summary>
    public List<LeftoverBackupItem> Items { get; set; } = new();
}

/// <summary>
/// O resultado de um backup de sobras.
/// </summary>
public sealed class LeftoverBackupResult
{
    /// <summary>True quando o backup foi gravado.</summary>
    public bool Ok { get; init; }

    /// <summary>O identificador do backup.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>A pasta do backup.</summary>
    public string Folder { get; init; } = string.Empty;

    /// <summary>Quantos itens foram guardados.</summary>
    public int ItemCount { get; init; }

    /// <summary>A mensagem, quando deu errado.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// O backup das sobras ANTES de apaga-las.
///
/// Hoje o Tranal faz backup do registro, nao dos residues: quando a limpeza
/// apaga a pasta de um programa, o conteudo vai embora sem volta. O Wise
/// pede exatamente este item ("backup dos residuos antes de apaga-los, com
/// expiracao").
///
/// O backup tem validade: o que passa do prazo e apagado, porque backup que
/// nunca expira enche a maquina sem ninguem pedir.
/// </summary>
public static class LeftoverBackup
{
    /// <summary>
    /// Substitui a pasta dos backups (so a suite de testes).
    /// </summary>
    internal static string? FolderOverride { get; set; }

    /// <summary>A pasta onde ficam os backups das sobras.</summary>
    public static string Folder =>
        FolderOverride ?? Path.Combine(RegistryBackup.DataDirectory, "backups", "leftovers");

    /// <summary>
    /// Guarda as sobras antes de elas serem apagadas.
    /// </summary>
    /// <param name="items">Os itens de sobra que serao removidos.</param>
    /// <param name="programName">O nome do programa (para o backup ser reconhecivel).</param>
    /// <param name="daysToLive">A validade do backup, em dias.</param>
    public static LeftoverBackupResult Backup(
        IEnumerable<LeftoverItem> items,
        string programName,
        int daysToLive = 30)
    {
        var lista = items?.ToList() ?? new List<LeftoverItem>();
        if (lista.Count == 0)
            return new LeftoverBackupResult { Ok = false, Message = "nao ha itens para guardar" };

        var id = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Sanear(programName)}";
        var pasta = Path.Combine(Folder, id);
        var manifesto = new LeftoverBackupManifest
        {
            Id = id,
            ProgramName = programName,
            CreatedUtc = DateTime.UtcNow,
        };

        try
        {
            Directory.CreateDirectory(pasta);

            var indice = 0;
            foreach (var item in lista)
            {
                indice++;
                var guardado = Guardar(item, pasta, indice);
                if (guardado is not null) manifesto.Items.Add(guardado);
            }

            manifesto.Items.Add(new LeftoverBackupItem
            {
                Kind = "Validade",
                Path = daysToLive.ToString(),
            });

            var json = JsonSerializer.Serialize(manifesto, JsonOptions);
            File.WriteAllText(Path.Combine(pasta, "manifest.json"), json, EncodingUtf8);

            // O limite da pasta de snapshots: o Total Uninstall tem
            // "Limits for Snapshots folder". A REGRA mora no modulo de
            // limites; aqui e so a chamada, depois do backup gravado.
            SnapshotLimits.Enforce();

            return new LeftoverBackupResult
            {
                Ok = true,
                Id = id,
                Folder = pasta,
                ItemCount = manifesto.Items.Count(i => i.Kind is "Arquivo" or "Pasta" or "Registro"),
            };
        }
        catch (Exception ex)
        {
            return new LeftoverBackupResult { Ok = false, Folder = pasta, Message = ex.Message };
        }
    }

    /// <summary>
    /// Guarda um item: o arquivo e copiado, o valor de registro e lido.
    /// </summary>
    private static LeftoverBackupItem? Guardar(LeftoverItem item, string pasta, int indice)
    {
        if (item is null) return null;

        var tipo = (item.Kind ?? string.Empty).Trim();

        if (string.Equals(tipo, "Registro", StringComparison.OrdinalIgnoreCase))
        {
            var valor = LerValor(item.Path);
            if (valor is null) return null;

            return new LeftoverBackupItem
            {
                Kind = "Registro",
                Path = item.Path,
                Value = valor,
            };
        }

        if (string.Equals(tipo, "Pasta", StringComparison.OrdinalIgnoreCase))
        {
            // A pasta e guardada como lista de arquivos: e o conteudo que
            // importa, e o que a limpeza apaga.
            var guardados = new List<string>();
            try
            {
                if (Directory.Exists(item.Path))
                {
                    foreach (var arquivo in Directory.EnumerateFiles(item.Path, "*", SearchOption.AllDirectories))
                    {
                        var relativo = CopiarArquivo(arquivo, pasta, indice);
                        if (relativo is not null) guardados.Add(relativo);
                    }
                }
            }
            catch (Exception) { /* o que nao deu para ler fica de fora */ }

            return new LeftoverBackupItem
            {
                Kind = "Pasta",
                Path = item.Path,
                StoredPath = string.Join(";", guardados),
            };
        }

        if (string.Equals(tipo, "Arquivo", StringComparison.OrdinalIgnoreCase))
        {
            var relativo = CopiarArquivo(item.Path, pasta, indice);
            if (relativo is null) return null;

            return new LeftoverBackupItem
            {
                Kind = "Arquivo",
                Path = item.Path,
                StoredPath = relativo,
            };
        }

        return null;
    }

    private static string? CopiarArquivo(string caminho, string pasta, int indice)
    {
        try
        {
            if (!File.Exists(caminho)) return null;

            var nome = $"{indice:D4}-{Path.GetFileName(caminho)}";
            var sub = Path.Combine(pasta, "files");
            Directory.CreateDirectory(sub);

            // A compressao Brotli (BCU) reduz bastante o backup do que
            // sobra de programa: arquivos de texto e de configuracao.
            // O sufixo ".br" no arquivo guardado e o que diz, na hora de
            // devolver, que ele precisa ser descomprimido.
            var comprimir = AppSettingsStore.Current.CompressBackups;
            var destino = Path.Combine(sub, comprimir ? nome + ".br" : nome);

            if (comprimir)
            {
                using var entrada = File.OpenRead(caminho);
                using var saida = File.Create(destino);
                using var brotli = new BrotliStream(saida, CompressionLevel.Optimal, leaveOpen: true);
                entrada.CopyTo(brotli);
            }
            else
            {
                File.Copy(caminho, destino, overwrite: true);
            }

            return Path.GetFileName(destino);
        }
        catch (Exception) { return null; }
    }

    /// <summary>
    /// Devolve um arquivo guardado ao lugar original, descomprimindo quando
    /// ele foi guardado comprimido.
    ///
    /// A REGRA mora aqui, em um lugar so: quem devolve um arquivo do backup
    /// chama este metodo. Antes, cada restaurador copiava o arquivo do
    /// backup por conta propria — e o que sabia comprimir nao era o que
    /// sabia descomprimir.
    /// </summary>
    /// <param name="pasta">A pasta do backup.</param>
    /// <param name="guardado">O nome do arquivo dentro do backup.</param>
    /// <param name="destino">Onde o arquivo vai.</param>
    public static bool ExtractStored(string pasta, string guardado, string destino)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(pasta) || string.IsNullOrWhiteSpace(guardado)) return false;

            var origem = Path.Combine(pasta, "files", guardado);
            if (!File.Exists(origem)) return false;

            var sub = Path.GetDirectoryName(destino);
            if (!string.IsNullOrEmpty(sub)) Directory.CreateDirectory(sub);

            if (guardado.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
            {
                using var entrada = File.OpenRead(origem);
                using var saida = File.Create(destino);
                using var brotli = new BrotliStream(entrada, CompressionMode.Decompress);
                brotli.CopyTo(saida);
                return true;
            }

            File.Copy(origem, destino, overwrite: true);
            return true;
        }
        catch (Exception) { return false; }
    }

    /// <summary>
    /// O nome original do arquivo, a partir do nome com que ele foi guardado.
    ///
    /// O nome guardado tem o indice e o sufixo ".br" da compressao; nenhum
    /// dos dois faz parte do nome do arquivo do usuario.
    /// </summary>
    /// <param name="guardado">O nome do arquivo dentro do backup.</param>
    public static string OriginalNameOf(string guardado)
    {
        var nome = Path.GetFileName(guardado ?? string.Empty);
        var indice = nome.IndexOf('-');
        var original = indice > 0 ? nome[(indice + 1)..] : nome;

        if (original.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
            original = original[..^3];

        return original;
    }

    /// <summary>
    /// Le o valor de uma chave de registro para poder devolvê-lo.
    /// </summary>
    private static RegistryValueBackup? LerValor(string caminhoChave)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(caminhoChave)) return null;

            var (hive, sub) = SafetyRules.SplitRegistryPath(caminhoChave);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(sub);
            if (key is null) return null;

            var nomes = key.GetValueNames();
            if (nomes.Length == 0) return null;

            // Sem nome de valor: e o valor padrao da chave.
            var nome = nomes.Length == 1 ? nomes[0] : string.Empty;
            var kind = key.GetValueKind(nome);

            return new RegistryValueBackup
            {
                KeyPath = caminhoChave,
                Name = nome,
                Kind = kind.ToString(),
                Data = Converter(key.GetValue(nome, null, RegistryValueOptions.DoNotExpandEnvironmentNames), kind),
            };
        }
        catch (Exception) { return null; }
    }

    private static string Converter(object? valor, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.Binary => Convert.ToHexString((byte[])(valor ?? Array.Empty<byte>())),
        RegistryValueKind.MultiString => string.Join("|", (string[])(valor ?? Array.Empty<string>())),
        _ => valor?.ToString() ?? string.Empty,
    };

    /// <summary>
    /// Devolve as sobras de um backup ao sistema.
    /// </summary>
    /// <param name="id">O identificador do backup.</param>
    public static (bool Ok, string Message) Restore(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return (false, "backup sem identificador");

        var pasta = Path.Combine(Folder, id);
        var manifesto = Carregar(pasta);
        if (manifesto is null) return (false, "backup nao encontrado ou ilegive");

        var devolvidos = 0;
        var falhas = 0;

        foreach (var item in manifesto.Items)
        {
            try
            {
                if (string.Equals(item.Kind, "Arquivo", StringComparison.OrdinalIgnoreCase))
                {
                    if (ExtractStored(pasta, item.StoredPath ?? string.Empty, item.Path)) devolvidos++;
                    else falhas++;
                }
                else if (string.Equals(item.Kind, "Pasta", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var guardado in (item.StoredPath ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var destino = Path.Combine(item.Path, OriginalNameOf(guardado));

                        if (ExtractStored(pasta, guardado, destino)) devolvidos++;
                        else falhas++;
                    }
                }
                else if (string.Equals(item.Kind, "Registro", StringComparison.OrdinalIgnoreCase))
                {
                    if (item.Value is null) { falhas++; continue; }
                    if (GravarValor(item.Value)) devolvidos++;
                    else falhas++;
                }
            }
            catch (Exception) { falhas++; }
        }

        return (falhas == 0,
            $"{devolvidos} item(ns) devolvido(s)" + (falhas > 0 ? $", {falhas} falha(s)" : string.Empty));
    }

    private static bool GravarValor(RegistryValueBackup valor)
    {
        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(valor.KeyPath);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.CreateSubKey(sub, writable: true);
            if (key is null) return false;

            switch (Enum.TryParse<RegistryValueKind>(valor.Kind, out var kind) ? kind : RegistryValueKind.String)
            {
                case RegistryValueKind.Binary:
                    key.SetValue(valor.Name, Convert.FromHexString(valor.Data), RegistryValueKind.Binary);
                    break;
                case RegistryValueKind.DWord:
                    key.SetValue(valor.Name, Convert.ToInt32(valor.Data), RegistryValueKind.DWord);
                    break;
                case RegistryValueKind.QWord:
                    key.SetValue(valor.Name, Convert.ToInt64(valor.Data), RegistryValueKind.QWord);
                    break;
                case RegistryValueKind.MultiString:
                    key.SetValue(valor.Name, valor.Data.Split('|'), RegistryValueKind.MultiString);
                    break;
                case RegistryValueKind.ExpandString:
                    key.SetValue(valor.Name, valor.Data, RegistryValueKind.ExpandString);
                    break;
                default:
                    key.SetValue(valor.Name, valor.Data, RegistryValueKind.String);
                    break;
            }

            return true;
        }
        catch (Exception) { return false; }
    }

    /// <summary>
    /// Os backups que existem, do mais novo para o mais antigo.
    /// </summary>
    public static IReadOnlyList<LeftoverBackupManifest> List()
    {
        var lista = new List<LeftoverBackupManifest>();

        try
        {
            if (!Directory.Exists(Folder)) return lista;

            foreach (var pasta in Directory.EnumerateDirectories(Folder))
            {
                var manifesto = Carregar(pasta);
                if (manifesto is not null) lista.Add(manifesto);
            }
        }
        catch (Exception) { return lista; }

        lista.Sort((a, b) => b.CreatedUtc.CompareTo(a.CreatedUtc));
        return lista;
    }

    /// <summary>
    /// Le o manifesto de um backup.
    ///
    /// E publico porque o <see cref="ProgramBackup"/> restaura a partir da
    /// mesma pasta: o formato do manifesto fica escrito em um lugar só.
    /// </summary>
    /// <param name="pasta">A pasta do backup.</param>
    public static LeftoverBackupManifest? LoadManifest(string pasta)
    {
        try
        {
            var arquivo = Path.Combine(pasta, "manifest.json");
            if (!File.Exists(arquivo)) return null;

            return JsonSerializer.Deserialize<LeftoverBackupManifest>(
                File.ReadAllText(arquivo, EncodingUtf8), JsonOptions);
        }
        catch (Exception) { return null; }
    }

    private static LeftoverBackupManifest? Carregar(string pasta) => LoadManifest(pasta);

    /// <summary>
    /// Apaga os backups que passaram da validade.
    /// </summary>
    /// <param name="days">A validade, em dias. Zero: 30 dias.</param>
    /// <returns>Quantos backups foram apagados.</returns>
    public static int Expire(int days = 30)
    {
        if (days <= 0) days = 30;

        var apagados = 0;
        var limite = DateTime.UtcNow.AddDays(-days);

        try
        {
            if (!Directory.Exists(Folder)) return 0;

            foreach (var pasta in Directory.EnumerateDirectories(Folder))
            {
                var manifesto = Carregar(pasta);
                var criado = manifesto?.CreatedUtc ?? Directory.GetCreationTimeUtc(pasta);
                if (criado >= limite) continue;

                try
                {
                    Directory.Delete(pasta, recursive: true);
                    apagados++;
                }
                catch (Exception) { /* backup em uso: fica para a proxima vez */ }
            }
        }
        catch (Exception) { return apagados; }

        return apagados;
    }

    /// <summary>
    /// Apaga um backup especifico.
    /// </summary>
    /// <param name="id">O identificador do backup.</param>
    public static bool Delete(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        try
        {
            var pasta = Path.Combine(Folder, id);
            if (!Directory.Exists(pasta)) return false;

            Directory.Delete(pasta, recursive: true);
            return true;
        }
        catch (Exception) { return false; }
    }

    /// <summary>Um nome de pasta seguro para o programa.</summary>
    private static string Sanear(string nome)
    {
        var limpo = new string((nome ?? string.Empty)
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
            .ToArray());

        if (limpo.Length == 0) limpo = "programa";
        return limpo.Length > 40 ? limpo[..40] : limpo;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Encoding EncodingUtf8 = new UTF8Encoding(false);
}
