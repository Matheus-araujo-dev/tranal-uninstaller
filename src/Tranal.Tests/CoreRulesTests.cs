using Tranal.Uninstaller.Core;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Regras de seguranca: o Tranal nunca pode apagar estrutura do sistema,
/// mesmo quando a varredura casa com o nome do programa.
/// </summary>
public class SafetyRulesTests
{
    [Theory]
    [InlineData(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft")]
    [InlineData(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node")]
    [InlineData(@"HKEY_CURRENT_USER\SOFTWARE\Classes")]
    [InlineData(@"HKEY_LOCAL_MACHINE\SYSTEM")]
    [InlineData(@"")]
    public void ChavesEstruturaisSaoProtegidas(string path)
    {
        Assert.True(SafetyRules.IsProtectedRegistryKey(path));
    }

    [Theory]
    [InlineData(@"C:\Windows")]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"C:\Program Files")]
    [InlineData(@"C:\")]
    [InlineData(@"")]
    public void PastasDeSistemaSaoProtegidas(string path)
    {
        Assert.True(SafetyRules.IsProtectedFolder(path));
    }

    [Theory]
    [InlineData(@"C:\Users\Usuario\AppData\Local\MeuPrograma")]
    [InlineData(@"C:\Program Files\MeuApp")]
    [InlineData(@"D:\Apps\MeuApp")]
    public void PastasDeProgramasNaoSaoProtegidas(string path)
    {
        Assert.False(SafetyRules.IsProtectedFolder(path));
    }

    /// <summary>
    /// Subpastas temporarias do Windows sao liberadas para limpeza; a pasta
    /// "Windows" e as estruturais continuam protegidas.
    /// </summary>
    [Theory]
    [InlineData(@"C:\Windows\Temp")]
    [InlineData(@"C:\Windows\Prefetch")]
    [InlineData(@"C:\Windows\SoftwareDistribution\Download")]
    [InlineData(@"C:\Windows\System32\winevt\Logs")]
    [InlineData(@"C:\Windows\Logs")]
    public void SubpastasTemporariasDoWindowsSaoLiberadas(string path)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var real = Path.Combine(windows, path.Substring(@"C:\Windows\".Length));

        Assert.False(SafetyRules.IsProtectedFolder(real));
    }

    [Fact]
    public void WindowsESubpastasEstruturaisContinuamProtegidas()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        Assert.True(SafetyRules.IsProtectedFolder(windows));
        Assert.True(SafetyRules.IsProtectedFolder(Path.Combine(windows, "System32")));
        Assert.True(SafetyRules.IsProtectedFolder(Path.Combine(windows, "SoftwareDistribution")));
        Assert.True(SafetyRules.IsProtectedFolder(Path.Combine(windows, "assembly")));
    }

    [Fact]
    public void NomeDoHiveEhCanonico()
    {
        Assert.Equal("HKEY_LOCAL_MACHINE", SafetyRules.HiveName(Microsoft.Win32.RegistryHive.LocalMachine));
        Assert.Equal("HKEY_CURRENT_USER", SafetyRules.HiveName(Microsoft.Win32.RegistryHive.CurrentUser));
    }
}

/// <summary>Casamento por palavra inteira: evita "Steam" casando com "Steamworks".</summary>
public class NameMatchTests
{
    [Theory]
    [InlineData("Steamworks", "Steam", false)]
    [InlineData("Steam Client", "Steam", true)]
    [InlineData("Electronic Arts Steam", "Steam", true)]
    [InlineData("Steam", "Steam", true)]
    public void ContainsWordNaoCasaParcialmente(string candidate, string variant, bool expected)
    {
        Assert.Equal(expected, NameMatch.ContainsWord(candidate, variant));
    }

    [Theory]
    [InlineData("Steamworks", "Steam", false)]
    [InlineData("Steam Client", "Steam", true)]
    public void StartsWithWordNaoCasaParcialmente(string candidate, string variant, bool expected)
    {
        Assert.Equal(expected, NameMatch.StartsWithWord(candidate, variant));
    }
}

/// <summary>Normalizacao de caminho: a mesma pasta nao pode aparecer duas vezes.</summary>
public class PathUtilTests
{
    [Fact]
    public void BarraFinalNaoCriaDuplicata()
    {
        var a = PathUtil.Normalize(@"C:\Program Files\App\");
        var b = PathUtil.Normalize(@"C:\Program Files\App");
        Assert.Equal(a, b);
    }

    [Fact]
    public void CaminhoVazioNormalizaParaVazio()
    {
        Assert.Equal(string.Empty, PathUtil.Normalize(null));
        Assert.Equal(string.Empty, PathUtil.Normalize("   "));
    }
}
