using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Tranal.Cli;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Reporting;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O lote 31: o gerenciador do log de rastreio.
///
/// <para>O log de instalacao e a fonte do que o instalador gravou no
/// computador — e e ele que a desinstalacao dirigida pelo log remove. O
/// <c>TraceLogEditor</c> ja sabia acrescentar e excluir um item do log, mas
/// **ninguem chamava**: a pessoa nao tinha como corrigir o log antes da
/// remocao.</para>
///
/// <para>O lote tambem fechou uma duplicacao: a gravacao do arquivo de log
/// existia em dois lugares (o editor e o <c>TraceStore</c>). Agora ela mora em
/// um lugar so, e e atomica em toda escrita.</para>
///
/// <para>Os testes rodam com a base de logs apontada para uma pasta
/// temporaria: a suite nao pode mexer no %LOCALAPPDATA% de quem a roda.</para>
/// </summary>
public class Lote31FeatureTests : IDisposable
{
    private readonly string _pasta;

    public Lote31FeatureTests()
    {
        _pasta = Path.Combine(Path.GetTempPath(), "tranal-lote31-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_pasta);
        TraceStore.DirectoryOverride = _pasta;
    }

    public void Dispose()
    {
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

    // ==================================================================
    // O gerenciador
    // ==================================================================

    [Fact]
    public void O_gerenciador_acrescenta_arquivo_pasta_e_chave()
    {
        var log = CriarLog("Meu App");

        var comArquivo = TraceLogEditor.AddFile(log, @"C:\Program Files\Meu App\app.exe", 2048);
        Assert.NotNull(comArquivo);

        var comPasta = TraceLogEditor.AddFolder(comArquivo!, @"C:\Program Files\Meu App");
        Assert.NotNull(comPasta);

        var comChave = TraceLogEditor.AddRegistryKey(comPasta!, @"HKEY_CURRENT_USER\Software\MeuApp");
        Assert.NotNull(comChave);

        var entradas = LogOperations.Entries(comChave!);

        Assert.Single(entradas, e => e.Kind == LogEntryKind.File && !e.IsFolder);
        Assert.Single(entradas, e => e.IsFolder);
        Assert.Single(entradas, e => e.Kind == LogEntryKind.RegistryKey);

        // E o item acrescentado a mao entrou no log gravado (nao so na
        // memoria): quem le o log em outro processo ve o mesmo.
        var relido = TraceStore.Get(log.Id);
        Assert.NotNull(relido);
        Assert.Equal(3, LogOperations.Entries(relido!).Count);
    }

    [Fact]
    public void O_gerenciador_exclui_o_item_do_log()
    {
        var log = CriarLog("Meu App");

        var comArquivo = TraceLogEditor.AddFile(log, @"C:\Program Files\Meu App\app.exe");
        Assert.NotNull(comArquivo);

        var semArquivo = TraceLogEditor.Remove(comArquivo!, @"C:\Program Files\Meu App\app.exe");
        Assert.NotNull(semArquivo);
        Assert.Equal(0, semArquivo!.FileCount);

        var relido = TraceStore.Get(log.Id);
        Assert.NotNull(relido);
        Assert.Empty(LogOperations.Entries(relido!));
    }

    [Fact]
    public void O_item_acrescentado_a_mao_nao_confia_como_o_rastreado()
    {
        // MEDIDO (10/11/2026): o item que a pessoa acrescenta a mao nao foi
        // medido por ninguem — a confianca dele e media, nao alta. Dizer que
        // o item foi "medido" seria mentira.
        var log = CriarLog("Meu App");

        var comArquivo = TraceLogEditor.AddFile(log, @"C:\Program Files\Meu App\app.exe");
        Assert.NotNull(comArquivo);

        var entrada = LogOperations.Entries(comArquivo!).Single();
        Assert.Equal(LeftoverConfidence.Medium, entrada.Confidence);
        Assert.True(entrada.Added);
    }

    [Fact]
    public void A_gravacao_do_log_e_atomica_e_guarda_a_copia()
    {
        var log = CriarLog("Meu App");

        var editado = TraceLogEditor.AddFolder(log, @"C:\Program Files\Meu App");
        Assert.NotNull(editado);

        // O log anterior fica ao lado, e o temporario nao sobra.
        Assert.True(File.Exists(log.LogPath + ".bak"));
        Assert.False(File.Exists(log.LogPath + ".tmp"));
    }

    [Fact]
    public void A_escrita_do_log_esta_em_um_lugar_so()
    {
        // MEDIDO (10/11/2026): a gravacao do arquivo de log existia em dois
        // lugares — o editor (com copia .bak) e o TraceStore (sem copia).
        // Quem escrevesse pelo TraceStore perdia a copia; quem escrevesse pelo
        // editor nao atualizava as contagens do envelope.
        var editor = RepoFiles.Read(
            "src", "Tranal.Uninstaller", "Core", "Reporting", "TraceLogExporter.cs");
        var store = RepoFiles.Read(
            "src", "Tranal.Uninstaller", "Core", "Modules", "TraceStore.cs");

        // A gravacao mora no TraceStore.
        Assert.Contains("GravarAtomicamente", store);
        Assert.DoesNotContain("GravarAtomicamente", editor);

        // E o editor nao escreve o arquivo por conta propria: ele le pelo
        // TraceStore (que e quem sabe o formato) e mudanca o conteudo pelo
        // LogOperations (que e quem sabe o que e uma entrada).
        Assert.Contains("TraceStore.LoadPayload", editor);
        Assert.Contains("TraceStore.ReadPayload", editor);
        Assert.DoesNotContain("File.WriteAllText(caminho", editor);

        // A regra do que muda no log e do LogOperations.
        Assert.Contains("LogOperations.AddEntry", editor);
        Assert.Contains("LogOperations.RemoveEntry", editor);
        Assert.DoesNotContain("LogOperations.AddEntry", store);
    }

    [Fact]
    public void A_tela_do_gerenciador_tem_os_botoes_e_o_modulo_chama_ela()
    {
        var tela = RepoFiles.Read(
            "src", "Tranal.Uninstaller", "Ui", "Tracing", "TraceManagerWindow.xaml.cs");
        var xaml = RepoFiles.Read(
            "src", "Tranal.Uninstaller", "Ui", "Tracing", "TraceManagerWindow.xaml");
        var modulo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Modules", "ModuleViews.cs");
        var janela = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "TracedLogWindow.xaml");

        // A tela existe e tem o que a pessoa precisa.
        Assert.Contains("TraceLogEditor.AddFile", tela);
        Assert.Contains("TraceLogEditor.Remove", tela);
        Assert.Contains("TraceManagerItems", xaml);
        Assert.Contains("TraceManagerAdd", xaml);
        Assert.Contains("TraceManagerRemove", xaml);
        Assert.Contains("TraceManagerPath", xaml);
        Assert.Contains("TraceManagerKind", xaml);

        // E ela tem dois chamadores: o modulo da base de logs e a janela do
        // proprio log.
        Assert.Contains("LogsManager", modulo);
        Assert.Contains("TraceManagerWindow", modulo);
        Assert.Contains("TracedLogManager", janela);
    }

    [Fact]
    public void A_linha_de_comando_acrescenta_e_exclui_do_log()
    {
        var log = CriarLog("Meu App");
        var caminho = @"C:\Program Files\Meu App\app.exe";

        var flags = new HashSet<string> { "/log-add" };
        var codigo = LogCommands.TryRun(
            new[] { "/log-add", log.Id, "/caminho", caminho, "/tipo", "arquivo" }, flags);

        Assert.NotNull(codigo);
        Assert.Equal(0, codigo!.Value);

        var relido = TraceStore.Get(log.Id);
        Assert.NotNull(relido);
        Assert.Single(LogOperations.Entries(relido!));

        var flagsDrop = new HashSet<string> { "/log-drop" };
        var codigoDrop = LogCommands.TryRun(
            new[] { "/log-drop", log.Id, "/caminho", caminho }, flagsDrop);

        Assert.NotNull(codigoDrop);
        Assert.Equal(0, codigoDrop!.Value);

        var depois = TraceStore.Get(log.Id);
        Assert.NotNull(depois);
        Assert.Empty(LogOperations.Entries(depois!));
    }

    [Fact]
    public void A_linha_de_comando_recusa_o_uso_errado()
    {
        var flags = new HashSet<string> { "/log-add" };

        // Sem o caminho: o comando recusa (e nao devolve 0).
        var codigo = LogCommands.TryRun(new[] { "/log-add", "qualquer" }, flags);
        Assert.NotNull(codigo);
        Assert.NotEqual(0, codigo!.Value);

        // Sem o log: tambem recusa.
        var codigo2 = LogCommands.TryRun(
            new[] { "/log-add", "log-que-nao-existe", "/caminho", @"C:\x" }, flags);
        Assert.NotNull(codigo2);
        Assert.NotEqual(0, codigo2!.Value);
    }

    [Fact]
    public void As_chaves_do_gerenciador_estao_nos_dois_idiomas()
    {
        var chaves = new[]
        {
            "Mod.TraceManager.Title",
            "Mod.TraceManager.Hint",
            "Mod.TraceManager.Add",
            "Mod.TraceManager.Remove",
            "Mod.TraceManager.Kind",
            "Mod.TraceManager.Path",
            "Mod.TraceManager.Items",
            "Mod.TraceManager.Added",
            "Mod.TraceManager.Removed",
            "Mod.TraceManager.Failed",
            "Mod.TraceManager.NeedPath",
            "Mod.TraceManager.NeedItem",
            "Mod.TraceManager.Kind.File",
            "Mod.TraceManager.Kind.Folder",
            "Mod.TraceManager.Kind.RegistryKey",
            "Mod.TraceManager.State.Log",
            "Mod.TraceManager.State.Ignored",
            "Mod.TraceManager.State.Added",
            "Mod.Logs.Manager",
        };

        foreach (var chave in chaves)
        {
            Assert.True(Loc.HasKey(chave) || Loc.Get(chave) != chave, "falta a chave " + chave);
        }
    }

    /// <summary>
    /// Grava um log de verdade na base de logs de teste, pelo proprio
    /// TraceStore — e o formato que a suite ja valida.
    /// </summary>
    private static TracedInstall CriarLog(string nome)
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

        var payload = new Dictionary<string, object>
        {
            ["files"] = Array.Empty<object>(),
            ["folders"] = Array.Empty<object>(),
            ["registryKeys"] = Array.Empty<object>(),
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
