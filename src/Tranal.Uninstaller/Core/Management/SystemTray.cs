using System.IO;
using Tranal.Uninstaller.Core.Settings;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// Uma regra da bandeja, com o nome da regra e se ela esta valendo.
/// </summary>
/// <param name="Id">O identificador fixo da regra.</param>
/// <param name="Enabled">Se a regra esta valendo.</param>
public sealed record TrayRule(string Id, bool Enabled);

/// <summary>
/// As regras da bandeja do sistema.
///
/// A bandeja e a forma de o Tranal continuar vivo sem ocupar a tela: o icone
/// fica na area de notificacoes e o programa volta com um clique.
///
/// Este e o **unico** lugar que sabe o que a bandeja faz. A tela de opcoes, a
/// tela da ferramenta, a linha de comando e o proprio icone leem estas
/// mesmas regras — nao existe uma segunda copia da condicao em cada
/// consumidor, que e como a bandeja e a janela passam a discordar entre si.
///
/// As preferencias ficam em <see cref="AppSettings"/>; este modulo apenas
/// traduz o nome da preferencia para o nome da regra.
/// </summary>
public static class SystemTray
{
    /// <summary>Mostra o icone do Tranal na bandeja do sistema.</summary>
    public static bool ShowIcon => AppSettingsStore.Current.ShowTrayIcon;

    /// <summary>
    /// Comeca minimizado na bandeja. Sem esta regra, o Tranal sempre abre a
    /// janela na tela.
    /// </summary>
    public static bool StartMinimized => AppSettingsStore.Current.StartMinimizedToTray;

    /// <summary>
    /// Minimiza na bandeja enquanto a desinstalacao roda. E o que o usuario
    /// quer quando o desinstalador abre varias janelas: a janela do Tranal
    /// atrapalharia o proprio desinstalador.
    /// </summary>
    public static bool MinimizeDuringUninstall =>
        AppSettingsStore.Current.MinimizeToTrayDuringUninstall;

    /// <summary>Volta para a janela com um clique no icone da bandeja.</summary>
    public static bool RestoreOnClick => AppSettingsStore.Current.RestoreTrayOnClick;

    /// <summary>
    /// O caminho do executavel do proprio Tranal. E dele que sai o icone da
    /// bandeja.
    /// </summary>
    public static string ExecutablePath
    {
        get
        {
            try
            {
                var path = Environment.ProcessPath;
                return string.IsNullOrWhiteSpace(path) ? string.Empty : path;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <summary>O texto que a bandeja mostra ao passar o mouse no icone.</summary>
    public static string Tooltip => "Tranal Uninstaller";

    /// <summary>
    /// As regras da bandeja, na ordem. A tela e a linha de comando mostram
    /// exatamente esta lista — nenhum dos dois tem uma lista propria.
    /// </summary>
    public static IReadOnlyList<TrayRule> Rules => new[]
    {
        new TrayRule("ShowIcon", ShowIcon),
        new TrayRule("StartMinimized", StartMinimized),
        new TrayRule("MinimizeDuringUninstall", MinimizeDuringUninstall),
        new TrayRule("RestoreOnClick", RestoreOnClick),
    };

    /// <summary>
    /// O que a bandeja esta fazendo agora, em uma frase.
    ///
    /// Quando o icone esta desligado, a bandeja nao existe — e o texto diz
    /// isso, em vez de fingir que existe um icone escondido.
    /// </summary>
    public static string Describe()
    {
        if (!ShowIcon) return "A bandeja do sistema esta desativada.";

        if (StartMinimized) return "O Tranal abre minimizado na bandeja.";

        return "O icone do Tranal aparece na bandeja do sistema.";
    }
}
