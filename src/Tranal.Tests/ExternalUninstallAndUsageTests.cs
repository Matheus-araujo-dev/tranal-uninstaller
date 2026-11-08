using System;
using System.IO;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Management;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Testes da deteccao de desinstalacao externa e dos sinais de uso.
///
/// Item 5 e item 6 da fila da auditoria binaria.
/// </summary>
public class ExternalUninstallAndUsageTests
{
    [Fact]
    public void RegistryPathTraduzOHiveEORelativo()
    {
        var parsed = RegistryPath.Parse(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{X}");

        Assert.NotNull(parsed);
        Assert.Equal(Microsoft.Win32.RegistryHive.LocalMachine, parsed!.Value.Hive);
        Assert.Equal(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{X}", parsed.Value.RelativePath);
    }

    [Fact]
    public void RegistryPathAceitaTextoSemPrefixo()
    {
        var parsed = RegistryPath.Parse(@"SOFTWARE\Qualquer\Coisa");

        Assert.NotNull(parsed);
        Assert.Equal(@"SOFTWARE\Qualquer\Coisa", parsed!.Value.RelativePath);
    }

    [Fact]
    public void RegistryPathAceitaOFormatoQueOScannerGrava()
    {
        // O scanner grava o caminho como "LocalMachine\SOFTWARE\...\Uninstall\{GUID}"
        // (o nome do hive, sem o prefixo HKEY_). As duas formas valem.
        var parsed = RegistryPath.Parse(@"LocalMachine\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{X}");

        Assert.NotNull(parsed);
        Assert.Equal(Microsoft.Win32.RegistryHive.LocalMachine, parsed!.Value.Hive);
        Assert.Equal(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{X}", parsed.Value.RelativePath);

        var usuario = RegistryPath.Parse(@"CurrentUser\Software\Tranal");

        Assert.NotNull(usuario);
        Assert.Equal(Microsoft.Win32.RegistryHive.CurrentUser, usuario!.Value.Hive);
        Assert.Equal(@"Software\Tranal", usuario.Value.RelativePath);
    }

    [Fact]
    public void RegistryPathRecusaTextoVazio()
    {
        Assert.Null(RegistryPath.Parse(null));
        Assert.Null(RegistryPath.Parse("   "));
    }

    [Fact]
    public void ProgramaSemChaveDeRegistroNaoEhDeclaradoRemovido()
    {
        var program = new InstalledProgram { DisplayName = "Store App" };

        Assert.True(ExternalUninstallWatcher.IsStillRegistered(program));
        Assert.Null(ExternalUninstallWatcher.Inspect(program));
    }

    [Fact]
    public void ChaveDeRegistroInexistenteMarcaComoDesinstaladoFora()
    {
        var program = new InstalledProgram
        {
            DisplayName = "Fantasma",
            RegistryPath = @"HKEY_CURRENT_USER\Software\Tranal\Teste\Inexistente\{0}",
        };

        var ev = ExternalUninstallWatcher.Inspect(program);

        Assert.NotNull(ev);
        Assert.True(ev!.RegistryEntryGone);
        Assert.True(ev.WasUninstalledElsewhere);
        Assert.Contains("fora do Tranal", ev.Detail);
    }

    [Fact]
    public void ChaveDeRegistroExistenteNaoMarcaComoRemovido()
    {
        // "HKEY_CURRENT_USER\Software" existe em qualquer conta do Windows.
        var program = new InstalledProgram
        {
            DisplayName = "Real",
            RegistryPath = @"HKEY_CURRENT_USER\Software",
        };

        Assert.True(ExternalUninstallWatcher.IsStillRegistered(program));
        Assert.Null(ExternalUninstallWatcher.Inspect(program));
    }

    [Fact]
    public void InspecionarListaDevolveSomenteOsQueSumiram()
    {
        var keep = new InstalledProgram
        {
            DisplayName = "Fica",
            RegistryPath = @"HKEY_CURRENT_USER\Software",
        };

        var gone = new InstalledProgram
        {
            DisplayName = "Saiu",
            RegistryPath = @"HKEY_CURRENT_USER\Software\Tranal\Teste\Inexistente\{0}",
        };

        var events = ExternalUninstallWatcher.Inspect(new[] { keep, gone });

        Assert.Single(events);
        Assert.Equal("Saiu", events[0].Program.DisplayName);
    }

    [Fact]
    public void UserAssistDecodificaONomeDoExecutavel()
    {
        // O Windows grava o caminho codificado: ROT13 nas letras e +0x50 nos
        // demais caracteres. O teste vai e volta pela tabela de traducao.
        var original = @"C:\Windows\System32\notepad.exe";

        var codificado = UsageSignals.EncodeUserAssistName(original);
        var decodificado = UsageSignals.DecodeUserAssistName(codificado);

        Assert.Equal(original, decodificado);
    }

    [Fact]
    public void UserAssistDescartaNomeQueNaoEUmCaminho()
    {
        // Texto sem unidade e sem barra nao e caminho: o decodificador
        // descarta em vez de devolver lixo.
        Assert.Equal(string.Empty, UsageSignals.DecodeUserAssistName("P\x86\x9a\x8d\x86"));
        Assert.False(UsageSignals.LooksLikePath("lixo"));
        Assert.True(UsageSignals.LooksLikePath(@"C:\x.exe"));
    }

    [Fact]
    public void UserAssistLeDataELerOFiletime()
    {
        var fileTime = DateTime.Now.AddDays(-3).ToFileTimeUtc();
        var bytes = new byte[72];
        BitConverter.GetBytes(fileTime).CopyTo(bytes, 8);
        BitConverter.GetBytes(7).CopyTo(bytes, 4);

        var (lastUsed, runCount) = UsageSignals.ParseUserAssistData(bytes);

        Assert.Equal(7, runCount);
        Assert.NotNull(lastUsed);
        Assert.Equal(DateTime.Now.AddDays(-3).Date, lastUsed!.Value.Date);
    }

    [Fact]
    public void UserAssistAceitaDataCurtaSemInventarData()
    {
        var (lastUsed, runCount) = UsageSignals.ParseUserAssistData(new byte[4]);

        Assert.Null(lastUsed);
        Assert.Equal(0, runCount);
    }

    [Fact]
    public void ProgramaSemExecutavelConhecidoNaoEhRaro()
    {
        var program = new InstalledProgram
        {
            DisplayName = "Sem executavel",
            UninstallString = @"C:\inexistente\nao-existe.exe",
            InstallLocation = string.Empty,
        };

        Assert.False(UsageSignals.IsRarelyUsed(program));
    }

    [Fact]
    public void ExecutavelDoProgramaPreferEODesinstalador()
    {
        var program = new InstalledProgram
        {
            UninstallString = "\"C:\\Program Files\\Exemplo\\unins000.exe\" /VERYSILENT",
        };

        Assert.Equal(@"C:\Program Files\Exemplo\unins000.exe", UsageSignals.ExecutableOf(program));
    }
}
