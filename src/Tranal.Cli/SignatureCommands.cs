using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Management;

namespace Tranal.Cli;

/// <summary>
/// A assinatura digital do desinstalador.
///
///   tranal /signature "C:\\Program Files\\App\\unins000.exe" - o estado de um arquivo
///   tranal /signatures                                  - a assinatura dos desinstaladores
///
/// A REGRA esta no nucleo (<c>UninstallerCertificate</c>): ele le a cadeia do
/// certificado e pergunta ao Windows (<c>WinVerifyTrust</c>) se a assinatura e
/// confiavel de verdade. A linha de comando so pede e mostra.
/// </summary>
public static class SignatureCommands
{
    /// <summary>
    /// Tenta tratar um comando de assinatura. Devolve null quando nenhum
    /// comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/signatures")) return RunAll();
        if (CliArgs.Has(flags, "/signature")) return RunOne(args);
        return null;
    }

    private static int RunOne(string[] args)
    {
        var alvo = CliArgs.ValueAfter(args, "/signature");
        if (string.IsNullOrWhiteSpace(alvo))
        {
            Console.Error.WriteLine("Uso: tranal /signature \"C:\\Program Files\\App\\unins000.exe\"");
            return 1;
        }

        if (!File.Exists(alvo))
        {
            Console.Error.WriteLine($"Arquivo nao encontrado: {alvo}");
            return 1;
        }

        var assinatura = UninstallerCertificate.Verify(alvo);
        Mostrar(alvo!, assinatura);

        return assinatura.IsSigned ? 0 : 1;
    }

    private static int RunAll()
    {
        var programas = ProgramScanner.Scan(
            new ProgramScanner.ScanOptions { RequireUninstallString = false });

        var cronometro = Stopwatch.StartNew();
        var contados = 0;
        var assinados = 0;

        foreach (var programa in programas)
        {
            var caminho = UninstallerExecutable.Resolve(programa);
            if (caminho.Length == 0) continue;

            var assinatura = UninstallerCertificate.Verify(caminho);
            contados++;

            if (assinatura.IsSigned) assinados++;

            Console.WriteLine(
                $"{programa.DisplayName,-30} {assinatura.Status,-14} {assinatura.SignerSubject}");
        }

        cronometro.Stop();

        Console.WriteLine();
        Console.WriteLine($"Desinstaladores verificados: {contados}");
        Console.WriteLine($"Com assinatura digital: {assinados}");
        Console.WriteLine($"Tempo: {cronometro.ElapsedMilliseconds} ms");

        return 0;
    }

    /// <summary>
    /// Mostra o resultado da verificacao.
    /// </summary>
    /// <param name="caminho">O arquivo verificado.</param>
    /// <param name="assinatura">O resultado.</param>
    private static void Mostrar(string caminho, UninstallerSignature assinatura)
    {
        Console.WriteLine($"Arquivo:  {caminho}");
        Console.WriteLine($"Assinado: {(assinatura.IsSigned ? "sim" : "nao")}");
        Console.WriteLine($"Estado:   {assinatura.Status}");

        // A explicacao do estado mora no nucleo: a linha de comando mostra
        // ela (o texto e escrito em portugues, como todo o texto da CLI).
        if (assinatura.Message.Length > 0)
            Console.WriteLine($"          {assinatura.Message}");
        Console.WriteLine($"Signatario: {assinatura.SignerSubject}");
        Console.WriteLine($"Emissor:  {assinatura.SignerIssuer}");

        if (assinatura.NotAfter is not null)
        {
            Console.WriteLine($"Validade: {assinatura.NotBefore:yyyy-MM-dd} a {assinatura.NotAfter:yyyy-MM-dd}");
        }

        if (assinatura.ChainSubjects.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Cadeia:");

            foreach (var sujeito in assinatura.ChainSubjects)
            {
                Console.WriteLine("    " + sujeito);
            }
        }

        if (assinatura.ChainProblems.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Problemas:");

            foreach (var problema in assinatura.ChainProblems)
            {
                Console.WriteLine("    " + problema);
            }
        }
    }
}
