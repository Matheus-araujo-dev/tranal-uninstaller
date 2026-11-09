using System.IO;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Tracing;

namespace Tranal.Cli;

/// <summary>
/// Os comandos de inspecao do Tranal: a leitura do estado da maquina que
/// responde "o que foi instalado, o que quebrou, o que sobrou".
///
///   tranal /inspection-checksums "caminho" [/against "pasta"]  - somas de verificacao
///   tranal /inspection-dumps [/clean]                          - dumps de falha
///   tranal /inspection-printers                                - impressoras e fila
///   tranal /inspection-duplicates                              - instalacoes duplicadas
///   tranal /inspection-tasks [/unhide "tarefa"]                - tarefas ocultas
///
/// A REGRA mora no nucleo: cada comando aqui consome o veredito de uma
/// classe do <c>Core</c> e nao repete a regra de negocio.
/// </summary>
public static class InspectionCommands
{
    /// <summary>
    /// Tenta tratar o comando. Devolve null quando nenhum comando foi pedido.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int? TryRun(string[] args, HashSet<string> flags)
    {
        if (CliArgs.Has(flags, "/inspection-checksums")) return ExecutarChecksums(args);
        if (CliArgs.Has(flags, "/inspection-dumps")) return ExecutarDumps(args, flags);
        if (CliArgs.Has(flags, "/inspection-printers")) return ExecutarImpressoras();
        if (CliArgs.Has(flags, "/inspection-duplicates")) return ExecutarEntradasDuplicadas();
        if (CliArgs.Has(flags, "/inspection-tasks")) return ExecutarTarefasOcultas(args);
        return null;
    }

    /// <summary>
    /// As somas de verificacao dos arquivos de uma pasta (ou de um arquivo).
    ///
    /// Com <c>/against</c>, compara duas pastas e mostra os arquivos novos:
    /// os que o instalador trouxe e que ainda estao no disco.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    public static int ExecutarChecksums(string[] args)
    {
        var caminho = CliArgs.ValueAfter(args, "/inspection-checksums");
        if (string.IsNullOrWhiteSpace(caminho))
        {
            Console.Error.WriteLine("Uso: tranal /inspection-checksums \"caminho\" [/against \"pasta\"]");
            return 1;
        }

        var contra = CliArgs.ValueAfter(args, "/against");

        if (!string.IsNullOrWhiteSpace(contra))
        {
            var antes = InstallChecksum.ComputeOfFolder(contra);
            var depois = InstallChecksum.ComputeOfFolder(caminho);
            var novos = InstallChecksum.Compare(antes, depois);

            Console.WriteLine($"Arquivos somados: {depois.Count}");
            Console.WriteLine($"Arquivos novos: {novos.Count}");
            Console.WriteLine();

            foreach (var novo in novos)
            {
                Console.WriteLine($"    {novo.Path}");
                Console.WriteLine($"        MD5 {novo.Md5}  {ByteFormatter.Format(novo.SizeBytes)}");
            }

            return 0;
        }

        if (Directory.Exists(caminho))
        {
            var somas = InstallChecksum.ComputeOfFolder(caminho);

            Console.WriteLine($"Arquivos somados: {somas.Count}");
            Console.WriteLine();

            foreach (var soma in somas)
            {
                Console.WriteLine($"    {soma.Path}");
                Console.WriteLine($"        MD5 {soma.Md5}  SHA256 {soma.Sha256}  {ByteFormatter.Format(soma.SizeBytes)}");
            }

            return 0;
        }

        var arquivo = InstallChecksum.Of(caminho);
        if (arquivo is null)
        {
            Console.Error.WriteLine($"Nao foi possivel ler '{caminho}'.");
            return 1;
        }

        Console.WriteLine($"MD5    {arquivo.Md5}");
        Console.WriteLine($"SHA256 {arquivo.Sha256}");
        Console.WriteLine($"Tamanho {ByteFormatter.Format(arquivo.SizeBytes)}");

        return 0;
    }

    /// <summary>
    /// O relatorio dos dumps de falha: quantos sao, de quem sao e quanto
    /// ocupam — com o correlato das falhas do Event Log.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    /// <param name="flags">As opcoes reconhecidas.</param>
    public static int ExecutarDumps(string[] args, HashSet<string> flags)
    {
        var dumps = CrashDumpCollector.Collect();
        var resumo = CrashDumpCollector.Summarize(dumps);

        Console.WriteLine($"Dumps de falha: {resumo.Count} ({ByteFormatter.Format(resumo.TotalBytes)})");
        Console.WriteLine();

        foreach (var programa in resumo.ByProgram)
        {
            Console.WriteLine($"    {programa.Program}  {programa.Dumps} dump(s), {ByteFormatter.Format(programa.Bytes)}");
            if (programa.Crashes > 0) Console.WriteLine($"        falhas registradas no Event Log: {programa.Crashes}");
        }

        if (CliArgs.Has(flags, "/clean"))
        {
            var relatorio = CrashDumpCollector.Clear();
            Console.WriteLine();
            Console.WriteLine($"Removidos: {relatorio.Outcomes.Count(o => o.Removed)}");
            Console.WriteLine($"Adiados para o proximo reinicio: {relatorio.Outcomes.Count(o => o.Status == RemovalStatus.Deferred)}");
        }

        return 0;
    }

    /// <summary>
    /// As impressoras instaladas, a fila de impressao e o driver de cada uma.
    /// </summary>
    public static int ExecutarImpressoras()
    {
        var impressoras = PrinterEnumerator.ListWithQueue();

        Console.WriteLine($"Impressoras: {impressoras.Count}");
        Console.WriteLine();

        foreach (var impressora in impressoras)
        {
            var driver = impressora.Driver.Length > 0
                ? impressora.Driver
                : PrinterEnumerator.FindDriver(impressora.Name);

            var fila = impressora.QueuedJobs > 0 ? $"  fila: {impressora.QueuedJobs}" : string.Empty;
            var padrao = impressora.IsDefault ? "  (padrao)" : string.Empty;

            Console.WriteLine($"    {impressora.Name}{padrao}  [{impressora.Port}]{fila}");
            if (driver.Length > 0) Console.WriteLine($"        driver: {driver}");
        }

        var trabalhos = PrinterEnumerator.ListJobs();
        if (trabalhos.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"Trabalhos na fila: {trabalhos.Count}");
            foreach (var trabalho in trabalhos)
            {
                Console.WriteLine($"    {trabalho.Document}  ({trabalho.Owner})");
            }
        }

        return 0;
    }

    /// <summary>
    /// As instalacoes duplicadas: o mesmo programa instalado em mais de um
    /// lugar, e a copia vencedora de cada grupo.
    /// </summary>
    public static int ExecutarEntradasDuplicadas()
    {
        var instalados = ProgramScanner.Scan(new ProgramScanner.ScanOptions
        {
            IncludeSystemComponents = false,
            IncludeMsiProducts = false,
        });

        var grupos = DuplicateInstallDetector.Detect(instalados);

        Console.WriteLine($"Grupos de instalacao duplicada: {grupos.Count}");
        Console.WriteLine();

        foreach (var grupo in grupos)
        {
            Console.WriteLine($"    {grupo.Name} ({grupo.Count} copias, por {grupo.Reason})");
            Console.WriteLine($"        vencedora: {grupo.Winner?.InstallLocation}");

            foreach (var programa in grupo.Entries)
            {
                Console.WriteLine($"        {programa.InstallLocation}");
            }
        }

        return 0;
    }

    /// <summary>
    /// As tarefas agendadas ocultas: as da Microsoft (que a visao esconde) e
    /// as que o Windows marca como ocultas.
    ///
    /// Com <c>/unhide</c>, deixa a tarefa visivel de novo — sem apagar e
    /// sem desabilitar nada.
    /// </summary>
    /// <param name="args">Os argumentos da linha de comando.</param>
    public static int ExecutarTarefasOcultas(string[] args)
    {
        var microsoft = ScheduledTaskVisibility.ListMicrosoftTasks();
        var ocultas = ScheduledTaskVisibility.ListHidden();

        Console.WriteLine($"Tarefas da Microsoft (escondidas na visao): {microsoft.Count}");
        Console.WriteLine($"Tarefas marcadas como ocultas pelo Windows: {ocultas.Count}");
        Console.WriteLine();

        foreach (var tarefa in ocultas)
        {
            Console.WriteLine($"    {tarefa.Name}");
            if (tarefa.Command.Length > 0) Console.WriteLine($"        {tarefa.Command} {tarefa.Arguments}");
        }

        var nome = CliArgs.ValueAfter(args, "/unhide");
        if (string.IsNullOrWhiteSpace(nome)) return 0;

        var alvo = ScheduledTaskManager.List()
            .FirstOrDefault(t => string.Equals(t.Name, nome, StringComparison.OrdinalIgnoreCase));

        if (alvo is null)
        {
            Console.Error.WriteLine($"Tarefa nao encontrada: '{nome}'.");
            return 1;
        }

        var resultado = ScheduledTaskVisibility.Unhide(alvo);
        Console.WriteLine(resultado.Message);

        return resultado.Success ? 0 : 1;
    }
}
