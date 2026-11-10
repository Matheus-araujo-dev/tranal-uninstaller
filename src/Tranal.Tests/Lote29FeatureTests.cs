using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Tranal.Cli;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Recovery;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O lote 29 da consumidor de verdade ao que ja existia e nao tinha dono.
///
/// <para>A varredura dos atalhos (<c>ShortcutDiscovery</c>), a remocao das
/// atualizacoes do Windows (<c>WindowsUpdateManager</c>) e o pacote portatil
/// (<c>PortableArchive</c> e <c>ProgramListImporter</c>) estavam prontos no
/// nucleo, e nenhuma linha do produto os chamava. Um modulo sem consumidor
/// nao conta: o codigo existe, mas a pessoa nunca chega nele.</para>
///
/// <para>A ligacao com a interface e conferida sobre o fonte, pelo mesmo
/// motivo das outras paridades: a suite nao referencia o projeto grafico
/// (WPF).</para>
/// </summary>
[Collection("preferencias")]
public class Lote29FeatureTests
{
    // ==================================================================
    // O modulo orfao: agora tem quem o chame
    // ==================================================================

    [Fact]
    public void O_modulo_da_varredura_dos_atalhos_tem_consumidor()
    {
        var tela = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Discovery", "DiscoveryWindow.xaml.cs");

        Assert.Contains("ShortcutDiscovery.Discover", tela);
        Assert.Contains("Discovery.Category.Shortcuts", tela);
    }

    [Fact]
    public void O_modulo_da_remocao_de_atualizacao_tem_consumidor()
    {
        var tela = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Discovery", "DiscoveryWindow.xaml.cs");
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Discovery", "DiscoveryWindow.xaml");

        Assert.Contains("WindowsUpdateManager.Remove", tela);
        Assert.Contains("DiscoveryRemoveUpdate", xaml);
    }

    [Fact]
    public void O_modulo_do_pacote_portatil_tem_consumidor()
    {
        var cli = RepoFiles.Read("src", "Tranal.Cli", "PortableCommands.cs");

        Assert.Contains("PortableArchive.Export", cli);
        Assert.Contains("PortableArchive.Import", cli);
        Assert.Contains("ProgramListImporter.Import", cli);
    }

    // ==================================================================
    // O icone do atalho
    // ==================================================================

    [Fact]
    public void Atalho_sem_icone_devolve_vazio()
    {
        Assert.Equal(string.Empty, ShortcutFixer.ReadIconLocation(null));
        Assert.Equal(string.Empty, ShortcutFixer.ReadIconLocation(string.Empty));
        Assert.Equal(string.Empty, ShortcutFixer.ReadIconLocation(@"C:\atalho-que-nao-existe.lnk"));
    }

    [Fact]
    public void O_icone_do_atalho_e_lido()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "tranal-lote29-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(pasta);

        var atalho = Path.Combine(pasta, "Teste.lnk");
        var alvo = Path.Combine(pasta, "alvo.exe");
        File.WriteAllText(alvo, "conteudo");

        try
        {
            // O mesmo Shell que o Explorador usa: e ele que grava o icone no
            // .lnk.
            dynamic? shell = null;
            dynamic? atalhoCom = null;
            try
            {
                var tipo = Type.GetTypeFromProgID("WScript.Shell");
                if (tipo is null) return; // sem Shell: nao ha o que medir

                shell = Activator.CreateInstance(tipo);
                if (shell is null) return;

                atalhoCom = shell.CreateShortcut(atalho);
                atalhoCom.TargetPath = alvo;
                atalhoCom.IconLocation = alvo + ",0";
                atalhoCom.Save();
            }
            finally
            {
                (atalhoCom as IDisposable)?.Dispose();
                (shell as IDisposable)?.Dispose();
            }

            var icone = ShortcutFixer.ReadIconLocation(atalho);

            Assert.False(string.IsNullOrWhiteSpace(icone));
            Assert.Contains("alvo.exe", icone, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { if (Directory.Exists(pasta)) Directory.Delete(pasta, true); } catch { }
        }
    }

    [Fact]
    public void A_varredura_dos_atalhos_conhece_as_pastas()
    {
        var locais = ShortcutDiscovery.Locations();

        Assert.Equal(4, locais.Count);
        foreach (var (location, folder) in locais)
        {
            Assert.False(string.IsNullOrWhiteSpace(location));
            Assert.False(string.IsNullOrWhiteSpace(folder));
        }
    }

    [Fact]
    public void A_varredura_dos_atalhos_devolve_itens_com_alvo()
    {
        // A varredura do desktop e do Menu Iniciar depende da maquina: o que
        // vale aqui e a REGRA (todo item tem alvo e icone lidos) e que a
        // varredura nunca estoura.
        var achados = ShortcutDiscovery.Discover(Array.Empty<InstalledProgram>());

        Assert.NotNull(achados);
        foreach (var achado in achados)
        {
            Assert.False(string.IsNullOrWhiteSpace(achado.TargetPath));
            Assert.False(string.IsNullOrWhiteSpace(achado.Name));
        }
    }

    // ==================================================================
    // A remocao da atualizacao do Windows
    // ==================================================================

    [Theory]
    [InlineData("KB5031356", "5031356")]
    [InlineData("5031356", "5031356")]
    [InlineData("kb5031356", "5031356")]
    [InlineData("KB5031356 ", "5031356")]
    public void O_numero_do_artigo_e_lido(string entrada, string esperado)
    {
        Assert.Equal(esperado, WindowsUpdateManager.NumeroDoArtigo(entrada));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("KB")]
    public void Artigo_invalido_nao_vira_numero(string? entrada)
    {
        Assert.Equal(string.Empty, WindowsUpdateManager.NumeroDoArtigo(entrada));
    }

    [Fact]
    public void Remover_artigo_invalido_e_recusado_sem_rodar_o_wusa()
    {
        var resultado = WindowsUpdateManager.Remove("nao-e-um-artigo");

        Assert.False(resultado.Success);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Message));
    }

    [Fact]
    public void Remover_varios_artigos_sem_lista_devolve_vazio()
    {
        Assert.Empty(WindowsUpdateManager.RemoveMany(null));
        Assert.Empty(WindowsUpdateManager.RemoveMany("   "));
    }

    [Fact]
    public void A_lista_de_atualizacoes_do_windows_nao_estoura()
    {
        var atualizacoes = WindowsUpdateManager.ListInstalled();

        Assert.NotNull(atualizacoes);
    }

    // ==================================================================
    // A linha de comando do pacote portatil
    // ==================================================================

    [Fact]
    public void A_cli_do_pacote_portatil_so_responde_ao_seu_comando()
    {
        Assert.Null(PortableCommands.TryRun(Array.Empty<string>(), new HashSet<string>()));

        var flags = new HashSet<string> { "/portable-export" };
        var codigo = PortableCommands.TryRun(new[] { "/portable-export" }, flags);

        // Sem o arquivo, o comando devolve o erro de uso (e nao 0).
        Assert.NotNull(codigo);
        Assert.NotEqual(0, codigo!.Value);
    }

    [Fact]
    public void A_cli_do_pacote_portatil_recusa_a_lista_inexistente()
    {
        var flags = new HashSet<string> { "/portable-import-list" };
        var codigo = PortableCommands.TryRun(
            new[] { "/portable-import-list", @"C:\lote29-lista-que-nao-existe.csv" }, flags);

        Assert.NotNull(codigo);
        Assert.NotEqual(0, codigo!.Value);
    }

    // ==================================================================
    // O centro de mudancas: o tipo "Programa" em producao
    // ==================================================================

    [Fact]
    public void O_backup_das_sobras_registra_a_remocao_no_centro_de_mudancas()
    {
        using var ambiente = new AmbienteDeRecuperacao();

        var arquivo = Path.Combine(ambiente.Raiz, "sobra.txt");
        File.WriteAllText(arquivo, "conteudo");

        var programa = new InstalledProgram { DisplayName = "Programa do Lote 29" };
        var itens = new[] { new LeftoverItem { Kind = "Arquivo", Path = arquivo } };

        var backup = UninstallEngine.BackupLeftovers(
            itens, programa.DisplayName, new UninstallRemovalOptions { Backup = true }, programa);

        Assert.True(backup.Ok);

        // A operacao entrou no registro: e o que o centro de mudancas le.
        Assert.Contains(ProgramBackup.Operations(), o => o.Id == backup.Id);

        // E o centro mostra a mudanca como do tipo "Programa".
        var mudancas = UndoChangesCenter.Scan();
        var linha = mudancas.Items.FirstOrDefault(m => m.Id == backup.Id);

        Assert.NotNull(linha);
        Assert.Equal("Programa", linha!.Kind);
        Assert.Equal("Programa do Lote 29", linha.ProgramName);
        Assert.Equal(1, linha.ItemCount);

        // O perfil do programa foi gravado: e o que diz o que ele deixou.
        var perfil = ProgramBackup.ListProfiles().FirstOrDefault(p => p.ProgramName == "Programa do Lote 29");

        Assert.NotNull(perfil);
        Assert.Contains(arquivo, perfil!.Files);
    }

    [Fact]
    public void Desfazer_devolve_o_conteudo_e_refazer_apaga_de_novo()
    {
        using var ambiente = new AmbienteDeRecuperacao();

        var arquivo = Path.Combine(ambiente.Raiz, "sobra.txt");
        File.WriteAllText(arquivo, "conteudo");

        var programa = new InstalledProgram { DisplayName = "Programa do Lote 29" };
        var itens = new[] { new LeftoverItem { Kind = "Arquivo", Path = arquivo } };

        var backup = UninstallEngine.BackupLeftovers(
            itens, programa.DisplayName, new UninstallRemovalOptions { Backup = true }, programa);

        Assert.True(backup.Ok);

        // A remocao: o conteudo sai do disco.
        File.Delete(arquivo);
        Assert.False(File.Exists(arquivo));

        // Desfazer: o conteudo volta e a mudanca fica marcada.
        var (ok, mensagem) = UndoChangesCenter.Undo(backup.Id);

        Assert.True(ok);
        Assert.True(File.Exists(arquivo));

        // A operacao ficou marcada como desfeita: e o que o centro mostra.
        var operacoes = ProgramBackup.Operations();
        Assert.Contains(operacoes, o => o.Id == backup.Id);
        Assert.True(operacoes.First(o => o.Id == backup.Id).Undone);

        var depois = UndoChangesCenter.Scan().Items.FirstOrDefault(m => m.Id == backup.Id);
        Assert.NotNull(depois);
        Assert.True(depois!.Undone);

        // Refazer: o conteudo sai de novo. O perfil continua: a mudanca ainda
        // pode ser desfeita outra vez.
        var (okRedo, _) = UndoChangesCenter.Redo(backup.Id);

        Assert.True(okRedo);
        Assert.False(File.Exists(arquivo));
        Assert.Contains(ProgramBackup.ListProfiles(), p => p.ProgramName == "Programa do Lote 29");

        // Quando o snapshot expira, o conteudo guardado vai embora: a
        // mudanca e o perfil sao esquecidos, porque nao ha mais o que desfazer.
        Envelhecer(backup.Id, DateTime.UtcNow.AddDays(-40));
        var apagados = LeftoverBackup.Expire(30);

        Assert.Equal(1, apagados);
        Assert.DoesNotContain(ProgramBackup.Operations(), o => o.Id == backup.Id);
        Assert.DoesNotContain(ProgramBackup.ListProfiles(), p => p.ProgramName == "Programa do Lote 29");
        Assert.DoesNotContain(UndoChangesCenter.Scan().Items, m => m.Id == backup.Id);
    }

    [Fact]
    public void O_registro_recusa_o_backup_que_falhou()
    {
        using var ambiente = new AmbienteDeRecuperacao();

        var falhou = new LeftoverBackupResult { Ok = false, Message = "nao ha sobras para guardar" };

        Assert.False(ProgramBackup.Register(falhou, null, "Programa do Lote 29", null));
        Assert.Empty(ProgramBackup.Operations());
    }

    [Fact]
    public void A_tela_e_a_cli_do_centro_de_mudancas_mostram_o_perfil()
    {
        var tela = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Tools", "ToolViews.cs");
        var cli = RepoFiles.Read("src", "Tranal.Cli", "AssociationCommands.cs");

        Assert.Contains("UndoChangesCenter.Scan", tela);
        Assert.Contains("Tool.UndoCenter.Profile", tela);
        Assert.Contains("UndoChangesCenter.Scan", cli);
        Assert.Contains("perfil:", cli);
    }

    // ==================================================================
    // O catalogo de idiomas
    // ==================================================================

    [Fact]
    public void As_chaves_da_descoberta_estao_nos_dois_idiomas()
    {
        var chaves = new[]
        {
            "Discovery.Category.Shortcuts",
            "Discovery.Update.Select",
            "Discovery.Update.Working",
            "Discovery.Update.Removed",
            "Discovery.Update.Failed",
            "Tool.UndoCenter.Profile",
        };

        foreach (var chave in chaves)
        {
            Assert.True(Loc.HasKey(chave) || Loc.Get(chave) != chave, "falta a chave " + chave);
        }
    }

    /// <summary>
    /// Envelhece um backup: e o que a validade do snapshot compara.
    /// </summary>
    /// <param name="id">O identificador do backup.</param>
    /// <param name="quando">A data de criacao nova.</param>
    private static void Envelhecer(string id, DateTime quando)
    {
        var manifesto = Path.Combine(LeftoverBackup.Folder, id, "manifest.json");
        var texto = File.ReadAllText(manifesto);
        var obj = JsonSerializer.Deserialize<LeftoverBackupManifest>(texto)!;

        obj.CreatedUtc = quando;

        File.WriteAllText(manifesto, JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        }));
    }

    /// <summary>
    /// Um ambiente de recuperacao isolado.
    ///
    /// As pastas de backup sao trocadas pela pasta temporaria do teste: o
    /// teste nao escreve no computador de quem roda a suite, e o que ele
    /// grava some junto com a pasta.
    /// </summary>
    private sealed class AmbienteDeRecuperacao : IDisposable
    {
        private readonly string _sobrasAntigas;
        private readonly string? _programasAntigos;

        /// <summary>Cria o ambiente e aponta as pastas de backup para ele.</summary>
        public AmbienteDeRecuperacao()
        {
            Raiz = Path.Combine(
                Path.GetTempPath(),
                "lote29-recuperacao-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(Raiz);

            _sobrasAntigas = LeftoverBackup.FolderOverride ?? string.Empty;
            _programasAntigos = ProgramBackup.FolderOverride;

            LeftoverBackup.FolderOverride = Path.Combine(Raiz, "sobras");
            ProgramBackup.FolderOverride = Path.Combine(Raiz, "programas");
        }

        /// <summary>A pasta temporaria do teste.</summary>
        public string Raiz { get; }

        /// <summary>Devolve as pastas de backup e apaga a pasta temporaria.</summary>
        public void Dispose()
        {
            LeftoverBackup.FolderOverride = _sobrasAntigas.Length > 0 ? _sobrasAntigas : null;
            ProgramBackup.FolderOverride = _programasAntigos;

            try { if (Directory.Exists(Raiz)) Directory.Delete(Raiz, true); }
            catch (IOException) { /* a pasta temporaria fica para o Windows limpar */ }
        }
    }
}
