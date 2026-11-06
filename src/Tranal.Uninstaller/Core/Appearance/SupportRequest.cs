using System;
using System.Text;
using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// O pedido de suporte: a mensagem que o usuario envia e o endereco para
/// onde ela vai.
///
/// O Revo Pro tem "Send feedback / Support online" no menu. Aqui o suporte
/// e o **repositorio publico do projeto**: nao existe servidor de suporte
/// do Tranal, e inventar um seria prometer um servico que nao existe.
///
/// A mensagem e montada com os dados do programa e o que a interface mostra,
/// para o usuario nao precisar escrever do zero.
/// </summary>
public static class SupportRequest
{
    /// <summary>
    /// O endereco onde o pedido e registrado.
    ///
    /// E o repositorio publico do projeto. A URL fica em **um** lugar: a
    /// janela de suporte, o item do menu e a linha de comando usam esta
    /// constante, e nao cada um a sua.
    /// </summary>
    public const string SupportUrl = "https://github.com/Matheus-araujo-dev/tranal-uninstaller/issues";

    /// <summary>
    /// O endereco do projeto, para o rodape da janela de suporte.
    /// </summary>
    public const string ProjectUrl = "https://github.com/Matheus-araujo-dev/tranal-uninstaller";

    /// <summary>
    /// O texto da mensagem, com os dados do programa.
    ///
    /// Nao inclui nada do ambiente do usuario alem do que ja esta na tela:
    /// nome, versao, empresa e o comando de desinstalacao. Nada de caminho de
    /// perfil, nome de conta ou lista de programas — um pedido de suporte nao
    /// precisa disso e o usuario nao precisa mandar.
    /// </summary>
    /// <param name="program">O programa sobre o qual e o pedido (pode ser nulo).</param>
    /// <param name="details">O que o usuario escreveu.</param>
    public static string BuildMessage(InstalledProgram? program, string? details = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine("### O que aconteceu");
        sb.AppendLine();
        sb.AppendLine(string.IsNullOrWhiteSpace(details)
            ? "(descreva aqui o que aconteceu)"
            : details.Trim());
        sb.AppendLine();

        if (program is not null)
        {
            sb.AppendLine("### Programa");
            sb.AppendLine();
            sb.AppendLine($"- Nome: {program.DisplayName}");
            if (!string.IsNullOrWhiteSpace(program.DisplayVersion)) sb.AppendLine($"- Versão: {program.DisplayVersion}");
            if (!string.IsNullOrWhiteSpace(program.Publisher)) sb.AppendLine($"- Empresa: {program.Publisher}");
            if (!string.IsNullOrWhiteSpace(program.UninstallString)) sb.AppendLine($"- Comando de desinstalação: `{program.UninstallString}`");
            sb.AppendLine();
        }

        sb.AppendLine("### Ambiente");
        sb.AppendLine();
        sb.AppendLine($"- Windows: {Environment.OSVersion.VersionString}");
        sb.AppendLine($"- Arquitetura: {(Environment.Is64BitOperatingSystem ? "64 bits" : "32 bits")}");
        sb.AppendLine($"- Versão do Tranal: {Version()}");

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// A versao do aplicativo, para o pedido de suporte.
    /// </summary>
    private static string Version()
    {
        var assembly = typeof(SupportRequest).Assembly;
        var version = assembly.GetName().Version;
        return version is null ? "desconhecida" : version.ToString();
    }

    /// <summary>
    /// O endereco do pedido, com o texto pronto na URL.
    ///
    /// O GitHub aceita o corpo do pedido pela URL; assim o usuario ve a
    /// mensagem ja escrita no navegador, e pode corrigir antes de enviar.
    /// </summary>
    public static string BuildUrl(InstalledProgram? program, string? details = null)
    {
        var title = program is null
            ? "Dúvida ou problema"
            : $"Dúvida ou problema: {program.DisplayName}";

        var body = BuildMessage(program, details);

        return SupportUrl + "?title=" + Uri.EscapeDataString(title) + "&body=" + Uri.EscapeDataString(body);
    }
}
