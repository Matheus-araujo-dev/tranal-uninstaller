using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// Uma tarefa agendada do Windows.
///
/// A listagem vem da **leitura dos arquivos de definicao** em
/// <c>%SystemRoot%\System32\Tasks</c>, e nao do <c>schtasks.exe</c>: a saida do
/// <c>schtasks</c> traz o cabecalho no idioma do sistema, e um leitor que so
/// funciona em ingles nao serve para um produto bilíngue. O arquivo XML da
/// tarefa e a definicao oficial do Agendador de Tarefas, sempre em ingles.
/// </summary>
public sealed class ScheduledTask
{
    /// <summary>Nome completo da tarefa, com as pastas (ex.: <c>\Microsoft\Windows\Defrag\Defrag</c>).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Pasta da tarefa (o que vem antes do ultimo separador).</summary>
    public string Folder { get; init; } = string.Empty;

    /// <summary>Caminho do arquivo que define a tarefa.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Executavel que a tarefa roda.</summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>Argumentos da tarefa.</summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>Estado declarado no XML ("Ready", "Running", "Disabled").</summary>
    public string State { get; init; } = string.Empty;

    /// <summary>True quando a tarefa esta habilitada no Agendador.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Descricao da tarefa, quando o XML traz.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// O executavel, ja sem os argumentos.
    ///
    /// Uma tarefa pode ter varios <c>Exec</c>; o primeiro e o que roda.
    /// </summary>
    public string ExecutablePath => ExecutablePathResolver.Extract(Command);
}

/// <summary>
/// Gerenciador proprio de tarefas agendadas.
///
/// Hoje o Tranal apenas **abre** o Agendador de Tarefas
/// (<c>Core/Tools/WindowsToolsCatalog.cs</c>). Este gerenciador lista,
/// correlaciona com o programa instalado e age sem sair do aplicativo.
///
/// Listar e ler (dos arquivos XML); agir e com o <c>schtasks.exe</c>, que e a
/// ferramenta nativa e trata o bloqueio de arquivo do proprio Windows.
/// </summary>
public static class ScheduledTaskManager
{
    /// <summary>A pasta onde o Windows guarda a definicao das tarefas.</summary>
    public static string TasksRoot
    {
        get
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (string.IsNullOrEmpty(windows)) windows = @"C:\Windows";
            return Path.Combine(windows, "System32", "Tasks");
        }
    }

    /// <summary>Todas as tarefas agendadas legiveis.</summary>
    public static IReadOnlyList<ScheduledTask> List()
    {
        var root = TasksRoot;
        if (!Directory.Exists(root)) return Array.Empty<ScheduledTask>();

        var tasks = new List<ScheduledTask>();

        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var task = Read(file, root);
                if (task is not null) tasks.Add(task);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // A pasta de tarefas e protegida: sem administrador, a leitura
            // devolve o que deu para ler em vez de nada.
        }
        catch (IOException)
        {
            // Arquivo trancado por outro processo durante o varredura.
        }

        return tasks.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// As tarefas cujo executavel esta dentro de um caminho de instalacao.
    ///
    /// E o que amarra a tarefa ao programa: uma tarefa que roda
    /// <c>C:\App\updater.exe</c> pertence ao programa instalado em
    /// <c>C:\App</c>.
    /// </summary>
    public static IReadOnlyList<ScheduledTask> ListFor(string installPath)
    {
        var root = PathUtil.Normalize(installPath);
        if (string.IsNullOrEmpty(root)) return Array.Empty<ScheduledTask>();

        return List()
            .Where(t => ServiceManager.BelongsTo(t.ExecutablePath, root))
            .ToList();
    }

    /// <summary>Desativa a tarefa no Agendador de Tarefas do Windows.</summary>
    public static ManagementResult Disable(ScheduledTask task)
    {
        if (task is null) return ManagementResult.Fail("Tarefa nao informada.");

        var (code, output) = RunSchtasks($"/change /tn \"{task.Name}\" /disable");
        if (code == 0) return ManagementResult.Ok($"A tarefa '{task.Name}' foi desativada.");

        return ManagementResult.Fail(
            $"Nao foi possivel desativar a tarefa '{task.Name}' (codigo {code}). {output}".Trim());
    }

    /// <summary>Reativa a tarefa no Agendador de Tarefas do Windows.</summary>
    public static ManagementResult Enable(ScheduledTask task)
    {
        if (task is null) return ManagementResult.Fail("Tarefa nao informada.");

        var (code, output) = RunSchtasks($"/change /tn \"{task.Name}\" /enable");
        if (code == 0) return ManagementResult.Ok($"A tarefa '{task.Name}' foi reativada.");

        return ManagementResult.Fail(
            $"Nao foi possivel reativar a tarefa '{task.Name}' (codigo {code}). {output}".Trim());
    }

    /// <summary>Remove a tarefa do Agendador de Tarefas do Windows.</summary>
    public static ManagementResult Delete(ScheduledTask task)
    {
        if (task is null) return ManagementResult.Fail("Tarefa nao informada.");

        var (code, output) = RunSchtasks($"/delete /tn \"{task.Name}\" /f");
        if (code == 0) return ManagementResult.Ok($"A tarefa '{task.Name}' foi removida.");

        return ManagementResult.Fail(
            $"Nao foi possivel remover a tarefa '{task.Name}' (codigo {code}). {output}".Trim());
    }

    /// <summary>
    /// Cria uma tarefa agendada.
    ///
    /// A criacao usa o <c>schtasks.exe</c> do proprio Windows, pela mesma
    /// raza de listar: o Agendador de Tarefas real e quem decide se a tarefa
    /// existe, e nao um XML escrito a mao.
    ///
    /// O gatilho aceito e o diario, na hora escolhida — que e o que o
    /// HiBit oferece ("criar tarefa diaria") e o que serve para o
    /// atualizador de um programa.
    ///
    /// </summary>
    /// <param name="name">O nome da tarefa (sem a pasta inicial).</param>
    /// <param name="executable">O executavel que a tarefa roda.</param>
    /// <param name="arguments">Os argumentos do executavel.</param>
    /// <param name="time">A hora do disparo, no formato "HH:mm".</param>
    /// <param name="folder">A pasta da tarefa (ex.: "\\Tranal").</param>
    /// <param name="description">A descricao da tarefa.</param>
    public static ManagementResult Create(
        string? name,
        string? executable,
        string? arguments = null,
        string? time = null,
        string? folder = null,
        string? description = null)
    {
        var nome = (name ?? string.Empty).Trim();
        if (nome.Length == 0) return ManagementResult.Fail("Informe o nome da tarefa.");

        var programa = (executable ?? string.Empty).Trim().Trim('"');
        if (programa.Length == 0) return ManagementResult.Fail("Informe o executavel da tarefa.");

        if (!File.Exists(programa))
            return ManagementResult.Fail($"O executavel '{programa}' nao existe.");

        var hora = (time ?? "00:00").Trim();
        if (!Regex.IsMatch(hora, @"^\d{1,2}:\d{2}$"))
            return ManagementResult.Fail($"A hora '{hora}' nao esta no formato HH:mm.");

        var partes = hora.Split(':');
        var h = int.Parse(partes[0]);
        var m = int.Parse(partes[1]);
        if (h > 23 || m > 59)
            return ManagementResult.Fail($"A hora '{hora}' nao e uma hora valida.");

        var linha = new StringBuilder();
        linha.Append($"/create /tn \"{nome}\" /tr \"{programa}");
        if (!string.IsNullOrWhiteSpace(arguments)) linha.Append(' ').Append(arguments.Trim());

        if (!string.IsNullOrWhiteSpace(folder))
        {
            var pasta = folder.Trim();
            if (!pasta.StartsWith("\\", StringComparison.Ordinal)) pasta = "\\" + pasta;
            nome = pasta + "\\" + nome;
            linha.Clear();
            linha.Append($"/create /tn \"{nome}\" /tr \"{programa}");
            if (!string.IsNullOrWhiteSpace(arguments)) linha.Append(' ').Append(arguments.Trim());
            linha.Append(" /f");
        }

        linha.Append($" /sc daily /st {h:00}:{m:00}");

        if (!string.IsNullOrWhiteSpace(description))
            linha.Append($" /desc \"{description.Trim()}\"");

        var (code, output) = RunSchtasks(linha.ToString());
        if (code == 0) return ManagementResult.Ok($"A tarefa '{nome}' foi criada.");

        return ManagementResult.Fail(
            $"Nao foi possivel criar a tarefa '{nome}' (codigo {code}). {output}".Trim());
    }

    /// <summary>
    /// Roda o <c>schtasks.exe</c> e devolve o codigo de saida e a saida.
    ///
    /// O executavel vem do proprio Windows: nao ha PATH a_lookup nem binario
    /// proprio que possa divergir do Agendador de Tarefas real.
    /// </summary>
    private static (int Code, string Output) RunSchtasks(string arguments)
    {
        try
        {
            var info = new ProcessStartInfo("schtasks.exe", arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using var process = Process.Start(info);
            if (process is null) return (-1, "Nao foi possivel iniciar o schtasks.exe.");

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(30_000);

            var text = string.IsNullOrWhiteSpace(error) ? output : error;
            return (process.ExitCode, text.Trim());
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    /// <summary>
    /// Le uma tarefa do arquivo XML de definicao.
    ///
    /// O XML do Agendador usa o namespace do Windows; o codigo o ignora de
    /// proposito — o que importa e o nome do elemento, que e fixo.
    /// </summary>
    private static ScheduledTask? Read(string file, string root)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            // Arquivo ilegivel ou protegido: a tarefa simplesmente nao entra
            // na lista. Um XML invalido nao pode derrubar a listagem toda.
            return null;
        }

        var relative = file.Length > root.Length ? file.Substring(root.Length) : file;
        relative = relative.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullName = "\\" + relative.Replace(Path.DirectorySeparatorChar, '\\');

        var separator = fullName.LastIndexOf('\\');
        var folder = separator > 0 ? fullName.Substring(0, separator) : "\\";
        var name = separator > 0 ? fullName.Substring(separator + 1) : fullName;

        var exec = document.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Exec");
        var command = exec?.Elements().FirstOrDefault(e => e.Name.LocalName == "Command")?.Value ?? string.Empty;
        var arguments = exec?.Elements().FirstOrDefault(e => e.Name.LocalName == "Arguments")?.Value ?? string.Empty;

        var enabledElement = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Enabled");
        var enabled = enabledElement is null || !string.Equals(enabledElement.Value, "false", StringComparison.OrdinalIgnoreCase);

        var state = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "State")?.Value ?? string.Empty;

        var description = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Description")?.Value ?? string.Empty;

        return new ScheduledTask
        {
            Name = fullName,
            Folder = folder,
            FilePath = file,
            Command = command,
            Arguments = arguments,
            State = state,
            Enabled = enabled,
            Description = description,
        };
    }
}
