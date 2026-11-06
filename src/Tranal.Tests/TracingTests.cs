using System;
using System.IO;
using System.Linq;
using System.Threading;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Tracing;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Os testes do Lote B: log de rastreio com tipo de mudanca, uniao de logs,
/// politica de processos, watcher em tempo real, comparacao A×B, biblioteca de
/// snapshots, plano de remocao dirigido pelo log e analise forense.
/// </summary>
public sealed class TracingTests
{
    // ---------------------------------------------------------------------
    // #41 — Log de rastreio com tipo de mudanca, nome antigo e dado antigo
    // ---------------------------------------------------------------------

    [Fact]
    public void OLogNovoGuardaOTipoDeMudancaEONomeAntigoEReleTudoAoSalvar()
    {
        var carimbo = new DateTimeOffset(2026, 11, 5, 10, 22, 30, TimeSpan.Zero);

        var entrada = new TraceEntry
        {
            Path = @"C:\Program Files\Exemplo\app.exe",
            Kind = TraceChangeKind.Renamed,
            OldName = "app.exe",
            OldValue = @"C:\Temp\app.exe",
            NewValue = @"C:\Program Files\Exemplo\app.exe",
            Size = 2048,
            Timestamp = carimbo,
        };

        var log = TraceLogV2.Create("Exemplo", new[] { entrada });

        var arquivo = Path.Combine(Path.GetTempPath(), "tranal-trace-" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            var caminho = log.Save(arquivo);
            Assert.False(string.IsNullOrWhiteSpace(caminho));

            var relido = TraceLogV2.Load(caminho);
            Assert.NotNull(relido);
            Assert.Equal("Exemplo", relido!.ProgramName);
            Assert.Single(relido.Entries);

            var lida = relido.Entries[0];
            Assert.Equal(TraceChangeKind.Renamed, lida.Kind);
            Assert.Equal("app.exe", lida.OldName);
            Assert.Equal(@"C:\Temp\app.exe", lida.OldValue);
            Assert.Equal(@"C:\Program Files\Exemplo\app.exe", lida.NewValue);
            Assert.Equal(2048L, lida.Size!.Value);
            Assert.Equal(carimbo.UtcTicks, lida.Timestamp.UtcTicks);
        }
        finally
        {
            if (File.Exists(arquivo)) File.Delete(arquivo);
        }
    }

    [Fact]
    public void OLogNovoGuardaOTipoDeMudancaDeCadaEntrada()
    {
        var carimbo = DateTimeOffset.UtcNow;

        var entradas = new[]
        {
            new TraceEntry { Path = @"C:\A\criado.txt", Kind = TraceChangeKind.Created, Timestamp = carimbo },
            new TraceEntry { Path = @"C:\A\sumiu.txt", Kind = TraceChangeKind.Deleted, Timestamp = carimbo },
            new TraceEntry { Path = @"C:\A\trocado.txt", Kind = TraceChangeKind.Overwritten, Timestamp = carimbo },
            new TraceEntry { Path = @"C:\A\mystery.txt", Kind = TraceChangeKind.Unknown, Timestamp = carimbo },
        };

        var log = TraceLogV2.Create("Exemplo", entradas);
        var arquivo = Path.Combine(Path.GetTempPath(), "tranal-trace-" + Guid.NewGuid().ToString("N") + ".json");

        try
        {
            var relido = TraceLogV2.Load(log.Save(arquivo));
            Assert.NotNull(relido);

            var tipos = relido!.Entries.Select(e => e.Kind).ToList();
            Assert.Contains(TraceChangeKind.Created, tipos);
            Assert.Contains(TraceChangeKind.Deleted, tipos);
            Assert.Contains(TraceChangeKind.Overwritten, tipos);
            Assert.Contains(TraceChangeKind.Unknown, tipos);
        }
        finally
        {
            if (File.Exists(arquivo)) File.Delete(arquivo);
        }
    }

    // ---------------------------------------------------------------------
    // #44 — Unir logs do mesmo programa sem duplicar entrada
    // ---------------------------------------------------------------------

    [Fact]
    public void UnirDoisLogsDoMesmoProgramaNaoDuplicaEntradaIdentica()
    {
        var carimbo = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

        var primeiro = TraceLogV2.Create("Exemplo", new[]
        {
            new TraceEntry { Path = @"C:\A\um.txt", Kind = TraceChangeKind.Created, Timestamp = carimbo },
            new TraceEntry { Path = @"C:\A\dois.txt", Kind = TraceChangeKind.Created, Timestamp = carimbo.AddSeconds(1) },
        });

        var segundo = TraceLogV2.Create("Exemplo", new[]
        {
            // Identica a primeira entrada: nao pode duplicar.
            new TraceEntry { Path = @"C:\A\um.txt", Kind = TraceChangeKind.Created, Timestamp = carimbo },
            // Distinta: tem de entrar.
            new TraceEntry { Path = @"C:\A\tres.txt", Kind = TraceChangeKind.Created, Timestamp = carimbo.AddSeconds(2) },
        });

        var unido = TraceLogV2.Merge(new[] { primeiro, segundo });

        Assert.Equal(3, unido.Entries.Count);
        Assert.Equal(3, unido.Entries.Select(e => e.Path).Distinct().Count());
        Assert.Contains(unido.Entries, e => e.Path == @"C:\A\tres.txt");
    }

    [Fact]
    public void UnirLogsPreservaOProgramaEOcarimboDoLogMaisRecente()
    {
        var antigo = new TraceLogV2
        {
            Id = "antigo",
            ProgramName = "Exemplo Antigo",
            Timestamp = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero),
            Entries = Array.Empty<TraceEntry>(),
        };

        var novo = new TraceLogV2
        {
            Id = "novo",
            ProgramName = "Exemplo Novo",
            Timestamp = new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero),
            Entries = Array.Empty<TraceEntry>(),
        };

        var unido = TraceLogV2.Merge(new[] { antigo, novo });

        Assert.Equal("Exemplo Novo", unido.ProgramName);
        Assert.Equal(novo.Timestamp.UtcTicks, unido.Timestamp.UtcTicks);
    }

    // ---------------------------------------------------------------------
    // #48 — Politica de processos: a exclusao vence a inclusao
    // ---------------------------------------------------------------------

    [Fact]
    public void AExclusaoDeProcessoVenceAInclusao()
    {
        var politica = new TraceProcessPolicy();
        politica.Include("setup");
        politica.IncludePath(@"C:\Temp");
        politica.Exclude("setup");
        politica.ExcludePath(@"C:\Temp\Ruim");

        // Excluido por nome, mesmo tendo o mesmo nome incluido.
        Assert.False(politica.ShouldObserve("setup.exe", @"C:\Outro\setup.exe"));

        // Excluido por caminho, mesmo dentro de uma pasta incluida.
        Assert.False(politica.ShouldObserve("instalador.exe", @"C:\Temp\Ruim\instalador.exe"));

        // Incluido por caminho, dentro de uma pasta incluida e nao excluida.
        Assert.True(politica.ShouldObserve("instalador.exe", @"C:\Temp\instalador.exe"));
    }

    [Fact]
    public void SemInclusaoAPoliticaObservaTudoMenosOExcluido()
    {
        var politica = new TraceProcessPolicy();
        politica.Exclude("fundo.exe");

        Assert.True(politica.ShouldObserve("qualquer.exe", @"C:\Apps\qualquer.exe"));
        Assert.False(politica.ShouldObserve("fundo.exe", @"C:\Apps\fundo.exe"));
    }

    [Fact]
    public void APoliticaGuardaEOQueFoiIncluidoEExcluido()
    {
        var politica = new TraceProcessPolicy();
        politica.Include("setup.exe");
        politica.Exclude("fundo.exe");
        politica.IncludePath(@"C:\Apps");

        Assert.Equal("setup", politica.IncludedNames.Single());
        Assert.Equal("fundo", politica.ExcludedNames.Single());
        Assert.Equal(@"C:\Apps", politica.IncludedPaths.Single());
        Assert.True(politica.HasRestriction);
    }

    // ---------------------------------------------------------------------
    // #7 — Watcher em tempo real, sem driver
    // ---------------------------------------------------------------------

    [Fact]
    public void OWatcherLidaComAFaltaDaPastaSemLancarExcecao()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "tranal-pasta-inexistente-" + Guid.NewGuid().ToString("N"));

        WriteWatcher? watcher = null;

        var exception = Record.Exception(() => watcher = WriteWatcher.Iniciar(pasta));

        Assert.Null(exception);
        Assert.Null(watcher);

        watcher?.Dispose();
    }

    [Fact]
    public void OWatcherObservaArquivoCriadoNaPasta()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "tranal-watcher-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);

        WriteWatcher? watcher = null;

        try
        {
            watcher = WriteWatcher.Iniciar(pasta);
            Assert.NotNull(watcher);
            Assert.True(watcher!.EstaRodando);

            var arquivo = Path.Combine(pasta, "criado.txt");
            File.WriteAllText(arquivo, "conteudo");

            var achado = false;
            var limite = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < limite)
            {
                if (watcher.Entradas.Any(e => string.Equals(e.Path, arquivo, StringComparison.OrdinalIgnoreCase)))
                {
                    achado = true;
                    break;
                }

                Thread.Sleep(100);
            }

            Assert.True(achado,
                "o watcher deveria ter visto o arquivo criado. entradas=" + watcher.Entradas.Count +
                " erro=" + watcher.UltimoErro);
            Assert.Empty(watcher.UltimoErro);
        }
        finally
        {
            watcher?.Dispose();

            try { Directory.Delete(pasta, true); }
            catch { /* a pasta temporaria pode ja ter sumido */ }
        }
    }

    // ---------------------------------------------------------------------
    // #30 — Comparacao A×B: novos, removidos, modificados e inalterados
    // ---------------------------------------------------------------------

    [Fact]
    public void AComparacaoClassificaRemovidoEModificado()
    {
        var antes = new SnapshotState
        {
            Files = new[]
            {
                new SnapshotFileState { Path = @"C:\A\mantido.txt", SizeBytes = 10, ModifiedUtc = new DateTime(2026, 1, 1) },
                new SnapshotFileState { Path = @"C:\A\modificado.txt", SizeBytes = 10, ModifiedUtc = new DateTime(2026, 1, 1) },
                new SnapshotFileState { Path = @"C:\A\removido.txt", SizeBytes = 10, ModifiedUtc = new DateTime(2026, 1, 1) },
            },
        };

        var depois = new SnapshotState
        {
            Files = new[]
            {
                new SnapshotFileState { Path = @"C:\A\mantido.txt", SizeBytes = 10, ModifiedUtc = new DateTime(2026, 1, 1) },
                new SnapshotFileState { Path = @"C:\A\modificado.txt", SizeBytes = 99, ModifiedUtc = new DateTime(2026, 2, 2) },
                new SnapshotFileState { Path = @"C:\A\novo.txt", SizeBytes = 5, ModifiedUtc = new DateTime(2026, 2, 2) },
            },
        };

        var comparacao = SnapshotComparer.Compare(antes, depois);

        Assert.Single(comparacao.RemovedFiles);
        Assert.Equal(@"C:\A\removido.txt", comparacao.RemovedFiles[0].Path);

        Assert.Single(comparacao.ModifiedFiles);
        Assert.Equal(@"C:\A\modificado.txt", comparacao.ModifiedFiles[0].Path);

        Assert.Single(comparacao.NewFiles);
        Assert.Equal(@"C:\A\novo.txt", comparacao.NewFiles[0].Path);

        Assert.Single(comparacao.UnchangedFiles);
        Assert.Equal(@"C:\A\mantido.txt", comparacao.UnchangedFiles[0].Path);
    }

    [Fact]
    public void AComparacaoClassificaChaveNovaValorAlteradoEValorRemovido()
    {
        var antes = new SnapshotState
        {
            RegistryKeys = new[] { @"HKEY_LOCAL_MACHINE\Software\Antiga" },
            RegistryValues = new[]
            {
                new SnapshotRegistryValueState { Key = @"HKEY_LOCAL_MACHINE\Software\Comum", ValueName = "Instalado", Value = "1" },
                new SnapshotRegistryValueState { Key = @"HKEY_LOCAL_MACHINE\Software\Comum", ValueName = "Saiu", Value = "2" },
            },
        };

        var depois = new SnapshotState
        {
            RegistryKeys = new[] { @"HKEY_LOCAL_MACHINE\Software\Nova" },
            RegistryValues = new[]
            {
                new SnapshotRegistryValueState { Key = @"HKEY_LOCAL_MACHINE\Software\Comum", ValueName = "Instalado", Value = "9" },
                new SnapshotRegistryValueState { Key = @"HKEY_LOCAL_MACHINE\Software\Comum", ValueName = "Veio", Value = "3" },
            },
        };

        var comparacao = SnapshotComparer.Compare(antes, depois);

        Assert.Single(comparacao.NewRegistryKeys);
        Assert.Equal(@"HKEY_LOCAL_MACHINE\Software\Nova", comparacao.NewRegistryKeys[0]);

        Assert.Single(comparacao.RemovedRegistryKeys);
        Assert.Equal(@"HKEY_LOCAL_MACHINE\Software\Antiga", comparacao.RemovedRegistryKeys[0]);

        Assert.Single(comparacao.NewRegistryValues);
        Assert.Equal("Veio", comparacao.NewRegistryValues[0].ValueName);

        Assert.Single(comparacao.RemovedRegistryValues);
        Assert.Equal("Saiu", comparacao.RemovedRegistryValues[0].ValueName);

        Assert.Single(comparacao.ModifiedRegistryValues);
        Assert.Equal("Instalado", comparacao.ModifiedRegistryValues[0].ValueName);
        Assert.Equal("1", comparacao.ModifiedRegistryValues[0].OldValue);
        Assert.Equal("9", comparacao.ModifiedRegistryValues[0].NewValue);
    }

    // ---------------------------------------------------------------------
    // #31 — Biblioteca de snapshots nomeados
    // ---------------------------------------------------------------------

    [Fact]
    public void ABibliotecaDeSnapshotsCriaListaApagaECompara()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "tranal-snapshots-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);

        SnapshotLibrary.DirectoryOverride = pasta;

        try
        {
            var antes = SnapshotLibrary.Save(
                "antes",
                new SnapshotState
                {
                    Files = new[] { new SnapshotFileState { Path = @"C:\A\um.txt", SizeBytes = 1 } },
                },
                "instalacao do Exemplo");

            var depois = SnapshotLibrary.Save(
                "depois",
                new SnapshotState
                {
                    Files = new[]
                    {
                        new SnapshotFileState { Path = @"C:\A\um.txt", SizeBytes = 2 },
                        new SnapshotFileState { Path = @"C:\A\dois.txt", SizeBytes = 3 },
                    },
                });

            Assert.NotNull(antes);
            Assert.NotNull(depois);
            Assert.Equal("instalacao do Exemplo", antes!.Label);

            var lista = SnapshotLibrary.List();
            Assert.Equal(2, lista.Count);
            Assert.Contains(lista, m => m.Name == "antes");
            Assert.Contains(lista, m => m.Name == "depois");
            Assert.Equal(1, lista.First(m => m.Name == "antes").FileCount);

            var comparacao = SnapshotLibrary.Compare(antes.Id, depois!.Id);
            Assert.NotNull(comparacao);
            Assert.Single(comparacao!.ModifiedFiles);
            Assert.Single(comparacao.NewFiles);

            Assert.True(SnapshotLibrary.Delete(antes.Id));
            Assert.Single(SnapshotLibrary.List());
        }
        finally
        {
            SnapshotLibrary.DirectoryOverride = null;

            try { Directory.Delete(pasta, true); }
            catch { /* a pasta temporaria pode ja ter sumido */ }
        }
    }

    // ---------------------------------------------------------------------
    // #32 — Plano de remocao dirigido pelo log: so o que esta no log
    // ---------------------------------------------------------------------

    [Fact]
    public void OPlanoDeRemocaoDirigidoPeloLogSoContemOQueEstaNoLog()
    {
        var log = new MsiInstallLog
        {
            ProductCode = "{1234-5678}",
            Files = new[] { @"C:\Program Files\Exemplo\app.exe" },
            Directories = new[] { @"C:\Program Files\Exemplo" },
            RegistryEntries = new[] { new MsiRegistryEntry(@"Software\Exemplo", "Instalado") },
        };

        var plano = ForensicAnalyzer.BuildPlan(log);

        Assert.Equal(3, plano.Items.Count);
        Assert.Contains(plano.Items, i => i.Path == @"C:\Program Files\Exemplo\app.exe");
        Assert.Contains(plano.Items, i => i.Path == @"C:\Program Files\Exemplo");
        Assert.Contains(plano.Items, i => i.Path == @"Software\Exemplo");

        // Nada que o log nao mencionou: nem a pasta do programa, nem um
        // executavel parecidos pelo nome.
        Assert.DoesNotContain(plano.Items, i => i.Path.Contains("unins", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, plano.Items.Select(i => i.Path).Distinct().Count());
    }

    [Fact]
    public void OPlanoDeRemocaoDoLogDeRastreioSoContemOQueFoiCriado()
    {
        var log = TraceLogV2.Create("Exemplo", new[]
        {
            new TraceEntry { Path = @"C:\A\criado.txt", Kind = TraceChangeKind.Created },
            new TraceEntry { Path = @"C:\A\sumiu.txt", Kind = TraceChangeKind.Deleted },
            new TraceEntry { Path = @"C:\A\mystery.txt", Kind = TraceChangeKind.Unknown },
        });

        var plano = ForensicAnalyzer.BuildPlan(log);

        Assert.Single(plano.Items);
        Assert.Equal(@"C:\A\criado.txt", plano.Items[0].Path);
    }

    // ---------------------------------------------------------------------
    // #36 — Analise forense: a proveniencia e inferido, nunca medido
    // ---------------------------------------------------------------------

    [Fact]
    public void AAnaliseForenseMarcaAProcedenciaComoInferidoENuncaComoMedido()
    {
        var programa = new InstalledProgram
        {
            DisplayName = "Programa Que Nao Existe 98765",
            InstallLocation = @"C:\Program Files\Programa Que Nao Existe 98765",
            UninstallString = "\"C:\\Program Files\\Programa Que Nao Existe 98765\\unins000.exe\"",
            RegistryPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Uninstall\ProgramaQueNaoExiste",
        };

        var relatorio = ForensicAnalyzer.Analyze(programa);

        Assert.NotEmpty(relatorio.Entries);
        Assert.Equal(0, relatorio.MeasuredCount);
        Assert.False(relatorio.HasMeasuredEntries);
        Assert.True(relatorio.InferredCount > 0);
        Assert.All(relatorio.Entries, e => Assert.Equal(TraceProvenance.Inferred, e.Provenance));
    }

    [Fact]
    public void AAnaliseForenseMarcaOCandidatoComConfiancaMaisBaixa()
    {
        var programa = new InstalledProgram
        {
            DisplayName = "Programa Que Nao Existe 98765",
            InstallLocation = @"C:\Program Files\Programa Que Nao Existe 98765",
        };

        var relatorio = ForensicAnalyzer.Analyze(programa);

        var candidatos = relatorio.Entries.Where(e => e.IsCandidate).ToList();
        Assert.All(candidatos, e => Assert.Equal(LeftoverConfidence.Low, e.Confidence));
        Assert.All(candidatos, e => Assert.Equal(TraceProvenance.Inferred, e.Provenance));
    }

    // ---------------------------------------------------------------------
    // #42 — O log correspondente e uma opcao, nunca uma imposicao
    // ---------------------------------------------------------------------

    [Fact]
    public void OLogCorrelacionadoEOpcionalENaoImposto()
    {
        var programa = new InstalledProgram { DisplayName = "Exemplo" };

        var logDoPrograma = TraceLogV2.Create("Exemplo", new[]
        {
            new TraceEntry { Path = @"C:\Apps\Exemplo\app.exe", Kind = TraceChangeKind.Created },
        });

        var logDeOutro = TraceLogV2.Create("Outro Programa", new[]
        {
            new TraceEntry { Path = @"C:\Apps\Outro\app.exe", Kind = TraceChangeKind.Created },
        });

        var candidatos = ForensicAnalyzer.Correlate(programa, new[] { logDoPrograma, logDeOutro });

        Assert.Single(candidatos);
        Assert.Equal(logDoPrograma.Id, candidatos[0].Id);

        // A escolha e do usuario: o nucleo correlaciona e entrega os
        // candidatos, mas nao escolhe por ele.
        var escolhido = ForensicAnalyzer.SelectLog(candidatos, logDoPrograma.Id);
        Assert.NotNull(escolhido);
        Assert.Equal(logDoPrograma.Id, escolhido!.Id);

        Assert.Null(ForensicAnalyzer.SelectLog(candidatos, "identificador-que-nao-existe"));
    }

    [Fact]
    public void OLogCorrelacionaPeloCaminhoDeInstalacao()
    {
        var programa = new InstalledProgram
        {
            DisplayName = "Nome Diferente Do Programa",
            InstallLocation = @"C:\Apps\Exemplo",
        };

        var log = TraceLogV2.Create("Outro Nome", new[]
        {
            new TraceEntry { Path = @"C:\Apps\Exemplo\app.exe", Kind = TraceChangeKind.Created },
        });

        var candidatos = ForensicAnalyzer.Correlate(programa, new[] { log });

        Assert.Single(candidatos);
    }
}
