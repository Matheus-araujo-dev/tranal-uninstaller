using System.Text.Json;
using Tranal.Uninstaller.Core;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O log de remocao por item.
///
/// O Uninstall Tool grava um log detalhado de remocao com codigo de erro, e
/// a auditoria do Geek/MSI pede o mesmo. Antes o Tranal contava: sabia
/// quantos itens sairam, mas nao deixava rastro de QUAL item e POR QUE nao
/// saiu.
///
/// A regra mora em <c>Core/RemovalLog.cs</c>.
/// </summary>
public class RemovalLogTests : IDisposable
{
    private readonly List<string> _sandboxes = new();

    private string CreateSandbox()
    {
        var path = Path.Combine(Path.GetTempPath(), "tranal-log-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        _sandboxes.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _sandboxes)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
            catch { }
        }
    }

    /// <summary>Cria uma sobra de arquivo.</summary>
    private static LeftoverItem Arquivo(string path) => new()
    {
        Kind = "Arquivo",
        Path = path,
        Confidence = LeftoverConfidence.High,
        Reason = LeftoverReason.ExactNameMatch,
    };

    /// <summary>Le um arquivo do repositorio.</summary>
    private static string Read(params string[] parts) => RepoFiles.Read(parts);

    /// <summary>Cria um resultado de remocao.</summary>
    private static RemovalOutcome Outcome(
        LeftoverItem item, RemovalStatus status, string message = "", int errorCode = 0) => new()
    {
        Item = item,
        Status = status,
        Message = message,
        ErrorCode = errorCode,
    };

    // ---------------------------------------------------------------------
    // Codigo de erro
    // ---------------------------------------------------------------------

    [Fact]
    public void ItemRemovidoNaoTemCodigoDeErro()
    {
        Assert.Equal(0, RemovalLog.ErrorCodeFor(Outcome(Arquivo("a.txt"), RemovalStatus.Removed)));
    }

    [Fact]
    public void ItemProtegidoUsaOCodigoDoTranal()
    {
        // 403 nao e um codigo do Windows: e o "o Windows recusou" contra o
        // "o Tranal protegeu".
        Assert.Equal(RemovalLog.BlockedByPolicy,
            RemovalLog.ErrorCodeFor(Outcome(Arquivo("a.txt"), RemovalStatus.Protected, "pasta de sistema protegida")));
    }

    [Fact]
    public void ItemAdiadoUsaOCodigoDoInstaladorDoWindows()
    {
        Assert.Equal(RemovalLog.DeferredToReboot,
            RemovalLog.ErrorCodeFor(Outcome(Arquivo("a.txt"), RemovalStatus.Deferred)));
    }

    [Fact]
    public void FalhaUsaOCodigoDoWindowsQuandoExiste()
    {
        var outcome = Outcome(Arquivo("a.txt"), RemovalStatus.Failed, "acesso negado", errorCode: 5);

        Assert.Equal(5, RemovalLog.ErrorCodeFor(outcome));
    }

    [Fact]
    public void FalhaSemCodigoDoWindowsFicaComZero()
    {
        var outcome = Outcome(Arquivo("a.txt"), RemovalStatus.Failed, "tipo desconhecido");

        Assert.Equal(0, RemovalLog.ErrorCodeFor(outcome));
    }

    [Fact]
    public void OcodigoDoWindowsVeemDaParteBaixaDoHResult()
    {
        // UnauthorizedAccessException tem HResult 0x80070005: a parte baixa
        // e o codigo 5 do Windows (ERROR_ACCESS_DENIED).
        var code = RemovalOutcome.ErrorCodeOf(new UnauthorizedAccessException());

        Assert.Equal(5, code);
    }

    // ---------------------------------------------------------------------
    // Construcao do log
    // ---------------------------------------------------------------------

    [Fact]
    public void OLogTemUmaLinhaPorItemDoRelatorio()
    {
        var report = new RemovalReport(new[]
        {
            Outcome(Arquivo("C:\\a.txt"), RemovalStatus.Removed, "removido"),
            Outcome(Arquivo("C:\\b.txt"), RemovalStatus.Failed, "em uso", 32),
        });

        var entries = RemovalLog.Build(report);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Arquivo", entries[0].Kind);
        Assert.Equal("C:\\a.txt", entries[0].Path);
        Assert.Equal("Removed", entries[0].Status);
        Assert.Equal(32, entries[1].ErrorCode);
    }

    [Fact]
    public void OLogCarregaAsContagensDeDentroDaPasta()
    {
        var outcome = new RemovalOutcome
        {
            Item = Arquivo("C:\\pasta"),
            Status = RemovalStatus.Protected,
            Message = "permaneceu",
            RemovedInside = 3,
            ProtectedInside = 1,
            FailedInside = 2,
            DeferredInside = 4,
        };

        var entries = RemovalLog.Build(new RemovalReport(new[] { outcome }));

        Assert.Equal(3, entries[0].RemovedInside);
        Assert.Equal(1, entries[0].ProtectedInside);
        Assert.Equal(2, entries[0].FailedInside);
        Assert.Equal(4, entries[0].DeferredInside);
    }

    // ---------------------------------------------------------------------
    // CSV
    // ---------------------------------------------------------------------

    [Fact]
    public void OCsvTemCabecalhoEUmaLinhaPorItem()
    {
        var report = new RemovalReport(new[]
        {
            Outcome(Arquivo("C:\\a.txt"), RemovalStatus.Removed, "removido"),
            Outcome(Arquivo("C:\\b.txt"), RemovalStatus.Failed, "em uso", 32),
        });

        var csv = RemovalLog.ToCsv(RemovalLog.Build(report));
        var linhas = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, linhas.Length);
        Assert.Contains("Tipo;Caminho;Resultado;Codigo", linhas[0]);
        Assert.Contains("C:\\a.txt", linhas[1]);
        Assert.Contains("Removed", linhas[1]);
        Assert.Contains("32", linhas[2]);
    }

    [Fact]
    public void OCsvColocaEntreAspasOCampoComPontoEVirgula()
    {
        var report = new RemovalReport(new[]
        {
            Outcome(Arquivo("C:\\a;b.txt"), RemovalStatus.Removed, "removido"),
        });

        var csv = RemovalLog.ToCsv(RemovalLog.Build(report));

        Assert.Contains("\"C:\\a;b.txt\"", csv);
    }

    // ---------------------------------------------------------------------
    // JSON e gravacao
    // ---------------------------------------------------------------------

    [Fact]
    public void OJsonEValidoETemAsContagens()
    {
        var report = new RemovalReport(new[]
        {
            Outcome(Arquivo("C:\\a.txt"), RemovalStatus.Removed, "removido"),
            Outcome(Arquivo("C:\\b.txt"), RemovalStatus.Failed, "em uso", 32),
        });

        using var doc = JsonDocument.Parse(RemovalLog.ToJson(report, "MeuApp"));

        Assert.Equal("MeuApp", doc.RootElement.GetProperty("Programa").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("Removidos").GetInt32());
        Assert.Equal(1, doc.RootElement.GetProperty("Falharam").GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("Itens").GetArrayLength());
        Assert.Equal(32, doc.RootElement.GetProperty("Itens")[1].GetProperty("ErrorCode").GetInt32());
    }

    [Fact]
    public void GravarCriaOArquivoComOConteudoEsperado()
    {
        var sandbox = CreateSandbox();
        var destino = Path.Combine(sandbox, "log.json");

        var report = new RemovalReport(new[]
        {
            Outcome(Arquivo("C:\\a.txt"), RemovalStatus.Removed, "removido"),
        });

        var gravado = RemovalLog.Save(report, "MeuApp", destino);

        Assert.Equal(destino, gravado);
        Assert.True(File.Exists(destino));

        var texto = File.ReadAllText(destino);
        Assert.Contains("MeuApp", texto);

        // O caminho e conferido no JSON desserializado: no arquivo bruto a
        // barra invertida aparece escapada, e o teste passaria a medir a
        // forma da serializacao em vez do conteudo.
        using var doc = JsonDocument.Parse(texto);
        Assert.Equal("C:\\a.txt", doc.RootElement.GetProperty("Itens")[0].GetProperty("Path").GetString());
    }

    [Fact]
    public void GravarSemCaminhoUsaOPastaPadraoComNomeAutomatico()
    {
        var report = new RemovalReport(new[]
        {
            Outcome(Arquivo("C:\\a.txt"), RemovalStatus.Removed, "removido"),
        });

        var gravado = RemovalLog.Save(report, "MeuApp/Teste");

        try
        {
            Assert.True(File.Exists(gravado));
            Assert.StartsWith(RemovalLog.LogDirectory, gravado);

            // O nome do arquivo nao pode conter o separador de pasta do
            // programa: "MeuApp/Teste" viraria "MeuApp\Teste".
            Assert.DoesNotContain("/", Path.GetFileName(gravado));
            Assert.Contains("MeuApp-Teste", Path.GetFileName(gravado));
        }
        finally
        {
            try { if (File.Exists(gravado)) File.Delete(gravado); }
            catch { }
        }
    }

    /// <summary>
    /// A interface grafica e a CLI gravam o log: elas consomem o mesmo
    /// resultado do nucleo, e nao escrevem o log por conta propria.
    /// </summary>
    [Fact]
    public void AInterfaceEACliGravamOLogDeRemocao()
    {
        var wizard = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml.cs");
        var cli = RepoFiles.Read("src", "Tranal.Cli", "Program.cs");

        Assert.Contains("RemovalLog.Save(", wizard);
        Assert.Contains("RemovalLog.Save(", cli);
    }

    /// <summary>
    /// O codigo de erro do Windows tem de chegar ao log: e o numero que o
    /// usuario consegue pesquisar ("acesso negado" e 5, "arquivo em uso" e 32).
    /// </summary>
    [Fact]
    public void OCodigoDoWindowsChegaNoLog()
    {
        var code = Read("src", "Tranal.Uninstaller", "Core", "UninstallSupport.cs");

        Assert.Contains("RemovalOutcome.ErrorCodeOf(inner)", code);
        Assert.Contains("RemovalOutcome.ErrorCodeOf(ex)", code);
    }
}
