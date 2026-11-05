using System.Diagnostics;
using System.IO;

namespace Tranal.Uninstaller.Core.Tools;

/// <summary>
/// Uma ferramenta nativa do WindowsOferecida pelo Tranal Uninstaller Pro.
/// </summary>
public sealed class WindowsTool
{
    /// <summary>Nome exibido da ferramenta.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Categoria estavel ("system", "disk", "network", "users", "services", "tasks", "dev", "security").</summary>
    public string CategoryId { get; init; } = string.Empty;

    /// <summary>Executavel ou comando (ex.: "cleanmgr.exe").</summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>Argumentos passados ao comando.</summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>Descricao curta do que a ferramenta faz.</summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Catalogo das ferramentas nativas do Windows (paridade com o modulo "Windows Tools" do Revo).
/// Todas as entradas sao executadas pelo shell do Windows, de modo que arquivos .msc,
/// .cpl e .exe abrem corretamente.
/// </summary>
public static class WindowsToolsCatalog
{
    /// <summary>
    /// Lista imutavel com todas as ferramentas do catalogo.
    /// </summary>
    public static IReadOnlyList<WindowsTool> All { get; } = Array.AsReadOnly(new[]
    {
        new WindowsTool
        {
            Name = "Limpeza de Disco",
            CategoryId = "disk",
            Command = "cleanmgr.exe",
            Description = "Remove arquivos temporarios e libera espaco em disco.",
        },
        new WindowsTool
        {
            Name = "Otimizacao de Disco",
            CategoryId = "disk",
            Command = "dfrgui.exe",
            Description = "Desfragmenta e otimiza as unidades de disco.",
        },
        new WindowsTool
        {
            Name = "Gerenciador de Particoes",
            CategoryId = "disk",
            Command = "diskmgmt.msc",
            Description = "Gerencia particoes, volumes e letras de unidade.",
        },
        new WindowsTool
        {
            Name = "Configuracao do Sistema",
            CategoryId = "system",
            Command = "msconfig.exe",
            Description = "Inicializacao, programas de inicializacao e opcoes de diagnostico.",
        },
        new WindowsTool
        {
            Name = "Gerenciador de Tarefas",
            CategoryId = "tasks",
            Command = "taskmgr.exe",
            Description = "Processos, desempenho, historico de inicializacao e aplicacoes.",
        },
        new WindowsTool
        {
            Name = "Agendador de Tarefas",
            CategoryId = "tasks",
            Command = "taskschd.msc",
            Description = "Gerencia as tarefas agendadas do Windows.",
        },
        new WindowsTool
        {
            Name = "Servicos",
            CategoryId = "services",
            Command = "services.msc",
            Description = "Gerencia os servicos do Windows e seus modos de inicializacao.",
        },
        new WindowsTool
        {
            Name = "Gerenciador de Dispositivos",
            CategoryId = "system",
            Command = "devmgmt.msc",
            Description = "Gerencia os dispositivos de hardware e seus drivers.",
        },
        new WindowsTool
        {
            Name = "Visualizador de Eventos",
            CategoryId = "system",
            Command = "eventvwr.msc",
            Description = "Consulta os logs de evento do Windows.",
        },
        new WindowsTool
        {
            Name = "Editor do Registro",
            CategoryId = "system",
            Command = "regedit.exe",
            Description = "Edita as chaves do registro do Windows.",
        },
        new WindowsTool
        {
            Name = "Monitor de Desempenho",
            CategoryId = "system",
            Command = "perfmon.exe",
            Description = "Graficos de desempenho do sistema em tempo real.",
        },
        new WindowsTool
        {
            Name = "Monitor de Recursos",
            CategoryId = "system",
            Command = "resmon.exe",
            Description = "Uso de CPU, memoria, disco e rede por processo.",
        },
        new WindowsTool
        {
            Name = "Diagnostico de Problemas",
            CategoryId = "system",
            Command = "msdt.exe",
            Description = "Executa o assistente de diagnostico do Windows.",
        },
        new WindowsTool
        {
            Name = "Gerenciamento do Computador",
            CategoryId = "system",
            Command = "compmgmt.msc",
            Description = "Reune as ferramentas de gerenciamento do sistema.",
        },
        new WindowsTool
        {
            Name = "Configuracao de Backup",
            CategoryId = "system",
            Command = "wbadmin.msc",
            Description = "Configura e restaura copias de seguranca do Windows.",
        },
        new WindowsTool
        {
            Name = "Prompt de Comando",
            CategoryId = "dev",
            Command = "cmd.exe",
            Description = "Prompt de comando do Windows.",
        },
        new WindowsTool
        {
            Name = "PowerShell",
            CategoryId = "dev",
            Command = "powershell.exe",
            Description = "Console do Windows PowerShell.",
        },
        new WindowsTool
        {
            Name = "Checksum",
            CategoryId = "dev",
            Command = "certutil.exe",
            Description = "Calcula o hash de arquivos (util para conferir integridade).",
        },
        new WindowsTool
        {
            Name = "Paineis de Controle",
            CategoryId = "system",
            Command = "control.exe",
            Description = "Abre o Painel de Controle do Windows.",
        },
        new WindowsTool
        {
            Name = "Ferramentas de Administracao",
            CategoryId = "system",
            Command = "control.exe",
            Arguments = "/name Microsoft.Administration.Tools",
            Description = "Abre as ferramentas administrativas do Windows.",
        },
        new WindowsTool
        {
            Name = "Gerenciador de Impressoras",
            CategoryId = "system",
            Command = "printui.exe",
            Description = "Gerencia as impressoras e os drivers de impressora.",
        },
        new WindowsTool
        {
            Name = "Gerenciador de Impressoras (MMC)",
            CategoryId = "system",
            Command = "mmc.exe",
            Arguments = "printmgmt.msc",
            Description = "Gerenciador de impressoras com visualizacao de fila.",
        },
        new WindowsTool
        {
            Name = "Firewall do Windows",
            CategoryId = "security",
            Command = "firewall.cpl",
            Description = "Regras do Firewall do Windows Defender.",
        },
        new WindowsTool
        {
            Name = "Gerenciador de Usuarios e Computadores",
            CategoryId = "users",
            Command = "lusrmgr.msc",
            Description = "Gerencia usuarios locais, grupos e contas.",
        },
        new WindowsTool
        {
            Name = "Conexoes de Rede",
            CategoryId = "network",
            Command = "ncpa.cpl",
            Description = "Adaptadores, conexoes e compartilhamento de rede.",
        },
        new WindowsTool
        {
            Name = "Opcoes da Internet (Atalhos do Windows)",
            CategoryId = "network",
            Command = "inetcpl.cpl",
            Description = "Atalhos do Windows, Internet e zonas de seguranca.",
        },
        new WindowsTool
        {
            Name = "Fontes",
            CategoryId = "system",
            Command = "fonts.cpl",
            Description = "Gerencia as fontes instaladas no Windows.",
        },
        new WindowsTool
        {
            Name = "Programas e Recursos",
            CategoryId = "system",
            Command = "appwiz.cpl",
            Description = "Desinstalacao de programas e habilitacao de recursos do Windows.",
        },
        new WindowsTool
        {
            Name = "Codecs de Midia",
            CategoryId = "system",
            Command = "mmsys.cpl",
            Description = "Configuracao dos codecs e dispositivos de midia.",
        },
    });

    /// <summary>
    /// Executa a ferramenta pelo shell do Windows.
    /// Devolve true quando o processo foi iniciado e false quando falhou.
    /// Nunca lanca.
    /// </summary>
    public static bool Run(WindowsTool tool)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tool.Command))
            {
                ToolLog.Info("Ferramenta sem comando definido.");
                return false;
            }

            // UseShellExecute permite abrir .msc, .cpl e .exe como o proprio Windows faria.
            var startInfo = new ProcessStartInfo(tool.Command, tool.Arguments ?? string.Empty)
            {
                UseShellExecute = true,
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                ToolLog.Error($"Nao foi possivel iniciar '{tool.Name}' ({tool.Command}).");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao iniciar '{tool.Name}' ({tool.Command}): {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// Registro de diagnostico das ferramentas do nucleo (best effort).
/// Existe aqui porque e o ponto comum das seis ferramentas: qualquer falha
/// de operacao precisa ser registrada sem interromper o fluxo principal.
/// </summary>
internal static class ToolLog
{
    private static readonly object Gate = new();

    private static readonly string FilePath = Path.Combine(
        RegistryBackup.DataDirectory, "logs", "tools.log");

    /// <summary>Grava uma informacao no registro de diagnostico.</summary>
    internal static void Info(string message) => Write("INFO", message);

    /// <summary>Grava uma falha no registro de diagnostico.</summary>
    internal static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                File.AppendAllText(FilePath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // O log e best effort: uma falha ao gravar nunca pode derrubar a operacao.
        }
    }
}
