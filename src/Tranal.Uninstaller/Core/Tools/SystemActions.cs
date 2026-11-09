using System;
using System.Diagnostics;
using System.IO;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// As ações que o Tranal abre no Windows em nome do usuário.
///
/// São três coisas que os concorrentes têm e que aqui viram funções com
/// resultado honesto (abriu ou não abriu):
///
///   - a Limpeza de Disco do Windows (o BCU abre direto);
///   - a pasta dos logs de remoção (o Ashampoo abre a pasta de logs);
///   - a página do fabricante do programa (o SoftOrganizer mostra o
///     "removal tool from the developer").
///
/// A REGRA mora aqui: quem chama não monta comando nem decide caminho.
/// </summary>
public static class SystemActions
{
    /// <summary>
    /// Abre a Limpeza de Disco do Windows.
    ///
    /// O "cleanmgr.exe" é o proprio do Windows: a Limpeza de Disco que o
    /// usuario ja conhece, com os arquivos temporários, a Reciclagem e os
    /// arquivos de atualização.
    /// </summary>
    /// <returns>True quando o programa foi aberto.</returns>
    public static bool AbrirLimpezaDeDisco()
    {
        return Abrir("cleanmgr.exe");
    }

    /// <summary>
    /// Abre a pasta onde o Tranal guarda os logs de remoção, no Explorer.
    ///
    /// A pasta pode ainda não existir (quando nada foi removido ainda): o
    /// Explorer é aberto na pasta que o Tranal usaria.
    /// </summary>
    /// <returns>True quando o Explorer foi aberto.</returns>
    public static bool AbrirPastaDeLogs()
    {
        var pasta = RemovalLog.LogDirectory;

        try
        {
            if (!Directory.Exists(pasta)) Directory.CreateDirectory(pasta);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao preparar a pasta de logs '{pasta}': {ex.Message}");
            return false;
        }

        return Abrir($"explorer.exe", $"\"{pasta}\"");
    }

    /// <summary>
    /// Abre a página do fabricante do programa.
    ///
    /// E o "removal tool from the developer": a pagina oficial do programa,
    /// onde esta a ferramenta de remocao do proprio fabricante. Quando o
    /// programa nao declara a pagina, devolve false — e o Tranal nao inventa
    /// um endereco.
    /// </summary>
    /// <param name="programa">O programa instalado.</param>
    /// <returns>True quando a pagina foi aberta.</returns>
    public static bool AbrirPaginaDoFabricante(InstalledProgram? programa)
    {
        var url = (programa?.UrlInfoAbout ?? string.Empty).Trim();
        if (url.Length == 0) return false;

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        return Abrir(url);
    }

    /// <summary>
    /// Agenda o reinicio da maquina.
    ///
    /// O prazo de 60 segundos nao e um detalhe: e o que da chance de o
    /// usuario cancelar o reinicio e de os programas salvarem o que estao
    /// escrevendo. O chamador decide se o reinicio acontece — aqui so
    /// ele e agendado.
    /// </summary>
    /// <param name="segundos">Quanto tempo ate o reinicio (minimo de 5).</param>
    /// <returns>True quando o reinicio foi agendado.</returns>
    public static bool AgentarReinicio(int segundos = 60)
    {
        if (segundos < 5) segundos = 5;

        try
        {
            var info = new ProcessStartInfo("shutdown.exe", $"/r /t {segundos}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var processo = Process.Start(info);
            return processo is not null;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao agendar o reinicio: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Abre um endereco ou programa com o programa padrao do Windows.
    /// </summary>
    /// <param name="arquivo">O executavel ou o endereco.</param>
    /// <param name="argumentos">Os argumentos (vazio quando nao ha).</param>
    private static bool Abrir(string arquivo, string argumentos = "")
    {
        try
        {
            var info = new ProcessStartInfo(arquivo, argumentos)
            {
                UseShellExecute = true,
            };

            using var processo = Process.Start(info);
            return processo is not null;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao abrir '{arquivo}': {ex.Message}");
            return false;
        }
    }
}
