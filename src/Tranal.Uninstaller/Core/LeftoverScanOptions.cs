using Tranal.Uninstaller.Core.Settings;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Como a varredura de sobras deve ser feita.
///
/// Antes, o modo e o "marcar tudo" iam como parametros soltos na assinatura do
/// scanner. Com mais um comportamento (varredura em todas as contas do
/// Windows), quatro parametros posicionais booleanos ficaram impossiveis de
/// ler: qualquer chamada errada trocava o modo por um booleano. As opcoes
/// agora viajam como um unico objeto nomeado.
/// </summary>
public sealed record LeftoverScanOptions
{
    /// <summary>Profundidade da varredura (Somente, Seguro, Moderado, Avancado).</summary>
    public UninstallMode Mode { get; init; } = UninstallMode.Moderate;

    /// <summary>
    /// Marca todos os itens encontrados, inclusive os de confianca media e
    /// baixa. Corresponde a opcao "Marcar todos os itens restantes por padrao".
    /// </summary>
    public bool CheckAllLeftovers { get; init; }

    /// <summary>
    /// Procura sobras tambem nas outras contas do Windows (as chaves em
    /// HKEY_USERS e as pastas dos outros perfis). Corresponde a opcao "Scan all
    /// Windows accounts for leftovers" do Revo.
    /// </summary>
    public bool ScanAllUserAccounts { get; init; }

    /// <summary>
    /// Deixa marcados para remocao os itens que pertencem a outras contas.
    /// Sem esta opcao, a varredura em todas as contas e apenas informativa:
    /// os itens aparecem, mas nao vem marcados.
    /// Corresponde a opcao "Uninstall from all Windows accounts" do Revo.
    /// </summary>
    public bool SelectOtherAccountItems { get; init; }

    /// <summary>Opcoes padrao: varredura moderada, sem tocar em outras contas.</summary>
    public static LeftoverScanOptions Default { get; } = new();

    /// <summary>
    /// Opcoes derivadas das preferencias do usuario. Resolvidas uma unica vez
    /// por operacao para que a interface e a CLI nunca discordem sobre o que
    /// vai acontecer.
    /// </summary>
    public static LeftoverScanOptions FromSettings(UninstallMode mode)
    {
        var settings = AppSettingsStore.Current;

        return new LeftoverScanOptions
        {
            Mode = mode,
            CheckAllLeftovers = settings.CheckAllLeftoversByDefault,
            ScanAllUserAccounts = settings.ScanAllWindowsAccounts,

            // Marcar itens de outra conta sem varredura em todas as contas nao
            // tem efeito nenhum — e a combinacao nao deve existir. A
            // normalizacao mora aqui para que a interface e a CLI nunca
            // discordem sobre isso.
            SelectOtherAccountItems =
                settings.ScanAllWindowsAccounts && settings.UninstallFromAllWindowsAccounts,
        };
    }
}
