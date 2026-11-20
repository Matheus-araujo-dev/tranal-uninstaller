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
/// O endereco padrao do feed do Tranal e a API de releases do GitHub
/// (DEC-APPS-DISTRIBUICAO-001): a versao publicada e a maior tag
/// <c>vX.Y.Z</c> (<see cref="GitHubReleases"/>), e o instalador e o asset
/// <see cref="SetupAssetName"/> dessa release.
/// </summary>
public static class UpdateService
{
    /// <summary>
    /// Feed padrao da versao do proprio Tranal: a lista de releases do
    /// GitHub.
    ///
    /// A lista, e nao <c>/releases/latest</c>: a regra e a maior tag
    /// <c>vX.Y.Z</c>, e a release rolante "latest" (tag movel, sem numero)
    /// existe ao lado das versionadas. Cada push na main publica uma tag
    /// versionada (<c>.github/workflows/publicar.yml</c>).
    /// </summary>
    public const string FeedDoTranalPadrao =
        "https://api.github.com/repos/Matheus-araujo-dev/tranal-uninstaller/releases?per_page=30";

    /// <summary>
    /// O nome do instalador publicado em cada release
    /// (<c>OutputBaseFilename</c> do <c>installer/tranal.iss</c>).
    /// </summary>
    public const string SetupAssetName = "TranalUninstaller-Setup.exe";

    /// <summary>
    /// De quanto em quanto tempo o app aberto confere se ha versao nova
    /// (alem da conferencia ao abrir). A API do GitHub sem token aceita 60
    /// chamadas por hora: uma por hora fica longe do limite.
    /// </summary>
    public static readonly TimeSpan IntervaloDeVerificacao = TimeSpan.FromHours(1);

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
    /// O ciclo automatico de atualizacao: confere, baixa o Setup (conferindo
    /// o tamanho) e o roda em modo silencioso.
    ///
    /// A ordem das decisoes mora aqui, e nao na tela: a janela so chama e,
    /// quando o resultado e <see cref="AutoUpdateOutcome.Installing"/>, fecha
    /// o aplicativo para o instalador trocar os arquivos (ele o reabre no
    /// fim).
    /// </summary>
    /// <param name="podeInstalar">
    /// Pergunta, na hora de instalar, se o app pode fechar agora (nao pode no
    /// meio de uma desinstalacao). Quando devolve false, a instalacao fica
    /// para a proxima conferencia.
    /// </param>
    /// <param name="instalarAutomaticamente">
    /// A preferencia "Atualizar automaticamente". Desligada, o ciclo para na
    /// descoberta da versao nova.
    /// </param>
    /// <param name="ct">Token de cancelamento.</param>
    public static async Task<AutoUpdateResult> VerificarEInstalarAsync(
        Func<bool> podeInstalar, bool instalarAutomaticamente, CancellationToken ct = default)
    {
        var check = await VerificarTranalAsync(ct).ConfigureAwait(false);
        var decisao = Decidir(check, VersaoAtual(), instalarAutomaticamente);
        if (decisao.Outcome != AutoUpdateOutcome.ShouldDownload) return decisao;

        var destino = CaminhoDoPacote(check.DownloadUrl);
        var baixou = await AutoUpdateClient
            .DownloadAsync(check.DownloadUrl!, destino, null, ct, check.ExpectedSize)
            .ConfigureAwait(false);
        if (!baixou) return decisao with { Outcome = AutoUpdateOutcome.DownloadFailed };

        if (!podeInstalar()) return decisao with { Outcome = AutoUpdateOutcome.Postponed };

        var execucao = UpdateInstaller.RunSilent(destino);
        return decisao with
        {
            Outcome = execucao.Started ? AutoUpdateOutcome.Installing : AutoUpdateOutcome.InstallFailed,
            Detail = execucao.Detail,
        };
    }

    /// <summary>
    /// A parte pura do ciclo: o que fazer com o resultado da conferencia.
    /// Separada para ser testada sem rede.
    /// </summary>
    /// <param name="check">O que o feed devolveu.</param>
    /// <param name="versaoInstalada">A versao que esta rodando.</param>
    /// <param name="instalarAutomaticamente">A preferencia "Atualizar automaticamente".</param>
    public static AutoUpdateResult Decidir(UpdateCheckResult check, string? versaoInstalada, bool instalarAutomaticamente)
    {
        if (!check.Success)
            return new AutoUpdateResult(AutoUpdateOutcome.CheckFailed, null, check.ErrorMessage);

        if (!TemVersaoNova(versaoInstalada, check.LatestVersion))
            return new AutoUpdateResult(AutoUpdateOutcome.UpToDate, check.LatestVersion, null);

        if (!instalarAutomaticamente)
            return new AutoUpdateResult(AutoUpdateOutcome.NewVersionAvailable, check.LatestVersion, null);

        if (string.IsNullOrWhiteSpace(check.DownloadUrl))
            return new AutoUpdateResult(AutoUpdateOutcome.NoInstaller, check.LatestVersion,
                $"A release nao tem o {SetupAssetName}.");

        return new AutoUpdateResult(AutoUpdateOutcome.ShouldDownload, check.LatestVersion, null);
    }

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

/// <summary>
/// Em que ponto o ciclo automatico de atualizacao parou.
/// </summary>
public enum AutoUpdateOutcome
{
    /// <summary>Nao deu para conferir (sem rede, feed fora do ar).</summary>
    CheckFailed,

    /// <summary>A versao instalada ja e a mais nova.</summary>
    UpToDate,

    /// <summary>Ha versao nova, mas a preferencia nao manda instalar sozinho.</summary>
    NewVersionAvailable,

    /// <summary>Ha versao nova, mas a release nao traz o Setup.</summary>
    NoInstaller,

    /// <summary>Etapa intermediaria: ha versao nova e o Setup deve ser baixado.</summary>
    ShouldDownload,

    /// <summary>O download falhou (ou o tamanho nao bateu).</summary>
    DownloadFailed,

    /// <summary>Baixado, mas o app nao pode fechar agora: fica para a proxima conferencia.</summary>
    Postponed,

    /// <summary>O Setup nao pode ser iniciado.</summary>
    InstallFailed,

    /// <summary>O Setup silencioso esta rodando: o app deve fechar.</summary>
    Installing,
}

/// <summary>
/// O resultado do ciclo automatico de atualizacao.
/// </summary>
/// <param name="Outcome">Onde o ciclo parou.</param>
/// <param name="LatestVersion">A versao publicada (quando conhecida).</param>
/// <param name="Detail">O detalhe da falha, quando houver.</param>
public sealed record AutoUpdateResult(AutoUpdateOutcome Outcome, string? LatestVersion, string? Detail);
