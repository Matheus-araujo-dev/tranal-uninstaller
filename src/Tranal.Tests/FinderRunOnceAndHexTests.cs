using System;
using System.IO;
using System.Text;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O localizador de arquivos e chaves, o agendamento para o proximo logon e
/// o visor hexadecimal.
/// </summary>
public class FinderRunOnceAndHexTests : IDisposable
{
    private readonly string _temp;

    public FinderRunOnceAndHexTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "tranal-inspecao-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        // Os itens agendados vao para a chave RunOnce do usuario: o teste
        // limpa o que criou, para nao deixar rastro no ambiente.
        foreach (var item in RunOnceScheduler.List())
        {
            var separador = item.IndexOf('=');
            var nome = separador > 0 ? item[..separador].Trim() : item;
            RunOnceScheduler.Cancel(nome);
        }

        try
        {
            if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
        }
        catch (IOException)
        {
            // Temporario do sistema: o proprio Windows limpa depois.
        }
    }

    // ------------------------------------------------------------------
    // Localizador
    // ------------------------------------------------------------------

    [Fact]
    public void CasaAceitaCuringaDoWindows()
    {
        Assert.True(ResourceFinder.Casa("programa.log", "*.log"));
        Assert.True(ResourceFinder.Casa("programa.log", "programa.?og"));
        Assert.False(ResourceFinder.Casa("programa.txt", "*.log"));
    }

    [Fact]
    public void CasaAceitaTrechoDoNome()
    {
        Assert.True(ResourceFinder.Casa("MeuPrograma.exe", "programa"));
        Assert.False(ResourceFinder.Casa("MeuPrograma.exe", "outro"));
    }

    [Fact]
    public void LocalizadorEncontraArquivoNaPasta()
    {
        var arquivo = Path.Combine(_temp, "alvo.log");
        File.WriteAllText(arquivo, "conteudo");

        var achados = ResourceFinder.FindFiles(_temp, "*.log");

        Assert.Contains(achados, a => a.Path.EndsWith("alvo.log", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LocalizadorEmPastaInexistenteNaoQuebra()
    {
        var achados = ResourceFinder.FindFiles(Path.Combine(_temp, "nao-existe"), "*");

        Assert.Empty(achados);
    }

    [Fact]
    public void LocalizadorDeRegistroNaoQuebra()
    {
        var achados = ResourceFinder.FindRegistryKeys(@"HKEY_LOCAL_MACHINE\SOFTWARE", "Microsoft");

        Assert.NotNull(achados);
    }

    // ------------------------------------------------------------------
    // Acao agendada
    // ------------------------------------------------------------------

    [Fact]
    public void AcaoAgendadaECancelada()
    {
        var nome = "Tranal_Teste_" + Guid.NewGuid().ToString("N").Substring(0, 8);

        var agendado = RunOnceScheduler.Schedule(nome, "cmd.exe /c teste");
        Assert.True(agendado.Scheduled);
        Assert.Contains(RunOnceScheduler.List(), i => i.StartsWith(nome, StringComparison.OrdinalIgnoreCase));

        Assert.True(RunOnceScheduler.Cancel(nome));
        Assert.DoesNotContain(RunOnceScheduler.List(), i => i.StartsWith(nome, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AgendarSemComandoNaoFunciona()
    {
        var resultado = RunOnceScheduler.Schedule("Tranal_SemComando", "   ");

        Assert.False(resultado.Scheduled);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Message));
    }

    [Fact]
    public void AgendarDesinstalacaoMontaONomeDoItem()
    {
        var resultado = RunOnceScheduler.ScheduleUninstall("Meu Programa", "unins000.exe /VERYSILENT");

        Assert.True(resultado.Scheduled);
        Assert.Contains("Meu_Programa", resultado.RegistryPath);

        RunOnceScheduler.Cancel("Tranal_Meu_Programa");
    }

    // ------------------------------------------------------------------
    // Visor hexadecimal
    // ------------------------------------------------------------------

    [Fact]
    public void VisorHexagonalMostraOsBytes()
    {
        var arquivo = Path.Combine(_temp, "bytes.bin");
        File.WriteAllBytes(arquivo, new byte[] { 0x41, 0x42, 0x43 });

        var linhas = HexViewer.Dump(arquivo);

        Assert.NotEmpty(linhas);
        Assert.Equal("41 42 43", linhas[0].Hex);
        Assert.Equal("ABC", linhas[0].Text);
        Assert.Equal(0, linhas[0].Offset);
    }

    [Fact]
    public void SomaMd5EReproduzivel()
    {
        var arquivo = Path.Combine(_temp, "soma.txt");
        File.WriteAllText(arquivo, "tranal", Encoding.UTF8);

        var md5 = HexViewer.Md5Of(arquivo);

        Assert.Equal(32, md5.Length);
        Assert.Equal(md5, HexViewer.Md5Of(arquivo));
        Assert.True(HexViewer.MatchesMd5(arquivo, md5));
        Assert.False(HexViewer.MatchesMd5(arquivo, "00000000000000000000000000000000"));
    }

    [Fact]
    public void VisorDeArquivoInexistenteNaoQuebra()
    {
        Assert.Empty(HexViewer.Dump(Path.Combine(_temp, "nao-existe.bin")));
        Assert.Equal(string.Empty, HexViewer.Md5Of(Path.Combine(_temp, "nao-existe.bin")));
    }
}
