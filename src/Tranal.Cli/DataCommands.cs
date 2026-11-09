using System;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Updates;

namespace Tranal.Cli;

/// <summary>
/// Os comandos do banco local, dos logs antigos, das ligacoes e do
/// rastreio.
///
///   tranal /local-db            - o banco local embutido
///   tranal /local-db /recent    - as ultimas remocoes registradas
///   tranal /import-log          - importa um log antigo do UnInstaller 4
///   tranal /attachments         - as ligacoes entre programa e log
///   tranal /trace-service       - a versao do servico de rastreio
///   tranal /votes               - os votos do usuario
///   tranal /uninstallers        - os desinstaladores rodando agora
///   tranal /accessibility       - acessibilidade e modo rapido
///   tranal /update-install      - executa o instalador de uma versao nova
///
/// A REGRA mora no nucleo: cada comando aqui consome o veredito de uma
/// classe do <c>Core</c> e nao repete a regra de negocio.
/// </summary>
public static class DataCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/local-db")) return ExecutarBanco(flags);
        if (CliArgs.Has(flags, "/import-log")) return ExecutarImportarLog(args);
        if (CliArgs.Has(flags, "/attachments")) return ExecutarLigacoes();
        if (CliArgs.Has(flags, "/trace-service")) return ExecutarTraceService();
        if (CliArgs.Has(flags, "/votes")) return ExecutarVotos(flags);
        if (CliArgs.Has(flags, "/uninstallers")) return ExecutarDesinstaladores();
        if (CliArgs.Has(flags, "/accessibility")) return ExecutarAcessibilidade();
        if (CliArgs.Has(flags, "/update-install")) return ExecutarUpdateInstall(args);
        return null;
    }

    /// <summary>O banco local embutido.</summary>
    private static int ExecutarBanco(HashSet<string> flags)
    {
        Console.WriteLine($"Banco local: {LocalSqlStore.DatabasePath}");
        Console.WriteLine($"SQLite do Windows disponivel: {(LocalSqlStore.IsAvailable ? "sim" : "nao")}");

        if (!CliArgs.Has(flags, "/recent")) return 0;

        var linhas = LocalSqlStore.Recent(20);

        Console.WriteLine();
        Console.WriteLine($"Remocoes registradas: {linhas.Count}");
        foreach (var linha in linhas)
        {
            Console.WriteLine(string.Join(" | ", linha));
        }

        return 0;
    }

    /// <summary>Importa um log antigo.</summary>
    private static int ExecutarImportarLog(string[] args)
    {
        var caminho = Value(args, "/path") ?? string.Empty;
        if (caminho.Length == 0)
        {
            Console.WriteLine("Uso: tranal /import-log /path <arquivo de log>");
            return 1;
        }

        var entradas = LegacyLogImporter.Import(caminho);

        Console.WriteLine(LegacyLogImporter.Describe(caminho));
        Console.WriteLine();

        foreach (var entrada in entradas)
        {
            var quando = entrada.When is { } data ? data.ToString("dd/MM/yyyy HH:mm") : "(sem data)";
            Console.WriteLine($"{quando,-18} {entrada.ProgramName,-26} {entrada.Action}");
        }

        return 0;
    }

    /// <summary>As ligacoes entre programa e log de instalacao.</summary>
    private static int ExecutarLigacoes()
    {
        var ligacoes = TraceAttachment.List();

        Console.WriteLine($"Ligacoes: {ligacoes.Count}");
        foreach (var ligacao in ligacoes)
        {
            Console.WriteLine($"{ligacao.ProgramName,-26} {ligacao.TraceId} {ligacao.TracePath}");
        }

        return 0;
    }

    /// <summary>A versao do servico de rastreio.</summary>
    private static int ExecutarTraceService()
    {
        var estado = TraceService.State();

        Console.WriteLine($"Versao gravada: {(estado.Version.Length > 0 ? estado.Version : "(nenhuma)")}");
        Console.WriteLine($"Versao atual: {estado.LatestKnownVersion}");
        Console.WriteLine($"Aviso de atualizacao pendente: {(TraceService.NeedsUpdatePrompt() ? "sim" : "nao")}");

        return 0;
    }

    /// <summary>Os votos do usuario.</summary>
    private static int ExecutarVotos(HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/export"))
        {
            var destino = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "tranal-votos.json");

            var gravado = CommunityVotes.Export(destino);
            Console.WriteLine(gravado.Length > 0
                ? $"Votos exportados para: {gravado}"
                : "Nao foi possivel exportar os votos.");
            return 0;
        }

        var votos = CommunityVotes.List();

        Console.WriteLine($"Votos: {votos.Count}");
        foreach (var voto in votos)
        {
            Console.WriteLine($"{voto.ProgramName,-26} {voto.Score} estrelas  {voto.Comment}");
        }

        return 0;
    }

    /// <summary>Os desinstaladores rodando agora.</summary>
    private static int ExecutarDesinstaladores()
    {
        var processos = UninstallerWatcher.List();

        Console.WriteLine(UninstallerWatcher.Describe());
        Console.WriteLine();

        foreach (var processo in processos)
        {
            Console.WriteLine($"{processo.ProcessId,8}  {processo.Name,-18} {(processo.IsSilent ? "silencioso" : "com janela")}");
        }

        return 0;
    }

    /// <summary>Acessibilidade e modo rapido.</summary>
    private static int ExecutarAcessibilidade()
    {
        Console.WriteLine(AccessibilityOptions.Describe());
        return 0;
    }

    /// <summary>Executa o instalador de uma versao nova.</summary>
    private static int ExecutarUpdateInstall(string[] args)
    {
        var caminho = Value(args, "/path") ?? string.Empty;
        if (caminho.Length == 0)
        {
            Console.WriteLine("Uso: tranal /update-install /path <pacote>");
            return 1;
        }

        var resultado = UpdateInstaller.Run(caminho);

        Console.WriteLine(resultado.Detail);
        return resultado.Started ? 0 : 1;
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
