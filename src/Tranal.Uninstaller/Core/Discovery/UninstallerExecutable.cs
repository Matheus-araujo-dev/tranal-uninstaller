using System;
using System.Collections.Generic;
using System.IO;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// O executavel que faz a desinstalacao de um programa.
///
/// A REGRA mora aqui, em um lugar so. Antes ela estava espalhada em tres
/// copias que discordavam entre si: a coluna "Localizacao do desinstalador"
/// mostrava <c>MsiExec.exe</c> (o nome cru, sem caminho), a tela de assinatura
/// dizia que o programa "nao tem desinstalador registrado" (porque o nome
/// solto nao existe no disco), e a linha de comando pulava o programa.
/// Medido em 10/11/2026 no lote 30.
///
/// <para>Duas perguntas, dois metodos, e as duas com a resposta no nucleo:</para>
/// <list type="bullet">
/// <item><see cref="Declared"/>: <b>o que o registro nomeia</b>. E o texto do
/// comando de desinstalacao, sem inventar caminho.</item>
/// <item><see cref="Resolve"/>: <b>onde o executavel esta</b>. E o caminho
/// absoluto quando da para saber — o que inclui o caso do instalador do
/// Windows (<c>msiexec.exe</c>), que o registro nomeia sem caminho.</item>
/// </list>
///
/// <para>A resolucao segue a regra do proprio Windows: um comando sem caminho
/// absoluto e procurado no PATH. E por isso que <c>MsiExec.exe /X{...}</c>
/// vira <c>C:\Windows\System32\msiexec.exe</c> — que e o executavel que
/// realmente roda.</para>
/// </summary>
public static class UninstallerExecutable
{
    /// <summary>
    /// O executavel do desinstalador, como o registro o nomeia.
    ///
    /// Nao procura o disco: devolve o que esta escrito no comando. Quando o
    /// comando nao nomeia nenhum executavel, devolve string vazia — a coluna
    /// fica vazia em vez de mostrar um caminho inventado.
    /// </summary>
    /// <param name="program">O programa instalado.</param>
    public static string Declared(InstalledProgram? program)
    {
        if (program is null) return string.Empty;

        var comando = program.UninstallString;
        if (string.IsNullOrWhiteSpace(comando)) return string.Empty;

        return ProgramScanner.ExtractExecutablePath(comando).Trim();
    }

    /// <summary>
    /// O caminho absoluto do executavel do desinstalador, quando da para saber.
    ///
    /// Tres casos, nesta ordem:
    ///
    ///   1. o comando ja traz caminho absoluto — devolve esse caminho (mesmo
    ///      que o arquivo nao exista mais: o comando continua sendo o do
    ///      registro, e a tela mostra que o arquivo sumiu);
    ///   2. o comando traz so o nome do executavel — procura no PATH, como o
    ///      proprio Windows faz quando roda o comando;
    ///   3. nao ha como saber — devolve string vazia.
    /// </summary>
    /// <param name="program">O programa instalado.</param>
    public static string Resolve(InstalledProgram? program)
    {
        var declarado = Declared(program);
        if (declarado.Length == 0) return string.Empty;

        // Ja tem raiz: e o caminho do registro. A existencia do arquivo e
        // outra pergunta (a do "desinstalador quebrado").
        if (Path.IsPathRooted(declarado)) return declarado;

        // Nome solto: o Windows procura no PATH. A busca do Tranal e a mesma.
        var encontrado = ProcurarNoPath(declarado);
        return encontrado.Length > 0 ? encontrado : string.Empty;
    }

    /// <summary>
    /// True quando o programa e desinstalado pelo instalador do Windows
    /// (o <c>msiexec.exe</c>).
    ///
    /// E um caso que merece fala propria: o executavel e um componente do
    /// Windows, assinado pelo catalogo de componentes do proprio Windows —
    /// nao por um editor de software. Dizer "sem assinatura digital" sobre
    /// ele seria enganoso.
    /// </summary>
    /// <param name="program">O programa instalado.</param>
    public static bool IsWindowsInstaller(InstalledProgram? program)
    {
        if (program is null) return false;

        if (program.Kind == UninstallerKind.MsiExec) return true;
        if (program.IsWindowsInstaller || program.IsMsi) return true;

        var declarado = Declared(program);
        return declarado.Length > 0
            && Path.GetFileName(declarado)
                .Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True quando o caminho aponta para um componente do proprio Windows
    /// (a pasta System32 ou a SysWOW64).
    ///
    /// E o mesmo juizo do <see cref="IsWindowsInstaller"/>, olhando o disco:
    /// um executavel do Windows nao e assinado por um editor de software, e
    /// sim pelo catalogo de componentes do Windows.
    /// </summary>
    /// <param name="caminho">O caminho do executavel.</param>
    public static bool IsWindowsComponent(string? caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho)) return false;

        try
        {
            var sistema = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (sistema.Length == 0) return false;

            if (caminho.StartsWith(sistema, StringComparison.OrdinalIgnoreCase)) return true;

            // A pasta de 32 bits do Windows fica ao lado da de 64 bits.
            var windows = Path.GetDirectoryName(sistema.TrimEnd('\\'));
            if (string.IsNullOrEmpty(windows)) return false;

            var wow64 = Path.Combine(windows, "SysWOW64");
            return caminho.StartsWith(wow64, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            // Um caminho invalido nao e um componente do Windows: o veredito
            // fica "nao", e a tela mostra o que deu.
            return false;
        }
    }

    /// <summary>
    /// Procura o executavel no PATH, como o Windows faz.
    ///
    /// A pasta System32 entra na busca mesmo sem estar no PATH: e o caso do
    /// <c>msiexec.exe</c>, e sem ele a assinatura do desinstalador do Windows
    /// Installer ficaria sem resposta.
    /// </summary>
    /// <param name="nome">O nome do executavel.</param>
    private static string ProcurarNoPath(string nome)
    {
        if (nome.Contains('/') || nome.Contains('\\')) return string.Empty;

        foreach (var pasta in PastasDoPath())
        {
            try
            {
                var candidato = Path.Combine(pasta, nome);
                if (File.Exists(candidato)) return candidato;
            }
            catch (Exception)
            {
                // Pasta inacessivel: segue para a proxima. O PATH do
                // usuario nao pode derrubar a resolucao.
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// As pastas onde o Windows procuraria o executavel: o PATH do processo
    /// e a pasta System32.
    /// </summary>
    private static IEnumerable<string> PastasDoPath()
    {
        var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var pasta in path.Split(';'))
        {
            var limpa = pasta.Trim().Trim('"');
            if (limpa.Length == 0) continue;

            if (vistas.Add(limpa)) yield return limpa;
        }

        var sistema = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (sistema.Length > 0 && vistas.Add(sistema)) yield return sistema;
    }
}
