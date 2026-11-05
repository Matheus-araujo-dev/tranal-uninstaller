using System.IO;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Settings;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Os quatro modos de desinstalacao do Revo Uninstaller Pro (Built-in, Safe,
/// Moderate, Advanced) traduzidos para um unico enum. Este e o contrato que
/// GUI e CLI consomem: nao pode existir um segundo dial de profundidade.
/// </summary>
public class UninstallModeTests
{
    [Fact]
    public void QuatroModosNaOremDoRevo()
    {
        Assert.Equal(
            new[] { UninstallMode.BuiltIn, UninstallMode.Safe, UninstallMode.Moderate, UninstallMode.Advanced },
            UninstallModes.All);
    }

    [Fact]
    public void SomenteOModoSomenteNaoVarre()
    {
        Assert.False(UninstallModes.Scans(UninstallMode.BuiltIn));
        Assert.True(UninstallModes.Scans(UninstallMode.Safe));
        Assert.True(UninstallModes.Scans(UninstallMode.Moderate));
        Assert.True(UninstallModes.Scans(UninstallMode.Advanced));
    }

    [Theory]
    [InlineData(UninstallMode.BuiltIn)]
    [InlineData(UninstallMode.Safe)]
    [InlineData(UninstallMode.Moderate)]
    [InlineData(UninstallMode.Advanced)]
    public void TodoModoTemDescricaoENomeCurto(UninstallMode mode)
    {
        Assert.False(string.IsNullOrWhiteSpace(UninstallModes.DescriptionKey(mode)));
        Assert.False(string.IsNullOrWhiteSpace(UninstallModes.ShortName(mode)));

        // A descricao mora apenas no catalogo: e o mesmo texto que a GUI e a
        // CLI mostram. Se a chave sumir do catalogo, o modo fica sem texto.
        Loc.Load(Loc.DefaultLanguage);
        Assert.True(Loc.HasKey(UninstallModes.DescriptionKey(mode)),
            $"a descricao do modo {mode} precisa existir no catalogo");
    }

    [Fact]
    public void ModoSomenteNaoEncontraSobras()
    {
        var program = new InstalledProgram { DisplayName = "Programa de Teste Tranal" };

        Assert.Empty(LeftoverScanner.Scan(program, new LeftoverScanOptions { Mode = UninstallMode.BuiltIn }));
        Assert.Empty(LeftoverScanner.ScanForced(@"C:\Windows\System32", new LeftoverScanOptions { Mode = UninstallMode.BuiltIn }));
    }

    [Fact]
    public void ModoSomenteNaoEncontraSobrasNemForcadoEmPastaReal()
    {
        using var temp = new TempFolder();
        var folder = temp.CreateSubFolder("Programa de Teste Tranal");

        Assert.Empty(LeftoverScanner.ScanForced(folder, new LeftoverScanOptions { Mode = UninstallMode.BuiltIn }));
    }
}

/// <summary>
/// A confianca da associacao item/programa. O Revo destaca em negrito apenas
/// os itens de confianca alta e apaga so esses — o Tranal reproduz isso
/// marcando apenas a confianca alta.
/// </summary>
public class LeftoverConfidenceTests
{
    [Fact]
    public void SomenteAltaConfiancaVemMarcada()
    {
        var high = new LeftoverItem { Kind = "Pasta", Path = @"C:\App", Confidence = LeftoverConfidence.High };
        var medium = new LeftoverItem { Kind = "Pasta", Path = @"C:\App2", Confidence = LeftoverConfidence.Medium };
        var low = new LeftoverItem { Kind = "Pasta", Path = @"C:\App3", Confidence = LeftoverConfidence.Low };

        Assert.True(high.IsHighConfidence);
        Assert.False(medium.IsHighConfidence);
        Assert.False(low.IsHighConfidence);
    }

    [Fact]
    public void PastaDeInstalacaoRegistradaEAltaConfianca()
    {
        using var temp = new TempFolder();
        var folder = temp.CreateSubFolder("Programa de Teste Tranal");

        var program = new InstalledProgram
        {
            DisplayName = "Programa de Teste Tranal",
            InstallLocation = folder,
        };

        var items = LeftoverScanner.Scan(program, new LeftoverScanOptions { Mode = UninstallMode.Safe });

        var install = Assert.Single(items, i => i.Kind == "Pasta" && i.Path == folder);
        Assert.Equal(LeftoverConfidence.High, install.Confidence);
        Assert.True(install.Selected);
    }

    [Fact]
    public void ItemDeConfiancaMediaEDeBaixaNaoVemMarcados()
    {
        using var temp = new TempFolder();
        var folder = temp.CreateSubFolder("Programa de Teste Tranal");

        var program = new InstalledProgram
        {
            DisplayName = "Programa de Teste Tranal",
            InstallLocation = folder,
        };

        var items = LeftoverScanner.Scan(program, new LeftoverScanOptions { Mode = UninstallMode.Advanced });

        Assert.All(items.Where(i => i.Confidence != LeftoverConfidence.High),
            i => Assert.False(i.Selected));
    }
}

/// <summary>
/// O Gerenciador de Backups: ponto de restauracao por operacao, restauracao
/// integral ou item a item, e exclusao por tempo.
///
/// Na colecao "backups" porque o diretorio dos pontos de restauracao e um
/// estado global redirecionado: duas classes que o mexem ao mesmo tempo
/// enxergam a raiz uma da outra.
/// </summary>
[Collection("backups")]
public class BackupManagerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "tranal-backup-tests-" + Guid.NewGuid().ToString("N")[..8]);

    public BackupManagerTests()
    {
        // Os pontos de restauracao do teste ficam num diretorio temporario:
        // sem isso, "excluir todos" apagaria os pontos REAIS do usuario.
        BackupManager.RootDirectoryOverride = _root;
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        BackupManager.RootDirectoryOverride = null;
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* limpeza best effort */ }
    }

    [Fact]
    public void PontoDeRestauracaoCriadoApareceNaListagem()
    {
        var point = BackupManager.Create("Teste", BackupManager.OperationFiles);

        var list = BackupManager.List();
        Assert.Contains(list, p => p.Id == point.Id);
        Assert.Equal("Teste", BackupManager.Load(point.Id)!.Name);
    }

    [Fact]
    public void ArquivoCapturadoERestaurado()
    {
        var source = Path.Combine(_root, "original.txt");
        Directory.CreateDirectory(_root);
        File.WriteAllText(source, "conteudo original");

        var point = BackupManager.Create("Arquivo", BackupManager.OperationFiles);
        Assert.True(BackupManager.AddFile(point, source));

        File.Delete(source);
        Assert.False(File.Exists(source));

        var result = BackupManager.Restore(point);

        Assert.Equal(1, result.Restored);
        Assert.Equal(0, result.Failed);
        Assert.True(File.Exists(source));
        Assert.Equal("conteudo original", File.ReadAllText(source));
    }

    [Fact]
    public void PastaCapturadaERestaurada()
    {
        var folder = Path.Combine(_root, "pasta");
        Directory.CreateDirectory(Path.Combine(folder, "interna"));
        File.WriteAllText(Path.Combine(folder, "interna", "a.txt"), "a");

        var point = BackupManager.Create("Pasta", BackupManager.OperationFiles);
        Assert.True(BackupManager.AddFolder(point, folder));

        Directory.Delete(folder, recursive: true);
        Assert.False(Directory.Exists(folder));

        var result = BackupManager.Restore(point);

        Assert.Equal(1, result.Restored);
        Assert.True(File.Exists(Path.Combine(folder, "interna", "a.txt")));
    }

    [Fact]
    public void ChaveDeRegistroCapturadaERestaurada()
    {
        const string keyPath = @"HKEY_CURRENT_USER\Software\Tranal\Testes\Backup";
        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Tranal\Testes\Backup"))
        {
            key.SetValue("Texto", "valor de teste");
            key.SetValue("Numero", 42, RegistryValueKind.DWord);
        }

        try
        {
            var point = BackupManager.Create("Registro", BackupManager.OperationRegistry);
            Assert.True(BackupManager.AddRegistryKey(point, keyPath));

            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Tranal\Testes\Backup", writable: true))
                key?.DeleteValue("Texto", throwOnMissingValue: false);

            var result = BackupManager.Restore(point);

            Assert.Equal(1, result.Restored);
            using var restored = Registry.CurrentUser.OpenSubKey(@"Software\Tranal\Testes\Backup");
            Assert.Equal("valor de teste", restored?.GetValue("Texto"));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Tranal\Testes", throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void RestauracaoCustomizadaRestauraSomenteOEscolhido()
    {
        var first = Path.Combine(_root, "primeiro.txt");
        var second = Path.Combine(_root, "segundo.txt");
        Directory.CreateDirectory(_root);
        File.WriteAllText(first, "1");
        File.WriteAllText(second, "2");

        var point = BackupManager.Create("Dois arquivos", BackupManager.OperationFiles);
        BackupManager.AddFile(point, first);
        BackupManager.AddFile(point, second);

        File.Delete(first);
        File.Delete(second);

        var chosen = point.Items.Where(i => i.OriginalPath == first).ToList();
        var result = BackupManager.Restore(point, chosen);

        Assert.Equal(1, result.Restored);
        Assert.Equal(1, result.Skipped);
        Assert.True(File.Exists(first));
        Assert.False(File.Exists(second));
    }

    [Fact]
    public void ExclusaoPorDataRemoveOsAntigos()
    {
        var point = BackupManager.Create("Antigo", BackupManager.OperationFiles);

        var antes = BackupManager.List().Count;
        BackupManager.DeleteOlderThan(DateTime.Now.AddMinutes(1));
        var depois = BackupManager.List().Count;

        Assert.True(depois < antes);
        Assert.DoesNotContain(BackupManager.List(), p => p.Id == point.Id);
    }

    [Fact]
    public void ExclusaoDeTodosLimpaALista()
    {
        BackupManager.Create("A", BackupManager.OperationFiles);
        BackupManager.Create("B", BackupManager.OperationFiles);

        BackupManager.DeleteAll();

        Assert.Empty(BackupManager.List());
    }
}

/// <summary>Serializacao dos valores de registro capturados no backup.</summary>
public class RegistrySnapshotTests
{
    [Theory]
    [InlineData(RegistryValueKind.String, "texto simples")]
    [InlineData(RegistryValueKind.ExpandString, "%windir%\\System32")]
    [InlineData(RegistryValueKind.DWord, "1234")]
    [InlineData(RegistryValueKind.QWord, "1234567890123")]
    public void ValorSimplesFazIdaEVolta(RegistryValueKind kind, string data)
    {
        var snapshot = new RegistrySnapshot
        {
            KeyPath = @"HKEY_CURRENT_USER\Software\Tranal\Testes",
            ValueName = "valor",
            Kind = kind,
            Data = data,
        };

        var decoded = BackupManager.DecodeValue(snapshot);

        Assert.NotNull(decoded);
        Assert.Equal(data, decoded.ToString());
    }

    [Fact]
    public void BinarioFazIdaEVolta()
    {
        var bytes = new byte[] { 1, 2, 3, 250 };

        var snapshot = new RegistrySnapshot
        {
            KeyPath = @"HKEY_CURRENT_USER\Software\Tranal\Testes",
            ValueName = "bin",
            Kind = RegistryValueKind.Binary,
            Data = BackupManager.EncodeValue(bytes, RegistryValueKind.Binary),
        };

        var decoded = BackupManager.DecodeValue(snapshot);

        Assert.Equal(bytes, Assert.IsType<byte[]>(decoded));
    }

    [Fact]
    public void MultiStringFazIdaEVolta()
    {
        var values = new[] { "um", "dois", "três" };

        var snapshot = new RegistrySnapshot
        {
            KeyPath = @"HKEY_CURRENT_USER\Software\Tranal\Testes",
            ValueName = "multi",
            Kind = RegistryValueKind.MultiString,
            Data = BackupManager.EncodeValue(values, RegistryValueKind.MultiString),
        };

        var decoded = BackupManager.DecodeValue(snapshot);

        Assert.Equal(values, Assert.IsType<string[]>(decoded));
    }
}

/// <summary>A Lixeira do Windows e o adiamento para o proximo reinicio.</summary>
public class DisposalTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "tranal-disposal-tests-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* limpeza best effort */ }
    }

    [Fact]
    public void RaizDeUnidadeNaoPodeIrParaALixeira()
    {
        Assert.False(RecycleBin.CanAccept("C:\\"));
        Assert.False(RecycleBin.CanAccept(""));
    }

    [Fact]
    public void ArquivoInexistenteNaoPodeIrParaALixeira()
    {
        Assert.False(RecycleBin.CanAccept(Path.Combine(_root, "nao-existe.txt")));
    }

    [Fact]
    public void ArquivoExistentePodeIrParaALixeira()
    {
        Directory.CreateDirectory(_root);
        var file = Path.Combine(_root, "existe.txt");
        File.WriteAllText(file, "x");

        Assert.True(RecycleBin.CanAccept(file));
    }

    [Fact]
    public void ArquivoInexistenteNaoEAdiado()
    {
        Assert.False(DeferredDelete.Schedule(Path.Combine(_root, "nao-existe.txt")));
    }
}

/// <summary>
/// Resumo do comportamento de remocao com as opcoes do Revo.
///
/// Na colecao "preferencias" porque consulta o <see cref="AppSettingsStore"/>
/// global, que e estado compartilhado com o app.
/// </summary>
[Collection("preferencias")]
public class UninstallRemovalOptionsTests
{
    [Fact]
    public void PadroesSeguemORevo()
    {
        var options = UninstallRemovalOptions.Default;

        Assert.True(options.Backup);
        Assert.True(options.UseRecycleBin);
        Assert.True(options.DeferToNextRestart);
    }

    [Fact]
    public void OpcoesDerivadasDasPreferenciasRespeitamAsFlags()
    {
        var settings = AppSettingsStore.Current;
        var original = settings.SendFilesToRecycleBin;
        try
        {
            settings.SendFilesToRecycleBin = false;
            Assert.False(UninstallRemovalOptions.FromSettings().UseRecycleBin);
        }
        finally
        {
            settings.SendFilesToRecycleBin = original;
        }
    }
}

/// <summary>
/// Pasta temporaria isolada para os testes: nada toca o disco do usuario e
/// tudo e apagado no fim.
/// </summary>
public sealed class TempFolder : IDisposable
{
    private readonly string _root;

    public TempFolder()
    {
        _root = Path.Combine(Path.GetTempPath(), "tranal-temp-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_root);
    }

    /// <summary>Cria uma subpasta e devolve o caminho completo.</summary>
    public string CreateSubFolder(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Cria um arquivo dentro da pasta temporaria e devolve o caminho.</summary>
    public string CreateFile(string name, string content = "conteudo")
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Caminho da pasta temporaria.</summary>
    public string Root => _root;

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* o sandbox e temporario */ }
    }
}
