using System;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Reporting;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Cli;

/// <summary>
/// Os comandos de limpeza, de sistema e do agente de fundo.
///
///   tranal /caches                - os caches que os programas deixam
///   tranal /caches /clean         - limpa os caches listados
///   tranal /screenshot            - captura a tela em um arquivo
///   tranal /crash-reports         - os relatorios de falha gravados
///   tranal /simple-delete         - gera o script de remocao simples
///   tranal /wow64                 - o caminho de 32 bits de um executavel
///   tranal /hive                  - as chaves de registro montadas
///   tranal /station               - o usuario e a estacao de rede
///   tranal /privileges            - os privilegios do token
///   tranal /chrome-sync           - a sincronizacao do Chrome
///   tranal /agent                 - o agente de fundo
///
/// A REGRA mora no nucleo: cada comando aqui consome o veredito de uma
/// classe do <c>Core</c> e nao repete a regra de negocio.
/// </summary>
public static class AgentCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/caches")) return ExecutarCaches(flags);
        if (CliArgs.Has(flags, "/screenshot")) return ExecutarScreenshot(args);
        if (CliArgs.Has(flags, "/crash-reports")) return ExecutarCrashReports();
        if (CliArgs.Has(flags, "/simple-delete")) return ExecutarSimpleDelete(args, flags);
        if (CliArgs.Has(flags, "/wow64")) return ExecutarWow64(args);
        if (CliArgs.Has(flags, "/hive")) return ExecutarHive(flags);
        if (CliArgs.Has(flags, "/station")) return ExecutarStation();
        if (CliArgs.Has(flags, "/privileges")) return ExecutarPrivileges();
        if (CliArgs.Has(flags, "/chrome-sync")) return ExecutarChromeSync();
        if (CliArgs.Has(flags, "/agent")) return ExecutarAgent(flags);
        return null;
    }

    /// <summary>Os caches que os programas instalados deixam atras.</summary>
    private static int ExecutarCaches(HashSet<string> flags)
    {
        var instalados = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = false,
        });

        var caches = AppCacheCleaner.Scan(instalados);

        Console.WriteLine($"Pastas de cache: {caches.Count}");
        Console.WriteLine();

        long total = 0;
        foreach (var cache in caches)
        {
            total += cache.SizeBytes;
            Console.WriteLine($"{cache.ProgramName,-26} {cache.Kind,-14} {cache.SizeBytes,12:N0} {cache.Path}");

            if (!CliArgs.Has(flags, "/clean")) continue;

            try
            {
                if (Directory.Exists(cache.Path)) Directory.Delete(cache.Path, recursive: true);
            }
            catch
            {
                // Cache trancado: fica para a proxima vez.
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Total em cache: {total:N0} bytes");

        return 0;
    }

    /// <summary>Captura a tela.</summary>
    private static int ExecutarScreenshot(string[] args)
    {
        var destino = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "tranal-captura.bmp");

        foreach (var arg in args)
        {
            if (!arg.StartsWith("/out=", StringComparison.OrdinalIgnoreCase)) continue;
            destino = arg["/out=".Length..];
        }

        var gravado = ScreenCapture.Capture(destino);
        if (gravado.Length == 0)
        {
            Console.WriteLine("Nao foi possivel capturar a tela.");
            if (ScreenCapture.LastError.Length > 0)
            {
                Console.WriteLine($"Motivo: {ScreenCapture.LastError}");
            }

            return 1;
        }

        Console.WriteLine($"Captura gravada em: {gravado}");
        return 0;
    }

    /// <summary>Os relatorios de falha gravados.</summary>
    private static int ExecutarCrashReports()
    {
        var relatorios = CrashReport.List();

        Console.WriteLine($"Relatorios de falha: {relatorios.Count}");
        foreach (var relatorio in relatorios)
        {
            Console.WriteLine(relatorio);
        }

        return 0;
    }

    /// <summary>Gera o script de remocao simples de um programa.</summary>
    private static int ExecutarSimpleDelete(string[] args, HashSet<string> flags)
    {
        var nome = ArgValue(args, "/name") ?? string.Empty;
        if (nome.Length == 0)
        {
            Console.WriteLine("Uso: tranal /simple-delete /name <nome do programa>");
            return 1;
        }

        var instalados = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = false,
        });

        var programa = instalados.FirstOrDefault(p =>
            p.DisplayName.Contains(nome, StringComparison.OrdinalIgnoreCase));

        if (programa is null)
        {
            Console.WriteLine($"Programa nao encontrado: {nome}");
            return 1;
        }

        var destino = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            SimpleDeleteWriter.FileName);

        var gravado = SimpleDeleteWriter.Write(
            programa.InstallLocation,
            new[] { programa.RegistryPath },
            programa.DisplayName,
            destino);

        if (gravado.Length == 0)
        {
            Console.WriteLine("Nao foi possivel gravar o script.");
            return 1;
        }

        Console.WriteLine($"Script de remocao simples gravado em: {gravado}");
        return 0;
    }

    /// <summary>O caminho de 32 bits de um executavel.</summary>
    private static int ExecutarWow64(string[] args)
    {
        var executavel = ArgValue(args, "/path") ?? string.Empty;
        if (executavel.Length == 0)
        {
            Console.WriteLine($"Pasta de 32 bits: {Wow64Process.SysWow64Folder}");
            Console.WriteLine($"O processo atual e de {(Wow64Process.Is32BitProcess ? "32" : "64")} bits.");
            return 0;
        }

        var caminho = Wow64Process.X86PathOf(executavel);
        Console.WriteLine(caminho ?? "O executavel nao existe na pasta de 32 bits.");
        return 0;
    }

    /// <summary>As chaves de registro montadas.</summary>
    private static int ExecutarHive(HashSet<string> flags)
    {
        var nome = ArgValue(flags, "/name") ?? string.Empty;

        if (CliArgs.Has(flags, "/unload"))
        {
            var ok = RegistryHiveLoader.Unload(nome);
            Console.WriteLine(ok ? $"Chave '{nome}' descarregada." : $"Nao foi possivel descarregar '{nome}'.");
            return 0;
        }

        if (CliArgs.Has(flags, "/load"))
        {
            var arquivo = ArgValue(flags, "/file") ?? string.Empty;
            var ok = RegistryHiveLoader.Load(nome, arquivo);
            Console.WriteLine(ok ? $"Chave '{nome}' carregada." : $"Nao foi possivel carregar '{nome}'.");
            return 0;
        }

        var chaves = RegistryHiveLoader.List();
        Console.WriteLine($"Chaves montadas: {chaves.Count}");
        foreach (var chave in chaves)
        {
            Console.WriteLine($"{chave.Name,-24} {chave.Path}");
        }

        return 0;
    }

    /// <summary>O usuario e a estacao de rede.</summary>
    private static int ExecutarStation()
    {
        var estacao = NetworkStationInfo.Current();

        Console.WriteLine($"Usuario: {estacao.UserName}");
        Console.WriteLine($"Dominio: {estacao.DomainName}");
        Console.WriteLine($"Computador: {estacao.ComputerName}");
        Console.WriteLine($"Grupo de trabalho: {(estacao.IsWorkgroup ? "sim" : "nao")}");
        Console.WriteLine($"Placa de rede: {estacao.MacAddress}");
        Console.WriteLine($"Endereco IPv4: {NetworkStationInfo.IPv4Address()}");

        return 0;
    }

    /// <summary>Os privilegios do token do processo.</summary>
    private static int ExecutarPrivileges()
    {
        Console.WriteLine($"Rodando como administrador: {(TokenPrivileges.IsElevated() ? "sim" : "nao")}");
        Console.WriteLine();

        foreach (var privilegio in TokenPrivileges.List())
        {
            Console.WriteLine($"{privilegio.Name,-26} {(privilegio.Enabled ? "ligado" : "desligado")}");
        }

        return 0;
    }

    /// <summary>A sincronizacao do Chrome.</summary>
    private static int ExecutarChromeSync()
    {
        Console.WriteLine($"Sincronizacao do Chrome: {(ChromeSyncGuard.IsSyncEnabled() ? "ligada" : "desligada")}");

        var aviso = ChromeSyncGuard.WarningFor("teste");
        if (aviso.Length > 0) Console.WriteLine(aviso);

        return 0;
    }

    /// <summary>O agente de fundo.</summary>
    private static int ExecutarAgent(HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/start"))
        {
            var id = BackgroundAgent.Start();
            Console.WriteLine(id > 0 ? $"Agente iniciado (processo {id})." : "Nao foi possivel iniciar o agente.");
            return 0;
        }

        if (CliArgs.Has(flags, "/stop"))
        {
            Console.WriteLine(BackgroundAgent.Stop() ? "Agente parado." : "O agente nao estava rodando.");
            return 0;
        }

        Console.WriteLine(BackgroundAgent.IsRunning()
            ? $"Agente rodando (processo {BackgroundAgent.RunningProcessId()})."
            : "Agente parado.");

        return 0;
    }

    private static string? ArgValue(IEnumerable<string> args, string prefix)
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
