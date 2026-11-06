using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// A atribuicao das sobras pela cadeia de desinstalacao do Windows Installer.
///
/// Ate aqui a varredura trabalhava pelo NOME: casava pastas e chaves pelo
/// nome do programa. Isso e heuristica. Para os produtos do Windows Installer
/// (149 dos 211 programas desta maquina) existe um registro exato do que o
/// instalador gravou — o pacote .msi em cache — e e ele que este conjunto de
/// regras usa.
///
/// As regras de nome vem de MEDICAO, nao de suposicao: o pacote do WSL traz
/// <c>DefaultDir = x0zlheba|wslsettings</c> e o que existe em disco e
/// "wslsettings"; o ponto (".") significa a mesma pasta do pai.
/// </summary>
public class MsiInstallLogTests : IDisposable
{
    private readonly List<string> _sandboxes = new();

    private string CreateSandbox(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        _sandboxes.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _sandboxes)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
            catch { }
        }
    }

    /// <summary>Cria uma linha da tabela Directory como o pacote a traz.</summary>
    private static MsiInstallLogReader.DirectoryRow Row(string key, string parent, string defaultDir) =>
        new(key, parent, defaultDir);

    [Fact]
    public void OANomeDaPastaEODestinoEAposOPipe()
    {
        // Medido: o pacote do WSL traz "x0zlheba|wslsettings" e o que existe
        // em disco e "wslsettings".
        Assert.Equal(
            "wslsettings",
            MsiInstallLogReader.OnDiskName(Row("WSLSETTINGS", "INSTALLDIR", "x0zlheba|wslsettings")));

        Assert.Equal(
            "DirectXRedist",
            MsiInstallLogReader.OnDiskName(Row("dir", "INSTALLDIR", "vcymgiuq|DirectXRedist")));

        // Sem a segunda parte, vale a primeira.
        Assert.Equal(
            "tools",
            MsiInstallLogReader.OnDiskName(Row("TOOLSFOLDER", "INSTALLDIR", "tools")));
    }

    [Fact]
    public void OPontoSignificaAMesmaPastaDoPai()
    {
        // O ponto nao e um nome de pasta: e a propria pasta do pai.
        Assert.Equal(
            ".",
            MsiInstallLogReader.OnDiskName(Row("SELECTEDINSTALLFOLDER", "ProgramFilesFolder", ".")));
    }

    [Fact]
    public void ONomeLongoDoArquivoEODestino()
    {
        // A tabela File traz "curto|longo", e o que fica no disco e o longo.
        Assert.Equal(
            "wslserviceproxystub.dll",
            MsiInstallLogReader.LongName("ozn7dfvi.dll|wslserviceproxystub.dll"));

        Assert.Equal("wsl.exe", MsiInstallLogReader.LongName("wsl.exe"));
    }

    [Fact]
    public void OLogSoValeDepoisQueODesinstaladorNativoRodou()
    {
        // Antes disso o programa continua instalado: usar o log descreveria
        // o programa inteiro como sobra, e a etapa seguinte apagaria o que o
        // usuario queria manter.
        var log = new MsiInstallLog { Files = new[] { @"C:\qualquer\coisa.txt" } };

        Assert.Null(MsiInstallLogReader.UsableAfter(log, uninstalled: false));
        Assert.NotNull(MsiInstallLogReader.UsableAfter(log, uninstalled: true));
        Assert.Null(MsiInstallLogReader.UsableAfter(null, uninstalled: true));
    }

    [Fact]
    public void ODesinstaladorQuePediuReiniciarContaComoDesinstalado()
    {
        // 3010 e o codigo do Windows Installer para "desinstalado, mas e
        // preciso reiniciar". Do ponto de vista das sobras, deu certo.
        var reiniciar = new BuiltInUninstallResult
        {
            ProgramName = "MeuApp",
            Command = "MsiExec.exe /X{...}",
            Started = true,
            ExitCode = 3010,
        };

        Assert.True(reiniciar.Uninstalled);
        Assert.False(reiniciar.Succeeded);

        var falhou = new BuiltInUninstallResult
        {
            ProgramName = "MeuApp",
            Command = "MsiExec.exe /X{...}",
            Started = true,
            ExitCode = 1603,
        };

        Assert.False(falhou.Uninstalled);

        var nemRodou = new BuiltInUninstallResult
        {
            ProgramName = "MeuApp",
            Command = string.Empty,
            Started = false,
        };

        Assert.False(nemRodou.Uninstalled);
    }

    [Fact]
    public void OItemQueSobrouDoInstaladorEntraMarcado()
    {
        var folder = CreateSandbox("tranal-msi-");
        var file = Path.Combine(folder, "sobrou.txt");
        File.WriteAllText(file, "conteudo");

        var log = new MsiInstallLog
        {
            ProductCode = "{00000000-0000-0000-0000-000000000000}",
            Files = new[] { file },
        };

        var items = MsiInstallLogReader.FindLeftovers(log);

        var item = Assert.Single(items);
        Assert.Equal("Arquivo", item.Kind);
        Assert.Equal(file, item.Path);
        Assert.Equal(LeftoverConfidence.High, item.Confidence);
        Assert.Equal(LeftoverReason.InstallerLog, item.Reason);
        Assert.True(item.Selected);

        // Confianca e motivo decidem sempre juntos.
        Assert.Equal(LeftoverReason.InstallerLog, item.Reason);
    }

    [Fact]
    public void OItemQueODesinstaladorJaApagouNaoEntra()
    {
        var folder = CreateSandbox("tranal-msi-");
        var file = Path.Combine(folder, "ja-apagado.txt");
        File.WriteAllText(file, "conteudo");

        var log = new MsiInstallLog { Files = new[] { file } };

        // Simula o desinstalador nativo ja ter rodado: o arquivo sumiu.
        File.Delete(file);

        Assert.Empty(MsiInstallLogReader.FindLeftovers(log));
    }

    [Fact]
    public void PastaComConteudoDeOutroProgramaNaoEntra()
    {
        var folder = CreateSandbox("tranal-msi-");
        var sub = Path.Combine(folder, "com-conteudo");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "de-outro.txt"), "conteudo");

        var vazia = Path.Combine(folder, "vazia");
        Directory.CreateDirectory(vazia);

        var log = new MsiInstallLog { Directories = new[] { sub, vazia } };

        var items = MsiInstallLogReader.FindLeftovers(log);

        // So a pasta vazia e sobra deste programa.
        var item = Assert.Single(items);
        Assert.Equal("Pasta", item.Kind);
        Assert.Equal(vazia, item.Path);
        Assert.Equal(LeftoverReason.InstallerLog, item.Reason);
    }

    [Fact]
    public void AVarreduraRecebeOItemDoLogDoInstalador()
    {
        var folder = CreateSandbox("tranal-msi-");
        var file = Path.Combine(folder, "registrado.txt");
        File.WriteAllText(file, "conteudo");

        var program = new InstalledProgram
        {
            DisplayName = "Programa de Teste Tranal",
            InstallLocation = folder,
        };

        var log = new MsiInstallLog { Files = new[] { file } };

        var items = LeftoverScanner.Scan(
            program, new LeftoverScanOptions { Mode = UninstallMode.Advanced }, log);

        Assert.Contains(items, i =>
            i.Kind == "Arquivo" && i.Reason == LeftoverReason.InstallerLog && i.Path == file);
    }

    [Fact]
    public void UmProgramaQueNaoEDoWindowsInstallerNaoTemLog()
    {
        var program = new InstalledProgram
        {
            DisplayName = "Programa de Teste Tranal",
            Kind = UninstallerKind.Nsis,
            UninstallString = @"""C:\Program Files\MeuApp\uninstall.exe""",
        };

        Assert.Null(MsiInstallLogReader.Capture(program));
    }

    [Fact]
    public void OLogDeUmProdutoQueNaoEstaInstaladoEhNulo()
    {
        Assert.Null(MsiInstallLogReader.Capture("{00000000-0000-0000-0000-000000000000}"));
        Assert.Null(MsiInstallLogReader.Capture(string.Empty));
    }

    [Fact]
    public void OMotivoDoLogTemFraseNosDoisIdiomas()
    {
        var pt = LeftoverLabels.Reason(LeftoverReason.InstallerLog);
        var en = LeftoverLabels.Reason(LeftoverReason.InstallerLog);

        Assert.False(string.IsNullOrWhiteSpace(pt));
        Assert.False(string.IsNullOrWhiteSpace(en));

        // O motivo novo nao pode se confundir com "sem motivo registrado".
        Assert.NotEqual(LeftoverLabels.Reason(LeftoverReason.Unknown), pt);
        Assert.NotEqual(LeftoverLabels.Reason(LeftoverReason.Unknown), en);
    }

    [Fact]
    public void ACapturaDoLogNaoQuebraComProdutoInexistente()
    {
        // Nenhum produto com esse codigo existe: a captura devolve nulo em
        // vez de estourar.
        var log = MsiInstallLogReader.Capture("{11111111-2222-3333-4444-555555555555}");

        Assert.Null(log);
        Assert.Empty(MsiInstallLogReader.FindLeftovers(log!));
    }
}
