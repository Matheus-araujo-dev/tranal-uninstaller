using System.IO;
using System.Windows;
using Tranal.Uninstaller.Core;
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

        // Acao de manutencao pedida pela linha de comando (usada pelo
        // instalador). Precisa rodar ANTES de tudo: sem janela, sem tema, sem
        // idioma — so a acao pedida.
        if (ManutencaoExecutar(e.Args))
        {
            Shutdown();
            return;
        }

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
        // Precisa acontecer ANTES de a janela principal ser exibida: assim a
        // primeira pintura ja sai com o tema escolhido.
        PerguntarTemaNoPrimeiroLancamento();

        window.Show();

        // Entrada pelo menu de contexto do Explorer: o alvo vem na linha de
        // comando. Sem este tratamento, o clique no Explorer apenas abria a
        // janela principal e jogava o alvo fora.
        var target = UninstallTargetArgument.Extract(e.Args);
        if (target.Length > 0)
        {
            var forced = new ForcedUninstallWindow { Owner = window };
            forced.Show();
            forced.Analyze(target);
        }
    }

    /// <summary>
    /// Executa uma acao de manutencao pedida pela linha de comando e devolve
    /// true quando ela foi executada (a aplicacao deve encerrar).
    ///
    /// O desinstalador usa "--unregister-context-menu": deixar a entrada do
    /// menu de contexto apontando para um executavel apagado e uma referencia
    /// orfa. A remocao e feita pelo proprio codigo do programa
    /// (<see cref="ExplorerContextMenu"/>) e nao pelo script do instalador —
    /// a regra do registro fica em um lugar so.
    /// </summary>
    private static bool ManutencaoExecutar(string[] args)
    {
        foreach (var arg in args ?? Array.Empty<string>())
        {
            if (string.Equals(arg, "--unregister-context-menu", StringComparison.OrdinalIgnoreCase))
            {
                ExplorerContextMenu.RemoveRegistryScript();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Pergunta o tema na primeira execucao, como o Revo Pro faz.
    ///
    /// Duas condicoes da partida a frio (sem arquivo de preferencias):
    ///  1. A janela principal ainda nao foi exibida, e o WPF nao aceita uma
    ///     janela nao exibida como Owner -&gt; o dialogo abre sem Owner.
    ///  2. Durante a pergunta o dialogo e a unica janela do app: com o modo
    ///     "encerrar na ultima janela fechada" o app terminaria ao fim da
    ///     pergunta. O modo e trocado durante a pergunta e restaurado em
    ///     seguida.
    ///
    /// O tema e aplicado aqui, antes de a janela principal ser exibida: assim
    /// a primeira pintura ja sai com o tema escolhido.
    /// </summary>
    private void PerguntarTemaNoPrimeiroLancamento()
    {
        var modoAnterior = ShutdownMode;
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        try
        {
            if (AppSettingsStore.Current.FirstRunCompleted) return;

            var dialog = new FirstRunWindow();
            if (dialog.ShowDialog() == true)
                ThemeManager.Apply(dialog.SelectedTheme);
            AppSettingsStore.Update(s => s.FirstRunCompleted = true);
        }
        finally
        {
            ShutdownMode = modoAnterior;
        }
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
