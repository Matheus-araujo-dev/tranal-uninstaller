using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// Resultado da busca do "Modo Cacador": o programa corresponde ao item
/// arrastado sobre a janela do Tranal.
/// </summary>
public sealed class HunterResolution
{
    /// <summary>True quando um programa instalado foi identificado.</summary>
    public bool Found { get; init; }

    /// <summary>Programa identificado.</summary>
    public InstalledProgram? Program { get; init; }

    /// <summary>Caminho real do executavel (destino do atalho, quando houver).</summary>
    public string ResolvedPath { get; init; } = string.Empty;

    /// <summary>Nome exibido: o do programa quando ele for identificado.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Mensagem de resultado (inclusive quando nada e encontrado).</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// "Modo Cacador" do Revo: arrastar o icone de um programa sobre a janela do
/// Tranal para desinstalar aquele programa.
///
/// O caminho arrastado pode ser um atalho (.lnk), um atalho web (.url) ou o
/// proprio executavel (.exe/.com). O alvo real e casado com a lista de
/// programas instalados (ProgramScanner).
/// </summary>
public static class HunterMode
{
    /// <summary>Resolve um caminho arrastado e identifica o programa correspondente.</summary>
    public static HunterResolution Resolve(string droppedPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(droppedPath))
                return NotFound(string.Empty, "Nenhum caminho foi informado.");

            var path = PathUtil.Normalize(droppedPath);
            if (path.Length == 0) return NotFound(string.Empty, "Caminho inválido.");

            string extension;
            try { extension = Path.GetExtension(path).ToLowerInvariant(); }
            catch { extension = string.Empty; }

            switch (extension)
            {
                case ".lnk":
                {
                    var target = ResolveShortcut(path) ?? ResolveShortcutByScan(path);
                    if (string.IsNullOrWhiteSpace(target))
                        return NotFound(path, "Não foi possível resolver o alvo do atalho.");

                    return ResolveExecutable(target);
                }

                case ".url":
                {
                    var url = ReadUrlTarget(path);
                    if (string.IsNullOrWhiteSpace(url))
                        return NotFound(path, "O atalho web não informa um destino válido.");

                    // Um .url pode apontar para um arquivo local.
                    if (File.Exists(url)) return ResolveExecutable(url);

                    return NotFound(path, $"O atalho aponta para uma URL, não para um programa local: {url}");
                }

                case ".exe":
                case ".com":
                case ".bat":
                case ".cmd":
                default:
                    return ResolveExecutable(path);
            }
        }
        catch (Exception ex)
        {
            return new HunterResolution
            {
                Found = false,
                Message = $"Falha ao resolver o caminho arrastado: {ex.Message}",
            };
        }
    }

    /// <summary>
    /// Identifica o programa instalado a partir do caminho do executavel.
    /// Nunca lanca: sem correspondencia devolve Found = false e uma mensagem.
    /// </summary>
    public static HunterResolution ResolveExecutable(string executablePath)
    {
        try
        {
            var path = PathUtil.Normalize(executablePath);
            if (path.Length == 0) return NotFound(string.Empty, "Caminho de executável inválido.");

            var program = MatchProgram(path, ScanPrograms());

            if (program is null)
            {
                return new HunterResolution
                {
                    Found = false,
                    ResolvedPath = path,
                    DisplayName = SafeFileNameWithoutExtension(path),
                    Message = "Nenhum programa instalado corresponde a este executável.",
                };
            }

            return new HunterResolution
            {
                Found = true,
                Program = program,
                ResolvedPath = path,
                DisplayName = program.DisplayName,
                Message = $"Programa identificado: {program.DisplayName}",
            };
        }
        catch (Exception ex)
        {
            return new HunterResolution
            {
                Found = false,
                Message = $"Falha ao identificar o programa: {ex.Message}",
            };
        }
    }

    /// <summary>
    /// Identifica, com uma varredura so, o programa dono de cada caminho.
    ///
    /// Resolver um caminho por vez refaz a varredura de programas instalados a
    /// cada chamada: numa lista com dez entradas de menu de contexto isso
    /// sao dez varreduras. Aqui a varredura acontece uma vez, e o casamento
    /// continua sendo o mesmo (<see cref="MatchProgram"/>).
    /// </summary>
    /// <param name="executablePaths">Os caminhos a casar.</param>
    /// <returns>O programa de cada caminho, indexado pelo caminho original e pelo normalizado.</returns>
    public static IReadOnlyDictionary<string, InstalledProgram> ResolveExecutables(IEnumerable<string> executablePaths)
    {
        var mapa = new Dictionary<string, InstalledProgram>(StringComparer.OrdinalIgnoreCase);

        if (executablePaths is null) return mapa;

        try
        {
            var programas = ScanPrograms();

            foreach (var bruto in executablePaths)
            {
                if (string.IsNullOrWhiteSpace(bruto)) continue;

                var caminho = PathUtil.Normalize(bruto);
                if (caminho.Length == 0) continue;

                var programa = mapa.TryGetValue(caminho, out var jaachado)
                    ? jaachado
                    : MatchProgram(caminho, programas);

                if (programa is null) continue;

                mapa[caminho] = programa;
                mapa[bruto] = programa;
            }
        }
        catch (Exception ex)
        {
            // Sem a lista de programas, a coluna fica vazia: a entrada continua
            // valendo, so sem o nome do dono.
            ToolLog.Error($"Falha ao identificar os donos dos executaveis: {ex.Message}");
        }

        return mapa;
    }

    /// <summary>
    /// Casa o executavel com a lista de programas instalados. Ordem de preferencia
    /// (medida no Revo):
    ///   1. o executavel esta dentro da pasta de instalacao registrada;
    ///   2. o executavel e o proprio desinstalador do programa;
    ///   3. o nome do arquivo casa com o nome do programa (palavra inteira).
    /// </summary>
    private static InstalledProgram? MatchProgram(string executablePath, IReadOnlyList<InstalledProgram> programs)
    {
        if (programs is null || programs.Count == 0) return null;

        var exeName = SafeFileNameWithoutExtension(executablePath);

        InstalledProgram? best = null;
        var bestScore = 0;

        foreach (var program in programs)
        {
            if (program is null) continue;

            var score = 0;
            var location = PathUtil.Normalize(program.InstallLocation);

            // 1) Dentro da pasta de instalacao.
            if (location.Length > 0 && IsUnder(executablePath, location)) score = 3;

            // 2) E o executavel de desinstalacao do programa.
            if (score < 3)
            {
                var uninstallExe = ProgramScanner.ExtractExecutablePath(program.UninstallString);
                if (!string.IsNullOrWhiteSpace(uninstallExe))
                {
                    var normalized = PathUtil.Normalize(uninstallExe);
                    if (normalized.Length > 0
                        && string.Equals(normalized, executablePath, StringComparison.OrdinalIgnoreCase))
                    {
                        score = Math.Max(score, 2);
                    }
                }
            }

            // 3) Nome do arquivo contra o nome do programa.
            if (score == 0)
            {
                if (MatchesName(exeName, program.DisplayName)) score = 1;
                else if (MatchesName(exeName, SafeFileName(location))) score = 1;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = program;
            }
        }

        return best;
    }

    /// <summary>Lista de programas instalados usada na casamento.</summary>
    private static IReadOnlyList<InstalledProgram> ScanPrograms()
    {
        try
        {
            return ProgramScanner.Scan(new ProgramScanner.ScanOptions
            {
                IncludeSystemComponents = AppSettingsStore.Current.ShowSystemComponents,
                IncludeMsiProducts = false,
                ComputeFolderSize = false,
                RequireUninstallString = false,
            });
        }
        catch
        {
            return Array.Empty<InstalledProgram>();
        }
    }

    /// <summary>Verdadeiro quando o nome do executavel casa com o nome do programa.</summary>
    private static bool MatchesName(string exeName, string programName)
    {
        if (string.IsNullOrWhiteSpace(exeName) || string.IsNullOrWhiteSpace(programName)) return false;

        var exeVariants = TraceStore.TraceText.NameVariants(exeName);
        var programVariants = TraceStore.TraceText.NameVariants(programName);

        // O nome do executavel aparece no nome do programa ("chrome" em "Google Chrome").
        if (TraceStore.TraceText.ContainsAnyWholeWord(programName, exeVariants)) return true;

        // O nome do programa aparece no nome do executavel.
        return TraceStore.TraceText.ContainsAnyWholeWord(exeName, programVariants);
    }

    /// <summary>
    /// Resolve o alvo de um atalho .lnk pelo COM WScript.Shell (o metodo mais
    /// simples e o mesmo que o Revo usa). Devolve null quando o COM nao esta
    /// disponivel ou o atalho nao tem alvo.
    /// </summary>
    private static string? ResolveShortcut(string lnkPath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return null;

            var instance = Activator.CreateInstance(shellType);
            if (instance is null) return null;

            dynamic shell = instance;
            dynamic shortcut = shell.CreateShortcut(lnkPath);

            var target = shortcut.TargetPath as string;
            if (string.IsNullOrWhiteSpace(target)) return null;

            // Os argumentos do atalho nao fazem parte do executavel resolvido.
            return target.Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Alternativa ao COM: procura dentro do proprio .lnk a primeira string
    /// legivel que pareca um caminho de executavel.
    /// </summary>
    private static string? ResolveShortcutByScan(string lnkPath)
    {
        try
        {
            var bytes = File.ReadAllBytes(lnkPath);
            if (bytes.Length == 0) return null;

            var best = string.Empty;

            foreach (var encoding in new[] { Encoding.Latin1, Encoding.Unicode })
            {
                string text;
                try { text = encoding.GetString(bytes); }
                catch { continue; }

                foreach (Match match in Regex.Matches(text, @"[A-Za-z]:\\[^\x00-\x1F""<>|]{2,240}"))
                {
                    var candidate = TrimToExecutable(match.Value);
                    if (candidate.Length > best.Length) best = candidate;
                }
            }

            return best.Length > 0 ? best : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Corta o trecho lido ate a extensao de executavel.</summary>
    private static string TrimToExecutable(string candidate)
    {
        foreach (var extension in new[] { ".exe", ".com", ".bat", ".cmd", ".msi" })
        {
            var index = candidate.LastIndexOf(extension, StringComparison.OrdinalIgnoreCase);
            if (index > 0) return candidate[..(index + extension.Length)];
        }

        return string.Empty;
    }

    /// <summary>Le a linha "URL=" de um atalho web (.url).</summary>
    private static string ReadUrlTarget(string urlFile)
    {
        try
        {
            foreach (var line in File.ReadAllLines(urlFile))
            {
                var text = line.Trim();
                if (!text.StartsWith("URL=", StringComparison.OrdinalIgnoreCase)) continue;

                var value = text[4..].Trim();
                if (value.Length == 0) continue;

                if (value.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    try { value = new Uri(value).LocalPath; }
                    catch { }
                }

                return value;
            }
        }
        catch { }

        return string.Empty;
    }

    private static bool IsUnder(string path, string folder)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(folder)) return false;
        if (path.Length <= folder.Length) return false;
        if (!path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) return false;

        var next = path[folder.Length];
        return next == '\\' || next == '/';
    }

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

    private static string SafeFileNameWithoutExtension(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            return Path.GetFileNameWithoutExtension(path) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static HunterResolution NotFound(string path, string message) => new()
    {
        Found = false,
        ResolvedPath = path,
        DisplayName = SafeFileNameWithoutExtension(path),
        Message = message,
    };
}
