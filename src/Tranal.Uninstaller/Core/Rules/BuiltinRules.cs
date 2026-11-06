namespace Tranal.Uninstaller.Core.Rules;

/// <summary>
/// As regras que vem com o Tranal.
///
/// Sao poucas e sao todas de protecao: o que o Windows atualiza sozinho e
/// what o usuario nao pode perder. Todas podem ser desligadas no arquivo de
/// regras — elas ficam la como padrao, nao como imposicao.
///
/// A regra do sistema (pasta protegida, chave protegida) NAO mora aqui: ela
/// e do proprio Windows e o motor a aplica sempre, antes das regras do
/// arquivo.
/// </summary>
public static class BuiltinRules
{
    /// <summary>
    /// Os nomes protegidos por padrao.
    /// </summary>
    private static readonly (string Nome, string Nota)[] Protegidos =
    {
        ("Windows Security", "Recurso de seguranca do Windows: a desinstalacao dele deixa o sistema sem antimalware."),
        ("Microsoft Defender", "Antimalware do Windows: a desinstalacao dele deixa o sistema sem protecao."),
        ("Windows Update", "O Windows Update e quem mantem o sistema atualizado."),
        ("Windows Driver Update", "Componente do Windows Update que atualiza drivers de dispositivos."),
    };

    /// <summary>
    /// Cria um motor com as regras embutidas.
    /// </summary>
    public static RuleEngine Create()
    {
        var motor = new RuleEngine();

        foreach (var (nome, nota) in Protegidos)
        {
            motor.Add(new RemovalRule
            {
                Pattern = nome,
                Category = RuleCategory.Protect,
                Scope = RuleScope.General,
                Note = nota,
            });
        }

        return motor;
    }

    /// <summary>
    /// Os nomes protegidos por padrao (a lista, para a interface mostrar).
    /// </summary>
    public static IReadOnlyList<string> ProtectedNames => Protegidos.Select(p => p.Nome).ToList();
}
