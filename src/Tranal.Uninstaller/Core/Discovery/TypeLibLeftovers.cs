using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Uma biblioteca de tipos (TypeLib) que sobrou de um programa.
/// </summary>
public sealed class TypeLibEntry
{
    /// <summary>O identificador da biblioteca ("{GUID}").</summary>
    public string Guid { get; init; } = string.Empty;

    /// <summary>A versao da biblioteca.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>A pasta (ou arquivo) que a biblioteca aponta.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>A descricao da biblioteca ("OLE Automation").</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>True quando o caminho apontado nao existe mais.</summary>
    public bool Missing { get; init; }
}

/// <summary>
/// As sobras que o Windows guarda nas bibliotecas de tipos (TypeLib).
///
/// Toda DLL que exporta componentes COM registra uma "biblioteca de tipos"
/// em <c>HKCR\TypeLib\{GUID}\{versao}</c>, apontando para o arquivo da DLL. Um
/// programa desinstalado deixa esse registro para tras: o GUID continua
/// registrado, mas o arquivo nao existe mais.
///
/// Isso e sobra de verdade — e e um dos rastros que os concorrentes medem
/// (Uninstalr: "Deteccao de sobras por TypeLib").
///
/// A leitura e somente leitura.
/// </summary>
public static class TypeLibLeftovers
{
    /// <summary>
    /// Todas as bibliotecas de tipos registradas.
    /// </summary>
    public static IReadOnlyList<TypeLibEntry> List()
    {
        var entradas = new List<TypeLibEntry>();

        try
        {
            using var raiz = Registry.ClassesRoot.OpenSubKey("TypeLib");
            if (raiz is null) return entradas;

            foreach (var guid in raiz.GetSubKeyNames())
            {
                using var versoes = raiz.OpenSubKey(guid);
                if (versoes is null) continue;

                foreach (var versao in versoes.GetSubKeyNames())
                {
                    using var versaoKey = versoes.OpenSubKey(versao);
                    if (versaoKey is null) continue;

                    // O valor padrao da chave de versao e a DESCRICAO da
                    // biblioteca ("OLE Automation"), nao o arquivo. O caminho
                    // do arquivo fica na subchave "0", dentro de "win32" ou
                    // "win64".
                    var descricao = versaoKey.GetValue(string.Empty) as string ?? string.Empty;
                    var caminho = LerCaminho(versaoKey);
                    if (caminho.Length == 0) continue;

                    entradas.Add(new TypeLibEntry
                    {
                        Guid = guid,
                        Version = versao,
                        Path = caminho.Trim(),
                        Description = descricao.Trim(),
                        Missing = !Existe(caminho.Trim()),
                    });
                }
            }
        }
        catch
        {
            // Sem permissao para ler as classes: sem leitura, sem drama.
        }

        return entradas;
    }

    /// <summary>
    /// As bibliotecas de tipos cujo arquivo nao existe mais.
    /// </summary>
    public static IReadOnlyList<TypeLibEntry> Leftovers() =>
        List().Where(e => e.Missing).ToList();

    /// <summary>
    /// As sobras que pertencem a um programa (pelo caminho apontado).
    /// </summary>
    /// <param name="programName">O nome do programa desinstalado.</param>
    public static IReadOnlyList<TypeLibEntry> LeftoversOf(string? programName)
    {
        if (string.IsNullOrWhiteSpace(programName)) return Array.Empty<TypeLibEntry>();

        var alvo = programName.Trim();

        return Leftovers()
            .Where(e => e.Path.Contains(alvo, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool Existe(string caminho)
    {
        if (caminho.Length == 0) return false;

        try
        {
            if (File.Exists(caminho)) return true;
            if (Directory.Exists(caminho)) return true;
        }
        catch
        {
            // Caminho invalido: conta como ausente.
        }

        return false;
    }

    /// <summary>
    /// Le o caminho do arquivo da biblioteca de tipos.
    ///
    /// O caminho esta em <c>0\win32</c> (ou <c>0\win64</c>): a subchave "0"
    /// guarda as versoes por arquitetura, e o valor padrao de cada uma e o
    /// caminho da biblioteca (.tlb ou .dll).
    ///
    /// O valor tem DUAS partes depois do caminho: a versao da biblioteca, no
    /// formato <c>...\vbscript.dll\2</c>. Sem tirar essa versao, o caminho
    /// parece nao existir e toda biblioteca do Windows vira "sobra".
    ///
    /// </summary>
    private static string LerCaminho(RegistryKey versaoKey)
    {
        try
        {
            using var zero = versaoKey.OpenSubKey("0");
            if (zero is null) return string.Empty;

            foreach (var arquitetura in new[] { "win64", "win32" })
            {
                using var chave = zero.OpenSubKey(arquitetura);
                if (chave is null) continue;

                var caminho = chave.GetValue(string.Empty) as string;
                if (string.IsNullOrWhiteSpace(caminho)) continue;

                return Normalizar(caminho);
            }
        }
        catch
        {
            // Subchave inacessivel: a biblioteca nao pode ser julgada.
        }

        return string.Empty;
    }

    /// <summary>
    /// Expande as variaveis de ambiente e tira a versao da biblioteca do fim
    /// do caminho.
    /// </summary>
    private static string Normalizar(string caminho)
    {
        var texto = caminho.Trim();

        try
        {
            texto = Environment.ExpandEnvironmentVariables(texto);
        }
        catch
        {
            // Variavel invalida: fica como esta.
        }

        // A versao da biblioteca vem depois da barra: "...\vbscript.dll\2".
        var ultimaBarra = texto.LastIndexOf('\\');
        if (ultimaBarra > 0 && ultimaBarra < texto.Length - 1)
        {
            var cauda = texto[(ultimaBarra + 1)..];
            if (cauda.Length > 0 && cauda.All(char.IsDigit))
                texto = texto[..ultimaBarra];
        }

        return texto.Trim();
    }
}
