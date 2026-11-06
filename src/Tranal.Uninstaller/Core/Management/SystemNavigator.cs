using System.Diagnostics;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// Um destino do Windows que o Tranal sabe abrir.
/// </summary>
/// <param name="ResourceKey">A chave do texto traduzido do destino.</param>
/// <param name="FileName">O executavel a abrir.</param>
/// <param name="Arguments">Os argumentos passados ao executavel.</param>
public sealed record SystemNavigationTarget(string ResourceKey, string FileName, string Arguments)
{
    /// <summary>O nome do destino, no idioma ativo.</summary>
    public string Title => Loc.Get(ResourceKey);
}

/// <summary>
/// Abre os lugares do Windows onde a desinstalacao acontece.
///
/// O Revo Uninstaller Pro 5.5.4 oferece, no menu de contexto da entrada,
/// o acesso direto a "Programas e Recursos", ao Registro e a Recuperacao do
/// Sistema. A evidencia esta no proprio binario do Revo Pro: a tabela de
/// idioma tem a secao "[Uninstaller Toolbar]" com os textos desses comandos.
///
/// A regra mora aqui, em um lugar so: a interface grafica e a CLI pedem o
/// DESTINO a esta classe e abrem o que ela devolve; ninguem monta o comando
/// por conta propria.
/// </summary>
public static class SystemNavigator
{
    /// <summary>
    /// Os destinos disponiveis para um programa.
    ///
    /// O Registro so entra quando a entrada tem chave de origem conhecida;
    /// "Programas e Recursos" e "Recuperacao do Sistema" valem para qualquer
    /// programa.
    /// </summary>
    /// <param name="program">O programa selecionado.</param>
    public static IReadOnlyList<SystemNavigationTarget> TargetsFor(InstalledProgram program)
    {
        var targets = new List<SystemNavigationTarget>();

        var registry = RegistryEditor(program);
        if (registry is not null) targets.Add(registry);

        targets.Add(AppsAndFeatures());
        targets.Add(SystemRestore());

        return targets;
    }

    /// <summary>
    /// O destino que abre a chave do programa no Registro, ou nulo quando a
    /// entrada nao tem chave de origem conhecida.
    /// </summary>
    /// <param name="program">O programa selecionado.</param>
    public static SystemNavigationTarget? RegistryEditor(InstalledProgram program)
    {
        if (program is null) return null;

        var path = (program.RegistryPath ?? string.Empty).Trim();
        if (path.Length == 0) return null;

        return new SystemNavigationTarget("Cmd.OpenRegistry", "regedit.exe", $"\"{path}\"");
    }

    /// <summary>O destino que abre "Programas e Recursos" (o painel do Windows).</summary>
    public static SystemNavigationTarget AppsAndFeatures() =>
        new("Cmd.OpenAppsAndFeatures", "appwiz.cpl", string.Empty);

    /// <summary>O destino que abre a aba "Recuperacao do Sistema" das propriedades do sistema.</summary>
    public static SystemNavigationTarget SystemRestore() =>
        new("Cmd.OpenSystemRestore", "SystemPropertiesProtection.exe", string.Empty);

    /// <summary>
    /// Abre o destino no Windows.
    ///
    /// O metodo devolve o codigo de saida do processo (ou -1 quando nao deu
    /// para abrir) em vez de lancar: abrir um painel do Windows pode falhar
    /// por falta de permissao, e isso nao e uma excecao do Tranal.
    /// </summary>
    /// <param name="target">O destino a abrir.</param>
    /// <param name="start">Como iniciar o processo. Por padrao, <see cref="Process.Start(ProcessStartInfo)"/>.</param>
    public static int Open(SystemNavigationTarget target, Func<ProcessStartInfo, int>? start = null)
    {
        if (target is null) return -1;

        var info = new ProcessStartInfo(target.FileName)
        {
            Arguments = target.Arguments,
            UseShellExecute = true,
        };

        if (start is not null) return start(info);

        try
        {
            using var process = Process.Start(info);
            if (process is null) return -1;
            process.WaitForExit(10_000);
            return process.HasExited ? process.ExitCode : 0;
        }
        catch (Exception)
        {
            return -1;
        }
    }
}
