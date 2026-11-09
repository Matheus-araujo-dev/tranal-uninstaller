using Microsoft.Win32;
using Tranal.Uninstaller.Core.RegistryEditing;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Um vestigio de "uso recente" (MRU) encontrado no Windows.
/// </summary>
public sealed class MruItem
{
    /// <summary>"Valor" ou "Chave" (quando o MRU e a chave inteira).</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>A chave de registro que guarda o vestigio.</summary>
    public string RegistryPath { get; init; } = string.Empty;

    /// <summary>O nome do valor (vazio quando o alvo e a chave inteira).</summary>
    public string ValueName { get; init; } = string.Empty;

    /// <summary>O conteudo do valor, para o usuario reconhecer.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>True quando o vestigio esta marcado para limpeza.</summary>
    public bool Selected { get; set; } = true;
}

/// <summary>
/// Limpeza do cache MRU do Windows ("Most Recently Used").
///
/// O Windows guarda em varias chaves a lista do que o usuario abriu
/// recentemente: comandos digitados no "Executar", caminhos digitados no
/// Explorer, documentos recentes, itens do "Abrir como", consultas digitadas.
///
/// O Geek Uninstaller tem um "limpar cache MRU" proprio, e o ashampoo tambem
/// (dentro do Windows Cleaner). O Revo faz o mesmo pelo modulo de limpeza.
///
/// A REGRA do que e MRU mora aqui, em um lugar so: as chaves listadas aqui
/// sao as do Windows, e a limpeza apaga os **valores** dessas chaves — nunca a
/// chave inteira, porque a chave e do Windows e recriada vazia em seguida.
/// </summary>
public static class MruCleaner
{
    /// <summary>
    /// As chaves de MRU do usuario atual.
    /// </summary>
    private static readonly string[] MruKeys =
    {
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\TypedPaths",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\WordWheelQuery",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\OpenSavePidlMRU",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\LastVisitedPidlMRU",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Search\RecentApps",
        @"HKEY_CURRENT_USER\Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\BagMRU",
        @"HKEY_CURRENT_USER\Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\Bags",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder",
        // O cache MRU do proprio Windows (a chave que o Geek tambem limpa):
        // e onde ficam os caminhos usados no "Executar" e na caixa de
        // dialogo, varridos a cada uso.
        @"HKEY_CURRENT_USER\Software\Classes\Local Settings\MrtCache",
    };

    /// <summary>
    /// As chaves de MRU que o limpador apaga.
    ///
    /// A lista e publica para que a regra seja conferivel: quem decide o
    /// que e "uso recente" esta aqui, e nao espalhado por quem apaga.
    /// </summary>
    public static IReadOnlyList<string> KnownKeys => MruKeys;

    /// <summary>
    /// Varre as chaves de MRU e devolve os valores encontrados.
    /// </summary>
    public static IReadOnlyList<MruItem> Scan()
    {
        var itens = new List<MruItem>();

        foreach (var chave in MruKeys)
        {
            try
            {
                var (hive, sub) = SafetyRules.SplitRegistryPath(chave);
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var key = baseKey.OpenSubKey(sub);
                if (key is null) continue;

                foreach (var nome in key.GetValueNames())
                {
                    if (nome.Length == 0) continue;

                    var conteudo = key.GetValue(nome) as string ?? string.Empty;

                    itens.Add(new MruItem
                    {
                        Kind = "Valor",
                        RegistryPath = chave,
                        ValueName = nome,
                        Content = conteudo,
                    });
                }
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao varrer a chave de MRU '{chave}': {ex.Message}");
            }
        }

        return itens;
    }

    /// <summary>
    /// Limpa os vestigios marcados e devolve o relatorio canonico.
    /// </summary>
    /// <param name="items">Os vestigios marcados para limpeza.</param>
    public static RemovalReport Clear(IEnumerable<MruItem> items)
    {
        var outcomes = new List<RemovalOutcome>();

        foreach (var item in items)
        {
            if (!item.Selected) continue;

            var resultado = RegistryEditor.DeleteValue(item.RegistryPath, item.ValueName);
            outcomes.Add(new RemovalOutcome
            {
                Item = new LeftoverItem
                {
                    Kind = "Registro",
                    Path = item.ValueName.Length == 0
                        ? item.RegistryPath
                        : $"{item.RegistryPath}\\{item.ValueName}",
                    Detail = "MRU",
                    Selected = true,
                },
                Status = resultado.Success ? RemovalStatus.Removed : RemovalStatus.Failed,
                Message = resultado.Message,
                ErrorCode = resultado.ErrorCode,
            });
        }

        return new RemovalReport(outcomes);
    }
}
