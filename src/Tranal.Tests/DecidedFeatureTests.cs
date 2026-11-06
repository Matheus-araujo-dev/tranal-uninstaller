using System.Diagnostics;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Management;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O lote decidido a partir da engenharia reversa dos concorrentes:
///
///   - A12: leitura do <c>NoRemove</c> (entrada protegida);
///   - A14: deteccao de desinstalador ausente ou corrompido;
///   - B8:  ordenacao inteligente (barulhento primeiro);
///   - B17: programas relacionados;
///   - B19: impedir desligamento/reinicio durante a tarefa;
///   - D16: abrir no Regedit / Programas e Recursos / Recuperacao do Sistema.
///
/// Nenhum teste toca o registro, o disco real ou a interface: tudo passa por
/// injecao, para que a suite possa rodar em qualquer maquina.
/// </summary>
public class DecidedFeatureTests
{
    private static InstalledProgram Program(
        string name = "Exemplo",
        string uninstall = "\"C:\\Program Files\\Exemplo\\unins000.exe\"",
        string location = @"C:\Program Files\Exemplo",
        string publisher = "Fabrica Exemplo",
        string registryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Exemplo")
        => new()
        {
            DisplayName = name,
            UninstallString = uninstall,
            InstallLocation = location,
            Publisher = publisher,
            RegistryPath = registryPath,
        };

    // ---------------- A14: saude do desinstalador ----------------

    [Fact]
    public void Sem_comando_de_desinstalador_a_saude_e_ausente()
    {
        var program = Program(uninstall: string.Empty);
        Assert.Equal(UninstallerHealth.Missing, UninstallerHealthEvaluator.Evaluate(program));
    }

    [Fact]
    public void Comando_sem_executavel_a_saude_e_ausente()
    {
        var program = Program(uninstall: "/S");
        Assert.Equal(UninstallerHealth.Missing, UninstallerHealthEvaluator.Evaluate(program));
    }

    [Fact]
    public void Caminho_absoluto_que_existe_e_saude_ok()
    {
        var program = Program();
        Func<string, bool> existe = _ => true;
        Assert.Equal(UninstallerHealth.Ok, UninstallerHealthEvaluator.Evaluate(program, existe));
    }

    [Fact]
    public void Caminho_absoluto_que_nao_existe_e_desinstalador_corrompido()
    {
        var program = Program();
        Func<string, bool> existe = _ => false;
        Assert.Equal(UninstallerHealth.Broken, UninstallerHealthEvaluator.Evaluate(program, existe));
        Assert.True(UninstallerHealthEvaluator.IsBroken(program, existe));
    }

    [Fact]
    public void Comando_sem_caminho_absoluto_nao_e_provado_como_corrompido()
    {
        // "MsiExec.exe /X{...}" e resolvido pelo PATH: nao prova nada sobre o disco.
        var program = Program(uninstall: "MsiExec.exe /X{1234}");
        Assert.Equal(UninstallerHealth.Ok, UninstallerHealthEvaluator.Evaluate(program, _ => false));
    }

    // ---------------- B17: programas relacionados ----------------

    [Fact]
    public void Mesmo_produto_em_versoes_diferentes_e_relacionado()
    {
        var alvo = Program(name: "Acme Suite 3.1");
        var outro = Program(name: "Acme Suite 4.0",
            registryPath: @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Acme4");

        var related = RelatedProgramFinder.Find(alvo, new[] { alvo, outro });

        var item = Assert.Single(related);
        Assert.Equal("Acme Suite 4.0", item.Name);
        Assert.Equal(RelatedProgramReason.SameProduct, item.Reason);
    }

    [Fact]
    public void Mesmo_fabricante_e_relacionado()
    {
        var alvo = Program(name: "Alpha");
        var outro = Program(name: "Beta", registryPath: @"HKEY_LOCAL_MACHINE\SOFTWARE\...\Beta");

        var item = Assert.Single(RelatedProgramFinder.Find(alvo, new[] { alvo, outro }));
        Assert.Equal(RelatedProgramReason.SamePublisher, item.Reason);
    }

    [Fact]
    public void Mesma_pasta_de_instalacao_e_relacionado()
    {
        var alvo = Program(name: "Alpha", publisher: "A");
        var outro = Program(name: "Beta", publisher: "B", registryPath: @"HKEY_LOCAL_MACHINE\SOFTWARE\...\Beta");

        var item = Assert.Single(RelatedProgramFinder.Find(alvo, new[] { alvo, outro }));
        Assert.Equal(RelatedProgramReason.SameInstallLocation, item.Reason);
    }

    [Fact]
    public void Mesmo_desinstalador_e_relacionado()
    {
        var alvo = Program(name: "Alpha", publisher: "A", location: @"C:\Pasta A");
        var outro = Program(name: "Beta", publisher: "B", location: @"C:\Pasta B", registryPath: @"HKEY_LOCAL_MACHINE\SOFTWARE\...\Beta");

        var item = Assert.Single(RelatedProgramFinder.Find(alvo, new[] { alvo, outro }));
        Assert.Equal(RelatedProgramReason.SameUninstaller, item.Reason);
    }

    [Fact]
    public void Programa_sem_relacao_nao_entra_na_lista()
    {
        var alvo = Program(name: "Alpha", publisher: "A", location: @"C:\Pasta A");
        var outro = Program(
            name: "Zeta",
            publisher: "Z",
            location: @"C:\Pasta Z",
            uninstall: "\"C:\\Program Files\\Zeta\\unins.exe\"",
            registryPath: @"HKEY_LOCAL_MACHINE\SOFTWARE\...\Zeta");

        Assert.Empty(RelatedProgramFinder.Find(alvo, new[] { alvo, outro }));
    }

    [Fact]
    public void A_mesma_entrada_lida_duas_vezes_nao_e_programa_relacionado()
    {
        var alvo = Program(name: "Alpha", publisher: "A");
        var copia = Program(name: "Alpha", publisher: "A");

        Assert.Empty(RelatedProgramFinder.Find(alvo, new[] { alvo, copia }));
    }

    // ---------------- B19: impedir desligamento ----------------

    [Fact]
    public void A_guarda_de_desligamento_pede_para_o_sistema_nao_desligar()
    {
        uint? pedido = null;

        using var guard = ShutdownGuard.Acquire(apply: flags =>
        {
            pedido = flags;
            return flags;
        });

        Assert.NotNull(guard);
        Assert.True(guard!.IsActive);
        Assert.NotNull(pedido);
        Assert.Equal(0x80000000u, pedido!.Value & 0x80000000u);   // ES_CONTINUOUS
        Assert.Equal(0x00000001u, pedido.Value & 0x00000001u);       // ES_SYSTEM_REQUIRED
        Assert.Equal(0u, pedido.Value & 0x00000002u);                // sem manter a tela acesa
    }

    [Fact]
    public void A_guarda_pode_manter_a_tela_acesa()
    {
        uint? pedido = null;
        using var guard = ShutdownGuard.Acquire(keepDisplayOn: true, apply: flags =>
        {
            pedido = flags;
            return flags;
        });

        Assert.NotNull(guard);
        Assert.Equal(0x00000002u, pedido!.Value & 0x00000002u);       // ES_DISPLAY_REQUIRED
    }

    [Fact]
    public void Quando_o_windows_recusa_a_solicitacao_nao_ha_guarda()
    {
        var guard = ShutdownGuard.Acquire(apply: _ => 0);
        Assert.Null(guard);
    }

    [Fact]
    public void Soltar_a_guarda_e_idempotente()
    {
        var guard = ShutdownGuard.Acquire(apply: flags => flags);
        Assert.NotNull(guard);
        guard!.Dispose();
        Assert.False(guard.IsActive);
        guard.Dispose();
    }

    // ---------------- D16: abrir no Windows ----------------

    [Fact]
    public void O_destino_do_regedit_aponta_para_a_chave_do_programa()
    {
        var alvo = Program();
        var target = SystemNavigator.RegistryEditor(alvo);

        Assert.NotNull(target);
        Assert.Equal("regedit.exe", target!.FileName);
        Assert.Contains(alvo.RegistryPath, target.Arguments);
    }

    [Fact]
    public void Sem_chave_de_registro_nao_ha_destino_de_regedit()
    {
        var alvo = Program();
        alvo.RegistryPath = string.Empty;
        Assert.Null(SystemNavigator.RegistryEditor(alvo));
    }

    [Fact]
    public void Os_destinos_padrao_sao_programas_e_recursos_e_recuperacao()
    {
        var targets = SystemNavigator.TargetsFor(Program());

        Assert.Contains(targets, t => t.FileName == "appwiz.cpl");
        Assert.Contains(targets, t => t.FileName == "SystemPropertiesProtection.exe");
    }

    [Fact]
    public void Abrir_o_destino_chama_o_iniciador_com_o_comando_certo()
    {
        var target = new SystemNavigationTarget("Cmd.OpenRegistry", "regedit.exe", "\"HKEY_LOCAL_MACHINE\\X\"");
        ProcessStartInfo? recebido = null;

        var codigo = SystemNavigator.Open(target, info =>
        {
            recebido = info;
            return 7;
        });

        Assert.Equal(7, codigo);
        Assert.NotNull(recebido);
        Assert.Equal("regedit.exe", recebido!.FileName);
        Assert.Equal("\"HKEY_LOCAL_MACHINE\\X\"", recebido.Arguments);
    }

    // ---------------- B8: ordenacao inteligente ----------------

    [Fact]
    public void A_ordenacao_coloca_o_desinstalador_barulhento_primeiro()
    {
        var silencioso = Program(name: "Silencioso");
        silencioso.QuietUninstallString = "\"C:\\Program Files\\S\\unins.exe\" /S";
        silencioso.QuietUninstallCommand = silencioso.QuietUninstallString;

        var barulhento = Program(name: "Barulhento");

        var order = SmartOrdering.Order(new[] { silencioso, barulhento });

        Assert.Equal("Barulhento", order[0].DisplayName);
        Assert.Equal("Silencioso", order[1].DisplayName);
    }

    [Fact]
    public void A_ordenacao_invertida_coloca_o_silencioso_primeiro()
    {
        var silencioso = Program(name: "Silencioso");
        silencioso.QuietUninstallString = "\"C:\\Program Files\\S\\unins.exe\" /S";
        silencioso.QuietUninstallCommand = silencioso.QuietUninstallString;

        var barulhento = Program(name: "Barulhento");

        var order = SmartOrdering.Order(new[] { barulhento, silencioso }, loudFirst: false);

        Assert.Equal("Silencioso", order[0].DisplayName);
    }

    [Fact]
    public void Msi_e_tratado_como_silencioso()
    {
        var msi = Program(name: "Pacote");
        msi.Kind = UninstallerKind.MsiExec;
        msi.QuietUninstallCommand = "MsiExec.exe /X{1} /qn";

        Assert.False(SmartOrdering.IsLoud(msi));
    }

    // ---------------- A12 e A14: avisos ----------------

    [Fact]
    public void Entrada_protegida_gera_aviso()
    {
        var alvo = Program();
        alvo.IsProtected = true;

        var aviso = UninstallAdvisories.ProtectedAdvisory(alvo);

        Assert.NotNull(aviso);
        Assert.Equal(UninstallAdvisoryKind.ProtectedByRegistry, aviso!.Kind);
    }

    [Fact]
    public void Entrada_normal_nao_gera_aviso_de_protecao()
    {
        Assert.Null(UninstallAdvisories.ProtectedAdvisory(Program()));
    }

    [Fact]
    public void Desinstalador_ausente_gera_aviso()
    {
        var alvo = Program();
        alvo.UninstallerHealth = UninstallerHealth.Missing;

        var aviso = UninstallAdvisories.UninstallerHealthAdvisory(alvo);

        Assert.NotNull(aviso);
        Assert.Equal(UninstallAdvisoryKind.UninstallerMissing, aviso!.Kind);
    }

    [Fact]
    public void Desinstalador_corrompido_gera_aviso()
    {
        var alvo = Program();
        alvo.UninstallerHealth = UninstallerHealth.Broken;

        var aviso = UninstallAdvisories.UninstallerHealthAdvisory(alvo);

        Assert.NotNull(aviso);
        Assert.Equal(UninstallAdvisoryKind.UninstallerBroken, aviso!.Kind);
    }

    [Fact]
    public void Desinstalador_saudavel_nao_gera_aviso()
    {
        var alvo = Program();
        alvo.UninstallerHealth = UninstallerHealth.Ok;

        Assert.Null(UninstallAdvisories.UninstallerHealthAdvisory(alvo));
    }

    [Fact]
    public void Programas_relacionados_geram_aviso_com_os_nomes()
    {
        var alvo = Program(name: "Alpha", publisher: "A", location: @"C:\Pasta A");
        var outro = Program(name: "Beta", location: @"C:\Pasta B",
            registryPath: @"HKEY_LOCAL_MACHINE\SOFTWARE\...\Beta");

        var aviso = UninstallAdvisories.RelatedProgramsAdvisory(alvo, new[] { alvo, outro });

        Assert.NotNull(aviso);
        Assert.Equal(UninstallAdvisoryKind.RelatedPrograms, aviso!.Kind);
        Assert.Contains("Beta", aviso.Items);
    }

    [Fact]
    public void O_conjunto_de_avisos_traz_todos_os_tipos()
    {
        var alvo = Program(name: "Alpha", publisher: "A", location: @"C:\Pasta A");
        alvo.IsProtected = true;
        alvo.UninstallerHealth = UninstallerHealth.Broken;
        var outro = Program(name: "Beta", location: @"C:\Pasta B",
            registryPath: @"HKEY_LOCAL_MACHINE\SOFTWARE\...\Beta");

        var avisos = UninstallAdvisories.Build(alvo, new[] { alvo, outro });

        Assert.Contains(avisos, a => a.Kind == UninstallAdvisoryKind.ProtectedByRegistry);
        Assert.Contains(avisos, a => a.Kind == UninstallAdvisoryKind.UninstallerBroken);
        Assert.Contains(avisos, a => a.Kind == UninstallAdvisoryKind.RelatedPrograms);
    }
}
