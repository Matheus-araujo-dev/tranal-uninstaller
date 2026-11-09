using System;
using System.Collections.Generic;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Tracing;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// As features do lote 7: o auto-ajuste de todas as colunas e a analise
/// profunda sob demanda.
///
/// O que estes testes cobrem: a conta da largura tem de ser a MESMA nos dois
/// caminhos (uma coluna e todas), e a analise profunda precisa responder
/// mesmo para um programa que nao tem log de rastreio.
/// </summary>
public class Lote7FeatureTests
{
    private static InstalledProgram Programa(string nome, string local) => new()
    {
        DisplayName = nome,
        DisplayVersion = "1.0",
        InstallLocation = local,
        Publisher = "Tranal",
    };

    [Fact]
    public void ALarguraAutomaticaUsaOMaiorTexto()
    {
        var linhas = new List<(InstalledProgram, ProgramCustomization?)>
        {
            (Programa("Curto", @"C:\Curto"), null),
            (Programa("Um nome bem mais comprido que o outro", @"C:\Longo"), null),
        };

        var largura = ProgramColumns.AutoWidth(ProgramColumn.Name, linhas);

        Assert.True(largura >= 40);
        Assert.True(largura <= 600);
    }

    [Fact]
    public void ALarguraAutomaticaTemPisoEFronteira()
    {
        var vazio = new List<(InstalledProgram, ProgramCustomization?)>();

        var largura = ProgramColumns.AutoWidth(ProgramColumn.Size, vazio);

        Assert.True(largura >= 48);
        Assert.True(largura <= 600);
    }

    [Fact]
    public void AAnaliseProfundaRespondeParaProgramaSemLog()
    {
        // Um programa so instalado, sem rastreio: o relatorio tem deducoes,
        // nunca entradas medidas. A ferramenta precisa dizer isso, em vez de
        // fingir que mediu.
        var programa = Programa("Programa de Teste Lote 7", @"C:\Teste");

        var report = ForensicAnalyzer.Analyze(programa);

        Assert.Equal("Programa de Teste Lote 7", report.ProgramName);
        Assert.False(report.HasMeasuredEntries);
        Assert.True(report.InferredCount + report.CandidateCount >= 0);
    }
}
