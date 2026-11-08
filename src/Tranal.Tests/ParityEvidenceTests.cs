using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// A tabela de paridade com o Revo Uninstaller Pro so vale se a evidencia
/// citada continuar no codigo.
///
/// Este teste confere a mesma coisa que
/// <c>tools/auditoria/verificar-paridade.ps1</c>: cada linha do dado
/// (<c>docs/auditoria/paridade-revo.json</c>) aponta para um arquivo e, as
/// vezes, para um simbolo. Se o arquivo sumiu ou o simbolo foi renomeado, a
/// linha esta Errada — e a tabela passa a prometer algo que o codigo nao faz.
///
/// A regra da auditoria e simples: "Medido vale. Inferido nao vale."
/// </summary>
public class ParityEvidenceTests
{
    [Fact]
    public void TodaLinhaDaParidadeTemEvidenciaNoCodigo()
    {
        var caminho = RepoFiles.Find("docs", "auditoria", "paridade-revo.json");
        Assert.NotNull(caminho);

        using var document = JsonDocument.Parse(File.ReadAllText(caminho!));
        var linhas = document.RootElement.GetProperty("linhas");

        Assert.True(linhas.GetArrayLength() >= 70,
            "a paridade do Revo Pro tem 70 funcionalidades medidas");

        foreach (var linha in linhas.EnumerateArray())
        {
            var id = linha.GetProperty("id").GetInt32();
            var status = linha.GetProperty("status").GetString() ?? string.Empty;
            var evidencia = linha.GetProperty("evidencia");

            Assert.True(
                evidencia.GetArrayLength() > 0,
                $"linha {id} ({status}) nao cita nenhuma evidencia");

            foreach (var item in evidencia.EnumerateArray())
            {
                var referencia = item.GetString() ?? string.Empty;
                Assert.False(string.IsNullOrWhiteSpace(referencia),
                    $"linha {id}: evidencia vazia");

                var partes = referencia.Split(new[] { "::" }, StringSplitOptions.None);
                var relativo = partes[0];
                var simbolo = partes.Length > 1 ? partes[1] : string.Empty;

                var arquivo = Localizar(relativo);
                Assert.True(arquivo is not null,
                    $"linha {id}: o arquivo '{relativo}' citado nao existe no codigo");

                if (simbolo.Length == 0) continue;

                var texto = File.ReadAllText(arquivo!);
                Assert.True(texto.Contains(simbolo, StringComparison.Ordinal),
                    $"linha {id}: o simbolo '{simbolo}' nao existe em '{relativo}'");
            }
        }
    }

    /// <summary>
    /// Localiza o arquivo citado: relativo a raiz do repositorio ou ao
    /// projeto grafico (que e onde mora o nucleo).
    /// </summary>
    private static string? Localizar(string relativo)
    {
        var peloRepo = RepoFiles.Find(relativo.Replace('/', Path.DirectorySeparatorChar));
        if (peloRepo is not null) return peloRepo;

        return RepoFiles.Find(
            "src", "Tranal.Uninstaller", relativo.Replace('/', Path.DirectorySeparatorChar));
    }
}
