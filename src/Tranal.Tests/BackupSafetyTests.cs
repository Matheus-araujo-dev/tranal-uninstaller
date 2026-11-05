using System.IO;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Regressoes dos defeitos encontrados na revisao adversarial.
///
/// Cada teste aqui existe porque uma correcao concreta poderia voltar sem
/// ninguem perceber. Eles sao deliberadamente adversariais: tentam fazer o
/// Tranal apagar o que nao deve.
/// </summary>
[Collection("backups")]
public class BackupSafetyTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly string _backupRoot;

    public BackupSafetyTests()
    {
        // Os pontos de restauracao do teste ficam num diretorio temporario:
        // o diretorio real do usuario nao pode ser tocado por um teste.
        _backupRoot = Path.Combine(_temp.Root, "backups");
        Directory.CreateDirectory(_backupRoot);
        BackupManager.RootDirectoryOverride = _backupRoot;
    }

    public void Dispose()
    {
        BackupManager.RootDirectoryOverride = null;
        _temp.Dispose();
    }

    // ---------------------------------------------------------------
    // Roteamento dos rastros: so aconfidence alta entra marcada
    // ---------------------------------------------------------------

    [Fact]
    public void RastroDeNavegadorORoteadoParaOTracksCleaner()
    {
        // O roteamento casa pelo prefixo da categoria. Um prefixo errado
        // (ponto em vez de hifen) mandava o rastro inteiro para o motor que
        // apaga a PASTA, e nao o conteudo.
        var file = _temp.CreateFile("cookies.sqlite", "dados");

        var item = new HistoryItem
        {
            Kind = "Arquivo",
            Path = file,
            CategoryId = "browsers-cookies",
            Selected = true,
        };

        var report = HistoryCleaner.Clear(new[] { item });

        Assert.Equal(1, report.RemovedCount);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void RastroDePastaNaoApagaAPastaInteira()
    {
        var folder = _temp.CreateSubFolder("cache");
        File.WriteAllText(Path.Combine(folder, "a.txt"), "a");

        var item = new HistoryItem
        {
            Kind = "Pasta",
            Path = folder,
            CategoryId = "windows-cache",
            Filter = "*",
            Selected = true,
        };

        HistoryCleaner.Clear(new[] { item });

        Assert.True(Directory.Exists(folder), "a pasta em si nao pode ser removida");
        Assert.Empty(Directory.GetFiles(folder));
    }

    // ---------------------------------------------------------------
    // Restauracao: o manifesto nao vira escrita arbitraria
    // ---------------------------------------------------------------

    [Fact]
    public void RestauracaoRecusaDestinoProtegido()
    {
        var point = BackupManager.Create("Teste", BackupManager.OperationFiles);

        // Destino forjado: uma pasta de sistema, que o Tranal protege.
        var item = new BackupItem
        {
            Kind = "Arquivo",
            OriginalPath = @"C:\Windows\System32\arquivo-restaurado.txt",
            BackupPath = "files\\inexistente.txt",
            Size = 0,
        };

        var result = BackupManager.Restore(point, new[] { item });

        Assert.Equal(0, result.Restored);
        Assert.False(File.Exists(@"C:\Windows\System32\arquivo-restaurado.txt"));
    }

    [Fact]
    public void RestauracaoPorPastaNaoApagaOQueFoiCriadoDepois()
    {
        var folder = Path.Combine(_temp.Root, "restaurada");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "original.txt"), "original");

        var point = BackupManager.Create("Pasta", BackupManager.OperationFiles);
        BackupManager.AddFolder(point, folder);

        // Arquivo criado depois do backup.
        File.WriteAllText(Path.Combine(folder, "criado-depois.txt"), "nao pode sumir");

        BackupManager.Restore(point);

        Assert.True(File.Exists(Path.Combine(folder, "original.txt")));
        Assert.True(File.Exists(Path.Combine(folder, "criado-depois.txt")),
            "restaurar nao pode apagar o que foi criado depois do backup");
    }

    [Fact]
    public void RestauracaoSelecionadaCasaPorConteudoE_naoPorReferencia()
    {
        var first = _temp.CreateFile("primeiro.txt", "1");
        var second = _temp.CreateFile("segundo.txt", "2");

        var point = BackupManager.Create("Dois arquivos", BackupManager.OperationFiles);
        BackupManager.AddFile(point, first);
        BackupManager.AddFile(point, second);

        File.Delete(first);
        File.Delete(second);

        // Selecao montada a partir de OUTRA leitura do manifesto: instancias
        // diferentes com o mesmo conteudo.
        var selection = new List<BackupItem>
        {
            new BackupItem { Kind = "Arquivo", OriginalPath = first, BackupPath = "files\\x" },
        };

        var result = BackupManager.Restore(point, selection);

        Assert.Equal(1, result.Restored);
        Assert.True(File.Exists(first));
        Assert.False(File.Exists(second));
    }

    [Fact]
    public void ValorMultiStringPreservaElementosVazios()
    {
        var values = new[] { "um", "", "tres" };

        var snapshot = new RegistrySnapshot
        {
            KeyPath = @"HKEY_CURRENT_USER\Software\Tranal\Testes",
            ValueName = "multi",
            Kind = RegistryValueKind.MultiString,
            Data = BackupManager.EncodeValue(values, RegistryValueKind.MultiString),
        };

        var decoded = Assert.IsType<string[]>(BackupManager.DecodeValue(snapshot));

        Assert.Equal(3, decoded.Length);
        Assert.Equal(string.Empty, decoded[1]);
    }

    [Fact]
    public void SplitRegistryPathReconheceTodosOsHives()
    {
        var local = SafetyRules.SplitRegistryPath(@"HKEY_LOCAL_MACHINE\SOFTWARE\X");
        var user = SafetyRules.SplitRegistryPath(@"HKEY_CURRENT_USER\Software\X");
        var classes = SafetyRules.SplitRegistryPath(@"HKEY_CLASSES_ROOT\X");
        var users = SafetyRules.SplitRegistryPath(@"HKEY_USERS\S-1-5-21");
        var config = SafetyRules.SplitRegistryPath(@"HKEY_CURRENT_CONFIG\X");

        Assert.Equal(RegistryHive.LocalMachine, local.Hive);
        Assert.Equal(@"SOFTWARE\X", local.SubPath);
        Assert.Equal(RegistryHive.CurrentUser, user.Hive);
        Assert.Equal(RegistryHive.ClassesRoot, classes.Hive);
        Assert.Equal(RegistryHive.Users, users.Hive);
        Assert.Equal(RegistryHive.CurrentConfig, config.Hive);
    }

    [Fact]
    public void SplitRegistryPathNaoAssumeHiveDesconhecido()
    {
        // Prefixo invalido nao pode virar HKEY_LOCAL_MACHINE: a restauracao
        // acabaria gravando no hive errado.
        var unknown = SafetyRules.SplitRegistryPath(@"HKEY_DESCONHECIDO\X");

        Assert.Equal(string.Empty, unknown.SubPath);
    }

    // ---------------------------------------------------------------
    // Backup: o que nao pode ser removido tambem nao e copiado
    // ---------------------------------------------------------------

    [Fact]
    public void PastaProtegidaNaoEntraNoBackup()
    {
        var point = BackupManager.Create("Protegida", BackupManager.OperationFiles);

        var copied = BackupManager.AddFolder(point, @"C:\Windows\System32");

        Assert.False(copied);
        Assert.DoesNotContain(point.Items, i => i.OriginalPath.Contains("System32", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ChaveProtegidaNaoEntraNoBackup()
    {
        var point = BackupManager.Create("Protegida", BackupManager.OperationRegistry);

        var copied = BackupManager.AddRegistryKey(point, @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion");

        Assert.False(copied);
    }

    // ---------------------------------------------------------------
    // Limpeza de rastros: nada de pasta protegida
    // ---------------------------------------------------------------

    [Fact]
    public void LimpezaDeRastroRecusaPastaProtegida()
    {
        // Caminho protegido E inexistente: mesmo que a protecao falhasse, nao
        // haveria nada ali para apagar.
        var item = new HistoryItem
        {
            Kind = "Pasta",
            Path = @"C:\Windows\System32\pasta-inexistente-de-teste",
            CategoryId = "windows-temp",
            Filter = "*",
            Selected = true,
        };

        var report = TrackCleaner.Clean(new[] { item });

        Assert.Equal(0, report.RemovedCount);
        Assert.Equal(1, report.ProtectedCount);
        Assert.False(Directory.Exists(@"C:\Windows\System32\pasta-inexistente-de-teste"));
    }

    [Fact]
    public void LimpezaDeRastroRecusaChaveProtegida()
    {
        // "Microsoft" e um nome de chave protegido; a subchave usada aqui nao
        // existe, entao o teste nao tem efeito colateral.
        var item = new HistoryItem
        {
            Kind = "Registro",
            Path = @"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet1000\Microsoft",
            CategoryId = "windows-registry",
            Selected = true,
        };

        var report = TrackCleaner.Clean(new[] { item });

        Assert.Equal(0, report.RemovedCount);
        Assert.Equal(1, report.ProtectedCount);
    }

    // ---------------------------------------------------------------
    // Confianca: nome exato e exigido
    // ---------------------------------------------------------------

    [Fact]
    public void ChaveDeDesinstalacaoDeProdutoHomonimoNaoEAltaConfianca()
    {
        // "Steam Client" contem "Steam", mas e um produto diferente: apagar a
        // chave de desinstalacao dele seria remover o programa de outra coisa.
        var program = new InstalledProgram { DisplayName = "Steam" };

        var items = LeftoverScanner.Scan(program, new LeftoverScanOptions { Mode = UninstallMode.Safe });

        var homonimo = items.FirstOrDefault(i =>
            i.Kind == "Registro" && i.Detail.Contains("Steam Client", StringComparison.OrdinalIgnoreCase));

        if (homonimo is not null)
        {
            Assert.NotEqual(LeftoverConfidence.High, homonimo.Confidence);
            Assert.False(homonimo.Selected);
        }
    }

    [Fact]
    public void PastaDeInstalacaoProtegidaNaoEntraComoMarcada()
    {
        var program = new InstalledProgram
        {
            DisplayName = "Programa de Teste Tranal",
            InstallLocation = @"C:\Windows",
        };

        var items = LeftoverScanner.Scan(program, new LeftoverScanOptions { Mode = UninstallMode.Safe });

        Assert.DoesNotContain(items, i =>
            i.Kind == "Pasta" && i.Selected && i.Path.StartsWith(@"C:\Windows", StringComparison.OrdinalIgnoreCase));
    }

    // ---------------------------------------------------------------
    // Pontos orfaos
    // ---------------------------------------------------------------

    [Fact]
    public void PontoComArquivoTravadoContinuaVisivelNaListagem()
    {
        // A exclusao de um ponto apaga o CONTEUDO primeiro e so depois o
        // manifesto. Se o manifesto fosse apagado primeiro, um arquivo
        // travado deixaria a pasta orfa e invisivel para o app.
        var origem = _temp.CreateFile("original.txt", "conteudo");
        var point = BackupManager.Create("Travado", BackupManager.OperationFiles);
        Assert.True(BackupManager.AddFile(point, origem));
        BackupManager.Save(point);

        // trava o arquivo copiado dentro da pasta do backup
        var copiado = Path.Combine(point.FolderPath, point.Items[0].BackupPath);
        using var trava = new FileStream(copiado, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.False(BackupManager.Delete(point.Id));

        // o manifesto sobreviveu: o ponto continua listavel e nao virou orfao
        Assert.Contains(BackupManager.List(), p => p.Id == point.Id);
        Assert.DoesNotContain(point.Id, BackupManager.ListOrphans());
    }

    [Fact]
    public void PontoComArquivoTravadoSomeDepoisDeLiberado()
    {
        var origem = _temp.CreateFile("original.txt", "conteudo");
        var point = BackupManager.Create("Travado", BackupManager.OperationFiles);
        Assert.True(BackupManager.AddFile(point, origem));
        BackupManager.Save(point);

        var copiado = Path.Combine(point.FolderPath, point.Items[0].BackupPath);
        var trava = new FileStream(copiado, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.False(BackupManager.Delete(point.Id));
        trava.Dispose();

        // liberada a trava, a segunda tentativa apaga tudo
        Assert.True(BackupManager.Delete(point.Id));
        Assert.DoesNotContain(BackupManager.List(), p => p.Id == point.Id);
        Assert.False(Directory.Exists(Path.Combine(BackupManager.RootDirectory, point.Id)));
    }

    [Fact]
    public void PastaSemManifestoEOrfaEDetectada()
    {
        var folder = Path.Combine(BackupManager.RootDirectory, "orfão-sem-manifesto");
        Directory.CreateDirectory(folder);

        try
        {
            Assert.Contains("orfão-sem-manifesto", BackupManager.ListOrphans());
            Assert.Equal(1, BackupManager.DeleteOrphans());
            Assert.False(Directory.Exists(folder));
        }
        finally
        {
            try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch { }
        }
    }
}
