using System.IO;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// A integridade do arquivo de preferencias do usuario
/// (program-data.json).
///
/// Sao as garantias que a documentacao do Lote 2 promete:
///  - um arquivo invalido NAO e sobrescrito: e preservado como ".corrompido";
///  - a gravacao e atomica e deixa uma copia em ".bak";
///  - a falha e avisada, e nada some em silencio.
///
/// Cada teste aqui reproduz um defeito encontrado na revisao adversarial:
/// o guarda cobria so JSON que nao deserializa, e nao JSON que deserializa
/// inutilizavel (lista nula); o fallback da troca atomica descartava o
/// ".bak"; e o temporario sobrava na pasta de dados quando a gravacao
/// falhava.
/// </summary>
[Collection("preferencias")]
public class ProgramDataIntegrityTests : IDisposable
{
    private readonly string _file;
    private readonly InstalledProgram _programa;

    public ProgramDataIntegrityTests()
    {
        _file = Path.Combine(Path.GetTempPath(),
            "tranal-integridade-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");

        ProgramCustomizationStore.FilePathOverride = _file;
        ProgramCustomizationStore.Reload();

        _programa = new InstalledProgram
        {
            DisplayName = "Programa de Teste",
            RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{teste}",
        };
    }

    public void Dispose()
    {
        ProgramCustomizationStore.ResetForTests();

        try
        {
            if (File.Exists(_file)) File.Delete(_file);
            if (File.Exists(_file + ".tmp")) File.Delete(_file + ".tmp");
            if (File.Exists(_file + ".bak")) File.Delete(_file + ".bak");
            if (File.Exists(_file + ".corrompido")) File.Delete(_file + ".corrompido");
        }
        catch { /* o temporario e descartavel */ }
    }

    /// <summary>
    /// JSON valido com lista nula deserializa sem erro — e mesmo assim
    /// inutiliza a lista inteira. O arquivo do usuario e preservado.
    /// </summary>
    [Fact]
    public void JsonComListaNulaEpreservadoEAvisado()
    {
        File.WriteAllText(_file, "{\"Groups\": null, \"Programs\": null}");

        ProgramCustomizationStore.Reload();

        Assert.Equal(LoadState.Preserved, ProgramCustomizationStore.LastLoad);
        Assert.False(File.Exists(_file));
        Assert.True(File.Exists(_file + ".corrompido"));

        // O conteudo do usuario continua inteiro no arquivo preservado.
        Assert.Contains("null", File.ReadAllText(_file + ".corrompido"));

        // E a loja volta vazia sem estourar: a lista inteira do programa
        // depende dela (uma exception aqui derrubava a listagem no arranque).
        Assert.Empty(ProgramCustomizationStore.Groups);
        Assert.False(ProgramCustomizationStore.Has(_programa));
    }

    /// <summary>
    /// Um item nulo dentro da lista deserializa sem erro e estoura em
    /// <c>Has</c> e <c>For</c>. O arquivo e preservado do mesmo jeito.
    /// </summary>
    [Fact]
    public void JsonComItemNuloNaListaEpreservado()
    {
        File.WriteAllText(_file, "{\"Groups\": [], \"Programs\": [null]}");

        ProgramCustomizationStore.Reload();

        Assert.Equal(LoadState.Preserved, ProgramCustomizationStore.LastLoad);
        Assert.True(File.Exists(_file + ".corrompido"));
        Assert.False(ProgramCustomizationStore.Has(_programa));
    }

    /// <summary>
    /// Um programa anotado com a lista de grupos nula tambem inutiliza o
    /// arquivo: <c>Prune</c> contaria os grupos.
    /// </summary>
    [Fact]
    public void JsonComGruposNulosNoItemEpreservado()
    {
        File.WriteAllText(_file,
            "{\"Groups\": [], \"Programs\": [{\"Key\": \"x\", \"Comment\": \"\", \"IconIndex\": -1, \"IconPath\": \"\", \"Groups\": null}]}");

        ProgramCustomizationStore.Reload();

        Assert.Equal(LoadState.Preserved, ProgramCustomizationStore.LastLoad);
        Assert.True(File.Exists(_file + ".corrompido"));
    }

    /// <summary>
    /// A exportacao e uma operacao de LEITURA: ela nao pode renomear o
    /// arquivo do usuario nem disparar a preservacao.
    /// </summary>
    [Fact]
    public void ExportacaoNaoMexeNoArquivoDoUsuario()
    {
        // JSON valido e inutilizavel: e o caminho que passa pelo guarda de
        // uso (um JSON que nem deserializa nem chega la).
        File.WriteAllText(_file, "{\"Groups\": null, \"Programs\": null}");

        var csv = Exporters.ToCsv(new[] { _programa }, new[] { ProgramColumn.Name });

        Assert.Contains("Programa de Teste", csv);

        // O arquivo continua onde estava, com o mesmo conteudo.
        var naPasta = string.Join(" | ", Directory.GetFiles(Path.GetDirectoryName(_file)!,
            "tranal-integridade-*"));
        Assert.True(File.Exists(_file), "arquivos na pasta: " + naPasta);
        Assert.False(File.Exists(_file + ".corrompido"));
        Assert.Equal("{\"Groups\": null, \"Programs\": null}", File.ReadAllText(_file));
    }

    /// <summary>
    /// O mesmo para um arquivo que nem deserializa.
    /// </summary>
    [Fact]
    public void ExportacaoNaoMexeEmArquivoIlegivel()
    {
        File.WriteAllText(_file, "{ isto nao e json }");

        var csv = Exporters.ToCsv(new[] { _programa }, new[] { ProgramColumn.Name });

        Assert.Contains("Programa de Teste", csv);

        Assert.True(File.Exists(_file));
        Assert.False(File.Exists(_file + ".corrompido"));
        Assert.Equal("{ isto nao e json }", File.ReadAllText(_file));
    }

    /// <summary>
    /// Quando a troca atomica nao da para ser feita, a gravacao e abortada
    /// em vez de sobrescrever o arquivo sem deixar copia. E o caminho que
    /// descarta o ".bak" prometido na documentacao.
    /// </summary>
    [Fact]
    public void GravacaoSemCopiaDeSegurancaNaoSobrescreveOOriginal()
    {
        File.WriteAllText(_file, "ANTIGO");

        // Um diretorio no lugar do ".bak" faz a troca atomica falhar — e
        // tambem impede a copia de seguranca.
        Directory.CreateDirectory(_file + ".bak");

        Assert.False(ProgramCustomizationStore.Save());

        // O original continua intacto: nada foi sobrescrito sem backup.
        Assert.Equal("ANTIGO", File.ReadAllText(_file));

        // E o temporario nao sobra: ele carrega os dados do usuario em texto
        // claro dentro da pasta de dados.
        Assert.False(File.Exists(_file + ".tmp"));
    }

    /// <summary>
    /// A gravacao bem-sucedida deixa a versao anterior no ".bak".
    /// </summary>
    [Fact]
    public void GravacaoComSucessoGuardaAVersaoAnterior()
    {
        ProgramCustomizationStore.SetComment(_programa, "primeira anotacao");

        var primeira = File.ReadAllText(_file);
        ProgramCustomizationStore.SetComment(_programa, "segunda anotacao");

        Assert.True(File.Exists(_file + ".bak"));
        Assert.Equal(primeira, File.ReadAllText(_file + ".bak"));
        Assert.Contains("segunda anotacao", File.ReadAllText(_file));
    }

    /// <summary>
    /// O conflito de nome e a falha de gravacao sao coisas diferentes, e a
    /// janela precisa dizer a diferença: "ja existe" busca um problema que
    /// pode nao existir.
    /// </summary>
    [Fact]
    public void ConflitoDeNomeEFalhaDeGravacaoSaoCoisasDiferentes()
    {
        Assert.Equal(GroupResult.Ok, ProgramCustomizationStore.CreateGroup("Jogos"));
        Assert.Equal(GroupResult.AlreadyExists, ProgramCustomizationStore.CreateGroup("jogos"));

        // Agora a gravacao falha: a pasta de dados esta bloqueada.
        var pasta = Path.Combine(Path.GetTempPath(),
            "tranal-integridade-dir-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(pasta);
        var bloqueio = Path.Combine(pasta, "bloqueio");
        File.WriteAllText(bloqueio, "x");

        var arquivoOriginal = ProgramCustomizationStore.FilePathOverride;
        ProgramCustomizationStore.FilePathOverride = Path.Combine(bloqueio, "program-data.json");

        Assert.Equal(GroupResult.SaveFailed, ProgramCustomizationStore.CreateGroup("Outro"));

        ProgramCustomizationStore.FilePathOverride = arquivoOriginal;

        try { Directory.Delete(pasta, true); } catch { /* o temporario e descartavel */ }
    }

    /// <summary>
    /// Renomear e apagar que falham na gravacao desfazem a mudanca na
    /// memoria: sem isso a tela mostraria o nome novo enquanto o disco
    /// ainda teria o antigo.
    /// </summary>
    [Fact]
    public void RenomearQueFalhaDesfazAMudancaNaMemoria()
    {
        ProgramCustomizationStore.CreateGroup("Jogos");
        ProgramCustomizationStore.AddToGroup(_programa, "Jogos");

        var pasta = Path.Combine(Path.GetTempPath(),
            "tranal-integridade-dir-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(pasta);
        var bloqueio = Path.Combine(pasta, "bloqueio");
        File.WriteAllText(bloqueio, "x");

        var arquivoOriginal = ProgramCustomizationStore.FilePathOverride;
        ProgramCustomizationStore.FilePathOverride = Path.Combine(bloqueio, "program-data.json");

        Assert.Equal(GroupResult.SaveFailed, ProgramCustomizationStore.RenameGroup("Jogos", "Antigos"));

        ProgramCustomizationStore.FilePathOverride = arquivoOriginal;

        // O nome continua o de antes: nem na tela, nem no disco.
        Assert.Contains("Jogos", ProgramCustomizationStore.Groups);
        Assert.DoesNotContain("Antigos", ProgramCustomizationStore.Groups);

        try { Directory.Delete(pasta, true); } catch { /* o temporario e descartavel */ }
    }

    /// <summary>
    /// A chave de reserva (programa sem chave de registro) tambem leva a
    /// visao do registro: dois homonimos de visoes diferentes sao entradas
    /// diferentes.
    /// </summary>
    [Fact]
    public void AChaveDeReservaTambemDistingueAVisao()
    {
        var de32 = new InstalledProgram { DisplayName = "Programa", View = RegistryView.Registry32 };
        var de64 = new InstalledProgram { DisplayName = "Programa", View = RegistryView.Registry64 };

        Assert.NotEqual(ProgramCustomizationStore.KeyOf(de32), ProgramCustomizationStore.KeyOf(de64));
    }

    /// <summary>
    /// A anotacao gravada aparece no arquivo exportado: a exportacao le o
    /// arquivo do usuario, sem depender da loja em memoria.
    /// </summary>
    [Fact]
    public void AExportacaoLeAsAnotacoesDoDisco()
    {
        ProgramCustomizationStore.SetComment(_programa, "nao mexer");

        // A loja em memoria e recarregada: a exportacao precisa ler do
        // disco, porque e uma operacao que nao pode alterar nada.
        var csv = Exporters.ToCsv(new[] { _programa }, new[] { ProgramColumn.Note });

        Assert.Contains("nao mexer", csv);
    }
}
