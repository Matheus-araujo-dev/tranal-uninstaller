using System.IO;
using System.Text;
using System.Text.Json;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Automation;

/// <summary>
/// A familia do instalador, classificada pela assinatura do arquivo.
/// </summary>
public enum InstallerFamily
{
    /// <summary>Nenhuma regra casou.</summary>
    Desconhecido,

    /// <summary>Inno Setup.</summary>
    InnoSetup,

    /// <summary>Nullsoft Scriptable Install System.</summary>
    Nsis,

    /// <summary>InstallShield.</summary>
    InstallShield,

    /// <summary>Windows Installer (pacote MSI).</summary>
    WindowsInstaller,

    /// <summary>Squirrel.</summary>
    Squirrel,

    /// <summary>InstallAware.</summary>
    InstallAware,

    /// <summary>Advanced Installer.</summary>
    AdvancedInstaller,

    /// <summary>Setup Factory.</summary>
    SetupFactory,

    /// <summary>Arquivo autoextraivel do 7-Zip.</summary>
    SevenZipSfx,
}

/// <summary>
/// A propriedade do arquivo que uma regra compara.
/// </summary>
public enum InstallerProperty
{
    /// <summary>O nome da empresa.</summary>
    CompanyName,

    /// <summary>O nome do produto.</summary>
    ProductName,

    /// <summary>A descricao do arquivo.</summary>
    FileDescription,

    /// <summary>O nome original do arquivo.</summary>
    OriginalFilename,
}

/// <summary>
/// Uma regra do Instal guard: como reconhecer um instalador.
/// </summary>
public sealed class InstallerGuardRule
{
    /// <summary>O nome da regra.</summary>
    public required string Name { get; set; }

    /// <summary>A familia que a regra identifica.</summary>
    public InstallerFamily Family { get; set; } = InstallerFamily.Desconhecido;

    /// <summary>
    /// A assinatura esperada DENTRO do arquivo (texto ASCII).
    ///
    /// E a forma mais confiavel: o nome do produto pode ser qualquer coisa,
    /// mas a assinatura do instalador esta no binario.
    /// </summary>
    public string Signature { get; set; } = string.Empty;

    /// <summary>A propriedade comparada quando a regra usa propriedade.</summary>
    public InstallerProperty Property { get; set; } = InstallerProperty.FileDescription;

    /// <summary>O valor esperado da propriedade (sem distincao de maiusculas).</summary>
    public string PropertyValue { get; set; } = string.Empty;

    /// <summary>True quando a regra foi criada pelo usuario.</summary>
    public bool IsCustom { get; set; }
}

/// <summary>
/// O que o Instal guard encontrou em um arquivo.
/// </summary>
public sealed class InstallerGuardFinding
{
    /// <summary>O caminho analisado.</summary>
    public required string Path { get; init; }

    /// <summary>A familia detectada.</summary>
    public InstallerFamily Family { get; init; } = InstallerFamily.Desconhecido;

    /// <summary>O nome da regra que casou.</summary>
    public string MatchedRule { get; init; } = string.Empty;

    /// <summary>True quando a assinatura foi encontrada no binario.</summary>
    public bool SignatureFound { get; init; }

    /// <summary>O nome da empresa.</summary>
    public string CompanyName { get; init; } = string.Empty;

    /// <summary>O nome do produto.</summary>
    public string ProductName { get; init; } = string.Empty;

    /// <summary>A descricao do arquivo.</summary>
    public string FileDescription { get; init; } = string.Empty;

    /// <summary>O nome original do arquivo.</summary>
    public string OriginalFilename { get; init; } = string.Empty;

    /// <summary>True quando alguma regra reconheceu o arquivo.</summary>
    public bool Detected => Family != InstallerFamily.Desconhecido;

    /// <summary>As propriedades lidas, em uma linha.</summary>
    public string PropertiesText
    {
        get
        {
            var partes = new List<string>();
            if (CompanyName.Length > 0) partes.Add($"empresa={CompanyName}");
            if (ProductName.Length > 0) partes.Add($"produto={ProductName}");
            if (FileDescription.Length > 0) partes.Add($"descricao={FileDescription}");
            if (OriginalFilename.Length > 0) partes.Add($"original={OriginalFilename}");
            return partes.Count == 0 ? "-" : string.Join("; ", partes);
        }
    }
}

/// <summary>
/// O Instal guard: as regras que dizem COMO o programa identifica um setup
/// e quais propriedades do arquivo usar.
///
/// O Ashampoo Uninstall Installer tem o painel "Instal guard", em que o
/// usuario ve as assinaturas que o programa reconhece. No Tranal a mesma
/// informacao serve para algo util de verdade: dizer de que familia e o
/// instalador, para que o desinstalador nativo seja acionado com o switch
/// certo.
///
/// A REGRA mora aqui, em um lugar so: as regras embutidas, a leitura da
/// assinatura, a comparacao das propriedades e a persistencia das regras
/// do usuario.
/// </summary>
public static class InstallerGuard
{
    /// <summary>Quantos bytes do inicio do arquivo sao lidos para procurar a assinatura.</summary>
    public const int SignatureWindowBytes = 1024 * 1024;

    /// <summary>A pasta onde as regras personalizadas ficam gravadas.</summary>
    public static string DataDirectory { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Tranal Group", "Tranal Uninstaller Pro", "installer-guard");

    /// <summary>O arquivo das regras personalizadas.</summary>
    public static string FilePath => System.IO.Path.Combine(DataDirectory, "rules.json");

    /// <summary>
    /// As regras embutidas: as familias de instalador que o Tranal reconhece.
    /// </summary>
    public static IReadOnlyList<InstallerGuardRule> BuiltinRules { get; } = new[]
    {
        new InstallerGuardRule
        {
            Name = "Inno Setup",
            Family = InstallerFamily.InnoSetup,
            Signature = "Inno Setup Setup Data",
        },
        new InstallerGuardRule
        {
            Name = "NSIS",
            Family = InstallerFamily.Nsis,
            Signature = "Nullsoft Install System",
        },
        new InstallerGuardRule
        {
            Name = "Squirrel",
            Family = InstallerFamily.Squirrel,
            Signature = "Squirrel",
        },
        new InstallerGuardRule
        {
            Name = "Setup Factory",
            Family = InstallerFamily.SetupFactory,
            Signature = "Setup Factory",
        },
        new InstallerGuardRule
        {
            Name = "7-Zip SFX",
            Family = InstallerFamily.SevenZipSfx,
            Signature = "7-Zip",
        },
        new InstallerGuardRule
        {
            Name = "InstallShield",
            Family = InstallerFamily.InstallShield,
            Property = InstallerProperty.FileDescription,
            PropertyValue = "InstallShield",
        },
        new InstallerGuardRule
        {
            Name = "InstallAware",
            Family = InstallerFamily.InstallAware,
            Property = InstallerProperty.FileDescription,
            PropertyValue = "InstallAware",
        },
        new InstallerGuardRule
        {
            Name = "Advanced Installer",
            Family = InstallerFamily.AdvancedInstaller,
            Property = InstallerProperty.FileDescription,
            PropertyValue = "Advanced Installer",
        },
        new InstallerGuardRule
        {
            Name = "Windows Installer",
            Family = InstallerFamily.WindowsInstaller,
            Property = InstallerProperty.FileDescription,
            PropertyValue = "Windows Installer",
        },
    };

    /// <summary>
    /// As regras criadas pelo usuario.
    /// </summary>
    public static IReadOnlyList<InstallerGuardRule> CustomRules { get; private set; } = Load();

    /// <summary>
    /// Todas as regras: as embutidas e as do usuario.
    /// </summary>
    public static IReadOnlyList<InstallerGuardRule> Rules =>
        BuiltinRules.Concat(CustomRules).ToList();

    /// <summary>
    /// Analisa um arquivo e diz de que familia de instalador ele e.
    ///
    /// A assinatura tem prioridade: ela esta no binario e nao mente. A
    /// propriedade do arquivo e a segunda via, quando o instalador nao
    /// assina o executavel.
    /// </summary>
    /// <param name="path">O caminho do instalador.</param>
    public static InstallerGuardFinding Detect(string? path)
    {
        var full = PathUtil.Normalize(path);
        if (full.Length == 0)
        {
            return new InstallerGuardFinding { Path = string.Empty };
        }

        var meta = ExecutableMetadataReader.Read(full);
        var finding = new InstallerGuardFinding
        {
            Path = full,
            CompanyName = meta?.CompanyName ?? string.Empty,
            ProductName = meta?.ProductName ?? string.Empty,
            FileDescription = meta?.FileDescription ?? string.Empty,
            OriginalFilename = meta?.OriginalFilename ?? string.Empty,
        };

        var assinatura = ReadSignature(full);
        var extensaoMsi = string.Equals(System.IO.Path.GetExtension(full), ".msi",
                                       StringComparison.OrdinalIgnoreCase);

        foreach (var regra in Rules)
        {
            if (regra.Signature.Length > 0)
            {
                if (assinatura.IndexOf(regra.Signature, StringComparison.OrdinalIgnoreCase) < 0) continue;

                return new InstallerGuardFinding
                {
                    Path = finding.Path,
                    Family = regra.Family,
                    MatchedRule = regra.Name,
                    SignatureFound = true,
                    CompanyName = finding.CompanyName,
                    ProductName = finding.ProductName,
                    FileDescription = finding.FileDescription,
                    OriginalFilename = finding.OriginalFilename,
                };
            }

            if (regra.PropertyValue.Length == 0) continue;

            if (extensaoMsi && regra.Family == InstallerFamily.WindowsInstaller)
            {
                return new InstallerGuardFinding
                {
                    Path = finding.Path,
                    Family = regra.Family,
                    MatchedRule = regra.Name,
                    CompanyName = finding.CompanyName,
                    ProductName = finding.ProductName,
                    FileDescription = finding.FileDescription,
                    OriginalFilename = finding.OriginalFilename,
                };
            }

            var valor = PropertyOf(finding, regra.Property);
            if (valor.IndexOf(regra.PropertyValue, StringComparison.OrdinalIgnoreCase) < 0) continue;

            return new InstallerGuardFinding
            {
                Path = finding.Path,
                Family = regra.Family,
                MatchedRule = regra.Name,
                CompanyName = finding.CompanyName,
                ProductName = finding.ProductName,
                FileDescription = finding.FileDescription,
                OriginalFilename = finding.OriginalFilename,
            };
        }

        return finding;
    }

    /// <summary>
    /// Cria (ou substitui) uma regra do usuario.
    /// </summary>
    /// <param name="regra">A regra a gravar.</param>
    public static void AddCustomRule(InstallerGuardRule regra)
    {
        if (regra is null || string.IsNullOrWhiteSpace(regra.Name)) return;

        regra.IsCustom = true;

        var lista = CustomRules.ToList();
        lista.RemoveAll(r => string.Equals(r.Name, regra.Name, StringComparison.OrdinalIgnoreCase));
        lista.Add(regra);

        CustomRules = lista;
        Save(lista);
    }

    /// <summary>
    /// Apaga uma regra do usuario.
    /// </summary>
    /// <param name="name">O nome da regra.</param>
    public static bool RemoveCustomRule(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;

        var lista = CustomRules
            .Where(r => !string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (lista.Count == CustomRules.Count) return false;

        CustomRules = lista;
        Save(lista);
        return true;
    }

    private static string PropertyOf(InstallerGuardFinding finding, InstallerProperty property) => property switch
    {
        InstallerProperty.CompanyName => finding.CompanyName,
        InstallerProperty.ProductName => finding.ProductName,
        InstallerProperty.OriginalFilename => finding.OriginalFilename,
        _ => finding.FileDescription,
    };

    /// <summary>
    /// Le o inicio do arquivo para procurar a assinatura do instalador.
    ///
    /// A leitura e em Latin-1 de proposito: cada byte vira um caractere, e
    /// a busca por texto funciona mesmo com o arquivo binario.
    /// </summary>
    private static string ReadSignature(string path)
    {
        try
        {
            if (!File.Exists(path)) return string.Empty;

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[Math.Min(SignatureWindowBytes, stream.Length)];
            var lidos = stream.Read(buffer, 0, buffer.Length);
            if (lidos <= 0) return string.Empty;

            return Encoding.Latin1.GetString(buffer, 0, lidos);
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler a assinatura de '{path}': {ex.Message}");
            return string.Empty;
        }
    }

    private static List<InstallerGuardRule> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<InstallerGuardRule>();

            var json = File.ReadAllText(FilePath, Encoding.UTF8);
            if (json.Trim().Length == 0) return new List<InstallerGuardRule>();

            var lista = JsonSerializer.Deserialize<List<InstallerGuardRule>>(json);
            if (lista is null) return new List<InstallerGuardRule>();

            foreach (var regra in lista) regra.IsCustom = true;
            return lista;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao ler as regras do Instal guard: {ex.Message}");
            return new List<InstallerGuardRule>();
        }
    }

    private static void Save(List<InstallerGuardRule> lista)
    {
        try
        {
            System.IO.Directory.CreateDirectory(DataDirectory);
            var json = JsonSerializer.Serialize(lista, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao gravar as regras do Instal guard: {ex.Message}");
        }
    }
}
