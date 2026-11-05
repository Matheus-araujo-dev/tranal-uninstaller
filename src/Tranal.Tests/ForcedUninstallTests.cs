using System.IO;
using Tranal.Uninstaller.Core;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// A desinstalação forçada: o alvo informado pelo usuário é o item principal.
///
/// O mesmo caminho pode entrar na lista por dois motivos — a pasta de
/// instalação e a pasta achada pelo nome do programa. A desinstalação
/// forçada precisa remover o que o usuário apontou, e o que ele aponta vem
/// marcado.
///
/// Estes testes têm dentes: o sandbox fica DIRETO na pasta temporária, que é
/// o segundo nível abaixo de %LOCALAPPDATA%. A varredura encontra a pasta
/// por lá (com confiança baixa), e é essa ocorrência fraca que sombreava a
/// forte — a desinstalação forçada terminava sem remover o alvo.
///
/// A mesma montagem cobre a pasta de instalação do assistente: ela também é
/// achada pelo nome, e também precisa vencer.
/// </summary>
[Collection("preferencias")]
public class ForcedUninstallTests : IDisposable
{
    private readonly ExcludeList _original;
    private readonly List<string> _sandboxes = new();

    public ForcedUninstallTests()
    {
        _original = ExcludeList.Current;
        ExcludeList.SetCurrent(new ExcludeList());
    }

    public void Dispose()
    {
        ExcludeList.SetCurrent(_original);

        foreach (var sandbox in _sandboxes)
        {
            try { if (Directory.Exists(sandbox)) Directory.Delete(sandbox, recursive: true); }
            catch { /* o sandbox é temporário */ }
        }
    }

    /// <summary>
    /// Cria uma pasta DIRETO na pasta temporária do Windows — o segundo nível
    /// abaixo de %LOCALAPPDATA%, que é onde a varredura dos programas olha.
    /// </summary>
    private string CreateSandbox(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        _sandboxes.Add(path);
        return path;
    }

    [Fact]
    public void OAlvoInformadoEmPastaVemMarcado()
    {
        var folder = CreateSandbox("tranal-forcado-");
        File.WriteAllText(Path.Combine(folder, "programa.exe"), "conteudo");

        var items = LeftoverScanner.ScanForced(folder, new LeftoverScanOptions
        {
            Mode = UninstallMode.Advanced,
        });

        var alvo = Assert.Single(items, i =>
            string.Equals(i.Path, folder, StringComparison.OrdinalIgnoreCase));

        Assert.True(alvo.Selected);
        Assert.Equal(LeftoverConfidence.High, alvo.Confidence);
    }

    [Fact]
    public void OAlvoInformadoVemMarcadoMesmoComMarcarTudoDesligado()
    {
        // "Marcar todas as sobras por padrão" é uma opção independente: o
        // alvo informado vem marcado mesmo com ela desligada.
        var folder = CreateSandbox("tranal-forcado-");

        var items = LeftoverScanner.ScanForced(folder, new LeftoverScanOptions
        {
            Mode = UninstallMode.Advanced,
            CheckAllLeftovers = false,
        });

        var alvo = Assert.Single(items, i =>
            string.Equals(i.Path, folder, StringComparison.OrdinalIgnoreCase));

        Assert.True(alvo.Selected);
        Assert.Equal(LeftoverConfidence.High, alvo.Confidence);
    }

    [Fact]
    public void OAlvoInformadoEmArquivoVemMarcado()
    {
        var folder = CreateSandbox("tranal-forcado-");
        var file = Path.Combine(folder, "programa-forcado.exe");
        File.WriteAllText(file, "conteudo");

        var items = LeftoverScanner.ScanForced(file, new LeftoverScanOptions
        {
            Mode = UninstallMode.Advanced,
        });

        var alvo = Assert.Single(items, i =>
            string.Equals(i.Path, file, StringComparison.OrdinalIgnoreCase));

        Assert.True(alvo.Selected);
        Assert.Equal(LeftoverConfidence.High, alvo.Confidence);
    }

    [Fact]
    public void APastaDeInstalacaoVemMarcadaMesmoAchadaPeloNome()
    {
        // O caso do assistente: a pasta de instalacao do programa tambem
        // casa com o nome dele. A ocorrencia forte (pasta de instalacao, alta
        // confianca) tem de vencer a fraca (achada pelo nome, confianca
        // baixa) — senao a pasta de instalacao nao vem marcada e o
        // desinstalador nao a remove.
        var folder = CreateSandbox("Alvo Tranal ");

        var program = new InstalledProgram
        {
            DisplayName = "Alvo",
            InstallLocation = folder,
        };

        var items = LeftoverScanner.Scan(program, new LeftoverScanOptions
        {
            Mode = UninstallMode.Advanced,
        });

        var instalacao = Assert.Single(items, i =>
            string.Equals(i.Path, folder, StringComparison.OrdinalIgnoreCase));

        Assert.True(instalacao.Selected);
        Assert.Equal(LeftoverConfidence.High, instalacao.Confidence);
    }

    [Fact]
    public void ADesinstalacaoForcadaDeUmArquivoNaoMarcaAPastaQueOContem()
    {
        // A pasta que contem o arquivo apontado nao e a pasta de instalacao
        // do programa. Inferi-la seria perigoso: apontar para um executavel
        // em %LOCALAPPDATA%\Temp removeria a pasta Temp inteira.
        var folder = CreateSandbox("programa-forcado-");
        var file = Path.Combine(folder, "programa-forcado.exe");
        File.WriteAllText(file, "conteudo");

        var items = LeftoverScanner.ScanForced(file, new LeftoverScanOptions
        {
            Mode = UninstallMode.Advanced,
        });

        var pasta = items.FirstOrDefault(i => i.Kind == "Pasta"
            && string.Equals(i.Path, folder, StringComparison.OrdinalIgnoreCase));

        Assert.True(pasta is null || !pasta.Selected,
            "a pasta que contem o arquivo apontado nao pode vir marcada");
    }

    [Fact]
    public void ADesinstalacaoForcadaDeUmArquivoNaoRemoveAPastaQueOContem()
    {
        var folder = CreateSandbox("programa-forcado-");
        var file = Path.Combine(folder, "programa-forcado.exe");
        File.WriteAllText(file, "conteudo");

        var alheio = Path.Combine(folder, "arquivo-de-outro-programa.dat");
        File.WriteAllText(alheio, "nao e do programa");

        var items = LeftoverScanner.ScanForced(file, new LeftoverScanOptions
        {
            Mode = UninstallMode.Advanced,
        });

        var options = new UninstallRemovalOptions
        {
            Backup = false,
            UseRecycleBin = false,
            DeferToNextRestart = false,
        };

        UninstallEngine.RemoveLeftovers(items, options);

        // O alvo sai.
        Assert.False(File.Exists(file));

        // A pasta que o continha fica, com o que mais havia nela: o usuario
        // apontou um arquivo, nao a pasta.
        Assert.True(Directory.Exists(folder));
        Assert.True(File.Exists(alheio));
    }
}
