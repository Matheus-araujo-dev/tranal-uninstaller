using System;
using System.IO;
using System.Text;
using System.Windows;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Ui;

/// <summary>
/// A assinatura digital do desinstalador do programa.
///
/// E o que o Wise chama de "Trust/Unknown" nos detalhes da entrada, e o que o
/// Soft Organizer verifica antes de rodar um executavel.
///
/// A REGRA da verificacao mora no nucleo (<see cref="UninstallerCertificate"/>):
/// ele le a cadeia do certificado e pergunta ao Windows se a assinatura e
/// confiavel. A tela so mostra o resultado.
///
/// A verificacao leva cerca de 160 ms por arquivo (MEDIDO), e por isso ela e
/// sob demanda: o usuario abre a tela do programa que quer ver.
/// </summary>
public sealed partial class UninstallerSignatureWindow : Window
{
    /// <summary>
    /// Abre a tela para o programa informado.
    /// </summary>
    /// <param name="programa">O programa cujo desinstalador sera verificado.</param>
    public UninstallerSignatureWindow(InstalledProgram programa)
    {
        InitializeComponent();

        Title = Loc.Get("Signature.Title");

        DetailBox.Text = programa is null
            ? Loc.Get("Signature.None")
            : Descrever(programa);
    }

    /// <summary>
    /// O texto da tela: o veredito, quem assinou e a cadeia.
    /// </summary>
    /// <param name="programa">O programa.</param>
    private static string Descrever(InstalledProgram programa)
    {
        var sb = new StringBuilder();

        var caminho = UninstallerExecutable.Resolve(programa);
        if (caminho.Length == 0)
            return Loc.Get("Signature.None");

        var assinatura = UninstallerCertificate.Verify(caminho);

        sb.AppendLine(Loc.Get("Signature.File") + ": " + caminho);
        sb.AppendLine(Loc.Get("Signature.Status") + ": " + Loc.Get("Signature.Status." + assinatura.Status));
        sb.AppendLine(Loc.Get("Signature.Signed") + ": " + (assinatura.IsSigned ? Loc.Get("Common.Yes") : Loc.Get("Common.No")));

        // O instalador do Windows e um caso especial: o executavel e do
        // Windows e a assinatura vem do catalogo de componentes do Windows,
        // nao de um editor de software. Dizer "sem assinatura digital" sem
        // essa nota seria enganoso.
        //
        // As duas notas sao cumulativas: uma explica a assinatura (o
        // catalogo de componentes do Windows) e a outra explica quem
        // desinstala (o instalador do Windows). Juntas elas dizem o que o
        // usuario precisa saber; sozinhas, cada uma deixa uma duvida.
        if (UninstallerExecutable.IsWindowsComponent(caminho))
        {
            sb.AppendLine();
            sb.AppendLine(Loc.Get("Signature.WindowsComponent"));
        }

        if (UninstallerExecutable.IsWindowsInstaller(programa))
        {
            sb.AppendLine();
            sb.AppendLine(Loc.Get("Signature.WindowsInstaller"));
        }

        if (assinatura.SignerSubject.Length > 0)
            sb.AppendLine(Loc.Get("Signature.Signer") + ": " + assinatura.SignerSubject);

        if (assinatura.SignerIssuer.Length > 0)
            sb.AppendLine(Loc.Get("Signature.Issuer") + ": " + assinatura.SignerIssuer);

        if (assinatura.NotAfter is not null)
        {
            sb.AppendLine(Loc.Get("Signature.Validity") + ": "
                + (assinatura.NotBefore?.ToString("yyyy-MM-dd") ?? string.Empty)
                + " a " + assinatura.NotAfter.Value.ToString("yyyy-MM-dd"));
        }

        if (assinatura.ChainSubjects.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(Loc.Get("Signature.Chain"));

            foreach (var sujeito in assinatura.ChainSubjects)
                sb.AppendLine("    " + sujeito);
        }

        if (assinatura.ChainProblems.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(Loc.Get("Signature.Problems"));

            foreach (var problema in assinatura.ChainProblems)
                sb.AppendLine("    " + problema);
        }

        // O arquivo do desinstalador pode ter sumido depois que o programa
        // foi lido do registro: e um caso que o usuario precisa ver.
        if (!File.Exists(caminho))
            sb.AppendLine(Loc.Get("Signature.FileMissing"));

        // O detalhe da falha vem do nucleo; a frase que o explica e
        // traduzida. A explicacao do estado ja esta na linha do estado
        // (e no catalogo) — repetir o texto do nucleo aqui seria repetir
        // a mesma informacao em duas linguas.
        if (assinatura.Detail.Length > 0)
            sb.AppendLine(Loc.Get("Signature.VerifyFailed", assinatura.Detail));

        return sb.ToString();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(DetailBox.Text ?? string.Empty);
            DetailBox.Focus();
        }
        catch (Exception)
        {
            // A area de transferencia pode estar ocupada por outro programa:
            // a tela continua aberta e o texto segue visivel.
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
