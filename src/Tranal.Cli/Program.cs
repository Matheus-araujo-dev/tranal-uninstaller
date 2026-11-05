using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Core.Topmost;

namespace Tranal.Cli;

/// <summary>
/// tranal.exe — linha de comando do Tranal Uninstaller.
/// Mantem o contrato do RevoCmd.exe:
///   tranal /m "programa" [/i] [/u] [/p] [/t]
///   tranal /winapps [/i] [/p]
///   tranal /?
/// E reproduz as operacoes do RevoUninPro.exe:
///   tranal /mu "programa" [/mode builtin|safe|moderate|advanced]
///   tranal /scan "programa" [/mode ...] [/remove]
///   tranal /backup | /bk [restore|delete|delete-all|delete-older]
///   tranal /exclude [add|addreg|rm|rmreg|clear]
/// </summary>
public static class Program
{
    /// <summary>Executa a CLI e devolve o codigo de saida (0 = sucesso, 1 = uso invalido, 2 = falha).</summary>
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // O catalogo de idioma e carregado sob demanda: sem isso, Loc.Get
        // devolveria a propria chave. A saida da CLI e sempre em portugues
        // do Brasil, independentemente do idioma escolhido para a interface.
        Loc.Load(Loc.DefaultLanguage);

        // Marcador interno: o processo ja foi relancado com elevacao.
        var elevated = args.Contains("/elevated", StringComparer.OrdinalIgnoreCase);
        var effective = args.Where(a => !a.Equals("/elevated", StringComparison.OrdinalIgnoreCase)).ToArray();

        if (effective.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        var flags = effective.Select(a => a.ToLowerInvariant()).ToHashSet();
        if (flags.Contains("/?") || flags.Contains("/help") || flags.Contains("-h"))
        {
            PrintHelp();
            return 0;
        }

        // Varredura de sobras: /scan "programa" [/mode ...] [/remove]
        if (flags.Contains("/scan"))
        {
            return RunScan(effective, flags, elevated);
        }

        // Desinstalacao completa: /mu "programa" [/mode ...] — paridade com RevoUninPro.exe /mu
        if (flags.Contains("/mu"))
        {
            return RunUninstall(effective, flags, elevated);
        }

        // Alvo apontado pelo menu de contexto do Explorer:
        // tranal.exe --uninstall-target "<caminho do executavel>"
        var hasTargetArgument = Array.Exists(effective,
            a => a.Equals(UninstallTargetArgument.Name, StringComparison.OrdinalIgnoreCase));
        if (hasTargetArgument)
        {
            var uninstallTarget = UninstallTargetArgument.Extract(effective);
            if (uninstallTarget.Length == 0)
            {
                Console.Error.WriteLine("Uso: tranal --uninstall-target \"<caminho do executavel>\"");
                return 1;
            }

            return RunUninstallTarget(uninstallTarget, elevated);
        }

        // Comandos com paridade de contrato com o RevoCmd/RevoUninPro:
        // /winapps, /wa, /be, /autorun, /tools, /export, /hunter, /lang, /theme
        // e o Gerenciador de Backups (/backup, /bk).
        var extra = ExtraCommands.TryRun(effective, flags);
        if (extra.HasValue) return extra.Value;

        var includeLocation = flags.Contains("/i");
        var includeUninstall = flags.Contains("/u");
        var asTable = flags.Contains("/t");
        var pause = flags.Contains("/p");

        string? pattern = null;
        for (var i = 0; i < effective.Length - 1; i++)
        {
            if (effective[i].Equals("/m", StringComparison.OrdinalIgnoreCase))
            {
                pattern = effective[i + 1];
                break;
            }
        }

        var options = new ProgramScanner.ScanOptions
        {
            // A CLI do Revo lista tambem os componentes de sistema (SystemComponent=1).
            IncludeSystemComponents = true,
            IncludeMsiProducts = false,
        };

        var programs = ProgramScanner.Scan(options);

        if (pattern is not null)
        {
            var regex = WildcardToRegex(pattern);
            programs = programs
                .Where(p => regex.IsMatch(p.DisplayName))
                .ToList();
        }

        if (asTable)
            PrintTable(programs, includeLocation, includeUninstall);
        else
            PrintPlain(programs, includeLocation, includeUninstall);

        if (pause) Pause();
        return 0;
    }

    /// <summary>
    /// Varre sobras de um programa. Sem /remove e apenas relatorio (dry-run).
    /// </summary>
    private static int RunScan(string[] args, HashSet<string> flags, bool elevated)
    {
        string? name = null;
        var mode = UninstallMode.Moderate;
        var remove = flags.Contains("/remove");

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("/scan", StringComparison.OrdinalIgnoreCase))
            {
                if (!args[i + 1].StartsWith("/", StringComparison.Ordinal)) name = args[i + 1];
                continue;
            }

            if (args[i].Equals("/mode", StringComparison.OrdinalIgnoreCase)
                || args[i].Equals("/level", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseMode(args[i + 1], out mode))
                {
                    Console.Error.WriteLine($"Modo de desinstalação inválido: {args[i + 1]}");
                    Console.Error.WriteLine("Modos válidos: builtin, safe, moderate, advanced.");
                    return 1;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("Uso: tranal /scan \"nome do programa\" "
                                  + "[/mode builtin|safe|moderate|advanced] [/all|/high] [/remove] "
                                  + "[/all-accounts] [/uninstall-all-accounts] [/kill] [/regbackup]");
            return 1;
        }

        // Remover chaves HKLM e arquivos em Program Files exige o token administrativo.
        if (remove && !elevated && !IsElevated())
        {
            Console.WriteLine("A remoção exige privilégios de administrador: relançando...");
            return RelaunchElevated(args);
        }

        var target = FindProgram(name);
        if (target is null)
        {
            Console.Error.WriteLine($"Programa não encontrado: {name}");
            return 1;
        }

        Console.WriteLine($"Alvo: {target.DisplayName}  ({target.VersionOrNoVersion})");
        Console.WriteLine($"Desinstalador: {target.UninstallString}");
        PrintMode(mode);
        Console.WriteLine();

        // O modo "Somente" nao varre: nao existe lista de sobras para mostrar.
        if (!UninstallModes.Scans(mode))
        {
            Console.WriteLine(Loc.Get("Wizard.ScanSkipped"));
            return 0;
        }

        // As opcoes de varredura sao resolvidas pelo nucleo: a CLI apenas
        // diz o que o usuario pediu e deixa a decisao (o que fica marcado, o
        // que entra das outras contas) para uma unica interpretacao.
        var scanOptions = BuildScanOptions(flags, mode);

        var items = LeftoverScanner.Scan(target, scanOptions);
        PrintOtherAccountSummary(items, scanOptions);

        if (items.Count == 0)
        {
            Console.WriteLine("Nenhuma sobra encontrada.");
            return 0;
        }

        var shown = SelectLeftovers(items, flags.Contains("/high"));
        PrintLeftovers(shown);
        Console.WriteLine();
        Console.WriteLine(DescribeLeftoverTotals(shown));
        PrintSelectionNote(flags.Contains("/all"), scanOptions);

        var options = BuildRemovalOptions(flags, $"Varredura de {target.DisplayName}");
        PrintRemovalOptions(options);
        Console.WriteLine();

        if (!remove)
        {
            Console.WriteLine("(dry-run: nada foi removido; use /remove para apagar)");
            return 0;
        }

        // O backup completo do registro vem antes da remocao. Aqui nao ha
        // desinstalador nativo, entao este e o ultimo momento possivel.
        var fullBackup = CreateFullRegistryBackup(options);

        var report = UninstallEngine.RemoveLeftovers(items, options, fullBackup);
        PrintRemovalSummary(report, options);

        return RemovalExitCode(report);
    }

    /// <summary>
    /// Cria o backup completo do registro quando a operacao pede por ele, e
    /// informa o resultado. O backup precisa existir ANTES de qualquer coisa
    /// que apague chaves: depois do desinstalador nativo, o registro ja esta
    /// limpo.
    /// </summary>
    private static FullRegistryBackupResult? CreateFullRegistryBackup(UninstallRemovalOptions options)
    {
        if (!options.FullRegistryBackup) return null;

        Console.WriteLine("Criando o backup completo do registro...");
        var backup = UninstallEngine.PrepareFullRegistryBackup(options);
        Console.WriteLine(backup is { Success: true }
            ? $"Backup completo do registro criado em {backup.Directory}"
            : $"Falha ao criar o backup completo do registro: {backup?.Message}");
        Console.WriteLine();

        return backup;
    }

    /// <summary>
    /// Codigo de saida da operacao: 0 quando nada falhou, 2 quando sobrou
    /// item protegido ou com falha. Um processo que nao pode ser encerrado
    /// tambem e falha da operacao — o arquivo dele continua em uso.
    /// </summary>
    private static int RemovalExitCode(RemovalReport report) =>
        (report.FailedCount
         + report.ProtectedCount
         + report.CountOf("Processo", RemovalStatus.Failed)) == 0 ? 0 : 2;

    /// <summary>
    /// Linha de resumo de uma remocao: as contagens de itens e o que saiu de
    /// dentro das pastas que ficaram no sistema.
    ///
    /// Uma pasta com item protegido e um item so na lista. Sem a segunda
    /// linha, o resumo diria "0 removidos" com arquivos de fato apagados —
    /// e esconderia o item que ficou para tras.
    /// </summary>
    private static string RemovalSummaryLine(RemovalReport report)
    {
        var line = $"Removidos: {report.RemovedCount} | Já inexistentes: {report.AbsentCount} | "
                 + $"Falhas: {report.FailedCount} | Protegidos: {report.ProtectedCount} | "
                 + $"Adiados para o próximo reinício: {report.DeferredCount}";

        if (report.RemovedInsideCount > 0 || report.ProtectedInsideCount > 0
            || report.FailedInsideCount > 0 || report.DeferredInsideCount > 0)
        {
            line += Environment.NewLine
                  + $"Dentro das pastas que permaneceram: {report.RemovedInsideCount} removido(s), "
                  + $"{report.ProtectedInsideCount} protegido(s), {report.FailedInsideCount} com falha, "
                  + $"{report.DeferredInsideCount} adiado(s).";
        }

        return line;
    }

    /// <summary>
    /// Desinstalacao completa: desinstalador nativo -> varredura -> remocao.
    /// Equivale ao fluxo do assistente (5 etapas) nao-interativo.
    /// </summary>
    private static int RunUninstall(string[] args, HashSet<string> flags, bool elevated)
    {
        string? name = null;
        var mode = UninstallMode.Moderate;
        var runBuiltIn = !flags.Contains("/nobuiltin");
        var remove = !flags.Contains("/noremove");

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("/mu", StringComparison.OrdinalIgnoreCase))
            {
                if (!args[i + 1].StartsWith("/", StringComparison.Ordinal)) name = args[i + 1];
                continue;
            }

            if (args[i].Equals("/mode", StringComparison.OrdinalIgnoreCase)
                || args[i].Equals("/level", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseMode(args[i + 1], out mode))
                {
                    Console.Error.WriteLine($"Modo de desinstalação inválido: {args[i + 1]}");
                    Console.Error.WriteLine("Modos válidos: builtin, safe, moderate, advanced.");
                    return 1;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("Uso: tranal /mu \"nome do programa\" "
                                  + "[/mode builtin|safe|moderate|advanced] [/all|/high] "
                                  + "[/recycle|/norecycle] [/backup|/nobackup] [/defer|/nodefer] "
                                  + "[/nobuiltin] [/noremove] [/all-accounts] [/uninstall-all-accounts] "
                                  + "[/kill] [/regbackup]");
            return 1;
        }

        if (!elevated && !IsElevated())
        {
            Console.WriteLine("A desinstalação exige privilégios de administrador: relançando...");
            return RelaunchElevated(args);
        }

        var target = FindProgram(name);
        if (target is null)
        {
            Console.Error.WriteLine($"Programa não encontrado: {name}");
            return 1;
        }

        Console.WriteLine($"Alvo: {target.DisplayName}  ({target.VersionOrNoVersion})");
        PrintMode(mode);
        Console.WriteLine();

        // Ponto de restauracao do sistema: mesma preferencia da interface grafica.
        if (AppSettingsStore.Current.CreateRestorePointBeforeUninstall)
        {
            var created = UninstallEngine.TryCreateRestorePoint($"Tranal Uninstaller Pro - {target.DisplayName}");
            Console.WriteLine(created
                ? "Ponto de restauração do sistema criado."
                : "Aviso: não foi possível criar o ponto de restauração do sistema "
                  + "(o serviço de restauração do Windows pode estar desativado).");
        }

        // As opcoes da operacao sao resolvidas uma vez, antes de tudo: o
        // backup completo do registro precisa existir ANTES do desinstalador
        // nativo, porque depois dele as chaves do programa ja foram apagadas.
        var options = BuildRemovalOptions(flags, $"Desinstalação de {target.DisplayName}");

        // O backup completo do registro vem ANTES do desinstalador nativo: e
        // o contrato do original ("Make a System Restore Point and a full
        // Registry backup before uninstalling", 15162).
        var fullBackup = CreateFullRegistryBackup(options);

        var results = new List<BuiltInUninstallResult>();
        if (runBuiltIn)
        {
            Console.WriteLine("Executando o desinstalador nativo...");
            results.Add(UninstallEngine.RunBuiltInUninstaller(target).GetAwaiter().GetResult());
            foreach (var result in results) Console.WriteLine("  " + result.Describe());
            Console.WriteLine();
        }

        var items = new List<LeftoverItem>();
        RemovalReport report = new(Array.Empty<RemovalOutcome>());

        // O modo "Somente" roda apenas o desinstalador nativo: nenhuma sobra e procurada.
        if (UninstallModes.Scans(mode))
        {
            var scanOptions = BuildScanOptions(flags, mode);

            items = LeftoverScanner.Scan(target, scanOptions);
            PrintOtherAccountSummary(items, scanOptions);
            Console.WriteLine($"Sobras encontradas: {items.Count}");

            var shown = SelectLeftovers(items, flags.Contains("/high"));
            PrintLeftovers(shown);
            Console.WriteLine();
            Console.WriteLine(DescribeLeftoverTotals(shown));
            PrintSelectionNote(flags.Contains("/all"), scanOptions);
            Console.WriteLine();

            PrintRemovalOptions(options);
            Console.WriteLine();

            if (remove)
            {
                report = UninstallEngine.RemoveLeftovers(items, options, fullBackup);
                PrintRemovalSummary(report, options);
            }
            else
            {
                Console.WriteLine("(dry-run: nada foi removido)");
            }
        }
        else
        {
            Console.WriteLine(Loc.Get("Wizard.ScanSkipped"));
            Console.WriteLine("O modo \"Somente\" executa apenas o desinstalador nativo: nenhuma sobra foi procurada.");
        }

        if (remove)
        {
            UninstallHistory.Append(new UninstallHistoryEntry
            {
                Programs = target.DisplayName,
                Level = mode.ToString(),
                BuiltIn = string.Join(" | ", results.Select(r => r.Describe())),
                Report = report.Describe(),
                Removed = report.RemovedCount,
                Failed = report.FailedCount + report.ProtectedCount,
            });
        }

        return RemovalExitCode(report);
    }

    /// <summary>
    /// Desinstalacao a partir de um caminho de executavel — e o que o menu de
    /// contexto do Explorer chama. O alvo nao precisa estar registrado no
    /// Windows: o Tranal varre o sistema em busca do que pertence ao programa
    /// e mostra o que encontrou antes de remover.
    /// </summary>
    private static int RunUninstallTarget(string path, bool elevated)
    {
        var full = PathUtil.Normalize(path);
        if (full.Length == 0 || (!File.Exists(full) && !Directory.Exists(full)))
        {
            Console.Error.WriteLine($"Alvo não encontrado: {path}");
            return 1;
        }

        // Resolver o alvo reuse a regra do Modo Caçador: o nome vem do arquivo
        // apontado, nunca de uma segunda interpretacao.
        var resolved = TopmostTargetResolver.ResolveFromFile(full);
        var name = resolved?.DisplayName ?? Path.GetFileName(full);

        Console.WriteLine($"Alvo: {name}  ({full})");
        if (resolved?.Program is not null)
        {
            Console.WriteLine($"Programa instalado: {resolved.Program.DisplayName} "
                + $"({resolved.Program.VersionOrNoVersion})");
            Console.WriteLine($"Desinstalador: {resolved.Program.UninstallString}");
        }
        else
        {
            Console.WriteLine("O alvo não corresponde a um programa registrado no Windows.");
        }

        if (!elevated && !IsElevated())
        {
            Console.WriteLine("A remoção exige privilégios de administrador: relançando...");
            return RelaunchElevated(new[] { "--uninstall-target", full });
        }

        // A desinstalacao forcada usa as mesmas preferencias da interface
        // grafica (inclusive "marcar todos os itens restantes"), porem a
        // profundidade e sempre a mais completa.
        var scanOptions = LeftoverScanOptions.FromSettings(UninstallMode.Advanced);
        var items = LeftoverScanner.ScanForced(full, scanOptions);
        Console.WriteLine();
        Console.WriteLine($"Sobras encontradas: {items.Count}");

        foreach (var item in items)
        {
            var mark = item.Selected ? "marcado" : "não marcado";
            Console.WriteLine($"  [{item.Kind}] {item.Path}   ({item.Detail}) — confiança: {item.Confidence}, {mark}");
        }

        if (items.Count == 0)
        {
            Console.WriteLine("Nada a remover.");
            return 0;
        }

        var removalOptions = UninstallRemovalOptions.FromSettings($"Desinstalação de {name}");
        var fullBackup = CreateFullRegistryBackup(removalOptions);
        var report = UninstallEngine.RemoveLeftovers(items, removalOptions, fullBackup);

        Console.WriteLine();
        Console.WriteLine(report.Describe());
        Console.WriteLine();
        Console.WriteLine(RemovalSummaryLine(report));

        if (!string.IsNullOrEmpty(report.RestorePointId))
            Console.WriteLine($"Ponto de restauração criado: {report.RestorePointId}");

        PrintExtraRemovalInfo(report, removalOptions);

        UninstallHistory.Append(new UninstallHistoryEntry
        {
            Programs = name,
            Level = UninstallMode.Advanced.ToString(),
            BuiltIn = "(alvo apontado pelo menu de contexto)",
            Report = report.Describe(),
            Removed = report.RemovedCount,
            Failed = report.FailedCount + report.ProtectedCount,
        });

        return RemovalExitCode(report);
    }

    private static InstalledProgram? FindProgram(string name)
    {
        var programs = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = true,
            IncludeMsiProducts = false,
        });

        return programs.FirstOrDefault(p =>
            p.DisplayName.Contains(name, StringComparison.CurrentCultureIgnoreCase))
            ?? programs.FirstOrDefault(p =>
                p.DisplayName.Equals(name, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>
    /// Converte o texto do modo de desinstalacao. Aceita os nomes em ingles
    /// (usados na linha de comando) e os nomes curtos em portugues.
    /// </summary>
    private static bool TryParseMode(string value, out UninstallMode mode)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "builtin":
            case "somente":
                mode = UninstallMode.BuiltIn;
                return true;

            case "safe":
            case "seguro":
                mode = UninstallMode.Safe;
                return true;

            case "moderate":
            case "moderado":
                mode = UninstallMode.Moderate;
                return true;

            case "advanced":
            case "avancado":
                mode = UninstallMode.Advanced;
                return true;

            default:
                mode = UninstallMode.Moderate;
                return false;
        }
    }

    /// <summary>
    /// Restringe a lista exibida aos itens de alta confianca quando o usuario
    /// pediu com "/high".
    ///
    /// A marcacao do que sera removido ja foi resolvida pelo nucleo (em
    /// <see cref="LeftoverScanOptions"/>): a CLI nao mantem uma segunda
    /// interpretacao de "marcar todos".
    /// </summary>
    private static List<LeftoverItem> SelectLeftovers(List<LeftoverItem> items, bool onlyHigh) =>
        onlyHigh
            ? items.Where(i => i.IsHighConfidence).ToList()
            : items.ToList();

    /// <summary>
    /// Monta as opcoes de varredura. A base sao sempre as preferencias do
    /// usuario (as mesmas que a interface grafica usa); as flags da linha de
    /// comando apenas sobrescrevem o que foi explicitamente pedido.
    ///
    /// A decisao do que fica marcado para remocao — e do que pertence a outra
    /// conta do Windows — e do nucleo: aqui so se traduz o que o usuario
    /// pediu.
    /// </summary>
    private static LeftoverScanOptions BuildScanOptions(HashSet<string> flags, UninstallMode mode)
    {
        var options = LeftoverScanOptions.FromSettings(mode);

        var allAccounts = flags.Contains("/all-accounts");
        var uninstallAll = flags.Contains("/uninstall-all-accounts");

        // "/uninstall-all-accounts" so faz sentido junto de "/all-accounts":
        // sem a varredura em todas as contas nao existe item de outra conta
        // para marcar. Nesse caso as duas valem, e o usuario e avisado.
        if (uninstallAll && !allAccounts)
        {
            Console.WriteLine("Aviso: /uninstall-all-accounts só faz sentido junto com /all-accounts; "
                              + "a varredura em todas as contas do Windows foi ativada.");
            allAccounts = true;
        }

        return options with
        {
            // "/high" restringe a lista as sobras de alta confianca: marcar
            // tambem as de media e baixa faria o oposto do que foi pedido.
            CheckAllLeftovers = !flags.Contains("/high")
                                 && (flags.Contains("/all") || options.CheckAllLeftovers),

            // A flag prevalece sobre a preferencia: quem pede a varredura em
            // todas as contas nao precisa mexer nas Opcoes antes.
            ScanAllUserAccounts = allAccounts || options.ScanAllUserAccounts,
            SelectOtherAccountItems = uninstallAll || options.SelectOtherAccountItems,
        };
    }

    /// <summary>
    /// Lista as sobras encontradas, indicando a confianca de cada item.
    /// Itens de outra conta do Windows sao sinalizados: eles exigem pedido
    /// explicito para entrar na remocao.
    /// </summary>
    private static void PrintLeftovers(List<LeftoverItem> items)
    {
        foreach (var item in items)
        {
            var detail = string.IsNullOrWhiteSpace(item.Detail) ? string.Empty : $"   ({item.Detail})";
            var marker = item.Selected ? "marcado" : "não marcado";
            var account = item.OtherAccount ? " - outra conta do Windows" : string.Empty;
            Console.WriteLine($"  [{item.Kind}] {item.Path}{detail}"
                              + $"   confiança: {ConfidenceLabel(item.Confidence)} - {marker}{account}");
        }
    }

    /// <summary>
    /// Explica o que sera removido conforme a selecao de sobras.
    /// </summary>
    private static void PrintSelectionNote(bool removeAll, LeftoverScanOptions options)
    {
        if (removeAll)
            Console.WriteLine("Atenção: /all inclui na remoção as sobras de confiança média e baixa.");
        else if (options.CheckAllLeftovers)
            Console.WriteLine("Atenção: a preferência \"marcar todos os itens restantes\" está ligada; "
                              + "as sobras de confiança média e baixa também entram na remoção.");
        else
            Console.WriteLine("Somente os itens marcados (confiança alta) são removidos; os demais são apenas listados.");

        if (options.ScanAllUserAccounts && !options.SelectOtherAccountItems)
            Console.WriteLine("Atenção: as sobras de outras contas do Windows são apenas listadas; "
                              + "use /uninstall-all-accounts para marcá-las.");
    }

    /// <summary>
    /// Informa quantas sobras pertencem a outras contas do Windows. So faz
    /// sentido quando a varredura incluiu as outras contas.
    /// </summary>
    private static void PrintOtherAccountSummary(List<LeftoverItem> items, LeftoverScanOptions options)
    {
        if (!options.ScanAllUserAccounts) return;

        Console.WriteLine($"Sobras em outras contas do Windows: {items.Count(i => i.OtherAccount)}");
    }

    /// <summary>
    /// Resume a contagem das sobras por confianca e quantas entram na remocao.
    /// </summary>
    private static string DescribeLeftoverTotals(List<LeftoverItem> items)
    {
        var high = items.Count(i => i.Confidence == LeftoverConfidence.High);
        var medium = items.Count(i => i.Confidence == LeftoverConfidence.Medium);
        var low = items.Count(i => i.Confidence == LeftoverConfidence.Low);
        var marked = items.Count(i => i.Selected);

        return $"Total: {items.Count} {Loc.Get("Common.Items")} "
             + $"(confiança alta: {high}, média: {medium}, baixa: {low}) "
             + $"| marcados para remoção: {marked}";
    }

    /// <summary>Rotulo da confianca de uma sobra, no idioma ativo.</summary>
    private static string ConfidenceLabel(LeftoverConfidence confidence) => confidence switch
    {
        LeftoverConfidence.High => Loc.Get("Common.High"),
        LeftoverConfidence.Medium => Loc.Get("Common.Medium"),
        LeftoverConfidence.Low => Loc.Get("Common.Low"),
        _ => confidence.ToString(),
    };

    /// <summary>
    /// Mostra o modo de desinstalacao escolhido e o que ele faz.
    /// </summary>
    private static void PrintMode(UninstallMode mode)
    {
        Console.WriteLine($"Modo: {UninstallModes.ShortName(mode)}");
        var description = Loc.Get(UninstallModes.DescriptionKey(mode));
        if (!string.IsNullOrWhiteSpace(description)) Console.WriteLine($"  {description}");
    }

    /// <summary>
    /// Monta as opcoes de remocao. A base sao sempre as preferencias do
    /// usuario (as mesmas que a interface grafica usa); as flags da linha de
    /// comando apenas sobrescrevem o que foi explicitamente pedido.
    /// </summary>
    private static UninstallRemovalOptions BuildRemovalOptions(HashSet<string> flags, string restorePointName)
    {
        var settings = UninstallRemovalOptions.FromSettings(restorePointName);

        return new UninstallRemovalOptions
        {
            Backup = ResolveFlag(flags, "/backup", "/nobackup", settings.Backup),
            UseRecycleBin = ResolveFlag(flags, "/recycle", "/norecycle", settings.UseRecycleBin),
            DeferToNextRestart = ResolveFlag(flags, "/defer", "/nodefer", settings.DeferToNextRestart),
            KillRunningProcesses = ResolveFlag(flags, "/kill", "/nokill", settings.KillRunningProcesses),
            FullRegistryBackup = ResolveFlag(flags, "/regbackup", "/noregbackup", settings.FullRegistryBackup),
            RestorePointName = settings.RestorePointName,
        };
    }

    /// <summary>Verdadeiro/falso conforme a flag usada; sem flag, vale o padrao.</summary>
    private static bool ResolveFlag(HashSet<string> flags, string on, string off, bool current) =>
        flags.Contains(on) ? true : flags.Contains(off) ? false : current;

    /// <summary>
    /// Mostra as opcoes de remocao que serao aplicadas.
    /// </summary>
    private static void PrintRemovalOptions(UninstallRemovalOptions options)
    {
        Console.WriteLine("Opções de remoção: "
                        + $"backup antes de remover: {(options.Backup ? "sim" : "não")} | "
                        + $"Lixeira: {(options.UseRecycleBin ? "sim" : "não")} | "
                        + $"adiar para o próximo reinício: {(options.DeferToNextRestart ? "sim" : "não")} | "
                        + $"encerrar executáveis: {(options.KillRunningProcesses ? "sim" : "não")} | "
                        + $"backup completo do registro: {(options.FullRegistryBackup ? "sim" : "não")}");
    }

    /// <summary>
    /// Mostra o relatorio da remocao: contagens, itens enviados para a Lixeira,
    /// itens adiados para o proximo reinicio e o ponto de restauracao criado.
    /// </summary>
    private static void PrintRemovalSummary(RemovalReport report, UninstallRemovalOptions options)
    {
        var description = report.Describe();
        if (!string.IsNullOrWhiteSpace(description))
        {
            Console.WriteLine(description);
            Console.WriteLine();
        }

        Console.WriteLine(RemovalSummaryLine(report));
        Console.WriteLine(Loc.Get("Wizard.SummaryRecycleBin", CountRecycleBin(report)));

        if (!string.IsNullOrWhiteSpace(report.RestorePointId))
            Console.WriteLine(Loc.Get("Wizard.BackupPointSaved", report.RestorePointId));

        PrintExtraRemovalInfo(report, options);
    }

    /// <summary>
    /// Mostra as informacoes que so existem quando as opcoes correspondentes
    /// estao ligadas: os processos encerrados.
    /// A CLI nao repete a decisao do nucleo — ela mostra o que o relatorio
    /// canonico devolveu.
    ///
    /// O backup completo do registro NAO aparece aqui: ele e criado antes do
    /// desinstalador nativo e ja foi informado no momento da criacao.
    /// </summary>
    private static void PrintExtraRemovalInfo(RemovalReport report, UninstallRemovalOptions options)
    {
        if (options.KillRunningProcesses)
        {
            var terminated = report.CountOf("Processo", RemovalStatus.Removed);
            var failed = report.CountOf("Processo", RemovalStatus.Failed);
            Console.WriteLine($"Processos encerrados: {terminated}"
                              + (failed > 0 ? $" | Não encerrados: {failed}" : string.Empty));
        }
    }

    /// <summary>
    /// Conta os itens que foram enviados para a Lixeira. O nucleo sinaliza a
    /// Lixeira pela mensagem do resultado ("enviado para a Lixeira").
    /// </summary>
    private static int CountRecycleBin(RemovalReport report) =>
        report.Outcomes.Count(o => o.Message.Contains("Lixeira", StringComparison.OrdinalIgnoreCase));

    /// <summary>True quando o processo atual roda com o token administrativo.</summary>
    internal static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    /// <summary>Relanca a propria CLI com o token administrativo (elevacao UAC).</summary>
    internal static int RelaunchElevated(string[] args)
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return 3;

            var startInfo = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                Verb = "runas",
            };
            foreach (var arg in args) startInfo.ArgumentList.Add(arg);
            startInfo.ArgumentList.Add("/elevated");

            using var process = Process.Start(startInfo);
            if (process is null) return 3;

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine("A elevação foi cancelada ou negada: a remoção exige administrador.");
            return 3;
        }
    }

    private static void PrintPlain(
        List<InstalledProgram> programs, bool includeLocation, bool includeUninstall)
    {
        foreach (var p in programs)
        {
            var line = $"{p.DisplayName} - {p.VersionOrNoVersion}";
            if (includeLocation && !string.IsNullOrWhiteSpace(p.InstallLocation))
                line += $" - {p.InstallLocation}";
            if (includeUninstall && !string.IsNullOrWhiteSpace(p.UninstallString))
                line += $" - {p.UninstallString}";
            Console.WriteLine(line);
            Console.WriteLine();
        }
    }

    private static void PrintTable(
        List<InstalledProgram> programs, bool includeLocation, bool includeUninstall)
    {
        var headers = new List<string> { "Programa", "Versão" };
        if (includeLocation) headers.Add("Local de instalação");
        if (includeUninstall) headers.Add("Comando de desinstalação");
        Console.WriteLine(string.Join("\t", headers));
        foreach (var p in programs)
        {
            var cells = new List<string> { p.DisplayName, p.VersionOrNoVersion };
            if (includeLocation) cells.Add(p.InstallLocation);
            if (includeUninstall) cells.Add(p.UninstallString);
            Console.WriteLine(string.Join("\t", cells));
        }
    }

    /// <summary>Converte um padrao com curingas * e ? para regex.</summary>
    internal static Regex WildcardToRegex(string pattern)
    {
        var escaped = System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".");
        return new Regex($"^{escaped}$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.Singleline);
    }

    private static void Pause()
    {
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey(true);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("tranal v1.0 - Tranal Uninstaller Pro");
        Console.WriteLine();
        Console.WriteLine("Uso: tranal /m \"nome do programa\" [/i] [/u] [/p] [/t]");
        Console.WriteLine("/m - Casa o nome do programa usando curingas * e ?");
        Console.WriteLine("     O asterisco (*) representa qualquer coleção de caracteres");
        Console.WriteLine("     O ponto de interrogação (?) representa um único caractere");
        Console.WriteLine("     Exemplo: tranal /m Microso*");
        Console.WriteLine("     Mostra todos os programas que começam com o nome Microso");
        Console.WriteLine();
        Console.WriteLine("/i - Inclui o local de instalação, quando existir");
        Console.WriteLine("/u - Inclui o comando de desinstalação");
        Console.WriteLine("/p - Pausa ao final da listagem");
        Console.WriteLine("/t - Mostra o resultado em tabela");
        Console.WriteLine("/winapps - Lista os aplicativos do Windows");
        Console.WriteLine("/? | /help - Esta ajuda");
        Console.WriteLine();
        Console.WriteLine("Operações:");
        Console.WriteLine("tranal /mu \"nome do programa\" [/mode builtin|safe|moderate|advanced] [/all|/high] "
                        + "[/recycle|/norecycle] [/backup|/nobackup] [/defer|/nodefer] [/nobuiltin] [/noremove] "
                        + "[/all-accounts] [/uninstall-all-accounts] [/kill] [/regbackup]");
        Console.WriteLine("     Desinstala um programa (desinstalador nativo, varredura de sobras e remoção)");
        Console.WriteLine("tranal /scan \"nome do programa\" [/mode builtin|safe|moderate|advanced] [/all|/high] "
                        + "[/remove] [/all-accounts] [/uninstall-all-accounts] [/kill] [/regbackup]");
        Console.WriteLine("     Lista as sobras (arquivos, pastas e chaves de registro) com a confiança de cada item");
        Console.WriteLine();
        Console.WriteLine("Modos de desinstalação (/mode, ou /level como apelido):");
        Console.WriteLine("builtin   - Apenas o desinstalador nativo; nenhuma sobra é procurada");
        Console.WriteLine("safe      - Varredura das sobras seguras de apagar (a mais rápida)");
        Console.WriteLine("moderate  - Varredura estendida nos lugares mais comuns do Registro e do disco");
        Console.WriteLine("advanced  - Varredura profunda e completa no Registro e no disco (a mais lenta)");
        Console.WriteLine();
        Console.WriteLine("Opções de varredura e remoção:");
        Console.WriteLine("/all      - Inclui as sobras de confiança média e baixa também na remoção");
        Console.WriteLine("/high     - Considera somente as sobras de alta confiança");
        Console.WriteLine("/recycle  - Envia arquivos e pastas para a Lixeira (padrão: conforme as preferências)");
        Console.WriteLine("/norecycle - Apaga arquivos e pastas de forma definitiva");
        Console.WriteLine("/backup   - Cria o ponto de restauração antes de remover (padrão: conforme as preferências)");
        Console.WriteLine("/nobackup - Não cria o ponto de restauração antes de remover");
        Console.WriteLine("/defer    - Adia para o próximo reinício os itens em uso (padrão: conforme as preferências)");
        Console.WriteLine("/nodefer  - Não adia para o próximo reinício os itens em uso");
        Console.WriteLine("/all-accounts - Procura sobras também nas outras contas do Windows "
                        + "(prevalece sobre as preferências)");
        Console.WriteLine("/uninstall-all-accounts - Marca para remoção as sobras das outras contas do Windows "
                        + "(usar junto com /all-accounts)");
        Console.WriteLine("/kill     - Encerra os executáveis do programa antes de remover");
        Console.WriteLine("/regbackup - Cria o backup completo do registro antes de desinstalar");
        Console.WriteLine();
        Console.WriteLine("Lista de exclusões (itens que o Tranal nunca toca):");
        Console.WriteLine("tranal /exclude                     - Lista as pastas, os arquivos e as chaves protegidos");
        Console.WriteLine("tranal /exclude add \"caminho\"       - Protege uma pasta ou um arquivo");
        Console.WriteLine("tranal /exclude addreg \"chave\"      - Protege uma chave de registro");
        Console.WriteLine("tranal /exclude rm \"caminho\"        - Libera a proteção de uma pasta ou arquivo");
        Console.WriteLine("tranal /exclude rmreg \"chave\"       - Libera a proteção de uma chave de registro");
        Console.WriteLine("tranal /exclude clear               - Esvazia a lista de exclusões");
        Console.WriteLine();
        Console.WriteLine("Gerenciador de Backups:");
        Console.WriteLine("tranal /backup | /bk                   - Lista os pontos de restauração");
        Console.WriteLine("tranal /backup restore <id> [caminhos] - Restaura um ponto de restauração");
        Console.WriteLine("tranal /backup delete <id>             - Exclui um ponto de restauração (pede confirmação)");
        Console.WriteLine("tranal /backup delete-all              - Exclui todos os pontos de restauração");
        Console.WriteLine("tranal /backup delete-older <dias>     - Exclui os pontos com mais de N dias (pede confirmação)");
        Console.WriteLine("/yes      - Dispensa a confirmação nas operações destrutivas");
        Console.WriteLine();
        Console.WriteLine("Módulos e ferramentas:");
        Console.WriteLine("tranal /winapps              - Lista os aplicativos do Windows");
        Console.WriteLine("tranal /wa \"App1;App2\"       - Desinstala aplicativos do Windows pelo nome");
        Console.WriteLine("tranal /be                   - Lista as extensões de navegador");
        Console.WriteLine("tranal /be \"Ext1;Ext2\" /remove - Remove extensões de navegador pelo nome");
        Console.WriteLine("tranal /autorun              - Lista os itens de inicialização automática (chaves, serviços, tarefas)");
        Console.WriteLine("tranal /tools                - Lista as ferramentas nativas do Windows");
        Console.WriteLine("tranal /export \"caminho\" [/txt|/html|/csv|/xml] - Exporta a listagem de programas");
        Console.WriteLine("tranal /export \"caminho\" /csv /columns \"Program,Version,Note\" - Exporta só as colunas pedidas");
        Console.WriteLine("tranal /hunter \"caminho\"       - Resolve um atalho/executável para o programa instalado");
        Console.WriteLine("tranal /lang [código]        - Mostra ou define o idioma da interface");
        Console.WriteLine("tranal /theme [dark|light]   - Mostra ou define o tema da interface");
        Console.WriteLine("tranal /new                  - Lista o grupo \"Novos Programas\"");
    }
}
