using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O projeto grafico (WPF) nao tem "System.IO" implicito.
///
/// O SDK da os usings implicitos completos (com System.IO) para a CLI e para
/// a suite de testes, que compilam o Core por fonte compartilhada. No projeto
/// WPF o conjunto e menor: System, System.Collections.Generic, System.Linq,
/// System.Threading e System.Threading.Tasks.
///
/// Consequencia medida: um arquivo do Core que use Path., File. ou Directory.
/// sem declarar "using System.IO;" passa na suite — 429 testes verdes — e
/// NAO compila no aplicativo (6 erros CS0103). A suite nao enxerga esse
/// problema por construcao: ela nunca compila o projeto WPF.
///
/// Este teste existe para fechar a porta.
/// </summary>
public class ImplicitUsingsTests
{
    /// <summary>
    /// Tipos de System.IO que o projeto WPF nao tem por padrao. O casamento
    /// exige um ponto depois do nome, entao "PathUtil" (que e do Tranal) nao
    /// entra.
    /// </summary>
    private static readonly Regex UsoDeIo = new(
        @"\b(Path|File|Directory|FileInfo|DirectoryInfo|Stream|FileStream|StreamReader|StreamWriter)\s*\.",
        RegexOptions.CultureInvariant);

    [Fact]
    public void TodoArquivoDoProjetoGraficoQueUsaSystemIoDeclaraOUsing()
    {
        var raiz = RepoFiles.FindDirectory("src", "Tranal.Uninstaller");
        Assert.NotNull(raiz);

        var semUsing = new List<string>();

        foreach (var arquivo in Directory.EnumerateFiles(raiz!, "*.cs", SearchOption.AllDirectories))
        {
            // obj/ e bin/ nao fazem parte do codigo do projeto.
            if (arquivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                || arquivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;

            var texto = File.ReadAllText(arquivo);
            if (texto.Contains("global using System.IO;", StringComparison.Ordinal)) continue;

            var usaIo = false;
            var linhas = texto.Replace("\r\n", "\n").Split('\n');

            foreach (var linha in linhas)
            {
                var trimada = linha.TrimStart();
                if (trimada.StartsWith("//", StringComparison.Ordinal)) continue;
                if (trimada.StartsWith("*", StringComparison.Ordinal)) continue;

                foreach (Match achado in UsoDeIo.Matches(linha))
                {
                    // O uso ja qualificado (System.IO.Path) nao precisa do using.
                    var antes = linha[..achado.Index];
                    if (antes.EndsWith("System.IO.", StringComparison.Ordinal)) continue;

                    usaIo = true;
                    break;
                }

                if (usaIo) break;
            }

            if (!usaIo) continue;

            if (texto.Contains("using System.IO;", StringComparison.Ordinal)) continue;

            semUsing.Add(Path.GetRelativePath(raiz!, arquivo));
        }

        Assert.True(
            semUsing.Count == 0,
            "Estes arquivos usam Path/File/Directory sem declarar \"using System.IO;\". "
            + "A suite passa (o Core e compilado por fonte compartilhada, com o using implicito), "
            + "mas o aplicativo WPF NAO compila: " + string.Join(", ", semUsing));
    }

    /// <summary>
    /// O conjunto de usings implicitos do projeto WPF. Se o SDK mudar, este
    /// teste avisa — e a mudanca quebraria todos os arquivos do Core de uma
    /// vez, sem nenhum aviso.
    /// </summary>
    [Fact]
    public void OProjetoGraficoNaoTemSystemIoImplicito()
    {
        var gerado = RepoFiles.FindDirectory("src", "Tranal.Uninstaller", "obj");
        Assert.NotNull(gerado);

        var arquivos = Directory
            .EnumerateFiles(gerado!, "*GlobalUsings.g.cs", SearchOption.AllDirectories)
            .ToList();

        // Sem obj/ gerado (build limpo ainda nao rodou) o teste nao tem o que
        // conferir — e nao e motivo para reprovar.
        if (arquivos.Count == 0) return;

        foreach (var arquivo in arquivos)
        {
            var texto = File.ReadAllText(arquivo);
            Assert.DoesNotContain("global using System.IO;", texto);
        }
    }
}
