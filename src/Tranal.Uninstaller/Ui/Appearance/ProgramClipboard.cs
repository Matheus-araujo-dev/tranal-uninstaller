using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;

namespace Tranal.Uninstaller.Ui.Appearance;

/// <summary>
/// A gravacao na area de transferencia do Windows.
///
/// A formatacao do texto mora no nucleo
/// (<see cref="ProgramTextFormatter"/>); aqui mora so a parte que e do
/// Windows — a area de transferencia e um recurso do sistema operacional, e o
/// nucleo nao depende dela.
///
/// A area de transferencia do Windows e **exclusiva**: outro programa pode
/// estar com ela trancada no instante em que o Tranal escreve. Por isso a
/// escrita tenta varias vezes, com uma espera curta entre elas — sem isso,
/// "copiar" falharia de vez em quando, sem que o usuario entendesse o
/// motivo.
/// </summary>
public static class ProgramClipboard
{
    /// <summary>Quantas vezes a escrita e tentada antes de desistir.</summary>
    private const int Attempts = 6;

    /// <summary>Espera entre as tentativas.</summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(60);

    /// <summary>
    /// Copia os programas para a area de transferencia.
    ///
    /// </summary>
    /// <param name="programs">Os programas a copiar.</param>
    /// <param name="format">O formato do texto.</param>
    /// <returns>True quando o texto foi gravado.</returns>
    public static bool Copy(IEnumerable<InstalledProgram> programs, ClipboardFormat format)
    {
        var text = ProgramTextFormatter.Format(programs, format);
        if (string.IsNullOrEmpty(text)) return false;

        return SetText(text);
    }

    /// <summary>
    /// Grava um texto na area de transferencia, tentando de novo se o Windows
    /// estiver com ela trancada.
    /// </summary>
    public static bool SetText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // A area de transferencia esta trancada por outro programa:
                // uma espera curta e a tentativa de novo.
                System.Threading.Thread.Sleep(RetryDelay);
            }
        }

        return false;
    }
}
