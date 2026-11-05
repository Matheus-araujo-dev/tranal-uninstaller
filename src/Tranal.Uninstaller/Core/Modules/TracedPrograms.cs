using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// Modulo "Programas Monitorados" do Revo: executar um instalador com RASTREIO
/// (fotografando o sistema antes e depois) e depois desinstalar exatamente o
/// que o instalador criou, a partir do log.
/// </summary>
public static class TracedPrograms
{
    /// <summary>Lista os logs de instalacao monitorada.</summary>
    public static IReadOnlyList<TracedInstall> List()
    {
        try { return TraceStore.List(); }
        catch { return Array.Empty<TracedInstall>(); }
    }

    /// <summary>
    /// Executa o instalador com rastreio e grava o log do que ele criou.
    ///
    /// Fluxo: fotografia ANTES -&gt; executa o instalador -&gt; fotografia DEPOIS
    /// -&gt; diff -&gt; filtro de plausibilidade -&gt; log.
    ///
    /// O instalador pode pedir reinicializacao (codigo 3010/1641): o Tranal
    /// NUNCA reinicia a maquina, o pedido e apenas ignorado e o log e gravado
    /// com o estado observedo.
    ///
    /// Devolve null quando o instalador nao existe, nao pode ser executado ou o
    /// log nao pode ser gravado. Nunca lanca.
    /// </summary>
    public static TracedInstall? Install(string installerPath, string programName, int timeoutSeconds = 1800)
    {
        try
        {
            var installer = PathUtil.Normalize(installerPath);
            if (installer.Length == 0 || !File.Exists(installer)) return null;

            var name = (programName ?? string.Empty).Trim();

            // 1) Fotografia ANTES (so vive durante a operacao).
            var before = SystemSnapshot.Capture();

            // 2) Executa o instalador.
            RunInstaller(installer, timeoutSeconds);

            // 3) Fotografia DEPOIS.
            var after = SystemSnapshot.Capture();

            // 4) Diff.
            var diff = SystemSnapshot.Diff(before, after);

            // 5) Filtro de plausibilidade: nem tudo que nasceu pertence ao programa.
            var filtered = FilterDiff(diff, name);

            // 6) Grava o log.
            var payload = new TraceStore.TracePayload
            {
                Files = filtered.Files,
                Folders = filtered.Folders,
                RegistryKeys = filtered.RegistryKeys,
            };

            var id = Guid.NewGuid().ToString("D");
            var install = new TracedInstall
            {
                Id = id,
                ProgramName = name,
                Timestamp = DateTime.UtcNow,
                InstallerPath = installer,
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
                    Id = id,
                    ProgramName = name,
                    Timestamp = install.Timestamp,
                    InstallerPath = installer,
                    LogPath = saved,
                    FileCount = install.FileCount,
                    FolderCount = install.FolderCount,
                    RegistryKeyCount = install.RegistryKeyCount,
                    Imported = false,
                };
            }

            return install;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Desinstala a partir do log: reconstroi os itens de sobra e entrega tudo a
    /// rotina canonica <see cref="UninstallEngine.RemoveLeftovers"/>.
    ///
    /// REGRA POR NIVEL:
    ///   - Safe:     remove SOMENTE o que casa com o nome do programa (palavra inteira).
    ///   - Moderate: remove o log inteiro.
    ///   - Advanced: identico ao Moderate — o log ja e, por definicao, o resultado
    ///                de uma varredura avancada (ele nasceu de um rastreio real).
    ///
    /// Itens barrados pelas <see cref="SafetyRules"/> ficam Protected no relatorio.
    /// </summary>
    public static RemovalReport Uninstall(TracedInstall install, UninstallMode mode = UninstallMode.Moderate)
    {
        try
        {
            if (install is null) return EmptyReport();

            var payload = TraceStore.LoadPayload(install);
            if (payload is null) return EmptyReport();

            var items = BuildLeftoverItems(payload.Value, install.ProgramName, mode);
            if (items.Count == 0) return EmptyReport();

            return UninstallEngine.RemoveLeftovers(items);
        }
        catch
        {
            return EmptyReport();
        }
    }

    /// <summary>
    /// Importa um log externo e desinstala a partir dele. Devolve um relatorio
    /// vazio quando o log nao pode ser importado.
    /// </summary>
    public static RemovalReport UninstallByLogPath(string logPath, UninstallMode mode = UninstallMode.Moderate)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(logPath)) return EmptyReport();

            var install = TraceStore.Import(logPath);
            if (install is null) return EmptyReport();

            return Uninstall(install, mode);
        }
        catch
        {
            return EmptyReport();
        }
    }

    /// <summary>
    /// Executa o instalador e aguarda ate o prazo informado. Nunca lanca.
    /// O processo NAO e morto ao estourar o prazo (o usuario pode estar diante
    /// de uma tela de instalacao legitima); nesse caso o codigo devolvido e -2.
    /// </summary>
    private static int RunInstaller(string installerPath, int timeoutSeconds)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null) return -1;

            var seconds = timeoutSeconds <= 0 ? 1 : timeoutSeconds;
            if (!process.WaitForExit(seconds * 1000)) return -2;

            return process.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Filtro de plausibilidade do diff. Um item so entra no log se for NOVO e
    /// estiver ligado ao programa instalado. O criterio (medido no Revo):
    ///
    ///   1) o caminho (ou o nome do arquivo/pasta/chave) contem o nome do
    ///      programa como PALAVRA INTEIRA, sem acento e sem caixa; ou
    ///   2) o caminho esta sob uma pasta de instalacao recem-criada (pasta nova
    ///      cujo nome casa com o programa, ou pasta que contem um arquivo novo
    ///      cujo nome casa com o programa).
    ///
    /// Sem nome de programa, tudo que e novo entra no log.
    /// </summary>
    private static FilteredTrace FilterDiff(SnapshotDiff diff, string programName)
    {
        var result = new FilteredTrace();
        if (diff is null) return result;

        var variants = TraceStore.TraceText.NameVariants(programName);

        // Sem nome de programa: aceita tudo que e novo.
        if (variants.Count == 0)
        {
            foreach (var file in diff.NewFiles)
            {
                result.Files.Add(new TraceStore.TraceFileEntry
                {
                    Path = file.Path,
                    SizeBytes = file.SizeBytes,
                    ModifiedUtc = file.ModifiedUtc,
                });
            }

            result.Folders.AddRange(diff.NewFolders);
            foreach (var key in diff.NewRegistryKeys) result.RegistryKeys.Add(key.Path);
            return result;
        }

        // Pastas candidatas a "pasta de instalacao recem-criada".
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in diff.NewFolders)
        {
            string leaf;
            try { leaf = Path.GetFileName(folder); }
            catch { continue; }

            if (TraceStore.TraceText.ContainsAnyWholeWord(leaf, variants))
            {
                try { roots.Add(PathUtil.Normalize(folder)); }
                catch { }
            }
        }

        foreach (var file in diff.NewFiles)
        {
            string leaf;
            try { leaf = Path.GetFileNameWithoutExtension(file.Path); }
            catch { continue; }

            if (!TraceStore.TraceText.ContainsAnyWholeWord(leaf, variants)) continue;

            try
            {
                var dir = Path.GetDirectoryName(file.Path);
                if (!string.IsNullOrEmpty(dir)) roots.Add(PathUtil.Normalize(dir));
            }
            catch { }
        }

        // Arquivos: casam pelo nome OU estao sob uma pasta de instalacao nova.
        foreach (var file in diff.NewFiles)
        {
            var byName = TraceStore.TraceText.ContainsAnyWholeWord(file.Path, variants);
            if (!byName && !IsUnderAny(file.Path, roots)) continue;

            result.Files.Add(new TraceStore.TraceFileEntry
            {
                Path = file.Path,
                SizeBytes = file.SizeBytes,
                ModifiedUtc = file.ModifiedUtc,
            });
        }

        // Pastas: casam pelo nome, sao a propria pasta de instalacao ou estao sob ela.
        foreach (var folder in diff.NewFolders)
        {
            var byName = TraceStore.TraceText.ContainsAnyWholeWord(folder, variants);
            if (!byName && !IsUnderAny(folder, roots) && !roots.Contains(folder)) continue;

            result.Folders.Add(folder);
        }

        // Chaves de registro: casam pelo caminho (o nome aparece na chave).
        foreach (var key in diff.NewRegistryKeys)
        {
            if (!TraceStore.TraceText.ContainsAnyWholeWord(key.Path, variants)) continue;
            result.RegistryKeys.Add(key.Path);
        }

        return result;
    }

    /// <summary>Reconstroi os itens de sobra a partir do payload do log.</summary>
    private static List<LeftoverItem> BuildLeftoverItems(JsonElement payload, string programName, UninstallMode mode)
    {
        var items = new List<LeftoverItem>();
        var trace = TraceStore.ReadPayload(payload);
        var variants = TraceStore.TraceText.NameVariants(programName);

        bool Accept(string path)
        {
            // Safe restringe ao nome do programa; Moderate e Advanced usam o log inteiro.
            if (mode != UninstallMode.Safe) return true;
            if (variants.Count == 0) return true;
            return TraceStore.TraceText.ContainsAnyWholeWord(path, variants);
        }

        foreach (var file in trace.Files)
        {
            if (string.IsNullOrWhiteSpace(file.Path)) continue;
            if (!Accept(file.Path)) continue;

            items.Add(new LeftoverItem
            {
                Kind = "Arquivo",
                Path = file.Path,
                Detail = file.SizeBytes > 0 ? $"{file.SizeBytes / 1024} KB" : "Arquivo registrado no log",
            });
        }

        // Pastas: as mais profundas primeiro, para que o relatorio registre a
        // remocao real de cada item (a pasta que contem a subpasta some antes).
        foreach (var folder in trace.Folders.OrderByDescending(f => f.Length))
        {
            if (string.IsNullOrWhiteSpace(folder)) continue;
            if (!Accept(folder)) continue;

            items.Add(new LeftoverItem
            {
                Kind = "Pasta",
                Path = folder,
                Detail = "Pasta registrada no log",
            });
        }

        foreach (var key in trace.RegistryKeys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (!Accept(key)) continue;

            items.Add(new LeftoverItem
            {
                Kind = "Registro",
                Path = key,
                Detail = "Chave registrada no log",
            });
        }

        return items;
    }

    /// <summary>Verdadeiro quando o caminho esta sob uma das pastas informadas.</summary>
    private static bool IsUnderAny(string path, IEnumerable<string> roots)
    {
        foreach (var root in roots)
        {
            if (root.Length == 0) continue;
            if (path.Length <= root.Length) continue;
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;

            var next = path[root.Length];
            if (next == '\\' || next == '/') return true;
        }

        return false;
    }

    private static RemovalReport EmptyReport() => new(Array.Empty<RemovalOutcome>());

    /// <summary>Resultado do filtro de plausibilidade.</summary>
    private sealed class FilteredTrace
    {
        public List<TraceStore.TraceFileEntry> Files { get; } = new();
        public List<string> Folders { get; } = new();
        public List<string> RegistryKeys { get; } = new();
    }
}
