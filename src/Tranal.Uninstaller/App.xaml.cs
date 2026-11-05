using System.IO;
using System.Windows;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Core.Topmost;
using Tranal.Uninstaller.Theming;
using Tranal.Uninstaller.Ui;

namespace Tranal.Uninstaller;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        CultureInfoSetup();

        AppDomain.CurrentDomain.UnhandledException += (_, args) => FatalLog.Write(args.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, args) =>
        {
            FatalLog.Write(args.Exception);
            args.Handled = true;
        };

        // Idioma e tema sao aplicados ANTES de a janela principal ser criada:
        // assim a primeira pintura ja sai no tema/idioma corretos.
        Loc.SetLanguage(AppSettingsStore.Current.Language);
        ThemeManager.ApplyStored();

        // A entrada no menu de contexto do Windows segue a preferencia
        // "Mostrar comandos no menu de contexto do Windows" do Revo.
        ExplorerContextMenu.Apply(AppSettingsStore.Current.ShowContextMenuCommands);

        // Sonda de diagnostico dos recursos de tema (so com a variavel de
        // ambiente ligada): grava o resultado em %TEMP%\tranal-theme-probe.txt.
        if (Environment.GetEnvironmentVariable("TRANAL_THEME_PROBE") == "1")
            Theming.ThemeProbe.Run();

        var window = new MainWindow();

        // Escolha de tema no primeiro lancamento (como no Revo Pro).
        if (!AppSettingsStore.Current.FirstRunCompleted)
        {
            var dialog = new FirstRunWindow { Owner = window };
            if (dialog.ShowDialog() == true)
                ThemeManager.Apply(dialog.SelectedTheme);
            AppSettingsStore.Update(s => s.FirstRunCompleted = true);
        }

        window.Show();
    }

    /// <summary>Grava a falha fatal em disco (a UI nao consegue mostrar o erro em sessao nao interativa).</summary>
    private static void LogFatal(Exception? exception) => FatalLog.Write(exception);

    /// <summary>Mantem a formatacao numerica independente do locale do sistema.</summary>
    private static void CultureInfoSetup()
    {
        var culture = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
