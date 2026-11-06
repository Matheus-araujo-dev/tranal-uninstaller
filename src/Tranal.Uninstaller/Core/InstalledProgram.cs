using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Uma entrada de programa instalado, nao um aplicativo instalado.
/// O mesmo vocabulario de campos do modulo "Todos os Programas" do Revo.
/// </summary>
public sealed class InstalledProgram
{
    public string DisplayName { get; set; } = string.Empty;
    public string DisplayVersion { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
    public string UninstallString { get; set; } = string.Empty;
    public string QuietUninstallString { get; set; } = string.Empty;
    public string DisplayIcon { get; set; } = string.Empty;
    public string UrlInfoAbout { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public string RegistryKey { get; set; } = string.Empty;
    public string Installer { get; set; } = string.Empty;

    /// <summary>Data de instalacao no formato dd/MM/yyyy, vazia quando desconhecida.</summary>
    public string InstallDate { get; set; } = string.Empty;

    /// <summary>Tamanho estimado em bytes. 0 quando desconhecido.</summary>
    public long SizeBytes { get; set; }

    /// <summary>"64-bits", "32-bits" ou "" quando desconhecido.</summary>
    public string Bits { get; set; } = string.Empty;

    public bool IsSystemComponent { get; set; }
    public bool IsWindowsInstaller { get; set; }
    public bool IsUpgrade { get; set; }

    /// <summary>
    /// True quando o registro marca o programa como protegido: o valor
    /// <c>NoRemove</c> existe na chave de desinstalacao.
    ///
    /// O Windows usa esse valor para dizer "nao remova". O Revo Uninstaller
    /// Pro 5.5.4 tambem o le e sinaliza a entrada como protegida.
    /// </summary>
    public bool IsProtected { get; set; }

    /// <summary>
    /// A saude do desinstalador registrado: existe, falta ou aponta para
    /// um arquivo que nao existe mais.
    /// </summary>
    public UninstallerHealth UninstallerHealth { get; set; } = UninstallerHealth.Ok;

    /// <summary>True quando veio do catalogo MSI (installer de pacote).</summary>
    public bool IsMsi { get; set; }

    /// <summary>
    /// O tipo do desinstalador, classificado por <see cref="UninstallerClassifier"/>.
    ///
    /// Nao e um detalhe cosmetico: e o que decide a estrategia de
    /// desinstalacao (o <c>ProductCode</c> de um MSI, o caminho do
    /// desinstalador de um Inno Setup, a ausencia de desinstalador de um
    /// aplicativo da Store).
    /// </summary>
    public UninstallerKind Kind { get; set; } = UninstallerKind.Unknown;

    /// <summary>
    /// O codigo do produto (GUID) do Windows Installer, quando existe.
    /// Vem do valor <c>ProductCode</c> do registro ou e extraido do comando
    /// de desinstalacao.
    /// </summary>
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>
    /// O comando de desinstalacao silenciosa, quando existe um jeito
    /// conhecido de silenciar este desinstalador.
    ///
    /// Vem do valor <c>QuietUninstallString</c> do registro quando ele
    /// existe; senao, do switch da familia do desinstalador, escolhido pela
    /// classificacao (<see cref="QuietUninstall"/>).
    ///
    /// O Tranal **nao** troca o desinstalador nativo por conta propria: ele
    /// oferece o comando silencioso, e o usuario escolhe.
    /// </summary>
    public string QuietUninstallCommand { get; set; } = string.Empty;

    /// <summary>Data de instalacao como DateTime? para ordenacao por recencia.</summary>
    public DateTime? InstallDateValue { get; set; }

    /// <summary>Chave de registro que originou a entrada (para remocao e backup).</summary>
    [JsonIgnore]
    public string RegistryPath { get; set; } = string.Empty;

    [JsonIgnore]
    public string RegistryHive { get; set; } = string.Empty;

    [JsonIgnore]
    public RegistryView View { get; set; } = RegistryView.Default;

    /// <summary>Versao formatada como o Revo mostra ("No Version" quando vazia).</summary>
    public string VersionOrNoVersion => string.IsNullOrWhiteSpace(DisplayVersion)
        ? "No Version"
        : DisplayVersion;

    /// <summary>Tamanho formatado como o Revo mostra (KB, MB, GB).</summary>
    public string SizeText
    {
        get
        {
            if (SizeBytes <= 0) return string.Empty;
            double kb = SizeBytes / 1024.0;
            if (kb < 1024) return $"{kb:0.##} KB";
            double mb = kb / 1024.0;
            if (mb < 1024) return $"{mb:0.##} MB";
            return $"{mb / 1024.0:0.##} GB";
        }
    }

    /// <summary>Caminho do icone extraido do campo DisplayIcon.</summary>
    public string IconPath
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DisplayIcon)) return string.Empty;
            var raw = DisplayIcon.Trim();
            var comma = raw.LastIndexOf(',');
            if (comma > 0) raw = raw[..comma].Trim();
            return raw.Trim('"');
        }
    }
}
