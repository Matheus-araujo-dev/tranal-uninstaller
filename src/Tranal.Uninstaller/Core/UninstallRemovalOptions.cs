namespace Tranal.Uninstaller.Core;

/// <summary>
/// Opcoes que governam uma remocao de sobras.
///
/// Os padroes reproduzem o comportamento medido do Revo Uninstaller Pro:
/// um backup e criado antes de remover, os arquivos e pastas vão para a
/// Lixeira em vez de sumir definitivamente, e o que estiver em uso e
/// adiado para o proximo reinicio do Windows.
/// </summary>
public sealed class UninstallRemovalOptions
{
    /// <summary>
    /// Cria um ponto de restauracao com o conteudo de tudo o que sera removido.
    /// Padrao: <c>true</c>.
    /// </summary>
    public bool Backup { get; init; } = true;

    /// <summary>
    /// Envia arquivos e pastas para a Lixeira do Windows em vez de apagar de
    /// forma definitiva. Padrao: <c>true</c>.
    /// </summary>
    public bool UseRecycleBin { get; init; } = true;

    /// <summary>
    /// Agenda para o proximo reinicio os itens que nao puderam ser removidos
    /// por estarem em uso. Padrao: <c>true</c>.
    /// </summary>
    public bool DeferToNextRestart { get; init; } = true;

    /// <summary>Nome exibido do ponto de restauracao criado.</summary>
    public string? RestorePointName { get; init; }

    /// <summary>Opcoes padrao, iguais ao comportamento do Revo Uninstaller Pro.</summary>
    public static UninstallRemovalOptions Default { get; } = new();

    /// <summary>
    /// Opcoes derivadas das preferencias do usuario. Resolvidas uma unica vez
    /// por operacao para que GUI e CLI nunca discordem sobre o que vai acontecer.
    /// </summary>
    public static UninstallRemovalOptions FromSettings(string? restorePointName = null)
    {
        var settings = Settings.AppSettingsStore.Current;

        return new UninstallRemovalOptions
        {
            Backup = settings.BackupBeforeRemoval,
            UseRecycleBin = settings.SendFilesToRecycleBin,
            DeferToNextRestart = settings.DeferRemovalToNextRestart,
            RestorePointName = restorePointName,
        };
    }
}
