using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Recovery;
using Tranal.Uninstaller.Core.Rules;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O lote C+D: motor de regras, historico de desinstalacoes, backup das
/// sobras, backup do programa com undo/redo, operacoes pendentes e backup
/// diario do registro.
///
/// Tudo toca apenas pastas temporarias: nenhum teste escreve no ambiente de
/// quem roda a suite.
/// </summary>
[Collection("preferencias")]
public class RecoveryTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly string _regras;

    public RecoveryTests()
    {
        _regras = Path.Combine(_temp.Root, "rules.json");

        RuleEngine.FilePathOverride = _regras;
        RuleEngine.FolderOverride = _temp.Root;
        UninstallHistory.FilePathOverride = Path.Combine(_temp.Root, "uninstalls.jsonl");
        LeftoverBackup.FolderOverride = Path.Combine(_temp.Root, "leftovers");
        ProgramBackup.FolderOverride = Path.Combine(_temp.Root, "programs");
        DailyRegistryBackup.MarkerPathOverride = Path.Combine(_temp.Root, "diario.txt");
        DailyRegistryBackup.DestinationOverride = Path.Combine(_temp.Root, "diario");
        FullRegistryBackup.HivesOverride = null;
    }

    public void Dispose()
    {
        RuleEngine.FilePathOverride = null;
        RuleEngine.FolderOverride = null;
        RuleEngine.ResetForTests();
        UninstallHistory.FilePathOverride = null;
        LeftoverBackup.FolderOverride = null;
        ProgramBackup.FolderOverride = null;
        DailyRegistryBackup.MarkerPathOverride = null;
        DailyRegistryBackup.DestinationOverride = null;
        FullRegistryBackup.HivesOverride = null;
        _temp.Dispose();
    }

    private static InstalledProgram Programa(string nome, string? local = null) => new()
    {
        DisplayName = nome,
        DisplayVersion = "1.0",
        Publisher = "Editora de Teste",
        InstallLocation = local ?? $@"C:\Program Files\{nome}",
        UninstallString = $@"C:\Program Files\{nome}\uninstall.exe",
    };

    // ---------------------------------------------------------------
    // #26 — Motor de regras
    // ---------------------------------------------------------------

    [Fact]
    public void AExcludeVenceOInclude()
    {
        var motor = RuleEngine.Default;
        motor.Add(new RemovalRule { Pattern = "Teste", Category = RuleCategory.Include });
        motor.Add(new RemovalRule { Pattern = "Teste", Category = RuleCategory.Exclude });

        Assert.Equal(RuleDecision.Exclude, motor.Decide(Programa("Programa Teste")));
    }

    [Fact]
    public void AProtectVenceOExclude()
    {
        var motor = RuleEngine.Default;
        motor.Add(new RemovalRule { Pattern = "Teste", Category = RuleCategory.Exclude });
        motor.Add(new RemovalRule { Pattern = "Teste", Category = RuleCategory.Protect });

        Assert.Equal(RuleDecision.Protect, motor.Decide(Programa("Programa Teste")));
    }

    [Fact]
    public void ARegraDoAplicativoVenceAGeral()
    {
        var motor = RuleEngine.Default;
        motor.Add(new RemovalRule { Pattern = "Teste", Category = RuleCategory.Include });
        motor.Add(new RemovalRule
        {
            Pattern = "Teste",
            Category = RuleCategory.Exclude,
            Scope = RuleScope.Application,
            Application = "Programa Teste",
        });

        Assert.Equal(RuleDecision.Exclude, motor.Decide(Programa("Programa Teste")));
        Assert.Equal(RuleDecision.Include, motor.Decide(Programa("Outro Teste")));
    }

    [Fact]
    public void AExpressaoRegularIgnoraMaiusculasEMinusculas()
    {
        var motor = RuleEngine.Default;
        motor.Add(new RemovalRule
        {
            Pattern = "^programa teste$",
            Category = RuleCategory.Exclude,
            IsRegex = true,
        });

        Assert.Equal(RuleDecision.Exclude, motor.Decide(Programa("Programa Teste")));
        Assert.Equal(RuleDecision.Cleanup, motor.Decide(Programa("Outro")));
    }

    [Fact]
    public void AExpressaoRegularInvalidaERejeitadaNoCarregamento()
    {
        File.WriteAllText(_regras, JsonSerializer.Serialize(new[]
        {
            new RemovalRule { Pattern = "Teste", Category = RuleCategory.Exclude },
            new RemovalRule { Pattern = "[", Category = RuleCategory.Protect, IsRegex = true },
        }));

        var (motor, avisos) = RuleEngine.Load(_regras);

        Assert.Contains(avisos, a => a.Contains("Expressao regular invalida", StringComparison.OrdinalIgnoreCase));

        // A regra invalida nao entrou; a valida entrou.
        Assert.DoesNotContain(motor.Rules, r => r.IsRegex);
        Assert.Contains(motor.Rules, r => r.Pattern == "Teste");
        Assert.Equal(RuleDecision.Exclude, motor.Decide(Programa("Programa Teste")));
    }

    [Fact]
    public void OArquivoIlegivelEPreservadoComoCorrompido()
    {
        File.WriteAllText(_regras, "{ isto nao e json");

        var (motor, avisos) = RuleEngine.Load(_regras);

        Assert.NotEmpty(avisos);
        Assert.True(File.Exists(_regras + ".corrompido"));
        Assert.NotNull(motor);
    }

    [Fact]
    public void AProtecaoDoSistemaVenceTudo()
    {
        var motor = RuleEngine.Default;
        motor.Add(new RemovalRule { Pattern = "Teste", Category = RuleCategory.Include });

        // A pasta de instalacao e de sistema: o Windows manda, e o arquivo de
        // regras nao pode liberar.
        var programa = Programa("Programa Teste", @"C:\Windows\Programa Teste");

        Assert.Equal(RuleDecision.Protect, motor.Decide(programa));
    }

    [Fact]
    public void OFallbackValeQuandoNenhumaRegraCasa()
    {
        var motor = RuleEngine.Default;

        Assert.Equal(RuleDecision.Cleanup, motor.Decide(Programa("Qualquer")));
        Assert.Equal(RuleDecision.Exclude, motor.Decide(Programa("Qualquer"), RuleDecision.Exclude));
    }

    [Fact]
    public void AsRegrasGravamERelemDoArquivo()
    {
        var motor = RuleEngine.Default;
        motor.Add(new RemovalRule { Pattern = "Teste", Category = RuleCategory.Exclude });

        Assert.True(motor.Save(_regras));
        Assert.True(File.Exists(_regras));

        var (recarregado, avisos) = RuleEngine.Load(_regras);
        Assert.Empty(avisos);
        Assert.Equal(RuleDecision.Exclude, recarregado.Decide(Programa("Programa Teste")));
    }

    [Fact]
    public void ARegraPorCaminhoCasaComAPastaDeInstalacao()
    {
        var motor = RuleEngine.Default;
        motor.Add(new RemovalRule
        {
            Pattern = @"C:\Program Files\Antigo",
            Category = RuleCategory.Exclude,
        });

        Assert.Equal(RuleDecision.Exclude, motor.Decide(Programa("X", @"C:\Program Files\Antigo")));
        Assert.Equal(RuleDecision.Cleanup, motor.Decide(Programa("X", @"C:\Program Files\Novo")));
    }

    // ---------------------------------------------------------------
    // #26 — A regra e consumida pela varredura de sobras
    // ---------------------------------------------------------------

    [Fact]
    public void UmaRegraExcludeImpedeAVarreduraDasSobras()
    {
        var pasta = Path.Combine(_temp.Root, "Programa Teste");
        Directory.CreateDirectory(pasta);

        GravarRegra(new RemovalRule { Pattern = "Programa Teste", Category = RuleCategory.Exclude });

        var itens = LeftoverScanner.Scan(
            Programa("Programa Teste", pasta),
            new LeftoverScanOptions { Mode = UninstallMode.Advanced });

        Assert.Empty(itens);
    }

    [Fact]
    public void UmaRegraProtectDesmarcaAsSobras()
    {
        var pasta = Path.Combine(_temp.Root, "Programa Teste");
        Directory.CreateDirectory(pasta);

        GravarRegra(new RemovalRule { Pattern = "Programa Teste", Category = RuleCategory.Protect });

        var itens = LeftoverScanner.Scan(
            Programa("Programa Teste", pasta),
            new LeftoverScanOptions { Mode = UninstallMode.Advanced });

        Assert.NotEmpty(itens);
        Assert.All(itens, i => Assert.False(i.Selected));
    }

    private void GravarRegra(RemovalRule regra)
    {
        File.WriteAllText(_regras, JsonSerializer.Serialize(new[] { regra },
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
            }));

        RuleEngine.Reload();
    }

    // ---------------------------------------------------------------
    // #22 — Historico de desinstalacoes
    // ---------------------------------------------------------------

    [Fact]
    public void OHistoricoGravaERele()
    {
        UninstallHistory.Append(new UninstallHistoryEntry
        {
            Programs = "Programa Teste",
            Version = "2.0",
            Publisher = "Editora",
            Uninstalled = true,
            BuiltIn = "desinstalador nativo concluído",
            RemovedItems = { @"C:\Program Files\Programa Teste" },
        });

        var entradas = UninstallHistory.Read();

        Assert.Single(entradas);
        Assert.Equal("Programa Teste", entradas[0].Programs);
        Assert.True(entradas[0].Uninstalled);
        Assert.Single(entradas[0].RemovedItems);
        Assert.NotEmpty(entradas[0].Id);
    }

    [Fact]
    public void UmaLinhaCorrompidaNaoDerrubaOHistorico()
    {
        UninstallHistory.Append(new UninstallHistoryEntry { Programs = "Bom" });
        File.AppendAllText(UninstallHistory.FilePath, "{ linha quebrada" + Environment.NewLine);
        UninstallHistory.Append(new UninstallHistoryEntry { Programs = "Bom 2" });

        var entradas = UninstallHistory.Read();

        Assert.Equal(2, entradas.Count);
        Assert.Equal("Bom 2", entradas[0].Programs);
    }

    [Fact]
    public void AEntradaDoHistoricoSeEncontraPeloIdentificador()
    {
        UninstallHistory.Append(new UninstallHistoryEntry { Programs = "Programa Teste" });

        var entrada = UninstallHistory.Read().First();
        Assert.True(UninstallHistory.TryGet(entrada.Id, out var achada));
        Assert.Equal("Programa Teste", achada!.Programs);
        Assert.False(UninstallHistory.TryGet("nao-existe", out _));
    }

    // ---------------------------------------------------------------
    // #23 — Backup das sobras
    // ---------------------------------------------------------------

    [Fact]
    public void OBackupGuardaOConteudoAntesDaRemocao()
    {
        var arquivo = Path.Combine(_temp.Root, "dados.txt");
        File.WriteAllText(arquivo, "conteudo importante");

        var resultado = LeftoverBackup.Backup(
            new[] { new LeftoverItem { Kind = "Arquivo", Path = arquivo } },
            "Programa Teste");

        Assert.True(resultado.Ok, resultado.Message);
        Assert.Equal(1, resultado.ItemCount);

        File.Delete(arquivo);
        Assert.False(File.Exists(arquivo));

        var (ok, mensagem) = LeftoverBackup.Restore(resultado.Id);
        Assert.True(ok, mensagem);
        Assert.True(File.Exists(arquivo));
        Assert.Equal("conteudo importante", File.ReadAllText(arquivo));
    }

    [Fact]
    public void AExpiracaoRemoveOVencidoEPreservaOValido()
    {
        var arquivo = Path.Combine(_temp.Root, "dados.txt");
        File.WriteAllText(arquivo, "x");

        var antigo = LeftoverBackup.Backup(
            new[] { new LeftoverItem { Kind = "Arquivo", Path = arquivo } }, "Antigo");
        var novo = LeftoverBackup.Backup(
            new[] { new LeftoverItem { Kind = "Arquivo", Path = arquivo } }, "Novo");

        // O backup velho passa a ter 40 dias: a validade e de 30.
        Envelhecer(antigo.Id, DateTime.UtcNow.AddDays(-40));

        var antes = LeftoverBackup.List().Count;
        var apagados = LeftoverBackup.Expire(30);
        var depois = LeftoverBackup.List().Count;

        Assert.Equal(2, antes);
        Assert.Equal(1, apagados);
        Assert.Equal(1, depois);
        Assert.DoesNotContain(LeftoverBackup.List(), b => b.Id == antigo.Id);
        Assert.Contains(LeftoverBackup.List(), b => b.Id == novo.Id);
    }

    private static void Envelhecer(string id, DateTime quando)
    {
        var manifesto = Path.Combine(LeftoverBackup.Folder, id, "manifest.json");
        var texto = File.ReadAllText(manifesto);
        var obj = JsonSerializer.Deserialize<LeftoverBackupManifest>(texto)!;
        obj.CreatedUtc = quando;
        File.WriteAllText(manifesto, JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        }));
    }

    // ---------------------------------------------------------------
    // #33 — Backup do programa e undo/redo
    // ---------------------------------------------------------------

    [Fact]
    public void OPerfilDoProgramaERestauradoPorInteiro()
    {
        var arquivo = Path.Combine(_temp.Root, "perfil.txt");
        File.WriteAllText(arquivo, "x");

        var perfil = ProgramBackup.CreateProfile(Programa("Programa Teste"), new[]
        {
            new LeftoverItem { Kind = "Arquivo", Path = arquivo },
            new LeftoverItem { Kind = "Pasta", Path = @"C:\Program Files\Programa Teste" },
        });

        Assert.Single(perfil.Files);
        Assert.Single(perfil.Folders);

        var caminho = ProgramBackup.SaveProfile(perfil);
        Assert.True(File.Exists(caminho));

        var salvos = ProgramBackup.ListProfiles();
        Assert.Single(salvos);
        Assert.Equal("Programa Teste", salvos[0].ProgramName);
        Assert.Single(salvos[0].Files);
    }

    [Fact]
    public void OUndoRedoERegistrado()
    {
        var arquivo = Path.Combine(_temp.Root, "desfeito.txt");
        File.WriteAllText(arquivo, "conteudo");

        var backup = LeftoverBackup.Backup(
            new[] { new LeftoverItem { Kind = "Arquivo", Path = arquivo } }, "Programa Teste");

        var operacao = new RecoverOperation
        {
            Description = "remocao das sobras",
            ProgramName = "Programa Teste",
            BackupFolder = backup.Folder,
        };

        // O arquivo sai do disco (a remocao) e a operacao fica registrada.
        File.Delete(arquivo);
        Assert.True(ProgramBackup.Record(operacao));

        var registradas = ProgramBackup.Operations();
        Assert.True(File.Exists(ProgramBackup.OperationsFile),
            $"sem arquivo em {ProgramBackup.OperationsFile}; conteudo: "
            + (File.Exists(ProgramBackup.OperationsFile)
                ? File.ReadAllText(ProgramBackup.OperationsFile)
                : "(sem arquivo)"));
        Assert.Single(registradas);
        Assert.Equal("remocao das sobras", registradas[0].Description);

        // Desfazer devolve o arquivo.
        var (okDesfazer, mensagem) = ProgramBackup.Undo(operacao);
        Assert.True(okDesfazer, mensagem);
        Assert.True(File.Exists(arquivo));
        Assert.Equal("conteudo", File.ReadAllText(arquivo));

        // Refazer apaga de novo.
        var (okRefazer, _) = ProgramBackup.Redo(operacao);
        Assert.True(okRefazer);
        Assert.False(File.Exists(arquivo));
    }

    // ---------------------------------------------------------------
    // #35 — Operacoes pendentes visiveis sem reiniciar
    // ---------------------------------------------------------------

    [Fact]
    public void AFilaDeOperacoesPendentesEParseadaEmPares()
    {
        var fila = PendingOperations.ParseQueue(new[]
        {
            @"C:\antigo.txt", @"C:\novo.txt",
            @"C:\apagar.txt", string.Empty,
        });

        Assert.Equal(2, fila.Count);
        Assert.Equal(@"C:\antigo.txt", fila[0].Source);
        Assert.Equal(@"C:\novo.txt", fila[0].Destination);
        Assert.True(fila[1].IsDelete);
    }

    [Fact]
    public void UmaFilaImparNaoViraOperacaoDeApagar()
    {
        var fila = PendingOperations.ParseQueue(new[] { @"C:\sozinho.txt" });

        Assert.Empty(fila);
    }

    [Fact]
    public void AOperacaoForcadaFalhaDeFormaLimpaQuandoOArquivoNaoExiste()
    {
        var operacao = new PendingOperation
        {
            Source = Path.Combine(_temp.Root, "nao-existe.txt"),
            Destination = Path.Combine(_temp.Root, "novo.txt"),
        };

        var (ok, mensagem) = PendingOperations.Apply(operacao);

        Assert.False(ok);
        Assert.NotEmpty(mensagem);
    }

    [Fact]
    public void AOperacaoForcadaAplicaAgoraQuandoOArquivoEstaLivre()
    {
        var origem = Path.Combine(_temp.Root, "origem.txt");
        var destino = Path.Combine(_temp.Root, "destino.txt");
        File.WriteAllText(origem, "x");

        var (ok, mensagem) = PendingOperations.Apply(new PendingOperation
        {
            Source = origem,
            Destination = destino,
        });

        Assert.True(ok, mensagem);
        Assert.False(File.Exists(origem));
        Assert.True(File.Exists(destino));
    }

    [Fact]
    public void AOperacaoForcadaFalhaDeFormaLimpaQuandoOArquivoEstaEmUso()
    {
        var origem = Path.Combine(_temp.Root, "em-uso.txt");
        File.WriteAllText(origem, "x");

        using (var trava = new FileStream(origem, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var (ok, mensagem) = PendingOperations.Apply(new PendingOperation
            {
                Source = origem,
                Destination = Path.Combine(_temp.Root, "destino.txt"),
            });

            Assert.False(ok);
            Assert.NotEmpty(mensagem);
        }
    }

    // ---------------------------------------------------------------
    // #49 — Backup completo do registro todo dia
    // ---------------------------------------------------------------

    [Fact]
    public void OBackupDiarioNaoRepeteNoMesmoDia()
    {
        // O marcador de hoje: o backup diario nao pode rodar de novo.
        File.WriteAllText(DailyRegistryBackup.MarkerPath, DateTime.UtcNow.ToString("O"));

        Assert.False(DailyRegistryBackup.IsDue);

        var resultado = DailyRegistryBackup.RunIfDue();

        Assert.False(resultado.Ran);
        Assert.True(resultado.Skipped);
        Assert.NotEmpty(resultado.Message);
    }

    [Fact]
    public void OBackupDiarioRodaQuandoAindaNaoHouveBackupHoje()
    {
        DailyRegistryBackup.Reset();

        Assert.True(DailyRegistryBackup.IsDue);
        Assert.Null(DailyRegistryBackup.LastRunUtc);
    }

    [Fact]
    public void OBackupDiarioRespeitaAValidadeDeUmDia()
    {
        // O marcador de tres dias atras: o backup de hoje ainda e due.
        File.WriteAllText(
            DailyRegistryBackup.MarkerPath,
            DateTime.UtcNow.AddDays(-3).ToString("O"));

        Assert.True(DailyRegistryBackup.IsDue);

        // Com validade de cinco dias, ainda nao e hora.
        var resultado = DailyRegistryBackup.RunIfDue(5);

        Assert.False(resultado.Ran);
        Assert.True(resultado.Skipped);
    }

    [Fact]
    public void OBackupDiarioGravaOMarcadorQuandoRodou()
    {
        const string raiz = @"Software\Tranal\Testes\Diario";
        FullRegistryBackup.HivesOverride = new[] { @"HKCU\" + raiz };

        try
        {
            using (var chave = Registry.CurrentUser.CreateSubKey(raiz))
                chave?.SetValue("Marca", "valor de teste");
        }
        catch { return; }

        DailyRegistryBackup.Reset();

        var resultado = DailyRegistryBackup.RunIfDue();

        Assert.True(resultado.Ran, resultado.Message);
        Assert.True(resultado.Success, resultado.Message);
        Assert.NotNull(DailyRegistryBackup.LastRunUtc);
        Assert.False(DailyRegistryBackup.IsDue);

        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(@"Software\Tranal\Testes", writable: true);
            root?.DeleteSubKeyTree("Diario", throwOnMissingSubKey: false);
        }
        catch { /* chave de teste ja removida */ }
    }
}
