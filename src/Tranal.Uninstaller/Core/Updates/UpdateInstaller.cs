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
/// <para><b>Quando roda sozinho.</b> Pela DEC-APPS-DISTRIBUICAO-001, a
/// versao nova e instalada em modo silencioso (<see cref="SilentArguments"/>)
/// quando a preferencia "Atualizar automaticamente" esta ligada — e ela e o
/// padrao. Quem decide <i>quando</i> e o <see cref="UpdateService"/>
/// (<c>VerificarEInstalarAsync</c>), que nunca instala no meio de uma
/// desinstalacao. O instalador (Inno Setup, <c>CloseApplications=force</c>)
/// fecha o Tranal, troca os arquivos e o reabre no fim.</para>
///
/// A regra esta em um lugar so: este modulo sabe executar o pacote.
/// </summary>
public static class UpdateInstaller
{
    /// <summary>
    /// Os argumentos da instalacao silenciosa do Setup (Inno Setup), os mesmos
    /// em todos os apps Tranal: sem janela, sem caixa de mensagem, sem
    /// reiniciar o Windows, fechando o app que esta aberto.
    /// </summary>
    public const string SilentArguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS";

    /// <summary>
    /// Executa o Setup em modo silencioso (<see cref="SilentArguments"/>).
    /// </summary>
    /// <param name="packagePath">O Setup baixado.</param>
    public static UpdateInstallResult RunSilent(string? packagePath) => Run(packagePath, SilentArguments);

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
