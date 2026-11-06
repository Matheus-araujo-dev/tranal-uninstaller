using System.IO;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Localization;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O painel lateral de configuracoes: o criterio de filtragem e o de realce.
///
/// A REGRA mora no nucleo (<see cref="ProgramFilterSet"/>), e nao na tela: a
/// janela principal reaplica o mesmo conjunto que o painel edita. Estes
/// testes sao do nucleo (a suite nao referencia o WPF) e conferem o
/// julgamento: quem casa, quem nao casa, e a cor do realce.
///
/// Na colecao "preferencias" porque a anotacao do usuario por programa e um
/// estado global (o caminho do arquivo e redirecionado para um temporario,
/// para nao tocar no ambiente de quem roda os testes).
/// </summary>
[Collection("preferencias")]
public class SettingsPanelTests : IDisposable
{
    private const long Mb = 1024L * 1024L;
    private const long Gb = 1024L * Mb;

    private readonly string _file;

    public SettingsPanelTests()
    {
        // As preferencias do teste ficam num arquivo temporario: sem isso, um
        // teste escreveria no arquivo real do usuario.
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

    // ------------------------------------------------------------------
    // Busca
    // ------------------------------------------------------------------

    [Fact]
    public void BuscaCasaPeloNomeDoPrograma()
    {
        var programa = Programa("Visual Studio 2022");
        var filtros = new ProgramFilterSet { SearchText = "studio" };

        Assert.True(filtros.Matches(programa));
    }

    [Fact]
    public void BuscaCasaPeloNomeDaEditora()
    {
        var programa = Programa("Visual Studio 2022", "Microsoft");
        var filtros = new ProgramFilterSet { SearchText = "micro" };

        Assert.True(filtros.Matches(programa));
    }

    [Fact]
    public void BuscaCasaPeloComentarioDoRegistro()
    {
        var programa = Programa("Programa");
        programa.Comments = "Instalador de teste";

        var filtros = new ProgramFilterSet { SearchText = "instalador" };

        Assert.True(filtros.Matches(programa));
    }

    [Fact]
    public void BuscaCasaPeloComentarioDoUsuario()
    {
        var programa = Programa("Programa");
        ProgramCustomizationStore.SetComment(programa, "guardar o instalador");
        ProgramCustomizationStore.Reload();

        var filtros = new ProgramFilterSet { SearchText = "instalador" };

        Assert.True(filtros.Matches(programa));
    }

    [Fact]
    public void BuscaNaoCasaQuandoOTextoNaoAparece()
    {
        var programa = Programa("Visual Studio 2022", "Microsoft");
        var filtros = new ProgramFilterSet { SearchText = "ausente" };

        Assert.False(filtros.Matches(programa));
    }

    [Fact]
    public void BuscaVaziaDeixaPassarTudo()
    {
        var filtros = new ProgramFilterSet { SearchText = string.Empty };

        Assert.True(filtros.Matches(Programa("Um")));
        Assert.True(filtros.Matches(Programa("Outro")));
    }

    [Fact]
    public void BuscaNaoQuebraComProgramaSemAnotacao()
    {
        // O programa nao tem anotacao do usuario: o filtro precisa perguntar
        // antes, e nao estourar excecao.
        var programa = Programa("Sem anotacao");
        var filtros = new ProgramFilterSet { SearchText = "sem" };

        Assert.True(filtros.Matches(programa));
    }

    // ------------------------------------------------------------------
    // Filtro de tipo
    // ------------------------------------------------------------------

    [Fact]
    public void FiltroDeTipoEscolheOsProgramosDoBitsCerto()
    {
        var de32 = Programa(bits: "32-bits");
        var de64 = Programa(bits: "64-bits");

        var filtros = new ProgramFilterSet { TypeFilter = "32" };
        Assert.True(filtros.Matches(de32));
        Assert.False(filtros.Matches(de64));

        filtros.TypeFilter = "64";
        Assert.True(filtros.Matches(de64));
        Assert.False(filtros.Matches(de32));
    }

    [Fact]
    public void FiltroDeTipoMsiEscolheOsPacotesDoWindowsInstaller()
    {
        var filtros = new ProgramFilterSet { TypeFilter = "msi" };

        Assert.True(filtros.Matches(Programa(msi: true)));
        Assert.False(filtros.Matches(Programa(msi: false)));
    }

    [Fact]
    public void FiltroDeTipoAllPassaTudo()
    {
        var filtros = new ProgramFilterSet { TypeFilter = "All" };

        Assert.True(filtros.Matches(Programa(bits: "32-bits")));
        Assert.True(filtros.Matches(Programa(bits: "64-bits")));
        Assert.True(filtros.Matches(Programa(msi: true)));
    }

    // ------------------------------------------------------------------
    // Filtro de tamanho
    // ------------------------------------------------------------------

    [Fact]
    public void FiltroDeTamanhoTemAsTresFaixas()
    {
        var filtros = new ProgramFilterSet { SizeFilter = "small" };
        Assert.True(filtros.Matches(Programa(tamanho: 10 * Mb)));
        Assert.False(filtros.Matches(Programa(tamanho: 500 * Mb)));

        filtros.SizeFilter = "medium";
        Assert.True(filtros.Matches(Programa(tamanho: 500 * Mb)));
        Assert.False(filtros.Matches(Programa(tamanho: 2 * Gb)));

        filtros.SizeFilter = "large";
        Assert.True(filtros.Matches(Programa(tamanho: 2 * Gb)));
        Assert.False(filtros.Matches(Programa(tamanho: 500 * Mb)));
    }

    [Fact]
    public void OsLimitesDeTamanhoSaoExatos()
    {
        var filtros = new ProgramFilterSet { SizeFilter = "small" };

        // Pequeno: ate 100 MB, sem os 100 MB.
        Assert.True(filtros.Matches(Programa(tamanho: 100 * Mb - 1)));
        Assert.False(filtros.Matches(Programa(tamanho: 100 * Mb)));

        // Medio: de 100 MB ate 1 GB, com os dois extremos.
        filtros.SizeFilter = "medium";
        Assert.True(filtros.Matches(Programa(tamanho: 100 * Mb)));
        Assert.True(filtros.Matches(Programa(tamanho: Gb)));
        Assert.False(filtros.Matches(Programa(tamanho: 100 * Mb - 1)));
        Assert.False(filtros.Matches(Programa(tamanho: Gb + 1)));

        // Grande: acima de 1 GB.
        filtros.SizeFilter = "large";
        Assert.True(filtros.Matches(Programa(tamanho: Gb + 1)));
        Assert.False(filtros.Matches(Programa(tamanho: Gb)));
    }

    [Fact]
    public void FiltroDeTamanhoAllPassaTudo()
    {
        var filtros = new ProgramFilterSet { SizeFilter = "all" };

        Assert.True(filtros.Matches(Programa(tamanho: 0)));
        Assert.True(filtros.Matches(Programa(tamanho: 10 * Mb)));
        Assert.True(filtros.Matches(Programa(tamanho: 5 * Gb)));
    }

    // ------------------------------------------------------------------
    // Filtro de unidade
    // ------------------------------------------------------------------

    [Fact]
    public void FiltroDeUnidadeCasaComOCaminhoDaUnidade()
    {
        var filtros = new ProgramFilterSet { DriveFilter = @"C:\" };

        Assert.True(filtros.Matches(Programa(local: @"C:\Program Files\Programa")));
        Assert.False(filtros.Matches(Programa(local: @"D:\Program Files\Programa")));
    }

    [Fact]
    public void FiltroDeUnidadeNuloDeixaPassarTudo()
    {
        var filtros = new ProgramFilterSet { DriveFilter = null };

        Assert.True(filtros.Matches(Programa(local: @"C:\Program Files\Programa")));
        Assert.True(filtros.Matches(Programa(local: @"D:\Program Files\Programa")));
    }

    [Fact]
    public void FiltroDeUnidadeUsaOComandoDeDesinstalacaoQuandoNaoHaPasta()
    {
        var programa = Programa(local: string.Empty);
        programa.UninstallString = @"E:\Apps\Programa\unins000.exe";

        var filtros = new ProgramFilterSet { DriveFilter = @"E:\" };

        Assert.True(filtros.Matches(programa));
    }

    // ------------------------------------------------------------------
    // Filtro de editora
    // ------------------------------------------------------------------

    [Fact]
    public void FiltroDeEditoraCasaComONomeDaEditora()
    {
        var programa = Programa("Programa", "Microsoft Corporation");

        var filtros = new ProgramFilterSet { PublisherFilter = "corporation" };
        Assert.True(filtros.Matches(programa));

        filtros.PublisherFilter = "outra editora";
        Assert.False(filtros.Matches(programa));
    }

    // ------------------------------------------------------------------
    // So com comentario / so com grupo
    // ------------------------------------------------------------------

    [Fact]
    public void SoComComentarioAceitaSoOProgramaAnotado()
    {
        var anotado = Programa("Anotado");
        var semAnotacao = Programa("Sem anotacao");

        ProgramCustomizationStore.SetComment(anotado, "guardar o instalador");
        ProgramCustomizationStore.Reload();

        var filtros = new ProgramFilterSet { OnlyWithComments = true };

        Assert.True(filtros.Matches(anotado));
        Assert.False(filtros.Matches(semAnotacao));
    }

    [Fact]
    public void SoComGrupoAceitaSoOProgramaEmAlgumGrupo()
    {
        var noGrupo = Programa("No grupo");
        var fora = Programa("Fora");

        ProgramCustomizationStore.CreateGroup("Jogos");
        ProgramCustomizationStore.AddToGroup(noGrupo, "Jogos");
        ProgramCustomizationStore.Reload();

        var filtros = new ProgramFilterSet { OnlyWithGroups = true };

        Assert.True(filtros.Matches(noGrupo));
        Assert.False(filtros.Matches(fora));
    }

    // ------------------------------------------------------------------
    // Realce
    // ------------------------------------------------------------------

    [Fact]
    public void RegraQueCasaDevolveAChaveDeCor()
    {
        var programa = Programa("Visual Studio 2022");

        var filtros = new ProgramFilterSet();
        filtros.Highlights.Add(new HighlightRule { Pattern = "visual", ColorKey = HighlightPalette.Danger });

        Assert.Equal(HighlightPalette.Danger, filtros.HighlightColorOf(programa));
    }

    [Fact]
    public void APrimeiraRegraQueCasaVence()
    {
        var programa = Programa("Visual Studio 2022");

        var filtros = new ProgramFilterSet();
        filtros.Highlights.Add(new HighlightRule { Pattern = "visual", ColorKey = HighlightPalette.Danger });
        filtros.Highlights.Add(new HighlightRule { Pattern = "studio", ColorKey = HighlightPalette.Success });

        Assert.Equal(HighlightPalette.Danger, filtros.HighlightColorOf(programa));
    }

    [Fact]
    public void RegraDesabilitadaEIgnorada()
    {
        var programa = Programa("Visual Studio 2022");

        var filtros = new ProgramFilterSet();
        filtros.Highlights.Add(new HighlightRule { Pattern = "visual", ColorKey = HighlightPalette.Danger, Enabled = false });

        Assert.Null(filtros.HighlightColorOf(programa));
    }

    [Fact]
    public void PadraoVazioNuncaCasa()
    {
        var programa = Programa("Visual Studio 2022");
        var regra = new HighlightRule { Pattern = string.Empty };

        Assert.False(regra.Matches(programa));

        var filtros = new ProgramFilterSet();
        filtros.Highlights.Add(regra);

        Assert.Null(filtros.HighlightColorOf(programa));
    }

    [Fact]
    public void OEscopoDoRealceOlhaOTextoCerto()
    {
        var programa = Programa("Visual Studio", "Microsoft", local: @"D:\Apps\Visual Studio");
        programa.UninstallString = @"D:\Apps\Visual Studio\unins.exe";

        Assert.True(new HighlightRule { Scope = HighlightScope.Name, Pattern = "visual" }.Matches(programa));
        Assert.True(new HighlightRule { Scope = HighlightScope.Publisher, Pattern = "microsoft" }.Matches(programa));
        Assert.True(new HighlightRule { Scope = HighlightScope.InstallLocation, Pattern = "apps" }.Matches(programa));
        Assert.True(new HighlightRule { Scope = HighlightScope.UninstallString, Pattern = "unins" }.Matches(programa));
    }

    [Fact]
    public void OEscopoNaoOlhaParaOTextoDeOutroEscopo()
    {
        var programa = Programa("Visual Studio", "Microsoft");

        Assert.False(new HighlightRule { Scope = HighlightScope.Name, Pattern = "microsoft" }.Matches(programa));
        Assert.False(new HighlightRule { Scope = HighlightScope.Publisher, Pattern = "visual" }.Matches(programa));
        Assert.False(new HighlightRule { Scope = HighlightScope.InstallLocation, Pattern = "visual" }.Matches(programa));
        Assert.False(new HighlightRule { Scope = HighlightScope.UninstallString, Pattern = "visual" }.Matches(programa));
    }

    [Fact]
    public void OTextoDoEscopoEOTextoDoPrograma()
    {
        var programa = Programa("Visual Studio", "Microsoft", local: @"D:\Apps\Visual Studio");
        programa.UninstallString = @"D:\Apps\Visual Studio\unins.exe";

        Assert.Equal("Visual Studio", new HighlightRule { Scope = HighlightScope.Name }.SubjectOf(programa));
        Assert.Equal("Microsoft", new HighlightRule { Scope = HighlightScope.Publisher }.SubjectOf(programa));
        Assert.Equal(@"D:\Apps\Visual Studio", new HighlightRule { Scope = HighlightScope.InstallLocation }.SubjectOf(programa));
        Assert.Equal(@"D:\Apps\Visual Studio\unins.exe", new HighlightRule { Scope = HighlightScope.UninstallString }.SubjectOf(programa));
    }

    [Fact]
    public void ORealceNaoMudaOQuePassaNoFiltro()
    {
        var programa = Programa("Visual Studio 2022");
        var filtros = new ProgramFilterSet { TypeFilter = "64" };

        filtros.Highlights.Add(new HighlightRule { Pattern = "visual", ColorKey = HighlightPalette.Warning });

        // O realce pinta; ele nao filtra.
        Assert.True(filtros.Matches(programa));
        Assert.Equal(HighlightPalette.Warning, filtros.HighlightColorOf(programa));
    }

    // ------------------------------------------------------------------
    // Estado inicial
    // ------------------------------------------------------------------

    [Fact]
    public void UmConjuntoNovoNaoFiltraNada()
    {
        var filtros = new ProgramFilterSet();

        Assert.True(filtros.IsEmpty);
        Assert.True(filtros.Matches(Programa()));
    }

    [Fact]
    public void ClearVoltaAoEstadoInicial()
    {
        var filtros = new ProgramFilterSet
        {
            SearchText = "texto",
            TypeFilter = "32",
            SizeFilter = "small",
            DriveFilter = @"C:\",
            PublisherFilter = "editora",
            OnlyWithComments = true,
            OnlyWithGroups = true,
        };
        filtros.Highlights.Add(new HighlightRule { Pattern = "padrao" });

        Assert.False(filtros.IsEmpty);

        filtros.Clear();

        Assert.True(filtros.IsEmpty);
        Assert.Equal(string.Empty, filtros.SearchText);
        Assert.Equal("All", filtros.TypeFilter);
        Assert.Equal("all", filtros.SizeFilter);
        Assert.Null(filtros.DriveFilter);
        Assert.Equal(string.Empty, filtros.PublisherFilter);
        Assert.False(filtros.OnlyWithComments);
        Assert.False(filtros.OnlyWithGroups);
        Assert.Empty(filtros.Highlights);
    }

    // ------------------------------------------------------------------
    // A paleta
    // ------------------------------------------------------------------

    [Fact]
    public void APaletaTemAsQuatroChaves()
    {
        Assert.Equal(4, HighlightPalette.All.Count);
        Assert.Contains(HighlightPalette.Warning, HighlightPalette.All);
        Assert.Contains(HighlightPalette.Danger, HighlightPalette.All);
        Assert.Contains(HighlightPalette.Success, HighlightPalette.All);
        Assert.Contains(HighlightPalette.Accent, HighlightPalette.All);
    }

    [Fact]
    public void OHexDaCorTemAlfa()
    {
        foreach (var chave in HighlightPalette.All)
        {
            var hex = HighlightPalette.HexOf(chave);

            Assert.NotNull(hex);
            Assert.Equal(9, hex!.Length);
            Assert.StartsWith("#FF", hex, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void OHexDeChaveDesconhecidaENulo()
    {
        Assert.Null(HighlightPalette.HexOf("outra-cor"));
        Assert.Null(HighlightPalette.HexOf(string.Empty));
    }

    [Fact]
    public void ORotuloDaCorExisteNoCatalogoDeIdioma()
    {
        var catalogo = Loc.Load("pt-BR");
        Assert.NotEmpty(catalogo);

        foreach (var chave in HighlightPalette.All)
        {
            var rotulo = HighlightPalette.LabelKeyOf(chave);

            Assert.True(catalogo.ContainsKey(rotulo), $"chave ausente no catalogo: {rotulo}");
        }
    }

    // ------------------------------------------------------------------
    // Ajuda
    // ------------------------------------------------------------------

    /// <summary>Um programa de teste, com o que cada filtro olha.</summary>
    private static InstalledProgram Programa(
        string nome = "Programa",
        string editora = "Editora S.A.",
        string local = @"C:\Program Files\Programa",
        long tamanho = 0,
        string bits = "64-bits",
        bool msi = false)
    {
        return new InstalledProgram
        {
            DisplayName = nome,
            Publisher = editora,
            InstallLocation = local,
            UninstallString = @"C:\Program Files\Programa\unins000.exe",
            RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{"
                + Guid.NewGuid().ToString("N") + "}",
            SizeBytes = tamanho,
            Bits = bits,
            IsWindowsInstaller = msi,
        };
    }
}
