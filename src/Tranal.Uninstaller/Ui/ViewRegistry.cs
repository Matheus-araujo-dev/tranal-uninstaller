using System.Windows;
using System.Windows.Controls;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// Registro das telas de modulo e ferramenta. A MainWindow pede a tela pelo id
/// (o mesmo id usado na Tag dos botoes da barra lateral) e nao conhece a
/// implementacao concreta: assim nenhum modulo precisa conhecer a janela.
/// </summary>
public static class ViewRegistry
{
    private static readonly Dictionary<string, Func<FrameworkElement>> Factories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registra a fabrica de uma tela.</summary>
    public static void Register(string id, Func<FrameworkElement> factory) => Factories[id] = factory;

    /// <summary>Cria a tela do id informado (null quando o id nao existe).</summary>
    public static FrameworkElement? Create(string id) =>
        Factories.TryGetValue(id, out var factory) ? factory() : null;

    /// <summary>Ids registrados (usado nos testes e na verificacao de paridade).</summary>
    public static IReadOnlyCollection<string> RegisteredIds => Factories.Keys;
}
