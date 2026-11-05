using System.Diagnostics;
using System.IO;
using Tranal.Uninstaller.Core;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O motor nunca atravessa atalho (junction, symlink).
///
/// Medido nesta máquina (.NET 10, <c>mklink /J</c>):
///
/// <list type="bullet">
/// <item><c>Directory.EnumerateFiles(pasta, "*", AllDirectories)</c> atravessa
/// o atalho e traz os arquivos do alvo.</item>
/// <item><c>Directory.Delete(pasta, true)</c> falha com
/// <c>UnauthorizedAccessException</c> ao chegar no atalho: não apaga o
/// alvo, mas deixa a pasta pela metade.</item>
/// <item><c>Directory.Delete(link, false)</c> apaga só o link.</item>
/// </list>
///
/// O alvo de um atalho está FORA da pasta que o usuário mandou remover.
/// Apagar arquivo alheio seria perda de dados não pedida — e o motor de
/// remoção não atravessa atalho em nenhum dos dois caminhos.
/// </summary>
[Collection("preferencias")]
public class SafeTraversalTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly ExcludeList _original;
    private readonly List<string> _junctions = new();

    /// <summary>
    /// Opções herméticas: sem backup (não cria pontos de restauração no
    /// ambiente de quem roda os testes), sem Lixeira e sem adiar para o
    /// próximo reinício (a fila de remoção pendente pertence ao Windows do
    /// usuário).
    /// </summary>
    private static readonly UninstallRemovalOptions Opcoes = new()
    {
        Backup = false,
        UseRecycleBin = false,
        DeferToNextRestart = false,
    };

    public SafeTraversalTests()
    {
        _original = ExcludeList.Current;
        ExcludeList.SetCurrent(new ExcludeList());
    }

    public void Dispose()
    {
        ExcludeList.SetCurrent(_original);

        // Os atalhos saem como atalho: o alvo fica intacto e a limpeza do
        // sandbox não trava.
        foreach (var link in _junctions)
        {
            try { if (Directory.Exists(link)) Directory.Delete(link, recursive: false); }
            catch { /* o sandbox é temporário */ }
        }

        _temp.Dispose();
    }

    /// <summary>
    /// Cria um atalho de pasta (junction). O <c>mklink /J</c> não precisa de
    /// administrador.
    /// </summary>
    private bool TryCreateJunction(string link, string target)
    {
        var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        if (process is null) return false;

        process.WaitForExit();
        return process.ExitCode == 0;
    }

    /// <summary>
    /// Uma pasta com item protegido não fica intacta por inteiro: o que não
    /// era protegido sai, e o relatório diz quanto.
    /// </summary>
    [Fact]
    public void RestoDaPastaComItemProtegidoERemovido()
    {
        var folder = _temp.CreateSubFolder("Programa Com Conteudo");
        var protegido = Path.Combine(folder, "protegido.dat");
        var comum = Path.Combine(folder, "comum.dat");
        File.WriteAllText(protegido, "protegido");
        File.WriteAllText(comum, "comum");

        ExcludeList.Current.AddPath(protegido);

        var report = UninstallEngine.RemoveLeftovers(new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = folder, Detail = "teste", Selected = true },
        }, Opcoes);

        // O conteúdo que não era protegido saiu.
        Assert.False(File.Exists(comum));
        Assert.Equal(1, report.RemovedInsideCount);

        // E o protegido continua, com a pasta em pé.
        Assert.True(File.Exists(protegido));
        Assert.Equal(1, report.ProtectedInsideCount);
        Assert.Equal(1, report.ProtectedCount);
        Assert.Equal(0, report.RemovedCount);
        Assert.True(Directory.Exists(folder));
    }

    /// <summary>
    /// O caminho granular (pasta com item protegido) não atravessa atalho:
    /// o arquivo do outro lado do link sobrevive.
    /// </summary>
    [Fact]
    public void AtalhoDentroDaPastaNaoApagaOAlvoNoCaminhoGranular()
    {
        var folder = _temp.CreateSubFolder("Programa Com Atalho");
        var protegido = Path.Combine(folder, "protegido.dat");
        File.WriteAllText(protegido, "protegido");

        var fora = _temp.CreateSubFolder("Dados De Outro Alheio");
        var vitima = Path.Combine(fora, "vitima.txt");
        File.WriteAllText(vitima, "nao pode ser apagado");

        var link = Path.Combine(folder, "atalho");
        Assert.True(TryCreateJunction(link, fora));
        _junctions.Add(link);

        ExcludeList.Current.AddPath(protegido);

        UninstallEngine.RemoveLeftovers(new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = folder, Detail = "teste", Selected = true },
        }, Opcoes);

        // O arquivo do outro lado do atalho sobrevive: ele está fora da
        // pasta que o usuário mandou remover.
        Assert.True(File.Exists(vitima));

        // E o link sai como link.
        Assert.False(Directory.Exists(link));
    }

    /// <summary>
    /// O caminho normal (pasta sem item protegido) também não atravessa
    /// atalho: o arquivo do outro lado do link sobrevive e a pasta sai.
    /// </summary>
    [Fact]
    public void AtalhoDentroDaPastaNaoApagaOAlvoNoCaminhoNormal()
    {
        var folder = _temp.CreateSubFolder("Programa Com Atalho Sem Protecao");
        var fora = _temp.CreateSubFolder("Dados De Outro Alheio Sem Protecao");
        var vitima = Path.Combine(fora, "vitima.txt");
        File.WriteAllText(vitima, "nao pode ser apagado");

        var link = Path.Combine(folder, "atalho");
        Assert.True(TryCreateJunction(link, fora));
        _junctions.Add(link);

        var report = UninstallEngine.RemoveLeftovers(new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = folder, Detail = "teste", Selected = true },
        }, Opcoes);

        Assert.True(File.Exists(vitima));
        Assert.Equal(1, report.RemovedCount);
        Assert.False(Directory.Exists(folder));
    }

    /// <summary>
    /// Item em uso dentro da pasta não fica em silêncio: ele aparece na
    /// contagem de itens que ficaram para trás e no texto do relatório.
    /// </summary>
    [Fact]
    public void ArquivoEmUsoDentroDaPastaApareceNoRelatorio()
    {
        var folder = _temp.CreateSubFolder("Programa Com Arquivo Em Uso");
        var protegido = Path.Combine(folder, "protegido.dat");
        var travado = Path.Combine(folder, "travado.dat");
        File.WriteAllText(protegido, "protegido");
        File.WriteAllText(travado, "em uso");

        ExcludeList.Current.AddPath(protegido);

        using var stream = new FileStream(travado, FileMode.Open, FileAccess.Read, FileShare.None);

        var report = UninstallEngine.RemoveLeftovers(new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = folder, Detail = "teste", Selected = true },
        }, Opcoes);

        // O arquivo em uso não pode sumir em silêncio.
        Assert.Equal(1, report.FailedInsideCount);
        Assert.Equal(0, report.RemovedInsideCount);

        // E o texto do relatório diz o que ficou.
        var outcome = Assert.Single(report.Outcomes);
        Assert.Equal(RemovalStatus.Protected, outcome.Status);
        Assert.Contains("não puderam ser removidos", outcome.Message);
        Assert.Contains("permaneceu", report.Describe());
    }

    /// <summary>
    /// A remoção da árvore apaga o link e não o alvo.
    /// </summary>
    [Fact]
    public void ArvoreApagadaNaoAtravessaAtalho()
    {
        var folder = _temp.CreateSubFolder("Arvore Com Atalho");
        var fora = _temp.CreateSubFolder("Alvo Do Atalho");
        var vitima = Path.Combine(fora, "vitima.txt");
        File.WriteAllText(vitima, "alvo");

        var link = Path.Combine(folder, "atalho");
        Assert.True(TryCreateJunction(link, fora));
        _junctions.Add(link);

        File.WriteAllText(Path.Combine(folder, "comum.txt"), "comum");

        SafeTraversal.DeleteDirectoryTree(folder);

        Assert.True(File.Exists(vitima));
        Assert.False(Directory.Exists(folder));
    }

    /// <summary>
    /// A contagem de tamanho de uma pasta não inclui o alvo do atalho: ele
    /// pode ser outra unidade inteira, e a conta não terminaria.
    /// </summary>
    [Fact]
    public void TamanhoDaPastaNaoContaOAlvoDoAtalho()
    {
        var folder = _temp.CreateSubFolder("Pasta Medida");
        var fora = _temp.CreateSubFolder("Alvo Medido");
        File.WriteAllBytes(Path.Combine(fora, "grande.bin"), new byte[4096]);

        var link = Path.Combine(folder, "atalho");
        Assert.True(TryCreateJunction(link, fora));
        _junctions.Add(link);

        File.WriteAllBytes(Path.Combine(folder, "dentro.bin"), new byte[10]);

        Assert.Equal(10, SafeTraversal.MeasureSize(folder));
    }

    /// <summary>
    /// Quando o próprio item de remoção é um atalho, o alvo — que está fora
    /// da pasta pedida — não é apagado. O link sai como link.
    /// </summary>
    [Fact]
    public void AtalhoComoRaizNaoApagaOAlvo()
    {
        var fora = _temp.CreateSubFolder("Alvo Do Atalho Como Raiz");
        var vitima = Path.Combine(fora, "vitima.txt");
        File.WriteAllText(vitima, "nao pode ser apagado");

        var link = Path.Combine(_temp.Root, "atalho-raiz");
        Assert.True(TryCreateJunction(link, fora));
        _junctions.Add(link);

        var report = UninstallEngine.RemoveLeftovers(new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = link, Detail = "teste", Selected = true },
        }, Opcoes);

        // O alvo sobrevive: ele está fora da pasta que o usuário mandou
        // remover.
        Assert.True(File.Exists(vitima));

        // E o link sai como link.
        Assert.False(Directory.Exists(link));

        // Nada de dentro do alvo foi removido.
        Assert.Equal(0, report.RemovedInsideCount);
    }

    /// <summary>
    /// Um atalho como raiz não burla a lista de exclusões: o item protegido
    /// continua no lugar, mesmo quando o caminho chega pelo link.
    /// </summary>
    [Fact]
    public void AtalhoComoRaizNaoBurlaAListaDeExclusoes()
    {
        var fora = _temp.CreateSubFolder("Alvo Com Item Protegido");
        var protegido = Path.Combine(fora, "protegido.dat");
        File.WriteAllText(protegido, "protegido");

        var link = Path.Combine(_temp.Root, "atalho-protegido");
        Assert.True(TryCreateJunction(link, fora));
        _junctions.Add(link);

        // A lista guarda o caminho real do item protegido.
        ExcludeList.Current.AddPath(protegido);

        UninstallEngine.RemoveLeftovers(new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = link, Detail = "teste", Selected = true },
        }, Opcoes);

        // A lista de exclusões é a última palavra: o item protegido fica.
        Assert.True(File.Exists(protegido));
    }

    /// <summary>
    /// A varredura profunda de arquivos não atravessa atalho: um limpador
    /// que usasse essa lista removeria arquivos de fora do escopo.
    /// </summary>
    [Fact]
    public void VarreduraProfundaNaoAtravessaAtalho()
    {
        var folder = _temp.CreateSubFolder("Pasta Percorrida");
        var fora = _temp.CreateSubFolder("Alvo Da Varredura");
        File.WriteAllText(Path.Combine(fora, "vitima.txt"), "fora do escopo");

        var link = Path.Combine(folder, "atalho");
        Assert.True(TryCreateJunction(link, fora));
        _junctions.Add(link);

        File.WriteAllText(Path.Combine(folder, "dentro.txt"), "dentro");

        var files = SafeTraversal.EnumerateFilesDeep(folder).ToList();

        Assert.Contains(files, f => f.EndsWith("dentro.txt", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(files, f => f.EndsWith("vitima.txt", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// O backup não copia o alvo do atalho: o alvo está fora da pasta que o
    /// usuário mandou fazer backup.
    /// </summary>
    [Fact]
    public void BackupNaoCopiaAtravesDoAtalho()
    {
        var folder = _temp.CreateSubFolder("Programa Com Atalho No Backup");
        var fora = _temp.CreateSubFolder("Alvo Fora Do Escopo Do Backup");
        File.WriteAllText(Path.Combine(fora, "vitima.txt"), "fora do escopo");

        var link = Path.Combine(folder, "atalho");
        Assert.True(TryCreateJunction(link, fora));
        _junctions.Add(link);

        File.WriteAllText(Path.Combine(folder, "dentro.txt"), "dentro");

        var backupRoot = Path.Combine(Path.GetTempPath(), "tranal-backup-atalho-" + Guid.NewGuid().ToString("N")[..8]);
        BackupManager.RootDirectoryOverride = backupRoot;
        try
        {
            var point = BackupManager.Create("Pasta", BackupManager.OperationFiles);
            Assert.True(BackupManager.AddFolder(point, folder));

            var copied = Path.Combine(point.FolderPath, "files", $"0000-{Path.GetFileName(folder)}");

            // O conteúdo de dentro da pasta foi copiado.
            Assert.True(File.Exists(Path.Combine(copied, "dentro.txt")));

            // E o alvo do atalho não: ele está fora do escopo do backup.
            Assert.False(File.Exists(Path.Combine(copied, "atalho", "vitima.txt")));
        }
        finally
        {
            BackupManager.RootDirectoryOverride = null;
            try { if (Directory.Exists(backupRoot)) Directory.Delete(backupRoot, recursive: true); }
            catch { /* limpeza best effort */ }
        }
    }

    /// <summary>
    /// O backup de uma RAIZ que é atalho não copia o alvo: o alvo está fora
    /// da pasta que o usuário mandou fazer backup.
    /// </summary>
    [Fact]
    public void BackupDeUmaRaizAtalhoNaoCopiaOAlvo()
    {
        var fora = _temp.CreateSubFolder("Alvo Do Backup De Atalho");
        File.WriteAllText(Path.Combine(fora, "vitima.txt"), "fora do escopo");

        var link = Path.Combine(_temp.Root, "atalho-raiz-backup");
        Assert.True(TryCreateJunction(link, fora));
        _junctions.Add(link);

        var backupRoot = Path.Combine(Path.GetTempPath(), "tranal-backup-raiz-" + Guid.NewGuid().ToString("N")[..8]);
        BackupManager.RootDirectoryOverride = backupRoot;
        try
        {
            var point = BackupManager.Create("Pasta", BackupManager.OperationFiles);
            BackupManager.AddFolder(point, link);

            var copied = Path.Combine(point.FolderPath, "files", $"0000-{Path.GetFileName(link)}");

            Assert.False(File.Exists(Path.Combine(copied, "vitima.txt")));
        }
        finally
        {
            BackupManager.RootDirectoryOverride = null;
            try { if (Directory.Exists(backupRoot)) Directory.Delete(backupRoot, recursive: true); }
            catch { /* limpeza best effort */ }
        }
    }
}
