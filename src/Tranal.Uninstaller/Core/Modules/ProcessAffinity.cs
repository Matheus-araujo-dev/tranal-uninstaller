using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// A afinidade de processador de um processo: quais nucleos o Windows deixa
/// aquele programa usar.
/// </summary>
public sealed class ProcessAffinityInfo
{
    /// <summary>O identificador do processo.</summary>
    public int ProcessId { get; init; }

    /// <summary>O nome do executavel.</summary>
    public string ProcessName { get; init; } = string.Empty;

    /// <summary>A mascara de afinidade (um bit por nucleo logico).</summary>
    public long Mask { get; init; }

    /// <summary>Quantos nucleos logicos o computador tem.</summary>
    public int ProcessorCount { get; init; }

    /// <summary>
    /// A lista de nucleos, em texto ("0-3, 8").
    ///
    /// E o jeito legivel: a mascara crua ("255") nao diz nada para quem
    /// olha o painel.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// True quando o processo esta preso a menos nucleos do que a maquina
    /// tem — o sinal de que alguem mexeu na afinidade.
    /// </summary>
    public bool IsRestricted { get; init; }
}

/// <summary>
/// A afinidade de processador dos processos que rodam agora.
///
/// O HiBit mede a "afinidade de processador do programa": quantos nucleos o
/// Windows deixa o programa usar. E a informacao que explica um programa
/// que "sobe" a maquina: ele esta preso a um nucleo, ou esta usando todos.
///
/// A REGRA mora aqui: a leitura da mascara e a expansao em texto sao feitas
/// uma vez, e quem mostra consome o resultado.
/// </summary>
public static class ProcessAffinity
{
    /// <summary>
    /// A afinidade de cada processo da lista.
    ///
    /// Processo que o Windows nao deixa ler (permissao) e Simply ignorado:
    /// lista de processos nunca pode quebrar por causa de um deles.
    /// </summary>
    /// <param name="processes">Os processos a ler. Vazio = todos os que rodam.</param>
    public static IReadOnlyList<ProcessAffinityInfo> List(IEnumerable<RunningProcess>? running = null)
    {
        var lista = running?.ToList() ?? ProcessInspector.List();
        var resultado = new List<ProcessAffinityInfo>();

        foreach (var processo in lista)
        {
            var info = Of(processo.Id, processo.Name);
            if (info is not null) resultado.Add(info);
        }

        return resultado;
    }

    /// <summary>
    /// A afinidade de um processo.
    /// </summary>
    /// <param name="processId">O identificador do processo.</param>
    /// <param name="nome">O nome do processo (quando ja se sabe).</param>
    public static ProcessAffinityInfo? Of(int processId, string? nome = null)
    {
        try
        {
            using var processo = Process.GetProcessById(processId);

            var mascara = processo.ProcessorAffinity.ToInt64();
            var total = Environment.ProcessorCount;

            return new ProcessAffinityInfo
            {
                ProcessId = processId,
                ProcessName = nome ?? processo.ProcessName,
                Mask = mascara,
                ProcessorCount = total,
                Text = Expandir(mascara),
                IsRestricted = Contar(mascara) < total,
            };
        }
        catch (Exception)
        {
            // Processo encerrado no meio da leitura, ou sem permissao: nao
            // ha o que informar sobre ele.
            return null;
        }
    }

    /// <summary>
    /// A mascara como lista de nucleos, em texto ("0-3, 8").
    /// </summary>
    /// <param name="mascara">A mascara de afinidade.</param>
    public static string Expandir(long mascara)
    {
        if (mascara <= 0) return "(nenhum)";

        var nucleos = new List<int>();
        for (var i = 0; i < 64; i++)
        {
            if ((mascara & (1L << i)) != 0) nucleos.Add(i);
        }

        if (nucleos.Count == 0) return "(nenhum)";

        var partes = new List<string>();
        var inicio = nucleos[0];
        var anterior = nucleos[0];

        for (var i = 1; i <= nucleos.Count; i++)
        {
            var atual = i < nucleos.Count ? nucleos[i] : int.MinValue;

            if (atual == anterior + 1)
            {
                anterior = atual;
                continue;
            }

            partes.Add(inicio == anterior ? inicio.ToString() : $"{inicio}-{anterior}");
            if (atual != int.MinValue)
            {
                inicio = atual;
                anterior = atual;
            }
        }

        return string.Join(", ", partes);
    }

    /// <summary>
    /// Quantos nucleos a mascara libera.
    /// </summary>
    /// <param name="mascara">A mascara de afinidade.</param>
    public static int Contar(long mascara)
    {
        var total = 0;
        for (var i = 0; i < 64; i++)
        {
            if ((mascara & (1L << i)) != 0) total++;
        }

        return total;
    }
}
