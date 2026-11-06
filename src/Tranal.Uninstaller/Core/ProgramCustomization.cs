using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tranal.Uninstaller.Core.Appearance;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// O que o usuario guardou sobre cada programa: comentario, icone e grupos.
///
/// O Revo oferece "Add Comment" (15216), "Edit Comment" (15217), "Change
/// Icon" (15079), "Add to Group" (15080) e "Remove from Group" (15081), e
/// pergunta no fim da sessao se as preferencias devem ser mantidas (15364:
/// "Do you want to keep your preferences?\nIcons, comments, groups, etc.").
///
/// No Tranal a resposta e sempre sim: as preferencias ficam em disco, em
/// program-data.json.
/// </summary>
public sealed class ProgramCustomization
{
    /// <summary>
    /// Identidade estavel do programa. E o caminho da chave de desinstalacao
    /// (o GUID), que sobrevive a mudanca de versao e de nome de pasta; quando
    /// o programa nao vem do registro, cai para o nome exibido.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Comentario do usuario sobre o programa (15281: "Enter your comment:").
    /// Vazio quando o usuario nao escreveu nada.
    /// </summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Indice do icone escolhido na galeria (15240: "Icon index").
    /// -1 significa "nenhum icone escolhido" — o icone do proprio programa
    /// continua valendo.
    /// </summary>
    public int IconIndex { get; set; } = -1;

    /// <summary>
    /// Caminho de um arquivo de icone escolhido pelo usuario (15283:
    /// "File path:"). Vazio quando o icone veio da galeria.
    /// </summary>
    public string IconPath { get; set; } = string.Empty;

    /// <summary>Grupos a que o programa pertence (15080 e 15081).</summary>
    public List<string> Groups { get; set; } = new();

    /// <summary>
    /// A classificacao do usuario sobre o programa, de 0 a 5 estrelas.
    /// Zero significa "sem classificacao" — e nao "classificado como zero".
    ///
    /// A regra da escala (quantas estrelas, como o valor fora da faixa e
    /// tratado e como a estrela e desenhada) mora em
    /// <see cref="ProgramRating"/>; aqui mora so o dado.
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// Comando que roda **antes** do desinstalador nativo.
    ///
    /// Serve para derrubar o programa, fechar o banco de dados ou liberar o
    /// arquivo antes que o desinstalador tente apaga-lo.
    /// </summary>
    public string PreCommand { get; set; } = string.Empty;

    /// <summary>
    /// Comando que roda **depois** da desinstalacao.
    ///
    /// Serve para refazer o que o desinstalador do programa desfez.
    /// </summary>
    public string PostCommand { get; set; } = string.Empty;
}

/// <summary>
/// Resultado de uma operacao sobre grupos.
///
/// A janela precisa distinguir "o nome ja existe" de "nao deu para gravar":
/// as duas coisas devolveviam <c>false</c>, e a janela tratava as duas como
/// conflito — o usuario recebia "ja existe um grupo com esse nome" ao tentar
/// apagar um grupo, e nunca ficava sabendo que a gravacao tinha falhado.
/// </summary>
public enum GroupResult
{
    /// <summary>Operacao concluida e gravada em disco.</summary>
    Ok,

    /// <summary>O nome pedido ja existe.</summary>
    AlreadyExists,

    /// <summary>O grupo indicado nao existe.</summary>
    NotFound,

    /// <summary>O nome informado e invalido (vazio).</summary>
    InvalidName,

    /// <summary>A gravacao no disco falhou.</summary>
    SaveFailed,
}

/// <summary>
/// Como a carga das preferencias terminou.
///
/// "JSON valido" nao e o mesmo que "JSON utilizavel": um arquivo com
/// <c>{"Programs": null}</c> deserializa sem erro e mesmo assim inutiliza a
/// lista inteira. A interface precisa saber a diferenca — um arquivo
/// preservado e um arquivo que nao deu para ler sao situacoes diferentes.
/// </summary>
public enum LoadState
{
    /// <summary>Arquivo ausente (primeira execucao) ou carregado normalmente.</summary>
    Ok,

    /// <summary>O arquivo era invalido e foi preservado como ".corrompido".</summary>
    Preserved,

    /// <summary>Nao deu para ler o arquivo; nada foi alterado.</summary>
    Unreadable,
}

/// <summary>
/// As preferencias do usuario por programa, em disco.
///
/// Um arquivo so (program-data.json) com os nomes dos grupos e as anotacoes
/// de cada programa: e a "regra fica em um lugar so" — quem le a lista de
/// grupos, quem le o comentario de um programa e quem renomeia um grupo
/// leem a MESMA estrutura.
/// </summary>
public static class ProgramCustomizationStore
{
    private sealed class StoreData
    {
        public List<string> Groups { get; set; } = new();
        public List<ProgramCustomization> Programs { get; set; } = new();
    }

    /// <summary>
    /// Caminho do arquivo. Existe para os testes: sem ele, um teste que
    /// mexe nas preferencias escreveria no arquivo real do usuario.
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    /// <summary>Caminho do arquivo que guarda as preferencias.</summary>
    public static string FilePath =>
        FilePathOverride ?? Path.Combine(RegistryBackup.DataDirectory, "program-data.json");

    private static StoreData? _current;

    /// <summary>Como a ultima carga terminou. A interface usa para avisar o usuario.</summary>
    public static LoadState LastLoad { get; private set; } = LoadState.Ok;

    private static StoreData Current => _current ??= Load();

    /// <summary>
    /// A identidade estavel de um programa. E a chave usada para casar o
    /// programa com a anotacao do usuario.
    ///
    /// A visao do registro (32 ou 64 bits) entra na chave: a mesma subchave
    /// pode existir nas duas visoes, e sao entradas diferentes quando a
    /// versao diverge. Sem a visao, a anotacao de uma poderia aparecer no
    /// outro. A visao entra tambem na chave de reserva (programa sem chave
    /// de registro), pelo mesmo motivo.
    /// </summary>
    public static string KeyOf(InstalledProgram program)
    {
        if (!string.IsNullOrWhiteSpace(program.RegistryPath))
            return program.RegistryPath + "|" + program.View;

        if (!string.IsNullOrWhiteSpace(program.RegistryKey))
            return program.RegistryKey + "|" + program.View;

        return program.DisplayName.Trim() + "|" + program.View;
    }

    /// <summary>Os nomes dos grupos que o usuario criou, na ordem em que aparecem.</summary>
    public static IReadOnlyList<string> Groups => Current.Groups;

    /// <summary>
    /// A anotacao de um programa. Devolve uma anotacao vazia (e NAO a
    /// guarda) quando o programa ainda nao foi anotado.
    /// </summary>
    public static ProgramCustomization For(InstalledProgram program)
    {
        var key = KeyOf(program);
        foreach (var item in Current.Programs)
        {
            if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) return item;
        }

        return new ProgramCustomization { Key = key };
    }

    /// <summary>True quando o programa ja tem anotacao salva.</summary>
    public static bool Has(InstalledProgram program)
    {
        var key = KeyOf(program);
        return Current.Programs.Any(i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// As anotacoes do usuario, lidas direto do disco, sem tocar em nada.
    ///
    /// A exportacao e uma operacao de leitura: ela nao pode renomear o
    /// arquivo corrompido do usuario nem disparar a preservacao. Por isso
    /// le o arquivo por conta propria, em vez de usar a loja em memoria.
    /// </summary>
    public static IReadOnlyList<ProgramCustomization> PeekAll()
    {
        try
        {
            if (!File.Exists(FilePath)) return Array.Empty<ProgramCustomization>();

            var json = File.ReadAllText(FilePath);
            var data = JsonSerializer.Deserialize<StoreData>(json);
            if (!IsUsable(data)) return Array.Empty<ProgramCustomization>();

            return data!.Programs;
        }
        catch
        {
            // Arquivo invalido ou travado: a exportacao segue sem as
            // anotacoes, mas nao mexe no arquivo do usuario.
            return Array.Empty<ProgramCustomization>();
        }
    }

    /// <summary>
    /// Guarda (ou apaga) o comentario do usuario. Comentario vazio remove a
    /// anotacao: sem comentario nao ha anotacao.
    /// </summary>
    public static bool SetComment(InstalledProgram program, string comment)
    {
        var text = (comment ?? string.Empty).Trim();
        var item = FindOrCreate(program);

        if (text.Length == 0)
        {
            item.Comment = string.Empty;
            Prune(item);
        }
        else
        {
            item.Comment = text;
        }

        return Save();
    }

    /// <summary>
    /// Guarda (ou apaga) os comandos que o usuario mandou rodar **antes** e
    /// **depois** da desinstalacao do programa.
    ///
    /// Comando vazio remove o comando. Sem nenhum dos dois, a anotacao e
    /// podada: um programa sem comando nao tem o que guardar.
    /// </summary>
    /// <param name="program">O programa.</param>
    /// <param name="preCommand">O comando que roda antes do desinstalador.</param>
    /// <param name="postCommand">O comando que roda depois da desinstalacao.</param>
    public static bool SetCommands(InstalledProgram program, string preCommand, string postCommand)
    {
        var pre = (preCommand ?? string.Empty).Trim();
        var post = (postCommand ?? string.Empty).Trim();
        var item = FindOrCreate(program);

        item.PreCommand = pre;
        item.PostCommand = post;

        if (pre.Length == 0 && post.Length == 0) Prune(item);

        return Save();
    }

    /// <summary>Escolhe o icone do programa (indice da galeria ou caminho de arquivo).</summary>
    public static bool SetIcon(InstalledProgram program, int iconIndex, string iconPath)
    {
        var item = FindOrCreate(program);
        item.IconIndex = iconIndex;
        item.IconPath = (iconPath ?? string.Empty).Trim();

        if (item.IconIndex < 0 && item.IconPath.Length == 0) Prune(item);
        return Save();
    }

    /// <summary>Coloca o programa em um grupo (15080). Devolve false se o grupo nao existe.</summary>
    public static bool AddToGroup(InstalledProgram program, string group)
    {
        var name = (group ?? string.Empty).Trim();
        if (name.Length == 0) return false;
        if (!Current.Groups.Any(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase))) return false;

        var item = FindOrCreate(program);
        if (!item.Groups.Any(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase)))
            item.Groups.Add(name);

        return Save();
    }

    /// <summary>
    /// Tira o programa de um grupo (15081). Devolve false quando a gravacao
    /// falhou.
    /// </summary>
    public static bool RemoveFromGroup(InstalledProgram program, string group)
    {
        var name = (group ?? string.Empty).Trim();
        var item = FindOrCreate(program);
        item.Groups.RemoveAll(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase));
        Prune(item);
        return Save();
    }

    /// <summary>
    /// Cria um grupo (15173: "Add new Group").
    ///
    /// O resultado distingue o conflito de nome da falha de gravacao: a
    /// janela precisa mostrar mensagens diferentes para as duas coisas.
    /// </summary>
    public static GroupResult CreateGroup(string name)
    {
        var text = (name ?? string.Empty).Trim();
        if (text.Length == 0) return GroupResult.InvalidName;
        if (Current.Groups.Any(g => string.Equals(g, text, StringComparison.OrdinalIgnoreCase)))
            return GroupResult.AlreadyExists;

        Current.Groups.Add(text);
        if (Save()) return GroupResult.Ok;

        // A gravacao falhou: o grupo nao existe em disco, entao nao pode
        // ficar na memoria como se tivesse sido criado.
        Current.Groups.Remove(text);
        return GroupResult.SaveFailed;
    }

    /// <summary>
    /// Renomeia um grupo. Os programas que estavam nele acompanham a
    /// mudanca — um grupo renomeado nao pode deixar os programas orfaos.
    ///
    /// Quando a gravacao falha, a mudanca e desfeita na memoria: sem isso a
    /// tela mostraria o nome novo enquanto o disco ainda teria o antigo.
    /// </summary>
    public static GroupResult RenameGroup(string oldName, string newName)
    {
        var from = (oldName ?? string.Empty).Trim();
        var to = (newName ?? string.Empty).Trim();
        if (from.Length == 0 || to.Length == 0) return GroupResult.InvalidName;
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return GroupResult.InvalidName;
        if (Current.Groups.Any(g => string.Equals(g, to, StringComparison.OrdinalIgnoreCase)))
            return GroupResult.AlreadyExists;

        var index = Current.Groups.FindIndex(g => string.Equals(g, from, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return GroupResult.NotFound;

        Current.Groups[index] = to;
        var trocados = new List<ProgramCustomization>();
        foreach (var item in Current.Programs)
        {
            for (var i = 0; i < item.Groups.Count; i++)
            {
                if (string.Equals(item.Groups[i], from, StringComparison.OrdinalIgnoreCase))
                {
                    item.Groups[i] = to;
                    trocados.Add(item);
                }
            }
        }

        if (Save()) return GroupResult.Ok;

        // A gravacao falhou: desfaz a mudanca na memoria.
        Current.Groups[index] = from;
        foreach (var item in trocados)
        {
            for (var i = 0; i < item.Groups.Count; i++)
            {
                if (string.Equals(item.Groups[i], to, StringComparison.OrdinalIgnoreCase))
                    item.Groups[i] = from;
            }
        }

        return GroupResult.SaveFailed;
    }

    /// <summary>
    /// Apaga um grupo. Os programas que estavam nele NAO saem de campo: o
    /// grupo e so uma classificacao, e o programa continua instalado.
    ///
    /// Quando a gravacao falha, a mudanca e desfeita na memoria.
    /// </summary>
    public static GroupResult DeleteGroup(string name)
    {
        var text = (name ?? string.Empty).Trim();
        var index = Current.Groups.FindIndex(g => string.Equals(g, text, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return GroupResult.NotFound;

        var removido = Current.Groups[index];
        Current.Groups.RemoveAt(index);

        var atingidos = new List<ProgramCustomization>();
        foreach (var item in Current.Programs)
        {
            if (!item.Groups.Any(g => string.Equals(g, text, StringComparison.OrdinalIgnoreCase))) continue;
            item.Groups.RemoveAll(g => string.Equals(g, text, StringComparison.OrdinalIgnoreCase));
            atingidos.Add(item);
        }

        if (Save()) return GroupResult.Ok;

        // A gravacao falhou: desfaz a mudanca na memoria.
        Current.Groups.Insert(index, removido);
        foreach (var item in atingidos)
            item.Groups.Add(text);

        return GroupResult.SaveFailed;
    }

    /// <summary>
    /// Guarda a classificacao do usuario (estrelas) do programa.
    ///
    /// A gravacao e a mesma das outras preferencias: arquivo temporario e
    /// troca — um "program-data.json" pela metade apagaria a classificacao
    /// do usuario junto com o resto. O valor gravado **sobrescreve** o
    /// anterior, e zero estrelas apaga a classificacao.
    ///
    /// Devolve false quando a gravacao falhou: quem chama precisa avisar o
    /// usuario, senao a estrela some quando o aplicativo fecha.
    /// </summary>
    /// <param name="program">O programa a classificar.</param>
    /// <param name="rating">A classificacao, de 0 a 5 estrelas.</param>
    public static bool SetRating(InstalledProgram program, int rating)
    {
        var item = FindOrCreate(program);
        item.Rating = ProgramRating.Clamp(rating);

        if (item.Rating == 0) Prune(item);

        return Save();
    }

    /// <summary>
    /// A classificacao do usuario sobre o programa. Zero quando o usuario
    /// ainda nao classificou.
    /// </summary>
    /// <param name="program">O programa instalado.</param>
    public static int RatingOf(InstalledProgram program) => For(program).Rating;

    /// <summary>
    /// Grava as preferencias no disco. Devolve false quando nao deu para
    /// gravar.
    ///
    /// A escrita e atomica: o conteudo vai para um arquivo temporario e so
    /// entao substitui o original. Uma queda no meio da gravacao deixaria o
    /// arquivo pela metade — e a proxima gravacao apagaria de vez os
    /// comentarios e grupos do usuario.
    /// </summary>
    public static bool Save()
    {
        var temporario = FilePath + ".tmp";
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() },
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });

            File.WriteAllText(temporario, json);

            // File.Replace e atomico e guarda a versao anterior em .bak; quando
            // nao ha versao anterior (primeira gravacao), Move substitui.
            if (File.Exists(FilePath))
            {
                try { File.Replace(temporario, FilePath, FilePath + ".bak", true); }
                catch (PlatformNotSupportedException) { ReplaceWithBackup(temporario); }
                catch (IOException) { ReplaceWithBackup(temporario); }
            }
            else
            {
                File.Move(temporario, FilePath);
            }

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            // O temporario nao pode sobrar na pasta de dados: ele carrega os
            // dados do usuario em texto claro e ninguem sabe que ele existe.
            try { if (File.Exists(temporario)) File.Delete(temporario); }
            catch { /* o arquivo temporario fica para o proximo Save() sobrescrever */ }
        }
    }

    /// <summary>
    /// Substitui o arquivo quando <c>File.Replace</c> nao esta disponivel
    /// (volume sem suporte a ReplaceFile: FAT32, exFAT, rede).
    ///
    /// A copia para .bak vem ANTES da substituicao: sem ela o fallback
    /// sobrescreveria o arquivo sem deixar copia da versao anterior — e o
    /// ".bak" prometido na documentacao nao existiria nesse caminho. Se a
    /// copia falhar, a gravacao e abortada: e melhor avisar do que fingir
    /// que houve backup.
    /// </summary>
    private static void ReplaceWithBackup(string temporario)
    {
        File.Copy(FilePath, FilePath + ".bak", true);
        File.Move(temporario, FilePath, true);
    }

    /// <summary>
    /// Le as preferencias do disco.
    ///
    /// Um arquivo corrompido NAO e sobrescrito: ele e renomeado para
    /// "program-data.json.corrompido" e o aplicativo comeca vazio. O
    /// usuario ainda tem o arquivo original para recuperar — perder comentarios
    /// e grupos sem aviso nao e aceitavel.
    ///
    /// Um arquivo que nao deu para LER (aberto por outro processo) e outra
    /// coisa: ele nao esta corrompido e nao e renomeado. Ele fica no lugar,
    /// e a proxima gravacao atomica guarda a versao anterior em ".bak".
    /// </summary>
    private static StoreData Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                LastLoad = LoadState.Ok;
                return new StoreData();
            }

            var json = File.ReadAllText(FilePath);
            var data = JsonSerializer.Deserialize<StoreData>(json);

            // JSON valido nao e o mesmo que JSON utilizavel: uma lista nula
            // ({"Programs": null}) deserializa sem erro e mesmo assim
            // inutilizaria a lista inteira — com o arquivo do usuario
            // sobrescrito na proxima gravacao. Suches arquivos sao
            // tratados como corrompidos.
            if (IsUsable(data))
            {
                LastLoad = LoadState.Ok;
                return data!;
            }
        }
        catch (IOException)
        {
            // Nao deu para ler o arquivo (ele esta aberto por outro
            // processo, por exemplo). Ele nao esta corrompido: nao deve ser
            // renomeado, e sim avisado.
            LastLoad = LoadState.Unreadable;
            return new StoreData();
        }
        catch
        {
            // JSON ilegivel: cai no bloco de baixo, o arquivo e preservado.
        }

        LastLoad = PreserveCorruptedFile() ? LoadState.Preserved : LoadState.Unreadable;
        return new StoreData();
    }

    /// <summary>
    /// O conteudo carregado serve para alguma coisa?
    ///
    /// Uma lista nula e um item nulo na lista deserializam sem erro e
    /// estouram em <see cref="Has"/> e <see cref="For"/>. Um arquivo assim
    /// e inutilizavel, e inutilizavel e diferente de ilegivel: o arquivo do
    /// usuario e preservado, do mesmo jeito.
    /// </summary>
    private static bool IsUsable(StoreData? data)
    {
        if (data is null) return false;
        if (data.Groups is null || data.Programs is null) return false;

        foreach (var item in data.Programs)
        {
            if (item is null) return false;
            if (item.Groups is null) return false;
        }

        return true;
    }

    /// <summary>
    /// Guarda o arquivo ilegivel ao lado do original, para o usuario poder
    /// recuperar o conteudo. A gravacao seguinte cria um arquivo novo, sem
    /// tocar no original.
    ///
    /// Devolve true quando o arquivo foi mesmo preservado.
    /// </summary>
    private static bool PreserveCorruptedFile()
    {
        try
        {
            if (!File.Exists(FilePath)) return true;

            var destino = FilePath + ".corrompido";
            var contador = 1;
            while (File.Exists(destino) && contador < 100)
            {
                destino = $"{FilePath}.corrompido.{contador}";
                contador++;
            }

            File.Move(FilePath, destino);
            return true;
        }
        catch
        {
            // Nao deu para preservar (o arquivo esta aberto por outro
            // processo, por exemplo). Ele fica como esta, e a proxima
            // gravacao atomica guarda a versao anterior em ".bak".
            return false;
        }
    }

    /// <summary>Recarrega as preferencias do disco.</summary>
    public static void Reload() => _current = Load();

    /// <summary>
    /// Esvazia a loja sem tocar no arquivo de quem roda o teste.
    ///
    /// Um <c>Reload()</c> com o override nulo leria o program-data.json real
    /// do usuario — e, se ele estivesse corrompido, o renomearia para
    /// ".corrompido". A suite nao pode mexer no ambiente de quem a roda.
    /// </summary>
    internal static void ResetForTests()
    {
        var vazio = Path.Combine(Path.GetTempPath(),
            "tranal-sem-arquivo-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json");
        FilePathOverride = vazio;
        Reload();
        FilePathOverride = null;
    }

    private static ProgramCustomization FindOrCreate(InstalledProgram program)
    {
        var key = KeyOf(program);
        foreach (var item in Current.Programs)
        {
            if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) return item;
        }

        var created = new ProgramCustomization { Key = key };
        Current.Programs.Add(created);
        return created;
    }

    /// <summary>
    /// Remove a anotacao quando ela ficou sem nada: um programa sem
    /// comentario, sem icone, sem grupo, sem classificacao e sem comando
    /// nao tem o que ser anotado.
    /// </summary>
    private static void Prune(ProgramCustomization item)
    {
        if (item.Comment.Length == 0
            && item.IconPath.Length == 0
            && item.IconIndex < 0
            && item.Groups.Count == 0
            && item.Rating == 0
            && item.PreCommand.Length == 0
            && item.PostCommand.Length == 0)
        {
            Current.Programs.Remove(item);
        }
    }
}
