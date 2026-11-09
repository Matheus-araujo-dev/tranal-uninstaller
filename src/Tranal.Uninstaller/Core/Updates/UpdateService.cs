using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tranal.Uninstaller.Core.Settings;

namespace Tranal.Uninstaller.Core.Updates;

/// <summary>
/// O ponto unico das atualizacoes do Tranal.
///
/// A REGRA mora aqui: onde esta o feed, qual e a versao do aplicativo e como
/// o atualizador de programas e montado. A GUI, a CLI e o teste consomem
/// este servico e nunca montam feed nem comparam versao por conta propria.
///
/// Sao dois assuntos diferentes e por isso dois enderecos:
///
///   - o feed do TRANAL (a propria versao do aplicativo), lido pelo
///     <see cref="AutoUpdateClient"/>;
///   - o feed de PROGRAMAS (as versoes mais novas publicadas pelos
///     fabricantes), lido pelo <see cref="SoftwareUpdater"/>, que cruza o feed
///     com o que esta instalado na maquina.
///
/// O endereco padrao do feed do Tranal e a API de releases do GitHub, que ja
/// fala o formato que o <see cref="AutoUpdateClient"/> entende
/// ("tag_name" e "browser_download_url").
/// </summary>
public static class UpdateService
{
    /// <summary>
    /// Feed padrao da versao do proprio Tranal.
    ///
    /// E o arquivo "feed/tranal.json" do repositorio, e nao a API de
    /// releases do GitHub: a release "latest" e uma tag movel (o nome dela
    /// e "latest", sem numero), e uma tag sem numero nao diz qual versao
    /// foi publicada. O arquivo e reescrito pela publicacao com a versao
    /// real e o endereco do instalador.
    /// </summary>
    public const string FeedDoTranalPadrao =
        "https://raw.githubusercontent.com/Matheus-araujo-dev/tranal-uninstaller/main/feed/tranal.json";

    /// <summary>
    /// Feed padrao das atualizacoes dos programas instalados.
    /// </summary>
    public const string FeedDeProgramasPadrao =
        "https://raw.githubusercontent.com/Matheus-araujo-dev/tranal-uninstaller/main/feed/programas.json";

    /// <summary>
    /// O endereco do feed do Tranal: o que esta nas preferencias ou o padrao.
    /// </summary>
    public static string UrlDoTranal()
    {
        var configurado = AppSettingsStore.Current.UpdateFeedUrl;
        return string.IsNullOrWhiteSpace(configurado) ? FeedDoTranalPadrao : configurado.Trim();
    }

    /// <summary>
    /// O endereco do feed dos programas instalados: o que esta nas preferencias
    /// ou o padrao.
    /// </summary>
    public static string UrlDeProgramas()
    {
        var configurado = AppSettingsStore.Current.SoftwareUpdateFeedUrl;
        return string.IsNullOrWhiteSpace(configurado) ? FeedDeProgramasPadrao : configurado.Trim();
    }

    /// <summary>
    /// A versao do Tranal que esta rodando.
    /// </summary>
    public static string VersaoAtual()
    {
        var versao = typeof(UpdateService).Assembly.GetName().Version;
        return versao is null ? "0.0" : versao.ToString();
    }

    /// <summary>
    /// O provedor do feed do Tranal.
    /// </summary>
    public static IUpdateFeedProvider FeedDoTranal() => new HttpUpdateFeedProvider(UrlDoTranal());

    /// <summary>
    /// O provedor do feed dos programas instalados.
    /// </summary>
    public static IUpdateFeedProvider FeedDeProgramas() => new HttpUpdateFeedProvider(UrlDeProgramas());

    /// <summary>
    /// O atualizador dos programas instalados, ja com a lista negra das
    /// preferencias.
    /// </summary>
    public static SoftwareUpdater AtualizadorDeProgramas()
    {
        var listaNegra = AppSettingsStore.Current.UpdateBlacklist ?? new List<string>();
        return new SoftwareUpdater(FeedDeProgramas(), listaNegra);
    }

    /// <summary>
    /// Verifica se ha versao nova do proprio Tranal.
    /// </summary>
    /// <param name="ct">Token de cancelamento.</param>
    public static Task<UpdateCheckResult> VerificarTranalAsync(CancellationToken ct = default) =>
        AutoUpdateClient.CheckAsync(UrlDoTranal(), ct);

    /// <summary>
    /// True quando a versao publicada e mais nova que a instalada.
    ///
    /// A comparacao e a de <see cref="VersionComparer"/> — o mesmo metodo que
    /// o <see cref="SoftwareUpdater"/> usa para montar o plano, para o Tranal
    /// nao oferecer atualizacao que ele proprio nao faria.
    /// </summary>
    /// <param name="versaoInstalada">A versao que esta rodando.</param>
    /// <param name="versaoPublicada">A versao que o feed publicou.</param>
    public static bool TemVersaoNova(string? versaoInstalada, string? versaoPublicada) =>
        VersionComparer.IsNewer(versaoPublicada, versaoInstalada);

    /// <summary>
    /// O caminho onde o pacote de atualizacao e baixado: a pasta temporaria
    /// do Tranal, com o nome do arquivo tirado do endereco.
    ///
    /// A REGRA do nome mora aqui para a GUI e a CLI escolherem o mesmo
    /// caminho — um pacote baixado duas vezes, em dois lugares, e um
    /// arquivo orfao no disco do usuario.
    /// </summary>
    /// <param name="url">O endereco do pacote.</param>
    public static string CaminhoDoPacote(string? url)
    {
        var nome = "tranal-atualizacao.exe";

        try
        {
            var parte = (url ?? string.Empty).Split('?')[0];
            var ultimo = parte.Split('/').LastOrDefault(s => s.Length > 0);

            // So o ultimo segmento com extensao e um arquivo: um endereco
            // sem nome de arquivo ("https://exemplo/") fica com o nome
            // padrao.
            if (!string.IsNullOrWhiteSpace(ultimo) && ultimo.Contains('.')) nome = ultimo;
        }
        catch
        {
            // Endereco invalido: o nome padrao serve.
        }

        var pasta = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Tranal");
        return System.IO.Path.Combine(pasta, nome);
    }
}
