using System;
using System.Windows;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Ui.Appearance;

/// <summary>
/// A tela de inicializacao (splash).
///
/// O Revo Pro mostra uma tela de inicializacao com o logotipo enquanto
/// carrega; o Tranal tambem. Ela e **sem botao e sem interacao**: e um
/// cartaz, e o usuario nao tem nada a fazer nela.
///
/// A tela fica no minimo por um instante. Uma tela que aparece e some em
/// trinta milissegundos parece um piscar na tela — e uma tela que some
/// rapido demais impede o usuario de ler o nome do aplicativo.
/// </summary>
public partial class SplashWindow : Window
{
    /// <summary>
    /// Quanto tempo a tela fica no minimo, para o usuario chegar a le-la.
    /// </summary>
    public static readonly TimeSpan MinimumVisible = TimeSpan.FromMilliseconds(900);

    /// <summary>
    /// Constroi a tela de inicializacao.
    /// </summary>
    public SplashWindow()
    {
        InitializeComponent();

        // Window.Title nao aceita binding: texto via codigo.
        Title = Loc.Get("App.Title");

        VersionText.Text = Loc.Get("Splash.Version", Version());
    }

    /// <summary>A versao do aplicativo, como o usuario ve.</summary>
    private static string Version()
    {
        var version = typeof(SplashWindow).Assembly.GetName().Version;
        return version is null ? string.Empty : version.ToString();
    }

    /// <summary>
    /// Fecha a tela de inicializacao, respeitando o tempo minimo.
    ///
    /// </summary>
    /// <param name="splash">A tela a fechar (pode ser nula).</param>
    /// <param name="startedAt">O instante em que a tela foi aberta.</param>
    public static void CloseAfter(SplashWindow? splash, DateTime startedAt)
    {
        if (splash is null) return;

        var elapsed = DateTime.UtcNow - startedAt;
        var remaining = MinimumVisible - elapsed;

        if (remaining <= TimeSpan.Zero)
        {
            splash.Close();
            return;
        }

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = remaining,
        };

        timer.Tick += (sender, _) =>
        {
            timer.Stop();
            splash.Close();
        };

        timer.Start();
    }
}
