namespace Tranal.Uninstaller.Core;

/// <summary>
/// Modo de desinstalacao: a unica interpretacao canonica de "quanto fundo" a
/// remocao vai. Corresponde 1:1 aos quatro modos do Revo Uninstaller Pro.
///
/// O modo e a unica opcao que o usuario escolhe; toda a profundidade de
/// varredura deriva dele. Nao existe um segundo dial de escaneamento em outro
/// lugar do aplicativo: quem consome este enum e apenas decide o que fazer
/// com o resultado.
/// </summary>
public enum UninstallMode
{
    /// <summary>
    /// Somente o desinstalador nativo: nenhum rastro e procurado.
    /// Modo mais rapido e o unico que nao altera nada alem do proprio programa.
    /// </summary>
    BuiltIn = 0,

    /// <summary>
    /// Inclui o modo Built-in e procura apenas as sobras que sao seguras de
    /// apagar: a pasta de instalacao do programa e a sua chave de desinstalacao.
    /// E o modo mais rapido entre os que varrem o sistema.
    /// </summary>
    Safe = 1,

    /// <summary>
    /// Inclui o modo Safe e amplia a busca para os lugares mais comuns do
    /// Registro e do disco, casando tambem variantes conhecidas do nome.
    /// </summary>
    Moderate = 2,

    /// <summary>
    /// Inclui o modo Moderate e faz uma busca profunda e completa no Registro
    /// e no disco, incluindo o segundo nivel das arvores de fornecedores.
    /// E o modo mais lento e o que encontra mais sobras.
    /// </summary>
    Advanced = 3,
}

/// <summary>
/// Metadados dos modos de desinstalacao, usado pela GUI, pela CLI e pelo
/// relatorio final. A descricao e unica: nao ha texto duplicado por tela.
/// </summary>
public static class UninstallModes
{
    /// <summary>True quando o modo efetivamente varre o sistema em busca de sobras.</summary>
    public static bool Scans(UninstallMode mode) => mode != UninstallMode.BuiltIn;

    /// <summary>
    /// Chave do catalogo de idiomas com a descricao completa do modo.
    ///
    /// O texto mora **apenas** no catalogo (GUI e CLI leem o mesmo texto de
    /// <c>Wizard.Mode.*Desc</c>); o nucleo devolve a chave, nunca a frase.
    /// </summary>
    public static string DescriptionKey(UninstallMode mode) => mode switch
    {
        UninstallMode.BuiltIn => "Wizard.Mode.BuiltInDesc",
        UninstallMode.Safe => "Wizard.Mode.SafeDesc",
        UninstallMode.Moderate => "Wizard.Mode.ModerateDesc",
        UninstallMode.Advanced => "Wizard.Mode.AdvancedDesc",
        _ => string.Empty,
    };

    /// <summary>Nome curto do modo, como aparece na lista de selecao.</summary>
    public static string ShortName(UninstallMode mode) => mode switch
    {
        UninstallMode.BuiltIn => "Somente",
        UninstallMode.Safe => "Seguro",
        UninstallMode.Moderate => "Moderado",
        UninstallMode.Advanced => "Avancado",
        _ => mode.ToString(),
    };

    /// <summary>Todos os modos, na ordem em que devem ser apresentados ao usuario.</summary>
    public static IReadOnlyList<UninstallMode> All { get; } = new[]
    {
        UninstallMode.BuiltIn,
        UninstallMode.Safe,
        UninstallMode.Moderate,
        UninstallMode.Advanced,
    };
}
