using System;
using System.Collections.Generic;
using System.IO;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Discovery;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Lote 35: a lista que a pessoa usa todo dia.
///
/// A lista do Tranal ja ordenava por qualquer coluna (o clique no cabecalho)
/// e ja agrupava os programas novos. O que nao existia era a **memoria** da
/// grade: a largura que a pessoa arrastou, a posicao que ela escolheu e a
/// ordenacao que ela deixou sumiam quando o programa fechava.
///
/// E o **realce** do programa novo: o grupo existia, mas o programa
/// recem-instalado nao se destacava na lista.
///
/// Este arquivo mede os dois.
/// </summary>
public sealed class Lote35FeatureTests
{
    /// <summary>
    /// A ordem que a pessoa deixou vale, e as colunas novas vao para o fim.
    /// </summary>
    [Fact]
    public void AOrdemGuardadaVale()
    {
        var visiveis = new[]
        {
            ProgramColumn.Name,
            ProgramColumn.Size,
            ProgramColumn.Version,
            ProgramColumn.Company,
        };

        // A pessoa deixou o nome em segundo.
        var guardada = new[] { "Size", "Name" };
        var ordem = ProgramColumnMemory.Order(visiveis, guardada);

        Assert.Equal(new[] { ProgramColumn.Size, ProgramColumn.Name }, ordem.Take(2));

        // As colunas que apareceram depois vao para o fim, na ordem do catalogo.
        Assert.Equal(new[] { ProgramColumn.Version, ProgramColumn.Company }, ordem.Skip(2));

        // Sem memoria, a ordem do catalogo vale.
        Assert.Equal(visiveis, ProgramColumnMemory.Order(visiveis, null));
    }

    /// <summary>
    /// Uma coluna que a pessoa escondeu e depois mostrou de novo nao perde a
    /// posicao: e o nome dela que continua na memoria.
    /// </summary>
    [Fact]
    public void AColunaQueSumiuNaoQuebraAMemoria()
    {
        var visiveis = new[] { ProgramColumn.Name, ProgramColumn.Size };

        // "Version" nao existe mais na lista: a memoria ignora e segue.
        var ordem = ProgramColumnMemory.Order(visiveis, new[] { "Version", "Size" });

        Assert.Equal(new[] { ProgramColumn.Size, ProgramColumn.Name }, ordem);

        // Um nome de coluna que nem existe no catalogo tambem e ignorado.
        var ordem2 = ProgramColumnMemory.Order(visiveis, new[] { "ColunaQueNaoExiste" });
        Assert.Equal(visiveis, ordem2);
    }

    /// <summary>
    /// A largura que a pessoa deixou vale; a invalida e descartada.
    /// </summary>
    [Fact]
    public void ALarguraGuardadaVale()
    {
        var guardadas = new[]
        {
            new ProgramColumnWidth("Size", 321),
            new ProgramColumnWidth("Version", 3),
            new ProgramColumnWidth("Company", 99999),
        };

        var memoria = ProgramColumnMemory.Read(guardadas);

        Assert.Equal(321, memoria[ProgramColumn.Size]);

        // A largura de 3 pixels e a de 99999 nao chegam a memoria: um
        // arquivo de preferencia editado a mao nao pode estragar a grade.
        Assert.False(memoria.ContainsKey(ProgramColumn.Version));
        Assert.False(memoria.ContainsKey(ProgramColumn.Company));

        // E a largura final: a guardada vence o padrao.
        Assert.Equal(321, ProgramColumnMemory.WidthOf(ProgramColumn.Size, memoria, 95));
        Assert.Equal(95, ProgramColumnMemory.WidthOf(ProgramColumn.Date, memoria, 95));
    }

    /// <summary>
    /// A memoria vai para o arquivo de preferencias em texto legivel.
    /// </summary>
    [Fact]
    public void AMemoriaVaiParaOArquivoDePreferencias()
    {
        var colunas = new[] { ProgramColumn.Size, ProgramColumn.Name };
        Assert.Equal(new[] { "Size", "Name" }, ProgramColumnMemory.NamesOf(colunas));

        var larguras = new Dictionary<ProgramColumn, double>
        {
            [ProgramColumn.Size] = 200,
            [ProgramColumn.Name] = 250,
            [ProgramColumn.Version] = 10,
        };

        var gravadas = ProgramColumnMemory.WidthsOf(larguras);

        // A largura invalida nao chega ao arquivo.
        Assert.Equal(2, gravadas.Count);
        Assert.Equal("Size", gravadas[0].Column);
        Assert.Equal(200, gravadas[0].Width);
    }

    /// <summary>
    /// As preferencias tem os campos da memoria da grade.
    /// </summary>
    [Fact]
    public void AsPreferenciasGuardamAMemoriaDaGrade()
    {
        var settings = new Tranal.Uninstaller.Core.Settings.AppSettings();

        Assert.NotNull(settings.ColumnOrder);
        Assert.NotNull(settings.ColumnWidths);
        Assert.Equal(string.Empty, settings.SortColumn);
        Assert.True(settings.SortAscending);
    }

    /// <summary>
    /// A janela de "Novos Programas": o dia inteiro conta, como no Revo.
    /// </summary>
    [Fact]
    public void AJaneladeNovosProgramasContaODiaInteiro()
    {
        var corte = NewInstallsTracker.Cutoff(7);

        // O corte e a meia-noite: um programa instalado ontem continua novo
        // hoje de manha.
        Assert.Equal(0, corte.Hour);
        Assert.Equal(0, corte.Minute);
        Assert.Equal(0, corte.Second);

        var ontem = DateTime.Now.AddDays(-1);
        Assert.True(NewInstallsTracker.IsRecent(
            new InstalledProgram { DisplayName = "Ontem", InstallDateValue = ontem }, 7));

        var antigo = DateTime.Now.AddDays(-30);
        Assert.False(NewInstallsTracker.IsRecent(
            new InstalledProgram { DisplayName = "Antigo", InstallDateValue = antigo }, 7));

        // Sem data de instalacao, nao ha prova de que o programa e novo.
        Assert.False(NewInstallsTracker.IsRecent(new InstalledProgram { DisplayName = "Sem data" }, 7));
    }

    /// <summary>
    /// O realce do programa novo usa a MESMA regra do grupo.
    /// </summary>
    [Fact]
    public void ORealceDoNovoUsaAMesmaRegra()
    {
        var filtros = new ProgramFilterSet();

        var novo = new InstalledProgram
        {
            DisplayName = "Recem instalado",
            InstallDateValue = DateTime.Now.AddHours(-2),
        };

        var antigo = new InstalledProgram
        {
            DisplayName = "Antigo",
            InstallDateValue = DateTime.Now.AddDays(-60),
        };

        // Sem o pedido, nenhum realce.
        Assert.Null(filtros.HighlightColorOf(novo));

        filtros.HighlightNewPrograms = true;

        Assert.Equal(HighlightPalette.Accent, filtros.HighlightColorOf(novo));
        Assert.Null(filtros.HighlightColorOf(antigo));

        // A regra do grupo e a mesma: o que o realce marca, o grupo tambem.
        Assert.True(NewInstallsTracker.IsRecent(novo));
        Assert.False(NewInstallsTracker.IsRecent(antigo));
    }

    /// <summary>
    /// O mapa das colunas comeca depois das colunas de comando.
    ///
    /// A coluna das caixas de selecao e um comando, nao uma coluna do
    /// catalogo: quando ela contava no mapa, cada cabecalho mostrava o nome
    /// da coluna seguinte e cada coluna recebia a largura da outra.
    /// </summary>
    [Fact]
    public void OMapaDasColunasComecaDepoisDosComandos()
    {
        var visiveis = new[]
        {
            ProgramColumn.Name,
            ProgramColumn.Size,
            ProgramColumn.Version,
        };

        // A coluna de comando nao tem identificador: ela nao e uma coluna do
        // catalogo. A primeira coluna do catalogo vem depois dela.
        Assert.Null(ProgramColumnMemory.IdAt(0, visiveis, 1));
        Assert.Equal(ProgramColumn.Name, ProgramColumnMemory.IdAt(1, visiveis, 1));
        Assert.Equal(ProgramColumn.Size, ProgramColumnMemory.IdAt(2, visiveis, 1));
        Assert.Equal(ProgramColumn.Version, ProgramColumnMemory.IdAt(3, visiveis, 1));
        Assert.Null(ProgramColumnMemory.IdAt(4, visiveis, 1));

        // Sem coluna de comando, a grade comeca na primeira coluna.
        Assert.Equal(ProgramColumn.Name, ProgramColumnMemory.IdAt(0, visiveis, 0));
        Assert.Equal(ProgramColumn.Size, ProgramColumnMemory.IdAt(1, visiveis, 0));
        Assert.Equal(ProgramColumn.Version, ProgramColumnMemory.IdAt(2, visiveis, 0));

        // O catalogo inteiro: nenhuma coluna fica sem par.
        var catalogo = ProgramColumns.All.Select(c => c.Id).ToList();
        for (var i = 0; i < catalogo.Count; i++)
        {
            Assert.Equal(catalogo[i], ProgramColumnMemory.IdAt(i + 1, catalogo, 1));
        }
    }

    /// <summary>
    /// A janela mapeia as colunas depois dos comandos e move a coluna dentro
    /// da grade que ja esta na tela.
    /// </summary>
    [Fact]
    public void AJanelaMapeiaAsColunasDepoisDosComandos()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        // O mapa e da regra do nucleo: a janela nao decide a conta.
        Assert.Contains("ProgramColumnMemory.IdAt(i, visiveis, ProgramListLayout.CommandColumns)", codigo, StringComparison.Ordinal);

        // A coluna muda de lugar dentro da grade que esta na tela: e o que
        // faz o cabecalho e as linhas andarem juntos.
        Assert.Contains("private void MoverColunaNaGrade(ProgramColumn id, int passo)", codigo, StringComparison.Ordinal);
        Assert.Contains("grid.Columns.Remove(coluna);", codigo, StringComparison.Ordinal);
        Assert.Contains("grid.Columns.Insert(novo, coluna);", codigo, StringComparison.Ordinal);
    }

    /// <summary>
    /// A tela consome a memoria: a grade nasce com a largura guardada.
    /// </summary>
    [Fact]
    public void AGradeNasceComALarguraGuardada()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "ProgramColumnLayout.cs");

        // A regra mora no nucleo: a grade nao decide a largura, ela consome.
        Assert.Contains("ProgramColumnMemory.WidthOf(column, widths, WidthOf(column))", codigo, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyDictionary<ProgramColumn, double>? widths", codigo, StringComparison.Ordinal);
    }

    /// <summary>
    /// A janela principal aplica a memoria, move a coluna e guarda a
    /// ordenacao.
    /// </summary>
    [Fact]
    public void AJanelaPrincipalAplicaAMemoria()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        // A ordem e a largura guardadas entram na grade.
        Assert.Contains("ProgramColumnMemory.Order(", codigo, StringComparison.Ordinal);
        Assert.Contains("ProgramColumnLayout.Build(visiveis, template, checkTemplate, _columnWidths)", codigo, StringComparison.Ordinal);

        // A coluna pode ser movida, e o novo lugar fica guardado.
        Assert.Contains("private void MoveColumn(ProgramColumn id, int passo)", codigo, StringComparison.Ordinal);
        Assert.Contains("s.ColumnOrder = ProgramColumnMemory.NamesOf(_visibleColumns);", codigo, StringComparison.Ordinal);
        Assert.Contains("s.ColumnWidths = ProgramColumnMemory.WidthsOf(_columnWidths);", codigo, StringComparison.Ordinal);

        // A ordenacao tambem e lembrada.
        Assert.Contains("s.SortColumn = id.ToString();", codigo, StringComparison.Ordinal);
        Assert.Contains("private void ApplySavedSort()", codigo, StringComparison.Ordinal);
    }

    /// <summary>
    /// O painel lateral tem o botao do realce dos novos.
    /// </summary>
    [Fact]
    public void OPainelLateralTemOBotaoDoRealce()
    {
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Appearance", "SettingsSidebar.xaml");
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Appearance", "SettingsSidebar.xaml.cs");

        Assert.Contains("OptHighlightNewPrograms", xaml, System.StringComparison.Ordinal);
        Assert.Contains("HighlightNewProgramsCheck_Click", codigo, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// As chaves novas do catalogo existem nos dois idiomas.
    /// </summary>
    [Fact]
    public void AsChavesNovasEstaoNosDoisIdiomas()
    {
        var chaves = new[]
        {
            "Cmd.MoveColumn",
            "Cmd.MoveColumnLeft",
            "Cmd.MoveColumnRight",
            "Sidebar.Filter.HighlightNewPrograms",
        };

        foreach (var arquivo in new[] { "pt-BR.json", "en-US.json" })
        {
            var caminho = RepoFiles.Find("src", "Tranal.Uninstaller", "Lang", arquivo);
            Assert.NotNull(caminho);

            var texto = File.ReadAllText(caminho!);
            foreach (var chave in chaves)
            {
                Assert.True(texto.Contains("\"" + chave + "\"", System.StringComparison.Ordinal),
                    $"a chave '{chave}' falta em {arquivo}");
            }
        }
    }
}
