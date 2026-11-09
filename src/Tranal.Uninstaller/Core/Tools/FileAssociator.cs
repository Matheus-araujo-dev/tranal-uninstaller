using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Uma associacao de tipo de arquivo: a extensao e o programa que abre o
/// arquivo com ela.
/// </summary>
public sealed class FileAssociationItem
{
    /// <summary>A extensao, com o ponto (".pdf").</summary>
    public string Extension { get; init; } = string.Empty;

    /// <summary>A descricao legivel do tipo ("Documento PDF").</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>O identificador do tipo no registro ("AcroExch.Document.DC").</summary>
    public string ProgId { get; init; } = string.Empty;

    /// <summary>O caminho do programa que abre o arquivo.</summary>
    public string ProgramPath { get; init; } = string.Empty;

    /// <summary>O nome do programa que abre o arquivo.</summary>
    public string ProgramName { get; init; } = string.Empty;

    /// <summary>Onde a associacao vive ("Usuario" ou "Maquina").</summary>
    public string Scope { get; init; } = string.Empty;

    /// <summary>True quando existe um programa que abre o arquivo.</summary>
    public bool HasProgram { get; init; }

    /// <summary>
    /// True quando o programa vem da escolha do usuario (a chave
    /// <c>UserChoice</c>), que e o que o Windows respeita.
    /// </summary>
    public bool IsUserChoice { get; init; }

    /// <summary>
    /// True quando a associacao aponta para um programa que nao existe mais.
    /// E o tipo de associacao quebrada que o File Associator do Ashampoo
    /// procura.
    /// </summary>
    public bool IsBroken { get; init; }
}

/// <summary>
/// O resultado da varredura das associacoes.
/// </summary>
public sealed class FileAssociatorReport
{
    /// <summary>Todas as associacoes encontradas.</summary>
    public IReadOnlyList<FileAssociationItem> Items { get; init; } = Array.Empty<FileAssociationItem>();

    /// <summary>Quantas extensoes nao tem programa nenhum.</summary>
    public int UnassociatedCount => Items.Count(i => !i.HasProgram);

    /// <summary>Quantas associacoes apontam para um programa que sumiu.</summary>
    public int BrokenCount => Items.Count(i => i.IsBroken);
}

/// <summary>
/// O File Associator: os tipos de arquivo sem programa associado, com edicao
/// e desfazer.
///
/// E o "File Associator" do Ashampoo UnInstaller 27.0.1 e o gerenciador de
/// associacoes do Uninstall Tool: a lista de extensoes que o Windows conhece,
/// o programa que abre cada uma, e a possibilidade de trocar o programa e
/// voltar atras.
///
/// A REGRA mora aqui, em um lugar so: a tela e a linha de comando consomem
/// esta varredura e estas acoes. O que grava e o registro do Windows — e o
/// Windows que decide o que vale, nao o Tranal.
///
/// A escrita e sempre no perfil do usuario
/// (<c>HKCU\Software\Classes</c>): nao pede administrador e nao mexe no
/// sistema. O estado anterior fica guardado, para o desfazer ter o que
/// restaurar.
/// </summary>
public static class FileAssociator
{
    /// <summary>Substitui a pasta do estado (so a suite de testes).</summary>
    internal static string? StateFolderOverride { get; set; }

    private const string ClassesRoot = @"Software\Classes";
    private const string UserChoiceRoot = @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts";
    private const string BackupFileName = "file-associations.json";

    /// <summary>A pasta onde fica o estado guardado das associacoes.</summary>
    public static string StateFolder =>
        StateFolderOverride ?? Path.Combine(RegistryBackup.DataDirectory, "associations");

    /// <summary>O arquivo com o estado guardado.</summary>
    public static string BackupFile => Path.Combine(StateFolder, BackupFileName);

    /// <summary>
    /// Varre as associacoes de arquivo do Windows.
    ///
    /// A conta do usuario vem PRIMEIRO: quando o mesmo tipo esta associado
    /// no usuario e na maquina, o que o usuario escolheu e o que vale — e o
    /// que o Windows abre.
    /// </summary>
    public static FileAssociatorReport Scan()
    {
        var items = new List<FileAssociationItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in Roots())
        {
            using var baseKey = RegistryKey.OpenBaseKey(root.Hive, root.View);
            using var classes = baseKey.OpenSubKey(ClassesRoot);
            if (classes is null) continue;

            foreach (var name in classes.GetSubKeyNames())
            {
                if (!name.StartsWith(".", StringComparison.Ordinal)) continue;
                if (!seen.Add(name)) continue;

                items.Add(ReadItem(name, root.Scope, root.View));
            }
        }

        return new FileAssociatorReport { Items = items };
    }

    /// <summary>
    /// As extensoes que nao tem programa associado.
    /// </summary>
    public static IReadOnlyList<FileAssociationItem> ListUnassociated() =>
        Scan().Items
            .Where(i => !i.HasProgram)
            .OrderBy(i => i.Extension, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// As extensoes cuja associacao aponta para um programa que nao existe
    /// mais.
    /// </summary>
    public static IReadOnlyList<FileAssociationItem> ListBroken() =>
        Scan().Items
            .Where(i => i.IsBroken)
            .OrderBy(i => i.Extension, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Define o programa que abre a extensao.
    ///
    /// A escrita e no perfil do usuario: e assim que o Windows faz, sem
    /// pedir administrador, e sem mexer no programa instalado.
    /// </summary>
    /// <param name="extension">A extensao, com ou sem o ponto.</param>
    /// <param name="executablePath">O caminho do programa.</param>
    /// <param name="programName">O nome do programa, para a mensagem.</param>
    /// <returns>True quando a associacao foi gravada.</returns>
    public static (bool Ok, string Message) SetDefault(
        string extension,
        string executablePath,
        string? programName = null)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return (false, "a extensao nao foi informada.");

        if (!extension.StartsWith(".", StringComparison.Ordinal)) extension = "." + extension;

        if (string.IsNullOrWhiteSpace(executablePath))
            return (false, "o programa nao foi informado.");

        if (!File.Exists(executablePath))
            return (false, $"o programa '{executablePath}' nao existe.");

        var progId = BuildProgId(executablePath);

        // O estado anterior e guardado ANTES da mudanca: e o que permite
        // desfazer. Sem isso, o desfazer seria uma promessa vazia.
        SaveState(extension, ReadState(extension));

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(ClassesRoot + "\\" + extension, writable: true);
            if (key is null) return (false, $"nao foi possivel abrir a chave de '{extension}'.");

            key.SetValue(string.Empty, progId, RegistryValueKind.String);

            using var icon = key.CreateSubKey("DefaultIcon", writable: true);
            icon?.SetValue(string.Empty, "\"" + executablePath + "\",0", RegistryValueKind.String);

            using var command = key.CreateSubKey(@"shell\open\command", writable: true);
            command?.SetValue(string.Empty, "\"" + executablePath + "\" \"%1\"", RegistryValueKind.String);
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "sem permissao para gravar a associacao.");
        }
        catch (IOException ex)
        {
            return (false, $"nao foi possivel gravar a associacao: {ex.Message}");
        }

        var nome = string.IsNullOrWhiteSpace(programName)
            ? Path.GetFileNameWithoutExtension(executablePath)
            : programName!;

        return (true, $"'{extension}' agora abre com {nome}.");
    }

    /// <summary>
    /// Desfaz a ultima associacao gravada pela extensao, devolvendo o estado
    /// anterior.
    /// </summary>
    /// <param name="extension">A extensao.</param>
    /// <returns>True quando o estado anterior foi restaurado.</returns>
    public static (bool Ok, string Message) Undo(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return (false, "a extensao nao foi informada.");
        if (!extension.StartsWith(".", StringComparison.Ordinal)) extension = "." + extension;

        var state = LoadStates().GetValueOrDefault(extension);
        if (state is null)
            return (false, $"nao ha alteracao guardada para '{extension}'.");

        try
        {
            using var classes = Registry.CurrentUser.OpenSubKey(ClassesRoot, writable: true);
            if (classes is null)
                return (false, "nao foi possivel abrir as associacoes do usuario.");

            if (!state.Existed)
            {
                // A chave nao existia antes: remover a chave devolve o
                // estado anterior (a associacao da maquina volta a valer).
                classes.DeleteSubKeyTree(extension, throwOnMissingSubKey: false);
            }
            else
            {
                using var key = classes.OpenSubKey(extension, writable: true);
                if (key is null)
                    return (false, $"a associacao de '{extension}' nao existe mais.");

                RestoreValue(key, string.Empty, state.HadDefault, state.DefaultValue);

                using var icon = key.OpenSubKey("DefaultIcon", writable: true);
                if (icon is not null)
                {
                    if (state.HadIcon) icon.SetValue(string.Empty, state.IconValue ?? string.Empty, RegistryValueKind.String);
                    else icon.DeleteValue(string.Empty, throwOnMissingValue: false);
                }

                using var command = key.OpenSubKey(@"shell\open\command", writable: true);
                if (command is not null)
                {
                    if (state.HadCommand) command.SetValue(string.Empty, state.CommandValue ?? string.Empty, RegistryValueKind.String);
                    else command.DeleteValue(string.Empty, throwOnMissingValue: false);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "sem permissao para desfazer a associacao.");
        }
        catch (IOException ex)
        {
            return (false, $"nao foi possivel desfazer a associacao: {ex.Message}");
        }

        var estados = LoadStates();
        estados.Remove(extension);
        SaveStates(estados);

        return (true, $"a associacao de '{extension}' voltou ao estado anterior.");
    }

    /// <summary>
    /// As extensoes que tem alteracao guardada (e que podem ser desfeitas).
    /// </summary>
    public static IReadOnlyList<string> PendingUndo() =>
        LoadStates().Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// O identificador do tipo criado para o programa. O nome vem do
    /// executavel, para duas versoes do mesmo programa nao colidirem.
    /// </summary>
    private static string BuildProgId(string executablePath)
    {
        var nome = Path.GetFileNameWithoutExtension(executablePath);
        var limpo = new string(nome.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        if (string.IsNullOrWhiteSpace(limpo)) limpo = "Programa";
        return "Tranal.Assoc." + limpo;
    }

    /// <summary>
    /// O comando de abertura do tipo, lido do registro.
    /// </summary>
    private static (string Command, string Description) ReadOpenCommand(string progId, RegistryView view)
    {
        if (string.IsNullOrWhiteSpace(progId)) return (string.Empty, string.Empty);

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, view);

            using var prog = baseKey.OpenSubKey(progId);
            var description = prog?.GetValue(string.Empty) as string ?? string.Empty;

            using var command = baseKey.OpenSubKey(progId + @"\shell\open\command");
            var texto = command?.GetValue(string.Empty) as string ?? string.Empty;

            return (texto, description);
        }
        catch (Exception)
        {
            return (string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// A escolha do usuario para a extensao (a chave <c>UserChoice</c>).
    /// E ela que o Windows respeita quando o usuario ja escolheu na mao.
    /// </summary>
    private static (string ProgId, string ProgramName) ReadUserChoice(string extension)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(UserChoiceRoot + "\\" + extension + @"\UserChoice");
            if (key is null) return (string.Empty, string.Empty);

            var progId = key.GetValue("ProgId") as string ?? string.Empty;
            var nome = key.GetValue("PSChildName") as string ?? string.Empty;

            return (progId, nome);
        }
        catch (Exception)
        {
            return (string.Empty, string.Empty);
        }
    }

    /// <summary>
    /// Le a associacao de uma extensao.
    /// </summary>
    private static FileAssociationItem ReadItem(string extension, string scope, RegistryView view)
    {
        var progId = string.Empty;
        var description = string.Empty;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(
                scope == "Usuario" ? RegistryHive.CurrentUser : RegistryHive.LocalMachine, view);
            using var key = baseKey.OpenSubKey(ClassesRoot + "\\" + extension);
            progId = key?.GetValue(string.Empty) as string ?? string.Empty;
        }
        catch (Exception)
        {
            // Uma chave malformada nao derruba a varredura inteira.
        }

        var (choice, choiceName) = ReadUserChoice(extension);
        var isUserChoice = !string.IsNullOrWhiteSpace(choice);
        if (isUserChoice) progId = choice;

        var (command, progDescription) = ReadOpenCommand(progId, view);

        var executable = ExtractExecutable(command);

        if (string.IsNullOrWhiteSpace(description) && !isUserChoice)
        {
            // A chave da extensao, quando o valor padrao nao e um ProgID, e
            // a descricao legivel do tipo ("Documento PDF").
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(
                    scope == "Usuario" ? RegistryHive.CurrentUser : RegistryHive.LocalMachine, view);
                using var key = baseKey.OpenSubKey(ClassesRoot + "\\" + extension);
                var valor = key?.GetValue(string.Empty) as string ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(valor) && valor.Contains(' ')) description = valor;
            }
            catch (Exception)
            {
                // Sem descricao legivel: a coluna fica vazia.
            }
        }

        if (string.IsNullOrWhiteSpace(description)) description = progDescription;

        var hasProgram = !string.IsNullOrWhiteSpace(executable) && File.Exists(executable);

        var programName = isUserChoice && !string.IsNullOrWhiteSpace(choiceName)
            ? choiceName
            : (hasProgram ? Path.GetFileNameWithoutExtension(executable) : string.Empty);

        return new FileAssociationItem
        {
            Extension = extension,
            Description = description,
            ProgId = progId,
            ProgramPath = executable,
            ProgramName = programName,
            Scope = scope,
            HasProgram = hasProgram,
            IsUserChoice = isUserChoice,
            IsBroken = !string.IsNullOrWhiteSpace(command) && !hasProgram,
        };
    }

    /// <summary>
    /// O executavel de um comando de abertura.
    ///
    /// O comando do Windows e uma linha de comando: o caminho vem entre
    /// aspas quando tem espaco. O caminho sem aspas vai ate o primeiro
    /// espaco.
    /// </summary>
    private static string ExtractExecutable(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return string.Empty;

        var texto = command.Trim();

        if (texto.StartsWith("\"", StringComparison.Ordinal))
        {
            var fim = texto.IndexOf('"', 1);
            if (fim > 1) return texto.Substring(1, fim - 1);
        }

        var primeiro = texto.Split(' ')[0];
        var ponto = primeiro.LastIndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return ponto > 0 ? primeiro.Substring(0, ponto + 4) : primeiro;
    }

    private readonly struct Root
    {
        public Root(RegistryHive hive, RegistryView view, string scope)
        {
            Hive = hive;
            View = view;
            Scope = scope;
        }

        public RegistryHive Hive { get; }
        public RegistryView View { get; }
        public string Scope { get; }
    }

    /// <summary>
    /// As raizes varridas, na ordem de prioridade: o que o usuario escolheu
    /// vem antes do que a maquina tem.
    /// </summary>
    private static IEnumerable<Root> Roots()
    {
        yield return new Root(RegistryHive.CurrentUser, RegistryView.Default, "Usuario");
        yield return new Root(RegistryHive.LocalMachine, RegistryView.Registry64, "Maquina");
        yield return new Root(RegistryHive.LocalMachine, RegistryView.Registry32, "Maquina");
    }

    /// <summary>
    /// O estado anterior de uma extensao, como o registro esta agora.
    /// </summary>
    private static AssociationState ReadState(string extension)
    {
        var state = new AssociationState { Extension = extension };

        try
        {
            using var classes = Registry.CurrentUser.OpenSubKey(ClassesRoot);
            using var key = classes?.OpenSubKey(extension);
            if (key is null) return state;

            state.Existed = true;

            var padrao = key.GetValue(string.Empty);
            state.HadDefault = padrao is not null;
            state.DefaultValue = padrao as string;

            using var icon = key.OpenSubKey("DefaultIcon");
            var valorIcone = icon?.GetValue(string.Empty);
            state.HadIcon = valorIcone is not null;
            state.IconValue = valorIcone as string;

            using var command = key.OpenSubKey(@"shell\open\command");
            var valorComando = command?.GetValue(string.Empty);
            state.HadCommand = valorComando is not null;
            state.CommandValue = valorComando as string;
        }
        catch (Exception)
        {
            // Sem leitura: o estado fica como "nao existia", que desfaz
            // apagando a chave — o mais seguro para um estado desconhecido.
        }

        return state;
    }

    private static void RestoreValue(RegistryKey key, string name, bool existed, string? value)
    {
        if (existed) key.SetValue(name, value ?? string.Empty, RegistryValueKind.String);
        else key.DeleteValue(name, throwOnMissingValue: false);
    }

    private static Dictionary<string, AssociationState> LoadStates()
    {
        var estados = new Dictionary<string, AssociationState>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (!File.Exists(BackupFile)) return estados;

            var json = File.ReadAllText(BackupFile);
            var lidos = JsonSerializer.Deserialize<Dictionary<string, AssociationState>>(json);
            if (lidos is null) return estados;

            foreach (var par in lidos) estados[par.Key] = par.Value;
        }
        catch (Exception)
        {
            // Estado corrompido nao impede a operacao: o desfazer simplesmente
            // nao tem o que restaurar.
        }

        return estados;
    }

    private static void SaveState(string extension, AssociationState state)
    {
        var estados = LoadStates();
        estados[extension] = state;
        SaveStates(estados);
    }

    private static void SaveStates(Dictionary<string, AssociationState> estados)
    {
        try
        {
            Directory.CreateDirectory(StateFolder);
            var json = JsonSerializer.Serialize(estados, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(BackupFile, json);
        }
        catch (Exception)
        {
            // Sem permissao para gravar: a associacao foi feita, mas o
            // desfazer nao tera o que restaurar. A operacao nao e perdida.
        }
    }

    /// <summary>
    /// O estado guardado de uma extensao.
    /// </summary>
    private sealed class AssociationState
    {
        public string Extension { get; set; } = string.Empty;
        public bool Existed { get; set; }
        public bool HadDefault { get; set; }
        public string? DefaultValue { get; set; }
        public bool HadIcon { get; set; }
        public string? IconValue { get; set; }
        public bool HadCommand { get; set; }
        public string? CommandValue { get; set; }
    }
}
