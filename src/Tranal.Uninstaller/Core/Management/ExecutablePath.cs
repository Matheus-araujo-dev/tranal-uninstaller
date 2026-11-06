using System;
using System.IO;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// Extracao do executavel a partir de uma linha de comando.
///
/// Todo o painel de gestao (servico, tarefa agendada, inicializacao, menu de
/// contexto) precisa da **mesma** resposta para a pergunta "qual executavel
/// isso roda?". Se cada um fizesse o parse por conta propria, um acabaria
/// cortando no primeiro espaco e outro nao — e a correlacao com o programa
/// instalado passaria a falhar em um caso e funcionar no outro.
///
/// A regra e do Windows: o executavel pode vir entre aspas, ou solto. Quando
/// vem solto e contem espaco, o prefixo mais longo que **existe no disco** e o
/// executavel — porque so o disco sabe onde o nome do arquivo termina.
/// </summary>
internal static class ExecutablePathResolver
{
    /// <summary>
    /// Devolve o executavel de uma linha de comando, ou string vazia.
    ///
    /// </summary>
    /// <param name="commandLine">A linha de comando completa.</param>
    public static string Extract(string? commandLine)
    {
        var raw = (commandLine ?? string.Empty).Trim();
        if (raw.Length == 0) return string.Empty;

        if (raw[0] == '"')
        {
            var closing = raw.IndexOf('"', 1);
            if (closing > 1) return raw.Substring(1, closing - 1);

            return raw.Trim('"');
        }

        return LongestExistingPrefix(raw);
    }

    /// <summary>
    /// O prefixo mais longo da linha de comando que existe como arquivo.
    ///
    /// Sem aspas, o Windows aceita um executavel com espaco no nome; cortar no
    /// primeiro espaco daria "C:\Program", que nao existe e nao casa com
    /// nada. Por isso o prefixo mais longo que existe no disco vence.
    /// </summary>
    private static string LongestExistingPrefix(string raw)
    {
        var cut = raw.Length;
        var fallback = raw;

        while (cut > 0)
        {
            var candidate = raw.Substring(0, cut).Trim();
            if (candidate.Length == 0) break;

            if (File.Exists(candidate)) return candidate;

            // O proximo corte e no ultimo separador antes do espaco.
            var space = raw.LastIndexOf(' ', cut - 1);
            if (space <= 0) break;

            fallback = raw.Substring(0, space);
            cut = space;
        }

        var firstSpace = raw.IndexOf(' ');
        return firstSpace > 0 ? raw.Substring(0, firstSpace) : fallback;
    }
}
