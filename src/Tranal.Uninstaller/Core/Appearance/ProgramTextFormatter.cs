using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Core.Appearance;

/// <summary>
/// O formato do texto que vai para a area de transferencia.
/// </summary>
public enum ClipboardFormat
{
    /// <summary>Texto simples, uma linha por programa.</summary>
    PlainText,

    /// <summary>Colunas separadas por tabulacao, para colar numa planilha.</summary>
    TabSeparated,

    /// <summary>O comando de desinstalacao de cada programa.</summary>
    UninstallCommand,

    /// <summary>Todos os campos conhecidos, um por linha.</summary>
    FullDetails,
}

/// <summary>
/// O texto que vai para a area de transferencia.
///
/// O Revo Pro tem o comando "Copy to Clipboard" como comando geral do menu,
/// e o BCU tem uma copia "avancada" com varios formatos. Aqui os dois sao o
/// mesmo lugar: um unico formatador com quatro formatos.
///
/// A formatacao mora no nucleo (e testavel sem area de transferencia) e a
/// gravacao na area de transferencia e da GUI — a area de transferencia e
/// do Windows, e o nucleo nao depende dela.
/// </summary>
public static class ProgramTextFormatter
{
    /// <summary>
    /// O texto de um conjunto de programas, no formato pedido.
    ///
    /// </summary>
    /// <param name="programs">Os programas a formatar.</param>
    /// <param name="format">O formato do texto.</param>
    /// <param name="columns">As colunas do formato tabulado (as padrao do catalogo).</param>
    public static string Format(
        IEnumerable<InstalledProgram> programs,
        ClipboardFormat format,
        IReadOnlyList<ProgramColumn>? columns = null)
    {
        var list = programs?.ToList() ?? new List<InstalledProgram>();
        if (list.Count == 0) return string.Empty;

        return format switch
        {
            ClipboardFormat.TabSeparated => FormatTab(list, columns),
            ClipboardFormat.UninstallCommand => FormatCommands(list),
            ClipboardFormat.FullDetails => FormatDetails(list),
            _ => FormatPlain(list),
        };
    }

    /// <summary>O texto de um programa, no formato pedido.</summary>
    public static string Format(
        InstalledProgram program,
        ClipboardFormat format,
        IReadOnlyList<ProgramColumn>? columns = null)
    {
        if (program is null) return string.Empty;
        return Format(new[] { program }, format, columns);
    }

    /// <summary>
    /// Texto simples: nome, versao e empresa, um por linha.
    ///
    /// E o formato que serve para colar num e-mail ou num forum — onde o
    /// tabulador vira um espaco esquisito.
    /// </summary>
    private static string FormatPlain(List<InstalledProgram> programs)
    {
        var sb = new StringBuilder();
        foreach (var program in programs)
        {
            var parts = new List<string> { program.DisplayName };
            if (!string.IsNullOrWhiteSpace(program.DisplayVersion)) parts.Add(program.DisplayVersion);
            if (!string.IsNullOrWhiteSpace(program.Publisher)) parts.Add(program.Publisher);

            sb.AppendLine(string.Join(" — ", parts));
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Colunas separadas por tabulacao, com o cabecalho na primeira linha.
    ///
    /// O cabecalho e o que faz o texto colar direto numa planilha: sem ele, a
    /// primeira linha vira um dado sem nome.
    /// </summary>
    private static string FormatTab(List<InstalledProgram> programs, IReadOnlyList<ProgramColumn>? columns)
    {
        var sel = columns ?? Exporters.DefaultColumns;
        var anotacoes = ProgramCustomizationStore.PeekAll();
        var sb = new StringBuilder();

        sb.AppendLine(string.Join("\t", sel.Select(c => ProgramColumns.Info(c).ExportName)));

        foreach (var program in programs)
        {
            sb.AppendLine(string.Join("\t",
                sel.Select(c => Clean(ProgramColumns.Value(c, program, CustomizationOf(program, anotacoes))))));
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// O comando de desinstalacao de cada programa.
    ///
    /// E o formato que o usuario quer quando vai rodar a desinstalacao em
    /// outro computador, ou em um script.
    /// </summary>
    private static string FormatCommands(List<InstalledProgram> programs)
    {
        var sb = new StringBuilder();
        foreach (var program in programs)
        {
            var command = program.UninstallString;
            if (string.IsNullOrWhiteSpace(command)) command = program.QuietUninstallCommand;
            if (string.IsNullOrWhiteSpace(command))
            {
                sb.AppendLine($"{program.DisplayName}: (sem comando de desinstalação registrado)");
                continue;
            }

            sb.AppendLine($"{program.DisplayName}: {command}");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Todos os campos conhecidos, um por linha, com rotulo.
    ///
    /// E o formato de quem vai comparar dois maquinas: o campo vem com o
    /// nome, e nao na posicao.
    /// </summary>
    private static string FormatDetails(List<InstalledProgram> programs)
    {
        var sb = new StringBuilder();

        foreach (var program in programs)
        {
            sb.AppendLine(program.DisplayName);
            sb.AppendLine("=".PadRight(program.DisplayName.Length, '='));

            Add(sb, "Versão", program.DisplayVersion);
            Add(sb, "Empresa", program.Publisher);
            Add(sb, "Data de instalação", program.InstallDate);
            Add(sb, "Tipo de desinstalador", program.Kind.ToString());
            Add(sb, "Código do produto", program.ProductCode);
            Add(sb, "Local de instalação", program.InstallLocation);
            Add(sb, "Comando de desinstalação", program.UninstallString);
            Add(sb, "Comando silencioso", program.QuietUninstallCommand);
            Add(sb, "Chave do registro", program.RegistryKey);
            Add(sb, "Instalador", program.Installer);
            Add(sb, "Página inicial", program.UrlInfoAbout);
            Add(sb, "Comentários", program.Comments);
            Add(sb, "Tamanho", program.SizeText);
            Add(sb, "Bits", program.Bits);

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static void Add(StringBuilder sb, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        sb.AppendLine($"{label}: {value}");
    }

    /// <summary>
    /// Limpa um valor para o formato tabulado.
    ///
    /// Tabulacao e quebra de linha separam colunas: um valor com esses
    /// caracteres dentro quebraria a coluna silenciosamente.
    /// </summary>
    private static string Clean(string? value)
    {
        var text = value ?? string.Empty;
        return text.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
    }

    /// <summary>
    /// A anotacao do usuario sobre o programa, sem tocar no arquivo dela.
    /// </summary>
    private static ProgramCustomization? CustomizationOf(
        InstalledProgram program, IReadOnlyList<ProgramCustomization> anotacoes)
    {
        var key = ProgramCustomizationStore.KeyOf(program);
        foreach (var item in anotacoes)
        {
            if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) return item;
        }

        return null;
    }

    /// <summary>
    /// Os nomes dos formatos, para a ajuda e para o menu.
    /// </summary>
    public static string Describe(ClipboardFormat format) => format switch
    {
        ClipboardFormat.TabSeparated => "Colunas separadas por tabulação",
        ClipboardFormat.UninstallCommand => "Comandos de desinstalação",
        ClipboardFormat.FullDetails => "Todos os detalhes",
        _ => "Texto simples",
    };
}
