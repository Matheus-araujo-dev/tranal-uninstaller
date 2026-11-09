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

    /// <summary>
    /// Encerra os executaveis do programa que esta sendo removido antes de
    /// apagar os arquivos deles. Corresponde a opcao "Forcibly stop running
    /// executable files during their uninstall" do Revo.
    /// Padrao: <c>false</c>.
    /// </summary>
    public bool KillRunningProcesses { get; init; }

    /// <summary>
    /// Cria um backup completo do registro (todos os hives) antes de remover.
    /// Corresponde a opcao "Create a full Registry Backup before uninstall"
    /// do Revo. Padrao: <c>false</c>.
    /// </summary>
    public bool FullRegistryBackup { get; init; }

    /// <summary>
    /// Mantem as preferencias do usuario (comentario, icone, grupos) do
    /// programa que foi desinstalado.
    ///
    /// Corresponde a pergunta do Revo ("Do you want to keep your
    /// preferences? Icons, comments, groups"). Padrao: <c>true</c>.
    /// </summary>
    public bool KeepPreferences { get; init; } = true;

    /// <summary>
    /// Preserva os arquivos que estao em pastas pessoais do usuario
    /// (documentos, imagens, videos, musicas, area de trabalho, downloads e
    /// favoritos).
    ///
    /// Corresponde ao aviso do IObit Uninstall antes de apagar um arquivo
    /// pessoal. Padrao: <c>true</c>.
    /// </summary>
    public bool ProtectPersonalFiles { get; init; } = true;

    /// <summary>
    /// O filtro de tipo de arquivo: as familias que a operacao PRESERVA.
    ///
    /// Nulo = sem filtro (o padrao): tudo o que esta marcado e removido.
    /// </summary>
    public Automation.FileTypeFilter? FileTypeFilter { get; init; }

    /// <summary>Nome exibido do ponto de restauracao criado.</summary>
    public string? RestorePointName { get; init; }

    /// <summary>Opcoes padrao, iguais ao comportamento do Revo Uninstaller Pro.</summary>
    public static UninstallRemovalOptions Default { get; } = new();

    /// <summary>
    /// Opcoes derivadas das preferencias do usuario. Resolvidas uma unica vez
    /// por operacao para que GUI e CLI nunca discordem sobre o que vai acontecer.
    /// </summary>
    /// <param name="restorePointName">Nome do ponto de restauracao a criar.</param>
    /// <param name="keepPreferences">
    /// Mantem as preferencias do programa desinstalado. Quando nulo, vale a
    /// preferencia do usuario; quando informado, vale a escolha da operacao
    /// (a caixa do assistente).
    /// </param>
    public static UninstallRemovalOptions FromSettings(string? restorePointName = null, bool? keepPreferences = null)
    {
        var settings = Settings.AppSettingsStore.Current;

        return new UninstallRemovalOptions
        {
            Backup = settings.BackupBeforeRemoval,
            UseRecycleBin = settings.SendFilesToRecycleBin,
            DeferToNextRestart = settings.DeferRemovalToNextRestart,
            KillRunningProcesses = settings.ForciblyStopRunningExecutables,
            FullRegistryBackup = settings.CreateFullRegistryBackupBeforeUninstall,
            KeepPreferences = keepPreferences ?? settings.KeepPreferencesOnUninstall,
            ProtectPersonalFiles = settings.ProtectPersonalFiles,
            RestorePointName = restorePointName,
            FileTypeFilter = Automation.FileTypeFilter.FromNames(settings.PreservedFileKinds),
        };
    }
}
