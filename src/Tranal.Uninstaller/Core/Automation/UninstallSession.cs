using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// A retomada de uma desinstalação interrompida.
///
/// Uma desinstalação pode ser interrompida no meio: o Windows reinicia, o
/// programa trava, a tomada cai. Quando isso acontece, o trabalho feito
/// até ali não pode ser jogado fora — e o trabalho que faltava não pode ser
/// repetido do começo.
///
/// A sessão é o que guarda essa continuidade: um arquivo por desinstalação,
/// com o plano e o número do último passo concluído. Ela é gravada a cada
/// etapa, de forma atômica (arquivo temporário e troca), porque um arquivo
/// pela metade tiraria do usuário justamente a retomada que ele precisa.
/// </summary>
public sealed class UninstallSession
{
    /// <summary>
    /// A identidade da sessão. É também o nome do arquivo em disco, e por
    /// isso só tem letras, números, hífen e sublinhado.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>O nome do programa que está sendo desinstalado.</summary>
    public string ProgramName { get; init; } = string.Empty;

    /// <summary>
    /// O plano da operação. Ele é gravado junto com a sessão para que a
    /// retomada use exatamente o mesmo plano — e não um plano reconstruído,
    /// que poderia ter etapas diferentes.
    /// </summary>
    public UninstallPlan Plan { get; init; } = new();

    /// <summary>
    /// O número do último passo concluído com sucesso. Menos zero quando
    /// nada foi concluído ainda. A retomada começa pelo passo seguinte.
    /// </summary>
    public int LastCompletedStep { get; init; } = -1;

    /// <summary>
    /// A carga da sessão: o que ela carrega além do plano.
    ///
    /// Para uma desinstalação, a carga é a lista de itens que ainda precisam
    /// sair do computador (ver <see cref="RemovalPayload"/>). Para uma sessão
    /// de plano puro, a carga é vazia.
    /// </summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>
    /// Onde esta sessão está gravada. Existe para o usuário (e para a linha
    /// de comando) saber onde procurar a desinstalação interrompida.
    /// </summary>
    [JsonIgnore]
    public string FilePath => Path.Combine(SessionDirectory, Id + ".json");

    /// <summary>
    /// A pasta onde as sessões pendentes ficam gravadas.
    ///
    /// Fica ao lado dos outros dados do Tranal (%LOCALAPPDATA%), e não na
    /// pasta do programa: a sessão precisa sobreviver à desinstalação do
    /// programa que ela descreve.
    /// </summary>
    public static string SessionDirectory =>
        DirectoryOverride ?? Path.Combine(RegistryBackup.DataDirectory, "sessions");

    /// <summary>
    /// Redireciona a pasta das sessões. Existe para os testes: sem ele, um
    /// teste escreveria no %LOCALAPPDATA% de quem está rodando a suíte.
    /// </summary>
    internal static string? DirectoryOverride { get; set; }

    /// <summary>
    /// Cria uma sessão nova para o plano informado, com identidade própria.
    /// </summary>
    /// <param name="plan">O plano que a sessão vai acompanhar.</param>
    /// <param name="programName">O nome do programa (padrão: o do plano).</param>
    public static UninstallSession Start(UninstallPlan plan, string? programName = null)
    {
        var name = string.IsNullOrWhiteSpace(programName) ? plan.ProgramName : programName;

        return new UninstallSession
        {
            Id = NewId(name),
            ProgramName = name,
            Plan = plan,
            LastCompletedStep = -1,
        };
    }

    /// <summary>
    /// Uma cópia desta sessão com o passo concluído atualizado.
    ///
    /// A sessão é imutável de propósito: o executor guarda a última versão
    /// boa e só avança quando a etapa deu certo.
    /// </summary>
    /// <param name="index">O número do passo concluído.</param>
    public UninstallSession WithCompletedStep(int index) => new()
    {
        Id = Id,
        ProgramName = ProgramName,
        Plan = Plan,
        LastCompletedStep = index,
        Payload = Payload,
    };

    /// <summary>
    /// Uma cópia desta sessão com a carga trocada.
    ///
    /// </summary>
    /// <param name="payload">A carga, em JSON.</param>
    public UninstallSession WithPayload(string payload) => new()
    {
        Id = Id,
        ProgramName = ProgramName,
        Plan = Plan,
        LastCompletedStep = LastCompletedStep,
        Payload = payload,
    };

    /// <summary>
    /// True quando o plano inteiro já foi concluído: não há mais passo
    /// nenhum para retomar.
    /// </summary>
    public bool IsFinished => LastCompletedStep >= Plan.Steps.Count - 1;

    /// <summary>
    /// As opções de leitura e de gravação da sessão.
    ///
    /// As mesmas opções valem para os dois lados: o enum é gravado pelo nome
    /// (<c>PreCommand</c>), e uma leitura sem o conversor não reconheceria
    /// o nome — a sessão voltaria vazia, e a retomada perderia o plano.
    /// </summary>
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Grava a sessão em disco.
    ///
    /// A gravação é atômica: o conteúdo vai para um arquivo temporário e só
    /// então substitui o original. Um corte de energia no meio deixaria o
    /// arquivo pela metade — e a próxima gravação apagaria de vez o passo
    /// concluído, fazendo o Tranal repetir (ou pular) trabalho.
    /// </summary>
    /// <param name="session">A sessão a gravar.</param>
    /// <returns>A sessão gravada.</returns>
    public static UninstallSession Save(UninstallSession session)
    {
        if (session is null) throw new ArgumentNullException(nameof(session));

        var path = session.FilePath;
        var temporario = path + ".tmp";

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(session, Opcoes);

            File.WriteAllText(temporario, json);

            // File.Replace e atomico e guarda a versao anterior em .bak;
            // quando nao ha versao anterior (primeira gravacao), Move
            // substitui.
            if (File.Exists(path))
            {
                try { File.Replace(temporario, path, path + ".bak", true); }
                catch (PlatformNotSupportedException) { ReplaceWithBackup(temporario, path); }
                catch (IOException) { ReplaceWithBackup(temporario, path); }
            }
            else
            {
                File.Move(temporario, path);
            }
        }
        catch
        {
            // Falhou a gravacao: a sessao anterior (que existe em disco)
            // continua valendo. Perder a sessao e perder a retomada; nao
            // perder a sessao e perder so este passo.
        }
        finally
        {
            // O temporario nao pode sobrar na pasta de dados.
            try { if (File.Exists(temporario)) File.Delete(temporario); }
            catch { /* o arquivo temporario fica para a proxima gravacao sobrescrever */ }
        }

        return session;
    }

    /// <summary>
    /// As sessões que ainda têm trabalho pela frente.
    ///
    /// Uma sessão concluída (todas as etapas feitas) não aparece: ela não
    /// precisa de retomada, e encher a lista de coisas prontas só faria o
    /// usuário duvidar do que está pendente de verdade.
    /// </summary>
    public static IReadOnlyList<UninstallSession> Pending()
    {
        var dir = SessionDirectory;
        var result = new List<UninstallSession>();

        string[] arquivos;
        try
        {
            if (!Directory.Exists(dir)) return result;
            arquivos = Directory.GetFiles(dir, "*.json");
        }
        catch
        {
            // Pasta inacessivel: nao ha sessao a mostrar, e a operacao
            // corrente nao pode falhar por causa disso.
            return result;
        }

        foreach (var arquivo in arquivos)
        {
            var id = Path.GetFileNameWithoutExtension(arquivo);
            var session = Load(id);
            if (session is null) continue;
            if (session.IsFinished) continue;
            result.Add(session);
        }

        return result;
    }

    /// <summary>
    /// Carrega a sessão do identificador informado.
    /// Devolve <c>null</c> quando não existe ou quando o arquivo não dá para
    /// ler — uma sessão ilegível não pode derrubar a listagem.
    /// </summary>
    /// <param name="id">A identidade da sessão.</param>
    public static UninstallSession? Load(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        var path = Path.Combine(SessionDirectory, id + ".json");
        try
        {
            if (!File.Exists(path)) return null;

            var json = File.ReadAllText(path);
            var session = JsonSerializer.Deserialize<UninstallSession>(json, Opcoes);
            if (session is null) return null;
            if (string.IsNullOrWhiteSpace(session.Id)) session = new UninstallSession
            {
                Id = id,
                ProgramName = session.ProgramName,
                Plan = session.Plan,
                LastCompletedStep = session.LastCompletedStep,
                Payload = session.Payload,
            };

            return session;
        }
        catch
        {
            // Arquivo ilegivel ou session em JSON invalido: a sessao e
            // descartada, e o trabalho recomeca do começo. Preservar um
            // arquivo pela metade seria pior.
            return null;
        }
    }

    /// <summary>
    /// Apaga a sessão informada. Devolve <c>true</c> quando o arquivo
    /// existia e deixou de existir.
    /// </summary>
    /// <param name="id">A identidade da sessão.</param>
    public static bool Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        var path = Path.Combine(SessionDirectory, id + ".json");
        try
        {
            if (!File.Exists(path)) return false;
            File.Delete(path);

            var backup = path + ".bak";
            if (File.Exists(backup)) File.Delete(backup);

            return true;
        }
        catch
        {
            // O arquivo esta aberto por outro processo (ou a pasta sumiu):
            // a sessao fica como estava, e a proxima gravacao tenta de novo.
            return false;
        }
    }

    /// <summary>
    /// Monta a identidade de uma sessão nova.
    ///
    /// O nome do programa vem primeiro para o arquivo ser reconhecível na
    /// pasta; o resto é o instante e um sorteio curto, para que duas
    /// desinstalações do mesmo programa não disputem o mesmo arquivo.
    /// </summary>
    /// <param name="programName">O nome do programa.</param>
    public static string NewId(string? programName)
    {
        var slug = Slug(programName);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var suffix = Guid.NewGuid().ToString("N")[..4];

        return string.IsNullOrEmpty(slug)
            ? $"{stamp}-{suffix}"
            : $"{slug}-{stamp}-{suffix}";
    }

    /// <summary>
    /// Transforma um nome em um pedaço de nome de arquivo seguro: sem
    /// barra, sem dois pontos, sem espaço.
    /// </summary>
    private static string Slug(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var chars = text.Trim()
            .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-')
            .ToArray();

        var slug = new string(chars);
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        slug = slug.Trim('-');

        return slug.Length > 40 ? slug[..40].Trim('-') : slug;
    }

    /// <summary>
    /// Substitui o arquivo quando <c>File.Replace</c> não está disponível
    /// (volume sem suporte a ReplaceFile: FAT32, exFAT, rede).
    /// </summary>
    private static void ReplaceWithBackup(string temporario, string path)
    {
        File.Copy(path, path + ".bak", true);
        File.Move(temporario, path, true);
    }
}
