using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O destravador de arquivos, o limpador de internet, o Drive Cleaner, o
/// conserto de atalhos, a limpeza de MRU e o catalogo de componentes
/// compartilhados.
/// </summary>
public class UnlockerAndCleanupTests : IDisposable
{
    private readonly string _temp;

    public UnlockerAndCleanupTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "tranal-limpeza-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
        InternetCleaner.ExceptionsPathOverride = Path.Combine(_temp, "excecoes.json");
    }

    public void Dispose()
    {
        InternetCleaner.ExceptionsPathOverride = null;

        try
        {
            if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
        }
        catch (IOException)
        {
            // Temporario do sistema: o proprio Windows limpa depois.
        }
    }

    // ------------------------------------------------------------------
    // Unlocker
    // ------------------------------------------------------------------

    [Fact]
    public void UnlockerIgnoraCaminhoInexistente()
    {
        var inexistente = Path.Combine(_temp, "nao-existe.txt");

        Assert.Null(Unlocker.Scan(inexistente));
        Assert.False(Unlocker.IsLocked(inexistente));
    }

    [Fact]
    public void UnlockerNaoConsideraArquivoLivreComoTravado()
    {
        var arquivo = Path.Combine(_temp, "livre.txt");
        File.WriteAllText(arquivo, "conteudo");

        Assert.Null(Unlocker.Scan(arquivo));
        Assert.False(Unlocker.IsLocked(arquivo));
    }

    [Fact]
    public void UnlockerDestravaArquivoQueNaoEstaEmUso()
    {
        var arquivo = Path.Combine(_temp, "destravado.txt");
        File.WriteAllText(arquivo, "conteudo");

        var resultado = Unlocker.Unlock(arquivo);

        Assert.True(resultado.Unlocked);
        Assert.False(resultado.DeferredToRestart);
        Assert.True(File.Exists(arquivo));
    }

    [Fact]
    public void UnlockerDetectaArquivoEmUso()
    {
        var arquivo = Path.Combine(_temp, "em-uso.txt");
        File.WriteAllText(arquivo, "conteudo");

        using var stream = new FileStream(arquivo, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.True(Unlocker.IsLocked(arquivo));

        var travado = Unlocker.Scan(arquivo);
        Assert.NotNull(travado);
        Assert.False(travado!.IsDirectory);
        Assert.Equal(Path.GetFullPath(arquivo), travado.Path);
    }

    // ------------------------------------------------------------------
    // Internet Cleaner
    // ------------------------------------------------------------------

    [Fact]
    public void InternetCleanerGuardaELeExcecoes()
    {
        Assert.True(InternetCleaner.AddException("www.exemplo.com.br"));
        Assert.True(InternetCleaner.IsExcepted("exemplo.com.br"));
        Assert.True(InternetCleaner.IsExcepted("https://www.exemplo.com.br/pagina"));

        var lista = InternetCleaner.LoadExceptions();
        Assert.Contains("exemplo.com.br", lista);

        Assert.True(InternetCleaner.RemoveException("www.exemplo.com.br"));
        Assert.False(InternetCleaner.IsExcepted("exemplo.com.br"));
    }

    [Fact]
    public void InternetCleanerNaoDuplicaExcecao()
    {
        Assert.True(InternetCleaner.AddException("exemplo.com"));
        Assert.False(InternetCleaner.AddException("exemplo.com"));
        Assert.Single(InternetCleaner.LoadExceptions());
    }

    [Fact]
    public void InternetCleanerIgnoraDominioVazio()
    {
        Assert.False(InternetCleaner.AddException("   "));
        Assert.False(InternetCleaner.AddException(null));
        Assert.Empty(InternetCleaner.LoadExceptions());
    }

    [Fact]
    public void InternetCleanerListaPerfisSemQuebrar()
    {
        var perfis = InternetCleaner.ListProfiles();

        Assert.NotNull(perfis);
        Assert.All(perfis, p => Assert.False(string.IsNullOrWhiteSpace(p.Browser)));
    }

    [Fact]
    public void InternetCleanerVarreUmNavegadorInexistente()
    {
        var itens = InternetCleaner.Scan("NavegadorQueNaoExiste");

        Assert.Empty(itens);
    }

    // ------------------------------------------------------------------
    // Drive Cleaner
    // ------------------------------------------------------------------

    [Fact]
    public void DriveCleanerTemCategoriasComPastas()
    {
        var categorias = DriveCleaner.Categories();

        Assert.NotEmpty(categorias);
        Assert.All(categorias, c => Assert.NotEmpty(c.Folders));
        Assert.All(categorias, c => Assert.False(string.IsNullOrWhiteSpace(c.Label)));
    }

    [Fact]
    public void DriveCleanerAgrupaPorCategoria()
    {
        var itens = new List<DriveCleanerItem>
        {
            new() { CategoryId = "a", Category = "A", Kind = "Arquivo", Path = "x", SizeBytes = 10 },
            new() { CategoryId = "a", Category = "A", Kind = "Arquivo", Path = "y", SizeBytes = 20 },
            new() { CategoryId = "b", Category = "B", Kind = "Arquivo", Path = "z", SizeBytes = 5 },
        };

        var resumo = DriveCleaner.Summary(itens);

        Assert.Equal(2, resumo.Count);
        Assert.Equal("A", resumo[0].Category);
        Assert.Equal(30, resumo[0].Bytes);
    }

    [Fact]
    public void DriveCleanerLimparSemNadaMarcadoNaoFazNada()
    {
        var relatorio = DriveCleaner.Clear(new List<DriveCleanerItem>());

        Assert.Empty(relatorio.Outcomes);
    }

    [Fact]
    public void DriveCleanerFormataTamanho()
    {
        Assert.Equal("0 B", ByteFormatter.Format(0));
        Assert.Equal("512 B", ByteFormatter.Format(512));
        Assert.Equal("1 KB", ByteFormatter.Format(1024));
        Assert.Equal("1.5 KB", ByteFormatter.Format(1536));
        Assert.Equal("1 MB", ByteFormatter.Format(1024 * 1024));
    }

    // ------------------------------------------------------------------
    // Atalhos
    // ------------------------------------------------------------------

    [Fact]
    public void ShortcutFixerTemLocaisConhecidos()
    {
        var locais = ShortcutFixer.Locations();

        Assert.NotEmpty(locais);
        Assert.All(locais, l => Assert.False(string.IsNullOrWhiteSpace(l.Location)));
    }

    [Fact]
    public void ShortcutFixerLeAlvoDeAtalhoQueNaoExiste()
    {
        var alvo = ShortcutFixer.ReadTarget(Path.Combine(_temp, "nao-existe.lnk"));

        Assert.Equal(string.Empty, alvo);
    }

    [Fact]
    public void ShortcutFixerNaoReparaAtalhoSemCandidato()
    {
        var issue = new ShortcutIssue
        {
            ShortcutPath = Path.Combine(_temp, "atalho.lnk"),
            Name = "atalho",
            Kind = ShortcutFixer.KindMissingTarget,
            CanRepair = false,
        };

        var resultado = ShortcutFixer.Repair(issue);

        Assert.False(resultado.Repaired);
        Assert.False(resultado.Removed);
    }

    // ------------------------------------------------------------------
    // MRU
    // ------------------------------------------------------------------

    [Fact]
    public void MruCleanerVarreSemQuebrar()
    {
        var itens = MruCleaner.Scan();

        Assert.NotNull(itens);
        Assert.All(itens, i => Assert.False(string.IsNullOrWhiteSpace(i.RegistryPath)));
    }

    [Fact]
    public void MruCleanerLimparSemNadaMarcadoNaoFazNada()
    {
        var relatorio = MruCleaner.Clear(new List<MruItem>());

        Assert.Empty(relatorio.Outcomes);
    }

    // ------------------------------------------------------------------
    // Componentes compartilhados
    // ------------------------------------------------------------------

    [Fact]
    public void SharedDllCatalogListaSemQuebrar()
    {
        var entradas = SharedDllCatalog.List();

        Assert.NotNull(entradas);
        Assert.All(entradas, e => Assert.False(string.IsNullOrWhiteSpace(e.Name)));
    }

    [Fact]
    public void SharedDllCatalogRecusaRemoverBibliotecaEmUso()
    {
        var entrada = new SharedDllEntry
        {
            Name = "exemplo.dll",
            Path = Path.Combine(_temp, "exemplo.dll"),
            ReferenceCount = 3,
            UsedBy = new[] { "Programa A", "Programa B" },
        };

        var resultado = SharedDllCatalog.Remove(entrada);

        Assert.False(resultado.Success);
        Assert.Contains("3", resultado.Message);
    }

    [Fact]
    public void SharedDllCatalogRecusaRemoverBibliotecaComContadorAlto()
    {
        var entrada = new SharedDllEntry
        {
            Name = "exemplo.dll",
            Path = Path.Combine(_temp, "exemplo.dll"),
            ReferenceCount = 2,
            UsedBy = Array.Empty<string>(),
        };

        var resultado = SharedDllCatalog.Remove(entrada);

        Assert.False(resultado.Success);
    }
}
