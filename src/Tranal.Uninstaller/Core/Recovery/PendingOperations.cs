using System.IO;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Recovery;

/// <summary>
/// Uma operacao pendente do Windows: a renomeacao (ou a remocao) que o
/// Windows vai aplicar no proximo arranque.
///
/// O Windows guarda essas operacoes em
/// <c>HKLM\SYSTEM\CurrentControlSet\Control\Session Manager</c>, no valor
/// <c>PendingFileRenameOperations</c>: uma lista com pares (origem, destino).
/// Ate hoje o Tranal agendava a remocao e nao mostrava nada: o usuario
/// ficava sem saber o que estava na fila.
/// </summary>
public sealed class PendingOperation
{
    /// <summary>O caminho de origem (o que vai ser renomeado ou apagado).</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// O caminho de destino. Vazio: a operacao apaga a origem em vez de
    /// renomear.
    /// </summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>True quando a operacao apaga o arquivo em vez de renomear.</summary>
    public bool IsDelete => Destination.Length == 0;

    /// <summary>Uma linha que descreve a operacao.</summary>
    public string Describe() =>
        IsDelete ? $"apagar {Source}" : $"renomear {Source} para {Destination}";
}

/// <summary>
/// As operacoes pendentes do Windows, visiveis sem reiniciar.
///
/// O <see cref="DeferredDelete"/> ja agendava a remocao na fila oficial do
/// Windows; o que faltava era a camada de leitura — e a possibilidade de
/// tentar a operacao na hora, quando o arquivo ja nao esta em uso.
/// </summary>
public static class PendingOperations
{
    /// <summary>
    /// Substitui o caminho do valor do registro (so a suite de testes).
    /// </summary>
    internal static string? ValuePathOverride { get; set; }

    /// <summary>
    /// O caminho do valor que guarda a fila.
    /// </summary>
    public static string ValuePath =>
        ValuePathOverride ?? @"SYSTEM\CurrentControlSet\Control\Session Manager\PendingFileRenameOperations";

    /// <summary>
    /// As operacoes pendentes, na ordem em que o Windows vai aplica-las.
    /// </summary>
    public static IReadOnlyList<PendingOperation> List()
    {
        var resultado = new List<PendingOperation>();

        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(ValuePath);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(sub);
            if (key is null) return resultado;

            if (key.GetValueNames().Length == 0) return resultado;

            var bruto = key.GetValue(null) as string[];
            if (bruto is null) return resultado;

            resultado = ParseQueue(bruto);
        }
        catch (Exception)
        {
            return resultado;
        }

        return resultado;
    }

    /// <summary>
    /// Converte a lista bruta do Windows em operacoes.
    ///
    /// A lista e de pares (origem, destino). Uma lista impar e o sinal de que
    /// algo deu errado: o par incompleto e descartado, em vez de virar uma
    /// operacao que apagaria o caminho errado.
    /// </summary>
    /// <param name="bruto">A lista do registro.</param>
    internal static List<PendingOperation> ParseQueue(string[] bruto)
    {
        var resultado = new List<PendingOperation>();
        if (bruto is null) return resultado;

        for (var i = 0; i + 1 < bruto.Length; i += 2)
        {
            resultado.Add(new PendingOperation
            {
                Source = bruto[i] ?? string.Empty,
                Destination = bruto[i + 1] ?? string.Empty,
            });
        }

        return resultado;
    }

    /// <summary>
    /// Tenta aplicar a operacao agora, sem esperar o reinicio.
    ///
    /// Funciona quando o arquivo ja nao esta em uso. Quando o Windows segura
    /// o arquivo, a operacao continua na fila — e a mensagem diz isso, em vez
    /// de fingir que deu certo.
    /// </summary>
    /// <param name="operacao">A operacao.</param>
    public static (bool Ok, string Message) Apply(PendingOperation operacao)
    {
        if (operacao is null) return (false, "operacao invalida");
        if (operacao.Source.Length == 0) return (false, "operacao sem origem");

        try
        {
            if (operacao.IsDelete)
            {
                if (Directory.Exists(operacao.Source)) Directory.Delete(operacao.Source, recursive: true);
                else if (File.Exists(operacao.Source)) File.Delete(operacao.Source);
                else return (false, "o arquivo ja nao existe");

                Remove(operacao);
                return (true, $"removido agora: {operacao.Source}");
            }

            var destino = operacao.Destination;

            // O destino do Windows vem com o prefixo "\??\" para os caminhos
            // absolutos: e a forma como o Windows escreve a fila.
            if (destino.StartsWith(@"\??\", StringComparison.Ordinal)) destino = destino[4..];

            if (File.Exists(operacao.Source))
            {
                var pasta = Path.GetDirectoryName(destino);
                if (!string.IsNullOrEmpty(pasta) && !Directory.Exists(pasta))
                    Directory.CreateDirectory(pasta);

                File.Move(operacao.Source, destino, overwrite: true);
            }
            else if (Directory.Exists(operacao.Source))
            {
                Directory.Move(operacao.Source, destino);
            }
            else
            {
                return (false, "a origem ja nao existe");
            }

            Remove(operacao);
            return (true, $"aplicado agora: {operacao.Source}");
        }
        catch (IOException ex)
        {
            return (false, $"o Windows ainda segura o arquivo: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return (false, $"sem permissao para aplicar agora: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Tira a operacao da fila do Windows (sem aplica-la).
    /// </summary>
    /// <param name="operacao">A operacao.</param>
    public static bool Remove(PendingOperation operacao)
    {
        if (operacao is null) return false;

        try
        {
            var (hive, sub) = SafetyRules.SplitRegistryPath(ValuePath);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(sub, writable: true);
            if (key is null) return false;

            var bruto = key.GetValue(null) as string[];
            if (bruto is null) return false;

            var novo = new List<string>();
            for (var i = 0; i + 1 < bruto.Length; i += 2)
            {
                if (string.Equals(bruto[i], operacao.Source, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(bruto[i + 1], operacao.Destination, StringComparison.OrdinalIgnoreCase))
                    continue;

                novo.Add(bruto[i]);
                novo.Add(bruto[i + 1]);
            }

            key.SetValue(null, novo.ToArray(), RegistryValueKind.MultiString);
            return true;
        }
        catch (Exception) { return false; }
    }

    /// <summary>
    /// True quando ha operacoes pendentes.
    /// </summary>
    public static bool HasPending => List().Count > 0;
}
