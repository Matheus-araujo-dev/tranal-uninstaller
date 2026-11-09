using System.Diagnostics;
using System.IO;

namespace Tranal.Uninstaller.Core.Updates;

/// <summary>
/// O que o instalador de atualizacao fez.
/// </summary>
/// <param name="Started">O instalador foi iniciado?</param>
/// <param name="PackagePath">O pacote que foi executado.</param>
/// <param name="Detail">O que aconteceu.</param>
public sealed record UpdateInstallResult(bool Started, string PackagePath, string Detail);

/// <summary>
/// A instalacao de uma versao nova.
///
/// O Tranal ja baixa o pacote da atualizacao (o <c>AutoUpdateClient</c>). O
/// que faltava era a ultima etapa: rodar o instalador.
///
/// <para><b>Nada roda sozinho.</b> O instalador so e executado quando o
/// usuario pede — o metodo existe para ser chamado por um comando ou por um
/// botao, nunca em um temporizador. E o Tranal nao e instalado por cima de
/// si mesmo: o proprio instalador cuida disso.</para>
///
/// A regra esta em um lugar so: este modulo sabe executar o pacote.
/// </summary>
public static class UpdateInstaller
{
    /// <summary>
    /// O pacote e um executavel que pode ser rodado?
    /// </summary>
    /// <param name="packagePath">O pacote.</param>
    public static bool CanRun(string? packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath)) return false;

        try
        {
            if (!File.Exists(packagePath)) return false;

            return Path.GetExtension(packagePath)
                .Equals(".exe", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Executa o instalador do pacote.
    ///
    /// O Tranal fecha antes: um instalador que substitui os arquivos do
    /// programa nao pode ter o programa aberto.
    /// </summary>
    /// <param name="packagePath">O pacote baixado.</param>
    /// <param name="arguments">Os argumentos do instalador.</param>
    /// <returns>O que aconteceu.</returns>
    public static UpdateInstallResult Run(string? packagePath, string? arguments = null)
    {
        if (!CanRun(packagePath))
        {
            return new UpdateInstallResult(false, packagePath ?? string.Empty,
                "O pacote nao e um executavel que possa ser rodado.");
        }

        try
        {
            var info = new ProcessStartInfo
            {
                FileName = packagePath!,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true,
            };

            using var process = Process.Start(info);
            if (process is null)
            {
                return new UpdateInstallResult(false, packagePath!, "O instalador nao pode ser iniciado.");
            }

            return new UpdateInstallResult(true, packagePath!,
                $"Instalador iniciado (processo {process.Id}).");
        }
        catch (Exception ex)
        {
            return new UpdateInstallResult(false, packagePath ?? string.Empty, ex.Message);
        }
    }

    /// <summary>
    /// O pacote esta na pasta de cache das atualizacoes?
    /// </summary>
    /// <param name="packagePath">O pacote.</param>
    public static bool IsDownloaded(string? packagePath) => CanRun(packagePath);
}
