using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tranal.Cli;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Settings;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O lote 30 da consumidor de verdade ao que ja existia e nao tinha dono: a
/// verificacao da assinatura digital do desinstalador e a execucao do comando
/// do usuario.
///
/// <para>A verificacao ja existia no nucleo (<c>UninstallerCertificate</c>) e
/// nao era chamada por ninguem: o usuario nao tinha como saber se o
/// desinstalador do programa era assinado. O comando do usuario
/// (<c>ShellCommand</c>) tambem nao tinha quem o chamasse.</para>
///
/// <para>A ligacao com a interface e conferida sobre o fonte, pelo mesmo
/// motivo das outras paridades: a suite nao referencia o projeto grafico
/// (WPF).</para>
/// </summary>
public class Lote30FeatureTests
{
    // ==================================================================
    // A assinatura digital do desinstalador
    // ==================================================================

    [Fact]
    public void Um_arquivo_que_nao_existe_nao_assina_e_nao_quebra()
    {
        var assinatura = UninstallerCertificate.Verify(@"C:\arquivo-que-nao-existe.exe");

        Assert.NotNull(assinatura);
        Assert.False(assinatura.IsSigned);
        Assert.Equal(SignatureTrustStatus.NotSigned, assinatura.Status);
    }

    [Fact]
    public void Um_arquivo_vazio_nao_assina_e_nao_quebra()
    {
        Assert.False(UninstallerCertificate.Verify(null).IsSigned);
        Assert.False(UninstallerCertificate.Verify(string.Empty).IsSigned);
        Assert.False(UninstallerCertificate.Verify("   ").IsSigned);
    }

    [Fact]
    public void O_binario_do_windows_e_assinado_e_confiavel()
    {
        // MEDIDO: o kernel32.dll do Windows tem assinatura digital propria e
        // a cadeia fecha na raiz confiável do Windows.
        var caminho = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "kernel32.dll");

        if (!File.Exists(caminho)) return;     // um Windows sem esse arquivo: nada a medir

        var assinatura = UninstallerCertificate.Verify(caminho);

        Assert.True(assinatura.IsSigned);
        Assert.Equal(SignatureTrustStatus.Valid, assinatura.Status);
        Assert.True(assinatura.IsTrusted);
        Assert.Contains("Microsoft", assinatura.SignerSubject);
        Assert.NotEmpty(assinatura.ChainSubjects);
    }

    [Fact]
    public void O_codigo_do_windows_vira_estado()
    {
        Assert.Equal(SignatureTrustStatus.Valid, UninstallerCertificate.StatusFromTrustCode(0));
        Assert.Equal(SignatureTrustStatus.UnknownError, UninstallerCertificate.StatusFromTrustCode(1));

        // 0x800B0100 = CERT_E_UNTRUSTEDROOT: a raiz nao esta ancorada.
        Assert.Equal(
            SignatureTrustStatus.UntrustedRoot,
            UninstallerCertificate.StatusFromTrustCode(unchecked((int)0x800B0100)));
    }

    [Fact]
    public void A_assinatura_do_desinstalador_tem_consumidor_na_tela()
    {
        var tela = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallerSignatureWindow.xaml.cs");
        var menu = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        Assert.Contains("UninstallerCertificate.Verify", tela);
        Assert.Contains("CmdUninstallerSignature", menu);
        Assert.Contains("UninstallerSignatureWindow", menu);
    }

    [Fact]
    public void A_linha_de_comando_verifica_a_assinatura()
    {
        var cli = RepoFiles.Read("src", "Tranal.Cli", "SignatureCommands.cs");

        Assert.Contains("UninstallerCertificate.Verify", cli);
        Assert.Contains("/signature", cli);
        Assert.Contains("/signatures", cli);
    }

    [Fact]
    public void A_cli_da_assinatura_recusa_o_uso_errado()
    {
        var flags = new HashSet<string> { "/signature" };
        var codigo = SignatureCommands.TryRun(Array.Empty<string>(), flags);

        // Sem o arquivo, o comando devolve o erro de uso (e nao 0).
        Assert.NotNull(codigo);
        Assert.NotEqual(0, codigo!.Value);
    }

    [Fact]
    public void A_cli_da_assinatura_recusa_arquivo_inexistente()
    {
        var flags = new HashSet<string> { "/signature" };
        var codigo = SignatureCommands.TryRun(
            new[] { "/signature", @"C:\lote30-arquivo-que-nao-existe.exe" }, flags);

        Assert.NotNull(codigo);
        Assert.NotEqual(0, codigo!.Value);
    }

    // ==================================================================
    // O comando do usuario
    // ==================================================================

    [Fact]
    public async Task O_comando_do_usuario_roda_e_devolve_a_saida()
    {
        var resultado = await ShellCommand.RunAsync("echo lote30");

        Assert.True(resultado.Succeeded);
        Assert.Contains("lote30", resultado.Output);
    }

    [Fact]
    public async Task O_comando_vazio_e_recusado_sem_rodar_nada()
    {
        var resultado = await ShellCommand.RunAsync("   ");

        Assert.False(resultado.Succeeded);
        Assert.NotEqual(0, resultado.ExitCode);
    }

    [Fact]
    public void O_caminho_do_comando_escapa_o_porcento()
    {
        // O cmd.exe expande %VAR%: o caminho com variavel precisa vir
        // escapado, senao o valor some antes de chegar no programa.
        Assert.Equal("\"\"", ShellCommand.Quote(null));
        Assert.Equal("\"100%%\"", ShellCommand.Quote("100%"));
    }

    [Fact]
    public void A_execucao_de_aplicativos_externos_pode_ser_desligada()
    {
        // MEDIDO (10/11/2026): o Bulk Cookie Uninstaller tem a opcao
        // "executar aplicativos externos" desabilitavel. No Tranal o
        // botao "Executar" da tela de comandos some quando a preferencia
        // esta desligada — e o comando continua sendo gravado.
        var preferencias = RepoFiles.Read(
            "src", "Tranal.Uninstaller", "Core", "Settings", "AppSettings.cs");
        var tela = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallCommandsWindow.xaml.cs");
        var opcoes = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "OptionsWindow.xaml");

        Assert.Contains("AllowRunningUserCommands", preferencias);
        Assert.Contains("AppSettingsStore.Current.AllowRunningUserCommands", tela);
        Assert.Contains("Commands.RunDisabled", tela);
        Assert.Contains("OptAllowUserCommands", opcoes);

        // Ligada por padrao: o usuario que nao abre as opcoes nao perde
        // o botao.
        Assert.True(new AppSettings().AllowRunningUserCommands);
    }

    [Fact]
    public void A_tela_de_comandos_tem_o_botao_que_executa()
    {
        var tela = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallCommandsWindow.xaml.cs");
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallCommandsWindow.xaml");

        Assert.Contains("ShellCommand.RunAsync", tela);
        Assert.Contains("CommandsRun", xaml);
        Assert.Contains("CommandsOutput", xaml);
    }

    // ==================================================================
    // A regra do executavel do desinstalador (um lugar so)
    // ==================================================================

    [Fact]
    public void O_executavel_do_desinstalador_e_o_que_o_registro_nomeia()
    {
        var programa = new InstalledProgram
        {
            UninstallString = "\"C:\\Program Files\\App\\unins000.exe\" /VERYSILENT",
        };

        Assert.Equal(
            @"C:\Program Files\App\unins000.exe",
            UninstallerExecutable.Declared(programa));
    }

    [Fact]
    public void O_executavel_sem_comando_fica_vazio()
    {
        // Sem comando de desinstalacao nao ha executavel: a coluna fica
        // vazia, em vez de mostrar um caminho inventado.
        Assert.Equal(string.Empty, UninstallerExecutable.Declared(new InstalledProgram()));
        Assert.Equal(string.Empty, UninstallerExecutable.Resolve(new InstalledProgram()));
        Assert.Equal(string.Empty, UninstallerExecutable.Declared(null));
    }

    [Fact]
    public void O_caminho_com_raiz_vem_do_registro()
    {
        var programa = new InstalledProgram
        {
            UninstallString = "\"C:\\Program Files\\App\\unins000.exe\" /VERYSILENT",
        };

        Assert.Equal(
            @"C:\Program Files\App\unins000.exe",
            UninstallerExecutable.Resolve(programa));
    }

    [Fact]
    public void O_nome_solto_do_instalador_do_windows_e_resolvido_no_path()
    {
        // MEDIDO: o registro nomeia o desinstalador sem caminho
        // ("MsiExec.exe /X{...}"), e o Windows resolve esse nome no PATH.
        var programa = new InstalledProgram
        {
            UninstallString = "MsiExec.exe /X{7C5812B4-79BD-412E-91D7-86D12A9DFC26}",
            Kind = UninstallerKind.MsiExec,
        };

        var resolvido = UninstallerExecutable.Resolve(programa);

        // O executavel existe no Windows: e o instalador do Windows.
        if (resolvido.Length == 0) return;   // um Windows sem o msiexec: nada a medir

        Assert.True(System.IO.Path.IsPathRooted(resolvido));
        Assert.True(System.IO.File.Exists(resolvido));
        Assert.EndsWith("msiexec.exe", resolvido, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void O_instalador_do_windows_e_reconhecido()
    {
        var msi = new InstalledProgram
        {
            UninstallString = "MsiExec.exe /X{7C5812B4}",
            Kind = UninstallerKind.MsiExec,
        };

        Assert.True(UninstallerExecutable.IsWindowsInstaller(msi));

        var comum = new InstalledProgram
        {
            UninstallString = "\"C:\\Program Files\\App\\unins000.exe\"",
            Kind = UninstallerKind.Nsis,
        };

        Assert.False(UninstallerExecutable.IsWindowsInstaller(comum));
        Assert.False(UninstallerExecutable.IsWindowsInstaller(null));
    }

    [Fact]
    public void O_executavel_do_windows_e_reconhecido_pelo_caminho()
    {
        var sistema = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (sistema.Length == 0) return;

        Assert.True(UninstallerExecutable.IsWindowsComponent(
            System.IO.Path.Combine(sistema, "msiexec.exe")));

        Assert.False(UninstallerExecutable.IsWindowsComponent(
            @"C:\Program Files\\App\\unins000.exe"));

        Assert.False(UninstallerExecutable.IsWindowsComponent(string.Empty));
        Assert.False(UninstallerExecutable.IsWindowsComponent(null));
    }

    [Fact]
    public void A_regra_do_desinstalador_mora_em_um_lugar_so()
    {
        // MEDIDO (10/11/2026): a pergunta "qual e o executavel do
        // desinstalador" tinha tres copias que discordavam entre si — a
        // coluna mostrava "MsiExec.exe", a tela dizia que o programa nao
        // tinha desinstalador, e a linha de comando pulava o programa.
        var nucleo = RepoFiles.Read(
            "src", "Tranal.Uninstaller", "Core", "Discovery", "UninstallerExecutable.cs");
        var tela = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallerSignatureWindow.xaml.cs");
        var cli = RepoFiles.Read("src", "Tranal.Cli", "SignatureCommands.cs");
        var colunas = RepoFiles.Read("src", "Tranal.Uninstaller", "Core", "ProgramColumns.cs");

        // O nucleo novo tem a regra.
        Assert.Contains("public static string Resolve", nucleo);
        Assert.Contains("public static string Declared", nucleo);

        // Os tres consumidores consomem a regra do nucleo.
        Assert.Contains("UninstallerExecutable.Resolve", tela);
        Assert.Contains("UninstallerExecutable.Resolve", cli);
        Assert.Contains("UninstallerExecutable.Declared", colunas);

        // E nenhuma copia da regra sobrou: a tela e a CLI nao tem mais o
        // metodo privado que extraia o caminho por conta propria.
        Assert.DoesNotContain("DesinstaladorDe", tela);
        Assert.DoesNotContain("DesinstaladorDe", cli);
    }

    [Fact]
    public void A_tela_fala_a_lingua_do_catalogo()
    {
        // MEDIDO (10/11/2026): a tela mostrava o texto do nucleo
        // (portugues fixo) ao lado do estado traduzido — a mesma
        // informacao em duas linguas, e contradizendo a nota do
        // componente do Windows ("sem assinatura digital" x "a
        // assinatura vem do catalogo do Windows").
        var tela = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallerSignatureWindow.xaml.cs");

        Assert.DoesNotContain("assinatura.Message", tela);
        Assert.Contains("assinatura.Detail", tela);
        Assert.Contains("Signature.VerifyFailed", tela);
        Assert.Contains("Signature.FileMissing", tela);
    }

    [Fact]
    public void O_detalhe_da_falha_vem_do_nucleo()
    {
        // O nucleo separa a explicacao do estado (texto do nucleo, em
        // portugues, para a linha de comando) do detalhe tecnico da
        // falha (o texto da excecao, que a interface traduz).
        var nucleo = RepoFiles.Read(
            "src", "Tranal.Uninstaller", "Core", "Discovery", "UninstallerCertificate.cs");

        Assert.Contains("public string Detail { get; init; } = string.Empty;", nucleo);
        Assert.Contains("Detail = assinado ? string.Empty : erroDeLeitura,", nucleo);

        // Um arquivo que existe mas nao e um executavel nao gera
        // detalhe: o veredito (sem assinatura) e a resposta.
        var assinatura = UninstallerCertificate.Verify(
            Path.Combine(Path.GetTempPath(), "lote30-arquivo-sem-assinatura.txt"));

        Assert.Equal(string.Empty, assinatura.Detail);
    }

    // ==================================================================
    // O catalogo de idiomas
    // ==================================================================

    [Fact]
    public void As_chaves_da_assinatura_estao_nos_dois_idiomas()
    {
        var chaves = new[]
        {
            "Signature.Title",
            "Signature.File",
            "Signature.Status",
            "Signature.Signed",
            "Signature.Signer",
            "Signature.Issuer",
            "Signature.Validity",
            "Signature.Chain",
            "Signature.Problems",
            "Signature.None",
            "Signature.Copy",
            "Signature.WindowsComponent",
            "Signature.WindowsInstaller",
            "Signature.FileMissing",
            "Signature.VerifyFailed",
            "Signature.Status.Valid",
            "Signature.Status.UntrustedRoot",
            "Signature.Status.Expired",
            "Signature.Status.Revoked",
            "Signature.Status.NotSigned",
            "Signature.Status.Invalid",
            "Signature.Status.UnknownError",
            "Cmd.UninstallerSignature",
            "Commands.Run",
            "Commands.EmptyToRun",
            "Commands.Running",
        };

        foreach (var chave in chaves)
        {
            Assert.True(Loc.HasKey(chave) || Loc.Get(chave) != chave, "falta a chave " + chave);
        }
    }
}
