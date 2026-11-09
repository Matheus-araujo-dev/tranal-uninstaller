using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Os avisos de seguranca da desinstalacao.
///
/// O original (Revo Uninstaller Pro) avisa sobre tres coisas que o Tranal nao
/// avisava: que outro programa divide a pasta de instalacao, que remover o
/// programa pode parar recursos do Windows, e que as pastas listadas podem
/// conter dados do usuario e documentos.
///
/// O terceiro e o mais grave: uma sobra em "C:\Users\Alguem\Documents\MeuApp"
/// pode ser trabalho do usuario, e a varredura por nome a marca para remocao
/// como se fosse cache de programa.
///
/// A regra mora no nucleo (<c>Core/UninstallAdvisories.cs</c>); estes testes
/// medem a regra, e nao o texto exibido.
/// </summary>
public class UninstallAdvisoriesTests
{
    /// <summary>Cria um programa instalado com os campos que os avisos leem.</summary>
    private static InstalledProgram Program(
        string name,
        string installLocation = "",
        bool systemComponent = false,
        string registryPath = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\X")
        => new()
        {
            DisplayName = name,
            InstallLocation = installLocation,
            IsSystemComponent = systemComponent,
            RegistryPath = registryPath,
        };

    /// <summary>Cria uma sobra de pasta.</summary>
    private static LeftoverItem Folder(string path) => new()
    {
        Kind = "Pasta",
        Path = path,
        Confidence = LeftoverConfidence.High,
        Reason = LeftoverReason.VariantNameMatch,
    };

    /// <summary>Cria uma sobra de registro.</summary>
    private static LeftoverItem RegistryItem(string path) => new()
    {
        Kind = "Registro",
        Path = path,
        Confidence = LeftoverConfidence.High,
        Reason = LeftoverReason.RegistrySecondLevel,
    };

    // ---------------------------------------------------------------------
    // Pasta de instalacao compartilhada
    // ---------------------------------------------------------------------

    [Fact]
    public void ProgramaQueDivideAPastaComOutroGeraAviso()
    {
        var alvo = Program("MeuApp", @"C:\Program Files\Suite");
        var vizinho = Program("OutroApp", @"C:\Program Files\Suite",
            registryPath: @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Y");

        var advisory = UninstallAdvisories.SharedInstallLocationAdvisory(alvo, new[] { alvo, vizinho });

        Assert.NotNull(advisory);
        Assert.Equal(UninstallAdvisoryKind.SharedInstallLocation, advisory!.Kind);
        Assert.Contains("OutroApp", advisory.Items);
        Assert.DoesNotContain("MeuApp", advisory.Items);
    }

    [Fact]
    public void ProgramaQueNaoDividePastaComNinguemNaoGeraAviso()
    {
        var alvo = Program("MeuApp", @"C:\Program Files\MeuApp");
        var outro = Program("OutroApp", @"C:\Program Files\OutroApp",
            registryPath: @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Y");

        Assert.Null(UninstallAdvisories.SharedInstallLocationAdvisory(alvo, new[] { alvo, outro }));
    }

    [Fact]
    public void PastaDeInstalacaoVaziaNaoGeraAviso()
    {
        // Metade dos programas nao registra InstallLocation. Sem ela nao ha
        // pasta compartilhada a comparar.
        var alvo = Program("MeuApp");
        var outro = Program("OutroApp",
            registryPath: @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Y");

        Assert.Null(UninstallAdvisories.SharedInstallLocationAdvisory(alvo, new[] { alvo, outro }));
    }

    [Fact]
    public void AMesmaEntradaLidaDuasVezesNaoContaComoVizinho()
    {
        // A lista pode vir de varias fontes e repetir a mesma entrada.
        var alvo = Program("MeuApp", @"C:\Program Files\Suite");
        var mesma = Program("MeuApp", @"C:\Program Files\Suite");

        Assert.Null(UninstallAdvisories.SharedInstallLocationAdvisory(alvo, new[] { alvo, mesma }));
    }

    [Fact]
    public void PastaIrmaoNaoContaComoPastaCompartilhada()
    {
        // "C:\Program Files\App" e "C:\Program Files" NAO sao a mesma pasta.
        // Tratar como compartilhada transformaria o aviso em ruido.
        var alvo = Program("MeuApp", @"C:\Program Files\MeuApp");
        var outro = Program("OutroApp", @"C:\Program Files",
            registryPath: @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Y");

        Assert.Null(UninstallAdvisories.SharedInstallLocationAdvisory(alvo, new[] { alvo, outro }));
    }

    [Fact]
    public void ComparacaoDaPastaIgnoraMaiusculasEBarraFinal()
    {
        var alvo = Program("MeuApp", @"C:\Program Files\Suite");
        var vizinho = Program("OutroApp", @"c:\program files\suite\",
            registryPath: @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Y");

        var advisory = UninstallAdvisories.SharedInstallLocationAdvisory(alvo, new[] { alvo, vizinho });

        Assert.NotNull(advisory);
        Assert.Contains("OutroApp", advisory!.Items);
    }

    [Fact]
    public void SemListaDeProgramadosNaoHaAvisoDePastaCompartilhada()
    {
        var alvo = Program("MeuApp", @"C:\Program Files\Suite");

        Assert.Null(UninstallAdvisories.SharedInstallLocationAdvisory(alvo, null));
    }

    // ---------------------------------------------------------------------
    // Componente do sistema
    // ---------------------------------------------------------------------

    [Fact]
    public void ComponenteDoSistemaGeraAviso()
    {
        var advisory = UninstallAdvisories.SystemComponentAdvisory(
            Program("Recurso do Windows", systemComponent: true));

        Assert.NotNull(advisory);
        Assert.Equal(UninstallAdvisoryKind.SystemComponent, advisory!.Kind);
    }

    [Fact]
    public void ProgramaComumNaoGeraAvisoDeComponenteDoSistema()
    {
        Assert.Null(UninstallAdvisories.SystemComponentAdvisory(Program("MeuApp")));
    }

    // ---------------------------------------------------------------------
    // Dados do usuario
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(@"C:\Users\Alguem\Documents\MeuApp")]
    [InlineData(@"C:\Users\Alguem\Desktop\MeuApp")]
    [InlineData(@"C:\Users\Alguem\Pictures\MeuApp")]
    [InlineData(@"C:\Users\Alguem\Music\MeuApp")]
    [InlineData(@"C:\Users\Alguem\Videos\MeuApp")]
    [InlineData(@"C:\Users\Alguem\Saved Games\MeuApp")]
    [InlineData(@"C:\Users\Public\MeuApp")]
    public void PastaDeConteudoDoUsuarioEReconhecida(string path)
    {
        Assert.True(UninstallAdvisories.IsUserDataFolder(path));
    }

    [Theory]
    [InlineData(@"C:\Users\Alguem\AppData\Local\MeuApp")]
    [InlineData(@"C:\Program Files\MeuApp")]
    [InlineData(@"C:\Windows\System32\MeuApp")]
    [InlineData(@"C:\Users\Alguem\AppData\Roaming\MeuApp\Config")]
    [InlineData(@"")]
    public void PastaDeProgramaNaoEConteudoDoUsuario(string path)
    {
        Assert.False(UninstallAdvisories.IsUserDataFolder(path));
    }

    [Fact]
    public void SoPastasEntramNoAvisoDeDadosDoUsuario()
    {
        var sobras = new[]
        {
            Folder(@"C:\Users\Alguem\Documents\MeuApp"),
            RegistryItem(@"HKCU\Software\MeuApp"),
        };

        var folders = UninstallAdvisories.UserDataFolders(sobras);

        Assert.Single(folders);
        Assert.Equal(@"C:\Users\Alguem\Documents\MeuApp", folders[0]);
    }

    [Fact]
    public void AvisoDeDadosDoUsuarioSomeQuandoNenhumaPastaEDeConteudo()
    {
        var sobras = new[]
        {
            Folder(@"C:\Program Files\MeuApp"),
            Folder(@"C:\Users\Alguem\AppData\Local\MeuApp"),
        };

        Assert.Null(UninstallAdvisories.UserDataAdvisory(sobras));
    }

    [Fact]
    public void AvisoDeDadosDoUsuarioListaAsPastas()
    {
        var sobras = new[]
        {
            Folder(@"C:\Users\Alguem\Documents\MeuApp"),
            Folder(@"C:\Users\Alguem\Desktop\MeuApp"),
        };

        var advisory = UninstallAdvisories.UserDataAdvisory(sobras);

        Assert.NotNull(advisory);
        Assert.Equal(UninstallAdvisoryKind.UserData, advisory!.Kind);
        Assert.Equal(2, advisory.Items.Count);
    }

    [Fact]
    public void SemSobrasNaoHaAvisoDeDadosDoUsuario()
    {
        Assert.Null(UninstallAdvisories.UserDataAdvisory(null));
        Assert.Null(UninstallAdvisories.UserDataAdvisory(Array.Empty<LeftoverItem>()));
    }

    // ---------------------------------------------------------------------
    // Composicao e traducao
    // ---------------------------------------------------------------------

    [Fact]
    public void BuildJuntaOsTresAvisosDoRevoEOAvisoDeResiduo()
    {
        var alvo = Program("MeuApp", @"C:\Program Files\Suite", systemComponent: true,
            registryPath: @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MeuApp");
        var vizinho = Program("OutroApp", @"C:\Program Files\Suite",
            registryPath: @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Y");

        var advisories = UninstallAdvisories.Build(
            alvo,
            new[] { alvo, vizinho },
            new[] { Folder(@"C:\Users\Alguem\Documents\MeuApp") });

        // Sao os tres avisos do Revo e, quando a varredura achou sobra, o
        // quarto: o aviso de que o desinstalador do fabricante vai deixar
        // residuo (o "still leaves" do IObit).
        Assert.Equal(4, advisories.Count);
        Assert.Contains(advisories, a => a.Kind == UninstallAdvisoryKind.SharedInstallLocation);
        Assert.Contains(advisories, a => a.Kind == UninstallAdvisoryKind.SystemComponent);
        Assert.Contains(advisories, a => a.Kind == UninstallAdvisoryKind.UserData);
        Assert.Contains(advisories, a => a.Kind == UninstallAdvisoryKind.LeavesResidue);
    }

    [Fact]
    public void BuildSemNadaAParadoDevolveListaVazia()
    {
        var advisories = UninstallAdvisories.Build(Program("MeuApp"));

        Assert.Empty(advisories);
    }

    /// <summary>
    /// As chaves dos avisos precisam existir nos dois catálogos: sem traducao
    /// a tela mostraria o nome cru da chave.
    /// </summary>
    [Theory]
    [InlineData("Advisory.Title.SharedInstallLocation")]
    [InlineData("Advisory.Title.SystemComponent")]
    [InlineData("Advisory.Title.UserData")]
    [InlineData("Advisory.SharedInstallLocation")]
    [InlineData("Advisory.SystemComponent")]
    [InlineData("Advisory.UserData")]
    public void ChaveDeAvisoExisteNosDoisCatalogos(string key)
    {
        var pt = Loc.Load("pt-BR");
        var en = Loc.Load("en-US");

        Assert.True(pt.ContainsKey(key), $"pt-BR nao tem a chave {key}");
        Assert.True(en.ContainsKey(key), $"en-US nao tem a chave {key}");
    }

    /// <summary>
    /// O texto do aviso de pasta compartilhada precisa conter o nome do
    /// programa vizinho — e o que o usuario precisa para entender o aviso.
    /// </summary>
    [Fact]
    public void TextoDoAvisoContemONomeDoProgramaVizinho()
    {
        var original = Loc.Current;
        try
        {
            Loc.SetLanguage("pt-BR");

            var advisory = UninstallAdvisories.SharedInstallLocationAdvisory(
                Program("MeuApp", @"C:\Program Files\Suite"),
                new[]
                {
                    Program("MeuApp", @"C:\Program Files\Suite"),
                    Program("OutroApp", @"C:\Program Files\Suite",
                        registryPath: @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Y"),
                });

            Assert.NotNull(advisory);
            Assert.Contains("OutroApp", advisory!.Message);
            Assert.NotEmpty(advisory.Title);
        }
        finally { Loc.SetLanguage(original); }
    }
}
