using Tranal.Uninstaller.Core;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// A desinstalação silenciosa.
///
/// O switch não é adivinhado: ele vem da família do desinstalador, que o
/// Tranal classifica. A tabela veio do comportamento observado de três
/// concorrentes (BCU, Uninstalr e HiBit).
/// </summary>
public class QuietUninstallTests
{
    [Fact]
    public void CadaFamiliaConhecidaTemOSeuSwitch()
    {
        Assert.Equal(
            "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
            QuietUninstall.SwitchFor(UninstallerKind.InnoSetup));

        Assert.Equal("/S", QuietUninstall.SwitchFor(UninstallerKind.Nsis));

        Assert.Equal("/quiet /qn /norestart", QuietUninstall.SwitchFor(UninstallerKind.MsiExec));

        Assert.Equal("/s /v\"/qn\"", QuietUninstall.SwitchFor(UninstallerKind.InstallShield));

        Assert.Equal("/S", QuietUninstall.SwitchFor(UninstallerKind.SdbInst));
    }

    [Theory]
    [InlineData(UninstallerKind.Unknown)]
    [InlineData(UninstallerKind.StoreApp)]
    [InlineData(UninstallerKind.Steam)]
    [InlineData(UninstallerKind.Chocolatey)]
    [InlineData(UninstallerKind.Oculus)]
    [InlineData(UninstallerKind.PowerShell)]
    [InlineData(UninstallerKind.SimpleDelete)]
    public void FamiliaSemSwitchConhecidoNaoInventaNenhum(UninstallerKind kind)
    {
        // Inventar um switch para uma familia desconhecida seria rodar o
        // desinstalador do usuario com opcoes que ele nao pediu.
        Assert.Null(QuietUninstall.SwitchFor(kind));
    }

    [Fact]
    public void OValorDoRegistroTemPrioridade()
    {
        // Quando o proprio registro traz o QuietUninstallString, ele e a
        // resposta certa: nao ha por que montar nada.
        var program = new InstalledProgram
        {
            DisplayName = "MeuApp",
            Kind = UninstallerKind.Nsis,
            UninstallString = @"""C:\Program Files\MeuApp\uninstall.exe""",
            QuietUninstallString = @"""C:\Program Files\MeuApp\uninstall.exe"" /S",
        };

        Assert.Equal(
            @"""C:\Program Files\MeuApp\uninstall.exe"" /S",
            QuietUninstall.Build(program));
    }

    [Fact]
    public void InnoSetupRecebeOSwitchDoInno()
    {
        var program = new InstalledProgram
        {
            DisplayName = "MeuApp",
            Kind = UninstallerKind.InnoSetup,
            UninstallString = @"""C:\Program Files\MeuApp\unins000.exe""",
        };

        Assert.Equal(
            @"""C:\Program Files\MeuApp\unins000.exe"" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
            QuietUninstall.Build(program));
    }

    [Fact]
    public void NsisRecebeOSwitchDoNsis()
    {
        var program = new InstalledProgram
        {
            DisplayName = "MeuApp",
            Kind = UninstallerKind.Nsis,
            UninstallString = @"""C:\Program Files\MeuApp\uninstall.exe""",
        };

        Assert.Equal(
            @"""C:\Program Files\MeuApp\uninstall.exe"" /S",
            QuietUninstall.Build(program));
    }

    [Fact]
    public void MsiRecebeOsSwitchesDoMsi()
    {
        var program = new InstalledProgram
        {
            DisplayName = "MeuApp",
            Kind = UninstallerKind.MsiExec,
            UninstallString = @"MsiExec.exe /X{12345678-1234-1234-1234-123456789012}",
        };

        Assert.Equal(
            @"MsiExec.exe /X{12345678-1234-1234-1234-123456789012} /quiet /qn /norestart",
            QuietUninstall.Build(program));
    }

    [Fact]
    public void OInstallShieldTemOComandoSubstituido()
    {
        // O InstallShield nao aceita o switch em conjunto: o "/x{GUID}" do
        // comando de desinstalacao e substituido pela forma silenciosa.
        var program = new InstalledProgram
        {
            DisplayName = "MeuApp",
            Kind = UninstallerKind.InstallShield,
            UninstallString =
                @"""C:\Program Files (x86)\InstallShield Installation Information\{X}\setup.exe"" /x{12345678-1234-1234-1234-123456789012}",
        };

        var result = QuietUninstall.Build(program);

        Assert.Equal(
            @"""C:\Program Files (x86)\InstallShield Installation Information\{X}\setup.exe"" /s /v""/qn""",
            result);
        Assert.DoesNotContain("/x{", result);
    }

    [Fact]
    public void SemSwitchConhecidoNaoHaComandoSilencioso()
    {
        var program = new InstalledProgram
        {
            DisplayName = "MeuApp",
            Kind = UninstallerKind.Unknown,
            UninstallString = @"""C:\Program Files\MeuApp\remove.exe""",
        };

        Assert.Equal(string.Empty, QuietUninstall.Build(program));
        Assert.False(QuietUninstall.Supports(program));
    }

    [Fact]
    public void OComandoJaSilenciosoNaoEAlterado()
    {
        var program = new InstalledProgram
        {
            DisplayName = "MeuApp",
            Kind = UninstallerKind.Nsis,
            UninstallString = @"""C:\Program Files\MeuApp\uninstall.exe"" /S",
        };

        Assert.Equal(
            @"""C:\Program Files\MeuApp\uninstall.exe"" /S",
            QuietUninstall.Build(program));
    }

    [Fact]
    public void SemComandoDeDesinstalacaoNaoHaComandoSilencioso()
    {
        var program = new InstalledProgram { DisplayName = "MeuApp", Kind = UninstallerKind.Nsis };

        Assert.Equal(string.Empty, QuietUninstall.Build(program));
    }

    [Fact]
    public void OTipoDesconhecidoEhClassificadoAntesDeMontarOComando()
    {
        // Um programa lido fora do scanner (por exemplo, vindo de um
        // arquivo) pode ainda nao ter o tipo classificado.
        var program = new InstalledProgram
        {
            DisplayName = "MeuApp",
            UninstallString = @"""C:\Program Files\MeuApp\unins000.exe""",
        };

        Assert.Equal(
            @"""C:\Program Files\MeuApp\unins000.exe"" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
            QuietUninstall.Build(program));
    }

    [Fact]
    public void AColunaMostraOComandoSilencioso()
    {
        var program = new InstalledProgram
        {
            DisplayName = "MeuApp",
            Kind = UninstallerKind.Nsis,
            UninstallString = @"""C:\Program Files\MeuApp\uninstall.exe""",
            QuietUninstallCommand = @"""C:\Program Files\MeuApp\uninstall.exe"" /S",
        };

        Assert.Equal(
            @"""C:\Program Files\MeuApp\uninstall.exe"" /S",
            ProgramColumns.Value(ProgramColumn.QuietUninstall, program, null));
    }

    [Fact]
    public void AColunaNovaTemNomeDeExportacaoFixo()
    {
        Assert.True(ProgramColumns.TryParse(new[] { "Program", "QuietUninstall" }, out var colunas, out _));
        Assert.Contains(ProgramColumn.QuietUninstall, colunas!);
    }
}

/// <summary>
/// A desinstalação silenciosa precisa estar ligada nas duas pontas: a
/// interface (assistente) e a CLI. Uma delas sem a opção é um recurso pela
/// metade.
/// </summary>
public class QuietUninstallUiTests
{
    [Fact]
    public void OAssistenteTemAMarcaDeSilencioso()
    {
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml");
        var code = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml.cs");

        Assert.Contains("x:Name=\"SilentUninstallCheck\"", xaml);
        Assert.Contains("SilentUninstallCheck.IsChecked == true", code);
        Assert.Contains("RunBuiltInUninstaller(program, silent)", code);
    }

    [Fact]
    public void ACliTemAOpcaoSilencioso()
    {
        var cli = RepoFiles.Read("src", "Tranal.Cli", "Program.cs");

        Assert.Contains("flags.Contains(\"/silent\")", cli);
        Assert.Contains("RunBuiltInUninstaller(target, silent)", cli);
        Assert.Contains("/silent   - Executa o desinstalador nativo em silêncio", cli);
    }

    [Fact]
    public void ODesinstaladorNativoAceitaOParametroSilencioso()
    {
        var core = RepoFiles.Read("src", "Tranal.Uninstaller", "Core", "UninstallSupport.cs");

        Assert.Contains(
            "public static async Task<BuiltInUninstallResult> RunBuiltInUninstaller(",
            core);
        Assert.Contains("InstalledProgram program, bool silent = false)", core);
    }
}
