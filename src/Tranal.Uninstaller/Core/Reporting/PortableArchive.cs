using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Tranal.Uninstaller.Core.Modules;

namespace Tranal.Uninstaller.Core.Reporting;

/// <summary>
/// O manifesto de um pacote portatil.
///
/// O manifesto e o que torna o pacote autocontido: ele diz qual log e
/// qual, com quantos arquivos, quantas pastas e quantas chaves. A
/// importacao valida esse manifesto antes de trazer qualquer log — um
/// <c>.zip</c> qualquer nao entra na base de logs.
/// </summary>
public sealed class PortableManifest
{
    /// <summary>O identificador do formato do manifesto.</summary>
    public string Format { get; set; } = PortableArchive.ManifestFormat;

    /// <summary>Quando o pacote foi gerado (UTC).</summary>
    public DateTime ExportedUtc { get; set; }

    /// <summary>Os logs que o pacote leva.</summary>
    public List<PortableEntry> Entries { get; set; } = new();
}

/// <summary>
/// Um log dentro do pacote portatil.
/// </summary>
public sealed class PortableEntry
{
    /// <summary>O identificador do log.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>O nome do programa instalado.</summary>
    public string ProgramName { get; set; } = string.Empty;

    /// <summary>A data do rastreio (UTC).</summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>O instalador executado.</summary>
    public string InstallerPath { get; set; } = string.Empty;

    /// <summary>O caminho do log dentro do pacote (ex.: <c>logs/abc.json</c>).</summary>
    public string LogFile { get; set; } = string.Empty;

    /// <summary>Quantos arquivos o log tem.</summary>
    public int FileCount { get; set; }

    /// <summary>Quantas pastas o log tem.</summary>
    public int FolderCount { get; set; }

    /// <summary>Quantas chaves de registro o log tem.</summary>
    public int RegistryKeyCount { get; set; }
}

/// <summary>
/// O resultado de uma importacao de pacote portatil.
/// </summary>
public sealed class PortableImportResult
{
    /// <summary>Quantos logs entraram na base.</summary>
    public int Imported { get; internal set; }

    /// <summary>Quantos logs foram recusados.</summary>
    public int Skipped { get; internal set; }

    /// <summary>O erro que impediu a importacao, quando houve.</summary>
    public string Error { get; internal set; } = string.Empty;

    /// <summary>Os logs importados.</summary>
    public List<TracedInstall> Logs { get; } = new();

    /// <summary>O que aconteceu com cada log recusado.</summary>
    public List<string> Messages { get; } = new();
}

/// <summary>
/// O pacote portatil de programas monitorados.
///
/// Um <c>.zip</c> autocontido: o manifesto e os logs de instalacao
/// monitorada. Serve para levar a base de logs de uma maquina para outra —
/// ou para guardar um log antes de mexer no sistema.
///
/// A importacao NUNCA sobrescreve um log que ja existe: quando o
/// identificador do log ja esta na base, o log importado entra com outro
/// identificador. O log antigo continua intacto. Essa decisao mora no
/// <see cref="TraceStore"/>, que ja faz isso; o pacote so consome.
/// </summary>
public static class PortableArchive
{
    /// <summary>O identificador do formato do manifesto aceito na importacao.</summary>
    public const string ManifestFormat = "Tranal.Portable.v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>
    /// Monta o pacote portatil com os logs indicados.
    /// </summary>
    /// <param name="installs">Os logs a levar no pacote.</param>
    /// <param name="destinationZipPath">O caminho do <c>.zip</c> a gravar.</param>
    /// <returns>O caminho gravado, ou string vazia quando falhou.</returns>
    public static string Export(IEnumerable<TracedInstall> installs, string destinationZipPath)
    {
        var pastaTrabalho = string.Empty;

        try
        {
            if (string.IsNullOrWhiteSpace(destinationZipPath)) return string.Empty;

            var lista = (installs ?? Enumerable.Empty<TracedInstall>())
                .Where(i => i is not null)
                .ToList();

            var destino = Path.GetFullPath(destinationZipPath);
            var pasta = Path.GetDirectoryName(destino);
            if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

            pastaTrabalho = Path.Combine(Path.GetTempPath(), "tranal-portable-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(pastaTrabalho);

            var logs = Path.Combine(pastaTrabalho, "logs");
            Directory.CreateDirectory(logs);

            var manifesto = new PortableManifest { ExportedUtc = DateTime.UtcNow };

            foreach (var install in lista)
            {
                var nome = Sanear(install.Id) + ".json";
                var caminho = Path.Combine(logs, nome);

                // O log do pacote sai no formato canonico: quem importa le
                // com o leitor do proprio TraceStore.
                var gravado = TraceStore.Export(install, caminho);
                if (string.IsNullOrWhiteSpace(gravado) || !File.Exists(caminho)) continue;

                manifesto.Entries.Add(new PortableEntry
                {
                    Id = install.Id,
                    ProgramName = install.ProgramName,
                    TimestampUtc = install.Timestamp,
                    InstallerPath = install.InstallerPath,
                    LogFile = "logs/" + nome,
                    FileCount = install.FileCount,
                    FolderCount = install.FolderCount,
                    RegistryKeyCount = install.RegistryKeyCount,
                });
            }

            File.WriteAllText(
                Path.Combine(pastaTrabalho, "manifest.json"),
                JsonSerializer.Serialize(manifesto, JsonOptions),
                new UTF8Encoding(false));

            if (File.Exists(destino)) File.Delete(destino);
            ZipFile.CreateFromDirectory(pastaTrabalho, destino, CompressionLevel.Optimal, includeBaseDirectory: false);

            return destino;
        }
        catch
        {
            // Falha de gravacao: quem chama precisa avisar o usuario, e o
            // destino fica vazio para isso.
            return string.Empty;
        }
        finally
        {
            // A pasta de trabalho nao pode sobrar no disco: ela tem os logs
            // do usuario em texto claro.
            try { if (pastaTrabalho.Length > 0 && Directory.Exists(pastaTrabalho)) Directory.Delete(pastaTrabalho, true); }
            catch { /* a pasta fica para o proximo ciclo de Imports limpar */ }
        }
    }

    /// <summary>
    /// Importa um pacote portatil para a base de logs.
    ///
    /// O manifesto e validado antes de qualquer coisa: um <c>.zip</c> sem
    /// manifesto valido nao entra na base. Cada log do pacote entra pelo
    /// <see cref="TraceStore.Import"/>, que renomeia o log quando o
    /// identificador ja existe — o log que ja estava na base nunca e
    /// sobrescrito.
    /// </summary>
    /// <param name="zipPath">O caminho do pacote.</param>
    public static PortableImportResult Import(string zipPath)
    {
        var result = new PortableImportResult();
        var pastaTrabalho = string.Empty;

        try
        {
            if (string.IsNullOrWhiteSpace(zipPath))
            {
                result.Error = "Nenhum pacote informado.";
                return result;
            }

            var origem = Path.GetFullPath(zipPath);
            if (!File.Exists(origem))
            {
                result.Error = "Pacote não encontrado.";
                return result;
            }

            using var archive = ZipFile.OpenRead(origem);

            var entrada = FindEntry(archive, "manifest.json");
            if (entrada is null)
            {
                result.Error = "O pacote não tem manifesto.";
                return result;
            }

            PortableManifest? manifesto;
            using (var stream = entrada.Open())
            {
                manifesto = JsonSerializer.Deserialize<PortableManifest>(stream, JsonOptions);
            }

            if (manifesto is null || !string.Equals(manifesto.Format, ManifestFormat, StringComparison.Ordinal))
            {
                result.Error = "O manifesto do pacote não é do Tranal.";
                return result;
            }

            pastaTrabalho = Path.Combine(Path.GetTempPath(), "tranal-portable-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(pastaTrabalho);

            foreach (var item in manifesto.Entries)
            {
                if (item is null) continue;

                var log = FindEntry(archive, item.LogFile);
                if (log is null)
                {
                    result.Skipped++;
                    result.Messages.Add($"O log \"{item.ProgramName}\" não está no pacote.");
                    continue;
                }

                var temporario = Path.Combine(pastaTrabalho, Sanear(item.Id) + ".json");
                log.ExtractToFile(temporario, overwrite: true);

                var imported = TraceStore.Import(temporario);
                if (imported is null)
                {
                    result.Skipped++;
                    result.Messages.Add($"O log \"{item.ProgramName}\" não pôde ser lido.");
                    continue;
                }

                result.Imported++;
                result.Logs.Add(imported);
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            return result;
        }
        finally
        {
            try { if (pastaTrabalho.Length > 0 && Directory.Exists(pastaTrabalho)) Directory.Delete(pastaTrabalho, true); }
            catch { /* a pasta fica para o proximo ciclo de Imports limpar */ }
        }
    }

    /// <summary>
    /// Procura uma entrada do pacote pelo nome, tolerando a barra invertida
    /// que alguns compactadores usam no lugar da barra normal.
    /// </summary>
    /// <param name="archive">O pacote.</param>
    /// <param name="name">O nome da entrada.</param>
    private static ZipArchiveEntry? FindEntry(ZipArchive archive, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var procurado = name.Replace('\\', '/').TrimStart('/');
        var folha = procurado[(procurado.LastIndexOf('/') + 1)..];

        foreach (var entry in archive.Entries)
        {
            var nome = entry.FullName.Replace('\\', '/').TrimStart('/');
            if (string.Equals(nome, procurado, StringComparison.OrdinalIgnoreCase)) return entry;
            if (string.Equals(nome, folha, StringComparison.OrdinalIgnoreCase)) return entry;
        }

        return null;
    }

    /// <summary>
    /// Um nome de arquivo seguro, derivado do identificador do log.
    /// </summary>
    /// <param name="id">O identificador.</param>
    private static string Sanear(string? id)
    {
        var texto = (id ?? string.Empty).Trim();
        if (texto.Length == 0) return Guid.NewGuid().ToString("N");

        var invalidos = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(texto.Length);
        foreach (var ch in texto)
        {
            builder.Append(Array.IndexOf(invalidos, ch) >= 0 ? '_' : ch);
        }

        return builder.ToString();
    }
}

/// <summary>
/// O resultado de uma importacao de lista de programas.
/// </summary>
public sealed class ProgramImportResult
{
    /// <summary>Os programas da lista que casaram com o que esta instalado.</summary>
    public List<ProgramImportMatch> Matched { get; } = new();

    /// <summary>Os programas da lista que nao casaram.</summary>
    public List<ProgramImportMatch> Unmatched { get; } = new();

    /// <summary>Quantas entradas a lista tinha.</summary>
    public int TotalCount => Matched.Count + Unmatched.Count;
}

/// <summary>
/// O resultado do casamento de uma entrada da lista com o que esta
/// instalado.
/// </summary>
/// <param name="Name">O nome como estava na lista importada.</param>
/// <param name="Program">O programa instalado que casou, quando casou.</param>
/// <param name="Matched">True quando casou.</param>
/// <param name="Reason">O motivo do casamento (ou da falta dele).</param>
public sealed record ProgramImportMatch(string Name, InstalledProgram? Program, bool Matched, string Reason);

/// <summary>
/// A importacao de uma lista de programas exportada.
///
/// O Tranal exporta a lista em TXT, CSV e XML; esta e a porta de entrada
/// para trazer essa lista de volta — em outra maquina, ou depois de um
/// tempo. O que importa nao e so o que casou: e tambem o que NAO casou, e
/// por que. Um programa que nao esta instalado nesta maquina e uma
/// informacao util, nao um erro.
///
/// O casamento e por nome (ignorando acentos, caixa e espacos) ou pela
/// chave de registro, quando a lista traz a chave.
/// </summary>
public static class ProgramListImporter
{
    /// <summary>
    /// Importa uma lista de programas exportada e casa com o que esta
    /// instalado.
    /// </summary>
    /// <param name="filePath">O arquivo exportado (TXT, CSV ou XML).</param>
    /// <param name="installed">Os programas instalados nesta maquina.</param>
    public static ProgramImportResult Import(string filePath, IReadOnlyList<InstalledProgram> installed)
    {
        var result = new ProgramImportResult();

        try
        {
            if (string.IsNullOrWhiteSpace(filePath)) return result;

            var origem = Path.GetFullPath(filePath);
            if (!File.Exists(origem)) return result;

            return ImportContent(File.ReadAllText(origem), origem, installed);
        }
        catch
        {
            // Arquivo ilegivel: a importacao devolve a lista vazia em vez de
            // derrubar a tela de quem so queria conferir um nome.
            return result;
        }
    }

    /// <summary>
    /// Importa o conteudo de uma lista exportada e casa com o que esta
    /// instalado. O formato vem do nome do arquivo (TXT, CSV ou XML).
    /// </summary>
    /// <param name="content">O conteudo do arquivo exportado.</param>
    /// <param name="fileName">O nome do arquivo (usado para descobrir o formato).</param>
    /// <param name="installed">Os programas instalados nesta maquina.</param>
    public static ProgramImportResult ImportContent(
        string content, string fileName, IReadOnlyList<InstalledProgram> installed)
    {
        var result = new ProgramImportResult();
        var entradas = LerEntradas(content, fileName);

        foreach (var entrada in entradas)
        {
            result.Match(entrada, installed);
        }

        return result;
    }

    /// <summary>
    /// As entradas (nome e chave de registro) de uma lista exportada.
    /// </summary>
    /// <param name="content">O conteudo do arquivo.</param>
    /// <param name="fileName">O nome do arquivo.</param>
    private static List<(string Nome, string Chave)> LerEntradas(string content, string fileName)
    {
        var entradas = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(content)) return entradas;

        var extensao = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();

        return extensao switch
        {
            ".csv" => LerCsv(content),
            ".xml" => LerXml(content),
            _ => LerTexto(content),
        };
    }

    /// <summary>
    /// Le a lista em CSV: a primeira linha e o cabecalho (com o nome fixo
    /// das colunas) e cada linha seguinte e um programa.
    /// </summary>
    /// <param name="content">O conteudo do arquivo.</param>
    private static List<(string, string)> LerCsv(string content)
    {
        var entradas = new List<(string, string)>();
        var linhas = content.Replace("\r\n", "\n").Split('\n');
        if (linhas.Length == 0) return entradas;

        var cabecalho = SplitCsv(linhas[0]);
        var indiceNome = cabecalho.FindIndex(c => string.Equals(c.Trim(), "Program", StringComparison.OrdinalIgnoreCase));
        var indiceChave = cabecalho.FindIndex(c => string.Equals(c.Trim(), "RegistryKey", StringComparison.OrdinalIgnoreCase));

        for (var i = 1; i < linhas.Length; i++)
        {
            if (linhas[i].Trim().Length == 0) continue;

            var campos = SplitCsv(linhas[i]);
            var nome = indiceNome >= 0 && indiceNome < campos.Count ? campos[indiceNome].Trim() : string.Empty;
            if (nome.Length == 0) continue;

            var chave = indiceChave >= 0 && indiceChave < campos.Count ? campos[indiceChave].Trim() : string.Empty;
            entradas.Add((nome, chave));
        }

        return entradas;
    }

    /// <summary>
    /// Le a lista em XML: o exportador poe um <c>Entry</c> por programa, com
    /// o nome fixo da coluna como elemento.
    /// </summary>
    /// <param name="content">O conteudo do arquivo.</param>
    private static List<(string, string)> LerXml(string content)
    {
        var entradas = new List<(string, string)>();

        try
        {
            var documento = XDocument.Parse(content);
            foreach (var entry in documento.Root?.Elements("Entry") ?? Enumerable.Empty<XElement>())
            {
                var nome = entry.Element("Program")?.Value.Trim() ?? string.Empty;
                if (nome.Length == 0) continue;

                var chave = entry.Element("RegistryKey")?.Value.Trim() ?? string.Empty;
                entradas.Add((nome, chave));
            }
        }
        catch
        {
            // XML invalido: a lista fica vazia. O chamador mostra a mensagem.
        }

        return entradas;
    }

    /// <summary>
    /// Le a lista em texto: o exportador separa os campos por " - ", e o
    /// primeiro e o nome do programa.
    /// </summary>
    /// <param name="content">O conteudo do arquivo.</param>
    private static List<(string, string)> LerTexto(string content)
    {
        var entradas = new List<(string, string)>();

        foreach (var linha in content.Replace("\r\n", "\n").Split('\n'))
        {
            if (linha.Trim().Length == 0) continue;

            var nome = linha.Split(" - ")[0].Trim();
            if (nome.Length == 0) continue;

            entradas.Add((nome, string.Empty));
        }

        return entradas;
    }

    /// <summary>
    /// Separa os campos de uma linha de CSV, respeitando as aspas.
    /// </summary>
    /// <param name="linha">A linha.</param>
    private static List<string> SplitCsv(string linha)
    {
        var campos = new List<string>();
        var atual = new StringBuilder();
        var dentroDeAspas = false;

        for (var i = 0; i < linha.Length; i++)
        {
            var ch = linha[i];

            if (dentroDeAspas)
            {
                if (ch == '"')
                {
                    // Aspas dobradas dentro do campo valem uma aspa.
                    if (i + 1 < linha.Length && linha[i + 1] == '"')
                    {
                        atual.Append('"');
                        i++;
                    }
                    else
                    {
                        dentroDeAspas = false;
                    }
                }
                else
                {
                    atual.Append(ch);
                }
            }
            else if (ch == '"')
            {
                dentroDeAspas = true;
            }
            else if (ch == ';')
            {
                campos.Add(atual.ToString());
                atual.Clear();
            }
            else
            {
                atual.Append(ch);
            }
        }

        campos.Add(atual.ToString());
        return campos;
    }
}

/// <summary>
/// O casamento das entradas importadas com os programas instalados.
/// </summary>
internal static class ProgramImportMatchExtensions
{
    /// <summary>
    /// Casa uma entrada da lista com o que esta instalado e guarda o
    /// resultado (casou ou nao casou) no resultado da importacao.
    /// </summary>
    /// <param name="resultado">O resultado da importacao.</param>
    /// <param name="entrada">A entrada importada (nome e chave).</param>
    /// <param name="instalados">Os programas instalados.</param>
    internal static void Match(
        this ProgramImportResult resultado,
        (string Nome, string Chave) entrada,
        IReadOnlyList<InstalledProgram> instalados)
    {
        var casamento = ProgramImportMatcher.Casar(entrada.Nome, entrada.Chave, instalados);

        if (casamento.Program is not null)
            resultado.Matched.Add(new ProgramImportMatch(entrada.Nome, casamento.Program, true, casamento.Reason));
        else
            resultado.Unmatched.Add(new ProgramImportMatch(entrada.Nome, null, false, casamento.Reason));
    }
}

/// <summary>
/// A regra do casamento entre a lista importada e o que esta instalado.
/// </summary>
internal static class ProgramImportMatcher
{
    /// <summary>
    /// Casa uma entrada da lista com um programa instalado.
    ///
    /// A ordem e: chave de registro (a identidade do programa), nome
    /// exato (ignorando acentos e caixa) e, por ultimo, nome sem acentos,
    /// caixa nem espacos.
    /// </summary>
    /// <param name="nome">O nome da lista.</param>
    /// <param name="chave">A chave de registro da lista, quando ela traz.</param>
    /// <param name="instalados">Os programas instalados.</param>
    internal static (InstalledProgram? Program, string Reason) Casar(
        string nome, string chave, IReadOnlyList<InstalledProgram> instalados)
    {
        var lista = instalados ?? Array.Empty<InstalledProgram>();

        if (!string.IsNullOrWhiteSpace(chave))
        {
            foreach (var program in lista)
            {
                if (program is null) continue;
                if (string.Equals(program.RegistryKey?.Trim(), chave.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return (program, "casou pela chave de registro");
                }
            }
        }

        var normal = TraceStore.TraceText.Normalize(nome);
        if (normal.Length > 0)
        {
            foreach (var program in lista)
            {
                if (program is null) continue;
                if (string.Equals(TraceStore.TraceText.Normalize(program.DisplayName), normal, StringComparison.Ordinal))
                {
                    return (program, "casou pelo nome exato");
                }
            }

            var compacto = SomenteLetrasEDigitos(normal);
            if (compacto.Length > 0)
            {
                foreach (var program in lista)
                {
                    if (program is null) continue;
                    if (string.Equals(SomenteLetrasEDigitos(TraceStore.TraceText.Normalize(program.DisplayName)), compacto, StringComparison.Ordinal))
                    {
                        return (program, "casou pelo nome, ignorando acentos, caixa e espaços");
                    }
                }
            }
        }

        return (null, MotivoDaNaoCasada(nome, lista));
    }

    /// <summary>
    /// O motivo pelo qual a entrada nao casou: quando existe um programa de
    /// nome parecido, o motivo diz qual e — o usuario precisa saber se
    /// faltou o programa ou se o nome mudou.
    /// </summary>
    /// <param name="nome">O nome da lista.</param>
    /// <param name="instalados">Os programas instalados.</param>
    private static string MotivoDaNaoCasada(string nome, IReadOnlyList<InstalledProgram> instalados)
    {
        var normal = TraceStore.TraceText.Normalize(nome);
        var compacto = SomenteLetrasEDigitos(normal);

        if (compacto.Length > 0)
        {
            foreach (var program in instalados)
            {
                if (program is null) continue;
                var outro = SomenteLetrasEDigitos(TraceStore.TraceText.Normalize(program.DisplayName));
                if (outro.Length == 0) continue;

                if (outro.Contains(compacto, StringComparison.Ordinal) || compacto.Contains(outro, StringComparison.Ordinal))
                {
                    return $"não é o mesmo programa que \"{program.DisplayName}\", que está instalado";
                }
            }
        }

        return "não há programa instalado com esse nome";
    }

    /// <summary>
    /// O texto sem acentos, caixa, espacos e pontuacao — a forma mais
    /// frouxa de comparar dois nomes.
    /// </summary>
    /// <param name="texto">O texto.</param>
    private static string SomenteLetrasEDigitos(string texto) =>
        new((texto ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
}
