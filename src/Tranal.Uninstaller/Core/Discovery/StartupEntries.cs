using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Uma entrada que faz um programa subir junto com o Windows.
/// </summary>
/// <param name="Location">A chave de registro, para o texto do usuario.</param>
/// <param name="Name">O nome do valor.</param>
/// <param name="Command">O comando que o Windows executa.</param>
public sealed record StartupEntry(string Location, string Name, string Command);

/// <summary>
/// As entradas de inicializacao do Windows — o <c>Run</c>, o <c>RunOnce</c> e
/// companhia.
///
/// A evidencia de que o Revo Uninstaller Pro 5.5.4 usa esse sinal esta no
/// proprio binario dele: a secao "[Uninstaller Options]" da tabela de idioma
/// traz o item de "Show system components", e a varredura de sobras do Revo
/// trata a inicializacao como sinal de que o item pertence ao programa.
///
/// A regra mora aqui, em um lugar so: quem precisa saber se uma entrada de
/// inicializacao pertence a um programa chama <see cref="BelongsTo"/>.
/// </summary>
public static class StartupEntries
{
    /// <summary>As chaves onde o Windows guarda o que sobe junto com ele.</summary>
    private static readonly (RegistryHive Hive, string Path, string Label)[] Sources =
    {
        (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU\\...\\Run"),
        (RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "HKCU\\...\\RunOnce"),
        (RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM\\...\\Run"),
        (RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "HKLM\\...\\RunOnce"),
        (RegistryHive.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM\\WOW6432Node\\...\\Run"),
    };

    /// <summary>
    /// Todas as entradas de inicializacao legiveis nesta maquina.
    ///
    /// Nunca lanca: uma chave que nao pode ser lida devolve lista vazia em
    /// vez de derrubar a varredura de sobras.
    /// </summary>
    public static IReadOnlyList<StartupEntry> Scan()
    {
        var entries = new List<StartupEntry>();

        foreach (var source in Sources)
        {
            try
            {
                using var key = RegistryKey.OpenBaseKey(source.Hive, RegistryView.Registry64)
                    .OpenSubKey(source.Path);

                if (key is null) continue;

                foreach (var name in key.GetValueNames())
                {
                    var value = key.GetValue(name) as string;
                    if (string.IsNullOrWhiteSpace(value)) continue;

                    entries.Add(new StartupEntry(source.Label, name, value.Trim()));
                }
            }
            catch
            {
                // Chave protegida ou ausente: segue para a proxima.
            }
        }

        return entries;
    }

    /// <summary>
    /// As entradas de inicializacao que pertencem ao programa.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    /// <param name="entries">
    /// As entradas a considerar. Por padrao, as lidas do registro — o
    /// parametro existe para os testes nao dependerem da maquina.
    /// </param>
    public static IReadOnlyList<StartupEntry> For(
        InstalledProgram program, IReadOnlyList<StartupEntry>? entries = null)
    {
        if (program is null) return Array.Empty<StartupEntry>();

        var source = entries ?? Scan();
        return source.Where(e => BelongsTo(e, program)).ToList();
    }

    /// <summary>
    /// Verdadeiro quando a entrada de inicializacao pertence ao programa.
    ///
    /// Dois sinais, do mais forte para o mais fraco:
    ///
    ///   1. o executavel pointed pela entrada esta dentro da pasta de
    ///      instalacao do programa;
    ///   2. o comando cita o nome do programa.
    ///
    /// O segundo sinal e mais fraco de proposito: um nome generico casaria
    /// com-programas-que-nao-sao-o-programa.
    /// </summary>
    /// <param name="entry">A entrada de inicializacao.</param>
    /// <param name="program">O programa inspecionado.</param>
    public static bool BelongsTo(StartupEntry entry, InstalledProgram program)
    {
        if (entry is null || program is null) return false;

        var command = entry.Command ?? string.Empty;
        if (command.Length == 0) return false;

        var location = PathUtil.Normalize(program.InstallLocation);
        if (location.Length > 0)
        {
            var executable = ProgramScanner.ExtractExecutablePath(command);
            if (executable.Length > 0)
            {
                var normalized = PathUtil.Normalize(executable);
                if (normalized.StartsWith(location, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        foreach (var variant in LeftoverScanner.NameVariants(program))
        {
            if (command.Contains(variant, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }
}
