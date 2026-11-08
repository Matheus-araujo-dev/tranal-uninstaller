using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Tranal.Uninstaller.Core.Modules;

namespace Tranal.Uninstaller.Core.Reporting;

/// <summary>
/// Gera o script PowerShell que remove aplicativos da Store (MSIX).
///
/// O modulo "Windows Apps" do Revo remove o pacote; este writer existe para
/// o outro lado do recurso: exportar a operacao como script, para rodar em
/// outra maquina, em outra conta, ou depois de uma reinstalacao.
///
/// A regra mora aqui, em um lugar so: quem chama o writer passa os pacotes
/// ja escolhidos e grava o conteudo devolvido.
/// </summary>
public static class StoreAppScriptWriter
{
    /// <summary>
    /// Monta o script PowerShell que remove os aplicativos informados.
    ///
    /// O script e seguro por construcao:
    /// <list type="bullet">
    /// <item>o nome do pacote vai entre aspas simples, e a aspa simples
    /// dentro do nome e duplicada (e o escape do proprio PowerShell);</item>
    /// <item>a remocao e feita pelo <c>PackageFullName</c> quando ele existe
    /// (e o que identifica a versao exata) e pelo nome do pacote quando
    /// nao;</item>
    /// <item>o script nao apaga nada que nao esteja na lista — ele so
    /// remove o que foi escolhido.</item>
    /// </list>
    /// </summary>
    /// <param name="apps">Os aplicativos da Store a remover.</param>
    public static string Build(IEnumerable<WindowsApp>? apps)
    {
        var lista = (apps ?? Enumerable.Empty<WindowsApp>())
            .Where(a => a is not null)
            .ToList();

        var sb = new StringBuilder();

        sb.AppendLine("#Requires -Version 5.1");
        sb.AppendLine("# Script de remocao de aplicativos da Microsoft Store (MSIX).");
        sb.AppendLine("# Tranal Uninstaller Pro - gerado em "
            + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
        sb.AppendLine("# " + lista.Count + " aplicativo(s).");
        sb.AppendLine("#");
        sb.AppendLine("# Execute em um PowerShell com permissao de administrador.");
        sb.AppendLine();
        sb.AppendLine("$ErrorActionPreference = 'Continue'");
        sb.AppendLine();
        sb.AppendLine("$pacotes = @(");

        foreach (var app in lista)
        {
            sb.AppendLine("    '" + Escape(app.PackageFullName) + "'");
        }

        sb.AppendLine(")");
        sb.AppendLine();
        sb.AppendLine("foreach ($pacote in $pacotes) {");
        sb.AppendLine("    if ([string]::IsNullOrWhiteSpace($pacote)) { continue }");
        sb.AppendLine();
        sb.AppendLine("    Write-Host \"Procurando o pacote $pacote ...\"");
        sb.AppendLine();
        sb.AppendLine("    # O PackageFullName identifica a versao exata; o Name,");
        sb.AppendLine("    # sem versao, remove todas as versoes instaladas.");
        sb.AppendLine("    $app = Get-AppxPackage -Name $pacote -ErrorAction SilentlyContinue");
        sb.AppendLine();
        sb.AppendLine("    if (-not $app) {");
        sb.AppendLine("        $app = Get-AppxPackage | Where-Object { $_.PackageFullName -eq $pacote }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    if (-not $app) {");
        sb.AppendLine("        Write-Host \"  [ignorado] pacote nao encontrado: $pacote\" -ForegroundColor Yellow");
        sb.AppendLine("        continue");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    $alvo = @($app)");
        sb.AppendLine();
        sb.AppendLine("    foreach ($pacoteAtual in $alvo) {");
        sb.AppendLine("        try {");
        sb.AppendLine("            Remove-AppxPackage -Package $pacoteAtual.PackageFullName -ErrorAction Stop");
        sb.AppendLine("            Write-Host \"  [removido] $($pacoteAtual.PackageFullName)\" -ForegroundColor Green");
        sb.AppendLine("        }");
        sb.AppendLine("        catch {");
        sb.AppendLine("            Write-Host \"  [falha] $($pacoteAtual.PackageFullName): $($_.Exception.Message)\" -ForegroundColor Red");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("Write-Host 'Concluido.'");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Grava o script no destino informado e devolve o caminho gravado.
    ///
    /// O arquivo sai em **UTF-8 com BOM**: o PowerShell 5.1 (o que vem no
    /// Windows) le o script sem BOM como ANSI, e o nome de um aplicativo com
    /// acento viraria lixo.
    /// </summary>
    /// <param name="path">O caminho do arquivo a gravar.</param>
    /// <param name="apps">Os aplicativos da Store a remover.</param>
    /// <returns>O caminho gravado, ou string vazia quando falhou.</returns>
    public static string Write(string? path, IEnumerable<WindowsApp>? apps)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            var destino = Path.GetFullPath(path);
            var pasta = Path.GetDirectoryName(destino);
            if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

            File.WriteAllText(destino, Build(apps), new UTF8Encoding(true));
            return destino;
        }
        catch
        {
            // Falha de gravacao: quem chama precisa avisar o usuario, e o
            // destino fica vazio para isso.
            return string.Empty;
        }
    }

    /// <summary>
    /// Escapa o texto para sair dentro de aspas simples do PowerShell: a
    /// aspa simples e duplicada.
    /// </summary>
    /// <param name="text">O texto.</param>
    private static string Escape(string? text) =>
        (text ?? string.Empty).Replace("'", "''");
}
