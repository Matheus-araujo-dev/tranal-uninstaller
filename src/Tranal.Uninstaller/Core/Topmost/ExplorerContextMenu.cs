using System.IO;
using Microsoft.Win32;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Topmost;

/// <summary>
/// Integracao do Tranal com o menu de contexto do Windows Explorer.
///
/// O Revo registra uma entrada de menu ("Uninstall with Revo...") que aparece
/// sobre um executavel instalado. Aqui a integracao e feita SEM shell
/// extension COM: e uma chave de registro em
/// <c>HKCU\Software\Classes\*\shell\TranalUninstaller</c> (com
/// <c>MUIVerb</c>, valor padrao e <c>Icon</c>) e o comando em
/// <c>...\command</c>, no formato:
/// <c>"&lt;exe do Tranal&gt;" --uninstall-target "%1"</c>.
///
/// A gravacao e feita por <see cref="Apply"/>, que liga e desliga a entrada
/// conforme a preferencia "Mostrar comandos no menu de contexto do Windows".
/// As demais funcoes informam o estado (<see cref="IsRegistered"/>), devolvem
/// o executavel e os argumentos a registrar (<see cref="BuildRegistration"/>)
/// e geram o script .reg equivalente (<see cref="BuildRegistryScript"/>).
///
/// Somente HKCU e usado: o nucleo do Tranal roda elevado e nao deve escrever
/// no registro do sistema.
///
/// <para>As **outras** entradas do Tranal no Explorer (rastrear a instalacao,
/// shredar um arquivo, abrir o monitor de instalacao) estao na classe
/// <see cref="ExplorerShellMenu"/>, no mesmo arquivo: e o mesmo registro, com
/// a mesma regra de escrita.</para>
/// </summary>
public static class ExplorerContextMenu
{
    /// <summary>Nome do produto exibido no menu de contexto.</summary>
    public const string ProductName = "Tranal Uninstaller Pro";

    /// <summary>Nome da chave da entrada de menu.</summary>
    public const string ShellKeyName = "TranalUninstaller";

    /// <summary>Caminho relativo da chave da entrada de menu (dentro de HKCU).</summary>
    public const string ShellKeyRelative = @"Software\Classes\*\shell\TranalUninstaller";

    /// <summary>Caminho relativo da chave do comando (dentro de HKCU).</summary>
    public const string CommandKeyRelative = @"Software\Classes\*\shell\TranalUninstaller\command";

    /// <summary>Argumento que a CLI do Tranal recebe com o caminho do alvo.</summary>
    public const string TargetArgument = "--uninstall-target";

    /// <summary>
    /// Texto exibido no menu de contexto. Combina o rotulo de comandos
    /// (<c>ContextMenu.Commands</c>) com o nome do produto.
    /// </summary>
    public static string RegistrationDescription =>
        $"{Loc.Get("ContextMenu.Commands")} — {ProductName}";

    /// <summary>
    /// Caminho do script .reg gerado por <see cref="BuildRegistryScript"/>.
    /// Fica na pasta de dados do Tranal (sem escrita em areas do sistema).
    /// </summary>
    public static string RegistryScriptPath =>
        Path.Combine(RegistryBackup.DataDirectory, "TranalContextMenu.reg");

    /// <summary>
    /// True quando a entrada de menu ja esta registrada em
    /// <c>HKCU\Software\Classes\*\shell\TranalUninstaller</c>.
    /// </summary>
    public static bool IsRegistered()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            using var key = baseKey.OpenSubKey(ShellKeyRelative);
            return key is not null;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao verificar o registro do menu de contexto: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Argumentos passados ao executavel do Tranal quando o item do menu e
    /// acionado. O caminho do alvo e o ultimo argumento, entre aspas (o
    /// marcador <c>%1</c> e usado quando nenhum caminho e informado).
    /// </summary>
    public static string[] BuildArguments(string targetPath)
    {
        var target = (targetPath ?? string.Empty).Trim();
        if (target.Length == 0) target = "%1";

        return new[] { TargetArgument, Quote(target) };
    }

    /// <summary>
    /// Devolve o caminho do executavel do Tranal e os argumentos a registrar
    /// no comando do menu de contexto.
    /// </summary>
    public static (string ExecutablePath, string[] Arguments) BuildRegistration(string tranalExePath, string targetPath)
    {
        var executable = string.IsNullOrWhiteSpace(tranalExePath)
            ? GetTranalExecutablePath()
            : tranalExePath.Trim();

        return (executable, BuildArguments(targetPath));
    }

    /// <summary>
    /// Caminho do executavel do Tranal (o proprio processo em execucao).
    /// </summary>
    public static string GetTranalExecutablePath()
    {
        try
        {
            var process = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(process)) return process;
        }
        catch { }

        try
        {
            var candidate = Path.Combine(AppContext.BaseDirectory, "TranalUninstaller.exe");
            if (File.Exists(candidate)) return candidate;
        }
        catch { }

        return string.Empty;
    }

    /// <summary>
    /// Grava um verbo do menu de contexto no Explorer.
    ///
    /// E o **unico** lugar que escreve um verbo: a entrada de desinstalar, as
    /// entradas do Tranal e as entradas que o usuario cria passam todas por
    /// aqui. A regra do verbo (o valor padrao, o <c>MUIVerb</c>, o
    /// <c>Icon</c> e a subchave <c>command</c>) mora aqui.
    /// </summary>
    /// <param name="keyRelative">A chave (relativa a HKCU) do verbo.</param>
    /// <param name="displayName">O texto que aparece no menu.</param>
    /// <param name="commandLine">A linha de comando que roda o verbo.</param>
    /// <param name="icon">O caminho do icone (vazio deixa o do Windows).</param>
    /// <returns>True quando o verbo foi gravado.</returns>
    public static bool WriteShellEntry(string keyRelative, string displayName, string commandLine, string icon)
    {
        if (string.IsNullOrWhiteSpace(keyRelative)) return false;
        if (string.IsNullOrWhiteSpace(displayName)) return false;
        if (string.IsNullOrWhiteSpace(commandLine)) return false;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);

            using (var shell = baseKey.CreateSubKey(keyRelative))
            {
                if (shell is null) return false;

                shell.SetValue(string.Empty, displayName);
                shell.SetValue("MUIVerb", displayName);

                if (!string.IsNullOrWhiteSpace(icon))
                    shell.SetValue("Icon", icon);
            }

            using (var command = baseKey.CreateSubKey(keyRelative + @"\command"))
            {
                if (command is null) return false;

                command.SetValue(string.Empty, commandLine);
            }

            return true;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao gravar o verbo de menu de contexto: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gera o script .reg equivalente ao registro criado pelo Revo.
    ///
    /// O conteudo (e apenas o conteudo) e devolvido: quem chama decide se
    /// grava o arquivo e/ou aplica o script.
    /// </summary>
    public static string BuildRegistryScript(string tranalExePath)
    {
        var (executable, arguments) = BuildRegistration(tranalExePath, "%1");
        var description = RegistrationDescription;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();
        sb.AppendLine($"[HKEY_CURRENT_USER\\{ShellKeyRelative}]");
        sb.AppendLine($"@=\"{EscapeRegValue(description)}\"");
        sb.AppendLine($"\"MUIVerb\"=\"{EscapeRegValue(description)}\"");
        sb.AppendLine($"\"Icon\"=\"{EscapeRegValue(executable)}\"");
        sb.AppendLine();
        sb.AppendLine($"[HKEY_CURRENT_USER\\{CommandKeyRelative}]");
        sb.AppendLine($"@=\"{EscapeRegValue(BuildCommandLine(executable, arguments))}\"");

        return sb.ToString();
    }

    /// <summary>
    /// Remove a entrada de menu de contexto (apenas em HKCU) e apaga o script
    /// .reg gerado, se existir. Devolve true quando a entrada nao esta mais
    /// registrada.
    /// </summary>
    public static bool RemoveRegistryScript()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            baseKey.DeleteSubKeyTree(ShellKeyRelative, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao remover a entrada do menu de contexto: {ex.Message}");
        }

        try
        {
            var script = RegistryScriptPath;
            if (File.Exists(script)) File.Delete(script);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao apagar o script do menu de contexto: {ex.Message}");
        }

        return !IsRegistered();
    }

    /// <summary>
    /// Registra ou remove a entrada de menu de contexto do Windows conforme a
    /// preferencia escolhida pelo usuario ("Mostrar comandos no menu de
    /// contexto do Windows" do Revo).
    ///
    /// A gravacao e feita direto no registro (apenas HKCU), sem passar por um
    /// script .reg. Devolve true quando o estado pedido esta em vigor.
    /// </summary>
    public static bool Apply(bool enabled)
    {
        if (!enabled) return RemoveRegistryScript();

        try
        {
            var (executable, arguments) = BuildRegistration(GetTranalExecutablePath(), "%1");
            if (executable.Length == 0) return false;

            var description = RegistrationDescription;

            var gravado = WriteShellEntry(
                ShellKeyRelative,
                description,
                BuildCommandLine(executable, arguments),
                executable);

            if (!gravado) return false;

            // O script .reg e apenas uma copia legivel do que foi gravado.
            try
            {
                var script = RegistryScriptPath;
                var dir = Path.GetDirectoryName(script);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(script, BuildRegistryScript(executable));
            }
            catch { /* a copia do script e opcional */ }

            return IsRegistered();
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao registrar a entrada do menu de contexto: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// True quando o executavel informado corresponde a um programa instalado.
    /// </summary>
    public static bool IsInstalled(string executablePath)
    {
        try
        {
            var target = TopmostTargetResolver.ResolveFromFile(executablePath);
            return target.Program is not null;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao verificar se '{executablePath}' esta instalado: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Local de instalacao do programa correspondente ao executavel informado
    /// (string vazia quando o executavel nao pertence a um programa instalado).
    /// </summary>
    public static string GetInstallLocation(string executablePath)
    {
        try
        {
            var target = TopmostTargetResolver.ResolveFromFile(executablePath);
            var location = target.Program?.InstallLocation ?? string.Empty;
            return location.Trim().Trim('"');
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao obter o local de instalacao de '{executablePath}': {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>Command line completa do item de menu (sem as aspas externas).</summary>
    internal static string BuildCommandLine(string executable, string[] arguments)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('"').Append(executable).Append('"');

        foreach (var argument in arguments)
        {
            sb.Append(' ').Append(argument);
        }

        return sb.ToString();
    }

    /// <summary>Coloca o valor entre aspas (formato de linha de comando).</summary>
    internal static string Quote(string value) =>
        "\"" + (value ?? string.Empty).Replace("\"", string.Empty) + "\"";

    /// <summary>Escapa um valor para o formato de arquivo .reg.</summary>
    internal static string EscapeRegValue(string value) =>
        (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
}

/// <summary>
/// Uma entrada de menu do Tranal no Explorer, alem da de desinstalar.
/// </summary>
/// <param name="Id">O identificador fixo da entrada.</param>
/// <param name="DisplayName">O texto que aparece no menu.</param>
/// <param name="ShellKeyRelative">A chave (dentro de HKCU) onde a entrada mora.</param>
/// <param name="Arguments">Os argumentos passados ao executavel do Tranal.</param>
public sealed record ShellMenuEntry(
    string Id,
    string DisplayName,
    string ShellKeyRelative,
    string[] Arguments);

/// <summary>
/// As entradas do Tranal no menu de contexto do Explorer.
///
/// Ha tres delas, e todas nascem da mesma coisa: o botao direito do
/// Explorer. "Rastrear a instalacao" (o "Install and Trace" do Uninstall
/// Tool) aparece sobre executaveis e instaladores; "Shred" (o do IObit)
/// aparece sobre qualquer arquivo; e o "Monitor de Instalacao" (o do HiBit)
/// aparece sobre os dois tipos de pacote.
///
/// A regra de escrita esta em **um** lugar so: e a mesma que a entrada de
/// desinstalar usa (a chave <c>shell</c> com <c>MUIVerb</c>, <c>Icon</c> e a
/// subchave <c>command</c>). E o mesmo arquivo.
/// </summary>
public static class ExplorerShellMenu
{
    /// <summary>A entrada que rastreia a instalacao de um executavel.</summary>
    public const string InstallAndTraceId = "TranalInstallAndTrace";

    /// <summary>A entrada que shreda um arquivo.</summary>
    public const string ShredId = "TranalShred";

    /// <summary>A entrada que abre o monitor de instalacao.</summary>
    public const string MonitorId = "TranalMonitor";

    /// <summary>O argumento que rastreia a instalacao.</summary>
    public const string TraceArgument = "--trace-install";

    /// <summary>O argumento que shreda um arquivo.</summary>
    public const string ShredArgument = "--shred";

    /// <summary>O argumento que abre o monitor de instalacao.</summary>
    public const string MonitorArgument = "--monitor";

    /// <summary>
    /// As entradas do Tranal no Explorer, com as chaves onde cada uma mora.
    /// </summary>
    public static IReadOnlyList<ShellMenuEntry> Entries { get; } = new[]
    {
        new ShellMenuEntry(
            InstallAndTraceId,
            "Rastrear a instalacao com o Tranal",
            @"Software\Classes\SystemFileAssociations\.exe\shell\TranalInstallAndTrace",
            new[] { TraceArgument, "\"%1\"" }),

        new ShellMenuEntry(
            InstallAndTraceId + "Msi",
            "Rastrear a instalacao com o Tranal",
            @"Software\Classes\SystemFileAssociations\.msi\shell\TranalInstallAndTrace",
            new[] { TraceArgument, "\"%1\"" }),

        new ShellMenuEntry(
            ShredId,
            "Shred com o Tranal",
            @"Software\Classes\*\shell\TranalShred",
            new[] { ShredArgument, "\"%1\"" }),

        new ShellMenuEntry(
            MonitorId,
            "Monitor de instalacao do Tranal",
            @"Software\Classes\Msi.Package\shell\TranalMonitor",
            new[] { MonitorArgument, "\"%1\"" }),

        new ShellMenuEntry(
            MonitorId + "Exe",
            "Monitor de instalacao do Tranal",
            @"Software\Classes\exefile\shell\TranalMonitor",
            new[] { MonitorArgument, "\"%1\"" }),
    };

    /// <summary>
    /// As entradas por identificador: a mesma entrada pode morar em mais de
    /// um lugar (o "Install and Trace" vale para <c>.exe</c> e para
    /// <c>.msi</c>).
    /// </summary>
    /// <param name="id">O identificador da entrada.</param>
    public static IReadOnlyList<ShellMenuEntry> EntriesOf(string id) =>
        Entries
            .Where(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)
                     || e.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <summary>
    /// A entrada esta registrada? Basta uma das chaves dela existir.
    /// </summary>
    /// <param name="id">O identificador da entrada.</param>
    public static bool IsRegistered(string id) =>
        EntriesOf(id).Any(e => KeyExists(e.ShellKeyRelative));

    /// <summary>
    /// Registra ou remove as entradas do Tranal no Explorer.
    /// </summary>
    /// <param name="enabled">Verdadeiro para registrar.</param>
    /// <returns>Quantas entradas ficaram no estado pedido.</returns>
    public static int Apply(bool enabled)
    {
        var executable = ExplorerContextMenu.GetTranalExecutablePath();
        var aplicadas = 0;

        foreach (var entry in Entries)
        {
            var ok = enabled ? Write(entry, executable) : Remove(entry);
            if (ok) aplicadas++;
        }

        return aplicadas;
    }

    /// <summary>
    /// Remove so as entradas de um identificador.
    ///
    /// A mesma entrada pode morar em mais de um lugar (o "Rastrear a
    /// instalacao" vale para <c>.exe</c> e para <c>.msi</c>): todas as
    /// chaves do identificador sao removidas.
    /// </summary>
    /// <param name="id">O identificador da entrada.</param>
    /// <returns>Quantas chaves foram removidas.</returns>
    public static int Remove(string id)
    {
        var removidas = 0;

        foreach (var entry in EntriesOf(id))
        {
            if (Remove(entry)) removidas++;
        }

        return removidas;
    }

    private static bool Write(ShellMenuEntry entry, string executable)
    {
        if (string.IsNullOrWhiteSpace(executable)) return false;

        return ExplorerContextMenu.WriteShellEntry(
            entry.ShellKeyRelative,
            entry.DisplayName,
            ExplorerContextMenu.BuildCommandLine(executable, entry.Arguments),
            executable);
    }

    private static bool Remove(ShellMenuEntry entry)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            baseKey.DeleteSubKeyTree(entry.ShellKeyRelative, throwOnMissingSubKey: false);
            return true;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao remover a entrada '{entry.Id}': {ex.Message}");
            return false;
        }
    }

    private static bool KeyExists(string relative)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            using var key = baseKey.OpenSubKey(relative);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }
}
