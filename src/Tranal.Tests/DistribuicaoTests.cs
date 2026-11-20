using System;
using System.IO;
using System.Text.Json;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Core.Updates;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O padrao de distribuicao dos apps Tranal (DEC-APPS-DISTRIBUICAO-001):
/// o atualizador escolhe a maior tag vX.Y.Z, roda o Setup em silencio e o
/// instalador fecha e reabre o app; o CI publica uma tag versionada a cada
/// push na main.
///
/// A parte pura (escolha da release, decisao do ciclo, migracao das
/// preferencias) e testada aqui sem rede. O .iss e o workflow sao conferidos
/// sobre o fonte: a suite nao roda o Inno Setup nem o GitHub Actions.
/// </summary>
public sealed class DistribuicaoTests
{
    private const string Releases = """
    [
      { "tag_name": "latest", "draft": false, "prerelease": false,
        "assets": [ { "name": "TranalUninstaller-Setup.exe", "size": 1, "browser_download_url": "https://x/latest/TranalUninstaller-Setup.exe" } ] },
      { "tag_name": "v1.4.9", "draft": false, "prerelease": false, "html_url": "https://x/v1.4.9",
        "assets": [ { "name": "TranalUninstaller-Setup.exe", "size": 100, "browser_download_url": "https://x/v1.4.9/TranalUninstaller-Setup.exe" } ] },
      { "tag_name": "v1.4.10", "draft": false, "prerelease": false, "html_url": "https://x/v1.4.10",
        "assets": [
          { "name": "tranal-cli.zip", "size": 5, "browser_download_url": "https://x/v1.4.10/tranal-cli.zip" },
          { "name": "TranalUninstaller-Setup.exe", "size": 64000000, "browser_download_url": "https://x/v1.4.10/TranalUninstaller-Setup.exe" } ] },
      { "tag_name": "v9.0.0", "draft": true, "prerelease": false, "assets": [] },
      { "tag_name": "v8.0.0", "draft": false, "prerelease": true, "assets": [] },
      { "tag_name": "v2.0-beta", "draft": false, "prerelease": false, "assets": [] }
    ]
    """;

    [Fact]
    public void AMaiorTagDeVersaoVenceEOResteEIgnorado()
    {
        using var doc = JsonDocument.Parse(Releases);
        var r = GitHubReleases.PickLatest(doc.RootElement, UpdateService.SetupAssetName);

        // 1.4.10 > 1.4.9 (comparacao numerica, nao de texto); "latest", o
        // rascunho, a pre-release e a tag que nao e vX.Y.Z ficam de fora.
        Assert.True(r.Success);
        Assert.Equal("1.4.10", r.LatestVersion);
        Assert.Equal("https://x/v1.4.10/TranalUninstaller-Setup.exe", r.DownloadUrl);
        Assert.Equal(64000000, r.ExpectedSize);
        Assert.Equal("https://x/v1.4.10", r.ReleaseNotes);
    }

    [Fact]
    public void SemTagVersionadaNaoHaVersao()
    {
        using var doc = JsonDocument.Parse("""[ { "tag_name": "latest", "assets": [] } ]""");
        Assert.False(GitHubReleases.PickLatest(doc.RootElement, UpdateService.SetupAssetName).Success);
    }

    [Fact]
    public void ReleaseSemOSetupNaoTemEnderecoDeDownload()
    {
        using var doc = JsonDocument.Parse("""[ { "tag_name": "v1.5.0", "assets": [ { "name": "outro.zip", "size": 3, "browser_download_url": "https://x/o.zip" } ] } ]""");
        var r = GitHubReleases.PickLatest(doc.RootElement, UpdateService.SetupAssetName);
        Assert.True(r.Success);
        Assert.Equal("1.5.0", r.LatestVersion);
        Assert.Null(r.DownloadUrl);
        Assert.Null(r.ExpectedSize);
    }

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("v10.0.0", "10.0.0")]
    [InlineData("latest", null)]
    [InlineData("1.2.3", null)]
    [InlineData("v1.2", null)]
    [InlineData("v1.2.3-rc1", null)]
    [InlineData("", null)]
    public void SoTagVXYZEVersao(string tag, string? esperada)
    {
        Assert.Equal(esperada, GitHubReleases.VersionOfTag(tag)?.ToString(3));
    }

    [Fact]
    public void OFeedGenericoUsaARegraDasReleasesQuandoRecebeAListaDoGitHub()
    {
        // Antes, uma lista JSON valia pelo primeiro item — na lista do GitHub
        // o primeiro item pode ser a release rolante "latest".
        var r = AutoUpdateClient.ParseFeed(Releases);
        Assert.Equal("1.4.10", r.LatestVersion);
        Assert.Equal(64000000, r.ExpectedSize);
    }

    [Fact]
    public void OFeedPadraoEAListaDeReleasesDoGitHub()
    {
        Assert.StartsWith("https://api.github.com/repos/Matheus-araujo-dev/tranal-uninstaller/releases",
            UpdateService.FeedDoTranalPadrao);
        Assert.Equal(TimeSpan.FromHours(1), UpdateService.IntervaloDeVerificacao);
    }

    [Fact]
    public void AInstalacaoSilenciosaUsaOsArgumentosDoPadrao()
    {
        Assert.Equal("/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS", UpdateInstaller.SilentArguments);
        Assert.False(UpdateInstaller.RunSilent(Path.Combine(Path.GetTempPath(), "nao-existe-" + Guid.NewGuid() + ".exe")).Started);
    }

    [Fact]
    public void ADecisaoDoCicloSegueAOrdemDoPadrao()
    {
        var nova = new UpdateCheckResult { Success = true, LatestVersion = "1.5.0", DownloadUrl = "https://x/s.exe", ExpectedSize = 10 };

        Assert.Equal(AutoUpdateOutcome.CheckFailed,
            UpdateService.Decidir(new UpdateCheckResult { Success = false }, "1.4.0.0", true).Outcome);
        Assert.Equal(AutoUpdateOutcome.UpToDate,
            UpdateService.Decidir(nova, "1.5.0.0", true).Outcome);
        Assert.Equal(AutoUpdateOutcome.UpToDate,
            UpdateService.Decidir(nova, "1.6.0.0", true).Outcome);
        Assert.Equal(AutoUpdateOutcome.NewVersionAvailable,
            UpdateService.Decidir(nova, "1.4.0.0", false).Outcome);
        Assert.Equal(AutoUpdateOutcome.NoInstaller,
            UpdateService.Decidir(new UpdateCheckResult { Success = true, LatestVersion = "1.5.0" }, "1.4.0.0", true).Outcome);

        var baixar = UpdateService.Decidir(nova, "1.4.0.0", true);
        Assert.Equal(AutoUpdateOutcome.ShouldDownload, baixar.Outcome);
        Assert.Equal("1.5.0", baixar.LatestVersion);
    }

    [Fact]
    public void PreferenciasAntigasSaoMigradasUmaVezEDepoisAEscolhaVale()
    {
        // Arquivo de antes do padrao: as duas desligadas (o padrao antigo).
        var antigo = JsonSerializer.Deserialize<AppSettings>("""{ "CheckForUpdateOnStartup": false, "UpdateAutomatically": false }""")!;
        var migrado = AppSettingsStore.Normalize(antigo);
        Assert.True(migrado.CheckForUpdateOnStartup);
        Assert.True(migrado.UpdateAutomatically);
        Assert.Equal(AppSettingsStore.UpdatePolicyCurrentRevision, migrado.UpdatePolicyRevision);

        // Depois da migracao, o usuario desliga e a escolha fica.
        var escolhido = JsonSerializer.Deserialize<AppSettings>(
            """{ "CheckForUpdateOnStartup": false, "UpdateAutomatically": false, "UpdatePolicyRevision": 1 }""")!;
        var normalizado = AppSettingsStore.Normalize(escolhido);
        Assert.False(normalizado.CheckForUpdateOnStartup);
        Assert.False(normalizado.UpdateAutomatically);

        // Preferencias novas ja nascem ligadas.
        Assert.True(new AppSettings().CheckForUpdateOnStartup);
        Assert.True(new AppSettings().UpdateAutomatically);
    }

    [Fact]
    public void OInstaladorFechaEReabreOAppEAVersaoVemDeMyAppVersion()
    {
        var iss = Ler("installer", "tranal.iss");

        Assert.Contains("CloseApplications=force", iss);
        Assert.Contains("AppId={{4F0C1D6E-2A83-4B57-9E14-7C8D5B2A9F30}", iss);
        Assert.Contains(@"DefaultDirName={autopf}\{#AppGroup}\{#AppName}", iss);
        Assert.Contains("#define AppGroup \"Tranal Group\"", iss);
        Assert.Contains("AppVersion={#MyAppVersion}", iss);
        Assert.Contains("OutputBaseFilename=TranalUninstaller-Setup", iss);
        Assert.Matches(@"Flags: nowait skipifnotsilent runascurrentuser", iss);
    }

    [Fact]
    public void OWorkflowPublicaUmaTagVersionadaACadaPushNaMain()
    {
        var yml = Ler(".github", "workflows", "publicar.yml");

        Assert.Contains("branches: [main]", yml);
        Assert.Contains("workflow_dispatch:", yml);
        Assert.Contains("options: [patch, minor, major]", yml);
        Assert.DoesNotContain("tags: ['v*']", yml);
        Assert.Contains("git ls-remote --tags origin", yml);
        Assert.Contains("-Version \"${{ steps.versao.outputs.numero }}\"", yml);
        Assert.Contains("gh release create $tag publish\\setup\\TranalUninstaller-Setup.exe", yml);
        Assert.Contains("--latest --title", yml);
    }

    private static string Ler(params string[] partes)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Tranal.Uninstaller.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var caminho = Path.Combine(dir!.FullName, Path.Combine(partes));
        return File.ReadAllText(caminho);
    }
}
