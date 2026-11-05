using System.Text.RegularExpressions;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// O tipo do desinstalador de um programa.
///
/// A classificação não é um detalhe cosmético: **é o que decide a
/// estratégia**. Um desinstalador do Windows Installer recebe o
/// <c>ProductCode</c>, um Inno Setup expõe o caminho do próprio
/// desinstalador, um aplicativo da Store não tem desinstalador nenhum — e é
/// essa diferença que decide o que a varredura de sobras pode procurar com
/// segurança.
///
/// O vocabulário segue o Bulk Crap Uninstaller 6.3.0
/// (<c>docs/auditoria/BCU.md</c>, seção 2.1), que é o concorrente de código
/// aberto e portanto a referência de implementação. Os nomes são **fixos**,
/// independentes do idioma: a coluna exportada continua legível por
/// qualquer ferramenta e a tela não ganha uma segunda tradução que possa
/// divergir do código.
/// </summary>
public enum UninstallerKind
{
    /// <summary>Não foi possível identificar o tipo do desinstalador.</summary>
    Unknown,

    /// <summary>
    /// Windows Installer (<c>msiexec.exe</c>). O produto tem um
    /// <c>ProductCode</c> (GUID) que identifica a instalação.
    /// </summary>
    MsiExec,

    /// <summary>
    /// Inno Setup. O desinstalador do Inno é o <c>unins000.exe</c>, nome
    /// bastante distintoivo — a identificação é confiável.
    /// </summary>
    InnoSetup,

    /// <summary>
    /// Nullsoft Scriptable Install System. O desinstalador padrão do NSIS é
    /// o <c>uninstall.exe</c>.
    ///
    /// Atenção: esse nome **não é exclusivo** do NSIS — um instalador
    /// próprio também pode se chamar <c>uninstall.exe</c>. A classificação é
    /// uma pista, não uma garantia.
    /// </summary>
    Nsis,

    /// <summary>InstallShield (<c>setup.exe /x{…}</c>).</summary>
    InstallShield,

    /// <summary>SDBInst (pacote de driver do Windows Installer).</summary>
    SdbInst,

    /// <summary>Aplicativo distribuído pela Steam.</summary>
    Steam,

    /// <summary>Aplicativo empacotado da Windows Store (MSIX).</summary>
    StoreApp,

    /// <summary>Pacote gerenciado pelo Chocolatey.</summary>
    Chocolatey,

    /// <summary>Aplicativo da Oculus Store.</summary>
    Oculus,

    /// <summary>Script PowerShell.</summary>
    PowerShell,

    /// <summary>
    /// Desinstalação por simples remoção de arquivos: não há desinstalador
    /// de verdade, só um comando que apaga a pasta.
    /// </summary>
    SimpleDelete,
}

/// <summary>
/// Classifica o desinstalador de um programa.
///
/// A classificação é uma **heurística** sobre o comando de desinstalação, o
/// caminho de instalação e o que o registro declara. Ela nunca decide sozinha
/// o que será removido: apenas descreve o tipo, para a lista mostrar e para
/// a estratégia de desinstalação poder ser escolhida por tipo.
///
/// A ordem das regras é fixa e testada. Cada regra tem um motivo
/// observável:
/// </summary>
public static class UninstallerClassifier
{
    /// <summary>
    /// O desinstalador do Inno Setup chama-se <c>unins000.exe</c> (o número
    /// varia). É o nome mais distintoivo entre os empacotadores — por isso a
    /// regra vem antes das demais.
    /// </summary>
    /// <summary>
    /// O executavel do comando, em qualquer posicao da linha de comando.
    /// Aceita o caminho entre aspas ou solto e devolve o NOME DO ARQUIVO.
    ///
    /// E o nome do arquivo, e nao a linha inteira, que decide a regra: o
    /// desinstalador do NSIS se chama "uninstall.exe", e um
    /// "EAUninstall.exe" nao e o mesmo arquivo so porque contem a mesma
    /// palavra.
    /// </summary>
    private static readonly Regex ExecutableToken =
        new(@"(?:""(?<quoted>[^""]+\.(?:exe|com|bat|cmd))""|(?<bare>[^\s""]+\.(?:exe|com|bat|cmd)))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// O desinstalador do Inno Setup chama-se <c>unins000.exe</c> (o numero
    /// varia). E o nome mais distintivo entre os empacotadores - por isso a
    /// regra vem antes das demais.
    /// </summary>
    private static readonly Regex InnoUninstaller =
        new(@"^unins\d{0,3}\.exe$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// O desinstalador padrao do NSIS chama-se <c>uninstall.exe</c>.
    ///
    /// O casamento e pelo NOME DO ARQUIVO, nao por substring: um
    /// <c>EAUninstall.exe</c> nao e um <c>uninstall.exe</c> do NSIS.
    /// </summary>
    private static readonly Regex NsisUninstaller =
        new(@"^uninstall\.exe$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// O InstallShield desinstala por <c>setup.exe /x{…}</c>, dentro de uma
    /// pasta "InstallShield Installation Information". Exigir o <c>/x</c>
    /// evita confundir com o <c>setup.exe</c> de qualquer outro instalador.
    /// </summary>
    private static readonly Regex InstallShieldSetup =
        new(@"setup\.exe\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Um GUID entre chaves, como o <c>ProductCode</c> de um MSI.</summary>
    private static readonly Regex GuidPattern =
        new(@"\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\}",
            RegexOptions.CultureInvariant);

    /// <summary>
    /// Classifica o desinstalador de um programa já lido do registro.
    /// </summary>
    public static UninstallerKind Classify(InstalledProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);

        return Classify(
            program.UninstallString,
            program.InstallLocation,
            program.IsMsi,
            program.IsWindowsInstaller);
    }

    /// <summary>
    /// Classifica a partir dos sinais brutos da entrada do registro.
    ///
    /// A ordem importa e é fixa:
    /// <list type="number">
    /// <item>Windows Installer — o próprio registro ou o comando declaram.</item>
    /// <item>Windows Store — a pasta de instalação está em <c>WindowsApps</c>.</item>
    /// <item>Inno Setup — o desinstalador é o <c>unins000.exe</c>.</item>
    /// <item>InstallShield e SDBInst — a marca aparece no caminho ou no comando.</item>
    /// <item>Lojas de aplicativo — Steam, Chocolatey e Oculus.</item>
    /// <item>PowerShell — o desinstalador é um script.</item>
    /// <item>NSIS — o desinstalador é o <c>uninstall.exe</c>.</item>
    /// <item>Remoção simples — o comando só apaga arquivos.</item>
    /// </list>
    /// </summary>
    public static UninstallerKind Classify(
        string? uninstallString,
        string? installLocation = null,
        bool isMsi = false,
        bool isWindowsInstaller = false)
    {
        var command = (uninstallString ?? string.Empty).Trim();

        // O Windows Installer se declara de duas maneiras: o valor
        // "WindowsInstaller" do registro ou o próprio comando msiexec.
        if (isMsi || isWindowsInstaller || Mentions(command, "msiexec"))
            return UninstallerKind.MsiExec;

        // Aplicativo da Store: a pasta de instalação está no WindowsApps.
        if (installLocation is not null
            && installLocation.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase))
            return UninstallerKind.StoreApp;

        // Inno Setup: nome de arquivo distintoivo.
        if (InnoUninstaller.IsMatch(ExecutableFileName(command)))
            return UninstallerKind.InnoSetup;

        // SDBInst antes de InstallShield: o sdbinst.exe é ferramenta do
        // InstallShield e o nome dele é mais específico.
        if (Mentions(command, "sdbinst"))
            return UninstallerKind.SdbInst;

        if (Mentions(command, "installshield")
            || (InstallShieldSetup.IsMatch(command) && Mentions(command, "/x")))
            return UninstallerKind.InstallShield;

        // Lojas de aplicativo: o nome do cliente aparece no comando.
        if (Mentions(command, "steam"))
            return UninstallerKind.Steam;
        if (Mentions(command, "choco"))
            return UninstallerKind.Chocolatey;
        if (Mentions(command, "oculus"))
            return UninstallerKind.Oculus;

        // Script PowerShell.
        if (Mentions(command, "powershell") || command.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            return UninstallerKind.PowerShell;

        // NSIS: convenção do nome do desinstalador.
        if (NsisUninstaller.IsMatch(ExecutableFileName(command)))
            return UninstallerKind.Nsis;

        // Remoção simples: o comando manda apagar, sem desinstalador.
        if (IsSimpleDelete(command))
            return UninstallerKind.SimpleDelete;

        return UninstallerKind.Unknown;
    }

    /// <summary>
    /// Extrai o <c>ProductCode</c> (GUID) do comando de desinstalação.
    /// Devolve string vazia quando não há GUID.
    /// </summary>
    public static string ExtractProductCode(string? uninstallString)
    {
        var match = GuidPattern.Match(uninstallString ?? string.Empty);
        return match.Success ? match.Value : string.Empty;
    }

    /// <summary>
    /// True quando o comando só apaga arquivos, sem chamar um desinstalador
    /// de verdade. São os casos em que o "desinstalador" do registro é na
    /// verdade um <c>cmd /c del</c> ou <c>rmdir</c>.
    /// </summary>
    private static bool IsSimpleDelete(string command)
    {
        if (command.Length == 0) return false;

        // Sem o interpretador de comandos não é um "cmd /c ...".
        if (!Mentions(command, "cmd")) return false;

        return Mentions(command, "del ")
            || Mentions(command, "erase ")
            || Mentions(command, "rmdir")
            || Mentions(command, " rd ")
            || command.TrimEnd().EndsWith(" rd", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True quando o texto menciona o termo, ignorando a caixa.
    /// </summary>
    /// <summary>
    /// O nome do arquivo do executavel do comando.
    ///
    /// Devolve string vazia quando o comando nao tem executavel — o que
    /// acontece, por exemplo, no comando "winget uninstall --product-code …".
    /// </summary>
    private static string ExecutableFileName(string command)
    {
        var match = ExecutableToken.Match(command);
        if (!match.Success) return string.Empty;

        var path = match.Groups["quoted"].Success
            ? match.Groups["quoted"].Value
            : match.Groups["bare"].Value;

        // O token solto pode ter vindo colado num argumento (ex.: "uninstall.exe"/S),
        // entao o nome e o que vem depois da ultima barra.
        var cut = path.LastIndexOfAny(new[] { '\\', '/' });
        return cut >= 0 ? path[(cut + 1)..] : path;
    }

    private static bool Mentions(string text, string term) =>
        text.Contains(term, StringComparison.OrdinalIgnoreCase);
}
