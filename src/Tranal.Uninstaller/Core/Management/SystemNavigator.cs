using System.Diagnostics;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// Um destino do Windows que o Tranal sabe abrir.
/// </summary>
/// <param name="ResourceKey">A chave do texto traduzido do destino.</param>
/// <param name="FileName">O executavel a abrir.</param>
/// <param name="Arguments">Os argumentos passados ao executavel.</param>
/// <param name="AutomationId">
/// O identificador do item de menu. Quando nulo, o identificador vem do
/// <paramref name="ResourceKey"/> — que e unico por destino.
/// </param>
public sealed record SystemNavigationTarget(
    string ResourceKey,
    string FileName,
    string Arguments,
    string? AutomationId = null)
{
    /// <summary>O nome do destino, no idioma ativo.</summary>
    public string Title => Loc.Get(ResourceKey);

    /// <summary>
    /// O identificador do item de menu, unico por destino.
    ///
    /// Dois destinos podem chamar o mesmo executavel (as duas pastas abrem
    /// o Explorer), entao o identificador nao pode vir do nome do
    /// executavel: seria o mesmo para os dois. E o endereco da loja nem
    /// serviria: ele muda a cada busca.
    /// </summary>
    public string MenuId => AutomationId
        ?? ("Cmd" + (ResourceKey.StartsWith("Cmd.", StringComparison.Ordinal)
            ? ResourceKey.Substring("Cmd.".Length)
            : ResourceKey));
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

        var loja = MicrosoftStore(program);
        if (loja is not null) targets.Add(loja);

        var pastaPrograma = ProgramFolder(program);
        if (pastaPrograma is not null) targets.Add(pastaPrograma);

        var pastaDesinstalador = UninstallerFolder(program);
        if (pastaDesinstalador is not null) targets.Add(pastaDesinstalador);

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
    /// O destino que abre o programa na Microsoft Store.
    ///
    /// E o "Ir para a Microsoft Store" do Geek: o programa e procurado na
    /// loja pelo nome e pela editora. A loja e onde o programa foi comprado —
    /// e onde ele pode ser reavaliado.
    ///
    /// Devolve nulo quando o programa nao tem nome: sem nome, a busca seria
    /// da loja inteira.
    /// </summary>
    /// <param name="program">O programa selecionado.</param>
    public static SystemNavigationTarget? MicrosoftStore(InstalledProgram program)
    {
        if (program is null) return null;

        var consulta = StoreQueryOf(program);
        if (consulta.Length == 0) return null;

        // O endereco da loja e um URI: ele precisa chegar inteiro, sem
        // argumentos em separado (o "Open" monta o comando com o nome do
        // executavel e os argumentos).
        return new SystemNavigationTarget(
            "Cmd.OpenMicrosoftStore",
            "ms-windows-store://search/?query=" + Uri.EscapeDataString(consulta),
            string.Empty);
    }

    /// <summary>
    /// O destino que abre a pasta de instalacao do programa no Explorer.
    ///
    /// E o "Browse Location..." do Geek: onde o programa esta instalado.
    /// </summary>
    /// <param name="program">O programa selecionado.</param>
    public static SystemNavigationTarget? ProgramFolder(InstalledProgram program)
    {
        var pasta = FolderOf(program?.InstallLocation);
        if (pasta is null) return null;

        return new SystemNavigationTarget("Cmd.OpenProgramFolder", "explorer.exe", $"\"{pasta}\"");
    }

    /// <summary>
    /// O destino que abre a pasta do desinstalador do programa.
    ///
    /// E o "Browse Source..." do Geek: de onde veio o programa. Quando o
    /// desinstalador esta em outra pasta (o caso comum: o programa e
    /// instalado em "Program Files" e o desinstalador em "AppData"), sao
    /// duas pastas diferentes — e as duas sao uteis.
    /// </summary>
    /// <param name="program">O programa selecionado.</param>
    public static SystemNavigationTarget? UninstallerFolder(InstalledProgram program)
    {
        var executavel = ExecutablePathResolver.Extract(program?.UninstallString);
        if (executavel.Length == 0) return null;

        var pasta = FolderOf(executavel);
        if (pasta is null) return null;

        return new SystemNavigationTarget("Cmd.OpenUninstallerFolder", "explorer.exe", $"\"{pasta}\"");
    }

    /// <summary>
    /// O termo de busca do programa na Microsoft Store: o nome e a editora.
    /// </summary>
    private static string StoreQueryOf(InstalledProgram program)
    {
        var partes = new List<string>();

        var nome = (program.DisplayName ?? string.Empty).Trim();
        var editora = (program.Publisher ?? string.Empty).Trim();

        if (nome.Length > 0) partes.Add(nome);

        // A editora so entra quando o nome nao a traz: repetir o mesmo nome
        // duas vezes so estraga a busca.
        if (editora.Length > 0
            && !nome.Contains(editora, StringComparison.OrdinalIgnoreCase))
        {
            partes.Add(editora);
        }

        return string.Join(" ", partes);
    }

    /// <summary>
    /// A pasta de um caminho, quando ela existe no disco. O caminho pode ser
    /// a propria pasta ou um arquivo dentro dela.
    /// </summary>
    private static string? FolderOf(string? path)
    {
        var bruto = (path ?? string.Empty).Trim().Trim('"');
        if (bruto.Length == 0) return null;

        string pasta;
        if (System.IO.Directory.Exists(bruto)) pasta = bruto;
        else if (System.IO.File.Exists(bruto)) pasta = System.IO.Path.GetDirectoryName(bruto) ?? string.Empty;
        else return null;

        if (pasta.Length == 0) return null;

        return System.IO.Directory.Exists(pasta) ? pasta : null;
    }



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
