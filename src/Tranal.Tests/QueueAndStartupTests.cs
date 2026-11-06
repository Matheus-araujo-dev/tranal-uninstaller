using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Discovery;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O lote seguinte, decidido a partir da engenharia reversa dos concorrentes:
///
///   - B7: fila de desinstalacao com prevencao de colisao (um por vez);
///   - B18: quem nao desinstalou em silencio sai da tarefa;
///   - C7: confianca de inicializacao (o registro de inicializacao do app);
///   - fila binaria #4: reparo da instalacao MSI.
///
/// Nenhum teste roda desinstalador, mexe no registro real ou escreve no disco:
/// tudo entra por injecao.
/// </summary>
public class QueueAndStartupTests
{
    private static InstalledProgram Program(
        string name,
        string location = @"C:\Programa\Exemplo",
        string uninstall = "\"C:\\Programa\\Exemplo\\unins000.exe\"",
        string publisher = "Fabrica Exemplo") =>
        new()
        {
            DisplayName = name,
            InstallLocation = location,
            UninstallString = uninstall,
            Publisher = publisher,
            RegistryPath = $@"HKEY_LOCAL_MACHINE\SOFTWARE\...\Uninstall\{name}",
        };

    // ---------------- B7: um de cada vez ----------------

    [Fact]
    public void A_fila_roda_um_desinstalador_por_vez()
    {
        var simultaneos = 0;
        var maximo = 0;
        var programas = new[] { Program("A"), Program("B"), Program("C") };

        var resultado = UninstallQueue.Run(programas, _ =>
        {
            simultaneos++;
            if (simultaneos > maximo) maximo = simultaneos;
            simultaneos--;
            return true;
        });

        Assert.Equal(3, resultado.Count);
        Assert.Equal(1, maximo);
    }

    [Fact]
    public void A_fila_usa_a_ordem_inteligente()
    {
        var silencioso = Program("Silencioso");
        silencioso.QuietUninstallString = "\"C:\\Programa\\S\\unins.exe\" /S";
        silencioso.QuietUninstallCommand = silencioso.QuietUninstallString;

        var barulhento = Program("Barulhento");

        var resultado = UninstallQueue.Run(new[] { silencioso, barulhento }, _ => true);

        Assert.Equal("Barulhento", resultado[0].Program.DisplayName);
        Assert.Equal("Silencioso", resultado[1].Program.DisplayName);
    }

    [Fact]
    public void Dois_programas_que_dividem_a_pasta_colidem()
    {
        var a = Program("A", location: @"C:\Compartilhada");
        var b = Program("B", location: @"C:\Compartilhada");

        Assert.True(UninstallQueue.Collide(a, b));

        var colisoes = UninstallQueue.DetectCollisions(new[] { a, b });
        var colisao = Assert.Single(colisoes);
        Assert.Equal("A", colisao.First.DisplayName);
        Assert.Equal("B", colisao.Second.DisplayName);
    }

    [Fact]
    public void Dois_programas_com_o_mesmo_desinstalador_colidem()
    {
        var a = Program("A", location: @"C:\A", uninstall: "\"C:\\Comum\\unins.exe\"");
        var b = Program("B", location: @"C:\B", uninstall: "\"C:\\Comum\\unins.exe\"");

        Assert.True(UninstallQueue.Collide(a, b));
    }

    [Fact]
    public void Programas_sem_relacao_nao_colidem()
    {
        var a = Program("A", location: @"C:\A", uninstall: "\"C:\\A\\unins.exe\"");
        var b = Program("B", location: @"C:\B", uninstall: "\"C:\\B\\unins.exe\"");

        Assert.False(UninstallQueue.Collide(a, b));
        Assert.Empty(UninstallQueue.DetectCollisions(new[] { a, b }));
    }

    [Fact]
    public void A_fila_declara_a_colisao_no_resultado()
    {
        var a = Program("A", location: @"C:\Compartilhada");
        var b = Program("B", location: @"C:\Compartilhada");

        var resultado = UninstallQueue.Run(new[] { a, b }, _ => true);

        var segundo = resultado.Single(i => i.Program.DisplayName == "B");
        Assert.Equal("A", segundo.CollidesWith);
    }

    // ---------------- B18: sai da tarefa ----------------

    [Fact]
    public void Quem_nao_desinstalou_em_silencio_sai_da_tarefa()
    {
        var programa = Program("A");

        var resultado = UninstallQueue.Run(
            new[] { programa },
            _ => false,                                   // o desinstalador nao rodou
            _ => true);                                   // e o programa continua instalado

        var item = Assert.Single(resultado);
        Assert.Equal(UninstallQueueOutcome.RemovedFromTask, item.Outcome);
        Assert.True(item.LeftTheTask);
        Assert.Contains("silêncio", item.Detail);
    }

    [Fact]
    public void Quem_saiu_do_registro_por_conta_propria_esta_desinstalado()
    {
        var programa = Program("A");

        var resultado = UninstallQueue.Run(
            new[] { programa },
            _ => false,
            _ => false);                                  // nao esta mais instalado

        var item = Assert.Single(resultado);
        Assert.Equal(UninstallQueueOutcome.Completed, item.Outcome);
    }

    [Fact]
    public void A_falha_de_um_programa_nao_derruba_a_fila()
    {
        var primeiro = Program("A");
        var segundo = Program("B");

        var resultado = UninstallQueue.Run(new[] { primeiro, segundo }, p =>
        {
            if (p.DisplayName == "A") throw new InvalidOperationException("falhou");
            return true;
        });

        Assert.Equal(UninstallQueueOutcome.Failed, resultado[0].Outcome);
        Assert.Equal("falhou", resultado[0].Detail);
        Assert.Equal(UninstallQueueOutcome.Completed, resultado[1].Outcome);
    }

    [Fact]
    public void Uma_fila_vazia_devolve_lista_vazia()
    {
        Assert.Empty(UninstallQueue.Run(Array.Empty<InstalledProgram>(), _ => true));
    }

    // ---------------- C7: confianca de inicializacao ----------------

    [Fact]
    public void Uma_entrada_que_aponta_para_a_pasta_do_programa_e_dele()
    {
        var programa = Program("Exemplo", location: @"C:\Programa\Exemplo");
        var entrada = new StartupEntry("HKCU\\...\\Run", "Exemplo", "\"C:\\Programa\\Exemplo\\app.exe\" --start");

        Assert.True(StartupEntries.BelongsTo(entrada, programa));
    }

    [Fact]
    public void Uma_entrada_que_cita_o_nome_do_programa_e_dele()
    {
        var programa = Program("Exemplo");
        var entrada = new StartupEntry("HKLM\\...\\Run", "Outro", @"C:\Windows\system32\app.exe ""Exemplo updater""");

        Assert.True(StartupEntries.BelongsTo(entrada, programa));
    }

    [Fact]
    public void Uma_entrada_de_outro_programa_nao_e_do_programa()
    {
        var programa = Program("Exemplo", location: @"C:\Programa\Exemplo");
        var entrada = new StartupEntry("HKCU\\...\\Run", "Outro", "\"C:\\Outro\\app.exe\"");

        Assert.False(StartupEntries.BelongsTo(entrada, programa));
    }

    [Fact]
    public void A_filtra_apenas_as_entradas_do_programa()
    {
        var programa = Program("Exemplo", location: @"C:\Programa\Exemplo");
        var entradas = new[]
        {
            new StartupEntry("HKCU\\...\\Run", "Exemplo", "\"C:\\Programa\\Exemplo\\app.exe\""),
            new StartupEntry("HKCU\\...\\Run", "Outro", "\"C:\\Outro\\app.exe\""),
        };

        var doPrograma = StartupEntries.For(programa, entradas);

        var item = Assert.Single(doPrograma);
        Assert.Equal("Exemplo", item.Name);
    }

    [Fact]
    public void O_motivo_da_inicializacao_tem_texto_no_catalogo()
    {
        // O texto mora em um lugar so (LeftoverLabels): a tela e a CLI nao
        // podem divergir na primeira traducao esquecida.
        var texto = Tranal.Uninstaller.Core.Localization.LeftoverLabels.Reason(LeftoverReason.StartupEntry);
        Assert.False(string.IsNullOrWhiteSpace(texto));
        Assert.NotEqual("Common.Reason.StartupEntry", texto);
    }

    // ---------------- fila binaria #4: reparo MSI ----------------

    [Fact]
    public void O_comando_de_reparo_usa_o_verbo_fa()
    {
        Assert.True(MsiProductCatalog.TryBuildRepairCommand(
            "{12345678-1234-1234-1234-123456789ABC}", true, out var command, out var error));

        Assert.Equal(string.Empty, error);
        Assert.Contains("/fa", command);
        Assert.Contains("/qn", command);
        Assert.Contains("{12345678-1234-1234-1234-123456789ABC}", command);
    }

    [Fact]
    public void O_comando_de_reparo_sem_interface_nao_tem_qn()
    {
        Assert.True(MsiProductCatalog.TryBuildRepairCommand(
            "{12345678-1234-1234-1234-123456789ABC}", false, out var command, out _));

        Assert.Contains("/fa", command);
        Assert.DoesNotContain("/qn", command);
    }

    [Fact]
    public void Um_codigo_invalido_e_recusado_com_mensagem()
    {
        Assert.False(MsiProductCatalog.TryBuildRepairCommand("nao-e-guid", true, out _, out var error));
        Assert.Contains("GUID", error);
    }

    [Fact]
    public void O_comando_de_desinstalacao_continua_sendo_o_x()
    {
        Assert.True(MsiProductCatalog.TryBuildUninstallCommand(
            "{12345678-1234-1234-1234-123456789ABC}", true, out var command, out _));

        Assert.Contains("/x", command);
        Assert.DoesNotContain("/fa", command);
    }
}
