using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// Onde o Windows procura um executavel, e se ele esta instalado.
///
/// A REGRA mora aqui, em um lugar so. O limpador de registro, o verificador de
/// atualizacoes e o contador de referencia precisam todos da mesma resposta
/// para a pergunta "esse executavel existe?" — e a resposta tem tres partes:
///
///   1. o caminho absoluto, quando existe no disco;
///   2. o caminho SEM extensao, porque o Windows aceita "perfmon" para
///      "perfmon.exe";
///   3. o nome sem caminho, resolvido pelo PATH do sistema e pela tabela
///      "App Paths" do proprio Windows.
///
/// A terceira parte e a que impede falsos positivos: o registro do
/// Image File Execution Options guarda "excel.exe", e o Excel nao esta no
/// PATH — ele esta em "C:\Program Files\Microsoft Office\...". Sem a tabela
/// "App Paths" e sem a lista de programas instalados, o Excel pareceria um
/// programa removido.
/// </summary>
public static class ExecutableLocator
{
    private static readonly object Sincronia = new();
    private static IndiceInstalado? _indice;
    private static DateTime _marcador;

    /// <summary>
    /// True quando o executavel existe no disco, resolvido pelas tres
    /// regras acima.
    ///
    /// </summary>
    /// <param name="pathOrName">O caminho ou o nome do executavel.</param>
    public static bool Exists(string? pathOrName)
    {
        var texto = Normalizar(pathOrName);
        if (texto.Length == 0) return false;

        if (texto.Contains('\\') || texto.Contains('/'))
        {
            if (File.Exists(texto)) return true;
            if (Path.GetExtension(texto).Length == 0 && File.Exists(texto + ".exe")) return true;
            return false;
        }

        foreach (var pasta in PastasDoSistema())
        {
            try
            {
                var completa = Path.Combine(pasta, texto);
                if (File.Exists(completa)) return true;
                if (Path.GetExtension(completa).Length == 0 && File.Exists(completa + ".exe")) return true;
            }
            catch
            {
                // Pasta invalida: segue para a proxima.
            }
        }

        var peloAppPaths = ResolverPorAppPaths(texto);
        return peloAppPaths is not null && ExisteComExtensao(peloAppPaths);
    }

    /// <summary>
    /// True quando um programa INSTALADO usa esse executavel.
    ///
    /// E a segunda metade da regra: um programa que esta instalado pode ter o
    /// executavel fora do PATH e fora da tabela "App Paths". O desinstalador
    /// dele e o lugar onde o Windows escreveu o caminho — e ele e a prova de
    /// que o programa existe.
    ///
    /// A busca cobre as duas formas: o executavel e o proprio desinstalador
    /// ("EXCEL.EXE"), ou e um arquivo dentro da pasta de instalacao do
    /// programa ("excelcnv.exe", que esta em "...\Microsoft Office\root\Office16").
    ///
    /// </summary>
    /// <param name="exeName">O nome do executavel (com ou sem caminho).</param>
    public static bool IsInstalled(string? exeName)
    {
        var nome = Normalizar(exeName);
        if (nome.Length == 0) return false;

        var alvo = Path.GetFileName(nome);
        if (alvo.Length == 0) return false;

        var indice = ObterIndice();

        if (indice.Nomes.Contains(alvo)) return true;

        return indice.NomesNasPastas.ContainsKey(alvo);
    }

    /// <summary>
    /// Limpa o cache do indice de programas instalados. Existe para os testes.
    /// </summary>
    public static void ResetCache()
    {
        lock (Sincronia)
        {
            _indice = null;
            _marcador = DateTime.MinValue;
        }
    }

    /// <summary>
    /// O indice dos programas instalados, reconstruido a cada cinco minutos.
    /// </summary>
    private static IndiceInstalado ObterIndice()
    {
        lock (Sincronia)
        {
            if (_indice is null || (DateTime.UtcNow - _marcador).TotalMinutes > 5)
            {
                _indice = LerIndice();
                _marcador = DateTime.UtcNow;
            }

            return _indice;
        }
    }

    /// <summary>
    /// Os nomes de arquivo dos executaveis de desinstalacao e as pastas de
    /// instalacao dos programas instalados.
    /// </summary>
    private static IndiceInstalado LerIndice()
    {
        var indice = new IndiceInstalado();

        try
        {
            var programas = Core.ProgramScanner.Scan(new Core.ProgramScanner.ScanOptions
            {
                IncludeSystemComponents = true,
                IncludeMsiProducts = false,
                IncludeEntriesWithoutUninstaller = true,
            });

            foreach (var programa in programas)
            {
                foreach (var comando in new[] { programa.UninstallString, programa.QuietUninstallString, programa.DisplayIcon })
                {
                    var exe = ExtrairExecutavel(comando);
                    if (exe.Length == 0) continue;

                    var arquivo = Path.GetFileName(exe);
                    if (arquivo.Length == 0) continue;

                    indice.Nomes.Add(arquivo);

                    var pasta = Path.GetDirectoryName(exe);
                    if (!string.IsNullOrEmpty(pasta)) indice.Pastas.Add(pasta);
                }

                var local = (programa.InstallLocation ?? string.Empty).Trim().Trim('"');
                if (local.Length > 0) indice.Pastas.Add(local);
            }
        }
        catch (Exception ex)
        {
            Core.Tools.ToolLog.Error($"Falha ao ler a lista de programas instalados: {ex.Message}");
        }

        indice.Montar();

        return indice;
    }

    /// <summary>
    /// As pastas onde o Windows procura um executavel sem caminho.
    /// </summary>
    private static IEnumerable<string> PastasDoSistema()
    {
        var pastas = new List<string>();

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        pastas.AddRange(path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length > 0)
            .Select(p => p.Trim()));

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (windows.Length > 0)
        {
            pastas.Add(Path.Combine(windows, "System32"));
            pastas.Add(Path.Combine(windows, "SysWOW64"));
            pastas.Add(windows);

            // O .NET Framework guarda executaveis do sistema fora do
            // System32 ("mscorsvw.exe", "ngen.exe"). Sem estas pastas, o
            // Windows inteiro pareceria ter programas removidos.
            foreach (var versao in new[] { "v4.0.30319", "v3.5", "v2.0.50727" })
            {
                pastas.Add(Path.Combine(windows, "Microsoft.NET", "Framework64", versao));
                pastas.Add(Path.Combine(windows, "Microsoft.NET", "Framework", versao));
            }
        }

        return pastas;
    }

    /// <summary>
    /// Procura o executavel na tabela "App Paths" do Windows.
    ///
    /// A tabela e o proprio mecanismo do Windows para resolver um nome de
    /// executavel: e o que faz "Iniciar &gt; Executar" achar o Excel.
    /// </summary>
    private static string? ResolverPorAppPaths(string nome)
    {
        const string relativo = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";

        var fontes = new[]
        {
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32),
            (RegistryHive.CurrentUser, RegistryView.Registry64),
        };

        foreach (var (hive, view) in fontes)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var chave = baseKey.OpenSubKey(relativo + "\\" + nome);
                if (chave is null) continue;

                var valor = chave.GetValue(string.Empty) as string;
                if (string.IsNullOrWhiteSpace(valor)) continue;

                return valor.Trim().Trim('"');
            }
            catch
            {
                // Chave inacessivel: segue para a proxima fonte.
            }
        }

        return null;
    }

    /// <summary>
    /// True quando o caminho existe, aceitando o caminho sem extensao.
    /// </summary>
    private static bool ExisteComExtensao(string caminho)
    {
        if (caminho.Length == 0) return false;

        if (File.Exists(caminho)) return true;

        return Path.GetExtension(caminho).Length == 0 && File.Exists(caminho + ".exe");
    }

    /// <summary>
    /// Extrai o executavel de uma linha de comando.
    /// </summary>
    private static string ExtrairExecutavel(string? comando)
    {
        var texto = (comando ?? string.Empty).Trim();
        if (texto.Length == 0) return string.Empty;

        if (texto.StartsWith("\"", StringComparison.Ordinal))
        {
            var fim = texto.IndexOf('"', 1);
            return fim > 1 ? texto[1..fim] : texto.Trim('"');
        }

        var minusculo = texto.ToLowerInvariant();
        foreach (var extensao in new[] { ".exe", ".com", ".bat", ".cmd" })
        {
            var indice = minusculo.IndexOf(extensao, StringComparison.Ordinal);
            if (indice > 0) return texto[..(indice + extensao.Length)];
        }

        var espaco = texto.IndexOf(' ');
        return espaco > 0 ? texto[..espaco] : texto;
    }

    /// <summary>
    /// Normaliza o texto: expande as variaveis de ambiente e tira as aspas.
    /// </summary>
    private static string Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;

        var texto = valor.Trim().Trim('"').Trim();
        if (texto.Length == 0) return string.Empty;

        try
        {
            texto = Environment.ExpandEnvironmentVariables(texto);
        }
        catch
        {
            // Variavel invalida: fica como esta.
        }

        return texto.Trim();
    }

    /// <summary>
    /// O indice dos programas instalados: os nomes dos executaveis de
    /// desinstalacao e as pastas onde os programas estao instalados.
    /// </summary>
    private sealed class IndiceInstalado
    {
        /// <summary>Quantas subpastas a busca percorre dentro de cada pasta.</summary>
        private const int ProfundidadeMaxima = 3;

        /// <summary>
        /// Quantos arquivos a busca visita no total, em todas as pastas.
        ///
        /// O orcamento existe porque a soma das pastas de instalacao tem
        /// centenas de milhares de arquivos, e a varredura inteira do disco
        /// nao pode ser o preco de uma limpeza de registro. Acabado o
        /// orcamento, o indice fica com o que deu para ver — e a regra
        /// continua valendo: um arquivo que nao foi visto nao prova que o
        /// programa sumiu.
        /// </summary>
        private const int ArquivosMaximos = 20000;

        /// <summary>
        /// Quanto tempo a busca pode gastar, no maximo.
        ///
        /// O tempo e o limite que o usuario sente; a contagem de arquivos e o
        /// limite que o disco sente. Os dois valem.
        /// </summary>
        private static readonly TimeSpan TempoMaximo = TimeSpan.FromSeconds(4);

        /// <summary>Os nomes de arquivo dos executaveis de desinstalacao.</summary>
        public HashSet<string> Nomes { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>As pastas de instalacao dos programas.</summary>
        public List<string> Pastas { get; } = new();

        /// <summary>
        /// Os nomes de todos os executaveis encontrados dentro das pastas de
        /// instalacao.
        ///
        /// E um conjunto concorrente porque a varredura das pastas roda em
        /// paralelo.
        /// </summary>
        public ConcurrentDictionary<string, byte> NomesNasPastas { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        private int _orcamento;

        /// <summary>
        /// Monta o indice: os nomes dos desinstaladores e os executaveis das
        /// pastas de instalacao.
        ///
        /// A varredura das pastas roda em PARALELO: a soma das pastas de
        /// instalacao tem centenas de milhares de arquivos, e em serie a
        /// leitura levava mais de 30 segundos. Em paralelo, cabe no tempo de
        /// uma varredura de registro.
        /// </summary>
        public void Montar()
        {
            _orcamento = ArquivosMaximos;
            var inicio = DateTime.UtcNow;

            Parallel.ForEach(Pastas, pasta =>
            {
                if (Voltar_Orcamento() <= 0) return;
                if (DateTime.UtcNow - inicio > TempoMaximo) return;

                Varrer(pasta, ProfundidadeMaxima, inicio);
            });
        }

        /// <summary>Le o orcamento restante de forma segura entre as threads.</summary>
        private int Voltar_Orcamento() => Volatile.Read(ref _orcamento);

        /// <summary>
        /// Percorre a pasta guardando os nomes dos executaveis.
        /// </summary>
        private void Varrer(string pasta, int profundidade, DateTime inicio)
        {
            if (profundidade < 0) return;

            try
            {
                if (!Directory.Exists(pasta)) return;

                foreach (var arquivo in Directory.EnumerateFiles(pasta, "*.exe"))
                {
                    if (Interlocked.Decrement(ref _orcamento) <= 0) return;

                    var nome = Path.GetFileName(arquivo);
                    if (nome.Length == 0) continue;

                    NomesNasPastas.TryAdd(nome, 0);
                }

                foreach (var sub in Directory.EnumerateDirectories(pasta))
                {
                    if (Voltar_Orcamento() <= 0) return;
                    if (DateTime.UtcNow - inicio > TempoMaximo) return;

                    Varrer(sub, profundidade - 1, inicio);
                }
            }
            catch
            {
                // Pasta inacessivel: segue para a proxima.
            }
        }
    }
}
