using System;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Cli;

/// <summary>
/// Os comandos do manifesto, do rastreio, do removedor teimoso e do
/// Bluetooth.
///
///   tranal /sxs-manifest        - o manifesto que redireciona as DLLs
///   tranal /trace-quality       - o aviso sobre a qualidade do rastreio
///   tranal /stubborn            - o que trava a remocao de um caminho
///   tranal /bluetooth           - os dispositivos Bluetooth
///
/// A REGRA mora no nucleo: cada comando aqui consome o veredito de uma
/// classe do <c>Core</c> e nao repete a regra de negocio.
/// </summary>
public static class ManifestCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/sxs-manifest")) return ExecutarManifesto(flags);
        if (CliArgs.Has(flags, "/trace-quality")) return ExecutarQualidade(flags);
        if (CliArgs.Has(flags, "/stubborn")) return ExecutarTeimoso(args);
        if (CliArgs.Has(flags, "/bluetooth")) return ExecutarBluetooth();
        return null;
    }

    /// <summary>O manifesto que redireciona as DLLs de sistema.</summary>
    private static int ExecutarManifesto(HashSet<string> flags)
    {
        var manifesto = SxSManifest.Build();

        if (CliArgs.Has(flags, "/write"))
        {
            var destino = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "tranal-sxs.manifest");

            var gravado = SxSManifest.Write(destino);
            Console.WriteLine(gravado.Length > 0
                ? $"Manifesto gravado em: {gravado}"
                : "Nao foi possivel gravar o manifesto.");
            return 0;
        }

        Console.WriteLine($"Manifesto valido: {(SxSManifest.IsValidXml(manifesto) ? "sim" : "nao")}");
        Console.WriteLine($"DLLs declaradas: {string.Join(", ", SxSManifest.SystemDlls)}");
        Console.WriteLine();
        Console.WriteLine(manifesto);

        return 0;
    }

    /// <summary>O aviso sobre a qualidade do rastreio.</summary>
    private static int ExecutarQualidade(HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/ask"))
        {
            TraceQuality.MarkAsked();
            Console.WriteLine("O aviso foi marcado como mostrado.");
            return 0;
        }

        if (CliArgs.Has(flags, "/off"))
        {
            TraceQuality.Disable();
            Console.WriteLine("O aviso foi desligado.");
            return 0;
        }

        var estado = TraceQuality.State();
        Console.WriteLine(estado.Detail);
        Console.WriteLine($"Avisos mostrados: {estado.TimesAsked} de {TraceQuality.MaxTimes}");

        return 0;
    }

    /// <summary>O que trava a remocao de um caminho.</summary>
    private static int ExecutarTeimoso(string[] args)
    {
        var caminho = Value(args, "/path") ?? string.Empty;
        if (caminho.Length == 0)
        {
            Console.WriteLine(StubbornRemover.Describe());
            return 0;
        }

        var motivo = StubbornRemover.ReasonOf(caminho);
        var resultado = StubbornRemover.Treat(caminho, motivo);

        Console.WriteLine($"Caminho: {resultado.Path}");
        Console.WriteLine($"Motivo: {resultado.Reason}");
        Console.WriteLine($"Resolvido: {(resultado.Solved ? "sim" : "nao")}");
        Console.WriteLine($"Detalhe: {resultado.Detail}");

        return 0;
    }

    /// <summary>Os dispositivos Bluetooth.</summary>
    private static int ExecutarBluetooth()
    {
        Console.WriteLine(BluetoothLeDevices.Describe());
        Console.WriteLine();

        foreach (var dispositivo in BluetoothLeDevices.List())
        {
            Console.WriteLine($"{dispositivo.Name,-28} {(dispositivo.IsLowEnergy ? "LE" : "classico"),-10} {dispositivo.HardwareId}");
        }

        return 0;
    }

    private static string? Value(IEnumerable<string> args, string prefix)
    {
        foreach (var arg in args)
        {
            if (!arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

            var value = arg[prefix.Length..].Trim();
            if (value.StartsWith("=", StringComparison.Ordinal)) value = value[1..];
            return value.Trim('"');
        }

        return null;
    }
}
