using System.IO;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Acesso aos arquivos do repositorio a partir da suite de testes.
///
/// A suite nao referencia o projeto grafico (WPF): o nucleo e compilado
/// tambem na CLI, e referenciar os dois criaria dois tipos "Core"
/// diferentes. Quando a verificacao precisa olhar a camada de interface, ela
/// le o fonte em disco.
/// </summary>
internal static class RepoFiles
{
    /// <summary>Localiza um arquivo do repositorio a partir da pasta de saida do teste.</summary>
    public static string? Find(params string[] parts)
    {
        var relative = Path.Combine(parts);

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate)) return candidate;

            directory = directory.Parent;
        }

        return null;
    }

    /// <summary>Le um arquivo do repositorio. Falha o teste se ele nao existir.</summary>
    public static string Read(params string[] parts)
    {
        var path = Find(parts);
        Assert.NotNull(path);
        return File.ReadAllText(path!);
    }

    /// <summary>
    /// O corpo de um metodo do fonte.
    ///
    /// As verificacoes de interface sao feitas sobre o fonte (a suite nao
    /// referencia o WPF). Ancorar a assercao no metodo evita que um "contem"
    /// solto passe por causa de outra ocorrencia da mesma frase no arquivo.
    /// </summary>
    public static string Metodo(string codigo, string nome)
    {
        // A definicao, e nao a chamada: um metodo chamado antes de ser
        // definido apareceria primeiro na busca por texto.
        var definicao = new System.Text.RegularExpressions.Regex(
            $@"(?:void|bool|string|int|MenuItem|ContextMenu|IReadOnlyList<[^>]+>|List<[^>]+>)\s+{System.Text.RegularExpressions.Regex.Escape(nome)}\s*\(",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        var achado = definicao.Match(codigo);
        Assert.True(achado.Success, $"definicao de '{nome}' nao encontrada");

        var abre = codigo.IndexOf('{', achado.Index);
        Assert.True(abre >= 0, $"corpo de '{nome}' nao encontrado");

        var profundidade = 0;
        for (var i = abre; i < codigo.Length; i++)
        {
            if (codigo[i] == '{') profundidade++;
            else if (codigo[i] == '}')
            {
                profundidade--;
                if (profundidade == 0) return codigo[achado.Index..(i + 1)];
            }
        }

        return codigo[achado.Index..];
    }
}
