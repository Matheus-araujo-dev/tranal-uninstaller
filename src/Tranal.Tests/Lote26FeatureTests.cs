using System;
using System.Linq;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Management;using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.RegistryEditing;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O lote 26 fecha a remocao de entradas da lista do Windows: a acao em
/// lote (GEEK T1#10 "Batch Remove Entries", HIBIT #3, WISE T1#8 e
/// UNINSTALLTOOL #8 "Remove entry").
///
/// A REGRA mora no nucleo (<c>Core/Management/WindowsListEntry.cs</c> e
/// <c>Core/RegistryEditing/RegistryBatchRemover.cs</c>) e a suite testa
/// esses modulos. A ligacao com a interface e conferida sobre o fonte, pelo
/// mesmo motivo das outras paridades: a suite nao referencia o projeto
/// grafico (WPF).
///
/// Os testes tocam o registro de verdade, mas so em chaves de teste do
/// HKCU, criadas e apagadas pelo proprio teste. Nenhum software instalado
/// da maquina e alcançado.
/// </summary>
public class Lote26FeatureTests
{
    private const string BaseRelativa = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    /// <summary>
    /// Cria uma entrada de desinstalacao de teste no HKCU e devolve o caminho
    /// completo da chave.
    /// </summary>
    private static string CriaEntrada(string nome)
    {
        using var chave = Registry.CurrentUser.CreateSubKey($@"{BaseRelativa}\{nome}", writable: true);
        chave!.SetValue("DisplayName", nome);
        return $@"HKEY_CURRENT_USER\{BaseRelativa}\{nome}";
    }

    /// <summary>True quando a chave de teste ainda existe.</summary>
    private static bool Existe(string nome)
    {
        using var chave = Registry.CurrentUser.OpenSubKey($@"{BaseRelativa}\{nome}");
        return chave is not null;
    }

    /// <summary>Apaga a chave de teste, se ela sobrou.</summary>
    private static void ApagaEntrada(string nome)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"{BaseRelativa}\{nome}", throwOnMissingSubKey: false);
        }
        catch (UnauthorizedAccessException)
        {
            // A chave e de teste e fica no HKCU do usuario que roda a suite:
            // quando ela nao pode ser apagada, o proximo teste nao a encontra.
        }
    }

    /// <summary>Uma entrada de programa que aponta para a chave de teste.</summary>
    private static InstalledProgram Entrada(string nome, string caminho) => new()
    {
        DisplayName = nome,
        RegistryPath = caminho,
    };

    // ==================================================================
    // A remocao em lote
    // ==================================================================

    [Fact]
    public void O_lote_remove_a_entrada_do_programa()
    {
        var nome = "TranalLote26_Um";
        var caminho = CriaEntrada(nome);

        try
        {
            var relatorio = WindowsListEntry.RemoveMany(new[] { Entrada("Programa Um", caminho) });

            Assert.Equal(1, relatorio.RemovedCount);
            Assert.Equal(0, relatorio.FailedCount);
            Assert.False(Existe(nome));
        }
        finally
        {
            ApagaEntrada(nome);
        }
    }

    [Fact]
    public void A_entrada_removida_sai_no_relatorio_com_o_nome_do_programa()
    {
        var nome = "TranalLote26_Dois";
        var caminho = CriaEntrada(nome);

        try
        {
            var relatorio = WindowsListEntry.RemoveMany(new[] { Entrada("Programa Dois", caminho) });

            var saida = Assert.Single(relatorio.Outcomes);
            Assert.Equal(RemovalStatus.Removed, saida.Status);
            Assert.Contains("Programa Dois", saida.Item.Detail);
            Assert.Equal("Registro", saida.Item.Kind);
        }
        finally
        {
            ApagaEntrada(nome);
        }
    }

    [Fact]
    public void Varias_entradas_saem_no_mesmo_lote()
    {
        var um = "TranalLote26_LoteA";
        var dois = "TranalLote26_LoteB";

        try
        {
            var relatorio = WindowsListEntry.RemoveMany(new[]
            {
                Entrada("Lote A", CriaEntrada(um)),
                Entrada("Lote B", CriaEntrada(dois)),
            });

            Assert.Equal(2, relatorio.RemovedCount);
            Assert.False(Existe(um));
            Assert.False(Existe(dois));
        }
        finally
        {
            ApagaEntrada(um);
            ApagaEntrada(dois);
        }
    }

    [Fact]
    public void Entrada_sem_chave_conhecida_vira_falha_e_nao_excecao()
    {
        var relatorio = WindowsListEntry.RemoveMany(new[]
        {
            new InstalledProgram { DisplayName = "Sem Chave", RegistryPath = string.Empty },
        });

        Assert.Equal(1, relatorio.FailedCount);
        Assert.Equal(0, relatorio.RemovedCount);
    }

    [Fact]
    public void Entrada_protegida_nao_e_removida()
    {
        // O nome do ultimo segmento e o que a regra de seguranca olha:
        // "System" e um nome protegido.
        var nome = "System";
        var caminho = CriaEntrada(nome);

        try
        {
            var relatorio = WindowsListEntry.RemoveMany(new[] { Entrada("Protegido", caminho) });

            Assert.Equal(1, relatorio.ProtectedCount);
            Assert.Equal(0, relatorio.RemovedCount);
            Assert.True(Existe(nome));
        }
        finally
        {
            ApagaEntrada(nome);
        }
    }

    [Fact]
    public void Entrada_que_nao_existe_mais_vira_ja_inexistente()
    {
        var relatorio = WindowsListEntry.RemoveMany(new[]
        {
            Entrada("Sumido", @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\TranalLote26_NaoExiste"),
        });

        Assert.Equal(1, relatorio.AbsentCount);
        Assert.Equal(0, relatorio.RemovedCount);
    }

    // ==================================================================
    // Uma so regra para a acao avulsa e para o lote
    // ==================================================================

    [Fact]
    public void A_acao_avulsa_usa_a_mesma_regra_do_lote()
    {
        var nome = "TranalLote26_Avulsa";
        var caminho = CriaEntrada(nome);

        try
        {
            Assert.True(WindowsListEntry.Remove(Entrada("Programa Avulso", caminho)));
            Assert.False(Existe(nome));
        }
        finally
        {
            ApagaEntrada(nome);
        }
    }

    [Fact]
    public void A_acao_avulsa_devolve_falso_quando_a_entrada_nao_existe()
    {
        Assert.False(WindowsListEntry.Remove(null));
        Assert.False(WindowsListEntry.Remove(
            new InstalledProgram { DisplayName = "Sem Chave", RegistryPath = string.Empty }));
    }

    [Fact]
    public void A_regra_da_remocao_de_entrada_mora_em_um_lugar_so()
    {
        var entrada = RepoFiles.Read("src", "Tranal.Uninstaller", "Core", "Management", "WindowsListEntry.cs");

        // A acao avulsa e a acao em lote passam pelo mesmo codigo.
        Assert.Contains("RegistryBatchRemover.Remove(alvos)", entrada);
        Assert.DoesNotContain("RegistryEditor.DeleteKey", entrada);
    }

    // ==================================================================
    // O alvo
    // ==================================================================

    [Fact]
    public void O_alvo_preserva_o_hive_e_a_visao_do_registro_do_programa()
    {
        var programa = new InstalledProgram
        {
            DisplayName = "Programa",
            RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Exemplo",
            View = RegistryView.Registry32,
        };

        var alvo = RegistryTarget.FromProgram(programa);

        Assert.NotNull(alvo);
        Assert.Equal(RegistryHive.LocalMachine, alvo!.Hive);
        Assert.Equal(RegistryView.Registry32, alvo.View);
        Assert.Equal(@"SOFTWARE\Exemplo", alvo.RelativePath);
        Assert.Equal("Programa", alvo.Label);
    }

    [Fact]
    public void O_alvo_do_programa_usa_a_chave_do_hkcu()
    {
        var programa = new InstalledProgram
        {
            DisplayName = "Programa",
            RegistryPath = @"HKEY_CURRENT_USER\Software\Exemplo",
        };

        var alvo = RegistryTarget.FromProgram(programa);

        Assert.NotNull(alvo);
        Assert.Equal(RegistryHive.CurrentUser, alvo!.Hive);
    }

    [Fact]
    public void Programa_sem_chave_nao_vira_alvo()
    {
        Assert.Null(RegistryTarget.FromProgram(null));
        Assert.Null(RegistryTarget.FromProgram(new InstalledProgram { RegistryPath = string.Empty }));
    }

    // ==================================================================
    // A ligacao com a interface e com a CLI
    // ==================================================================

    [Fact]
    public void A_interface_tem_a_acao_em_lote()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        Assert.Contains("CmdRemoveEntriesBatch", codigo);
        Assert.Contains("WindowsListEntry.RemoveMany", codigo);

        // A confirmacao vem antes da remocao, e diz que nada e desinstalado.
        var metodo = RepoFiles.Metodo(codigo, "RemoveEntriesBatch");
        Assert.Contains("UserMessages.Confirm", metodo);
        Assert.Contains("Cmd.RemoveEntriesBatchConfirm", metodo);
        Assert.True(
            metodo.IndexOf("UserMessages.Confirm", StringComparison.Ordinal)
            < metodo.IndexOf("WindowsListEntry.RemoveMany", StringComparison.Ordinal),
            "a confirmacao precisa vir antes da remocao");
    }

    [Fact]
    public void A_cli_tem_o_comando_de_remover_entradas()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Cli", "EntryCommands.cs");

        Assert.Contains("/remove-entries", codigo);
        Assert.Contains("WindowsListEntry.RemoveMany", codigo);
    }

    [Fact]
    public void A_cli_avisa_que_o_programa_nao_e_desinstalado()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Cli", "EntryCommands.cs");

        Assert.Contains("o programa NAO e desinstalado", codigo);
    }

    [Fact]
    public void Os_dois_catalogos_trazem_as_chaves_da_remocao_em_lote()
    {
        foreach (var idioma in new[] { "pt-BR", "en-US" })
        {
            var catalogo = Loc.Load(idioma);

            foreach (var chave in new[]
            {
                "Cmd.RemoveEntriesBatch",
                "Cmd.RemoveEntriesBatchWithCount",
                "Cmd.RemoveEntriesBatchConfirm",
                "Cmd.RemoveEntriesBatchDone",
            })
            {
                Assert.True(catalogo.ContainsKey(chave), $"{idioma} sem a chave {chave}");
                Assert.False(string.IsNullOrWhiteSpace(catalogo[chave]), $"a chave {chave} esta vazia em {idioma}");
            }
        }
    }

    [Fact]
    public void A_confirmacao_diz_que_o_programa_continua_instalado()
    {
        var pt = Loc.Load("pt-BR");

        Assert.Contains("NÃO serão desinstalados", pt["Cmd.RemoveEntriesBatchConfirm"]);
    }
}
