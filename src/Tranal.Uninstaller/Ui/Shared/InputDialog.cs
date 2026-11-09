using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Ui.Shared;

/// <summary>
/// Um dialogo que pede um ou mais textos.
///
/// Existe porque a regra do projeto e "nenhuma feature sem consumidor": o
/// Boot Center precisa que o usuario escolha o nome e o comando do item de
/// inicializacao, e nao ha tela para isso. Um dialogo com N campos cobre o
/// caso e nao vira uma tela por feature.
///
/// Os rotulos e os botoes vem do catalogo de idioma: o dialogo nao escreve
/// texto proprio.
/// </summary>
public sealed class InputDialog : Window
{
    private readonly List<TextBox> _campos = new();

    /// <summary>
    /// Cria o dialogo.
    /// </summary>
    /// <param name="title">O titulo da janela.</param>
    /// <param name="fields">Os campos: rotulo e valor inicial.</param>
    public InputDialog(string title, params (string Label, string Initial)[] fields)
    {
        Title = title;
        Width = 480;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;

        var painel = new StackPanel { Margin = new Thickness(16) };

        foreach (var campo in fields)
        {
            painel.Children.Add(new TextBlock
            {
                Text = campo.Label,
                Margin = new Thickness(0, 0, 0, 6),
                TextWrapping = TextWrapping.Wrap,
            });

            var caixa = new TextBox { Text = campo.Initial ?? string.Empty };
            _campos.Add(caixa);
            painel.Children.Add(caixa);
            painel.Children.Add(new FrameworkElement { Height = 10 });
        }

        var botoes = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var confirmar = new Button
        {
            Content = Loc.Get("Common.Ok"),
            Width = 96,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true,
        };

        confirmar.Click += (_, _) => { DialogResult = true; };

        var cancelar = new Button
        {
            Content = Loc.Get("Common.Cancel"),
            Width = 96,
            IsCancel = true,
        };

        botoes.Children.Add(confirmar);
        botoes.Children.Add(cancelar);
        painel.Children.Add(botoes);

        Content = painel;

        Loaded += (_, _) =>
        {
            if (_campos.Count > 0)
            {
                _campos[0].Focus();
                _campos[0].SelectAll();
            }
        };
    }

    /// <summary>Os valores digitados, na ordem dos campos.</summary>
    public IReadOnlyList<string> Values => _campos.Select(c => c.Text).ToList();

    /// <summary>
    /// O valor do primeiro campo.
    /// </summary>
    public string Value => _campos.Count > 0 ? _campos[0].Text : string.Empty;
}
