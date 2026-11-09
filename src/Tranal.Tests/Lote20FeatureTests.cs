using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Topmost;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O gerenciador de menu de contexto: as entradas do Explorer, do Tranal e
/// dos outros programas.
///
/// E o "Context Menu Manager" do HiBit (medido no catalogo dele): uma tela
/// onde o usuario ve o que os programas deixaram no botao direito do
/// Explorer, e onde pode desativar e remover.
///
/// <para>A regra mora no nucleo (<c>Core/Management/ContextMenuOverview.cs</c>)
/// e a suite testa esse modulo. A ligacao com a interface (o botao na barra
/// lateral, a tela e a linha de comando) e conferida sobre o fonte, pelo
/// mesmo motivo das outras paridades: a suite nao referencia o projeto
/// grafico (WPF).</para>
///
/// <para>Nenhum teste aqui escreve no registro: os comandos que alteram a
/// maquina (<c>Restore</c>, <c>Disable</c> e <c>Remove</c>) so sao verificados
/// por assinatura e por fonte.</para>
/// </summary>
public class Lote20FeatureTests
{
    // ------------------------------------------------------------------
    // A regra: o nucleo
    // ------------------------------------------------------------------

    [Fact]
    public void AEntradaDeDesinstalarDoTranalApareceNoGerenciador()
    {
        var desinstalar = Assert.Single(
            ContextMenuOverview.TranalEntries(),
            e => e.Id == ExplorerContextMenu.ShellKeyName);

        Assert.Equal(ContextMenuKind.Tranal, desinstalar.Kind);
        Assert.True(desinstalar.CanRemove);
        Assert.Contains(ExplorerContextMenu.ShellKeyName, desinstalar.Location);
    }

    [Fact]
    public void CadaMenuDoTranalApareceUmaVezSo()
    {
        // Uma entrada do Tranal pode morar em mais de um lugar do registro (o
        // "Rastrear a instalacao" vale para .exe e para .msi): na tela ela
        // e UMA linha, com todos os lugares onde mora.
        var distintos = ExplorerShellMenu.Entries
            .Select(e => e.DisplayName)
            .Distinct(StringComparer.Ordinal)
            .Count();

        var entradas = ContextMenuOverview.TranalEntries()
            .Where(e => e.Kind == ContextMenuKind.Tranal)
            .ToList();

        Assert.Equal(distintos + 1, entradas.Count);
    }

    [Fact]
    public void OsIdentificadoresDasEntradasDoTranalSaoOsEsperados()
    {
        var ids = ContextMenuOverview.TranalEntries()
            .Where(e => e.Kind == ContextMenuKind.Tranal)
            .Select(e => e.Id)
            .OrderBy(i => i, StringComparer.Ordinal)
            .ToArray();

        var esperados = new[]
        {
            ExplorerContextMenu.ShellKeyName,
            ExplorerShellMenu.InstallAndTraceId,
            ExplorerShellMenu.MonitorId,
            ExplorerShellMenu.ShredId,
        }.OrderBy(i => i, StringComparer.Ordinal).ToArray();

        Assert.Equal(esperados, ids);
    }

    [Fact]
    public void AEntradaQueRastreiaAMostraOsDoisLugaresDoRegistro()
    {
        var rastreio = Assert.Single(
            ContextMenuOverview.TranalEntries(),
            e => e.Id == ExplorerShellMenu.InstallAndTraceId);

        Assert.Contains(".exe", rastreio.Location);
        Assert.Contains(".msi", rastreio.Location);
    }

    [Fact]
    public void OEstadoDaEntradaRefleteOQueEstaNoRegistro()
    {
        var desinstalar = ContextMenuOverview.TranalEntries()
            .First(e => e.Id == ExplorerContextMenu.ShellKeyName);

        Assert.Equal(ExplorerContextMenu.IsRegistered(), desinstalar.IsEnabled);
    }

    [Fact]
    public void CadaEntradaDaMaquinaTemIdentificadorUnico()
    {
        var todas = ContextMenuOverview.List();

        var ids = todas.Select(e => e.Id).ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void AsEntradasDeTerceirosTaoNoCatalogoDoRegistro()
    {
        foreach (var item in ContextMenuOverview.OtherEntries())
        {
            Assert.True(
                item.Kind == ContextMenuKind.Programa || item.Kind == ContextMenuKind.Windows,
                $"Entrada de terceiro com dono inesperado: {item.Kind}");

            // O identificador de uma entrada de terceiro e o caminho da chave
            // do registro: e o que a distingue de outra com o mesmo nome.
            Assert.StartsWith("HKEY_", item.Id, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AQuantidadeDeEntradasDeTerceirosBateComORegistro()
    {
        Assert.Equal(
            ContextMenuManager.List().Count,
            ContextMenuOverview.OtherEntries().Count);
    }

    [Fact]
    public void ODonoDeUmaEntradaDeTerceiroSoApareceComODaCaminhoDaDll()
    {
        foreach (var item in ContextMenuOverview.OtherEntries())
        {
            if (item.Program.Length == 0) continue;

            Assert.False(string.IsNullOrWhiteSpace(item.ServerPath));
        }
    }

    [Fact]
    public void AProcuraEncontraAEntradaPeloIdentificador()
    {
        var item = ContextMenuOverview.Find(ExplorerShellMenu.ShredId);

        Assert.NotNull(item);
        Assert.Equal(ContextMenuKind.Tranal, item!.Kind);
        Assert.Equal(ExplorerShellMenu.ShredId, item.Id);
    }

    [Fact]
    public void AProcuraAceitaOTituloTambem()
    {
        var titulo = ContextMenuOverview.TranalEntries()
            .First(e => e.Id == ExplorerShellMenu.ShredId)
            .Title;

        var item = ContextMenuOverview.Find(titulo);

        Assert.NotNull(item);
        Assert.Equal(ExplorerShellMenu.ShredId, item!.Id);
    }

    [Fact]
    public void AProcuraDeAlgoQueNaoExisteDevolveNulo()
    {
        Assert.Null(ContextMenuOverview.Find("entrada-que-nao-existe-xyz"));
        Assert.Null(ContextMenuOverview.Find(string.Empty));
        Assert.Null(ContextMenuOverview.Find("   "));
    }

    // ------------------------------------------------------------------
    // A regra: remover uma entrada pelo identificador
    // ------------------------------------------------------------------

    [Fact]
    public void AsEntradasDoTranalPodemSerRemovidasPeloIdentificador()
    {
        // O comando existe e e publico: e o que a tela e a linha de comando
        // usam para tirar uma entrada do Explorer.
        var metodo = typeof(ExplorerShellMenu).GetMethod("Remove");

        Assert.NotNull(metodo);
        Assert.True(metodo!.IsPublic);
        Assert.Equal(typeof(int), metodo.ReturnType);
    }

    [Fact]
    public void AEntradaQueRastreiaMoraEmDuasChavesAoMesmoTempo()
    {
        var entradas = ExplorerShellMenu.EntriesOf(ExplorerShellMenu.InstallAndTraceId);

        Assert.Equal(2, entradas.Count);
        Assert.All(entradas, e => Assert.StartsWith(ExplorerShellMenu.InstallAndTraceId, e.Id, StringComparison.Ordinal));
    }

    [Fact]
    public void AEntradaQueRastreiaRemoveTodasAsChavesDela()
    {
        // "Rastrear a instalacao" vale para .exe e para .msi: remover a
        // entrada tem que tirar as duas, e nao so a primeira.
        var ids = ExplorerShellMenu.EntriesOf(ExplorerShellMenu.InstallAndTraceId)
            .Select(e => e.Id)
            .ToArray();

        Assert.Contains(ExplorerShellMenu.InstallAndTraceId, ids);
        Assert.Contains(ExplorerShellMenu.InstallAndTraceId + "Msi", ids);
    }

    [Fact]
    public void AChaveDaEntradaDeDesinstalarEOValorEsperado()
    {
        Assert.Equal(
            @"Software\Classes\*\shell\TranalUninstaller",
            ExplorerContextMenu.ShellKeyRelative);
    }

    // ------------------------------------------------------------------
    // A regra: o dono das entradas, resolvido em lote
    // ------------------------------------------------------------------

    [Fact]
    public void AResolucaoEmLoteAceitaUmaListaDeCaminhos()
    {
        var mapa = HunterMode.ResolveExecutables(new[] { @"C:\nao-existe\programa.exe" });

        Assert.NotNull(mapa);
        Assert.Empty(mapa);
    }

    [Fact]
    public void AResolucaoEmLoteAceitaUmaListaVazia()
    {
        Assert.Empty(HunterMode.ResolveExecutables(Array.Empty<string>()));
        Assert.Empty(HunterMode.ResolveExecutables(null!));
    }

    [Fact]
    public void AResolucaoEmLoteFazUmaVarreduraSo()
    {
        // Resolver um caminho por vez refaz a varredura de programas
        // instalados a cada chamada. O gerenciador usa a versao em lote.
        var texto = RepoFiles.Read("src", "Tranal.Uninstaller", "Core", "Modules", "HunterMode.cs");

        Assert.Contains("ResolveExecutables", texto);

        var metodo = RepoFiles.Metodo(texto, "ResolveExecutables");
        var chamadas = metodo.Split("ScanPrograms()").Length - 1;

        Assert.Equal(1, chamadas);
    }

    // ------------------------------------------------------------------
    // A ligacao com a interface e a linha de comando
    // ------------------------------------------------------------------

    [Fact]
    public void AFerramentaEstaNoCatalogoDeFerramentas()
    {
        Assert.Contains(ToolCatalog.ContextMenu, ToolCatalog.All);
    }

    [Fact]
    public void ATelaDoGerenciadorExiste()
    {
        var texto = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Tools", "ToolViews.cs");

        Assert.Contains("ToolCatalog.ContextMenu", texto);
        Assert.Contains("ContextMenuViews.Create()", texto);
    }

    [Fact]
    public void OBotaoEstaNaBarraLateral()
    {
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml");

        Assert.Contains("AutomationProperties.AutomationId=\"ToolContextMenu\"", xaml);
        Assert.Contains("Tag=\"ContextMenu\"", xaml);
        Assert.Contains("Module.ContextMenu", xaml);
    }

    [Fact]
    public void ATelaTemOsQuatroComandos()
    {
        var tela = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Tools", "ContextMenuViews.cs");

        foreach (var id in new[]
                 {
                     "ContextMenuRefresh",
                     "ContextMenuRestore",
                     "ContextMenuDisable",
                     "ContextMenuRemove",
                 })
        {
            Assert.Contains(id, tela);
        }
    }

    [Fact]
    public void ATelaMostraAsColunasDoGerenciador()
    {
        var tela = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Tools", "ContextMenuViews.cs");

        foreach (var coluna in new[]
                 {
                     "ContextMenu.Column.Item",
                     "ContextMenu.Column.Owner",
                     "ContextMenu.Column.Location",
                     "ContextMenu.Column.State",
                     "ContextMenu.Column.Program",
                 })
        {
            Assert.Contains(coluna, tela);
        }
    }

    [Fact]
    public void ATelaUsaAsMensagensDoCatalogo()
    {
        var tela = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Tools", "ContextMenuViews.cs");

        Assert.Contains("ContextMenu.Summary", tela);
        Assert.Contains("ContextMenu.Failed", tela);
        Assert.Contains("ContextMenu.ConfirmRemove", tela);
        Assert.Contains("ContextMenu.Owner.Tranal", tela);
        Assert.Contains("ContextMenu.Owner.Other", tela);
    }

    [Fact]
    public void ALinhaDeComandoTemOComando()
    {
        var comandos = RepoFiles.Read("src", "Tranal.Cli", "ExtraCommands.cs");

        Assert.Contains("\"/contextmenu\"", comandos);
        Assert.Contains("RunContextMenu", comandos);

        var ajuda = RepoFiles.Read("src", "Tranal.Cli", "Program.cs");
        Assert.Contains("/contextmenu -", ajuda);
    }

    [Fact]
    public void ALinhaDeComandoTemAsOpcoesDoGerenciador()
    {
        var comandos = RepoFiles.Read("src", "Tranal.Cli", "ExtraCommands.cs");

        Assert.Contains("\"/restore\"", comandos);
        Assert.Contains("\"/disable\"", comandos);
        Assert.Contains("\"/remove\"", comandos);
    }

    // ------------------------------------------------------------------
    // Os idiomas
    // ------------------------------------------------------------------

    [Fact]
    public void OGerenciadorTemTituloEDescricaoNosDoisIdiomas()
    {
        var pt = Loc.Load("pt-BR");
        var en = Loc.Load("en-US");

        foreach (var key in new[] { "Module.ContextMenu", "Desc.ContextMenu" })
        {
            Assert.True(pt.ContainsKey(key), $"Falta '{key}' no catalogo pt-BR.");
            Assert.True(en.ContainsKey(key), $"Falta '{key}' no catalogo en-US.");
        }
    }

    [Fact]
    public void AsMensagensDoGerenciadorEstaoNosDoisIdiomas()
    {
        var pt = Loc.Load("pt-BR");
        var en = Loc.Load("en-US");

        foreach (var key in new[]
                 {
                     "ContextMenu.Column.Item",
                     "ContextMenu.Column.Owner",
                     "ContextMenu.Column.Location",
                     "ContextMenu.Column.State",
                     "ContextMenu.Column.Program",
                     "ContextMenu.Restore",
                     "ContextMenu.Disable",
                     "ContextMenu.Summary",
                     "ContextMenu.Failed",
                     "ContextMenu.ConfirmRemove",
                     "ContextMenu.Owner.Tranal",
                     "ContextMenu.Owner.Other",
                 })
        {
            Assert.True(pt.ContainsKey(key), $"Falta '{key}' no catalogo pt-BR.");
            Assert.True(en.ContainsKey(key), $"Falta '{key}' no catalogo en-US.");
        }
    }

    [Fact]
    public void OsDoisIdiomasTemAsMesmasChaves()
    {
        var pt = Loc.Load("pt-BR");
        var en = Loc.Load("en-US");

        var soPt = pt.Keys.Where(k => !en.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        var soEn = en.Keys.Where(k => !pt.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToArray();

        Assert.True(soPt.Length == 0, $"So no pt-BR: {string.Join(", ", soPt)}");
        Assert.True(soEn.Length == 0, $"So no en-US: {string.Join(", ", soEn)}");
    }

    // ------------------------------------------------------------------
    // A regra: as entradas de terceiros, montadas sem tocar no registro
    // ------------------------------------------------------------------

    [Fact]
    public void AEntradaDeTerceiroMontaOCaminhoCompletoDaChave()
    {
        var entradas = ContextMenuManager.Build(
            new[]
            {
                new ShellHandlerRef(
                    @"*\shellex\ContextMenuHandlers",
                    "Exemplo",
                    "Exemplo",
                    "{EXEMPLO-0000-0000-000000000000}",
                    false,
                    RegistryView.Registry64),
            },
            h => @"C:\Programas\Exemplo\exemplo.dll");

        var entrada = Assert.Single(entradas);

        Assert.Equal("Exemplo", entrada.DisplayName);
        Assert.Equal(
            @"HKEY_CURRENT_USER\Software\Classes\*\shellex\ContextMenuHandlers\Exemplo",
            entrada.HandlerKeyPath);
        Assert.Equal(@"Software\Classes\*\shellex\ContextMenuHandlers", entrada.Location);
        Assert.Equal(@"*\shellex\ContextMenuHandlers", entrada.RelativeLocation);
        Assert.Equal(@"C:\Programas\Exemplo\exemplo.dll", entrada.ServerPath);
        Assert.False(entrada.IsMachineWide);
        Assert.Equal("Registry64", entrada.RegistryView);
    }

    [Fact]
    public void AEntradaDeTerceiroDoSistemaCompletoTemARaizDoRegistroCerta()
    {
        var entradas = ContextMenuManager.Build(
            new[]
            {
                new ShellHandlerRef(
                    @"Directory\shellex\ContextMenuHandlers",
                    "Pasta",
                    "Pasta",
                    "{EXEMPLO-0000-0000-000000000001}",
                    true,
                    RegistryView.Registry32),
            },
            h => string.Empty);

        var entrada = Assert.Single(entradas);

        Assert.True(entrada.IsMachineWide);
        Assert.StartsWith(@"HKEY_LOCAL_MACHINE", entrada.HandlerKeyPath, StringComparison.Ordinal);
    }

    [Fact]
    public void ManipuladorSemClsidNaoViraEntrada()
    {
        var entradas = ContextMenuManager.Build(
            new[]
            {
                new ShellHandlerRef(
                    @"*\shellex\ContextMenuHandlers",
                    "Vazio",
                    "Vazio",
                    string.Empty,
                    false,
                    RegistryView.Registry64),
            },
            h => string.Empty);

        Assert.Empty(entradas);
    }

    [Fact]
    public void AEntradaDeTerceiroViraLinhaComOIdDaChave()
    {
        var entries = ContextMenuManager.Build(
            new[]
            {
                new ShellHandlerRef(
                    @"*\shellex\ContextMenuHandlers",
                    "Exemplo",
                    "Exemplo",
                    "{EXEMPLO-0000-0000-000000000002}",
                    false,
                    RegistryView.Registry64),
            },
            h => @"C:\Programas\Exemplo\exemplo.dll");

        var item = Assert.Single(ContextMenuOverview.Build(entries));

        Assert.Equal(ContextMenuKind.Programa, item.Kind);
        Assert.True(item.CanRemove);
        Assert.Equal(
            @"HKEY_CURRENT_USER\Software\Classes\*\shellex\ContextMenuHandlers\Exemplo",
            item.Id);
        Assert.Equal("Exemplo", item.Title);
        Assert.Equal(@"C:\Programas\Exemplo\exemplo.dll", item.ServerPath);
        Assert.True(item.CanRemove);
    }

    [Fact]
    public void AEntradaDeTerceiroSemNomeMostraOClsid()
    {
        var entries = ContextMenuManager.Build(
            new[]
            {
                new ShellHandlerRef(
                    @"*\shellex\ContextMenuHandlers",
                    string.Empty,
                    string.Empty,
                    "{EXEMPLO-0000-0000-000000000003}",
                    false,
                    RegistryView.Registry64),
            },
            h => string.Empty);

        var item = Assert.Single(ContextMenuOverview.Build(entries));

        Assert.Equal("{EXEMPLO-0000-0000-000000000003}", item.Title);
    }

    [Fact]
    public void AResolucaoEmLoteConcordaComAResolucaoDeUmEmUm()
    {
        // A regra do casamento e uma so: a versao em lote tem que dar a mesma
        // resposta que a versao de um em um.
        var programas = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = false,
            ComputeFolderSize = false,
            RequireUninstallString = false,
        });

        string? executavel = null;

        foreach (var programa in programas)
        {
            var local = programa.InstallLocation;
            if (string.IsNullOrWhiteSpace(local)) continue;
            if (!Directory.Exists(local)) continue;

            var candidato = Directory.EnumerateFiles(local, "*.exe", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
            if (candidato is null) continue;

            executavel = candidato;
            break;
        }

        // Maquina sem programas instalados: nao ha o que casar.
        if (executavel is null) return;

        var um = HunterMode.ResolveExecutable(executavel);
        var lote = HunterMode.ResolveExecutables(new[] { executavel });

        Assert.Equal(um.Found, lote.ContainsKey(executavel));

        if (um.Found && um.Program is not null)
            Assert.Equal(um.Program.DisplayName, lote[executavel].DisplayName);
    }

    [Fact]
    public void OsLocaisDoGerenciadorSaoRelativosAChaveDeClasses()
    {
        // A chave ja aberta e "Software\Classes": um local que comeca por
        // "Software\Classes" procuraria a chave duas vezes e nunca
        // encontraria nada. Foi o que acontecia — medido no teste de ponta a
        // ponta, quando a entrada de terceiro criada para o teste nao
        // aparecia na listagem.
        var texto = RepoFiles.Read("src", "Tranal.Uninstaller", "Core", "Management", "ContextMenuManager.cs");

        var inicio = texto.IndexOf("HandlerLocations =", StringComparison.Ordinal);
        Assert.True(inicio > 0, "a lista de locais do gerenciador nao foi encontrada");

        var fim = texto.IndexOf("};", inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio, "a lista de locais do gerenciador nao foi fechada");

        var trecho = texto.Substring(inicio, fim - inicio);

        Assert.DoesNotContain("Software\\Classes", trecho);
    }

    [Fact]
    public void AEntradaDoWindowsNaoPodeSerRemovida()
    {
        // As entradas do Windows ("Open With", "Sharing", "Enviar para"...)
        // nao sao sobra de programa: remove-las quebra o Explorer.
        var shell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "system32",
            "shell32.dll");

        var entries = ContextMenuManager.Build(
            new[]
            {
                new ShellHandlerRef(
                    @"*\shellex\ContextMenuHandlers",
                    "Open With",
                    "Open With",
                    "{EXEMPLO-0000-0000-000000000004}",
                    false,
                    RegistryView.Registry64),
            },
            h => shell);

        Assert.True(Assert.Single(entries).IsSystem);

        var item = Assert.Single(ContextMenuOverview.Build(entries));

        Assert.Equal(ContextMenuKind.Windows, item.Kind);
        Assert.False(item.CanRemove);
    }

    [Fact]
    public void AEntradaDeProgramaPodeSerRemovida()
    {
        var entries = ContextMenuManager.Build(
            new[]
            {
                new ShellHandlerRef(
                    @"*\shellex\ContextMenuHandlers",
                    "Exemplo",
                    "Exemplo",
                    "{EXEMPLO-0000-0000-000000000005}",
                    false,
                    RegistryView.Registry64),
            },
            h => @"C:\Programas\Exemplo\exemplo.dll");

        Assert.False(Assert.Single(entries).IsSystem);

        var item = Assert.Single(ContextMenuOverview.Build(entries));

        Assert.Equal(ContextMenuKind.Programa, item.Kind);
        Assert.True(item.CanRemove);
    }

    [Fact]
    public void AEntradaSemDllConhecidaEDoWindows()
    {
        // As classes internas do shell (o "Fixar na barra de tarefas", o
        // "Offline Files") nao declaram caminho: elas sao do Windows.
        Assert.True(ContextMenuManager.IsWindowsEntry(string.Empty));
        Assert.True(ContextMenuManager.IsWindowsEntry("   "));
        Assert.False(ContextMenuManager.IsWindowsEntry(@"C:\Programas\Exemplo\exemplo.dll"));
    }

    [Fact]
    public void AEntradaDoWindowsEhRecusadaNaRemocao()
    {
        var shell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "system32",
            "shell32.dll");

        var entries = ContextMenuManager.Build(
            new[]
            {
                new ShellHandlerRef(
                    @"*\shellex\ContextMenuHandlers",
                    "Open With",
                    "Open With",
                    "{EXEMPLO-0000-0000-000000000006}",
                    false,
                    RegistryView.Registry64),
            },
            h => shell);

        var item = Assert.Single(ContextMenuOverview.Build(entries));

        var resultado = ContextMenuOverview.Remove(item);

        Assert.False(resultado.Success);
        Assert.Contains("Windows", resultado.Message);
    }

    [Fact]
    public void AsDuasConvencoesDoExplorerSaoReconhecidas()
    {
        // O Explorer usa as duas convencoes e elas convivem: as vezes o NOME
        // da chave e o CLSID e o valor padrao e o nome bonitinho ("Taskband
        // Pin"); as vezes e o contrario ("Open With" -> o valor padrao e o
        // CLSID). O CLSID e o dos dois que parece um CLSID.
        Assert.True(ContextMenuManager.PareceClsid("{90AA3A4E-1CBA-4233-B8BB-535773D48449}"));
        Assert.True(ContextMenuManager.PareceClsid("  {90AA3A4E-1CBA-4233-B8BB-535773D48449}  "));

        Assert.False(ContextMenuManager.PareceClsid("Taskband Pin"));
        Assert.False(ContextMenuManager.PareceClsid("Open With"));
        Assert.False(ContextMenuManager.PareceClsid(string.Empty));
        Assert.False(ContextMenuManager.PareceClsid("   "));
        Assert.False(ContextMenuManager.PareceClsid("{"));
    }

    [Fact]
    public void AEntradaComOClsidComoNomeDaChaveTemOClsidCerto()
    {
        // Medido na maquina: "Fixar na barra de tarefas" e "Fixar no menu
        // Iniciar" tem o CLSID como nome da chave. Quem so olhar o valor
        // padrao nao ve essas entradas — foi o que acontecia.
        var entradas = ContextMenuManager.List();

        foreach (var entrada in entradas.Where(e => ContextMenuManager.PareceClsid(e.HandlerName)))
        {
            Assert.True(ContextMenuManager.PareceClsid(entrada.Clsid));
            Assert.Equal(entrada.HandlerName, entrada.Clsid);
        }
    }

    // ------------------------------------------------------------------
    // A regra: criar e apagar tudo
    // ------------------------------------------------------------------

    [Fact]
    public void OIdentificadorDaEntradaDoUsuarioVemDoNome()
    {
        // O nome escolhido e o que o usuario ve; o identificador e o que o
        // Explorer usa como chave do verbo.
        Assert.Equal("TranalMeuComando", CustomShellMenu.IdFor("Meu Comando"));
        Assert.Equal("Tranalmeucomando", CustomShellMenu.IdFor("meu-comando"));
        Assert.StartsWith(CustomShellMenu.IdPrefix, CustomShellMenu.IdFor(""), StringComparison.Ordinal);
    }

    [Fact]
    public void CriarEntradaSemNomeOuSemComandoEhRecusado()
    {
        // Nenhuma das duas tentativas toca no registro: a recusa vem antes.
        var semNome = ContextMenuOverview.Create(string.Empty, "notepad.exe");
        Assert.False(semNome.Success);
        Assert.Contains("nome", semNome.Message);

        var semComando = ContextMenuOverview.Create("Meu Comando", "   ");
        Assert.False(semComando.Success);
        Assert.Contains("comando", semComando.Message);
    }

    [Fact]
    public void AsEntradasFixasDoTranalNaoSaoEntradasDoUsuario()
    {
        // A entrada de desinstalar e a de shredar moram na mesma pasta dos
        // verbos: o gerenciador nao pode confundir as duas coisas.
        Assert.DoesNotContain(
            ExplorerContextMenu.ShellKeyName,
            CustomShellMenu.List().Select(e => e.Id));

        Assert.DoesNotContain(
            ExplorerShellMenu.ShredId,
            CustomShellMenu.List().Select(e => e.Id));
    }

    [Fact]
    public void ATelaTemOsComandosDeCriarEApagarTudo()
    {
        var tela = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Tools", "ContextMenuViews.cs");

        Assert.Contains("ContextMenuCreate", tela);
        Assert.Contains("ContextMenuRemoveAll", tela);
        Assert.Contains("ContextMenuOverview.Create", tela);
        Assert.Contains("ContextMenuOverview.RemoveAll", tela);
    }

    [Fact]
    public void ALinhaDeComandoTemAsOpcoesDeCriarEApagarTudo()
    {
        var comandos = RepoFiles.Read("src", "Tranal.Cli", "ExtraCommands.cs");

        Assert.Contains("\"/create\"", comandos);
        Assert.Contains("\"/removeall\"", comandos);
        Assert.Contains("ContextMenuOverview.Create", comandos);
        Assert.Contains("ContextMenuOverview.RemoveAll", comandos);
    }

    [Fact]
    public void OEscritorDoVerboDoExplorerFicouEmUmLugarSo()
    {
        // A regra do verbo (o valor padrao, o MUIVerb, o Icon e a subchave
        // "command") mora em um lugar so: quem grava um verbo do Explorer
        // passa pelo mesmo escritor.
        var texto = RepoFiles.Read("src", "Tranal.Uninstaller", "Core", "Topmost", "ExplorerContextMenu.cs");

        Assert.Contains("WriteShellEntry", texto);

        var chamadas = texto.Split("WriteShellEntry(").Length - 1;
        Assert.True(chamadas >= 2, $"o escritor unico foi chamado {chamadas} vez(es)");

        // Nenhum outro modulo escreve a subchave "command" por conta propria.
        var shell = RepoFiles.Read("src", "Tranal.Uninstaller", "Core", "Topmost", "CustomShellMenu.cs");
        Assert.DoesNotContain("CreateSubKey", shell);
    }
}
