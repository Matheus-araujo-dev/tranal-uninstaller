using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// O tipo de aviso que a desinstalacao precisa dar antes de apagar.
/// </summary>
public enum UninstallAdvisoryKind
{
    /// <summary>
    /// Outros programas estao instalados na mesma pasta. As sobras de todos
    /// eles podem ser encontradas — e apagadas junto.
    /// </summary>
    SharedInstallLocation,

    /// <summary>
    /// O programa e um componente do sistema: remover pode parar recursos do
    /// Windows ou outros softwares.
    /// </summary>
    SystemComponent,

    /// <summary>
    /// A sobra esta numa pasta que pode conter dados do usuario e documentos.
    /// </summary>
    UserData,
}

/// <summary>
/// Um aviso que a desinstalacao precisa dar antes de apagar.
///
/// O aviso e um dado, nao um texto: a interface grafica e a CLI recebem o
/// mesmo objeto e cada uma escreve do seu jeito. O texto traduzido mora em
/// <see cref="AdvisoryLabels"/>.
/// </summary>
/// <param name="Kind">O tipo do aviso.</param>
/// <param name="Title">O titulo curto do aviso, ja traduzido.</param>
/// <param name="Message">A explicacao do aviso, ja traduzida.</param>
/// <param name="Items">Os nomes ou caminhos envolvidos no aviso.</param>
public sealed record UninstallAdvisory(
    UninstallAdvisoryKind Kind,
    string Title,
    string Message,
    IReadOnlyList<string> Items);

/// <summary>
/// Os avisos que a desinstalacao precisa dar antes de apagar.
///
/// O original (Revo Uninstaller Pro) tem tres avisos que o Tranal nao tinha:
/// "programas instalados na mesma pasta", "remover este produto pode fazer
/// recursos do Windows pararem de funcionar" e "as pastas abaixo podem conter
/// dados do usuario e documentos".
///
/// A regra mora aqui, em um lugar so: a interface grafica e a CLI consomem
/// estes metodos e nunca montam o aviso por conta propria.
/// </summary>
public static class UninstallAdvisories
{
    /// <summary>
    /// Pastas de conteudo do usuario, dentro do perfil. Uma sobra que cai
    /// dentro delas pode ser documento, foto ou trabalho do usuario — nunca
    /// cache de programa.
    ///
    /// "AppData" NAO entra aqui de proposito: ali mora dado de programa, que
    /// e justamente o que a desinstalacao deve apagar. Avisar sobre todo
    /// AppData seria um aviso a cada operacao, e um aviso constante nao e
    /// aviso nenhum.
    /// </summary>
    private static readonly HashSet<string> UserContentFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Documents", "Desktop", "Pictures", "Music", "Videos", "Saved Games",
        "Favorites", "3D Objects", "Contacts", "Links", "Searches", "Downloads",
    };

    /// <summary>
    /// Monta todos os avisos que se aplicam a operacao.
    /// </summary>
    /// <param name="program">O programa que vai ser desinstalado.</param>
    /// <param name="allPrograms">
    /// Todos os programas instalados, para descobrir quem divide a pasta de
    /// instalacao. Pode ser nulo: sem a lista, o aviso de pasta compartilhada
    /// simplesmente nao existe.
    /// </param>
    /// <param name="leftovers">
    /// As sobras encontradas, para descobrir as pastas de dados do usuario.
    /// Pode ser nulo: sem as sobras, o aviso de dados do usuario nao existe.
    /// </param>
    public static IReadOnlyList<UninstallAdvisory> Build(
        InstalledProgram program,
        IEnumerable<InstalledProgram>? allPrograms = null,
        IEnumerable<LeftoverItem>? leftovers = null)
    {
        var advisories = new List<UninstallAdvisory>();

        var shared = SharedInstallLocationAdvisory(program, allPrograms);
        if (shared is not null) advisories.Add(shared);

        var system = SystemComponentAdvisory(program);
        if (system is not null) advisories.Add(system);

        var userData = UserDataAdvisory(leftovers);
        if (userData is not null) advisories.Add(userData);

        return advisories;
    }

    /// <summary>
    /// O aviso de programas que dividem a pasta de instalacao, ou nulo quando
    /// ninguem divide a pasta.
    /// </summary>
    /// <param name="program">O programa que vai ser desinstalado.</param>
    /// <param name="allPrograms">Todos os programas instalados.</param>
    public static UninstallAdvisory? SharedInstallLocationAdvisory(
        InstalledProgram program, IEnumerable<InstalledProgram>? allPrograms)
    {
        var shared = SharedInstallLocation(program, allPrograms);
        if (shared.Count == 0) return null;

        return new UninstallAdvisory(
            UninstallAdvisoryKind.SharedInstallLocation,
            AdvisoryLabels.Title(UninstallAdvisoryKind.SharedInstallLocation),
            AdvisoryLabels.SharedInstallLocation(shared),
            shared);
    }

    /// <summary>
    /// O aviso de remocao de componente do sistema, ou nulo quando o programa
    /// nao e componente do sistema.
    /// </summary>
    /// <param name="program">O programa que vai ser desinstalado.</param>
    public static UninstallAdvisory? SystemComponentAdvisory(InstalledProgram program)
    {
        if (!program.IsSystemComponent) return null;

        return new UninstallAdvisory(
            UninstallAdvisoryKind.SystemComponent,
            AdvisoryLabels.Title(UninstallAdvisoryKind.SystemComponent),
            AdvisoryLabels.SystemComponent(),
            Array.Empty<string>());
    }

    /// <summary>
    /// O aviso de pastas que podem conter dados do usuario, ou nulo quando
    /// nenhuma sobra caiu em pasta de conteudo do usuario.
    /// </summary>
    /// <param name="leftovers">As sobras encontradas.</param>
    public static UninstallAdvisory? UserDataAdvisory(IEnumerable<LeftoverItem>? leftovers)
    {
        var folders = UserDataFolders(leftovers);
        if (folders.Count == 0) return null;

        return new UninstallAdvisory(
            UninstallAdvisoryKind.UserData,
            AdvisoryLabels.Title(UninstallAdvisoryKind.UserData),
            AdvisoryLabels.UserData(folders),
            folders);
    }

    /// <summary>
    /// Os outros programas instalados na MESMA pasta de instalacao.
    ///
    /// A comparacao e por igualdade exata do caminho normalizado: um programa
    /// instalado em "C:\Program Files" e outro em "C:\Program Files\App" nao
    /// dividem a pasta — e treated-los como dividindo transformaria o aviso
    /// em ruido.
    /// </summary>
    /// <param name="program">O programa que vai ser desinstalado.</param>
    /// <param name="allPrograms">Todos os programas instalados.</param>
    public static IReadOnlyList<string> SharedInstallLocation(
        InstalledProgram program, IEnumerable<InstalledProgram>? allPrograms)
    {
        if (allPrograms is null) return Array.Empty<string>();

        var mine = PathUtil.Normalize(program.InstallLocation);
        if (mine.Length == 0) return Array.Empty<string>();

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var other in allPrograms)
        {
            if (other is null) continue;
            if (ReferenceEquals(other, program)) continue;

            // A mesma entrada lida duas vezes (a lista pode vir de varias
            // fontes) nao e um programa vizinho.
            if (!string.IsNullOrEmpty(other.RegistryPath)
                && !string.IsNullOrEmpty(program.RegistryPath)
                && string.Equals(other.RegistryPath, program.RegistryPath, StringComparison.OrdinalIgnoreCase))
                continue;

            var theirs = PathUtil.Normalize(other.InstallLocation);
            if (theirs.Length == 0) continue;
            if (!string.Equals(mine, theirs, StringComparison.OrdinalIgnoreCase)) continue;

            var name = string.IsNullOrWhiteSpace(other.DisplayName) ? theirs : other.DisplayName.Trim();
            if (seen.Add(name)) result.Add(name);
        }

        return result;
    }

    /// <summary>
    /// As sobras que estao em pastas de conteudo do usuario.
    /// </summary>
    /// <param name="leftovers">As sobras encontradas.</param>
    public static IReadOnlyList<string> UserDataFolders(IEnumerable<LeftoverItem>? leftovers)
    {
        if (leftovers is null) return Array.Empty<string>();

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in leftovers)
        {
            if (item is null) continue;
            if (!string.Equals(item.Kind, "Pasta", StringComparison.OrdinalIgnoreCase)) continue;
            if (!IsUserDataFolder(item.Path)) continue;
            if (seen.Add(item.Path)) result.Add(item.Path);
        }

        return result;
    }

    /// <summary>
    /// Verdadeiro quando a pasta pode conter dados do usuario e documentos.
    ///
    /// O caminho precisa ter a forma "C:\Users\&lt;perfil&gt;\&lt;conteudo&gt;\...".
    /// A pasta "Users" em si e a pasta de um perfil inteiro sao protegidas
    /// pelo motor; o que interessa e o conteudo DENTRO delas.
    /// </summary>
    /// <param name="folderPath">O caminho da pasta.</param>
    public static bool IsUserDataFolder(string folderPath)
    {
        var full = PathUtil.Normalize(folderPath);
        if (full.Length == 0) return false;

        var parts = full.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4) return false;

        // "C:" (ou outra unidade) + "Users" + perfil + conteudo
        if (!IsDriveRoot(parts[0])) return false;
        if (!parts[1].Equals("Users", StringComparison.OrdinalIgnoreCase)) return false;

        // "C:\Users\Public\..." e a pasta compartilhada da maquina.
        if (parts[2].Equals("Public", StringComparison.OrdinalIgnoreCase)) return true;

        return UserContentFolders.Contains(parts[3]);
    }

    /// <summary>
    /// Verdadeiro quando o segmento e a raiz de uma unidade ("C:", "D:"...).
    /// </summary>
    private static bool IsDriveRoot(string segment) =>
        segment.Length >= 2 && segment[1] == ':' && char.IsLetter(segment[0]);
}
