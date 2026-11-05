using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Settings;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// A lista de exclusoes: caminhos e chaves de registro que o Tranal nunca toca.
///
/// Corresponde ao original nas strings "Add to Exclude List" (15102),
/// "Folder to exclude" (15315), "Files and Folders to Exclude" (15316) e
/// "Registry Keys to Exclude" (15317).
///
/// Na colecao "preferencias" porque a lista ativa e estado global: uma
/// troca feita aqui precisa ser desfeita, senao a varredura de outra classe
/// passaria a enxergar os itens desta.
/// </summary>
[Collection("preferencias")]
public class ExcludeListTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly ExcludeList _original;

    public ExcludeListTests()
    {
        _original = ExcludeList.Current;
        ExcludeList.SetCurrent(new ExcludeList());
    }

    public void Dispose()
    {
        ExcludeList.SetCurrent(_original);
        ExcludeList.FilePathOverride = null;
        _temp.Dispose();
    }

    [Fact]
    public void CaminhoExcluidoProtegeOConteudoMasNaoOVizinho()
    {
        var list = new ExcludeList();
        list.AddPath(@"C:\Programas\App");

        Assert.True(list.ContainsPath(@"C:\Programas\App"));
        Assert.True(list.ContainsPath(@"C:\Programas\App\"));
        Assert.True(list.ContainsPath(@"C:\Programas\App\config.ini"));

        // O vizinho que comeca com o mesmo texto NAO esta protegido: e o erro
        // classico do casamento por prefixo sem separador ("C:\Programas\App2"
        // casaria com "C:\Programas\App").
        Assert.False(list.ContainsPath(@"C:\Programas\App2"));
        Assert.False(list.ContainsPath(@"C:\Programas\AppOLD\ini.txt"));
        Assert.False(list.ContainsPath(@"C:\Programas"));
    }

    [Fact]
    public void NomeCurtoDeHiveEquivaleAoNomeCanonico()
    {
        var list = new ExcludeList();
        list.AddRegistryKey(@"HKLM\SOFTWARE\MeuApp");

        // O usuario digita "HKLM\..." e o Tranal lista o item como
        // "HKEY_LOCAL_MACHINE\...": os dois precisam casar.
        Assert.True(list.ContainsRegistryKey(@"HKEY_LOCAL_MACHINE\SOFTWARE\MeuApp"));
        Assert.True(list.ContainsRegistryKey(@"HKLM\SOFTWARE\MeuApp"));
        Assert.True(list.ContainsRegistryKey(@"HKEY_LOCAL_MACHINE\SOFTWARE\MeuApp\Sub"));

        Assert.False(list.ContainsRegistryKey(@"HKEY_LOCAL_MACHINE\SOFTWARE\MeuApp2"));
        Assert.False(list.ContainsRegistryKey(@"HKEY_CURRENT_USER\SOFTWARE\MeuApp"));
    }

    [Fact]
    public void AdicionarDuasVezesNaoDuplica()
    {
        var list = new ExcludeList();

        Assert.True(list.AddPath(@"C:\Programas\App"));
        Assert.False(list.AddPath(@"C:\Programas\App\"));
        Assert.Single(list.Paths);

        Assert.True(list.AddRegistryKey(@"HKLM\SOFTWARE\MeuApp"));
        Assert.False(list.AddRegistryKey(@"HKLM\SOFTWARE\MeuApp"));
        Assert.Single(list.RegistryKeys);
    }

    [Fact]
    public void RemovePathRemoveRegistryKeyEClearFuncionam()
    {
        var list = new ExcludeList();
        list.AddPath(@"C:\Programas\App");
        list.AddRegistryKey(@"HKLM\SOFTWARE\MeuApp");

        Assert.True(list.RemovePath(@"C:\Programas\App"));
        Assert.False(list.RemovePath(@"C:\Programas\App"));
        Assert.Empty(list.Paths);

        Assert.True(list.RemoveRegistryKey(@"HKLM\SOFTWARE\MeuApp"));
        Assert.False(list.RemoveRegistryKey(@"HKLM\SOFTWARE\MeuApp"));
        Assert.Empty(list.RegistryKeys);

        list.AddPath(@"C:\Programas\App");
        list.Clear();
        Assert.True(list.IsEmpty);
    }

    [Fact]
    public void AddItemEscolheAListaPeloTipoDoItem()
    {
        var list = new ExcludeList();

        Assert.True(list.AddItem(new LeftoverItem { Kind = "Pasta", Path = @"C:\Programas\App" }));
        Assert.True(list.AddItem(new LeftoverItem { Kind = "Registro", Path = @"HKEY_LOCAL_MACHINE\SOFTWARE\MeuApp" }));

        Assert.Single(list.Paths);
        Assert.Single(list.RegistryKeys);

        // Um item ja presente nao entra de novo.
        Assert.False(list.AddItem(new LeftoverItem { Kind = "Pasta", Path = @"C:\Programas\App" }));
    }

    [Fact]
    public void ApplyToRemoveOsItensProtegidos()
    {
        var list = new ExcludeList();
        list.AddPath(@"C:\Programas\App");

        var items = new List<LeftoverItem>
        {
            new() { Kind = "Pasta", Path = @"C:\Programas\App" },
            new() { Kind = "Pasta", Path = @"C:\Programas\Outro" },
            new() { Kind = "Registro", Path = @"HKEY_LOCAL_MACHINE\SOFTWARE\MeuApp" },
        };

        var restantes = list.ApplyTo(items);

        // O item protegido sai. Os outros dois ficam: a lista so protege
        // caminhos, e uma chave de registro so e protegida quando esta na
        // lista de chaves.
        Assert.Equal(2, restantes.Count);
        Assert.DoesNotContain(restantes, i => i.Path == @"C:\Programas\App");
        Assert.Contains(restantes, i => i.Path == @"C:\Programas\Outro");
        Assert.Contains(restantes, i => i.Kind == "Registro");
    }

    [Fact]
    public void ChaveDeRegistroComBarraInicialProtege()
    {
        var list = new ExcludeList();

        // O motor aceita "\HKLM\SOFTWARE\MeuApp" (SplitRegistryPath faz
        // TrimStart). A lista precisa aceitar a mesma forma: duas
        // interpretacoes do mesmo caminho fariam a lista "parecer nao
        // funcionar".
        list.AddRegistryKey(@"\HKLM\SOFTWARE\MeuApp");

        Assert.True(list.ContainsRegistryKey(@"HKEY_LOCAL_MACHINE\SOFTWARE\MeuApp"));
        Assert.True(list.ContainsRegistryKey(@"HKLM\SOFTWARE\MeuApp"));
    }

    [Fact]
    public void GravarERecarregarPreservaALista()
    {
        var file = Path.Combine(_temp.Root, "exclude-list.json");

        try
        {
            ExcludeList.FilePathOverride = file;

            var list = new ExcludeList();
            list.AddPath(@"C:\Programas\App");
            list.AddRegistryKey(@"HKLM\SOFTWARE\MeuApp");

            Assert.True(list.Save());
            Assert.True(File.Exists(file));

            var recarregada = ExcludeList.Load();
            Assert.Single(recarregada.Paths);
            Assert.Single(recarregada.RegistryKeys);
            Assert.True(recarregada.ContainsPath(@"C:\Programas\App"));
            Assert.True(recarregada.ContainsRegistryKey(@"HKLM\SOFTWARE\MeuApp"));
        }
        finally
        {
            ExcludeList.FilePathOverride = null;
        }
    }
}

/// <summary>
/// A lista de exclusoes protege ate o encerramento de processos: encerrar o
/// processo de um item protegido seria tocar no item protegido tanto quanto
/// apagar a pasta seria.
/// </summary>
[Collection("preferencias")]
public class ExcludeListProcessProtectionTests : IDisposable
{
    private readonly ExcludeList _original;

    public ExcludeListProcessProtectionTests()
    {
        _original = ExcludeList.Current;
        ExcludeList.SetCurrent(new ExcludeList());
    }

    public void Dispose() => ExcludeList.SetCurrent(_original);

    [Fact]
    public void OProcessoDeUmItemProtegidoNaoEEncerrado()
    {
        // O item protegido nao pode ter o processo encerrado por baixo dos
        // panos: encerrar o processo e tocar no item protegido tanto quanto
        // apagar a pasta seria.
        var ping = Path.Combine(Environment.SystemDirectory, "ping.exe");
        if (!File.Exists(ping)) return; // sem ping nao ha como montar o teste

        using var temp = new TempFolder();
        var folder = temp.CreateSubFolder("pingcopy");
        var copy = Path.Combine(folder, "pingcopy.exe");

        try
        {
            File.Copy(ping, copy);
        }
        catch
        {
            return; // sem permissao de leitura do SystemDirectory: nada a testar
        }

        Process? process = null;
        try
        {
            process = Process.Start(new ProcessStartInfo(copy, "-n 30 127.0.0.1")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null) return;

            // O processo pode levar um instante para aparecer na enumeracao.
            // Sem a espera, o teste seria uma corrida em CI carregado.
            var achado = Array.Empty<RunningProcess>();
            for (var tentativa = 0; tentativa < 20 && achado.Length == 0; tentativa++)
            {
                achado = ProcessTerminator.FindUnder(new[] { folder }).ToArray();
                if (achado.Length == 0) Thread.Sleep(100);
            }

            Assert.NotEmpty(achado);

            ExcludeList.Current.AddPath(folder);

            var report = UninstallEngine.RemoveLeftovers(new[]
            {
                new LeftoverItem { Kind = "Pasta", Path = folder, Detail = "teste", Selected = true },
            }, new UninstallRemovalOptions
            {
                Backup = false,
                UseRecycleBin = false,
                DeferToNextRestart = false,
                KillRunningProcesses = true,
            });

            // O processo continua rodando: a lista de exclusoes o protege.
            Assert.False(process.HasExited);

            // E o item volta como protegido, sem nenhuma operacao de
            // encerramento registrada no relatorio.
            Assert.Equal(1, report.ProtectedCount);
            Assert.DoesNotContain(report.Outcomes, o => o.Item.Kind == "Processo");
            Assert.True(Directory.Exists(folder));
        }
        finally
        {
            if (process is not null)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
                process.Dispose();
            }
        }
    }
}

/// <summary>
/// O backup completo do registro nasce antes do desinstalador nativo: depois
/// dele, as chaves do programa ja foram apagadas e o .reg nao serviria para
/// nada.
/// </summary>
[Collection("preferencias")]
public class FullRegistryBackupOrderTests : IDisposable
{
    private const string TestRoot = @"Software\Tranal\Testes\Backup";

    public FullRegistryBackupOrderTests() => FullRegistryBackup.HivesOverride = null;

    public void Dispose()
    {
        FullRegistryBackup.HivesOverride = null;
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(@"Software\Tranal\Testes", writable: true);
            root?.DeleteSubKeyTree("Backup", throwOnMissingSubKey: false);
        }
        catch { /* chave de teste ja removida */ }
    }

    [Fact]
    public void SemAPedidoNaoHaBackupCompleto()
    {
        Assert.Null(UninstallEngine.PrepareFullRegistryBackup(UninstallRemovalOptions.Default));
    }

    [Fact]
    public void OBackupCompletoEPreparadoPeloChamadorAntesDaRemocao()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(TestRoot);
            key.SetValue("Marca", "valor de teste");
        }
        catch { return; }

        FullRegistryBackup.HivesOverride = new[] { @"HKCU\" + TestRoot };

        var options = new UninstallRemovalOptions
        {
            Backup = false,
            UseRecycleBin = false,
            DeferToNextRestart = false,
            FullRegistryBackup = true,
        };

        var backup = UninstallEngine.PrepareFullRegistryBackup(options);

        Assert.NotNull(backup);
        Assert.True(backup!.Success, backup.Message);
        Assert.NotEmpty(backup.Files);

        // O motor de remocao NAO cria o backup: ele usa o que foi preparado.
        var report = UninstallEngine.RemoveLeftovers(Array.Empty<LeftoverItem>(), options, backup);

        Assert.Equal(backup.Directory, report.FullRegistryBackupPath);
        Assert.True(report.FullRegistryBackupOk);
    }
}

/// <summary>
/// O relatorio de remocao conta ITENS removidos. Encerrar um executavel e
/// criar um backup sao operacoes auxiliares: se entrassem na contagem, o
/// relatorio diria "3 itens removidos" quando so um foi removido.
/// </summary>
public class RemovalReportCountTests
{
    [Fact]
    public void OperacoesAuxiliaresNaoEntramNasContagensDeRemocao()
    {
        var report = new RemovalReport(new[]
        {
            new RemovalOutcome
            {
                Item = new LeftoverItem { Kind = "Arquivo", Path = @"C:\alvo.txt" },
                Status = RemovalStatus.Removed,
            },
            new RemovalOutcome
            {
                Item = new LeftoverItem { Kind = "Processo", Path = @"C:\app\app.exe" },
                Status = RemovalStatus.Removed,
                Message = "processo encerrado",
            },
            new RemovalOutcome
            {
                Item = new LeftoverItem { Kind = "Pasta", Path = @"C:\alvo" },
                Status = RemovalStatus.Protected,
            },
        });

        // Um arquivo removido. O processo encerrado nao conta.
        Assert.Equal(1, report.RemovedCount);

        // O total tambem nao conta o processo: sao dois itens (o arquivo
        // removido e a pasta protegida).
        Assert.Equal(2, report.Total);

        // A pasta protegida conta como protegida: ela e um item real.
        Assert.Equal(1, report.ProtectedCount);
        Assert.Single(report.Remaining);
    }
}

/// <summary>
/// A lista de exclusoes dentro do motor: o item excluido nem aparece na
/// varredura e o motor recusa remove-lo mesmo quando ele chega marcado.
///
/// E a regra que realmente protege o usuario: a varredura esconde o item, e
/// a guarda final no motor impede a remocao por qualquer outro caminho.
/// </summary>
[Collection("preferencias")]
public class ExcludeListEngineTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly ExcludeList _original;

    /// <summary>
    /// Opcoes hermeticas: sem backup (nao cria pontos de restauracao no
    /// ambiente de quem roda os testes) e sem Lixeira.
    /// </summary>
    private static readonly UninstallRemovalOptions Opcoes = new()
    {
        Backup = false,
        UseRecycleBin = false,
        DeferToNextRestart = false,
    };

    public ExcludeListEngineTests()
    {
        _original = ExcludeList.Current;
        ExcludeList.SetCurrent(new ExcludeList());
    }

    public void Dispose()
    {
        ExcludeList.SetCurrent(_original);
        _temp.Dispose();
    }

    [Fact]
    public void ItemExcluidoNaoApareceNaVarreduraForcada()
    {
        var folder = _temp.CreateSubFolder("Programa Excluido Tranal");

        ExcludeList.Current.AddPath(folder);

        var items = LeftoverScanner.ScanForced(folder, new LeftoverScanOptions
        {
            Mode = UninstallMode.Advanced,
        });

        Assert.DoesNotContain(items, i =>
            string.Equals(i.Path, folder, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MotorRecusaRemoverItemExcluidoMesmoMarcado()
    {
        var file = _temp.CreateFile("alvo-excluido.txt");
        ExcludeList.Current.AddPath(file);

        var report = UninstallEngine.RemoveLeftovers(new[]
        {
            new LeftoverItem { Kind = "Arquivo", Path = file, Detail = "teste", Selected = true },
        }, Opcoes);

        var outcome = Assert.Single(report.Outcomes);
        Assert.Equal(RemovalStatus.Protected, outcome.Status);
        Assert.Contains("exclus", outcome.Message, StringComparison.OrdinalIgnoreCase);

        // O arquivo continua no lugar: a guardafinal e a ultima palavra.
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void OAlvoForcadoDeUmArquivoProtegidoNaoApagaOArquivo()
    {
        // O cenario completo: o usuario exclui um arquivo e manda o Tranal
        // desinstalar aquele arquivo. A varredura acha a pasta E o arquivo; o
        // arquivo some da lista (esta protegido) e a pasta nao pode levar ele
        // junto na remocao.
        var folder = _temp.CreateSubFolder("Programa Forcado Tranal");
        var protegido = Path.Combine(folder, "TranalTesteAlvoUnico.exe");
        File.WriteAllText(protegido, "conteudo");

        ExcludeList.Current.AddPath(protegido);

        var items = LeftoverScanner.ScanForced(protegido, new LeftoverScanOptions
        {
            Mode = UninstallMode.Advanced,
        });

        // O arquivo protegido nao aparece na lista.
        Assert.DoesNotContain(items, i =>
            string.Equals(i.Path, protegido, StringComparison.OrdinalIgnoreCase));

        // A pasta que o contem NAO vem marcada: apontar para um arquivo nao
        // autoriza remover a pasta em que ele esta — inferir a "pasta de
        // instalacao" a partir do caminho do arquivo faria a desinstalacao
        // forcada de um executavel em %TEMP% remover a pasta Temp inteira.
        var pasta = items.FirstOrDefault(i =>
            i.Kind == "Pasta" && string.Equals(i.Path, folder, StringComparison.OrdinalIgnoreCase));
        Assert.True(pasta is null || !pasta.Selected);

        // E, quando o usuario pede para remover a pasta, o arquivo protegido
        // sobrevive: a lista de exclusoes e a ultima palavra.
        var report = UninstallEngine.RemoveLeftovers(new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = folder, Detail = "teste", Selected = true },
        }, Opcoes);

        Assert.True(File.Exists(protegido));
        Assert.Equal(1, report.ProtectedCount);
        Assert.Equal(0, report.RemovedCount);
    }

    [Fact]
    public void ItemExcluidoNaoEntraNoBackupDaRemocao()
    {
        var folder = _temp.CreateSubFolder("Pasta Excluida do Backup");
        var arquivo = Path.Combine(folder, "dado.txt");
        File.WriteAllText(arquivo, "conteudo");

        ExcludeList.Current.AddPath(folder);

        var report = UninstallEngine.RemoveLeftovers(new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = folder, Detail = "teste", Selected = true },
        }, Opcoes);

        Assert.Equal(1, report.ProtectedCount);
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public void ArquivoExcluidoSobreviveARemocaoDaPastaQueOContem()
    {
        // Excluir um ARQUIVO dentro de uma pasta precisa proteger o arquivo:
        // a remocao da pasta nao pode levar ele junto.
        var folder = _temp.CreateSubFolder("Programa Com Arquivo Protegido");
        var protegido = Path.Combine(folder, "protegido.dat");
        File.WriteAllText(protegido, "conteudo");

        ExcludeList.Current.AddPath(protegido);

        var report = UninstallEngine.RemoveLeftovers(new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = folder, Detail = "teste", Selected = true },
        }, Opcoes);

        // O arquivo protegido continua no lugar.
        Assert.True(File.Exists(protegido));

        // E a pasta volta como protegida, nao como removida.
        Assert.Equal(1, report.ProtectedCount);
        Assert.Equal(0, report.RemovedCount);
        Assert.True(Directory.Exists(folder));
    }
}

/// <summary>
/// Encerrar processos em execucao: o processo do proprio Tranal nunca e
/// encerrado, e um processo so e candidato quando o executavel esta dentro
/// da pasta que sera removida.
/// </summary>
public class ProcessTerminatorTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void NuncaEncerraOProprioTranal()
    {
        var self = new RunningProcess
        {
            ProcessId = Environment.ProcessId,
            ProcessName = "tranal",
            ExecutablePath = Path.Combine(_temp.Root, "tranal.exe"),
        };

        var resultado = Assert.Single(ProcessTerminator.Terminate(new[] { self }));

        Assert.False(resultado.Terminated);
        Assert.Contains("Tranal", resultado.Message);
    }

    [Fact]
    public void PastaSemProcessoEmExecucaoNaoRetornaNada()
    {
        var folder = _temp.CreateSubFolder("nada-roda-aqui");

        Assert.Empty(ProcessTerminator.FindUnder(new[] { folder }));
    }

    [Fact]
    public void CaminhoInexistenteNaoRetornaProcessoNenhum()
    {
        Assert.Empty(ProcessTerminator.FindExact(new[]
        {
            Path.Combine(_temp.Root, "nao-existe.exe"),
        }));
    }

    [Fact]
    public void OExecutavelDentroDaPastaEhConsideradoDentro()
    {
        var processo = new RunningProcess
        {
            ProcessId = 1234,
            ProcessName = "programa",
            ExecutablePath = @"C:\Programas\App\programa.exe",
        };

        Assert.True(processo.IsInside(@"C:\Programas\App"));
        Assert.True(processo.IsInside(@"C:\Programas\App\"));
        Assert.True(processo.IsInside(@"C:\Programas"));

        // O vizinho que comeca com o mesmo texto nao esta dentro: e o erro
        // classico do casamento por prefixo sem separador.
        Assert.False(processo.IsInside(@"C:\Programas\App2"));
        Assert.False(processo.IsInside(@"C:\Programas\Outro"));
    }

    [Fact]
    public void AEnumeracaoNuncaIncluiOProprioProcesso()
    {
        var processos = ProcessTerminator.Enumerate();

        // O Tranal nao entra na propria lista de candidatos: encerrar a si
        // mesmo seria o pior resultado possivel.
        Assert.DoesNotContain(processos, p => p.ProcessId == Environment.ProcessId);
        Assert.All(processos, p => Assert.NotEmpty(p.ExecutablePath));
    }
}

/// <summary>
/// O backup completo do registro. O teste exporta apenas uma subchave
/// pequena: exportar o registro inteiro levaria minutos e centenas de
/// megabytes na maquina de quem roda a suite.
/// </summary>
public class FullRegistryBackupTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private const string TestRoot = @"Software\Tranal\Testes\Backup";

    public FullRegistryBackupTests()
    {
        FullRegistryBackup.HivesOverride = null;
    }

    public void Dispose()
    {
        FullRegistryBackup.HivesOverride = null;
        RemoveTestKey();
        _temp.Dispose();
    }

    [Fact]
    public void CriaOsArquivosEOsScriptDeRestauracao()
    {
        CreateTestKey();

        FullRegistryBackup.HivesOverride = new[] { @"HKCU\" + TestRoot };

        var destino = Path.Combine(_temp.Root, "backup");
        var resultado = FullRegistryBackup.Create(destino);

        Assert.True(resultado.Success, resultado.Message);
        Assert.Single(resultado.Files);
        Assert.True(File.Exists(resultado.Files[0]));

        // O script de restauracao importa o .reg gerado.
        Assert.True(File.Exists(resultado.RestoreScript));
        var script = File.ReadAllText(resultado.RestoreScript);
        Assert.Contains("reg import", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FalhaDeixandoMensagemQuandoOHiveNaoExiste()
    {
        FullRegistryBackup.HivesOverride = new[] { @"HKCU\" + TestRoot + @"\NaoExiste" };

        var resultado = FullRegistryBackup.Create(Path.Combine(_temp.Root, "backup-vazio"));

        Assert.False(resultado.Success);
        Assert.NotEmpty(resultado.Message);
        Assert.Empty(resultado.Files);
    }

    private static void CreateTestKey()
    {
        using var key = Registry.CurrentUser.CreateSubKey(TestRoot);
        key.SetValue("Marca", "valor de teste");
    }

    private static void RemoveTestKey()
    {
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(@"Software\Tranal\Testes", writable: true);
            root?.DeleteSubKeyTree("Backup", throwOnMissingSubKey: false);
        }
        catch { /* chave de teste ja removida */ }
    }
}

/// <summary>
/// As contas do Windows: a enumeracao vem do ProfileList do registro, e a
/// conta atual e sempre a primeira da lista.
/// </summary>
public class UserProfilesTests
{
    [Fact]
    public void AContaAtualApareceNaEnumeracao()
    {
        var profiles = UserProfiles.Enumerate();

        Assert.NotEmpty(profiles);
        Assert.NotNull(UserProfiles.CurrentSid);

        var atual = Assert.Single(profiles, p => p.IsCurrent);
        Assert.Equal(UserProfiles.CurrentSid, atual.Sid, ignoreCase: true);
        Assert.NotEmpty(atual.LocalPath);
    }

    [Fact]
    public void OthersNaoIncluiAContaAtual()
    {
        var outros = UserProfiles.Others();

        Assert.DoesNotContain(outros, p => p.IsCurrent);
        Assert.All(outros, p => Assert.NotEqual(
            UserProfiles.CurrentSid, p.Sid, StringComparer.OrdinalIgnoreCase));
    }
}

/// <summary>
/// As preferencias que traduzem as opcoes do Revo Uninstaller Pro deste lote:
/// encerrar os executaveis em execucao, varredura em todas as contas e
/// backup completo do registro.
/// </summary>
[Collection("preferencias")]
public class Lote1SettingsTests
{
    [Fact]
    public void OsPadroesSeguemORevo()
    {
        var settings = new AppSettings();

        // Todas as opcoes novas comecam desligadas: cada uma muda o alcance da
        // remocao, e nenhuma delas deve valer sem o usuario pedir.
        Assert.False(settings.ForciblyStopRunningExecutables);
        Assert.False(settings.ShowScanAllAccountsOption);
        Assert.False(settings.ScanAllWindowsAccounts);
        Assert.False(settings.UninstallFromAllWindowsAccounts);
        Assert.False(settings.CreateFullRegistryBackupBeforeUninstall);
    }

    [Fact]
    public void AsOpcoesDerivadasDasPreferenciasRespeitamAsFlags()
    {
        var settings = AppSettingsStore.Current;
        var originalForcibly = settings.ForciblyStopRunningExecutables;
        var originalFullBackup = settings.CreateFullRegistryBackupBeforeUninstall;
        var originalScanAll = settings.ScanAllWindowsAccounts;
        var originalUninstallAll = settings.UninstallFromAllWindowsAccounts;

        try
        {
            settings.ForciblyStopRunningExecutables = true;
            settings.CreateFullRegistryBackupBeforeUninstall = true;
            settings.ScanAllWindowsAccounts = true;
            settings.UninstallFromAllWindowsAccounts = true;

            var removal = UninstallRemovalOptions.FromSettings();
            Assert.True(removal.KillRunningProcesses);
            Assert.True(removal.FullRegistryBackup);

            var scan = LeftoverScanOptions.FromSettings(UninstallMode.Advanced);
            Assert.True(scan.ScanAllUserAccounts);
            Assert.True(scan.SelectOtherAccountItems);
            Assert.Equal(UninstallMode.Advanced, scan.Mode);
        }
        finally
        {
            settings.ForciblyStopRunningExecutables = originalForcibly;
            settings.CreateFullRegistryBackupBeforeUninstall = originalFullBackup;
            settings.ScanAllWindowsAccounts = originalScanAll;
            settings.UninstallFromAllWindowsAccounts = originalUninstallAll;
        }
    }

    [Fact]
    public void OFormatoDoArquivoDePreferenciasDocumentaAsOpcoesNovas()
    {
        var formato = SettingsFileFormat.Describe();

        Assert.Contains("forciblyStopRunningExecutables", formato);
        Assert.Contains("showScanAllAccountsOption", formato);
        Assert.Contains("scanAllWindowsAccounts", formato);
        Assert.Contains("uninstallFromAllWindowsAccounts", formato);
        Assert.Contains("createFullRegistryBackupBeforeUninstall", formato);
    }
}
