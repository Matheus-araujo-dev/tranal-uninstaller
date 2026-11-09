using System.IO;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Recovery;

/// <summary>
/// O resultado da aplicacao dos limites na pasta de snapshots.
/// </summary>
public sealed class SnapshotLimitResult
{
    /// <summary>Quantos snapshots foram apagados.</summary>
    public int RemovedCount { get; init; }

    /// <summary>Quantos bytes foram liberados.</summary>
    public long FreedBytes { get; init; }

    /// <summary>Quantos snapshots ficaram na pasta.</summary>
    public int TotalCount { get; init; }

    /// <summary>Quantos bytes a pasta ocupa agora.</summary>
    public long TotalBytes { get; init; }
}

/// <summary>
/// O limite da pasta de snapshots.
///
/// O Total Uninstall tem "Limits for Snapshots folder": quantos snapshots
/// a pasta guarda e quantos bytes ela ocupa. Sem esse limite, a pasta de
/// backups cresce sem parar — e o backup que existe para devolver as
/// sobras acaba sendo o que enche o disco.
///
/// A REGRA mora aqui, em um lugar so: quem cria um snapshot chama
/// <see cref="Enforce"/>, e o que sai (o mais antigo primeiro) e decidido
/// aqui.
/// </summary>
public static class SnapshotLimits
{
    /// <summary>
    /// Aplica os limites na pasta de snapshots.
    /// </summary>
    /// <param name="maxCount">
    /// Quantos snapshots a pasta guarda. Zero ou menos = sem limite de
    /// quantidade.
    /// </param>
    /// <param name="maxBytes">
    /// Quantos bytes a pasta ocupa. Zero ou menos = sem limite de tamanho.
    /// </param>
    public static SnapshotLimitResult Enforce(int? maxCount = null, long? maxBytes = null)
    {
        var limiteQuantidade = maxCount ?? AppSettingsStore.Current.MaxSnapshotCount;
        var limiteBytes = maxBytes ?? (long)AppSettingsStore.Current.MaxSnapshotSizeMb * 1024L * 1024L;

        var snapshots = LeftoverBackup.List();
        if (snapshots.Count == 0)
        {
            return new SnapshotLimitResult { TotalCount = 0, TotalBytes = 0 };
        }

        var tamanhos = Measure();

        var removidos = 0;
        var liberados = 0L;
        var total = tamanhos.Sum(t => t.Bytes);

        // O mais antigo sai primeiro: a lista vem do mais novo para o mais
        // antigo, entao a varredura e ao contrario.
        for (var i = tamanhos.Count - 1; i >= 0; i--)
        {
            var estourouQuantidade = limiteQuantidade > 0 && (tamanhos.Count - removidos) > limiteQuantidade;
            var estourouTamanho = limiteBytes > 0 && (total - liberados) > limiteBytes;

            if (!estourouQuantidade && !estourouTamanho) break;

            var alvo = tamanhos[i];
            var apagado = Apagar(alvo.Id);
            if (!apagado) continue;

            removidos++;
            liberados += alvo.Bytes;
            total -= alvo.Bytes;
        }

        return new SnapshotLimitResult
        {
            RemovedCount = removidos,
            FreedBytes = liberados,
            TotalCount = tamanhos.Count - removidos,
            TotalBytes = total,
        };
    }

    /// <summary>
    /// O tamanho de cada snapshot, do mais novo para o mais antigo.
    /// </summary>
    public static IReadOnlyList<(string Id, long Bytes, DateTime CreatedUtc)> Measure()
    {
        var resultado = new List<(string, long, DateTime)>();

        foreach (var snapshot in LeftoverBackup.List())
        {
            var pasta = Path.Combine(LeftoverBackup.Folder, snapshot.Id);
            resultado.Add((snapshot.Id, FolderSize(pasta), snapshot.CreatedUtc));
        }

        return resultado;
    }

    /// <summary>
    /// Quantos bytes uma pasta ocupa, com os arquivos dentro dela.
    /// </summary>
    /// <param name="folder">A pasta.</param>
    public static long FolderSize(string folder)
    {
        try
        {
            if (!Directory.Exists(folder)) return 0;

            long total = 0;
            foreach (var arquivo in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                try
                {
                    total += new FileInfo(arquivo).Length;
                }
                catch (Exception) { /* o que nao der para ler fica de fora */ }
            }

            return total;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao medir a pasta '{folder}': {ex.Message}");
            return 0;
        }
    }

    private static bool Apagar(string id)
    {
        try
        {
            var pasta = Path.Combine(LeftoverBackup.Folder, id);
            if (!Directory.Exists(pasta)) return false;

            Directory.Delete(pasta, recursive: true);
            return true;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao apagar o snapshot '{id}': {ex.Message}");
            return false;
        }
    }
}
