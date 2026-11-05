using System.IO;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O nucleo do Lote 2: o catalogo de colunas, a store de preferencias por
/// programa e a exportacao com escolha de colunas.
///
/// Cobre os cantos que a tela nao mostra sozinha: coluna desconhecida nas
/// preferencias, programa sem chave de registro, anotacao que sobra vazia e
/// os formatos de exportacao com colunas escolhidas.
/// </summary>
[Collection("preferencias")]
public class Lote2CoreTests : IDisposable
{
    private readonly string _file;

    public Lote2CoreTests()
    {
        _file = Path.Combine(Path.GetTempPath(), "tranal-lote2-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        ProgramCustomizationStore.FilePathOverride = _file;
        ProgramCustomizationStore.Reload();
    }

    public void Dispose()
    {
        ProgramCustomizationStore.ResetForTests();
        try { if (File.Exists(_file)) File.Delete(_file); }
        catch { /* o temporario e descartavel */ }
    }

    private static InstalledProgram Programa(string nome, string? chave = null) => new()
    {
        DisplayName = nome,
        RegistryKey = chave ?? "{guid-de-teste}",
        RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + (chave ?? "{guid-de-teste}"),
    };

    // ------------------------------------------------------------------
    // O catalogo de colunas
    // ------------------------------------------------------------------

    [Fact]
    public void SemPreferenciaTodasAsColunasAparecem()
    {
        var visiveis = ProgramColumns.Visible(null);

        Assert.Equal(ProgramColumns.All.Count, visiveis.Count);
    }

    [Fact]
    public void ColunaDesconhecidaNasPreferenciasNaoQuebraALista()
    {
        // As preferencias podem ter uma coluna que nao existe mais (o
        // catalogo mudou entre versoes). A lista nao pode quebrar por isso.
        var visiveis = ProgramColumns.Visible(new[] { "Size", "ColunaQueNaoExiste" });

        Assert.DoesNotContain(ProgramColumn.Size, visiveis);
        Assert.Contains(ProgramColumn.Name, visiveis);
        Assert.Equal(ProgramColumns.All.Count - 1, visiveis.Count);
    }

    [Fact]
    public void OValorDeUmaColunaSemAnotacaoEtextoVazio()
    {
        var programa = Programa("Meu App");

        Assert.Equal(string.Empty, ProgramColumns.Value(ProgramColumn.Note, programa, null));
        Assert.Equal(string.Empty, ProgramColumns.Value(ProgramColumn.Group, programa, null));
    }

    [Fact]
    public void VariosGruposAparecemSeparadosPorVirgula()
    {
        var programa = Programa("Meu App");

        ProgramCustomizationStore.CreateGroup("Jogos");
        ProgramCustomizationStore.CreateGroup("Trabalho");
        ProgramCustomizationStore.AddToGroup(programa, "Jogos");
        ProgramCustomizationStore.AddToGroup(programa, "Trabalho");

        var valor = ProgramColumns.Value(ProgramColumn.Group, programa, ProgramCustomizationStore.For(programa));

        Assert.Equal("Jogos, Trabalho", valor);
    }

    [Fact]
    public void TodoCatalogoTemRotuloDeReserva()
    {
        // Sem o rotulo de reserva, uma chave que falte no catalogo de idiomas
        // mostraria o nome da chave na tela.
        foreach (var coluna in ProgramColumns.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(coluna.FallbackLabel));
            Assert.False(string.IsNullOrWhiteSpace(coluna.ExportName));
            Assert.StartsWith("Common.", coluna.LabelKey, StringComparison.Ordinal);
        }
    }

    // ------------------------------------------------------------------
    // A store de preferencias
    // ------------------------------------------------------------------

    [Fact]
    public void ProgramaSemChaveDeRegistroUsaONome()
    {
        var programa = new InstalledProgram { DisplayName = "Programa Solto" };

        ProgramCustomizationStore.SetComment(programa, "anotacao");

        // A chave de reserva usa o nome exibido, com a visao do registro: dois
        // homonimos de visoes diferentes sao entradas diferentes.
        Assert.StartsWith("Programa Solto", ProgramCustomizationStore.KeyOf(programa));
        Assert.Equal("Programa Solto|Default", ProgramCustomizationStore.KeyOf(programa));
        Assert.Equal("anotacao", ProgramCustomizationStore.For(programa).Comment);
    }

    [Fact]
    public void DoisProgramasComOMesmoNomeTemAnotacoesSeparadas()
    {
        // A chave do registro e a identidade; quando ela falta, o nome entra
        // como reserva — e dois programas de mesmo nome sem chave sao o
        // mesmo programa, nao dois.
        var a = Programa("Office", "{a}");
        var b = Programa("Office", "{b}");

        ProgramCustomizationStore.SetComment(a, "do primeiro");
        ProgramCustomizationStore.SetComment(b, "do segundo");

        Assert.Equal("do primeiro", ProgramCustomizationStore.For(a).Comment);
        Assert.Equal("do segundo", ProgramCustomizationStore.For(b).Comment);
    }

    [Fact]
    public void AnotacaoVaziaDesapareceDoDisco()
    {
        var programa = Programa("Meu App");

        ProgramCustomizationStore.SetIcon(programa, 3, string.Empty);
        Assert.True(ProgramCustomizationStore.Has(programa));

        // Tirar o icone deixa a anotacao sem nada: ela some do arquivo.
        ProgramCustomizationStore.SetIcon(programa, -1, string.Empty);

        Assert.False(ProgramCustomizationStore.Has(programa));
    }

    [Fact]
    public void SairDoUltimoGrupoDesapareceComAAnotacao()
    {
        var programa = Programa("Meu App");

        ProgramCustomizationStore.CreateGroup("Jogos");
        ProgramCustomizationStore.AddToGroup(programa, "Jogos");
        Assert.True(ProgramCustomizationStore.Has(programa));

        ProgramCustomizationStore.RemoveFromGroup(programa, "Jogos");

        Assert.False(ProgramCustomizationStore.Has(programa));
    }

    [Fact]
    public void ApagarOGrupoTiraOsProgramasDele()
    {
        var programa = Programa("Meu App");

        ProgramCustomizationStore.CreateGroup("Jogos");
        ProgramCustomizationStore.AddToGroup(programa, "Jogos");

        ProgramCustomizationStore.DeleteGroup("Jogos");

        Assert.DoesNotContain("Jogos", ProgramCustomizationStore.For(programa).Groups);
    }

    [Fact]
    public void GrupoVazioNaoCria()
    {
        Assert.Equal(GroupResult.InvalidName, ProgramCustomizationStore.CreateGroup("   "));
        Assert.Equal(GroupResult.InvalidName, ProgramCustomizationStore.CreateGroup(string.Empty));
        Assert.Empty(ProgramCustomizationStore.Groups);
    }

    [Fact]
    public void RenomearParaOMesmoNomeNaoFazNada()
    {
        ProgramCustomizationStore.CreateGroup("Jogos");

        // Renomear "Jogos" para "jogos" (so muda a caixa) nao e renomear.
        Assert.Equal(GroupResult.InvalidName, ProgramCustomizationStore.RenameGroup("Jogos", "jogos"));
        Assert.Contains("Jogos", ProgramCustomizationStore.Groups);
    }

    [Fact]
    public void OsGruposEAsAnotacoesSobrevivemAoDisco()
    {
        var programa = Programa("Meu App");

        ProgramCustomizationStore.CreateGroup("Jogos");
        ProgramCustomizationStore.AddToGroup(programa, "Jogos");
        ProgramCustomizationStore.SetComment(programa, "nao mexer");

        ProgramCustomizationStore.Reload();

        Assert.Contains("Jogos", ProgramCustomizationStore.Groups);
        Assert.Contains("Jogos", ProgramCustomizationStore.For(programa).Groups);
        Assert.Equal("nao mexer", ProgramCustomizationStore.For(programa).Comment);
    }

    // ------------------------------------------------------------------
    // A exportacao com escolha de colunas
    // ------------------------------------------------------------------

    [Fact]
    public void OHtmlUsaAsColunasEscolhidasETitulosTraduzidos()
    {
        var original = Loc.Current;
        try
        {
            // O HTML e lido por gente: o titulo da coluna vem do catalogo de
            // idiomas, nao do nome fixo do arquivo.
            Loc.SetLanguage("pt-BR");

            var programa = Programa("Meu App");
            programa.DisplayVersion = "2.0";

            var html = Exporters.ToHtml(new[] { programa }, new[] { ProgramColumn.Name, ProgramColumn.Version });

            Assert.Contains("<th>Programa</th>", html);
            Assert.Contains("<th>Versão</th>", html);
            Assert.DoesNotContain("<th>Empresa</th>", html);
            Assert.Contains("<td>Meu App</td>", html);
        }
        finally
        {
            Loc.SetLanguage(original);
        }
    }

    [Fact]
    public void OXmlUsaAsColunasEscolhidas()
    {
        var programa = Programa("Meu App");

        var xml = Exporters.ToXml(new[] { programa }, new[] { ProgramColumn.Name, ProgramColumn.Note });

        Assert.Contains("<Entry>", xml);
        Assert.Contains("<Program>Meu App</Program>", xml);
        Assert.DoesNotContain("<Company>", xml);
    }

    [Fact]
    public void ORegistroDoXmlNaoTemOMesmoNomeDaColunaDoPrograma()
    {
        var programa = Programa("Meu App");

        var xml = Exporters.ToXml(new[] { programa });

        // "<Program><Program>" confundiria quem lê o arquivo.
        Assert.DoesNotContain("<Program><Program>", xml);
        Assert.Contains("<Entry>", xml);
        Assert.Contains("<Program>Meu App</Program>", xml);
    }

    [Fact]
    public void OTextoUsaAsColunasEscolhidas()
    {
        var programa = Programa("Meu App");
        programa.DisplayVersion = "2.0";

        var texto = Exporters.ToText(new[] { programa }, new[] { ProgramColumn.Name, ProgramColumn.Version });

        Assert.Equal("Meu App - 2.0", texto.Trim());
    }

    [Fact]
    public void OCsvEscapaOpontoEVirgulaEAsAspas()
    {
        var programa = Programa("Meu App");
        programa.Comments = "um; dois \"tres\"";

        var csv = Exporters.ToCsv(new[] { programa }, new[] { ProgramColumn.Comments });

        Assert.Contains("\"um; dois \"\"tres\"\"\"", csv);
    }

    [Fact]
    public void OHtmlEscapaAsMarcas()
    {
        var programa = Programa("Meu App");
        programa.Publisher = "Fabrica <X> & Cia";

        var html = Exporters.ToHtml(new[] { programa }, new[] { ProgramColumn.Company });

        Assert.Contains("Fabrica &lt;X&gt; &amp; Cia", html);
    }

    [Fact]
    public void AColunaDoRegistroVaiParaAExportacao()
    {
        var programa = Programa("Meu App", "{minha-chave}");

        var csv = Exporters.ToCsv(new[] { programa }, new[] { ProgramColumn.RegistryKey });

        Assert.Contains("{minha-chave}", csv);
    }

    [Fact]
    public void ExportarSemColunasUsaOConjuntoPadraoDoRevo()
    {
        var programa = Programa("Meu App");

        var cabecalho = Exporters.ToCsv(new[] { programa }).Split('\n')[0].Trim();

        // O conjunto padrao e explicito e fixo: o formato do arquivo exportado
        // nao pode depender do catalogo de colunas da tela, que o usuario
        // pode mudar.
        Assert.Equal(
            "Program;Version;Type;Date;Company;HomePage;Comments;UninstallString;RegistryKey",
            cabecalho);
    }

    [Fact]
    public void AsColunasPedidasResolvemPeloNomeDoArquivo()
    {
        // O nome aceito e o do arquivo exportado: fixo, para o comando
        // roteirizavel nao depender do idioma da interface.
        Assert.True(ProgramColumns.TryParse(new[] { "Program", "Version", "Note" }, out var colunas, out _));

        Assert.Equal(
            new[] { ProgramColumn.Name, ProgramColumn.Version, ProgramColumn.Note },
            colunas);
    }

    [Fact]
    public void AsColunasPedidasSaemNaOrdemDoCatalogo()
    {
        // independentemente da ordem pedida: o arquivo sai sempre igual.
        Assert.True(ProgramColumns.TryParse(new[] { "Note", "Name" }, out var colunas, out _));

        Assert.Equal(new[] { ProgramColumn.Name, ProgramColumn.Note }, colunas);
    }

    [Fact]
    public void ColunaDesconhecidaNoPedidoEUmErro()
    {
        Assert.False(ProgramColumns.TryParse(new[] { "Program", "ColunaQueNaoExiste" }, out _, out var desconhecida));

        Assert.Equal("ColunaQueNaoExiste", desconhecida);
    }

    [Fact]
    public void PedirColunaPelaDataNaoCabeNoNomeDoArquivo()
    {
        // O nome do arquivo exportado de "Date" e "Date"; o do enum tambem.
        Assert.True(ProgramColumns.TryParse(new[] { "Date" }, out var colunas, out _));

        Assert.Equal(new[] { ProgramColumn.Date }, colunas);
    }

    [Fact]
    public void AColunaVersaoMostraNoVersionComoORevo()
    {
        // A versao vazia vira "No Version" — e na lista e na exportacao, que
        // leem o mesmo valor.
        var programa = Programa("Meu App");

        Assert.Equal("No Version", ProgramColumns.Value(ProgramColumn.Version, programa, null));

        var comVersao = Programa("Meu App");
        comVersao.DisplayVersion = "2.0";
        Assert.Equal("2.0", ProgramColumns.Value(ProgramColumn.Version, comVersao, null));
    }

    [Fact]
    public void AExportacaoUsaNoVersionQuandoNaoHaVersao()
    {
        var programa = Programa("Meu App");

        var csv = Exporters.ToCsv(new[] { programa }, new[] { ProgramColumn.Name, ProgramColumn.Version });

        Assert.Contains("Meu App;No Version", csv);
    }

    [Fact]
    public void DoisProgramasDaMesmaChaveEmVisoesDiferentesSaoDiferentes()
    {
        // A mesma subchave pode existir nas visoes de 32 e de 64 bits. Sem a
        // visao na chave, a anotacao de uma apareceria na outra.
        var a = Programa("Meu App", "{mesma-chave}");
        var b = Programa("Meu App", "{mesma-chave}");

        a.View = Microsoft.Win32.RegistryView.Registry64;
        b.View = Microsoft.Win32.RegistryView.Registry32;

        Assert.NotEqual(ProgramCustomizationStore.KeyOf(a), ProgramCustomizationStore.KeyOf(b));
    }

    [Fact]
    public void AAnotacaoFicaNoProgramaCerto()
    {
        var a = Programa("Meu App", "{mesma-chave}");
        var b = Programa("Meu App", "{mesma-chave}");

        a.View = Microsoft.Win32.RegistryView.Registry64;
        b.View = Microsoft.Win32.RegistryView.Registry32;

        ProgramCustomizationStore.SetComment(a, "anotacao do de 64 bits");

        Assert.Equal("anotacao do de 64 bits", ProgramCustomizationStore.For(a).Comment);
        Assert.Equal(string.Empty, ProgramCustomizationStore.For(b).Comment);
    }

    // ------------------------------------------------------------------
    // A gravacao em disco
    // ------------------------------------------------------------------

    [Fact]
    public void OArquivoCorrompidoEPreservadoEmVezDeSobrescrito()
    {
        // Um arquivo ilegivel nao pode ser apagado pela gravacao seguinte: o
        // usuario ainda precisa do conteudo para recuperar.
        File.WriteAllText(_file, "{ isto nao e json");

        ProgramCustomizationStore.Reload();

        Assert.False(File.Exists(_file));
        Assert.True(File.Exists(_file + ".corrompido"));

        // E a gravacao seguinte cria um arquivo novo, sem tocar no original.
        Assert.True(ProgramCustomizationStore.SetComment(Programa("Meu App"), "anotacao"));
        Assert.True(File.Exists(_file));
        Assert.True(File.Exists(_file + ".corrompido"));
    }

    [Fact]
    public void AConteudoCorrompidoContinuaNoArquivoPreservado()
    {
        File.WriteAllText(_file, "{ isto nao e json");

        ProgramCustomizationStore.Reload();

        var preservado = File.ReadAllText(_file + ".corrompido");
        Assert.Contains("isto nao e json", preservado);
    }

    [Fact]
    public void AGranacaoDeixaUmaCopiaDaVersaoAnterior()
    {
        ProgramCustomizationStore.SetComment(Programa("Meu App"), "primeiro");
        ProgramCustomizationStore.SetComment(Programa("Meu App"), "segundo");

        // A gravacao e atomica e guarda a versao anterior.
        Assert.True(File.Exists(_file + ".bak"));
        Assert.Contains("primeiro", File.ReadAllText(_file + ".bak"));
        Assert.Contains("segundo", File.ReadAllText(_file));
    }

    [Fact]
    public void QuandoAGravacaoFalhaOChamadorDescobre()
    {
        // Sem isto, o usuario veria o comentario na lista e ele sumiria ao
        // fechar o aplicativo.
        var pasta = Path.Combine(Path.GetTempPath(), "tranal-lote2-dir-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(pasta);

        // Um arquivo no lugar da pasta: nenhum caminho abaixo dele pode ser
        // criado.
        var bloqueio = Path.Combine(pasta, "bloqueio");
        File.WriteAllText(bloqueio, "x");

        ProgramCustomizationStore.FilePathOverride = Path.Combine(bloqueio, "program-data.json");

        var gravou = ProgramCustomizationStore.SetComment(Programa("Meu App"), "anotacao");

        Assert.False(gravou);

        try { Directory.Delete(pasta, true); } catch { /* o temporario e descartavel */ }
    }

    [Fact]
    public void OGrupoNaoFicaNaMemoriaQuandoAGravacaoFalha()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "tranal-lote2-dir-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(pasta);

        var bloqueio = Path.Combine(pasta, "bloqueio");
        File.WriteAllText(bloqueio, "x");

        ProgramCustomizationStore.FilePathOverride = Path.Combine(bloqueio, "program-data.json");

        Assert.Equal(GroupResult.SaveFailed, ProgramCustomizationStore.CreateGroup("Jogos"));
        Assert.Empty(ProgramCustomizationStore.Groups);

        try { Directory.Delete(pasta, true); } catch { /* o temporario e descartavel */ }
    }
}
