using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Updates;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// As atualizacoes: a comparacao de versoes, o plano de atualizacao dos
/// programas (com lista negra), a leitura do feed e a verificacao da versao
/// do proprio Tranal.
///
/// A REGRA e que a comparacao de versoes mora em um lugar so
/// (<see cref="VersionComparer"/>) e o plano em outro
/// (<see cref="SoftwareUpdater"/>). Estes testes medem as duas.
/// </summary>
public class UpdateFeatureTests
{
    // ------------------------------------------------------------------
    // Comparacao de versoes
    // ------------------------------------------------------------------

    [Fact]
    public void VersaoComparaPorNumeroEOTextoNaoDecide()
    {
        Assert.True(VersionComparer.IsNewer("2.0", "10.0") is false);
        Assert.True(VersionComparer.IsNewer("10.0", "2.0"));
        Assert.True(VersionComparer.IsNewer("1.0.1", "1.0"));
        Assert.False(VersionComparer.IsNewer("1.0", "1.0"));
    }

    [Fact]
    public void VersaoSemNumeroNaoViraAtualizacao()
    {
        // Sem informacao suficiente nao se oferecera atualizacao: seria
        // pior do que nao oferecer. E o que o proprio comparador promete
        // no comentario dele.
        Assert.False(VersionComparer.IsNewer("desconhecida", "1.0"));
        Assert.False(VersionComparer.IsNewer("1.0", string.Empty));
        Assert.False(VersionComparer.IsNewer(null, null));
        Assert.False(VersionComparer.IsNewer("1.0", null));
        Assert.Equal(0, VersionComparer.Compare("1.0", "desconhecida"));
    }

    [Fact]
    public void PreLancamentoEMenorQueAVersaoFinal()
    {
        Assert.True(VersionComparer.IsNewer("1.0", "1.0-beta"));
        Assert.False(VersionComparer.IsNewer("1.0-beta", "1.0"));
        Assert.Equal(0, VersionComparer.Compare("v1.2.3", "1.2.3"));
        Assert.Equal(0, VersionComparer.Compare("1.0", "1.0.0"));
    }

    // ------------------------------------------------------------------
    // Plano de atualizacao dos programas
    // ------------------------------------------------------------------

    private static InstalledProgram Programa(string nome, string versao, string editor = "Fabricante") =>
        new()
        {
            DisplayName = nome,
            DisplayVersion = versao,
            Publisher = editor,
        };

    [Fact]
    public void PlanoSoAceitaVersaoMaisNova()
    {
        var atualizador = new SoftwareUpdater(new FeedVazio());

        var plano = atualizador.BuildPlan(
            new[] { Programa("Alpha", "1.0"), Programa("Beta", "2.0") },
            new[]
            {
                new UpdateFeedEntry("alpha", "Alpha", "Fabricante", "1.5", "https://exemplo/alpha", ""),
                new UpdateFeedEntry("beta", "Beta", "Fabricante", "2.0", "https://exemplo/beta", ""),
                new UpdateFeedEntry("gamma", "Gamma", "Fabricante", "3.0", "https://exemplo/gamma", ""),
            });

        Assert.Single(plano.Items);
        Assert.Equal("Alpha", plano.Items[0].Candidate.ProgramName);
        Assert.Equal("1.5", plano.Items[0].Candidate.AvailableVersion);
        Assert.Equal("1.0", plano.Items[0].Candidate.InstalledVersion);
        Assert.True(plano.Items[0].Pending);
    }

    [Fact]
    public void ListaNegraBarraAAtualizacaoEExplicaOMotivo()
    {
        var atualizador = new SoftwareUpdater(new FeedVazio(), new[] { "alpha" });

        var plano = atualizador.BuildPlan(
            new[] { Programa("Alpha", "1.0"), Programa("Beta", "1.0") },
            new[]
            {
                new UpdateFeedEntry("alpha", "Alpha", "Fabricante", "2.0", "https://exemplo/alpha", ""),
                new UpdateFeedEntry("beta", "Beta", "Fabricante", "2.0", "https://exemplo/beta", ""),
            });

        Assert.Equal(2, plano.Items.Count);
        Assert.Single(plano.Pending);
        Assert.Equal("Beta", plano.Pending[0].ProgramName);

        var barrada = Assert.Single(plano.Skipped);
        Assert.Equal("Alpha", barrada.Candidate.ProgramName);
        Assert.Contains("alpha", barrada.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ListaNegraTambemCasaPeloNomeDoPrograma()
    {
        var atualizador = new SoftwareUpdater(new FeedVazio(), new[] { "Alpha" });

        var plano = atualizador.BuildPlan(
            new[] { Programa("Alpha", "1.0") },
            new[] { new UpdateFeedEntry("chave-1", "Alpha", "Fabricante", "2.0", "https://exemplo/alpha", "") });

        Assert.Single(plano.Skipped);
        Assert.Empty(plano.Pending);
    }

    [Fact]
    public void ProgramaQueNaoEstaInstaladoNaoEntraNoPlano()
    {
        var atualizador = new SoftwareUpdater(new FeedVazio());

        var plano = atualizador.BuildPlan(
            new[] { Programa("Alpha", "1.0") },
            new[] { new UpdateFeedEntry("outro", "Outro Programa", "Fabricante", "2.0", "https://exemplo/outro", "") });

        Assert.Empty(plano.Items);
    }

    [Fact]
    public async Task ExecucaoDoPlanoRelataOResultadoDeCadaItem()
    {
        var atualizador = new SoftwareUpdater(new FeedVazio(), opener: new AbridorFalso());

        var plano = atualizador.BuildPlan(
            new[] { Programa("Alpha", "1.0"), Programa("Beta", "1.0") },
            new[]
            {
                new UpdateFeedEntry("alpha", "Alpha", "Fabricante", "2.0", "https://exemplo/alpha", ""),
                new UpdateFeedEntry("beta", "Beta", "Fabricante", "2.0", string.Empty, ""),
            });

        var resultados = await atualizador.ExecutarAsync(plano);

        Assert.Equal(2, resultados.Count);
        Assert.True(resultados[0].Success);
        Assert.False(resultados[1].Success);
        Assert.Contains("endereco", resultados[1].Reason, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // Leitura do feed
    // ------------------------------------------------------------------

    [Fact]
    public void FeedJsonInvalidoDevolveListaVazia()
    {
        Assert.Empty(UpdateFeedReader.Parse("isto nao e json"));
        Assert.Empty(UpdateFeedReader.Parse("{ }"));
        Assert.Empty(UpdateFeedReader.Parse(null));
        Assert.Empty(UpdateFeedReader.Parse(string.Empty));
    }

    [Fact]
    public void FeedJsonValidoDevolveAsEntradas()
    {
        var entradas = UpdateFeedReader.Parse(
            "{ \"atualizacoes\": [ { \"chave\": \"a\", \"programa\": \"Alpha\", \"editor\": \"F\", \"versao\": \"2.0\", \"url\": \"https://x/a\", \"notas\": \"n\" } ] }");

        var entrada = Assert.Single(entradas);
        Assert.Equal("a", entrada.Key);
        Assert.Equal("Alpha", entrada.ProgramName);
        Assert.Equal("2.0", entrada.Version);
        Assert.Equal("https://x/a", entrada.Url);
    }

    [Fact]
    public async Task FeedLocalEmDiscoELeido()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "tranal-teste-feed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        var arquivo = Path.Combine(pasta, "programas.json");

        try
        {
            await File.WriteAllTextAsync(arquivo,
                "{ \"atualizacoes\": [ { \"programa\": \"Alpha\", \"versao\": \"2.0\" } ] }");

            var entradas = await new LocalUpdateFeedProvider(arquivo).ObterAsync(CancellationToken.None);

            Assert.Single(entradas);
            Assert.Equal("Alpha", entradas[0].ProgramName);

            // Arquivo que nao existe: lista vazia, nunca excecao.
            var inexistente = await new LocalUpdateFeedProvider(Path.Combine(pasta, "nao-existe.json"))
                .ObterAsync(CancellationToken.None);
            Assert.Empty(inexistente);
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }

    // ------------------------------------------------------------------
    // A versao do proprio Tranal
    // ------------------------------------------------------------------

    [Fact]
    public void FeedDoTranalAceitaJsonETextoSimples()
    {
        var json = AutoUpdateClient.ParseFeed("{ \"tag_name\": \"v1.5.0\", \"browser_download_url\": \"https://x/p.msi\" }");
        Assert.True(json.Success);
        Assert.Equal("v1.5.0", json.LatestVersion);
        Assert.Equal("https://x/p.msi", json.DownloadUrl);

        var texto = AutoUpdateClient.ParseFeed("2.0.0");
        Assert.True(texto.Success);
        Assert.Equal("2.0.0", texto.LatestVersion);
        Assert.Null(texto.DownloadUrl);

        Assert.False(AutoUpdateClient.ParseFeed(string.Empty).Success);
        Assert.False(AutoUpdateClient.ParseFeed("{ \"nada\": 1 }").Success);
    }

    [Fact]
    public void ServicoDeAtualizacaoRespeitaAPreferenciaDoFeed()
    {
        Assert.Equal(UpdateService.FeedDoTranalPadrao, UpdateService.UrlDoTranal());
        Assert.Equal(UpdateService.FeedDeProgramasPadrao, UpdateService.UrlDeProgramas());

        Assert.True(UpdateService.TemVersaoNova("1.0.0", "1.1.0"));
        Assert.False(UpdateService.TemVersaoNova("1.1.0", "1.0.0"));
        Assert.False(UpdateService.TemVersaoNova("1.0.0", "1.0.0"));
        Assert.False(string.IsNullOrWhiteSpace(UpdateService.VersaoAtual()));
    }

    [Fact]
    public void OEnderecoDoPacoteSaiDoEndereco()
    {
        var caminho = UpdateService.CaminhoDoPacote("https://exemplo/download/tranal-1.5.0.exe?token=1");

        Assert.EndsWith("tranal-1.5.0.exe", caminho, StringComparison.Ordinal);
        Assert.Contains("Tranal", caminho, StringComparison.OrdinalIgnoreCase);

        // Endereco sem arquivo: o nome padrao serve.
        Assert.EndsWith("tranal-atualizacao.exe", UpdateService.CaminhoDoPacote("https://exemplo/"),
            StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Apoio
    // ------------------------------------------------------------------

    /// <summary>Um feed sem entradas: o teste monta o plano na mao.</summary>
    private sealed class FeedVazio : IUpdateFeedProvider
    {
        public Task<IReadOnlyList<UpdateFeedEntry>> ObterAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<UpdateFeedEntry>>(Array.Empty<UpdateFeedEntry>());
    }

    /// <summary>Um abridor que nao abre nada: o teste confere o resultado.</summary>
    private sealed class AbridorFalso : IUpdateOpener
    {
        public Task<bool> OpenAsync(string url, CancellationToken ct) =>
            Task.FromResult(!string.IsNullOrWhiteSpace(url));
    }
}
