using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Tranal.Uninstaller.Core.Settings;

/// <summary>
/// Um programa guardado no cache da ultima varredura.
///
/// O cache guarda **o minimo** para a tela mostrar a lista antes da varredura
/// terminar: nome, versao, editora, local, comando de desinstalacao e o
/// tamanho. Nao e uma copia da lista — e um resumo, e por isso ele nao
/// cresce sem limite.
/// </summary>
public sealed class CachedProgram
{
    /// <summary>O nome do programa.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>A versao do programa.</summary>
    public string DisplayVersion { get; set; } = string.Empty;

    /// <summary>A editora do programa.</summary>
    public string Publisher { get; set; } = string.Empty;

    /// <summary>A pasta de instalacao.</summary>
    public string InstallLocation { get; set; } = string.Empty;

    /// <summary>O comando de desinstalacao.</summary>
    public string UninstallString { get; set; } = string.Empty;

    /// <summary>A chave de desinstalacao no registro.</summary>
    public string RegistryKey { get; set; } = string.Empty;

    /// <summary>A data de instalacao (dd/MM/yyyy).</summary>
    public string InstallDate { get; set; } = string.Empty;

    /// <summary>O tamanho estimado, em bytes.</summary>
    public long SizeBytes { get; set; }

    /// <summary>"64-bits", "32-bits" ou vazio.</summary>
    public string Bits { get; set; } = string.Empty;

    /// <summary>Converte um programa instalado em entrada de cache.</summary>
    /// <param name="program">O programa instalado.</param>
    public static CachedProgram From(InstalledProgram program) => new()
    {
        DisplayName = program.DisplayName ?? string.Empty,
        DisplayVersion = program.DisplayVersion ?? string.Empty,
        Publisher = program.Publisher ?? string.Empty,
        InstallLocation = program.InstallLocation ?? string.Empty,
        UninstallString = program.UninstallString ?? string.Empty,
        RegistryKey = program.RegistryKey ?? string.Empty,
        InstallDate = program.InstallDate ?? string.Empty,
        SizeBytes = program.SizeBytes,
        Bits = program.Bits ?? string.Empty,
    };

    /// <summary>Converte a entrada de cache em programa da lista.</summary>
    public InstalledProgram ToProgram() => new()
    {
        DisplayName = DisplayName,
        DisplayVersion = DisplayVersion,
        Publisher = Publisher,
        InstallLocation = InstallLocation,
        UninstallString = UninstallString,
        RegistryKey = RegistryKey,
        InstallDate = InstallDate,
        SizeBytes = SizeBytes,
        Bits = Bits,
    };
}

/// <summary>
/// O cache da ultima varredura: a data em que foi gravado e a versao do
/// formato.
///
/// A versao e o que permite reconstruir o cache: quando o formato muda, o
/// cache antigo e descartado e a varredura comeca do zero, em vez de o
/// aplicativo ler dados de um formato que nao entende.
/// </summary>
public sealed class ProgramCache
{
    /// <summary>A versao do formato do cache.</summary>
    public const int CurrentVersion = 1;

    /// <summary>A versao do formato em que o cache foi gravado.</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>A data da gravacao (UTC).</summary>
    public DateTime SavedAtUtc { get; set; }

    /// <summary>Quantos programas havia no cache quando ele foi gravado.</summary>
    public int ProgramCount { get; set; }

    /// <summary>Os programas do cache.</summary>
    public List<CachedProgram> Programs { get; set; } = new();
}

/// <summary>
/// Como terminou a gravacao do cache.
/// </summary>
public enum ProgramCacheSaveStatus
{
    /// <summary>O cache foi gravado.</summary>
    Ok,

    /// <summary>Na havia programa nenhum para guardar.</summary>
    NothingToSave,

    /// <summary>
    /// Nao ha espaco em disco suficiente para gravar o arquivo de cache
    /// (15150 do Revo: "There is not enough available disk space to write
    /// the cache data file").
    /// </summary>
    NotEnoughDiskSpace,

    /// <summary>A gravacao falhou.</summary>
    WriteFailed,
}

/// <summary>
/// Como terminou a leitura do cache.
/// </summary>
public enum ProgramCacheLoadStatus
{
    /// <summary>O cache foi lido.</summary>
    Ok,

    /// <summary>Nao ha cache gravado.</summary>
    NotFound,

    /// <summary>O arquivo do cache esta corrompido e foi descartado.</summary>
    Corrupted,

    /// <summary>
    /// O cache foi gravado por outra versao do formato e nao pode ser lido
    /// por esta.
    /// </summary>
    Incompatible,
}

/// <summary>
/// O cache de programas: a ultima varredura, gravada em disco.
///
/// O cache e um **acelerador**, nunca uma fonte de verdade: quando ele
/// falta, esta corrompido ou foi gravado por outra versao do formato, a
/// varredura comeca do zero. Nenhum desses casos pode impedir o
/// aplicativo de funcionar.
/// </summary>
public static class ProgramCacheStore
{
    /// <summary>
    /// O numero maximo de programas guardados no cache.
    ///
    /// O cache e um resumo, nao uma copia da lista: sem este limite, um
    /// Windows com milhares de programas produziria um arquivo que cresce a
    /// cada varredura e nunca mais para de crescer.
    /// </summary>
    public const int MaxEntries = 2000;

    /// <summary>
    /// Caminho alternativo do arquivo de cache. Existe para os testes:
    /// sem ele, um teste que grava o cache escreveria no arquivo real do
    /// usuario. Nulo em uso normal.
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    /// <summary>O caminho do arquivo de cache.</summary>
    public static string FilePath =>
        FilePathOverride ?? Path.Combine(RegistryBackup.DataDirectory, "program-cache.json");

    /// <summary>Como a ultima leitura terminou.</summary>
    public static ProgramCacheLoadStatus LastLoadStatus { get; private set; } = ProgramCacheLoadStatus.NotFound;

    /// <summary>
    /// Grava o cache da ultima varredura.
    ///
    /// A gravacao e atomica (temporario e troca) e o arquivo e limitado a
    /// <see cref="MaxEntries"/> entradas. Quando o disco nao tem espaco
    /// para o arquivo, o motivo e devolvido em vez de um "false" generico:
    /// a tela precisa dizer ao usuario o que aconteceu.
    /// </summary>
    /// <param name="programs">Os programas da varredura.</param>
    public static ProgramCacheSaveStatus Save(IReadOnlyList<InstalledProgram> programs)
    {
        if (programs is null || programs.Count == 0) return ProgramCacheSaveStatus.NothingToSave;

        var entradas = programs
            .Where(p => p is not null)
            .Take(MaxEntries)
            .Select(CachedProgram.From)
            .ToList();

        var cache = new ProgramCache
        {
            Version = ProgramCache.CurrentVersion,
            SavedAtUtc = DateTime.UtcNow,
            ProgramCount = entradas.Count,
            Programs = entradas,
        };

        var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions
        {
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        var necessario = Math.Max(64 * 1024L, json.Length * 2L);

        try
        {
            var caminho = Path.GetFullPath(FilePath);
            var pasta = Path.GetDirectoryName(caminho);
            if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

            if (!HaEspaco(caminho, necessario)) return ProgramCacheSaveStatus.NotEnoughDiskSpace;

            var temporario = caminho + ".tmp";
            File.WriteAllText(temporario, json, new UTF8Encoding(false));

            if (File.Exists(caminho)) File.Move(temporario, caminho, true);
            else File.Move(temporario, caminho);

            return ProgramCacheSaveStatus.Ok;
        }
        catch (IOException)
        {
            // Sem espaco em disco e arquivo travado chegam aqui como
            // IOException: o motivo para o usuario e o mesmo ("nao deu para
            // gravar"), e a varredura continua funcionando sem cache.
            return ProgramCacheSaveStatus.WriteFailed;
        }
        catch (UnauthorizedAccessException)
        {
            return ProgramCacheSaveStatus.WriteFailed;
        }
    }

    /// <summary>
    /// Le o cache gravado.
    ///
    /// Devolve false quando nao ha cache, quando ele esta corrompido ou
    /// quando foi gravado por outra versao do formato — nos tres casos o
    /// aplicativo recomeca do zero, sem erro e sem mexer no arquivo do
    /// usuario.
    /// </summary>
    /// <param name="cache">O cache lido.</param>
    public static bool TryLoad(out ProgramCache? cache)
    {
        cache = null;

        try
        {
            if (!File.Exists(FilePath))
            {
                LastLoadStatus = ProgramCacheLoadStatus.NotFound;
                return false;
            }

            var json = File.ReadAllText(FilePath);
            var dados = JsonSerializer.Deserialize<ProgramCache>(json);

            if (dados is null || dados.Programs is null)
            {
                LastLoadStatus = ProgramCacheLoadStatus.Corrupted;
                return false;
            }

            if (dados.Version <= 0 || dados.Version > ProgramCache.CurrentVersion)
            {
                LastLoadStatus = ProgramCacheLoadStatus.Incompatible;
                return false;
            }

            cache = dados;
            LastLoadStatus = ProgramCacheLoadStatus.Ok;
            return true;
        }
        catch (JsonException)
        {
            LastLoadStatus = ProgramCacheLoadStatus.Corrupted;
            return false;
        }
        catch (IOException)
        {
            LastLoadStatus = ProgramCacheLoadStatus.Corrupted;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            LastLoadStatus = ProgramCacheLoadStatus.Corrupted;
            return false;
        }
    }

    /// <summary>
    /// Descarta o cache gravado: o proximo arranque comeca do zero.
    ///
    /// E o que a reconstrucao do cache usa — o arquivo e reescrito na
    /// proxima varredura, entao nao ha nada a apagar na mao.
    /// </summary>
    public static void Invalidate()
    {
        LastLoadStatus = ProgramCacheLoadStatus.NotFound;

        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch
        {
            // Sem permissao para apagar: o cache sera sobrescrito na
            // proxima gravacao, e um cache velho nao impede a varredura.
        }
    }

    /// <summary>
    /// A explicacao, em portugues, do resultado da gravacao.
    /// </summary>
    /// <param name="status">O resultado da gravacao.</param>
    public static string Describe(ProgramCacheSaveStatus status) => status switch
    {
        ProgramCacheSaveStatus.Ok => "Cache de programas gravado.",
        ProgramCacheSaveStatus.NothingToSave => "Nao havia programa nenhum para guardar no cache.",
        ProgramCacheSaveStatus.NotEnoughDiskSpace =>
            "Nao ha espaco em disco suficiente para gravar o arquivo de cache de dados.",
        _ => "Nao foi possivel gravar o cache de programas.",
    };

    /// <summary>
    /// True quando a unidade do arquivo tem espaco livre para o cache.
    /// </summary>
    private static bool HaEspaco(string caminho, long necessario)
    {
        try
        {
            var raiz = Path.GetPathRoot(caminho);
            if (string.IsNullOrEmpty(raiz)) return true;

            var unidade = new DriveInfo(raiz);
            return unidade.AvailableFreeSpace >= necessario;
        }
        catch
        {
            // Sem informacao do disco (unidade removida, rede): nao ha
            // motivo para proibir a gravacao.
            return true;
        }
    }
}
