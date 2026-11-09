using System.Diagnostics;
using System.IO;
using System.Text;
using Tranal.Uninstaller.Core.Management;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Um grupo de instalacoes do mesmo programa.
/// </summary>
public sealed record DuplicateInstall
{
    /// <summary>A chave que agrupou as instalacoes.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>O nome exibido (o da primeira instalacao do grupo).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// O que revelou a duplicidade: "ProductCode", "UninstallPath" ou
    /// "NameAndFolder".
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// A instalacao vencedora: a primeira do grupo.
    ///
    /// A primeira e a vencedora porque e a que o Windows Installer e o
    /// registro ja conhecem — as outras sao sobras de uma instalacao antiga.
    /// </summary>
    public InstalledProgram? Winner { get; init; }

    /// <summary>Todas as instalacoes do grupo, na ordem em que apareceram.</summary>
    public IReadOnlyList<InstalledProgram> Entries { get; init; } = Array.Empty<InstalledProgram>();

    /// <summary>Quantas copias existem.</summary>
    public int Count => Entries.Count;
}

/// <summary>
/// Os metadados de um instalador, lidos sem executar o instalador.
/// </summary>
public sealed record CustomInstallEntry
{
    /// <summary>O caminho do instalador.</summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>O tipo do instalador ("MSI", "InnoSetup" ou "Executavel").</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>O nome do produto.</summary>
    public string ProductName { get; init; } = string.Empty;

    /// <summary>A versao do produto.</summary>
    public string ProductVersion { get; init; } = string.Empty;

    /// <summary>A empresa que assina o produto.</summary>
    public string CompanyName { get; init; } = string.Empty;

    /// <summary>A descricao do instalador.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>O tamanho do instalador, em bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>True quando o instalador e um pacote do Windows Installer.</summary>
    public bool IsMsi { get; init; }

    /// <summary>True quando o instalador foi feito com o Inno Setup.</summary>
    public bool IsInnoSetup { get; init; }
}

/// <summary>
/// Detecta instalacoes duplicadas: o mesmo programa instalado em dois
/// lugares.
///
/// O Uninstalr e o Uninstall Tool mostram as duplicatas, porque so uma das
/// copias costuma ser a "certa" — as outras sao sobras de uma instalacao
/// antiga, ou a versao de teste de um.
///
/// A REGRA mora aqui, em um lugar so: a identificacao do programa (o
/// <c>ProductCode</c>, o caminho do desinstalador ou o nome com a pasta) e
/// o agrupamento ficam aqui.
///
/// A ordem dos criterios e fixa, do mais forte para o mais fraco:
/// <list type="number">
/// <item><c>ProductCode</c> — o mesmo produto MSI instalado duas vezes.</item>
/// <item><c>UninstallPath</c> — o mesmo desinstalador apontado por duas entradas.</item>
/// <item><c>NameAndFolder</c> — o mesmo nome normalizado na mesma pasta.</item>
/// </list>
///
/// O que conta como "mesmo programa" e o nome sem o numero da versao, sem o
/// sufixo do fabricante e sem acentos: "Adobe Acrobat Reader DC (x64)" e
/// "Adobe Acrobat Reader" sao o mesmo programa para o usuario.
/// </summary>
public static class DuplicateInstallDetector
{
    /// <summary>
    /// Os grupos de instalacoes duplicadas.
    ///
    /// A vencedora de cada grupo e a primeira entrada: e a que o Windows
    /// Installer e o registro ja conhecem.
    /// </summary>
    /// <param name="programs">Os programas instalados.</param>
    public static IReadOnlyList<DuplicateInstall> Detect(IEnumerable<InstalledProgram>? programs)
    {
        var grupos = new List<DuplicateInstall>();
        if (programs is null) return grupos;

        var porChave = new Dictionary<string, List<InstalledProgram>>(StringComparer.OrdinalIgnoreCase);
        var motivos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ordem = new List<string>();

        foreach (var programa in programs)
        {
            if (programa is null) continue;

            var (chave, motivo) = Chave(programa);
            if (chave.Length == 0) continue;

            if (!porChave.TryGetValue(chave, out var lista))
            {
                lista = new List<InstalledProgram>();
                porChave[chave] = lista;
                motivos[chave] = motivo;
                ordem.Add(chave);
            }

            lista.Add(programa);
        }

        foreach (var chave in ordem)
        {
            var lista = porChave[chave];
            if (lista.Count < 2) continue;

            grupos.Add(new DuplicateInstall
            {
                Key = chave,
                Name = lista[0].DisplayName,
                Reason = motivos[chave],
                Winner = lista[0],
                Entries = lista,
            });
        }

        return grupos.OrderByDescending(g => g.Count).ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Normaliza o nome de um programa para comparar com outro.
    /// </summary>
    /// <param name="name">O nome do programa.</param>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var texto = name.Trim().ToLowerInvariant();

        // Tira o acento.
        var decomposto = texto.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        texto = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);

        // Tira o numero da versao: "(1.2.3)", "- 1.2.3", "v1.2.3".
        texto = System.Text.RegularExpressions.Regex.Replace(texto, @"[\(\[]\s*v?\d+(\.\d+)*\s*[\)\]]", " ");
        texto = System.Text.RegularExpressions.Regex.Replace(texto, @"\s+-\s+v?\d+(\.\d+)*", " ");

        // Tira a versao no fim do nome: "Meu Programa 1.0".
        texto = System.Text.RegularExpressions.Regex.Replace(texto, @"\s+v?\d+(\.\d+)+$", " ");

        // Tira o sufixo do fabricante e o da arquitetura.
        texto = System.Text.RegularExpressions.Regex.Replace(texto, @"\((x86|x64|64-bit|32-bit|amd64)\)", " ");
        texto = System.Text.RegularExpressions.Regex.Replace(texto, @"\s+-\s+\S+\s+(inc|corporation|corp\.?|ltda|llc|gmbh|s\.a\.)\s*$", " ");

        // Ajusta o espaco.
        texto = System.Text.RegularExpressions.Regex.Replace(texto, @"\s+", " ").Trim();

        return texto;
    }

    /// <summary>
    /// A chave que agrupa o programa e o motivo do agrupamento.
    ///
    /// A chave mais forte que o programa tem e a que vale: o
    /// <c>ProductCode</c> identifica o produto MSI, o caminho do
    /// desinstalador identifica a instalacao, e o nome com a pasta
    /// identifica o programa solto no disco.
    /// </summary>
    private static (string Chave, string Motivo) Chave(InstalledProgram programa)
    {
        var codigo = ProductCode(programa);
        if (codigo.Length > 0) return ($"pc:{codigo}", "ProductCode");

        var desinstalador = UninstallPath(programa);
        if (desinstalador.Length > 0) return ($"up:{desinstalador}", "UninstallPath");

        var nome = Normalize(programa.DisplayName);
        if (nome.Length < 3) return (string.Empty, string.Empty);

        return ($"nf:{nome}|{Pasta(programa.InstallLocation)}", "NameAndFolder");
    }

    /// <summary>
    /// O <c>ProductCode</c> do programa, quando ele tem um.
    /// </summary>
    private static string ProductCode(InstalledProgram programa)
    {
        if (MsiProductCatalog.TryNormalizeProductCode(programa.ProductCode, out var codigo))
            return codigo;

        var doComando = UninstallerClassifier.ExtractProductCode(programa.UninstallString);
        if (MsiProductCatalog.TryNormalizeProductCode(doComando, out codigo)) return codigo;

        return string.Empty;
    }

    /// <summary>
    /// O caminho do desinstalador, quando o comando aponta para um arquivo.
    ///
    /// O comando "msiexec.exe /x {GUID}" nao tem caminho: e o
    /// <c>ProductCode</c> que identifica aquele caso.
    /// </summary>
    private static string UninstallPath(InstalledProgram programa)
    {
        var comando = (programa.UninstallString ?? string.Empty).Trim();
        if (comando.Length == 0) return string.Empty;

        var caminho = ExecutablePathResolver.Extract(comando);
        if (caminho.Length == 0) return string.Empty;
        if (!caminho.Contains('\\') && !caminho.Contains(':')) return string.Empty;

        return PathUtil.Normalize(caminho);
    }

    /// <summary>
    /// A pasta onde o programa esta instalado: a pasta que contem a
    /// instalacao (o pai do caminho declarado).
    /// </summary>
    private static string Pasta(string? installLocation)
    {
        var pasta = PathUtil.Normalize(installLocation);
        if (pasta.Length == 0) return string.Empty;

        try
        {
            var pai = Path.GetDirectoryName(pasta);
            return string.IsNullOrEmpty(pai) ? string.Empty : PathUtil.Normalize(pai);
        }
        catch (ArgumentException)
        {
            // Caminho invalido: a pasta fica vazia e o nome manda sozinho.
            return string.Empty;
        }
    }
}

/// <summary>
/// Le os metadados de um instalador sem executar o instalador.
///
/// O instalador e um executavel (ou um pacote MSI) que roda codigo do
/// fabricante: rodar para saber o nome do programa e o caminho do produto e
/// o que a ferramenta nao deve fazer. Os metadados ficam no proprio arquivo
/// — no MSI, na tabela de propriedades; no executavel, nos recursos de
/// versao e na assinatura do empacotador.
///
/// A REGRA mora aqui, em um lugar so: o que da para ler de um instalador
/// fica aqui, e quem mostra consome o veredito.
/// </summary>
public static class CustomInstallEntryReader
{
    /// <summary>
    /// Tenta ler os metadados do instalador.
    ///
    /// Devolve false quando o arquivo nao existe, nao e um instalador ou nao
    /// tem metadado legivel.
    /// </summary>
    /// <param name="path">O caminho do instalador.</param>
    /// <param name="entry">Os metadados lidos (vazio quando deu false).</param>
    public static bool TryRead(string? path, out CustomInstallEntry entry)
    {
        entry = new CustomInstallEntry();

        var arquivo = PathUtil.Normalize(path);
        if (arquivo.Length == 0) return false;

        try
        {
            if (!File.Exists(arquivo)) return false;

            var info = new FileInfo(arquivo);
            var ehMsi = string.Equals(info.Extension, ".msi", StringComparison.OrdinalIgnoreCase);
            var ehExecutavel = string.Equals(info.Extension, ".exe", StringComparison.OrdinalIgnoreCase);

            if (!ehMsi && !ehExecutavel) return false;

            var dados = ehMsi ? ReadMsi(arquivo) : ReadExecutable(arquivo);

            if (dados is null) return false;

            entry = new CustomInstallEntry
            {
                SourcePath = arquivo,
                Kind = dados.Kind,
                ProductName = dados.ProductName,
                ProductVersion = dados.ProductVersion,
                CompanyName = dados.CompanyName,
                Description = dados.Description,
                SizeBytes = info.Length,
                IsMsi = ehMsi,
                IsInnoSetup = string.Equals(dados.Kind, "InnoSetup", StringComparison.Ordinal),
            };

            return true;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler os metadados de '{arquivo}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Os metadados lidos de um instalador.
    /// </summary>
    private sealed record Metadados(
        string Kind,
        string ProductName,
        string ProductVersion,
        string CompanyName,
        string Description);

    /// <summary>
    /// Os metadados de um executavel: os recursos de versao do proprio
    /// arquivo e a assinatura do empacotador.
    /// </summary>
    private static Metadados? ReadExecutable(string caminho)
    {
        var versao = FileVersionInfo.GetVersionInfo(caminho);

        var nome = (versao.ProductName ?? string.Empty).Trim();
        var descricao = (versao.FileDescription ?? string.Empty).Trim();
        var empresa = (versao.CompanyName ?? string.Empty).Trim();
        var numero = (versao.ProductVersion ?? string.Empty).Trim();

        var ehInno = IsInnoSetup(caminho);

        if (nome.Length == 0 && descricao.Length == 0 && !ehInno) return null;

        if (nome.Length == 0) nome = descricao;

        return new Metadados(
            ehInno ? "InnoSetup" : "Executavel",
            nome,
            numero,
            empresa,
            descricao);
    }

    /// <summary>
    /// Os metadados de um pacote MSI: a tabela de propriedades do Windows
    /// Installer, lida pelo proprio Windows Installer.
    /// </summary>
    private static Metadados? ReadMsi(string caminho)
    {
        var nome = string.Empty;
        var versao = string.Empty;
        var empresa = string.Empty;

        try
        {
            var tipo = Type.GetTypeFromProgID("WindowsInstaller.Installer");
            if (tipo is null) return null;

            dynamic instalador = Activator.CreateInstance(tipo)!;
            if (instalador is null) return null;

            dynamic banco = instalador.OpenDatabase(caminho, 0);

            nome = Property(banco, "ProductName");
            versao = Property(banco, "ProductVersion");
            empresa = Property(banco, "Manufacturer");
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler as propriedades de '{caminho}': {ex.Message}");
            return null;
        }

        if (nome.Length == 0) return null;

        return new Metadados("MSI", nome, versao, empresa, string.Empty);
    }

    /// <summary>
    /// Uma propriedade da tabela "Property" de um pacote MSI.
    /// </summary>
    private static string Property(dynamic banco, string nome)
    {
        try
        {
            dynamic visao = banco.OpenView($"SELECT Value FROM Property WHERE Property='{nome}'");
            visao.Execute();

            object? registro = visao.Fetch();
            if (registro is null) return string.Empty;

            dynamic linha = registro;
            var valor = linha.StringData(1)?.ToString() ?? string.Empty;

            return valor.Trim();
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler a propriedade '{nome}' do pacote: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// True quando o executavel foi feito com o Inno Setup.
    ///
    /// O instalador do Inno Setup carrega a assinatura "Inno Setup" no
    /// comeco do arquivo — e o que o distingue de um executavel comum.
    /// </summary>
    private static bool IsInnoSetup(string caminho)
    {
        try
        {
            using var stream = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            var buffer = new byte[256 * 1024];
            var lidos = stream.Read(buffer, 0, buffer.Length);
            if (lidos <= 0) return false;

            var texto = Encoding.Latin1.GetString(buffer, 0, lidos);
            return texto.Contains("Inno Setup", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao procurar a assinatura do instalador '{caminho}': {ex.Message}");
            return false;
        }
    }
}
