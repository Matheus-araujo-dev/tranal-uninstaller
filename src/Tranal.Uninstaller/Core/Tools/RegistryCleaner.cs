using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Management;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Uma categoria do Limpador de Registro.
///
/// A categoria e a unidade de agrupamento da tela: o usuario ve "ActiveX (7)",
/// "DLLs compartilhadas (12)" e decide bloco a bloco.
/// </summary>
public sealed class RegistryCleanerCategory
{
    /// <summary>Identificador estavel da categoria (usado na linha de comando).</summary>
    public required string Id { get; init; }

    /// <summary>Nome exibido (pt-BR).</summary>
    public required string Name { get; init; }

    /// <summary>Explicacao curta do que a categoria procura.</summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Uma entrada obsoleta encontrada pelo Limpador de Registro.
///
/// A entrada pode ser uma CHAVE inteira ou um VALOR dentro de uma chave.
/// A distincao importa: apagar a chave ".docx" inteira levaria junto as
/// associacoes que ainda valem; apagar o valor default dela tira so a
/// associacao morta.
/// </summary>
public sealed class RegistryCleanerEntry
{
    /// <summary>Identificador da categoria da entrada.</summary>
    public required string CategoryId { get; init; }

    /// <summary>Nome da categoria (pt-BR), para exibir sem consultar a lista.</summary>
    public required string CategoryName { get; init; }

    /// <summary>
    /// Caminho completo da chave, no formato do Editor do Registro
    /// (ex.: <c>HKEY_LOCAL_MACHINE\SOFTWARE\...\App Paths\app.exe</c>).
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Nome do valor obsoleto. Vazio quando a entrada e a chave inteira.
    /// </summary>
    public string ValueName { get; init; } = string.Empty;

    /// <summary>Explicacao do porque a entrada e obsoleta.</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>True quando a entrada esta marcada para remocao.</summary>
    public bool Selected { get; set; } = true;

    /// <summary>True quando a entrada e um valor, e nao uma chave.</summary>
    public bool IsValue => ValueName.Length > 0;

    /// <summary>
    /// O hive da chave. Guardado aqui porque o caminho exibido nao basta para
    /// apagar: a mesma chave pode existir nas visoes de 32 e de 64 bits, e a
    /// que foi lida e a que tem que ser apagada.
    /// </summary>
    public RegistryHive Hive { get; init; }

    /// <summary>A visao do registro em que a chave foi lida.</summary>
    public RegistryView View { get; init; } = RegistryView.Default;

    /// <summary>O caminho da chave dentro do hive (sem o nome do hive).</summary>
    public string RelativePath { get; init; } = string.Empty;
}

/// <summary>
/// Limpador de Registro: procura as sobras obsoletas do registro por
/// categoria e permite remove-las.
///
/// A REGRA e uma so, e ela vale para todas as categorias: **um registro e
/// sobra quando o caminho que ele aponta nao existe mais no disco**. Nao e
/// casamento de nome, nao e data antiga, nao e "o programa nao esta na lista":
/// e o arquivo referenciado que sumiu.
///
/// As 16 categorias cobrem os rastros que os concorrentes medem — o limpador de
/// registro do HiBit (ActiveX, FileTypes, SharedDLLs, IFEO, FirewallRules,
/// MUICache), o deteccao de sobras por MuiCache do Uninstalr, as bibliotecas de
/// tipos do Total Uninstall.
///
/// Regra de seguranca: uma chave que aponta para um arquivo compartilhado por
/// outro programa **nao** e apagada por este limpador. A regra de "arquivo
/// compartilhado" mora em <see cref="SharedReferenceCounter"/>, e e la que a
/// contagem de quem usa o arquivo acontece.
/// </summary>
public static class RegistryCleaner
{
    /// <summary>Categoria de controles ActiveX registrados.</summary>
    public const string CategoryActiveX = "ActiveX";

    /// <summary>Categoria dos caminhos de aplicativos (App Paths).</summary>
    public const string CategoryAppPaths = "AppPaths";

    /// <summary>Categoria das classes COM registradas.</summary>
    public const string CategoryComClasses = "ComClasses";

    /// <summary>Categoria das associacoes de tipo de arquivo.</summary>
    public const string CategoryFileTypes = "FileTypes";

    /// <summary>Categoria das regras de firewall.</summary>
    public const string CategoryFirewallRules = "FirewallRules";

    /// <summary>Categoria das fontes registradas.</summary>
    public const string CategoryFonts = "Fonts";

    /// <summary>Categoria do Image File Execution Options.</summary>
    public const string CategoryIfeo = "Ifeo";

    /// <summary>Categoria dos produtos registrados do Windows Installer.</summary>
    public const string CategoryInstallerProducts = "InstallerProducts";

    /// <summary>Categoria do cache do Menu Iniciar (MuiCache).</summary>
    public const string CategoryMuiCache = "MuiCache";

    /// <summary>Categoria das entradas "Abrir com".</summary>
    public const string CategoryOpenWith = "OpenWith";

    /// <summary>Categoria dos identificadores de programa (ProgID).</summary>
    public const string CategoryProgIds = "ProgIds";

    /// <summary>Categoria dos itens de inicializacao.</summary>
    public const string CategoryRunEntries = "RunEntries";

    /// <summary>Categoria das DLLs compartilhadas.</summary>
    public const string CategorySharedDlls = "SharedDlls";

    /// <summary>Categoria das extensoes do shell.</summary>
    public const string CategoryShellExtensions = "ShellExtensions";

    /// <summary>Categoria das bibliotecas de tipos (TypeLib).</summary>
    public const string CategoryTypeLib = "TypeLib";

    /// <summary>Categoria das entradas de desinstalacao orfas.</summary>
    public const string CategoryUninstallEntries = "UninstallEntries";

    /// <summary>
    /// As categorias do limpador, na ordem em que aparecem na tela.
    /// </summary>
    public static IReadOnlyList<RegistryCleanerCategory> Categories { get; } = new[]
    {
        new RegistryCleanerCategory
        {
            Id = CategoryActiveX,
            Name = "Controles ActiveX",
            Description = "Controles ActiveX registrados cuja DLL nao existe mais.",
        },
        new RegistryCleanerCategory
        {
            Id = CategoryAppPaths,
            Name = "Caminhos de aplicativos",
            Description = "Atalhos de execucao (App Paths) que apontam para programas removidos.",
        },
        new RegistryCleanerCategory
        {
            Id = CategoryComClasses,
            Name = "Classes COM",
            Description = "Classes COM registradas sem nenhum servidor (DLL ou EXE) valido.",
        },
        new RegistryCleanerCategory
        {
            Id = CategoryFileTypes,
            Name = "Tipos de arquivo",
            Description = "Associacoes de extensao que apontam para programas removidos.",
        },
        new RegistryCleanerCategory
        {
            Id = CategoryFirewallRules,
            Name = "Regras de firewall",
            Description = "Regras de firewall que liberam programas que nao existem mais.",
        },
        new RegistryCleanerCategory
        {
            Id = CategoryFonts,
            Name = "Fontes",
            Description = "Fontes registradas cujo arquivo de fonte nao existe mais.",
        },
        new RegistryCleanerCategory
        {
            Id = CategoryIfeo,
            Name = "Image File Execution Options",
            Description = "Opcoes de execucao de imagem que valem para programas inexistentes.",
        },
        new RegistryCleanerCategory
        {
            Id = CategoryInstallerProducts,
            Name = "Produtos do Windows Installer",
            Description = "Produtos do instalador do Windows registrados sem as propriedades.",
        },
        new RegistryCleanerCategory
        {
            Id = CategoryMuiCache,
            Name = "Cache do Menu Iniciar",
            Description = "Entradas do MuiCache que ainda apontam para programas removidos.",
        },
        new RegistryCleanerCategory
        {
            Id = CategoryOpenWith,
            Name = "Abrir com",
            Description = "Programas registrados em \"Abrir com\" que nao existem mais.",
        },
        new RegistryCleanerCategory
        {
            Id = CategoryProgIds,
            Name = "Identificadores de programa",
            Description = "Identificadores de programa (ProgID) cujo comando nao existe mais.",
        },
        new RegistryCleanerCategory
        {
            Id = CategoryRunEntries,
            Name = "Itens de inicializacao",
            Description = "Itens que sobem com o Windows e apontam para programas removidos.",
        },
        new RegistryCleanerCategory
        {
            Id = CategorySharedDlls,
            Name = "DLLs compartilhadas",
            Description = "DLLs compartilhadas cujo arquivo nao existe mais.",
        },
        new RegistryCleanerCategory
        {
            Id = CategoryShellExtensions,
            Name = "Extensoes do shell",
            Description = "Contextos de menu do shell que apontam para classes inexistentes.",
        },
        new RegistryCleanerCategory
        {
            Id = CategoryTypeLib,
            Name = "Bibliotecas de tipos",
            Description = "Bibliotecas de tipos (TypeLib) registradas sem o arquivo da DLL.",
        },
        new RegistryCleanerCategory
        {
            Id = CategoryUninstallEntries,
            Name = "Entradas de desinstalacao",
            Description = "Entradas de desinstalacao cujo programa e desinstalador nao existem mais.",
        },
    };

    /// <summary>
    /// Procura as sobras obsoletas em TODAS as categorias.
    /// Nunca lanca: uma categoria que falha devolve vazio, e as outras seguem.
    /// </summary>
    public static IReadOnlyList<RegistryCleanerEntry> Scan() => Scan(null);

    /// <summary>
    /// Procura as sobras obsoletas nas categorias indicadas.
    /// </summary>
    /// <param name="categoryIds">
    /// Os identificadores das categorias. Nulo ou vazio significa todas.
    /// </param>
    public static IReadOnlyList<RegistryCleanerEntry> Scan(IEnumerable<string>? categoryIds)
    {
        var desejadas = new HashSet<string>(
            categoryIds?.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim())
                ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        var todas = desejadas.Count == 0;

        var entradas = new List<RegistryCleanerEntry>();

        void Adicionar(string id, Action<List<RegistryCleanerEntry>> rotina)
        {
            if (!todas && !desejadas.Contains(id)) return;

            var doCategoria = new List<RegistryCleanerEntry>();
            try
            {
                rotina(doCategoria);
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao varrer a categoria '{id}': {ex.Message}");
                return;
            }

            entradas.AddRange(doCategoria);
        }

        Adicionar(CategoryActiveX, e => ScanActiveX(e));
        Adicionar(CategoryAppPaths, e => ScanAppPaths(e));
        Adicionar(CategoryComClasses, e => ScanComClasses(e));
        Adicionar(CategoryFileTypes, e => ScanFileTypes(e));
        Adicionar(CategoryFirewallRules, e => ScanFirewallRules(e));
        Adicionar(CategoryFonts, e => ScanFonts(e));
        Adicionar(CategoryIfeo, e => ScanIfeo(e));
        Adicionar(CategoryInstallerProducts, e => ScanInstallerProducts(e));
        Adicionar(CategoryMuiCache, e => ScanMuiCache(e));
        Adicionar(CategoryOpenWith, e => ScanOpenWith(e));
        Adicionar(CategoryProgIds, e => ScanProgIds(e));
        Adicionar(CategoryRunEntries, e => ScanRunEntries(e));
        Adicionar(CategorySharedDlls, e => ScanSharedDlls(e));
        Adicionar(CategoryShellExtensions, e => ScanShellExtensions(e));
        Adicionar(CategoryTypeLib, e => ScanTypeLib(e));
        Adicionar(CategoryUninstallEntries, e => ScanUninstallEntries(e));

        return entradas;
    }

    /// <summary>
    /// Remove as entradas obsoletas informadas.
    ///
    /// As CHAVES passam pelo motor canonico (<see cref="UninstallEngine"/>),
    /// que respeita a lista de exclusoes, cria o backup e o ponto de
    /// restauracao. Os VALORES sao apagados pelo proprio limpador, porque o
    /// motor canonico trabalha com chaves.
    /// </summary>
    /// <param name="entries">As entradas a remover.</param>
    public static RemovalReport Remove(IEnumerable<RegistryCleanerEntry> entries)
    {
        var lista = entries?.Where(e => e is not null && e.Selected).ToList()
                    ?? new List<RegistryCleanerEntry>();

        var itensDeChave = new List<LeftoverItem>();
        var itensDeValor = new List<RegistryCleanerEntry>();

        foreach (var entrada in lista)
        {
            if (entrada.IsValue)
            {
                itensDeValor.Add(entrada);
                continue;
            }

            if (SafetyRules.IsProtectedRegistryKey(entrada.Path))
            {
                ToolLog.Info($"Chave de registro protegida, ignorada: {entrada.Path}");
                continue;
            }

            itensDeChave.Add(new LeftoverItem
            {
                Kind = "Registro",
                Path = entrada.Path,
                Detail = $"{entrada.CategoryName}: {entrada.Detail}",
                Selected = true,
                Confidence = LeftoverConfidence.High,
                Reason = LeftoverReason.OrphanedRegistryEntry,
            });
        }

        var resultados = new List<RemovalOutcome>();

        if (itensDeChave.Count > 0)
        {
            var relatorio = UninstallEngine.RemoveLeftovers(itensDeChave);
            resultados.AddRange(relatorio.Outcomes);
        }

        foreach (var entrada in itensDeValor)
        {
            resultados.Add(RemoveValue(entrada));
        }

        return new RemovalReport(resultados);
    }

    /// <summary>
    /// Apaga um valor do registro e devolve o resultado como item de remocao.
    /// </summary>
    private static RemovalOutcome RemoveValue(RegistryCleanerEntry entrada)
    {
        var item = new LeftoverItem
        {
            Kind = "Valor de Registro",
            Path = entrada.Path,
            Detail = $"{entrada.CategoryName}: valor '{entrada.ValueName}' — {entrada.Detail}",
            Selected = true,
            Confidence = LeftoverConfidence.High,
            Reason = LeftoverReason.OrphanedRegistryEntry,
        };

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(entrada.Hive, entrada.View);
            using var chave = baseKey.OpenSubKey(entrada.RelativePath, writable: true);

            if (chave is null)
            {
                return new RemovalOutcome
                {
                    Item = item,
                    Status = RemovalStatus.AlreadyAbsent,
                    Message = "A chave nao existe mais.",
                };
            }

            var nomes = chave.GetValueNames();
            if (!nomes.Contains(entrada.ValueName, StringComparer.OrdinalIgnoreCase))
            {
                return new RemovalOutcome
                {
                    Item = item,
                    Status = RemovalStatus.AlreadyAbsent,
                    Message = "O valor nao existe mais.",
                };
            }

            chave.DeleteValue(entrada.ValueName, throwOnMissingValue: false);

            return new RemovalOutcome
            {
                Item = item,
                Status = RemovalStatus.Removed,
                Message = "Valor removido do registro.",
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            return new RemovalOutcome
            {
                Item = item,
                Status = RemovalStatus.Failed,
                Message = $"Sem permissao para alterar o registro: {ex.Message}",
                ErrorCode = RemovalOutcome.ErrorCodeOf(ex),
            };
        }
        catch (System.Security.SecurityException ex)
        {
            return new RemovalOutcome
            {
                Item = item,
                Status = RemovalStatus.Failed,
                Message = $"Sem permissao para alterar o registro: {ex.Message}",
                ErrorCode = RemovalOutcome.ErrorCodeOf(ex),
            };
        }
        catch (Exception ex)
        {
            return new RemovalOutcome
            {
                Item = item,
                Status = RemovalStatus.Failed,
                Message = ex.Message,
                ErrorCode = RemovalOutcome.ErrorCodeOf(ex),
            };
        }
    }

    // ---------------------------------------------------------------------
    // Categorias
    // ---------------------------------------------------------------------

    /// <summary>
    /// Controles ActiveX registrados cuja DLL sumiu.
    /// </summary>
    private static void ScanActiveX(List<RegistryCleanerEntry> entradas)
    {
        foreach (var raiz in ClassesRoots())
        {
            using var chave = raiz.Open("ActiveX");
            if (chave is null) continue;

            foreach (var clsid in chave.SubKeys())
            {
                // O nome da subchave ja e o CLSID do controle.
                using var inproc = chave.Open(clsid + "\\InprocServer32");
                if (inproc is null) continue;

                var dll = inproc.ReadDefaultValue(string.Empty);
                if (dll.Length == 0) continue;

                if (File.Exists(dll)) continue;

                entradas.Add(NewEntry(
                    CategoryActiveX,
                    raiz.Canonical("ActiveX\\" + clsid),
                    "",
                    $"controle ActiveX {clsid} aponta para uma DLL inexistente: {dll}",
                    RegistryHive.ClassesRoot,
                    raiz.View,
                    "ActiveX\\" + clsid));
            }
        }
    }

    /// <summary>
    /// Caminhos de aplicativos (App Paths) que apontam para programas removidos.
    /// </summary>
    private static void ScanAppPaths(List<RegistryCleanerEntry> entradas)
    {
        const string relativo = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";

        foreach (var local in SoftwareLocations())
        {
            using var chave = local.Open(relativo);
            if (chave is null) continue;

            foreach (var exe in chave.SubKeys())
            {
                var caminho = chave.ReadDefaultValue(exe);
                if (caminho.Length == 0) continue;

                var limpo = caminho.Trim().Trim('"');
                if (limpo.Length == 0) continue;

                // O valor pode ser so o nome do executavel (o Windows procura
                // nos diretorios do PATH); nesse caso nao ha caminho para
                // conferir, e a entrada nao e julgada.
                if (!limpo.Contains('\\')) continue;

                if (File.Exists(limpo)) continue;

                entradas.Add(NewEntry(
                    CategoryAppPaths,
                    local.Canonical(relativo + "\\" + exe),
                    "",
                    $"atalho de execucao aponta para um programa inexistente: {limpo}",
                    RegistryHive.LocalMachine,
                    local.View,
                    relativo + "\\" + exe));
            }
        }
    }

    /// <summary>
    /// Classes COM registradas sem nenhum servidor.
    ///
    /// O criterio e estrito de proposito: a classe so e sobra quando nao tem
    /// servidor (InprocServer32, LocalServer32, LocalServer ou
    /// InprocHandler32), nao tem outras subchaves e nao tem valores. Qualquer
    /// um desses presentes significa que a classe ainda diz algo ao Windows.
    /// </summary>
    private static void ScanComClasses(List<RegistryCleanerEntry> entradas)
    {
        var servidores = new[]
        {
            "InprocServer32", "LocalServer32", "LocalServer", "InprocHandler32",
        };

        foreach (var raiz in ClassesRoots())
        {
            using var chave = raiz.Open("CLSID");
            if (chave is null) continue;

            foreach (var clsid in chave.SubKeys())
            {
                if (!clsid.StartsWith("{", StringComparison.Ordinal)) continue;

                var subchaves = chave.SubKeys(clsid);
                if (subchaves.Any(s => servidores.Contains(s, StringComparer.OrdinalIgnoreCase))) continue;
                if (subchaves.Length > 0) continue;
                if (chave.ValueNames(clsid).Length > 0) continue;

                entradas.Add(NewEntry(
                    CategoryComClasses,
                    raiz.Canonical("CLSID\\" + clsid),
                    "",
                    "classe COM registrada sem servidor (nem DLL, nem EXE)",
                    RegistryHive.ClassesRoot,
                    raiz.View,
                    "CLSID\\" + clsid));
            }
        }
    }

    /// <summary>
    /// Associacoes de extensao que apontam para programas removidos.
    ///
    /// Apaga-se o VALOR default da extensao (a associacao morta), nunca a
    /// chave inteira: a chave pode ter outras associacoes validas.
    /// </summary>
    private static void ScanFileTypes(List<RegistryCleanerEntry> entradas)
    {
        foreach (var raiz in ClassesRoots())
        {
            using var chave = raiz.Open(string.Empty);
            if (chave is null) continue;

            foreach (var extensao in chave.SubKeys())
            {
                if (!extensao.StartsWith(".", StringComparison.Ordinal)) continue;

                var progId = chave.ReadDefaultValue(extensao);
                if (progId.Length == 0) continue;
                if (progId.Contains("*", StringComparison.Ordinal)) continue;

                var comando = ReadOpenCommand(raiz, progId);
                if (comando.Length == 0) continue;

                var exe = ExecutablePathResolver.Extract(comando);
                if (exe.Length == 0) continue;
                if (!LooksLikePath(exe)) continue;
                if (ExecutableExists(exe)) continue;

                entradas.Add(NewEntry(
                    CategoryFileTypes,
                    raiz.Canonical(extensao),
                    string.Empty,
                    $"a associacao '{progId}' aponta para um programa inexistente: {exe}",
                    RegistryHive.ClassesRoot,
                    raiz.View,
                    extensao));
            }
        }
    }

    /// <summary>
    /// Regras de firewall que liberam programas que nao existem mais.
    ///
    /// O valor da regra tem o formato
    /// <c>{GUID}...|App=C:\Program Files\app.exe|...</c>: o caminho vem no
    /// segmento <c>App=</c>.
    /// </summary>
    private static void ScanFirewallRules(List<RegistryCleanerEntry> entradas)
    {
        const string relativo =
            @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules";

        var local = new LocalLocation(RegistryHive.LocalMachine, RegistryView.Registry64, "HKEY_LOCAL_MACHINE");
        using var chave = local.Open(relativo);
        if (chave is null) return;

        foreach (var nome in chave.ValueNames())
        {
            var valor = chave.ReadValue(nome);
            if (valor.Length == 0) continue;

            var caminho = ExtrairSegmento(valor, "App=");
            if (caminho.Length == 0) continue;

            // A regra do Windows grava o caminho com variaveis de ambiente
            // ("%SystemRoot%\system32\svchost.exe").
            var limpo = Expandir(caminho.Trim().Trim('"'));
            if (!LooksLikePath(limpo)) continue;
            if (File.Exists(limpo)) continue;

            entradas.Add(NewEntry(
                CategoryFirewallRules,
                local.Canonical(relativo),
                nome,
                $"regra de firewall libera um programa inexistente: {limpo}",
                RegistryHive.LocalMachine,
                RegistryView.Registry64,
                relativo));
        }
    }

    /// <summary>
    /// Fontes registradas cujo arquivo nao existe mais.
    ///
    /// O valor do registro e o NOME do arquivo ("arial.ttf"), e o arquivo
    /// mora na pasta de fontes do Windows. O valor tambem pode vir com o
    /// caminho completo, e nesse caso ele manda.
    /// </summary>
    private static void ScanFonts(List<RegistryCleanerEntry> entradas)
    {
        const string relativo = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";

        var pastaDeFontes = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");

        foreach (var local in SoftwareLocations())
        {
            using var chave = local.Open(relativo);
            if (chave is null) continue;

            foreach (var nome in chave.ValueNames())
            {
                var valor = Expandir(chave.ReadValue(nome).Trim().Trim('"'));
                if (valor.Length == 0) continue;

                var completo = LooksLikePath(valor)
                    ? valor
                    : Path.Combine(pastaDeFontes, valor);

                if (File.Exists(completo)) continue;

                entradas.Add(NewEntry(
                    CategoryFonts,
                    local.Canonical(relativo),
                    nome,
                    $"fonte registrada aponta para um arquivo inexistente: {completo}",
                    RegistryHive.LocalMachine,
                    local.View,
                    relativo));
            }
        }
    }

    /// <summary>
    /// Image File Execution Options que valem para programas inexistentes.
    ///
    /// As entradas comecadas por "~" sao desativadas pelo proprio Windows e
    /// nao sao julgadas.
    /// </summary>
    private static void ScanIfeo(List<RegistryCleanerEntry> entradas)
    {
        const string relativo =
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";

        foreach (var local in SoftwareLocations())
        {
            using var chave = local.Open(relativo);
            if (chave is null) continue;

            foreach (var exe in chave.SubKeys())
            {
                if (exe.StartsWith("~", StringComparison.Ordinal)) continue;

                // Um programa instalado pode ter o executavel fora do PATH
                // (o Excel esta em "C:\Program Files\Microsoft Office\...").
                // Sem essa segunda verificacao, todo o Office apareceria
                // como sobra.
                if (ExecutableLocator.Exists(exe)) continue;
                if (ExecutableLocator.IsInstalled(exe)) continue;

                entradas.Add(NewEntry(
                    CategoryIfeo,
                    local.Canonical(relativo + "\\" + exe),
                    "",
                    $"opcao de execucao de imagem vale para um programa inexistente: {exe}",
                    RegistryHive.LocalMachine,
                    local.View,
                    relativo + "\\" + exe));
            }
        }
    }

    /// <summary>
    /// Produtos do Windows Installer registrados sem as propriedades.
    ///
    /// A DUAS metades do registro de um produto sao:
    ///
    ///   <c>HKLM\SOFTWARE\Classes\Installer\Products\{codigo}</c>
    ///   <c>HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\{SID}\Products\{codigo}</c>
    ///
    /// A segunda e a que tem as <c>InstallProperties</c> (nome, codigo, versao
    /// de upgrade). Um produto cuja metade de UserData sumiu — ou que nao tem
    /// <c>InstallProperties</c> — esta pela metade e pode ser limpo.
    ///
    /// O codigo do produto e o mesmo nas duas metades: e o que permite
    /// casar um com o outro.
    /// </summary>
    private static void ScanInstallerProducts(List<RegistryCleanerEntry> entradas)
    {
        const string raizClasses = @"SOFTWARE\Classes\Installer\Products";
        const string raizUserData =
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData";

        var codigosComPropriedades = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var local in SoftwareLocations())
        {
            var userData = local.Open(raizUserData);
            if (userData is null) continue;

            foreach (var sid in userData.SubKeys())
            {
                using var produtos = userData.Open(sid + @"\Products");
                if (produtos is null) continue;

                foreach (var codigo in produtos.SubKeys())
                {
                    var completo = sid + @"\Products\" + codigo;
                    if (!codigosComPropriedades.Add(codigo)) continue;

                    var temPropriedades = produtos.SubKeys(codigo)
                        .Any(s => s.Equals("InstallProperties", StringComparison.OrdinalIgnoreCase));

                    if (temPropriedades) continue;

                    entradas.Add(NewEntry(
                        CategoryInstallerProducts,
                        local.Canonical(completo),
                        "",
                        "produto do instalador do Windows registrado sem InstallProperties",
                        RegistryHive.LocalMachine,
                        local.View,
                        completo));
                }
            }
        }

        // A outra metade: um produto em Classes sem o par em UserData.
        foreach (var local in SoftwareLocations())
        {
            using var classes = local.Open(raizClasses);
            if (classes is null) continue;

            foreach (var codigo in classes.SubKeys())
            {
                if (codigosComPropriedades.Contains(codigo)) continue;

                entradas.Add(NewEntry(
                    CategoryInstallerProducts,
                    local.Canonical(raizClasses + "\\" + codigo),
                    "",
                    "produto do instalador do Windows sem o registro de usuario correspondente",
                    RegistryHive.LocalMachine,
                    local.View,
                    raizClasses + "\\" + codigo));
            }
        }
    }

    /// <summary>
    /// Entradas do MuiCache que ainda apontam para programas removidos.
    /// </summary>
    private static void ScanMuiCache(List<RegistryCleanerEntry> entradas)
    {
        foreach (var entrada in MuiCacheLeftovers.Leftovers())
        {
            entradas.Add(NewEntry(
                CategoryMuiCache,
                "HKEY_CURRENT_USER\\" + MuiCacheLeftovers.CachePath,
                entrada.Name,
                $"cache do Menu Iniciar ainda aponta para um programa inexistente: {entrada.Path}",
                RegistryHive.CurrentUser,
                RegistryView.Default,
                MuiCacheLeftovers.CachePath));
        }
    }

    /// <summary>
    /// Programas registrados em "Abrir com" que nao existem mais.
    /// </summary>
    private static void ScanOpenWith(List<RegistryCleanerEntry> entradas)
    {
        foreach (var raiz in ClassesRoots())
        {
            using var chave = raiz.Open("Applications");
            if (chave is null) continue;

            foreach (var app in chave.SubKeys())
            {
                var comando = ReadOpenCommand(raiz, app, "Applications\\" + app);
                if (comando.Length == 0) continue;

                var exe = ExecutablePathResolver.Extract(comando);
                if (exe.Length == 0) continue;
                if (!LooksLikePath(exe)) continue;
                if (ExecutableExists(exe)) continue;

                entradas.Add(NewEntry(
                    CategoryOpenWith,
                    raiz.Canonical("Applications\\" + app),
                    "",
                    $"programa de \"Abrir com\" nao existe mais: {exe}",
                    RegistryHive.ClassesRoot,
                    raiz.View,
                    "Applications\\" + app));
            }
        }
    }

    /// <summary>
    /// Identificadores de programa (ProgID) cujo comando nao existe mais.
    /// </summary>
    private static void ScanProgIds(List<RegistryCleanerEntry> entradas)
    {
        foreach (var raiz in ClassesRoots())
        {
            using var chave = raiz.Open(string.Empty);
            if (chave is null) continue;

            foreach (var progId in chave.SubKeys())
            {
                if (progId.StartsWith(".", StringComparison.Ordinal)) continue;
                if (progId.Equals("CLSID", StringComparison.OrdinalIgnoreCase)) continue;
                if (progId.Equals("TypeLib", StringComparison.OrdinalIgnoreCase)) continue;
                if (progId.Equals("ActiveX", StringComparison.OrdinalIgnoreCase)) continue;
                if (progId.Equals("Wow6432Node", StringComparison.OrdinalIgnoreCase)) continue;

                var comando = ReadOpenCommand(raiz, progId);
                if (comando.Length == 0) continue;

                var exe = ExecutablePathResolver.Extract(comando);
                if (exe.Length == 0) continue;
                if (!LooksLikePath(exe)) continue;
                if (ExecutableExists(exe)) continue;

                entradas.Add(NewEntry(
                    CategoryProgIds,
                    raiz.Canonical(progId),
                    "",
                    $"identificador de programa aponta para um executavel inexistente: {exe}",
                    RegistryHive.ClassesRoot,
                    raiz.View,
                    progId));
            }
        }
    }

    /// <summary>
    /// Itens que sobem com o Windows e apontam para programas removidos.
    /// </summary>
    private static void ScanRunEntries(List<RegistryCleanerEntry> entradas)
    {
        var roots = new[] { "Run", "RunOnce", "RunServices", "RunOnceEx" };
        const string relativo = @"SOFTWARE\Microsoft\Windows\CurrentVersion";

        foreach (var local in SoftwareLocations())
        {
            foreach (var root in roots)
            {
                var caminhoRelativo = relativo + "\\" + root;
                using var chave = local.Open(caminhoRelativo);
                if (chave is null) continue;

                foreach (var nome in chave.ValueNames())
                {
                    var valor = chave.ReadValue(nome);
                    if (valor.Length == 0) continue;

                    var exe = ExecutablePathResolver.Extract(valor);
                    if (exe.Length == 0) continue;
                    if (!LooksLikePath(exe)) continue;
                    if (ExecutableExists(exe)) continue;

                    entradas.Add(NewEntry(
                        CategoryRunEntries,
                        local.Canonical(caminhoRelativo),
                        nome,
                        $"item de inicializacao aponta para um programa inexistente: {exe}",
                        RegistryHive.LocalMachine,
                        local.View,
                        caminhoRelativo));
                }
            }
        }
    }

    /// <summary>
    /// DLLs compartilhadas cujo arquivo nao existe mais.
    ///
    /// O NOME do valor e o caminho da DLL, e o DADO e a contagem de
    /// referencias ("3"). O inverso (nome = numero, dado = caminho) era a
    /// leitura anterior, e nao encontrava nada: o valor "3" nao e um
    /// caminho.
    ///
    /// O valor "count" (a contagem de DLLs compartilhadas) nao tem caminho e
    /// por isso nao entra na conta.
    /// </summary>
    private static void ScanSharedDlls(List<RegistryCleanerEntry> entradas)
    {
        const string relativo = @"SOFTWARE\Microsoft\Windows\CurrentVersion\SharedDLLs";

        foreach (var local in SoftwareLocations())
        {
            using var chave = local.Open(relativo);
            if (chave is null) continue;

            foreach (var nome in chave.ValueNames())
            {
                // O valor "count" e a contagem de referencias, e as entradas
                // que comecam com "s" sao referencias cruzadas: nenhuma das
                // duas e um arquivo.
                if (nome.Equals("count", StringComparison.OrdinalIgnoreCase)) continue;

                var limpo = nome.Trim().Trim('"');
                if (!LooksLikePath(limpo)) continue;
                if (File.Exists(limpo)) continue;

                var dado = chave.ReadValue(nome);

                entradas.Add(NewEntry(
                    CategorySharedDlls,
                    local.Canonical(relativo),
                    nome,
                    $"DLL compartilhada aponta para um arquivo inexistente: {limpo} (referencias: {dado})",
                    RegistryHive.LocalMachine,
                    local.View,
                    relativo));
            }
        }
    }

    /// <summary>
    /// Contextos de menu do shell que apontam para classes inexistentes.
    /// </summary>
    private static void ScanShellExtensions(List<RegistryCleanerEntry> entradas)
    {
        var roots = new[]
        {
            "*\\shellex\\ContextMenuHandlers",
            "AllFilesystemObjects\\shellex\\ContextMenuHandlers",
            "Directory\\shellex\\ContextMenuHandlers",
            "Directory\\Background\\shellex\\ContextMenuHandlers",
            "Folder\\shellex\\ContextMenuHandlers",
            "Drive\\shellex\\ContextMenuHandlers",
        };

        foreach (var raiz in ClassesRoots())
        {
            foreach (var root in roots)
            {
                using var chave = raiz.Open(root);
                if (chave is null) continue;

                foreach (var handler in chave.SubKeys())
                {
                    var clsid = chave.ReadDefaultValue(handler);
                    if (clsid.Length == 0) continue;

                    // O valor padrao nem sempre e um CLSID: o Windows tambem
                    // grava ali o nome amigavel ("Taskband Pin"). Um nome
                    // amigavel nao e uma classe, e nao pode ser julgado.
                    if (!clsid.StartsWith("{", StringComparison.Ordinal)) continue;

                    var classe = raiz.Open("CLSID\\" + clsid);
                    if (classe is not null) continue;

                    entradas.Add(NewEntry(
                        CategoryShellExtensions,
                        raiz.Canonical(root + "\\" + handler),
                        "",
                        $"extensao do shell aponta para uma classe inexistente: {clsid}",
                        RegistryHive.ClassesRoot,
                        raiz.View,
                        root + "\\" + handler));
                }
            }
        }
    }

    /// <summary>
    /// Bibliotecas de tipos registradas sem o arquivo da DLL.
    /// </summary>
    private static void ScanTypeLib(List<RegistryCleanerEntry> entradas)
    {
        foreach (var entrada in TypeLibLeftovers.Leftovers())
        {
            entradas.Add(NewEntry(
                CategoryTypeLib,
                $"HKEY_CLASSES_ROOT\\TypeLib\\{entrada.Guid}\\{entrada.Version}",
                "",
                $"biblioteca de tipos aponta para um arquivo inexistente: {entrada.Path}",
                RegistryHive.ClassesRoot,
                RegistryView.Default,
                $"TypeLib\\{entrada.Guid}\\{entrada.Version}"));
        }
    }

    /// <summary>
    /// Entradas de desinstalacao cujo programa e desinstalador nao existem mais.
    ///
    /// O criterio exige as DUAS coisas: o executavel do desinstalador sumiu E
    /// a pasta de instalacao (quando ha) tambem sumiu. Uma entrada cujo
    /// desinstalador quebrou mas cuja pasta existe nao e orfa — o programa
    /// ainda esta instalado.
    /// </summary>
    private static void ScanUninstallEntries(List<RegistryCleanerEntry> entradas)
    {
        const string relativo = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

        foreach (var local in SoftwareLocations())
        {
            using var chave = local.Open(relativo);
            if (chave is null) continue;

            foreach (var nome in chave.SubKeys())
            {
                var uninstall = chave.ReadValue("UninstallString");
                if (uninstall.Length == 0) uninstall = chave.ReadValue("QuietUninstallString");
                if (uninstall.Length == 0) continue;

                var exe = ExecutablePathResolver.Extract(uninstall);
                if (exe.Length == 0) continue;
                if (!LooksLikePath(exe)) continue;
                if (ExecutableExists(exe)) continue;

                var pasta = chave.ReadValue("InstallLocation");
                var pastaViva = pasta.Length > 0 && Directory.Exists(pasta.Trim().Trim('"'));
                if (pastaViva) continue;

                entradas.Add(NewEntry(
                    CategoryUninstallEntries,
                    local.Canonical(relativo + "\\" + nome),
                    "",
                    $"entrada de desinstalacao aponta para um programa inexistente: {exe}",
                    RegistryHive.LocalMachine,
                    local.View,
                    relativo + "\\" + nome));
            }
        }
    }

    // ---------------------------------------------------------------------
    // Auxiliares
    // ---------------------------------------------------------------------

    /// <summary>
    /// Monta uma entrada do limpador.
    /// </summary>
    private static RegistryCleanerEntry NewEntry(
        string categoryId,
        string path,
        string valueName,
        string detail,
        RegistryHive hive,
        RegistryView view,
        string relativePath)
    {
        var nome = Categories.FirstOrDefault(c => c.Id.Equals(categoryId, StringComparison.OrdinalIgnoreCase))?.Name
                   ?? categoryId;

        return new RegistryCleanerEntry
        {
            CategoryId = categoryId,
            CategoryName = nome,
            Path = path,
            ValueName = valueName,
            Detail = detail,
            Hive = hive,
            View = view,
            RelativePath = relativePath,
        };
    }

    /// <summary>
    /// Expande as variaveis de ambiente de um caminho.
    ///
    /// O registro do Windows guarda "%SystemRoot%\system32\svchost.exe": sem
    /// expandir, o arquivo pareceria inexistente.
    /// </summary>
    private static string Expandir(string caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho)) return string.Empty;

        try
        {
            return Environment.ExpandEnvironmentVariables(caminho).Trim();
        }
        catch
        {
            return caminho.Trim();
        }
    }

    /// <summary>
    /// True quando o texto parece um caminho de arquivo.
    ///
    /// Sem essa conferencia, um valor como "System" (de um item de
    /// inicializacao) seria tratado como um executavel inexistente.
    /// </summary>
    private static bool LooksLikePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var text = value.Trim();
        return text.Contains('\\') || text.Contains('/');
    }

    /// <summary>
    /// Extrai um segmento de um valor separado por barras verticais.
    ///
    /// </summary>
    /// <param name="value">O valor bruto.</param>
    /// <param name="name">O nome do segmento (ex.: "App=").</param>
    private static string ExtrairSegmento(string value, string name)
    {
        foreach (var parte in value.Split('|'))
        {
            if (!parte.StartsWith(name, StringComparison.OrdinalIgnoreCase)) continue;
            return parte[name.Length..];
        }

        return string.Empty;
    }

    /// <summary>
    /// Le o comando de abertura de um ProgID (ou de um programa em
    /// "Abrir com").
    ///
    /// </summary>
    /// <param name="raiz">A raiz das classes.</param>
    /// <param name="progId">O identificador lido.</param>
    /// <param name="caminhoRelativo">O caminho relativo quando o identificador
    /// nao esta na raiz das classes (caso de "Abrir com").</param>
    private static string ReadOpenCommand(ClassRoot raiz, string progId, string? caminhoRelativo = null)
    {
        var relativo = caminhoRelativo is null
            ? progId + "\\shell\\open\\command"
            : caminhoRelativo + "\\shell\\open\\command";

        using var chave = raiz.Open(relativo);
        if (chave is null) return string.Empty;

        return chave.ReadDefaultValue(string.Empty);
    }

    /// <summary>
    /// True quando o executavel existe no disco.
    ///
    /// A regra mora em <see cref="ExecutableLocator"/>: caminho absoluto,
    /// caminho sem extensao e nome resolvido pelo PATH e pela tabela
    /// "App Paths" do Windows.
    /// </summary>
    private static bool ExecutableExists(string nome) => ExecutableLocator.Exists(nome);

    /// <summary>
    /// As raizes das classes: a visao mesclada e a das classes de 32 bits.
    /// </summary>
    private static IReadOnlyList<ClassRoot> ClassesRoots() => new[]
    {
        new ClassRoot(RegistryHive.ClassesRoot, RegistryView.Registry64, string.Empty),
        new ClassRoot(RegistryHive.ClassesRoot, RegistryView.Registry64, "Wow6432Node"),
    };

    /// <summary>
    /// As visoes do registro em que as chaves de software do sistema sao
    /// lidas: 64 bits e 32 bits.
    ///
    /// As duas visoes existem de verdade no Windows: um programa de 32 bits
    /// grava o seu registro em "SOFTWARE\WOW6432Node", e uma varredura so da
    /// visao de 64 bits nao acha nada disso.
    /// </summary>
    private static IReadOnlyList<LocalLocation> SoftwareLocations() => new[]
    {
        new LocalLocation(RegistryHive.LocalMachine, RegistryView.Registry64, "HKEY_LOCAL_MACHINE"),
        new LocalLocation(RegistryHive.LocalMachine, RegistryView.Registry32, "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node"),
        new LocalLocation(RegistryHive.CurrentUser, RegistryView.Registry64, "HKEY_CURRENT_USER"),
    };

    /// <summary>
    /// Tira o prefixo "SOFTWARE\" do caminho, para montar o caminho
    /// equivalente na arvore de 32 bits.
    ///
    /// O redirecionamento do Windows funciona assim:
    /// <c>HKLM\SOFTWARE\X</c> vira <c>HKLM\SOFTWARE\WOW6432Node\X</c> — o
    /// "SOFTWARE" nao se repete.
    /// </summary>
    private static string SemPrefixoSoftware(string relativo)
    {
        const string prefixo = "SOFTWARE\\";

        return relativo.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)
            ? relativo[prefixo.Length..]
            : relativo;
    }

    /// <summary>
    /// Uma visao do registro do sistema, com o prefixo canonical que a tela
    /// mostra.
    /// </summary>
    private sealed class LocalLocation
    {
        public LocalLocation(RegistryHive hive, RegistryView view, string prefixo)
        {
            Hive = hive;
            View = view;
            Prefixo = prefixo;
        }

        public RegistryHive Hive { get; }

        public RegistryView View { get; }

        private string Prefixo { get; }

        /// <summary>
        /// O caminho completo da chave, como o Editor do Registro mostra.
        ///
        /// A arvore de 32 bits e o prefixo "SOFTWARE\WOW6432Node": o
        /// caminho relativo ja comeca com "SOFTWARE", e o "SOFTWARE" nao se
        /// repete.
        /// </summary>
        public string Canonical(string relativo)
        {
            if (!Prefixo.EndsWith("WOW6432Node", StringComparison.OrdinalIgnoreCase))
                return Prefixo + "\\" + relativo;

            return Prefixo + "\\" + SemPrefixoSoftware(relativo);
        }

        /// <summary>Abre a chave. Devolve null quando ela nao existe.</summary>
        public RegistryKeyView? Open(string relativo)
        {
            try
            {
                var baseKey = RegistryKey.OpenBaseKey(Hive, View);

                // Na visao de 32 bits, uma chave que NAO e redirecionada aparece
                // igual nas duas visoes. Reportar as duas seria contar a mesma
                // entrada duas vezes — e o caminho mostrado seria errado,
                // porque ela nao esta em "WOW6432Node".
                //
                // A verificacao e a propria existencia da chave na arvore de
                // 32 bits: e ela que diz se a entrada e de verdade de 32 bits.
                if (View == RegistryView.Registry32 && Hive == RegistryHive.LocalMachine)
                {
                    var naArvoreDe32Bits = "SOFTWARE\\WOW6432Node\\" + SemPrefixoSoftware(relativo);
                    using var base64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                    using var existe = base64.OpenSubKey(naArvoreDe32Bits);

                    if (existe is null)
                    {
                        baseKey.Dispose();
                        return null;
                    }
                }

                RegistryKey? chave = baseKey.OpenSubKey(relativo);
                if (chave is null)
                {
                    baseKey.Dispose();
                    return null;
                }

                return new RegistryKeyView(baseKey, chave, Hive, View, relativo);
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao abrir {Hive}\\{relativo}: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// A raiz das classes, com o mesmo papel de <see cref="LocalLocation"/>.
    /// </summary>
    private sealed class ClassRoot
    {
        public ClassRoot(RegistryHive hive, RegistryView view, string prefixo)
        {
            Hive = hive;
            View = view;
            Prefixo = prefixo;
        }

        public RegistryHive Hive { get; }

        public RegistryView View { get; }

        private string Prefixo { get; }

        public string Canonical(string relativo)
        {
            var base_ = Prefixo.Length == 0 ? "HKEY_CLASSES_ROOT" : "HKEY_CLASSES_ROOT\\" + Prefixo;
            return relativo.Length == 0 ? base_ : base_ + "\\" + relativo;
        }

        public RegistryKeyView? Open(string relativo)
        {
            try
            {
                var baseKey = RegistryKey.OpenBaseKey(Hive, View);

                // Um caminho vazio e a propria raiz: nao existe subchave de
                // nome vazio, e a raiz e o que a varredura de ProgIDs e de
                // tipos de arquivo precisa.
                RegistryKey? chave;
                var caminho = Prefixo.Length == 0
                    ? relativo
                    : Prefixo + (relativo.Length == 0 ? string.Empty : "\\" + relativo);

                if (caminho.Length == 0)
                {
                    chave = baseKey;
                }
                else
                {
                    chave = baseKey.OpenSubKey(caminho);
                    if (chave is null)
                    {
                        baseKey.Dispose();
                        return null;
                    }
                }

                return new RegistryKeyView(baseKey, chave, Hive, View, caminho);
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao abrir as classes '{Prefixo}\\{relativo}': {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Uma chave de registro aberta, com os metodos de leitura que o
    /// limpador usa.
    ///
    /// A chave e dispose: quem abre e responsavel por fechar.
    /// </summary>
    private sealed class RegistryKeyView : IDisposable
    {
        private readonly RegistryKey? _base;
        private readonly RegistryKey _chave;

        /// <summary>
        /// Construtor da chave de primeiro nivel: ela e dona da chave base do
        /// hive, e por isso fecha os dois.
        /// </summary>
        public RegistryKeyView(RegistryKey baseKey, RegistryKey chave, RegistryHive hive, RegistryView view, string relativo)
        {
            _base = baseKey;
            _chave = chave;
            Hive = hive;
            View = view;
            Relativo = relativo;
        }

        /// <summary>
        /// Construtor de uma subchave: ela nao e dona da chave base — quem
        /// abriu a chave de primeiro nivel e que fecha a base.
        /// </summary>
        private RegistryKeyView(RegistryKey chave, RegistryHive hive, RegistryView view, string relativo)
        {
            _base = null;
            _chave = chave;
            Hive = hive;
            View = view;
            Relativo = relativo;
        }

        public RegistryHive Hive { get; }

        public RegistryView View { get; }

        public string Relativo { get; }

        /// <summary>
        /// Abre uma subchave desta chave.
        /// </summary>
        public RegistryKeyView? Open(string relativo)
        {
            try
            {
                var sub = _chave.OpenSubKey(relativo);
                if (sub is null) return null;

                return new RegistryKeyView(sub, Hive, View, Relativo + "\\" + relativo);
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            try
            {
                if (_base is null || !ReferenceEquals(_base, _chave)) _chave.Dispose();
            }
            catch
            {
                // Chave ja fechada: nada a fazer.
            }

            try
            {
                _base?.Dispose();
            }
            catch
            {
                // Chave ja fechada: nada a fazer.
            }
        }

        /// <summary>Os nomes das subchaves.</summary>
        public string[] SubKeys()
        {
            try { return _chave.GetSubKeyNames(); }
            catch { return Array.Empty<string>(); }
        }

        /// <summary>Os nomes das subchaves de uma subchave.</summary>
        public string[] SubKeys(string nome)
        {
            try
            {
                using var sub = _chave.OpenSubKey(nome);
                return sub?.GetSubKeyNames() ?? Array.Empty<string>();
            }
            catch { return Array.Empty<string>(); }
        }

        /// <summary>Os nomes dos valores.</summary>
        public string[] ValueNames()
        {
            try { return _chave.GetValueNames(); }
            catch { return Array.Empty<string>(); }
        }

        /// <summary>Os nomes dos valores de uma subchave.</summary>
        public string[] ValueNames(string nome)
        {
            try
            {
                using var sub = _chave.OpenSubKey(nome);
                return sub?.GetValueNames() ?? Array.Empty<string>();
            }
            catch { return Array.Empty<string>(); }
        }

        /// <summary>Le um valor. Devolve string vazia quando nao existe.</summary>
        public string ReadValue(string nome)
        {
            try
            {
                var value = _chave.GetValue(nome) as string;
                return value ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        /// <summary>Le o valor padrao de uma subchave.</summary>
        public string ReadDefaultValue(string subKey)
        {
            try
            {
                using var sub = _chave.OpenSubKey(subKey);
                if (sub is null) return string.Empty;

                var value = sub.GetValue(string.Empty) as string;
                return value ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        /// <summary>Le o valor padrao da propria chave.</summary>
        public string ReadDefaultValue() => ReadDefaultValue(string.Empty);
    }
}
