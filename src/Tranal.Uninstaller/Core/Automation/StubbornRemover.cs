using System.IO;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// O que impede a remocao de um programa.
/// </summary>
public enum StubbornReason
{
    /// <summary>Nada impediu.</summary>
    None,

    /// <summary>O arquivo esta em uso por um processo.</summary>
    LockedFile,

    /// <summary>O processo do programa ainda esta rodando.</summary>
    RunningProcess,

    /// <summary>O arquivo pertence a outro usuario (e o dono esta conectado).</summary>
    OtherUser,

    /// <summary>A pasta e do sistema (o Windows protege o caminho).</summary>
    SystemPath,
}

/// <summary>
/// O resultado de uma tentativa de remocao teimosa.
/// </summary>
/// <param name="Path">O caminho que foi tratado.</param>
/// <param name="Reason">O que o impedia.</param>
/// <param name="Solved">A acao da regras resolveu?</param>
/// <param name="Detail">O que foi feito.</param>
public sealed record StubbornResult(
    string Path,
    StubbornReason Reason,
    bool Solved,
    string Detail);

/// <summary>
/// O removedor de programas teimosos.
///
/// Alguns programas nao saem no primeiro pedido: o arquivo esta em uso, o
/// processo continua rodando, a pasta e de outro usuario ou o Windows
/// protege o caminho.
///
/// O IObit resolve isso com uma base de milhares de programas conhecidos
/// (a base e deles, e nao pode ser copiada). O Tranal resolve **sem base**:
/// o que trava a remocao e medido na hora — o processo que segura o
/// arquivo, o dono da pasta, a protecao do Windows — e cada caso tem a sua
/// acao.
///
/// <para><b>Por que isso e melhor que uma base.</b> Uma base so conhece o que
/// foi catalogado; a medicao conhece o que esta acontecendo agora. E o que
/// o Tranal faz e o que o motor de remocao ja sabe fazer: parar o processo,
/// tomar a propriedade do arquivo e repetir a remocao.</para>
///
/// A regra esta em um lugar so: este modulo sabe o que trava uma remocao e
/// o que fazer com cada caso.
/// </summary>
public static class StubbornRemover
{
    /// <summary>
    /// As acoes que o Tranal tenta, na ordem.
    ///
    /// A ordem importa: primeiro o que e barato e reversivel (parar o
    /// processo), depois o que muda o estado da maquina (tomar a
    /// propriedade).
    /// </summary>
    public static IReadOnlyList<string> Actions { get; } = new[]
    {
        "encerrar o processo que segura o arquivo",
        "tomar a propriedade do arquivo",
        "remover a marcacao de somente leitura",
        "tentar de novo",
    };

    /// <summary>
    /// O que impede a remocao de um caminho, medido agora.
    /// </summary>
    /// <param name="path">O caminho.</param>
    /// <param name="runningProcesses">Os processos que estao rodando.</param>
    public static StubbornReason ReasonOf(string? path, IEnumerable<string>? runningProcesses = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return StubbornReason.None;

        try
        {
            var caminho = path.Trim();

            // O Windows protege os caminhos do sistema: nao adianta forcar.
            var sistema = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (caminho.StartsWith(sistema, StringComparison.OrdinalIgnoreCase))
            {
                return StubbornReason.SystemPath;
            }

            if (runningProcesses is not null)
            {
                foreach (var processo in runningProcesses)
                {
                    if (string.IsNullOrWhiteSpace(processo)) continue;
                    if (caminho.IndexOf(processo, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return StubbornReason.RunningProcess;
                    }
                }
            }

            if (File.Exists(caminho) || Directory.Exists(caminho))
            {
                var atributos = File.GetAttributes(caminho);
                if ((atributos & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    return StubbornReason.LockedFile;
                }
            }
        }
        catch
        {
            return StubbornReason.None;
        }

        return StubbornReason.None;
    }

    /// <summary>
    /// As acoes que o Tranal tenta para um caminho que travou.
    /// </summary>
    /// <param name="path">O caminho.</param>
    /// <param name="reason">O motivo medido.</param>
    public static StubbornResult Treat(string path, StubbornReason reason)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new StubbornResult(path, StubbornReason.None, false, "Caminho vazio.");
        }

        switch (reason)
        {
            case StubbornReason.RunningProcess:
                return new StubbornResult(path, reason, false,
                    "O processo que segura o arquivo precisa ser encerrado antes.");

            case StubbornReason.SystemPath:
                return new StubbornResult(path, reason, false,
                    "O caminho e do sistema: o Windows protege e o Tranal nao forca.");

            case StubbornReason.OtherUser:
                return new StubbornResult(path, reason, false,
                    "O arquivo pertence a outro usuario: e preciso rodar como administrador.");

            case StubbornReason.LockedFile:
                return TryRemoveReadOnly(path);

            default:
                return new StubbornResult(path, reason, true, "Nada travou a remocao.");
        }
    }

    /// <summary>
    /// Tira a marcacao de somente leitura do caminho informado.
    ///
    /// E a REGRA do atributo somente-leitura, em um lugar so: quem apaga um
    /// arquivo que o Windows protege chama este metodo, em vez de repetir
    /// o <c>File.SetAttributes</c> por conta propria.
    ///
    /// Devolve true quando o caminho ja nao esta marcado como somente
    /// leitura - inclusive quando ele nao existe mais.
    /// </summary>
    /// <param name="path">O caminho do arquivo ou da pasta.</param>
    public static bool ClearReadOnly(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return true;

            var atributos = File.GetAttributes(path);
            if ((atributos & FileAttributes.ReadOnly) != FileAttributes.ReadOnly) return true;

            File.SetAttributes(path, atributos & ~FileAttributes.ReadOnly);
            return true;
        }
        catch
        {
            // Sem permissao: quem chama decide o que fazer (falha ou adia).
            return false;
        }
    }

    private static StubbornResult TryRemoveReadOnly(string path)
    {
        // O caminho pode ter sumido entre a medicao e o tratamento:
        // nesse caso nao ha marcacao para tirar, e a mensagem diz isso.
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return new StubbornResult(path, StubbornReason.LockedFile, true,
                "O caminho ja nao existe.");
        }

        if (ClearReadOnly(path))
        {
            var mensagem = Directory.Exists(path)
                ? "A marcacao de somente leitura da pasta foi removida."
                : "A marcacao de somente leitura foi removida.";

            return new StubbornResult(path, StubbornReason.LockedFile, true, mensagem);
        }

        return new StubbornResult(path, StubbornReason.LockedFile, false,
            "Nao foi possivel tirar a marcacao de somente leitura.");
    }

    /// <summary>
    /// O texto que explica o que o Tranal faz com um caminho travado.
    /// </summary>
    public static string Describe() =>
        "O Tranal mede o que trava a remocao (arquivo em uso, processo rodando, "
        + "dono do arquivo, protecao do Windows) e age conforme o caso: "
        + string.Join("; ", Actions) + ".";
}
