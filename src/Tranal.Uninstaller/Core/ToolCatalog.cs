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
    /// Painel de gestao do sistema: permissoes do programa, servicos, tarefas
    /// agendadas, drivers e saude do software.
    /// </summary>
    public const string SystemManagement = "SystemManagement";

    /// <summary>
    /// Aparencia: mapa de area, legenda de cores, agrupamento por coluna e
    /// classificacao por estrelas.
    /// </summary>
    public const string Appearance = "Appearance";

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
        CustomTools,
        SystemManagement,
        Appearance,
    };
}
