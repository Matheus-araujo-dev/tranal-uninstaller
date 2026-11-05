using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Topmost;

/// <summary>
/// Acoes oferecidas pela janela principal flutuante do Tranal.
/// Cada acao so aparece quando faz sentido para o alvo resolvido.
/// </summary>
public enum TopmostAction
{
    /// <summary>Desinstalar o programa instalado identificado.</summary>
    Uninstall,

    /// <summary>Impedir a inicializacao automatica do programa.</summary>
    StopAutoStart,

    /// <summary>Encerrar o processo do alvo.</summary>
    KillProcess,

    /// <summary>Encerrar o processo e apagar o executavel do alvo.</summary>
    KillAndDelete,

    /// <summary>Abrir a pasta que contem o arquivo do alvo.</summary>
    OpenFolder,

    /// <summary>Mostrar as informacoes do alvo.</summary>
    Info,

    /// <summary>Abrir as propriedades do arquivo do alvo.</summary>
    Properties,
}

/// <summary>
/// Alvo resolvido pela janela principal flutuante.
///
/// O alvo pode vir de um arquivo arrastado (atalho ou executavel) ou de uma
/// janela do Windows (HWND). O modelo e o mesmo nos dois casos: o que muda e
/// apenas de onde vem o caminho do executavel.
/// </summary>
public sealed class TopmostTarget
{
    /// <summary>True quando o alvo foi identificado (arquivo, processo ou programa).</summary>
    public bool Resolved { get; init; }

    /// <summary>Nome do processo do alvo (vazio quando o alvo nao tem processo).</summary>
    public string ProcessName { get; init; } = string.Empty;

    /// <summary>Identificador do processo do alvo (0 quando o alvo nao tem processo).</summary>
    public int ProcessId { get; init; }

    /// <summary>Caminho do executavel do alvo.</summary>
    public string ExecutablePath { get; init; } = string.Empty;

    /// <summary>Caminho do icone do alvo.</summary>
    public string IconPath { get; init; } = string.Empty;

    /// <summary>
    /// Programa instalado correspondente ao alvo. Null quando o alvo nao
    /// corresponde a nenhum programa instalado (a acao de desinstalar, nesse
    /// caso, responde <c>Topmost.NoPackage</c>).
    /// </summary>
    public InstalledProgram? Program { get; init; }

    /// <summary>Motivo da falha de resolucao (vazio quando o alvo foi resolvido).</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Nome exibido do alvo.</summary>
    public string DisplayName
    {
        get
        {
            if (Program is not null && Program.DisplayName.Length > 0) return Program.DisplayName;
            if (ProcessName.Length > 0) return ProcessName;
            return SafeFileName(ExecutablePath);
        }
    }

    /// <summary>Nome do arquivo do alvo (sem erro quando o caminho e invalido).</summary>
    private static string SafeFileName(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            return Path.GetFileName(path) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}

/// <summary>
/// Resolve o alvo apontado pela janela principal flutuante.
///
/// A resolucao tem duas entradas:
///   - por CAMINHO (arquivo arrastado sobre a janela): resolve atalhos .lnk e
///     identifica o programa instalado correspondente;
///   - por HWND (janela do Windows sob o ponteiro): identifica o processo e,
///     com ele, o executavel e o programa instalado.
///
/// O casamento com a lista de programas instalados e o MESMO do Modo Cacador
/// (<see cref="HunterMode"/>), que por sua vez usa o
/// <see cref="ProgramScanner"/>. Nao existe uma segunda interpretacao de
/// "este executavel pertence a um programa instalado".
/// </summary>
public static class TopmostTargetResolver
{
    /// <summary>Tempo (em segundos) durante o qual a varredura de inicializacao automatica e reaproveitada.</summary>
    private const int AutorunCacheSeconds = 60;

    private static readonly object AutorunGate = new();
    private static IReadOnlyList<AutorunItem>? _autorunCache;
    private static DateTime _autorunCacheStamp = DateTime.MinValue;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsWindow(IntPtr hWnd);

    /// <summary>
    /// Resolve um conjunto de arquivos arrastados sobre a janela.
    ///
    /// Regras do Revo (medidas): apenas UM arquivo por vez e aceito e uma pasta
    /// nunca e um alvo valido.
    /// </summary>
    public static TopmostTarget ResolveFromFiles(IReadOnlyList<string>? paths)
    {
        try
        {
            if (paths is null || paths.Count == 0) return Unresolved("Topmost.Unresolved");
            if (paths.Count > 1) return Unresolved("Topmost.OnlyOneFile");

            return ResolveFromFile(paths[0]);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao resolver os arquivos arrastados: {ex.Message}");
            return Unresolved("Topmost.Unresolved");
        }
    }

    /// <summary>
    /// Resolve um alvo pelo caminho do arquivo (executavel ou atalho .lnk).
    /// Nunca lanca: devolve um alvo nao resolvido com o motivo da falha.
    /// </summary>
    public static TopmostTarget ResolveFromFile(string? path)
    {
        try
        {
            var full = PathUtil.Normalize(path);
            if (full.Length == 0) return Unresolved("Topmost.Unresolved");

            // Uma pasta nunca e um alvo valido.
            if (Directory.Exists(full)) return Unresolved("Topmost.DroppedFolder");
            if (!File.Exists(full)) return Unresolved("Topmost.Unresolved");

            var executable = full;

            // Atalho (.lnk): o alvo real e o executavel apontado por ele.
            if (SafeExtension(full).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var target = ShellLinkReader.ResolveTarget(full);
                if (string.IsNullOrWhiteSpace(target)) return Unresolved("Topmost.Unresolved");
                executable = PathUtil.Normalize(target);
            }

            var program = MatchInstalledProgram(executable);
            var process = FindProcessByPath(executable);

            return new TopmostTarget
            {
                Resolved = true,
                ProcessName = process?.ProcessName ?? string.Empty,
                ProcessId = process?.Id ?? 0,
                ExecutablePath = executable,
                IconPath = ResolveIconPath(program, executable),
                Program = program,
            };
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao resolver o caminho '{path}': {ex.Message}");
            return Unresolved("Topmost.Unresolved");
        }
    }

    /// <summary>
    /// Resolve um alvo pela janela do Windows (HWND). Devolve o processo, o
    /// executavel e o programa instalado correspondente.
    /// </summary>
    public static TopmostTarget ResolveFromHwnd(IntPtr hwnd)
    {
        try
        {
            if (hwnd == IntPtr.Zero) return Unresolved("Topmost.Unresolved");

            try
            {
                if (!IsWindow(hwnd)) return Unresolved("Topmost.Unresolved");
            }
            catch
            {
                // A API pode falhar em sessoes nao interativas: segue pela PID.
            }

            uint pid = 0;
            try { GetWindowThreadProcessId(hwnd, out pid); }
            catch { return Unresolved("Topmost.Unresolved"); }

            if (pid == 0) return Unresolved("Topmost.Unresolved");

            Process? process = null;
            try { process = Process.GetProcessById((int)pid); }
            catch { /* processo ja encerrado ou sem permissao */ }

            var executable = string.Empty;
            var processName = string.Empty;

            try { if (process is not null) processName = process.ProcessName ?? string.Empty; }
            catch { }

            try
            {
                var module = process?.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(module)) executable = PathUtil.Normalize(module);
            }
            catch
            {
                // Processos protegidos nao entregam o caminho do executavel.
            }

            var program = executable.Length > 0 ? MatchInstalledProgram(executable) : null;

            return new TopmostTarget
            {
                Resolved = true,
                ProcessName = processName,
                ProcessId = (int)pid,
                ExecutablePath = executable,
                IconPath = ResolveIconPath(program, executable),
                Program = program,
            };
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao resolver a janela '{hwnd}': {ex.Message}");
            return Unresolved("Topmost.Unresolved");
        }
    }

    /// <summary>
    /// Devolve as acoes validas para o alvo. A acao de desinstalar so existe
    /// quando ha programa instalado; a de encerrar processo so existe quando
    /// ha processo.
    /// </summary>
    public static IReadOnlyList<TopmostAction> AvailableActions(TopmostTarget? target)
    {
        var actions = new List<TopmostAction>();
        if (target is null || !target.Resolved) return actions;

        if (target.Program is not null) actions.Add(TopmostAction.Uninstall);
        if (target.Program is not null && HasAutoStart(target.Program)) actions.Add(TopmostAction.StopAutoStart);
        if (target.ProcessId > 0)
        {
            actions.Add(TopmostAction.KillProcess);
            actions.Add(TopmostAction.KillAndDelete);
        }
        if (target.ExecutablePath.Length > 0)
        {
            actions.Add(TopmostAction.OpenFolder);
            actions.Add(TopmostAction.Properties);
        }

        actions.Add(TopmostAction.Info);
        return actions;
    }

    /// <summary>
    /// True quando o programa possui ponto de inicializacao automatica
    /// (pasta de inicializacao, chave Run, servico ou tarefa agendada).
    /// </summary>
    public static bool HasAutoStart(InstalledProgram? program) =>
        FindAutoStartItems(program).Count > 0;

    /// <summary>
    /// Itens de inicializacao automatica que pertencem ao programa informado
    /// (usados para desabilitar a inicializacao automatica).
    /// </summary>
    public static IReadOnlyList<AutorunItem> FindAutoStartItems(InstalledProgram? program)
    {
        var matches = new List<AutorunItem>();
        if (program is null) return matches;

        try
        {
            var location = PathUtil.Normalize(program.InstallLocation);
            var uninstallExe = PathUtil.Normalize(ProgramScanner.ExtractExecutablePath(program.UninstallString));
            var items = GetAutorunItems();

            foreach (var item in items)
            {
                if (MatchesProgram(item, program, location, uninstallExe)) matches.Add(item);
            }

            return matches;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao verificar a inicializacao automatica: {ex.Message}");
            return matches;
        }
    }

    // ---------------------------------------------------------------------
    // Auxiliares
    // ---------------------------------------------------------------------

    /// <summary>
    /// Casa o executavel com a lista de programas instalados, usando o
    /// casamento canonico do Modo Cacador.
    /// </summary>
    private static InstalledProgram? MatchInstalledProgram(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable)) return null;

        try
        {
            var resolution = HunterMode.ResolveExecutable(executable);
            return resolution.Found ? resolution.Program : null;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao identificar o programa de '{executable}': {ex.Message}");
            return null;
        }
    }

    /// <summary>Procura o processo cujo executavel e o caminho informado.</summary>
    private static Process? FindProcessByPath(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable)) return null;

        try
        {
            var name = SafeFileNameWithoutExtension(executable);
            if (name.Length == 0) return null;

            var candidates = Process.GetProcessesByName(name);
            Process? fallback = null;

            foreach (var candidate in candidates)
            {
                try
                {
                    var path = candidate.MainModule?.FileName;
                    if (string.IsNullOrWhiteSpace(path)) continue;

                    if (string.Equals(PathUtil.Normalize(path), executable, StringComparison.OrdinalIgnoreCase))
                        return candidate;

                    fallback ??= candidate;
                }
                catch
                {
                    // Processo protegido: sem acesso ao modulo principal.
                }
            }

            return fallback;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao procurar o processo de '{executable}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// True quando o item de inicializacao automatica pertence ao programa.
    /// </summary>
    private static bool MatchesProgram(
        AutorunItem item, InstalledProgram program, string location, string uninstallExe)
    {
        foreach (var candidate in new[] { item.Command, item.Location })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;

            var exe = ProgramScanner.ExtractExecutablePath(candidate);
            if (exe.Length == 0) exe = candidate;

            var normalized = PathUtil.Normalize(exe);
            if (normalized.Length == 0) continue;

            // 1) O item aponta para dentro da pasta de instalacao.
            if (location.Length > 0 && IsUnder(normalized, location)) return true;

            // 2) O item e o proprio executavel de desinstalacao.
            if (uninstallExe.Length > 0
                && string.Equals(normalized, uninstallExe, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // 3) O nome do executavel casa com o nome do programa.
            var leaf = SafeFileNameWithoutExtension(normalized);
            if (leaf.Length > 0 && NameMatch.ContainsWord(program.DisplayName, leaf)) return true;
        }

        return false;
    }

    /// <summary>
    /// Caminho do icone: o icone registrado do programa quando existe em disco,
    /// senao o proprio executavel do alvo.
    /// </summary>
    private static string ResolveIconPath(InstalledProgram? program, string executable)
    {
        try
        {
            if (program is not null)
            {
                var icon = program.IconPath;
                if (!string.IsNullOrWhiteSpace(icon) && File.Exists(icon)) return icon;
            }
        }
        catch { }

        return executable;
    }

    /// <summary>Itens de inicializacao automatica (com cache curto).</summary>
    private static IReadOnlyList<AutorunItem> GetAutorunItems()
    {
        lock (AutorunGate)
        {
            var now = DateTime.UtcNow;
            if (_autorunCache is not null && (now - _autorunCacheStamp).TotalSeconds < AutorunCacheSeconds)
                return _autorunCache;

            try
            {
                _autorunCache = AutorunManager.Scan();
                _autorunCacheStamp = now;
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao varrer a inicializacao automatica: {ex.Message}");
                _autorunCache = Array.Empty<AutorunItem>();
                _autorunCacheStamp = now;
            }

            return _autorunCache;
        }
    }

    /// <summary>Cria um alvo nao resolvido com o texto da falha.</summary>
    private static TopmostTarget Unresolved(string key) => new()
    {
        Resolved = false,
        Message = Loc.Get(key),
    };

    /// <summary>True quando o caminho esta dentro da pasta informada.</summary>
    private static bool IsUnder(string path, string folder)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(folder)) return false;
        if (path.Length <= folder.Length) return false;
        if (!path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) return false;

        var next = path[folder.Length];
        return next == '\\' || next == '/';
    }

    private static string SafeExtension(string path)
    {
        try { return Path.GetExtension(path) ?? string.Empty; }
        catch { return string.Empty; }
    }

    private static string SafeFileName(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            return Path.GetFileName(path) ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    private static string SafeFileNameWithoutExtension(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            return Path.GetFileNameWithoutExtension(path) ?? string.Empty;
        }
        catch { return string.Empty; }
    }
}
