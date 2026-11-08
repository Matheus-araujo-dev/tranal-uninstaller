using System.IO;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O que a tela das ferramentas novas consome do nucleo: a limpeza dos
/// rastros de privacidade, a varredura dos fluxos alternativos, o registro
/// das ferramentas personalizadas, o modo de visao de cores e o filtro de
/// uso raro.
///
/// O nucleo ja era testado; o que faltava era a casca da tela. Estes testes
/// cobrem as duas pontas que a tela criou: o campo novo nas preferencias e
/// a opcao nova no filtro da lista.
/// </summary>
public class PrivacyToolsAndOptionsTests
{
    // ------------------------------------------------------------------
    // Modo de visao de cores
    // ------------------------------------------------------------------

    /// <summary>
    /// O modo comeca no padrao: a paleta so muda quando o usuario pede.
    /// </summary>
    [Fact]
    public void OModoDeVisaoDeCoresComecaNoPadrao()
    {
        var settings = new AppSettings();

        Assert.Equal("Standard", settings.ColorVisionMode);
    }

    /// <summary>
    /// O arquivo de preferencias em disco declara o campo novo: um arquivo
    /// gravado por outra versao precisa ser legivel por esta.
    /// </summary>
    [Fact]
    public void OFormatoDoArquivoDePreferenciasMencionaOModoDeVisaoDeCores()
    {
        Assert.Contains("colorVisionMode", SettingsFileFormat.Describe());
    }

    // ------------------------------------------------------------------
    // Filtro de uso raro
    // ------------------------------------------------------------------

    /// <summary>
    /// A opcao de uso raro faz parte do conjunto de filtros e some quando o
    /// filtro e limpo.
    /// </summary>
    [Fact]
    public void AFiltroDeUsoRaroFazParteDoConjuntoDeFiltros()
    {
        var filtros = new ProgramFilterSet();

        Assert.False(filtros.OnlyRarelyUsed);
        Assert.True(filtros.IsEmpty);

        filtros.OnlyRarelyUsed = true;
        Assert.False(filtros.IsEmpty);

        filtros.Clear();
        Assert.False(filtros.OnlyRarelyUsed);
        Assert.True(filtros.IsEmpty);
    }

    /// <summary>
    /// Sem sinal de uso do Windows nao ha prova de uso raro: o programa fica
    /// de fora do filtro. E o mesmo veredito que o nucleo da.
    /// </summary>
    [Fact]
    public void AFiltroDeUsoRaroEscondeOProgramaSemSinalDeUso()
    {
        var programa = new InstalledProgram
        {
            DisplayName = "Programa sem sinal de uso",
            InstallLocation = @"C:\Program Files\Inexistente\programa.exe",
        };

        var filtros = new ProgramFilterSet();
        Assert.True(filtros.Matches(programa));

        filtros.OnlyRarelyUsed = true;
        Assert.False(filtros.Matches(programa));
    }

    /// <summary>
    /// A regra do que conta como "raro" mora no nucleo: o filtro consome o
    /// veredito do <see cref="UsageSignal"/> e nao repete a janela de tempo.
    /// </summary>
    [Fact]
    public void AFiltroDeUsoRaroUsaAVerdicDoNucleo()
    {
        // Um executavel que nunca rodou conta como uso raro.
        var semData = new UsageSignal { ExecutablePath = @"C:\nada.exe" };
        Assert.True(semData.IsRarelyUsed(UsageSignals.DefaultRarelyUsedWindow));

        // E a janela de tempo do nucleo e a que vale: um executavel usado
        // ontem nao e uso raro.
        var antigo = new UsageSignal
        {
            ExecutablePath = @"C:\antigo.exe",
            LastUsed = DateTime.Now.AddDays(-200),
        };
        Assert.True(antigo.IsRarelyUsed(UsageSignals.DefaultRarelyUsedWindow));

        var recente = new UsageSignal
        {
            ExecutablePath = @"C:\recente.exe",
            LastUsed = DateTime.Now.AddDays(-1),
        };
        Assert.False(recente.IsRarelyUsed(UsageSignals.DefaultRarelyUsedWindow));
    }

    // ------------------------------------------------------------------
    // Rastros de privacidade
    // ------------------------------------------------------------------

    /// <summary>
    /// Cada grupo de rastro tem o proprio rotulo de idioma, e o rotulo vem
    /// do nucleo (a tela so o consome).
    /// </summary>
    [Fact]
    public void CadaGrupoDeRastroTemORotuloProprio()
    {
        var grupos = PrivacyTraceCleaner.Groups;

        Assert.Equal(6, grupos.Count);
        Assert.All(grupos, g => Assert.StartsWith("Privacy.", g.LabelKey));
        Assert.Equal(grupos.Count, grupos.Select(g => g.LabelKey).Distinct().Count());
    }

    // ------------------------------------------------------------------
    // Fluxos alternativos
    // ------------------------------------------------------------------

    /// <summary>
    /// A varredura de uma pasta sem arquivos nao devolve o fluxo principal:
    /// o que interessa e o fluxo escondido.
    /// </summary>
    [Fact]
    public void AVarreduraDeFluxosAlternativosDeUmaPastaVaziaNaoDevolveNada()
    {
        using var temp = new TempFolder();
        var pasta = temp.CreateSubFolder("ads");

        var streams = AlternateDataStreamScanner.ScanFolder(pasta, recursive: true);

        Assert.DoesNotContain(streams, s => s.IsDefault);
    }
}

/// <summary>
/// O modo de visao de cores sobrevive a ida e volta do arquivo de
/// preferencias.
///
/// Na colecao "preferencias" porque o <see cref="AppSettingsStore"/> e
/// estado global: dois testes que gravam preferencias ao mesmo tempo
/// brigariam pelo mesmo arquivo.
/// </summary>
[Collection("preferencias")]
public class ColorVisionSettingsTests
{
    [Fact]
    public void OModoDeVisaoDeCoresSobreviveAGravacao()
    {
        var temp = Path.Combine(Path.GetTempPath(),
            "tranal-modo-visao-" + Guid.NewGuid().ToString("N")[..8] + ".json");

        try
        {
            AppSettingsStore.FilePathOverride = temp;

            AppSettingsStore.Save(new AppSettings { ColorVisionMode = "Deuteranopia" });

            Assert.Equal("Deuteranopia", AppSettingsStore.Load().ColorVisionMode);
        }
        finally
        {
            AppSettingsStore.FilePathOverride = null;
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch { /* o temporario e descartavel */ }
        }
    }

    /// <summary>
    /// Uma preferencia corrompida nao pode deixar a tela sem cor: o modo
    /// volta para o padrao.
    /// </summary>
    [Fact]
    public void OModoDeVisaoDeCoresVazioVoltaParaOPadrao()
    {
        var temp = Path.Combine(Path.GetTempPath(),
            "tranal-modo-visao-vazio-" + Guid.NewGuid().ToString("N")[..8] + ".json");

        try
        {
            File.WriteAllText(temp, "{\"ColorVisionMode\": \"  \"}");

            AppSettingsStore.FilePathOverride = temp;

            Assert.Equal("Standard", AppSettingsStore.Load().ColorVisionMode);
        }
        finally
        {
            AppSettingsStore.FilePathOverride = null;
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch { /* o temporario e descartavel */ }
        }
    }
}

/// <summary>
/// A casca da tela: as ferramentas novas, a opcao de visao de cores e o
/// filtro de uso raro precisam existir na interface.
///
/// A verificacao e feita sobre o fonte da interface: a suite nao referencia
/// o projeto grafico (WPF) — o nucleo e compilado tambem na CLI, e
/// referenciar os dois criaria dois tipos "Core" diferentes.
/// </summary>
public class PrivacyToolsGuiParityTests
{
    /// <summary>Le um arquivo do projeto grafico.</summary>
    private static string Read(params string[] parts) => RepoFiles.Read(parts);

    /// <summary>
    /// A tela dos rastros de privacidade existe e consome o limpador do
    /// nucleo.
    /// </summary>
    [Fact]
    public void AInterfaceTemATelaDeRastrosDePrivacidade()
    {
        var code = Read("src", "Tranal.Uninstaller", "Ui", "Tools", "ToolViews.cs");

        Assert.Contains("CreatePrivacyTraces", code);
        Assert.Contains("PrivacyTraceCleaner.Scan", code);
        Assert.Contains("PrivacyTraceCleaner.Clean", code);

        // Os comandos da tela tem identificador de automacao.
        Assert.Contains("PrivacyTracesScan", code);
        Assert.Contains("PrivacyTracesClean", code);
    }

    /// <summary>
    /// A tela dos fluxos alternativos existe e consome o varredor do
    /// nucleo.
    /// </summary>
    [Fact]
    public void AInterfaceTemATelaDeFluxosAlternativos()
    {
        var code = Read("src", "Tranal.Uninstaller", "Ui", "Tools", "ToolViews.cs");

        Assert.Contains("CreateAlternateDataStreams", code);
        Assert.Contains("AlternateDataStreamScanner.ScanFolder", code);
        Assert.Contains("AdsScanFolder", code);
    }

    /// <summary>
    /// A tela das ferramentas personalizadas existe e consome o registro do
    /// nucleo (e nao uma lista propria da tela).
    /// </summary>
    [Fact]
    public void AInterfaceTemATelaDeFerramentasPersonalizadas()
    {
        var code = Read("src", "Tranal.Uninstaller", "Ui", "Tools", "ToolViews.cs");

        Assert.Contains("CreateCustomTools", code);
        Assert.Contains("CustomToolRegistry.List", code);
        Assert.Contains("CustomToolRegistry.Add", code);
        Assert.Contains("CustomToolRegistry.Rename", code);
        Assert.Contains("CustomToolRegistry.Remove", code);
        Assert.Contains("CustomToolRegistry.Run", code);
    }

    /// <summary>
    /// A janela de opcoes tem o modo de visao de cores e o grava.
    /// </summary>
    [Fact]
    public void AsOpcoesTemOModoDeVisaoDeCores()
    {
        var xaml = Read("src", "Tranal.Uninstaller", "Ui", "OptionsWindow.xaml");
        var code = Read("src", "Tranal.Uninstaller", "Ui", "OptionsWindow.xaml.cs");

        Assert.Contains("OptColorVisionCombo", xaml);
        Assert.Contains("ColorVisionCombo", code);
        Assert.Contains("s.ColorVisionMode =", code);

        // A lista de modos vem do enum do nucleo.
        Assert.Contains("Enum.GetValues<ColorVisionMode>()", code);
    }

    /// <summary>
    /// As janelas de opcoes e as telas novas sao abertas de um lugar que o
    /// usuario alcanca.
    /// </summary>
    [Fact]
    public void AsOpcoesAbremAsFerramentasNovas()
    {
        var xaml = Read("src", "Tranal.Uninstaller", "Ui", "OptionsWindow.xaml");
        var code = Read("src", "Tranal.Uninstaller", "Ui", "OptionsWindow.xaml.cs");

        Assert.Contains("OptOpenPrivacyTraces", xaml);
        Assert.Contains("OptOpenAlternateDataStreams", xaml);
        Assert.Contains("OptOpenCustomTools", xaml);

        Assert.Contains("ToolViews.CreatePrivacyTraces()", code);
        Assert.Contains("ToolViews.CreateAlternateDataStreams()", code);
        Assert.Contains("ToolViews.CreateCustomTools()", code);
    }

    /// <summary>
    /// O painel lateral tem a caixa do filtro de uso raro e a escreve no
    /// conjunto de filtros do nucleo.
    /// </summary>
    [Fact]
    public void OPainelLateralTemOFiltroDeUsoRaro()
    {
        var xaml = Read("src", "Tranal.Uninstaller", "Ui", "Appearance", "SettingsSidebar.xaml");
        var code = Read("src", "Tranal.Uninstaller", "Ui", "Appearance", "SettingsSidebar.xaml.cs");

        Assert.Contains("OptOnlyRarelyUsed", xaml);
        Assert.Contains("_filters.OnlyRarelyUsed", code);
    }

    /// <summary>
    /// O painel lateral abre o gerenciador das ferramentas personalizadas.
    /// </summary>
    [Fact]
    public void OPainelLateralAbreOFerramentasPersonalizadas()
    {
        var xaml = Read("src", "Tranal.Uninstaller", "Ui", "Appearance", "SettingsSidebar.xaml");
        var code = Read("src", "Tranal.Uninstaller", "Ui", "Appearance", "SettingsSidebar.xaml.cs");

        Assert.Contains("OptManageCustomTools", xaml);
        Assert.Contains("ToolViews.CreateCustomTools()", code);
    }

    /// <summary>
    /// O tema aplica a paleta adaptada depois de montar as brushes: a
    /// adaptacao mora no nucleo e a tela so escreve a cor.
    /// </summary>
    [Fact]
    public void OTemaAplicaAPaletaAdaptada()
    {
        var code = Read("src", "Tranal.Uninstaller", "Theming", "ThemeManager.cs");

        Assert.Contains("ApplyColorVisionPalette", code);
        Assert.Contains("ColorBlindPalette.Adapt", code);
        Assert.Contains("ColorBlindPalette.IsAdjusted", code);
    }

    /// <summary>
    /// A regra do filtro mora no nucleo: e o conjunto de filtros que decide,
    /// e nao a tela.
    /// </summary>
    [Fact]
    public void ARegraDoUsoRaroMoraNoNucleo()
    {
        var code = Read("src", "Tranal.Uninstaller", "Core", "Appearance", "ProgramFilters.cs");

        Assert.Contains("OnlyRarelyUsed", code);
        Assert.Contains("UsageSignals", code);
    }
}
