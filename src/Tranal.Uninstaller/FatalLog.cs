using System.IO;
using System.Runtime.CompilerServices;

namespace Tranal.Uninstaller;

/// <summary>
/// Registro de falhas fatais.
///
/// O inicializador de modulo roda ANTES do Application ser construido — por isso
/// uma falha ao carregar o App.xaml (que acontece antes de OnStartup) tambem
/// fica registrada em disco. Sem isso, o app some sem deixar rastro.
/// </summary>
internal static class FatalLog
{
    /// <summary>Caminho do log de falhas.</summary>
    public static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Tranal Group", "Tranal Uninstaller Pro", "erro-inicializacao.log");

    [ModuleInitializer]
    internal static void Register()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Write(args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Write(args.Exception);
            args.SetObserved();
        };
    }

    /// <summary>Grava a falha em disco (best effort).</summary>
    public static void Write(Exception? exception)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.AppendAllText(LogPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n{exception}\n\n{new string('-', 60)}\n");
        }
        catch { /* a UI nao consegue mostrar o erro em sessao nao interativa */ }
    }
}
