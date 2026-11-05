using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// Paridade da interface do Lote 1: a lista de exclusoes, o encerramento dos
/// executaveis, a varredura em todas as contas e o backup completo do
/// registro.
///
/// A verificacao e feita sobre o fonte da interface, que e a unica forma de
/// conferir a cobertura sem acoplar a suite ao WPF (referenciar o projeto
/// grafico criaria dois tipos "Core" diferentes).
/// </summary>
public class Lote1UiParityTests
{
    /// <summary>Le um arquivo do projeto grafico.</summary>
    private static string Read(params string[] parts) => RepoFiles.Read(parts);

    [Fact]
    public void AsOpcoesDeContaSoAparecemComOInterruptorMestreLigado()
    {
        var code = Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml.cs");

        // "Show the option to scan all Windows accounts for leftovers" (15345)
        // e um interruptor mestre: sem ele, as duas opcoes de conta ficam
        // ocultas — como no original, onde a opcao existe mas fica escondida
        // ate o usuario habilita-la nas Opcoes.
        Assert.Contains("var showAccounts = settings.ShowScanAllAccountsOption;", code);
        Assert.Contains(
            "ScanAllAccountsCheck.Visibility = showAccounts ? Visibility.Visible : Visibility.Collapsed;",
            code);
        Assert.Contains(
            "UninstallAllAccountsCheck.Visibility = showAccounts ? Visibility.Visible : Visibility.Collapsed;",
            code);
    }

    [Fact]
    public void OBackupCompletoDoRegistroVemAntesDoDesinstaladorNativo()
    {
        // "Make a System Restore Point and a full Registry backup BEFORE
        // uninstalling" (15162): depois do desinstalador nativo, as chaves
        // do programa ja foram apagadas e o .reg nao serviria para nada.
        var wizard = Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml.cs");
        var cli = Read("src", "Tranal.Cli", "Program.cs");

        var backupWizard = wizard.IndexOf("UninstallEngine.PrepareFullRegistryBackup", StringComparison.Ordinal);
        var nativoWizard = wizard.IndexOf("UninstallEngine.RunBuiltInUninstaller", StringComparison.Ordinal);

        Assert.True(backupWizard > 0, "o assistente nao prepara o backup completo do registro");
        Assert.True(nativoWizard > 0, "o assistente nao roda o desinstalador nativo");
        Assert.True(backupWizard < nativoWizard,
            "no assistente, o backup completo do registro precisa vir antes do desinstalador nativo");

        var backupCli = cli.IndexOf("UninstallEngine.PrepareFullRegistryBackup", StringComparison.Ordinal);
        var nativoCli = cli.IndexOf("UninstallEngine.RunBuiltInUninstaller", StringComparison.Ordinal);

        Assert.True(backupCli > 0, "a CLI nao prepara o backup completo do registro");
        Assert.True(nativoCli > 0, "a CLI nao roda o desinstalador nativo");
        Assert.True(backupCli < nativoCli,
            "na CLI, o backup completo do registro precisa vir antes do desinstalador nativo");
    }

    [Fact]
    public void AJanelaDoGerenciadorDeExclusoesExiste()
    {
        var xaml = Read("src", "Tranal.Uninstaller", "Ui", "ExcludeListWindow.xaml");
        var code = Read("src", "Tranal.Uninstaller", "Ui", "ExcludeListWindow.xaml.cs");

        // As duas listas do original: "Files and Folders to Exclude" (15316) e
        // "Registry Keys to Exclude" (15317).
        Assert.Contains("ExcludePathsList", xaml);
        Assert.Contains("ExcludeRegistryList", xaml);
        Assert.Contains("ExcludeAddFolder", xaml);
        Assert.Contains("ExcludeAddPath", xaml);
        Assert.Contains("ExcludeAddRegistryKey", xaml);
        Assert.Contains("ExcludeRemove", xaml);
        Assert.Contains("ExcludeClear", xaml);

        // A janela usa a lista de exclusoes do nucleo e grava no disco.
        Assert.Contains("ExcludeList.Current", code);
        Assert.Contains("ExcludeList.Current.Save()", code);
        Assert.Contains("ExcludeList.Reload()", code);

        // A limpeza da lista pede confirmacao: limpar a lista retira uma
        // protecao, e a confirmacao de uma acao destrutiva nunca e omitida.
        Assert.Contains("MessageBox.Show", code);
        Assert.Contains("ExcludeList.ClearConfirm", code);
    }

    [Fact]
    public void AsOpcoesNovasEstaoNaJanelaDeOpcoes()
    {
        var xaml = Read("src", "Tranal.Uninstaller", "Ui", "OptionsWindow.xaml");
        var code = Read("src", "Tranal.Uninstaller", "Ui", "OptionsWindow.xaml.cs");

        // "Forcibly stop running executable files during their uninstall" (15265)
        Assert.Contains("OptForciblyStopExecutables", xaml);
        Assert.Contains("Options.ForciblyStopRunningExecutables", xaml);

        // "Show the option to scan all Windows accounts for leftovers" (15345)
        Assert.Contains("OptShowScanAllAccounts", xaml);
        Assert.Contains("Options.ShowScanAllAccountsOption", xaml);

        // As duas preferencias existem no nucleo e sao gravadas pela janela.
        Assert.Contains("s.ForciblyStopRunningExecutables =", code);
        Assert.Contains("s.ShowScanAllAccountsOption =", code);
    }

    [Fact]
    public void OAssistenteTemAsOpcoesDoOriginal()
    {
        var xaml = Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml");
        var code = Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml.cs");

        // "Create a full Registry Backup before uninstall" (15307)
        Assert.Contains("FullRegistryBackupCheck", xaml);
        Assert.Contains("Wizard.FullRegistryBackupCheck", xaml);

        // "Scan all Windows accounts for leftovers" (15352)
        Assert.Contains("ScanAllAccountsCheck", xaml);
        Assert.Contains("Wizard.ScanAllAccountsCheck", xaml);

        // "Uninstall from all Windows accounts" (15353)
        Assert.Contains("UninstallAllAccountsCheck", xaml);
        Assert.Contains("Wizard.UninstallAllAccountsCheck", xaml);

        // As opcoes de conta so aparecem com o interruptor mestre ligado.
        Assert.Contains("ShowScanAllAccountsOption", code);

        // A invariante entre as duas opcoes e garantida no codigo.
        Assert.Contains("SyncAccountChecks", code);

        // O menu de contexto da lista de sobras tem o "Add to Exclude List"
        // (15102).
        Assert.Contains("ContextMenu.AddToExcludeList", code);
        Assert.Contains("CtxAddToExcludeList", code);
    }

    [Fact]
    public void OMenuPrincipalAbreOGerenciadorDeExclusoes()
    {
        var code = Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        Assert.Contains("Cmd.ExcludeList", code);
        Assert.Contains("CmdExcludeList", code);
        Assert.Contains("ShowExcludeList", code);
        Assert.Contains("new ExcludeListWindow", code);
    }

    [Fact]
    public void OGerenciadorDeExclusoesEUmItemDoMenuPrincipal()
    {
        // A entrada precisa estar no menu principal (e nao apenas existir):
        // e por ela que o usuario chega a lista de exclusoes sem passar pela
        // tela de desinstalacao.
        var code = Read("src", "Tranal.Uninstaller", "MainWindow.xaml.cs");

        var menu = code.Substring(
            code.IndexOf("MainMenuButton_Click", StringComparison.Ordinal),
            2500);

        Assert.Contains("CmdExcludeList", menu);
    }

    [Fact]
    public void OsDoisCatalogosTrazemAsChavesNovas()
    {
        var pt = Read("src", "Tranal.Uninstaller", "Lang", "pt-BR.json");
        var en = Read("src", "Tranal.Uninstaller", "Lang", "en-US.json");

        var keys = new[]
        {
            "Options.ForciblyStopRunningExecutables",
            "Options.ShowScanAllAccountsOption",
            "Wizard.FullRegistryBackupCheck",
            "Wizard.ScanAllAccountsCheck",
            "Wizard.UninstallAllAccountsCheck",
            "ExcludeList.Title",
            "ExcludeList.AddFolder",
            "ExcludeList.AddRegistryKey",
            "ExcludeList.ClearConfirm",
            "ContextMenu.AddToExcludeList",
            "Cmd.ExcludeList",
        };

        foreach (var key in keys)
        {
            Assert.Contains($"\"{key}\"", pt);
            Assert.Contains($"\"{key}\"", en);
        }
    }

    [Fact]
    public void AsJanelasNovasNaoUsamCorFixa()
    {
        // Brush de fundo nao pode pintar texto, e cor literal no XAML quebra
        // no tema claro. A janela nova passa no teste automatico de tema.
        var xaml = Read("src", "Tranal.Uninstaller", "Ui", "ExcludeListWindow.xaml");

        var coresFixas = Regex.Matches(xaml, @"(?<![A-Za-z])#[0-9A-Fa-f]{6,8}\b");
        Assert.Empty(coresFixas.Select(m => m.Value).Distinct().ToList());
    }

    [Fact]
    public void ADesinstalacaoForcadaUsaAsMesmasOpcoesDaInterface()
    {
        var code = Read("src", "Tranal.Uninstaller", "Ui", "ForcedUninstallWindow.xaml.cs");

        // A forcada nao pode ter um caminho proprio de remocao: ela consome
        // as mesmas opcoes do assistente e da CLI (backup do registro,
        // encerramento dos executaveis, Lixeira, adiamento).
        Assert.Contains("UninstallRemovalOptions.FromSettings", code);
        Assert.Contains("UninstallEngine.PrepareFullRegistryBackup", code);

        // E o resumo mostra o que saiu de dentro das pastas que ficaram no
        // sistema — sem isso, o resumo diria "0 removidos" com arquivos
        // de fato apagados.
        Assert.Contains("report.RemovedInsideCount", code);
        Assert.Contains("Forced.RemovedInside", code);
    }

    [Fact]
    public void OMotorNuncaAtravessaAtalho()
    {
        var code = Read("src", "Tranal.Uninstaller", "Core", "UninstallSupport.cs");

        // A regra e uma so: o motor consulta o helper antes de descer em
        // uma subpasta e antes de apagar a arvore.
        Assert.Contains("SafeTraversal.IsReparsePoint", code);
        Assert.Contains("SafeTraversal.DeleteDirectoryTree", code);

        // E a remocao recursiva "cega" nao existe mais no motor: ela
        // atravessava o atalho e apagava o alvo.
        Assert.DoesNotContain("Directory.Delete(full, true)", code);
    }
}
