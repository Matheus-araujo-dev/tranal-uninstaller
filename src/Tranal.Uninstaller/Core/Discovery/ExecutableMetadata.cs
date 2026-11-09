using System.Diagnostics;
using System.IO;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Os dados que o executavel carrega sobre si.
/// </summary>
public sealed class ExecutableMetadataInfo
{
    /// <summary>O nome da empresa (o "publisher" do executavel).</summary>
    public string CompanyName { get; init; } = string.Empty;

    /// <summary>O nome do produto.</summary>
    public string ProductName { get; init; } = string.Empty;

    /// <summary>A descricao do arquivo.</summary>
    public string FileDescription { get; init; } = string.Empty;

    /// <summary>A versao do arquivo.</summary>
    public string FileVersion { get; init; } = string.Empty;

    /// <summary>O nome original do arquivo.</summary>
    public string OriginalFilename { get; init; } = string.Empty;

    /// <summary>O idioma do recurso de versao (hexadecimal, ex.: "0419").</summary>
    public string Language { get; init; } = string.Empty;

    /// <summary>True quando o executavel traz algum dado de empresa.</summary>
    public bool HasCompany => CompanyName.Length > 0;
}

/// <summary>
/// Le os dados do executavel (os recursos de versao do Windows).
///
/// A REGRA mora aqui, em um lugar so: a leitura dos recursos de versao do
/// arquivo acontece uma unica vez, neste modulo. O painel de processos, o
/// analisador de falhas, o gerenciador de autorun, o detector de pais e a
/// coluna de plataforma consomem o veredito daqui — nenhum deles abre o
/// recurso de versao por conta propria.
///
/// O Uninstalr mostra a empresa do manifesto do executavel quando o registro
/// nao informa a editora; o ashampoo tambem mostra a empresa e o nome do
/// programa a partir do proprio executavel.
/// </summary>
public static class ExecutableMetadataReader
{
    /// <summary>
    /// Le os dados do executavel. Devolve null quando o arquivo nao existe
    /// ou nao traz recursos de versao.
    /// </summary>
    /// <param name="path">O caminho do executavel.</param>
    public static ExecutableMetadataInfo? Read(string? path)
    {
        var full = PathUtil.Normalize(path);
        if (full.Length == 0) return null;

        try
        {
            if (!File.Exists(full)) return null;

            var info = FileVersionInfo.GetVersionInfo(full);

            var empresa = (info.CompanyName ?? string.Empty).Trim();
            var produto = (info.ProductName ?? string.Empty).Trim();
            var descricao = (info.FileDescription ?? string.Empty).Trim();
            var versao = (info.FileVersion ?? string.Empty).Trim();
            var original = (info.OriginalFilename ?? string.Empty).Trim();
            var idioma = (info.Language ?? string.Empty).Trim();

            if (empresa.Length == 0 && produto.Length == 0 && descricao.Length == 0) return null;

            return new ExecutableMetadataInfo
            {
                CompanyName = empresa,
                ProductName = produto,
                FileDescription = descricao,
                FileVersion = versao,
                OriginalFilename = original,
                Language = idioma,
            };
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler os dados de '{full}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// A empresa do executavel (o "publisher"), ou string vazia.
    /// </summary>
    /// <param name="path">O caminho do executavel.</param>
    public static string CompanyOf(string? path) => Read(path)?.CompanyName ?? string.Empty;

    /// <summary>
    /// O nome do produto do executavel, ou string vazia.
    /// </summary>
    /// <param name="path">O caminho do executavel.</param>
    public static string ProductOf(string? path) => Read(path)?.ProductName ?? string.Empty;

    /// <summary>
    /// A descricao do executavel, ou string vazia.
    /// </summary>
    /// <param name="path">O caminho do executavel.</param>
    public static string DescriptionOf(string? path) => Read(path)?.FileDescription ?? string.Empty;

    /// <summary>
    /// A versao do executavel, ou string vazia.
    /// </summary>
    /// <param name="path">O caminho do executavel.</param>
    public static string VersionOf(string? path) => Read(path)?.FileVersion ?? string.Empty;

    /// <summary>
    /// A empresa de um programa: a do registro quando ela existe, a do
    /// executavel quando o registro nao informa.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    public static string PublisherOf(InstalledProgram? program)
    {
        if (program is null) return string.Empty;

        if (!string.IsNullOrWhiteSpace(program.Publisher)) return program.Publisher.Trim();

        var executavel = UsageSignals.ExecutableOf(program);
        if (executavel.Length == 0) return string.Empty;

        return CompanyOf(executavel);
    }

    /// <summary>
    /// O nome do produto de um programa: o do registro quando ele existe, o
    /// do executavel quando o registro nao informa.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    public static string ProductNameOf(InstalledProgram? program)
    {
        if (program is null) return string.Empty;

        if (!string.IsNullOrWhiteSpace(program.DisplayName)) return program.DisplayName.Trim();

        var executavel = UsageSignals.ExecutableOf(program);
        if (executavel.Length == 0) return string.Empty;

        return ProductOf(executavel);
    }
}
