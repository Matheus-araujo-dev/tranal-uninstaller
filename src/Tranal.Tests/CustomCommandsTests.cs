using System.IO;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// B15: comandos que rodam antes e depois da desinstalacao, guardados por
/// programa.
///
/// O plano de desinstalacao ja tinha as duas etapas; o que faltava era o
/// lugar onde o usuario guarda o comando — e a regra que junta o que o
/// usuario guardou com o que a chamada passa.
///
/// Nenhum teste toca o arquivo de preferencias de quem roda a suite: o
/// caminho e apontado para uma pasta temporaria.
/// </summary>
[Collection("preferencias")]
public class CustomCommandsTests : IDisposable
{
    private readonly string _arquivo;

    public CustomCommandsTests()
    {
        _arquivo = Path.Combine(
            Path.GetTempPath(),
            "tranal-comandos-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");

        ProgramCustomizationStore.FilePathOverride = _arquivo;
        ProgramCustomizationStore.Reload();
        UninstallPlanBuilder.CustomizationProvider = null;
    }

    public void Dispose()
    {
        UninstallPlanBuilder.CustomizationProvider = null;
        ProgramCustomizationStore.FilePathOverride = null;

        try
        {
            if (File.Exists(_arquivo)) File.Delete(_arquivo);
        }
        catch
        {
            // Arquivo temporario: falhar ao apagar nao estraga o teste.
        }
    }

    private static InstalledProgram Programa() => new()
    {
        DisplayName = "Exemplo",
        RegistryKey = "{exemplo-guid}",
        RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\...\Uninstall\{exemplo-guid}",
        UninstallString = "\"C:\\Programa\\Exemplo\\unins000.exe\"",
    };

    [Fact]
    public void Os_comandos_sao_gravados_e_lidos_de_volta()
    {
        var programa = Programa();

        Assert.True(ProgramCustomizationStore.SetCommands(programa, "taskkill /IM Exemplo.exe", "reg add HKCU\\Teste /v ok /t dword /d 1"));

        var salvo = ProgramCustomizationStore.For(programa);
        Assert.Equal("taskkill /IM Exemplo.exe", salvo.PreCommand);
        Assert.Equal("reg add HKCU\\Teste /v ok /t dword /d 1", salvo.PostCommand);
    }

    [Fact]
    public void Comando_vazio_poda_a_anotacao()
    {
        var programa = Programa();

        ProgramCustomizationStore.SetCommands(programa, "antes", "depois");
        Assert.True(ProgramCustomizationStore.Has(programa));

        ProgramCustomizationStore.SetCommands(programa, string.Empty, string.Empty);
        Assert.False(ProgramCustomizationStore.Has(programa));
    }

    [Fact]
    public void Sem_provedor_as_opcoes_ficam_como_estao()
    {
        var options = new UninstallPlanOptions { PreCommand = "da chamada" };

        var resolvidas = UninstallPlanBuilder.ResolveOptions(Programa(), options);

        Assert.Equal("da chamada", resolvidas.PreCommand);
    }

    [Fact]
    public void O_comando_guardado_entra_quando_a_chamada_nao_traz_nada()
    {
        UninstallPlanBuilder.CustomizationProvider = _ => ("guardado antes", "guardado depois");

        var resolvidas = UninstallPlanBuilder.ResolveOptions(Programa(), null);

        Assert.Equal("guardado antes", resolvidas.PreCommand);
        Assert.Equal("guardado depois", resolvidas.PostCommand);
    }

    [Fact]
    public void A_chamada_vence_o_comando_guardado()
    {
        UninstallPlanBuilder.CustomizationProvider = _ => ("guardado antes", "guardado depois");

        var resolvidas = UninstallPlanBuilder.ResolveOptions(
            Programa(),
            new UninstallPlanOptions { PreCommand = "da chamada" });

        Assert.Equal("da chamada", resolvidas.PreCommand);
        Assert.Equal("guardado depois", resolvidas.PostCommand);
    }

    [Fact]
    public void Um_provedor_que_falha_nao_derruba_a_desinstalacao()
    {
        UninstallPlanBuilder.CustomizationProvider = _ => throw new InvalidOperationException("falhou");

        var resolvidas = UninstallPlanBuilder.ResolveOptions(Programa(), null);

        Assert.Null(resolvidas.PreCommand);
        Assert.Null(resolvidas.PostCommand);
    }

    [Fact]
    public void O_plano_ganha_as_etapas_de_comando_quando_o_usuario_guardou()
    {
        UninstallPlanBuilder.CustomizationProvider = _ => ("taskkill /IM Exemplo.exe", "reg add HKCU\\Teste /v ok /t dword /d 1");

        var plano = UninstallPlanBuilder.Build(Programa(), UninstallMode.Moderate);

        Assert.Contains(plano.Steps, s => s.Kind == UninstallStepKind.PreCommand && s.Command == "taskkill /IM Exemplo.exe");
        Assert.Contains(plano.Steps, s => s.Kind == UninstallStepKind.PostCommand && s.Command!.Contains("reg add"));
    }

    [Fact]
    public void Sem_comando_guardado_o_plano_nao_tem_etapa_de_comando()
    {
        var plano = UninstallPlanBuilder.Build(Programa(), UninstallMode.Moderate);

        Assert.DoesNotContain(plano.Steps, s => s.Kind == UninstallStepKind.PreCommand);
        Assert.DoesNotContain(plano.Steps, s => s.Kind == UninstallStepKind.PostCommand);
    }
}
