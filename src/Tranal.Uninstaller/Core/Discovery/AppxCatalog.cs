using Tranal.Uninstaller.Core.Modules;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Um pacote empacotado do Windows, no catalogo.
/// </summary>
public sealed class AppxCatalogItem
{
    /// <summary>O nome exibido do pacote.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>O nome completo do pacote.</summary>
    public string PackageFullName { get; init; } = string.Empty;

    /// <summary>O editor do pacote.</summary>
    public string Publisher { get; init; } = string.Empty;

    /// <summary>A versao do pacote.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>A pasta de instalacao do pacote.</summary>
    public string InstallLocation { get; init; } = string.Empty;

    /// <summary>
    /// True quando o Windows nao deixa remover o pacote.
    ///
    /// E o sinal medido de "pre-instalado": o proprio Windows marca o
    /// pacote como nao removivel, e e por isso que ele vem com o sistema.
    /// </summary>
    public bool NonRemovable { get; init; }

    /// <summary>True quando o pacote e um framework (dependencia de outro app).</summary>
    public bool IsFramework { get; init; }
}

/// <summary>
/// O catalogo dos pacotes empacotados do Windows.
///
/// O IObit Uninstall tem o catalogo embutido dos Appx pre-instalados: a
/// lista do que o Windows traz de fabrica. No Tranal, os pacotes MSIX
/// apareciam na varredura de programas, mas sem a distincao que importa
/// para o usuario — o que ele pode desinstalar e o que veio com o sistema.
///
/// A REGRA mora aqui, em um lugar so: o catalogo classifica, e a tela e a
/// CLI consomem a classificacao.
/// </summary>
public static class AppxCatalog
{
    /// <summary>
    /// Todos os pacotes empacotados, com a distincao entre os que o
    /// Windows nao deixa remover e os que o usuario instalou.
    /// </summary>
    public static IReadOnlyList<AppxCatalogItem> List() =>
        WindowsApps.List()
            .Select(a => new AppxCatalogItem
            {
                DisplayName = a.DisplayName,
                PackageFullName = a.PackageFullName,
                Publisher = a.Publisher,
                Version = a.Version,
                InstallLocation = a.InstallLocation,
                NonRemovable = a.NonRemovable,
                IsFramework = a.IsFramework,
            })
            .OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>
    /// Os pacotes que vieram com o Windows (o Windows nao deixa remover).
    /// </summary>
    public static IReadOnlyList<AppxCatalogItem> PreInstalled() =>
        List().Where(a => a.NonRemovable).ToList();

    /// <summary>
    /// Os pacotes que o usuario instalou e pode remover.
    /// </summary>
    public static IReadOnlyList<AppxCatalogItem> UserInstalled() =>
        List().Where(a => !a.NonRemovable && !a.IsFramework).ToList();
}
