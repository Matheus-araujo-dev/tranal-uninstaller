using System.IO;
using Tranal.Uninstaller.Core;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O relatorio canonico de remocao: e o unico lugar onde o app interpreta
/// "foi removido / ja nao existia / falhou / protegido".
/// </summary>
public class RemovalReportTests
{
    [Fact]
    public void ContagensPorTipoEDistintas()
    {
        var outcomes = new[]
        {
            Outcome("Arquivo", RemovalStatus.Removed),
            Outcome("Arquivo", RemovalStatus.Removed),
            Outcome("Pasta", RemovalStatus.AlreadyAbsent),
            Outcome("Registro", RemovalStatus.Failed),
            Outcome("Registro", RemovalStatus.Protected),
        };

        var report = new RemovalReport(outcomes);

        Assert.Equal(5, report.Total);
        Assert.Equal(2, report.RemovedCount);
        Assert.Equal(1, report.AbsentCount);
        Assert.Equal(1, report.FailedCount);
        Assert.Equal(1, report.ProtectedCount);
        Assert.Equal(2, report.CountOf("Arquivo", RemovalStatus.Removed));
        Assert.Equal(2, report.Remaining.Count);
    }

    [Fact]
    public void ResumoContaPorTipoDeItem()
    {
        var report = new RemovalReport(new[]
        {
            Outcome("Arquivo", RemovalStatus.Removed),
            Outcome("Pasta", RemovalStatus.Removed),
        });

        var text = report.Describe();
        Assert.Contains("Arquivo", text);
        Assert.Contains("Pasta", text);
    }

    private static RemovalOutcome Outcome(string kind, RemovalStatus status) => new()
    {
        Item = new LeftoverItem { Kind = kind, Path = @"C:\Temp\alvo", Detail = "teste" },
        Status = status,
        Message = "teste",
    };
}

/// <summary>Remocao real de arquivos, com as protecoes ativas.</summary>
public class RemovalExecutionTests : IDisposable
{
    private readonly string _sandbox;

    /// <summary>
    /// Opcoes hermeticas: sem Lixeira (o arquivo some do disco, sem encher a
    /// lixeira do usuario) e sem backup (nao cria pontos de restauracao no
    /// ambiente de quem roda os testes).
    /// </summary>
    private static readonly UninstallRemovalOptions Opcoes = new()
    {
        Backup = false,
        UseRecycleBin = false,
        DeferToNextRestart = false,
    };

    public RemovalExecutionTests()
    {
        _sandbox = Path.Combine(Path.GetTempPath(), "tranal-tests-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(_sandbox);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_sandbox)) Directory.Delete(_sandbox, true); }
        catch { /* o sandbox e temporario */ }
    }

    [Fact]
    public void ArquivoExistenteERemovido()
    {
        var file = Path.Combine(_sandbox, "alvo.txt");
        File.WriteAllText(file, "conteudo");

        var report = UninstallEngine.RemoveLeftovers(new[]
        {
            new LeftoverItem { Kind = "Arquivo", Path = file, Detail = "teste" },
        }, Opcoes);

        Assert.Equal(1, report.RemovedCount);
        Assert.Equal(0, report.FailedCount);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void ArquivoInexistenteContaComoJaAusente()
    {
        var file = Path.Combine(_sandbox, "nunca-existiu.txt");

        var report = UninstallEngine.RemoveLeftovers(new[]
        {
            new LeftoverItem { Kind = "Arquivo", Path = file, Detail = "teste" },
        }, Opcoes);

        Assert.Equal(1, report.AbsentCount);
        Assert.Equal(0, report.RemovedCount);
    }

    [Fact]
    public void PastaDeSistemaEProtegida()
    {
        var report = UninstallEngine.RemoveLeftovers(new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = @"C:\Windows\System32", Detail = "teste" },
        }, Opcoes);

        Assert.Equal(1, report.ProtectedCount);
        Assert.Equal(0, report.RemovedCount);
        Assert.True(Directory.Exists(@"C:\Windows\System32"));
    }

    [Fact]
    public void ApenasItensSelecionadosSaoProcessados()
    {
        var alvo = Path.Combine(_sandbox, "alvo.txt");
        var outro = Path.Combine(_sandbox, "outro.txt");
        File.WriteAllText(alvo, "a");
        File.WriteAllText(outro, "b");

        var report = UninstallEngine.RemoveLeftovers(new[]
        {
            new LeftoverItem { Kind = "Arquivo", Path = alvo, Detail = "t", Selected = false },
            new LeftoverItem { Kind = "Arquivo", Path = outro, Detail = "t", Selected = true },
        }, Opcoes);

        Assert.Equal(1, report.Total);
        Assert.True(File.Exists(alvo));
        Assert.False(File.Exists(outro));
    }
}

/// <summary>Backup do registro em formato .reg importavel.</summary>
public class RegistryBackupTests
{
    [Fact]
    public void BackupGeradoEUmRegValido()
    {
        var destination = Path.Combine(Path.GetTempPath(), $"tranal-backup-{Guid.NewGuid():N}.reg");

        try
        {
            var file = RegistryBackup.Backup(new[] { @"HKEY_CURRENT_USER\Software" }, destination);

            Assert.True(File.Exists(file));
            var text = File.ReadAllText(file);

            Assert.StartsWith("Windows Registry Editor Version 5.00", text);
            Assert.Contains("[HKEY_CURRENT_USER\\Software]", text);

            // O valor padrao usa "@" (nome reservado do formato .reg).
            Assert.Contains("\"@\"=", text);
        }
        finally
        {
            try { if (File.Exists(destination)) File.Delete(destination); } catch { }
        }
    }
}
