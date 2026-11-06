using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Reporting;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// A exportacao para o Excel.
///
/// O Tranal nao pode depender do Excel instalado: ele precisa funcionar
/// numa maquina sem Office. O formato escolhido e o SpreadsheetML 2003
/// (XML puro), que o Excel abre nativamente e continua sendo texto plano
/// auditavel. O conteudo e a MESMA lista e as MESMAS colunas do
/// exportador — nao existe uma segunda lista de colunas.
/// </summary>
public class ExcelExportTests
{
    private static List<InstalledProgram> Programas() => new()
    {
        new InstalledProgram
        {
            DisplayName = "MeuApp",
            DisplayVersion = "1.2.3",
            Publisher = "Tranal",
            UninstallString = @"""C:\Program Files\MeuApp\uninstall.exe""",
            RegistryKey = @"HKEY_CURRENT_USER\Software\MeuApp",
        },
        new InstalledProgram
        {
            DisplayName = "OutroApp",
            DisplayVersion = "4.5",
            Publisher = "Tranal",
        },
    };

    [Fact]
    public void AExportacaoTrazOsNomesEAsColunasDosProgramas()
    {
        var excel = ReportFormats.ToExcel(Programas(), null);

        // Os nomes dos programas...
        Assert.Contains("MeuApp", excel);
        Assert.Contains("OutroApp", excel);

        // ...e os valores das colunas.
        Assert.Contains("1.2.3", excel);
        Assert.Contains(@"C:\Program Files\MeuApp\uninstall.exe", excel);
    }

    [Fact]
    public void OArquivoParaExcelEhXmlValido()
    {
        var excel = ReportFormats.ToExcel(Programas(), null);

        // Um XML que nao abre nao serve nem para o Excel nem para auditoria.
        var documento = XDocument.Parse(excel);

        Assert.Equal("Workbook", documento.Root!.Name.LocalName);
        Assert.Equal(SpreadsheetNs, documento.Root.Name.NamespaceName);

        var tabela = documento.Root.Descendants().First(e => e.Name.LocalName == "Table");
        var linhas = tabela.Elements().Where(e => e.Name.LocalName == "Row").ToList();

        // Uma linha de cabecalho e uma linha por programa.
        Assert.Equal(3, linhas.Count);

        var celulas = linhas[0].Elements().Where(e => e.Name.LocalName == "Cell").ToList();
        Assert.Equal(Exporters.DefaultColumns.Count, celulas.Count);
        Assert.All(celulas, c =>
        {
            var data = c.Descendants().First(d => d.Name.LocalName == "Data");
            Assert.NotNull(data);
            Assert.NotNull(data!.Attribute(XNamespace.Get(SpreadsheetNs) + "Type"));
        });

        // O arquivo sai na forma canonica do SpreadsheetML: elemento no
        // namespace padrao e atributo com o prefixo "ss" — e assim que o
        // proprio Excel grava.
        Assert.Contains("<Styles>", excel);
        Assert.Contains("ss:StyleID=\"Cabecalho\"", excel);
        Assert.DoesNotContain("<ss:Styles>", excel);
    }

    [Fact]
    public void AsColunasDoExcelSaoAsMesmasDoExportador()
    {
        var excel = ReportFormats.ToExcel(Programas(), null);
        var documento = XDocument.Parse(excel);

        var tabela = documento.Root!.Descendants().First(e => e.Name.LocalName == "Table");
        var cabecalho = tabela.Elements().First(e => e.Name.LocalName == "Row");
        var titulos = cabecalho
            .Elements()
            .Where(e => e.Name.LocalName == "Cell")
            .Select(c => c.Descendants().First(d => d.Name.LocalName == "Data").Value)
            .ToList();

        var esperados = Exporters.DefaultColumns
            .Select(c => ProgramColumns.Info(c).ExportName)
            .ToList();

        Assert.Equal(esperados, titulos);
    }

    [Fact]
    public void OExcelUsaAsColunasPedidas()
    {
        var colunas = new[] { ProgramColumn.Name, ProgramColumn.Version };
        var excel = ReportFormats.ToExcel(Programas(), colunas);
        var documento = XDocument.Parse(excel);

        var tabela = documento.Root!.Descendants().First(e => e.Name.LocalName == "Table");
        var cabecalho = tabela.Elements().First(e => e.Name.LocalName == "Row");
        var titulos = cabecalho
            .Elements()
            .Where(e => e.Name.LocalName == "Cell")
            .Select(c => c.Descendants().First(d => d.Name.LocalName == "Data").Value)
            .ToList();

        Assert.Equal(new[] { "Program", "Version" }, titulos);
    }

    /// <summary>O namespace do SpreadsheetML 2003.</summary>
    private const string SpreadsheetNs = "urn:schemas-microsoft-com:office:spreadsheet";
}

/// <summary>
/// O layout de impressao.
///
/// O nucleo produz o layout (colunas, cabecalho, paginacao) e a interface
/// grafica so o desenha. Por isso o layout e testavel sem impressora e sem
/// WPF.
/// </summary>
public class PrintLayoutTests
{
    private static List<InstalledProgram> Programas(int quantidade)
    {
        var lista = new List<InstalledProgram>();
        for (var i = 1; i <= quantidade; i++)
        {
            lista.Add(new InstalledProgram
            {
                DisplayName = "Programa " + i,
                DisplayVersion = "1.0",
                Publisher = "Tranal",
            });
        }

        return lista;
    }

    [Fact]
    public void OLayoutPaginaAsLinhas()
    {
        var layout = PrintLayout.Build(Programas(25), null, rowsPerPage: 10);

        // 25 linhas em paginas de 10: 3 paginas.
        Assert.Equal(3, layout.TotalPages);
        Assert.Equal(25, layout.TotalRows);
        Assert.Equal(10, layout.RowsPerPage);

        // Cada pagina tem o cabecalho das colunas mais as suas linhas.
        Assert.Equal(11, layout.Pages[0].Rows.Count);
        Assert.Equal(11, layout.Pages[1].Rows.Count);
        Assert.Equal(6, layout.Pages[2].Rows.Count);

        // O rodape diz o numero da pagina.
        Assert.Contains("1", layout.Pages[0].Footer);
        Assert.Contains("3", layout.Pages[2].Footer);
    }

    [Fact]
    public void OCabecalhoApareceEmCadaPagina()
    {
        var layout = PrintLayout.Build(Programas(25), null, rowsPerPage: 10);

        // O cabecalho do documento diz quantos programas sao.
        Assert.Contains("25", layout.Header);
        Assert.Contains("Tranal", layout.Header);

        // O cabecalho das colunas se repete em toda pagina: e o que permite
        // ler a tabela impressa.
        foreach (var pagina in layout.Pages)
        {
            Assert.Contains("Programa", pagina.Rows[0]);
            Assert.Contains("Versão", pagina.Rows[0]);
        }
    }

    [Fact]
    public void AListaVaziaGeraUmaPaginaComOCabecalho()
    {
        var layout = PrintLayout.Build(new List<InstalledProgram>(), null, rowsPerPage: 10);

        Assert.Equal(1, layout.TotalPages);
        Assert.Equal(0, layout.TotalRows);
        Assert.Single(layout.Pages[0].Rows);
    }

    [Fact]
    public void AsColunasDoLayoutSaoAsDoExportador()
    {
        var layout = PrintLayout.Build(Programas(2), null);

        Assert.Equal(Exporters.DefaultColumns.Count, layout.Columns.Count);
        Assert.Equal("Programa", layout.Columns[0].Title);
    }
}

/// <summary>
/// A exportacao da varredura de sobras.
///
/// O que serve para auditoria e o motivo e a confianca: o caminho sozinho
/// nao diz por que o item e sobra.
/// </summary>
public class LeftoverReportTests
{
    private static List<LeftoverItem> Sobras() => new()
    {
        new()
        {
            Kind = "Pasta",
            Path = @"C:\Program Files\MeuApp",
            Detail = "12 MB",
            Confidence = LeftoverConfidence.High,
            Reason = LeftoverReason.InstallFolder,
            Selected = true,
            OtherAccount = false,
        },
        new()
        {
            Kind = "Registro",
            Path = @"HKEY_CURRENT_USER\Software\MeuApp",
            Detail = "Chave do programa",
            Confidence = LeftoverConfidence.Low,
            Reason = LeftoverReason.InstallerLog,
            Selected = false,
            OtherAccount = true,
        },
    };

    [Fact]
    public void AExportacaoLevaOMotivoEACiancaDeCadaItem()
    {
        var xml = LeftoverReportExporter.ToXml(Sobras());

        var documento = XDocument.Parse(xml);
        var itens = documento.Root!.Elements().Where(e => e.Name.LocalName == "Item").ToList();

        Assert.Equal(2, itens.Count);

        var primeiro = itens[0];
        Assert.Equal("Pasta", primeiro.Attribute("kind")!.Value);
        Assert.Equal(@"C:\Program Files\MeuApp", primeiro.Attribute("path")!.Value);
        Assert.Equal("High", primeiro.Attribute("confidence")!.Value);
        Assert.Equal("InstallFolder", primeiro.Attribute("reason")!.Value);
        Assert.Equal("true", primeiro.Attribute("selected")!.Value);
        Assert.Equal("false", primeiro.Attribute("otherAccount")!.Value);

        var segundo = itens[1];
        Assert.Equal("Low", segundo.Attribute("confidence")!.Value);
        Assert.Equal("InstallerLog", segundo.Attribute("reason")!.Value);
        Assert.Equal("false", segundo.Attribute("selected")!.Value);
        Assert.Equal("true", segundo.Attribute("otherAccount")!.Value);
    }

    [Fact]
    public void AExportacaoDeSobrasEhXmlValido()
    {
        var xml = LeftoverReportExporter.ToXml(Sobras());

        var documento = XDocument.Parse(xml);
        Assert.Equal("LeftoverScan", documento.Root!.Name.LocalName);
        Assert.Equal("2", documento.Root.Attribute("count")!.Value);
    }
}

/// <summary>
/// A exportacao de registro para o formato .reg.
///
/// O formato e o do proprio Windows: o Editor do Registro e o reg.exe
/// esperam o cabecalho "Windows Registry Editor Version 5.00", as secoes
/// [HKEY_...] e o sinal de menos para apagar.
/// </summary>
public class RegistryExportTests
{
    [Fact]
    public void OArquivoRegTemOCabecalhoCorreto()
    {
        var reg = ReportFormats.ToReg(new[]
        {
            new RegistryChange { KeyPath = @"HKEY_CURRENT_USER\Software\MeuApp", Operation = RegistryChangeOperation.CreateKey },
        });

        Assert.StartsWith("Windows Registry Editor Version 5.00", reg);
        Assert.Contains("[HKEY_CURRENT_USER\\Software\\MeuApp]", reg);
    }

    [Fact]
    public void OValorDwordUsaOTipoCerto()
    {
        var reg = ReportFormats.ToReg(new[]
        {
            new RegistryChange
            {
                KeyPath = @"HKEY_CURRENT_USER\Software\MeuApp",
                ValueName = "Contador",
                ValueKind = Microsoft.Win32.RegistryValueKind.DWord,
                Data = "16",
            },
        });

        Assert.Contains("\"Contador\"=dword:00000010", reg);
    }

    [Fact]
    public void OValorApagadoUsaOSinalDeMenos()
    {
        var reg = ReportFormats.ToReg(new[]
        {
            new RegistryChange
            {
                KeyPath = @"HKEY_CURRENT_USER\Software\MeuApp",
                ValueName = "Antigo",
                Operation = RegistryChangeOperation.DeleteValue,
            },
        });

        Assert.Contains("\"Antigo\"=-", reg);
    }

    [Fact]
    public void AChaveApagadaUsaOSinalDeMenosNoNomeDaChave()
    {
        var reg = ReportFormats.ToReg(new[]
        {
            new RegistryChange
            {
                KeyPath = @"HKEY_CURRENT_USER\Software\MeuApp",
                Operation = RegistryChangeOperation.DeleteKey,
            },
        });

        Assert.Contains("[-HKEY_CURRENT_USER\\Software\\MeuApp]", reg);
    }

    [Fact]
    public void OValorPadraoDaChaveUsaOArroba()
    {
        var reg = ReportFormats.ToReg(new[]
        {
            new RegistryChange
            {
                KeyPath = @"HKEY_CURRENT_USER\Software\MeuApp",
                ValueName = string.Empty,
                ValueKind = Microsoft.Win32.RegistryValueKind.String,
                Data = "texto",
            },
        });

        Assert.Contains("\"@\"=\"texto\"", reg);
    }

    [Fact]
    public void OValorBinarioSaiEmHexadecimal()
    {
        var reg = ReportFormats.ToReg(new[]
        {
            new RegistryChange
            {
                KeyPath = @"HKEY_CURRENT_USER\Software\MeuApp",
                ValueName = "Dados",
                ValueKind = Microsoft.Win32.RegistryValueKind.Binary,
                Data = "00,1A,FF",
            },
        });

        Assert.Contains("\"Dados\"=hex:00,1A,FF", reg);
    }

    [Fact]
    public void OArquivoRegGravadoEhLidoDeVolta()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "tranal-reg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);

        try
        {
            var caminho = Path.Combine(pasta, "alteracoes.reg");
            var gravado = ReportFormats.WriteReg(caminho, new[]
            {
                new RegistryChange
                {
                    KeyPath = @"HKEY_CURRENT_USER\Software\MeuApp",
                    ValueName = "Nome",
                    ValueKind = Microsoft.Win32.RegistryValueKind.String,
                    Data = "Meu App",
                },
            });

            Assert.Equal(caminho, gravado);
            Assert.True(File.Exists(caminho));

            // O arquivo .reg do Windows e UTF-16 LE com BOM: e o que o
            // reg.exe e o Editor do Registro esperam.
            var bytes = File.ReadAllBytes(caminho);
            Assert.Equal(0xFF, bytes[0]);
            Assert.Equal(0xFE, bytes[1]);

            var conteudo = File.ReadAllText(caminho);
            Assert.Contains("Windows Registry Editor Version 5.00", conteudo);
            Assert.Contains("\"Nome\"=\"Meu App\"", conteudo);
        }
        finally
        {
            try { Directory.Delete(pasta, true); }
            catch { /* a pasta de teste fica para o sistema limpar */ }
        }
    }
}

/// <summary>
/// O pacote portatil de programas monitorados.
///
/// O pacote leva o log e um manifesto. A importacao valida o manifesto e
/// NUNCA sobrescreve um log que ja existe: ela renomeia.
/// </summary>
public class PortableArchiveTests : IDisposable
{
    private readonly List<string> _sandboxes = new();
    private readonly List<string> _logsImportados = new();

    private string Sandbox()
    {
        var path = Path.Combine(Path.GetTempPath(), "tranal-portable-teste-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        _sandboxes.Add(path);
        return path;
    }

    public void Dispose()
    {
        // Os logs importados vao para a base de logs real: a suite limpa o
        // que criou, para nao deixar rastro na maquina de quem roda.
        foreach (var log in _logsImportados)
        {
            try { if (File.Exists(log)) File.Delete(log); }
            catch { /* o arquivo some com a limpeza do sistema */ }
        }

        foreach (var path in _sandboxes)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch { /* a pasta de teste fica para o sistema limpar */ }
        }
    }

    private TracedInstall LogDeTeste(string pasta)
    {
        var id = Guid.NewGuid().ToString("D");
        var install = new TracedInstall
        {
            Id = id,
            ProgramName = "MeuApp",
            Timestamp = new DateTime(2026, 11, 5, 10, 22, 31, DateTimeKind.Utc),
            InstallerPath = @"C:\Temp\setup.exe",
            LogPath = Path.Combine(pasta, id + ".json"),
        };

        // O log nasce no formato canonico: e o proprio TraceStore que escreve.
        TraceStore.Export(install, install.LogPath);
        return install;
    }

    [Fact]
    public void OPacotePortatilExportaEReimporta()
    {
        var pasta = Sandbox();
        var install = LogDeTeste(pasta);

        var zip = PortableArchive.Export(new[] { install }, Path.Combine(pasta, "pacote.zip"));
        Assert.True(File.Exists(zip));

        var resultado = PortableArchive.Import(zip);
        foreach (var log in resultado.Logs) _logsImportados.Add(TraceStore.PathForId(log.Id));

        Assert.Equal(string.Empty, resultado.Error);
        Assert.Equal(1, resultado.Imported);
        Assert.Single(resultado.Logs);
        Assert.Equal("MeuApp", resultado.Logs[0].ProgramName);
    }

    [Fact]
    public void AImportacaoNaoSobrescreveUmLogExistente()
    {
        var pasta = Sandbox();
        var install = LogDeTeste(pasta);

        var zip = PortableArchive.Export(new[] { install }, Path.Combine(pasta, "pacote.zip"));

        var primeira = PortableArchive.Import(zip);
        var segunda = PortableArchive.Import(zip);
        foreach (var log in primeira.Logs) _logsImportados.Add(TraceStore.PathForId(log.Id));
        foreach (var log in segunda.Logs) _logsImportados.Add(TraceStore.PathForId(log.Id));

        Assert.Equal(1, primeira.Imported);
        Assert.Equal(1, segunda.Imported);

        // O log que ja estava na base continua com o identificador dele: o
        // importado entrou com outro nome.
        Assert.NotEqual(primeira.Logs[0].Id, segunda.Logs[0].Id);
        Assert.Equal(install.Id, primeira.Logs[0].Id);

        // E o arquivo original continua no lugar, intacto.
        var caminhoOriginal = TraceStore.PathForId(primeira.Logs[0].Id);
        Assert.True(File.Exists(caminhoOriginal));
        Assert.Contains("MeuApp", File.ReadAllText(caminhoOriginal));
    }

    [Fact]
    public void OPacoteSemManifestoNaoEntraNaBase()
    {
        var pasta = Sandbox();
        var zip = Path.Combine(pasta, "sem-manifesto.zip");

        using (var arquivo = File.Create(zip))
        using (var pacote = new ZipArchive(arquivo, ZipArchiveMode.Create))
        {
            var entrada = pacote.CreateEntry("logs/x.json");
            using var stream = entrada.Open();
            stream.Write(new byte[] { 1, 2, 3 });
        }

        var resultado = PortableArchive.Import(zip);

        Assert.Equal(0, resultado.Imported);
        Assert.NotEqual(string.Empty, resultado.Error);
    }
}

/// <summary>
/// A importacao de uma lista de programas exportada.
///
/// O que importa nao e so o que casou: e o que NAO casou, e por que.
/// </summary>
public class ProgramListImportTests : IDisposable
{
    private readonly List<string> _sandboxes = new();

    private string Sandbox()
    {
        var path = Path.Combine(Path.GetTempPath(), "tranal-lista-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        _sandboxes.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _sandboxes)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch { /* a pasta de teste fica para o sistema limpar */ }
        }
    }

    private static List<InstalledProgram> Exportados() => new()
    {
        new InstalledProgram { DisplayName = "MeuApp", RegistryKey = @"HKEY_CURRENT_USER\Software\MeuApp" },
        new InstalledProgram { DisplayName = "Ausente" },
    };

    private static List<InstalledProgram> Instalados() => new()
    {
        new InstalledProgram { DisplayName = "MeuApp", RegistryKey = @"HKEY_CURRENT_USER\Software\MeuApp" },
        new InstalledProgram { DisplayName = "OutroApp" },
    };

    [Fact]
    public void AImportacaoDizOQueCasouEOQueNaoCasouComOMotivo()
    {
        var pasta = Sandbox();
        var arquivo = Path.Combine(pasta, "lista.csv");
        File.WriteAllText(arquivo, Exporters.ToCsv(Exportados()), new UTF8Encoding(false));

        var resultado = ProgramListImporter.Import(arquivo, Instalados());

        Assert.Equal(2, resultado.TotalCount);
        Assert.Single(resultado.Matched);
        Assert.Single(resultado.Unmatched);

        Assert.Equal("MeuApp", resultado.Matched[0].Name);
        Assert.NotNull(resultado.Matched[0].Program);
        Assert.True(resultado.Matched[0].Matched);
        Assert.NotEqual(string.Empty, resultado.Matched[0].Reason);

        Assert.Equal("Ausente", resultado.Unmatched[0].Name);
        Assert.Null(resultado.Unmatched[0].Program);
        Assert.Contains("não há programa instalado", resultado.Unmatched[0].Reason);
    }

    [Fact]
    public void AImportacaoCasouPelaChaveDeRegistro()
    {
        var pasta = Sandbox();
        var arquivo = Path.Combine(pasta, "lista.xml");
        File.WriteAllText(arquivo, Exporters.ToXml(Exportados()), new UTF8Encoding(false));

        var resultado = ProgramListImporter.Import(arquivo, Instalados());

        Assert.Single(resultado.Matched);
        Assert.Equal("MeuApp", resultado.Matched[0].Name);
    }

    [Fact]
    public void AImportacaoCasouIgnorandoAcentosECaixa()
    {
        var pasta = Sandbox();
        var arquivo = Path.Combine(pasta, "lista.txt");
        File.WriteAllText(arquivo, "Meu App - 1.0", new UTF8Encoding(false));

        var resultado = ProgramListImporter.Import(arquivo, new List<InstalledProgram>
        {
            new InstalledProgram { DisplayName = "MEU APP" },
        });

        Assert.Single(resultado.Matched);
        Assert.Equal("MEU APP", resultado.Matched[0].Program!.DisplayName);
    }
}

/// <summary>
/// A edicao de uma entrada do log de instalacao.
///
/// A gravacao e atomica: o log nao pode ficar pela metade, porque e ele
/// que diz o que o instalador gravou.
/// </summary>
public class TraceLogEditorTests : IDisposable
{
    private readonly List<string> _sandboxes = new();

    private string Sandbox()
    {
        var path = Path.Combine(Path.GetTempPath(), "tranal-log-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        _sandboxes.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _sandboxes)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch { /* a pasta de teste fica para o sistema limpar */ }
        }
    }

    private TracedInstall LogDeTeste(string pasta)
    {
        var id = Guid.NewGuid().ToString("D");
        var install = new TracedInstall
        {
            Id = id,
            ProgramName = "MeuApp",
            Timestamp = new DateTime(2026, 11, 5, 10, 22, 31, DateTimeKind.Utc),
            InstallerPath = @"C:\Temp\setup.exe",
            LogPath = Path.Combine(pasta, id + ".json"),
        };

        TraceStore.Export(install, install.LogPath);
        return install;
    }

    [Fact]
    public void AEdicaoAdicionaOCaminhoDeArquivoPastaEChave()
    {
        var pasta = Sandbox();
        var install = LogDeTeste(pasta);

        var comArquivo = TraceLogEditor.AddFile(install, @"C:\Program Files\MeuApp\app.exe", 2048);
        Assert.NotNull(comArquivo);

        var comPasta = TraceLogEditor.AddFolder(comArquivo!, @"C:\Program Files\MeuApp");
        Assert.NotNull(comPasta);

        var comChave = TraceLogEditor.AddRegistryKey(comPasta!, @"HKEY_CURRENT_USER\Software\MeuApp");
        Assert.NotNull(comChave);

        // O log relido pelo leitor canonico tem o que foi acrescentado: o
        // arquivo gravado continua no formato que o TraceStore entende.
        var payload = TraceStore.LoadPayload(comChave!);
        Assert.NotNull(payload);

        var trace = TraceStore.ReadPayload(payload!.Value);
        Assert.Single(trace.Files);
        Assert.Equal(@"C:\Program Files\MeuApp\app.exe", trace.Files[0].Path);
        Assert.Single(trace.Folders);
        Assert.Equal(@"C:\Program Files\MeuApp", trace.Folders[0]);
        Assert.Single(trace.RegistryKeys);
        Assert.Equal(@"HKEY_CURRENT_USER\Software\MeuApp", trace.RegistryKeys[0]);
    }

    [Fact]
    public void AEdicaoRemoveOCaminho()
    {
        var pasta = Sandbox();
        var install = LogDeTeste(pasta);

        var comArquivo = TraceLogEditor.AddFile(install, @"C:\Program Files\MeuApp\app.exe");
        Assert.NotNull(comArquivo);

        var semArquivo = TraceLogEditor.Remove(comArquivo!, @"C:\Program Files\MeuApp\app.exe");
        Assert.NotNull(semArquivo);
        Assert.Equal(0, semArquivo!.FileCount);

        var payload = TraceStore.LoadPayload(semArquivo);
        Assert.NotNull(payload);
        Assert.Empty(TraceStore.ReadPayload(payload!.Value).Files);
    }

    [Fact]
    public void AEdicaoDeixaOCopiaDoLogAnteriorAoLado()
    {
        var pasta = Sandbox();
        var install = LogDeTeste(pasta);

        var editado = TraceLogEditor.AddFolder(install, @"C:\Program Files\MeuApp");
        Assert.NotNull(editado);

        // A gravacao e atomica e guarda a versao anterior.
        Assert.True(File.Exists(install.LogPath + ".bak"));

        // E o temporario nao sobra na pasta.
        Assert.False(File.Exists(install.LogPath + ".tmp"));
    }
}

/// <summary>
/// O script de desinstalacao em lote.
///
/// O script tem que rodar o desinstalador certo de cada programa, com as
/// aspas certas quando o caminho tem espaco, e sem deixar o texto do
/// usuario virar comando.
/// </summary>
public class BatchScriptTests
{
    [Fact]
    public void OScriptEscapaOCaminhoComEspaco()
    {
        var program = new InstalledProgram
        {
            DisplayName = "MeuApp",
            Kind = UninstallerKind.Nsis,
            UninstallString = @"""C:\Program Files\MeuApp\uninstall.exe""",
        };

        var script = BatchScriptWriter.Build(new[] { program });

        // O caminho com espaco vai entre aspas: sem elas o comando quebra.
        Assert.Contains(@"""C:\Program Files\MeuApp\uninstall.exe""", script);
    }

    [Fact]
    public void OScriptUsaOComandoSilenciosoQuandoExiste()
    {
        var program = new InstalledProgram
        {
            DisplayName = "MeuApp",
            Kind = UninstallerKind.Nsis,
            UninstallString = @"""C:\Program Files\MeuApp\uninstall.exe""",
        };

        var script = BatchScriptWriter.Build(new[] { program }, silent: true);

        // O switch do NSIS vem do lugar unico que decide ele.
        Assert.Contains("/S", script);
        Assert.Contains("MeuApp", script);
    }

    [Fact]
    public void OScriptUsaOComandoRegistradoQuandoNaoHaComandoSilencioso()
    {
        var program = new InstalledProgram
        {
            DisplayName = "MeuApp",
            Kind = UninstallerKind.Unknown,
            UninstallString = @"""C:\Apps\MeuApp\remove.exe""",
        };

        var script = BatchScriptWriter.Build(new[] { program }, silent: true);

        Assert.Contains(@"""C:\Apps\MeuApp\remove.exe""", script);
        Assert.DoesNotContain("/VERYSILENT", script);
    }

    [Fact]
    public void OScriptNaoDeixaOTextoDoUsuarioVirarComando()
    {
        var program = new InstalledProgram
        {
            DisplayName = "Foto & Video",
            Kind = UninstallerKind.Nsis,
            UninstallString = @"""C:\Apps\un.exe""",
        };

        var script = BatchScriptWriter.Build(new[] { program });

        // O "&" escapado: sem o "^", o nome do programa viraria um segundo
        // comando do cmd.
        Assert.Contains("Foto ^& Video", script);
    }

    [Fact]
    public void OScriptEscapaOPorcentoDoCaminho()
    {
        var program = new InstalledProgram
        {
            DisplayName = "MeuApp",
            Kind = UninstallerKind.Nsis,
            UninstallString = @"""C:\Program Files\%APPDATA%\MeuApp\uninstall.exe""",
        };

        var script = BatchScriptWriter.Build(new[] { program });

        // O "%" viraria variavel do cmd; no arquivo .cmd ele e "%%".
        Assert.Contains("%%APPDATA%%", script);
    }

    [Fact]
    public void OScriptTemOComandoDoExportador()
    {
        var program = new InstalledProgram
        {
            DisplayName = "MeuApp",
            Kind = UninstallerKind.Nsis,
            UninstallString = @"""C:\Apps\un.exe""",
        };

        // A porta de entrada da exportacao e uma so.
        Assert.Equal(BatchScriptWriter.Build(new[] { program }), ReportFormats.ToBatchScript(new[] { program }));
    }
}

/// <summary>
/// A exportacao do log de instalacao.
///
/// Ate aqui o log so saia em JSON — o formato que o proprio Tranal le.
/// Um JSON serve para o programa; nao serve para quem precisa conferir o
/// que o instalador gravou.
/// </summary>
public class TraceLogExporterTests : IDisposable
{
    private readonly List<string> _sandboxes = new();

    private string Sandbox()
    {
        var path = Path.Combine(Path.GetTempPath(), "tranal-export-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        _sandboxes.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _sandboxes)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch { /* a pasta de teste fica para o sistema limpar */ }
        }
    }

    private TracedInstall LogDeTeste(string pasta)
    {
        var id = Guid.NewGuid().ToString("D");
        var install = new TracedInstall
        {
            Id = id,
            ProgramName = "MeuApp",
            Timestamp = new DateTime(2026, 11, 5, 10, 22, 31, DateTimeKind.Utc),
            InstallerPath = @"C:\Temp\setup.exe",
            LogPath = Path.Combine(pasta, id + ".json"),
        };

        TraceStore.Export(install, install.LogPath);
        return install;
    }

    [Fact]
    public void OLogSaiEmTextoCsvEHtml()
    {
        var pasta = Sandbox();
        var install = LogDeTeste(pasta);

        var texto = TraceLogExporter.ToText(install);
        Assert.Contains("Programa:", texto);
        Assert.Contains("MeuApp", texto);

        var csv = TraceLogExporter.ToCsv(install);
        Assert.Contains("Programa;Tipo;Item;Detalhe;Modificado", csv);

        var html = TraceLogExporter.ToHtml(install);
        Assert.Contains("<table>", html);
        Assert.Contains("MeuApp", html);
    }

    [Fact]
    public void OLogGravadoNoDestinoEhRelido()
    {
        var pasta = Sandbox();
        var install = LogDeTeste(pasta);

        var caminho = Path.Combine(pasta, "log.csv");
        var gravado = TraceLogExporter.Write(install, caminho, TraceLogExportFormat.Csv);

        Assert.Equal(caminho, gravado);
        Assert.Contains("Programa;Tipo;Item", File.ReadAllText(caminho));
    }

    [Fact]
    public void AsChavesDeRegistroDoLogViramAlteracoesDeReg()
    {
        var pasta = Sandbox();
        var install = LogDeTeste(pasta);

        var comChave = TraceLogEditor.AddRegistryKey(install, @"HKEY_CURRENT_USER\Software\MeuApp");
        Assert.NotNull(comChave);

        var mudancas = TraceLogExporter.ToRegistryChanges(comChave!);
        Assert.Single(mudancas);
        Assert.Equal(@"HKEY_CURRENT_USER\Software\MeuApp", mudancas[0].KeyPath);

        var reg = ReportFormats.ToReg(mudancas);
        Assert.Contains("[HKEY_CURRENT_USER\\Software\\MeuApp]", reg);
    }
}
