using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Tranal.Uninstaller.Core.Settings;

namespace Tranal.Uninstaller.Ui.Appearance;

/// <summary>
/// A animacao de texto da interface.
///
/// O Revo Pro tem a opcao "desativar animacoes de texto"; o Tranal tambem
/// (a preferencia <c>DisableTextAnimations</c>). Para que a preferencia
/// **signifique** alguma coisa, precisa existir uma animacao de texto que
/// ela desligue — e e este o unico lugar do projeto onde ha uma.
///
/// A animacao e um aparecimento suave do texto (opacidade de 0 a 1 em
/// 180 ms) quando o texto muda. Com a preferencia desligada, o texto
/// aparece na hora.
///
/// O controle usa uma **propriedade anexada**: basta por
/// <c>TextAnimation.Animate="True"</c> no XAML. Nenhum ponto do codigo
/// precisa lembrar de chamar a animacao — e o XAML que diz.
/// </summary>
public static class TextAnimation
{
    /// <summary>Quanto tempo dura o aparecimento.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(180);

    /// <summary>
    /// A propriedade anexada que liga a animacao em um controle.
    /// </summary>
    public static readonly DependencyProperty AnimateProperty =
        DependencyProperty.RegisterAttached(
            "Animate",
            typeof(bool),
            typeof(TextAnimation),
            new PropertyMetadata(false, OnAnimateChanged));

    /// <summary>True quando o controle anima o texto.</summary>
    public static bool GetAnimate(DependencyObject element) =>
        (bool)element.GetValue(AnimateProperty);

    /// <summary>Liga ou desliga a animacao no controle.</summary>
    public static void SetAnimate(DependencyObject element, bool value) =>
        element.SetValue(AnimateProperty, value);

    /// <summary>
    /// True quando a animacao de texto esta permitida agora.
    ///
    /// A preferencia e consultada no momento da animacao, e nao na montagem da
    /// tela: assim o usuario desliga e o efeito vale para a proxima mensagem,
    /// sem precisar reiniciar o aplicativo.
    /// </summary>
    public static bool IsAllowed => !AppSettingsStore.Current.DisableTextAnimations;

    private static void OnAnimateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock) return;

        if (e.NewValue is true)
        {
            var descriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
            descriptor?.AddValueChanged(textBlock, OnTextChanged);
        }
        else
        {
            var descriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
            descriptor?.RemoveValueChanged(textBlock, OnTextChanged);
        }
    }

    private static void OnTextChanged(object? sender, EventArgs e)
    {
        if (sender is not TextBlock textBlock) return;
        if (string.IsNullOrEmpty(textBlock.Text)) return;

        if (!IsAllowed)
        {
            // Sem animacao: o texto aparece inteiro, na hora.
            textBlock.BeginAnimation(UIElement.OpacityProperty, null);
            textBlock.Opacity = 1;
            return;
        }

        textBlock.BeginAnimation(UIElement.OpacityProperty, null);
        textBlock.Opacity = 0;

        var animation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = Duration,
            FillBehavior = FillBehavior.Stop,
        };

        animation.Completed += (_, _) => textBlock.Opacity = 1;

        textBlock.BeginAnimation(UIElement.OpacityProperty, animation);
    }
}
