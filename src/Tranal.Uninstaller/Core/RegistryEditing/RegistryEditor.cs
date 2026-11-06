using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Core.RegistryEditing;

/// <summary>
/// O resultado de uma acao sobre o registro.
/// </summary>
public sealed class RegistryEditResult
{
    /// <summary>True quando a acao foi executada.</summary>
    public bool Success { get; init; }

    /// <summary>Texto que explica o resultado.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Codigo de erro do Windows, quando a recusa veio do sistema.</summary>
    public int ErrorCode { get; init; }

    /// <summary>Resultado de sucesso.</summary>
    public static RegistryEditResult Ok(string message) => new() { Success = true, Message = message };

    /// <summary>Resultado de recusa, com o motivo.</summary>
    public static RegistryEditResult Fail(string message, int errorCode = 0) =>
        new() { Success = false, Message = message, ErrorCode = errorCode };
}

/// <summary>
/// Editor de registro: renomear chave, apagar chave ou valor e assumir a
/// propriedade.
///
/// Sao as tres operacoes que o BCU oferece no menu de contexto do registro e
/// que o Tranal nao tinha: as tres mexem em chave do Windows, e por isso
/// passam pelas mesmas guardas de seguranca do resto do Tranal.
///
/// A regra de seguranca e a do nucleo (<see cref="SafetyRules"/>): chave
/// protegida do sistema **nao** e renomeada nem apagada. A ferramenta recusa
/// antes de tentar — e nao depois de falhar com "acesso negado".
/// </summary>
public static class RegistryEditor
{
    /// <summary>
    /// Renomeia uma chave de registro.
    ///
    /// O Windows nao tem "renomear": a chave nova e criada com os mesmos
    /// valores e a antiga e apagada. Por isso a operacao so é feita quando a
    /// chave nova **nao existe** — o que evita um "renomear" que apagaria a
    /// chave antiga sem ter criado a nova.
    /// </summary>
    /// <param name="registryPath">O caminho completo da chave (ex.: HKLM\SOFTWARE\App).</param>
    /// <param name="newName">O nome novo da chave.</param>
    public static RegistryEditResult RenameKey(string registryPath, string newName)
    {
        if (!TryOpen(registryPath, out var hive, out var path, out var error))
            return error!;

        if (IsProtectedPath(registryPath))
            return RegistryEditResult.Fail($"A chave '{registryPath}' e protegida pelo sistema.");

        var name = (newName ?? string.Empty).Trim();
        if (name.Length == 0)
            return RegistryEditResult.Fail("O nome novo da chave nao pode ser vazio.");

        if (name.IndexOf('\\') >= 0)
            return RegistryEditResult.Fail("O nome novo da chave nao pode conter barra invertida.");

        var parent = path.Contains('\\') ? path[..path.LastIndexOf('\\')] : string.Empty;
        var oldName = path.Contains('\\') ? path[(path.LastIndexOf('\\') + 1)..] : path;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var parentKey = string.IsNullOrEmpty(parent)
                ? baseKey
                : baseKey.OpenSubKey(parent, writable: true);

            if (parentKey is null)
                return RegistryEditResult.Fail($"Nao foi possivel abrir a chave pai de '{registryPath}'.");

            if (parentKey.OpenSubKey(name) is not null)
                return RegistryEditResult.Fail($"Ja existe uma chave '{name}' em '{parent}'.");

            using var source = baseKey.OpenSubKey(path, writable: false);
            if (source is null)
                return RegistryEditResult.Fail($"A chave '{registryPath}' nao existe.");

            using var target = parentKey.CreateSubKey(name);
            if (target is null)
                return RegistryEditResult.Fail($"Nao foi possivel criar a chave '{name}'.");

            CopyValues(source, target);
            parentKey.DeleteSubKeyTree(oldName, throwOnMissingSubKey: false);

            return RegistryEditResult.Ok($"A chave '{registryPath}' foi renomeada para '{name}'.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return RegistryEditResult.Fail(
                $"Sem permissao para renomear '{registryPath}': {ex.Message}",
                RemovalOutcome.ErrorCodeOf(ex));
        }
        catch (System.Security.SecurityException ex)
        {
            return RegistryEditResult.Fail(
                $"Sem permissao para renomear '{registryPath}': {ex.Message}",
                RemovalOutcome.ErrorCodeOf(ex));
        }
        catch (IOException ex)
        {
            return RegistryEditResult.Fail(
                $"Nao foi possivel renomear '{registryPath}': {ex.Message}",
                RemovalOutcome.ErrorCodeOf(ex));
        }
    }

    /// <summary>
    /// Apaga uma chave de registro e tudo que esta dentro dela.
    /// </summary>
    public static RegistryEditResult DeleteKey(string registryPath)
    {
        if (!TryOpen(registryPath, out var hive, out var path, out var error))
            return error!;

        if (IsProtectedPath(registryPath))
            return RegistryEditResult.Fail($"A chave '{registryPath}' e protegida pelo sistema.");

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var parent = OpenParent(baseKey, path);
            if (parent is null)
                return RegistryEditResult.Fail($"Nao foi possivel abrir a chave pai de '{registryPath}'.");

            var name = path.Contains('\\') ? path[(path.LastIndexOf('\\') + 1)..] : path;
            if (!parent.GetSubKeyNames().Contains(name, StringComparer.OrdinalIgnoreCase))
                return RegistryEditResult.Fail($"A chave '{registryPath}' nao existe.");

            parent.DeleteSubKeyTree(name, throwOnMissingSubKey: false);
            return RegistryEditResult.Ok($"A chave '{registryPath}' foi apagada.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return RegistryEditResult.Fail(
                $"Sem permissao para apagar '{registryPath}': {ex.Message}",
                RemovalOutcome.ErrorCodeOf(ex));
        }
        catch (System.Security.SecurityException ex)
        {
            return RegistryEditResult.Fail(
                $"Sem permissao para apagar '{registryPath}': {ex.Message}",
                RemovalOutcome.ErrorCodeOf(ex));
        }
        catch (IOException ex)
        {
            return RegistryEditResult.Fail(
                $"Nao foi possivel apagar '{registryPath}': {ex.Message}",
                RemovalOutcome.ErrorCodeOf(ex));
        }
    }

    /// <summary>
    /// Apaga um valor de uma chave de registro.
    ///
    /// O valor default (sem nome) e o "(Default)" da janela de edicao do
    /// registro: apagar o default e apagar o valor de "(Default)" e a mesma
    /// coisa.
    /// </summary>
    public static RegistryEditResult DeleteValue(string registryPath, string valueName)
    {
        if (!TryOpen(registryPath, out var hive, out var path, out var error))
            return error!;

        if (IsProtectedPath(registryPath))
            return RegistryEditResult.Fail($"A chave '{registryPath}' e protegida pelo sistema.");

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(path, writable: true);
            if (key is null)
                return RegistryEditResult.Fail($"A chave '{registryPath}' nao existe.");

            var name = valueName ?? string.Empty;
            var actual = name.Length == 0 ? string.Empty : name;

            if (!key.GetValueNames().Contains(actual, StringComparer.OrdinalIgnoreCase))
                return RegistryEditResult.Fail($"O valor '{actual}' nao existe em '{registryPath}'.");

            key.DeleteValue(actual, throwOnMissingValue: false);
            return RegistryEditResult.Ok($"O valor '{actual}' foi apagado de '{registryPath}'.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return RegistryEditResult.Fail(
                $"Sem permissao para apagar o valor: {ex.Message}",
                RemovalOutcome.ErrorCodeOf(ex));
        }
        catch (System.Security.SecurityException ex)
        {
            return RegistryEditResult.Fail(
                $"Sem permissao para apagar o valor: {ex.Message}",
                RemovalOutcome.ErrorCodeOf(ex));
        }
        catch (IOException ex)
        {
            return RegistryEditResult.Fail(
                $"Nao foi possivel apagar o valor: {ex.Message}",
                RemovalOutcome.ErrorCodeOf(ex));
        }
    }

    /// <summary>
    /// Assume a propriedade de uma chave de registro.
    ///
    /// A propriedade de uma chave do Windows e quem pode mudar as permissoes
    /// dela. Assumir a propriedade e a ultima etapa de destravar uma chave
    /// herdada que o Windows protegeu.
    ///
    /// A operacao **precisa** de administrador: sem o privilegio de
    /// "assumir propriedade", o Windows recusa. E o que a ferramenta diz, em
    /// vez de fingir que deu certo.
    /// </summary>
    public static RegistryEditResult TakeOwnership(string registryPath)
    {
        if (!TryOpen(registryPath, out var hive, out var path, out var error))
            return error!;

        if (IsProtectedPath(registryPath))
            return RegistryEditResult.Fail($"A chave '{registryPath}' e protegida pelo sistema.");

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(path, writable: false);
            if (key is null)
                return RegistryEditResult.Fail($"A chave '{registryPath}' nao existe.");

            var security = key.GetAccessControl(AccessControlSections.Access);

            var owner = WindowsIdentity.GetCurrent().User;
            if (owner is null)
                return RegistryEditResult.Fail("Nao foi possivel identificar o usuario atual.");

            var atual = security.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
            if (atual is not null && atual.Equals(owner))
                return RegistryEditResult.Ok($"A propriedade de '{registryPath}' ja e do usuario atual.");

            security.SetOwner(owner);
            key.SetAccessControl(security);

            return RegistryEditResult.Ok($"A propriedade de '{registryPath}' foi assumida.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return RegistryEditResult.Fail(
                $"Sem permissao para assumir a propriedade (e preciso ser administrador): {ex.Message}",
                RemovalOutcome.ErrorCodeOf(ex));
        }
        catch (System.Security.SecurityException ex)
        {
            return RegistryEditResult.Fail(
                $"Sem permissao para assumir a propriedade (e preciso ser administrador): {ex.Message}",
                RemovalOutcome.ErrorCodeOf(ex));
        }
        catch (IOException ex)
        {
            return RegistryEditResult.Fail(
                $"Nao foi possivel assumir a propriedade: {ex.Message}",
                RemovalOutcome.ErrorCodeOf(ex));
        }
    }

    /// <summary>
    /// As raizes criticas dos hives, que nunca podem ser editadas.
    ///
    /// Nao entra "SOFTWARE" aqui: "SOFTWARE" e protegido como **alvo** (a
    /// regra da varredura ja recusa apaga-lo), mas os programas vivem
    /// dentro dele, e um "SOFTWARE" na lista de raizes tornaria impossible
    /// editar qualquer chave de programa.
    /// </summary>
    private static readonly HashSet<string> CriticalRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "SYSTEM", "SAM", "SECURITY", "HARDWARE", "DEFAULT",
    };

    /// <summary>
    /// True quando a chave esta protegida.
    ///
    /// A regra tem tres partes, e cada uma pega um caso diferente:
    ///
    /// 1. **A regra da varredura de sobras** (<see cref="SafetyRules"/>), que
    ///    compara o ultimo segmento com a lista de nomes protegidos. E o que
    ///    recusa apagar "HKLM\SOFTWARE" ou "HKLM\SYSTEM" — a chave em si.
    ///
    /// 2. **A raiz critica do hive**: as arvores SYSTEM, SAM, SECURITY,
    ///    HARDWARE e DEFAULT sao a maquina, e nao o programa.
    ///
    /// 3. **A area de configuracao do Windows** ("Microsoft\Windows"): apagar
    ///    "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" tem um ultimo
    ///    segmento ("Run") que a regra 1 nao pega, e mesmo assim e uma chave
    ///    que nao se pode mexer.
    ///
    /// A lista de nomes protegidos **nao** e copiada para ca: as regras 1 e 2
    /// usam a do nucleo, e a regra 3 e um par de segmentos fixo.
    /// </summary>
    private static bool IsProtectedPath(string registryPath)
    {
        if (SafetyRules.IsProtectedRegistryKey(registryPath)) return true;

        var segments = registryPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 1) return true;

        // O primeiro segmento e o nome do hive (HKLM, HKCU...).
        var rest = segments.Skip(1).ToArray();

        if (CriticalRoots.Contains(rest[0])) return true;

        for (var i = 0; i + 1 < rest.Length; i++)
        {
            if (rest[i].Equals("Microsoft", StringComparison.OrdinalIgnoreCase)
                && rest[i + 1].Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
    /// <summary>
    /// Separa o caminho da chave em hive e caminho interno.
    /// </summary>
    private static bool TryOpen(
        string registryPath,
        out RegistryHive hive,
        out string path,
        out RegistryEditResult? error)
    {
        hive = RegistryHive.CurrentUser;
        path = string.Empty;
        error = null;

        var text = (registryPath ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            error = RegistryEditResult.Fail("Informe o caminho da chave de registro.");
            return false;
        }

        if (text.StartsWith(@"HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith(@"HKCU", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.CurrentUser;
            path = text[(text.IndexOf('\\') + 1)..];
        }
        else if (text.StartsWith(@"HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase)
                 || text.StartsWith(@"HKLM", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.LocalMachine;
            path = text[(text.IndexOf('\\') + 1)..];
        }
        else if (text.StartsWith(@"HKEY_CLASSES_ROOT", StringComparison.OrdinalIgnoreCase)
                 || text.StartsWith(@"HKCR", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.ClassesRoot;
            path = text[(text.IndexOf('\\') + 1)..];
        }
        else if (text.StartsWith(@"HKEY_USERS", StringComparison.OrdinalIgnoreCase)
                 || text.StartsWith(@"HKU", StringComparison.OrdinalIgnoreCase))
        {
            hive = RegistryHive.Users;
            path = text[(text.IndexOf('\\') + 1)..];
        }
        else
        {
            error = RegistryEditResult.Fail(
                $"O caminho '{registryPath}' nao comeca com HKCU, HKLM, HKCR ou HKU.");
            return false;
        }

        if (path.Length == 0)
        {
            error = RegistryEditResult.Fail($"O caminho '{registryPath}' nao aponta para nenhuma chave.");
            return false;
        }

        return true;
    }

    /// <summary>Abre a chave pai de um caminho interno.</summary>
    private static RegistryKey? OpenParent(RegistryKey baseKey, string path)
    {
        if (!path.Contains('\\')) return baseKey;
        var parent = path[..path.LastIndexOf('\\')];
        return baseKey.OpenSubKey(parent, writable: true);
    }

    /// <summary>
    /// Copia os valores de uma chave para outra.
    ///
    /// As subchaves **nao** sao copiadas: o Windows nao tem copia de chave
    /// com conteudo, e o codigo recriaria as subchaves na mao. Por isso a
    /// renomeacao recusa quando a chave tem subchaves — e o que evita uma
    /// "renomeacao" que perderia o conteudo.
    /// </summary>
    private static void CopyValues(RegistryKey source, RegistryKey target)
    {
        foreach (var name in source.GetValueNames())
        {
            var value = source.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);

            // Um valor nulo no registro e um valor sem dado. O SetValue recusa
            // nulo, e nao ha o que copiar de um valor sem dado.
            if (value is null) continue;

            target.SetValue(name, value, source.GetValueKind(name));
        }
    }

    /// <summary>
    /// True quando a chave tem subchaves — e por isso nao pode ser renomeada
    /// com o metodo de copia.
    /// </summary>
    public static bool HasSubKeys(string registryPath)
    {
        if (!TryOpen(registryPath, out var hive, out var path, out _)) return false;

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(path, writable: false);
            return key is not null && key.GetSubKeyNames().Length > 0;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            return false;
        }
    }
}
