using System;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Reporting;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Confianca de pasta, avisos, filtro por regex e negacoes, marcacao de
/// atualizacao, pastas de instalacao do usuario, catalogo Oculus, script da
/// Store, paleta daltonica e limpador de rastros.
/// </summary>
public class ConfidenceAndFiltersTests
{
    // -----------------------------------------------------------------
    // Confianca de pasta (C6)
    // -----------------------------------------------------------------

    [Fact]
    public void PastaVaziaComOMesmoNomeDoProgramaTemConfiancaAlta()
    {
        var folder = NewFolder("Programa Tranal Confianca");

        var result = FolderConfidenceEvaluator.Evaluate(folder, "Programa Tranal");

        Assert.True(result.Usable);
        Assert.True(result.IsEmpty);
        Assert.Equal(FolderConfidenceLevel.High, result.Level);
    }

    [Fact]
    public void AvaliacaoExplicaOsNoveCriterios()
    {
        var folder = NewFolder("Programa Tranal Criterios");

        var result = FolderConfidenceEvaluator.Evaluate(folder, "Programa Tranal");

        Assert.Equal(9, result.Criteria.Count);
        Assert.Contains(result.Criteria, c => c.Id == FolderConfidenceEvaluator.EmptyFolderCriterion);
        Assert.Contains(result.Criteria, c => c.Id == FolderConfidenceEvaluator.ExecutablesCriterion);
        Assert.Contains(result.Criteria, c => c.Id == FolderConfidenceEvaluator.UserDataCriterion);
        Assert.Contains(result.Criteria, c => c.Id == FolderConfidenceEvaluator.KnownEditorCriterion);
        Assert.Contains(result.Criteria, c => c.Id == FolderConfidenceEvaluator.NameMatchCriterion);
        Assert.Contains(result.Criteria, c => c.Id == FolderConfidenceEvaluator.ProgramsFolderCriterion);
        Assert.Contains(result.Criteria, c => c.Id == FolderConfidenceEvaluator.UserFolderCriterion);
        Assert.Contains(result.Criteria, c => c.Id == FolderConfidenceEvaluator.ManyFilesCriterion);
        Assert.Contains(result.Criteria, c => c.Id == FolderConfidenceEvaluator.NoSubdirectoriesCriterion);
        Assert.False(string.IsNullOrWhiteSpace(result.Summary));
    }

    [Fact]
    public void PastaComExecutavelTemExecutaveisEConfiancaAlta()
    {
        var folder = NewFolder("Programa Tranal Executavel");
        File.WriteAllText(Path.Combine(folder, "app.exe"), "x");

        var result = FolderConfidenceEvaluator.Evaluate(folder, "Programa Tranal");

        Assert.True(result.HasExecutables);
        Assert.Equal(FolderConfidenceLevel.High, result.Level);
        Assert.False(result.MayContainUserData);
    }

    [Fact]
    public void PastaSoComDocumentosTemConfiancaBaixaEPodeConterDadoDoUsuario()
    {
        var folder = NewFolder("Documentos Tranal");
        File.WriteAllText(Path.Combine(folder, "meu documento.docx"), "x");
        File.WriteAllText(Path.Combine(folder, "foto.png"), "x");

        var result = FolderConfidenceEvaluator.Evaluate(folder, "Programa Tranal");

        Assert.Equal(FolderConfidenceLevel.Low, result.Level);
        Assert.True(result.MayContainUserData);
    }

    [Fact]
    public void PastaQueNaoExisteNaoPodeSerAvaliada()
    {
        var result = FolderConfidenceEvaluator.Evaluate(
            Path.Combine(Path.GetTempPath(), "tranal-pasta-que-nao-existe-" + Guid.NewGuid().ToString("N")),
            "Programa Tranal");

        Assert.False(result.Usable);
        Assert.Equal(FolderConfidenceLevel.Low, result.Level);
    }

    [Fact]
    public void PastaDeDocumentosDoUsuarioEDiferenteDePastaDePrograma()
    {
        Assert.True(FolderConfidenceEvaluator.IsUserContentFolder(@"C:\Users\Alguem\Documents\MeuApp"));
        Assert.False(FolderConfidenceEvaluator.IsUserContentFolder(@"C:\Users\Alguem\AppData\Local\MeuApp"));
    }

    // -----------------------------------------------------------------
    // Avisos (C8 e REVO #40)
    // -----------------------------------------------------------------

    [Fact]
    public void AvisoDeBaixaConfiancaAvisoDePastaComDadoDoUsuarioEAvisoDePerigo()
    {
        var sobras = new[]
        {
            new LeftoverItem
            {
                Kind = "Pasta",
                Path = @"C:\Users\Alguem\AppData\Local\MeuApp",
                Confidence = LeftoverConfidence.Low,
                Reason = LeftoverReason.ExactNameMatch,
                MayContainUserData = true,
            },
            new LeftoverItem
            {
                Kind = "Pasta",
                Path = @"C:\Program Files\MeuApp",
                Confidence = LeftoverConfidence.High,
                Reason = LeftoverReason.ExactNameMatch,
            },
        };

        var avisos = LeftoverWarnings.Build(sobras);

        // O aviso de perigo entra na lista: o primeiro item tem confianca
        // baixa e vem marcado, e e exatamente o caso que o aviso descreve.
        Assert.Equal(3, avisos.Count);

        var perigo = avisos.First(a => a.Kind == LeftoverWarningKind.Dangerous);
        Assert.Contains(@"C:\Users\Alguem\AppData\Local\MeuApp", perigo.Items);
        Assert.DoesNotContain(@"C:\Program Files\MeuApp", perigo.Items);

        var baixa = avisos.First(a => a.Kind == LeftoverWarningKind.LowConfidence);
        Assert.Equal("Leftovers.Warn.LowConfidence", baixa.MessageKey);
        Assert.Contains(@"C:\Users\Alguem\AppData\Local\MeuApp", baixa.Items);
        Assert.DoesNotContain(@"C:\Program Files\MeuApp", baixa.Items);

        var dados = avisos.First(a => a.Kind == LeftoverWarningKind.UserData);
        Assert.Equal("Leftovers.Warn.UserData", dados.MessageKey);
        Assert.Contains(@"C:\Users\Alguem\AppData\Local\MeuApp", dados.Items);
    }

    [Fact]
    public void ItemSemDadosDoUsuarioNaoGeraAvisoDeDadoDoUsuario()
    {
        var sobras = new[]
        {
            new LeftoverItem
            {
                Kind = "Pasta",
                Path = @"C:\Program Files\MeuApp",
                Confidence = LeftoverConfidence.High,
                Reason = LeftoverReason.ExactNameMatch,
            },
        };

        var avisos = LeftoverWarnings.Build(sobras);

        Assert.Empty(avisos);
        Assert.Empty(LeftoverWarnings.UserDataFolders(sobras));
        Assert.Empty(LeftoverWarnings.LowConfidenceItems(sobras));
    }

    [Fact]
    public void ItemBaixaConfiancaNaoEntraMarcado()
    {
        var item = new LeftoverItem
        {
            Kind = "Pasta",
            Path = @"C:\Users\Alguem\AppData\Local\MeuApp",
            Confidence = LeftoverConfidence.Low,
            Reason = LeftoverReason.ExactNameMatch,
        };

        Assert.False(item.IsHighConfidence);
    }

    // -----------------------------------------------------------------
    // Filtro por regex e negacoes (D2)
    // -----------------------------------------------------------------

    [Fact]
    public void FiltroPorExpressaoRegularFiltraPeloInicioDoNome()
    {
        var filtros = new ProgramFilterSet { SearchText = "regex:^Notepad" };

        Assert.True(filtros.Matches(new InstalledProgram { DisplayName = "Notepad++" }));
        Assert.False(filtros.Matches(new InstalledProgram { DisplayName = "Bloco de Notas" }));
        Assert.Equal("^Notepad", filtros.RegexExpression);
    }

    [Fact]
    public void ExpressaoRegularInvalidaNaoDerrubaALista()
    {
        var filtros = new ProgramFilterSet { SearchText = "regex:[invalida" };

        Assert.True(filtros.Matches(new InstalledProgram { DisplayName = "Qualquer Programa" }));
        Assert.True(filtros.HasInvalidRegex);
        Assert.False(filtros.Validate());
        Assert.NotEmpty(filtros.Diagnostics);
    }

    [Fact]
    public void TermoDeExclusaoForaProgramaDaLista()
    {
        var filtros = new ProgramFilterSet { SearchText = "jogo -demo" };

        Assert.True(filtros.Matches(new InstalledProgram { DisplayName = "Jogo" }));
        Assert.False(filtros.Matches(new InstalledProgram { DisplayName = "Jogo Demo" }));
        Assert.Contains("demo", filtros.ExclusionTerms);
    }

    [Fact]
    public void ExclusaoPelaListaDeTermosTambemFunciona()
    {
        var filtros = new ProgramFilterSet { SearchText = "jogo" };
        filtros.AddExclusion("-demo");

        Assert.True(filtros.Matches(new InstalledProgram { DisplayName = "Jogo" }));
        Assert.False(filtros.Matches(new InstalledProgram { DisplayName = "Jogo Demo" }));
    }

    [Fact]
    public void BuscaLiteralContinuaComoAntes()
    {
        var filtros = new ProgramFilterSet { SearchText = "Steam" };

        Assert.True(filtros.Matches(new InstalledProgram { DisplayName = "Steam Client" }));
        Assert.False(filtros.Matches(new InstalledProgram { DisplayName = "Notepad" }));
    }

    [Fact]
    public void LimparOFiltroApagaAsNegacoes()
    {
        var filtros = new ProgramFilterSet { SearchText = "jogo -demo" };
        Assert.False(filtros.IsEmpty);

        filtros.Clear();

        Assert.True(filtros.IsEmpty);
        Assert.Empty(filtros.ExcludedTerms);
    }

    // -----------------------------------------------------------------
    // Marcacao de atualizacao (A13)
    // -----------------------------------------------------------------

    [Fact]
    public void ProgramaDeAtualizacaoEDetectadoPeloNome()
    {
        var info = UpdateMarker.Detect(new InstalledProgram { DisplayName = "Atualização do Chrome 120" });

        Assert.True(info.IsUpdate);
        Assert.Equal("Chrome", info.BaseAppName);
        Assert.Equal(UpdateMarkerReason.NameSaysUpdate, info.Reason);
    }

    [Fact]
    public void ProgramaDeAtualizacaoEDetectadoPelaPasta()
    {
        var info = UpdateMarker.Detect(new InstalledProgram
        {
            DisplayName = "MeuApp",
            InstallLocation = @"C:\Program Files\MeuApp\Updates",
        });

        Assert.True(info.IsUpdate);
        Assert.Equal(UpdateMarkerReason.FolderSaysUpdate, info.Reason);
    }

    [Fact]
    public void ProgramaComumNaoEMarcadoComoAtualizacao()
    {
        var info = UpdateMarker.Detect(new InstalledProgram { DisplayName = "Notepad++" });

        Assert.False(info.IsUpdate);
        Assert.Equal(UpdateMarkerReason.None, info.Reason);
    }

    // -----------------------------------------------------------------
    // Pastas de instalacao do usuario (A15)
    // -----------------------------------------------------------------

    [Fact]
    public void PastasDeInstalacaoDoUsuarioSaoReconhecidas()
    {
        var raiz = UserInstallFolders.Roots
            .FirstOrDefault(r => r.EndsWith("Programs", StringComparison.OrdinalIgnoreCase));

        Assert.False(string.IsNullOrEmpty(raiz));
        Assert.True(UserInstallFolders.IsUserInstallPath(Path.Combine(raiz!, "MeuApp")));
        Assert.False(UserInstallFolders.IsUserInstallPath(@"C:\Program Files\MeuApp"));
    }

    // -----------------------------------------------------------------
    // Catalogo Oculus (A8)
    // -----------------------------------------------------------------

    [Fact]
    public void AplicativosDoOculusEReconhecidosPeloNome()
    {
        Assert.True(OculusCatalog.IsOculusApp("Oculus Client"));
        Assert.True(OculusCatalog.IsOculusApp("Meta Quest Link"));
        Assert.True(OculusCatalog.IsOculusApp("com.oculus.quest"));
        Assert.False(OculusCatalog.IsOculusApp("Bloco de Notas"));
        Assert.NotNull(OculusCatalog.Match("Oculus Remote Desktop"));
    }

    // -----------------------------------------------------------------
    // Script da Store (D12)
    // -----------------------------------------------------------------

    [Fact]
    public void ScriptDaStoreUsaGetAppxPackageERemoveAppxPackage()
    {
        var script = StoreAppScriptWriter.Build(new[]
        {
            new WindowsApp
            {
                Name = "Microsoft.XboxApp",
                PackageFullName = "Microsoft.XboxApp_1.0.0.0_x64__8wekyb3d8bbwe",
            },
        });

        Assert.Contains("Get-AppxPackage", script);
        Assert.Contains("Remove-AppxPackage", script);
        Assert.Contains("Microsoft.XboxApp_1.0.0.0_x64__8wekyb3d8bbwe", script);
    }

    [Fact]
    public void ScriptDaStoreEhGravadoComBomUtf8()
    {
        var destino = Path.Combine(Path.GetTempPath(),
            "tranal-store-" + Guid.NewGuid().ToString("N") + ".ps1");

        var gravado = StoreAppScriptWriter.Write(destino, new[]
        {
            new WindowsApp { Name = "Microsoft.XboxApp", PackageFullName = "Microsoft.XboxApp_1.0" },
        });

        Assert.False(string.IsNullOrEmpty(gravado));
        Assert.True(File.Exists(destino));

        var bytes = File.ReadAllBytes(destino);
        Assert.True(bytes.Length > 3);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }

    // -----------------------------------------------------------------
    // Paleta daltonica (D20)
    // -----------------------------------------------------------------

    [Fact]
    public void ModoDaltônicoTrocaORedEVergreen()
    {
        var perigo = ColorBlindPalette.HighlightHexOf(HighlightPalette.Danger, ColorVisionMode.Deuteranopia);
        var sucesso = ColorBlindPalette.HighlightHexOf(HighlightPalette.Success, ColorVisionMode.Deuteranopia);

        Assert.NotEqual(ColorBlindPalette.StandardDanger, perigo);
        Assert.NotEqual(ColorBlindPalette.StandardSuccess, sucesso);
        Assert.NotEqual(perigo, sucesso);
        Assert.True(ColorBlindPalette.DistinguishesByLuminance(perigo, sucesso));
    }

    [Fact]
    public void PaletaDaltonicaTemAcentoEAlcanceDeBrilho()
    {
        var palette = ColorBlindPalette.PaletteOf(ColorVisionMode.Protanopia);

        Assert.True(palette.Colors.ContainsKey(ThemeColorRole.Accent));
        Assert.True(palette.Colors.ContainsKey(ThemeColorRole.Selection));
        Assert.NotEqual(ColorBlindPalette.StandardAccent, palette.Colors[ThemeColorRole.Accent]);
    }

    // -----------------------------------------------------------------
    // Limpador de rastros (C14)
    // -----------------------------------------------------------------

    [Fact]
    public void LimpadorDeRastrosTemOsSeisGrupos()
    {
        Assert.Equal(6, PrivacyTraceCleaner.Groups.Count);
        Assert.Contains(PrivacyTraceCleaner.Groups, g => g.Group == PrivacyTraceGroup.Firewall);
        Assert.Contains(PrivacyTraceCleaner.Groups, g => g.Group == PrivacyTraceGroup.UserAssist);
        Assert.Contains(PrivacyTraceCleaner.Groups, g => g.Group == PrivacyTraceGroup.ErrorReports);
        Assert.Contains(PrivacyTraceCleaner.Groups, g => g.Group == PrivacyTraceGroup.Clsid);
        Assert.Contains(PrivacyTraceCleaner.Groups, g => g.Group == PrivacyTraceGroup.Audio);
        Assert.Contains(PrivacyTraceCleaner.Groups, g => g.Group == PrivacyTraceGroup.EventLogs);
    }

    [Fact]
    public void ListarUmGrupoNaoQuebra()
    {
        var itens = PrivacyTraceCleaner.Scan(PrivacyTraceGroup.Firewall);

        Assert.NotNull(itens);
        Assert.All(itens, i => Assert.Equal(PrivacyTraceGroup.Firewall, i.Group));
    }

    [Fact]
    public void LimparSemNadaNaoFazNada()
    {
        var relatorio = PrivacyTraceCleaner.Clean(Array.Empty<PrivacyTraceItem>());

        Assert.Empty(relatorio.Outcomes);
    }

    /// <summary>Cria uma pasta temporaria para o teste.</summary>
    private static string NewFolder(string name)
    {
        var folder = Path.Combine(
            Path.GetTempPath(),
            "tranal-teste-" + Guid.NewGuid().ToString("N"),
            name);

        Directory.CreateDirectory(folder);
        return folder;
    }
}
