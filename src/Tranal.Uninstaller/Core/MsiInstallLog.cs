using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Um valor de registro escrito pelo pacote do Windows Installer.
/// </summary>
/// <param name="Key">O caminho da chave, sem o nome do hive.</param>
/// <param name="ValueName">O nome do valor. Vazio quando o valor e o padrao da chave.</param>
public readonly record struct MsiRegistryEntry(string Key, string ValueName);

/// <summary>
/// O log de instalacao do Windows Installer: a lista exata do que o pacote
/// gravou no sistema.
///
/// Ate aqui a varredura de sobras do Tranal trabalhava pelo NOME: casava
/// pastas e chaves pelo nome do programa. Isso e heuristica. O Revo resolve
/// o mesmo problema de outro jeito — ele grava o que o programa instalou e
/// compara com o que restou depois.
///
/// Para os produtos do Windows Installer essa comparacao ja existe pronta:
/// o pacote .msi fica em cache em "%WINDIR%\Installer" e descreve,
/// arquivo por arquivo e chave por chave, tudo o que ele colocou no
/// sistema. Este tipo le esse registro.
///
/// O ciclo e o que importa: o log e capturado ANTES do desinstalador
/// nativo (depois dele o pacote sai do cache) e conferido DEPOIS, para
/// saber o que o desinstalador deixou para tras.
/// </summary>
public sealed class MsiInstallLog
{
    /// <summary>O codigo do produto (GUID) do Windows Installer.</summary>
    public string ProductCode { get; init; } = string.Empty;

    /// <summary>O caminho do pacote .msi em cache.</summary>
    public string LocalPackage { get; init; } = string.Empty;

    /// <summary>Os arquivos que o pacote gravou, com caminho absoluto.</summary>
    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();

    /// <summary>As pastas que o pacote gravou, com caminho absoluto.</summary>
    public IReadOnlyList<string> Directories { get; init; } = Array.Empty<string>();

    /// <summary>Os valores de registro que o pacote gravou.</summary>
    public IReadOnlyList<MsiRegistryEntry> RegistryEntries { get; init; } = Array.Empty<MsiRegistryEntry>();

    /// <summary>True quando o pacote nao descreve nada util (log vazio).</summary>
    public bool IsEmpty =>
        Files.Count == 0 && Directories.Count == 0 && RegistryEntries.Count == 0;
}

/// <summary>
/// Le o log de instalacao do Windows Installer.
///
/// A leitura e feita pela API do proprio Windows Installer (msi.dll), sem
/// depender de componente externo: o pacote .msi em cache e aberto em modo
/// somente-leitura e as tabelas <c>File</c>, <c>Component</c>,
/// <c>Directory</c>, <c>Folder</c> e <c>Registry</c> sao lidas.
/// </summary>
public static class MsiInstallLogReader
{
    // Codigos de erro do Windows Installer usados aqui.
    private const uint ErrorSuccess = 0;
    private const uint ErrorMoreData = 234;
    private const uint ErrorNoMoreItems = 259;

    // Propriedades do produto consultadas pela API.
    private const string InstallPropertyLocalPackage = "LocalPackage";
    private const string InstallPropertyInstallLocation = "InstallLocation";

    #region API do Windows Installer

    [DllImport("msi.dll", CharSet = CharSet.Unicode, EntryPoint = "MsiGetProductInfoW")]
    private static extern uint MsiGetProductInfo(
        string szProduct, string szProperty, [Out] char[] szValueBuf, ref int pcchValueBuf);

    [DllImport("msi.dll", CharSet = CharSet.Unicode, EntryPoint = "MsiOpenDatabaseW")]
    private static extern uint MsiOpenDatabase(
        string szDatabasePath, IntPtr szPersist, out IntPtr phDatabase);

    [DllImport("msi.dll", CharSet = CharSet.Unicode, EntryPoint = "MsiDatabaseOpenViewW")]
    private static extern uint MsiDatabaseOpenView(
        IntPtr hDatabase, string szQuery, out IntPtr phView);

    [DllImport("msi.dll", EntryPoint = "MsiViewExecute")]
    private static extern uint MsiViewExecute(IntPtr hView, IntPtr hRecord);

    [DllImport("msi.dll", EntryPoint = "MsiViewFetch")]
    private static extern uint MsiViewFetch(IntPtr hView, out IntPtr phRecord);

    [DllImport("msi.dll", CharSet = CharSet.Unicode, EntryPoint = "MsiRecordGetStringW")]
    private static extern uint MsiRecordGetString(
        IntPtr hRecord, int iField, [Out] char[] szValueBuf, ref int pcchValueBuf);

    [DllImport("msi.dll", EntryPoint = "MsiCloseHandle")]
    private static extern uint MsiCloseHandle(IntPtr hAny);

    #endregion

    /// <summary>
    /// Captura o log de instalacao de um programa, quando ele e um produto do
    /// Windows Installer.
    ///
    /// Devolve <c>null</c> quando o programa nao e MSI, quando o pacote nao
    /// esta em cache (produto ja desinstalado) ou quando o log nao diz nada.
    /// </summary>
    public static MsiInstallLog? Capture(InstalledProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);

        // So um produto do Windows Installer tem log de instalacao para ler.
        // Sem esta trava, um programa de outro tipo que por acaso tivesse um
        // GUID no comando de desinstalacao seria lido com o pacote de outro
        // produto — e as sobras viriam atribuidas ao programa errado.
        if (UninstallerClassifier.Classify(program) != UninstallerKind.MsiExec)
            return null;

        var productCode = program.ProductCode;
        if (string.IsNullOrWhiteSpace(productCode))
            productCode = UninstallerClassifier.ExtractProductCode(program.UninstallString);

        if (string.IsNullOrWhiteSpace(productCode)) return null;
        return Capture(productCode);
    }

    /// <summary>Captura o log de instalacao de um codigo de produto.</summary>
    public static MsiInstallLog? Capture(string productCode)
    {
        if (string.IsNullOrWhiteSpace(productCode)) return null;

        var normalized = NormalizeProductCode(productCode);

        var package = GetProductProperty(normalized, InstallPropertyLocalPackage);
        if (package.Length == 0 || !File.Exists(package)) return null;

        var installLocation = GetProductProperty(normalized, InstallPropertyInstallLocation);

        try
        {
            return ReadPackage(normalized, package, installLocation);
        }
        catch
        {
            // Um pacote corrompido ou de formato inesperado nao pode derrubar
            // a desinstalacao: sem log, a varredura pelo nome continua valendo.
            return null;
        }
    }

    /// <summary>
    /// O log do instalador so vale depois que o desinstalador nativo rodou.
    ///
    /// Sem esta condicao o log descreveria o programa INTEIRO como sobra: o
    /// programa continua instalado, e a etapa seguinte apagaria justamente o
    /// que o usuario queria manter. A regra fica aqui, em um lugar so, para
    /// que a interface grafica e a linha de comando nao possam divergir.
    /// </summary>
    /// <param name="log">O log capturado antes da desinstalacao.</param>
    /// <param name="uninstalled">True quando o desinstalador nativo rodou e deu certo.</param>
    public static MsiInstallLog? UsableAfter(MsiInstallLog? log, bool uninstalled) =>
        uninstalled ? log : null;

    /// <summary>
    /// Confere o log depois do desinstalador nativo: o que o pacote gravou e
    /// ainda esta no sistema e sobra de verdade.
    ///
    /// A regra fica aqui, em um lugar so: o item e sobra do programa quando
    /// o proprio instalador do programa registrou o item e ele continua
    /// existindo depois que o desinstalador nativo rodou.
    /// </summary>
    public static List<LeftoverItem> FindLeftovers(MsiInstallLog log)
    {
        var items = new List<LeftoverItem>();
        if (log is null || log.IsEmpty) return items;

        foreach (var file in log.Files)
        {
            try
            {
                if (!File.Exists(file)) continue;
                items.Add(new LeftoverItem
                {
                    Kind = "Arquivo",
                    Path = PathUtil.Normalize(file),
                    Detail = "Instalado pelo pacote do Windows Installer",
                    Confidence = LeftoverConfidence.High,
                    Reason = LeftoverReason.InstallerLog,
                    Selected = true,
                });
            }
            catch { }
        }

        // A pasta so entra quando esta vazia: uma pasta com conteudo de outro
        // programa nao e sobra deste.
        foreach (var directory in log.Directories)
        {
            try
            {
                if (!Directory.Exists(directory)) continue;
                if (Directory.EnumerateFileSystemEntries(directory).Any()) continue;

                items.Add(new LeftoverItem
                {
                    Kind = "Pasta",
                    Path = PathUtil.Normalize(directory),
                    Detail = "Criada pelo pacote do Windows Installer",
                    Confidence = LeftoverConfidence.High,
                    Reason = LeftoverReason.InstallerLog,
                    Selected = true,
                });
            }
            catch { }
        }

        foreach (var entry in log.RegistryEntries)
        {
            try
            {
                var (hive, sub) = SafetyRules.SplitRegistryPath(entry.Key);
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var key = baseKey.OpenSubKey(sub);
                if (key is null) continue;

                // So entra a chave em que o valor gravado pelo pacote ainda
                // esta. Se o valor sumiu mas a chave ficou, a chave pode ser
                // compartilhada com outro programa — e apagá-la seria remover
                // coisa de outra instalação.
                if (!ValueStillThere(key, entry.ValueName)) continue;

                items.Add(new LeftoverItem
                {
                    Kind = "Registro",
                    Path = entry.Key,
                    Detail = entry.ValueName.Length == 0
                        ? "Valor padrão gravado pelo pacote do Windows Installer"
                        : $"Valor '{entry.ValueName}' gravado pelo pacote do Windows Installer",
                    Confidence = LeftoverConfidence.High,
                    Reason = LeftoverReason.InstallerLog,
                    Selected = true,
                });
            }
            catch { }
        }

        return items;
    }

    /// <summary>True quando o valor ainda existe na chave.</summary>
    private static bool ValueStillThere(RegistryKey key, string valueName)
    {
        try
        {
            // O valor padrao da chave e lido pelo nome vazio.
            if (valueName.Length == 0)
                return key.GetValue(string.Empty) is not null;

            return key.GetValueNames().Contains(valueName, StringComparer.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>Le o pacote .msi em cache e monta o log.</summary>
    private static MsiInstallLog? ReadPackage(
        string productCode, string package, string installLocation)
    {
        if (MsiOpenDatabase(package, IntPtr.Zero, out var database) != ErrorSuccess)
            return null;

        try
        {
            var folders = ReadFolders(database);
            var directories = ReadDirectories(database);
            var components = ReadComponents(database);

            // Um pacote de 32 bits instala em "Program Files (x86)"; um de
            // 64 bits, em "Program Files". O sumario do pacote diz qual e.
            var is64Bit = Is64BitPackage(database, directories);

            var files = ReadFiles(
                database, components, directories, folders, installLocation, is64Bit);
            var registry = ReadRegistry(database);

            // As pastas tambem entram no log: a pasta que sobrou vazia e
            // sobra do programa tanto quanto o arquivo que sobrou dentro dela.
            foreach (var key in directories.Keys.ToList())
            {
                var resolved = ResolveDirectory(
                    key, directories, folders, installLocation, is64Bit,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                if (resolved.Length > 0)
                    directories[key] = directories[key] with { AbsolutePath = resolved };
            }

            return new MsiInstallLog
            {
                ProductCode = productCode,
                LocalPackage = package,
                Files = files,
                Directories = directories.Values
                    .Select(d => d.AbsolutePath)
                    .Where(p => p.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                RegistryEntries = registry,
            };
        }
        finally
        {
            MsiCloseHandle(database);
        }
    }

    /// <summary>
    /// True quando o pacote e de 64 bits.
    ///
    /// O sumario do Windows Installer guarda o "Template" do pacote
    /// ("x64;1033" ou "Intel;1033"), que e exatamente o que o instalador
    /// usa para escolher "Program Files" ou "Program Files (x86)".
    /// </summary>
    private static bool Is64BitPackage(
        IntPtr database, Dictionary<string, DirectoryRow> directories)
    {
        // O "Template" do pacote esta na tabela _SummaryInformation: "x64;1033"
        // para um pacote de 64 bits, "Intel;1033" para um de 32. A propriedade
        // 7 e o Template (PID_TEMPLATE).
        try
        {
            foreach (var row in Query(
                database, "SELECT `Property`,`Value` FROM `_SummaryInformation` WHERE `Property` = 7", 2))
            {
                if (row.Length < 2) continue;
                var template = row[1];
                if (template.Length == 0) continue;

                return !template.StartsWith("Intel", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { }

        // Reserva: um pacote que declara a pasta de 64 bits e de 64 bits.
        // Medido nesta maquina: o pacote do ClickOnce Bootstrapper (32 bits)
        // so declara "ProgramFilesFolder" e instala em "Program Files (x86)",
        // enquanto o do Node.js declara "ProgramFiles64Folder" e instala em
        // "Program Files".
        return directories.ContainsKey("ProgramFiles64Folder");
    }

    internal sealed record DirectoryRow(string Key, string Parent, string DefaultDir)
    {
        public string AbsolutePath { get; init; } = string.Empty;
    }

    /// <summary>A tabela <c>Folder</c>: o caminho absoluto das pastas especiais.</summary>
    private static Dictionary<string, string> ReadFolders(IntPtr database)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Query(database, "SELECT `Directory_`,`Folder` FROM `Folder`", 2))
        {
            if (row.Length < 2) continue;
            if (row[0].Length == 0 || row[1].Length == 0) continue;
            map[row[0]] = row[1];
        }
        return map;
    }

    /// <summary>A tabela <c>Directory</c>, com o caminho absoluto resolvido.</summary>
    private static Dictionary<string, DirectoryRow> ReadDirectories(IntPtr database)
    {
        var rows = new Dictionary<string, DirectoryRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Query(database, "SELECT `Directory`,`Directory_Parent`,`DefaultDir` FROM `Directory`", 3))
        {
            if (row.Length < 3 || row[0].Length == 0) continue;
            rows[row[0]] = new DirectoryRow(row[0], row[1], row[2]);
        }
        return rows;
    }

    /// <summary>A tabela <c>Component</c>: do componente para a pasta.</summary>
    private static Dictionary<string, string> ReadComponents(IntPtr database)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Query(database, "SELECT `Component`,`Directory_` FROM `Component`", 2))
        {
            if (row.Length < 2 || row[0].Length == 0) continue;
            map[row[0]] = row[1];
        }
        return map;
    }

    /// <summary>A tabela <c>File</c>, com o caminho absoluto de cada arquivo.</summary>
    private static List<string> ReadFiles(
        IntPtr database,
        Dictionary<string, string> components,
        Dictionary<string, DirectoryRow> directories,
        Dictionary<string, string> folders,
        string installLocation,
        bool is64Bit)
    {
        var result = new List<string>();
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in Query(database, "SELECT `Component_`,`FileName` FROM `File`", 2))
        {
            if (row.Length < 2) continue;

            var component = row[0];
            var fileName = LongName(row[1]);
            if (component.Length == 0 || fileName.Length == 0) continue;

            if (!components.TryGetValue(component, out var directoryKey)) continue;

            if (!resolved.TryGetValue(directoryKey, out var directoryPath))
            {
                directoryPath = ResolveDirectory(
                    directoryKey, directories, folders, installLocation, is64Bit,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                resolved[directoryKey] = directoryPath;
            }

            if (directoryPath.Length == 0) continue;

            try
            {
                result.Add(Path.Combine(directoryPath, fileName));
            }
            catch { }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>A tabela <c>Registry</c>, com o hive ja resolvido.</summary>
    private static List<MsiRegistryEntry> ReadRegistry(IntPtr database)
    {
        var result = new List<MsiRegistryEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in Query(database, "SELECT `Root`,`Key`,`Name` FROM `Registry`", 3))
        {
            if (row.Length < 3) continue;

            if (!int.TryParse(row[0], out var root)) continue;
            var key = row[1];
            var valueName = row[2];

            // Chave com referencia a propriedade ([INSTALLDIR], [#FileKey]):
            // o caminho absoluto depende da resolucao de propriedade do
            // instalador, e inventar um caminho seria pior que nao reportar.
            if (key.Length == 0 || key.Contains('[')) continue;

            var hive = root switch
            {
                0 => RegistryHive.ClassesRoot,
                1 => RegistryHive.CurrentUser,
                2 => RegistryHive.LocalMachine,
                3 => RegistryHive.Users,
                _ => RegistryHive.CurrentUser,
            };

            var full = SafetyRules.HiveName(hive) + "\\" + key.TrimStart('\\');

            // O item e a chave; o valor e o que prova que a chave e deste
            // programa. Uma chave repetida entra uma vez so.
            var signature = full + "|" + valueName;
            if (!seen.Add(signature)) continue;

            result.Add(new MsiRegistryEntry(full, valueName));
        }

        return result;
    }

    /// <summary>
    /// Resolve o caminho absoluto de uma pasta da tabela <c>Directory</c>.
    ///
    /// A ordem e fixa: primeiro a tabela <c>Folder</c> (que ja traz o
    /// caminho absoluto das pastas especiais), depois as pastas de sistema
    /// conhecidas, e por fim a arvore, subindo ate uma delas.
    /// </summary>
    internal static string ResolveDirectory(
        string key,
        Dictionary<string, DirectoryRow> directories,
        Dictionary<string, string> folders,
        string installLocation,
        bool is64Bit,
        HashSet<string> visiting)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        if (!visiting.Add(key)) return string.Empty;   // ciclo na arvore

        try
        {
            if (folders.TryGetValue(key, out var folderPath) && folderPath.Length > 0)
                return PathUtil.Normalize(folderPath);

            var system = SystemFolder(key, installLocation, is64Bit);
            if (system.Length > 0) return system;

            if (!directories.TryGetValue(key, out var row)) return string.Empty;

            var parent = ResolveDirectory(
                row.Parent, directories, folders, installLocation, is64Bit, visiting);
            if (parent.Length == 0) return string.Empty;

            var name = OnDiskName(row);

            // O ponto significa a mesma pasta do pai: a pasta nao tem nome
            // proprio, ela e o proprio pai.
            if (name.Length == 0 || name == ".") return parent;

            try { return PathUtil.Normalize(Path.Combine(parent, name)); }
            catch { return string.Empty; }
        }
        finally
        {
            visiting.Remove(key);
        }
    }

    /// <summary>
    /// As pastas de sistema do Windows Installer. Sao um conjunto fechado:
    /// o instalador sempre usa estes nomes.
    ///
    /// A tabela <c>Folder</c> do pacote resolve o caminho exato quando ela
    /// existe; este mapa e a reserva para os pacotes que nao a trazem.
    /// </summary>
    private static string SystemFolder(string key, string installLocation, bool is64Bit)
    {
        var programFiles = is64Bit
            ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var commonFiles = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Common Files");
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        string? mapped = key switch
        {
            "TARGETDIR" => RootOf(installLocation),
            "ProgramFilesFolder" => programFiles,
            "ProgramFiles64Folder" => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "ProgramFiles6432Folder" => programFilesX86,
            "CommonFilesFolder" => commonFiles,
            "CommonFiles64Folder" => commonFiles,
            "CommonFiles6432Folder" => commonFiles,
            "LocalAppDataFolder" => localAppData,
            "LocalAppDataProgramFilesFolder" => Path.Combine(localAppData, "Programs"),
            "AppDataFolder" => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WindowsFolder" => windows,
            "WindowsVolume" => windows,
            "SystemFolder" => Path.Combine(windows, "System32"),
            "System64Folder" => Path.Combine(windows, "System32"),
            "System16Folder" => Path.Combine(windows, "SysWOW64"),
            "PersonalFolder" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "DesktopFolder" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "PicturesFolder" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "TemplatesFolder" => Environment.GetFolderPath(Environment.SpecialFolder.Templates),
            "FavoritesFolder" => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Favorites"),
            "AdminToolsFolder" => Path.Combine(programFiles, "Administrator Tools"),
            "TempFolder" => Path.GetTempPath(),
            "FontsFolder" => Path.Combine(windows, "Fonts"),
            _ => null,
        };

        return string.IsNullOrWhiteSpace(mapped) ? string.Empty : PathUtil.Normalize(mapped);
    }

    /// <summary>A raiz da unidade onde o programa foi instalado.</summary>
    private static string RootOf(string installLocation)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(installLocation))
            {
                var root = Path.GetPathRoot(installLocation);
                if (!string.IsNullOrEmpty(root)) return root;
            }
        }
        catch { }
        return "C:\\";
    }

    /// <summary>
    /// O nome da pasta como ele aparece no disco.
    ///
    /// <c>DefaultDir</c> vem no formato "origem|destino", e o que fica no
    /// disco e o destino — a segunda parte. Isso foi medido nesta maquina:
    /// o pacote do WSL traz <c>DefaultDir = x0zlheba|wslsettings</c> e o que
    /// existe em disco e "wslsettings", nao "x0zlheba".
    ///
    /// O ponto (".") tem um significado proprio: a pasta e a MESMA do pai.
    /// O pacote do Epic Games usa <c>DefaultDir = "."</c> na pasta
    /// <c>SELECTEDINSTALLFOLDER</c>, e o caminho real e
    /// "C:\Program Files\Epic Games" — sem o nome da chave no meio.
    /// </summary>
    internal static string OnDiskName(DirectoryRow row)
    {
        var parts = (row.DefaultDir ?? string.Empty).Split('|');
        var first = parts.Length > 0 ? parts[0].Trim() : string.Empty;
        var second = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        if (second.Length > 0) return second;
        if (first.Length > 0) return first;     // "." volta como ".": e a mesma pasta do pai
        return string.Empty;                    // sem DefaultDir: a pasta nao tem nome
    }

    /// <summary>
    /// O nome do arquivo como ele aparece no disco: a tabela <c>File</c>
    /// guarda "curto|longo", e o nome longo e o que o instalador grava.
    /// </summary>
    internal static string LongName(string fileName)
    {
        var parts = (fileName ?? string.Empty).Split('|');
        var longName = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        return longName.Length > 0 ? longName : parts[0].Trim();
    }

    /// <summary>Le uma propriedade do produto instalado.</summary>
    private static string GetProductProperty(string productCode, string property)
    {
        // A API exige um buffer de verdade: com buffer nulo ela devolve
        // ERROR_INVALID_PARAMETER (87) em vez do tamanho necessario.
        foreach (var code in new[] { productCode, "{" + productCode + "}" })
        {
            try
            {
                var size = 512;
                var buffer = new char[size];
                var rc = MsiGetProductInfo(code, property, buffer, ref size);

                if (rc == ErrorMoreData)
                {
                    // A API devolve o tamanho que precisa.
                    buffer = new char[Math.Max(size, 1) + 1];
                    rc = MsiGetProductInfo(code, property, buffer, ref size);
                }

                if (rc != ErrorSuccess) continue;
                return new string(buffer).TrimEnd('\0').Trim();
            }
            catch { }
        }

        return string.Empty;
    }

    /// <summary>O codigo de produto sem chaves, como a API espera.</summary>
    private static string NormalizeProductCode(string productCode) =>
        productCode.Trim().Trim('{', '}');

    /// <summary>
    /// Executa uma consulta SQL do Windows Installer e devolve as linhas.
    ///
    /// A consulta usa o dialeto do proprio instalador, com os nomes das
    /// colunas entre crases. A quantidade de colunas e informada pelo
    /// chamador: e ela que define quantos campos cada registro tem.
    /// </summary>
    private static List<string[]> Query(IntPtr database, string sql, int columns)
    {
        var rows = new List<string[]>();

        if (MsiDatabaseOpenView(database, sql, out var view) != ErrorSuccess) return rows;

        try
        {
            if (MsiViewExecute(view, IntPtr.Zero) != ErrorSuccess) return rows;

            while (true)
            {
                uint rc = MsiViewFetch(view, out var record);
                if (rc == ErrorNoMoreItems) break;
                if (rc != ErrorSuccess) break;

                try
                {
                    var values = new string[columns];
                    for (var i = 0; i < columns; i++) values[i] = RecordString(record, i + 1);
                    rows.Add(values);
                }
                finally
                {
                    MsiCloseHandle(record);
                }
            }
        }
        finally
        {
            MsiCloseHandle(view);
        }

        return rows;
    }

    /// <summary>Le um campo de texto do registro.</summary>
    private static string RecordString(IntPtr record, int field)
    {
        try
        {
            // Mesmo cuidado da propriedade do produto: um buffer de verdade,
            // refeito no tamanho que a API pedir.
            var size = 512;
            var buffer = new char[size];
            var rc = MsiRecordGetString(record, field, buffer, ref size);

            if (rc == ErrorMoreData)
            {
                buffer = new char[Math.Max(size, 1) + 1];
                rc = MsiRecordGetString(record, field, buffer, ref size);
            }

            if (rc != ErrorSuccess) return string.Empty;
            return new string(buffer).TrimEnd('\0').Trim();
        }
        catch { return string.Empty; }
    }
}
