using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Management;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Painel de gestao do sistema: correlacao de permissoes, servicos, tarefas,
/// drivers e a analise de saude.
///
/// Os testes aqui rodam **sem** depender do estado da maquina: o que se
/// verifica e a regra (o casamento de caminho, o corte da linha de comando, o
/// criterio de "servico protegido", o formato da saida do pnputil), e nao a
/// presenca de um servico ou de um driver especifico.
/// </summary>
public class ManagementTests
{
    [Fact]
    public void OExecutavelDeUmServicoComAspacoNoNomeApontaParaOCaminhoCerto()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tranal-mgmt-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var exe = Path.Combine(dir, "Meu Servico.exe");
            File.WriteAllText(exe, "x");

            var service = new WindowsService
            {
                Name = "MeuServico",
                PathName = "\"" + exe + "\" --servico",
            };

            Assert.Equal(exe, service.ExecutablePath);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* o temporario e descartavel */ }
        }
    }

    [Fact]
    public void OExecutavelDeUmServicoSemAspacoNoNomeParaNoPrimeiroEspaco()
    {
        var service = new WindowsService
        {
            Name = "Svc",
            PathName = @"C:\Windows\System32\svchost.exe -k netsvcs",
        };

        Assert.Equal(@"C:\Windows\System32\svchost.exe", service.ExecutablePath);
    }

    [Fact]
    public void OExecutavelDeUmaLinhaDeComandoVaziaEVazio()
    {
        Assert.Equal(string.Empty, ExecutablePathResolver.Extract(null));
        Assert.Equal(string.Empty, ExecutablePathResolver.Extract("   "));
    }

    [Fact]
    public void OCasamentoDePastaEhPorPrefixoComSeparador()
    {
        Assert.True(ServiceManager.BelongsTo(@"C:\App\app.exe", @"C:\App"));
        Assert.True(ServiceManager.BelongsTo(@"C:\App", @"C:\App"));

        // C:\App2 nao esta dentro de C:\App: o separador e o que separa.
        Assert.False(ServiceManager.BelongsTo(@"C:\App2\app.exe", @"C:\App"));
        Assert.False(ServiceManager.BelongsTo(@"C:\Outro\app.exe", @"C:\App"));
    }

    [Fact]
    public void ServicoDoWindowsQueOPermitidoEDoSistemaEProtegido()
    {
        var rpc = new WindowsService { Name = "RpcSs", PathName = @"C:\Windows\System32\svchost.exe" };
        var audio = new WindowsService { Name = "AudioEndpointBuilder", PathName = @"C:\Windows\System32\audioendpointbuilder.exe" };

        Assert.True(ServiceManager.IsCritical(rpc));
        Assert.True(ServiceManager.IsCritical(audio));
    }

    [Fact]
    public void ServicoDeTerceirosNaoEProtegido()
    {
        var service = new WindowsService
        {
            Name = "MeuServico",
            PathName = @"C:\Program Files\Meu\App\service.exe",
        };

        Assert.False(ServiceManager.IsCritical(service));
    }

    [Fact]
    public void ServicoProtegidoNaoPodeSerRemovido()
    {
        var rpc = new WindowsService { Name = "RpcSs", PathName = @"C:\Windows\System32\svchost.exe" };

        var result = ServiceManager.Delete(rpc);

        Assert.False(result.Success);
        Assert.Contains("protegido", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ASaidaDoPnputilEhLidaPeloNomePublicadoDoPacote()
    {
        // A saida real do pnputul e localizada; o que nao muda e o nome
        // publicado (oemNN.inf) e a ordem dos campos.
        const string saida = """
            Published Name : oem12.inf
            Original Name  : nv_dispi.inf
            Provider Name  : NVIDIA
            Class Name     : Display
            Driver Version : 31.0.15.5222
            Signer Name    : Microsoft Windows Hardware Compatibility Publisher
            Date           : 01/10/2024

            Published Name : oem7.inf
            Original Name  : prnms009.inf
            Provider Name  : Microsoft
            Class Name     : Printer
            Driver Version : 10.0.26100.1
            Signer Name    : Microsoft Windows Hardware Compatibility Publisher
            Date           : 15/06/2024
            """;

        var pacotes = DriverManager.Parse(saida);

        Assert.Equal(2, pacotes.Count);
        Assert.Equal("oem12.inf", pacotes[0].PublishedName);
        Assert.Equal("NVIDIA", pacotes[0].ProviderName);
        Assert.Equal("Display", pacotes[0].ClassName);

        // O pacote da Microsoft e marcado como do sistema: nao e removido.
        Assert.True(pacotes[1].IsSystemPackage);
        Assert.False(pacotes[0].IsSystemPackage);
    }

    [Fact]
    public void APacoteDoWindowsNaoERemovido()
    {
        var pacote = new DriverPackage { PublishedName = "oem1.inf", IsSystemPackage = true };

        var result = DriverManager.Delete(pacote);

        Assert.False(result.Success);
        Assert.Contains("Windows", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ASaidaDoPnputilVaziaNaoGeraPacote()
    {
        Assert.Empty(DriverManager.Parse(string.Empty));
        Assert.Empty(DriverManager.Parse("nada aqui"));
    }

    [Fact]
    public void OsGruposUniversaisDoWindowsSaoReconhecidosNosDoisIdiomas()
    {
        Assert.True(SoftwareHealth.IsUniversalGroup("Everyone"));
        Assert.True(SoftwareHealth.IsUniversalGroup("Todos"));
        Assert.True(SoftwareHealth.IsUniversalGroup("Users"));
        Assert.True(SoftwareHealth.IsUniversalGroup("Usuários"));
        Assert.True(SoftwareHealth.IsUniversalGroup("Authenticated Users"));
        Assert.True(SoftwareHealth.IsUniversalGroup("Usuários Autenticados"));

        Assert.False(SoftwareHealth.IsUniversalGroup("Matheus"));
        Assert.False(SoftwareHealth.IsUniversalGroup(string.Empty));
    }

    [Fact]
    public void APastaDeInstalacaoVemDoRegistro()
    {
        var program = new InstalledProgram
        {
            DisplayName = "Exemplo",
            InstallLocation = @"C:\Program Files\Exemplo",
        };

        Assert.Equal(@"C:\Program Files\Exemplo", AppPermissionManager.InstallLocationOf(program));
    }

    [Fact]
    public void APastaDeInstalacaoCaiNoExecutavelDoDesinstaladorQuandoORegistroNaoTem()
    {
        var program = new InstalledProgram
        {
            DisplayName = "Exemplo",
            UninstallString = "\"C:\\Program Files\\Exemplo\\unins000.exe\" /VERYSILENT",
        };

        var location = AppPermissionManager.InstallLocationOf(program);

        Assert.Equal(@"C:\Program Files\Exemplo", location);
    }

    [Fact]
    public void SemPastaDeInstalacaoNaoHaPermissaoParaCorrelacionar()
    {
        var program = new InstalledProgram { DisplayName = "Sem lugar" };

        Assert.Equal(string.Empty, AppPermissionManager.InstallLocationOf(program));
        Assert.Empty(AppPermissionManager.For(program));
    }

    [Fact]
    public void ORelatorioDeSaudeSeparaAsQuatroCategorias()
    {
        var program = new InstalledProgram
        {
            DisplayName = "Sem desinstalador",
            InstallLocation = string.Empty,
            UninstallString = string.Empty,
            RegistryPath = string.Empty,
        };

        var report = SoftwareHealth.Analyze(new[] { program });

        // O programa nao tem comando de desinstalacao: isso e um aviso, e o
        // aviso tem de aparecer na categoria certa.
        Assert.Equal(1, report.CountOf(HealthCategory.Notifications));
        Assert.Equal(0, report.CountOf(HealthCategory.Leftovers));
        Assert.False(report.IsHealthy);
    }

    [Fact]
    public void UmProgramaSemAchadosDeixaORelatorioSaudavel()
    {
        var program = new InstalledProgram
        {
            DisplayName = "Exemplo",
            UninstallString = @"C:\Exemplo\unins.exe",
            InstallLocation = @"C:\Exemplo",
        };

        var report = SoftwareHealth.Analyze(new[] { program }, scanLeftovers: false, scanDuplicates: false);

        Assert.True(report.IsHealthy);
    }

    [Fact]
    public void ArquivosRepetidosSoEntramQuandoOTambemEORepetido()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tranal-dup-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var a = Path.Combine(dir, "a");
            var b = Path.Combine(dir, "b");
            Directory.CreateDirectory(a);
            Directory.CreateDirectory(b);

            var conteudo = "conteudo igual para os dois";
            File.WriteAllText(Path.Combine(a, "dados.txt"), conteudo);
            File.WriteAllText(Path.Combine(b, "dados.txt"), conteudo);

            // Mesmo nome e mesmo tamanho, conteudo diferente: nao e repetido.
            File.WriteAllText(Path.Combine(a, "outro.txt"), "aaaa");
            File.WriteAllText(Path.Combine(b, "outro.txt"), "bbbb");

            var hashA = SoftwareHealth.HashOf(Path.Combine(a, "dados.txt"));
            var hashB = SoftwareHealth.HashOf(Path.Combine(b, "dados.txt"));

            Assert.Equal(hashA, hashB);
            Assert.NotEqual(hashA, SoftwareHealth.HashOf(Path.Combine(a, "outro.txt")));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* o temporario e descartavel */ }
        }
    }

    [Fact]
    public void OHashDeUmArquivoInexistenteEVazio()
    {
        Assert.Equal(string.Empty, SoftwareHealth.HashOf(
            Path.Combine(Path.GetTempPath(), "tranal-nao-existe-" + Guid.NewGuid().ToString("N")[..8])));
    }
}
