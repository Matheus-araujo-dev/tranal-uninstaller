using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Classificacao do desinstalador.
///
/// A classificacao e o que decide a estrategia de desinstalacao, e por isso
/// as regras sao fixas e testadas uma a uma. Um empacotador esquecido aqui
/// e um empacotador que a desinstalacao vai tratar errado.
/// </summary>
public class UninstallerClassifierTests
{
    [Theory]
    // Windows Installer: o comando e o proprio msiexec.
    [InlineData(@"MsiExec.exe /X{12345678-1234-1234-1234-123456789012}", null, "MsiExec")]
    // Inno Setup: o nome do desinstalador e distintoivo.
    [InlineData(@"""C:\Program Files\App\unins000.exe""", null, "InnoSetup")]
    [InlineData(@"""C:\Program Files\App\unins000.exe"" /VERYSILENT", null, "InnoSetup")]
    // NSIS: convencao do nome do desinstalador.
    [InlineData(@"""C:\Program Files\App\uninstall.exe"" /S", null, "Nsis")]
    // InstallShield: setup.exe com o switch /x.
    [InlineData(@"""C:\Program Files (x86)\InstallShield Installation Information\{X}\setup.exe"" /x{X}", null, "InstallShield")]
    // SDBInst: o nome e mais especifico que InstallShield e vem primeiro.
    [InlineData(@"C:\Windows\Installer\sdbinst.exe", null, "SdbInst")]
    // Lojas de aplicativo.
    [InlineData(@"steam://uninstall/12345", null, "Steam")]
    [InlineData(@"choco uninstall meu-pacote -y", null, "Chocolatey")]
    [InlineData(@"""C:\Program Files\Oculus\uninstall.exe""", null, "Oculus")]
    // Script.
    [InlineData(@"powershell -ExecutionPolicy Bypass -File ""C:\Scripts\remove.ps1""", null, "PowerShell")]
    // Remocao simples: o comando so apaga.
    [InlineData(@"cmd /c rmdir /s /q ""C:\Program Files\App""", null, "SimpleDelete")]
    [InlineData(@"cmd /c del /f ""C:\Program Files\App\app.exe""", null, "SimpleDelete")]
    // Desinstalador proprio sem nome conhecido.
    [InlineData(@"""C:\Program Files\App\remove-me.exe""", null, "Unknown")]
    public void ClassificaPeloComandoDeDesinstalacao(string comando, string? pasta, string esperado)
    {
        var real = (UninstallerKind)Enum.Parse(typeof(UninstallerKind), esperado);

        Assert.Equal(real, UninstallerClassifier.Classify(comando, pasta));
    }

    [Fact]
    public void RegistroComWindowsInstallerUmEhMsiExec()
    {
        // O valor "WindowsInstaller" do registro ja declara o tipo: o comando
        // pode ser qualquer coisa e a classificacao nao muda.
        Assert.Equal(
            UninstallerKind.MsiExec,
            UninstallerClassifier.Classify(@"""C:\Program Files\App\setup.exe"" /x", null, false, true));

        Assert.Equal(
            UninstallerKind.MsiExec,
            UninstallerClassifier.Classify(@"""C:\Program Files\App\setup.exe"" /x", null, true, false));
    }

    [Fact]
    public void AplicativoDaStoreEhReconhecidoPelaPastaDeInstalacao()
    {
        Assert.Equal(
            UninstallerKind.StoreApp,
            UninstallerClassifier.Classify(
                @"""C:\Program Files\WindowsApps\MeuApp_1.0.0.0_x64__8wekyb3d8bbwe\app.exe""",
                @"C:\Program Files\WindowsApps\MeuApp_1.0.0.0_x64__8wekyb3d8bbwe"));

        // Sem a pista da pasta, o mesmo comando nao vira Store App.
        Assert.Equal(
            UninstallerKind.Unknown,
            UninstallerClassifier.Classify(
                @"""C:\Program Files\WindowsApps\MeuApp_1.0.0.0_x64__8wekyb3d8bbwe\app.exe"""));
    }

    [Fact]
    public void InnoSetupVenceAntesDeNsis()
    {
        // A ordem das regras e fixa: o nome do Inno e mais distintoivo e
        // vem primeiro, mesmo que a pasta contenha "uninstall".
        var kind = UninstallerClassifier.Classify(
            @"""C:\Program Files\App\uninstall\unins000.exe""");

        Assert.Equal(UninstallerKind.InnoSetup, kind);
    }

    /// <summary>
    /// Regressao medida nesta maquina: o "EA app" usa um desinstalador
    /// chamado "EAUninstall.exe". O nome contem "uninstall.exe", mas o
    /// arquivo NAO e o desinstalador do NSIS — e o casamento e pelo nome do
    /// arquivo, nao por substring.
    /// </summary>
    [Theory]
    [InlineData(@"""C:\Program Files\Electronic Arts\EA Desktop\EA Desktop\EAUninstall.exe"" /uninstall")]
    [InlineData(@"""C:\Program Files\App\my-uninstall.exe""")]
    [InlineData(@"""C:\Program Files\App\uninstaller.exe""")]
    public void DesinstaladorComUninstallNoNomeNaoEhNsis(string comando)
    {
        Assert.NotEqual(UninstallerKind.Nsis, UninstallerClassifier.Classify(comando));
    }

    [Fact]
    public void InstallShieldSoComSetupComSwitchX()
    {
        // "setup.exe" sozinho nao prova InstallShield — qualquer instalador
        // pode ter um setup.exe. O switch /x e o que distingue.
        Assert.Equal(
            UninstallerKind.Unknown,
            UninstallerClassifier.Classify(@"""C:\Program Files\App\setup.exe"""));

        Assert.Equal(
            UninstallerKind.InstallShield,
            UninstallerClassifier.Classify(@"""C:\Program Files\App\setup.exe"" /x"));
    }

    [Fact]
    public void ExtraiCodigoDoProdutoDoComando()
    {
        Assert.Equal(
            "{12345678-1234-1234-1234-123456789012}",
            UninstallerClassifier.ExtractProductCode(
                @"MsiExec.exe /X{12345678-1234-1234-1234-123456789012}"));

        Assert.Equal(
            "{12345678-1234-1234-1234-123456789012}",
            UninstallerClassifier.ExtractProductCode(
                @"MsiExec.exe /I{GUID} /X{12345678-1234-1234-1234-123456789012}"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"""C:\Program Files\App\uninstall.exe""")]
    public void SemGuidNaoHaCodigoDoProduto(string? comando)
    {
        Assert.Equal(string.Empty, UninstallerClassifier.ExtractProductCode(comando));
    }

    /// <summary>
    /// Guarda contra membro morto: todo tipo do enumerador precisa ser
    /// produzido por alguma regra. Um tipo que nenhuma regra produz e um
    /// tipo que a lista mostraria sem nunca acontecer.
    /// </summary>
    [Fact]
    public void TodoTipoDoEnumeradorERealizavel()
    {
        var comandos = new (string Comando, string? Pasta, bool Msi)[]
        {
            (@"MsiExec.exe /X{12345678-1234-1234-1234-123456789012}", null, false),
            (@"""C:\Program Files\App\unins000.exe""", null, false),
            (@"""C:\Program Files\App\uninstall.exe"" /S", null, false),
            (@"""C:\InstallShield Installation Information\{X}\setup.exe"" /x{X}", null, false),
            (@"C:\Windows\Installer\sdbinst.exe", null, false),
            (@"steam://uninstall/12345", null, false),
            (@"""C:\Program Files\WindowsApps\App\app.exe""", @"C:\Program Files\WindowsApps\App", false),
            (@"choco uninstall meu-pacote -y", null, false),
            (@"""C:\Program Files\Oculus\remove.exe""", null, false),
            (@"powershell -File ""C:\Scripts\remove.ps1""", null, false),
            (@"cmd /c rmdir /s /q ""C:\Program Files\App""", null, false),
            (@"""C:\Program Files\App\remove-me.exe""", null, false),
        };

        var produzidos = comandos
            .Select(c => UninstallerClassifier.Classify(c.Comando, c.Pasta, c.Msi, c.Msi))
            .ToHashSet();

        foreach (var tipo in Enum.GetValues<UninstallerKind>())
        {
            Assert.True(produzidos.Contains(tipo),
                $"Nenhuma regra produz o tipo {tipo}: ele jamais apareceria na lista.");
        }
    }

    [Fact]
    public void ClassificaUmProgramaLidoDoRegistro()
    {
        var program = new InstalledProgram
        {
            DisplayName = "Meu App",
            UninstallString = @"""C:\Program Files\MeuApp\unins000.exe""",
        };

        Assert.Equal(UninstallerKind.InnoSetup, UninstallerClassifier.Classify(program));
    }
}

/// <summary>
/// As colunas novas precisam existir no catalogo, ter nome de exportacao
/// fixo e ser pedidas pelo nome na CLI.
/// </summary>
public class UninstallerKindColumnTests
{
    [Fact]
    public void AsColunasNovasEstaoNoCatalogo()
    {
        Assert.Contains(ProgramColumns.All, c => c.Id == ProgramColumn.UninstallerKind);
        Assert.Contains(ProgramColumns.All, c => c.Id == ProgramColumn.ProductCode);
    }

    [Fact]
    public void OValorDaColunaEoTipoEDoCodigoDoProduto()
    {
        var program = new InstalledProgram
        {
            DisplayName = "Meu App",
            Kind = UninstallerKind.Nsis,
            ProductCode = "{12345678-1234-1234-1234-123456789012}",
        };

        Assert.Equal("Nsis", ProgramColumns.Value(ProgramColumn.UninstallerKind, program, null));
        Assert.Equal(
            "{12345678-1234-1234-1234-123456789012}",
            ProgramColumns.Value(ProgramColumn.ProductCode, program, null));
    }

    [Fact]
    public void OValorDaColunaNaoDependeDoIdioma()
    {
        // O valor e o identificador fixo do tipo: a coluna exportada
        // continua legivel por qualquer ferramenta, em qualquer idioma.
        var program = new InstalledProgram { Kind = UninstallerKind.MsiExec };

        var original = Loc.Current;
        try
        {
            Loc.Load("pt-BR");
            Loc.SetLanguage("pt-BR");
            var emPortugues = ProgramColumns.Value(ProgramColumn.UninstallerKind, program, null);

            Loc.Load("en-US");
            Loc.SetLanguage("en-US");
            var emIngles = ProgramColumns.Value(ProgramColumn.UninstallerKind, program, null);

            Assert.Equal("MsiExec", emPortugues);
            Assert.Equal(emPortugues, emIngles);
        }
        finally { Loc.SetLanguage(original); }
    }

    [Fact]
    public void TodaColunaTemRotuloNoCatalogo()
    {
        // Sem isso, uma coluna nova apareceria na tela com o nome da chave
        // ("Common.UninstallerKind") em vez do texto.
        var catalog = Loc.Load("pt-BR");
        Assert.NotEmpty(catalog);

        var faltando = ProgramColumns.All
            .Where(c => !catalog.ContainsKey(c.LabelKey))
            .Select(c => c.LabelKey)
            .ToList();

        Assert.True(faltando.Count == 0,
            "Colunas sem rotulo no catalogo pt-BR:\n" + string.Join("\n", faltando));
    }

    [Fact]
    public void ACliAceitaAsColunasNovasPeloNomeDeExportacao()
    {
        Assert.True(ProgramColumns.TryParse(new[] { "Program", "UninstallerKind", "ProductCode" },
            out var colunas, out var desconhecida));

        Assert.Equal(string.Empty, desconhecida);
        Assert.Equal(3, colunas!.Count);
        Assert.Contains(ProgramColumn.UninstallerKind, colunas);
        Assert.Contains(ProgramColumn.ProductCode, colunas);
    }

    [Fact]
    public void OConjuntoPadraoDaExportacaoNaoMudou()
    {
        // O formato do arquivo exportado e fixo: as colunas novas existem
        // para o usuario escolher, nao para mudar o exportado padrao.
        Assert.DoesNotContain(ProgramColumn.UninstallerKind, Exporters.DefaultColumns);
        Assert.DoesNotContain(ProgramColumn.ProductCode, Exporters.DefaultColumns);
    }
}
