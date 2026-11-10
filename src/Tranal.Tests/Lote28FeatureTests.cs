using System;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O lote 28 mostra na lista o que o Tranal **ja sabia** sobre o programa.
///
/// Tudo o que a lista precisa dizer antes de o usuario mexer ja era medido
/// pelo proprio Windows: a tag <c>NoRemove</c> do registro, o comando de
/// desinstalacao apontando para um arquivo que nao existe mais, o valor
/// <c>IsUpgrade</c> e a maquina do executavel. O que faltava era a camada
/// que mostra.
///
/// A REGRA mora no nucleo (<c>Core/ProgramMarks.cs</c>) e a suite testa esse
/// modulo. A ligacao com a interface e conferida sobre o fonte, pelo mesmo
/// motivo das outras paridades: a suite nao referencia o projeto grafico
/// (WPF).
/// </summary>
public class Lote28FeatureTests
{
    /// <summary>
    /// Um programa comum, registrado e sem nenhuma marca.
    /// </summary>
    private static InstalledProgram Programa(string nome = "Programa") => new()
    {
        DisplayName = nome,
        RegistryKey = "Programa",
        RegistryPath = @"HKEY_LOCAL_MACHINE\Software\Programa",
        Kind = UninstallerKind.Nsis,
    };

    // ==================================================================
    // As marcas
    // ==================================================================

    [Fact]
    public void Um_programa_comum_nao_tem_marca()
    {
        var marcas = ProgramMarks.Detect(Programa());

        Assert.False(marcas.Any);
        Assert.Empty(marcas.Marks);
    }

    [Fact]
    public void O_programa_de_32_bits_e_marcado()
    {
        // A marca so existe quando o Windows e de 64 bits: em um Windows de
        // 32 bits todo programa e de 32 bits e a marca nao diria nada.
        if (!Environment.Is64BitOperatingSystem) return;

        var programa = Programa();
        programa.Bits = "32-bits";

        var marcas = ProgramMarks.Detect(programa);

        Assert.True(marcas.Is32Bits);
        Assert.Contains(ProgramMark.ThirtyTwoBits, marcas.Marks);
    }

    [Fact]
    public void O_programa_de_64_bits_nao_e_marcado()
    {
        var programa = Programa();
        programa.Bits = "64-bits";

        var marcas = ProgramMarks.Detect(programa);

        Assert.False(marcas.Is32Bits);
        Assert.DoesNotContain(ProgramMark.ThirtyTwoBits, marcas.Marks);
    }

    [Fact]
    public void A_atualizacao_do_registro_e_marcada()
    {
        var programa = Programa();
        programa.IsUpgrade = true;

        var marcas = ProgramMarks.Detect(programa);

        Assert.True(marcas.IsUpdate);
        Assert.Contains(ProgramMark.Update, marcas.Marks);
        Assert.NotEqual(string.Empty, marcas.UpdateDetail);
    }

    [Fact]
    public void O_nome_que_diz_atualizacao_e_marcado()
    {
        var marcas = ProgramMarks.Detect(Programa("Atualização do Chrome"));

        Assert.True(marcas.IsUpdate);
        Assert.Contains(ProgramMark.Update, marcas.Marks);
    }

    [Fact]
    public void O_programa_sem_registro_e_orfao()
    {
        var programa = new InstalledProgram { DisplayName = "Achado no disco" };

        var marcas = ProgramMarks.Detect(programa);

        Assert.True(marcas.IsOrphan);
        Assert.Contains(ProgramMark.Orphan, marcas.Marks);
    }

    [Fact]
    public void O_programa_registrado_nao_e_orfao()
    {
        var marcas = ProgramMarks.Detect(Programa());

        Assert.False(marcas.IsOrphan);
        Assert.DoesNotContain(ProgramMark.Orphan, marcas.Marks);
    }

    [Fact]
    public void O_programa_do_instalador_do_windows_nao_e_orfao()
    {
        // Um produto MSI tem o proprio catalogo: mesmo sem chave de
        // desinstalacao, ele esta registrado.
        var programa = new InstalledProgram
        {
            DisplayName = "Produto MSI",
            IsWindowsInstaller = true,
            ProductCode = "{00000000-0000-0000-0000-000000000000}",
        };

        Assert.False(ProgramMarks.IsOrphan(programa));
    }

    [Fact]
    public void O_desinstalador_quebrado_e_marcado()
    {
        var programa = Programa();
        programa.UninstallerHealth = UninstallerHealth.Broken;

        var marcas = ProgramMarks.Detect(programa);

        Assert.True(marcas.IsInvalid);
        Assert.Contains(ProgramMark.Invalid, marcas.Marks);
    }

    [Fact]
    public void O_desinstalador_saudavel_nao_e_marcado()
    {
        var programa = Programa();
        programa.UninstallerHealth = UninstallerHealth.Ok;

        Assert.False(ProgramMarks.Detect(programa).IsInvalid);
    }

    [Fact]
    public void O_programa_que_pede_para_nao_ser_removido_e_marcado()
    {
        var programa = Programa();
        programa.IsProtected = true;

        var marcas = ProgramMarks.Detect(programa);

        Assert.True(marcas.IsProtected);
        Assert.Contains(ProgramMark.Protected, marcas.Marks);
    }

    // ==================================================================
    // A coluna "Marcas"
    // ==================================================================

    [Fact]
    public void A_coluna_marcas_mostra_as_marcas()
    {
        var programa = Programa();
        programa.IsProtected = true;
        programa.UninstallerHealth = UninstallerHealth.Broken;

        var texto = ProgramColumns.Value(ProgramColumn.Marks, programa, null);

        Assert.Contains(ProgramMarks.Label(ProgramMark.Invalid), texto);
        Assert.Contains(ProgramMarks.Label(ProgramMark.Protected), texto);
    }

    [Fact]
    public void A_coluna_marcas_e_vazia_sem_marca()
    {
        Assert.Equal(string.Empty, ProgramColumns.Value(ProgramColumn.Marks, Programa(), null));
    }

    [Fact]
    public void A_coluna_marcas_esta_no_catalogo_de_colunas()
    {
        var info = ProgramColumns.Info(ProgramColumn.Marks);

        Assert.Equal("Marks", info.ExportName);
        Assert.Contains(ProgramColumn.Marks, ProgramColumns.All.Select(c => c.Id));
    }

    // ==================================================================
    // O protegido e a origem
    // ==================================================================

    [Fact]
    public void A_coluna_protegido_considera_a_tag_do_programa()
    {
        // A coluna "Protegido" mostrava so a lista de exclusao do Tranal. A
        // tag "NoRemove" do proprio programa e outra coisa — e as duas
        // contam.
        var programa = Programa();
        programa.IsProtected = true;

        Assert.True(ProgramColumns.IsProtected(programa));
    }

    [Fact]
    public void A_origem_de_um_orfao_e_desconhecida()
    {
        var orfao = new InstalledProgram { DisplayName = "Achado no disco" };

        Assert.Equal(ProgramSource.Unknown, ProgramColumns.SourceOf(orfao));
        Assert.Equal(ProgramSource.UninstallKey, ProgramColumns.SourceOf(Programa()));
    }

    [Fact]
    public void A_regra_do_orfao_mora_em_um_lugar_so()
    {
        // Se a condicao "sem chave de registro" ganhasse uma segunda
        // interpretacao, a origem e a marca contariam coisas diferentes.
        var fonte = RepoFiles.Read("src", "Tranal.Uninstaller", "Core", "ProgramColumns.cs");

        Assert.DoesNotContain("IsNullOrWhiteSpace(program.RegistryKey)", fonte);
        Assert.Contains("ProgramMarks.IsOrphan(program)", fonte);
    }

    // ==================================================================
    // O nome exibido
    // ==================================================================

    [Fact]
    public void O_nome_exibido_embute_a_marca_de_32_bits()
    {
        if (!Environment.Is64BitOperatingSystem) return;

        var programa = Programa("Meu Programa");
        programa.Bits = "32-bits";

        var nome = ProgramMarks.WithBits(programa.DisplayName, ProgramMarks.Detect(programa));

        Assert.Equal("Meu Programa (32-bit)", nome);
    }

    [Fact]
    public void O_nome_exibido_fica_igual_sem_a_marca()
    {
        var programa = Programa("Meu Programa");

        var nome = ProgramMarks.WithBits(programa.DisplayName, ProgramMarks.Detect(programa));

        Assert.Equal("Meu Programa", nome);
    }

    // ==================================================================
    // Os idiomas e a ligacao com a interface
    // ==================================================================

    [Fact]
    public void As_marcas_estao_nos_dois_idiomas()
    {
        foreach (var idioma in new[] { "pt-BR", "en-US" })
        {
            var catalogo = Loc.Load(idioma);

            foreach (var chave in new[]
            {
                "Common.Marks",
                "ProgramMark.32Bits",
                "ProgramMark.Update",
                "ProgramMark.Orphan",
                "ProgramMark.Invalid",
                "ProgramMark.Protected",
                "ProgramMark.BitsSuffix",
            })
            {
                Assert.True(catalogo.ContainsKey(chave), $"{idioma} sem a chave {chave}");
                Assert.False(string.IsNullOrWhiteSpace(catalogo[chave]), $"a chave {chave} esta vazia em {idioma}");
            }
        }
    }

    [Fact]
    public void O_rotulo_da_marca_vem_do_catalogo()
    {
        var catalogo = Loc.Load("pt-BR");

        Assert.Equal(catalogo["ProgramMark.Protected"], ProgramMarks.Label(ProgramMark.Protected));
    }

    [Fact]
    public void A_tela_mostra_o_nome_com_a_marca()
    {
        var xaml = RepoFiles.Read("src", "Tranal.Uninstaller", "MainWindow.xaml");
        var linha = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "ProgramRow.cs");

        Assert.Contains("Binding Label", xaml);

        Assert.Contains("ProgramMarks.WithBits", linha);
        Assert.Contains("ProgramMarks.Detect", linha);
    }
}
