using System.IO;
using System.Text;

namespace Tranal.Uninstaller.Core.Reporting;

/// <summary>
/// O script de desinstalacao em lote.
///
/// O script roda o desinstalador nativo de cada programa. O comando vem
/// sempre de <see cref="QuietUninstall"/> (quando existe um jeito conhecido
/// de silenciar aquele desinstalador) ou do proprio registro — nunca de uma
/// lista reescrita aqui: a regra do comando silencioso mora em um lugar so.
///
/// O script gerado e seguro por construcao:
/// <list type="bullet">
/// <item>o executavel vai entre aspas, entao um caminho com espaco
/// ("C:\Program Files\App\uninstall.exe") funciona;</item>
/// <item>o <c>%</c> vira <c>%%</c>, para o texto do usuario nao virar
/// variavel do <c>cmd</c>;</item>
/// <item>o texto do <c>echo</c> escapa <c>&amp;</c>, <c>|</c>, <c>&lt;</c>,
/// <c>&gt;</c> e <c>^</c>, para o nome do programa nao virar comando.</item>
/// </list>
/// </summary>
public static class BatchScriptWriter
{
    /// <summary>
    /// Monta o script de desinstalacao em lote.
    ///
    /// Com <paramref name="silent"/> ligado, cada programa e desinstalado
    /// pelo comando silencioso quando existe um; sem ele, pelo comando
    /// registrado.
    /// </summary>
    /// <param name="programs">Os programas a desinstalar.</param>
    /// <param name="silent">Usar o comando silencioso quando existir.</param>
    public static string Build(IEnumerable<InstalledProgram> programs, bool silent = true)
    {
        var lista = (programs ?? Enumerable.Empty<InstalledProgram>())
            .Where(p => p is not null)
            .ToList();

        var sb = new StringBuilder();

        sb.AppendLine("@echo off");
        sb.AppendLine("setlocal");

        // O script e gravado em UTF-8; o console passa para UTF-8 para que os
        // acentos saiam certos.
        sb.AppendLine("chcp 65001 > nul");
        sb.AppendLine();

        sb.AppendLine("rem ---------------------------------------------------------------");
        sb.AppendLine("rem  Script de desinstalacao em lote");
        sb.AppendLine("rem  Tranal Uninstaller Pro - gerado em "
            + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
        sb.AppendLine("rem  " + lista.Count + " programa(s)");
        sb.AppendLine("rem");
        sb.AppendLine("rem  Este script roda os desinstaladores nativos. Alguns deles");
        sb.AppendLine("rem  pedem permissao de administrador: execute-o como administrador.");
        sb.AppendLine("rem ---------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine("echo.");

        var quantos = 0;

        foreach (var program in lista)
        {
            var comando = ComandoDe(program, silent);

            sb.AppendLine("echo Desinstalando: " + EscapeForEcho(program.DisplayName));

            if (string.IsNullOrWhiteSpace(comando))
            {
                sb.AppendLine("echo   [ignorado] sem comando de desinstalacao");
                sb.AppendLine();
                continue;
            }

            var (exe, argumentos) = UninstallEngine.SplitCommand(comando);
            if (string.IsNullOrWhiteSpace(exe))
            {
                sb.AppendLine("echo   [ignorado] comando de desinstalacao invalido");
                sb.AppendLine();
                continue;
            }

            sb.AppendLine(LinhaDeExecucao(exe, argumentos));
            sb.AppendLine("if errorlevel 1 echo   [falha] o desinstalador acima retornou erro");
            sb.AppendLine();

            quantos++;
        }

        sb.AppendLine("echo Concluido: " + quantos + " programa(s) desinstalado(s).");
        sb.AppendLine("endlocal");
        sb.AppendLine("exit /b 0");

        return sb.ToString();
    }

    /// <summary>
    /// Grava o script de desinstalacao em lote no destino informado.
    ///
    /// O arquivo sai em **UTF-8 sem BOM**: um <c>.cmd</c> com BOM faz o
    /// <c>cmd</c> tentar executar o proprio BOM como comando.
    /// </summary>
    /// <param name="path">O caminho do arquivo a gravar.</param>
    /// <param name="programs">Os programas a desinstalar.</param>
    /// <param name="silent">Usar o comando silencioso quando existir.</param>
    /// <returns>O caminho gravado, ou string vazia quando falhou.</returns>
    public static string Write(string path, IEnumerable<InstalledProgram> programs, bool silent = true)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            var destino = Path.GetFullPath(path);
            var pasta = Path.GetDirectoryName(destino);
            if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

            File.WriteAllText(destino, Build(programs, silent), new UTF8Encoding(false));
            return destino;
        }
        catch
        {
            // Falha de gravacao: quem chama precisa avisar o usuario, e o
            // destino fica vazio para isso.
            return string.Empty;
        }
    }

    /// <summary>
    /// O comando de desinstalacao do programa.
    ///
    /// Com <paramref name="silent"/> ligado, vale o comando silencioso: o
    /// que o registro traz (<c>QuietUninstallCommand</c>), ou o derivado da
    /// familia do desinstalador (<see cref="QuietUninstall.Build"/>). Sem
    /// ele, vale o comando registrado.
    /// </summary>
    /// <param name="program">O programa.</param>
    /// <param name="silent">Usar o comando silencioso.</param>
    private static string ComandoDe(InstalledProgram program, bool silent)
    {
        if (!silent) return program.UninstallString ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(program.QuietUninstallCommand))
            return program.QuietUninstallCommand.Trim();

        var derivado = QuietUninstall.Build(program);
        if (!string.IsNullOrWhiteSpace(derivado)) return derivado.Trim();

        return program.UninstallString ?? string.Empty;
    }

    /// <summary>
    /// A linha que roda o desinstalador: o executavel entre aspas e os
    /// argumentos como estavam.
    /// </summary>
    /// <param name="exe">O executavel.</param>
    /// <param name="argumentos">Os argumentos.</param>
    private static string LinhaDeExecucao(string exe, string argumentos)
    {
        // O executavel vai entre aspas: um caminho com espaco ("C:\Program
        // Files\App\uninstall.exe") precisa delas para funcionar.
        var limpo = (exe ?? string.Empty).Trim().Trim('"');
        var linha = "\"" + EscaparPercent(limpo) + "\"";

        if (!string.IsNullOrWhiteSpace(argumentos))
        {
            // Os argumentos vao como estavam: alguns desinstaladores dependem
            // das aspas internas do proprio comando (o InstallShield usa
            // /v"/qn").
            linha += " " + EscaparPercent(argumentos);
        }

        return linha;
    }

    /// <summary>
    /// O <c>%</c> vira <c>%%</c>: em um arquivo <c>.cmd</c> o <c>%</c> inicia
    /// uma variavel, e o texto do usuario nao pode virar variavel.
    /// </summary>
    /// <param name="text">O texto.</param>
    private static string EscaparPercent(string text) => (text ?? string.Empty).Replace("%", "%%");

    /// <summary>
    /// Escapa um texto para sair dentro de um <c>echo</c> do <c>cmd</c>.
    ///
    /// Sem isso, um programa chamado "Foto &amp; Video" transformaria o
    /// <c>echo</c> em dois comandos, e o "&amp;" viraria um separador de
    /// comando.
    /// </summary>
    /// <param name="text">O texto.</param>
    private static string EscapeForEcho(string text)
    {
        var sb = new StringBuilder();

        foreach (var ch in text ?? string.Empty)
        {
            switch (ch)
            {
                case '%': sb.Append("%%"); break;
                case '&': sb.Append("^&"); break;
                case '|': sb.Append("^|"); break;
                case '<': sb.Append("^<"); break;
                case '>': sb.Append("^>"); break;
                case '^': sb.Append("^^"); break;
                case '!': sb.Append("^!"); break;

                // Uma quebra de linha encerraria o comando do cmd.
                case '\r':
                case '\n': sb.Append(' '); break;

                default: sb.Append(ch); break;
            }
        }

        return sb.ToString();
    }
}
