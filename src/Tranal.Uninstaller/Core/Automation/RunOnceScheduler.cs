using Tranal.Uninstaller.Core.Tools;
using System.IO;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// O resultado do agendamento de uma desinstalacao reagendada.
/// </summary>
public sealed class RunOnceOutcome
{
    /// <summary>True quando o agendamento foi gravado.</summary>
    public bool Scheduled { get; init; }

    /// <summary>O comando que o Windows vai rodar no proximo logon.</summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>A chave de registro usada.</summary>
    public string RegistryPath { get; init; } = string.Empty;

    /// <summary>Explicacao do resultado.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Reagenda uma desinstalacao para o proximo logon.
///
/// O HiBit tem "desinstalacao forcada reagendada via RunOnce": quando o
/// programa esta em uso e o Windows nao deixa apagar o arquivo, a desinstalacao
/// fica agendada para rodar na proxima entrada do usuario, com o Windows
/// livre.
///
/// A REGRA mora aqui, em um lugar so: o agendamento (a chave RunOnce e o
/// comando) fica aqui, e quem chama consome o veredito.
///
/// A chave usada e a do proprio usuario (HKCU), que o Windows processa no
/// proximo logon sem pedir privilegio nenhum.
/// </summary>
public static class RunOnceScheduler
{
    /// <summary>
    /// A chave RunOnce do usuario atual.
    /// </summary>
    public const string RunOncePath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\RunOnce";

    /// <summary>
    /// Agenda um comando para rodar no proximo logon.
    /// </summary>
    /// <param name="name">O nome do item agendado.</param>
    /// <param name="command">O comando completo (com as aspas e argumentos).</param>
    public static RunOnceOutcome Schedule(string? name, string? command)
    {
        var item = (name ?? string.Empty).Trim();
        var cmd = (command ?? string.Empty).Trim();

        if (item.Length == 0)
            return new RunOnceOutcome { Message = "O item agendado precisa de um nome." };

        if (cmd.Length == 0)
            return new RunOnceOutcome { Message = "O comando agendado esta vazio." };

        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(RunOncePath);
            using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, Microsoft.Win32.RegistryView.Default);
            using var key = baseKey.CreateSubKey(sub, writable: true);
            if (key is null)
                return new RunOnceOutcome { Message = "Nao foi possivel abrir a chave RunOnce." };

            key.SetValue(item, cmd, Microsoft.Win32.RegistryValueKind.String);

            return new RunOnceOutcome
            {
                Scheduled = true,
                Command = cmd,
                RegistryPath = RunOncePath + "\\" + item,
                Message = $"A acao foi agendada para o proximo logon ({item}).",
            };
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao agendar a acao para o proximo logon: {ex.Message}");
            return new RunOnceOutcome { Message = $"Nao foi possivel agendar a acao: {ex.Message}" };
        }
    }

    /// <summary>
    /// Agenda a desinstalacao de um programa para o proximo logon.
    /// </summary>
    /// <param name="programName">O nome do programa (vai no nome do item).</param>
    /// <param name="command">O comando de desinstalacao.</param>
    public static RunOnceOutcome ScheduleUninstall(string? programName, string? command)
    {
        var nome = (programName ?? string.Empty).Trim();
        if (nome.Length == 0) nome = "Programa";

        // O nome do item nao pode ter caracteres que o registro recusa.
        var item = string.Join("_", nome.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Replace(" ", "_");
        if (item.Length == 0) item = "TranalUninstall";

        return Schedule("Tranal_" + item, command);
    }

    /// <summary>
    /// Os itens agendados que o Tranal criou.
    /// </summary>
    public static IReadOnlyList<string> List()
    {
        var itens = new List<string>();

        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(RunOncePath);
            using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, Microsoft.Win32.RegistryView.Default);
            using var key = baseKey.OpenSubKey(sub);
            if (key is null) return itens;

            foreach (var nome in key.GetValueNames())
            {
                if (nome.StartsWith("Tranal", StringComparison.OrdinalIgnoreCase))
                {
                    itens.Add(nome + " = " + (key.GetValue(nome) as string ?? string.Empty));
                }
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao listar as acoes agendadas: {ex.Message}");
        }

        return itens;
    }

    /// <summary>
    /// Cancela um item agendado.
    /// </summary>
    /// <param name="name">O nome do item.</param>
    public static bool Cancel(string? name)
    {
        var item = (name ?? string.Empty).Trim();
        if (item.Length == 0) return false;

        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(RunOncePath);
            using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, Microsoft.Win32.RegistryView.Default);
            using var key = baseKey.OpenSubKey(sub, writable: true);
            if (key is null) return false;

            if (!key.GetValueNames().Contains(item, StringComparer.OrdinalIgnoreCase)) return false;

            key.DeleteValue(item, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao cancelar a acao agendada: {ex.Message}");
            return false;
        }
    }
}
