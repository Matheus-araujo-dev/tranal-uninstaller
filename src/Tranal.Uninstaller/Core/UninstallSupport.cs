using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Discovery;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Confianca da associacao entre uma sobra e o programa removido.
///
/// O Revo Uninstaller Pro destaca em negrito apenas os itens de confianca
/// alta e avisa: "somente os itens marcados em negrito e seus subitens serao
/// excluidos". No Tranal essa regra e aplicada de forma explicita: itens de
/// confianca alta vem marcados para remocao, os demais vem desmarcados.
/// </summary>
public enum LeftoverConfidence
{
    /// <summary>
    /// Alta: o item pertence de fato ao programa — a pasta de instalacao
    /// registrada, a chave de desinstalacao ou um item com o nome exato.
    /// </summary>
    High,

    /// <summary>Media: o nome casa com uma variante conhecida do programa.</summary>
    Medium,

    /// <summary>
    /// Baixa: o nome apenas se parece com o do programa (casamento por
    /// subpasta ou por segundo nivel da arvore de registro).
    /// </summary>
    Low,
}

/// <summary>
/// O motivo pelo qual um item foi considerado sobra do programa.
///
/// A confianca sozinha diz o quanto o Tranal acredita no item; o motivo diz
/// <c>por que</c>. Sao o mesmo julgamento em duas partes, e por isso sao
/// decididos sempre juntos, no mesmo lugar
/// (<see cref="LeftoverScanner.Judge"/>).
///
/// O vocabulario segue o do Bulk Crap Uninstaller 6.3.0
/// (<c>docs/auditoria/BCU.md</c>, secao 2.3), que e o concorrente de codigo
/// aberto: la cada nivel de confianca vem acompanhado do motivo.
/// </summary>
public enum LeftoverReason
{
    /// <summary>Sem motivo registrado.</summary>
    Unknown,

    /// <summary>E a pasta de instalacao registrada do programa.</summary>
    InstallFolder,

    /// <summary>E um alvo informado pelo usuario.</summary>
    ExplicitTarget,

    /// <summary>O nome e exatamente o do programa.</summary>
    ExactNameMatch,

    /// <summary>O nome casa com uma variante conhecida do programa.</summary>
    VariantNameMatch,

    /// <summary>O nome apenas se parece com o do programa.</summary>
    SimilarNameMatch,

    /// <summary>E uma subpasta de uma pasta que se parece com a do programa.</summary>
    SubfolderMatch,

    /// <summary>
    /// O nome esta no segundo nivel da chave de registro: uma chave dentro
    /// da chave do programa, e nao a chave do programa.
    /// </summary>
    RegistrySecondLevel,

    /// <summary>E uma entrada obsoleta do Menu Iniciar.</summary>
    ObsoleteStartMenu,

    /// <summary>
    /// E uma entrada de registro que sobrou: o registro aponta para um
    /// caminho que nao existe mais no disco.
    /// </summary>
    OrphanedRegistryEntry,

    /// <summary>E um atalho obsoleto do Menu Iniciar.</summary>
    ObsoleteShortcut,

    /// <summary>Veio do historico de remocao do proprio Tranal.</summary>
    History,

    /// <summary>E o encerramento de um processo do programa.</summary>
    ProcessTermination,

    /// <summary>
    /// O proprio instalador do programa registrou este item e ele continua
    /// no sistema depois que o desinstalador nativo rodou.
    ///
    /// Este e o motivo mais forte que existe: nao e casamento de nome, e o
    /// registro do instalador. Vale para os produtos do Windows Installer
    /// (149 dos 211 programas desta maquina), em que o pacote .msi descreve
    /// arquivo por arquivo o que gravou.
    /// </summary>
    InstallerLog,

    /// <summary>
    /// E uma entrada de inicializacao do Windows que aponta para o programa:
    /// o <c>Run</c>, o <c>RunOnce</c> e companhia.
    ///
    /// Este e o sinal que a confianca vem do registro de inicializacao, e nao
    /// do nome: uma entrada que sobe com o Windows e do programa que a
    /// gravou.
    /// </summary>
    StartupEntry,

    /// <summary>
    /// Pasta da atualizacao do Windows 11 (a pasta que o Windows 11 usa para
    /// a atualizacao e que costuma ficar grande). Nao e sobra do programa:
    /// e sobra do sistema, e o Tranal pergunta antes de incluir.
    /// </summary>
    WindowsUpdateFolder,
}

public sealed class LeftoverItem
{
    public string Kind { get; init; } = string.Empty;      // "Arquivo" | "Pasta" | "Registro"
    public string Path { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public bool Selected { get; set; } = true;

    /// <summary>
    /// Confianca da associacao item/programa. Determina se o item entra
    /// marcado para remocao: apenas a confianca alta entra marcada.
    /// </summary>
    public LeftoverConfidence Confidence { get; init; } = LeftoverConfidence.High;

    /// <summary>
    /// O motivo do julgamento. Vai sempre junto com a confianca: um "baixa
    /// confianca" sem explicacao nao diz nada ao usuario.
    /// </summary>
    public LeftoverReason Reason { get; init; } = LeftoverReason.Unknown;

    /// <summary>True quando o item deve ser exibido em negrito na lista de sobras.</summary>
    public bool IsHighConfidence => Confidence == LeftoverConfidence.High;

    /// <summary>
    /// True quando o item pertence a outra conta do Windows, e nao a conta que
    /// esta usando o Tranal agora.
    ///
    /// Itens de outra conta nunca vem marcados por padrao: remover dados de
    /// outra conta e uma acao que precisa ser pedida explicitamente.
    /// </summary>
    public bool OtherAccount { get; init; }

    /// <summary>
    /// Os criterios que produziram a confianca do item, em texto.
    ///
    /// Vem vazio quando o item nao e uma pasta: a confianca de um item de
    /// registro vem do proprio registro, e nao de criterios de pasta.
    /// </summary>
    public IReadOnlyList<string> ConfidenceCriteria { get; init; } = Array.Empty<string>();

    /// <summary>
    /// True quando a pasta pode conter documentos ou dados do usuario.
    ///
    /// E o sinal do aviso "as pastas abaixo podem conter dados do usuario".
    /// </summary>
    public bool MayContainUserData { get; init; }
}

/// <summary>
/// O tipo de aviso sobre as sobras.
/// </summary>
public enum LeftoverWarningKind
{
    /// <summary>
    /// O item nao tem confianca alta: mexer nele pode errar o alvo.
    /// </summary>
    LowConfidence,

    /// <summary>
    /// A pasta pode conter documentos ou dados do usuario.
    /// </summary>
    UserData,
}

/// <summary>
/// Um aviso sobre as sobras, pronto para a tela mostrar.
/// </summary>
/// <param name="Kind">O tipo do aviso.</param>
/// <param name="MessageKey">A chave de idioma do texto do aviso.</param>
/// <param name="Items">Os caminhos que o aviso abrange.</param>
public sealed record LeftoverWarning(
    LeftoverWarningKind Kind,
    string MessageKey,
    IReadOnlyList<string> Items);

/// <summary>
/// Os avisos sobre as sobras: baixa confianca e pasta com dado do usuario.
///
/// A regra mora aqui, em um lugar so: quem mostra o aviso consome esta
/// lista e nunca monta a mensagem por conta propria.
/// </summary>
public static class LeftoverWarnings
{
    /// <summary>A chave de idioma do aviso de baixa confianca.</summary>
    public const string LowConfidenceKey = "Leftovers.Warn.LowConfidence";

    /// <summary>A chave de idioma do aviso de pasta com dado do usuario.</summary>
    public const string UserDataKey = "Leftovers.Warn.UserData";

    /// <summary>
    /// Os avisos das sobras informadas.
    /// </summary>
    /// <param name="leftovers">As sobras varridas.</param>
    public static IReadOnlyList<LeftoverWarning> Build(IEnumerable<LeftoverItem>? leftovers)
    {
        var lista = (leftovers ?? Enumerable.Empty<LeftoverItem>())
            .Where(i => i is not null)
            .ToList();

        var avisos = new List<LeftoverWarning>();

        var baixaConfianca = lista
            .Where(i => !i.IsHighConfidence)
            .Select(i => i.Path)
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (baixaConfianca.Count > 0)
            avisos.Add(new LeftoverWarning(LeftoverWarningKind.LowConfidence, LowConfidenceKey, baixaConfianca));

        var comDados = lista
            .Where(i => i.Kind == "Pasta" && (i.MayContainUserData || UninstallAdvisories.IsUserDataFolder(i.Path)))
            .Select(i => i.Path)
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (comDados.Count > 0)
            avisos.Add(new LeftoverWarning(LeftoverWarningKind.UserData, UserDataKey, comDados));

        return avisos;
    }

    /// <summary>
    /// As sobras sem confianca alta: sao as que precisam de aviso antes de
    /// mexer nelas.
    /// </summary>
    /// <param name="leftovers">As sobras varridas.</param>
    public static IReadOnlyList<LeftoverItem> LowConfidenceItems(IEnumerable<LeftoverItem>? leftovers) =>
        (leftovers ?? Enumerable.Empty<LeftoverItem>())
            .Where(i => i is not null && !i.IsHighConfidence)
            .ToList();

    /// <summary>
    /// As pastas que podem conter dado do usuario.
    /// </summary>
    /// <param name="leftovers">As sobras varridas.</param>
    public static IReadOnlyList<string> UserDataFolders(IEnumerable<LeftoverItem>? leftovers) =>
        (leftovers ?? Enumerable.Empty<LeftoverItem>())
            .Where(i => i is not null && i.Kind == "Pasta"
                && (i.MayContainUserData || UninstallAdvisories.IsUserDataFolder(i.Path)))
            .Select(i => i.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}

/// <summary>Normalizacao canonica de caminhos de sistema.</summary>
public static class PathUtil
{
    /// <summary>
    /// Canonicaliza um caminho de sistema: remove aspas, resolve caminho relativo,
    /// remove barra final e normaliza as barras. "C:\App\" e "C:\App" viram a mesma chave.
    /// </summary>
    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        var value = path.Trim().Trim('"');
        try { value = Path.GetFullPath(value); }
        catch { /* caminho nao normalizavel (ex.: caminho de registro) fica como esta */ }

        if (value.Length > 3) value = value.TrimEnd('\\', '/');
        return value;
    }
}

/// <summary>Casamento de nomes: evita falsos positivos como "Steam" x "Steamworks".</summary>
public static class NameMatch
{
    /// <summary>
    /// Verdadeiro quando <paramref name="variant"/> aparece em <paramref name="candidate"/>
    /// sem ser seguido de uma letra. "Steam" casa em "Steam Tools", mas nao em "Steamworks".
    /// </summary>
    public static bool ContainsWord(string candidate, string variant)
    {
        if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(variant)) return false;

        var index = candidate.IndexOf(variant, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            var end = index + variant.Length;
            if (end >= candidate.Length || !char.IsLetter(candidate[end])) return true;
            index = candidate.IndexOf(variant, index + 1, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    /// <summary>
    /// Verdadeiro quando <paramref name="candidate"/> COMECA com <paramref name="variant"/>
    /// e o proximo caractere nao e uma letra. Usado para nomes de pasta, que seguem
    /// o padrao "&lt;Programa&gt; &lt;Complemento&gt;".
    /// </summary>
    public static bool StartsWithWord(string candidate, string variant)
    {
        if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(variant)) return false;
        if (!candidate.StartsWith(variant, StringComparison.OrdinalIgnoreCase)) return false;

        var end = variant.Length;
        if (end >= candidate.Length) return true;
        return !char.IsLetter(candidate[end]);
    }
}

/// <summary>
/// Regras de seguranca aplicadas ANTES de qualquer remocao.
/// Nenhuma remocao pode tocar chaves de registro ou pastas de sistema.
/// </summary>
public static class SafetyRules
{
    /// <summary>Nomes de chave de registro que nunca podem ser removidos.</summary>
    private static readonly HashSet<string> ProtectedRegistryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft", "Classes", "Wow6432Node", "Policies", "ODBC", "MauiFeeds",

        // Raiz dos hives criticos: um "sistema" chamado assim jamais pode ser
        // apagado por varredura de sobras (HKLM\SYSTEM, HKLM\SAM, HKLM\SECURITY...).
        "System", "SAM", "SECURITY", "SOFTWARE", "HARDWARE", "DEFAULT",
    };

    /// <summary>Nomes de pasta que nunca podem ser removidos (comparados com o ULTIMO segmento).</summary>
    private static readonly HashSet<string> ProtectedLeafNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "windows", "system32", "syswow64", "drivers", "boot", "recovery", "winsxs", "servicing",
        "windowsapps", "packages", "common files", "program files", "program files (x86)",
        "users", "$recycle.bin", "system volume information", "microsoft",
    };

    /// <summary>
    /// Verdadeiro quando a chave de registro e estrutural demais para ser removida.
    /// </summary>
    public static bool IsProtectedRegistryKey(string registryPath)
    {
        if (string.IsNullOrWhiteSpace(registryPath)) return true;

        var parts = registryPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return true;

        var name = parts[^1];
        if (ProtectedRegistryNames.Contains(name)) return true;

        // Uma chave com muitos filhos diretos e uma raiz compartilhada de um fornecedor
        // (ex.: "SOFTWARE\JavaSoft"): removeria programas nao relacionados.
        try
        {
            var (hive, sub) = SplitRegistryPath(registryPath);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(sub);
            if (key is not null && key.GetSubKeyNames().Length > 40) return true;
        }
        catch { /* chave inacessivel: nao e motivo para bloquear, o caso raro e seguro */ }

        return false;
    }

    /// <summary>
    /// Subpastas do Windows liberadas para limpeza: sao areas temporarias por
    /// definicao (cache de atualizacao, logs, prefetch) e e exatamente o que as
    /// ferramentas "Junk Cleaner" e "Evidence Remover" precisam apagar.
    /// Os caminhos sao RELATIVOS a pasta do Windows (Environment.SpecialFolder.Windows).
    /// A propria pasta "Windows" continua protegida — so as subpastas listadas
    /// sao liberadas.
    /// </summary>
    private static readonly string[] AllowedWindowsSubfolders =
    {
        @"Temp",
        @"Prefetch",
        @"Logs",
        @"Minidump",
        @"LiveKernelReports",
        @"Downloaded Program Files",
        @"SoftwareDistribution\Download",
        @"System32\winevt\Logs",
        @"Panther\Logs",
    };

    /// <summary>
    /// Verdadeiro quando a pasta e de sistema e nao pode ser removida.
    /// </summary>
    public static bool IsProtectedFolder(string folderPath)
    {
        var full = PathUtil.Normalize(folderPath);
        if (full.Length == 0) return true;

        // Nunca a raiz de uma unidade.
        try
        {
            var root = Path.GetPathRoot(full);
            if (!string.IsNullOrEmpty(root) && full.TrimEnd('\\').Length <= root.TrimEnd('\\').Length)
                return true;
        }
        catch { return true; }

        // Subpastas liberadas do Windows: verificadas ANTES do bloqueio geral,
        // senao a limpeza de cache do Windows Update seria sempre barrada.
        if (IsAllowedWindowsSubfolder(full)) return false;

        var windows = PathUtil.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        if (windows.Length > 0 && full.StartsWith(windows + "\\", StringComparison.OrdinalIgnoreCase))
            return true;

        // Os containers do sistema sao protegidos pelo caminho EXATO: o que
        // esta dentro deles pode e deve ser removido (e e o que a
        // desinstalacao de programas precisa fazer), mas o container em si
        // jamais pode ser apagado.
        foreach (var container in ProtectedFolderPaths)
        {
            if (full.Equals(container, StringComparison.OrdinalIgnoreCase)) return true;
        }

        var leaf = Path.GetFileName(full);
        return ProtectedLeafNames.Contains(leaf);
    }

    /// <summary>
    /// Containers do sistema que nunca podem ser removidos (o caminho exato).
    /// </summary>
    private static readonly string[] ProtectedFolderPaths =
    {
        @"C:\Users",
        @"C:\Users\Public",
        @"C:\ProgramData",
        @"C:\Program Files",
        @"C:\Program Files (x86)",
        @"D:\Users",
        @"D:\Users\Public",
        @"D:\ProgramData",
        @"D:\Program Files",
        @"D:\Program Files (x86)",
    };

    /// <summary>True quando a pasta esta dentro de uma das subpastas liberadas do Windows.</summary>
    private static bool IsAllowedWindowsSubfolder(string full)
    {
        var windows = PathUtil.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        if (windows.Length == 0) return false;

        foreach (var relative in AllowedWindowsSubfolders)
        {
            var candidate = PathUtil.Normalize(Path.Combine(windows, relative));
            if (candidate.Length == 0) continue;

            // A propria subpasta liberada conta (ex.: "C:\Windows\Temp"),
            #pragma warning disable CA1307 // StartsWith(string, StringComparison) exige cultura explicita
            if (string.Equals(full, candidate, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(candidate + "\\", StringComparison.OrdinalIgnoreCase))
                return true;
            #pragma warning restore CA1307
        }

        return false;
    }

    /// <summary>
    /// Separa um caminho canonico de registro em (hive, subcaminho).
    ///
    /// Todos os hives canonicos sao reconhecidos. Um prefixo desconhecido
    /// <b>nao</b> e assumido como HKEY_LOCAL_MACHINE: assumption silenciosa
    /// faria uma restauracao gravar no hive errado.
    /// </summary>
    internal static (RegistryHive Hive, string SubPath) SplitRegistryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return (RegistryHive.LocalMachine, string.Empty);

        var trimmed = path.TrimStart('\\');

        if (trimmed.StartsWith(@"HKEY_CURRENT_USER\", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals(@"HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
            return (RegistryHive.CurrentUser, trimmed[@"HKEY_CURRENT_USER".Length..].TrimStart('\\'));

        if (trimmed.StartsWith(@"HKEY_CLASSES_ROOT\", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals(@"HKEY_CLASSES_ROOT", StringComparison.OrdinalIgnoreCase))
            return (RegistryHive.ClassesRoot, trimmed[@"HKEY_CLASSES_ROOT".Length..].TrimStart('\\'));

        if (trimmed.StartsWith(@"HKEY_LOCAL_MACHINE\", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals(@"HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
            return (RegistryHive.LocalMachine, trimmed[@"HKEY_LOCAL_MACHINE".Length..].TrimStart('\\'));

        if (trimmed.StartsWith(@"HKEY_USERS\", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals(@"HKEY_USERS", StringComparison.OrdinalIgnoreCase))
            return (RegistryHive.Users, trimmed[@"HKEY_USERS".Length..].TrimStart('\\'));

        if (trimmed.StartsWith(@"HKEY_CURRENT_CONFIG\", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals(@"HKEY_CURRENT_CONFIG", StringComparison.OrdinalIgnoreCase))
            return (RegistryHive.CurrentConfig, trimmed[@"HKEY_CURRENT_CONFIG".Length..].TrimStart('\\'));

        // Prefixo desconhecido: devolve um subcaminho vazio e um hive que nao
        // existe de fato, para que a operacao falhe em vez de escrever no
        // lugar errado.
        return (RegistryHive.LocalMachine, string.Empty);
    }

    /// <summary>Nome canonico da raiz de registro, como exibido pelo regedit.</summary>
    public static string HiveName(RegistryHive hive) => hive switch
    {
        RegistryHive.LocalMachine => "HKEY_LOCAL_MACHINE",
        RegistryHive.CurrentUser => "HKEY_CURRENT_USER",
        RegistryHive.ClassesRoot => "HKEY_CLASSES_ROOT",
        RegistryHive.Users => "HKEY_USERS",
        RegistryHive.CurrentConfig => "HKEY_CURRENT_CONFIG",
        _ => "HKEY_LOCAL_MACHINE",
    };
}

/// <summary>Varre o sistema por restos de um programa removido.</summary>
public static class LeftoverScanner
{
    private static readonly string[] UninstallRoots =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    };

    private static readonly string[] NameRoots =
    {
        @"SOFTWARE",
        @"SOFTWARE\WOW6432Node",
    };

    private static readonly string[] FolderRoots =
    {
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Microsoft\Windows",
    };

    /// <summary>
    /// Variantes do nome do programa usadas para casar chaves e pastas.
    /// NUNCA inclui a editora: publishers como "Microsoft Corporation" casariam
    /// milhares de chaves alheias e a remocao massiva seria insegura.
    /// </summary>
    public static IReadOnlyList<string> NameVariants(InstalledProgram program)
    {
        var variants = new List<string>();
        var full = program.DisplayName.Trim();
        if (full.Length >= 3) variants.Add(full);

        // "Notepad++ (64-bit x64)" -> "Notepad++"; "DBeaver 26.2.0" -> "DBeaver"
        var trimmed = full;
        var paren = trimmed.IndexOf('(');
        if (paren > 0) trimmed = trimmed[..paren].Trim();
        var versioned = System.Text.RegularExpressions.Regex
            .Replace(trimmed, @"\s+[\d][\d\.\-]*$", "").Trim();
        if (versioned.Length >= 3 && !variants.Contains(versioned)) variants.Add(versioned);
        if (trimmed.Length >= 3 && !variants.Contains(trimmed)) variants.Add(trimmed);

        // Nomes muito genericos nao sao usados para casar (evita falsos positivos).
        var generic = new[] { "app", "tool", "setup", "installer", "program", "software", "update" };
        return variants
            .Where(v => v.Length >= 4 && !generic.Contains(v.ToLowerInvariant()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Varre o sistema por restos de um programa removido.
    ///
    /// O <paramref name="installLog"/> e o registro do instalador, capturado
    /// ANTES do desinstalador nativo: e ele que permite dizer que um item
    /// e sobra de verdade, e nao apenas uma pasta cujo nome se parece com o
    /// do programa. Sem o log, a varredura continua funcionando pelo nome.
    /// </summary>
    public static List<LeftoverItem> Scan(
        InstalledProgram program, LeftoverScanOptions options, MsiInstallLog? installLog = null)
    {
        var items = new List<LeftoverItem>();

        // Modo "Somente": o desinstalador nativo faz o trabalho e nada mais
        // e procurado. Nenhuma sobra e reportada.
        if (!UninstallModes.Scans(options.Mode)) return items;

        // O motor de regras (o arquivo de regras e a base por aplicativo) e
        // consultado AQUI: e o unico lugar que decide o que fazer com um
        // programa. "Exclude" nao varre nada; "Protect" varre, mas nada
        // fica marcado para remocao.
        var decisao = Rules.RuleEngine.Current.Decide(program);
        if (decisao == Rules.RuleDecision.Exclude) return items;
        var protegido = decisao == Rules.RuleDecision.Protect;

        var all = NameVariants(program);
        // Safe usa apenas o nome completo; Moderate e Advanced ampliam.
        var variants = options.Mode switch
        {
            UninstallMode.Safe => all.Take(1).ToList(),
            UninstallMode.Moderate => all.Take(2).ToList(),
            _ => all,
        };

        foreach (var variant in variants)
            ScanRegistry(variant, options, items);

        foreach (var variant in variants)
            ScanFolders(variant, options, items);

        // Atribuicao pela cadeia de desinstalacao: o proprio instalador do
        // programa registrou o que gravou, e o item continua no sistema
        // depois que o desinstalador nativo rodou.
        //
        // Esta fonte e mais forte que o casamento por nome — e por isso entra
        // depois, para que a deduplicacao prefira o registro do instalador
        // quando o mesmo item aparece pelas duas vias.
        if (installLog is not null)
        {
            foreach (var item in MsiInstallLogReader.FindLeftovers(installLog))
                items.Add(item);
        }

        // A propria pasta de instalacao e sempre candidata: o nome dela nem sempre
        // contem o nome do programa (ex.: "VS Revo Group" para "Revo Uninstaller Pro").
        foreach (var folder in CandidateInstallFolders(program))
        {
            try
            {
                if (!Directory.Exists(folder)) continue;

                // Pasta de sistema nunca entra como "alta confianca, marcada":
                // a remocao seria barrada depois, e a lista prometeria apagar
                // uma pasta que o proprio Tranal protege.
                if (SafetyRules.IsProtectedFolder(PathUtil.Normalize(folder))) continue;

                items.Add(new LeftoverItem
                {
                    Kind = "Pasta",
                    Path = PathUtil.Normalize(folder),
                    Detail = "Pasta de instalação",
                    Confidence = LeftoverConfidence.High,
                    Reason = LeftoverReason.InstallFolder,
                    Selected = true,
                });
            }
            catch { }
        }

        // Entradas de inicializacao do Windows que apontam para o programa.
        // A confianca vem do registro de inicializacao, e nao do nome: e o
        // programa que gravou a entrada que sobe com o Windows.
        foreach (var startup in Discovery.StartupEntries.For(program))
        {
            items.Add(new LeftoverItem
            {
                Kind = "Registro",
                Path = $"{startup.Location}\\{startup.Name}",
                Detail = startup.Command,
                Confidence = LeftoverConfidence.High,
                Reason = LeftoverReason.StartupEntry,
                Selected = true,
            });
        }

        // Deduplicacao canonica: a mesma pasta/registro descoberta por caminhos
        // diferentes (ex.: "C:\App" e "C:\App\") aparece uma unica vez.
        var result = Deduplicate(items);

        // Opcao "marcar todos os itens restantes por padrao" do Revo: a
        // marcacao inicial passa a incluir a confianca media e baixa. A
        // confianca de cada item continua visivel na lista.
        //
        // Os itens de outra conta do Windows nao sao marcados por essa opcao:
        // "marcar tudo" nao pode ser lido como "remover tambem os dados dos
        // outros usuarios da maquina".
        if (options.CheckAllLeftovers)
        {
            foreach (var item in result)
            {
                if (item.OtherAccount && !options.SelectOtherAccountItems) continue;
                item.Selected = true;
            }
        }

        // Itens protegidos pela lista de exclusoes nao aparecem: nem para o
        // usuario duvidar deles, nem para ele marcar e remover.
        var filtrados = ExcludeList.Current.ApplyTo(result);

        // O motor de regras (o arquivo de regras e a base por aplicativo) e
        // a ultima palavra: um programa protegido aparece na lista, mas
        // nada dele fica marcado para remocao.
        if (protegido)
        {
            foreach (var item in filtrados) item.Selected = false;
        }

        return filtrados;
    }

    /// <summary>
    /// Varredura forçada a partir de um caminho (arquivo ou pasta), sem depender
    /// de chave de desinstalacao no registro. E o "Forced Uninstall" do Revo:
    /// o usuario informa o executavel ou a pasta e o Tranal procura tudo que
    /// estiver relacionado ao nome informado.
    /// </summary>
    public static List<LeftoverItem> ScanForced(
        string path, LeftoverScanOptions options)
    {
        var full = PathUtil.Normalize(path);
        if (full.Length == 0) return new List<LeftoverItem>();

        // No modo "Somente" nao ha varredura alguma — apenas o alvo informado.
        if (!UninstallModes.Scans(options.Mode)) return new List<LeftoverItem>();

        string name;
        string installLocation;

        try
        {
            if (Directory.Exists(full))
            {
                name = Path.GetFileName(full.TrimEnd('\\'));
                installLocation = full;
            }
            else
            {
                name = Path.GetFileNameWithoutExtension(full);

                // A pasta que contem o arquivo apontado NAO e a pasta de
                // instalacao do programa — e inferi-la seria perigoso: a
                // desinstalacao forcada de "%LOCALAPPDATA%\Temp\app.exe"
                // removeria a pasta Temp inteira, com tudo o que o usuario
                // tem la dentro. O alvo apontado e o unico item garantido;
                // a pasta aparece na lista pela varredura, sem confianca
                // suficiente para ser removida sozinha.
                installLocation = string.Empty;
            }
        }
        catch { return new List<LeftoverItem>(); }

        if (name.Length < 3) name = Path.GetFileName(full);

        var synthetic = new InstalledProgram
        {
            DisplayName = name,
            InstallLocation = installLocation,
        };

        // O proprio alvo informado entra PRIMEIRO na lista: e o item que o
        // usuario mandou remover, e ele vem marcado. Entrando depois, a
        // deduplicacao preferiria a ocorrencia fraca da varredura — a mesma
        // pasta achada pelo nome, com confianca baixa — e a desinstalacao
        // forcada terminaria sem remover o alvo. (A protecao contra pastas de
        // sistema continua valendo em UninstallEngine.RemoveLeftovers.)
        var items = new List<LeftoverItem>();

        try
        {
            if (Directory.Exists(full))
            {
                items.Add(new LeftoverItem
                {
                    Kind = "Pasta",
                    Path = full,
                    Detail = "Alvo informado",
                    Confidence = LeftoverConfidence.High,
                    Reason = LeftoverReason.ExplicitTarget,
                    Selected = true,
                });
            }
            else if (File.Exists(full))
            {
                items.Add(new LeftoverItem
                {
                    Kind = "Arquivo",
                    Path = full,
                    Detail = "Alvo informado",
                    Confidence = LeftoverConfidence.High,
                    Reason = LeftoverReason.ExplicitTarget,
                    Selected = true,
                });
            }
        }
        catch { }

        items.AddRange(Scan(synthetic, options));

        var result = Deduplicate(items);

        // O alvo informado ja entra marcado; a opcao "marcar todos" garante
        // que os itens achados pelo nome tambem venham marcados.
        if (options.CheckAllLeftovers)
        {
            foreach (var item in result)
            {
                if (item.OtherAccount && !options.SelectOtherAccountItems) continue;
                item.Selected = true;
            }
        }

        // Um alvo que esteja na lista de exclusoes nao aparece e nao pode ser
        // removido: a protecao vale mesmo quando o caminho foi digitado a mao.
        return ExcludeList.Current.ApplyTo(result);
    }

    /// <summary>
    /// Deduplica por tipo + caminho canonico.
    ///
    /// O mesmo item pode ser descoberto por caminhos diferentes: a pasta de
    /// instalacao tambem casa com o nome do programa, e o alvo informado de
    /// uma desinstalacao forcada tambem aparece na varredura pelo nome.
    /// Vale a ocorrencia mais forte — a marcada e de alta confianca.
    ///
    /// Sem esta regra a desinstalacao forcada do proprio alvo terminava sem
    /// remover nada: a lista devolvia a ocorrencia fraca (achada pelo nome,
    /// desmarcada) e descartava a forte, que e a que o usuario mandou
    /// remover.
    /// </summary>
    private static List<LeftoverItem> Deduplicate(IEnumerable<LeftoverItem> items) =>
        items
            .Where(i => !string.IsNullOrWhiteSpace(i.Path))
            .GroupBy(i => i.Kind + "|" + PathUtil.Normalize(i.Path), StringComparer.OrdinalIgnoreCase)
            .Select(g => g
                .OrderByDescending(i => i.Selected)
                .ThenBy(i => i.Confidence)
                .First())
            .ToList();

    /// <summary>Pastas candidatas: a registrada e a pasta do executavel de desinstalacao.</summary>
    private static IEnumerable<string> CandidateInstallFolders(InstalledProgram program)    {
        var result = new List<string>();
        if (!string.IsNullOrWhiteSpace(program.InstallLocation))
            result.Add(program.InstallLocation.Trim().Trim('"'));

        var exe = ProgramScanner.ExtractExecutablePath(program.UninstallString);
        if (exe.Length > 0)
        {
            try
            {
                var dir = Path.GetDirectoryName(exe);
                if (!string.IsNullOrWhiteSpace(dir)) result.Add(dir);
            }
            catch { }
        }

        return result
            .Select(PathUtil.Normalize)
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Uma arvore de registro a varrer.
    ///
    /// As contas do Windows entram como alvos proprios: as chaves de um
    /// usuario que nao e o atual nao ficam em HKEY_CURRENT_USER, e sim em
    /// HKEY_USERS com o SID da conta.
    /// </summary>
    private readonly record struct RegistryTarget(
        RegistryHive Hive, RegistryView View, string Sid, bool OtherAccount)
    {
        /// <summary>Caminho do subcaminho dentro do hive.</summary>
        public string SubPath(string root) => OtherAccount ? $@"{Sid}\{root}" : root;

        /// <summary>Caminho canonico do item, como exibido pelo regedit.</summary>
        public string CanonicalPath(string root, string name) =>
            OtherAccount
                ? $@"HKEY_USERS\{Sid}\{root}\{name}"
                : CanonicalRegistryPath(Hive, root, name, View);
    }

    /// <summary>
    /// Regra de marcacao: confianca alta vem marcada. Itens de outra conta do
    /// Windows so vem marcados quando o usuario pediu para desinstalar de todas
    /// as contas.
    /// </summary>
    private static bool Selects(
        LeftoverConfidence confidence, LeftoverScanOptions options, bool otherAccount) =>
        confidence == LeftoverConfidence.High && (!otherAccount || options.SelectOtherAccountItems);

    private static void ScanRegistry(string variant, LeftoverScanOptions options, List<LeftoverItem> items)
    {
        var targets = new List<RegistryTarget>
        {
            new(RegistryHive.LocalMachine, RegistryView.Registry64, string.Empty, false),
            new(RegistryHive.LocalMachine, RegistryView.Registry32, string.Empty, false),
            new(RegistryHive.CurrentUser, RegistryView.Registry64, string.Empty, false),
            new(RegistryHive.CurrentUser, RegistryView.Registry32, string.Empty, false),
        };

        // As outras contas do Windows so entram quando a varredura em todas as
        // contas esta ligada — e apenas as contas com o hive carregado, que
        // sao as unicas cujas chaves existem de fato no momento.
        if (options.ScanAllUserAccounts)
        {
            // A regra de "quais contas entram na varredura" mora em um lugar
            // so (AccountLeftoverScanner, que consume UserProfiles). Aqui
            // so se usa a lista.
            foreach (var profile in AccountLeftoverScanner.Accounts(AccountLeftoverScanOptions.Default))
            {
                if (!profile.IsLoaded) continue;
                targets.Add(new RegistryTarget(
                    RegistryHive.Users, RegistryView.Default, profile.Sid, true));
            }
        }

        foreach (var target in targets)
        {
            // 1) Chaves de desinstalacao: o NOME da chave e um GUID, entao o casamento
            //    precisa ser pelo valor DisplayName dentro dela.
            foreach (var root in UninstallRoots)
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(target.Hive, target.View);
                    using var key = baseKey.OpenSubKey(target.SubPath(root));
                    if (key is null) continue;

                    foreach (var name in key.GetSubKeyNames())
                    {
                        using var sub = key.OpenSubKey(name);
                        if (sub is null) continue;
                        var display = sub.GetValue("DisplayName")?.ToString();
                        if (display is null || !NameMatch.ContainsWord(display, variant)) continue;

                        // A chave de desinstalacao e evidencia forte, mas o
                        // nome precisa ser o do programa: "Steam Client"
                        // casa com "Steam" e e um produto diferente. Com
                        // nome exato, a confianca e alta e o item entra
                        // marcado; caso contrario, fica apenas listado.
                        var judgment = Judge(display.Trim(), variant);

                        items.Add(new LeftoverItem
                        {
                            Kind = "Registro",
                            Path = target.CanonicalPath(root, name),
                            Detail = display,
                            Confidence = judgment.Confidence,
                            Reason = judgment.Reason,
                            Selected = Selects(judgment.Confidence, options, target.OtherAccount),
                            OtherAccount = target.OtherAccount,
                        });
                    }
                }
                catch { }
            }
        }

        // 2) Demais arvores: casamento pelo NOME da chave (evita varrer todo valor).
        //    No nivel Advanced tambem se olha o segundo nivel, onde ficam as chaves
        //    do proprio programa (ex.: SOFTWARE\VS Revo Group\Revo Uninstaller Pro).
        foreach (var target in targets)
        {
            foreach (var root in NameRoots)
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(target.Hive, target.View);
                    using var key = baseKey.OpenSubKey(target.SubPath(root));
                    if (key is null) continue;

                    foreach (var name in key.GetSubKeyNames())
                    {
                        if (NameMatch.ContainsWord(name, variant))
                            AddRegistryItem(target, root, name, items, name,
                                Judge(name, variant), options);

                        if (options.Mode != UninstallMode.Advanced) continue;

                        try
                        {
                            using var vendor = key.OpenSubKey(name);
                            if (vendor is null) continue;

                            foreach (var child in vendor.GetSubKeyNames())
                            {
                                if (!NameMatch.ContainsWord(child, variant)) continue;
                                AddRegistryItem(target, root, $@"{name}\{child}", items, child,
                                    new LeftoverJudgment(LeftoverConfidence.Low, LeftoverReason.RegistrySecondLevel), options);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// O julgamento de uma sobra: a confianca e o motivo, sempre juntos.
    /// </summary>
    internal readonly record struct LeftoverJudgment(LeftoverConfidence Confidence, LeftoverReason Reason);

    /// <summary>
    /// Julga um item cujo nome casa com uma variante do programa.
    ///
    /// Nome exatamente igual e alta confianca; casamento parcial e media.
    /// O motivo acompanha sempre a confianca, porque um "baixa confianca"
    /// sem explicacao nao diz nada ao usuario.
    /// </summary>
    internal static LeftoverJudgment Judge(string name, string variant) =>
        string.Equals(name, variant, StringComparison.OrdinalIgnoreCase)
            ? new LeftoverJudgment(LeftoverConfidence.High, LeftoverReason.ExactNameMatch)
            : new LeftoverJudgment(LeftoverConfidence.Medium, LeftoverReason.VariantNameMatch);

    private static void AddRegistryItem(RegistryTarget target, string root, string relativePath,
        List<LeftoverItem> items, string detail, LeftoverJudgment judgment,
        LeftoverScanOptions options)
    {
        var path = target.CanonicalPath(root, relativePath);
        if (SafetyRules.IsProtectedRegistryKey(path)) return;

        items.Add(new LeftoverItem
        {
            Kind = "Registro",
            Path = path,
            Detail = detail,
            Confidence = judgment.Confidence,
            Reason = judgment.Reason,
            Selected = Selects(judgment.Confidence, options, target.OtherAccount),
            OtherAccount = target.OtherAccount,
        });
    }

    /// <summary>
    /// Converte o caminho encontrado na visao de registro em um caminho canonico
    /// (o mesmo exibido pelo regedit). Sem isso, uma chave vista na visao de 32 bits
    /// seria removida da visao de 64 bits — ou seja, a chave ERRADA.
    /// </summary>
    internal static string CanonicalRegistryPath(RegistryHive hive, string root, string name, RegistryView view)
    {
        // O redirecionador WOW64 so existe sob HKLM\SOFTWARE: HKCU\Software e compartilhado.
        if (view == RegistryView.Registry32
            && hive == RegistryHive.LocalMachine
            && Environment.Is64BitOperatingSystem
            && root.StartsWith(@"SOFTWARE\", StringComparison.OrdinalIgnoreCase)
            && !root.StartsWith(@"SOFTWARE\WOW6432Node", StringComparison.OrdinalIgnoreCase))
        {
            root = @"SOFTWARE\WOW6432Node\" + root[@"SOFTWARE\".Length..];
        }

        return $@"{SafetyRules.HiveName(hive)}\{root}\{name}";
    }

    private static void ScanFolders(string variant, LeftoverScanOptions options, List<LeftoverItem> items)
    {
        // Cada raiz sabe de qual conta ela vem: as pastas das outras contas
        // do Windows nao podem receber a mesma marcacao das pastas da conta
        // atual.
        var roots = new List<(string Path, bool OtherAccount)>();

        var baseRoots = options.Mode == UninstallMode.Advanced
            ? FolderRoots
            : FolderRoots.Take(4).ToArray();

        foreach (var root in baseRoots)
            roots.Add((root, false));

        // As pastas das outras contas do Windows: o AppData de cada perfil.
        if (options.ScanAllUserAccounts)
        {
            // Mesma regra do registro: a lista de contas vem de um lugar so.
            foreach (var profile in AccountLeftoverScanner.Accounts(AccountLeftoverScanOptions.Default))
            {
                if (string.IsNullOrWhiteSpace(profile.LocalPath)) continue;

                roots.Add((Path.Combine(profile.LocalPath, "AppData", "Local"), true));
                roots.Add((Path.Combine(profile.LocalPath, "AppData", "Roaming"), true));
                roots.Add((Path.Combine(profile.LocalPath, "AppData", "LocalLow"), true));
            }
        }

        foreach (var (root, otherAccount) in roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(dir);
                    if (NameMatch.StartsWithWord(name, variant))
                    {
                        AddFolderItem(dir, items, Judge(name, variant), options, otherAccount, variant);
                        continue;
                    }

                    // No modo Advanced o segundo nivel guarda a pasta real do programa
                    // (ex.: %LOCALAPPDATA%\VS Revo Group\Revo Uninstaller Pro).
                    if (options.Mode != UninstallMode.Advanced) continue;

                    try
                    {
                        foreach (var sub in Directory.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly))
                        {
                            if (NameMatch.StartsWithWord(Path.GetFileName(sub), variant))
                                AddFolderItem(sub, items,
                                    new LeftoverJudgment(LeftoverConfidence.Low, LeftoverReason.SubfolderMatch),
                                    options, otherAccount, variant);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }

    private static void AddFolderItem(string dir, List<LeftoverItem> items, LeftoverJudgment judgment,
        LeftoverScanOptions options, bool otherAccount, string? programName = null)
    {
        var full = PathUtil.Normalize(dir);
        if (SafetyRules.IsProtectedFolder(full)) return;

        // A confianca da pasta vem dos criterios reais (vazia, executaveis,
        // editor reconhecido, dado do usuario...), e nao so do nome.
        var evaluation = FolderConfidenceEvaluator.Evaluate(full, programName);
        var confidence = judgment.Confidence;

        if (evaluation.Usable)
        {
            var byCriteria = evaluation.Level switch
            {
                FolderConfidenceLevel.High => LeftoverConfidence.High,
                FolderConfidenceLevel.Medium => LeftoverConfidence.Medium,
                _ => LeftoverConfidence.Low,
            };

            // Os criterios podem REBAIXAR a confianca (a pasta tem dado do
            // usuario, nao tem executavel, nao tem relacao com o programa),
            // mas nao podem inventar uma ligacao que o nome nao fez: a
            // confianca nunca passa da do casamento de nome.
            if (byCriteria > confidence) confidence = byCriteria;
        }

        items.Add(new LeftoverItem
        {
            Kind = "Pasta",
            Path = full,
            Detail = $"{ProgramScanner.MeasureFolder(dir) / 1024 / 1024} MB",
            Confidence = confidence,
            Reason = judgment.Reason,
            Selected = Selects(confidence, options, otherAccount),
            OtherAccount = otherAccount,
            ConfidenceCriteria = evaluation.Descriptions,
            MayContainUserData = evaluation.MayContainUserData,
        });
    }
}

/// <summary>Backup e restauracao de chaves de registro no formato do Revo.</summary>
public static class RegistryBackup
{
    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Tranal Group", "Tranal Uninstaller Pro");

    /// <summary>
    /// Cria o backup das chaves indicadas e devolve o caminho do arquivo .reg.
    /// O arquivo gerado e importavel por "reg import" (formato .reg UTF-16 com BOM).
    /// </summary>
    public static string Backup(IEnumerable<string> registryPaths, string? destination = null)
    {
        var dir = Path.Combine(DataDirectory, "RegBackup", "Last");
        Directory.CreateDirectory(dir);
        var file = destination ?? Path.Combine(dir, "TranalRegistryBackup.reg");

        var sb = new StringBuilder();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();

        var exported = new List<string>();
        foreach (var path in registryPaths)
        {
            if (TryExport(path, sb)) exported.Add(path);
        }

        File.WriteAllText(file, sb.ToString(), Encoding.Unicode);

        // Script de restauracao: importa o .reg gerado. Equivalente ao Restore.dat do Revo.
        var restore = Path.Combine(dir, "Restore.dat");
        File.WriteAllText(restore, BuildRestoreScript(file, exported), Encoding.ASCII);

        return file;
    }

    private static string BuildRestoreScript(string regFile, IEnumerable<string> exportedPaths)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("echo Restaurando o backup do Registro do Tranal Uninstaller...");
        sb.AppendLine($"reg import \"{regFile}\"");
        sb.AppendLine("echo Concluido.");
        return sb.ToString();
    }

    private static bool TryExport(string path, StringBuilder sb)
    {
        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(path);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(sub);
            if (key is null) return false;

            sb.AppendLine($"[{path}]");
            foreach (var valueName in key.GetValueNames())
            {
                var value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (value is null) continue;

                // "@" e o nome reservado do valor padrao (sem nome) no formato .reg.
                var label = valueName.Length == 0 ? "@" : valueName;
                sb.AppendLine($"\"{label}\"={FormatValue(key.GetValueKind(valueName), value)}");
            }
            sb.AppendLine();

            foreach (var child in key.GetSubKeyNames())
                TryExport($"{path}\\{child}", sb);

            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// O valor no formato do Editor do Registro.
    ///
    /// A formatacao mora em <c>Core/Reporting/ReportFormats</c> — o mesmo
    /// lugar que formata o .reg exportado pelo painel de relatorios. Aqui so
    /// mora a traducao do valor do registro para o dado que esse formatador
    /// espera.
    ///
    /// O **tipo** vem do registro (<c>GetValueKind</c>), e nao do tipo do
    /// objeto lido: um valor binario chega como <c>byte[]</c> e e gravado
    /// como <c>hex:</c> (binario). Antes, pelo tipo do objeto, ele saia
    /// como <c>hex(2):</c> — que e texto expansivel — e o valor binario
    /// voltava do backup como texto.
    /// </summary>
    private static string FormatValue(RegistryValueKind kind, object value) => kind switch
    {
        RegistryValueKind.DWord => Core.Reporting.ReportFormats.FormatRegValue(
            kind, Convert.ToUInt32(value).ToString(CultureInfo.InvariantCulture)),
        RegistryValueKind.QWord => Core.Reporting.ReportFormats.FormatRegValue(
            kind, Convert.ToUInt64(value).ToString(CultureInfo.InvariantCulture)),
        RegistryValueKind.Binary => Core.Reporting.ReportFormats.FormatRegValue(kind, HexOf(value as byte[])),
        RegistryValueKind.MultiString => Core.Reporting.ReportFormats.FormatRegValue(
            kind, string.Join("\0", value as string[] ?? Array.Empty<string>())),
        _ => Core.Reporting.ReportFormats.FormatRegValue(kind, value.ToString() ?? string.Empty),
    };

    /// <summary>Os bytes de um valor binario, em hexadecimal.</summary>
    private static string HexOf(byte[]? bytes) =>
        bytes is null
            ? string.Empty
            : string.Join(",", bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
}

/// <summary>Execucao do desinstalador nativo e criacao de ponto de restauracao.</summary>
public static class UninstallEngine
{
    /// <summary>Separa um comando de desinstalacao em executavel e argumentos.</summary>
    public static (string Exe, string Arguments) SplitCommand(string command)
    {
        var text = command.Trim();
        if (text.Length == 0) return (string.Empty, string.Empty);

        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            if (end > 0)
                return (text[1..end], text[(end + 1)..].Trim());
        }

        // Sem aspas: primeiro token e o executavel, o resto sao argumentos.
        // Cuida de casos como "C:\Program Files\App\uninst.exe" sem aspas,
        // em que o caminho tem espacos: procura o primeiro .exe/.com/.bat.
        var space = text.IndexOf(' ');
        if (space < 0) return (text, string.Empty);

        var head = text[..space];
        var exeExt = head.LastIndexOf('.');
        if (exeExt < 0) return (head, text[(space + 1)..].Trim());

        var firstArgs = text[(space + 1)..].Trim();
        var candidate = text[..(space + 1 + firstArgs.IndexOf(' ', StringComparison.Ordinal))]
            .TrimEnd();
        if (candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return (candidate, firstArgs);

        return (head, firstArgs);
    }

    /// <summary>
    /// Executa o comando de desinstalacao nativo e aguarda o processo.
    ///
    /// Com <paramref name="silent"/> ligado, usa o comando silencioso
    /// derivado pela classificacao do desinstalador
    /// (<see cref="QuietUninstall"/>) quando existe; sem ele, vale o
    /// comportamento de sempre: o <c>QuietUninstallString</c> do registro
    /// quando houver, e o comando registrado caso contrario.
    /// </summary>
    /// <param name="program">O programa a desinstalar.</param>
    /// <param name="silent">Usar o comando silencioso derivado.</param>
    public static async Task<BuiltInUninstallResult> RunBuiltInUninstaller(
        InstalledProgram program, bool silent = false)
    {
        var command = silent
            ? (program.QuietUninstallCommand.Length > 0
                ? program.QuietUninstallCommand
                : program.UninstallString)
            : (!string.IsNullOrWhiteSpace(program.QuietUninstallString)
                ? program.QuietUninstallString
                : program.UninstallString);

        if (string.IsNullOrWhiteSpace(command))
            return new BuiltInUninstallResult
            {
                ProgramName = program.DisplayName,
                Command = string.Empty,
                Started = false,
                Message = "o programa não possui desinstalador nativo",
            };

        var (exe, arguments) = SplitCommand(command);
        if (exe.Length == 0)
            return new BuiltInUninstallResult
            {
                ProgramName = program.DisplayName,
                Command = command,
                Started = false,
                Message = "comando de desinstalação inválido",
            };

        // Executaveis sem caminho completo (MsiExec.exe) sao resolvidos pelo sistema.
        var fullPath = exe.Contains('\\') ? exe : null;
        if (fullPath is not null && !File.Exists(fullPath))
            return new BuiltInUninstallResult
            {
                ProgramName = program.DisplayName,
                Command = command,
                Started = false,
                Message = $"executável não encontrado: {fullPath}",
            };

        var startInfo = new ProcessStartInfo(exe, arguments) { UseShellExecute = true };
        try
        {
            // Durante a desinstalacao nativa o Windows nao pode desligar nem
            // reiniciar: uma maquina que apaga no meio do processo deixa o
            // programa pela metade.
            using var guard = ShutdownGuard.Acquire();

            using var process = Process.Start(startInfo);
            if (process is null)
                return new BuiltInUninstallResult
                {
                    ProgramName = program.DisplayName,
                    Command = command,
                    Started = false,
                    Message = "o processo não pôde ser criado",
                };

            await process.WaitForExitAsync();
            return new BuiltInUninstallResult
            {
                ProgramName = program.DisplayName,
                Command = command,
                Started = true,
                ExitCode = process.ExitCode,
            };
        }
        catch (Exception ex)
        {
            return new BuiltInUninstallResult
            {
                ProgramName = program.DisplayName,
                Command = command,
                Started = false,
                Message = ex.Message,
            };
        }
    }

    /// <summary>Cria um ponto de restauracao do sistema (best effort, requer admin).</summary>
    public static bool TryCreateRestorePoint(string description)
    {
        try
        {
            var scope = new System.Management.ManagementScope(@"\\.\root\cimv2");
            scope.Connect();
            using var restore = new System.Management.ManagementClass(
                scope, new System.Management.ManagementPath("SystemRestore"), null);

            var parameters = restore.GetMethodParameters("Create");
            parameters["Description"] = description;
            parameters["RestorePointType"] = 12; // MODIFIED_SETTINGS

            var result = restore.InvokeMethod("Create", parameters, null);
            return Convert.ToUInt32(result["ReturnValue"]) == 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// Remove os restos selecionados e devolve o relatorio detalhado.
    /// Itens bloqueados pelas regras de seguranca sao marcados como Protected,
    /// nunca como remocao tentada.
    /// </summary>
    /// <param name="items">Todos os itens encontrados; apenas os marcados sao removidos.</param>
    /// <param name="options">Opcoes da remocao. Quando nulo, usa <see cref="UninstallRemovalOptions.Default"/>.</param>
    /// <summary>
    /// Cria o backup completo do registro, quando a operacao pede por ele.
    ///
    /// Deve ser chamada ANTES do desinstalador nativo: depois dele, o
    /// desinstalador do proprio programa ja apagou as chaves dele, e o .reg
    /// resultante nao serviria para devolver nada. E o contrato do original:
    /// "Make a System Restore Point and a full Registry backup before
    /// uninstalling" (15162).
    ///
    /// O backup nao depende de haver sobras a remover: no modo "Somente" ele
    /// tambem e criado, porque e o backup do programa que vai ser
    /// desinstalado, nao das sobras.
    /// </summary>
    public static FullRegistryBackupResult? PrepareFullRegistryBackup(UninstallRemovalOptions options) =>
        options.FullRegistryBackup ? FullRegistryBackup.Create() : null;

    /// <summary>
    /// Remove as sobras marcadas.
    ///
    /// O backup completo do registro NAO e criado aqui: ele precisa existir
    /// antes do desinstalador nativo, e nao antes da remocao das sobras. Quem
    /// o cria (o assistente e a CLI) passa o resultado em
    /// <paramref name="fullRegistryBackup"/>.
    /// </summary>
    /// <param name="items">Sobras a remover.</param>
    /// <param name="options">Opcoes da operacao.</param>
    /// <param name="fullRegistryBackup">Backup completo ja criado, se houver.</param>
    /// <summary>
    /// Guarda as sobras ANTES de elas serem apagadas.
    ///
    /// O backup completo do registro, que ja existia, cobre o REGISTRO — nao
    /// os arquivos. Sem este backup, a limpeza de uma pasta tira do disco o
    /// conteudo que o usuario nao mais ve: e o que o "Recuperar" do Wise
    /// resolve guardando o conteudo antes de apagar.
    ///
    /// A REGRA (quando guardar) e a preferencia do usuario; a ACAO esta aqui,
    /// e a interface e a linha de comando chamam o mesmo metodo.
    /// </summary>
    /// <param name="items">As sobras que serao removidas.</param>
    /// <param name="programName">O nome do programa (para o backup ser reconhecivel).</param>
    /// <param name="options">As opcoes da operacao.</param>
    public static Recovery.LeftoverBackupResult BackupLeftovers(
        IEnumerable<LeftoverItem> items,
        string? programName,
        UninstallRemovalOptions? options = null)
    {
        var opts = options ?? UninstallRemovalOptions.Default;

        if (!opts.Backup)
            return new Recovery.LeftoverBackupResult { Ok = false, Message = "o backup esta desligado nas preferencias" };

        var lista = items?.Where(i => i.Selected).ToList() ?? new List<LeftoverItem>();
        if (lista.Count == 0)
            return new Recovery.LeftoverBackupResult { Ok = false, Message = "nao ha sobras para guardar" };

        var nome = string.IsNullOrWhiteSpace(programName) ? "sobras" : programName!;
        return Recovery.LeftoverBackup.Backup(lista, nome);
    }

    public static RemovalReport RemoveLeftovers(
        IEnumerable<LeftoverItem> items,
        UninstallRemovalOptions? options = null,
        FullRegistryBackupResult? fullRegistryBackup = null)
    {
        var opts = options ?? UninstallRemovalOptions.Default;
        var selected = items.Where(i => i.Selected).ToList();
        var outcomes = new List<RemovalOutcome>();

        // O backup e feito ANTES de qualquer remocao: e o que permite devolver
        // ao sistema tudo o que for excluido (equivalente ao Gerenciador de
        // Backups do Revo Uninstaller Pro).
        RestorePoint? point = null;
        if (opts.Backup && selected.Count > 0)
        {
            point = CreateRestorePoint(selected, opts);
        }

        // O backup completo do registro foi criado ANTES do desinstalador
        // nativo (e nao aqui): o que chega e o resultado ja pronto.
        var full = fullRegistryBackup;

        // Encerrar os executaveis do programa: um arquivo em uso nao pode ser
        // apagado, e o Revo oferece "Kill the Process and Continue" (15125).
        //
        // A lista de exclusoes protege ate aqui: encerrar o processo de um
        // item protegido seria tocar no item protegido. O item continua na
        // lista e o motor o recusa na remocao — mas o processo nao pode ser
        // encerrado por baixo dos panos.
        if (opts.KillRunningProcesses && selected.Count > 0)
        {
            var killable = selected
                .Where(i => !ExcludeList.Current.Contains(i.Kind, i.Path))
                .ToList();

            outcomes.AddRange(KillRunningProcesses(killable));
        }

        foreach (var item in selected)
        {
            outcomes.Add(RemoveOne(item, opts));
        }

        return new RemovalReport(outcomes)
        {
            RestorePointId = point?.Id,
            FullRegistryBackupPath = full?.Directory ?? string.Empty,
            FullRegistryBackupOk = full?.Success ?? false,
        };
    }

    /// <summary>
    /// True quando a pasta — ou alguma subpasta dela — contém item protegido
    /// pela lista de exclusões.
    ///
    /// Sem esta conferida, excluir um ARQUIVO dentro de uma pasta não
    /// protegeria nada: a remoção da pasta levaria o arquivo junto.
    ///
    /// Atalhos (junction, symlink) não são percorridos: o alvo deles está
    /// fora da pasta removida e não participa do que o usuário mandou
    /// remover.
    /// </summary>
    private static bool HasProtectedDescendant(string folder)
    {
        // A raiz e um atalho: o alvo esta fora da pasta e nao participa do
        // que o usuario mandou remover.
        if (SafeTraversal.IsReparsePoint(folder)) return false;

        foreach (var file in EnumerateFilesSafe(folder))
        {
            if (ExcludeList.Current.ContainsPath(file)) return true;
        }

        foreach (var dir in EnumerateDirectoriesSafe(folder))
        {
            if (ExcludeList.Current.ContainsPath(dir)) return true;
            if (SafeTraversal.IsReparsePoint(dir)) continue;
            if (HasProtectedDescendant(dir)) return true;
        }

        return false;
    }

    /// <summary>
    /// Como um item dentro de uma pasta removida item a item terminou.
    /// </summary>
    private enum ItemRemoval
    {
        /// <summary>O item saiu do disco.</summary>
        Removed,

        /// <summary>O item está em uso e foi agendado para o próximo reinício.</summary>
        Deferred,

        /// <summary>O item não pôde ser removido agora.</summary>
        Failed,
    }

    /// <summary>
    /// Resumo do que a remoção granular do conteúdo de uma pasta conseguiu.
    /// </summary>
    private readonly record struct FolderContent(int Removed, int Protected, int Failed, int Deferred);

    /// <summary>
    /// Remove o conteúdo de uma pasta item a item, preservando o que a lista
    /// de exclusões protege.
    ///
    /// A pasta em si só é removida quando ela fica vazia: havendo conteúdo
    /// protegido (ou item que não pode ser apagado agora), ela permanece.
    ///
    /// Atalhos (junction, symlink) nunca são atravessados: o alvo está fora
    /// da pasta removida, e apagar arquivo alheio seria perda de dados não
    /// pedida. O link é removido como link — só ele.
    /// </summary>
    private static FolderContent RemoveFolderContent(string folder, UninstallRemovalOptions options)
    {
        // A raiz e um atalho: o conteudo do alvo nao e da pasta, e nao se
        // toca nele.
        if (SafeTraversal.IsReparsePoint(folder)) return new FolderContent(0, 0, 0, 0);

        var removed = 0;
        var protectedKept = 0;
        var failed = 0;
        var deferred = 0;

        foreach (var file in EnumerateFilesSafe(folder))
        {
            if (ExcludeList.Current.ContainsPath(file))
            {
                protectedKept++;
                continue;
            }

            switch (RemoveFileSafely(file, options))
            {
                case ItemRemoval.Removed: removed++; break;
                case ItemRemoval.Deferred: deferred++; break;
                default: failed++; break;
            }
        }

        foreach (var dir in EnumerateDirectoriesSafe(folder))
        {
            if (ExcludeList.Current.ContainsPath(dir))
            {
                protectedKept++;
                continue;
            }

            // Atalho: remove-se o link, nunca o alvo.
            if (SafeTraversal.IsReparsePoint(dir))
            {
                switch (RemoveLinkSafely(dir, options))
                {
                    case ItemRemoval.Removed: removed++; break;
                    case ItemRemoval.Deferred: deferred++; break;
                    default: failed++; break;
                }
                continue;
            }

            var sub = RemoveFolderContent(dir, options);
            removed += sub.Removed;
            protectedKept += sub.Protected;
            failed += sub.Failed;
            deferred += sub.Deferred;
        }

        // A pasta em si só sai quando não sobrou nada dentro dela.
        if (protectedKept == 0 && failed == 0 && deferred == 0)
        {
            try { Directory.Delete(folder, recursive: false); }
            catch { /* a pasta não ficou vazia: ela permanece */ }
        }

        return new FolderContent(removed, protectedKept, failed, deferred);
    }

    /// <summary>
    /// Remove um arquivo de dentro de uma pasta, enviando-o para a Lixeira
    /// quando o usuário pediu isso.
    /// </summary>
    private static ItemRemoval RemoveFileSafely(string file, UninstallRemovalOptions options)
    {
        try
        {
            // Atalho de arquivo: apagar remove o link, nunca o alvo. Ele
            // também não vai para a Lixeira — a Lixeira moveria o conteúdo
            // do alvo junto.
            if (SafeTraversal.IsReparsePoint(file))
            {
                File.Delete(file);
                return ItemRemoval.Removed;
            }

            if (options.UseRecycleBin && RecycleBin.CanAccept(file) && RecycleBin.Send(file))
                return ItemRemoval.Removed;

            File.Delete(file);
            return ItemRemoval.Removed;
        }
        catch
        {
            // Em uso ou sem permissão: a fila de remoção pendente do Windows
            // resolve no próximo reinício, como o Revo avisa.
            if (options.DeferToNextRestart && DeferredDelete.Schedule(file))
                return ItemRemoval.Deferred;

            return ItemRemoval.Failed;
        }
    }

    /// <summary>
    /// Remove um atalho de pasta sem tocar no alvo.
    /// </summary>
    private static ItemRemoval RemoveLinkSafely(string dir, UninstallRemovalOptions options)
    {
        try
        {
            Directory.Delete(dir, recursive: false);
            return ItemRemoval.Removed;
        }
        catch
        {
            if (options.DeferToNextRestart && DeferredDelete.Schedule(dir))
                return ItemRemoval.Deferred;

            return ItemRemoval.Failed;
        }
    }

    /// <summary>Arquivos de uma pasta, sem falhar quando um deles está inacessível.</summary>
    private static IEnumerable<string> EnumerateFilesSafe(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Subpastas de uma pasta, sem falhar quando uma delas está inacessível.</summary>
    private static IEnumerable<string> EnumerateDirectoriesSafe(string folder)
    {
        try
        {
            return Directory.EnumerateDirectories(folder, "*", SearchOption.TopDirectoryOnly).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Encerra os processos que estao rodando a partir dos arquivos e pastas
    /// que serao removidos.
    /// </summary>
    private static IEnumerable<RemovalOutcome> KillRunningProcesses(List<LeftoverItem> selected)
    {
        var folders = selected
            .Where(i => i.Kind == "Pasta")
            .Select(i => PathUtil.Normalize(i.Path))
            .Where(p => p.Length > 0)
            .ToList();

        var files = selected
            .Where(i => i.Kind == "Arquivo")
            .Select(i => PathUtil.Normalize(i.Path))
            .Where(p => p.Length > 0)
            .ToList();

        var processes = ProcessTerminator.FindUnder(folders);
        if (files.Count > 0)
            processes = processes
                .Concat(ProcessTerminator.FindExact(files))
                .DistinctBy(p => p.ProcessId)
                .ToList();

        // Um processo que roda a partir de um item protegido nao e encerrado:
        // encerrar o processo seria tocar no item protegido tanto quanto
        // apagar o arquivo.
        processes = processes
            .Where(p => !ExcludeList.Current.ContainsPath(p.ExecutablePath))
            .ToList();

        foreach (var termination in ProcessTerminator.Terminate(processes))
        {
            yield return new RemovalOutcome
            {
                Item = new LeftoverItem
                {
                    Kind = "Processo",
                    Path = termination.Process.ExecutablePath,
                    Detail = termination.Process.ProcessName,
                },
                Status = termination.Terminated ? RemovalStatus.Removed : RemovalStatus.Failed,
                Message = termination.Terminated
                    ? $"processo {termination.Process.ProcessName} encerrado"
                    : $"não foi possível encerrar o processo {termination.Process.ProcessName}: {termination.Message}",
            };
        }
    }

    /// <summary>
    /// Cria o ponto de restauracao com o conteudo de tudo o que sera removido.
    /// O ponto so e criado quando pelo menos um item pode ser capturado.
    /// </summary>
    private static RestorePoint? CreateRestorePoint(List<LeftoverItem> selected, UninstallRemovalOptions options)
    {
        try
        {
            var name = string.IsNullOrWhiteSpace(options.RestorePointName)
                ? "Desinstalação"
                : options.RestorePointName;

            var operation = selected.All(i => i.Kind == "Registro") ? BackupManager.OperationRegistry
                : selected.All(i => i.Kind != "Registro") ? BackupManager.OperationFiles
                : BackupManager.OperationMixed;

            var point = BackupManager.Create(name, operation);

            foreach (var item in selected)
            {
                // Item na lista de exclusoes nunca e removido — logo, nunca e
                // copiado. Sem esta checagem, um backup guardaria justamente o
                // que o usuario mandou proteger.
                if (ExcludeList.Current.Contains(item.Kind, item.Path)) continue;

                // Item protegido nunca e removido — logo, nunca e copiado.
                // Sem esta checagem, uma varredura forçada sobre uma pasta de
                // sistema colocaria o System32 inteiro no backup.
                if (item.Kind == "Registro" && SafetyRules.IsProtectedRegistryKey(item.Path)) continue;
                if (item.Kind == "Pasta" && SafetyRules.IsProtectedFolder(PathUtil.Normalize(item.Path))) continue;
                if (item.Kind == "Arquivo"
                    && SafetyRules.IsProtectedFolder(Path.GetDirectoryName(PathUtil.Normalize(item.Path)) ?? string.Empty)) continue;

                switch (item.Kind)
                {
                    case "Registro":
                        BackupManager.AddRegistryKey(point, item.Path);
                        break;
                    case "Arquivo":
                        BackupManager.AddFile(point, item.Path);
                        break;
                    case "Pasta":
                        BackupManager.AddFolder(point, item.Path);
                        break;
                }
            }

            BackupManager.Save(point);
            return point;
        }
        catch { return null; }
    }

    private static RemovalOutcome RemoveOne(LeftoverItem item, UninstallRemovalOptions options)
    {
        try
        {
            // Guarda final da lista de exclusoes. A varredura ja esconde os
            // itens excluidos, mas o item pode chegar marcado por outro
            // caminho (uma marcacao antiga, um item vindo de outra tela). A
            // lista de exclusoes e a ultima palavra: o item nao e removido.
            if (ExcludeList.Current.Contains(item.Kind, item.Path))
                return Fail(item, RemovalStatus.Protected, "item na lista de exclusões");

            switch (item.Kind)
            {
                case "Pasta":
                {
                    var full = PathUtil.Normalize(item.Path);
                    if (SafetyRules.IsProtectedFolder(full))
                        return Fail(item, RemovalStatus.Protected, "pasta de sistema protegida");

                    if (!Directory.Exists(full))
                        return new RemovalOutcome { Item = item, Status = RemovalStatus.AlreadyAbsent, Message = "já não existia" };

                    // O proprio item e um atalho (junction, symlink): o alvo
                    // esta fora da pasta que o usuario mandou remover. Remove-se
                    // o link e nada mais — enumerar a raiz devolveria os
                    // arquivos do alvo, e a remocao apagaria conteudo de fora
                    // do escopo (e burlaria a lista de exclusoes).
                    if (SafeTraversal.IsReparsePoint(full))
                    {
                        try
                        {
                            Directory.Delete(full, recursive: false);
                            return new RemovalOutcome
                            {
                                Item = item,
                                Status = RemovalStatus.Removed,
                                Message = "removido o atalho (o destino do link não foi tocado)",
                            };
                        }
                        catch (Exception inner)
                        {
                            if (options.DeferToNextRestart && DeferredDelete.Schedule(full))
                                return new RemovalOutcome
                                {
                                    Item = item,
                                    Status = RemovalStatus.Deferred,
                                    Message = "atalho agendado para o próximo reinício do sistema",
                                };

                            return Fail(item, RemovalStatus.Failed, inner.Message, RemovalOutcome.ErrorCodeOf(inner));
                        }
                    }

                    // A remoção recursiva "cega" (Directory.Delete) e o envio da
                    // pasta inteira para a Lixeira levariam junto os arquivos
                    // que o usuário mandou proteger — e proteger é a única razão
                    // de a lista existir. Havendo item protegido dentro da
                    // pasta, o conteúdo sai item a item e a pasta em si
                    // permanece.
                    if (HasProtectedDescendant(full))
                    {
                        var content = RemoveFolderContent(full, options);

                        // Nada aqui fica em silêncio: o que saiu, o que ficou
                        // protegido, o que falhou e o que foi adiado para o
                        // próximo reinício aparecem todos no relatório.
                        var details = new List<string>();
                        if (content.Removed > 0) details.Add($"{content.Removed} item(ns) removido(s) dentro dela");
                        if (content.Protected > 0) details.Add($"{content.Protected} item(ns) protegido(s) mantido(s)");
                        if (content.Failed > 0) details.Add($"{content.Failed} item(ns) não puderam ser removidos");
                        if (content.Deferred > 0) details.Add($"{content.Deferred} item(ns) adiado(s) para o próximo reinício");

                        var status = content.Protected > 0 ? RemovalStatus.Protected
                            : content.Failed > 0 ? RemovalStatus.Failed
                            : content.Deferred > 0 ? RemovalStatus.Deferred
                            : RemovalStatus.Removed;

                        return new RemovalOutcome
                        {
                            Item = item,
                            Status = status,
                            Message = status == RemovalStatus.Removed
                                ? "removida"
                                : "permaneceu: " + string.Join(", ", details),
                            RemovedInside = content.Removed,
                            ProtectedInside = content.Protected,
                            FailedInside = content.Failed,
                            DeferredInside = content.Deferred,
                        };
                    }

                    // O Revo Pro envia as sobras de arquivos para a Lixeira.
                    if (options.UseRecycleBin && RecycleBin.CanAccept(full) && RecycleBin.Send(full))
                        return new RemovalOutcome { Item = item, Status = RemovalStatus.Removed, Message = "enviada para a Lixeira" };

                    try
                    {
                        // A remoção percorre a árvore sem atravessar atalho:
                        // o alvo de um junction está fora da pasta e não
                        // pode ser apagado junto.
                        SafeTraversal.DeleteDirectoryTree(full);
                        return new RemovalOutcome { Item = item, Status = RemovalStatus.Removed, Message = "removida" };
                    }
                    catch (Exception inner)
                    {
                        if (options.DeferToNextRestart && DeferredDelete.Schedule(full))
                            return new RemovalOutcome
                            {
                                Item = item,
                                Status = RemovalStatus.Deferred,
                                Message = "agendada para o próximo reinício do sistema",
                            };

                        return Fail(item, RemovalStatus.Failed, inner.Message, RemovalOutcome.ErrorCodeOf(inner));
                    }
                }

                case "Arquivo":
                {
                    var full = PathUtil.Normalize(item.Path);
                    var leaf = Path.GetFileName(full);
                    if (SafetyRules.IsProtectedFolder(Path.GetDirectoryName(full) ?? string.Empty))
                        return Fail(item, RemovalStatus.Protected, "caminho de sistema protegido");

                    if (!File.Exists(full))
                        return new RemovalOutcome { Item = item, Status = RemovalStatus.AlreadyAbsent, Message = "já não existia" };

                    // Atalho de arquivo: apagar remove o link, nunca o alvo.
                    // Ele também não vai para a Lixeira — a Lixeira moveria o
                    // conteúdo do alvo junto.
                    var isLink = SafeTraversal.IsReparsePoint(full);

                    if (!isLink && options.UseRecycleBin && RecycleBin.CanAccept(full) && RecycleBin.Send(full))
                        return new RemovalOutcome { Item = item, Status = RemovalStatus.Removed, Message = "enviado para a Lixeira" };

                    try
                    {
                        File.Delete(full);
                        return new RemovalOutcome { Item = item, Status = RemovalStatus.Removed, Message = "removido" };
                    }
                    catch (Exception inner)
                    {
                        if (options.DeferToNextRestart && DeferredDelete.Schedule(full))
                            return new RemovalOutcome
                            {
                                Item = item,
                                Status = RemovalStatus.Deferred,
                                Message = "agendado para o próximo reinício do sistema",
                            };

                        return Fail(item, RemovalStatus.Failed, inner.Message, RemovalOutcome.ErrorCodeOf(inner));
                    }
                }

                case "Registro":
                {
                    if (SafetyRules.IsProtectedRegistryKey(item.Path))
                        return Fail(item, RemovalStatus.Protected, "chave de registro protegida");

                    var (hive, sub) = SafetyRules.SplitRegistryPath(item.Path);
                    using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                    using var key = baseKey.OpenSubKey(sub);
                    if (key is null)
                        return new RemovalOutcome { Item = item, Status = RemovalStatus.AlreadyAbsent, Message = "já não existia" };

                    baseKey.DeleteSubKeyTree(sub, throwOnMissingSubKey: false);
                    return new RemovalOutcome { Item = item, Status = RemovalStatus.Removed, Message = "removida" };
                }

                default:
                    return Fail(item, RemovalStatus.Failed, $"tipo desconhecido: {item.Kind}");
            }
        }
        catch (Exception ex)
        {
            return Fail(item, RemovalStatus.Failed, ex.Message, RemovalOutcome.ErrorCodeOf(ex));
        }
    }

    private static RemovalOutcome Fail(LeftoverItem item, RemovalStatus status, string message, int errorCode = 0) =>
        new() { Item = item, Status = status, Message = message, ErrorCode = errorCode };
}
