using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Uma ferramenta personalizada do usuario: o nome que aparece na lista e a
/// linha de comando que e executada.
///
/// E a mesma forma de uma ferramenta do catalogo
/// (<see cref="WindowsTool"/>): o que muda e que esta vem do usuario e
/// fica gravada em disco.
/// </summary>
public sealed class CustomTool
{
    /// <summary>O nome da ferramenta (unico).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>O executavel ou comando (ex.: "notepad.exe").</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>Os argumentos passados ao comando.</summary>
    public string Arguments { get; set; } = string.Empty;

    /// <summary>Uma descricao curta do que a ferramenta faz.</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Como terminou uma operacao sobre as ferramentas personalizadas.
/// </summary>
public enum CustomToolStatus
{
    /// <summary>A operacao foi concluida e gravada em disco.</summary>
    Ok,

    /// <summary>
    /// Ja existe uma ferramenta com esse nome (17484 do Revo: "A custom
    /// tool with the same name already exists.").
    /// </summary>
    AlreadyExists,

    /// <summary>A ferramenta indicada nao existe.</summary>
    NotFound,

    /// <summary>O nome informado e invalido (vazio).</summary>
    InvalidName,

    /// <summary>O comando informado e invalido (vazio).</summary>
    InvalidCommand,

    /// <summary>A gravacao no disco falhou.</summary>
    SaveFailed,
}

/// <summary>
/// O registro das ferramentas personalizadas (paridade com "Windows Tools"
/// do Revo, que aceita ferramentas do usuario ao lado das nativas).
///
/// As ferramentas ficam em um arquivo JSON na pasta de dados e o registro e
/// a **fonte unica**: quem lista, quem adiciona, quem renomeia e quem
/// remove leem e gravam a mesma estrutura.
/// </summary>
public static class CustomToolRegistry
{
    /// <summary>
    /// A mensagem mostrada quando o nome ja existe. E o texto do Revo
    /// traduzido — o usuario precisa saber *por que* a operacao foi recusada.
    /// </summary>
    public const string DuplicateNameMessage =
        "Uma ferramenta personalizada com o mesmo nome ja existe.";

    /// <summary>
    /// Caminho alternativo do arquivo. Existe para os testes: sem ele, um
    /// teste que grava ferramentas escreveria no arquivo real do usuario.
    /// Nulo em uso normal.
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    /// <summary>O caminho do arquivo que guarda as ferramentas.</summary>
    public static string FilePath =>
        FilePathOverride ?? Path.Combine(RegistryBackup.DataDirectory, "custom-tools.json");

    private static List<CustomTool>? _current;

    /// <summary>As ferramentas personalizadas, na ordem em que foram adicionadas.</summary>
    public static IReadOnlyList<CustomTool> List() => Current.ToList();

    /// <summary>
    /// As ferramentas personalizadas na mesma forma das ferramentas do
    /// catalogo, para a tela mostrar as duas listas juntas.
    /// </summary>
    public static IReadOnlyList<WindowsTool> ListAsWindowsTools() =>
        Current.Select(AsWindowsTool).ToList();

    /// <summary>
    /// Acrescenta uma ferramenta.
    ///
    /// O nome precisa ser unico: duas ferramentas com o mesmo nome na tela
    /// sao indistinguiveis para o usuario, e o Revo recusa a segunda
    /// (17484).
    /// </summary>
    /// <param name="tool">A ferramenta a acrescentar.</param>
    public static CustomToolStatus Add(CustomTool tool)
    {
        if (tool is null) return CustomToolStatus.InvalidName;

        var nome = (tool.Name ?? string.Empty).Trim();
        var comando = (tool.Command ?? string.Empty).Trim();

        if (nome.Length == 0) return CustomToolStatus.InvalidName;
        if (comando.Length == 0) return CustomToolStatus.InvalidCommand;

        if (Current.Any(t => string.Equals(t.Name, nome, StringComparison.OrdinalIgnoreCase)))
            return CustomToolStatus.AlreadyExists;

        var nova = new CustomTool
        {
            Name = nome,
            Command = comando,
            Arguments = (tool.Arguments ?? string.Empty).Trim(),
            Description = (tool.Description ?? string.Empty).Trim(),
        };

        Current.Add(nova);
        return Save() ? CustomToolStatus.Ok : Reverter(nova, CustomToolStatus.SaveFailed);
    }

    /// <summary>
    /// Renomeia uma ferramenta. O nome novo tambem precisa ser unico.
    /// </summary>
    /// <param name="oldName">O nome atual.</param>
    /// <param name="newName">O nome novo.</param>
    public static CustomToolStatus Rename(string oldName, string newName)
    {
        var de = (oldName ?? string.Empty).Trim();
        var para = (newName ?? string.Empty).Trim();

        if (de.Length == 0 || para.Length == 0) return CustomToolStatus.InvalidName;
        if (string.Equals(de, para, StringComparison.OrdinalIgnoreCase)) return CustomToolStatus.InvalidName;

        var indice = Current.FindIndex(t => string.Equals(t.Name, de, StringComparison.OrdinalIgnoreCase));
        if (indice < 0) return CustomToolStatus.NotFound;

        if (Current.Any(t => string.Equals(t.Name, para, StringComparison.OrdinalIgnoreCase)))
            return CustomToolStatus.AlreadyExists;

        var anterior = Current[indice].Name;
        Current[indice].Name = para;

        if (Save()) return CustomToolStatus.Ok;

        Current[indice].Name = anterior;
        return CustomToolStatus.SaveFailed;
    }

    /// <summary>
    /// Remove uma ferramenta.
    /// </summary>
    /// <param name="name">O nome da ferramenta.</param>
    public static CustomToolStatus Remove(string name)
    {
        var texto = (name ?? string.Empty).Trim();
        if (texto.Length == 0) return CustomToolStatus.InvalidName;

        var indice = Current.FindIndex(t => string.Equals(t.Name, texto, StringComparison.OrdinalIgnoreCase));
        if (indice < 0) return CustomToolStatus.NotFound;

        var removida = Current[indice];
        Current.RemoveAt(indice);

        if (Save()) return CustomToolStatus.Ok;

        Current.Insert(indice, removida);
        return CustomToolStatus.SaveFailed;
    }

    /// <summary>
    /// A ferramenta com o nome indicado, quando existe.
    /// </summary>
    /// <param name="name">O nome da ferramenta.</param>
    /// <param name="tool">A ferramenta encontrada.</param>
    public static bool TryGet(string name, out CustomTool tool)
    {
        var texto = (name ?? string.Empty).Trim();
        tool = Current.FirstOrDefault(t => string.Equals(t.Name, texto, StringComparison.OrdinalIgnoreCase))!;
        return tool is not null;
    }

    /// <summary>
    /// Executa a ferramenta pelo shell do Windows, como as ferramentas do
    /// catalogo.
    /// </summary>
    /// <param name="tool">A ferramenta.</param>
    public static bool Run(CustomTool tool)
    {
        if (tool is null) return false;

        return WindowsToolsCatalog.Run(AsWindowsTool(tool));
    }

    /// <summary>
    /// Converte a ferramenta personalizada na mesma forma das ferramentas do
    /// catalogo.
    /// </summary>
    /// <param name="tool">A ferramenta personalizada.</param>
    public static WindowsTool AsWindowsTool(CustomTool tool) => new()
    {
        Name = tool.Name,
        CategoryId = "custom",
        Command = tool.Command,
        Arguments = tool.Arguments,
        Description = tool.Description,
    };

    /// <summary>Recarrega o registro do disco.</summary>
    public static void Reload() => _current = Load();

    /// <summary>
    /// Esvazia o registro sem tocar no arquivo de quem roda o teste.
    /// </summary>
    internal static void ResetForTests()
    {
        var vazio = Path.Combine(Path.GetTempPath(),
            "tranal-sem-arquivo-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        FilePathOverride = vazio;
        Reload();
        FilePathOverride = null;
    }

    private static List<CustomTool> Current => _current ??= Load();

    /// <summary>
    /// Le o arquivo do registro. Um arquivo corrompido nao impede o
    /// aplicativo de funcionar: ele e descartado e o registro comeca
    /// vazio.
    /// </summary>
    private static List<CustomTool> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<CustomTool>();

            var json = File.ReadAllText(FilePath);
            var dados = JsonSerializer.Deserialize<List<CustomTool>>(json);
            if (dados is null) return new List<CustomTool>();

            // Entradas nulas ou sem nome quebrariam a tela: sao descartadas.
            return dados
                .Where(t => t is not null && !string.IsNullOrWhiteSpace(t.Name))
                .ToList();
        }
        catch
        {
            return new List<CustomTool>();
        }
    }

    /// <summary>
    /// Grava o registro. A escrita e atomica: o conteudo vai para um
    /// arquivo temporario e so entao substitui o original — um arquivo pela
    /// metade apagaria as ferramentas do usuario.
    /// </summary>
    private static bool Save()
    {
        var temporario = FilePath + ".tmp";

        try
        {
            var pasta = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });

            File.WriteAllText(temporario, json, new UTF8Encoding(false));

            if (File.Exists(FilePath)) File.Move(temporario, FilePath, true);
            else File.Move(temporario, FilePath);

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            try { if (File.Exists(temporario)) File.Delete(temporario); }
            catch { /* o temporario e sobrescrito no proximo Save() */ }
        }
    }

    /// <summary>
    /// Desfaz a mudanca na memoria quando a gravacao falhou: sem isso a
    /// tela mostraria a ferramenta nova como se ela estivesse em disco.
    /// </summary>
    private static CustomToolStatus Reverter(CustomTool tool, CustomToolStatus status)
    {
        Current.RemoveAll(t => ReferenceEquals(t, tool));
        return status;
    }
}
