using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// Extensao de markup para textos localizados: Text="{loc:Tr App.Title}".
///
/// Detalhe importante do WPF: o parser BAML so aceita um
/// <see cref="System.Windows.Data.BindingExpression"/> como valor de uma
/// DependencyProperty. Um <see cref="Binding"/> puro seria gravado como valor
/// literal e lancaria "'System.Windows.Data.Binding' nao e um valor valido para
/// a propriedade". A conversao Binding -&gt; BindingExpression e feita pelo
/// metodo interno Binding.GetBindingExpression (o mesmo que o {Binding} do WPF
/// usa por dentro), acessado por reflexao — com fallback seguro para texto
/// estatico caso a API interna mude.
///
/// O texto e reavaliado quando o idioma muda, sem recriar a janela.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public class LocExtension : MarkupExtension
{
    /// <summary>Metodo interno do WPF que converte Binding em BindingExpression.</summary>
    private static readonly MethodInfo? GetBindingExpressionMethod = typeof(Binding).GetMethod(
        "GetBindingExpression",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
        binder: null,
        types: new[] { typeof(DependencyObject) },
        modifiers: null);

    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    /// <summary>Chave do texto no catalogo de idiomas.</summary>
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key)) return string.Empty;

        var proxy = new LocalizedString(Key);
        var binding = new Binding(nameof(LocalizedString.Value))
        {
            Source = proxy,
            Mode = BindingMode.OneWay,
        };

        // O alvo e necessario para transformar o binding em expressao vinculavel.
        if (GetBindingExpressionMethod is not null
            && serviceProvider?.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget provideValueTarget
            && provideValueTarget.TargetObject is DependencyObject target)
        {
            try
            {
                var expression = GetBindingExpressionMethod.Invoke(binding, new object[] { target });
                if (expression is not null) return expression;
            }
            catch
            {
                // cai no fallback abaixo
            }
        }

        // Fallback: texto estatico (atualiza ao recriar a janela).
        return Loc.Get(Key);
    }
}

/// <summary>Alias curto para uso no XAML: xmlns:loc="clr-namespace:Tranal.Uninstaller.Ui".</summary>
public sealed class TrExtension : LocExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key) : base(key)
    {
    }
}
