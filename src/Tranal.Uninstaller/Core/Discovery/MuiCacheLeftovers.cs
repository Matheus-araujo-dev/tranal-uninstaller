using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Uma entrada do MuiCache que aponta para um programa que nao existe mais.
/// </summary>
public sealed class MuiCacheEntry
{
    /// <summary>O caminho do executavel que o Windows guardou em cache.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>O nome do executavel, como o Windows gravou.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// O dado do valor: o nome amigavel que o Windows guardou ("SteelSeries
    /// GG Client"), quando o valor tem algum.
    /// </summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// True quando o executavel nao existe mais no disco: e sobra do
    /// programa, e o MuiCache ainda se lembra dele.
    /// </summary>
    public bool Missing { get; init; }
}

/// <summary>
/// As sobras que o Windows guarda no MuiCache.
///
/// O MuiCache e o cache do "Menu Iniciar" do Windows: cada vez que um
/// programa aparece na lista de aplicativos recentes, o Windows grava o
/// caminho dele em
/// <c>HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache</c>.
///
/// Um programa desinstalado deixa o registro para tras: o Windows continua
/// com o caminho guardado, o executavel nao existe mais. Isso e sobra de
/// verdade — e e um dos rastros que os concorrentes medem (Uninstalr: "Deteccao
/// de sobras por MuiCache").
///
/// A leitura e somente leitura: o Tranal **detecta** a sobra; a remocao
/// acontece pelo motor canonico de remocao.
/// </summary>
public static class MuiCacheLeftovers
{
    /// <summary>
    /// O caminho da chave do MuiCache da conta atual.
    /// </summary>
    public const string CachePath =
        @"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache";

    /// <summary>
    /// Todas as entradas do MuiCache da conta atual.
    /// </summary>
    public static IReadOnlyList<MuiCacheEntry> List()
    {
        var entradas = new List<MuiCacheEntry>();

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(CachePath);
            if (key is null) return entradas;

            foreach (var nome in key.GetValueNames())
            {
                // O CAMINHO esta no NOME do valor, e nao no dado: o Windows
                // grava "C:\...\app.exe.FriendlyAppName" como nome e o nome
                // amigavel do programa como dado. Ler o dado dava o nome do
                // programa ("SteelSeries A/S") em vez do caminho.
                var caminho = ExtrairCaminho(nome);
                if (caminho.Length == 0) continue;

                var dado = key.GetValue(nome) as string ?? string.Empty;

                entradas.Add(new MuiCacheEntry
                {
                    Path = caminho,
                    Name = nome,
                    Label = dado,
                    Missing = !File.Exists(caminho),
                });
            }
        }
        catch
        {
            // Chave protegida ou inexistente: sem leitura, sem drama.
        }

        return entradas;
    }

    /// <summary>
    /// As entradas cujo executavel nao existe mais: as sobras.
    /// </summary>
    public static IReadOnlyList<MuiCacheEntry> Leftovers() =>
        List().Where(e => e.Missing).ToList();

    /// <summary>
    /// As sobras que pertencem a um programa (pelo nome do executavel).
    /// </summary>
    /// <param name="programName">O nome do programa desinstalado.</param>
    public static IReadOnlyList<MuiCacheEntry> LeftoversOf(string? programName)
    {
        if (string.IsNullOrWhiteSpace(programName)) return Array.Empty<MuiCacheEntry>();

        var alvo = programName.Trim();

        return Leftovers()
            .Where(e => e.Name.Contains(alvo, StringComparison.OrdinalIgnoreCase)
                     || e.Path.Contains(alvo, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Os sufixos que o Windows acrescenta ao caminho no nome do valor do
    /// MuiCache. Sao metadados do programa (nome amigavel, empresa, icone) e
    /// nao fazem parte do caminho.
    /// </summary>
    private static readonly string[] SufixosConhecidos =
    {
        ".FriendlyAppName",
        ".ApplicationCompany",
        ".ApplicationName",
        ".ApplicationPath",
        ".ApplicationVersion",
        ".ApplicationIcon",
        ".ApplicationDescription",
    };

    /// <summary>
    /// Extrai o caminho do executavel do NOME do valor do MuiCache.
    ///
    /// O nome tem dois formatos medidos nesta maquina:
    ///
    ///   <c>C:\Program Files\App\app.exe.FriendlyAppName</c>
    ///   <c>C:\Program Files\App\app.exe,-12345</c>
    ///
    /// No primeiro, o sufixo e um metadado; no segundo, o numero depois da
    /// virgula e o "ID do recurso" do executavel. Nos dois, o caminho e o
    /// que vem antes.
    ///
    /// </summary>
    /// <param name="rawName">O nome bruto do valor.</param>
    public static string ExtrairCaminho(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return string.Empty;

        var texto = rawName.Trim();

        // O ID do recurso: ",<numero>" no fim do nome.
        var virgula = texto.IndexOf(",-", StringComparison.Ordinal);
        if (virgula < 0) virgula = texto.LastIndexOf(',');
        if (virgula > 0) texto = texto[..virgula];

        // O metadado: um dos sufixos conhecidos.
        foreach (var sufixo in SufixosConhecidos)
        {
            if (!texto.EndsWith(sufixo, StringComparison.OrdinalIgnoreCase)) continue;
            texto = texto[..^sufixo.Length];
            break;
        }

        texto = texto.Trim().Trim('"');

        // Um caminho do Windows tem barra invertida ou comeca com a letra da
        // unidade ("C:\..."). O criterio mais frouxo ("tem barra") aceitava o
        // nome de uma empresa ("SteelSeries A/S") como se fosse caminho.
        if (texto.Contains('\\')) return texto;
        if (texto.Length >= 3 && char.IsLetter(texto[0]) && texto[1] == ':' &&
            (texto[2] == '\\' || texto[2] == '/')) return texto;

        return string.Empty;
    }
}
