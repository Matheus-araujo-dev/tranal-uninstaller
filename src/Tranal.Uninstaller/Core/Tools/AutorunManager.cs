using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Management;
using Microsoft.Win32;
using Tranal.Uninstaller.Core.Recovery;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Natureza do item de inicializacao automatica.
/// </summary>
public enum AutorunItemKind
{
    /// <summary>Valor de registro em uma chave Run/RunOnce.</summary>
    RunKey,

    /// <summary>Atalho ou executavel na pasta de inicializacao.</summary>
    StartupFolder,

    /// <summary>Servico do Windows (consultado por WMI).</summary>
    Service,

    /// <summary>Tarefa agendada (Task Scheduler).</summary>
    ScheduledTask,
}

/// <summary>
/// Um item de inicializacao automatica encontrado no sistema.
/// </summary>
public sealed class AutorunItem
{
    /// <summary>Natureza do item.</summary>
    public AutorunItemKind Kind { get; init; }

    /// <summary>Nome exibido (valor do registro, nome do servico, nome da tarefa).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Comando ou caminho do executavel.</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>Chave de registro / pasta / nome do servico.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>Origen legivel do item (ex.: "HKCU\SOFTWARE\...\Run", "Servicos").</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Editora do executavel, quando consegue ser lida.</summary>
    public string Publisher { get; init; } = string.Empty;

    /// <summary>True quando o item esta habilitado.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>True para item de sistema: nunca pode ser removido.</summary>
    public bool IsSystem { get; init; }
}

/// <summary>
/// Gerencia os pontos de inicializacao automatica do Windows.
///
/// Fontes cobertas (mesma cobertura do modulo de inicializacao automatica do Revo):
///   - Valores das chaves Run/RunOnce/RunServices/RunServicesOnce/Explorer\Run
///     em HKCU e HKLM (visoes de 64 e 32 bits);
///   - Pastas de inicializacao do usuario e do sistema;
///   - Servicos do Windows (WMI: Win32_Service);
///   - Tarefas agendadas (Task Scheduler via COM).
///
/// Observacao sobre remocao: o motor canonico <see cref="UninstallEngine.RemoveLeftovers"/>
/// remove CHAVES de registro, nao VALORES. Por isso a remocao de um valor de Run e feita
/// direto pela API de registro (com as mesmas regras de seguranca aplicadas antes),
/// enquanto a remocao de arquivo da pasta de inicializacao passa pelo motor canonico.
/// </summary>
public static class AutorunManager
{
    /// <summary>Prefixo aplicado ao nome do valor de registro quando o item e desabilitado.</summary>
    private const string DisabledPrefix = "DisabledName-";

    /// <summary>Sufixo aplicado ao arquivo da pasta de inicializacao quando desabilitado.</summary>
    private const string DisabledFileSuffix = ".disabled";

    /// <summary>Chaves de registro varridas em busca de itens de inicializacao.</summary>
    private static readonly string[] RunKeyRelativePaths =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunServices",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunServicesOnce",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Run",
    };

    /// <summary>Entradas de Run consideradas parte do proprio Windows (nao podem ser removidas).</summary>
    private static readonly HashSet<string> SystemRunEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        "SecurityHealth",
        "WindowsSecurity",
        "RTHDVCPL",
    };

    /// <summary>Modo de inicializacao original dos servicos desabilitados nesta sessao.</summary>
    private static readonly ConcurrentDictionary<string, string> DisabledServiceModes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Varre todas as origens de inicializacao automatica.
    /// Nunca lanca: falhas parciais devolvem apenas o que foi lido com sucesso.
    /// </summary>
    public static IReadOnlyList<AutorunItem> Scan()
    {
        var items = new List<AutorunItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try { ScanRunKeys(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer chaves de Run: {ex.Message}"); }

        try { ScanStartupFolders(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer pastas de inicializacao: {ex.Message}"); }

        try { ScanServices(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer servicos: {ex.Message}"); }

        try { ScanScheduledTasks(items, seen); }
        catch (Exception ex) { ToolLog.Error($"Falha ao varrer tarefas agendadas: {ex.Message}"); }

        return items;
    }

    /// <summary>
    /// Habilita ou desabilita um item de inicializacao.
    /// Desabilitar um valor de Run renomeia o valor para "DisabledName-&lt;valor&gt;" preservando os dados;
    /// habilitar reverte a operacao. Devolve true em caso de sucesso.
    /// </summary>
    public static bool SetEnabled(AutorunItem item, bool enabled)
    {
        try
        {
            return item.Kind switch
            {
                AutorunItemKind.RunKey => SetRunKeyEnabled(item, enabled),
                AutorunItemKind.StartupFolder => SetStartupFileEnabled(item, enabled),
                AutorunItemKind.Service => SetServiceEnabled(item, enabled),
                AutorunItemKind.ScheduledTask => SetTaskEnabled(item, enabled),
                _ => false,
            };
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao alterar o estado de '{item.Name}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Remove um item de inicializacao.
    /// Servicos e tarefas agendadas nao sao removidos (apenas desabilitados) e itens
    /// de sistema nunca sao removidos. Devolve true em caso de sucesso.
    /// </summary>
    public static bool Remove(AutorunItem item)
    {
        try
        {
            if (item.IsSystem)
            {
                ToolLog.Info($"Item de sistema protegido, nao removido: {item.Name}");
                return false;
            }

            switch (item.Kind)
            {
                case AutorunItemKind.RunKey:
                    return RemoveRunKeyValue(item);

                case AutorunItemKind.StartupFolder:
                    return RemoveStartupFile(item);

                default:
                    ToolLog.Info($"'{item.Kind}' nao pode ser removido, apenas desabilitado: {item.Name}");
                    return false;
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao remover '{item.Name}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Adiciona um novo item de inicializacao.
    /// So tem efeito para chaves de registro e para a pasta de inicializacao.
    /// </summary>
    public static bool Add(AutorunItem item)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Command))
            {
                ToolLog.Info("Add: nome e comando sao obrigatorios.");
                return false;
            }

            return item.Kind switch
            {
                AutorunItemKind.RunKey => AddRunKeyValue(item),
                AutorunItemKind.StartupFolder => AddStartupFile(item),
                _ => false,
            };
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao adicionar '{item.Name}': {ex.Message}");
            return false;
        }
    }

    // ---------------------------------------------------------------------
    // Edicao
    // ---------------------------------------------------------------------

    /// <summary>
    /// Descobre a natureza do item a partir do local informado.
    ///
    /// O local pode ser o caminho de uma chave de registro
    /// ("HKEY_CURRENT_USER\SOFTWARE\...\Run") ou uma pasta. Vazio significa a
    /// chave de execucao automatica do usuario.
    ///
    /// A REGRA mora aqui, em um lugar so: quem pergunta (a tela e a CLI) e
    /// quem responde (o nucleo) nao podem ter copias diferentes.
    /// </summary>
    /// <param name="location">O local informado pelo usuario.</param>
    public static AutorunItemKind ResolveKind(string? location)
    {
        var local = (location ?? string.Empty).Trim();
        if (local.Length == 0) return AutorunItemKind.RunKey;

        if (local.StartsWith("HKEY_", StringComparison.OrdinalIgnoreCase)) return AutorunItemKind.RunKey;

        return AutorunItemKind.StartupFolder;
    }

    /// <summary>
    /// Edita um item de inicializacao existente: o nome e o comando.
    ///
    /// E o "editar" do StartUp Tuner: a entrada continua no mesmo lugar, mas
    /// passa a apontar para outro executavel e (quando o nome muda) ganha
    /// outro nome.
    ///
    /// Nome vazio mantem o nome atual; comando vazio mantem o comando atual.
    ///
    /// O que cada natureza de item aceita:
    ///   - chave de registro: o nome (o valor) e o comando;
    ///   - pasta de inicializacao: o nome (o arquivo) e o comando (o novo
    ///     executavel); o arquivo antigo vai para o backup antes de trocar;
    ///   - servico: o nome (o nome de exibicao) e o comando (o binario); o
    ///     nome do servico e a identidade dele no Windows e nao muda;
    ///   - tarefa agendada: so o comando (a acao); o nome da tarefa e o
    ///     endereco dela no Agendador e nao muda.
    ///
    /// Item de sistema nunca e editado. Devolve true em caso de sucesso.
    /// </summary>
    /// <param name="item">O item a editar (o mesmo que a varredura devolveu).</param>
    /// <param name="newName">O novo nome (vazio mantem o atual).</param>
    /// <param name="newCommand">O novo comando (vazio mantem o atual).</param>
    public static bool Update(AutorunItem item, string newName, string newCommand)
    {
        try
        {
            if (item is null) return false;

            if (item.IsSystem)
            {
                ToolLog.Info($"Item de sistema protegido, nao editado: {item.Name}");
                return false;
            }

            var nome = (newName ?? string.Empty).Trim();
            var comando = (newCommand ?? string.Empty).Trim();

            if (nome.Length == 0 && comando.Length == 0)
            {
                ToolLog.Info("Update: informe o novo nome ou o novo comando.");
                return false;
            }

            return item.Kind switch
            {
                AutorunItemKind.RunKey => UpdateRunKeyValue(item, nome, comando),
                AutorunItemKind.StartupFolder => UpdateStartupFile(item, nome, comando),
                AutorunItemKind.Service => UpdateService(item, nome, comando),
                AutorunItemKind.ScheduledTask => UpdateScheduledTask(item, comando),
                _ => false,
            };
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao editar '{item.Name}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Edita o VALOR de registro: o nome do valor e o comando apontado por ele.
    /// O valor nao e renomeavel no registro, entao a edicao grava o valor novo
    /// e apaga o antigo.
    /// </summary>
    private static bool UpdateRunKeyValue(AutorunItem item, string newName, string newCommand)
    {
        if (!TrySplitRegistryValuePath(item.Location, out var hive, out var subPath, out var valueName))
        {
            ToolLog.Info($"Localizacao invalida para item de registro: {item.Location}");
            return false;
        }

        var keyPath = $@"{SafetyRules.HiveName(hive)}\{subPath}";
        if (SafetyRules.IsProtectedRegistryKey(keyPath + "\\" + valueName))
        {
            ToolLog.Info($"Chave protegida pelas regras de seguranca: {keyPath}");
            return false;
        }

        if (ExcludeList.Current.ContainsRegistryKey(keyPath))
        {
            ToolLog.Info($"Chave na lista de exclusoes: {keyPath}");
            return false;
        }

        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.OpenSubKey(subPath, writable: true);
        if (key is null)
        {
            ToolLog.Info($"Chave de registro nao encontrada ou sem permissao: {keyPath}");
            return false;
        }

        // O Local do item pode estar desatualizado (o item pode ter sido lido
        // antes de uma desabilitacao): o nome ativo e resolvido pelo estado real.
        var activeName = ResolveActiveValueName(key, valueName);
        if (activeName is null)
        {
            ToolLog.Info($"Valor de registro nao encontrado: {item.Location}");
            return false;
        }

        // O item pode estar desabilitado: o prefixo do estado continua no nome.
        var disabled = activeName.StartsWith(DisabledPrefix, StringComparison.Ordinal);
        var targetName = newName.Length > 0
            ? (disabled ? DisabledPrefix + newName : newName)
            : activeName;

        var renomeia = !string.Equals(activeName, targetName, StringComparison.Ordinal);
        if (renomeia && ContainsValueName(key.GetValueNames(), targetName))
        {
            ToolLog.Info($"Ja existe o valor '{targetName}'; operacao cancelada.");
            return false;
        }

        if (newCommand.Length == 0)
        {
            // So o nome mudou: o valor e movido, preservando os dados e o tipo.
            if (renomeia && !MoveValue(key, activeName, targetName)) return false;
        }
        else
        {
            var kind = RegistryValueKind.String;
            try { kind = key.GetValueKind(activeName); }
            catch (Exception ex) { ToolLog.Error($"Falha ao ler o tipo do valor '{activeName}': {ex.Message}"); }

            if (kind is RegistryValueKind.Binary or RegistryValueKind.MultiString or RegistryValueKind.Unknown)
                kind = RegistryValueKind.String;

            key.SetValue(targetName, newCommand, kind);

            if (renomeia)
                key.DeleteValue(activeName, throwOnMissingValue: false);
        }

        item.Name = newName.Length > 0 ? newName : item.Name;
        if (newCommand.Length > 0) item.Command = newCommand;
        item.Location = keyPath + "\\" + targetName;
        return true;
    }

    /// <summary>
    /// Edita o item da pasta de inicializacao: o nome e o nome do arquivo, e o
    /// comando e o novo executavel apontado por ele.
    ///
    /// O arquivo antigo vai para o backup antes de ser trocado: o atalho e a
    /// manha de o programa comecar junto com a maquina.
    /// </summary>
    private static bool UpdateStartupFile(AutorunItem item, string newName, string newCommand)
    {
        var location = item.Location;
        var desabilitado = location.EndsWith(DisabledFileSuffix, StringComparison.OrdinalIgnoreCase);
        var basePath = desabilitado ? location[..^DisabledFileSuffix.Length] : location;

        // O arquivo pode ter mudado de nome depois da leitura: o caminho real
        // e resolvido pelo disco.
        string currentPath;
        if (File.Exists(basePath)) currentPath = basePath;
        else if (File.Exists(location)) currentPath = location;
        else
        {
            ToolLog.Info($"Arquivo de inicializacao nao encontrado: {location}");
            return false;
        }

        var folder = Path.GetDirectoryName(currentPath) ?? string.Empty;
        var extension = Path.GetExtension(basePath);
        var suffix = desabilitado ? DisabledFileSuffix : string.Empty;

        var targetPath = currentPath;
        if (newName.Length > 0)
        {
            var fileName = SanitizeFileName(newName) + extension + suffix;
            targetPath = Path.Combine(folder, fileName);
        }

        // O comando novo e o novo executavel.
        string? origem = null;
        if (newCommand.Length > 0
            && !string.Equals(newCommand, currentPath, StringComparison.OrdinalIgnoreCase))
        {
            var source = newCommand.Trim().Trim('"');
            if (!File.Exists(source))
            {
                ToolLog.Info($"Arquivo de origem nao encontrado: {source}");
                return false;
            }

            origem = source;
        }

        var renomeia = !string.Equals(targetPath, currentPath, StringComparison.OrdinalIgnoreCase);
        if (renomeia && File.Exists(targetPath))
        {
            ToolLog.Info($"Ja existe '{targetPath}'; operacao cancelada.");
            return false;
        }

        if (renomeia || origem is not null)
        {
            // Backup antes de trocar: o item antigo pode ser o unico atalho
            // que o usuario tinha.
            var backup = LeftoverBackup.Backup(
                new[]
                {
                    new LeftoverItem
                    {
                        Kind = "Arquivo",
                        Path = currentPath,
                        Detail = "Item de inicializacao automatica (backup antes da edicao)",
                        Selected = true,
                    },
                },
                string.IsNullOrWhiteSpace(item.Name) ? "Inicializacao" : item.Name);

            if (!backup.Ok)
            {
                ToolLog.Info($"Nao foi possivel guardar o backup do arquivo: {backup.Message}");
                return false;
            }
        }

        if (renomeia) File.Move(currentPath, targetPath);

        if (origem is not null)
        {
            if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                // Atalho: o novo alvo vira um novo atalho.
                if (!WriteShortcut(targetPath, origem)) return false;
            }
            else
            {
                File.Copy(origem, targetPath, overwrite: true);
            }
        }

        item.Name = newName.Length > 0 ? newName : item.Name;
        if (origem is not null) item.Command = origem;
        item.Location = targetPath;
        return true;
    }

    /// <summary>
    /// Edita o servico: o nome de exibicao e o binario que ele executa.
    /// O nome do servico e a identidade dele no Windows e nao muda.
    /// </summary>
    private static bool UpdateService(AutorunItem item, string newName, string newCommand)
    {
        var scope = new ManagementScope(@"\\.\root\cimv2");
        scope.Connect();
        using var searcher = new ManagementObjectSearcher(scope,
            new ObjectQuery($"SELECT * FROM Win32_Service WHERE Name='{EscapeWql(item.Location)}'"));
        using var results = searcher.Get();

        foreach (var raw in results)
        {
            if (raw is not ManagementObject service) continue;

            try
            {
                var parameters = service.GetMethodParameters("Change");

                // O modo de inicializacao e repassado como esta: a edicao nao
                // pode mudar como o servico sobe. A conta NAO e repassada:
                // mudar a conta do servico exige a senha dela.
                parameters["StartMode"] = service["StartMode"]?.ToString() ?? string.Empty;

                if (newName.Length > 0) parameters["DisplayName"] = newName;
                if (newCommand.Length > 0) parameters["PathName"] = newCommand;

                var result = service.InvokeMethod("Change", parameters, null);
                var code = Convert.ToUInt32(result["ReturnValue"]);

                // 0 = sucesso, 10 = o servico ja esta no estado pedido.
                if (code is 0 or 10)
                {
                    if (newName.Length > 0) item.Name = newName;
                    if (newCommand.Length > 0) item.Command = newCommand;
                    return true;
                }

                ToolLog.Error($"Falha ao editar o servico '{item.Location}' (codigo {code}). "
                             + "A operacao exige permissao de administrador.");
                return false;
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao editar o servico '{item.Location}': {ex.Message}");
                return false;
            }
        }

        ToolLog.Info($"Servico nao encontrado: {item.Location}");
        return false;
    }

    /// <summary>
    /// Edita a acao da tarefa agendada.
    ///
    /// O nome da tarefa e o endereco dela no Agendador: nao muda. O que muda e
    /// o executavel da acao.
    /// </summary>
    private static bool UpdateScheduledTask(AutorunItem item, string newCommand)
    {
        if (newCommand.Length == 0) return false;

        var com = CreateComObject("Schedule.Service");
        if (com is null)
        {
            ToolLog.Info("Agendador de Tarefas indisponivel.");
            return false;
        }

        dynamic service = com;
        try
        {
            service.Connect();

            var location = item.Location;
            var separator = location.LastIndexOf('\\');
            var folderPath = separator > 0 ? location[..(separator + 1)] : "\\";
            var taskName = separator > 0 ? location[(separator + 1)..] : location;

            dynamic folder = service.GetFolder(folderPath);
            dynamic task = folder.GetTask(taskName);

            dynamic definition = task.Definition;
            dynamic actions = definition.Actions;

            var count = 0;
            try { count = Convert.ToInt32(actions.Count); }
            catch (Exception ex) { ToolLog.Error($"Falha ao ler as acoes da tarefa agendada: {ex.Message}"); }

            var editada = false;
            for (var index = 1; index <= count && !editada; index++)
            {
                dynamic action = actions.Item(index);
                if (Convert.ToInt32(action.Type) != 0) continue; // 0 = Exec

                action.Path = newCommand;
                editada = true;
            }

            if (!editada)
            {
                ToolLog.Info($"A tarefa agendada '{item.Location}' nao tem acao de execucao para editar.");
                return false;
            }

            var logonType = 5; // TASK_LOGON_SERVICE_ACCOUNT
            try { logonType = Convert.ToInt32(definition.Principal.LogonType); }
            catch (Exception ex) { ToolLog.Error($"Falha ao ler a conta da tarefa agendada: {ex.Message}"); }

            var xml = Convert.ToString(definition.XmlText) ?? string.Empty;
            if (xml.Length == 0)
            {
                ToolLog.Info($"Nao foi possivel ler a definicao da tarefa agendada '{item.Location}'.");
                return false;
            }

            task.RegisterTaskDefinition(xml, null, null, taskName, logonType, null);

            item.Command = newCommand;
            return true;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao editar a tarefa agendada '{item.Location}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Grava um atalho do Windows (.lnk) apontando para o executavel informado.
    /// </summary>
    private static bool WriteShortcut(string targetPath, string target)
    {
        var com = CreateComObject("WScript.Shell");
        if (com is null)
        {
            ToolLog.Info("Nao foi possivel gravar o atalho: o componente de scripts do Windows nao esta disponivel.");
            return false;
        }

        dynamic shell = com;
        try
        {
            dynamic shortcut = shell.CreateShortcut(targetPath);
            shortcut.TargetPath = target;
            shortcut.Save();
            return true;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao gravar o atalho '{targetPath}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Limpa o nome de um arquivo: os caracteres que o Windows nao aceita em
    /// um nome viram "_".
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        var limpo = new string(name.Trim().Select(c => invalidos.Contains(c) ? '_' : c).ToArray());
        return limpo.Length > 0 ? limpo : "item";
    }


    // ---------------------------------------------------------------------
    // Varredura das chaves de registro
    // ---------------------------------------------------------------------

    private static void ScanRunKeys(List<AutorunItem> items, HashSet<string> seen)
    {
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                foreach (var relative in RunKeyRelativePaths)
                {
                    try
                    {
                        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                        using var key = baseKey.OpenSubKey(relative);
                        if (key is null) continue;

                        // Canonicalizacao identica a do resto do app: a visao de 32 bits
                        // do HKLM aparece com o prefixo SOFTWARE\WOW6432Node.
                        var keyPath = LeftoverScanner
                            .CanonicalRegistryPath(hive, relative, string.Empty, view)
                            .TrimEnd('\\');

                        foreach (var valueName in key.GetValueNames())
                        {
                            if (string.IsNullOrEmpty(valueName)) continue;

                            var raw = key.GetValue(valueName);
                            if (raw is null) continue;

                            var command = raw.ToString() ?? string.Empty;
                            var disabled = valueName.StartsWith(DisabledPrefix, StringComparison.Ordinal);
                            var displayName = disabled ? valueName[DisabledPrefix.Length..] : valueName;
                            var location = keyPath + "\\" + valueName;

                            if (!seen.Add("run|" + location)) continue;

                            items.Add(new AutorunItem
                            {
                                Kind = AutorunItemKind.RunKey,
                                Name = displayName,
                                Command = command,
                                Location = location,
                                Source = ShortHive(keyPath),
                                Publisher = ReadPublisher(command),
                                Enabled = !disabled,
                                IsSystem = SystemRunEntries.Contains(displayName),
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        ToolLog.Error($"Falha ao ler {hive}\\{relative}: {ex.Message}");
                    }
                }
            }
        }
    }

    // ---------------------------------------------------------------------
    // Varredura das pastas de inicializacao
    // ---------------------------------------------------------------------

    private static void ScanStartupFolders(List<AutorunItem> items, HashSet<string> seen)
    {
        foreach (var (folder, source) in StartupFolders())
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) continue;

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(folder); }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao listar {folder}: {ex.Message}");
                continue;
            }

            foreach (var file in files)
            {
                try
                {
                    if (!IsStartupExtension(Path.GetExtension(file))) continue;

                    var disabled = file.EndsWith(DisabledFileSuffix, StringComparison.OrdinalIgnoreCase);
                    var activePath = disabled ? file[..^DisabledFileSuffix.Length] : file;
                    var location = PathUtil.Normalize(file);

                    if (!seen.Add("startup|" + location)) continue;

                    items.Add(new AutorunItem
                    {
                        Kind = AutorunItemKind.StartupFolder,
                        Name = Path.GetFileNameWithoutExtension(activePath),
                        Command = activePath,
                        Location = location,
                        Source = source,
                        Publisher = ReadPublisher(activePath),
                        Enabled = !disabled,
                        IsSystem = false,
                    });
                }
                catch (Exception ex)
                {
                    ToolLog.Error($"Falha ao ler {file}: {ex.Message}");
                }
            }
        }
    }

    private static IEnumerable<(string Folder, string Source)> StartupFolders()
    {
        yield return (Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            "Pasta de inicializacao do usuario");
        yield return (Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            "Pasta de inicializacao do sistema");
    }

    private static bool IsStartupExtension(string extension) =>
        extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".url", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase);

    // ---------------------------------------------------------------------
    // Varredura dos servicos (WMI)
    // ---------------------------------------------------------------------

    private static void ScanServices(List<AutorunItem> items, HashSet<string> seen)
    {
        var scope = new ManagementScope(@"\\.\root\cimv2");
        scope.Connect();

        using var searcher = new ManagementObjectSearcher(scope,
            new ObjectQuery("SELECT Name, DisplayName, PathName, StartMode, State FROM Win32_Service"));
        using var results = searcher.Get();

        foreach (var raw in results)
        {
            if (raw is not ManagementObject service) continue;

            try
            {
                var name = service["Name"]?.ToString() ?? string.Empty;
                if (name.Length == 0) continue;
                if (!seen.Add("service|" + name)) continue;

                var pathName = service["PathName"]?.ToString() ?? string.Empty;
                var startMode = service["StartMode"]?.ToString() ?? string.Empty;

                items.Add(new AutorunItem
                {
                    Kind = AutorunItemKind.Service,
                    Name = service["DisplayName"]?.ToString() ?? name,
                    Command = pathName,
                    Location = name,
                    Source = "Servicos",
                    Publisher = ReadPublisher(ExtractExecutablePath(pathName)),
                    Enabled = !startMode.Equals("Disabled", StringComparison.OrdinalIgnoreCase),
                    IsSystem = IsSystemService(pathName),
                });
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao ler servico: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Servico de sistema: executavel dentro da propria pasta do Windows (ou sem caminho).
    /// </summary>
    private static bool IsSystemService(string pathName)
    {
        if (string.IsNullOrWhiteSpace(pathName)) return true;

        var exe = ExtractExecutablePath(pathName);
        if (exe.Length == 0) return true;

        var windows = PathUtil.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        if (windows.Length == 0) return false;

        var full = PathUtil.Normalize(exe);
        return full.StartsWith(windows + "\\", StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------
    // Varredura das tarefas agendadas (Task Scheduler via COM)
    // ---------------------------------------------------------------------

    private static void ScanScheduledTasks(List<AutorunItem> items, HashSet<string> seen)
    {
        var com = CreateComObject("Schedule.Service");
        if (com is null) return; // Task Scheduler indisponivel: devolve lista vazia, sem erro.

        dynamic service = com;
        try
        {
            service.Connect();

            dynamic root = service.GetFolder("\\");
            dynamic collection = root.GetTasks(0);

            var count = 0;
            try { count = Convert.ToInt32(collection.Count); }
            catch (Exception ex) { ToolLog.Error($"Falha ao contar tarefas agendadas: {ex.Message}"); return; }

            for (var index = 1; index <= count; index++)
            {
                try
                {
                    dynamic task = collection.Item(index);

                    var name = Convert.ToString(task.Name) ?? string.Empty;
                    var path = Convert.ToString(task.Path) ?? string.Empty;
                    if (name.Length == 0) continue;

                    var location = path.EndsWith('\\') ? path + name : path + "\\" + name;
                    if (!seen.Add("task|" + location)) continue;

                    var command = ReadTaskAction(task);
                    var enabled = true;
                    try { enabled = Convert.ToBoolean(task.Enabled); }
                    catch { enabled = Convert.ToInt32(task.State) != 1; }

                    items.Add(new AutorunItem
                    {
                        Kind = AutorunItemKind.ScheduledTask,
                        Name = name,
                        Command = command,
                        Location = location,
                        Source = "Tarefas agendadas",
                        Publisher = ReadPublisher(ExtractExecutablePath(command)),
                        Enabled = enabled,
                        IsSystem = path.StartsWith(@"\Microsoft\Windows\", StringComparison.OrdinalIgnoreCase),
                    });
                }
                catch (Exception ex)
                {
                    ToolLog.Error($"Falha ao ler tarefa agendada: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao consultar o Agendador de Tarefas: {ex.Message}");
        }
    }

    private static string ReadTaskAction(dynamic task)
    {
        try
        {
            dynamic definition = task.Definition;
            dynamic actions = definition.Actions;

            var count = 0;
            try { count = Convert.ToInt32(actions.Count); } catch { return string.Empty; }

            for (var index = 1; index <= count; index++)
            {
                dynamic action = actions.Item(index);
                if (Convert.ToInt32(action.Type) != 0) continue; // 0 = Exec

                var path = Convert.ToString(action.Path) ?? string.Empty;
                var arguments = Convert.ToString(action.Arguments) ?? string.Empty;
                return string.IsNullOrWhiteSpace(arguments) ? path : path + " " + arguments;
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler acao da tarefa agendada: {ex.Message}");
        }

        return string.Empty;
    }

    // ---------------------------------------------------------------------
    // Habilitar / desabilitar
    // ---------------------------------------------------------------------

    private static bool SetRunKeyEnabled(AutorunItem item, bool enabled)
    {
        if (!TrySplitRegistryValuePath(item.Location, out var hive, out var subPath, out var valueName))
        {
            ToolLog.Info($"Localizacao invalida para item de registro: {item.Location}");
            return false;
        }

        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.OpenSubKey(subPath, writable: true);
        if (key is null)
        {
            ToolLog.Info($"Chave de registro nao encontrada ou sem permissao: {item.Location}");
            return false;
        }

        // O Local do item pode estar desatualizado (o item pode ter sido lido
        // antes de ser desabilitado): o nome ativo e resolvido pelo estado real da chave.
        var activeName = ResolveActiveValueName(key, valueName);
        if (activeName is null)
        {
            ToolLog.Info($"Valor de registro nao encontrado: {item.Location}");
            return false;
        }

        var originalName = OriginalValueName(valueName);
        var targetName = enabled ? originalName : DisabledPrefix + originalName;

        if (string.Equals(activeName, targetName, StringComparison.Ordinal))
        {
            item.Enabled = enabled;
            return true; // ja esta no estado pedido
        }

        if (ContainsValueName(key.GetValueNames(), targetName))
        {
            ToolLog.Info($"Ja existe o valor '{targetName}'; operacao cancelada.");
            return false;
        }

        if (!MoveValue(key, activeName, targetName)) return false;

        item.Enabled = enabled;
        return true;
    }

    /// <summary>
    /// Move o valor de registro preservando os dados (e o tipo do valor).
    /// </summary>
    private static bool MoveValue(RegistryKey key, string oldName, string newName)
    {
        try
        {
            var data = key.GetValue(oldName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (data is null) return false;

            var kind = key.GetValueKind(oldName);
            key.SetValue(newName, data, kind);
            key.DeleteValue(oldName, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao renomear o valor '{oldName}': {ex.Message}");
            return false;
        }
    }

    private static bool SetStartupFileEnabled(AutorunItem item, bool enabled)
    {
        // O Local do item pode estar desatualizado (o item pode ter sido lido
        // antes de ser desabilitado): o caminho ativo e resolvido pelo disco.
        var originalPath = item.Location.EndsWith(DisabledFileSuffix, StringComparison.OrdinalIgnoreCase)
            ? item.Location[..^DisabledFileSuffix.Length]
            : item.Location;
        var disabledPath = originalPath + DisabledFileSuffix;

        string activePath;
        if (File.Exists(originalPath)) activePath = originalPath;
        else if (File.Exists(disabledPath)) activePath = disabledPath;
        else
        {
            ToolLog.Info($"Arquivo de inicializacao nao encontrado: {item.Location}");
            return false;
        }

        var targetPath = enabled ? originalPath : disabledPath;
        if (string.Equals(activePath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            item.Enabled = enabled;
            return true; // ja esta no estado pedido
        }

        if (File.Exists(targetPath))
        {
            ToolLog.Info($"Ja existe '{targetPath}'; operacao cancelada.");
            return false;
        }

        File.Move(activePath, targetPath);
        item.Enabled = enabled;
        return true;
    }

    private static bool SetServiceEnabled(AutorunItem item, bool enabled)
    {
        if (item.IsSystem)
        {
            ToolLog.Info($"Servico de sistema protegido: {item.Location}");
            return false;
        }

        var mode = enabled ? RestoreServiceMode(item.Location) : "Disabled";

        var scope = new ManagementScope(@"\\.\root\cimv2");
        scope.Connect();
        using var searcher = new ManagementObjectSearcher(scope,
            new ObjectQuery($"SELECT * FROM Win32_Service WHERE Name='{EscapeWql(item.Location)}'"));
        using var results = searcher.Get();

        foreach (var raw in results)
        {
            if (raw is not ManagementObject service) continue;

            try
            {
                if (!enabled)
                {
                    var current = service["StartMode"]?.ToString() ?? string.Empty;
                    if (!current.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
                        DisabledServiceModes[item.Location] = current.Length == 0 ? "Manual" : current;
                }

                var parameters = service.GetMethodParameters("ChangeStartMode");
                parameters["StartMode"] = mode;
                var result = service.InvokeMethod("ChangeStartMode", parameters, null);
                var code = Convert.ToUInt32(result["ReturnValue"]);

                // 0 = sucesso, 10 = o servico ja esta no modo pedido.
                if (code is 0 or 10)
                {
                    item.Enabled = enabled;
                    return true;
                }

                ToolLog.Error($"Falha ao alterar o modo do servico '{item.Location}' (codigo {code}). "
                             + "A operacao exige permissao de administrador.");
                return false;
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao alterar o servico '{item.Location}': {ex.Message}");
                return false;
            }
        }

        ToolLog.Info($"Servico nao encontrado: {item.Location}");
        return false;
    }

    /// <summary>
    /// Modo de inicializacao gravado quando o servico foi desabilitado nesta sessao.
    /// Sem o registro original, volta para "Manual".
    /// </summary>
    private static string RestoreServiceMode(string serviceName) =>
        DisabledServiceModes.TryGetValue(serviceName, out var mode) && mode.Length > 0 ? mode : "Manual";

    private static bool SetTaskEnabled(AutorunItem item, bool enabled)
    {
        var com = CreateComObject("Schedule.Service");
        if (com is null)
        {
            ToolLog.Info("Agendador de Tarefas indisponivel.");
            return false;
        }

        dynamic service = com;
        try
        {
            service.Connect();

            var location = item.Location;
            var separator = location.LastIndexOf('\\');
            var folderPath = separator > 0 ? location[..(separator + 1)] : "\\";
            var taskName = separator > 0 ? location[(separator + 1)..] : location;

            dynamic folder = service.GetFolder(folderPath);
            dynamic task = folder.GetTask(taskName);

            task.Enabled = enabled;
            task.Register();

            item.Enabled = enabled;
            return true;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao alterar a tarefa agendada '{item.Location}': {ex.Message}");
            return false;
        }
    }

    // ---------------------------------------------------------------------
    // Remocao
    // ---------------------------------------------------------------------

    /// <summary>
    /// Remove o VALOR de registro (nunca a chave inteira).
    /// O motor canonico RemoveLeftovers remove chaves, nao valores, por isso a
    /// remocao do valor e feita aqui — sempre apos as mesmas regras de seguranca.
    /// </summary>
    private static bool RemoveRunKeyValue(AutorunItem item)
    {
        if (!TrySplitRegistryValuePath(item.Location, out var hive, out var subPath, out var valueName))
        {
            ToolLog.Info($"Localizacao invalida para item de registro: {item.Location}");
            return false;
        }

        var keyPath = $@"{SafetyRules.HiveName(hive)}\{subPath}";
        if (SafetyRules.IsProtectedRegistryKey(keyPath + "\\" + valueName))
        {
            ToolLog.Info($"Chave protegida pelas regras de seguranca: {keyPath}");
            return false;
        }

        // A lista de exclusoes protege a CHAVE inteira: apagar um valor de
        // dentro de uma chave protegida seria um caminho para furar a
        // protecao. (A remocao deste item nao passa pelo motor canonico,
        // porque o motor remove chaves e nao valores.)
        if (ExcludeList.Current.ContainsRegistryKey(keyPath))
        {
            ToolLog.Info($"Chave na lista de exclusoes: {keyPath}");
            return false;
        }

        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.OpenSubKey(subPath, writable: true);
        if (key is null)
        {
            ToolLog.Info($"Chave de registro nao encontrada: {keyPath}");
            return true; // ja nao existe: o estado desejado ja foi alcancado
        }

        // Pode haver um valor com o prefixo de desabilitado (o item foi lido
        // antes de ser desabilitado): o que precisa sair e o valor ativo.
        var activeName = ResolveActiveValueName(key, valueName);
        if (activeName is null)
        {
            ToolLog.Info($"Valor ja nao existe: {keyPath}\\{valueName}");
            return true;
        }

        key.DeleteValue(activeName, throwOnMissingValue: false);
        return true;
    }

    /// <summary>
    /// Remove o arquivo da pasta de inicializacao passando pelo motor canonico
    /// (<see cref="UninstallEngine.RemoveLeftovers"/>), que centraliza a interpretacao do resultado.
    /// </summary>
    private static bool RemoveStartupFile(AutorunItem item)
    {
        // O item pode ter sido desabilitado depois de lido: apaga o arquivo ativo.
        var location = item.Location;
        var originalPath = location.EndsWith(DisabledFileSuffix, StringComparison.OrdinalIgnoreCase)
            ? location[..^DisabledFileSuffix.Length]
            : location;

        var activePath = File.Exists(originalPath) ? originalPath : location;

        var leftover = new LeftoverItem
        {
            Kind = "Arquivo",
            Path = activePath,
            Detail = "Item de inicializacao automatica",
            Selected = true,
        };

        var report = UninstallEngine.RemoveLeftovers(new[] { leftover });
        var outcome = report.Outcomes.FirstOrDefault();
        if (outcome is null) return false;

        if (outcome.Status is RemovalStatus.Removed or RemovalStatus.AlreadyAbsent) return true;

        ToolLog.Error($"Falha ao remover '{item.Location}': {outcome.Message}");
        return false;
    }

    // ---------------------------------------------------------------------
    // Inclusao
    // ---------------------------------------------------------------------

    private static bool AddRunKeyValue(AutorunItem item)
    {
        var hive = RegistryHive.CurrentUser;
        var subPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        if (!string.IsNullOrWhiteSpace(item.Location))
        {
            var parsed = TrySplitRegistryKeyPath(item.Location, out var parsedHive, out var parsedPath);
            if (!parsed)
            {
                ToolLog.Info($"Localizacao invalida de chave de registro: {item.Location}");
                return false;
            }

            hive = parsedHive;
            subPath = parsedPath;
        }

        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.CreateSubKey(subPath, writable: true);
        if (key is null) return false;

        key.SetValue(item.Name, item.Command, RegistryValueKind.String);
        return true;
    }

    private static bool AddStartupFile(AutorunItem item)
    {
        var source = item.Command.Trim().Trim('"');
        if (!File.Exists(source))
        {
            ToolLog.Info($"Arquivo de origem nao encontrado: {source}");
            return false;
        }

        var targetFolder = Directory.Exists(item.Location)
            ? item.Location
            : Environment.GetFolderPath(Environment.SpecialFolder.Startup);

        if (string.IsNullOrWhiteSpace(targetFolder)) return false;

        Directory.CreateDirectory(targetFolder);

        var fileName = Path.GetFileName(item.Name);
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.Contains('.'))
            fileName = Path.GetFileName(source);

        File.Copy(source, Path.Combine(targetFolder, fileName), overwrite: true);
        return true;
    }

    // ---------------------------------------------------------------------
    // Auxiliares
    // ---------------------------------------------------------------------

    /// <summary>
    /// Cria um objeto COM (Task Scheduler, WScript.Shell). Devolve null quando indisponivel.
    /// </summary>
    private static object? CreateComObject(string progId)
    {
        try
        {
            var type = Type.GetTypeFromProgID(progId);
            return type is null ? null : Activator.CreateInstance(type);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao criar o objeto COM '{progId}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Divide um caminho canonico de VALOR de registro: "HKEY_...\chave\subchave\NomeDoValor".
    /// </summary>
    private static bool TrySplitRegistryValuePath(string location,
        out RegistryHive hive, out string subPath, out string valueName)
    {
        hive = RegistryHive.CurrentUser;
        subPath = string.Empty;
        valueName = string.Empty;

        if (string.IsNullOrWhiteSpace(location)) return false;

        var trimmed = location.Trim().TrimEnd('\\');
        var separator = trimmed.LastIndexOf('\\');
        if (separator <= 0) return false;

        valueName = trimmed[(separator + 1)..];
        var keyPath = trimmed[..separator];

        if (!TrySplitRegistryKeyPath(keyPath, out hive, out subPath)) return false;

        return subPath.Length > 0 && valueName.Length > 0;
    }

    /// <summary>
    /// Divide um caminho canonico de CHAVE de registro: "HKEY_...\chave\subchave".
    /// </summary>
    private static bool TrySplitRegistryKeyPath(string path,
        out RegistryHive hive, out string subPath)
    {
        hive = RegistryHive.CurrentUser;
        subPath = string.Empty;

        if (string.IsNullOrWhiteSpace(path)) return false;

        var trimmed = path.Trim().TrimEnd('\\');

        if (trimmed.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.CurrentUser;
            subPath = trimmed["HKEY_CURRENT_USER".Length..].TrimStart('\\');
        }
        else if (trimmed.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.LocalMachine;
            subPath = trimmed["HKEY_LOCAL_MACHINE".Length..].TrimStart('\\');
        }
        else
        {
            return false;
        }

        return subPath.Length > 0;
    }

    private static bool ContainsValueName(string[] names, string name)
    {
        foreach (var candidate in names)
        {
            if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>
    /// Nome original do valor (sem o prefixo de desabilitado).
    /// </summary>
    private static string OriginalValueName(string valueName) =>
        valueName.StartsWith(DisabledPrefix, StringComparison.Ordinal)
            ? valueName[DisabledPrefix.Length..]
            : valueName;

    /// <summary>
    /// Resolve o nome ATUAL do valor de registro do item.
    /// O Local do item pode estar desatualizado (leido antes de uma desabilitacao),
    /// por isso o nome ativo e procurado pelo estado real da chave.
    /// </summary>
    private static string? ResolveActiveValueName(RegistryKey key, string valueName)
    {
        var names = key.GetValueNames();
        if (ContainsValueName(names, valueName)) return valueName;

        var disabledName = DisabledPrefix + OriginalValueName(valueName);
        if (ContainsValueName(names, disabledName)) return disabledName;

        return null;
    }

    /// <summary>
    /// Forma curta do nome da raiz, como o registro mostra: "HKCU\SOFTWARE\...".
    /// </summary>
    private static string ShortHive(string canonicalPath)
    {
        if (canonicalPath.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
            return "HKCU" + canonicalPath["HKEY_CURRENT_USER".Length..];

        if (canonicalPath.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
            return "HKLM" + canonicalPath["HKEY_LOCAL_MACHINE".Length..];

        return canonicalPath;
    }

    /// <summary>
    /// Extrai o caminho do executavel de um comando de linha.
    /// </summary>
    internal static string ExtractExecutablePath(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return string.Empty;

        var text = command.Trim();
        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            return end > 1 ? text[1..end] : string.Empty;
        }

        var space = text.IndexOf(' ');
        var candidate = space > 0 ? text[..space] : text;
        if (candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return candidate;

        var extension = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return extension > 0 ? text[..(extension + 4)] : candidate;
    }

    /// <summary>
    /// Le a editora do executavel a partir dos recursos do arquivo (best effort).
    /// </summary>
    private static string ReadPublisher(string executablePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(executablePath)) return string.Empty;
            if (!File.Exists(executablePath)) return string.Empty;

            return Discovery.ExecutableMetadataReader.CompanyOf(executablePath);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler a editora de '{executablePath}': {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Escapa aspa simples para uso em consultas WQL.
    /// </summary>
    private static string EscapeWql(string value) => value.Replace("'", "''");
}
