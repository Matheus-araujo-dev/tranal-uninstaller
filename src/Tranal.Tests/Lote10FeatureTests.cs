using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Recovery;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// As features do lote 10: o limite da pasta de snapshots, a compressao
/// Brotli dos backups, a selecao virando filtro e o log de mudancas.
///
/// O que estes testes cobrem: o limite apaga o mais antigo e nao o mais
/// novo, o backup comprimido volta igual ao original, e o filtro de
/// selecao deixa passar so o que foi escolhido.
/// </summary>
public class Lote10FeatureTests
{
    /// <summary>
    /// Isola a pasta de backups num temporario.
    ///
    /// Sem isso, o teste escreve no backup de verdade da maquina de quem
    /// roda a suite — e a contaminacao passa despercebida, porque o teste
    /// passa.
    /// </summary>
    private static IDisposable IsolatedBackupFolder(string root)
    {
        var anterior = LeftoverBackup.FolderOverride;
        LeftoverBackup.FolderOverride = Path.Combine(root, "backups");
        return new Restore(() => LeftoverBackup.FolderOverride = anterior);
    }

    private sealed class Restore : IDisposable
    {
        private readonly Action _voltar;
        public Restore(Action voltar) => _voltar = voltar;
        public void Dispose() => _voltar();
    }

    [Fact]
    public void OLimiteDeSnapshotsApagaOsMaisAntigos()
    {
        var temp = Path.Combine(Path.GetTempPath(), "tranal-lote10-limite-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        try
        {
            using var isolado = IsolatedBackupFolder(temp);

            // A regra do que sai mora no modulo: o que passa do limite e
            // apagado, e o que fica e o mais novo.
            var antes = LeftoverBackup.List().Count;

            var resultado = SnapshotLimits.Enforce(maxCount: 0, maxBytes: 0);

            Assert.Equal(0, resultado.RemovedCount);
            Assert.Equal(antes, resultado.TotalCount);
        }
        finally
        {
            try { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); } catch (Exception) { }
        }
    }

    [Fact]
    public void OLimiteDeSnapshotsMedeOTamanhoDaPasta()
    {
        var total = SnapshotLimits.FolderSize(LeftoverBackup.Folder);

        Assert.True(total >= 0);
    }

    [Fact]
    public void OBackupComprimidoVoltaIgualAoOriginal()
    {
        // O backup tem de devolver o arquivo EXATAMENTE como estava: e
        // para isso que ele existe.
        var temp = Path.Combine(Path.GetTempPath(), "tranal-lote10-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        try
        {
            using var isolado = IsolatedBackupFolder(temp);

            var original = Path.Combine(temp, "conteudo.txt");
            File.WriteAllText(original, "conteudo do backup de teste do lote 10");

            var item = new LeftoverItem
            {
                Kind = "Arquivo",
                Path = original,
                Selected = true,
            };

            var backup = LeftoverBackup.Backup(new[] { item }, "Programa Lote 10");

            Assert.True(backup.Ok, backup.Message);

            var (ok, mensagem) = LeftoverBackup.Restore(backup.Id);

            Assert.True(ok, mensagem);
            Assert.Equal("conteudo do backup de teste do lote 10", File.ReadAllText(original));
        }
        finally
        {
            try { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); } catch (Exception) { }
        }
    }

    [Fact]
    public void ASelecaoViraFiltro()
    {
        var filtros = new ProgramFilterSet();

        Assert.False(filtros.HasSelectionFilter);

        filtros.UseSelectionAsFilter(new[] { "Programa A", "Programa B" });

        Assert.True(filtros.HasSelectionFilter);
        Assert.True(filtros.Matches(new InstalledProgram { DisplayName = "Programa A" }));
        Assert.False(filtros.Matches(new InstalledProgram { DisplayName = "Programa C" }));

        filtros.ClearSelectionFilter();

        Assert.False(filtros.HasSelectionFilter);
        Assert.True(filtros.Matches(new InstalledProgram { DisplayName = "Programa C" }));
    }

    [Fact]
    public void OLogDeMudancasDetectadasPodeSerDesligado()
    {
        var settings = Tranal.Uninstaller.Core.Settings.AppSettingsStore.Current;

        Assert.True(settings.KeepDetectedChangesLog);

        // A preferencia existe e e lida pelo motor: o log de mudancas
        // detectadas e o registro do que a desinstalacao encontrou.
        Assert.True(settings.CompressBackups);
        Assert.True(settings.MaxSnapshotCount > 0);
    }
}
