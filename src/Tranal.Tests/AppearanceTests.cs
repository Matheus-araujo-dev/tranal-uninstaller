using System.IO;
using System.Text.RegularExpressions;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// As colunas novas da lista de programas.
///
/// As garantias que importam: o valor e honesto (vazio quando nao ha dado),
/// o nome de exportacao e fixo e o valor da coluna e o mesmo na tela e no
/// arquivo exportado.
/// </summary>
public class ColunasNovasTests : IDisposable
{
    public ColunasNovasTests()
    {
        // A lista de exclusoes e lida do disco do usuario quando ninguem
        // declara outra: aqui ela comeca vazia, para o teste nao depender do
        // estado da maquina de quem roda a suite.
        ExcludeList.SetCurrent(new ExcludeList());
        StartupEntryIndex.Clear();
    }

    public void Dispose()
    {
        ExcludeList.ResetCurrent();
        StartupEntryIndex.Clear();
    }

    private static InstalledProgram Programa(string nome = "Programa de Teste") => new()
    {
        DisplayName = nome,
        RegistryKey = "{chave-de-teste}",
        RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{chave-de-teste}",
    };

    [Fact]
    public void ColunasNovasTemNomeDeExportacaoFixo()
    {
        var esperados = new Dictionary<ProgramColumn, string>
        {
            [ProgramColumn.UninstallerLocation] = "UninstallerLocation",
            [ProgramColumn.Source] = "Source",
            [ProgramColumn.SystemComponent] = "SystemComponent",
            [ProgramColumn.Protected] = "Protected",
            [ProgramColumn.Startup] = "Startup",
            [ProgramColumn.Rating] = "Rating",
        };

        foreach (var (coluna, nome) in esperados)
            Assert.Equal(nome, ProgramColumns.Info(coluna).ExportName);

        // Nenhum nome de exportacao pode se repetir: o arquivo exportado
        // perderia a coluna.
        var nomes = ProgramColumns.All.Select(c => c.ExportName).ToList();
        Assert.Equal(nomes.Count, nomes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void ColunaDeLocalizacaoDoDesinstaladorMostraOExecutavel()
    {
        var programa = Programa();
        programa.UninstallString = "\"C:\\Program Files\\App\\unins000.exe\" /VERYSILENT";

        Assert.Equal(
            @"C:\Program Files\App\unins000.exe",
            ProgramColumns.Value(ProgramColumn.UninstallerLocation, programa, null));
    }

    [Fact]
    public void ColunaDeLocalizacaoDoDesinstaladorFicaVaziaSemComando()
    {
        // Sem comando de desinstalacao nao ha caminho: a coluna fica vazia,
        // em vez de mostrar um caminho inventado.
        Assert.Equal(
            string.Empty,
            ProgramColumns.Value(ProgramColumn.UninstallerLocation, Programa(), null));
    }

    [Fact]
    public void ColunaDeFonteDistingueAsOrigensDaEntrada()
    {
        var msi = Programa();
        msi.IsWindowsInstaller = true;
        Assert.Equal("WindowsInstaller", ProgramColumns.Value(ProgramColumn.Source, msi, null));

        var store = Programa();
        store.Kind = UninstallerKind.StoreApp;
        Assert.Equal("StoreApp", ProgramColumns.Value(ProgramColumn.Source, store, null));

        var chave = Programa();
        Assert.Equal("UninstallKey", ProgramColumns.Value(ProgramColumn.Source, chave, null));

        // Sem chave de registro nao se sabe de onde a entrada veio.
        var semRegistro = new InstalledProgram { DisplayName = "Sem Registro" };
        Assert.Equal("Unknown", ProgramColumns.Value(ProgramColumn.Source, semRegistro, null));
    }

    [Fact]
    public void ColunaDeComponenteDoSistemaRespondeAoRegistro()
    {
        var programa = Programa();

        programa.IsSystemComponent = true;
        Assert.Equal(Loc.Get("Common.Yes"), ProgramColumns.Value(ProgramColumn.SystemComponent, programa, null));

        programa.IsSystemComponent = false;
        Assert.Equal(Loc.Get("Common.No"), ProgramColumns.Value(ProgramColumn.SystemComponent, programa, null));
    }

    [Fact]
    public void ColunaProtegidoRespeitaAListaDeExclusoes()
    {
        ExcludeList.SetCurrent(new ExcludeList { Paths = { @"C:\Program Files\App" } });

        var protegido = Programa();
        protegido.InstallLocation = @"C:\Program Files\App";
        Assert.Equal(Loc.Get("Common.Yes"), ProgramColumns.Value(ProgramColumn.Protected, protegido, null));

        var livre = Programa();
        livre.InstallLocation = @"C:\Program Files\Outro";
        Assert.Equal(Loc.Get("Common.No"), ProgramColumns.Value(ProgramColumn.Protected, livre, null));
    }

    [Fact]
    public void ColunaDeInicializacaoFicaVaziaSemVarredura()
    {
        // Sem varredura nao ha resposta — e resposta inventada seria pior que
        // coluna vazia.
        var programa = Programa();
        programa.InstallLocation = @"C:\Program Files\App";

        Assert.Equal(string.Empty, ProgramColumns.Value(ProgramColumn.Startup, programa, null));
    }

    [Fact]
    public void ColunaDeInicializacaoRespondeDepoisDaVarredura()
    {
        var programa = Programa();
        programa.InstallLocation = @"C:\Program Files\App";

        StartupEntryIndex.Refresh(new[]
        {
            new AutorunItem
            {
                Kind = AutorunItemKind.RunKey,
                Name = "App",
                Command = "\"C:\\Program Files\\App\\app.exe\" --run",
            },
        });

        Assert.Equal(Loc.Get("Common.Yes"), ProgramColumns.Value(ProgramColumn.Startup, programa, null));

        var outro = Programa();
        outro.InstallLocation = @"C:\Program Files\Outro";
        Assert.Equal(Loc.Get("Common.No"), ProgramColumns.Value(ProgramColumn.Startup, outro, null));
    }

    [Fact]
    public void ColunaDaEstrelaMostraAEstrelaDoUsuario()
    {
        var programa = Programa();
        var custom = new ProgramCustomization { Key = "x", Rating = 3 };

        Assert.Equal("★★★☆☆", ProgramColumns.Value(ProgramColumn.Rating, programa, custom));

        // Sem preferencia do usuario nao ha estrela: a coluna fica vazia.
        Assert.Equal(string.Empty, ProgramColumns.Value(ProgramColumn.Rating, programa, null));
    }

    [Fact]
    public void ColunasNovasTemRotuloDeReservaEChavePropriaNoCatalogo()
    {
        foreach (var coluna in new[]
        {
            ProgramColumn.UninstallerLocation,
            ProgramColumn.Source,
            ProgramColumn.SystemComponent,
            ProgramColumn.Protected,
            ProgramColumn.Startup,
            ProgramColumn.Rating,
        })
        {
            var info = ProgramColumns.Info(coluna);

            // O catalogo de idiomas e a fonte; a chave propria das colunas
            // novas ainda esta pendente la fora (fora do escopo do lote), e
            // por isso o rotulo de reserva e o que aparece na tela.
            Assert.False(string.IsNullOrWhiteSpace(info.FallbackLabel));
            Assert.StartsWith("Common.", info.PendingLabelKey!, StringComparison.Ordinal);

            // A coluna nunca aparece com o nome da chave: nem a propria nem a
            // emprestada.
            var rotulo = ProgramColumns.Label(coluna);
            Assert.False(string.IsNullOrWhiteSpace(rotulo));
            Assert.NotEqual(info.PendingLabelKey, rotulo);
            Assert.NotEqual(info.LabelKey, rotulo);
        }
    }
}

/// <summary>
/// O mapa de area dos programas.
///
/// O que importa no treemap nao e aaparencia: e a area do retangulo
/// guardar a proporcao do tamanho, os retangulos nao se invadirem e a soma
/// das areas bater com o total.
/// </summary>
public class TreemapTests
{
    private static TreemapEntry[] Entradas() => new[]
    {
        new TreemapEntry("a", "Programa A", 600),
        new TreemapEntry("b", "Programa B", 300),
        new TreemapEntry("c", "Programa C", 100),
    };

    [Fact]
    public void AreaDoRetanguloEProporcionalAoTamanho()
    {
        var entradas = Entradas();
        var modelo = TreemapModel.Build(entradas, 400, 300);

        const double area = 400 * 300;
        const double total = 1000;

        Assert.Equal(3, modelo.Rectangles.Count);

        foreach (var rect in modelo.Rectangles)
        {
            var entrada = entradas.First(e => e.Key == rect.Key);
            var esperado = entrada.Size / total * area;

            Assert.True(
                Math.Abs(rect.Area - esperado) < 0.01,
                $"{rect.Key}: area {rect.Area} em vez de {esperado}");
        }
    }

    [Fact]
    public void RetangulosNaoSeSobrepoem()
    {
        var modelo = TreemapModel.Build(Entradas(), 400, 300);

        foreach (var a in modelo.Rectangles)
        {
            foreach (var b in modelo.Rectangles)
            {
                if (a == b) continue;

                const double margem = 0.01;
                var sobrepoe =
                    a.X < b.X + b.Width - margem && b.X < a.X + a.Width - margem &&
                    a.Y < b.Y + b.Height - margem && b.Y < a.Y + a.Height - margem;

                Assert.False(sobrepoe, $"{a.Key} invade {b.Key}");
            }
        }
    }

    [Fact]
    public void SomaDasAreasBateComOTotal()
    {
        var modelo = TreemapModel.Build(Entradas(), 400, 300);

        Assert.Equal(1000, modelo.TotalSize);
        Assert.True(
            Math.Abs(modelo.TotalArea - 400 * 300) < 0.01,
            "soma das areas: " + modelo.TotalArea);
    }

    [Fact]
    public void TodosOsRetangulosFicamDentroDoMapa()
    {
        var modelo = TreemapModel.Build(Entradas(), 400, 300);

        foreach (var rect in modelo.Rectangles)
        {
            Assert.True(rect.X >= -0.01, $"{rect.Key} comecou fora");
            Assert.True(rect.Y >= -0.01, $"{rect.Key} comecou fora");
            Assert.True(rect.X + rect.Width <= 400.01, $"{rect.Key} passou da borda");
            Assert.True(rect.Y + rect.Height <= 300.01, $"{rect.Key} passou da borda");
        }
    }

    [Fact]
    public void MapaComListaVaziaNaoQuebra()
    {
        var modelo = TreemapModel.Build(Array.Empty<TreemapEntry>(), 400, 300);

        Assert.Empty(modelo.Rectangles);
        Assert.Equal(0, modelo.TotalSize);
        Assert.Equal(0, modelo.TotalArea);
    }

    [Fact]
    public void MapaNuloNaoQuebra()
    {
        var modelo = TreemapModel.Build(null, 400, 300);

        Assert.Empty(modelo.Rectangles);
        Assert.Equal(0, modelo.TotalSize);
    }

    [Fact]
    public void ProgramaSemTamanhoNaoEntraNoMapa()
    {
        // Sem medida nao ha area proporcional: o programa fica de fora, em vez
        // de receber um retangulo de area zero.
        var entradas = new[]
        {
            new TreemapEntry("a", "Com tamanho", 100),
            new TreemapEntry("b", "Sem tamanho", 0),
        };

        var modelo = TreemapModel.Build(entradas, 400, 300);

        Assert.Single(modelo.Rectangles);
        Assert.Equal("a", modelo.Rectangles[0].Key);
        Assert.Equal(100, modelo.TotalSize);
    }

    [Fact]
    public void MapaEDeterministico()
    {
        var primeiro = TreemapModel.Build(Entradas(), 400, 300);
        var segundo = TreemapModel.Build(Entradas(), 400, 300);

        Assert.Equal(primeiro.Rectangles.Count, segundo.Rectangles.Count);

        for (var i = 0; i < primeiro.Rectangles.Count; i++)
        {
            Assert.Equal(primeiro.Rectangles[i].Key, segundo.Rectangles[i].Key);
            Assert.Equal(primeiro.Rectangles[i].X, segundo.Rectangles[i].X, 6);
            Assert.Equal(primeiro.Rectangles[i].Y, segundo.Rectangles[i].Y, 6);
            Assert.Equal(primeiro.Rectangles[i].Width, segundo.Rectangles[i].Width, 6);
            Assert.Equal(primeiro.Rectangles[i].Height, segundo.Rectangles[i].Height, 6);
        }
    }

    [Fact]
    public void MapaFeitoDaListaDeProgramasUsaOTamanhoDeCadaUm()
    {
        var programas = new[]
        {
            new InstalledProgram { DisplayName = "Grande", SizeBytes = 1000, RegistryKey = "g", RegistryPath = @"HKEY_LOCAL_MACHINE\Grande" },
            new InstalledProgram { DisplayName = "Pequeno", SizeBytes = 100, RegistryKey = "p", RegistryPath = @"HKEY_LOCAL_MACHINE\Pequeno" },
        };

        var modelo = TreemapModel.FromPrograms(programas, 200, 200);

        Assert.Equal(2, modelo.Rectangles.Count);
        Assert.Equal(1100, modelo.TotalSize);

        var grande = modelo.Rectangles.First(r => r.Key == @"HKEY_LOCAL_MACHINE\Grande");
        var pequeno = modelo.Rectangles.First(r => r.Key == @"HKEY_LOCAL_MACHINE\Pequeno");

        Assert.True(grande.Area > pequeno.Area * 9.9, "a area nao guardou a proporcao");
    }
}

/// <summary>
/// A legenda de cores.
///
/// A legenda e a unica fonte do que cada cor significa: ela deriva das
/// enums do nucleo e usa o mesmo rotulo que a lista mostra.
/// </summary>
public class ColorLegendTests
{
    [Fact]
    public void LegendaTemUmaEntradaPorNivelDeConfianca()
    {
        var niveis = Enum.GetValues<LeftoverConfidence>();

        Assert.Equal(
            niveis.Length,
            ColorLegend.Entries.Count(e => e.Subject == ColorLegendSubject.Confidence));

        foreach (var nivel in niveis)
        {
            var entrada = ColorLegend.ForConfidence(nivel);

            // O rotulo e o mesmo metodo que a lista usa: as duas nao podem
            // discordar do texto.
            Assert.Equal(LeftoverLabels.Confidence(nivel), entrada.Label);
            Assert.NotEmpty(entrada.Meaning);
            Assert.NotEmpty(entrada.Color);
            Assert.Matches("^#[0-9A-Fa-f]{6}$", entrada.Color);
        }
    }

    [Fact]
    public void LegendaTemUmaEntradaPorTipoDeDesinstalador()
    {
        var tipos = Enum.GetValues<UninstallerKind>();

        Assert.Equal(
            tipos.Length,
            ColorLegend.Entries.Count(e => e.Subject == ColorLegendSubject.UninstallerKind));

        foreach (var tipo in tipos)
        {
            var entrada = ColorLegend.ForKind(tipo);

            // A coluna "Tipo de desinstalador" mostra o identificador fixo do
            // tipo: a legenda mostra o mesmo texto.
            Assert.Equal(tipo.ToString(), entrada.Label);
            Assert.NotEmpty(entrada.Meaning);
            Assert.Matches("^#[0-9A-Fa-f]{6}$", entrada.Color);
        }
    }

    [Fact]
    public void CadaTipoTemUmaCorPropria()
    {
        var cores = Enum.GetValues<UninstallerKind>()
            .Select(k => ColorLegend.ForKind(k).Color)
            .ToList();

        Assert.Equal(cores.Count, cores.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void NaoExisteSegundaListaDeSignificadosDaLegenda()
    {
        var raiz = RepoFiles.FindDirectory("src");
        Assert.NotNull(raiz);

        var corHex = new Regex("#[0-9A-Fa-f]{6}", RegexOptions.CultureInvariant);
        var membroDaEnum = new Regex(@"\b(UninstallerKind|LeftoverConfidence)\.[A-Za-z]+", RegexOptions.CultureInvariant);
        var chaveDeLegenda = new Regex("\"Legend\\.[A-Za-z]+", RegexOptions.CultureInvariant);
        var infratores = new List<string>();

        foreach (var arquivo in Directory.EnumerateFiles(raiz!, "*.cs", SearchOption.AllDirectories))
        {
            // obj/ e bin/ nao fazem parte do codigo.
            if (arquivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                || arquivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;

            var relativo = Path.GetRelativePath(raiz!, arquivo).Replace('\\', '/');

            // A legenda e a fonte: e ela que pode ter a lista.
            if (relativo.EndsWith("Core/Appearance/ColorLegend.cs", StringComparison.OrdinalIgnoreCase)) continue;

            var texto = File.ReadAllText(arquivo);

            // Um arquivo que junta a enum com uma cor (ou que define uma
            // chave de legenda) e uma segunda lista de meanings: as duas
            // divergiriam na primeira cor trocada.
            var mapeiaEnumParaCor = corHex.IsMatch(texto) && membroDaEnum.IsMatch(texto);
            var defineChaveDeLegenda = chaveDeLegenda.IsMatch(texto);

            if (mapeiaEnumParaCor || defineChaveDeLegenda)
                infratores.Add(relativo);
        }

        Assert.True(
            infratores.Count == 0,
            "Existe uma segunda lista de meanings da legenda em: " + string.Join(", ", infratores));
    }
}

/// <summary>
/// O agrupamento automatico por coluna.
/// </summary>
public class GroupingModelTests
{
    private static InstalledProgram Programa(string nome, string empresa, long tamanho) => new()
    {
        DisplayName = nome,
        Publisher = empresa,
        SizeBytes = tamanho,
        RegistryKey = nome,
    };

    [Fact]
    public void AgrupamentoPorColunaDevolveOsGruposCertos()
    {
        var programas = new[]
        {
            Programa("A", "Fabrica X", 100),
            Programa("B", "Fabrica X", 200),
            Programa("C", "Outra Fabrica", 50),
        };

        var grupos = GroupingModel.Group(programas, ProgramColumn.Company, _ => null);

        Assert.Equal(2, grupos.Count);

        Assert.Equal("Fabrica X", grupos[0].Name);
        Assert.Equal(2, grupos[0].Count);
        Assert.Equal(300, grupos[0].TotalSize);

        Assert.Equal("Outra Fabrica", grupos[1].Name);
        Assert.Equal(1, grupos[1].Count);
        Assert.Equal(50, grupos[1].TotalSize);
    }

    [Fact]
    public void GruposOrdenadosPorTamanhoTotalDecrescente()
    {
        var programas = new[]
        {
            Programa("A", "Pequena", 10),
            Programa("B", "Grande", 300),
            Programa("C", "Media", 100),
        };

        var grupos = GroupingModel.Group(programas, ProgramColumn.Company, _ => null);

        Assert.Equal(new[] { "Grande", "Media", "Pequena" }, grupos.Select(g => g.Name).ToArray());
        Assert.Equal(new long[] { 300, 100, 10 }, grupos.Select(g => g.TotalSize).ToArray());
    }

    [Fact]
    public void OrdemOriginalDentroDoGrupoPreservada()
    {
        var programas = new[]
        {
            Programa("C", "Mesma", 10),
            Programa("A", "Mesma", 20),
            Programa("B", "Mesma", 30),
        };

        var grupos = GroupingModel.Group(programas, ProgramColumn.Company, _ => null);

        Assert.Single(grupos);
        Assert.Equal(new[] { "C", "A", "B" }, grupos[0].Programs.Select(p => p.DisplayName).ToArray());
    }

    [Fact]
    public void GrupoSemValorTemNomeDeReservaEContaOsProgramas()
    {
        var programas = new[]
        {
            new InstalledProgram { DisplayName = "Sem Empresa", RegistryKey = "a" },
        };

        var grupos = GroupingModel.Group(programas, ProgramColumn.Company, _ => null);

        Assert.Single(grupos);
        Assert.NotEqual(string.Empty, grupos[0].Name);
        Assert.Equal(1, grupos[0].Count);
    }

    [Fact]
    public void AgrupamentoVazioNaoQuebra()
    {
        Assert.Empty(GroupingModel.Group(null, ProgramColumn.Company, _ => null));
        Assert.Empty(GroupingModel.Group(Array.Empty<InstalledProgram>(), ProgramColumn.Company, _ => null));
    }
}

/// <summary>
/// A classificacao do usuario (estrelas).
///
/// Ela mora nas preferencias por programa: a gravacao e atomica (temporario
/// e troca), a leitura e pelo mesmo caminho das outras anotacoes e um
/// arquivo corrompido e preservado, nunca sobrescrito.
/// </summary>
[Collection("preferencias")]
public class ProgramRatingTests : IDisposable
{
    private readonly string _file;
    private readonly InstalledProgram _programa;

    public ProgramRatingTests()
    {
        _file = Path.Combine(Path.GetTempPath(),
            "tranal-estrelas-" + Guid.NewGuid().ToString("N")[..8] + ".json");

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

        try
        {
            if (File.Exists(_file)) File.Delete(_file);
            if (File.Exists(_file + ".tmp")) File.Delete(_file + ".tmp");
            if (File.Exists(_file + ".bak")) File.Delete(_file + ".bak");
            if (File.Exists(_file + ".corrompido")) File.Delete(_file + ".corrompido");
        }
        catch { /* o temporario e descartavel */ }
    }

    [Fact]
    public void EstrelaGravadaERelida()
    {
        Assert.True(ProgramCustomizationStore.SetRating(_programa, 4));

        ProgramCustomizationStore.Reload();

        Assert.Equal(4, ProgramCustomizationStore.RatingOf(_programa));
        Assert.Equal(4, ProgramCustomizationStore.For(_programa).Rating);
    }

    [Fact]
    public void EstrelaSobrescreveOValorAnterior()
    {
        ProgramCustomizationStore.SetRating(_programa, 2);
        ProgramCustomizationStore.SetRating(_programa, 5);

        ProgramCustomizationStore.Reload();

        Assert.Equal(5, ProgramCustomizationStore.RatingOf(_programa));
    }

    [Fact]
    public void ClassificacaoSoZinhaNaoDesaparece()
    {
        // Um programa so com estrela e um programa anotado: a anotacao nao
        // pode ser apagada por falta de comentario.
        ProgramCustomizationStore.SetRating(_programa, 3);

        ProgramCustomizationStore.Reload();

        Assert.True(ProgramCustomizationStore.Has(_programa));
    }

    [Fact]
    public void GravacaoDaEstrelaEAtomica()
    {
        ProgramCustomizationStore.SetRating(_programa, 3);
        var antes = File.ReadAllText(_file);

        ProgramCustomizationStore.SetRating(_programa, 4);

        // A versao anterior fica no ".bak" e o temporario nao sobra.
        Assert.True(File.Exists(_file + ".bak"));
        Assert.Equal(antes, File.ReadAllText(_file + ".bak"));
        Assert.False(File.Exists(_file + ".tmp"));

        Assert.Contains("\"Rating\": 4", File.ReadAllText(_file));
    }

    [Fact]
    public void ArquivoCorrompidoComEstrelaEPreservado()
    {
        File.WriteAllText(_file, "{ isto nao e json }");

        ProgramCustomizationStore.Reload();

        Assert.Equal(LoadState.Preserved, ProgramCustomizationStore.LastLoad);
        Assert.False(File.Exists(_file));
        Assert.True(File.Exists(_file + ".corrompido"));
    }

    [Fact]
    public void LeituraNaoRenomeiaOArquivoCorrompidoDoUsuario()
    {
        File.WriteAllText(_file, "{ isto nao e json }");

        var anotacoes = ProgramCustomizationStore.PeekAll();

        Assert.Empty(anotacoes);

        // A leitura nao mexe no arquivo do usuario.
        Assert.True(File.Exists(_file));
        Assert.False(File.Exists(_file + ".corrompido"));
    }

    [Fact]
    public void EstrelaApareceNaColunaDaLista()
    {
        ProgramCustomizationStore.SetRating(_programa, 5);

        var custom = ProgramCustomizationStore.For(_programa);

        Assert.Equal("★★★★★", ProgramColumns.Value(ProgramColumn.Rating, _programa, custom));
    }

    [Fact]
    public void ExportacaoLeAEstrelaDoDisco()
    {
        ProgramCustomizationStore.SetRating(_programa, 2);

        var csv = Exporters.ToCsv(new[] { _programa }, new[] { ProgramColumn.Rating });

        Assert.Contains("★★☆☆☆", csv);
    }

    [Fact]
    public void ValorForaDaEscalaECortado()
    {
        ProgramCustomizationStore.SetRating(_programa, 99);

        ProgramCustomizationStore.Reload();

        Assert.Equal(ProgramRating.Max, ProgramCustomizationStore.RatingOf(_programa));
    }
}
