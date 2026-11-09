using System.Text;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Cli;

/// <summary>
/// Comandos adicionales da CLI, com paridade de contrato com o Revo:
///
///   tranal /winapps                      - lista os aplicativos do Windows (RevoCmd /winapps)
///   tranal /wa "App1;App2"               - desinstala aplicativos do Windows (RevoUninPro /wa)
///   tranal /be                           - lista as extensoes de navegador (RevoUninPro /be)
///   tranal /be "Ext1;Ext2" /remove      - remove extensoes de navegador
///   tranal /autorun                      - lista os itens de inicializacao automatica
///   tranal /tools                        - lista as ferramentas nativas do Windows
///   tranal /export "caminho" [/txt|/html|/csv|/xml]  - exporta a listagem
///   tranal /hunter "caminho"             - resolve um icone para o programa instalado
///   tranal /lang "pt-BR"                 - define o idioma da interface
///   tranal /theme "dark|light"           - define o tema da interface
///   tranal /backup | /bk                 - Gerenciador de Backups (pontos de restauracao)
///   tranal /exclude [add|addreg|rm|rmreg|clear] - Gerenciador da lista de exclusoes
///   tranal /plan "programa"              - mostra o plano de desinstalacao, sem executar
///   tranal /batch "arquivo" [/remove]    - desinstalacao em lote a partir de um arquivo de lista
///   tranal /switches                     - lista os switches de linha de comando de cada ferramenta
///   tranal /regclean                     - lista as sobras de registro por categoria
///   tranal /regclean "ActiveX" /remove   - remove as sobras de uma categoria
///   tranal /com-unregister "C:\Pasta"    - lista e desregistra os componentes COM
///   tranal /shared "C:\Pasta"            - mostra quem usa o caminho
///   tranal /noremove "programa"          - mostra e desliga a protecao do desinstalador
///   tranal /create-service "nome" /exe "C:\Programa.exe"   - cria um servico do Windows
///   tranal /create-task "nome" /exe "C:\Programa.exe"      - cria uma tarefa agendada
///   tranal /sessions                 - lista as desinstalacoes interrompidas
///   tranal /resume "id"              - retoma uma desinstalacao interrompida
///   tranal /processes                - lista os processos em execucao e o programa dono
///   tranal /registry-remove "chave"   - apaga uma entrada de registro
///   tranal /report-html "arquivo"     - grava o relatorio HTML da limpeza de registro
///   tranal /unlock "arquivo"          - mostra quem segura o arquivo
///   tranal /unlock "arquivo" /force   - destrava o arquivo
///   tranal /internet /clean           - limpa os dados dos navegadores
///   tranal /drive /clean              - limpa os arquivos desnecessarios
///   tranal /shortcuts /repair         - repara os atalhos quebrados
///   tranal /mru /clean                - limpa o cache de uso recente
///   tranal /shared-dlls               - lista os componentes compartilhados
/// </summary>
public static class ExtraCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando nenhum dos comandos extras
    /// foi pedido (o fluxo normal segue no Program).
    /// </summary>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (flags.Contains("/winapps")) return RunWindowsApps(args, flags);
        if (flags.Contains("/wa")) return RunWindowsApps(args, flags, uninstall: true);
        if (flags.Contains("/be")) return RunBrowserExtensions(args, flags);
        if (flags.Contains("/autorun")) return RunAutorun();
        if (flags.Contains("/tools")) return RunWindowsTools();
        if (flags.Contains("/export")) return RunExport(args, flags);
        if (flags.Contains("/hunter")) return RunHunter(args);
        if (flags.Contains("/lang")) return RunLanguage(args);
        if (flags.Contains("/theme")) return RunTheme(args);
        if (flags.Contains("/new")) return RunNewPrograms();
        if (flags.Contains("/backup") || flags.Contains("/bk")) return RunBackupManager(args, flags);
        if (flags.Contains("/exclude")) return RunExcludeManager(args);
        if (flags.Contains("/plan")) return RunPlan(args);
        if (flags.Contains("/batch")) return RunBatch(args, flags);
        if (flags.Contains("/switches")) return RunSwitches();
        if (LogCommands.TryRun(args, flags) is int logResult) return logResult;
        if (ParityCommands.TryRun(args, flags) is int parityResult) return parityResult;
        if (RegistryCommands.TryRun(args, flags) is int registryResult) return registryResult;
        if (ResumeCommands.TryRun(args, flags) is int resumeResult) return resumeResult;
        if (CleanupCommands.TryRun(args, flags) is int cleanupResult) return cleanupResult;
        return null;
    }

    private static string[] SplitNames(string raw) =>
        raw.Split(';')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

    // ------------------------------------------------------------------
    // Windows Apps
    // ------------------------------------------------------------------
    private static int RunWindowsApps(string[] args, HashSet<string> flags, bool uninstall = false)
    {
        var apps = WindowsApps.List();

        if (!uninstall)
        {
            Console.WriteLine($"Aplicativos do Windows instalados: {apps.Count}");
            Console.WriteLine();
            foreach (var app in apps)
                Console.WriteLine($"{app.Name} - {app.Version}");
            return 0;
        }

        var raw = CliArgs.ValueAfter(args, "/wa");
        if (string.IsNullOrWhiteSpace(raw))
        {
            Console.Error.WriteLine("Uso: tranal /wa \"App1;App2\"");
            return 1;
        }

        var wanted = SplitNames(raw);
        var removed = 0;
        var failed = 0;

        foreach (var name in wanted)
        {
            var target = apps.FirstOrDefault(a =>
                a.Name.Contains(name, StringComparison.CurrentCultureIgnoreCase));

            if (target is null)
            {
                Console.WriteLine($"Não encontrado: {name}");
                failed++;
                continue;
            }

            var ok = WindowsApps.Uninstall(target);
            Console.WriteLine($"{(ok ? "Removido" : "Falhou")}: {target.Name} ({target.PackageFullName})");
            if (ok) removed++; else failed++;
        }

        Console.WriteLine();
        Console.WriteLine($"Removidos: {removed} | Falhas: {failed}");
        return failed == 0 ? 0 : 2;
    }

    // ------------------------------------------------------------------
    // Extensoes de navegador
    // ------------------------------------------------------------------
    private static int RunBrowserExtensions(string[] args, HashSet<string> flags)
    {
        var extensions = BrowserExtensions.List();

        var raw = CliArgs.ValueAfter(args, "/be");
        var remove = flags.Contains("/remove") || flags.Contains("/rm");

        if (string.IsNullOrWhiteSpace(raw) || !remove)
        {
            Console.WriteLine($"Extensões de navegador encontradas: {extensions.Count}");
            Console.WriteLine();
            foreach (var extension in extensions)
                Console.WriteLine($"{extension.Name} - {extension.Version} ({extension.BrowserName}/{extension.Profile})");
            return 0;
        }

        var wanted = SplitNames(raw);
        var removed = 0;
        var failed = 0;

        foreach (var name in wanted)
        {
            var target = extensions.FirstOrDefault(e =>
                e.Name.Contains(name, StringComparison.CurrentCultureIgnoreCase));

            if (target is null)
            {
                Console.WriteLine($"Não encontrada: {name}");
                failed++;
                continue;
            }

            var ok = BrowserExtensions.Remove(target);
            Console.WriteLine($"{(ok ? "Removida" : "Falhou")}: {target.Name} ({target.BrowserName})");
            if (ok) removed++; else failed++;
        }

        Console.WriteLine();
        Console.WriteLine($"Removidas: {removed} | Falhas: {failed}");
        return failed == 0 ? 0 : 2;
    }

    // ------------------------------------------------------------------
    // Autorun
    // ------------------------------------------------------------------
    private static int RunAutorun()
    {
        var items = AutorunManager.Scan();
        Console.WriteLine($"Itens de inicialização automática: {items.Count}");
        Console.WriteLine();

        foreach (var item in items)
        {
            var state = item.Enabled ? "ativo " : "inativo";
            Console.WriteLine($"[{state}] {item.Name}");
            Console.WriteLine($"         {item.Source} :: {item.Command}");
        }

        return 0;
    }

    // ------------------------------------------------------------------
    // Ferramentas do Windows
    // ------------------------------------------------------------------
    private static int RunWindowsTools()
    {
        var tools = WindowsToolsCatalog.All;
        Console.WriteLine($"Ferramentas nativas do Windows: {tools.Count}");
        Console.WriteLine();

        foreach (var tool in tools)
            Console.WriteLine($"{tool.Name} ({tool.CategoryId}) -> {tool.Command} {tool.Arguments}");

        return 0;
    }

    // ------------------------------------------------------------------
    // Exportacao da listagem
    // ------------------------------------------------------------------
    private static int RunExport(string[] args, HashSet<string> flags)
    {
        var destination = CliArgs.ValueAfter(args, "/export");
        if (string.IsNullOrWhiteSpace(destination))
        {
            Console.Error.WriteLine("Uso: tranal /export \"caminho\" [/txt|/html|/csv|/xml] [/columns \"Program,Version\"]");
            return 1;
        }

        if (!TryResolveColumns(CliArgs.ValueAfter(args, "/columns"), out var columns, out var erro))
        {
            Console.Error.WriteLine(erro);
            return 1;
        }

        var programs = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = true,
            IncludeMsiProducts = false,
        });

        string content;
        if (flags.Contains("/html")) content = Exporters.ToHtml(programs, columns);
        else if (flags.Contains("/csv")) content = Exporters.ToCsv(programs, columns);
        else if (flags.Contains("/xml")) content = Exporters.ToXml(programs, columns);
        else if (flags.Contains("/json")) content = Exporters.ToJson(programs, columns);
        else content = Exporters.ToText(programs, columns);

        try
        {
            File.WriteAllText(destination, content, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Não foi possível exportar a listagem: {ex.Message}");
            return 2;
        }

        Console.WriteLine($"Lista exportada: {destination} ({programs.Count} entradas)");
        return 0;
    }

    /// <summary>
    /// As colunas pedidas na linha de comando.
    ///
    /// Os nomes são os do arquivo exportado (Program, Version, Note...), fixos e
    /// independentes do idioma da interface — é o que torna o comando
    /// roteirizável. Null quando o usuário não pediu colunas: vale o conjunto
    /// fixo da exportação (<see cref="Exporters.DefaultColumns"/>), que é o
    /// mesmo do Revo — e não o catálogo inteiro de colunas da tela, que o
    /// usuário pode mudar a qualquer momento.
    /// </summary>
    private static bool TryResolveColumns(
        string? raw,
        out IReadOnlyList<ProgramColumn>? columns,
        out string? error)
    {
        columns = null;
        error = null;

        if (string.IsNullOrWhiteSpace(raw)) return true;

        var nomes = raw.Split(',', ';')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

        if (!ProgramColumns.TryParse(nomes, out var escolhidas, out var desconhecida))
        {
            error = desconhecida.Length == 0
                ? "Nenhuma coluna informada em /columns."
                : $"Coluna desconhecida: '{desconhecida}'. Colunas válidas: "
                    + string.Join(", ", ProgramColumns.All.Select(c => c.ExportName));
            return false;
        }

        columns = escolhidas;
        return true;
    }

    // ------------------------------------------------------------------
    // Modo caçador
    // ------------------------------------------------------------------
    private static int RunHunter(string[] args)
    {
        var path = CliArgs.ValueAfter(args, "/hunter");
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.Error.WriteLine("Uso: tranal /hunter \"caminho\"");
            return 1;
        }

        var resolution = HunterMode.Resolve(path);

        if (!resolution.Found || resolution.Program is null)
        {
            Console.WriteLine($"Não foi possível associar o item a um programa instalado: {resolution.Message}");
            return 1;
        }

        Console.WriteLine($"Programa identificado: {resolution.Program.DisplayName}");
        Console.WriteLine($"Versão: {resolution.Program.VersionOrNoVersion}");
        Console.WriteLine($"Executável: {resolution.ResolvedPath}");
        Console.WriteLine($"Chave de registro: {resolution.Program.RegistryPath}");
        Console.WriteLine();
        Console.WriteLine($"Use: tranal /mu \"{resolution.Program.DisplayName}\" /mode advanced");
        return 0;
    }

    // ------------------------------------------------------------------
    // Grupo "Novos Programas"
    // ------------------------------------------------------------------
    /// <summary>
    /// Lista o grupo "Novos Programas" usando a MESMA cadeia de datas do
    /// Tranal (e, portanto, a mesma da GUI). Serve para conferir a paridade
    /// com o Revo sem depender de captura de tela.
    /// </summary>
    private static int RunNewPrograms()
    {
        var programs = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = false,
        });

        var days = Math.Max(1, AppSettingsStore.Current.NewProgramsDays);
        var baseDate = DateTime.Now.AddDays(-days);
        var cutoff = new DateTime(baseDate.Year, baseDate.Month, baseDate.Day);

        var novos = programs
            .Where(p => p.InstallDateValue is { } date && date >= cutoff)
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Console.WriteLine($"\"Novos Programas\" (janela de {days} dias, corte {cutoff:dd/MM/yyyy}): {novos.Count}");
        Console.WriteLine();
        foreach (var p in novos)
            Console.WriteLine($"  {p.DisplayName} - {p.InstallDate}");
        Console.WriteLine();
        Console.WriteLine($"\"Outros Programas\": {programs.Count - novos.Count}");
        return 0;
    }

    // ------------------------------------------------------------------
    // Idioma e tema
    // ------------------------------------------------------------------
    private static int RunLanguage(string[] args)
    {
        var code = CliArgs.ValueAfter(args, "/lang");
        if (string.IsNullOrWhiteSpace(code))
        {
            Console.WriteLine($"Idioma atual: {Loc.Current}");
            Console.WriteLine($"Disponíveis ({LanguageCatalog.Count}): " +
                              string.Join(", ", LanguageCatalog.All.Select(l => l.Code)));
            return 0;
        }

        if (!LanguageCatalog.Exists(code))
        {
            Console.Error.WriteLine($"Idioma desconhecido: {code}");
            return 1;
        }

        AppSettingsStore.Update(s => s.Language = code);
        Loc.SetLanguage(code);
        Console.WriteLine($"Idioma definido: {code} ({LanguageCatalog.NativeName(code)})");
        return 0;
    }

    private static int RunTheme(string[] args)
    {
        var theme = CliArgs.ValueAfter(args, "/theme");
        if (string.IsNullOrWhiteSpace(theme))
        {
            Console.WriteLine($"Tema atual: {AppSettingsStore.Current.Theme}");
            Console.WriteLine("Disponíveis: Dark, Light");
            return 0;
        }

        var normalized = theme.Trim().ToLowerInvariant() switch
        {
            "light" or "claro" or "classico" => "Light",
            _ => "Dark",
        };

        AppSettingsStore.Update(s => s.Theme = normalized);
        Console.WriteLine($"Tema definido: {normalized}");
        return 0;
    }

    // ------------------------------------------------------------------
    // Gerenciador de Backups
    // ------------------------------------------------------------------
    /// <summary>
    /// Gerenciador de Backups: lista, restaura e apaga os pontos de
    /// restauracao criados pelo Tranal (equivalente ao modulo homonimo do
    /// Revo Uninstaller Pro).
    /// </summary>
    private static int RunBackupManager(string[] args, HashSet<string> flags)
    {
        var index = Array.FindIndex(args, a =>
            a.Equals("/backup", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/bk", StringComparison.OrdinalIgnoreCase));

        if (index < 0) return 1;

        // Os argumentos que seguem o comando sao os operandos; as flags
        // (comecadas por "/") sao tratadas a parte.
        var operands = args.Skip(index + 1)
            .Where(a => !a.StartsWith("/", StringComparison.Ordinal))
            .ToArray();

        if (operands.Length == 0) return ListRestorePoints();

        return operands[0].ToLowerInvariant() switch
        {
            "restore" => RestoreRestorePoint(args, operands.Skip(1).ToArray()),
            "delete" => DeleteRestorePoint(operands.Skip(1).ToArray(), flags),
            "delete-all" => DeleteAllRestorePoints(),
            "delete-older" => DeleteOlderRestorePoints(operands.Skip(1).ToArray(), flags),
            _ => UsageBackup(),
        };
    }

    /// <summary>
    /// Lista os pontos de restauracao: id, nome, operacao, data/hora, tamanho
    /// e a quantidade de itens de registro e de arquivos.
    /// </summary>
    private static int ListRestorePoints()
    {
        IReadOnlyList<RestorePoint> points;
        try
        {
            points = BackupManager.List();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Erro ao ler os pontos de restauração: {ex.Message}");
            return 2;
        }

        if (points.Count == 0)
        {
            Console.WriteLine(Loc.Get("Backup.Empty"));
            return 0;
        }

        Console.WriteLine($"{Loc.Get("Backup.Title")}: {points.Count} ponto(s) de restauração");
        Console.WriteLine();

        foreach (var point in points)
        {
            Console.WriteLine($"Id: {point.Id}");
            Console.WriteLine($"  {Loc.Get("Backup.Column.Name")}: {point.Name}");
            Console.WriteLine($"  {Loc.Get("Backup.Column.Operation")}: {point.Operation}");
            Console.WriteLine($"  {Loc.Get("Backup.Column.Date")}: {point.CreatedAt:dd/MM/yyyy} "
                              + $"{Loc.Get("Backup.Column.Time")}: {point.CreatedAt:HH:mm:ss}");
            Console.WriteLine($"  {Loc.Get("Backup.Column.Size")}: {FormatSize(point.Items.Sum(i => i.Size))}");
            Console.WriteLine($"  {Loc.Get("Backup.Column.Items")}: {point.Items.Count} "
                              + $"(registro: {point.RegistryItemCount}, arquivos/pastas: {point.FileItemCount})");
            Console.WriteLine();
        }

        return 0;
    }

    /// <summary>
    /// Restaura um ponto de restauracao. Sem caminhos, restaura tudo; com
    /// caminhos, restaura somente os itens que correspondem a eles.
    /// </summary>
    private static int RestoreRestorePoint(string[] args, string[] operands)
    {
        if (operands.Length == 0)
        {
            Console.Error.WriteLine("Uso: tranal /backup restore <id> [caminho...]");
            return 1;
        }

        var id = operands[0];
        var wanted = operands.Skip(1).ToArray();

        RestorePoint? point;
        try
        {
            point = BackupManager.Load(id);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Erro ao ler o ponto de restauração: {ex.Message}");
            return 2;
        }

        if (point is null)
        {
            Console.Error.WriteLine($"{Loc.Get("Backup.RestorePointDataNotFound")} ({id})");
            return 1;
        }

        List<BackupItem>? selected = null;
        if (wanted.Length > 0)
        {
            selected = point.Items.Where(item => MatchesPaths(item.OriginalPath, wanted)).ToList();
            if (selected.Count == 0)
            {
                Console.Error.WriteLine("Nenhum item do ponto de restauração corresponde aos caminhos informados.");
                return 1;
            }

            Console.WriteLine($"{Loc.Get("Backup.CustomRestore")}: {selected.Count} de {point.Items.Count} item(ns).");
        }

        // Devolver chaves do HKLM e arquivos em "Program Files" exige administrador.
        // Backups que ficam apenas no perfil do usuario sao restaurados sem elevacao.
        var targets = selected ?? point.Items;
        if (NeedsElevation(targets) && !Program.IsElevated())
        {
            Console.WriteLine("A restauração exige privilégios de administrador: relançando...");
            return Program.RelaunchElevated(args);
        }

        RestoreResult result;
        try
        {
            result = BackupManager.Restore(point, selected);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Erro ao restaurar: {ex.Message}");
            return 2;
        }

        foreach (var detail in result.Details)
            Console.WriteLine($"  {detail}");

        Console.WriteLine();
        Console.WriteLine(Loc.Get("Backup.RestoreDone", result.Restored, result.Failed));
        if (result.Skipped > 0)
            Console.WriteLine($"Ignorados: {result.Skipped}");

        return result.Succeeded ? 0 : 2;
    }

    /// <summary>Exclui um ponto de restauracao, apos confirmacao explicita.</summary>
    private static int DeleteRestorePoint(string[] operands, HashSet<string> flags)
    {
        if (operands.Length == 0)
        {
            Console.Error.WriteLine("Uso: tranal /backup delete <id>");
            return 1;
        }

        var id = operands[0];
        var point = BackupManager.Load(id);
        if (point is null)
        {
            Console.Error.WriteLine($"Ponto de restauração não encontrado: {id}");
            return 1;
        }

        if (!Confirm(Loc.Get("Backup.DeleteConfirm", point.Name), flags))
        {
            Console.WriteLine("Operação cancelada.");
            return 0;
        }

        var ok = BackupManager.Delete(id);
        Console.WriteLine(ok
            ? $"Ponto de restauração excluído: {id}"
            : $"Não foi possível excluir o ponto de restauração: {id}");
        return ok ? 0 : 2;
    }

    /// <summary>
    /// Exclui todos os pontos de restauracao. O comando e explicitamente
    /// destrutivo e sem ambiguidade, por isso nao pede confirmacao.
    /// </summary>
    private static int DeleteAllRestorePoints()
    {
        int count;
        try
        {
            count = BackupManager.DeleteAll();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Erro ao excluir os pontos de restauração: {ex.Message}");
            return 2;
        }

        Console.WriteLine($"Pontos de restauração excluídos: {count}");
        return 0;
    }

    /// <summary>Exclui os pontos de restauracao mais antigos que N dias.</summary>
    private static int DeleteOlderRestorePoints(string[] operands, HashSet<string> flags)
    {
        if (operands.Length == 0 || !int.TryParse(operands[0], out var days) || days <= 0)
        {
            Console.Error.WriteLine("Uso: tranal /backup delete-older <dias>");
            return 1;
        }

        var cutoff = DateTime.Now.AddDays(-days);
        if (!Confirm($"Tem certeza de que deseja excluir os pontos de restauração com mais de {days} dias?", flags))
        {
            Console.WriteLine("Operação cancelada.");
            return 0;
        }

        int count;
        try
        {
            count = BackupManager.DeleteOlderThan(cutoff);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Erro ao excluir os pontos de restauração: {ex.Message}");
            return 2;
        }

        Console.WriteLine($"Pontos de restauração excluídos: {count} (anteriores a {cutoff:dd/MM/yyyy})");
        return 0;
    }

    /// <summary>
    /// True quando a restauracao precisa do token administrativo: chaves de
    /// registro do sistema (HKLM, HKCR, HKU) e arquivos guardados em pastas
    /// protegidas como "Program Files" e "Windows".
    /// </summary>
    private static bool NeedsElevation(IEnumerable<BackupItem> items)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        };

        foreach (var item in items)
        {
            var path = item.OriginalPath;
            if (path.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase)) return true;
            if (path.StartsWith("HKEY_CLASSES_ROOT", StringComparison.OrdinalIgnoreCase)) return true;
            if (path.StartsWith("HKEY_USERS", StringComparison.OrdinalIgnoreCase)) return true;

            var normalized = NormalizeForMatch(path);
            foreach (var root in roots)
            {
                if (root.Length == 0) continue;
                if (normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Verifica se o caminho original de um item corresponde a um dos caminhos
    /// informados. A comparacao ignora maiusculas/minusculas e considera que
    /// um caminho de pasta inclui os itens guardados dentro dela.
    /// </summary>
    private static bool MatchesPaths(string originalPath, IReadOnlyList<string> wanted)
    {
        var original = NormalizeForMatch(originalPath);

        foreach (var candidate in wanted)
        {
            var target = NormalizeForMatch(candidate);
            if (target.Length == 0) continue;

            if (string.Equals(original, target, StringComparison.OrdinalIgnoreCase)) return true;
            if (original.StartsWith(target + "\\", StringComparison.OrdinalIgnoreCase)) return true;
            if (target.StartsWith(original + "\\", StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>
    /// Normaliza um caminho para comparacao. Caminhos de registro sao
    /// comparados como texto; os demais usam a normalizacao do nucleo.
    /// </summary>
    private static string NormalizeForMatch(string path)
    {
        var value = path.Trim().Trim('"');
        if (value.StartsWith("HKEY_", StringComparison.OrdinalIgnoreCase)) return value.TrimEnd('\\');
        return PathUtil.Normalize(value);
    }

    /// <summary>
    /// Pede confirmacao explicita no console. A flag "/yes" dispensa a pergunta.
    /// </summary>
    private static bool Confirm(string question, HashSet<string> flags)
    {
        if (flags.Contains("/yes")) return true;

        Console.Write($"{question} ({Loc.Get("Common.Yes")}/{Loc.Get("Common.No")}): ");
        var answer = Console.ReadLine();
        if (answer is null) return false;

        // A resposta pode chegar com a marca de ordem de bytes (BOM) do UTF-8
        // no inicio, quando o texto vem de um script ou de outro processo.
        var value = answer.Trim().Trim('\uFEFF').ToLowerInvariant();
        return value is "s" or "sim" or "y" or "yes";
    }

    /// <summary>Texto de uso do Gerenciador de Backups.</summary>
    private static int UsageBackup()
    {
        Console.Error.WriteLine("Uso: tranal /backup | /bk [restore <id> [caminho...] | delete <id> | delete-all | delete-older <dias>]");
        return 1;
    }

    /// <summary>Formata um tamanho em bytes, sem valor vazio quando o tamanho e zero.</summary>
    private static string FormatSize(long bytes)
    {
        var text = ByteFormatter.Format(bytes);
        return string.IsNullOrWhiteSpace(text) ? "0 KB" : text;
    }

    // ------------------------------------------------------------------
    // Lista de exclusoes
    // ------------------------------------------------------------------
    /// <summary>
    /// Gerenciador da lista de exclusoes: as pastas, os arquivos e as chaves de
    /// registro que o Tranal nunca toca. Equivale ao modulo homonimo do Revo
    /// Uninstaller Pro ("Add to Exclude List").
    ///
    /// A regra mora no nucleo (<see cref="ExcludeList"/>): a CLI apenas
    /// traduz o pedido do usuario em add/rm/clear e mostra o resultado.
    /// </summary>
    private static int RunExcludeManager(string[] args)
    {
        var index = Array.FindIndex(args, a => a.Equals("/exclude", StringComparison.OrdinalIgnoreCase));
        if (index < 0) return UsageExclude();

        // Os argumentos que seguem o comando sao os operandos; as flags
        // (comecadas por "/") sao tratadas a parte.
        var operands = args.Skip(index + 1)
            .Where(a => !a.StartsWith("/", StringComparison.Ordinal))
            .ToArray();

        if (operands.Length == 0) return ListExclusions();

        var action = operands[0].ToLowerInvariant();
        var value = operands.Length > 1 ? operands[1] : null;

        return action switch
        {
            "add" => ChangeExclusion(value, registry: false, add: true),
            "addreg" => ChangeExclusion(value, registry: true, add: true),
            "rm" => ChangeExclusion(value, registry: false, add: false),
            "rmreg" => ChangeExclusion(value, registry: true, add: false),
            "clear" => ClearExclusions(),
            _ => UsageExclude(),
        };
    }

    /// <summary>
    /// Mostra a lista de exclusoes atual: as pastas e os arquivos protegidos e
    /// as chaves de registro protegidas.
    /// </summary>
    private static int ListExclusions()
    {
        var list = ExcludeList.Current;

        Console.WriteLine($"Lista de exclusões: {list.Paths.Count} pasta(s)/arquivo(s) "
                          + $"e {list.RegistryKeys.Count} chave(s) de registro");
        Console.WriteLine();

        Console.WriteLine("Pastas e arquivos protegidos:");
        if (list.Paths.Count == 0) Console.WriteLine("  (nenhum)");
        foreach (var path in list.Paths) Console.WriteLine($"  {path}");

        Console.WriteLine();
        Console.WriteLine("Chaves de registro protegidas:");
        if (list.RegistryKeys.Count == 0) Console.WriteLine("  (nenhuma)");
        foreach (var key in list.RegistryKeys) Console.WriteLine($"  {key}");

        Console.WriteLine();
        Console.WriteLine($"Arquivo: {ExcludeList.FilePath}");
        return 0;
    }

    /// <summary>
    /// Adiciona ou remove um caminho (pasta/arquivo) ou uma chave de registro
    /// da lista de exclusoes, e grava a lista no disco.
    /// </summary>
    private static int ChangeExclusion(string? value, bool registry, bool add)
    {
        var what = registry ? "chave de registro" : "caminho";

        if (string.IsNullOrWhiteSpace(value))
        {
            Console.Error.WriteLine($"Uso: tranal /exclude {UsageAction(registry, add)} \"{what}\"");
            return 1;
        }

        var list = ExcludeList.Current;

        var changed = add
            ? (registry ? list.AddRegistryKey(value) : list.AddPath(value))
            : (registry ? list.RemoveRegistryKey(value) : list.RemovePath(value));

        if (!changed)
        {
            Console.WriteLine(add
                ? $"Já está na lista de exclusões: {value}"
                : $"Não está na lista de exclusões: {value}");
            return 0;
        }

        if (!list.Save())
        {
            Console.Error.WriteLine($"Não foi possível gravar a lista de exclusões: {ExcludeList.FilePath}");
            return 2;
        }

        if (registry)
            Console.WriteLine(add ? $"Chave de registro protegida: {value}" : $"Chave de registro liberada: {value}");
        else
            Console.WriteLine(add ? $"Pasta/arquivo protegido: {value}" : $"Pasta/arquivo liberado: {value}");

        return 0;
    }

    /// <summary>
    /// Esvazia a lista de exclusoes. O comando e explicitamente destrutivo e
    /// sem ambiguidade, por isso nao pede confirmacao (como o "delete-all" do
    /// Gerenciador de Backups).
    /// </summary>
    private static int ClearExclusions()
    {
        var list = ExcludeList.Current;
        if (list.IsEmpty)
        {
            Console.WriteLine("A lista de exclusões já está vazia.");
            return 0;
        }

        list.Clear();

        if (!list.Save())
        {
            Console.Error.WriteLine($"Não foi possível gravar a lista de exclusões: {ExcludeList.FilePath}");
            return 2;
        }

        Console.WriteLine("Lista de exclusões esvaziada.");
        return 0;
    }

    /// <summary>Nome da acao do gerenciador de exclusoes, para o texto de uso.</summary>
    private static string UsageAction(bool registry, bool add)
    {
        if (registry) return add ? "addreg" : "rmreg";
        return add ? "add" : "rm";
    }

    /// <summary>Texto de uso do gerenciador de exclusoes.</summary>
    private static int UsageExclude()
    {
        Console.Error.WriteLine("Uso: tranal /exclude "
                              + "[add \"caminho\" | addreg \"chave\" | rm \"caminho\" | rmreg \"chave\" | clear]");
        return 1;
    }

    // ------------------------------------------------------------------
    // Plano de desinstalacao (item 5 do backlog)
    // ------------------------------------------------------------------

    /// <summary>
    /// Mostra o plano de desinstalacao de um programa, sem executar nada.
    ///
    /// O plano e a ordem em que o Tranal vai trabalhar: comando antes,
    /// desinstalador nativo, varredura, remocao, comando depois. Ver o plano
    /// e o que separa "eu espero que o programa saia" de "eu sei o que vai
    /// acontecer".
    /// </summary>
    private static int RunPlan(string[] args)
    {
        var name = CliArgs.ValueAfter(args, "/plan");
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("Uso: tranal /plan \"nome do programa\" [/mode builtin|safe|moderate|advanced]");
            return 1;
        }

        var mode = UninstallMode.Moderate;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("/mode", StringComparison.OrdinalIgnoreCase)
                || args[i].Equals("/level", StringComparison.OrdinalIgnoreCase))
            {
                if (!Program.TryParseMode(args[i + 1], out mode))
                {
                    Console.Error.WriteLine($"Modo de desinstalação inválido: {args[i + 1]}");
                    Console.Error.WriteLine("Modos válidos: builtin, safe, moderate, advanced.");
                    return 1;
                }
            }
        }

        var programs = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = true,
            IncludeMsiProducts = false,
        });

        var target = programs.FirstOrDefault(p =>
            p.DisplayName.Equals(name, StringComparison.CurrentCultureIgnoreCase))
            ?? programs.FirstOrDefault(p =>
                p.DisplayName.Contains(name, StringComparison.CurrentCultureIgnoreCase));

        if (target is null)
        {
            Console.Error.WriteLine($"Programa não encontrado: {name}");
            return 1;
        }

        var plan = UninstallPlanBuilder.Build(target, mode);

        Console.WriteLine($"Plano de desinstalação: {plan.ProgramName}  (modo {mode})");
        Console.WriteLine();

        for (var i = 0; i < plan.Steps.Count; i++)
        {
            var step = plan.Steps[i];
            Console.WriteLine($"{i + 1}. [{step.Kind}] {step.Description}");
            if (!string.IsNullOrWhiteSpace(step.Command))
                Console.WriteLine($"   comando: {step.Command}");
        }

        Console.WriteLine();
        Console.WriteLine($"{plan.Steps.Count} etapa(s). Nada foi executado.");
        return 0;
    }

    // ------------------------------------------------------------------
    // Desinstalacao em lote a partir de um arquivo (item 6 do backlog)
    // ------------------------------------------------------------------

    /// <summary>
    /// Desinstalacao em lote roteirizavel a partir de um arquivo de lista.
    ///
    /// O arquivo tem uma regra por linha: nome exato, ou <c>regex:</c> seguido
    /// de uma expressao regular. Sem <c>/remove</c> o comando apenas **lista**
    /// o que casou — e a listagem sem efeito e o que permite conferir a
    /// expressao regular antes de ela rodar.
    /// </summary>
    private static int RunBatch(string[] args, HashSet<string> flags)
    {
        var path = CliArgs.ValueAfter(args, "/batch");
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.Error.WriteLine("Uso: tranal /batch \"arquivo\" [/remove] [/mode builtin|safe|moderate|advanced]");
            return 1;
        }

        var parsed = BatchList.Load(path);
        if (!parsed.Success || parsed.List is null)
        {
            Console.Error.WriteLine($"Não foi possível ler a lista: {parsed.ErrorMessage}");
            return 1;
        }

        var mode = UninstallMode.Moderate;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("/mode", StringComparison.OrdinalIgnoreCase)
                || args[i].Equals("/level", StringComparison.OrdinalIgnoreCase))
            {
                if (!Program.TryParseMode(args[i + 1], out mode))
                {
                    Console.Error.WriteLine($"Modo de desinstalação inválido: {args[i + 1]}");
                    return 1;
                }
            }
        }

        var programs = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = true,
            IncludeMsiProducts = false,
        });

        var matched = parsed.List.Match(programs);
        Console.WriteLine($"Programas casados com a lista: {matched.Count}");
        foreach (var program in matched)
            Console.WriteLine($"  - {program.DisplayName}");

        if (matched.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("(nada casou: nenhuma alteração foi feita)");
            return 0;
        }

        Console.WriteLine();

        if (!flags.Contains("/remove"))
        {
            Console.WriteLine("(dry-run: nada foi removido; use /remove para executar)");
            return 0;
        }

        if (!Program.IsElevated())
        {
            Console.WriteLine("A desinstalação exige privilégios de administrador: relançando...");
            return Program.RelaunchElevated(args);
        }

        var runBuiltIn = !flags.Contains("/nobuiltin");
        var exit = 0;

        // A fila serializa: um desinstalador por vez (dois instaladores ao
        // mesmo tempo disputam os mesmos arquivos), e quem nao desinstala em
        // silencio sai da tarefa em vez de travar o lote.
        var queue = UninstallQueue.Run(
            matched,
            program =>
            {
                Console.WriteLine();
                Console.WriteLine(new string('-', 60));

                var result = Program.UninstallProgram(program, programs, mode, runBuiltIn, remove: true, flags);
                if (result != 0) exit = result;
                return result == 0;
            },
            Program.IsStillInstalled);

        foreach (var item in queue)
        {
            if (item.CollidesWith.Length > 0)
                Console.WriteLine($"  [colisao] {item.Program.DisplayName} divide recursos com {item.CollidesWith}");

            if (item.Outcome == UninstallQueueOutcome.RemovedFromTask)
                Console.WriteLine($"  [fora da tarefa] {item.Program.DisplayName}: {item.Detail}");

            if (item.Outcome == UninstallQueueOutcome.Failed)
                Console.WriteLine($"  [falhou] {item.Program.DisplayName}: {item.Detail}");
        }

        return exit;
    }

    // ------------------------------------------------------------------
    // Switches por ferramenta (item 20 do backlog)
    // ------------------------------------------------------------------

    /// <summary>
    /// Lista os switches de linha de comando de cada ferramenta.
    ///
    /// O catalogo mora no nucleo (<c>Core/Automation/CliToolSwitch.cs</c>), que
    /// e a **fonte unica**: a ajuda da CLI e a documentacao leem a mesma lista,
    /// e um switch que existe em um lugar existe nos dois.
    /// </summary>
    private static int RunSwitches()
    {
        Console.WriteLine("Switches por ferramenta:");
        Console.WriteLine();

        foreach (var line in CliToolSwitchCatalog.DescribeAll())
            Console.WriteLine($"  {line}");

        Console.WriteLine();
        Console.WriteLine("A forma longa que executa a ferramenta é o nome com o sufixo "
                          + $"{CliToolSwitchCatalog.RunSuffix} (ex.: --temporarios-run).");
        return 0;
    }
}
