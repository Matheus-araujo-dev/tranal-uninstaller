using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O lote 25 fecha a fila de desinstalacao na tela (BCU T2#26), o aviso de
/// colisao e o comando de rodar em primeiro plano (BCU T2#36).
///
/// A REGRA mora no nucleo (<c>Core/Automation/UninstallQueue.cs</c> e
/// <c>Core/Automation/UninstallGuards.cs</c>) e a suite testa esses
/// modulos. A ligacao com a interface e conferida sobre o fonte, pelo mesmo
/// motivo das outras paridades: a suite nao referencia o projeto grafico
/// (WPF).
///
/// Nenhum teste roda desinstalador de verdade: a fila e testada por
/// injecao, como as outras suites do projeto.
/// </summary>
public class Lote25FeatureTests
{
    private static InstalledProgram Programa(
        string nome,
        string local = @"C:\Programa\Exemplo",
        string desinstalador = "\"C:\\Programa\\Exemplo\\unins000.exe\"") =>
        new()
        {
            DisplayName = nome,
            InstallLocation = local,
            UninstallString = desinstalador,
            Publisher = "Fabrica Exemplo",
            RegistryPath = $@"HKEY_LOCAL_MACHINE\SOFTWARE\...\Uninstall\{nome}",
        };

    // ==================================================================
    // A fila: um desinstalador por vez, e a parada da tarefa
    // ==================================================================

    [Fact]
    public async Task A_fila_assincrona_roda_um_desinstalador_por_vez()
    {
        var simultaneos = 0;
        var maximo = 0;

        var resultado = await UninstallQueue.RunAsync(
            new[] { Programa("A"), Programa("B"), Programa("C") },
            _ =>
            {
                simultaneos++;
                if (simultaneos > maximo) maximo = simultaneos;
                simultaneos--;
                return Task.FromResult(true);
            });

        Assert.Equal(3, resultado.Count);
        Assert.Equal(1, maximo);
        Assert.All(resultado, i => Assert.Equal(UninstallQueueOutcome.Completed, i.Outcome));
    }

    [Fact]
    public async Task A_fila_para_quando_o_usuario_manda_encerrar()
    {
        var rodados = new List<string>();

        var resultado = await UninstallQueue.RunAsync(
            new[] { Programa("A"), Programa("B"), Programa("C") },
            program =>
            {
                rodados.Add(program.DisplayName);
                return Task.FromResult(true);
            },
            stillInstalled: null,
            maxAttempts: 1,
            stopRequested: () => rodados.Count >= 1);

        // O primeiro rodou; o segundo e o terceiro nem comecaram.
        Assert.Equal(new[] { "A" }, rodados);
        Assert.Equal(3, resultado.Count);
        Assert.Equal(UninstallQueueOutcome.Completed, resultado[0].Outcome);
        Assert.Equal(UninstallQueueOutcome.Aborted, resultado[1].Outcome);
        Assert.Equal(UninstallQueueOutcome.Aborted, resultado[2].Outcome);
    }

    [Fact]
    public async Task A_fila_assincrona_tem_a_mesma_regra_da_sincrona()
    {
        var sincrona = UninstallQueue.Run(
            new[] { Programa("A"), Programa("B") },
            _ => true);

        var assincrona = await UninstallQueue.RunAsync(
            new[] { Programa("A"), Programa("B") },
            _ => Task.FromResult(true));

        Assert.Equal(sincrona.Count, assincrona.Count);
        Assert.Equal(
            sincrona.Select(i => i.Outcome),
            assincrona.Select(i => i.Outcome));
    }

    [Fact]
    public async Task Quem_nao_desinstala_em_silencio_sai_da_tarefa()
    {
        var resultado = await UninstallQueue.RunAsync(
            new[] { Programa("Barulhento") },
            _ => Task.FromResult(false),
            _ => true);

        var item = Assert.Single(resultado);
        Assert.Equal(UninstallQueueOutcome.RemovedFromTask, item.Outcome);
        Assert.True(item.LeftTheTask);
    }

    [Fact]
    public async Task A_colisao_e_declarada_na_fila()
    {
        var a = Programa("A", local: @"C:\Compartilhada");
        var b = Programa("B", local: @"C:\Compartilhada");

        var resultado = await UninstallQueue.RunAsync(
            new[] { a, b },
            _ => Task.FromResult(true));

        Assert.Contains(resultado, i => i.CollidesWith.Length > 0);
    }

    // ==================================================================
    // A guarda: a marca de "desinstalacao em andamento"
    // ==================================================================

    [Fact]
    public void A_guarda_marca_a_operacao_e_limpa_no_fim()
    {
        var durante = UninstallGuards.CheckRunning();

        UninstallGuards.RunGuarded("teste do lote 25", () =>
        {
            var marcada = UninstallGuards.CheckRunning();
            Assert.False(marcada.Allowed);
            Assert.Contains("teste do lote 25", marcada.Reason, StringComparison.OrdinalIgnoreCase);
            return true;
        });

        var depois = UninstallGuards.CheckRunning();
        Assert.True(depois.Allowed);
        Assert.NotNull(durante);
    }

    [Fact]
    public void A_guarda_limpa_a_marca_even_se_a_operacao_falhar()
    {
        Assert.Throws<InvalidOperationException>(() =>
            UninstallGuards.RunGuarded<int>("teste que falha", () =>
                throw new InvalidOperationException("falha proposital")));

        Assert.True(UninstallGuards.CheckRunning().Allowed);
    }

    [Fact]
    public async Task A_guarda_assincrona_marca_e_limpa()
    {
        await UninstallGuards.RunGuardedAsync("teste assincrono", async () =>
        {
            Assert.False(UninstallGuards.CheckRunning().Allowed);
            await Task.Yield();
            return 1;
        });

        Assert.True(UninstallGuards.CheckRunning().Allowed);
    }

    // ==================================================================
    // O motor: o "ainda instalado" mora no nucleo
    // ==================================================================

    [Fact]
    public void O_motor_diz_que_o_programa_continua_instalado()
    {
        // O CreateSubKey recebe o caminho RELATIVO a HKEY_CURRENT_USER; a
        // chave completa e o que o motor recebe.
        var relativo = $@"Software\Tranal\Tests\Lote25\{Guid.NewGuid():N}";
        var caminho = $@"HKEY_CURRENT_USER\{relativo}";

        using (var chave = Registry.CurrentUser.CreateSubKey(relativo))
        {
            Assert.NotNull(chave);
        }

        try
        {
            var programa = new InstalledProgram { DisplayName = "Teste", RegistryPath = caminho };

            Assert.True(UninstallEngine.IsStillInstalled(programa));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                @"Software\Tranal\Tests\Lote25", throwOnMissingSubKey: false);
        }

        var sumiu = new InstalledProgram { DisplayName = "Teste", RegistryPath = caminho };
        Assert.False(UninstallEngine.IsStillInstalled(sumiu));
    }

    [Fact]
    public void O_motor_diz_que_nao_da_para_saber_quando_o_caminho_e_vazio()
    {
        Assert.False(UninstallEngine.IsStillInstalled(null));
        Assert.False(UninstallEngine.IsStillInstalled(new InstalledProgram { DisplayName = "X" }));
    }

    // ==================================================================
    // O comando do desinstalador nativo
    // ==================================================================

    [Fact]
    public void O_comando_do_desinstalador_tem_tres_modos()
    {
        Assert.Equal(3, Enum.GetValues<NativeUninstallCommand>().Length);

        Assert.True(Enum.IsDefined(NativeUninstallCommand.Registered));
        Assert.True(Enum.IsDefined(NativeUninstallCommand.Quiet));
        Assert.True(Enum.IsDefined(NativeUninstallCommand.Silent));
    }

    // ==================================================================
    // A ligacao com a interface
    // ==================================================================

    [Fact]
    public void O_assistente_usa_a_fila_e_a_guarda()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml.cs");

        Assert.Contains("UninstallQueue.RunAsync", codigo);
        Assert.Contains("UninstallGuards.RunGuardedAsync", codigo);
        Assert.Contains("UninstallEngine.IsStillInstalled", codigo);
        Assert.Contains("UninstallQueueOutcome.RemovedFromTask", codigo);
    }

    [Fact]
    public void O_assistente_tem_o_painel_de_quem_saiu_da_tarefa()
    {
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml");
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml.cs");

        Assert.Contains("LeftTaskPanel", xaml);
        Assert.Contains("WizardForegroundButton", xaml);
        Assert.Contains("ForegroundButton_Click", xaml);

        Assert.Contains("ShowLeftTask", codigo);
        Assert.Contains("NativeUninstallCommand.Registered", codigo);
    }

    [Fact]
    public void As_chaves_novas_estao_nos_dois_idiomas()
    {
        var chaves = new[]
        {
            "Wizard.LeftTask.Text",
            "Wizard.LeftTask.Foreground",
            "Wizard.LeftTask.Running",
            "Wizard.LeftTask.StillThere",
            "Wizard.Collision",
        };

        var pt = LerCatalogo("pt-BR.json");
        var en = LerCatalogo("en-US.json");

        foreach (var chave in chaves)
        {
            Assert.True(pt.ContainsKey(chave), $"chave ausente no pt-BR: {chave}");
            Assert.True(en.ContainsKey(chave), $"chave ausente no en-US: {chave}");

            Assert.False(string.IsNullOrWhiteSpace(pt[chave]));
            Assert.False(string.IsNullOrWhiteSpace(en[chave]));
        }
    }

    // ==================================================================
    // Auxiliares
    // ==================================================================

    private static Dictionary<string, string> LerCatalogo(string arquivo)
    {
        var texto = RepoFiles.Read("src", "Tranal.Uninstaller", "Lang", arquivo);
        using var documento = System.Text.Json.JsonDocument.Parse(texto);

        var dicionario = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var propriedade in documento.RootElement.EnumerateObject())
        {
            dicionario[propriedade.Name] = propriedade.Value.GetString() ?? string.Empty;
        }

        return dicionario;
    }
}
