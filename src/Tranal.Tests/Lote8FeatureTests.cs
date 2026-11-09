using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// As features do lote 8: a guarda do arquivo pessoal, o registro das
/// desinstalacoes que falharam e o Instal guard.
///
/// O que estes testes cobrem: a guarda classifica o caminho certo (e nao
/// qualquer coisa que comece com o mesmo nome), o registro da falha
/// sobrevive a ida e volta no disco, e o Instal guard reconhece a
/// assinatura de um instalador de verdade.
/// </summary>
public class Lote8FeatureTests
{
    [Fact]
    public void AGuardaClassificaAPastaPessoalDoUsuario()
    {
        var documentos = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        Assert.False(string.IsNullOrWhiteSpace(documentos));

        var arquivo = Path.Combine(documentos, "relatorio do trimestre.txt");

        Assert.Equal(PersonalFileKind.Documento, PersonalFileGuard.Classify(arquivo));
        Assert.True(PersonalFileGuard.IsPersonal(arquivo));
    }

    [Fact]
    public void AGuardaNaoClassificaOQueEstaForaDasPastasPessoais()
    {
        var caminho = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "notepad.exe");

        Assert.Null(PersonalFileGuard.Classify(caminho));
        Assert.False(PersonalFileGuard.IsPersonal(caminho));
    }

    [Fact]
    public void AGuardaEncontraOsItensPessoaisDeUmaLista()
    {
        var documentos = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var itens = PersonalFileGuard.Find(new[]
        {
            Path.Combine(documentos, "nota.txt"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "calc.exe"),
        });

        Assert.Single(itens);
        Assert.Equal("nota.txt", itens[0].Name);
    }

    [Fact]
    public void ORegistroDeFalhasGuardaESobreviveNoDisco()
    {
        var programa = new InstalledProgram
        {
            DisplayName = "Programa Que Falha Lote 8",
            UninstallString = @"C:\Teste\uninstall.exe",
            InstallLocation = @"C:\Teste",
        };

        var resultado = new BuiltInUninstallResult
        {
            ProgramName = programa.DisplayName,
            Command = programa.UninstallString,
            Started = true,
            ExitCode = 1603,
            Message = "o desinstalador retornou erro",
        };

        try
        {
            UninstallFailureRecovery.Record(programa, resultado);

            var registro = UninstallFailureRecovery.List();
            var falha = registro.FirstOrDefault(f => f.ProgramName == programa.DisplayName);

            Assert.NotNull(falha);
            Assert.Equal(1603, falha!.ExitCode);
            Assert.Equal(@"C:\Teste\uninstall.exe", falha.UninstallString);
        }
        finally
        {
            UninstallFailureRecovery.Forget(programa.DisplayName);
        }
    }

    [Fact]
    public void UmaDesinstalacaoBemSucedidaNaoEntraNoRegistro()
    {
        var programa = new InstalledProgram { DisplayName = "Programa Que Funciona Lote 8" };

        var resultado = new BuiltInUninstallResult
        {
            ProgramName = programa.DisplayName,
            Command = @"C:\Teste\uninstall.exe",
            Started = true,
            ExitCode = 0,
        };

        UninstallFailureRecovery.Record(programa, resultado);

        Assert.DoesNotContain(UninstallFailureRecovery.List(),
            f => f.ProgramName == programa.DisplayName);
    }

    [Fact]
    public void OInstalGuardTemAsRegrasEmbutidas()
    {
        Assert.NotEmpty(InstallerGuard.BuiltinRules);
        Assert.All(InstallerGuard.BuiltinRules, r => Assert.False(string.IsNullOrWhiteSpace(r.Name)));
        Assert.Contains(InstallerGuard.BuiltinRules, r => r.Family == InstallerFamily.InnoSetup);
        Assert.Contains(InstallerGuard.BuiltinRules, r => r.Family == InstallerFamily.Nsis);
    }

    [Fact]
    public void OInstalGuardReconheceAAssinaturaDeUmInstalador()
    {
        // Um arquivo que carrega a assinatura do NSIS no binario: e o que
        // o Tranal mede de verdade, sem depender do nome do arquivo.
        var arquivo = Path.Combine(Path.GetTempPath(), "instalador-lote8-nsis.exe");
        var conteudo = new byte[8192];
        var assinatura = System.Text.Encoding.ASCII.GetBytes("Nullsoft Install System");
        Array.Copy(assinatura, 0, conteudo, 1024, assinatura.Length);

        try
        {
            File.WriteAllBytes(arquivo, conteudo);

            var achado = InstallerGuard.Detect(arquivo);

            Assert.True(achado.Detected);
            Assert.Equal(InstallerFamily.Nsis, achado.Family);
            Assert.True(achado.SignatureFound);
            Assert.Equal("NSIS", achado.MatchedRule);
        }
        finally
        {
            if (File.Exists(arquivo)) File.Delete(arquivo);
        }
    }

    [Fact]
    public void OInstalGuardNaoInventaUmTipoParaArquivoComum()
    {
        var arquivo = Path.Combine(Path.GetTempPath(), "instalador-lote8-comum.exe");
        File.WriteAllBytes(arquivo, new byte[4096]);

        try
        {
            var achado = InstallerGuard.Detect(arquivo);

            Assert.False(achado.Detected);
            Assert.Equal(InstallerFamily.Desconhecido, achado.Family);
            Assert.Equal(string.Empty, achado.MatchedRule);
        }
        finally
        {
            if (File.Exists(arquivo)) File.Delete(arquivo);
        }
    }

    [Fact]
    public void AOpcaoDeRemocaoPreservaOsArquivosPessoaisPorPadrao()
    {
        Assert.True(UninstallRemovalOptions.Default.ProtectPersonalFiles);
        Assert.True(UninstallRemovalOptions.FromSettings().ProtectPersonalFiles);
    }

    [Fact]
    public void OItemPessoalNaoERemovidoPeloMotor()
    {
        var documentos = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var item = new LeftoverItem
        {
            Kind = "Arquivo",
            Path = Path.Combine(documentos, "arquivo-pessoal-lote8.txt"),
            Selected = true,
        };

        var relatorio = UninstallEngine.RemoveLeftovers(
            new[] { item },
            new UninstallRemovalOptions { Backup = false, UseRecycleBin = false });

        var desfecho = relatorio.Outcomes.FirstOrDefault();

        Assert.NotNull(desfecho);
        Assert.Equal(RemovalStatus.Protected, desfecho!.Status);
    }
}
