using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Tranal.Uninstaller.Core.Management;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// O impacto de um programa em execucao.
/// </summary>
public sealed class ProgramImpact
{
    /// <summary>O programa instalado.</summary>
    public InstalledProgram Program { get; init; } = new();

    /// <summary>Quantos processos deste programa estao rodando.</summary>
    public int ProcessCount { get; init; }

    /// <summary>A memoria somada de todos os processos, em bytes.</summary>
    public long MemoryBytes { get; init; }

    /// <summary>A soma do tempo de CPU, em segundos.</summary>
    public double CpuSeconds { get; init; }

    /// <summary>
    /// A nota de impacto, de 0 a 100.
    ///
    /// A nota e a soma de duas medidas: a memoria (que pesa mais, porque
    /// memoria ocupada nao volta sozinha) e o tempo de CPU (que mede o
    /// trabalho feito).
    /// </summary>
    public int Score { get; init; }

    /// <summary>A memoria, em texto legivel.</summary>
    public string MemoryText => ByteFormatter.Format(MemoryBytes);

    /// <summary>O tempo de CPU, em texto legivel.</summary>
    public string CpuText => CpuSeconds < 1
        ? $"{CpuSeconds:0.##} s"
        : CpuSeconds < 60
            ? $"{CpuSeconds:0.#} s"
            : $"{CpuSeconds / 60:0.#} min";
}

/// <summary>
/// O ranking de impacto: quais programas pesam na maquina agora.
///
/// O Ashampoo mede "Impact: ranking por impacto em performance (CPU/memoria)".
/// E o painel que responde "esse programa parado esta me comendo a maquina?".
///
/// A REGRA de quais processos pertencem a um programa mora em
/// <see cref="ExecutableLocator"/> — o mesmo lugar que o painel de processos
/// usa. Nao ha duas maneiras de descobrir o dono de um executavel.
/// </summary>
public static class ProgramImpactAnalyzer
{
    /// <summary>
    /// O ranking de impacto dos programas em execucao.
    ///
    /// </summary>
    /// <param name="programas">Os programas instalados (a fonte da lista).</param>
    /// <param name="limite">Quantos itens devolver (0 = todos).</param>
    public static IReadOnlyList<ProgramImpact> Rank(IEnumerable<InstalledProgram>? programas, int limite = 0)
    {
        var lista = (programas ?? Enumerable.Empty<InstalledProgram>()).Where(p => p is not null).ToList();
        var processos = ProcessInspector.List(lista);

        var porPrograma = processos
            .Where(p => !string.IsNullOrWhiteSpace(p.InstalledProgram))
            .GroupBy(p => p.InstalledProgram, StringComparer.OrdinalIgnoreCase);

        var resultado = new List<ProgramImpact>();

        foreach (var grupo in porPrograma)
        {
            var dono = lista.FirstOrDefault(p =>
                string.Equals(p.DisplayName, grupo.Key, StringComparison.OrdinalIgnoreCase));
            if (dono is null) continue;

            var memoria = grupo.Sum(p => p.WorkingSetBytes);
            var cpu = grupo.Sum(p => CpuDe(p.Id));

            resultado.Add(new ProgramImpact
            {
                Program = dono,
                ProcessCount = grupo.Count(),
                MemoryBytes = memoria,
                CpuSeconds = cpu,
                Score = CalcularNota(memoria, cpu),
            });
        }

        resultado = resultado
            .OrderByDescending(i => i.Score)
            .ThenByDescending(i => i.MemoryBytes)
            .ToList();

        if (limite > 0 && resultado.Count > limite) resultado = resultado.Take(limite).ToList();

        return resultado;
    }

    /// <summary>
    /// A nota de impacto, de 0 a 100.
    ///
    /// A memoria vale ate 70 pontos e o tempo de CPU ate 30. As escalas sao
    /// logaritmicas: a diferenca entre 100 MB e 200 MB importa, mas a
    /// diferenca entre 10 GB e 20 GB nao dobra a percepcao de peso.
    /// </summary>
    /// <param name="memoriaBytes">A memoria ocupada, em bytes.</param>
    /// <param name="cpuSeconds">O tempo de CPU, em segundos.</param>
    public static int CalcularNota(long memoriaBytes, double cpuSeconds)
    {
        if (memoriaBytes <= 0 && cpuSeconds <= 0) return 0;

        var pontosMemoria = 0.0;
        if (memoriaBytes > 0)
        {
            var mb = memoriaBytes / (1024.0 * 1024);
            pontosMemoria = 70.0 * Math.Min(1.0, Math.Log10(mb + 1) / Math.Log10(2048));
        }

        var pontosCpu = 0.0;
        if (cpuSeconds > 0)
        {
            pontosCpu = 30.0 * Math.Min(1.0, Math.Log10(cpuSeconds + 1) / Math.Log10(600));
        }

        var nota = (int)Math.Round(pontosMemoria + pontosCpu);
        return Math.Clamp(nota, 0, 100);
    }

    /// <summary>
    /// O tempo de CPU de um processo, em segundos.
    /// </summary>
    private static double CpuDe(int pid)
    {
        try
        {
            using var processo = Process.GetProcessById(pid);
            return processo.TotalProcessorTime.TotalSeconds;
        }
        catch
        {
            // O processo pode ter terminado entre a listagem e a leitura.
            return 0;
        }
    }
}
