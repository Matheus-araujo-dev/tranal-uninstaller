using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Quem usa um caminho: os programas instalados que ainda apontam para ele.
/// </summary>
public sealed class SharedReference
{
    /// <summary>O caminho analisado (arquivo ou pasta).</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Quantos programas instalados ainda referenciam o caminho.</summary>
    public int ReferenceCount { get; init; }

    /// <summary>Os nomes dos programas que referenciam o caminho.</summary>
    public IReadOnlyList<string> Owners { get; init; } = Array.Empty<string>();

    /// <summary>
    /// True quando o caminho e usado por mais de um programa: e um item
    /// compartilhado, e o Total Uninstall pergunta antes de remove-lo
    /// ("Remove shared file?").
    /// </summary>
    public bool IsShared => ReferenceCount > 1;

    /// <summary>
    /// True quando nenhum programa instalado referencia o caminho: ele e
    /// sobra de verdade.
    /// </summary>
    public bool IsOrphan => ReferenceCount == 0;
}

/// <summary>
/// Contador de referencia de itens compartilhados.
///
/// Um arquivo em "C:\Arquivos de Programas\Common Files" pode ser usado por
/// tres programas. Apagar esse arquivo no primeiro programa que for
/// desinstalado quebra os outros dois — e e por isso que os concorrentes
/// perguntam "Remove shared file?" antes de remover.
///
/// A REGRA fica aqui, em um lugar so: um caminho e compartilhado quando
/// **mais de um** programa instalado ainda aponta para ele. Quem consome
/// (a janela de remocao, a CLI, o relatorio) le esta contagem em vez de
/// recalcular o criterio por conta propria.
///
/// A contagem usa as tres referencias que o registro de instalacao registra:
/// a pasta de instalacao, o executavel do desinstalador e o icone exibido.
/// </summary>
public static class SharedReferenceCounter
{
    /// <summary>
    /// Analisa um caminho e diz quais programas instalados ainda o usam.
    ///
    /// </summary>
    /// <param name="path">O caminho a analisar (arquivo ou pasta).</param>
    /// <param name="programs">Os programas instalados.</param>
    public static SharedReference Analyze(string? path, IEnumerable<InstalledProgram>? programs)
    {
        var alvo = Normalizar(path);
        var donos = new List<string>();

        if (alvo.Length > 0 && programs is not null)
        {
            foreach (var programa in programs)
            {
                if (programa is null) continue;
                if (string.IsNullOrWhiteSpace(programa.DisplayName)) continue;

                if (Referencia(programa, alvo))
                {
                    var nome = programa.DisplayName.Trim();
                    if (!donos.Contains(nome, StringComparer.OrdinalIgnoreCase))
                        donos.Add(nome);
                }
            }
        }

        return new SharedReference
        {
            Path = alvo,
            ReferenceCount = donos.Count,
            Owners = donos,
        };
    }

    /// <summary>
    /// Analisa varios caminhos de uma vez.
    ///
    /// </summary>
    /// <param name="paths">Os caminhos a analisar.</param>
    /// <param name="programs">Os programas instalados.</param>
    public static IReadOnlyList<SharedReference> Analyze(
        IEnumerable<string>? paths,
        IEnumerable<InstalledProgram>? programs)
    {
        var resultados = new List<SharedReference>();
        if (paths is null) return resultados;

        foreach (var caminho in paths)
        {
            var alvo = Normalizar(caminho);
            if (alvo.Length == 0) continue;

            resultados.Add(Analyze(alvo, programs));
        }

        return resultados;
    }

    /// <summary>
    /// Os caminhos que sao compartilhados por mais de um programa.
    ///
    /// E a lista que a tela de remocao usa para avisar antes de apagar.
    ///
    /// </summary>
    /// <param name="paths">Os caminhos candidatos.</param>
    /// <param name="programs">Os programas instalados.</param>
    public static IReadOnlyList<SharedReference> Shared(
        IEnumerable<string>? paths,
        IEnumerable<InstalledProgram>? programs) =>
        Analyze(paths, programs).Where(r => r.IsShared).ToList();

    /// <summary>
    /// True quando o programa referencia o caminho.
    ///
    /// A referencia existe quando o caminho esta dentro da pasta de
    /// instalacao do programa, ou quando o executavel do desinstalador (ou o
    /// icone exibido) esta dentro do caminho.
    /// </summary>
    private static bool Referencia(InstalledProgram programa, string alvo)
    {
        var pasta = Normalizar(programa.InstallLocation);
        if (pasta.Length > 0 && EstaDentro(alvo, pasta)) return true;

        foreach (var comando in new[] { programa.UninstallString, programa.QuietUninstallString, programa.DisplayIcon })
        {
            if (string.IsNullOrWhiteSpace(comando)) continue;

            var exe = ExtrairExecutavel(comando);
            if (exe.Length == 0) continue;

            var exeNormalizado = Normalizar(exe);
            if (exeNormalizado.Length == 0) continue;

            if (EstaDentro(exeNormalizado, alvo)) return true;
            if (EstaDentro(alvo, exeNormalizado)) return true;
        }

        return false;
    }

    /// <summary>
    /// True quando <paramref name="caminho"/> esta dentro de
    /// <paramref name="pasta"/>.
    /// </summary>
    private static bool EstaDentro(string caminho, string pasta)
    {
        if (caminho.Length == 0 || pasta.Length == 0) return false;

        if (caminho.Equals(pasta, StringComparison.OrdinalIgnoreCase)) return true;

        return caminho.StartsWith(pasta.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extrai o executavel de uma linha de comando, entre aspas ou solto.
    /// </summary>
    private static string ExtrairExecutavel(string comando)
    {
        var texto = comando.Trim();

        if (texto.StartsWith("\"", StringComparison.Ordinal))
        {
            var fim = texto.IndexOf('"', 1);
            return fim > 1 ? texto[1..fim] : texto.Trim('"');
        }

        // Sem aspas, o executavel e o primeiro token: um caminho de programa
        // com espaco sem aspas e um caso que o proprio Windows trata mal, e
        // nao vale a pena adivinhar.
        var espaco = texto.IndexOf(' ');
        return espaco > 0 ? texto[..espaco] : texto;
    }

    /// <summary>
    /// Normaliza um caminho: sem aspas, sem barra final e com o separador do
    /// Windows.
    /// </summary>
    private static string Normalizar(string? caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho)) return string.Empty;

        var texto = caminho.Trim().Trim('"').Trim();
        if (texto.Length == 0) return string.Empty;

        try
        {
            texto = Path.GetFullPath(texto);
        }
        catch
        {
            // Caminho que o Windows nao consegue normalizar fica como esta.
        }

        return texto.TrimEnd('\\');
    }
}
