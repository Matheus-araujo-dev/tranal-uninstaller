using System.IO;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Varredura e limpeza: fluxos alternativos (ADS), pastas problematicas do
/// Windows 11, sobras por conta do Windows e fragmentos do CHKDSK.
///
/// O que importa aqui e a regra, nao o resultado: o que a varredura devolve
/// tem de bater com o que a regra diz, e nada pode ser removido sem ser o que
/// a regra manda remover.
/// </summary>
public class ScanAndCleanTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    // -----------------------------------------------------------------
    // Fluxos alternativos (ADS)
    // -----------------------------------------------------------------

    [Fact]
    public void OFluxoAlternativoCriadoNoArquivoEEEncontrado()
    {
        var file = _temp.CreateFile("programa.exe", "conteudo");

        // Cria o stream escondido: e assim que o Windows marca um arquivo
        // baixado da internet (Zone.Identifier).
        var stream = file + ":Zone.Identifier";
        try { File.WriteAllText(stream, "[ZoneTransfer]\r\nZoneId=3\r\n"); }
        catch { return; /* volume sem suporte a streams: nada a verificar */ }

        var found = AlternateDataStreamScanner.Scan(file);

        var zone = Assert.Single(found, s => s.Name.Equals("Zone.Identifier", StringComparison.OrdinalIgnoreCase));
        Assert.False(zone.IsDefault);
        Assert.Equal("$DATA", zone.Type);
        Assert.True(zone.Size > 0);
        Assert.Equal($"{file}:Zone.Identifier", zone.StreamPath);
    }

    [Fact]
    public void AVarreduraDeFluxosNaoDevolveOFluxoPrincipalPorPadrao()
    {
        var file = _temp.CreateFile("programa.exe", "conteudo");

        var found = AlternateDataStreamScanner.Scan(file);

        Assert.DoesNotContain(found, s => s.IsDefault);
    }

    [Fact]
    public void OFluxoPrincipalApareceQuandoPedido()
    {
        var file = _temp.CreateFile("programa.exe", "conteudo");

        var found = AlternateDataStreamScanner.Scan(file, includeDefaultStream: true);

        var principal = Assert.Single(found, s => s.IsDefault);
        Assert.Equal(file, principal.StreamPath);
    }

    [Fact]
    public void ArquivoInexistenteNaoQuebraAVarreduraDeFluxos()
    {
        var inexistente = Path.Combine(_temp.Root, "nao-existe.exe");

        Assert.Empty(AlternateDataStreamScanner.Scan(inexistente));
        Assert.Empty(AlternateDataStreamScanner.Scan(string.Empty));
    }

    [Fact]
    public void AVarreduraDeFluxosDaPastaEncontraOsArquivosDeDentro()
    {
        var file = _temp.CreateFile("programa.exe", "conteudo");

        var stream = file + ":Zone.Identifier";
        try { File.WriteAllText(stream, "[ZoneTransfer]\r\n"); }
        catch { return; /* volume sem suporte a streams */ }

        var found = AlternateDataStreamScanner.ScanFolder(_temp.Root);

        Assert.Contains(found, s => s.Name.Equals("Zone.Identifier", StringComparison.OrdinalIgnoreCase));
    }

    // -----------------------------------------------------------------
    // Pastas problematicas do Windows 11
    // -----------------------------------------------------------------

    [Fact]
    public void APastasProblematicasDoWindows11SoAparecemQuandoExistem()
    {
        foreach (var folder in Windows11ProblemFolders.KnownFolders())
        {
            Assert.True(Directory.Exists(folder.Path), $"a pasta listada nao existe: {folder.Path}");
            Assert.False(string.IsNullOrWhiteSpace(folder.Description));
        }
    }

    [Fact]
    public void AsPastasConhecidasDoWindows11SaoReconhecidasPeloNome()
    {
        Assert.True(Windows11ProblemFolders.IsKnownProblemFolder(@"C:\$WINDOWS.~BT"));
        Assert.True(Windows11ProblemFolders.IsKnownProblemFolder(@"C:\$SysReset"));
        Assert.True(Windows11ProblemFolders.IsKnownProblemFolder(@"D:\Windows.old"));

        Assert.False(Windows11ProblemFolders.IsKnownProblemFolder(@"C:\Users"));
        Assert.False(Windows11ProblemFolders.IsKnownProblemFolder(string.Empty));
    }

    [Fact]
    public void ADescricaoDaPastaProblematicoVemComOTexto()
    {
        Assert.False(string.IsNullOrWhiteSpace(Windows11ProblemFolders.Describe(@"C:\Windows.old")));
        Assert.Empty(Windows11ProblemFolders.Describe(@"C:\Users"));
    }

    // -----------------------------------------------------------------
    // Sobras por conta do Windows
    // -----------------------------------------------------------------

    [Fact]
    public void AVarreduraPorContaNaoTrazAContaAtualPorPadrao()
    {
        var contas = AccountLeftoverScanner.Accounts(AccountLeftoverScanOptions.Default);

        Assert.DoesNotContain(contas, c => c.IsCurrent);
    }

    [Fact]
    public void AVarreduraPorContaTrazAContaAtualQuandoPedido()
    {
        var opcoes = AccountLeftoverScanOptions.Default with { IncludeCurrentAccount = true };

        var contas = AccountLeftoverScanner.Accounts(opcoes);

        Assert.Contains(contas, c => c.IsCurrent);
    }

    [Fact]
    public void ASobraDaContaEEncontradaNoPerfilDaConta()
    {
        var conta = ContaFalsa(Path.Combine(_temp.Root, "perfil"));
        Directory.CreateDirectory(Path.Combine(conta.LocalPath, "AppData", "Local", "Meu Programa"));

        var itens = AccountLeftoverScanner.ScanFolders(
            new[] { conta },
            new[] { @"AppData\Local\Meu Programa" },
            AccountLeftoverScanOptions.Default);

        var item = Assert.Single(itens);
        Assert.Equal(AccountItemKind.Folder, item.Kind);
        Assert.Equal(Path.Combine(conta.LocalPath, "AppData", "Local", "Meu Programa"), item.Path);
        Assert.Equal(conta.Sid, item.Profile!.Sid);
    }

    [Fact]
    public void ASobraDeOutraContaSoVemMarcadaQuandoSePedeParaRemoverDeTodas()
    {
        var conta = ContaFalsa(Path.Combine(_temp.Root, "perfil"));
        Directory.CreateDirectory(Path.Combine(conta.LocalPath, "AppData", "Local", "Meu Programa"));
        var relativo = new[] { @"AppData\Local\Meu Programa" };

        var informativa = AccountLeftoverScanner.ScanFolders(
            new[] { conta }, relativo, AccountLeftoverScanOptions.Default);
        Assert.False(Assert.Single(informativa).Selected);

        var removendo = AccountLeftoverScanner.ScanFolders(
            new[] { conta }, relativo, AccountLeftoverScanOptions.Default with { SelectAllAccounts = true });
        Assert.True(Assert.Single(removendo).Selected);
    }

    [Fact]
    public void OItemDaContaAtualVemMarcadoSempre()
    {
        var conta = ContaFalsa(Path.Combine(_temp.Root, "perfil"));
        conta = new UserProfile
        {
            Sid = conta.Sid,
            LocalPath = conta.LocalPath,
            UserName = conta.UserName,
            IsCurrent = true,
        };
        Directory.CreateDirectory(Path.Combine(conta.LocalPath, "AppData", "Local", "Meu Programa"));

        var itens = AccountLeftoverScanner.ScanFolders(
            new[] { conta },
            new[] { @"AppData\Local\Meu Programa" },
            AccountLeftoverScanOptions.Default);

        Assert.True(Assert.Single(itens).Selected);
    }

    [Fact]
    public void CaminhoComRaizNaoEhProcuradoNoPerfilDeCadaConta()
    {
        // Um caminho absoluto pertence a maquina, nao a uma conta: procurá-lo
        // em cada perfil devolveria o mesmo item N vezes.
        var conta = ContaFalsa(Path.Combine(_temp.Root, "perfil"));
        var fora = _temp.CreateSubFolder("fora");
        Directory.CreateDirectory(Path.Combine(conta.LocalPath, "AppData", "Local"));

        var itens = AccountLeftoverScanner.ScanFolders(
            new[] { conta },
            new[] { fora },
            AccountLeftoverScanOptions.Default);

        Assert.Empty(itens);
    }

    [Fact]
    public void ASobraCompartilhadaNaoTemConta()
    {
        var pasta = _temp.CreateSubFolder("ProgramData\\Meu Programa");

        var itens = AccountLeftoverScanner.ScanShared(new[] { pasta });

        var item = Assert.Single(itens);
        Assert.True(item.IsShared);
        Assert.Null(item.Profile);
        Assert.True(item.Selected);
        Assert.Equal(AccountItemKind.Folder, item.Kind);
    }

    [Fact]
    public void OProgramDataEListadoComoPastaCompartilhada()
    {
        var compartilhadas = AccountLeftoverScanner.SharedRootPaths();

        Assert.NotEmpty(compartilhadas);
        Assert.Contains(compartilhadas, p => p.Contains("ProgramData", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ChaveDeRegistroQueNaoExisteNaoVirouSobra()
    {
        var conta = ContaFalsa(Path.Combine(_temp.Root, "perfil"));

        var itens = AccountLeftoverScanner.ScanRegistry(
            new[] { conta },
            new[] { @"Software\Classes\Meu Programa Que Nao Existe" },
            AccountLeftoverScanOptions.Default with { RequireLoadedHive = false });

        Assert.Empty(itens);
    }

    // -----------------------------------------------------------------
    // Fragmentos do CHKDSK
    // -----------------------------------------------------------------

    [Fact]
    public void OFragmentoDoChkdskEReconhecidoPeloNome()
    {
        Assert.True(ChkdskFragmentCleaner.IsFragmentFileName("FILE0000.CHK"));
        Assert.True(ChkdskFragmentCleaner.IsFragmentFileName(@"C:\FILE0001.chk"));

        Assert.False(ChkdskFragmentCleaner.IsFragmentFileName("FILE0000.TXT"));
        Assert.False(ChkdskFragmentCleaner.IsFragmentFileName("outro.txt"));
        Assert.False(ChkdskFragmentCleaner.IsFragmentFileName(string.Empty));
    }

    [Fact]
    public void OsFragmentosDoChkdskSaoEncontradosNaRaizDaUnidade()
    {
        var fragmento = Path.Combine(_temp.Root, "FILE0000.CHK");
        File.WriteAllText(fragmento, "fragmento");

        var encontrados = ChkdskFragmentCleaner.FindFragmentsIn(_temp.Root);

        Assert.Contains(encontrados, f =>
            string.Equals(f, fragmento, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ArquivoQueNaoEFragmentoDoChkdskNaoERemovido()
    {
        var arquivo = _temp.CreateFile("importante.txt", "nao apague");

        var resultado = ChkdskFragmentCleaner.Clean(arquivo);

        Assert.Equal(ChkdskCleanupStatus.Failed, resultado.Status);
        Assert.True(File.Exists(arquivo));
    }

    [Fact]
    public void FragmentoDoChkdskQueNaoExisteEJAInexistente()
    {
        var resultado = ChkdskFragmentCleaner.Clean(Path.Combine(_temp.Root, "FILE9999.CHK"));

        Assert.Equal(ChkdskCleanupStatus.AlreadyAbsent, resultado.Status);
    }

    [Fact]
    public void OFragmentoDoChkdskERemovido()
    {
        var fragmento = Path.Combine(_temp.Root, "FILE0002.CHK");
        File.WriteAllText(fragmento, "fragmento");

        var resultado = ChkdskFragmentCleaner.Clean(fragmento);

        Assert.Equal(ChkdskCleanupStatus.Removed, resultado.Status);
        Assert.False(File.Exists(fragmento));
    }

    // -----------------------------------------------------------------
    // Tracks Cleaner: o modulo dos fragmentos do CHKDSK
    // -----------------------------------------------------------------

    [Fact]
    public void OModuloDoChkdskDevolveArquivosEFragmentosDoChkdsk()
    {
        var itens = TrackCleaner.Scan(TrackModule.Chkdsk);

        Assert.NotNull(itens);
        Assert.All(itens, item =>
        {
            Assert.Equal(ChkdskFragmentCleaner.CategoryId, item.CategoryId);
            Assert.Equal("Arquivo", item.Kind);
            Assert.True(ChkdskFragmentCleaner.IsFragmentFileName(item.Path));
        });
    }

    [Fact]
    public void AVarreduraDoWindowsNaoListaMaisARaizDaUnidade()
    {
        // Antes, o item do CHKDSK era a RAIZ da unidade com um filtro "*.CHK".
        // Listar a raiz esconderia o que existe de verdade — e, com o filtro
        // trocado, apagaria o conteudo inteiro da unidade.
        var itens = TrackCleaner.Scan(TrackModule.Windows);

        Assert.DoesNotContain(itens, item =>
            item.CategoryId == ChkdskFragmentCleaner.CategoryId && item.Kind == "Pasta");
    }

    [Fact]
    public void ALimpezaDoTracksCleanerRemoveOFragmentoDoChkdsk()
    {
        var fragmento = Path.Combine(_temp.Root, "FILE0003.CHK");
        File.WriteAllText(fragmento, "fragmento");

        var item = new HistoryItem
        {
            Kind = "Arquivo",
            Path = fragmento,
            CategoryId = ChkdskFragmentCleaner.CategoryId,
            Detail = "Fragmentos recuperados pelo CHKDSK",
            Selected = true,
        };

        var report = TrackCleaner.Clean(new[] { item });

        Assert.Equal(1, report.RemovedCount);
        Assert.False(File.Exists(fragmento));
    }

    /// <summary>
    /// Uma conta de mentira, com o perfil apontando para uma pasta
    /// temporaria: a varredura por conta pode ser testada sem depender de
    /// existir uma segunda conta na maquina.
    /// </summary>
    private static UserProfile ContaFalsa(string localPath) => new()
    {
        Sid = "S-1-5-21-9999999999-9999999999-9999999999-1000",
        LocalPath = localPath,
        UserName = "conta-de-teste",
        IsCurrent = false,
        IsLoaded = false,
    };
}
