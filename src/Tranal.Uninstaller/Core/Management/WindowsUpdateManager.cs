using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// Remove atualizacoes do Windows (artigos de base de conhecimento).
///
/// Item de alto impacto na auditoria binaria: HiBit, IObit e os demais
/// concorrentes offering "desinstalar atualizacoes do Windows" como uma
/// acao propria do desinstalador.
///
/// O catalogo ja existe em <c>Core/Discovery/WindowsFeatures.cs</c>
/// (<c>WindowsUpdate</c>, <c>ListInstalled</c>); aqui mora a **acao**: a
/// remocao, que e do Windows e nao do Tranal — por isso o comando e o
/// <c>wusa.exe</c>, e o resultado e lido do proprio codigo de saida.
/// </summary>
public static class WindowsUpdateManager
{
    /// <summary>
    /// Remove um artigo de base de conhecimento.
    /// </summary>
    /// <param name="kbArticle">O numero do artigo ("KB5031356" ou
    /// "5031356").</param>
    /// <param name="quiet">True para rodar sem interface (padrao: true).</param>
    public static ManagementResult Remove(string? kbArticle, bool quiet = true)
    {
        var numero = NumeroDoArtigo(kbArticle);
        if (numero.Length == 0)
            return ManagementResult.Fail("Informe o artigo de base de conhecimento (ex.: KB5031356).");

        var argumentos = new StringBuilder();
        argumentos.Append("/uninstall /kb:").Append(numero);
        argumentos.Append(quiet ? " /quiet /norestart" : string.Empty);

        var exitCode = -1;
        var saida = string.Empty;

        try
        {
            var inicio = new ProcessStartInfo
            {
                FileName = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "System32", "wusa.exe"),
                Arguments = argumentos.ToString(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var processo = Process.Start(inicio);
            if (processo is null)
                return ManagementResult.Fail("Nao foi possivel iniciar o wusa.exe.");

            saida = processo.StandardOutput.ReadToEnd();
            processo.WaitForExit();
            exitCode = processo.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return ManagementResult.Fail($"O Windows recusou a remocao: {ex.Message}", ex.NativeErrorCode);
        }
        catch (InvalidOperationException ex)
        {
            return ManagementResult.Fail($"O Windows recusou a remocao: {ex.Message}");
        }

        if (exitCode == 0)
            return ManagementResult.Ok($"KB{numero} removido.");

        if (exitCode == 3010)
            return ManagementResult.Ok($"KB{numero} removido (o Windows vai reiniciar).");

        if (exitCode == 2359302)
            return ManagementResult.Fail($"KB{numero} nao esta instalado (ou nao pode ser removido).", exitCode);

        return ManagementResult.Fail(
            $"O Windows nao removeu o KB{numero} (codigo {exitCode}).{(saida.Length > 0 ? " " + primeiroErro(saida) : string.Empty)}",
            exitCode);
    }

    /// <summary>
    /// Remove varios artigos, um a um, e devolve o resultado de cada um.
    /// </summary>
    /// <param name="kbArticles">Os artigos ("KB5031356;KB5034123").</param>
    public static IReadOnlyList<(string Artigo, ManagementResult Resultado)> RemoveMany(string? kbArticles)
    {
        var resultados = new List<(string, ManagementResult)>();

        if (string.IsNullOrWhiteSpace(kbArticles)) return resultados;

        foreach (var parte in kbArticles.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var artigo = parte.Trim();
            if (artigo.Length == 0) continue;

            resultados.Add((artigo, Remove(artigo)));
        }

        return resultados;
    }

    /// <summary>
    /// O numero do artigo, sem o prefixo "KB".
    /// </summary>
    /// <param name="kbArticle">O texto informado pelo usuario.</param>
    public static string NumeroDoArtigo(string? kbArticle)
    {
        if (string.IsNullOrWhiteSpace(kbArticle)) return string.Empty;

        var texto = kbArticle.Trim().ToUpperInvariant();
        if (texto.StartsWith("KB", StringComparison.Ordinal)) texto = texto[2..];

        return texto.All(char.IsDigit) && texto.Length > 0 ? texto : string.Empty;
    }

    /// <summary>
    /// As atualizacoes do Windows que o catalogo conhece.
    ///
    /// O catalogo mora em <c>WindowsFeatures</c> (a regra fica em um lugar so);
    /// aqui so se expoe o atalho.
    /// </summary>
    public static IReadOnlyList<WindowsUpdate> ListInstalled() =>
        WindowsUpdateCatalog.ListInstalled().Items;

    private static string primeiroErro(string saida)
    {
        foreach (var linha in saida.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var texto = linha.Trim();
            if (texto.Length > 0) return texto;
        }

        return string.Empty;
    }
}
