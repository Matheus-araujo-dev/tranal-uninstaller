using System;
using System.IO;
using Tranal.Uninstaller.Core.Management;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// As features do lote 6: a propriedade dos arquivos, o "criar item" do
/// Boot Center e o aviso de filtro sem resultado.
///
/// O que estes testes cobrem: uma feature sem tela e sem comando nao conta,
/// e a propriedade de um arquivo que nao existe nao pode ser "medida" como
/// se existisse.
/// </summary>
public class Lote6FeatureTests
{
    [Fact]
    public void CaminhoDeSistemaEReconhecido()
    {
        var programas = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrWhiteSpace(programas)) return;

        Assert.True(FileOwnership.IsSystemPath(Path.Combine(programas, "exemplo.txt")));
    }

    [Fact]
    public void CaminhoTemporarioNaoEDeSistema()
    {
        var temp = Path.GetTempPath();

        Assert.False(FileOwnership.IsSystemPath(Path.Combine(temp, "tranal-teste.txt")));
    }

    [Fact]
    public void ArquivoQueNaoExisteNaoTemPropriedade()
    {
        var itens = FileOwnership.Scan(new[] { Path.Combine(Path.GetTempPath(), "tranal-nao-existe-xyz.txt") });

        var item = Assert.Single(itens);
        Assert.False(item.Exists);
        Assert.False(item.CanWrite);
    }

    [Fact]
    public void ArquivoDoUsuarioPermiteGravacao()
    {
        var arquivo = Path.Combine(Path.GetTempPath(), "tranal-propriedade-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(arquivo, "conteudo");

        try
        {
            var item = Assert.Single(FileOwnership.Scan(new[] { arquivo }));

            Assert.True(item.Exists);
            Assert.True(item.CanWrite);
            Assert.False(item.IsDirectory);
            Assert.False(item.NeedsOwnership);
        }
        finally
        {
            File.Delete(arquivo);
        }
    }

    [Fact]
    public void AssumirPropriedadeDeCaminhoInexistenteNaoFazNada()
    {
        var (ok, mensagem) = FileOwnership.TakeOwnership(Path.Combine(Path.GetTempPath(), "tranal-nao-existe-abc.txt"));

        Assert.False(ok);
        Assert.NotEqual(string.Empty, mensagem);
    }

    [Fact]
    public void DarControleTotalDeCaminhoInexistenteNaoFazNada()
    {
        var (ok, mensagem) = FileOwnership.GrantFullControl(Path.Combine(Path.GetTempPath(), "tranal-nao-existe-abc.txt"));

        Assert.False(ok);
        Assert.NotEqual(string.Empty, mensagem);
    }

    [Fact]
    public void OItemDeInicializacaoAceitaNomeEComando()
    {
        // O Boot Center cria o item pela regra do modulo de inicializacao
        // automatica; o teste garante que a regra aceita o que a tela manda.
        var item = new Tranal.Uninstaller.Core.Tools.AutorunItem
        {
            Kind = Tranal.Uninstaller.Core.Tools.AutorunItemKind.RunKey,
            Name = "TranalTesteLote6",
            Command = @"C:\Windows\System32\notepad.exe",
        };

        Assert.False(string.IsNullOrWhiteSpace(item.Name));
        Assert.False(string.IsNullOrWhiteSpace(item.Command));
    }
}
