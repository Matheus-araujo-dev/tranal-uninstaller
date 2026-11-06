using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// Um pacote de driver instalado na maquina.
/// </summary>
public sealed class DriverPackage
{
    /// <summary>Nome publicado do pacote (o <c>oem12.inf</c>).</summary>
    public string PublishedName { get; init; } = string.Empty;

    /// <summary>Nome original do driver, quando o Windows informa.</summary>
    public string OriginalName { get; init; } = string.Empty;

    /// <summary>Fabricante do driver.</summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>Classe do dispositivo (Display, Net, System…).</summary>
    public string ClassName { get; init; } = string.Empty;

    /// <summary>Versao do driver.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Quem assinou o driver.</summary>
    public string SignerName { get; init; } = string.Empty;

    /// <summary>Data do pacote, quando o Windows informa.</summary>
    public string Date { get; init; } = string.Empty;

    /// <summary>True quando o pacote parece pertencer ao Windows.</summary>
    public bool IsSystemPackage { get; init; }
}

/// <summary>
/// Gerenciador de drivers: o que o item 19 do backlog chama de "remocao
/// definitiva de drivers".
///
/// A listagem vem do <c>pnputil.exe</c>, que e a ferramenta nativa do Windows
/// para os pacotes de driver da *Driver Store*. A saida dela esta no idioma do
/// sistema, mas o **nome publicado** (<c>oemNN.inf</c>) tem formato fixo — e o
/// que a remocao usa. Por isso o parser se agarra ao nome e nao aos rotulos.
///
/// Remover um driver e uma operacao de consequences: o dispositivo que depende
/// dele deixa de funcionar. Por isso o gerenciador exige confirmacao explicita
/// e recusa os pacotes do proprio Windows.
/// </summary>
public static class DriverManager
{
    /// <summary>O nome publicado tem o formato <c>oemNN.inf</c>.</summary>
    private static readonly Regex PublishedNamePattern = new(@"\boem\d+\.inf\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Os fabricantes cujo driver faz parte do Windows.
    ///
    /// O criterio e o **fabricante**, e nao o assinante: quase todo driver de
    /// terceiro e assinado pela "Microsoft Windows Hardware Compatibility
    /// Publisher" (o WHCP assina para todo mundo), entao usar o assinante
    /// classificaria o driver da NVIDIA como do Windows — e o painel
    /// protegeria justamente o driver que o usuario quer remover.
    /// </summary>
    private static readonly string[] SystemProviders = { "Microsoft" };

    /// <summary>Todos os pacotes de driver instalados.</summary>
    public static IReadOnlyList<DriverPackage> List()
    {
        var (code, output) = RunPnputil("/enum-drivers");
        if (code != 0) return Array.Empty<DriverPackage>();

        return Parse(output);
    }

    /// <summary>
    /// Converte a saida do <c>pnputil</c> em pacotes.
    ///
    /// A saida tem um formato de tabela; cada bloco comeca pelo nome publicado.
    /// O parser localiza os nomes publicados e usa as linhas seguintes como
    /// os demais campos, sem depender do rotulo em portugues ou ingles.
    /// </summary>
    internal static IReadOnlyList<DriverPackage> Parse(string output)
    {
        var packages = new List<DriverPackage>();
        if (string.IsNullOrWhiteSpace(output)) return packages;

        var lines = output.Split('\n');
        var current = new List<string>();
        var currentName = string.Empty;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0) continue;

            var match = PublishedNamePattern.Match(line);
            if (match.Success)
            {
                if (currentName.Length > 0)
                    packages.Add(Build(currentName, current));

                currentName = match.Value;
                current = new List<string>();
                var before = line.Substring(0, match.Index).Trim();
                if (before.Length > 0) current.Add(before);
                continue;
            }

            if (currentName.Length > 0) current.Add(line);
        }

        if (currentName.Length > 0) packages.Add(Build(currentName, current));

        return packages;
    }

    /// <summary>Remove um pacote de driver da Driver Store.</summary>
    public static ManagementResult Delete(DriverPackage package)
    {
        if (package is null) return ManagementResult.Fail("Pacote de driver nao informado.");
        if (package.IsSystemPackage)
            return ManagementResult.Fail(
                $"O pacote '{package.PublishedName}' pertence ao Windows e nao e removido pelo Tranal.");

        var (code, output) = RunPnputil($"/delete-driver {package.PublishedName} /uninstall /force");
        if (code == 0)
            return ManagementResult.Ok($"O driver '{package.PublishedName}' foi removido do sistema.");

        return ManagementResult.Fail(
            $"Nao foi possivel remover o driver '{package.PublishedName}' (codigo {code}). {output}".Trim());
    }

    private static DriverPackage Build(string publishedName, List<string> fields)
    {
        // A ordem dos campos do pnputil e fixa; o que muda e o rotulo, que
        // acompanha o idioma do sistema. Por isso o parser usa a **posicao**:
        //   0 = nome original
        //   1 = fabricante
        //   2 = classe do dispositivo
        //   3 = versao do driver
        //   4 = assinante
        //   5 = data
        var original = Field(fields, 0);
        var provider = Field(fields, 1);
        var className = Field(fields, 2);
        var version = Field(fields, 3);
        var signer = Field(fields, 4);
        var date = Field(fields, 5);

        var system = SystemProviders.Any(m =>
            provider.Contains(m, StringComparison.OrdinalIgnoreCase));

        return new DriverPackage
        {
            PublishedName = publishedName,
            ProviderName = provider,
            ClassName = className,
            Version = version,
            SignerName = signer,
            Date = date,
            OriginalName = original,
            IsSystemPackage = system,
        };
    }

    /// <summary>
    /// O n-esimo campo do bloco, descartando os pares "rotulo: valor".
    ///
    /// A saida do pnputil traz o rotulo na mesma linha do valor
    /// ("Nome do fornecedor : Microsoft"), que e o que se descarta aqui.
    /// </summary>
    private static string Field(List<string> fields, int index)
    {
        var values = fields
            .Select(f => f.Contains(':') ? f.Substring(f.IndexOf(':') + 1).Trim() : f)
            .Where(f => f.Length > 0)
            .ToList();

        return index < values.Count ? values[index] : string.Empty;
    }

    private static (int Code, string Output) RunPnputil(string arguments)
    {
        try
        {
            var info = new ProcessStartInfo("pnputil.exe", arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using var process = Process.Start(info);
            if (process is null) return (-1, "Nao foi possivel iniciar o pnputil.exe.");

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(30_000);

            var text = string.IsNullOrWhiteSpace(error) ? output : error;
            return (process.ExitCode, text.Trim());
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return (-1, ex.Message);
        }
    }
}
