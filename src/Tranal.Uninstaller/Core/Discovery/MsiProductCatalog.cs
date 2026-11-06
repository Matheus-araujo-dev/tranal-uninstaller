using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Um produto instalado pelo Windows Installer (MSI).
///
/// A identidade do produto e o <c>ProductCode</c> (GUID): e ele que o
/// Windows Installer usa para desinstalar, e nao o nome exibido nem o
/// caminho da pasta.
/// </summary>
public sealed class MsiProduct
{
    /// <summary>Nome exibido do produto.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Codigo do produto (GUID entre chaves).</summary>
    public string ProductCode { get; init; } = string.Empty;

    /// <summary>Pasta de instalacao declarada pelo produto (vazia quando desconhecida).</summary>
    public string InstallLocation { get; init; } = string.Empty;

    /// <summary>Comando de desinstalacao: <c>msiexec.exe /x {GUID}</c>.</summary>
    public string UninstallCommand { get; init; } = string.Empty;

    /// <summary>Comando de desinstalacao silenciosa: <c>msiexec.exe /x {GUID} /qn</c>.</summary>
    public string QuietUninstallCommand { get; init; } = string.Empty;

    /// <summary>Empresa que assina o produto.</summary>
    public string Publisher { get; init; } = string.Empty;

    /// <summary>Versao do produto.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Chave de registro de onde o produto foi lido.</summary>
    public string RegistryPath { get; init; } = string.Empty;
}

/// <summary>
/// Resultado de uma desinstalacao por <c>ProductCode</c>.
/// </summary>
public sealed class MsiUninstallResult
{
    /// <summary>True quando o Windows Installer terminou com codigo 0.</summary>
    public bool Success { get; init; }

    /// <summary>Codigo de saida do Windows Installer (3010 = precisa reiniciar).</summary>
    public int ExitCode { get; init; }

    /// <summary>Comando executado (vazio quando o GUID foi recusado).</summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>Explicacao do resultado, em portugues.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Catalogo dos produtos MSI instalados, e a desinstalacao por GUID.
///
/// O Windows Installer e a unica API do Windows que da para desinstalar por
/// identificador estavel: o <c>ProductCode</c>. O comando que o Painel de
/// Controle executa e exatamente <c>msiexec.exe /x {GUID}</c>.
///
/// A listagem vem do registro (<c>Uninstall</c>), que e a mesma fonte que o
/// <see cref="ProgramScanner"/> usa: a chave do produto MSI tem o
/// <c>ProductCode</c> como nome, o que torna a extracao confiavel.
///
/// A leitura e apenas leitura: este catalogo nunca desinstala nada sozinho.
/// A desinstalacao por GUID existe como metodo explicito, e quem chama e
/// quem decide.
/// </summary>
public static class MsiProductCatalog
{
    /// <summary>Executavel do Windows Installer.</summary>
    public const string MsiexecCommand = "msiexec.exe";

    /// <summary>Raiz das entradas de desinstalacao no registro.</summary>
    public const string UninstallRegistryRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    /// <summary>
    /// Normaliza um codigo de produto para a forma canonica <c>{GUID}</c>.
    /// Devolve false quando o texto nao e um GUID.
    /// </summary>
    public static bool TryNormalizeProductCode(string? productCode, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(productCode)) return false;

        var raw = productCode.Trim().Trim('{', '}');
        if (!Guid.TryParse(raw, out var guid)) return false;

        normalized = guid.ToString("B").ToUpperInvariant();
        return true;
    }

    /// <summary>
    /// Monta o comando de desinstalacao pelo <c>ProductCode</c>.
    ///
    /// O comando silencioso e o que o item #8 do backlog pede:
    /// <c>msiexec.exe /x {GUID} /qn</c>. Um GUID invalido e recusado com
    /// mensagem — o chamador mostra o texto ao usuario em vez de disparar um
    /// processo com argumento furado.
    /// </summary>
    public static bool TryBuildUninstallCommand(
        string? productCode,
        bool silent,
        out string command,
        out string error)
    {
        command = string.Empty;
        error = string.Empty;

        if (!TryNormalizeProductCode(productCode, out var code))
        {
            error = "O codigo do produto precisa ser um GUID, como {12345678-1234-1234-1234-123456789ABC}.";
            return false;
        }

        command = silent
            ? $"{MsiexecCommand} /x {code} /qn"
            : $"{MsiexecCommand} /x {code}";

        return true;
    }

    /// <summary>
    /// Monta o comando de desinstalacao pelo <c>ProductCode</c> e falha com
    /// mensagem quando o GUID nao e valido.
    /// </summary>
    /// <exception cref="ArgumentException">Quando o codigo do produto nao e um GUID.</exception>
    public static string BuildUninstallCommand(string productCode, bool silent = false)
    {
        if (!TryBuildUninstallCommand(productCode, silent, out var command, out var error))
            throw new ArgumentException(error, nameof(productCode));

        return command;
    }

    /// <summary>
    /// Lista os produtos MSI instalados nesta maquina (as tres visoes do
    /// registro). Devolve lista vazia quando o registro nao pode ser lido —
    /// a leitura nunca derruba o chamador.
    /// </summary>
    public static IReadOnlyList<MsiProduct> ListInstalled()
    {
        var products = new List<MsiProduct>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Visao de 64 bits e de 32 bits: o mesmo produto pode aparecer nas
        // duas, e a versao de 64 bits tem os dados mais completos.
        foreach (var (hive, root, view) in Roots())
        {
            foreach (var product in ListFromRegistry(hive, root, view))
            {
                if (!seen.Add(product.ProductCode)) continue;
                products.Add(product);
            }
        }

        return products
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Lista os produtos MSI de uma raiz de registro qualquer.
    ///
    /// O parametro <paramref name="rootPath"/> existe para que o teste
    /// possa montar uma arvore sintetica e provar a regra sem depender da
    /// maquina de quem roda a suite.
    /// </summary>
    public static IReadOnlyList<MsiProduct> ListFromRegistry(
        RegistryHive hive,
        string rootPath,
        RegistryView view = RegistryView.Default)
    {
        var products = new List<MsiProduct>();

        using var root = OpenKey(hive, rootPath, view);
        if (root is null) return products;

        string[] names;
        try { names = root.GetSubKeyNames(); }
        catch { return products; }

        foreach (var name in names)
        {
            // A chave de um produto MSI tem o ProductCode como nome.
            if (!TryNormalizeProductCode(name, out var code)) continue;

            RegistryKey? key;
            try { key = root.OpenSubKey(name); }
            catch { continue; }

            if (key is null) continue;

            using (key)
            {
                products.Add(Read(key, code, hive, rootPath, name));
            }
        }

        return products;
    }

    /// <summary>
    /// Desinstala o produto pelo <c>ProductCode</c>, chamando o Windows
    /// Installer. O comando e o mesmo do item #8 do backlog:
    /// <c>msiexec.exe /x {GUID}</c> (com <c>/qn</c> quando silencioso).
    ///
    /// O metodo existe para o Tranal ter um caminho proprio de desinstalacao
    /// por identificador; quem chama decide quando rodar. Devolve o codigo de
    /// saida do Windows Installer (0 = sucesso, 3010 = precisa reiniciar).
    /// </summary>
    public static MsiUninstallResult Uninstall(string productCode, bool silent = true)
    {
        if (!TryBuildUninstallCommand(productCode, silent, out var command, out var error))
        {
            return new MsiUninstallResult
            {
                Success = false,
                ExitCode = -1,
                Command = string.Empty,
                Message = error,
            };
        }

        if (!TryNormalizeProductCode(productCode, out var code))
        {
            // TryBuildUninstallCommand ja recusou o codigo: chegar aqui e impossivel.
            return new MsiUninstallResult { Success = false, ExitCode = -1, Message = error };
        }

        try
        {
            var startInfo = new ProcessStartInfo(MsiexecCommand)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("/x");
            startInfo.ArgumentList.Add(code);
            if (silent) startInfo.ArgumentList.Add("/qn");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new MsiUninstallResult
                {
                    Success = false,
                    ExitCode = -1,
                    Command = command,
                    Message = "Nao foi possivel iniciar o Windows Installer.",
                };
            }

            process.WaitForExit();

            var exitCode = process.ExitCode;
            return new MsiUninstallResult
            {
                Success = exitCode == 0,
                ExitCode = exitCode,
                Command = command,
                Message = exitCode == 0
                    ? "Produto desinstalado."
                    : $"O Windows Installer terminou com o codigo {exitCode}.",
            };
        }
        catch (Exception ex)
        {
            return new MsiUninstallResult
            {
                Success = false,
                ExitCode = -1,
                Command = command,
                Message = $"Falha ao executar o Windows Installer: {ex.Message}",
            };
        }
    }

    /// <summary>
    /// As raizes do registro onde as entradas de desinstalacao moram.
    /// </summary>
    private static IEnumerable<(RegistryHive Hive, string Root, RegistryView View)> Roots()
    {
        yield return (RegistryHive.LocalMachine, UninstallRegistryRoot, RegistryView.Registry64);
        yield return (RegistryHive.LocalMachine, UninstallRegistryRoot, RegistryView.Registry32);
        yield return (RegistryHive.CurrentUser, UninstallRegistryRoot, RegistryView.Default);
    }

    /// <summary>
    /// Abre uma chave de registro sem lancar excecao.
    /// </summary>
    private static RegistryKey? OpenKey(RegistryHive hive, string rootPath, RegistryView view)
    {
        try
        {
            if (hive == RegistryHive.CurrentUser && view == RegistryView.Default)
                return Registry.CurrentUser.OpenSubKey(rootPath);

            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            return baseKey.OpenSubKey(rootPath);
        }
        catch
        {
            // Chave ausente ou sem permissao: a listacao segue sem ela.
            return null;
        }
    }

    /// <summary>
    /// Le um produto da chave de registro.
    /// </summary>
    private static MsiProduct Read(
        RegistryKey key,
        string productCode,
        RegistryHive hive,
        string rootPath,
        string keyName)
    {
        var name = ReadString(key, "DisplayName");
        if (name.Length == 0) name = keyName;

        return new MsiProduct
        {
            DisplayName = name,
            ProductCode = productCode,
            InstallLocation = ReadString(key, "InstallLocation"),
            Publisher = ReadString(key, "Publisher"),
            Version = ReadString(key, "DisplayVersion"),
            UninstallCommand = BuildUninstallCommand(productCode),
            QuietUninstallCommand = BuildUninstallCommand(productCode, silent: true),
            RegistryPath = RegistryPathOf(hive, rootPath, keyName),
        };
    }

    /// <summary>
    /// Le um valor de texto da chave. Devolve string vazia quando o valor
    /// nao existe ou nao e texto.
    /// </summary>
    private static string ReadString(RegistryKey key, string name)
    {
        try
        {
            var value = key.GetValue(name);
            return value is null ? string.Empty : value.ToString() ?? string.Empty;
        }
        catch
        {
            // Valor inacessivel: o produto entra na lista sem esse campo.
            return string.Empty;
        }
    }

    /// <summary>
    /// Monta o caminho completo da chave, para a tela mostrar de onde veio.
    /// </summary>
    private static string RegistryPathOf(RegistryHive hive, string rootPath, string keyName)
    {
        var prefix = hive == RegistryHive.CurrentUser ? "HKEY_CURRENT_USER" : "HKEY_LOCAL_MACHINE";
        return $@"{prefix}\{rootPath}\{keyName}";
    }
}
