using Tranal.Uninstaller.Core.Settings;

namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// As opcoes de acessibilidade e de velocidade.
///
/// A acessibilidade e o conjunto de ajustes que fazem a interface funcionar
/// melhor para quem usa leitor de tela, precisa de foco visivel ou quer os
/// botoes com o papel padrao da janela.
///
/// A velocidade e o outro lado: quando o modo rapido esta ligado, as
/// verificações caras (a medicao do tamanho das pastas, por exemplo) sao
/// puladas — e o Tranal avisa que pulou, em vez de fingir que mediu.
///
/// A regra esta em um lugar so: este modulo sabe o que cada opcao faz. A
/// tela, as opcoes e a linha de comando leem estas mesmas regras.
/// </summary>
public static class AccessibilityOptions
{
    /// <summary>
    /// O modo de acessibilidade esta ligado?
    /// </summary>
    public static bool IsEnabled => AppSettingsStore.Current.AccessibilityMode;

    /// <summary>
    /// O leitor de tela deve ser avisado das mudancas da lista?
    ///
    /// E o que a propriedade <c>AutomationProperties</c> faz no WPF: ela
    /// avisa o leitor de tela sobre o que apareceu na tela.
    /// </summary>
    public static bool AnnounceChanges => IsEnabled;

    /// <summary>
    /// O foco visivel (o retangulo de foco) deve ser desenhado?
    /// </summary>
    public static bool ShowFocusVisuals => IsEnabled;

    /// <summary>
    /// O botao padrao da janela deve ser destacado?
    /// </summary>
    public static bool HighlightDefaultButton => IsEnabled;

    /// <summary>
    /// A escala da fonte da interface.
    /// </summary>
    public static double FontScale => IsEnabled ? 1.15 : 1.0;

    /// <summary>
    /// O modo rapido esta ligado?
    /// </summary>
    public static bool FastMode => AppSettingsStore.Current.FastMode;

    /// <summary>
    /// As verificacoes caras devem ser puladas?
    ///
    /// O que e caro de medir: o tamanho de cada pasta de instalacao. Sem
    /// essa medicao, a coluna de tamanho fica vazia — e o Tranal diz isso.
    /// </summary>
    public static bool SkipSlowChecks => FastMode;

    /// <summary>
    /// A descricao do estado, para a tela e para a linha de comando.
    /// </summary>
    public static string Describe() =>
        $"Acessibilidade: {(IsEnabled ? "ligada" : "desligada")}. "
        + $"Modo rapido: {(FastMode ? "ligado (as verificacoes lentas sao puladas)" : "desligado")}.";
}
