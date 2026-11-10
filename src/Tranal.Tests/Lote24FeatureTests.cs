using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O lote 24 fecha a confianca das sobras em cinco niveis (BCU T1#42), os
/// dois criterios de pasta que faltavam (BCU T1#44) e o aviso de perigo ao
/// mexer em item de baixa confianca (BCU T1#46).
///
/// A REGRA da nota mora no nucleo (<c>Core/LeftoverRatings.cs</c>) e a suite
/// testa esse modulo. A ligacao com a interface e conferida sobre o fonte,
/// pelo mesmo motivo das outras paridades: a suite nao referencia o projeto
/// grafico (WPF).
/// </summary>
public class Lote24FeatureTests
{
    // ==================================================================
    // A nota de cinco niveis
    // ==================================================================

    [Fact]
    public void A_nota_muito_bom_vem_da_confianca_alta_com_motivo_exato()
    {
        Assert.Equal(
            LeftoverRating.VeryGood,
            LeftoverRatings.Rate(LeftoverConfidence.High, LeftoverReason.ExactNameMatch));

        Assert.Equal(
            LeftoverRating.VeryGood,
            LeftoverRatings.Rate(LeftoverConfidence.High, LeftoverReason.InstallFolder));

        Assert.Equal(
            LeftoverRating.VeryGood,
            LeftoverRatings.Rate(LeftoverConfidence.High, LeftoverReason.ExplicitTarget));
    }

    [Fact]
    public void A_nota_boa_vem_da_confianca_alta_com_motivo_especifico()
    {
        Assert.Equal(
            LeftoverRating.Good,
            LeftoverRatings.Rate(LeftoverConfidence.High, LeftoverReason.ObsoleteShortcut));

        Assert.Equal(
            LeftoverRating.Good,
            LeftoverRatings.Rate(LeftoverConfidence.High, LeftoverReason.StartupEntry));
    }

    [Fact]
    public void A_nota_duvidosa_vem_da_confianca_media_com_nome_parecido()
    {
        Assert.Equal(
            LeftoverRating.Doubtful,
            LeftoverRatings.Rate(LeftoverConfidence.Medium, LeftoverReason.SimilarNameMatch));

        Assert.Equal(
            LeftoverRating.Doubtful,
            LeftoverRatings.Rate(LeftoverConfidence.Medium, LeftoverReason.SubfolderMatch));
    }

    [Fact]
    public void A_nota_boa_vem_da_confianca_media_com_motivo_forte()
    {
        Assert.Equal(
            LeftoverRating.Good,
            LeftoverRatings.Rate(LeftoverConfidence.Medium, LeftoverReason.VariantNameMatch));
    }

    [Fact]
    public void A_nota_mau_vem_da_confianca_baixa()
    {
        Assert.Equal(
            LeftoverRating.Bad,
            LeftoverRatings.Rate(LeftoverConfidence.Low, LeftoverReason.SimilarNameMatch));

        Assert.Equal(
            LeftoverRating.Bad,
            LeftoverRatings.Rate(LeftoverConfidence.Low, LeftoverReason.SubfolderMatch));
    }

    [Fact]
    public void Sem_motivo_nao_ha_nota()
    {
        Assert.Equal(
            LeftoverRating.Unknown,
            LeftoverRatings.Rate(LeftoverConfidence.High, LeftoverReason.Unknown));
    }

    [Fact]
    public void A_nota_da_sobra_ve_a_confianca_e_o_motivo()
    {
        var item = new LeftoverItem
        {
            Kind = "Pasta",
            Path = @"C:\App",
            Confidence = LeftoverConfidence.Medium,
            Reason = LeftoverReason.SimilarNameMatch,
        };

        Assert.Equal(LeftoverRating.Doubtful, LeftoverRatings.Rate(item));
        Assert.Equal(LeftoverRating.Unknown, LeftoverRatings.Rate((LeftoverItem?)null));
    }

    [Fact]
    public void A_nota_boa_e_a_nota_muito_bom_dispensam_o_aviso()
    {
        Assert.True(LeftoverRatings.IsSafe(LeftoverRating.VeryGood));
        Assert.True(LeftoverRatings.IsSafe(LeftoverRating.Good));

        Assert.False(LeftoverRatings.IsSafe(LeftoverRating.Doubtful));
        Assert.False(LeftoverRatings.IsSafe(LeftoverRating.Bad));
        Assert.False(LeftoverRatings.IsSafe(LeftoverRating.Unknown));
    }

    [Fact]
    public void A_nota_mau_e_perigosa()
    {
        Assert.True(LeftoverRatings.IsDangerous(LeftoverRating.Bad));

        Assert.False(LeftoverRatings.IsDangerous(LeftoverRating.VeryGood));
        Assert.False(LeftoverRatings.IsDangerous(LeftoverRating.Good));
        Assert.False(LeftoverRatings.IsDangerous(LeftoverRating.Doubtful));
    }

    [Fact]
    public void Os_itens_de_nota_duvidosa_ou_ruim_sao_os_riscosos()
    {
        var riscosos = LeftoverRatings.RiskyItems(new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = @"C:\A", Confidence = LeftoverConfidence.High, Reason = LeftoverReason.ExactNameMatch },
            new LeftoverItem { Kind = "Pasta", Path = @"C:\B", Confidence = LeftoverConfidence.Medium, Reason = LeftoverReason.SimilarNameMatch },
            new LeftoverItem { Kind = "Pasta", Path = @"C:\C", Confidence = LeftoverConfidence.Low, Reason = LeftoverReason.SubfolderMatch },
        });

        Assert.Equal(2, riscosos.Count);
        Assert.Contains(riscosos, i => i.Path == @"C:\B");
        Assert.Contains(riscosos, i => i.Path == @"C:\C");
    }

    // ==================================================================
    // O aviso de perigo
    // ==================================================================

    [Fact]
    public void O_aviso_de_perigo_aparece_para_item_marcado_de_nota_ruim()
    {
        var avisos = LeftoverWarnings.Build(new[]
        {
            new LeftoverItem
            {
                Kind = "Pasta",
                Path = @"C:\App",
                Selected = true,
                Confidence = LeftoverConfidence.Low,
                Reason = LeftoverReason.SimilarNameMatch,
            },
        });

        var perigo = avisos.Single(a => a.Kind == LeftoverWarningKind.Dangerous);
        Assert.Equal(LeftoverWarnings.DangerousKey, perigo.MessageKey);
        Assert.Contains(@"C:\App", perigo.Items);
    }

    [Fact]
    public void O_aviso_de_perigo_desaparece_quando_o_item_e_desmarcado()
    {
        var item = new LeftoverItem
        {
            Kind = "Pasta",
            Path = @"C:\App",
            Selected = true,
            Confidence = LeftoverConfidence.Low,
            Reason = LeftoverReason.SimilarNameMatch,
        };

        Assert.Contains(
            LeftoverWarnings.Build(new[] { item }),
            a => a.Kind == LeftoverWarningKind.Dangerous);

        item.Selected = false;

        Assert.DoesNotContain(
            LeftoverWarnings.Build(new[] { item }),
            a => a.Kind == LeftoverWarningKind.Dangerous);
    }

    [Fact]
    public void O_aviso_de_perigo_nao_aparece_para_item_de_nota_boa()
    {
        var avisos = LeftoverWarnings.Build(new[]
        {
            new LeftoverItem
            {
                Kind = "Pasta",
                Path = @"C:\App",
                Selected = true,
                Confidence = LeftoverConfidence.High,
                Reason = LeftoverReason.ExactNameMatch,
            },
        });

        Assert.DoesNotContain(avisos, a => a.Kind == LeftoverWarningKind.Dangerous);
    }

    [Fact]
    public void So_o_item_marcado_entra_no_aviso_de_perigo()
    {
        var marcados = LeftoverRatings.MarkedRiskyItems(new[]
        {
            new LeftoverItem { Kind = "Pasta", Path = @"C:\Marcado", Selected = true, Confidence = LeftoverConfidence.Low, Reason = LeftoverReason.SimilarNameMatch },
            new LeftoverItem { Kind = "Pasta", Path = @"C:\Desmarcado", Selected = false, Confidence = LeftoverConfidence.Low, Reason = LeftoverReason.SimilarNameMatch },
        });

        Assert.Single(marcados);
        Assert.Equal(@"C:\Marcado", marcados[0].Path);
    }

    [Fact]
    public void O_aviso_de_perigo_diz_o_que_pode_ser_perigoso()
    {
        var pt = LerCatalogo("pt-BR.json");
        var en = LerCatalogo("en-US.json");

        Assert.True(pt.ContainsKey(LeftoverWarnings.DangerousKey));
        Assert.True(en.ContainsKey(LeftoverWarnings.DangerousKey));

        Assert.Contains("perigoso", pt[LeftoverWarnings.DangerousKey], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dangerous", en[LeftoverWarnings.DangerousKey], StringComparison.OrdinalIgnoreCase);
    }

    // ==================================================================
    // Os rotulos da nota
    // ==================================================================

    [Fact]
    public void Os_rotulos_da_nota_estao_nos_dois_idiomas()
    {
        var chaves = new[]
        {
            "Common.Rating.VeryGood",
            "Common.Rating.Good",
            "Common.Rating.Doubtful",
            "Common.Rating.Bad",
            "Common.Rating.Unknown",
        };

        var pt = LerCatalogo("pt-BR.json");
        var en = LerCatalogo("en-US.json");

        foreach (var chave in chaves)
        {
            Assert.True(pt.ContainsKey(chave), $"chave ausente no pt-BR: {chave}");
            Assert.True(en.ContainsKey(chave), $"chave ausente no en-US: {chave}");

            Assert.False(string.IsNullOrWhiteSpace(pt[chave]));
            Assert.False(string.IsNullOrWhiteSpace(en[chave]));
        }
    }

    // ==================================================================
    // Os dois criterios de pasta que faltavam
    // ==================================================================

    [Fact]
    public void O_explicito_de_pasta_tem_o_criterio_de_muitos_arquivos()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "tranal-lote24-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);

        try
        {
            var avaliacao = FolderConfidenceEvaluator.Evaluate(pasta);

            Assert.Contains(avaliacao.Criteria, c => c.Id == FolderConfidenceEvaluator.ManyFilesCriterion);
            Assert.Contains(avaliacao.Criteria, c => c.Id == FolderConfidenceEvaluator.NoSubdirectoriesCriterion);
        }
        finally
        {
            Directory.Delete(pasta, true);
        }
    }

    [Fact]
    public void A_pasta_com_muitos_arquivos_tem_o_criterio_positivo()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "tranal-lote24-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);

        try
        {
            for (var i = 0; i < FolderConfidenceEvaluator.ManyFilesThreshold + 10; i++)
            {
                File.WriteAllText(Path.Combine(pasta, "arquivo" + i + ".txt"), "x");
            }

            var avaliacao = FolderConfidenceEvaluator.Evaluate(pasta);

            var criterio = avaliacao.Criteria.Single(c => c.Id == FolderConfidenceEvaluator.ManyFilesCriterion);
            Assert.True(criterio.Positive);

            var semSub = avaliacao.Criteria.Single(c => c.Id == FolderConfidenceEvaluator.NoSubdirectoriesCriterion);
            Assert.True(semSub.Positive);
        }
        finally
        {
            Directory.Delete(pasta, true);
        }
    }

    [Fact]
    public void A_pasta_com_subdiretorios_tem_o_criterio_negativo()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "tranal-lote24-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        Directory.CreateDirectory(Path.Combine(pasta, "sub"));

        try
        {
            var avaliacao = FolderConfidenceEvaluator.Evaluate(pasta);

            var semSub = avaliacao.Criteria.Single(c => c.Id == FolderConfidenceEvaluator.NoSubdirectoriesCriterion);
            Assert.False(semSub.Positive);
        }
        finally
        {
            Directory.Delete(pasta, true);
        }
    }

    [Fact]
    public void A_pasta_com_dado_do_usuario_continua_na_confianca_mais_baixa()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "tranal-lote24-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);

        try
        {
            // Muitos arquivos de documento: os dois criterios novos somam
            // ponto, mas a pasta tem documento do usuario e nao pode virar
            // "alta confianca" — alta confianca e "marcado para remocao".
            for (var i = 0; i < FolderConfidenceEvaluator.ManyFilesThreshold + 10; i++)
            {
                File.WriteAllText(Path.Combine(pasta, "documento" + i + ".txt"), "x");
            }

            var avaliacao = FolderConfidenceEvaluator.Evaluate(pasta);

            Assert.Equal(FolderConfidenceLevel.Low, avaliacao.Level);
            Assert.True(avaliacao.MayContainUserData);
        }
        finally
        {
            Directory.Delete(pasta, true);
        }
    }

    // ==================================================================
    // A ligacao com a interface e com a linha de comando
    // ==================================================================

    [Fact]
    public void A_arvore_mostra_a_nota_e_avisa_a_mudanca_de_marcacao()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "Shared", "LeftoversTree.xaml.cs");

        Assert.Contains("LeftoverRatings.Rate", codigo);
        Assert.Contains("LeftoverLabels.Rating", codigo);
        Assert.Contains("SelectionChanged", codigo);
    }

    [Fact]
    public void O_assistente_reage_a_marcacao_das_sobras()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Uninstaller", "Ui", "UninstallWizardWindow.xaml.cs");

        Assert.Contains("LeftoversTreeControl.SelectionChanged", codigo);
        Assert.Contains("ShowLeftoverAdvisory", codigo);
    }

    [Fact]
    public void A_linha_de_comando_mostra_a_nota()
    {
        var codigo = RepoFiles.Read("src", "Tranal.Cli", "Program.cs");

        Assert.Contains("LeftoverRatings.Rate", codigo);
        Assert.Contains("LeftoverLabels.Rating", codigo);
    }

    // ==================================================================
    // Auxiliares
    // ==================================================================

    private static Dictionary<string, string> LerCatalogo(string arquivo)
    {
        var texto = RepoFiles.Read("src", "Tranal.Uninstaller", "Lang", arquivo);
        using var documento = System.Text.Json.JsonDocument.Parse(texto);

        var dicionario = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var propriedade in documento.RootElement.EnumerateObject())
        {
            dicionario[propriedade.Name] = propriedade.Value.GetString() ?? string.Empty;
        }

        return dicionario;
    }
}
