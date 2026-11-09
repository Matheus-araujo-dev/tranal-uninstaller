using System;
using System.Collections.Generic;
using System.Linq;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Um aviso que aparece **antes** de uma operação, para o usuário decidir.
///
/// O aviso carrega um codigo estavel (para o teste e para a traducao
/// saberem do que se trata) e diz se ele impede a operação ou apenas
/// informa.
/// </summary>
/// <param name="Code">O identificador do aviso ("browser-running").</param>
/// <param name="Message">O texto do aviso, ja no idioma da interface.</param>
/// <param name="Blocking">True quando a operação não deve seguir.</param>
public sealed record OperationWarning(string Code, string Message, bool Blocking);

/// <summary>
/// As guardas de operação: o que precisa ser avisado **antes** de limpar o
/// navegador ou de forçar a desinstalação.
///
/// A REGRA mora aqui, em um lugar só: quem avisa consome esta lista, e a
/// interface gráfica e a linha de comando nunca discordam sobre o que
/// merece aviso.
///
/// São dois avisos medidos nos concorrentes:
///
///   - o navegador está em execução (o Wise avisa antes de limpar os dados
///     do navegador: arquivo em uso é arquivo que não se apaga);
///   - a desinstalação forçada não foi precedida da segura (o Wise avisa
///     que a forçada é o plano B, e o plano A é o desinstalador do
///     fabricante).
///
/// O texto mora no catalogo de idioma ("Guard.*"); o nucleo decide *quando*
/// avisar, e a traducao decide *como* o aviso se lê.
/// </summary>
public static class OperationGuards
{
    /// <summary>O codigo do aviso de navegador em execução.</summary>
    public const string CodeNavegadorRodando = "browser-running";

    /// <summary>O codigo do aviso de forçada antes da segura.</summary>
    public const string CodeForcadaAntesDaSegura = "forced-before-safe";

    /// <summary>
    /// O processo de cada navegador. O nome e o do executavel, sem o ".exe".
    /// </summary>
    private static readonly Dictionary<string, string[]> ProcessosPorNavegador = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Chrome"] = new[] { "chrome" },
        ["Edge"] = new[] { "msedge" },
        ["Brave"] = new[] { "brave" },
        ["Vivaldi"] = new[] { "vivaldi" },
        ["Opera"] = new[] { "opera" },
        ["Firefox"] = new[] { "firefox" },
    };

    /// <summary>
    /// Os navegadores que estão em execução agora.
    ///
    /// Não impede a limpeza (o aviso é informativo): quem decide é o
    /// usuário. Mas quem limpa sem saber que o navegador está aberto
    /// descobre que metade dos arquivos ficou para trás.
    /// </summary>
    public static IReadOnlyList<OperationWarning> NavegadoresRodando()
    {
        var avisos = new List<OperationWarning>();

        var processos = ProcessInspector.List();
        if (processos.Count == 0) return avisos;

        foreach (var par in ProcessosPorNavegador)
        {
            var nomes = par.Value;

            var quantos = processos.Count(p => nomes.Contains(p.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase));
            if (quantos == 0) continue;

            avisos.Add(new OperationWarning(
                CodeNavegadorRodando,
                Loc.Get("Guard.BrowserRunning", par.Key, quantos),
                Blocking: false));
        }

        return avisos;
    }

    /// <summary>
    /// True quando a desinstalação segura já foi tentada para o programa.
    ///
    /// A resposta vem do histórico: uma entrada com o nome do programa
    /// significa que o Tranal já o desinstalou uma vez. E o que distingue
    /// "a forçada é o plano B" de "a forçada é o único caminho" — e sem
    /// essa distinção o aviso apareceria para quem já usou o caminho seguro.
    /// </summary>
    /// <param name="programa">O programa que o usuário quer remover.</param>
    public static bool SeguraJaTentada(InstalledProgram? programa)
    {
        if (programa is null) return false;

        var nome = (programa.DisplayName ?? string.Empty).Trim();
        if (nome.Length == 0) return false;

        foreach (var entrada in UninstallHistory.Read(int.MaxValue))
        {
            var programas = entrada?.Programs ?? string.Empty;
            if (programas.Length == 0) continue;

            if (programas.IndexOf(nome, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return false;
    }

    /// <summary>
    /// O aviso de usar a desinstalação forçada antes da segura.
    ///
    /// O aviso só aparece quando faz sentido: quando o programa tem
    /// desinstalador registrado (é o plano A) e a segura ainda não foi
    /// tentada neste programa. Sem desinstalador, a forçada não é um plano
    /// B — é o único caminho — e não há o que avisar.
    /// </summary>
    /// <param name="programa">O programa que o usuario quer remover.</param>
    /// <param name="seguraJaTentada">True quando a desinstalação segura ja rodou.</param>
    public static OperationWarning? AvisoDeForcadaAntesDaSegura(
        InstalledProgram? programa,
        bool seguraJaTentada)
    {
        if (seguraJaTentada) return null;
        if (programa is null) return null;

        // Sem desinstalador registrado, a desinstalacao forcada nao e um
        // plano B: e o unico caminho. Nao ha o que avisar.
        if (string.IsNullOrWhiteSpace(programa.UninstallString)) return null;

        return new OperationWarning(
            CodeForcadaAntesDaSegura,
            Loc.Get("Guard.ForcedBeforeSafe", programa.DisplayName),
            Blocking: false);
    }
}
