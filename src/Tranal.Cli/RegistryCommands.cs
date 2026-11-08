using System;
using System.Collections.Generic;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Cli;

/// <summary>
/// Os comandos de limpeza de registro, desregistro COM, referencia
/// compartilhada e protecao de desinstalador.
///
/// Correspondem ao que os concorrentes medem:
///   tranal /regclean                        - lista as sobras por categoria
///   tranal /regclean "ActiveX;RunEntries"   - so duas categorias
///   tranal /regclean /remove                - remove as sobras listadas
///   tranal /regclean-categories             - lista as categorias
///   tranal /com-unregister "C:\Pasta"       - lista as DLLs COM registradas
///   tranal /com-unregister "C:\Pasta" /run  - desregistra as DLLs
///   tranal /shared "C:\Pasta"               - mostra quem usa o caminho
///   tranal /noremove "programa"             - mostra a protecao do item
///   tranal /noremove "programa" /disable    - desliga a protecao do item
/// </summary>
public static class RegistryCommands
{
    /// <summary>
    /// Tenta tratar um comando. Devolve null quando nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (flags.Contains("/regclean")) return RunRegistryCleaner(args, flags);
        if (flags.Contains("/regclean-categories")) return RunCategories();
        if (flags.Contains("/com-unregister")) return RunComUnregister(args, flags);
        if (flags.Contains("/shared")) return RunShared(args);
        if (flags.Contains("/noremove")) return RunNoRemove(args, flags);
        return null;
    }

    private static string? ValueAfter(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static int RunCategories()
    {
        Console.WriteLine("Categorias do Limpador de Registro:");
        Console.WriteLine();

        foreach (var categoria in RegistryCleaner.Categories)
        {
            Console.WriteLine($"{categoria.Id,-20} {categoria.Name}");
            Console.WriteLine($"{"",-20} {categoria.Description}");
        }

        Console.WriteLine();
        Console.WriteLine($"{RegistryCleaner.Categories.Count} categorias.");

        return 0;
    }

    private static int RunRegistryCleaner(string[] args, HashSet<string> flags)
    {
        var bruto = ValueAfter(args, "/regclean");
        var categorias = (bruto ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim())
            .Where(c => c.Length > 0)
            .ToList();

        var entradas = RegistryCleaner.Scan(categorias.Count == 0 ? null : categorias);
        var remover = flags.Contains("/remove");

        if (!remover)
        {
            Console.WriteLine($"Sobras de registro encontradas: {entradas.Count}");
            Console.WriteLine();

            foreach (var grupo in entradas.GroupBy(e => e.CategoryName).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine($"{grupo.Key} ({grupo.Count()})");
                foreach (var entrada in grupo.Take(20))
                {
                    var alvo = entrada.IsValue ? $"valor '{entrada.ValueName}'" : "chave";
                    Console.WriteLine($"    [{alvo}] {entrada.Path}");
                    Console.WriteLine($"        {entrada.Detail}");
                }

                if (grupo.Count() > 20) Console.WriteLine($"    ... e mais {grupo.Count() - 20}");
                Console.WriteLine();
            }

            Console.WriteLine("Use /remove para apagar as sobras listadas.");
            return 0;
        }

        if (entradas.Count == 0)
        {
            Console.WriteLine("Nenhuma sobra de registro encontrada.");
            return 0;
        }

        var relatorio = RegistryCleaner.Remove(entradas);

        Console.WriteLine($"Removidos: {relatorio.RemovedCount}  " +
                          $"Ja inexistentes: {relatorio.AbsentCount}  " +
                          $"Falhas: {relatorio.FailedCount}  " +
                          $"Protegidos: {relatorio.ProtectedCount}  " +
                          $"Adiados: {relatorio.DeferredCount}");

        foreach (var resultado in relatorio.Outcomes.Where(o => o.Status == RemovalStatus.Failed).Take(20))
            Console.WriteLine($"    falhou ({resultado.ErrorCode}): {resultado.Item.Path} — {resultado.Message}");

        return relatorio.FailedCount > 0 ? 1 : 0;
    }

    private static int RunComUnregister(string[] args, HashSet<string> flags)
    {
        var pasta = ValueAfter(args, "/com-unregister");
        if (string.IsNullOrWhiteSpace(pasta))
        {
            Console.Error.WriteLine("Informe a pasta do programa.");
            return 1;
        }

        var dlls = ComUnregister.RegisteredDlls(pasta);
        Console.WriteLine($"Componentes COM registrados em '{pasta}': {dlls.Count}");

        foreach (var dll in dlls) Console.WriteLine($"    {dll}");

        if (!flags.Contains("/run"))
        {
            if (dlls.Count > 0) Console.WriteLine("Use /run para desregistrar.");
            return 0;
        }

        var resultados = ComUnregister.UnregisterAll(dlls);
        var falhas = 0;

        foreach (var par in resultados)
        {
            Console.WriteLine($"    {(par.Value.Success ? "ok" : "falha")}: {par.Key} — {par.Value.Message}");
            if (!par.Value.Success) falhas++;
        }

        return falhas > 0 ? 1 : 0;
    }

    private static int RunShared(string[] args)
    {
        var caminho = ValueAfter(args, "/shared");
        if (string.IsNullOrWhiteSpace(caminho))
        {
            Console.Error.WriteLine("Informe o caminho.");
            return 1;
        }

        var programas = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = false,
        });

        var analise = SharedReferenceCounter.Analyze(caminho, programas);

        Console.WriteLine($"Caminho: {analise.Path}");
        Console.WriteLine($"Referencias: {analise.ReferenceCount}");
        Console.WriteLine(analise.IsShared
            ? "ATENCAO: o caminho e compartilhado por mais de um programa."
            : analise.IsOrphan
                ? "Nenhum programa instalado referencia o caminho."
                : "Somente um programa referencia o caminho.");

        foreach (var dono in analise.Owners) Console.WriteLine($"    {dono}");

        return 0;
    }

    private static int RunNoRemove(string[] args, HashSet<string> flags)
    {
        var nome = ValueAfter(args, "/noremove");
        if (string.IsNullOrWhiteSpace(nome))
        {
            Console.Error.WriteLine("Informe o programa.");
            return 1;
        }

        var programas = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = true,
            IncludeMsiProducts = false,
        });

        var alvo = programas.FirstOrDefault(p =>
            p.DisplayName.Equals(nome.Trim(), StringComparison.OrdinalIgnoreCase));

        if (alvo is null)
        {
            Console.Error.WriteLine($"Programa '{nome}' nao encontrado.");
            return 1;
        }

        var protegido = ProtectedUninstallTag.IsProtected(alvo);
        Console.WriteLine($"Programa: {alvo.DisplayName}");
        Console.WriteLine($"Protegido pela tag '{ProtectedUninstallTag.TagName}': {(protegido ? "sim" : "nao")}");

        if (!flags.Contains("/disable"))
        {
            if (!protegido) Console.WriteLine("O item nao esta protegido.");
            return 0;
        }

        var resultado = ProtectedUninstallTag.Clear(alvo);
        Console.WriteLine(resultado.Message);
        return resultado.Success ? 0 : 1;
    }
}
