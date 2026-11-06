using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace Tranal.Uninstaller.Core.Tracing;

/// <summary>
/// Observa as escritas que acontecem numa pasta, em tempo real e sem driver.
///
/// O Tranal fazia ate agora <b>polling</b>: a cada 30 segundos tirava uma
/// fotografia do sistema e comparava. Duas capturas podem perder uma escrita
/// rapida — e e exatamente a escrita rapida do instalador que interessa.
///
/// Este watcher usa a API do proprio Windows, <c>ReadDirectoryChangesW</c>
/// (<c>kernel32</c>): e o mecanismo real de observacao de escrita, e nao o
/// <c>FileSystemWatcher</c> — que coalesce eventos e perde alteracao sob
/// carga, exatamente o defeito que o polling tinha.
///
/// A leitura roda numa thread dedicada e bloqueia ate haver mudanca; e o
/// <c>CancelIoEx</c> (o mesmo caminho do Windows que interrompe leitura de
/// diretorio) acorda essa thread no <see cref="Dispose"/>.
///
/// As tres garantias do watcher:
///   1. <b>Nao derruba o processo.</b> Falha do watcher e registrada em
///      <see cref="UltimoErro"/>; excecao nunca chega a quem chamou.
///   2. <b>E descartavel.</b> <see cref="Dispose"/> fecha o handle do sistema.
///   3. <b>Falha limpa.</b> Pasta que nao existe (o %TEMP% apagado) devolve
///      <c>null</c> em <see cref="Iniciar"/>, sem excecao.
///
/// O buffer fica em memoria nativa: a leitura bloqueia a thread e o coletor
/// nao pode mover o buffer no meio da operacao.
/// </summary>
public sealed class WriteWatcher : IDisposable
{
    // Acesso desejado: listar o conteudo da pasta.
    private const uint AcessoListarDiretorio = 0x0001;

    // Compartilhamento: o watcher convive com quem estiver escrevendo.
    private const uint CompartilhamentoLeitura = 0x0001;
    private const uint CompartilhamentoEscrita = 0x0002;
    private const uint CompartilhamentoExclusao = 0x0004;

    private const uint AbrirExistente = 3;

    // Semantica de backup: e o que permite abrir uma pasta como handle.
    private const uint FlagSemanticaDeBackup = 0x02000000;

    // O que o watcher pede para ser avisado.
    private const uint NotificarNome = 0x0001;
    private const uint NotificarNomePasta = 0x0002;
    private const uint NotificarTamanho = 0x0008;
    private const uint NotificarUltimaEscrita = 0x0010;
    private const uint NotificarCriacao = 0x0040;

    private const uint FiltroDeNotificacao =
        NotificarNome | NotificarNomePasta | NotificarTamanho | NotificarUltimaEscrita | NotificarCriacao;

    // Codigos de erro do Windows usados aqui.
    private const int ErroOperacaoAbortada = 995;
    private const int ErroBufferCheio = 234;

    // Acoes do FILE_NOTIFY_INFORMATION.
    private const uint AcaoAdicionado = 1;
    private const uint AcaoRemovido = 2;
    private const uint AcaoModificado = 3;
    private const uint AcaoRenomeadoAntigo = 4;
    private const uint AcaoRenomeadoNovo = 5;

    // O buffer precisa comportar o cabecalho (12 bytes) mais o nome do item.
    private const int TamanhoDoBuffer = 64 * 1024;

    // O cabecalho de cada entrada do buffer: 4 (proximo) + 4 (acao) +
    // 4 (tamanho do nome) = 12 bytes.
    private const int CabecalhoDaEntrada = 12;

    // Quanto o Iniciar espera a thread se anunciar antes de devolver: sem
    // isso, a primeira escrita do instalador (que acontece logo depois de o
    // rastreio comecar) passaria despercebida.
    private const int MargemDeAterrissagem = 250;

    private readonly SafeFileHandle _handle;
    private readonly Thread _thread;
    private readonly ManualResetEvent _pronto = new(false);
    private readonly IntPtr _bufferDados;
    private readonly TraceProcessPolicy _politica;
    private readonly Action<TraceEntry>? _aoMudar;
    private readonly List<TraceEntry> _entradas = new();
    private readonly Dictionary<string, long> _tamanhosConhecidos = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _trava = new();

    // A fotografia da pasta tirada no instante em que a thread comeca: e o
    // estado do sistema antes de o watcher existir, e o que permite
    // reconciliar a janela de aterrissagem.
    private Dictionary<string, long> _fotoInicial = new(StringComparer.OrdinalIgnoreCase);

    private string _ultimoErro = string.Empty;
    private string? _nomeAntigoPendente;
    private string? _caminhoAntigoPendente;
    private volatile bool _rodando = true;
    private bool _descartado;

    private WriteWatcher(
        string diretorio,
        SafeFileHandle handle,
        TraceProcessPolicy politica,
        Action<TraceEntry>? aoMudar)
    {
        _handle = handle;
        _politica = politica;
        _aoMudar = aoMudar;
        Diretorio = diretorio;

        // O buffer em memoria nativa: a leitura bloqueia a thread e o coletor
        // nao pode mover o buffer no meio da operacao.
        _bufferDados = Marshal.AllocHGlobal(TamanhoDoBuffer);

        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "tranal-rastreio-escrita",
        };
        _thread.Start();
    }

    /// <summary>
    /// A pasta observada.
    /// </summary>
    public string Diretorio { get; }

    /// <summary>
    /// A politica que decide quais processos sao observados.
    /// </summary>
    public TraceProcessPolicy Politica => _politica;

    /// <summary>
    /// True enquanto o watcher estiver vivo.
    /// </summary>
    public bool EstaRodando => _rodando;

    /// <summary>
    /// A ultima falha registrada pelo watcher (string vazia quando nunca
    /// falhou). A falha nunca e propagada: ela fica aqui.
    /// </summary>
    public string UltimoErro
    {
        get { lock (_trava) return _ultimoErro; }
    }

    /// <summary>
    /// As entradas observadas ate agora, na ordem de chegada.
    /// </summary>
    public IReadOnlyList<TraceEntry> Entradas
    {
        get { lock (_trava) return _entradas.ToList(); }
    }

    /// <summary>
    /// Comeca a observar a pasta.
    ///
    /// Devolve <c>null</c> — sem excecao — quando a pasta nao existe, quando
    /// o Windows recusa abrir o handle, ou quando qualquer passo falha.
    /// </summary>
    /// <param name="diretorio">A pasta a observar.</param>
    /// <param name="politica">Politica de processos; quando nula, observa todos.</param>
    /// <param name="aoMudar">Chamado a cada entrada observada (na thread do watcher).</param>
    public static WriteWatcher? Iniciar(
        string? diretorio,
        TraceProcessPolicy? politica = null,
        Action<TraceEntry>? aoMudar = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(diretorio)) return null;

            var caminho = PathUtil.Normalize(diretorio);
            if (caminho.Length == 0) return null;

            // Pasta que nao existe (o %TEMP% apagado, por exemplo): falha
            // limpa. Nao ha handle, nao ha thread, nao ha excecao.
            if (!Directory.Exists(caminho)) return null;

            var pasta = caminho.EndsWith("\\", StringComparison.Ordinal) ? caminho : caminho + "\\";

            var handle = CriarArquivo(
                pasta,
                AcessoListarDiretorio,
                CompartilhamentoLeitura | CompartilhamentoEscrita | CompartilhamentoExclusao,
                IntPtr.Zero,
                AbrirExistente,
                FlagSemanticaDeBackup,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                handle.Dispose();
                return null;
            }

            var watcher = new WriteWatcher(
                caminho,
                handle,
                politica ?? TraceProcessPolicy.Default,
                aoMudar);

            watcher.AguardarAterrissagem();

            return watcher;
        }
        catch
        {
            // Falha ao montar o watcher nao pode derrubar quem chamou.
            return null;
        }
    }

    /// <summary>
    /// Espera a thread de observacao se anunciar e da uma folga curta para
    /// que a leitura esteja de fato armada quando o <see cref="Iniciar"/>
    /// devolve. Sem essa espera, a primeira escrita do instalador — que
    /// acontece logo depois de o rastreio comecar — passaria despercebida.
    /// </summary>
    /// <summary>
    /// Compara a pasta agora com a fotografia inicial e emite o que mudou
    /// durante a janela de aterrissagem.
    /// </summary>
    private void Reconciliar()
    {
        try
        {
            var agora = Fotografar(Diretorio);

            foreach (var par in agora)
            {
                var conhece = _tamanhosConhecidos.TryGetValue(par.Key, out var tamanhoConhecido);

                if (!conhece)
                {
                    // Nasceu dentro da janela.
                    Registrar(TraceChangeKind.Created, par.Key, null, null);
                    continue;
                }

                if (tamanhoConhecido == par.Value) continue;

                // Mudou de tamanho dentro da janela.
                Registrar(TraceChangeKind.Overwritten, par.Key, null, null);
            }

            foreach (var par in _fotoInicial)
            {
                if (agora.ContainsKey(par.Key)) continue;
                if (!_tamanhosConhecidos.ContainsKey(par.Key)) continue;

                Registrar(TraceChangeKind.Deleted, par.Key, null, null);
            }
        }
        catch (Exception ex)
        {
            // A reconciliacao e uma rede de seguranca: se ela falhar, o
            // watcher segue funcionando pelos eventos.
            RegistrarErro(ex.Message);
        }
    }

    /// <summary>
    /// A fotografia da pasta: os arquivos que existem e o tamanho de cada um.
    /// </summary>
    private static Dictionary<string, long> Fotografar(string pasta)
    {
        var foto = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (!Directory.Exists(pasta)) return foto;

            foreach (var arquivo in Directory.EnumerateFiles(pasta, "*", SearchOption.AllDirectories))
            {
                try
                {
                    foto[arquivo] = new FileInfo(arquivo).Length;
                }
                catch
                {
                    // O arquivo sumiu no meio da listagem: ele simplesmente
                    // nao entra na fotografia.
                }
            }
        }
        catch
        {
            // Pasta inacessivel: a fotografia fica vazia e a reconciliacao
            // nao acha nada — o watcher segue funcionando pelos eventos.
        }

        return foto;
    }

    private void AguardarAterrissagem()
    {
        try
        {
            _pronto.WaitOne(2000);
            Thread.Sleep(MargemDeAterrissagem);

            // A reconciliacao cobre a janela entre a fotografia inicial e a
            // leitura armada: o que mudou nesse intervalo teria sido
            // perdido, porque a leitura so avisa o que acontece depois de
            // armada.
            Reconciliar();
        }
        catch
        {
            // A espera e uma garantia de pontualidade, nao uma condicao de
            // funcionamento: se ela falhar, o watcher segue observing.
        }
    }

    /// <summary>
    /// Os processos em execucao que a politica manda observar.
    /// </summary>
    public IReadOnlyList<TraceProcessInfo> ProcessosObservados() => _politica.ObservedProcesses();

    /// <summary>
    /// Encerra o watcher e devolve o handle do sistema. Pode ser chamado
    /// mais de uma vez.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (_descartado) return;
            _descartado = true;

            _rodando = false;

            // Acorda a leitura pendente: a thread volta da chamada com
            // ERROR_OPERATION_ABORTED e encerra.
            try { CancelarIo(_handle, IntPtr.Zero); }
            catch { /* ja cancelado */ }

            var terminou = false;
            try { terminou = _thread.Join(3000); }
            catch { /* a thread ja morreu */ }

            if (!terminou)
            {
                // A thread ainda esta usando o handle e o buffer: fechar
                // agora seria corrupcao de memoria. O handle fica aberto e o
                // motivo fica registrado.
                RegistrarErro("A thread de observacao nao terminou a tempo; o handle foi mantido aberto.");
                return;
            }

            _handle.Dispose();
            Marshal.FreeHGlobal(_bufferDados);
            _pronto.Dispose();
        }
        catch
        {
            // Descartar nunca lanca.
        }
    }

    // ---------------------------------------------------------------------
    // A thread de observacao
    // ---------------------------------------------------------------------

    /// <summary>
    /// O laco que espera as mudancas da pasta e as transforma em entradas.
    /// </summary>
    private void Loop()
    {
        try
        {
            while (_rodando)
            {
                // A thread se anuncia antes de chamar a API do Windows: o
                // Iniciar usa isso para nao devolver antes de a leitura
                // estar armada.
                _pronto.Set();

                // A fotografia inicial e o estado "conhecido" do watcher:
                // sem ela, um arquivo que ja existia seria classificado como
                // criado quando o instalador apenas o sobrescreve.
                _fotoInicial = Fotografar(Diretorio);
                foreach (var par in _fotoInicial) _tamanhosConhecidos[par.Key] = par.Value;

                int bytes;
                var ok = LerMudancasDoDiretorio(
                    _handle,
                    _bufferDados,
                    TamanhoDoBuffer,
                    true,
                    FiltroDeNotificacao,
                    out bytes,
                    IntPtr.Zero,
                    IntPtr.Zero);

                if (!ok)
                {
                    var codigo = Marshal.GetLastWin32Error();

                    // Cancelamento pedido no Dispose: fim normal.
                    if (codigo == ErroOperacaoAbortada) break;

                    // O buffer do sistema transbordou: alguns eventos se
                    // perderam. E honesto registrar isso e seguir.
                    if (codigo == ErroBufferCheio)
                    {
                        RegistrarErro("O buffer de mudancas do sistema transbordou: parte dos eventos se perdeu.");
                        continue;
                    }

                    RegistrarErro($"A leitura de mudancas da pasta falhou (codigo {codigo}).");
                    break;
                }

                if (bytes > 0) ProcessarBuffer(_bufferDados, bytes);
            }
        }
        catch (Exception ex)
        {
            // A falha do watcher NUNCA chega a quem chamou: ela fica
            // registrada e a thread morre sem alarde.
            RegistrarErro(ex.Message);
        }
        finally
        {
            _rodando = false;
        }
    }

    /// <summary>
    /// Le o buffer devolvido pelo Windows e traduz as acoes em entradas.
    /// </summary>
    private void ProcessarBuffer(IntPtr buffer, int tamanho)
    {
        var posicao = 0;

        while (posicao + CabecalhoDaEntrada <= tamanho)
        {
            var proximo = Marshal.ReadInt32(buffer, posicao);
            var acao = (uint)Marshal.ReadInt32(buffer, posicao + 4);
            var tamanhoDoNome = (uint)Marshal.ReadInt32(buffer, posicao + 8);

            var inicioDoNome = posicao + CabecalhoDaEntrada;
            if (tamanhoDoNome > 0 && inicioDoNome + (int)tamanhoDoNome <= tamanho)
            {
                // O nome vem em UTF-16 e o Windows informa o tamanho em
                // bytes; sem o limite, a leitura iria ate o proximo zero do
                // buffer e traria lixo junto do nome.
                var nome = Marshal.PtrToStringUni(buffer + inicioDoNome, (int)(tamanhoDoNome / 2)) ?? string.Empty;
                ProcessarAcao(acao, nome);
            }

            if (proximo == 0) break;
            posicao += proximo;
        }
    }

    /// <summary>
    /// Traduz uma acao do Windows em entrada de log.
    /// </summary>
    private void ProcessarAcao(uint acao, string nome)
    {
        try
        {
            var caminho = Path.Combine(Diretorio, nome);

            switch (acao)
            {
                case AcaoAdicionado:
                    Registrar(TraceChangeKind.Created, caminho, null, null);
                    break;

                case AcaoModificado:
                    // Um arquivo recem-criado gera "adicionado" e depois
                    // "modificado": o tipo final depende do que ja se sabia
                    // do caminho (decidido em Registrar).
                    Registrar(TraceChangeKind.Overwritten, caminho, null, null);
                    break;

                case AcaoRemovido:
                    Registrar(TraceChangeKind.Deleted, caminho, null, null);
                    break;

                case AcaoRenomeadoAntigo:
                    _nomeAntigoPendente = nome;
                    _caminhoAntigoPendente = caminho;
                    break;

                case AcaoRenomeadoNovo:
                    var nomeAntigo = _nomeAntigoPendente;
                    var caminhoAntigo = _caminhoAntigoPendente;
                    _nomeAntigoPendente = null;
                    _caminhoAntigoPendente = null;
                    Registrar(TraceChangeKind.Renamed, caminho, nomeAntigo, caminhoAntigo);
                    break;
            }
        }
        catch (Exception ex)
        {
            RegistrarErro(ex.Message);
        }
    }

    /// <summary>
    /// Cria a entrada e a entrega ao ouvinte.
    /// </summary>
    private void Registrar(TraceChangeKind tipo, string caminho, string? nomeAntigo, string? caminhoAntigo)
    {
        try
        {
            var ehPasta = Directory.Exists(caminho);

            long? tamanho = null;
            if (!ehPasta)
            {
                try { tamanho = new FileInfo(caminho).Length; }
                catch { /* o arquivo sumiu no meio da leitura */ }
            }

            var conhece = _tamanhosConhecidos.TryGetValue(caminho, out var tamanhoAnterior);

            // O tipo final depende do que ja se sabia do caminho: um arquivo
            // novo que o Windows notifica como "modificado" (logo depois de
            // criado) e uma criacao; um arquivo conhecido e sobrescrito.
            var tipoFinal = tipo;
            if (tipoFinal == TraceChangeKind.Overwritten && !conhece) tipoFinal = TraceChangeKind.Created;
            if (tipoFinal == TraceChangeKind.Created && conhece) tipoFinal = TraceChangeKind.Overwritten;

            string? valorAntigo = null;
            string? valorNovo = null;

            switch (tipoFinal)
            {
                case TraceChangeKind.Created:
                    valorNovo = FormatarTamanho(tamanho);
                    break;

                case TraceChangeKind.Overwritten:
                    valorAntigo = FormatarTamanho(tamanhoAnterior);
                    valorNovo = FormatarTamanho(tamanho);
                    break;

                case TraceChangeKind.Deleted:
                    valorAntigo = conhece ? FormatarTamanho(tamanhoAnterior) : null;
                    break;

                case TraceChangeKind.Renamed:
                    valorAntigo = caminhoAntigo;
                    valorNovo = caminho;
                    break;
            }

            // Renomeado: o caminho novo herda o tamanho conhecido do antigo.
            if (tipoFinal == TraceChangeKind.Renamed && caminhoAntigo is not null)
            {
                if (_tamanhosConhecidos.TryGetValue(caminhoAntigo, out var tamanhoDoAntigo))
                {
                    _tamanhosConhecidos[caminho] = tamanhoDoAntigo;
                    tamanho = tamanhoDoAntigo;
                }
            }

            if (tipoFinal == TraceChangeKind.Deleted) _tamanhosConhecidos.Remove(caminho);
            else if (tamanho.HasValue) _tamanhosConhecidos[caminho] = tamanho.Value;

            var entrada = new TraceEntry
            {
                Path = caminho,
                Kind = tipoFinal,
                OldName = nomeAntigo,
                OldValue = valorAntigo,
                NewValue = valorNovo,
                Size = tamanho,
                Timestamp = DateTimeOffset.UtcNow,
                OwnerProcess = null,
                IsDirectory = ehPasta,
            };

            lock (_trava) { _entradas.Add(entrada); }

            try { _aoMudar?.Invoke(entrada); }
            catch { /* a excecao do ouvinte nao derruba o watcher */ }
        }
        catch (Exception ex)
        {
            RegistrarErro(ex.Message);
        }
    }

    /// <summary>
    /// Registra a ultima falha do watcher.
    /// </summary>
    private void RegistrarErro(string mensagem)
    {
        lock (_trava) { _ultimoErro = mensagem; }
    }

    /// <summary>
    /// O tamanho como texto (null quando nao ha tamanho).
    /// </summary>
    private static string? FormatarTamanho(long? tamanho) =>
        tamanho.HasValue ? tamanho.Value.ToString(CultureInfo.InvariantCulture) : null;

    // ---------------------------------------------------------------------
    // A API do Windows
    // ---------------------------------------------------------------------

    /// <summary>
    /// Abre a pasta como handle (o que <c>ReadDirectoryChangesW</c> consome).
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateFileW")]
    private static extern SafeFileHandle CriarArquivo(
        string nomeDoArquivo,
        uint acessoDesejado,
        uint modoDeCompartilhamento,
        IntPtr atributosDeSeguranca,
        uint disposicaoDeCriacao,
        uint bandeirasEAtributos,
        IntPtr modeloDeArquivo);

    /// <summary>
    /// Le as mudancas da pasta. A chamada bloqueia a thread ate haver
    /// mudanca (ou ate o <c>CancelIoEx</c> interromp-la).
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "ReadDirectoryChangesW")]
    private static extern bool LerMudancasDoDiretorio(
        SafeFileHandle handleDoDiretorio,
        IntPtr buffer,
        int tamanhoDoBuffer,
        bool observarSubpastas,
        uint filtroDeNotificacao,
        out int bytesRetornados,
        IntPtr sobreposto,
        IntPtr rotinaDeConclusao);

    /// <summary>
    /// Cancela as leituras pendentes do handle — e o que acorda a thread de
    /// observacao no <see cref="Dispose"/>.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "CancelIoEx")]
    private static extern bool CancelarIo(SafeFileHandle handleDoArquivo, IntPtr sobreposto);
}
