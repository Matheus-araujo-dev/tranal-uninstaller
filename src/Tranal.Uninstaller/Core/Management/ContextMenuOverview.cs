using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Topmost;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// De quem e a entrada de menu de contexto.
/// </summary>
public enum ContextMenuKind
{
    /// <summary>Uma entrada do proprio Tranal.</summary>
    Tranal,

    /// <summary>Uma entrada que um programa instalado deixou no Explorer.</summary>
    Programa,

    /// <summary>Uma entrada do proprio Windows.</summary>
    Windows,

    /// <summary>Uma entrada que o usuario criou no gerenciador.</summary>
    Pessoal,
}

/// <summary>
/// Uma entrada de menu de contexto, vista pelo gerenciador.
///
/// O item nao carrega a regra: ele carrega o que foi medido e o que pode
/// ser feito com a entrada. A regra de escrever continua nos modulos que a
/// escrevem (<see cref="ExplorerContextMenu"/> e
/// <see cref="ExplorerShellMenu"/>); este modulo so junta e mostra.
/// </summary>
public sealed class ContextMenuItem
{
    /// <summary>De quem e a entrada.</summary>
    public ContextMenuKind Kind { get; init; }

    /// <summary>
    /// O identificador da entrada, unico na maquina.
    ///
    /// Para as do Tranal e o id fixo da entrada (<c>TranalUninstaller</c>,
    /// <c>TranalShred</c>...); para as de terceiros, o caminho completo da
    /// chave do registro — dois programas podem usar o mesmo nome de chave em
    /// lugares diferentes, e o caminho e o que separa um do outro.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>O nome que aparece no menu.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Onde a entrada aparece (a chave do registro, ou o local).</summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>A DLL (ou o executavel) que implementa a entrada.</summary>
    public string ServerPath { get; init; } = string.Empty;

    /// <summary>
    /// A entrada esta ativa? So tem sentido para as entradas do Tranal: as
    /// de terceiros nao tem chave de ligar e desligar.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>A entrada pode ser removida por aqui?</summary>
    public bool CanRemove { get; init; }

    /// <summary>O programa que deixou a entrada (so para as de programas).</summary>
    public string Program { get; init; } = string.Empty;
}

/// <summary>
/// O gerenciador de menu de contexto: as entradas do Explorer, do Tranal e
/// dos outros programas, em uma lista so.
///
/// As entradas do Tranal sao as quatro que ele registra: a de desinstalar
/// (<see cref="ExplorerContextMenu"/>), a de rastrear a instalacao, a de
/// shredar um arquivo e a do monitor de instalacao
/// (<see cref="ExplorerShellMenu"/>). As de terceiros sao as que o
/// <see cref="ContextMenuManager"/> encontra no registro.
///
/// A regra de escrever continua nos modulos que escrevem: este modulo so
/// lista e delega.
/// </summary>
public static class ContextMenuOverview
{
    /// <summary>
    /// Todas as entradas de menu de contexto: as do Tranal primeiro, depois
    /// as dos outros programas.
    /// </summary>
    public static IReadOnlyList<ContextMenuItem> List()
    {
        var itens = new List<ContextMenuItem>();
        itens.AddRange(TranalEntries());
        itens.AddRange(OtherEntries());
        return itens;
    }

    /// <summary>
    /// As entradas do Tranal no Explorer, com o estado de cada uma.
    ///
    /// Uma entrada do Tranal pode morar em mais de um lugar do registro (o
    /// "Rastrear a instalacao" vale para <c>.exe</c> e para <c>.msi</c>):
    /// aqui ela aparece uma vez so, com todos os lugares onde mora.
    /// </summary>
    public static IReadOnlyList<ContextMenuItem> TranalEntries()
    {
        var itens = new List<ContextMenuItem>();

        itens.Add(new ContextMenuItem
        {
            Kind = ContextMenuKind.Tranal,
            Id = ExplorerContextMenu.ShellKeyName,
            Title = ExplorerContextMenu.RegistrationDescription,
            Location = @"HKCU\" + ExplorerContextMenu.ShellKeyRelative,
            IsEnabled = ExplorerContextMenu.IsRegistered(),
            CanRemove = true,
        });

        foreach (var grupo in ExplorerShellMenu.Entries.GroupBy(e => e.DisplayName, StringComparer.Ordinal))
        {
            var entradas = grupo.ToList();

            // O identificador do grupo e o mais curto: e o que o
            // <see cref="ExplorerShellMenu.Remove"/> apaga por completo,
            // porque as chaves da mesma entrada compartilham o prefixo.
            var id = entradas
                .Select(e => e.Id)
                .OrderBy(i => i.Length)
                .ThenBy(i => i, StringComparer.Ordinal)
                .First();

            itens.Add(new ContextMenuItem
            {
                Kind = ContextMenuKind.Tranal,
                Id = id,
                Title = entradas[0].DisplayName,
                Location = string.Join(", ", entradas.Select(e => @"HKCU\" + e.ShellKeyRelative)),
                IsEnabled = ExplorerShellMenu.IsRegistered(id),
                CanRemove = true,
            });
        }

        // As entradas que o usuario criou no gerenciador.
        foreach (var custom in CustomShellMenu.List())
        {
            itens.Add(new ContextMenuItem
            {
                Kind = ContextMenuKind.Pessoal,
                Id = custom.Id,
                Title = custom.DisplayName,
                Location = @"HKCU\" + CustomShellMenu.ShellFolder + @"\" + custom.Id,
                IsEnabled = true,
                CanRemove = true,
                Program = custom.Command,
            });
        }

        return itens;
    }

    /// <summary>
    /// As entradas que os outros programas deixaram no Explorer.
    /// </summary>
    public static IReadOnlyList<ContextMenuItem> OtherEntries() => Build(ContextMenuManager.List());

    /// <summary>
    /// Converte as entradas de terceiros ja medidas em linhas do gerenciador.
    ///
    /// A leitura vem do registro; a conversao nao. Separar as duas coisas e o
    /// que permite conferir a regra com uma lista montada a mao, sem
    /// precisar escrever no registro de quem roda o teste.
    /// </summary>
    /// <param name="entries">As entradas de terceiros ja medidas.</param>
    public static IReadOnlyList<ContextMenuItem> Build(IReadOnlyList<ContextMenuEntry> entries)
    {
        var itens = new List<ContextMenuItem>();
        if (entries is null) return itens;

        // O dono de cada entrada e resolvido com UMA varredura de programas
        // instalados: resolver um por um faria uma varredura por entrada.
        var donos = HunterMode.ResolveExecutables(entries.Select(e => e.ServerPath));

        foreach (var entry in entries)
        {
            var doPrograma = !entry.IsSystem;

            itens.Add(new ContextMenuItem
            {
                Kind = doPrograma ? ContextMenuKind.Programa : ContextMenuKind.Windows,
                Id = entry.HandlerKeyPath,
                Title = string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.Clsid : entry.DisplayName,
                Location = entry.Location,
                ServerPath = entry.ServerPath,
                IsEnabled = true,
                CanRemove = doPrograma,
                // O programa que deixou a entrada. Quando o registro de
                // desinstalacao nao sabe dizer qual e (a entrada e de um
                // programa que nao aparece na lista de instalados), a
                // identificacao honesta e o nome da DLL que a implementa.
                Program = doPrograma
                    ? (donos.TryGetValue(entry.ServerPath, out var dono)
                        ? dono.DisplayName
                        : NomeDaDll(entry.ServerPath))
                    : string.Empty,
            });
        }

        return itens;
    }

    /// <summary>
    /// O nome do arquivo que implementa a entrada.
    ///
    /// E a identificacao de quem deixou a entrada quando o programa nao
    /// aparece na lista de instalados: e o nome da DLL que o Windows carrega.
    /// </summary>
    /// <param name="serverPath">O caminho da DLL.</param>
    private static string NomeDaDll(string serverPath)
    {
        if (string.IsNullOrWhiteSpace(serverPath)) return string.Empty;

        try
        {
            return Path.GetFileName(serverPath);
        }
        catch (ArgumentException)
        {
            // Caminho invalido: sem nome de arquivo.
            return string.Empty;
        }
    }

    /// <summary>
    /// Cria uma entrada de menu de contexto escolhida pelo usuario.
    ///
    /// O nome escolhido e o que aparece no botao direito; o comando e o que
    /// roda quando a entrada e acionada.
    /// </summary>
    /// <param name="displayName">O nome que aparece no menu.</param>
    /// <param name="command">O comando que roda a entrada.</param>
    public static ManagementResult Create(string displayName, string command)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return ManagementResult.Fail("Informe o nome da entrada.");

        if (string.IsNullOrWhiteSpace(command))
            return ManagementResult.Fail("Informe o comando da entrada.");

        var id = CustomShellMenu.IdFor(displayName);

        var criado = CustomShellMenu.Create(id, displayName, command, IconeDoComando(command));

        return criado
            ? ManagementResult.Ok($"A entrada '{displayName}' foi criada no menu de contexto.")
            : ManagementResult.Fail($"Nao foi possivel criar a entrada '{displayName}'.");
    }

    /// <summary>
    /// Apaga todas as entradas que o gerenciador pode apagar: as do Tranal,
    /// as que o usuario criou e as que os outros programas deixaram.
    ///
    /// As do Windows nao entram: elas nao sao sobra de programa e remove-las
    /// quebraria o Explorer.
    /// </summary>
    public static ManagementResult RemoveAll()
    {
        var removidas = 0;
        var falhas = 0;

        // Tudo que vai sair e guardado em um .reg antes: o backup e o
        // caminho de volta das entradas dos outros programas.
        var guardadas = ContextMenuManager.List()
            .Where(e => !e.IsSystem)
            .Select(e => e.HandlerKeyPath)
            .ToList();

        var arquivo = string.Empty;
        if (guardadas.Count > 0)
        {
            try
            {
                arquivo = RegistryBackup.Backup(guardadas);
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao guardar as entradas de menu de contexto: {ex.Message}");
            }
        }

        if (ExplorerContextMenu.RemoveRegistryScript()) removidas++;
        else falhas++;

        foreach (var grupo in ExplorerShellMenu.Entries.GroupBy(e => e.DisplayName, StringComparer.Ordinal))
        {
            var id = grupo
                .Select(e => e.Id)
                .OrderBy(i => i.Length)
                .ThenBy(i => i, StringComparer.Ordinal)
                .First();

            if (ExplorerShellMenu.Remove(id) > 0) removidas++;
            else falhas++;
        }

        foreach (var custom in CustomShellMenu.List())
        {
            if (CustomShellMenu.Remove(custom.Id)) removidas++;
            else falhas++;
        }

        foreach (var entry in ContextMenuManager.List())
        {
            if (entry.IsSystem) continue;

            // O backup ja foi feito acima: um por entrada sobrescreveria o
            // arquivo com a ultima entrada.
            if (ContextMenuManager.Remove(entry, backup: false).Success) removidas++;
            else falhas++;
        }

        var mensagem = falhas == 0
            ? $"{removidas} entrada(s) de menu de contexto foram removidas."
            : $"{removidas} entrada(s) foram removidas e {falhas} falharam.";

        if (arquivo.Length > 0)
            mensagem += $" As chaves foram guardadas em {arquivo}.";

        return falhas == 0
            ? ManagementResult.Ok(mensagem)
            : ManagementResult.Fail(mensagem);
    }

    /// <summary>
    /// O executavel de um comando, para usar como icone da entrada.
    /// </summary>
    /// <param name="command">A linha de comando.</param>
    private static string IconeDoComando(string command)
    {
        try
        {
            return ExecutablePathResolver.Extract(command);
        }
        catch (ArgumentException)
        {
            // Comando invalido: a entrada fica sem icone proprio.
            return string.Empty;
        }
    }

    /// <summary>
    /// Procura uma entrada pelo identificador (ou pelo nome exibido).
    ///
    /// A busca pelo nome existe para o usuario: na linha de comando o nome
    /// e o que se ve na tela; o identificador e o que a maquina garante.
    /// </summary>
    /// <param name="id">O identificador ou o nome da entrada.</param>
    public static ContextMenuItem? Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        var alvo = id.Trim();
        var todas = List();

        return todas.FirstOrDefault(e =>
                   string.Equals(e.Id, alvo, StringComparison.OrdinalIgnoreCase))
               ?? todas.FirstOrDefault(e =>
                   string.Equals(e.Title, alvo, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Registra as entradas do Tranal no Explorer ("Restaurar").
    /// </summary>
    public static ManagementResult Restore()
    {
        try
        {
            var desinstalar = ExplorerContextMenu.Apply(true);
            var explorer = ExplorerShellMenu.Apply(true);

            if (!desinstalar && explorer == 0)
                return ManagementResult.Fail("Nenhuma entrada do Tranal pode ser registrada: o executavel nao foi encontrado.");

            return ManagementResult.Ok($"Entradas do Tranal registradas no menu de contexto ({explorer} no Explorer).");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return ManagementResult.Fail("Nao foi possivel registrar as entradas do Tranal: " + ex.Message);
        }
    }

    /// <summary>
    /// Remove as entradas do Tranal do Explorer ("Desativar").
    /// </summary>
    public static ManagementResult Disable()
    {
        try
        {
            ExplorerContextMenu.RemoveRegistryScript();
            var removidas = ExplorerShellMenu.Apply(false);

            return ManagementResult.Ok($"Entradas do Tranal removidas do menu de contexto ({removidas} no Explorer).");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return ManagementResult.Fail("Nao foi possivel remover as entradas do Tranal: " + ex.Message);
        }
    }

    /// <summary>
    /// Remove uma entrada do menu de contexto.
    ///
    /// A entrada do Tranal e desregistrada pelo modulo que a escreve; a de
    /// terceiros, pelo <see cref="ContextMenuManager"/>.
    /// </summary>
    /// <param name="item">A entrada a remover.</param>
    public static ManagementResult Remove(ContextMenuItem item)
    {
        if (item is null) return ManagementResult.Fail("Entrada nao informada.");

        if (item.Kind == ContextMenuKind.Tranal)
        {
            if (string.Equals(item.Id, ExplorerContextMenu.ShellKeyName, StringComparison.OrdinalIgnoreCase))
            {
                return ExplorerContextMenu.RemoveRegistryScript()
                    ? ManagementResult.Ok($"A entrada '{item.Title}' foi removida.")
                    : ManagementResult.Fail($"Nao foi possivel remover a entrada '{item.Title}'.");
            }

            var removidas = ExplorerShellMenu.Remove(item.Id);
            return removidas > 0
                ? ManagementResult.Ok($"A entrada '{item.Title}' foi removida.")
                : ManagementResult.Fail($"Nao foi possivel remover a entrada '{item.Title}'.");
        }

        if (item.Kind == ContextMenuKind.Pessoal)
        {
            return CustomShellMenu.Remove(item.Id)
                ? ManagementResult.Ok($"A entrada '{item.Title}' foi removida.")
                : ManagementResult.Fail($"Nao foi possivel remover a entrada '{item.Title}'.");
        }

        if (!item.CanRemove)
        {
            return ManagementResult.Fail(
                $"A entrada '{item.Title}' e do Windows e nao pode ser removida daqui.");
        }

        var entry = ContextMenuManager.List()
            .FirstOrDefault(e => string.Equals(e.HandlerKeyPath, item.Id, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
            return ManagementResult.Fail($"A entrada '{item.Title}' nao existe mais.");

        return ContextMenuManager.Remove(entry);
    }
}
