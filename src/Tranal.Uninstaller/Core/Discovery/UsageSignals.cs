using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Um sinal de uso de um executavel: quando ele rodou pela ultima vez e
/// quantas vezes.
/// </summary>
public sealed class UsageSignal
{
    /// <summary>O caminho do executavel, como o Windows o conhece.</summary>
    public string ExecutablePath { get; init; } = string.Empty;

    /// <summary>
    /// Quando o executavel rodou pela ultima vez. Null quando o Windows nao
    /// tem esse dado (executavel nunca aberto, ou outro usuario).
    /// </summary>
    public DateTime? LastUsed { get; init; }

    /// <summary>Quantas vezes o executavel rodou. Zero quando desconhecido.</summary>
    public int RunCount { get; init; }

    /// <summary>De onde veio o dado ("UserAssist", "Prefetch" ou "desconhecido").</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// True quando o executavel nao roda ha muito tempo — ou nunca rodou.
    /// </summary>
    /// <param name="window">Quanto tempo conta como "raro".</param>
    public bool IsRarelyUsed(TimeSpan window)
    {
        if (!LastUsed.HasValue) return true;
        return DateTime.Now - LastUsed.Value >= window;
    }
}

/// <summary>
/// Le os sinais de uso do Windows para saber quais programas o usuario
/// realmente usa.
///
/// Item 6 da fila da auditoria binaria: "filtros por tamanho e uso raro".
/// O filtro por tamanho ja existe na lista; o de uso raro precisa de dado que
/// o Windows guarda de graca, e esse dado existe em dois lugares:
///
///   - **UserAssist** (registro da conta): guarda, por executavel, quando ele
///     rodou pela ultima vez e quantas vezes. O nome do valor e o caminho do
///     executavel codificado (ROT13 nos letras, +0x50 nos demais) — e por
///     isso que a decodificacao mora aqui.
///   - **Prefetch** (pasta do Windows): um arquivo por executavel, com a data
///     da ultima execucao e o numero de execucoes.
///
/// A leitura e somente leitura: nada e gravado no registro do usuario.
/// </summary>
public static class UsageSignals
{
    /// <summary>
    /// O que conta como "uso raro" quando o filtro nao diz outra coisa.
    /// </summary>
    public static readonly TimeSpan DefaultRarelyUsedWindow = TimeSpan.FromDays(180);

    private const string UserAssistPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist";

    private const string PrefetchFolder = @"C:\Windows\Prefetch";

    /// <summary>
    /// Todos os sinais de uso que o Windows deixou na maquina.
    /// </summary>
    public static IReadOnlyList<UsageSignal> Collect()
    {
        var sinais = new List<UsageSignal>();

        sinais.AddRange(FromUserAssist());
        sinais.AddRange(FromPrefetch());

        return sinais;
    }

    /// <summary>
    /// O sinal de uso de um executavel, procurando primeiro no UserAssist e
    /// depois no Prefetch. Null quando o Windows nao tem dado nenhum.
    /// </summary>
    /// <param name="executablePath">O caminho do executavel.</param>
    public static UsageSignal? SignalOf(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return null;

        var alvo = executablePath.Trim();

        var assist = FromUserAssist().FirstOrDefault(s =>
            string.Equals(s.ExecutablePath, alvo, StringComparison.OrdinalIgnoreCase));

        if (assist is not null) return assist;

        var prefetch = FromPrefetch().FirstOrDefault(s =>
            string.Equals(s.ExecutablePath, alvo, StringComparison.OrdinalIgnoreCase));

        return prefetch;
    }

    /// <summary>
    /// True quando o programa nao e usado ha muito tempo (ou nunca foi).
    ///
    /// Quando o Windows nao tem nenhum dado sobre o executavel, a resposta e
    /// <c>false</c>: sem dado nao ha prova de uso raro, e marcar um programa
    /// como "raro" sem evidencia seria mentir para o usuario.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    /// <param name="window">Quanto tempo conta como "raro".</param>
    public static bool IsRarelyUsed(InstalledProgram program, TimeSpan? window = null)
    {
        if (program is null) return false;

        var executavel = ExecutableOf(program);
        if (executavel.Length == 0) return false;

        var sinal = SignalOf(executavel);
        if (sinal is null || !sinal.LastUsed.HasValue) return false;

        return sinal.IsRarelyUsed(window ?? DefaultRarelyUsedWindow);
    }

    /// <summary>
    /// O executavel de um programa: o proprio desinstalador, quando existe, ou
    /// o executavel da pasta de instalacao.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    public static string ExecutableOf(InstalledProgram program)
    {
        if (program is null) return string.Empty;

        var uninstaller = ProgramScanner.ExtractExecutablePath(program.UninstallString);
        if (!string.IsNullOrWhiteSpace(uninstaller) && File.Exists(uninstaller)) return uninstaller;

        var location = program.InstallLocation ?? string.Empty;
        if (location.Length > 0)
        {
            try
            {
                var exe = Directory.EnumerateFiles(location, "*.exe", SearchOption.TopDirectoryOnly)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(exe)) return exe;
            }
            catch
            {
                // Pasta inacessivel ou apagada: sem executavel conhecido.
            }
        }

        return uninstaller;
    }

    /// <summary>
    /// Os sinais lidos do UserAssist da conta atual.
    /// </summary>
    private static IEnumerable<UsageSignal> FromUserAssist()
    {
        var sinais = new List<UsageSignal>();

        try
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            using var key = root.OpenSubKey(UserAssistPath);
            if (key is null) return sinais;

            foreach (var guidName in key.GetSubKeyNames())
            {
                using var count = key.OpenSubKey($@"{guidName}\Count");
                if (count is null) continue;

                foreach (var valueName in count.GetValueNames())
                {
                    var raw = count.GetValue(valueName);
                    if (raw is not byte[] bytes) continue;

                    var caminho = DecodeUserAssistName(valueName);
                    if (caminho.Length == 0) continue;

                    var (lastUsed, runCount) = ParseUserAssistData(bytes);

                    sinais.Add(new UsageSignal
                    {
                        ExecutablePath = caminho,
                        LastUsed = lastUsed,
                        RunCount = runCount,
                        Source = "UserAssist",
                    });
                }
            }
        }
        catch
        {
            // UserAssist bloqueado ou inexistente: sem sinal, e tudo bem.
        }

        return sinais;
    }

    /// <summary>
    /// O dado do UserAssist: a partir do decimo sexto byte vem o FILETIME da
    /// ultima execucao e, antes dele, o numero de execucoes.
    /// </summary>
    /// <param name="bytes">O valor bruto do registro.</param>
    public static (DateTime? LastUsed, int RunCount) ParseUserAssistData(byte[] bytes)
    {
        if (bytes is null || bytes.Length < 16) return (null, 0);

        var runCount = BitConverter.ToInt32(bytes, 4);
        var fileTime = BitConverter.ToInt64(bytes, 8);

        DateTime? lastUsed = null;
        if (fileTime > 0)
        {
            try
            {
                var when = DateTime.FromFileTimeUtc(fileTime).ToLocalTime();
                if (when.Year > 2000) lastUsed = when;
            }
            catch
            {
                // FILETIME invalido: sem data, mas o numero de execucoes vale.
            }
        }

        return (lastUsed, runCount);
    }

    /// <summary>
    /// Traduz o nome de um valor do UserAssist para o caminho do executavel.
    ///
    /// O Windows grava o caminho codificado: as letras sao trocadas pelo
    /// ROT13 e os demais caracteres ganham 0x50. O valor vem com o prefixo
    /// da chave ("{GUID}\") e a barra invertida que separa o prefixo do
    /// caminho e a unica que sobra intacta — dentro do caminho, as barras
    /// tambem vao codificadas.
    ///
    /// O resultado passa por uma verificacao: se o texto decodificado nao
    /// parece um caminho (sem unidade e sem barra), ele e descartado. Uma
    /// tabela de traducao errada produz lixo; lixo descartado nao engana o
    /// usuario.
    /// </summary>
    /// <param name="encodedName">O nome do valor, como esta no registro.</param>
    public static string DecodeUserAssistName(string? encodedName)
    {
        if (string.IsNullOrEmpty(encodedName)) return string.Empty;

        // O valor vem com o prefixo do caminho dentro do proprio nome.
        var texto = encodedName;
        var indice = texto.LastIndexOf('\\');
        if (indice >= 0) texto = texto[(indice + 1)..];

        if (texto.Length == 0) return string.Empty;

        var builder = new StringBuilder(texto.Length);
        foreach (var c in texto)
        {
            if (c is >= 'A' and <= 'Z') builder.Append((char)('A' + ((c - 'A' + 13) % 26)));
            else if (c is >= 'a' and <= 'z') builder.Append((char)('a' + ((c - 'a' + 13) % 26)));
            else builder.Append((char)(c - 0x50));
        }

        var caminho = builder.ToString();

        // A primeira letra volta para maiuscula: o Windows sempre grava a
        // letra da unidade assim.
        if (caminho.Length > 1 && char.IsLetter(caminho[0]))
            caminho = char.ToUpperInvariant(caminho[0]) + caminho[1..];

        caminho = caminho.Trim();

        if (!LooksLikePath(caminho)) return string.Empty;

        return caminho;
    }

    /// <summary>
    /// Codifica um caminho no formato em que o UserAssist grava. E o inverso
    /// de <see cref="DecodeUserAssistName"/> e existe para conferer a tabela
    /// de traducao.
    /// </summary>
    /// <param name="path">O caminho do executavel.</param>
    public static string EncodeUserAssistName(string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        var builder = new StringBuilder(path.Length);
        foreach (var c in path)
        {
            if (c is >= 'A' and <= 'Z') builder.Append((char)('A' + ((c - 'A' + 13) % 26)));
            else if (c is >= 'a' and <= 'z') builder.Append((char)('a' + ((c - 'a' + 13) % 26)));
            else builder.Append((char)(c + 0x50));
        }

        return builder.ToString();
    }

    /// <summary>
    /// True quando o texto parece um caminho de executavel: comeca por letra,
    /// tem a unidade e tem barra.
    /// </summary>
    /// <param name="text">O texto conferido.</param>
    public static bool LooksLikePath(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (!char.IsLetter(text[0])) return false;

        return text.Contains('\\') && text.Contains(':');
    }

    /// <summary>
    /// Os sinais lidos do Prefetch: um arquivo por executavel rodado.
    /// </summary>
    private static IEnumerable<UsageSignal> FromPrefetch()
    {
        var sinais = new List<UsageSignal>();

        try
        {
            if (!Directory.Exists(PrefetchFolder)) return sinais;

            foreach (var arquivo in Directory.EnumerateFiles(PrefetchFolder, "*.pf"))
            {
                var nome = Path.GetFileNameWithoutExtension(arquivo);
                var separador = nome.IndexOf('-');
                if (separador <= 0) continue;

                var executavel = nome[..separador];
                if (executavel.Length == 0) continue;

                var quando = File.GetLastWriteTime(arquivo);

                sinais.Add(new UsageSignal
                {
                    ExecutablePath = executavel,
                    LastUsed = quando,
                    RunCount = 0,
                    Source = "Prefetch",
                });
            }
        }
        catch
        {
            // Prefetch bloqueado (sem admin) ou ausente: sem sinal.
        }

        return sinais;
    }

    /// <summary>
    /// Um identificador estavel do executavel, para comparar com o Prefetch
    /// (que so guarda o nome do arquivo).
    ///
    /// O Prefetch grava o nome abreviado com um codigo proprio do Windows
    /// (o hash Rtlx86Hash) e o Tranal nao o recalcula: a comparacao e pelo
    /// nome do arquivo, que e o que o Prefetch deixa legivel.
    /// </summary>
    /// <param name="executablePath">O caminho do executavel.</param>
    public static string FileNameTokenOf(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return string.Empty;

        try
        {
            return Path.GetFileNameWithoutExtension(executablePath);
        }
        catch
        {
            return string.Empty;
        }
    }
}
