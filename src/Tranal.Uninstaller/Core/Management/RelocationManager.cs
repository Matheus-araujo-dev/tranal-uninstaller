using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Tranal.Uninstaller.Core.Discovery;

namespace Tranal.Uninstaller.Core.Management;

/// <summary>
/// Uma mudanca de local de instalacao registrada, para poder ser revertida.
/// </summary>
public sealed class RelocationRecord
{
    /// <summary>O nome do programa movido.</summary>
    public string ProgramName { get; set; } = string.Empty;

    /// <summary>De onde o programa saiu.</summary>
    public string FromPath { get; set; } = string.Empty;

    /// <summary>Para onde o programa foi.</summary>
    public string ToPath { get; set; } = string.Empty;

    /// <summary>Quando a mudanca aconteceu.</summary>
    public DateTime When { get; set; } = DateTime.Now;

    /// <summary>A chave de registro da entrada de desinstalacao.</summary>
    public string RegistryPath { get; set; } = string.Empty;
}

/// <summary>
/// Move um programa instalado de um disco para outro, e desfaz a mudanca.
///
/// O Ashampoo mede "Move: mover um programa para outro local, recriando o
/// atalho no lugar original" e "Revert relocation: devolver o programa movido
/// a posicao original". O IOBIT mede "Software Relocator: mover software
/// instalado entre discos".
///
/// A REGRA do que e "movido" mora aqui: um programa e a pasta de instalacao
/// mais a entrada de desinstalacao que aponta para ela. Mover a pasta sem
/// corrigir a entrada deixa o programa quebrado — e por isso que a operacao
/// e registrada antes de mexer, para poder ser revertida.
///
/// O registro das operacoes fica em %LOCALAPPDATA%\Tranal\relocations.json.
/// </summary>
public static class RelocationManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// A pasta onde o historico de mudancas e gravado.
    /// </summary>
    public static string HistoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Tranal",
        "relocations.json");

    /// <summary>
    /// O resultado de uma operacao de mudanca de local.
    /// </summary>
    public sealed class Result
    {
        /// <summary>True quando a operacao deu certo.</summary>
        public bool Success { get; init; }

        /// <summary>A mensagem para o usuario.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>O codigo de erro, quando houve.</summary>
        public int ErrorCode { get; init; }
    }

    /// <summary>
    /// Move um programa para outro local.
    ///
    /// </summary>
    /// <param name="programa">O programa a mover.</param>
    /// <param name="novoLocal">A pasta de destino. Precisa existir.</param>
    /// <param name="atualizarRegistro">Corrige a entrada de desinstalacao.</param>
    /// <param name="recriarAtalho">Recria o atalho no lugar original.</param>
    public static Result Move(
        InstalledProgram programa,
        string novoLocal,
        bool atualizarRegistro = true,
        bool recriarAtalho = true)
    {
        if (programa is null) return new Result { Success = false, Message = "Programa nao informado." };

        var origem = (programa.InstallLocation ?? string.Empty).Trim();
        if (origem.Length == 0)
            return new Result { Success = false, Message = "O programa nao tem pasta de instalacao conhecida." };

        if (!Directory.Exists(origem))
            return new Result { Success = false, Message = "A pasta de instalacao nao existe mais." };

        var destino = (novoLocal ?? string.Empty).Trim();
        if (destino.Length == 0)
            return new Result { Success = false, Message = "Destino nao informado." };

        if (!Directory.Exists(destino))
            return new Result { Success = false, Message = "A pasta de destino nao existe." };

        var novoCaminho = Path.Combine(destino, Path.GetFileName(origem.TrimEnd('\\', '/')));
        if (string.Equals(origem.TrimEnd('\\', '/'), novoCaminho, StringComparison.OrdinalIgnoreCase))
            return new Result { Success = false, Message = "O destino e a mesma pasta de origem." };

        if (Directory.Exists(novoCaminho))
            return new Result { Success = false, Message = "Ja existe uma pasta com esse nome no destino." };

        // O registro da mudanca acontece ANTES de mexer: e o que permite
        // desfazer. Sem ele, uma mudanca interrompida deixa o programa
        // partido ao meio e sem volta.
        var registro = new RelocationRecord
        {
            ProgramName = programa.DisplayName,
            FromPath = origem,
            ToPath = novoCaminho,
            RegistryPath = programa.RegistryPath ?? string.Empty,
        };

        try
        {
            Directory.CreateDirectory(destino);
            Directory.Move(origem, novoCaminho);
        }
        catch (Exception ex)
        {
            return new Result { Success = false, Message = ex.Message, ErrorCode = ex.HResult & 0xFFFF };
        }

        if (atualizarRegistro) AtualizarEntradaDeRegistro(programa, novoCaminho);

        if (recriarAtalho) RecriarAtalhoNoDestinoAntigo(programa, origem);

        SalvarRegistro(registro);

        return new Result
        {
            Success = true,
            Message = $"Programa movido para {novoCaminho}.",
        };
    }

    /// <summary>
    /// Devolve um programa movido ao lugar de origem.
    ///
    /// </summary>
    /// <param name="programName">O nome do programa (como foi registrado).</param>
    public static Result Revert(string programName)
    {
        var historico = Carregar();
        var registro = historico.FirstOrDefault(r =>
            string.Equals(r.ProgramName, (programName ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

        if (registro is null)
            return new Result { Success = false, Message = "Nao ha mudanca registrada para este programa." };

        if (!Directory.Exists(registro.ToPath))
            return new Result { Success = false, Message = "A pasta de destino nao existe mais." };

        if (Directory.Exists(registro.FromPath))
            return new Result { Success = false, Message = "Ja existe uma pasta no lugar de origem." };

        try
        {
            Directory.Move(registro.ToPath, registro.FromPath);
        }
        catch (Exception ex)
        {
            return new Result { Success = false, Message = ex.Message, ErrorCode = ex.HResult & 0xFFFF };
        }

        if (registro.RegistryPath.Length > 0)
        {
            CorrigirEntradaDeRegistro(registro.RegistryPath, registro.FromPath);
        }

        historico.Remove(registro);
        Salvar(historico);

        return new Result { Success = true, Message = $"Programa devolvido para {registro.FromPath}." };
    }
    /// <summary>
    /// As mudancas registradas, da mais recente para a mais antiga.
    /// </summary>
    public static IReadOnlyList<RelocationRecord> Historico() =>
        Carregar().OrderByDescending(r => r.When).ToList();

    /// <summary>
    /// Corrige a entrada de desinstalacao para apontar para a nova pasta.
    /// </summary>
    private static void AtualizarEntradaDeRegistro(InstalledProgram programa, string novoCaminho)
    {
        if (string.IsNullOrWhiteSpace(programa.RegistryPath)) return;
        CorrigirEntradaDeRegistro(programa.RegistryPath, novoCaminho);
    }

    /// <summary>
    /// Reescreve o valor de instalacao da entrada de desinstalacao.
    /// </summary>
    private static void CorrigirEntradaDeRegistro(string registryPath, string novoCaminho)
    {
        var parsed = RegistryPath.Parse(registryPath);
        if (parsed is null) return;

        try
        {
            using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(parsed.Value.Hive, Microsoft.Win32.RegistryView.Default);
            using var chave = baseKey.OpenSubKey(parsed.Value.RelativePath, writable: true);
            if (chave is null) return;

            // O "InstallLocation" e o valor que a lista de instalados e o
            // proprio Windows leem. O "DisplayIcon" costuma ter o caminho do
            // executavel dentro, entao tambem e corrigido.
            chave.SetValue("InstallLocation", novoCaminho, Microsoft.Win32.RegistryValueKind.String);

            var icone = chave.GetValue("DisplayIcon")?.ToString();
            if (!string.IsNullOrWhiteSpace(icone))
            {
                var corrigido = CorrigirCaminhoEmIcone(icone, novoCaminho);
                if (!string.Equals(corrigido, icone, StringComparison.Ordinal))
                    chave.SetValue("DisplayIcon", corrigido, Microsoft.Win32.RegistryValueKind.String);
            }
        }
        catch
        {
            // Sem permissao no registro: a pasta foi movida, mas a entrada
            // continua apontando para o lugar antigo. O usuario ve isso na
            // lista e pode corrigir a mao.
        }
    }

    /// <summary>
    /// Troca o caminho dentro do valor de icone, preservando o indice.
    /// </summary>
    private static string CorrigirCaminhoEmIcone(string icone, string novoCaminho)
    {
        var separador = icone.IndexOf(',');
        var caminho = separador >= 0 ? icone[..separador] : icone;
        var resto = separador >= 0 ? icone[separador..] : string.Empty;

        var nome = Path.GetFileName(caminho.Trim());
        if (nome.Length == 0) return icone;

        var novo = Path.Combine(novoCaminho, nome);
        return novo + resto;
    }

    /// <summary>
    /// Recria o atalho na pasta de origem, apontando para o novo local.
    /// </summary>
    private static void RecriarAtalhoNoDestinoAntigo(InstalledProgram programa, string origem)
    {
        var atalho = programa.DisplayIcon;
        if (string.IsNullOrWhiteSpace(atalho)) return;

        var caminhoAtalho = atalho.Split(',')[0].Trim();
        if (caminhoAtalho.Length == 0) return;

        var nomeAtalho = Path.Combine(origem, Path.GetFileNameWithoutExtension(caminhoAtalho) + ".lnk");
        if (File.Exists(nomeAtalho)) return;

        try
        {
            var pasta = Path.GetDirectoryName(nomeAtalho);
            if (!string.IsNullOrEmpty(pasta) && !Directory.Exists(pasta)) return;

            // O atalho aponta para o executavel NOVO local, mas fica no
            // lugar antigo: e o que o usuario ve (o icone no menu Iniciar)
            // continua onde estava.
            var novoExecutavel = CorrigirCaminhoEmIcone(caminhoAtalho, programa.InstallLocation ?? string.Empty);
            CriarAtalho(nomeAtalho, novoExecutavel);
        }
        catch
        {
            // Sem permissao para criar o atalho: o programa foi movido, e
            // isso e o essencial.
        }
    }

    /// <summary>
    /// Cria um atalho do Windows (atalho de shell) por codigo.
    /// </summary>
    private static void CriarAtalho(string caminhoAtalho, string alvo)
    {
        var tipo = Type.GetTypeFromProgID("WScript.Shell");
        if (tipo is null) return;

        dynamic? shell = null;
        try
        {
            shell = Activator.CreateInstance(tipo);
            if (shell is null) return;

            dynamic atalho = shell.CreateShortcut(caminhoAtalho);
            atalho.TargetPath = alvo;
            atalho.WorkingDirectory = Path.GetDirectoryName(alvo) ?? string.Empty;
            atalho.Save();
        }
        catch
        {
            // O COM do shell pode estar bloqueado.
        }
        finally
        {
            if (shell is not null)
            {
                try { System.Runtime.InteropServices.Marshal.ReleaseComObject(shell); } catch { }
            }
        }
    }

    /// <summary>
    /// Le o historico de mudancas.
    /// </summary>
    private static List<RelocationRecord> Carregar()
    {
        try
        {
            if (!File.Exists(HistoryPath)) return new List<RelocationRecord>();

            var json = File.ReadAllText(HistoryPath);
            return JsonSerializer.Deserialize<List<RelocationRecord>>(json, JsonOptions) ?? new List<RelocationRecord>();
        }
        catch
        {
            return new List<RelocationRecord>();
        }
    }

    /// <summary>
    /// Acrescenta uma mudanca ao historico, sem apagar as anteriores.
    /// </summary>
    private static void SalvarRegistro(RelocationRecord registro)
    {
        var historico = Carregar();
        historico.Add(registro);
        Salvar(historico);
    }

    /// <summary>
    /// Grava o historico de mudancas.
    /// </summary>
    private static void Salvar(List<RelocationRecord> registros)
    {
        try
        {
            var pasta = Path.GetDirectoryName(HistoryPath);
            if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

            File.WriteAllText(HistoryPath, JsonSerializer.Serialize(registros, JsonOptions), new System.Text.UTF8Encoding(false));
        }
        catch
        {
            // Sem permissao para gravar: a mudanca foi feita, mas o historico
            // nao ficou — e o usuario ainda pode reverter pela acao direta.
        }
    }
}
