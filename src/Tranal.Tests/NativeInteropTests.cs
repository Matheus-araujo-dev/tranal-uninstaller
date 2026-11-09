using System.Reflection;
using System.Runtime.InteropServices;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Management;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Os contratos das chamadas de API do Windows.
///
/// Cada teste aqui trava um defeito que ja aconteceu: quando a estrutura
/// gerenda e menor do que a estrutura nativa, a API do Windows escreve alem
/// do fim do espaco gerenciado — e a memoria do processo fica corrompida.
/// O sintoma aparece longe da causa (um acesso invalido no ntdll, em outra
/// operacao qualquer), e derruba o programa.
///
/// A regra: a estrutura gerenciada tem o mesmo tamanho da nativa, e a
/// assinatura declara os mesmos parametros.
/// </summary>
public class NativeInteropTests
{
    // ------------------------------------------------------------------
    // Restart Manager
    // ------------------------------------------------------------------

    [Fact]
    public void AestruturaDoRestartManagerTemOTamanhoDoWindows()
    {
        // O Windows escreve 668 bytes em cada RM_PROCESS_INFO (os nomes sao
        // em UTF-16). Uma estrutura menor faz a escrita passar do fim do
        // array: 320 bytes de lixo em cada processo.
        Assert.Equal(668, RestartManager.TamanhoDaEstrutura);
        Assert.True(RestartManager.EstruturaNoTamanhoDoWindows);
    }

    [Fact]
    public void ARestartManagerDevolveAListaSemDestruirAMemoria()
    {
        // Um arquivo livre nao esta em uso por ninguem: a chamada completa
        // (a que preenche o array) acontece e volta.
        var arquivo = Path.Combine(Path.GetTempPath(), "tranal-restart-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(arquivo, "conteudo");

        try
        {
            var processos = RestartManager.WhoIsLocking(arquivo);

            Assert.NotNull(processos);
        }
        finally
        {
            File.Delete(arquivo);
        }
    }

    // ------------------------------------------------------------------
    // Confianca do Windows
    // ------------------------------------------------------------------

    [Fact]
    public void AestruturaDeConfiancaTemOTamanhoDoWindows()
    {
        // O WINTRUST_DATA do Windows tem 88 bytes: os dois ponteiros do
        // comeco (politica e provedor) vem antes da uniao.
        Assert.True(UninstallerCertificate.EstruturaNoTamanhoDoWindows);
    }

    [Fact]
    public void AVerificacaoDeAssinaturaDizQueOArquivoNaoTemAssinatura()
    {
        var arquivo = Path.Combine(Path.GetTempPath(), "tranal-sem-assinatura-" + Guid.NewGuid().ToString("N") + ".exe");
        File.WriteAllBytes(arquivo, new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 });

        try
        {
            var assinatura = UninstallerCertificate.Verify(arquivo);

            // O arquivo nao tem assinatura propria: o veredito e "nao
            // assinado", e nao "invalido" nem "desconhecido".
            Assert.False(assinatura.IsSigned);
            Assert.Equal(SignatureTrustStatus.NotSigned, assinatura.Status);
        }
        finally
        {
            File.Delete(arquivo);
        }
    }

    // ------------------------------------------------------------------
    // Servicos do Windows
    // ------------------------------------------------------------------

    [Fact]
    public void CriarServicoDeclaraOsParametrosDaApiDoWindows()
    {
        var metodo = typeof(ServiceControlNative)
            .GetMethod("CreateService", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.NotNull(metodo);

        var parametros = metodo!.GetParameters();

        // A API do Windows tem 15 parametros. A nossa tinha 15 tambem, mas
        // o ultimo era um IntPtr sem uso: o handle de saida nunca voltava e
        // o servico nunca era criado.
        Assert.Equal(15, parametros.Length);

        // O ultimo e o handle de saida (LPHANDLE).
        Assert.True(parametros[14].IsOut);
        Assert.Equal(typeof(IntPtr), parametros[14].ParameterType.GetElementType());

        // O grupo de carga e um texto (LPCWSTR), e nao um numero.
        Assert.Equal(typeof(string), parametros[7].ParameterType);

        // O tamanho da senha e um numero (DWORD), e nao um texto.
        Assert.Equal(typeof(uint), parametros[12].ParameterType);
    }

    [Fact]
    public void MudarConfiguracaoDoServicoDeclaraOsParametrosDaApiDoWindows()
    {
        var metodo = typeof(ServiceControlNative)
            .GetMethod("ChangeServiceConfig2", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.NotNull(metodo);

        // A API do Windows tem 3 parametros; a nossa tinha 4.
        Assert.Equal(3, metodo!.GetParameters().Length);
    }

    [Fact]
    public void ADescricaoDoServicoTemOTamanhoDoWindows()
    {
        // SERVICE_DESCRIPTION e um ponteiro para o texto.
        Assert.Equal(8, Marshal.SizeOf<ServiceControlNative.ServiceDescription>());
    }
}
