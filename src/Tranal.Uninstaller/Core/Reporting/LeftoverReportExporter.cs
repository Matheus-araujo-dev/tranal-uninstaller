using System.IO;
using System.Text;
using System.Xml;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Core.Reporting;

/// <summary>
/// A exportacao da varredura de sobras.
///
/// O exportador de programas (<see cref="Exporters"/>) exporta a LISTA de
/// instalados. A varredura e outra coisa: e o que o Tranal achou no disco e
/// no registro, com o motivo do julgamento e a confianca de cada item. E
/// essa segunda coisa que serve para auditoria — por isso ela sai com o
/// motivo e a confianca, e nao so com o caminho.
///
/// O motivo e a confianca sao os mesmos da tela: vem de
/// <see cref="LeftoverItem"/> e o texto vem de
/// <see cref="LeftoverLabels"/>. Nao existe um segundo julgamento.
/// </summary>
public static class LeftoverReportExporter
{
    /// <summary>
    /// Exporta a varredura de sobras em XML.
    ///
    /// Cada item leva o tipo, o caminho, o detalhe, a confianca, o motivo, se
    /// veio marcado e se pertence a outra conta do Windows. O nome do motivo
    /// e da confianca sai como identificador fixo (para a ferramenta ler) e
    /// o texto traduzido vem junto (para a pessoa ler).
    /// </summary>
    /// <param name="items">Os itens de sobra encontrados.</param>
    public static string ToXml(IEnumerable<LeftoverItem> items)
    {
        var lista = (items ?? Enumerable.Empty<LeftoverItem>())
            .Where(i => i is not null)
            .ToList();

        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
        using var stream = new MemoryStream();

        using (var writer = XmlWriter.Create(stream, settings))
        {
            writer.WriteStartElement("LeftoverScan");
            writer.WriteAttributeString("exported", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"));
            writer.WriteAttributeString("count", lista.Count.ToString());

            foreach (var item in lista)
            {
                writer.WriteStartElement("Item");
                writer.WriteAttributeString("kind", item.Kind ?? string.Empty);
                writer.WriteAttributeString("path", item.Path ?? string.Empty);
                writer.WriteAttributeString("detail", item.Detail ?? string.Empty);
                writer.WriteAttributeString("confidence", item.Confidence.ToString());
                writer.WriteAttributeString("confidenceLabel", LeftoverLabels.Confidence(item.Confidence));
                writer.WriteAttributeString("reason", item.Reason.ToString());
                writer.WriteAttributeString("reasonLabel", LeftoverLabels.Reason(item.Reason));
                writer.WriteAttributeString("selected", item.Selected ? "true" : "false");
                writer.WriteAttributeString("otherAccount", item.OtherAccount ? "true" : "false");
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Grava a varredura de sobras em XML no destino informado.
    ///
    /// O arquivo sai em UTF-8 sem BOM: e o que o Bloco de Notas e as
    /// ferramentas de auditoria leem sem surpresa.
    /// </summary>
    /// <param name="path">O caminho do arquivo a gravar.</param>
    /// <param name="items">Os itens de sobra encontrados.</param>
    /// <returns>O caminho gravado, ou string vazia quando falhou.</returns>
    public static string WriteXml(string path, IEnumerable<LeftoverItem> items)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            var destino = Path.GetFullPath(path);
            var pasta = Path.GetDirectoryName(destino);
            if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

            File.WriteAllText(destino, ToXml(items), new UTF8Encoding(false));
            return destino;
        }
        catch
        {
            // Falha de gravacao: quem chama precisa avisar o usuario, e o
            // destino fica vazio para isso.
            return string.Empty;
        }
    }
}
