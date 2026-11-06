using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// As quatro categorias do painel de saude do software.
///
/// A ideia vem de dois concorrentes independentes (IObit e Wise): em vez de
/// quatro telas separadas — sobras, arquivos redundantes, permissoes e
/// avisos — ha **um** painel que responde "o que esta errado na minha
/// maquina?". As quatro categorias sao as mesmas em ambos.
/// </summary>
public enum HealthCategory
{
    /// <summary>Sobras de programas ja desinstalados.</summary>
    Leftovers,

    /// <summary>Arquivos duplicados que ocupam espaco sem serve.</summary>
    RedundantFiles,

    /// <summary>Permissoes abertas demais em arquivos e chaves.</summary>
    Permissions,

    /// <summary>Avisos sobre o estado do programa.</summary>
    Notifications,
}

/// <summary>
/// Gravidade de um achado.
/// </summary>
public enum HealthSeverity
{
    /// <summary>Informativo.</summary>
    Info,

    /// <summary>Convem corrigir.</summary>
    Warning,

    /// <summary>Precisa de atencao.</summary>
    Critical,
}

/// <summary>
/// Um achado do painel de saude.
///
/// O achado **aponta** para o alvo (arquivo, chave, programa) — a tela usa
/// isso para navegar ate ele, e a CLI para imprimir o caminho.
/// </summary>
public sealed class HealthFinding
{
    /// <summary>A categoria do achado.</summary>
    public HealthCategory Category { get; init; }

    /// <summary>A gravidade do achado.</summary>
    public HealthSeverity Severity { get; init; }

    /// <summary>O que foi encontrado, em uma frase.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>O detalhe que explica o achado.</summary>
    public string Detail { get; init; } = string.Empty;

    /// <summary>O caminho ou chave em que o achado esta.</summary>
    public string Target { get; init; } = string.Empty;

    /// <summary>Bytes que o achado representa (0 quando nao se aplica).</summary>
    public long WastedBytes { get; init; }
}

/// <summary>
/// O resultado da analise de saude.
///
/// </summary>
public sealed class HealthReport
{
    /// <summary>Todos os achados, na ordem em que foram encontrados.</summary>
    public IReadOnlyList<HealthFinding> Findings { get; init; } = Array.Empty<HealthFinding>();

    /// <summary>Quantos achados ha em uma categoria.</summary>
    public int CountOf(HealthCategory category) => Findings.Count(f => f.Category == category);

    /// <summary>Os bytes desperdicados no total.</summary>
    public long WastedBytes => Findings.Sum(f => f.WastedBytes);

    /// <summary>True quando nao ha nenhum achado.</summary>
    public bool IsHealthy => Findings.Count == 0;
}

/// <summary>
/// Analise de saude do software: as quatro categorias em um painel so.
///
/// O painel nao e uma tela nova com logica propria — e a **consumicao** das
/// regras que ja existem no nucleo: a varredura de sobras
/// (<see cref="LeftoverScanner"/>), os avisos de seguranca
/// (<see cref="UninstallAdvisories"/>) e os modulos de gestao deste mesmo
/// lote. Nenhuma categoria inventa o seu proprio criterio.
/// </summary>
public static class SoftwareHealth
{
    /// <summary>
    /// Analisa os programas indicados e devolve os achados das quatro
    /// categorias.
    /// </summary>
    /// <param name="programs">Os programas a analisar.</param>
    /// <param name="scanLeftovers">
    /// Quando verdadeiro, a categoria "sobras" e varrida. A varredura e
    /// cara: por isso ela e opt-in, e nao acontece no arranque.
    /// </param>
    /// <param name="scanDuplicates">
    /// Quando verdadeiro, a categoria "arquivos redundantes" e varrida.
    /// </param>
    public static HealthReport Analyze(
        IEnumerable<InstalledProgram> programs,
        bool scanLeftovers = true,
        bool scanDuplicates = true)
    {
        var list = programs?.ToList() ?? new List<InstalledProgram>();
        var findings = new List<HealthFinding>();

        findings.AddRange(LeftoverFindings(list, scanLeftovers));
        findings.AddRange(DuplicateFindings(list, scanDuplicates));
        findings.AddRange(PermissionFindings(list));
        findings.AddRange(NotificationFindings(list));

        return new HealthReport { Findings = findings };
    }

    /// <summary>
    /// A categoria "sobras": o que sobrou de programas ja desinstalados.
    ///
    /// Usa o <see cref="LeftoverScanner"/> com as mesmas opcoes da
    /// desinstalacao — a varredura de saude e a varredura da desinstalacao,
    /// com o mesmo criterio. Uma categoria que usasse outro criterio
    /// mostraria sobras diferentes das que o Tranal apagaria.
    /// </summary>
    private static IEnumerable<HealthFinding> LeftoverFindings(
        List<InstalledProgram> programs, bool enabled)
    {
        if (!enabled) yield break;

        var options = LeftoverScanOptions.Default;
        foreach (var program in programs)
        {
            List<LeftoverItem> items;
            try
            {
                items = LeftoverScanner.Scan(program, options);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Uma pasta inacessivel nao pode derrubar a analise inteira.
                continue;
            }

            foreach (var item in items.Where(i => i.IsHighConfidence))
            {
                yield return new HealthFinding
                {
                    Category = HealthCategory.Leftovers,
                    Severity = HealthSeverity.Warning,
                    Title = $"Sobra de {program.DisplayName}: {item.Kind}",
                    Detail = item.Detail,
                    Target = item.Path,
                };
            }
        }
    }

    /// <summary>
    /// A categoria "arquivos redundantes": o mesmo arquivo em dois lugares.
    ///
    /// A busca e por **nome e tamanho**, e so o que bate nos dois e
    /// confirmado pelo conteudo (hash). Sem a confirmacao, dois arquivos de
    /// mesmo nome e mesmo tamanho mas conteudo diferente seriam reportados
    /// como duplicados — e o usuario perderia um arquivo legitimo.
    /// </summary>
    private static IEnumerable<HealthFinding> DuplicateFindings(
        List<InstalledProgram> programs, bool enabled)
    {
        if (!enabled) yield break;

        foreach (var program in programs)
        {
            var roots = CandidateRootsOf(program);
            if (roots.Count == 0) continue;

            var byNameAndSize = new Dictionary<(string Name, long Size), List<string>>();
            foreach (var file in EnumerateFilesSafe(roots))
            {
                var key = (Path.GetFileName(file).ToLowerInvariant(), new FileInfo(file).Length);
                if (!byNameAndSize.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    byNameAndSize[key] = list;
                }

                list.Add(file);
            }

            foreach (var group in byNameAndSize.Values.Where(v => v.Count > 1))
            {
                var hashes = new Dictionary<string, List<string>>();
                foreach (var file in group)
                {
                    var hash = HashOf(file);
                    if (hash.Length == 0) continue;

                    if (!hashes.TryGetValue(hash, out var list))
                    {
                        list = new List<string>();
                        hashes[hash] = list;
                    }

                    list.Add(file);
                }

                foreach (var duplicate in hashes.Values.Where(v => v.Count > 1))
                {
                    var wasted = new FileInfo(duplicate[0]).Length * (duplicate.Count - 1);
                    yield return new HealthFinding
                    {
                        Category = HealthCategory.RedundantFiles,
                        Severity = HealthSeverity.Info,
                        Title = $"Arquivo repetido {Path.GetFileName(duplicate[0])} ({duplicate.Count}x)",
                        Detail = string.Join(" | ", duplicate),
                        Target = duplicate[0],
                        WastedBytes = wasted,
                    };
                }
            }
        }
    }

    /// <summary>
    /// A categoria "permissoes": quem pode escrever onde nao deveria.
    ///
    /// O alvo sao as pastas e as chaves do proprio programa. A verificacao
    /// procura os gruposuniversais (Todos, Usuarios, Usuarios autenticados) com
    /// permissao de escrita — e essa combinacao e a que permite a um programa
    /// de outro usuario alterar a instalacao.
    /// </summary>
    private static IEnumerable<HealthFinding> PermissionFindings(List<InstalledProgram> programs)
    {
        foreach (var program in programs)
        {
            var location = AppPermissionManager.InstallLocationOf(program);
            if (location.Length == 0 || !Directory.Exists(location)) continue;

            if (IsWritableByEveryone(location))
            {
                yield return new HealthFinding
                {
                    Category = HealthCategory.Permissions,
                    Severity = HealthSeverity.Critical,
                    Title = $"A pasta de {program.DisplayName} pode ser alterada por qualquer usuario",
                    Detail = location,
                    Target = location,
                };
            }

            if (!string.IsNullOrEmpty(program.RegistryPath) && IsRegistryWritableByEveryone(program.RegistryPath))
            {
                yield return new HealthFinding
                {
                    Category = HealthCategory.Permissions,
                    Severity = HealthSeverity.Critical,
                    Title = $"A chave de {program.DisplayName} pode ser alterada por qualquer usuario",
                    Detail = program.RegistryPath,
                    Target = program.RegistryPath,
                };
            }
        }
    }

    /// <summary>
    /// A categoria "notificacoes": avisos sobre o estado do programa.
    ///
    /// Aqui moram os fatos que o usuario nao descobre sozinho: o programa
    /// nao tem como ser desinstalado, esta numa pasta compartilhada com outro
    /// programa, deixa um servico rodando, deixa uma tarefa agendada.
    /// </summary>
    private static IEnumerable<HealthFinding> NotificationFindings(List<InstalledProgram> programs)
    {
        foreach (var program in programs)
        {
            if (string.IsNullOrWhiteSpace(program.UninstallString))
            {
                yield return new HealthFinding
                {
                    Category = HealthCategory.Notifications,
                    Severity = HealthSeverity.Warning,
                    Title = $"{program.DisplayName} nao tem comando de desinstalacao",
                    Detail = "O Tranal vai ter de remover os rastros na mao.",
                    Target = program.RegistryPath,
                };
            }

            // A pasta compartilhada e um aviso do proprio nucleo de avisos
            // (UninstallAdvisories): a lista devolve os nomes dos vizinhos
            // que dividem a mesma pasta.
            var neighbours = UninstallAdvisories.SharedInstallLocation(program, programs);
            if (neighbours.Count > 0)
            {
                yield return new HealthFinding
                {
                    Category = HealthCategory.Notifications,
                    Severity = HealthSeverity.Warning,
                    Title = $"{program.DisplayName} esta numa pasta compartilhada com outro programa",
                    Detail = string.Join(", ", neighbours),
                    Target = program.InstallLocation,
                };
            }

            var location = AppPermissionManager.InstallLocationOf(program);
            foreach (var service in ServiceManager.ListFor(location))
            {
                if (!service.Started) continue;

                yield return new HealthFinding
                {
                    Category = HealthCategory.Notifications,
                    Severity = HealthSeverity.Info,
                    Title = $"{program.DisplayName} deixa o servico '{service.DisplayName}' rodando",
                    Detail = service.PathName,
                    Target = service.Name,
                };
            }

            foreach (var task in ScheduledTaskManager.ListFor(location))
            {
                if (!task.Enabled) continue;

                yield return new HealthFinding
                {
                    Category = HealthCategory.Notifications,
                    Severity = HealthSeverity.Info,
                    Title = $"{program.DisplayName} deixa a tarefa agendada '{task.Name}' ativa",
                    Detail = task.Command,
                    Target = task.Name,
                };
            }
        }
    }

    /// <summary>
    /// As pastas onde vale procurar os arquivos do programa.
    ///
    /// A pasta de instalacao e as pastas de dados do usuario (AppData local e
    /// roaming) com o nome do programa. Sem a pasta de instalacao, nao ha
    /// o que procurar.
    /// </summary>
    internal static List<string> CandidateRootsOf(InstalledProgram program)
    {
        var roots = new List<string>();

        var location = AppPermissionManager.InstallLocationOf(program);
        if (location.Length > 0 && Directory.Exists(location)) roots.Add(location);

        var name = program.DisplayName;
        if (!string.IsNullOrWhiteSpace(name))
        {
            foreach (var folder in new[]
                     {
                         Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     })
            {
                if (string.IsNullOrEmpty(folder)) continue;

                var candidate = Path.Combine(folder, name);
                if (Directory.Exists(candidate)) roots.Add(candidate);
            }
        }

        return roots;
    }

    /// <summary>Enumera os arquivos das pastas sem seguir atalhos.</summary>
    private static IEnumerable<string> EnumerateFilesSafe(List<string> roots)
    {
        foreach (var root in roots)
        {
            var files = new List<string>();

            try
            {
                files.AddRange(SafeTraversal.EnumerateFilesDeep(root));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Pasta inacessivel: a proxima raiz continua.
            }

            foreach (var file in files)
            {
                bool exists;
                try
                {
                    exists = File.Exists(file);
                }
                catch (IOException)
                {
                    // Arquivo sumiu entre a enumeracao e a verificacao.
                    continue;
                }

                if (exists) yield return file;
            }
        }
    }

    /// <summary>
    /// O hash do conteudo de um arquivo, ou string vazia quando nao da.
    ///
    /// O hash cobre o arquivo inteiro ate 4 MB; acima disso, cobre o
    /// inicio e o fim, o que basta para distinguir arquivos iguais de
    /// arquivos que so tem o mesmo tamanho.
    /// </summary>
    internal static string HashOf(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sha = SHA256.Create();

            const long limit = 4 * 1024 * 1024;
            if (stream.Length <= limit) return Convert.ToHexString(sha.ComputeHash(stream));

            var head = new byte[limit];
            var read = stream.Read(head, 0, head.Length);
            sha.TransformBlock(head, 0, read, null, 0);

            stream.Seek(-limit, SeekOrigin.End);
            var tail = new byte[limit];
            var tailRead = stream.Read(tail, 0, tail.Length);
            sha.TransformFinalBlock(tail, 0, tailRead);

            return Convert.ToHexString(sha.Hash ?? Array.Empty<byte>());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    /// <summary>True quando a pasta pode ser alterada por qualquer usuario.</summary>
    private static bool IsWritableByEveryone(string folder)
    {
        try
        {
            var security = new DirectoryInfo(folder).GetAccessControl();
            foreach (var rule in security.GetAccessRules(true, false, typeof(System.Security.Principal.NTAccount)))
            {
                if (rule is not System.Security.AccessControl.FileSystemAccessRule access) continue;

                var identity = access.IdentityReference.Value ?? string.Empty;
                if (!IsUniversalGroup(identity)) continue;
                if ((access.FileSystemRights & (System.Security.AccessControl.FileSystemRights.Write
                                              | System.Security.AccessControl.FileSystemRights.Modify
                                              | System.Security.AccessControl.FileSystemRights.FullControl)) == 0)
                    continue;

                return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Sem permissao para ler a seguranca: sem achado, e nao um erro.
        }

        return false;
    }

    /// <summary>True quando a chave de registro pode ser alterada por qualquer usuario.</summary>
    private static bool IsRegistryWritableByEveryone(string registryPath)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(registryPath);
            if (key is null) return false;

            var security = key.GetAccessControl();
            foreach (var rule in security.GetAccessRules(true, false, typeof(System.Security.Principal.NTAccount)))
            {
                if (rule is not System.Security.AccessControl.RegistryAccessRule access) continue;

                var identity = access.IdentityReference.Value ?? string.Empty;
                if (!IsUniversalGroup(identity)) continue;
                if ((access.RegistryRights & (System.Security.AccessControl.RegistryRights.ChangePermissions
                                            | System.Security.AccessControl.RegistryRights.FullControl
                                            | System.Security.AccessControl.RegistryRights.SetValue
                                            | System.Security.AccessControl.RegistryRights.WriteKey)) == 0)
                    continue;

                return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException or System.Security.SecurityException)
        {
            // Chave inacessivel ou formato que o Tranal nao abre: sem achado.
        }

        return false;
    }

    /// <summary>
    /// True quando a identidade e um grupo universal do Windows.
    ///
    /// Sao os grupos que existsem em **qualquer** conta: "Todos", "Usuarios",
    /// "Usuarios autenticados" e "Convidados". Em portugues e em ingles.
    ///
    /// A comparacao ignora os acentos: "Usuarios" e "Usuários" sao o mesmo
    /// grupo, e a versao em ingles da maquina nao tem o acento. Sem isso, o
    /// achado apareceria em uma maquina e sumiria em outra.
    /// </summary>
    internal static bool IsUniversalGroup(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity)) return false;

        var normalized = WithoutAccents(identity).ToUpperInvariant();
        return normalized.Contains("EVERYONE")
               || normalized.Contains("TODOS")
               || normalized.Contains("USERS")
               || normalized.Contains("USUARIOS")
               || normalized.Contains("AUTHENTICATED")
               || normalized.Contains("AUTENTICADOS")
               || normalized.Contains("GUESTS")
               || normalized.Contains("CONVIDADOS");
    }

    /// <summary>
    /// O texto sem os acentos.
    ///
    /// A forma canonica e decompor em Forms D e descartar os sinais que nao
    /// sao de espacamento — e assim que o "ã" vira "a" sem trocar o texto
    /// original.
    /// </summary>
    internal static string WithoutAccents(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
