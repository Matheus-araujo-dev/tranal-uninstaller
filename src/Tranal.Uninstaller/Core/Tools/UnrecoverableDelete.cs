using System.IO;
using System.Security;
using System.Security.Cryptography;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Um alvo de remocao irrecuperavel.
/// </summary>
public sealed class SecureDeleteTarget
{
    /// <summary>Caminho completo do alvo.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>True quando o alvo e uma pasta (e nao um arquivo).</summary>
    public bool IsFolder { get; init; }
}

/// <summary>
/// Resultado da remocao irrecuperavel de um alvo.
/// </summary>
public sealed class SecureDeleteResult
{
    /// <summary>Caminho do alvo processado.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>True quando o alvo foi sobrescrito e apagado.</summary>
    public bool Succeeded { get; init; }

    /// <summary>Descricao do resultado (ou do motivo da falha).</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Total de bytes sobrescritos.</summary>
    public long BytesOverwritten { get; init; }
}

/// <summary>
/// Remocao irrecuperavel: sobrescreve o conteudo dos arquivos antes de apaga-los,
/// para que os dados nao possam ser recuperados com ferramentas de forense.
///
/// Observacao sobre o motor canonico: a sobrescrita nao pode ser expressa como
/// "remocao de sobra" (o motor canonico remove o item e pronto), portanto esta
/// ferramenta trabalha direto sobre o sistema de arquivos, sempre respeitando
/// <see cref="SafetyRules.IsProtectedFolder"/>.
/// </summary>
public static class UnrecoverableDelete
{
    /// <summary>Numero de sobrescritas usado por padrao.</summary>
    public static int DefaultPasses { get; } = 3;

    /// <summary>Numero minimo de sobrescritas aceito.</summary>
    private const int MinPasses = 1;

    /// <summary>Numero maximo de sobrescritas aceito (padrao do DoD: 35).</summary>
    private const int MaxPasses = 35;

    /// <summary>Tamanho do bloco usado na sobrescrita.</summary>
    private const int BufferSize = 64 * 1024;

    /// <summary>Profundidade maxima de resolucao de atalhos encadeados.</summary>
    private const int MaxShortcutDepth = 5;

    /// <summary>
    /// Expande os caminhos informados nos arquivos que serao efetivamente apagados.
    /// Atalhos .lnk sao resolvidos e pastas sao expandidas recursivamente.
    /// Pastas protegidas pelas regras de seguranca sao ignoradas.
    /// </summary>
    public static IReadOnlyList<SecureDeleteTarget> Expand(IEnumerable<string> paths)
    {
        var targets = new List<SecureDeleteTarget>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in paths ?? Array.Empty<string>())
        {
            try
            {
                var resolved = ResolveShortcut(raw);
                if (string.IsNullOrWhiteSpace(resolved)) continue;

                var full = PathUtil.Normalize(resolved);
                if (full.Length == 0) continue;

                if (Directory.Exists(full))
                {
                    if (SafetyRules.IsProtectedFolder(full))
                    {
                        ToolLog.Info($"Pasta protegida pelas regras de seguranca, ignorada: {full}");
                        continue;
                    }

                    foreach (var file in EnumerateFilesSafe(full))
                    {
                        if (seen.Add(file)) targets.Add(new SecureDeleteTarget { Path = file, IsFolder = false });
                    }

                    // A pasta entra por ultimo: os arquivos sao apagados antes dela.
                    targets.Add(new SecureDeleteTarget { Path = full, IsFolder = true });
                }
                else if (File.Exists(full))
                {
                    if (seen.Add(full)) targets.Add(new SecureDeleteTarget { Path = full, IsFolder = false });
                }
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao expandir '{raw}': {ex.Message}");
            }
        }

        return targets;
    }

    /// <summary>
    /// Apaga os caminhos informados sobrescrevendo antes o conteudo dos arquivos.
    /// <paramref name="passes"/> e limitado ao intervalo de 1 a 35 sobrescritas.
    /// Nunca lanca: falhas sao devolvidas no resultado.
    /// </summary>
    public static IReadOnlyList<SecureDeleteResult> Delete(IEnumerable<string> paths, int passes)
    {
        var results = new List<SecureDeleteResult>();
        var effectivePasses = Math.Clamp(passes, MinPasses, MaxPasses);

        foreach (var raw in paths ?? Array.Empty<string>())
        {
            var result = DeleteOne(raw, effectivePasses);
            if (result is not null) results.Add(result);
        }

        return results;
    }

    // ---------------------------------------------------------------------
    // Remocao
    // ---------------------------------------------------------------------

    private static SecureDeleteResult? DeleteOne(string raw, int passes)
    {
        try
        {
            var resolved = ResolveShortcut(raw);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                return new SecureDeleteResult
                {
                    Path = raw,
                    Succeeded = false,
                    Message = "atalho nao pode ser resolvido",
                };
            }

            var full = PathUtil.Normalize(resolved);
            if (full.Length == 0)
            {
                return new SecureDeleteResult
                {
                    Path = raw,
                    Succeeded = false,
                    Message = "caminho invalido",
                };
            }

            if (Directory.Exists(full)) return DeleteFolder(full, passes);

            if (!File.Exists(full))
            {
                return new SecureDeleteResult
                {
                    Path = full,
                    Succeeded = true,
                    Message = "ja nao existia",
                };
            }

            return DeleteFile(full, passes);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao apagar '{raw}': {ex.Message}");
            return new SecureDeleteResult
            {
                Path = raw,
                Succeeded = false,
                Message = ex.Message,
            };
        }
    }

    private static SecureDeleteResult DeleteFile(string path, int passes)
    {
        var parent = Path.GetDirectoryName(path) ?? string.Empty;
        if (SafetyRules.IsProtectedFolder(parent))
        {
            return new SecureDeleteResult
            {
                Path = path,
                Succeeded = false,
                Message = "caminho protegido pelas regras de seguranca",
            };
        }

        try
        {
            ClearReadOnly(path);

            var length = new FileInfo(path).Length;
            var overwritten = OverwriteFile(path, passes);

            File.Delete(path);

            return new SecureDeleteResult
            {
                Path = path,
                Succeeded = true,
                Message = $"sobrescrito {passes}x e apagado ({length} bytes)",
                BytesOverwritten = overwritten,
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            ToolLog.Error($"Acesso negado ao apagar '{path}': {ex.Message}");
            return new SecureDeleteResult
            {
                Path = path,
                Succeeded = false,
                Message = $"acesso negado: {ex.Message}",
            };
        }
        catch (SecurityException ex)
        {
            ToolLog.Error($"Acesso negado ao apagar '{path}': {ex.Message}");
            return new SecureDeleteResult
            {
                Path = path,
                Succeeded = false,
                Message = $"acesso negado: {ex.Message}",
            };
        }
        catch (IOException ex)
        {
            ToolLog.Error($"Falha ao apagar '{path}': {ex.Message}");
            return new SecureDeleteResult
            {
                Path = path,
                Succeeded = false,
                Message = ex.Message,
            };
        }
    }

    private static SecureDeleteResult DeleteFolder(string path, int passes)
    {
        if (SafetyRules.IsProtectedFolder(path))
        {
            return new SecureDeleteResult
            {
                Path = path,
                Succeeded = false,
                Message = "pasta protegida pelas regras de seguranca",
            };
        }

        long overwritten = 0;
        var failures = new List<string>();

        foreach (var file in EnumerateFilesSafe(path))
        {
            var result = DeleteFile(file, passes);
            overwritten += result.BytesOverwritten;
            if (!result.Succeeded) failures.Add($"{file}: {result.Message}");
        }

        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao remover a pasta '{path}': {ex.Message}");
            failures.Add($"{path}: {ex.Message}");
        }

        var removed = !Directory.Exists(path);

        return new SecureDeleteResult
        {
            Path = path,
            Succeeded = failures.Count == 0 && removed,
            Message = failures.Count == 0
                ? "conteudo sobrescrito e pasta removida"
                : string.Join("; ", failures.Take(3)),
            BytesOverwritten = overwritten,
        };
    }

    /// <summary>
    /// Sobrescreve o arquivo <paramref name="passes"/> vezes: a primeira com bytes
    /// aleatorios e as demais com os padroes 0x00 e 0xFF alternados.
    /// </summary>
    private static long OverwriteFile(string path, int passes)
    {
        var length = new FileInfo(path).Length;
        if (length <= 0) return 0;

        var buffer = new byte[BufferSize];
        long total = 0;

        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        using var random = RandomNumberGenerator.Create();

        for (var pass = 0; pass < passes; pass++)
        {
            if (pass == 0)
            {
                random.GetBytes(buffer);
            }
            else
            {
                Array.Fill(buffer, pass % 2 == 1 ? (byte)0x00 : (byte)0xFF);
            }

            stream.Seek(0, SeekOrigin.Begin);

            var written = 0L;
            while (written < length)
            {
                var chunk = (int)Math.Min(buffer.Length, length - written);
                stream.Write(buffer, 0, chunk);
                written += chunk;
            }

            stream.Flush(flushToDisk: true);
            total += written;
        }

        return total;
    }

    private static void ClearReadOnly(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if ((info.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                info.Attributes &= ~FileAttributes.ReadOnly;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao liberar o atributo somente-leitura de '{path}': {ex.Message}");
        }
    }

    // ---------------------------------------------------------------------
    // Auxiliares
    // ---------------------------------------------------------------------

    /// <summary>
    /// Resolve atalhos .lnk (WScript.Shell) ate o alvo final.
    /// Devolve o proprio caminho quando o arquivo nao e um atalho.
    /// </summary>
    private static string? ResolveShortcut(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            var current = path.Trim().Trim('"');

            for (var depth = 0; depth < MaxShortcutDepth; depth++)
            {
                if (!current.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return current;

                var type = Type.GetTypeFromProgID("WScript.Shell");
                if (type is null) return null;

                var instance = Activator.CreateInstance(type);
                if (instance is null) return null;

                dynamic shell = instance;
                dynamic shortcut = shell.CreateShortcut(current);

                var target = Convert.ToString(shortcut.TargetPath);
                if (string.IsNullOrWhiteSpace(target)) return null;

                current = target.Trim().Trim('"');
            }

            return current;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao resolver o atalho '{path}': {ex.Message}");
            return null;
        }
    }

    private static IEnumerable<string> EnumerateFilesSafe(string folder)
    {
        var files = new List<string>();
        var stack = new Stack<string>();
        stack.Push(folder);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(current)) files.Add(PathUtil.Normalize(file));
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao listar os arquivos de '{current}': {ex.Message}");
            }

            try
            {
                foreach (var sub in Directory.EnumerateDirectories(current)) stack.Push(sub);
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao listar as pastas de '{current}': {ex.Message}");
            }
        }

        return files;
    }
}
