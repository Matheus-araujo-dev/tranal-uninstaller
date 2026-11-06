using System.IO;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Cobertura do Lote E: descoberta ampliada (produtos MSI, recursos e
/// atualizacoes do Windows, gerenciadores de pacotes, varredura de drives e
/// limpeza de pastas vazias) e verificacao de assinatura do desinstalador.
///
/// Os testes nao tocam a maquina: tudo vem de pasta temporaria. A unica
/// excecao e a chave <c>HKCU\Software\Tranal\Testes\Discovery</c>, criada e
/// apagada no mesmo teste — a mesma convencao que a suite ja usa para as
/// chaves de registro de teste.
/// </summary>
public class DiscoveryTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private const string TestRoot = @"Software\Tranal\Testes\Discovery";

    /// <summary>
    /// Apaga a chave de registro de teste e a pasta temporaria.
    /// </summary>
    public void Dispose()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Tranal\Testes", writable: true);
            key?.DeleteSubKeyTree("Discovery", throwOnMissingSubKey: false);
        }
        catch { /* a chave de teste ja foi removida */ }

        _temp.Dispose();
    }

    // ---------------------------------------------------------------
    // #8 — Desinstalar via MsiExec (GUID)
    // ---------------------------------------------------------------

    [Fact]
    public void OCatalogoMsiMontaOComandoDeDesinstalacaoPeloGuid()
    {
        var code = "{A1B2C3D4-E5F6-4789-ABCD-EF0123456789}";

        var silencioso = MsiProductCatalog.BuildUninstallCommand(code, silent: true);
        var normal = MsiProductCatalog.BuildUninstallCommand(code);

        Assert.Equal($"msiexec.exe /x {code} /qn", silencioso);
        Assert.Equal($"msiexec.exe /x {code}", normal);
    }

    [Fact]
    public void OCatalogoMsiRecusaUmGuidInvalidoComMensagem()
    {
        var ok = MsiProductCatalog.TryBuildUninstallCommand(
            "nao-e-um-guid", silent: false, out var comando, out var erro);

        Assert.False(ok);
        Assert.Equal(string.Empty, comando);
        Assert.Contains("GUID", erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OCatalogoMsiLeOsProdutosDeUmaChaveDeRegistroSintetica()
    {
        var root = TestRoot + @"\Uninstall";
        var code = "{A1B2C3D4-E5F6-4789-ABCD-EF0123456789}";

        using (var key = Registry.CurrentUser.CreateSubKey(root + @"\" + code))
        {
            Assert.NotNull(key);
            key!.SetValue("DisplayName", "Programa de Teste");
            key.SetValue("InstallLocation", @"C:\Program Files\Teste");
            key.SetValue("Publisher", "Tranal");
            key.SetValue("DisplayVersion", "1.2.3");
            key.SetValue("WindowsInstaller", 1);
        }

        var produtos = MsiProductCatalog.ListFromRegistry(RegistryHive.CurrentUser, root);

        Assert.Single(produtos);
        Assert.Equal("Programa de Teste", produtos[0].DisplayName);
        Assert.Equal(code, produtos[0].ProductCode);
        Assert.Equal(@"C:\Program Files\Teste", produtos[0].InstallLocation);
        Assert.Equal($"msiexec.exe /x {code} /qn", produtos[0].QuietUninstallCommand);
    }

    [Fact]
    public void OCatalogoMsiDevolveListaVaziaQuandoAChaveNaoExiste()
    {
        var produtos = MsiProductCatalog.ListFromRegistry(
            RegistryHive.CurrentUser, TestRoot + @"\Inexistente");

        Assert.Empty(produtos);
    }

    // ---------------------------------------------------------------
    // #12 — Steam, Chocolatey e Scoop
    // ---------------------------------------------------------------

    [Fact]
    public void OCatalogoDeGerenciadoresReconheceSteamChocolateyEScoop()
    {
        var pacotes = PackageManagerCatalog.Discover(new PackageManagerRoots
        {
            SteamPath = CriarArvoreDoSteam(),
            ChocolateyPath = CriarArvoreDoChocolatey(),
            ScoopPath = CriarArvoreDoScoop(),
        });

        Assert.Contains(pacotes, p => p.Manager == PackageManagerKind.Steam && p.Name == "Jogo de Teste");
        Assert.Contains(pacotes, p => p.Manager == PackageManagerKind.Chocolatey && p.Name == "pacote-teste");
        Assert.Contains(pacotes, p => p.Manager == PackageManagerKind.Scoop && p.Name == "app-teste");
    }

    [Fact]
    public void UmItemDeGerenciadorSemDesinstaladorProprioEMarcadoComoTal()
    {
        var pacotes = PackageManagerCatalog.Discover(new PackageManagerRoots
        {
            SteamPath = CriarArvoreDoSteam(),
        });

        var semDesinstalador = pacotes.First(p => p.Name == "Jogo de Teste");
        var comDesinstalador = pacotes.First(p => p.Name == "Jogo Com Desinstalador");

        Assert.False(semDesinstalador.HasOwnUninstaller);
        Assert.Equal(string.Empty, semDesinstalador.UninstallerPath);
        Assert.Contains("Steam", semDesinstalador.Note, StringComparison.OrdinalIgnoreCase);

        Assert.True(comDesinstalador.HasOwnUninstaller);
        Assert.EndsWith("uninstall.exe", comDesinstalador.UninstallerPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OChocolateyMarcaODesinstaladorPeloScriptDoPacote()
    {
        var pacotes = PackageManagerCatalog.Discover(new PackageManagerRoots
        {
            ChocolateyPath = CriarArvoreDoChocolatey(),
        });

        var comScript = pacotes.First(p => p.Name == "pacote-teste");
        var semScript = pacotes.First(p => p.Name == "pacote-sem-script");

        Assert.True(comScript.HasOwnUninstaller);
        Assert.EndsWith("chocolateyUninstall.ps1", comScript.UninstallerPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("2.0.0", comScript.Version);

        Assert.False(semScript.HasOwnUninstaller);
    }

    // ---------------------------------------------------------------
    // #11 — Windows Features e Windows Updates
    // ---------------------------------------------------------------

    [Fact]
    public void OCatalogoDeFeaturesDistingueInstaladoDeDisponivel()
    {
        var tabela = string.Join("\n", new[]
        {
            "Feature Name : State",
            "------------ : ---------",
            "Microsoft-Hyper-V-All : Disabled",
            "NetFx3 : Enabled",
            "Telnet : DisabledWithPayloadRemoved",
        });

        var recursos = WindowsFeatureCatalog.ParseDismTable(tabela);

        Assert.Equal(3, recursos.Count);

        var netFx3 = recursos.First(f => f.Name == "NetFx3");
        Assert.Equal(DiscoveryAvailability.Installed, netFx3.Availability);

        var hyperV = recursos.First(f => f.Name == "Microsoft-Hyper-V-All");
        Assert.Equal(DiscoveryAvailability.Available, hyperV.Availability);

        var telnet = recursos.First(f => f.Name == "Telnet");
        Assert.Equal(DiscoveryAvailability.Available, telnet.Availability);
    }

    [Fact]
    public void OCatalogoDeFeaturesDeclaraAOrigemDaInformacao()
    {
        var tabela = string.Join("\n", new[]
        {
            "NetFx3 : Enabled",
            "NetFx4AdvancedServices : Disabled",
        });

        var recursos = WindowsFeatureCatalog.ParseDismTable(tabela);

        Assert.All(recursos, r => Assert.Equal(DiscoveryOrigin.Measured, r.Origin));
    }

    [Fact]
    public void OCatalogoDeUpdatesDistingueInstaladoDeDisponivelEDeclaraAOrigem()
    {
        var fonte = new FonteDeAtualizacaoFalsa();

        var instaladas = WindowsUpdateCatalog.ListInstalled(fonte);
        var disponiveis = WindowsUpdateCatalog.ListAvailable(fonte);

        Assert.True(instaladas.SourceAvailable);
        Assert.Equal(DiscoveryOrigin.Measured, instaladas.Origin);
        Assert.Single(instaladas.Items);
        Assert.Equal(DiscoveryAvailability.Installed, instaladas.Items[0].Availability);

        Assert.Single(disponiveis.Items);
        Assert.Equal(DiscoveryAvailability.Available, disponiveis.Items[0].Availability);
    }

    // ---------------------------------------------------------------
    // #13 — Varredura de drives
    // ---------------------------------------------------------------

    [Fact]
    public void AVarreduraDeDrivesAchaDesinstaladorNaoRegistrado()
    {
        var drive = _temp.CreateSubFolder("drive");

        var registrado = Path.Combine(drive, "AppRegistrado");
        var orfao = Path.Combine(drive, "AppOrfao");
        Directory.CreateDirectory(registrado);
        Directory.CreateDirectory(orfao);

        var exeRegistrado = Path.Combine(registrado, "unins000.exe");
        var exeOrfao = Path.Combine(orfao, "unins000.exe");
        File.WriteAllText(exeRegistrado, "stub");
        File.WriteAllText(exeOrfao, "stub");

        var indice = RegisteredUninstallerIndex.From(new[]
        {
            new InstalledProgram
            {
                DisplayName = "App Registrado",
                InstallLocation = registrado,
                UninstallString = $"\"{exeRegistrado}\"",
            },
        });

        var resultado = DriveScanner.Scan(
            new DriveScanOptions { Roots = new[] { drive }, MaxDepth = 4 }, indice);

        Assert.Single(resultado.Found);
        Assert.Equal(exeOrfao, resultado.Found[0].FilePath);
    }

    [Fact]
    public void AVarreduraDeDrivesRespeitaOLimiteDeProfundidade()
    {
        var drive = _temp.CreateSubFolder("drive-profundo");

        var fundo = Path.Combine(drive, "nivel1", "nivel2", "nivel3");
        Directory.CreateDirectory(fundo);
        File.WriteAllText(Path.Combine(fundo, "unins000.exe"), "stub");

        var raso = DriveScanner.Scan(new DriveScanOptions { Roots = new[] { drive }, MaxDepth = 2 });
        var profundo = DriveScanner.Scan(new DriveScanOptions { Roots = new[] { drive }, MaxDepth = 4 });

        Assert.Empty(raso.Found);
        Assert.Single(profundo.Found);
    }

    [Fact]
    public void AVarreduraDeDrivesSoConsideraExecutaveisQueParecemDesinstalador()
    {
        var drive = _temp.CreateSubFolder("drive-nomes");

        var pasta = Path.Combine(drive, "App");
        Directory.CreateDirectory(pasta);
        File.WriteAllText(Path.Combine(pasta, "unins000.exe"), "stub");
        File.WriteAllText(Path.Combine(pasta, "programa.exe"), "stub");

        var resultado = DriveScanner.Scan(new DriveScanOptions { Roots = new[] { drive }, MaxDepth = 4 });

        Assert.Single(resultado.Found);
        Assert.Equal("unins000.exe", resultado.Found[0].FileName);
    }

    // ---------------------------------------------------------------
    // #14 — Limpeza de pastas vazias
    // ---------------------------------------------------------------

    [Fact]
    public void ALimpezaNaoRemoveAPropriaProgramFiles()
    {
        var root = _temp.CreateSubFolder("Program Files");
        var vazia = Path.Combine(root, "AppVazio");
        Directory.CreateDirectory(vazia);

        var resultado = EmptyFolderCleaner.Clean(new[] { root });

        Assert.Contains(vazia, resultado.Removed);
        Assert.True(Directory.Exists(root));
        Assert.False(Directory.Exists(vazia));
    }

    [Fact]
    public void ALimpezaSoTocaOQueEstaSobPastaDeProgramas()
    {
        var root = _temp.CreateSubFolder("Program Files");
        var fora = _temp.CreateSubFolder("Outra Pasta");
        var dentro = Path.Combine(root, "AppVazio");

        Directory.CreateDirectory(dentro);
        Directory.CreateDirectory(fora);

        Assert.True(EmptyFolderCleaner.IsUnderProgramRoot(dentro, new[] { root }));
        Assert.False(EmptyFolderCleaner.IsUnderProgramRoot(fora, new[] { root }));

        var candidatos = EmptyFolderCleaner.Find(new[] { root });

        Assert.Contains(candidatos, c => c.Path == dentro);
        Assert.DoesNotContain(candidatos, c => c.Path == fora);
    }

    [Fact]
    public void ALimpezaIgnoraPastaQueAindaTemExecutavel()
    {
        var root = _temp.CreateSubFolder("Program Files");
        var comExecutavel = Path.Combine(root, "AppComExecutavel");

        Directory.CreateDirectory(comExecutavel);
        File.WriteAllText(Path.Combine(comExecutavel, "app.exe"), "stub");

        var candidatos = EmptyFolderCleaner.Find(new[] { root });

        Assert.DoesNotContain(candidatos, c => c.Path == comExecutavel);
    }

    [Fact]
    public void ALimpezaReconhecePastaPelaMetadeComLogSo()
    {
        var root = _temp.CreateSubFolder("Program Files");
        var comLog = Path.Combine(root, "AppComLog");

        Directory.CreateDirectory(comLog);
        File.WriteAllText(Path.Combine(comLog, "instalacao.log"), "linha");

        var candidatos = EmptyFolderCleaner.Find(new[] { root });

        var candidato = Assert.Single(candidatos);
        Assert.False(candidato.IsEmpty);
        Assert.True(candidato.IsHalfEmpty);
    }

    // ---------------------------------------------------------------
    // #16 — Verificacao de certificado
    // ---------------------------------------------------------------

    [Fact]
    public void AVerificacaoDeCertificadoDevolveEstadoParaArquivoSemAssinatura()
    {
        var arquivo = _temp.CreateFile("sem-assinatura.txt", "isto nao e um executavel");

        var assinatura = UninstallerCertificate.Verify(arquivo);

        Assert.False(assinatura.IsSigned);
        Assert.Equal(SignatureTrustStatus.NotSigned, assinatura.Status);
        Assert.False(assinatura.IsTrusted);
    }

    [Fact]
    public void AVerificacaoDeCertificadoDevolveEstadoParaCertificadoInvalido()
    {
        var arquivo = Path.Combine(_temp.Root, "invalido.exe");
        File.WriteAllBytes(arquivo, new byte[] { 0x00, 0x01, 0x02, 0x03, 0xFF, 0xFE });

        var assinatura = UninstallerCertificate.Verify(arquivo);

        Assert.False(assinatura.IsSigned);
        Assert.NotEqual(SignatureTrustStatus.Valid, assinatura.Status);
    }

    [Fact]
    public void AVerificacaoDeCertificadoDevolveEstadoParaArquivoInexistente()
    {
        var assinatura = UninstallerCertificate.Verify(Path.Combine(_temp.Root, "nao-existe.exe"));

        Assert.False(assinatura.IsSigned);
        Assert.Equal(SignatureTrustStatus.NotSigned, assinatura.Status);
    }

    // ---------------------------------------------------------------
    // Arvores sinteticas
    // ---------------------------------------------------------------

    /// <summary>
    /// Monta uma Steam sintetica: biblioteca declarada no VDF (em outro
    /// disco) e dois jogos — um sem desinstalador e um com.
    /// </summary>
    private string CriarArvoreDoSteam()
    {
        var steam = _temp.CreateSubFolder("Steam");
        var steamapps = Path.Combine(steam, "steamapps");
        Directory.CreateDirectory(steamapps);

        File.WriteAllText(
            Path.Combine(steamapps, "libraryfolders.vdf"),
            "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"D:\\\\BibliotecaSteam\"\n\t}\n}\n");

        File.WriteAllText(
            Path.Combine(steamapps, "appmanifest_730.acf"),
            "\"AppState\"\n{\n\t\"appid\"\t\t\"730\"\n\t\"name\"\t\t\"Jogo de Teste\"\n\t\"installdir\"\t\t\"JogoTeste\"\n}\n");

        File.WriteAllText(
            Path.Combine(steamapps, "appmanifest_440.acf"),
            "\"AppState\"\n{\n\t\"appid\"\t\t\"440\"\n\t\"name\"\t\t\"Jogo Com Desinstalador\"\n\t\"installdir\"\t\t\"JogoComDesinstalador\"\n}\n");

        var jogo = Path.Combine(steamapps, "common", "JogoComDesinstalador");
        Directory.CreateDirectory(jogo);
        File.WriteAllText(Path.Combine(jogo, "uninstall.exe"), "stub");

        return steam;
    }

    /// <summary>
    /// Monta um Chocolatey sintetico: um pacote com script de desinstalacao e
    /// um sem.
    /// </summary>
    private string CriarArvoreDoChocolatey()
    {
        var chocolatey = _temp.CreateSubFolder("chocolatey");

        var pacote = Path.Combine(chocolatey, "lib", "pacote-teste");
        Directory.CreateDirectory(Path.Combine(pacote, "tools"));
        File.WriteAllText(
            Path.Combine(pacote, "pacote-teste.nuspec"),
            "<package><metadata><version>2.0.0</version></metadata></package>");
        File.WriteAllText(
            Path.Combine(pacote, "tools", "chocolateyUninstall.ps1"),
            "Write-Host 'desinstalando'");

        Directory.CreateDirectory(Path.Combine(chocolatey, "lib", "pacote-sem-script"));

        var bin = Path.Combine(chocolatey, "bin");
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, "pacote-teste.exe"), "stub");

        return chocolatey;
    }

    /// <summary>
    /// Monta um Scoop sintetico: um aplicativo com versao e shim.
    /// </summary>
    private string CriarArvoreDoScoop()
    {
        var scoop = _temp.CreateSubFolder("scoop");

        var app = Path.Combine(scoop, "apps", "app-teste", "1.2.3");
        Directory.CreateDirectory(app);
        File.WriteAllText(Path.Combine(app, "install.json"), "{}");

        var shims = Path.Combine(scoop, "shims");
        Directory.CreateDirectory(shims);
        File.WriteAllText(Path.Combine(shims, "app-teste.exe"), "stub");

        return scoop;
    }

    /// <summary>
    /// Fonte de atualizacoes falsa: permite provar a distincao entre
    /// instalado e disponivel sem depender do COM do Windows Update.
    /// </summary>
    private sealed class FonteDeAtualizacaoFalsa : IWindowsUpdateSource
    {
        public IReadOnlyList<WindowsUpdateRecord> Search(bool installedOnly) =>
            new List<WindowsUpdateRecord>
            {
                new()
                {
                    Title = installedOnly ? "Atualizacao instalada" : "Atualizacao disponivel",
                    KbArticles = "KB5031356",
                    Date = new DateTime(2026, 1, 15),
                },
            };
    }
}
