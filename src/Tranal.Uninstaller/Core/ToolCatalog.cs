namespace Tranal.Uninstaller.Core;

/// <summary>
/// Catalogo das ferramentas do Tranal.
///
/// E a fonte unica dos ids das ferramentas: a barra lateral, o registro de
/// telas e a suite de testes usam esta lista. Sem ela, um botao da barra
/// lateral pode existir sem a tela correspondente — e o clique abriria um
/// painel vazio, sem nenhum aviso.
///
/// A ordem segue a arvore de ferramentas do Revo Uninstaller Pro.
/// </summary>
public static class ToolCatalog
{
    /// <summary>Gerenciador de execucao automatica.</summary>
    public const string AutorunManager = "AutorunManager";
    public const string BootCenter = "BootCenter";
    public const string ActiveFiles = "ActiveFiles";
    public const string FileAssociator = "FileAssociator";
    public const string UndoCenter = "UndoCenter";
    public const string FileOwnership = "FileOwnership";
    public const string DeepAnalysis = "DeepAnalysis";

    /// <summary>Limpeza de arquivos temporarios.</summary>
    public const string JunkCleaner = "JunkCleaner";

    /// <summary>Ferramentas do Windows.</summary>
    public const string WindowsTools = "WindowsTools";

    /// <summary>Removedor de evidencias.</summary>
    public const string EvidenceRemover = "EvidenceRemover";

    /// <summary>Exclusao irrecuperavel.</summary>
    public const string UnrecoverableDelete = "UnrecoverableDelete";

    /// <summary>Otimizador de registro (entradas obsoletas do Menu Iniciar).</summary>
    public const string RegistryOptimizer = "RegistryOptimizer";

    /// <summary>
    /// Limpador de registro: as sobras do registro, agrupadas por categoria.
    /// </summary>
    public const string RegistryCleaner = "RegistryCleaner";

    /// <summary>
    /// Rastros de privacidade: o que a maquina guarda do uso do computador.
    /// </summary>
    public const string PrivacyTraces = "PrivacyTraces";

    /// <summary>
    /// Fluxos alternativos de dados (ADS): o conteudo escondido ao lado de
    /// um arquivo.
    /// </summary>
    public const string AlternateDataStreams = "AlternateDataStreams";

    /// <summary>
    /// Ferramentas personalizadas: comandos proprios do usuario, dentro do
    /// Tranal.
    /// </summary>
    public const string CustomTools = "CustomTools";

    /// <summary>
    /// Painel de processos: o que esta rodando agora, e de qual programa e.
    /// </summary>
    public const string Processes = "Processes";

    /// <summary>
    /// Painel de gestao do sistema: permissoes do programa, servicos, tarefas
    /// agendadas, drivers e saude do software.
    /// </summary>
    public const string SystemManagement = "SystemManagement";

    /// <summary>
    /// Aparencia: mapa de area, legenda de cores, agrupamento por coluna e
    /// classificacao por estrelas.
    /// </summary>
    public const string Appearance = "Appearance";

    /// <summary>
    /// Informacoes do sistema: sistema operacional, processador, memoria, disco,
    /// video e tempo de atividade.
    /// </summary>
    public const string SystemInfo = "SystemInfo";

    /// <summary>
    /// Impacto dos programas: quais pesam mais na maquina (CPU e memoria).
    /// </summary>
    public const string ProgramImpact = "ProgramImpact";

    /// <summary>
    /// Relatorio de falhas: as quedas do Windows agrupadas por programa.
    /// </summary>
    public const string CrashAnalyzer = "CrashAnalyzer";

    /// <summary>
    /// Relocacao: os programas movidos entre discos, e a volta ao lugar
    /// original.
    /// </summary>
    public const string Relocation = "Relocation";

    /// <summary>
    /// Destravador: quem esta segurando um arquivo, e as acoes para soltar.
    /// </summary>
    public const string Unlocker = "Unlocker";

    /// <summary>
    /// Limpador de internet: cookies, historico, cache e formularios dos
    /// navegadores.
    /// </summary>
    public const string InternetCleaner = "InternetCleaner";

    /// <summary>
    /// Drive Cleaner: arquivos desnecessarios do disco, por categoria.
    /// </summary>
    public const string DriveCleaner = "DriveCleaner";

    /// <summary>
    /// Conserto de atalhos quebrados do Menu Iniciar e da Area de Trabalho.
    /// </summary>
    public const string ShortcutFixer = "ShortcutFixer";

    /// <summary>
    /// Limpeza do cache MRU do Windows ("uso recente").
    /// </summary>
    public const string MruCleaner = "MruCleaner";

    /// <summary>
    /// Catalogo de componentes compartilhados (DLLs compartilhadas).
    /// </summary>
    public const string SharedDlls = "SharedDlls";

    /// <summary>
    /// Consulta da Lixeira: o que foi apagado sem querer.
    /// </summary>
    public const string RecycleBin = "RecycleBin";

    /// <summary>
    /// Localizador de arquivos e chaves de registro pelo nome.
    /// </summary>
    public const string Finder = "Finder";

    /// <summary>
    /// Acoes agendadas para o proximo logon (RunOnce).
    /// </summary>
    public const string RunOnce = "RunOnce";

    /// <summary>
    /// Visor hexadecimal com a soma de verificacao do arquivo.
    /// </summary>
    public const string HexViewer = "HexViewer";

    /// <summary>
    /// Programas que vieram com o Windows e quase nunca sao usados.
    /// </summary>
    public const string Bloatware = "Bloatware";

    /// <summary>
    /// Instalacoes duplicadas: o mesmo programa em dois lugares.
    /// </summary>
    public const string Duplicates = "Duplicates";

    /// <summary>
    /// Impressoras instaladas e fila de impressao.
    /// </summary>
    public const string Printers = "Printers";

    /// <summary>
    /// Dumps de falha do Windows.
    /// </summary>
    public const string CrashDumps = "CrashDumps";

    /// <summary>
    /// Limpeza do navegador: extensoes orfas e reset de padroes.
    /// </summary>
    public const string BrowserCleanup = "BrowserCleanup";

    /// <summary>
    /// Atualizacoes dos programas instalados: versoes novas publicadas pelos
    /// fabricantes, com lista negra.
    /// </summary>
    public const string SoftwareUpdates = "SoftwareUpdates";

    /// <summary>
    /// Atualizacao do proprio Tranal.
    /// </summary>
    public const string AppUpdate = "AppUpdate";

    /// <summary>
    /// Historico de desinstalacoes: o que saiu do computador, com a data
    /// em tempo relativo, os residuos que foram junto e o botao que devolve
    /// as coisas.
    /// </summary>
    public const string UninstallHistory = "UninstallHistory";

    /// <summary>Tracks Cleaner: todos os modulos de uma vez.</summary>
    public const string HistoryCleaner = "HistoryCleaner";

    /// <summary>Tracks Cleaner: modulo dos navegadores.</summary>
    public const string TracksBrowsers = "TracksBrowsers";

    /// <summary>Tracks Cleaner: modulo do Microsoft Office.</summary>
    public const string TracksOffice = "TracksOffice";

    /// <summary>Tracks Cleaner: modulo do Windows.</summary>
    public const string TracksWindows = "TracksWindows";

    /// <summary>
    /// Todas as ferramentas, na ordem em que aparecem na barra lateral.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = new[]
    {
        AutorunManager,
        BootCenter,
        ActiveFiles,
        FileAssociator,
        UndoCenter,
        FileOwnership,
        DeepAnalysis,
        JunkCleaner,
        WindowsTools,
        EvidenceRemover,
        UnrecoverableDelete,
        HistoryCleaner,
        TracksBrowsers,
        TracksOffice,
        TracksWindows,
        RegistryOptimizer,
        RegistryCleaner,
        PrivacyTraces,
        AlternateDataStreams,
        Unlocker,
        InternetCleaner,
        DriveCleaner,
        ShortcutFixer,
        MruCleaner,
        SharedDlls,
        RecycleBin,
        Finder,
        RunOnce,
        HexViewer,
        Bloatware,
        Duplicates,
        Printers,
        CrashDumps,
        BrowserCleanup,
        SoftwareUpdates,
        AppUpdate,
        UninstallHistory,
        CustomTools,
        Processes,
        SystemManagement,
        Appearance,
        SystemInfo,
        ProgramImpact,
        CrashAnalyzer,
        Relocation,
    };
}
