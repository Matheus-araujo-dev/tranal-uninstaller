using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// Uma informacao do sistema mostrada no painel.
/// </summary>
public sealed class SystemInfoItem
{
    /// <summary>O rotulo ("Memoria RAM").</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>O valor ("16,0 GB").</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// O valor cru, para a interface poder formatar como quiser.
    /// </summary>
    public string RawValue { get; init; } = string.Empty;
}

/// <summary>
/// O resumo do sistema: CPU, memoria, disco, sistema operacional e tempo de
/// atividade.
///
/// O HiBit mede um "Painel de informacoes do sistema no banner (CPU, GPU, RAM
/// disponivel, SO, tempo de atividade)". E o que o usuario olha antes de
/// decidir desinstalar: "a maquina esta com memoria acabando? o programa
/// parado esta segurando quanto?"
///
/// A REGRA do "quanto falta" mora aqui: a memoria disponivel e a total menos
/// a ocupada, e a coleta usa a mesma fonte de dados do Windows (WMI).
/// </summary>
public static class SystemInfoPanel
{
    /// <summary>
    /// O painel do sistema.
    /// </summary>
    /// <param name="incluirGpu">Inclui a GPU na coleta (a consulta e mais lenta).</param>
    public static IReadOnlyList<SystemInfoItem> Collect(bool incluirGpu = true)
    {
        var itens = new List<SystemInfoItem>();

        var so = OperatingSystemInfo();
        if (so is not null) itens.Add(so);

        var cpu = ProcessorInfo();
        if (cpu is not null) itens.Add(cpu);

        var ram = MemoryInfo();
        if (ram is not null) itens.Add(ram);

        var disco = DiskInfo();
        if (disco is not null) itens.Add(disco);

        if (incluirGpu)
        {
            var gpu = GraphicsInfo();
            if (gpu is not null) itens.Add(gpu);
        }

        var atividade = UptimeInfo();
        if (atividade is not null) itens.Add(atividade);

        return itens;
    }

    /// <summary>
    /// O sistema operacional, com a edicao.
    /// </summary>
    public static SystemInfoItem? OperatingSystemInfo()
    {
        try
        {
            var nome = Environment.OSVersion.VersionString;
            var edicao = ReadRegistryString(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "EditionID");
            if (string.IsNullOrWhiteSpace(edicao)) return null;

            var valor = $"{nome} ({edicao})";
            return new SystemInfoItem { Label = "Sistema operacional", Value = valor, RawValue = valor };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// O processador e o total de nucleos.
    /// </summary>
    public static SystemInfoItem? ProcessorInfo()
    {
        try
        {
            var nome = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")?.Trim();
            if (string.IsNullOrWhiteSpace(nome)) return null;

            var nucleos = Environment.ProcessorCount;
            var valor = nucleos > 0 ? $"{nome} ({nucleos} nucleos)" : nome;
            return new SystemInfoItem { Label = "Processador", Value = valor, RawValue = nome };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// A memoria: total, ocupada e disponivel.
    /// </summary>
    public static SystemInfoItem? MemoryInfo()
    {
        try
        {
            var info = ReadWmi("Win32_OperatingSystem", "TotalVisibleMemorySize", "FreePhysicalMemory");
            if (info is null) return null;

            var totalBytes = Convert.ToInt64(info.Value.Item1 ?? 0L) * 1024L;
            var livreBytes = Convert.ToInt64(info.Value.Item2 ?? 0L) * 1024L;
            if (totalBytes <= 0) return null;

            var usadaBytes = totalBytes - livreBytes;
            var valor = $"{ByteFormatter.Format(usadaBytes)} / {ByteFormatter.Format(totalBytes)} em uso "
                      + $"({ByteFormatter.Format(livreBytes)} disponiveis)";

            return new SystemInfoItem
            {
                Label = "Memoria",
                Value = valor,
                RawValue = totalBytes.ToString(CultureInfo.InvariantCulture),
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// O disco do sistema: quanto ha e quanto resta.
    /// </summary>
    public static SystemInfoItem? DiskInfo()
    {
        try
        {
            var raiz = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (string.IsNullOrWhiteSpace(raiz)) return null;

            var unidade = Path.GetPathRoot(raiz);
            if (string.IsNullOrWhiteSpace(unidade)) return null;

            var drive = new DriveInfo(unidade);
            if (!drive.IsReady) return null;

            var valor = $"{ByteFormatter.Format(drive.AvailableFreeSpace)} disponiveis de {ByteFormatter.Format(drive.TotalSize)}";
            return new SystemInfoItem
            {
                Label = $"Disco {unidade}",
                Value = valor,
                RawValue = drive.AvailableFreeSpace.ToString(CultureInfo.InvariantCulture),
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// A placa de video.
    /// </summary>
    public static SystemInfoItem? GraphicsInfo()
    {
        try
        {
            var info = ReadWmi("Win32_VideoController", "Name");
            if (info is null) return null;

            var nome = info?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(nome)) return null;

            return new SystemInfoItem { Label = "Video", Value = nome, RawValue = nome };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Ha quanto tempo a maquina esta ligada.
    /// </summary>
    public static SystemInfoItem? UptimeInfo()
    {
        try
        {
            using var processo = Process.GetCurrentProcess();
            var desde = DateTime.Now - processo.StartTime;
            if (desde < TimeSpan.Zero) return null;

            var valor = FormatarDuracao(desde);
            return new SystemInfoItem
            {
                Label = "Ligado ha",
                Value = valor,
                RawValue = desde.ToString("c", CultureInfo.InvariantCulture),
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Formata uma duracao em texto ("3 dias e 4 horas").
    /// </summary>
    /// <param name="duracao">A duracao.</param>
    public static string FormatarDuracao(TimeSpan duracao)
    {
        if (duracao < TimeSpan.Zero) return "-";

        var dias = (int)Math.Floor(duracao.TotalDays);
        var horas = duracao.Hours;
        var minutos = duracao.Minutes;

        if (dias > 0) return $"{dias} dia(s) e {horas} hora(s)";
        if (horas > 0) return $"{horas} hora(s) e {minutos} minuto(s)";
        return $"{minutos} minuto(s)";
    }

    /// <summary>
    /// Le duas propriedades do WMI, em uma consulta so.
    /// </summary>
    private static (object? Item1, object? Item2)? ReadWmi(string classe, string propriedade1, string propriedade2)
    {
        try
        {
            using var busca = new ManagementObjectSearcher($"SELECT {propriedade1}, {propriedade2} FROM {classe}");
            using var resultados = busca.Get();
            foreach (ManagementObject item in resultados)
            {
                return (item[propriedade1], item[propriedade2]);
            }
        }
        catch
        {
            // O WMI pode estar bloqueado por seguranca: o painel segue sem a
            // informacao, em vez de derrubar a tela.
        }

        return null;
    }

    /// <summary>
    /// Le uma propriedade do WMI.
    /// </summary>
    private static object? ReadWmi(string classe, string propriedade)
    {
        try
        {
            using var busca = new ManagementObjectSearcher($"SELECT {propriedade} FROM {classe}");
            using var resultados = busca.Get();
            foreach (ManagementObject item in resultados)
            {
                return item[propriedade];
            }
        }
        catch
        {
            // O WMI pode estar bloqueado por seguranca.
        }

        return null;
    }

    /// <summary>
    /// Le um valor de string do registro.
    ///
    /// O caminho pode vir com o prefixo do hive ("HKEY_LOCAL_MACHINE\...") ou
    /// sem ele: quem traduz e a regra unica do projeto,
    /// <see cref="Management.RegistryPath"/>.
    /// </summary>
    private static string? ReadRegistryString(string caminho, string nome)
    {
        try
        {
            var parsed = Management.RegistryPath.Parse(caminho);
            if (parsed is null) return null;

            using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(parsed.Value.Hive, Microsoft.Win32.RegistryView.Default);
            using var chave = baseKey.OpenSubKey(parsed.Value.RelativePath);
            return chave?.GetValue(nome)?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
