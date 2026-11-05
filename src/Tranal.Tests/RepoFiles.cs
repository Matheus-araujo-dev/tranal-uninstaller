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
}
