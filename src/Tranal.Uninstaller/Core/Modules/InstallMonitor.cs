using System.IO;
using System.Text.Json;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// Monitor de instalacao: equivalente ao "AppBar" do Revo Uninstaller Pro.
///
/// O Revo mostra uma barra que acompanha, ao vivo, tudo o que o instalador
/// cria no sistema durante a instalacao e, ao fim, salva o log. O Tranal ja
/// tinha a fotografia do sistema (<see cref="SystemSnapshot"/>), o
/// armazenamento dos logs (<see cref="TraceStore"/>) e a desinstalacao a partir
/// do log (<see cref="TracedPrograms"/>), mas nao tinha o acompanhamento AO
/// VIVO de uma instalacao feita pelo usuario (fora do Tranal).
///
/// Esta classe preenche exatamente essa lacuna: o usuario comeca o
/// monitoramento, instala o programa por conta propria, e ao parar o
/// monitoramento o log do que mudou no sistema e gravado na base de logs —
/// de onde a desinstalacao pelo modulo de programas monitorados consome.
///
/// O log gerado aqui e o MESMO formato do log do
/// <see cref="TracedPrograms"/> (Tranal.TraceLog.v1): um arquivo JSON com o
/// payload do que nasceu no sistema. A diferenca e de escopo: o log do
/// <see cref="TracedPrograms"/> cobre a execucao de um instalador dirigido
/// pelo Tranal (antes/depois); o log deste monitor cobre a janela de
/// monitoramento escolhida pelo usuario.
/// </summary>
public sealed class InstallMonitor
{
    /// <summary>Intervalo padrao entre capturas (segundos).</summary>
    public const int DefaultIntervalSeconds = 30;

    private readonly object _gate = new();
    private readonly Dictionary<string, TraceStore.TraceFileEntry> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _folders = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _registryKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _timer;

    private SystemSnapshot? _previous;
    private int _capturing;
    private bool _running = true;

    private InstallMonitor(string programName, int intervalSeconds)
    {
        ProgramName = programName ?? string.Empty;
        IntervalSeconds = intervalSeconds <= 0 ? DefaultIntervalSeconds : intervalSeconds;

        _previous = SystemSnapshot.Capture();

        _timer = new Timer(Capture, null, Timeout.Infinite, Timeout.Infinite);
        _timer.Change(TimeSpan.FromSeconds(IntervalSeconds), TimeSpan.FromSeconds(IntervalSeconds));
    }

    /// <summary>Nome do programa monitorado (o nome usado na tela do Revo).</summary>
    public string ProgramName { get; }

    /// <summary>Intervalo entre capturas, em segundos.</summary>
    public int IntervalSeconds { get; }

    /// <summary>True enquanto o monitoramento estiver ativo.</summary>
    public bool IsRunning
    {
        get { lock (_gate) return _running; }
    }

    /// <summary>Quantidade de arquivos novos detectados ate agora.</summary>
    public int ChangedFileCount
    {
        get { lock (_gate) return _files.Count; }
    }

    /// <summary>Quantidade de pastas novas detectadas ate agora.</summary>
    public int ChangedFolderCount
    {
        get { lock (_gate) return _folders.Count; }
    }

    /// <summary>Quantidade de chaves de registro novas detectadas ate agora.</summary>
    public int ChangedRegistryKeyCount
    {
        get { lock (_gate) return _registryKeys.Count; }
    }

    /// <summary>Resumo do que foi observado (texto para a interface).</summary>
    public string StatusText
    {
        get
        {
            lock (_gate)
            {
                return $"Monitorando: { _files.Count } arquivo(s), { _folders.Count } pasta(s), "
                       + $"{ _registryKeys.Count } chave(s) de registro.";
            }
        }
    }

    /// <summary>
    /// Comeca o monitoramento. A primeira fotografia do sistema (a linha de
    /// base) e tirada aqui.
    /// </summary>
    /// <param name="programName">Nome do programa que sera instalado.</param>
    /// <param name="intervalSeconds">Intervalo entre capturas.</param>
    public static InstallMonitor? Start(string programName, int intervalSeconds = DefaultIntervalSeconds)
    {
        try
        {
            return new InstallMonitor(programName, intervalSeconds);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Para o monitoramento e grava o log com tudo o que foi observado.
    /// Devolve o log gravado (null quando o log nao pode ser salvo).
    /// </summary>
    public TracedInstall? StopAndSave()
    {
        try
        {
            StopTimer();

            // Captura final: o que mudou depois da ultima captura periodica.
            Capture();

            TraceStore.TracePayload payload;
            lock (_gate)
            {
                payload = new TraceStore.TracePayload
                {
                    Files = _files.Values.ToList(),
                    Folders = _folders.ToList(),
                    RegistryKeys = _registryKeys.ToList(),
                };
            }

            if (payload.Files.Count == 0 && payload.Folders.Count == 0 && payload.RegistryKeys.Count == 0)
                return null;

            var id = Guid.NewGuid().ToString("D");
            var install = new TracedInstall
            {
                Id = id,
                ProgramName = ProgramName,
                Timestamp = DateTime.UtcNow,
                InstallerPath = string.Empty,
                LogPath = TraceStore.PathForId(id),
                FileCount = payload.Files.Count,
                FolderCount = payload.Folders.Count,
                RegistryKeyCount = payload.RegistryKeys.Count,
                Imported = false,
            };

            var saved = TraceStore.Save(install, TraceStore.SerializePayload(payload));
            if (string.IsNullOrWhiteSpace(saved)) return null;

            if (!string.Equals(saved, install.LogPath, StringComparison.OrdinalIgnoreCase))
            {
                install = new TracedInstall
                {
                    Id = install.Id,
                    ProgramName = install.ProgramName,
                    Timestamp = install.Timestamp,
                    InstallerPath = install.InstallerPath,
                    LogPath = saved,
                    FileCount = install.FileCount,
                    FolderCount = install.FolderCount,
                    RegistryKeyCount = install.RegistryKeyCount,
                    Imported = install.Imported,
                };
            }

            return install;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Para o monitoramento sem gravar log.</summary>
    public void Stop()
    {
        StopTimer();
    }

    // ---------------------------------------------------------------------
    // Captura periodica
    // ---------------------------------------------------------------------

    /// <summary>Captura o estado do sistema e acumula o que nasceu desde a captura anterior.</summary>
    private void Capture(object? state = null)
    {
        // Nunca duas capturas ao mesmo tempo: a fotografia do sistema e cara.
        if (Interlocked.CompareExchange(ref _capturing, 1, 0) != 0) return;

        try
        {
            var current = SystemSnapshot.Capture();
            var previous = _previous;
            _previous = current;

            if (previous is null) return;

            var diff = SystemSnapshot.Diff(previous, current);

            lock (_gate)
            {
                foreach (var file in diff.NewFiles)
                {
                    _files[file.Path] = new TraceStore.TraceFileEntry
                    {
                        Path = file.Path,
                        SizeBytes = file.SizeBytes,
                        ModifiedUtc = file.ModifiedUtc,
                    };
                }

                foreach (var folder in diff.NewFolders) _folders.Add(folder);
                foreach (var key in diff.NewRegistryKeys) _registryKeys.Add(key.Path);
            }
        }
        catch
        {
            // Uma captura falha nao interrompe o monitoramento.
        }
        finally
        {
            Interlocked.Exchange(ref _capturing, 0);
        }
    }

    /// <summary>Desliga o temporizador de captura.</summary>
    private void StopTimer()
    {
        lock (_gate)
        {
            if (!_running) return;
            _running = false;
        }

        try { _timer.Change(Timeout.Infinite, Timeout.Infinite); }
        catch { /* temporizador ja parado */ }

        try { _timer.Dispose(); }
        catch { /* temporizador ja descartado */ }
    }
}
