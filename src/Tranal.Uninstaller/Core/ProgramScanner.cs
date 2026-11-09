using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// Varre as chaves de desinstalacao do Windows e devolve a lista de programas
/// instalados. Equivalente funcional ao modulo "Todos os Programas".
///
/// Origens (mesma cobertura do Revo):
///   HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall   (64 e 32 bits)
///   HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall
///   HKCR\Installer\Products                                    (pacotes MSI)
/// </summary>
public static class ProgramScanner
{
    private const string UninstallRelative =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private static readonly (RegistryHive Hive, RegistryView View, string Path)[] Sources =
    {
        (RegistryHive.LocalMachine, RegistryView.Registry64, UninstallRelative),
        (RegistryHive.LocalMachine, RegistryView.Registry32, UninstallRelative),
        (RegistryHive.CurrentUser,  RegistryView.Registry64, UninstallRelative),
        (RegistryHive.CurrentUser,  RegistryView.Registry32, UninstallRelative),
    };

    public sealed record ScanOptions
    {
        /// <summary>Inclui componentes do sistema (SystemComponent=1).</summary>
        public bool IncludeSystemComponents { get; init; }

        /// <summary>
        /// Inclui pacotes MSI do catalogo Installer\Products. Desligado por padrao:
        /// o Revo lista somente as chaves de desinstalacao (os 144 produtos de
        /// Installer\Products desta maquina nao aparecem na listagem dele).
        /// </summary>
        public bool IncludeMsiProducts { get; init; }

        /// <summary>Calcula o tamanho real percorrendo a pasta instalada (lento).</summary>
        public bool ComputeFolderSize { get; init; }

        /// <summary>
        /// Oculta entradas sem comando de desinstalacao. O Revo aplica o mesmo
        /// criterio: 6 entradas do registro desta maquina (AMD DVR, AMD Settings,
        /// AMD WVR64, AMD User Experience Program, RyzenMasterSDK e a entrada
        /// duplicada de Battlefield 2042) nao aparecem na listagem dele.
        /// </summary>
        public bool RequireUninstallString { get; init; } = true;

        /// <summary>
        /// Inclui as entradas que NAO tem comando de desinstalador, marcadas
        /// como <see cref="UninstallerHealth.Missing"/>.
        ///
        /// Desligado por padrao, porque o Revo tambem as esconde (AMD DVR,
        /// AMD Settings, AMD WVR64, RyzenMasterSDK...). Ligado, elas aparecem
        /// marcadas — e uma entrada sem desinstalador e uma sobra de
        /// registro, que o usuario precisa ver para limpar na mao.
        /// </summary>
        public bool IncludeEntriesWithoutUninstaller { get; init; }
    }

    /// <summary>
    /// Varre as chaves de desinstalacao. A varredura pode ser interrompida:
    /// o token e conferido a cada chave, e o que ja foi lido e devolvido.
    /// </summary>
    /// <param name="options">As opcoes da varredura.</param>
    /// <param name="cancellation">
    /// O token que interrompe a varredura. O botao "Parar varredura" da
    /// interface cancela por ele.
    /// </param>
    public static List<InstalledProgram> Scan(ScanOptions options, CancellationToken cancellation = default)
    {
        var results = new ConcurrentBag<InstalledProgram>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (cancellation.IsCancellationRequested) return Finalize(results);

        foreach (var (hive, view, path) in Sources)
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var key = root.OpenSubKey(path);
            if (key is null) continue;

            foreach (var subName in key.GetSubKeyNames())
            {
                if (cancellation.IsCancellationRequested) return Finalize(results);

                using var sub = key.OpenSubKey(subName);
                if (sub is null) continue;

                var program = ReadEntry(sub, subName, hive, view, options, path);
                if (program is null) continue;

                // A identidade inclui a chave de origem: o Revo mantem entradas
                // homonimas em chaves diferentes (EA app aparece 2x, igual a ele).
                var identity = $"{program.DisplayName}|{program.DisplayVersion}|{program.RegistryPath}";
                if (!seen.Add(identity)) continue;
                results.Add(program);
            }
        }

        if (options.IncludeMsiProducts)
        {
            foreach (var program in ScanMsiProducts(options))
            {
                if (cancellation.IsCancellationRequested) return Finalize(results);

                var identity = $"{program.DisplayName}|{program.DisplayVersion}|{program.RegistryPath}";
                if (!seen.Add(identity)) continue;
                results.Add(program);
            }
        }

        return Finalize(results);
    }

    /// <summary>Ordena o resultado da varredura pelo nome do programa.</summary>
    private static List<InstalledProgram> Finalize(IEnumerable<InstalledProgram> results) =>
        results
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static InstalledProgram? ReadEntry(
        RegistryKey sub, string subName, RegistryHive hive, RegistryView view,
        ScanOptions options, string uninstallPath)
    {
        var displayName = (GetString(sub, "DisplayName") ?? string.Empty).Trim();
        if (displayName.Length == 0) return null;

        var systemComponent = GetInt(sub, "SystemComponent") == 1;
        if (systemComponent && !options.IncludeSystemComponents) return null;

        var uninstallString = GetString(sub, "UninstallString") ?? string.Empty;
        var quiet = GetString(sub, "QuietUninstallString") ?? string.Empty;
        // Sem comando de desinstalacao a entrada e invisivel para o Revo
        // (AMD DVR, AMD Settings, AMD WVR64, RyzenMasterSDK...). Quem quer
        // ver essas entradas liga IncludeEntriesWithoutUninstaller.
        if (options.RequireUninstallString
            && uninstallString.Trim().Length == 0
            && !options.IncludeEntriesWithoutUninstaller) return null;
        var installLocation = (GetString(sub, "InstallLocation") ?? string.Empty).Trim().Trim('"');
        var displayIcon = GetString(sub, "DisplayIcon") ?? string.Empty;

        var program = new InstalledProgram
        {
            DisplayName = displayName,
            DisplayVersion = (GetString(sub, "DisplayVersion") ?? string.Empty).Trim(),
            Publisher = (GetString(sub, "Publisher") ?? string.Empty).Trim(),
            InstallLocation = installLocation,
            UninstallString = uninstallString,
            QuietUninstallString = quiet,
            DisplayIcon = displayIcon,
            UrlInfoAbout = (GetString(sub, "URLInfoAbout") ?? string.Empty).Trim(),
            Comments = (GetString(sub, "Comments") ?? string.Empty).Trim(),
            RegistryKey = subName,
            RegistryHive = hive.ToString(),
            RegistryPath = $@"{hive}\{uninstallPath}\{subName}",
            View = view,
            IsSystemComponent = systemComponent,
            IsWindowsInstaller = GetInt(sub, "WindowsInstaller") == 1,
            IsUpgrade = GetInt(sub, "Upgrade") == 1,
            Installer = (GetString(sub, "InstallSource") ?? string.Empty).Trim(),
            // "NoRemove" no registro e o jeito do Windows dizer que a entrada
            // nao deve ser removida.
            IsProtected = GetInt(sub, "NoRemove") == 1,
        };

        // A saude do desinstalador e lida sempre: e o que permite avisar que
        // o desinstalador aponta para um arquivo que nao existe mais.
        program.UninstallerHealth = UninstallerHealthEvaluator.Evaluate(program);

        // O codigo do produto vem do valor "ProductCode" do registro; quando
        // o registro nao tem o valor, ele e extraido do comando de
        // desinstalacao (que no MSI e "MsiExec.exe /X{GUID}").
        var productCode = (GetString(sub, "ProductCode") ?? string.Empty).Trim();
        if (productCode.Length == 0)
            productCode = UninstallerClassifier.ExtractProductCode(uninstallString);
        program.ProductCode = productCode;

        // O tipo do desinstalador decide a estrategia de desinstalacao, e
        // por isso e classificado sempre — nao e um detalhe opcional.
        program.Kind = UninstallerClassifier.Classify(program);

        // Com o tipo em maos, da para saber se existe um jeito conhecido de
        // silenciar aquele desinstalador.
        program.QuietUninstallCommand = QuietUninstall.Build(program);

        var estimatedKb = GetInt(sub, "EstimatedSize");
        if (estimatedKb > 0) program.SizeBytes = estimatedKb * 1024L;

        program.InstallDateValue = ResolveEffectiveDate(sub, installLocation, uninstallString, quiet);
        program.InstallDate = program.InstallDateValue?.ToString("dd/MM/yyyy") ?? string.Empty;

        program.Bits = DetectBits(uninstallString, quiet, displayIcon, view, installLocation);

        if (options.ComputeFolderSize && program.SizeBytes == 0 && installLocation.Length > 0)
            program.SizeBytes = MeasureFolder(installLocation);

        return program;
    }


    /// <summary>
    /// Data efetiva de instalacao — ordem CALIBRADA contra o grupo
    /// "Novos Programas" do Revo Uninstaller Pro 5.5.4 (17 de 18 entradas
    /// coincidentes; ver tools/calib_full.py):
    ///
    ///   1) data de modificacao da pasta de instalacao — e a fonte que o Revo usa:
    ///      "Visual Studio Community 2026" tem InstallDate 04/09 no registro e
    ///      pasta de 29/10, e o Revo o trata como programa novo;
    ///   2) InstallDate do registro;
    ///   3) InstallerDate (MSI, FILETIME);
    ///   4) data do executavel de desinstalacao.
    /// </summary>
    private static DateTime? ResolveEffectiveDate(
        RegistryKey sub, string installLocation, string uninstallString, string quietUninstallString)
    {
        // 1) Pasta de instalacao.
        if (!string.IsNullOrWhiteSpace(installLocation))
        {
            try
            {
                if (Directory.Exists(installLocation))
                {
                    var dir = new DirectoryInfo(installLocation);
                    if (dir.LastWriteTime.Year > 1990) return dir.LastWriteTime;
                    if (dir.CreationTime.Year > 1990) return dir.CreationTime;
                }
            }
            catch { }
        }

        // 2) InstallDate do registro.
        var installDate = ReadInstallDateOnly(sub);
        if (installDate is not null) return installDate;

        // 3) InstallerDate do MSI.
        var installerDate = ReadInstallerDate(sub);
        if (installerDate is not null) return installerDate;

        // 4) Executavel de desinstalacao.
        foreach (var command in new[] { uninstallString, quietUninstallString })
        {
            var exe = ExtractExecutablePath(command);
            if (exe.Length == 0) continue;
            try
            {
                if (File.Exists(exe))
                {
                    var info = new FileInfo(exe);
                    if (info.LastWriteTime.Year > 1990) return info.LastWriteTime;
                    if (info.CreationTime.Year > 1990) return info.CreationTime;
                }
            }
            catch { }
        }

        return null;
    }

    /// <summary>Le apenas o campo InstallDate do registro.</summary>
    private static DateTime? ReadInstallDateOnly(RegistryKey sub)
    {
        var raw = GetString(sub, "InstallDate");
        if (string.IsNullOrWhiteSpace(raw)) return null;

        if (raw.Length == 8 && DateTime.TryParseExact(raw, "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsed))
            return parsed;

        if (DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var any))
            return any;

        return null;
    }

    /// <summary>Le o InstallerDate do MSI (um FILETIME).</summary>
    private static DateTime? ReadInstallerDate(RegistryKey sub)
    {
        var installerDate = GetLong(sub, "InstallerDate");
        if (installerDate <= 0) return null;

        try { return DateTime.FromFileTimeUtc(installerDate).ToLocalTime(); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>
    /// Deduz 32/64 bits pelo executavel de desinstalacao, pelo icone ou pela
    /// visao de registro em que a entrada foi encontrada.
    /// </summary>
    private static string DetectBits(
        string uninstallString, string quietUninstallString, string displayIcon,
        RegistryView view, string installLocation)
    {
        foreach (var candidate in new[] { uninstallString, quietUninstallString, displayIcon })
        {
            var exe = ExtractExecutablePath(candidate);
            if (exe.Length == 0) continue;
            var machine = PeMachineReader.ReadMachine(exe);
            switch (machine)
            {
                case 0x8664: return "64-bits";
                case 0x01c4 or 0xaa64: return "64-bits";
                case 0x014c: return "32-bits";
                case 0x01c0 or 0x01c2: return "32-bits";
            }
        }

        if (view == RegistryView.Registry32) return "32-bits";
        if (view == RegistryView.Registry64) return "64-bits";
        if (installLocation.Contains("(x86)", StringComparison.OrdinalIgnoreCase)) return "32-bits";
        return string.Empty;
    }

    /// <summary>
    /// Extrai o caminho do executavel de uma string de desinstalacao.
    ///
    /// Trata os dois formatos reais no registro:
    ///   "C:\Program Files\App\unins000.exe" /SILENT   (com aspas)
    ///   C:\Program Files\App\unins000.exe /SILENT       (sem aspas)
    /// No segundo caso, cortar no primeiro espaco devolveria "C:\Program" e o
    /// arquivo nao seria encontrado — foi o que impediu o Tranal de datar
    /// programas como "SteelSeries GG" e "WebAdvisor da McAfee".
    /// </summary>
    public static string ExtractExecutablePath(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return string.Empty;
        var text = command.Trim();

        // Remove argumentos comuns no fim.
        foreach (var arg in new[] { "/I", "/i", "/X", "/x", "/uninstall", "/uninst32", "/uninst64", "/silent", "/verysilent", "/quiet", "/norestart", "/S", "/s" })
        {
            if (text.EndsWith(" " + arg, StringComparison.OrdinalIgnoreCase))
                text = text[..^arg.Length].Trim();
        }

        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            if (end > 0) return text[1..end];
            return text[1..];
        }

        // Sem aspas: procura o primeiro executavel (.exe/.com/.bat) da string.
        var lower = text.ToLowerInvariant();
        foreach (var extension in new[] { ".exe", ".com", ".bat" })
        {
            var index = lower.IndexOf(extension, StringComparison.Ordinal);
            if (index > 0) return text[..(index + extension.Length)];
        }

        // Sem espaco: pega ate o primeiro espaco.
        var space = text.IndexOf(' ');
        return space > 0 ? text[..space] : text;
    }

    private static IEnumerable<InstalledProgram> ScanMsiProducts(ScanOptions options)
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Default);
        using var products = root.OpenSubKey(@"Installer\Products");
        if (products is null) yield break;

        foreach (var productCode in products.GetSubKeyNames())
        {
            using var product = products.OpenSubKey(productCode);
            if (product is null) continue;

            var name = GetString(product, "ProductName");
            if (string.IsNullOrWhiteSpace(name)) continue;

            // ProductCode formatado {GUID}
            var guid = productCode;
            if (guid.Length == 38 && guid.StartsWith('{'))
                guid = guid[..37] + guid[37..];

            using var msiKey = root.OpenSubKey($@"Installer\Products\{productCode}");
            var localPackage = msiKey is null
                ? string.Empty
                : GetString(msiKey, "LocalPackage") ?? string.Empty;

            var installSource = string.Empty;
            using (var sourceKey = root.OpenSubKey($@"Installer\Products\{productCode}\SourceList"))
            {
                if (sourceKey is not null)
                {
                    var first = sourceKey.GetValueNames().FirstOrDefault();
                    if (first is not null) installSource = sourceKey.GetValue(first)?.ToString() ?? string.Empty;
                }
            }

            // O comando de desinstalacao e o comando silencioso sao montados
            // aqui, porque o MSI tem um switch silencioso proprio
            // ("/qn") e nao ha como referenciar o objeto dentro do
            // inicializador.
            var msiUninstall = $@"MsiExec.exe /X{{{productCode}}}";
            var msiQuiet = $@"{msiUninstall} /qn";

            yield return new InstalledProgram
            {
                DisplayName = name.Trim(),
                DisplayVersion = (GetString(product, "Version") ?? string.Empty).Trim(),
                Publisher = string.Empty,
                UninstallString = msiUninstall,
                QuietUninstallString = msiQuiet,
                QuietUninstallCommand = msiQuiet,
                RegistryKey = productCode,
                RegistryHive = "ClassesRoot",
                RegistryPath = $@"HKEY_CLASSES_ROOT\Installer\Products\{productCode}",
                IsMsi = true,
                IsWindowsInstaller = true,
                Installer = installSource,
                Comments = localPackage,
                Bits = "32-bits",
                Kind = UninstallerKind.MsiExec,
                ProductCode = productCode,
            };
        }
    }

    public static long MeasureFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return 0;

            // A travessia nao atravessa atalho (junction, symlink): o alvo
            // pode ser outra unidade inteira, e a conta nao terminaria.
            return SafeTraversal.MeasureSize(path);
        }
        catch { return 0; }
    }

    /// <summary>
    /// Le um valor de string do registro.
    ///
    /// NÃO expande variáveis de ambiente: o Revo (aplicativo nativo) usa
    /// RegQueryValueEx e recebe o valor CRU de REG_EXPAND_SZ
    /// (ex.: "%windir%\system32\sdbinst.exe"). O .NET expandiria para
    /// "C:\WINDOWS\system32\sdbinst.exe" — e o programa "Microsoft Windows
    /// Application Compatibility Fix Database" passaria a ter data, entrando
    /// no grupo "Novos Programas" onde o Revo nao o coloca. Medido.
    /// </summary>
    private static string? GetString(RegistryKey key, string name)
    {
        try
        {
            var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            return value switch
            {
                null => null,
                string s => s,
                byte[] b => System.Text.Encoding.Unicode.GetString(b).TrimEnd('\0'),
                _ => value.ToString(),
            };
        }
        catch { return null; }
    }

    private static int GetInt(RegistryKey key, string name)
    {
        try
        {
            var value = key.GetValue(name);
            if (value is null) return 0;
            return Convert.ToInt32(value);
        }
        catch { return 0; }
    }

    /// <summary>Le um valor numerico longo (usado pelo InstallerDate do MSI, um FILETIME).</summary>
    private static long GetLong(RegistryKey key, string name)
    {
        try
        {
            var value = key.GetValue(name);
            if (value is null) return 0;
            return Convert.ToInt64(value);
        }
        catch { return 0; }
    }
}

/// <summary>Le o campo Machine do header PE para deduzir a arquitetura.</summary>
internal static class PeMachineReader
{
    public static ushort ReadMachine(string path)
    {
        try
        {
            if (!File.Exists(path)) return 0;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var br = new BinaryReader(fs);

            if (br.ReadUInt16() != 0x5A4D) return 0; // 'MZ'
            fs.Seek(0x3C, SeekOrigin.Begin);
            var peOffset = br.ReadUInt32();
            fs.Seek(peOffset, SeekOrigin.Begin);
            if (br.ReadUInt32() != 0x00004550) return 0; // 'PE\0\0'
            br.ReadUInt16(); // machine
            return br.ReadUInt16();
        }
        catch { return 0; }
    }
}
