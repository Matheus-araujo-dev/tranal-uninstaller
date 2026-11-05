using System.IO;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Topmost;
using Tranal.Uninstaller.Core.Tools;
using Tranal.Uninstaller.Core.Updates;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Cobertura das ferramentas que entraram na rodada das funcionalidades do
/// Revo Pro: Otimizador de Registro, Modo Portatil, menu de contexto do
/// Explorer, janela principal flutuante e cliente de atualizacao.
///
/// Os testes que tocam o registro usam chaves descartaveis em
/// <c>HKCU\Software\Tranal\Testes</c> e as apagam no fim.
///
/// Na colecao "backups" porque a remocao do Otimizador de Registro cria um
/// ponto de restauracao: sem redirecionar o diretorio de backups, o teste
/// escreveria no ambiente real do usuario.
/// </summary>
[Collection("backups")]
public class ToolsTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private const string TestRoot = @"Software\Tranal\Testes\Tools";

    public ToolsTests()
    {
        // Os pontos de restauracao do teste ficam num diretorio temporario.
        _backupRoot = Path.Combine(_temp.Root, "backups");
        Directory.CreateDirectory(_backupRoot);
        BackupManager.RootDirectoryOverride = _backupRoot;
    }

    private readonly string _backupRoot;

    public void Dispose()
    {
        BackupManager.RootDirectoryOverride = null;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Tranal\Testes", writable: true);
            key?.DeleteSubKeyTree("Tools", throwOnMissingSubKey: false);
        }
        catch { /* chave de teste ja removida */ }

        _temp.Dispose();
    }

    // ---------------------------------------------------------------
    // Otimizador de Registro
    // ---------------------------------------------------------------

    [Fact]
    public void OtimizadorRemoveAChaveObsoleta()
    {
        var path = CreateTestKey("OtimizadorRemove");

        var report = RegistryOptimizer.Remove(new[]
        {
            new RegistryOptimizerEntry
            {
                Path = path,
                DisplayName = "Programa removido",
                Detail = "Alvo inexistente",
            },
        });

        Assert.Equal(1, report.RemovedCount);
        Assert.False(RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default)
            .OpenSubKey(TestRoot + @"\OtimizadorRemove") is not null);
    }

    [Fact]
    public void OtimizadorNaoRemoveChaveProtegida()
    {
        // O ultimo segmento ("Policies") e um nome protegido: a chave tem de
        // sobreviver mesmo existindo de verdade.
        var path = CreateTestKey(@"Protegida\Policies");

        var report = RegistryOptimizer.Remove(new[]
        {
            new RegistryOptimizerEntry { Path = path, DisplayName = "Protegida" },
        });

        Assert.Equal(0, report.RemovedCount);

        using var key = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
        Assert.NotNull(key.OpenSubKey(TestRoot + @"\Protegida\Policies"));
    }

    [Fact]
    public void OtimizadorIgnoraEntradaSemCaminho()
    {
        var report = RegistryOptimizer.Remove(new[]
        {
            new RegistryOptimizerEntry { Path = "   ", DisplayName = "Sem caminho" },
            new RegistryOptimizerEntry { Path = string.Empty, DisplayName = "Vazio" },
        });

        Assert.Equal(0, report.Total);
    }

    [Fact]
    public void VarreduraDoMenuIniciarNaoLanca()
    {
        // Le o Menu Iniciar real do usuario: o que importa e que a varredura
        // devolve uma lista sem estourar.
        var entries = RegistryOptimizer.ScanStartMenu();

        Assert.NotNull(entries);
        foreach (var entry in entries)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Path));
            Assert.True(entry.IsRegistryKey == entry.Path.StartsWith("HKEY_", StringComparison.OrdinalIgnoreCase));
        }
    }

    // ---------------------------------------------------------------
    // Modo portavel
    // ---------------------------------------------------------------

    [Fact]
    public void PastaConfiguradaValidaEhUsada()
    {
        var folder = _temp.CreateSubFolder("portatil");

        var resolved = PortableMode.ResolveDataDirectory(folder);

        Assert.Equal(PathUtil.Normalize(folder), resolved);
        Assert.True(PortableMode.IsPortable(folder));
    }

    [Fact]
    public void PastaConfiguradaInacessivelCaiParaAPadrao()
    {
        // Um arquivo no lugar da pasta: a pasta configurada nao pode ser criada.
        var blocker = _temp.CreateFile("bloqueio.txt", "x");

        var ok = PortableMode.TryResolveDataDirectory(blocker, out var directory, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Equal(PortableMode.DefaultDataDirectory, directory);
        Assert.False(PortableMode.IsPortable(blocker));
    }

    [Fact]
    public void PastaVaziaUsaAPadrao()
    {
        var ok = PortableMode.TryResolveDataDirectory("   ", out var directory, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(PortableMode.DefaultDataDirectory, directory);
        Assert.False(PortableMode.IsPortable(string.Empty));
    }

    [Fact]
    public void ResumoDoModoPortavelMostraOFallback()
    {
        var blocker = _temp.CreateFile("bloqueio2.txt", "x");

        var resumo = PortableMode.DescribePortableState(blocker);

        Assert.Contains("NÃO", resumo, StringComparison.Ordinal);
        Assert.Contains(PortableMode.DefaultDataDirectory, resumo, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------
    // Menu de contexto do Explorer
    // ---------------------------------------------------------------

    [Fact]
    public void ArgumentosDoMenuDeContextoUsamOAlvoEntreAspas()
    {
        var arguments = ExplorerContextMenu.BuildArguments(@"C:\Program Files\App\app.exe");

        Assert.Equal(2, arguments.Length);
        Assert.Equal(ExplorerContextMenu.TargetArgument, arguments[0]);
        Assert.Equal(@"""C:\Program Files\App\app.exe""", arguments[1]);
    }

    [Fact]
    public void ArgumentosDoMenuDeContextoUsamOMarcadorSemAlvo()
    {
        var arguments = ExplorerContextMenu.BuildArguments("   ");

        Assert.Equal(@"""%1""", arguments[1]);
    }

    [Fact]
    public void ScriptDeRegistroTemAsDuasChaves()
    {
        var script = ExplorerContextMenu.BuildRegistryScript(@"C:\Tranal\TranalUninstaller.exe");

        Assert.Contains("[HKEY_CURRENT_USER\\" + ExplorerContextMenu.ShellKeyRelative + "]", script);
        Assert.Contains("[HKEY_CURRENT_USER\\" + ExplorerContextMenu.CommandKeyRelative + "]", script);
        Assert.Contains(ExplorerContextMenu.TargetArgument, script);
        Assert.Contains(@"""MUIVerb""", script);
    }

    [Fact]
    public void RegistroDoMenuDeContextoUsaSomenteHKCU()
    {
        // O nucleo roda elevado: escrever em HKLM seria mexer no sistema.
        Assert.Contains(@"HKEY_CURRENT_USER", ExplorerContextMenu.ShellKeyRelative.Replace("Software", "HKEY_CURRENT_USER\\Software"));
        Assert.StartsWith(@"Software\Classes", ExplorerContextMenu.ShellKeyRelative, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalDeInstalacaoDeArquivoInexistenteEVazio()
    {
        Assert.Equal(string.Empty, ExplorerContextMenu.GetInstallLocation(@"C:\nao-existe\app.exe"));
        Assert.False(ExplorerContextMenu.IsInstalled(@"C:\nao-existe\app.exe"));
    }

    // ---------------------------------------------------------------
    // Janela principal flutuante
    // ---------------------------------------------------------------

    [Fact]
    public void PastaArrastadaNaoEAlvoValido()
    {
        var folder = _temp.CreateSubFolder("arrastada");

        var target = TopmostTargetResolver.ResolveFromFiles(new[] { folder });

        Assert.False(target.Resolved);
        Assert.False(string.IsNullOrWhiteSpace(target.Message));
    }

    [Fact]
    public void MaisDeUmArquivoNaoEValido()
    {
        var a = _temp.CreateFile("a.exe", "a");
        var b = _temp.CreateFile("b.exe", "b");

        var target = TopmostTargetResolver.ResolveFromFiles(new[] { a, b });

        Assert.False(target.Resolved);
    }

    [Fact]
    public void ArquivoInexistenteNaoResolve()
    {
        var target = TopmostTargetResolver.ResolveFromFiles(new[] { Path.Combine(_temp.Root, "nao-existe.exe") });

        Assert.False(target.Resolved);
    }

    [Fact]
    public void AlvoNaoResolvidoNaoTemAcoes()
    {
        Assert.Empty(TopmostTargetResolver.AvailableActions(null));
        Assert.Empty(TopmostTargetResolver.AvailableActions(new TopmostTarget { Resolved = false }));
    }

    [Fact]
    public void AlvoResolvidoSempreOfereceInformacoes()
    {
        var file = _temp.CreateFile("app.exe", "x");

        var target = TopmostTargetResolver.ResolveFromFile(file);
        var actions = TopmostTargetResolver.AvailableActions(target);

        Assert.True(target.Resolved);
        Assert.Contains(TopmostAction.Info, actions);
        Assert.Contains(TopmostAction.OpenFolder, actions);
    }

    // ---------------------------------------------------------------
    // Cliente de atualizacao
    // ---------------------------------------------------------------

    [Fact]
    public void FeedEmJsonEInterpretado()
    {
        var result = AutoUpdateClient.ParseFeed(
            """{"latestVersion":"2.0.0","downloadUrl":"https://exemplo/app.zip","releaseNotes":"notas"}""");

        Assert.True(result.Success);
        Assert.Equal("2.0.0", result.LatestVersion);
        Assert.Equal("https://exemplo/app.zip", result.DownloadUrl);
        Assert.Equal("notas", result.ReleaseNotes);
    }

    [Fact]
    public void FeedEmJsonAceitaVariosCamposDeVersao()
    {
        Assert.Equal("1.2.3", AutoUpdateClient.ParseFeed("""{"tag_name":"1.2.3"}""").LatestVersion);
        Assert.Equal("1.2.3", AutoUpdateClient.ParseFeed("""{"version":"1.2.3"}""").LatestVersion);
        Assert.Equal("1.2.3", AutoUpdateClient.ParseFeed("""{"latest_version":"1.2.3"}""").LatestVersion);
    }

    [Fact]
    public void FeedEmListaUsaOPrimeiroItem()
    {
        var result = AutoUpdateClient.ParseFeed("""[{"version":"9.9.9"},{"version":"1.0.0"}]""");

        Assert.True(result.Success);
        Assert.Equal("9.9.9", result.LatestVersion);
    }

    [Fact]
    public void FeedEmTextoSimplesEInterpretado()
    {
        var result = AutoUpdateClient.ParseFeed("3.1.4\nmais detalhes");

        Assert.True(result.Success);
        Assert.Equal("3.1.4", result.LatestVersion);
    }

    [Fact]
    public void FeedVazioOuInvalidoFalha()
    {
        Assert.False(AutoUpdateClient.ParseFeed("   ").Success);
        Assert.False(AutoUpdateClient.ParseFeed("{ isso nao e json }").Success);
        Assert.False(AutoUpdateClient.ParseFeed("""{"outroCampo":"valor"}""").Success);
    }

    [Fact]
    public async Task VerificacaoComEnderecoVazioFalha()
    {
        var result = await AutoUpdateClient.CheckAsync("   ", CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task DownloadComEnderecoVazioFalha()
    {
        var ok = await AutoUpdateClient.DownloadAsync(
            string.Empty, Path.Combine(_temp.Root, "saida.zip"), null, CancellationToken.None);

        Assert.False(ok);
    }

    // ---------------------------------------------------------------
    // Busca na web (item "Search at %s for" do menu de contexto)
    // ---------------------------------------------------------------

    [Fact]
    public void BuscaNaWebUsaOMecanismoEscolhido()
    {
        Assert.Contains("google.com", WebSearch.BuildUrl("teste"));
        Assert.Contains("bing.com", WebSearch.BuildUrl("teste", "Bing"));
        Assert.Contains("duckduckgo.com", WebSearch.BuildUrl("teste", "DuckDuckGo"));
        Assert.Contains("yahoo.com", WebSearch.BuildUrl("teste", "Yahoo"));
    }

    [Fact]
    public void BuscaNaWebEscapaOTermo()
    {
        var url = WebSearch.BuildUrl("a b&c");

        Assert.DoesNotContain(" ", url);
        Assert.Contains("%26", url);
    }

    [Fact]
    public void BuscaNaWebSemTermoNaoMontaEndereco()
    {
        Assert.Equal(string.Empty, WebSearch.BuildUrl("   "));
        Assert.Equal(string.Empty, WebSearch.BuildUrl(""));
    }

    // ---------------------------------------------------------------
    // Auxiliares
    // ---------------------------------------------------------------

    /// <summary>Cria uma chave de registro descartavel e devolve o caminho canonico.</summary>
    private static string CreateTestKey(string relative)
    {
        using var root = Registry.CurrentUser.CreateSubKey(TestRoot);
        using var key = root.CreateSubKey(relative);
        key.SetValue("Marca", "tranal");

        return @"HKEY_CURRENT_USER\" + TestRoot + @"\" + relative;
    }
}
