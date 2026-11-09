using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Recovery;

/// <summary>
/// O perfil de um programa: o que ele gravou no computador.
///
/// O <see cref="BackupManager"/> restaura item a item; o perfil e o nivel
/// acima: o programa inteiro, com os arquivos e os valores de registro que
/// pertencem a ele, para devolver de uma vez.
/// </summary>
public sealed class ProgramProfile
{
    /// <summary>O nome do programa.</summary>
    public string ProgramName { get; set; } = string.Empty;

    /// <summary>A versao do programa.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>A pasta de instalacao.</summary>
    public string InstallLocation { get; set; } = string.Empty;

    /// <summary>Quando o perfil foi criado (UTC).</summary>
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Os arquivos que o programa gravou.</summary>
    public List<string> Files { get; set; } = new();

    /// <summary>As pastas que o programa gravou.</summary>
    public List<string> Folders { get; set; } = new();

    /// <summary>Os valores de registro que o programa gravou.</summary>
    public List<RegistryValueBackup> RegistryValues { get; set; } = new();

    /// <summary>Uma linha que descreve o perfil.</summary>
    public string Describe() =>
        $"{ProgramName} {Version} — {Files.Count} arquivo(s), {Folders.Count} pasta(s), "
        + $"{RegistryValues.Count} valor(es) de registro";
}

/// <summary>
/// O backup do programa com restauracao completa e undo/redo.
///
/// Este e o item 33 do backlog ("Backup do programa com restauracao completa
/// e undo/redo"): o <see cref="BackupManager"/> ja existia, mas so restaurava
/// item a item — falta o nivel de programa.
/// </summary>
public static class ProgramBackup
{
    /// <summary>
    /// Substitui a pasta dos perfis e das operacoes (so a suite de testes).
    /// </summary>
    internal static string? FolderOverride { get; set; }

    /// <summary>A pasta onde ficam os perfis e as operacoes.</summary>
    public static string Folder =>
        FolderOverride ?? Path.Combine(RegistryBackup.DataDirectory, "backups", "programs");

    /// <summary>A pasta dos perfis.</summary>
    public static string ProfileFolder => Path.Combine(Folder, "profiles");

    /// <summary>O arquivo das operacoes (undo/redo).</summary>
    public static string OperationsFile => Path.Combine(Folder, "operations.jsonl");

    /// <summary>
    /// Monta o perfil do programa a partir dos itens que ele gravou.
    /// </summary>
    /// <param name="program">O programa.</param>
    /// <param name="items">Os itens atribuidos ao programa.</param>
    public static ProgramProfile CreateProfile(InstalledProgram program, IEnumerable<LeftoverItem> items)
    {
        var perfil = new ProgramProfile
        {
            ProgramName = program?.DisplayName ?? string.Empty,
            Version = program?.DisplayVersion ?? string.Empty,
            InstallLocation = program?.InstallLocation ?? string.Empty,
        };

        foreach (var item in items ?? Enumerable.Empty<LeftoverItem>())
        {
            if (item is null) continue;

            var tipo = (item.Kind ?? string.Empty).Trim();

            if (string.Equals(tipo, "Registro", StringComparison.OrdinalIgnoreCase))
            {
                var valor = RegistryValueBackupFactory.Read(item.Path);
                if (valor is not null) perfil.RegistryValues.Add(valor);
            }
            else if (string.Equals(tipo, "Pasta", StringComparison.OrdinalIgnoreCase))
            {
                if (!perfil.Folders.Contains(item.Path)) perfil.Folders.Add(item.Path);
            }
            else if (string.Equals(tipo, "Arquivo", StringComparison.OrdinalIgnoreCase))
            {
                if (!perfil.Files.Contains(item.Path)) perfil.Files.Add(item.Path);
            }
        }

        return perfil;
    }

    /// <summary>
    /// Grava o perfil do programa.
    /// </summary>
    /// <param name="perfil">O perfil.</param>
    /// <returns>O caminho do arquivo gravado, ou vazio quando falhou.</returns>
    public static string SaveProfile(ProgramProfile perfil)
    {
        if (perfil is null) return string.Empty;

        try
        {
            Directory.CreateDirectory(ProfileFolder);

            var nome = Sanear(perfil.ProgramName);
            var arquivo = Path.Combine(ProfileFolder, $"{nome}.json");

            File.WriteAllText(arquivo, JsonSerializer.Serialize(perfil, JsonOptions), EncodingUtf8);
            return arquivo;
        }
        catch (Exception) { return string.Empty; }
    }

    /// <summary>
    /// Os perfis gravados.
    /// </summary>
    public static IReadOnlyList<ProgramProfile> ListProfiles()
    {
        var lista = new List<ProgramProfile>();

        try
        {
            if (!Directory.Exists(ProfileFolder)) return lista;

            foreach (var arquivo in Directory.EnumerateFiles(ProfileFolder, "*.json"))
            {
                try
                {
                    var perfil = JsonSerializer.Deserialize<ProgramProfile>(
                        File.ReadAllText(arquivo, EncodingUtf8), JsonOptions);
                    if (perfil is not null) lista.Add(perfil);
                }
                catch (JsonException) { /* um perfil ruim nao derruba os outros */ }
            }
        }
        catch (Exception) { return lista; }

        lista.Sort((a, b) => b.CreatedUtc.CompareTo(a.CreatedUtc));
        return lista;
    }

    /// <summary>
    /// Apaga o perfil de um programa.
    /// </summary>
    /// <param name="programName">O nome do programa.</param>
    public static bool DeleteProfile(string? programName)
    {
        if (string.IsNullOrWhiteSpace(programName)) return false;

        try
        {
            var arquivo = Path.Combine(ProfileFolder, Sanear(programName) + ".json");
            if (!File.Exists(arquivo)) return false;

            File.Delete(arquivo);
            return true;
        }
        catch (Exception) { return false; }
    }

    /// <summary>
    /// Registra uma operacao de remocao para poder ser desfeita.
    /// </summary>
    /// <param name="operacao">A operacao.</param>
    public static bool Record(RecoverOperation operacao)
    {
        if (operacao is null) return false;

        try
        {
            Directory.CreateDirectory(Folder);

            if (operacao.Id.Length == 0)
                operacao.Id = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}";

            File.AppendAllText(OperationsFile, operacao.ToJson() + Environment.NewLine, EncodingUtf8);
            return true;
        }
        catch (Exception) { return false; }
    }

    /// <summary>
    /// As operacoes registradas, da mais nova para a mais antiga.
    /// </summary>
    /// <param name="limit">Quantas devolver. Zero: todas.</param>
    public static IReadOnlyList<RecoverOperation> Operations(int limit = 0)
    {
        var lista = new List<RecoverOperation>();

        try
        {
            if (!File.Exists(OperationsFile)) return lista;

            foreach (var linha in File.ReadAllLines(OperationsFile, EncodingUtf8))
            {
                if (string.IsNullOrWhiteSpace(linha)) continue;

                var operacao = RecoverOperation.FromJson(linha);
                if (operacao is not null) lista.Add(operacao);
            }
        }
        catch (Exception) { return lista; }

        lista.Reverse();

        return limit > 0 && lista.Count > limit ? lista.GetRange(0, limit) : lista;
    }

    /// <summary>
    /// Desfaz a operacao: os arquivos voltam e os valores de registro sao
    /// reescritos.
    /// </summary>
    /// <param name="operacao">A operacao.</param>
    public static (bool Ok, string Message) Undo(RecoverOperation operacao)
    {
        if (operacao is null) return (false, "operacao invalida");
        if (operacao.Undone) return (false, "a operacao ja foi desfeita");

        // A operacao sabe onde esta o backup: e de la que o conteudo volta.
        var (ok, mensagem) = RestaurarDaPasta(operacao);
        if (!ok)
        {
            // Sem a pasta, tenta pelo identificador do backup.
            var (ok2, mensagem2) = LeftoverBackup.Restore(operacao.Id);
            if (!ok2) return (false, mensagem.Length > 0 ? mensagem : mensagem2);

            mensagem = mensagem2;
        }

        operacao.Undone = true;
        GravarOperacoes();

        return (true, $"Operacao desfeita. {mensagem}");
    }

    /// <summary>
    /// Refaz a operacao: os arquivos voltam a ser removidos e os valores de
    /// registro sao apagados de novo.
    /// </summary>
    /// <param name="operacao">A operacao.</param>
    public static (bool Ok, string Message) Redo(RecoverOperation operacao)
    {
        if (operacao is null) return (false, "operacao invalida");
        if (!operacao.Undone) return (false, "a operacao nao esta desfeita");

        var apagados = 0;

        foreach (var item in ItensDaOperacao(operacao))
        {
            try
            {
                if (string.Equals(item.Kind, "Arquivo", StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(item.Path)) { File.Delete(item.Path); apagados++; }
                }
                else if (string.Equals(item.Kind, "Pasta", StringComparison.OrdinalIgnoreCase))
                {
                    if (Directory.Exists(item.Path)) { Directory.Delete(item.Path, recursive: true); apagados++; }
                }
                else if (string.Equals(item.Kind, "Registro", StringComparison.OrdinalIgnoreCase))
                {
                    if (ApagarValor(item.Value)) apagados++;
                }
            }
            catch (Exception) { /* um item que falha nao impede os outros */ }
        }

        operacao.Undone = false;
        GravarOperacoes();

        return (true, $"{apagados} item(ns) removido(s) de novo");
    }

    /// <summary>
    /// Os itens da operacao: os que a propria operacao carrega ou, quando
    /// ela nao carrega, os do manifesto do backup (e onde eles estao, de
    /// verdade).
    /// </summary>
    private static IReadOnlyList<LeftoverBackupItem> ItensDaOperacao(RecoverOperation operacao)
    {
        if (operacao.Items.Count > 0) return operacao.Items;

        if (!string.IsNullOrWhiteSpace(operacao.BackupFolder))
        {
            var manifesto = LeftoverBackup.LoadManifest(operacao.BackupFolder);
            if (manifesto is not null) return manifesto.Items;
        }

        return Array.Empty<LeftoverBackupItem>();
    }

    private static (bool Ok, string Message) RestaurarDaPasta(RecoverOperation operacao)
    {
        if (string.IsNullOrWhiteSpace(operacao.BackupFolder)) return (false, "a operacao nao tem backup");

        var manifesto = LeftoverBackup.LoadManifest(operacao.BackupFolder);
        if (manifesto is null) return (false, "backup nao encontrado ou ilegive");

        var devolvidos = 0;

        foreach (var item in manifesto.Items)
        {
            try
            {
                if (string.Equals(item.Kind, "Arquivo", StringComparison.OrdinalIgnoreCase))
                {
                    // A extracao mora no backup: e ele que sabe se o
                    // arquivo foi guardado comprimido.
                    if (LeftoverBackup.ExtractStored(operacao.BackupFolder, item.StoredPath ?? string.Empty, item.Path))
                        devolvidos++;
                }
                else if (string.Equals(item.Kind, "Pasta", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var guardado in (item.StoredPath ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var destino = Path.Combine(item.Path, LeftoverBackup.OriginalNameOf(guardado));

                        if (LeftoverBackup.ExtractStored(operacao.BackupFolder, guardado, destino))
                            devolvidos++;
                    }
                }
                else if (string.Equals(item.Kind, "Registro", StringComparison.OrdinalIgnoreCase))
                {
                    if (item.Value is null) continue;
                    if (RegistryValueBackupFactory.Write(item.Value)) devolvidos++;
                }
            }
            catch (Exception) { /* segue para o proximo item */ }
        }

        return (devolvidos > 0, $"{devolvidos} item(ns) devolvido(s)");
    }

    private static bool ApagarValor(RegistryValueBackup? valor)
    {
        if (valor is null) return false;

        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(valor.KeyPath);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(sub, writable: true);
            if (key is null) return false;

            key.DeleteValue(valor.Name, throwOnMissingValue: false);
            return true;
        }
        catch (Exception) { return false; }
    }

    private static void GravarOperacoes()
    {
        try
        {
            var linhas = Operations().Select(o => o.ToJson()).ToList();
            File.WriteAllText(OperationsFile, string.Join(Environment.NewLine, linhas), EncodingUtf8);
        }
        catch (Exception) { /* o registro de operacoes nunca trava a operacao */ }
    }

    private static string Sanear(string nome)
    {
        var limpo = new string((nome ?? string.Empty)
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
            .ToArray());

        return limpo.Length == 0 ? "programa" : (limpo.Length > 40 ? limpo[..40] : limpo);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Encoding EncodingUtf8 = new UTF8Encoding(false);
}

/// <summary>
/// A leitura e a escrita dos valores de registro que o backup guarda.
///
/// Vive fora das classes de backup para que o <see cref="LeftoverBackup"/> e o
/// <see cref="ProgramBackup"/> usem a MESMA regra de tipo de valor: um valor
/// binario que volta como texto deixa de ser binario, e a chave restaurada
/// deixa de servir para nada.
/// </summary>
public static class RegistryValueBackupFactory
{
    /// <summary>
    /// Le o valor de uma chave de registro.
    /// </summary>
    /// <param name="caminhoChave">O caminho canonico da chave.</param>
    public static RegistryValueBackup? Read(string? caminhoChave)
    {
        if (string.IsNullOrWhiteSpace(caminhoChave)) return null;

        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(caminhoChave);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(sub);
            if (key is null) return null;

            var nomes = key.GetValueNames();
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

    /// <summary>
    /// Escreve de volta um valor de registro, com o tipo que ele tinha.
    /// </summary>
    /// <param name="valor">O valor guardado.</param>
    public static bool Write(RegistryValueBackup valor)
    {
        if (valor is null) return false;

        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(valor.KeyPath);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.CreateSubKey(sub, writable: true);
            if (key is null) return false;

            var kind = Enum.TryParse<RegistryValueKind>(valor.Kind, out var k) ? k : RegistryValueKind.String;

            switch (kind)
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

    private static string Converter(object? valor, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.Binary => Convert.ToHexString((byte[])(valor ?? Array.Empty<byte>())),
        RegistryValueKind.MultiString => string.Join("|", (string[])(valor ?? Array.Empty<string>())),
        _ => valor?.ToString() ?? string.Empty,
    };
}
