using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Paridade da interface do Lote 2: colunas customizaveis, exportacao com
/// escolha de colunas, grupos, comentario e icone por programa.
///
/// A verificacao e feita sobre o fonte da interface, que e a unica forma de
/// conferir a cobertura sem acoplar a suite ao WPF (referenciar o projeto
/// grafico criaria dois tipos "Core" diferentes).
/// </summary>
public class Lote2UiParityTests
{
    /// <summary>Le um arquivo do projeto grafico.</summary>
    private static string Read(params string[] parts) => RepoFiles.Read(parts);

    [Fact]
    public void AJanelaDeColunasUsaOCatalogoUnico()
    {
        var code = Read("src", "Tranal.Uninstaller", "Ui", "ColumnsWindow.xaml.cs");

        // "Columns" (15054) e "Select the columns you want to export:" (15066)
        // sao a mesma tela com propositos diferentes: as duas vem do catalogo
        // de colunas, nao de duas listas escritas a mao.
        Assert.Contains("ProgramColumns.All", code);
        Assert.Contains("ProgramColumns.Label(info.Id)", code);

        // A coluna do nome e fixa na lista.
        Assert.Contains("info.Id == ProgramColumn.Name", code);
    }

    [Fact]
    public void AJanelaDeColunasExigePeloMenosUmaColuna()
    {
        var code = Read("src", "Tranal.Uninstaller", "Ui", "ColumnsWindow.xaml.cs");

        // 15298: "You must select at least one column to export!"
        Assert.Contains("Columns.AtLeastOne", code);
        Assert.Contains("if (!HasSelection)", code);
    }

    [Fact]
    public void AExportacaoPedeAsColunasAoUsuario()
    {
        var code = Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        // 15066: "Select the columns you want to export:"
        Assert.Contains("new ColumnsWindow(forExport: true", code);
        Assert.Contains("Exporters.ToCsv(rows.Select(r => r.Program), selecionadas)", code);
        Assert.Contains("Exporters.ToHtml(rows.Select(r => r.Program), selecionadas)", code);
        Assert.Contains("Exporters.ToXml(rows.Select(r => r.Program), selecionadas)", code);
        Assert.Contains("Exporters.ToText(rows.Select(r => r.Program), selecionadas)", code);
    }

    [Fact]
    public void OMenuDeContextoDaListaTemComentarioIconeEGrupos()
    {
        var code = Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        // 15216/15217: "Add Comment"/"Edit Comment"
        Assert.Contains("Cmd.AddComment", code);
        Assert.Contains("Cmd.EditComment", code);

        // 15079: "Change Icon"
        Assert.Contains("Cmd.ChangeIcon", code);

        // 15080/15081: "Add to Group"/"Remove from Group"
        Assert.Contains("Cmd.AddToGroup", code);
        Assert.Contains("Cmd.RemoveFromGroup", code);
    }

    [Fact]
    public void OCliqueDireitoNaListaSelecionaALinha()
    {
        var xaml = Read("src", "Tranal.Uninstaller", "MainWindow.xaml");
        var code = Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        // Sem isso, o menu de contexto age sobre a linha selecionada antes:
        // o usuario anotaria o programa errado, sem nenhum aviso.
        Assert.Contains("PreviewMouseRightButtonDown=\"ProgramsList_PreviewMouseRightButtonDown\"", xaml);

        // A assercao e ancorada no metodo: a mesma atribuicao existe em outro
        // lugar do arquivo, e um "contem" solto passaria mesmo sem o metodo.
        var metodo = RepoFiles.Metodo(code, "ProgramsList_PreviewMouseRightButtonDown");

        // A linha apontada entra na selecao.
        Assert.Contains("ProgramsList.SelectedItems.Add(row);", metodo);

        // E a selecao e multipla (desinstalacao em lote): clicar com o botao
        // direito em uma linha JA selecionada nao descarta as outras. Sem
        // esse guarda, um lote de 20 programas viraria 1 a cada clique.
        Assert.Contains("if (!ProgramsList.SelectedItems.Cast<ProgramRow>().Contains(row))", metodo);

        // O menu de contexto age sobre a linha clicada, e nao sobre a
        // primeira linha selecionada.
        Assert.Contains("_contextRow = row;", metodo);
    }

    [Fact]
    public void AListaPrincipalAceitaSelecaoMultiplaParaDesinstalacaoEmLote()
    {
        var xaml = Read("src", "Tranal.Uninstaller", "MainWindow.xaml");
        var code = Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        // Sem selecao multipla, o caminho de desinstalacao em lote nunca chega
        // a N > 1: o codigo perguntaria "Deseja desinstalar 1 programa?".
        Assert.Contains("SelectionMode=\"Extended\"", xaml);
        Assert.DoesNotContain("SelectionMode=\"Single\"", xaml);

        // O alvo da desinstalacao e a selecao inteira.
        Assert.Contains("ProgramsList.SelectedItems.Cast<ProgramRow>().ToList()", code);

        // E a confirmacao do lote nunca e omitida pelas preferencias de
        // mensagem: e a pergunta que antecede a remocao.
        Assert.Contains("UserMessages.Confirm(this, message)", code);
        Assert.Contains("Cmd.UninstallManyConfirm", code);
    }

    [Fact]
    public void AGerenciadorDeGruposTemTituloTraduzido()
    {
        var code = Read("src", "Tranal.Uninstaller", "Ui", "GroupsManagerWindow.xaml.cs");

        // Window.Title nao aceita binding: o titulo vem do catalogo, para
        // acompanhar o idioma da interface. Um titulo fixo em pt-BR deixaria
        // a barra de titulo em portugues dentro de uma interface em ingles.
        Assert.Contains("Title = Loc.Get(\"Groups.Title\");", code);
    }

    [Fact]
    public void ALarguraAjustadaSobreviveATrocaDeModo()
    {
        var code = Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        // O "Best Fit" e um ajuste do usuario: voltar para o modo "Detalhes"
        // nao pode reescrever a largura e apagar o que ele acabou de fazer.
        var bestFit = RepoFiles.Metodo(code, "BestFit");

        // A declaracao tem de ESTAR no codigo: um "contem" solto aceitaria
        // tambem a linha comentada, e o teste passaria com o defeito
        // de volta. O "\r?" cobre o CRLF do checkout.
        Assert.Matches(@"(?m)^[ \t]*_columnWidths\[id\] = largura;[ \t]*\r?$", bestFit);

        var modo = RepoFiles.Metodo(code, "ApplyViewMode");
        Assert.Matches(@"(?m)^[ \t]*_ => _columnWidths\.TryGetValue\(id, out var largura\)", modo);
    }

    [Fact]
    public void OItemDeGruposVazioTemInstanciaPropriaEmCadaSubmenu()
    {
        var code = Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        // Um item de menu nao pode ter dois pais logicos: a excecao apareceria
        // no meio da montagem do menu, antes dos submenus virarem parte dele
        // — e o usuario nao veria nenhum aviso.
        var metodo = RepoFiles.Metodo(code, "ProgramContextMenu_Opened");

        // A asercao olha a expressao REAL das duas adicoes. A versao
        // anterior casava com "Items.Add(identificador)" e nao via nada: as
        // duas adicoes sao "Items.Add(NoGroupsItem())", com a chamada dentro
        // — o teste passava por acidente, sem proteger a correcao.
        Assert.Equal(2, Regex.Matches(metodo, @"Items\.Add\(NoGroupsItem\(\)\)").Count);

        // E nenhum item pode ser adicionado aos DOIS submenus: seria a mesma
        // instancia com dois pais logicos.
        var porSubmenu = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(metodo, @"(\w+)\.Items\.Add\((\w+)\)"))
        {
            var submenu = m.Groups[1].Value;
            var item = m.Groups[2].Value;

            if (!porSubmenu.TryGetValue(submenu, out var items))
            {
                items = new HashSet<string>(StringComparer.Ordinal);
                porSubmenu[submenu] = items;
            }

            items.Add(item);
        }

        Assert.True(porSubmenu.ContainsKey("addGroup"));
        Assert.True(porSubmenu.ContainsKey("removeGroup"));
        Assert.Empty(porSubmenu["addGroup"].Intersect(porSubmenu["removeGroup"]));
    }

    [Fact]
    public void OModoDeVisualizacaoSobreviveAReconstrucaoDaGrade()
    {
        var code = Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        // A grade nasce do catalogo de colunas; o modo de visualizacao e
        // reaplicado depois — se nao fosse, trocar de idioma ou de colunas
        // devolveria a lista ao layout "Detalhes".
        var construtor = RepoFiles.Metodo(code, "BuildProgramColumns");

        Assert.Contains("ApplyViewMode(_viewMode)", construtor);

        // E o modo nao esconde colunas de verdade: so ajusta a largura.
        var modo = RepoFiles.Metodo(code, "ApplyViewMode");

        Assert.Contains("ProgramColumnLayout.WidthOf(id)", modo);
    }

    [Fact]
    public void OIconeEscolhidoQueNaoCarregaCaiNoIconeDoPrograma()
    {
        var code = Read("src", "Tranal.Uninstaller", "Ui", "ProgramRow.cs");
        var janela = Read("src", "Tranal.Uninstaller", "Ui", "IconPickerWindow.xaml.cs");

        // Um caminho que nao carrega como imagem nao pode substituir o icone
        // do programa: o programa ficaria sem icone nenhum.
        Assert.Contains("?? IconPickerWindow.LoadIcon(program.IconPath)", code);

        // O carregamento do icone mora em um lugar so: a linha e a janela
        // usam o mesmo codigo, e um ajuste em um so nao diverge do outro.
        Assert.DoesNotContain("private static ImageSource? LoadIcon", code);

        // E a janela recusa o arquivo antes de grava-lo.
        Assert.Contains("LoadIcon(path) is null", janela);
        Assert.Contains("Icon.NotAnImage", janela);
    }

    [Fact]
    public void OMenuDaColunaRegistraOHandlerUmaVezSo()
    {
        var code = Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        // Registrar o handler a cada coluna faria o menu se reconstruir
        // varias vezes a cada abertura.
        var metodo = RepoFiles.Metodo(code, "BuildColumnMenu");

        Assert.Contains("if (_columnMenu is null)", metodo);
        Assert.Single(Regex.Matches(metodo, @"_columnMenu\.Opened \+="));
    }

    [Fact]
    public void APropriedadeDeColunaEscondidaNaoVolta()
    {
        var code = Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        // A regra antiga ("coluna escondida = largura zero") nao pode voltar:
        // ela contradiz a regra das preferencias (a coluna escondida e a que
        // nao esta na lista) e nao era usada por ninguem.
        Assert.DoesNotContain("PersistHiddenColumns", code);
    }

    [Fact]
    public void OBotaoProcurarDoSeletorDeIconeEstaLocalizado()
    {
        var xaml = Read("src", "Tranal.Uninstaller", "Ui", "IconPickerWindow.xaml");

        Assert.Contains("Content=\"{loc:Tr Icon.Browse}\"", xaml);
        Assert.DoesNotContain("Content=\"...\"", xaml);
    }

    [Fact]
    public void OGerenciadorDeGruposExisteEGravaEmDisco()
    {
        var xaml = Read("src", "Tranal.Uninstaller", "Ui", "GroupsManagerWindow.xaml");
        var code = Read("src", "Tranal.Uninstaller", "Ui", "GroupsManagerWindow.xaml.cs");

        // 15070: "Group name:", 15071: "Add", 15001: "Groups Manager"
        Assert.Contains("Groups.GroupName", xaml);
        Assert.Contains("Groups.Add", xaml);
        Assert.Contains("Groups.Title", xaml);

        // O gerenciador usa a store do nucleo: quem cria o grupo e quem
        // guarda o programa nele sao a mesma regra.
        Assert.Contains("ProgramCustomizationStore.CreateGroup", code);
        Assert.Contains("ProgramCustomizationStore.RenameGroup", code);
        Assert.Contains("ProgramCustomizationStore.DeleteGroup", code);
    }

    [Fact]
    public void RenomearGrupoNoGerenciadorTambemMoveOsProgramas()
    {
        var code = Read("src", "Tranal.Uninstaller", "Ui", "GroupsManagerWindow.xaml.cs");
        var store = Read("src", "Tranal.Uninstaller", "Core", "ProgramCustomization.cs");

        // O gerenciador nao tem um caminho proprio de renomear: ele chama a
        // store, e quem move os programas junto e a store.
        Assert.Contains("ProgramCustomizationStore.RenameGroup", code);
        Assert.Contains("item.Groups[i] = to;", store);
    }

    [Fact]
    public void AGaleriaDeIconesUsaOQueJaTemNaMaquina()
    {
        var xaml = Read("src", "Tranal.Uninstaller", "Ui", "IconPickerWindow.xaml");
        var code = Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        // 15284: "Select an icon from the list:" e 15283: "File path:"
        Assert.Contains("Icon.FilePath", xaml);
        Assert.Contains("Icon.Prompt", Read("src", "Tranal.Uninstaller", "Ui", "IconPickerWindow.xaml.cs"));

        // A galeria reune os icones dos programas instalados — nao uma galeria
        // de terceiros.
        Assert.Contains("BuildIconChoices", code);
        Assert.Contains("row.Program.IconPath", code);

        // O caminho de arquivo tambem e aceito.
        Assert.Contains("Icon.BrowseTitle", Read("src", "Tranal.Uninstaller", "Ui", "IconPickerWindow.xaml.cs"));
    }

    [Fact]
    public void OMenuDoCabecalhoRemoveAColunaEAjustaALargura()
    {
        var code = Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        // 15055: "Remove This Column"; 15056 a 15060: alignment e "Best Fit".
        Assert.Contains("Cmd.RemoveColumn", code);
        Assert.Contains("Cmd.Alignment", code);
        Assert.Contains("Cmd.BestFit", code);
        Assert.Contains("HideColumn", code);

        // A coluna do nome nunca some: sem ela a lista perde o sentido.
        Assert.Contains("remove.IsEnabled = id != ProgramColumn.Name;", code);
    }

    [Fact]
    public void OMenuDoCabecalhoUsaOCatalogoDeColunas()
    {
        var code = Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        // O rotulo da coluna vem do catalogo de colunas, nao de um segundo
        // mapa de rotulos que divergiria do primeiro.
        Assert.Contains("ProgramColumns.Label(", code);
        Assert.DoesNotContain("TitleForProperty", code);
    }

    [Fact]
    public void OColunasEscolhidasVaoparaAsPreferencias()
    {
        var code = Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        // 15364: "Do you want to keep your preferences? Icons, comments,
        // groups, etc." — no Tranal a resposta e sempre sim.
        //
        // A assercao e ancorada no metodo que grava: a preferencia e escrita
        // em um lugar so.
        var metodo = RepoFiles.Metodo(code, "SetVisibleColumns");

        Assert.Contains("settings.HiddenColumns", metodo);
        Assert.Contains("AppSettingsStore.Save(settings)", metodo);

        // A janela de colunas e o "Remove This Column" passam pelo mesmo
        // lugar: uma regra, um escritor.
        Assert.Contains("SetVisibleColumns", code);
    }

    [Fact]
    public void OFiltroHiddenApontaParaAPreferenciaDeComponentesDoSistema()
    {
        var xaml = Read("src", "Tranal.Uninstaller", "MainWindow.xaml");
        var code = Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        // 15362: "Hidden"
        Assert.Contains("FilterHidden", xaml);
        Assert.Contains("Sidebar.Hidden", xaml);

        // E a mesma preferencia que a janela de Opcoes usa: nao sao duas
        // regras para a mesma coisa.
        Assert.Contains("settings.ShowSystemComponents = mostrar;", code);
        Assert.Contains("AppSettingsStore.Current.ShowSystemComponents", code);
    }

    [Fact]
    public void AsJanelasNovasNaoUsamCorFixa()
    {
        // Brush de fundo nao pode pintar texto, e cor literal no XAML quebra
        // no tema claro.
        foreach (var arquivo in new[]
        {
            "ColumnsWindow.xaml",
            "CommentWindow.xaml",
            "IconPickerWindow.xaml",
            "GroupsManagerWindow.xaml",
        })
        {
            var xaml = Read("src", "Tranal.Uninstaller", "Ui", arquivo);

            var coresFixas = Regex.Matches(xaml, @"(?<![A-Za-z])#[0-9A-Fa-f]{6,8}\b");
            Assert.Empty(coresFixas.Select(m => m.Value).Distinct().ToList());
        }
    }

    [Fact]
    public void OsDoisCatalogosTrazemAsChavesNovas()
    {
        var pt = Read("src", "Tranal.Uninstaller", "Lang", "pt-BR.json");
        var en = Read("src", "Tranal.Uninstaller", "Lang", "en-US.json");

        var keys = new[]
        {
            "Common.Group",
            "Common.Note",
            "Cmd.Columns",
            "Cmd.GroupsManager",
            "Cmd.AddComment",
            "Cmd.EditComment",
            "Cmd.ChangeIcon",
            "Cmd.AddToGroup",
            "Cmd.RemoveFromGroup",
            "Cmd.NoGroups",
            "Cmd.RemoveColumn",
            "Cmd.Alignment",
            "Cmd.AlignLeft",
            "Cmd.AlignCenter",
            "Cmd.AlignRight",
            "Cmd.BestFit",
            "Cmd.ExportTxt",
            "Cmd.ExportCsv",
            "Cmd.ExportHtml",
            "Cmd.ExportXml",
            "Columns.Title",
            "Columns.Prompt",
            "Columns.ExportTitle",
            "Columns.ExportPrompt",
            "Columns.Export",
            "Columns.AtLeastOne",
            "Comment.AddTitle",
            "Comment.EditTitle",
            "Comment.Prompt",
            "Icon.Title",
            "Icon.Prompt",
            "Icon.FilePath",
            "Icon.BrowseTitle",
            "Icon.BrowseFilter",
            "Icon.Browse",
            "Icon.NotAnImage",
            "ProgramData.SaveError",
            "Groups.Title",
            "Groups.Header",
            "Groups.Empty",
            "Groups.GroupName",
            "Groups.Add",
            "Groups.Rename",
            "Groups.Delete",
            "Groups.NameRequired",
            "Groups.AlreadyExists",
            "Groups.Created",
            "Groups.Renamed",
            "Groups.Deleted",
            "Groups.SelectFirst",
            "Groups.RenameHint",
            "Sidebar.FilterByHidden",
            "Sidebar.Hidden",
        };

        foreach (var key in keys)
        {
            Assert.Contains($"\"{key}\"", pt);
            Assert.Contains($"\"{key}\"", en);
        }
    }

    [Fact]
    public void AColunaDoNomeEPrimeiraNaListaENuncaSomeDela()
    {
        var colunas = Read("src", "Tranal.Uninstaller", "Core", "ProgramColumns.cs");
        var layout = Read("src", "Tranal.Uninstaller", "Ui", "ProgramColumnLayout.cs");

        // O nome e a primeira coluna do catalogo: e por ele que a lista e a
        // exportacao comecam.
        Assert.Contains("new(ProgramColumn.Name,", colunas);
        Assert.Contains("ProgramColumn.Name => 250,", layout);

        // Na lista ele nunca some: sem ele a lista perde o sentido.
        Assert.Contains("if (info.Id == ProgramColumn.Name) { result.Add(info.Id); continue; }", colunas);
    }

    [Fact]
    public void AExportacaoTemUmConjuntoPadraoFixo()
    {
        var exportadores = Read("src", "Tranal.Uninstaller", "Core", "Exporters.cs");

        // O conjunto padrao e escrito a mao e fixo: o formato do arquivo
        // exportado nao pode mudar quando o usuario mexe nas colunas da tela.
        Assert.Contains("public static IReadOnlyList<ProgramColumn> DefaultColumns { get; }", exportadores);

        // E ele nao e o catalogo inteiro.
        Assert.DoesNotContain("DefaultColumns =>", exportadores);
    }

    [Fact]
    public void AListaEAAExportacaoConsomemOMesmoCatalogo()
    {
        var colunas = Read("src", "Tranal.Uninstaller", "Core", "ProgramColumns.cs");
        var exportadores = Read("src", "Tranal.Uninstaller", "Core", "Exporters.cs");
        var layout = Read("src", "Tranal.Uninstaller", "Ui", "ProgramColumnLayout.cs");

        // Uma coluna, uma interpretacao: o valor de uma coluna vem sempre de
        // "ProgramColumns.Value".
        Assert.Contains("ProgramColumns.Value(", exportadores);
        Assert.Contains("public static double WidthOf(ProgramColumn column)", layout);
        Assert.Contains("ProgramColumns.Label(column)", layout);
    }
}
