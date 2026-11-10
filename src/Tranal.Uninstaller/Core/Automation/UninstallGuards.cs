using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// O veredito de uma guarda de desinstalacao.
/// </summary>
public sealed class UninstallGuardResult
{
    /// <summary>True quando a desinstalacao pode continuar.</summary>
    public bool Allowed { get; init; } = true;

    /// <summary>O motivo da recusa (quando ha recusa).</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// True quando a guarda aceita continuar, mas pede um aviso ao usuario.
    /// </summary>
    public bool RequiresWarning { get; init; }

    /// <summary>O texto do aviso.</summary>
    public string Warning { get; init; } = string.Empty;
}

/// <summary>
/// As guardas que impedem uma desinstalacao problematica.
///
/// O Wise Uninstaller tem tres delas, e o Revo tambem se protege de
/// algumas situacoes:
///
/// - avisar antes de usar a desinstalacao forcada em vez da segura;
/// - recusar desinstalar o proprio desinstalador;
/// - avisar que ja existe uma desinstalacao em andamento.
///
/// A REGRA mora aqui, em um lugar so: quem desinstala consulta estas guardas
/// e nao reimplementa a protecao.
///
/// A guarda do proprio Tranal e a mais importante: desinstalar o programa
/// que esta desinstalando deixa o Windows em um estado quebrado.
/// </summary>
public static class UninstallGuards
{
    /// <summary>
    /// O marcador do proprio Tranal nos executaveis dele.
    /// </summary>
    public const string SelfMarker = "TranalUninstaller";

    /// <summary>
    /// A chave de registro que marca uma desinstalacao em andamento.
    /// </summary>
    public const string RunningKeyPath = @"HKEY_CURRENT_USER\Software\Tranal\Uninstaller";

    /// <summary>
    /// O nome do valor que guarda o processo em andamento.
    /// </summary>
    public const string RunningValueName = "RunningUninstall";

    /// <summary>
    /// Verifica as guardas para um programa.
    /// </summary>
    /// <param name="program">O programa a desinstalar.</param>
    /// <param name="forcado">True quando a desinstalacao forcada foi pedida.</param>
    public static UninstallGuardResult Check(InstalledProgram? program, bool forcado = false)
    {
        if (program is null)
        {
            return new UninstallGuardResult { Allowed = false, Reason = "Programa nao informado." };
        }

        var self = CheckSelf(program);
        if (!self.Allowed) return self;

        var running = CheckRunning();
        if (!running.Allowed) return running;

        if (forcado)
        {
            // A REGRA do aviso mora em OperationGuards: ele decide quando
            // ha o que avisar e o texto vem do catalogo de idioma. Aqui so
            // se traduz o veredito em "pode seguir, mas avisa".
            //
            // Sem desinstalador registrado a forcada nao e um plano B: e o
            // unico caminho, e nao ha o que avisar.
            var aviso = OperationGuards.AvisoDeForcadaAntesDaSegura(
                program, OperationGuards.SeguraJaTentada(program));

            if (aviso is null) return new UninstallGuardResult { Allowed = true };

            return new UninstallGuardResult
            {
                Allowed = true,
                RequiresWarning = true,
                Warning = aviso.Message,
            };
        }

        return new UninstallGuardResult { Allowed = true };
    }

    /// <summary>
    /// True quando o programa e o proprio Tranal (ou um dos seus executaveis).
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    public static bool IsSelf(InstalledProgram? program)
    {
        if (program is null) return false;

        var alvos = new List<string>();
        if (!string.IsNullOrWhiteSpace(program.InstallLocation)) alvos.Add(program.InstallLocation);
        if (!string.IsNullOrWhiteSpace(program.DisplayIcon)) alvos.Add(program.DisplayIcon);
        if (!string.IsNullOrWhiteSpace(program.UninstallString)) alvos.Add(program.UninstallString);

        foreach (var alvo in alvos)
        {
            if (alvo.IndexOf(SelfMarker, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return program.DisplayName.IndexOf("Tranal", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// A guarda que recusa desinstalar o proprio Tranal.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    public static UninstallGuardResult CheckSelf(InstalledProgram? program)
    {
        if (!IsSelf(program))
        {
            return new UninstallGuardResult { Allowed = true };
        }

        return new UninstallGuardResult
        {
            Allowed = false,
            Reason = "Este e o proprio Tranal Uninstaller. Desinstalar o desinstalador "
                     + "no meio da operacao deixa o sistema sem a ferramenta que faz a limpeza.",
        };
    }

    /// <summary>
    /// A guarda que avisa que ja existe uma desinstalacao em andamento.
    /// </summary>
    public static UninstallGuardResult CheckRunning()
    {
        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(RunningKeyPath);
            using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, Microsoft.Win32.RegistryView.Default);
            using var key = baseKey.OpenSubKey(sub);
            if (key is null) return new UninstallGuardResult { Allowed = true };

            var valor = key.GetValue(RunningValueName) as string;
            if (string.IsNullOrWhiteSpace(valor)) return new UninstallGuardResult { Allowed = true };

            return new UninstallGuardResult
            {
                Allowed = false,
                Reason = $"Ja existe uma desinstalacao em andamento ({valor}). "
                         + "Espere ela terminar antes de comecar outra.",
            };
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao verificar se ha desinstalacao em andamento: {ex.Message}");
            return new UninstallGuardResult { Allowed = true };
        }
    }

    /// <summary>
    /// Marca que uma desinstalacao comecou, para que a guarda veja.
    /// </summary>
    /// <param name="descricao">A descricao da operacao.</param>
    public static bool MarkRunning(string? descricao)
    {
        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(RunningKeyPath);
            using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, Microsoft.Win32.RegistryView.Default);
            using var key = baseKey.CreateSubKey(sub, writable: true);
            if (key is null) return false;

            key.SetValue(
                RunningValueName,
                string.IsNullOrWhiteSpace(descricao) ? DateTime.Now.ToString("O") : descricao,
                Microsoft.Win32.RegistryValueKind.String);

            return true;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao marcar a desinstalacao em andamento: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Executa a operacao com a marca de "desinstalacao em andamento".
    ///
    /// A marca e o que impede que DUAS desinstalacoes rodem ao mesmo tempo
    /// na mesma maquina: a outra janela (ou a outra instancia do Tranal) ve
    /// a marca e recusa. Sem a marca, a guarda nunca encontra nada — e a
    /// guarda que nunca encontra nada nao protege nada.
    ///
    /// A REGRA mora aqui, em um lugar so: quem desinstala chama este
    /// metodo e nao marca nem limpa a marca por conta propria.
    /// </summary>
    /// <typeparam name="T">O tipo do resultado da operacao.</typeparam>
    /// <param name="descricao">A descricao da operacao (o que esta rodando).</param>
    /// <param name="acao">A operacao a executar.</param>
    /// <returns>O resultado da operacao.</returns>
    public static T RunGuarded<T>(string? descricao, Func<T> acao)
    {
        if (acao is null) throw new ArgumentNullException(nameof(acao));

        MarkRunning(descricao);
        try
        {
            return acao();
        }
        finally
        {
            ClearRunning();
        }
    }

    /// <summary>
    /// Executa a operacao assincrona com a marca de "desinstalacao em
    /// andamento".
    ///
    /// E a mesma regra de <see cref="RunGuarded{T}(string, Func{T})"/>, para
    /// operacao que espera.
    /// </summary>
    /// <typeparam name="T">O tipo do resultado da operacao.</typeparam>
    /// <param name="descricao">A descricao da operacao (o que esta rodando).</param>
    /// <param name="acao">A operacao a executar.</param>
    /// <returns>O resultado da operacao.</returns>
    public static async Task<T> RunGuardedAsync<T>(string? descricao, Func<Task<T>> acao)
    {
        if (acao is null) throw new ArgumentNullException(nameof(acao));

        MarkRunning(descricao);
        try
        {
            return await acao().ConfigureAwait(false);
        }
        finally
        {
            ClearRunning();
        }
    }

    /// <summary>
    /// Limpa a marca de desinstalacao em andamento.
    /// </summary>
    public static bool ClearRunning()
    {
        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(RunningKeyPath);
            using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, Microsoft.Win32.RegistryView.Default);
            using var key = baseKey.OpenSubKey(sub, writable: true);
            if (key is null) return false;

            key.DeleteValue(RunningValueName, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao limpar a marca de desinstalacao em andamento: {ex.Message}");
            return false;
        }
    }
}
