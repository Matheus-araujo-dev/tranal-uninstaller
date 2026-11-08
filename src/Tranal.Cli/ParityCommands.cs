using System;
using System.Collections.Generic;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Reporting;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Cli;

/// <summary>
/// Os comandos de paridade que nao tinham entrada de linha de comando.
///
/// Cada feature nova do nucleo ganha um comando proprio: assim a feature
/// existe para o usuario (e nao so para o teste), e o comando fica
/// documentado na ajuda.
/// </summary>
public static class ParityCommands
{
    /// <summary>
    /// Tenta tratar um comando. Devolve null quando nenhum foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (flags.Contains("/oculus")) return RunOculus();
        if (flags.Contains("/updates")) return RunUpdates();
        if (flags.Contains("/userapps")) return RunUserApps();
        if (flags.Contains("/ads")) return RunAds(args);
        if (flags.Contains("/storeapp-script")) return RunStoreAppScript(args);
        if (flags.Contains("/privacy")) return RunPrivacy(args);
        if (flags.Contains("/tools-custom")) return RunCustomTools(args);
        if (flags.Contains("/external")) return RunExternal();
        if (flags.Contains("/usage")) return RunUsage();
        if (flags.Contains("/colorblind")) return RunColorBlind();
        return null;
    }


    private static IReadOnlyList<InstalledProgram> Scan() =>
        ProgramScanner.Scan(new ProgramScanner.ScanOptions { RequireUninstallString = false });

    /// <summary>Lista os aplicativos Oculus conhecidos e os instalados que batem.</summary>
    private static int RunOculus()
    {
        Console.WriteLine("Aplicativos Oculus/VR conhecidos:");
        foreach (var app in OculusCatalog.All)
        {
            Console.WriteLine($"  {app.Name}  ({app.PackageId})");
            if (app.Publisher.Length > 0)
                Console.WriteLine($"      {app.Publisher}");
        }

        Console.WriteLine();
        Console.WriteLine("Instalados que batem com o catalogo:");

        var encontrados = Scan().Where(p => OculusCatalog.IsOculusApp(p.DisplayName)).ToList();
        foreach (var program in encontrados)
        {
            var app = OculusCatalog.Match(program.DisplayName);
            Console.WriteLine($"  {program.DisplayName}  (catalogo: {app?.Name ?? "?"})");
        }

        if (encontrados.Count == 0)
            Console.WriteLine("  (nenhum)");

        return 0;
    }

    /// <summary>Lista os programas que sao atualizacao de outro aplicativo.</summary>
    private static int RunUpdates()
    {
        var programas = Scan();
        var atualizacoes = programas
            .Select(p => (Programa: p, Info: UpdateMarker.Detect(p)))
            .Where(x => x.Info is not null && x.Info.IsUpdate)
            .ToList();

        Console.WriteLine($"Atualizacoes de outro aplicativo: {atualizacoes.Count}");
        Console.WriteLine();

        foreach (var (programa, info) in atualizacoes)
        {
            Console.WriteLine($"{programa.DisplayName}");
            Console.WriteLine($"    {info!.Reason}: {info.BaseAppName}");
            if (info.Detail.Length > 0)
                Console.WriteLine($"    {info.Detail}");
        }

        return 0;
    }

    /// <summary>Lista os aplicativos instalados em pastas do usuario.</summary>
    private static int RunUserApps()
    {
        var apps = UserInstallFolders.Scan();

        Console.WriteLine($"Aplicativos em pastas do usuario: {apps.Count}");
        Console.WriteLine();

        foreach (var app in apps)
        {
            Console.WriteLine($"{app.Name}");
            Console.WriteLine($"    {app.Path}");
        }

        return 0;
    }

    /// <summary>Lista os fluxos alternativos (NTFS) de uma pasta.</summary>
    private static int RunAds(string[] args)
    {
        var pasta = CliArgs.ValueAfter(args, "/ads");

        if (string.IsNullOrWhiteSpace(pasta))
        {
            Console.Error.WriteLine("Uso: tranal /ads \"C:\\Pasta\" [/recursive]");
            return 1;
        }

        var recursive = args.Any(a => a.Equals("/recursive", StringComparison.OrdinalIgnoreCase));
        var streams = AlternateDataStreamScanner.ScanFolder(pasta!, recursive);

        Console.WriteLine($"Fluxos alternativos em {pasta}: {streams.Count}");
        Console.WriteLine();

        foreach (var stream in streams)
        {
            Console.WriteLine($"{stream.Name}  ({stream.Type})  {stream.Size} bytes");
            Console.WriteLine($"    {stream.StreamPath}");
        }

        return 0;
    }

    /// <summary>Gera o script PowerShell que remove os Store Apps escolhidos.</summary>
    private static int RunStoreAppScript(string[] args)
    {
        var nomes = CliArgs.ValueAfter(args, "/storeapp-script");
        var arquivo = CliArgs.ValueAfter(args, "/arquivo");

        if (string.IsNullOrWhiteSpace(nomes))
        {
            Console.Error.WriteLine("Uso: tranal /storeapp-script \"App1;App2\" [/arquivo caminho.ps1]");
            return 1;
        }

        var apps = WindowsApps.List()
            .Where(a => nomes.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Any(n => a.Name.Contains(n.Trim(), StringComparison.CurrentCultureIgnoreCase)))
            .ToList();

        if (apps.Count == 0)
        {
            Console.Error.WriteLine("Nenhum aplicativo do Windows encontrado com esse nome.");
            return 1;
        }

        var script = StoreAppScriptWriter.Build(apps);

        if (string.IsNullOrWhiteSpace(arquivo))
        {
            Console.WriteLine(script);
            return 0;
        }

        var gravado = StoreAppScriptWriter.Write(arquivo, apps);
        if (string.IsNullOrWhiteSpace(gravado))
        {
            Console.Error.WriteLine("Nao foi possivel gravar o script.");
            return 1;
        }

        Console.WriteLine($"Script gravado em: {arquivo}");
        return 0;
    }

    /// <summary>Lista (ou limpa) os rastros de privacidade.</summary>
    private static int RunPrivacy(string[] args)
    {
        var grupo = CliArgs.ValueAfter(args, "/grupo");
        var limpar = args.Any(a => a.Equals("/limpar", StringComparison.OrdinalIgnoreCase));

        PrivacyTraceGroup? selecionado = null;
        if (!string.IsNullOrWhiteSpace(grupo))
        {
            selecionado = Enum.GetValues<PrivacyTraceGroup>().FirstOrDefault(g =>
                g.ToString().Equals(grupo, StringComparison.OrdinalIgnoreCase));

            if (selecionado is null)
            {
                Console.Error.WriteLine("Grupos disponiveis: " +
                    string.Join(", ", Enum.GetValues<PrivacyTraceGroup>().Select(g => g.ToString())));
                return 1;
            }
        }

        var itens = PrivacyTraceCleaner.Scan(selecionado);

        Console.WriteLine($"Rastros de privacidade encontrados: {itens.Count}");
        Console.WriteLine();

        foreach (var item in itens)
        {
            Console.WriteLine($"{item.Group}  {item.Path}");
            if (item.Detail.Length > 0)
                Console.WriteLine($"    {item.Detail}");
        }

        if (!limpar) return 0;

        var relatorio = PrivacyTraceCleaner.Clean(itens);
        Console.WriteLine();
        Console.WriteLine($"Removidos: {relatorio.RemovedCount}  falhas: {relatorio.FailedCount}");
        return relatorio.FailedCount == 0 ? 0 : 1;
    }

    /// <summary>Lista (ou gerencia) as ferramentas personalizadas.</summary>
    private static int RunCustomTools(string[] args)
    {
        var nome = CliArgs.ValueAfter(args, "/nome");
        var comando = CliArgs.ValueAfter(args, "/comando");
        var excluir = CliArgs.ValueAfter(args, "/excluir");
        var renomear = CliArgs.ValueAfter(args, "/renomear");

        if (!string.IsNullOrWhiteSpace(excluir))
        {
            var status = CustomToolRegistry.Remove(excluir!);
            Console.WriteLine(status == CustomToolStatus.Ok
                ? $"Ferramenta removida: {excluir}"
                : $"Nao foi possivel remover: {excluir}");
            return status == CustomToolStatus.Ok ? 0 : 1;
        }

        if (!string.IsNullOrWhiteSpace(renomear) && !string.IsNullOrWhiteSpace(nome))
        {
            var status = CustomToolRegistry.Rename(renomear!, nome!);
            Console.WriteLine(status == CustomToolStatus.Ok
                ? $"Ferramenta renomeada: {renomear} -> {nome}"
                : $"Nao foi possivel renomear: {renomear}");
            return status == CustomToolStatus.Ok ? 0 : 1;
        }

        if (!string.IsNullOrWhiteSpace(nome) && !string.IsNullOrWhiteSpace(comando))
        {
            var status = CustomToolRegistry.Add(new CustomTool
            {
                Name = nome!,
                Command = comando!,
            });

            Console.WriteLine(status == CustomToolStatus.Ok
                ? $"Ferramenta adicionada: {nome}"
                : $"Nao foi possivel adicionar: {nome}");

            return status == CustomToolStatus.Ok ? 0 : 1;
        }

        var ferramentas = CustomToolRegistry.List();
        Console.WriteLine($"Ferramentas personalizadas: {ferramentas.Count}");
        Console.WriteLine();

        foreach (var tool in ferramentas)
        {
            Console.WriteLine($"{tool.Name}");
            Console.WriteLine($"    {tool.Command} {tool.Arguments}");
        }

        return 0;
    }

    /// <summary>Lista os programas que foram desinstalados fora do Tranal.</summary>
    private static int RunExternal()
    {
        var programas = Scan();
        var eventos = ExternalUninstallWatcher.Inspect(programas);

        Console.WriteLine($"Desinstalados fora do Tranal: {eventos.Count}");
        Console.WriteLine();

        foreach (var ev in eventos)
        {
            Console.WriteLine($"{ev.Program.DisplayName}");
            Console.WriteLine($"    {ev.Detail}");
        }

        return 0;
    }

    /// <summary>Lista os sinais de uso dos executaveis.</summary>
    private static int RunUsage()
    {
        var sinais = UsageSignals.Collect();

        Console.WriteLine($"Sinais de uso encontrados: {sinais.Count}");
        Console.WriteLine();

        foreach (var sinal in sinais.OrderByDescending(s => s.LastUsed ?? DateTime.MinValue))
        {
            var quando = sinal.LastUsed?.ToString("yyyy-MM-dd HH:mm") ?? "(sem data)";
            Console.WriteLine($"{quando}  {sinal.RunCount}x  {sinal.ExecutablePath}");
        }

        return 0;
    }

    /// <summary>Mostra a paleta adaptada para daltônicos.</summary>
    private static int RunColorBlind()
    {
        Console.WriteLine("Paleta para daltônicos (realce por luminancia, nao so por cor):");
        Console.WriteLine();

        foreach (var mode in Enum.GetValues<ColorVisionMode>())
        {
            Console.WriteLine(mode);

            foreach (var key in HighlightPalette.All)
            {
                Console.WriteLine($"    {key,-10} {ColorBlindPalette.HighlightHexOf(key, mode)}");
            }
        }

        return 0;
    }
}
