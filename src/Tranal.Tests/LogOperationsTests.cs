using System.Text.Json;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Modules;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Testes das operacoes sobre os logs de instalacao rastreada:
/// renomear, mesclar, editar entrada a entrada, ignorar e remover.
///
/// Os testes rodam com a base de logs apontada para uma pasta temporaria:
/// a suite nao pode mexer no %LOCALAPPDATA% de quem a roda.
/// </summary>
public sealed class LogOperationsTests : IDisposable
{
    private readonly string _pasta;

    public LogOperationsTests()
    {
        _pasta = Path.Combine(Path.GetTempPath(), "tranal-logs-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_pasta);
        TraceStore.DirectoryOverride = _pasta;
    }

    public void Dispose()
    {
        // A base de logs volta a apontar para uma pasta temporaria: uma
        // operacao disparada depois deste teste nao pode cair na base real
        // de quem roda a suite.
        TraceStore.ResetForTests();

        try
        {
            if (System.IO.Directory.Exists(_pasta))
                System.IO.Directory.Delete(_pasta, recursive: true);
        }
        catch
        {
            // O temporario e descartavel: falhar ao apagar nao estraga o teste.
        }
    }

    [Fact]
    public void AsEntradasTrazemTipoCaminhoOperacaoEConfianca()
    {
        var log = CriarLog("Meu App", new Dictionary<string, object>
        {
            ["files"] = new[] { new { path = @"C:\Program Files\Meu App\app.exe", sizeBytes = 10, modifiedUtc = "2026-11-05T10:22:30.0000000Z" } },
            ["folders"] = new[] { @"C:\Program Files\Meu App" },
            ["registryKeys"] = new[] { new { path = @"HKEY_CURRENT_USER\Software\MeuApp" } },
        });

        var entradas = LogOperations.Entries(log);

        Assert.Equal(3, entradas.Count);

        var arquivo = entradas.Single(e => e.Kind == LogEntryKind.File && !e.IsFolder);
        Assert.Equal(@"C:\Program Files\Meu App\app.exe", arquivo.Path);
        Assert.False(arquivo.IsFolder);
        Assert.Equal(LogEntryOperation.Created, arquivo.Operation);
        Assert.Equal(LeftoverConfidence.High, arquivo.Confidence);
        Assert.False(arquivo.Ignored);

        var pasta = entradas.Single(e => e.IsFolder);
        Assert.Equal(@"C:\Program Files\Meu App", pasta.Path);

        var chave = entradas.Single(e => e.Kind == LogEntryKind.RegistryKey);
        Assert.Equal(@"HKEY_CURRENT_USER\Software\MeuApp", chave.Path);
    }

    [Fact]
    public void RenomearMudaONomeDoLogEGravaOArquivo()
    {
        var log = CriarLog("Nome Antigo", new Dictionary<string, object>
        {
            ["files"] = new[] { new { path = @"C:\App\app.exe" } },
        });

        Assert.True(LogOperations.Rename(log, "Nome Novo"));

        var relido = TraceStore.Get(log.Id);
        Assert.NotNull(relido);
        Assert.Equal("Nome Novo", relido!.ProgramName);

        // O conteudo do log continua o que era.
        Assert.Single(LogOperations.Entries(relido));
    }

    [Fact]
    public void EditarEntradaMudaOCaminhoEPersiste()
    {
        var log = CriarLog("Meu App", new Dictionary<string, object>
        {
            ["files"] = new[] { new { path = @"C:\Antigo\app.exe" } },
        });

        var entrada = LogOperations.Entries(log).Single();

        Assert.True(LogOperations.EditEntry(log, new LogEntryEdit
        {
            Id = entrada.Id,
            Path = @"C:\Novo\app.exe",
        }));

        var relido = LogOperations.Entries(log).Single();
        Assert.Equal(@"C:\Novo\app.exe", relido.Path);
    }

    [Fact]
    public void RemoverEntradaTiraOItemDoLog()
    {
        var log = CriarLog("Meu App", new Dictionary<string, object>
        {
            ["files"] = new[] { new { path = @"C:\App\app.exe" }, new { path = @"C:\App\outro.exe" } },
        });

        var entrada = LogOperations.Entries(log).First(e => e.Path.EndsWith("app.exe", StringComparison.OrdinalIgnoreCase));

        Assert.True(LogOperations.RemoveEntry(log, entrada));

        var restantes = LogOperations.Entries(log);
        Assert.Single(restantes);
        Assert.DoesNotContain(restantes, e => e.Path == entrada.Path);
    }

    [Fact]
    public void IgnorarEntradaMantemNoLogMasMarcaComoIgnorada()
    {
        var log = CriarLog("Meu App", new Dictionary<string, object>
        {
            ["files"] = new[] { new { path = @"C:\App\app.exe" } },
        });

        var entrada = LogOperations.Entries(log).Single();

        Assert.True(LogOperations.IgnoreEntry(log, entrada));

        var relida = LogOperations.Entries(log).Single();
        Assert.True(relida.Ignored);
        Assert.Equal(@"C:\App\app.exe", relida.Path);
    }

    [Fact]
    public void MesclarJuntaOsLogsEGuardaAOrigemDeCadaEntrada()
    {
        var primeiro = CriarLog("App Um", new Dictionary<string, object>
        {
            ["files"] = new[] { new { path = @"C:\Um\um.exe" } },
        });

        var segundo = CriarLog("App Dois", new Dictionary<string, object>
        {
            ["files"] = new[] { new { path = @"C:\Dois\dois.exe" } },
        });

        var mesclado = LogOperations.Merge(new[] { primeiro, segundo }, "App Mesclado");

        Assert.NotNull(mesclado);
        Assert.Equal("App Mesclado", mesclado!.ProgramName);

        var entradas = LogOperations.Entries(mesclado);
        Assert.Equal(2, entradas.Count);

        var doUm = entradas.Single(e => e.Path.Contains("um.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("App Um", doUm.Origin);

        var doDois = entradas.Single(e => e.Path.Contains("dois.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("App Dois", doDois.Origin);
    }

    [Fact]
    public void ARevisaoDoCustomUninstallDecideItemAItem()
    {
        var log = CriarLog("Meu App", new Dictionary<string, object>
        {
            ["files"] = new[] { new { path = @"C:\App\app.exe" }, new { path = @"C:\App\config.ini" } },
            ["registryKeys"] = new[] { new { path = @"HKEY_CURRENT_USER\Software\MeuApp" } },
        });

        var entradas = LogOperations.Entries(log);
        var ignorada = entradas.Single(e => e.Path.EndsWith("config.ini", StringComparison.OrdinalIgnoreCase));
        Assert.True(LogOperations.IgnoreEntry(log, ignorada));

        var revisao = CustomUninstallReviewBuilder.Build(log);

        Assert.Equal(3, revisao.Items.Count);
        Assert.Equal(2, revisao.RemoveCount);
        Assert.Equal(1, revisao.KeepCount);

        var mantida = revisao.Items.Single(i => i.Entry.Id == ignorada.Id);
        Assert.Equal(CustomUninstallAction.Keep, mantida.Action);
        Assert.False(mantida.WillRemove);
        Assert.NotEmpty(mantida.Reason);

        // O plano de remocao leva os itens marcados, no formato canonico.
        Assert.Equal(2, revisao.Leftovers.Count);
        Assert.Contains(revisao.Leftovers, l => l.Kind == "Arquivo" && l.Path == @"C:\App\app.exe");
        Assert.Contains(revisao.Leftovers, l => l.Kind == "Registro");
    }

    [Fact]
    public void ARevisaoAvancadaDevolveOQueFicouForaDoLog()
    {
        var log = CriarLog("Meu App", new Dictionary<string, object>
        {
            ["files"] = new[] { new { path = @"C:\App\app.exe" } },
            ["folders"] = new[] { @"C:\App" },
        });

        var candidatos = new[]
        {
            new LeftoverItem { Kind = "Arquivo", Path = @"C:\App\app.exe" },
            new LeftoverItem { Kind = "Arquivo", Path = @"C:\App\interno\extra.exe" },
            new LeftoverItem { Kind = "Pasta", Path = @"C:\OutroPrograma" },
        };

        var revisao = AdvancedUninstallReviewBuilder.Build(log, candidatos);

        // O que o log registra nao e sobra.
        Assert.Single(revisao.Logged);
        Assert.Equal(@"C:\App\app.exe", revisao.Logged[0].Path);

        // O que nao esta no log volta para o usuario decidir.
        Assert.Equal(2, revisao.UnloggedCount);

        var dentro = revisao.Unlogged.Single(c => c.Item.Path.Contains("extra.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(AdvancedUninstallReason.InsideLoggedItem, dentro.Reason);

        var fora = revisao.Unlogged.Single(c => c.Item.Path.Contains("OutroPrograma", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(AdvancedUninstallReason.NotLogged, fora.Reason);
        Assert.NotEmpty(fora.Detail);
    }

    /// <summary>
    /// Grava um log de verdade na base de logs de teste, pelo proprio
    /// TraceStore — e o formato que a suite ja valida.
    /// </summary>
    private static TracedInstall CriarLog(string nome, Dictionary<string, object> payload)
    {
        var id = Guid.NewGuid().ToString("D");
        var install = new TracedInstall
        {
            Id = id,
            ProgramName = nome,
            Timestamp = DateTime.UtcNow,
            InstallerPath = @"C:\Temp\setup.exe",
            LogPath = TraceStore.PathForId(id),
        };

        var json = JsonSerializer.SerializeToElement(payload);
        var caminho = TraceStore.Save(install, json);
        Assert.NotEqual(string.Empty, caminho);

        return new TracedInstall
        {
            Id = id,
            ProgramName = nome,
            Timestamp = install.Timestamp,
            InstallerPath = install.InstallerPath,
            LogPath = caminho,
        };
    }
}
