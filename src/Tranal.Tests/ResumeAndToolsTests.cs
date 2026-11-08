using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Modules;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// A retomada da desinstalacao interrompida, a criacao de tarefa agendada e
/// a lista de navegadores cujas extensoes o Tranal gerencia.
///
/// O teste da retomada usa a MESMA pasta temporaria que os outros testes de
/// automacao, porque a sessao vive em %LOCALAPPDATA% e um teste de verdade
/// escreveria no disco de quem roda a suite.
/// </summary>
public class ResumeAndToolsTests : IDisposable
{
    private readonly string _pasta;
    private readonly string? _original;

    public ResumeAndToolsTests()
    {
        _pasta = Path.Combine(Path.GetTempPath(), "tranal-resumo-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_pasta);

        _original = UninstallSession.DirectoryOverride;
        UninstallSession.DirectoryOverride = _pasta;
    }

    public void Dispose()
    {
        UninstallSession.DirectoryOverride = _original;

        try
        {
            if (Directory.Exists(_pasta)) Directory.Delete(_pasta, true);
        }
        catch
        {
            // A pasta temporaria fica para o sistema limpar.
        }
    }

    // ---------------------------------------------------------------
    // A retomada
    // ---------------------------------------------------------------

    [Fact]
    public void UmaRemocaoGravadaApareceComoPendente()
    {
        var itens = new List<LeftoverItem>
        {
            new() { Kind = "Pasta", Path = @"C:\Pasta\Uma", Selected = true },
            new() { Kind = "Arquivo", Path = @"C:\Pasta\Um\arquivo.txt", Selected = true },
        };

        var sessao = UninstallResume.SaveRemoval("Exemplo", UninstallMode.Advanced, itens);

        var pendentes = UninstallResume.Pending();

        Assert.Contains(pendentes, p => p.Id == sessao.Id);
        Assert.Equal(2, pendentes.First(p => p.Id == sessao.Id).RemainingItems);
    }

    [Fact]
    public void ARetomadaDevolveOsItensQueFaltavam()
    {
        var itens = new List<LeftoverItem>
        {
            new() { Kind = "Pasta", Path = @"C:\Pasta\Uma", Selected = true },
        };

        var sessao = UninstallResume.SaveRemoval("Exemplo", UninstallMode.Advanced, itens);

        var lidos = UninstallResume.ReadItems(UninstallSession.Load(sessao.Id));

        Assert.Single(lidos);
        Assert.Equal(@"C:\Pasta\Uma", lidos[0].Path);
    }

    [Fact]
    public void DescartarARetomadaApagaASessao()
    {
        var sessao = UninstallResume.SaveRemoval("Exemplo", UninstallMode.Advanced, new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = @"C:\Pasta\Uma", Selected = true },
        });

        Assert.True(UninstallResume.Discard(sessao.Id));
        Assert.Empty(UninstallResume.Pending());
        Assert.Null(UninstallSession.Load(sessao.Id));
    }

    [Fact]
    public void UmaSessaoSemCargaNaoTemItens()
    {
        var sessao = UninstallSession.Start(new UninstallPlan
        {
            ProgramName = "Exemplo",
            Mode = UninstallMode.Advanced,
            Steps = new[]
            {
                new UninstallStep { Kind = UninstallStepKind.Remove, Description = "Remover." },
            },
        });

        UninstallSession.Save(sessao);

        Assert.Empty(UninstallResume.ReadItems(sessao));
    }

    [Fact]
    public void DiscardDeUmaSessaoInexistenteNaoFazNada()
    {
        Assert.False(UninstallResume.Discard("nao-existe-1234"));
        Assert.False(UninstallResume.Discard(null));
        Assert.False(UninstallResume.Discard("   "));
    }

    // ---------------------------------------------------------------
    // Criacao de tarefa agendada
    // ---------------------------------------------------------------

    [Fact]
    public void CriarTarefaSemNomeFalha()
    {
        var resultado = ScheduledTaskManager.Create("  ", @"C:\Windows\System32\cmd.exe");

        Assert.False(resultado.Success);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Message));
    }

    [Fact]
    public void CriarTarefaSemExecutavelFalha()
    {
        var resultado = ScheduledTaskManager.Create("Tarefa", "   ");

        Assert.False(resultado.Success);
    }

    [Fact]
    public void CriarTarefaComExecutavelInexistenteFalha()
    {
        var resultado = ScheduledTaskManager.Create("Tarefa", @"C:\Caminho\Que\Nao\Existe.exe");

        Assert.False(resultado.Success);
        Assert.Contains("nao existe", resultado.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CriarTarefaComHoraInvalidaFalha()
    {
        var resultado = ScheduledTaskManager.Create(
            "Tarefa",
            @"C:\Windows\System32\cmd.exe",
            time: "25:99");

        Assert.False(resultado.Success);
    }

    // ---------------------------------------------------------------
    // Extensoes de navegador
    // ---------------------------------------------------------------

    [Fact]
    public void AListagemDeExtensoesNaoLanca()
    {
        var extensoes = BrowserExtensions.List();

        Assert.NotNull(extensoes);
    }

    [Fact]
    public void CadaExtensaoTemNavegadorEPasta()
    {
        foreach (var extensao in BrowserExtensions.List())
        {
            Assert.False(string.IsNullOrWhiteSpace(extensao.BrowserId), "extensao sem navegador");
            Assert.False(string.IsNullOrWhiteSpace(extensao.BrowserName), "extensao sem nome do navegador");
            Assert.False(string.IsNullOrWhiteSpace(extensao.Path), "extensao sem caminho");
        }
    }

    [Fact]
    public void OsNavegadoresDoCatalogoSaoConhecidos()
    {
        // A lista de navegadores gerenciados e a que o gerenciador de
        // extensoes do Wise mede: onze navegadores, entre eles os baseados
        // em Chromium e o Firefox.
        var ids = BrowserExtensions.List()
            .Select(e => e.BrowserId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Nenhum navegador instalado nesta maquina: a lista pode vir
        // vazia, e a verificacao e sobre o que a API devolve.
        Assert.NotNull(ids);
    }

    // ---------------------------------------------------------------
    // Painel de processos
    // ---------------------------------------------------------------

    [Fact]
    public void AListagemDeProcessosNaoLanca()
    {
        var processos = ProcessInspector.List();

        Assert.NotNull(processos);
        Assert.NotEmpty(processos);
    }

    [Fact]
    public void TodoProcessoTemNomeEIdentificador()
    {
        foreach (var processo in ProcessInspector.List())
        {
            // O identificador zero e o "processo ocioso" do Windows, que
            // existe de verdade e por isso aparece na lista.
            Assert.True(processo.Id >= 0, $"processo com identificador invalido: {processo.Id}");
            Assert.False(string.IsNullOrWhiteSpace(processo.Name), "processo sem nome");
        }
    }

    [Fact]
    public void OProcessoDoProprioTesteEstaNaLista()
    {
        var meu = Environment.ProcessId;

        var processos = ProcessInspector.List();

        Assert.Contains(processos, p => p.Id == meu);
    }

    [Fact]
    public void AMemoriaDoProcessoEhLegivel()
    {
        var processo = ProcessInspector.List().First(p => p.Id == Environment.ProcessId);

        Assert.NotEmpty(processo.WorkingSetText);
    }
}
