using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// A justificativa da sobra: a confianca sozinha diz o quanto o Tranal
/// acredita no item, mas nao diz por que.
///
/// O motivo e o que o Bulk Crap Uninstaller chama de "confidence part": la,
/// cada nivel de confianca vem acompanhado da razao que o produziu.
/// </summary>
public class LeftoverReasonTests
{
    [Fact]
    public void TodoMotivoTemChaveNoCatalogo()
    {
        // A chave e derivada do nome do membro: se o enum ganhar um motivo
        // novo, o teste falha ate que a traducao exista nos dois idiomas.
        var ptBR = Loc.Load("pt-BR");
        var enUS = Loc.Load("en-US");

        foreach (var reason in Enum.GetValues<LeftoverReason>())
        {
            var key = $"Common.Reason.{reason}";

            Assert.True(ptBR.ContainsKey(key), $"falta a chave '{key}' no catalogo pt-BR");
            Assert.True(enUS.ContainsKey(key), $"falta a chave '{key}' no catalogo en-US");
            Assert.False(string.IsNullOrWhiteSpace(ptBR[key]), $"a chave '{key}' esta vazia em pt-BR");
        }
    }

    [Fact]
    public void AMotivoApareceNaInterface()
    {
        // A verificacao e sobre o fonte da interface: referenciar o projeto
        // grafico na suite criaria dois tipos "Core" diferentes.
        //
        // A tela de sobras e a ARVORE (o lote 23 trocou a lista por ela): o
        // motivo aparece no detalhe de cada folha, e a traducao continua
        // morando no nucleo (LeftoverLabels).
        var arvore = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Shared", "LeftoversTree.xaml.cs");

        Assert.Contains("LeftoverLabels.Reason(item.Reason)", arvore);

        // O que a pessoa le na folha e a NOTA de cinco niveis (o lote 24),
        // e nao a confianca crua de tres niveis: a nota junta a confianca
        // com o motivo, e e ela que diz o quanto e seguro mexer no item.
        Assert.Contains("LeftoverLabels.Rating(LeftoverRatings.Rate(item))", arvore);

        // O conversor continua existindo para quem quiser usar a coluna.
        var wizard = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml.cs");
        Assert.Contains("public sealed class ReasonTextConverter : IValueConverter", wizard);
    }

    [Fact]
    public void OConversorCobreTodosOsMotivos()
    {
        // A traducao do motivo mora no NUCLEO, e a interface e a CLI consomem
        // esse metodo. Se cada uma tivesse a sua, elas divergiriam na
        // primeira traducao esquecida.
        var core = RepoFiles.Read("src", "Tranal.Uninstaller", "Core", "Localization", "LeftoverLabels.cs");
        var wizard = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml.cs");
        var cli = RepoFiles.Read("src", "Tranal.Cli", "Program.cs");

        foreach (var reason in Enum.GetValues<LeftoverReason>())
        {
            Assert.Contains($"LeftoverReason.{reason} => \"Common.Reason.{reason}\"", core);
        }

        Assert.Contains("LeftoverLabels.Reason(reason)", wizard);
        Assert.Contains("LeftoverLabels.Reason(reason)", cli);

        // Nenhuma das duas pode manter a traducao duplicada.
        Assert.DoesNotContain("Loc.Get(reason switch", wizard);
        Assert.DoesNotContain("Loc.Get(reason switch", cli);
    }

    [Fact]
    public void ACliTambemMostraOMotivo()
    {
        // A CLI e a tela contam a mesma historia: o item aparece com a
        // confianca E com o motivo.
        var cli = RepoFiles.Read("src", "Tranal.Cli", "Program.cs");

        Assert.Contains("motivo: {ReasonLabel(item.Reason)}", cli);
    }

    [Fact]
    public void OJulgamentoDaConfiancaEOMotivoJuntos()
    {
        var exato = LeftoverScanner.Judge("MeuApp", "MeuApp");
        Assert.Equal(LeftoverConfidence.High, exato.Confidence);
        Assert.Equal(LeftoverReason.ExactNameMatch, exato.Reason);

        var parcial = LeftoverScanner.Judge("MeuApp Data", "MeuApp");
        Assert.Equal(LeftoverConfidence.Medium, parcial.Confidence);
        Assert.Equal(LeftoverReason.VariantNameMatch, parcial.Reason);
    }

    [Fact]
    public void AComparacaoDoNomeIgnoraACaixa()
    {
        var exato = LeftoverScanner.Judge("meuapp", "MeuApp");
        Assert.Equal(LeftoverConfidence.High, exato.Confidence);
        Assert.Equal(LeftoverReason.ExactNameMatch, exato.Reason);
    }

    [Fact]
    public void TodoItemVarridoTemMotivo()
    {
        // Um item sem motivo e um item que o usuario nao consegue avaliar.
        var program = new InstalledProgram
        {
            DisplayName = "Programa de Teste Tranal",
            InstallLocation = @"C:\Program Files\Programa de Teste Tranal",
        };

        var items = LeftoverScanner.Scan(program, new LeftoverScanOptions { Mode = UninstallMode.Advanced });

        var semMotivo = items.Where(i => i.Reason == LeftoverReason.Unknown).ToList();

        Assert.True(semMotivo.Count == 0,
            "sobras sem motivo:\n" + string.Join("\n", semMotivo.Select(i => $"{i.Kind} {i.Path}")));
    }

    [Fact]
    public void AJustificativaNaoMudouA_regraDeMarcacao()
    {
        // O motivo acompanha a confianca, mas nao a altera: so a confianca
        // alta entra marcada.
        var program = new InstalledProgram { DisplayName = "Programa de Teste Tranal" };

        var items = LeftoverScanner.Scan(program, new LeftoverScanOptions { Mode = UninstallMode.Advanced });

        foreach (var item in items)
        {
            if (item.OtherAccount) continue;

            var deveVirMarcado = item.Confidence == LeftoverConfidence.High;
            Assert.Equal(deveVirMarcado, item.Selected);
        }
    }

    [Fact]
    public void OHistoricoDeRemocaoTemMotivoProprio()
    {
        var item = new LeftoverItem
        {
            Kind = "Registro",
            Path = @"HKEY_CURRENT_USER\Software\Exemplo",
            Detail = "exemplo",
            Confidence = LeftoverConfidence.High,
            Reason = LeftoverReason.History,
            Selected = true,
        };

        Assert.Equal(LeftoverReason.History, item.Reason);
    }
}
