using System.IO;
using Tranal.Uninstaller.Core;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// As preferencias do usuario por programa: comentario, icone e grupos.
///
/// Corresponde ao "Add Comment" (15216), "Edit Comment" (15217), "Change
/// Icon" (15079), "Add to Group" (15080), "Remove from Group" (15081) e ao
/// gerenciador de grupos (15001) do Revo.
/// </summary>
[Collection("preferencias")]
public class ProgramCustomizationTests : IDisposable
{
    private readonly string _file;
    private readonly InstalledProgram _programa;

    public ProgramCustomizationTests()
    {
        // As preferencias do teste ficam num arquivo temporario: sem isso, um
        // teste escreveria no arquivo real do usuario.
        _file = Path.Combine(Path.GetTempPath(), "tranal-program-data-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        ProgramCustomizationStore.FilePathOverride = _file;
        ProgramCustomizationStore.Reload();

        _programa = new InstalledProgram
        {
            DisplayName = "Programa de Teste",
            RegistryKey = "{chave-de-teste}",
            RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{chave-de-teste}",
        };
    }

    public void Dispose()
    {
        ProgramCustomizationStore.ResetForTests();
        try { if (File.Exists(_file)) File.Delete(_file); }
        catch { /* o temporario e descartavel */ }
    }

    [Fact]
    public void ComentarioDoUsuarioFicaGravado()
    {
        ProgramCustomizationStore.SetComment(_programa, "  Instalar antes de desinstalar o Visual  ");

        // O comentario vai para o disco e volta dele.
        ProgramCustomizationStore.Reload();

        Assert.Equal("Instalar antes de desinstalar o Visual", ProgramCustomizationStore.For(_programa).Comment);
        Assert.True(File.Exists(_file));
    }

    [Fact]
    public void ComentarioVazioRemoveAAnotacao()
    {
        ProgramCustomizationStore.SetComment(_programa, "anotacao");
        ProgramCustomizationStore.SetComment(_programa, "   ");

        ProgramCustomizationStore.Reload();

        Assert.False(ProgramCustomizationStore.Has(_programa));
        Assert.Equal(string.Empty, ProgramCustomizationStore.For(_programa).Comment);
    }

    [Fact]
    public void GrupoCriadoApareceNaLista()
    {
        Assert.Equal(GroupResult.Ok, ProgramCustomizationStore.CreateGroup("Jogos"));
        Assert.Contains("Jogos", ProgramCustomizationStore.Groups);

        // O mesmo nome nao cria dois grupos.
        Assert.Equal(GroupResult.AlreadyExists, ProgramCustomizationStore.CreateGroup("jogos"));
        Assert.Single(ProgramCustomizationStore.Groups);
    }

    [Fact]
    public void ProgramaEntraESaiDoGrupo()
    {
        ProgramCustomizationStore.CreateGroup("Jogos");

        Assert.True(ProgramCustomizationStore.AddToGroup(_programa, "Jogos"));
        Assert.Contains("Jogos", ProgramCustomizationStore.For(_programa).Groups);

        ProgramCustomizationStore.RemoveFromGroup(_programa, "Jogos");
        Assert.DoesNotContain("Jogos", ProgramCustomizationStore.For(_programa).Groups);
    }

    [Fact]
    public void NaoEntraEmGrupoQueNaoExiste()
    {
        Assert.False(ProgramCustomizationStore.AddToGroup(_programa, "Grupo Que Nao Existe"));
        Assert.False(ProgramCustomizationStore.Has(_programa));
    }

    [Fact]
    public void RenomearGrupoMoveOsProgramas()
    {
        ProgramCustomizationStore.CreateGroup("Jogos");
        ProgramCustomizationStore.AddToGroup(_programa, "Jogos");

        Assert.Equal(GroupResult.Ok, ProgramCustomizationStore.RenameGroup("Jogos", "Jogos Antigos"));

        // Um grupo renomeado nao pode deixar os programas orfaos.
        Assert.Contains("Jogos Antigos", ProgramCustomizationStore.For(_programa).Groups);
        Assert.DoesNotContain("Jogos", ProgramCustomizationStore.Groups);
    }

    [Fact]
    public void ApagarGrupoNaoApagaOPrograma()
    {
        ProgramCustomizationStore.CreateGroup("Jogos");
        ProgramCustomizationStore.AddToGroup(_programa, "Jogos");

        Assert.Equal(GroupResult.Ok, ProgramCustomizationStore.DeleteGroup("Jogos"));

        // O grupo e so uma classificacao: o programa continua anotado.
        Assert.DoesNotContain("Jogos", ProgramCustomizationStore.Groups);
        Assert.DoesNotContain("Jogos", ProgramCustomizationStore.For(_programa).Groups);
    }

    [Fact]
    public void IconeEscolhidoFicaGravado()
    {
        ProgramCustomizationStore.SetIcon(_programa, 7, string.Empty);

        ProgramCustomizationStore.Reload();

        Assert.Equal(7, ProgramCustomizationStore.For(_programa).IconIndex);
    }

    [Fact]
    public void IconeDeArquivoFicaGravado()
    {
        ProgramCustomizationStore.SetIcon(_programa, -1, @"C:\icones\meu.ico");

        ProgramCustomizationStore.Reload();

        Assert.Equal(@"C:\icones\meu.ico", ProgramCustomizationStore.For(_programa).IconPath);
        Assert.Equal(-1, ProgramCustomizationStore.For(_programa).IconIndex);
    }

    [Fact]
    public void AAnotacaoPertenceAoProgramaCerto()
    {
        var outro = new InstalledProgram
        {
            DisplayName = "Outro Programa",
            RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{outro}",
        };

        ProgramCustomizationStore.SetComment(_programa, "comentario do primeiro");

        Assert.Equal("comentario do primeiro", ProgramCustomizationStore.For(_programa).Comment);
        Assert.Equal(string.Empty, ProgramCustomizationStore.For(outro).Comment);
    }
}

/// <summary>
/// O catalogo unico de colunas: a lista da tela e a exportacao consomem a
/// mesma fonte.
/// </summary>
[Collection("preferencias")]
public class ProgramColumnsTests : IDisposable
{
    private readonly string _file;

    public ProgramColumnsTests()
    {
        _file = Path.Combine(Path.GetTempPath(), "tranal-program-data-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        ProgramCustomizationStore.FilePathOverride = _file;
        ProgramCustomizationStore.Reload();
    }

    public void Dispose()
    {
        ProgramCustomizationStore.ResetForTests();
        try { if (File.Exists(_file)) File.Delete(_file); }
        catch { /* o temporario e descartavel */ }
    }

    [Fact]
    public void ColunaEscondidaSomeDaLista()
    {
        var visiveis = ProgramColumns.Visible(new[] { "Size", "Company" });

        Assert.DoesNotContain(ProgramColumn.Size, visiveis);
        Assert.DoesNotContain(ProgramColumn.Company, visiveis);
        Assert.Contains(ProgramColumn.Name, visiveis);
    }

    [Fact]
    public void ColunaDoNomeNuncaSome()
    {
        // Sem a coluna do nome a lista perde o sentido.
        var visiveis = ProgramColumns.Visible(new[] { "Name" });

        Assert.Contains(ProgramColumn.Name, visiveis);
    }

    [Fact]
    public void ExportacaoUsaAsColunasEscolhidas()
    {
        var programa = new InstalledProgram { DisplayName = "Meu App", DisplayVersion = "1.2.3" };

        var csv = Exporters.ToCsv(new[] { programa }, new[] { ProgramColumn.Name, ProgramColumn.Version });

        var linhas = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Program;Version", linhas[0].Trim());
        Assert.Equal("Meu App;1.2.3", linhas[1].Trim());
    }

    [Fact]
    public void AnotacaoDoUsuarioApareceNaExportacao()
    {
        var programa = new InstalledProgram
        {
            DisplayName = "Meu App",
            RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{meu-app}",
        };

        ProgramCustomizationStore.SetComment(programa, "nao desinstalar no trabalho");

        var csv = Exporters.ToCsv(new[] { programa }, new[] { ProgramColumn.Name, ProgramColumn.Note });

        Assert.Contains("nao desinstalar no trabalho", csv);
    }

    [Fact]
    public void OValorDaColunaEOMesmoNaTelaENaExportacao()
    {
        var programa = new InstalledProgram
        {
            DisplayName = "Meu App",
            Publisher = "Fabrica X",
            RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{meu-app}",
        };

        ProgramCustomizationStore.CreateGroup("Trabalho");
        ProgramCustomizationStore.AddToGroup(programa, "Trabalho");

        var custom = ProgramCustomizationStore.For(programa);

        // A coluna "Grupo" vale o mesmo para a tela e para a exportacao.
        Assert.Equal("Trabalho", ProgramColumns.Value(ProgramColumn.Group, programa, custom));
        Assert.Contains("Trabalho", Exporters.ToCsv(new[] { programa }, new[] { ProgramColumn.Group }));
    }

    [Fact]
    public void ExportacaoPadraoTemOConjuntoDoRevo()
    {
        var programa = new InstalledProgram { DisplayName = "Meu App" };

        var cabecalho = Exporters.ToCsv(new[] { programa }).Split('\n')[0].Trim();

        // O conjunto padrao e fixo e explicito: o catalogo de colunas da tela
        // pode mudar, mas o formato do arquivo exportado nao.
        Assert.Equal(
            "Program;Version;Type;Date;Company;HomePage;Comments;UninstallString;RegistryKey",
            cabecalho);
    }
}
