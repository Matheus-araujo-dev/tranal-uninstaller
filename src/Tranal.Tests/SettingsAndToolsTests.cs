using System;
using System.Collections.Generic;
using System.IO;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Appearance;
using Tranal.Uninstaller.Core.Reporting;
using Tranal.Uninstaller.Core.Settings;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// As preferencias que o Revo Pro tem e o Tranal ainda nao tinha:
/// exportar/importar todas as preferencias, o cache de programas, a
/// ferramenta personalizada, a exportacao pelo Excel instalado, os motores
/// de busca configuraveis e os papeis de cor que faltavam.
///
/// A regra de cada recurso mora no nucleo (e nao na tela): o que e testado
/// aqui e o comportamento que a interface vai consumir.
/// </summary>
[Collection("preferencias")]
public class SettingsAndToolsTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly string _settingsFile;
    private readonly string _cacheFile;
    private readonly string _toolsFile;

    public SettingsAndToolsTests()
    {
        // Os arquivos do teste ficam num caminho temporario: sem isso, um
        // teste escreveria no arquivo real do usuario.
        _settingsFile = Path.Combine(_temp.Root, "settings.json");
        _cacheFile = Path.Combine(_temp.Root, "program-cache.json");
        _toolsFile = Path.Combine(_temp.Root, "custom-tools.json");

        AppSettingsStore.FilePathOverride = _settingsFile;
        ProgramCacheStore.FilePathOverride = _cacheFile;
        CustomToolRegistry.FilePathOverride = _toolsFile;
    }

    public void Dispose()
    {
        AppSettingsStore.FilePathOverride = null;
        ProgramCacheStore.FilePathOverride = null;
        CustomToolRegistry.ResetForTests();
        _temp.Dispose();
    }

    // ---------------------------------------------------------------
    // Exportar / importar todas as preferencias (1002 e 1003)
    // ---------------------------------------------------------------

    [Fact]
    public void ExportarGravaUmArquivoJsonLegivel()
    {
        var arquivo = Path.Combine(_temp.Root, "preferencias.json");

        var ok = SettingsTransfer.Export(arquivo, new AppSettings
        {
            Theme = "Slate",
            Language = "en-US",
            AdvancedMode = true,
        });

        Assert.True(ok);
        Assert.True(File.Exists(arquivo));

        var json = File.ReadAllText(arquivo);
        Assert.Contains("\"Settings\"", json);
        Assert.Contains("Slate", json);
        Assert.Contains("FormatVersion", json);
    }

    [Fact]
    public void ExportarComCaminhoVazioNaoGravaNada()
    {
        Assert.False(SettingsTransfer.Export(string.Empty));
        Assert.False(SettingsTransfer.Export("   "));
    }

    [Fact]
    public void ImportarRestauraTodasAsPreferencias()
    {
        var arquivo = Path.Combine(_temp.Root, "preferencias.json");

        SettingsTransfer.Export(arquivo, new AppSettings
        {
            Theme = "Slate",
            Language = "en-US",
            AdvancedMode = true,
            WebSearchEngines = new List<string> { "Bing", "DuckDuckGo" },
            ExportToExcelInstalled = true,
        });

        var resultado = SettingsTransfer.Import(arquivo);

        Assert.True(resultado.Success);
        Assert.Equal(SettingsImportStatus.Ok, resultado.Status);

        var carregadas = AppSettingsStore.Load();
        Assert.Equal("Slate", carregadas.Theme);
        Assert.Equal("en-US", carregadas.Language);
        Assert.True(carregadas.AdvancedMode);
        Assert.Equal(new[] { "Bing", "DuckDuckGo" }, carregadas.WebSearchEngines);
        Assert.True(carregadas.ExportToExcelInstalled);
    }

    [Fact]
    public void APreviaMostraAsPreferenciasMasNaoGrava()
    {
        var arquivo = Path.Combine(_temp.Root, "preferencias.json");

        SettingsTransfer.Export(arquivo, new AppSettings { Theme = "Contrast" });

        AppSettingsStore.Save(new AppSettings { Theme = "Dark" });

        var resultado = SettingsTransfer.Import(arquivo, preview: true);

        Assert.True(resultado.Success);
        Assert.True(resultado.IsPreview);
        Assert.NotNull(resultado.Settings);
        Assert.Equal("Contrast", resultado.Settings!.Theme);

        // Nada foi gravado: a preferencia em uso continua a mesma.
        Assert.Equal("Dark", AppSettingsStore.Load().Theme);
    }

    [Fact]
    public void APreviaDescreveOQueVaiMudar()
    {
        var arquivo = Path.Combine(_temp.Root, "preferencias.json");

        AppSettingsStore.Save(new AppSettings { Theme = "Dark" });
        SettingsTransfer.Export(arquivo, new AppSettings { Theme = "Slate" });

        var resultado = SettingsTransfer.Import(arquivo, preview: true);

        Assert.NotEmpty(resultado.Changes);
        Assert.Contains(resultado.Changes, c => c.Contains("Theme", StringComparison.Ordinal));
    }

    [Fact]
    public void ArquivoCorrompidoDevolveMotivoENaoQuebraOAplicativo()
    {
        var arquivo = Path.Combine(_temp.Root, "corrompido.json");
        File.WriteAllText(arquivo, "isto nao e um json");

        var resultado = SettingsTransfer.Import(arquivo);

        Assert.False(resultado.Success);
        Assert.Equal(SettingsImportStatus.CorruptedFile, resultado.Status);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Message));

        // O aplicativo continua com as preferencias que tinha.
        Assert.NotNull(AppSettingsStore.Current);
    }

    [Fact]
    public void ArquivoDeOutraVersaoDoFormatoDevolveMotivo()
    {
        var arquivo = Path.Combine(_temp.Root, "futuro.json");
        File.WriteAllText(arquivo, """{ "FormatVersion": 99, "Settings": { "Theme": "Dark" } }""");

        var resultado = SettingsTransfer.Import(arquivo);

        Assert.False(resultado.Success);
        Assert.Equal(SettingsImportStatus.IncompatibleFile, resultado.Status);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Message));
    }

    [Fact]
    public void ArquivoInexistenteDevolveMotivo()
    {
        var resultado = SettingsTransfer.Import(Path.Combine(_temp.Root, "nao-existe.json"));

        Assert.False(resultado.Success);
        Assert.Equal(SettingsImportStatus.FileNotFound, resultado.Status);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Message));
    }

    // ---------------------------------------------------------------
    // Cache de programas (15150)
    // ---------------------------------------------------------------

    [Fact]
    public void OCacheGuardaAUltimaVarreduraComDataEVersao()
    {
        var status = ProgramCacheStore.Save(new[]
        {
            new InstalledProgram { DisplayName = "Programa Um", DisplayVersion = "1.0", Publisher = "Editora" },
            new InstalledProgram { DisplayName = "Programa Dois", SizeBytes = 1024 },
        });

        Assert.Equal(ProgramCacheSaveStatus.Ok, status);

        Assert.True(ProgramCacheStore.TryLoad(out var cache));
        Assert.NotNull(cache);
        Assert.Equal(ProgramCache.CurrentVersion, cache!.Version);
        Assert.NotEqual(default, cache.SavedAtUtc);
        Assert.Equal(2, cache.ProgramCount);
        Assert.Equal(2, cache.Programs.Count);
        Assert.Equal("Programa Um", cache.Programs[0].DisplayName);
        Assert.Equal("Editora", cache.Programs[0].Publisher);
    }

    [Fact]
    public void CacheCorrompidoRecomecaDoZero()
    {
        File.WriteAllText(_cacheFile, "{ isto nao e json");

        Assert.False(ProgramCacheStore.TryLoad(out var cache));
        Assert.Null(cache);
        Assert.Equal(ProgramCacheLoadStatus.Corrupted, ProgramCacheStore.LastLoadStatus);
    }

    [Fact]
    public void CacheDeOutraVersaoDoFormatoNaoEhLido()
    {
        File.WriteAllText(_cacheFile, """{ "Version": 99, "Programs": [] }""");

        Assert.False(ProgramCacheStore.TryLoad(out var cache));
        Assert.Null(cache);
        Assert.Equal(ProgramCacheLoadStatus.Incompatible, ProgramCacheStore.LastLoadStatus);
    }

    [Fact]
    public void OCacheNaoCresceSemLimite()
    {
        var muitos = new List<InstalledProgram>();
        for (var i = 0; i < ProgramCacheStore.MaxEntries + 500; i++)
            muitos.Add(new InstalledProgram { DisplayName = "Programa " + i });

        Assert.Equal(ProgramCacheSaveStatus.Ok, ProgramCacheStore.Save(muitos));

        Assert.True(ProgramCacheStore.TryLoad(out var cache));
        Assert.Equal(ProgramCacheStore.MaxEntries, cache!.Programs.Count);
    }

    [Fact]
    public void VarreduraSemProgramasNaoGravaCache()
    {
        Assert.Equal(ProgramCacheSaveStatus.NothingToSave, ProgramCacheStore.Save(Array.Empty<InstalledProgram>()));
        Assert.False(File.Exists(_cacheFile));
    }

    [Fact]
    public void InvalidarOCacheFazAProximaLeituraRecomecarDoZero()
    {
        ProgramCacheStore.Save(new[] { new InstalledProgram { DisplayName = "Programa" } });
        Assert.True(ProgramCacheStore.TryLoad(out _));

        ProgramCacheStore.Invalidate();

        Assert.False(ProgramCacheStore.TryLoad(out var cache));
        Assert.Null(cache);
    }

    // ---------------------------------------------------------------
    // Ferramenta personalizada (17480 e 17484)
    // ---------------------------------------------------------------

    [Fact]
    public void FerramentaPersonalizadaGravaEVoltaDoDisco()
    {
        var status = CustomToolRegistry.Add(new CustomTool
        {
            Name = "Meu Editor",
            Command = "notepad.exe",
            Arguments = "arquivo.txt",
            Description = "Abre o bloco de notas",
        });

        Assert.Equal(CustomToolStatus.Ok, status);

        CustomToolRegistry.Reload();

        var lista = CustomToolRegistry.List();
        Assert.Single(lista);
        Assert.Equal("Meu Editor", lista[0].Name);
        Assert.Equal("notepad.exe", lista[0].Command);
        Assert.Equal("arquivo.txt", lista[0].Arguments);
    }

    [Fact]
    public void FerramentaComNomeRepetidoERecusada()
    {
        CustomToolRegistry.Add(new CustomTool { Name = "Meu Editor", Command = "notepad.exe" });

        var status = CustomToolRegistry.Add(new CustomTool { Name = "meu editor", Command = "calc.exe" });

        Assert.Equal(CustomToolStatus.AlreadyExists, status);
        Assert.Single(CustomToolRegistry.List());
    }

    [Fact]
    public void FerramentaSemNomeOuSemComandoERecusada()
    {
        Assert.Equal(CustomToolStatus.InvalidName,
            CustomToolRegistry.Add(new CustomTool { Name = "  ", Command = "notepad.exe" }));

        Assert.Equal(CustomToolStatus.InvalidCommand,
            CustomToolRegistry.Add(new CustomTool { Name = "Sem comando", Command = "  " }));

        Assert.Empty(CustomToolRegistry.List());
    }

    [Fact]
    public void FerramentaPodeSerRenomeada()
    {
        CustomToolRegistry.Add(new CustomTool { Name = "Antigo", Command = "notepad.exe" });

        Assert.Equal(CustomToolStatus.Ok, CustomToolRegistry.Rename("Antigo", "Novo"));

        Assert.True(CustomToolRegistry.TryGet("Novo", out var ferramenta));
        Assert.Equal("notepad.exe", ferramenta.Command);
        Assert.False(CustomToolRegistry.TryGet("Antigo", out _));
    }

    [Fact]
    public void RenomearParaUmNomeQueJaExisteERecusado()
    {
        CustomToolRegistry.Add(new CustomTool { Name = "Um", Command = "notepad.exe" });
        CustomToolRegistry.Add(new CustomTool { Name = "Dois", Command = "calc.exe" });

        Assert.Equal(CustomToolStatus.AlreadyExists, CustomToolRegistry.Rename("Um", "Dois"));

        // Nada mudou: o nome antigo continua valendo.
        Assert.True(CustomToolRegistry.TryGet("Um", out _));
        Assert.True(CustomToolRegistry.TryGet("Dois", out _));
    }

    [Fact]
    public void RenomearUmaFerramentaInexistenteDevolveNaoEncontrada()
    {
        Assert.Equal(CustomToolStatus.NotFound, CustomToolRegistry.Rename("Nao existe", "Outro"));
    }

    [Fact]
    public void FerramentaPodeSerRemovida()
    {
        CustomToolRegistry.Add(new CustomTool { Name = "Para remover", Command = "notepad.exe" });

        Assert.Equal(CustomToolStatus.Ok, CustomToolRegistry.Remove("Para remover"));
        Assert.Empty(CustomToolRegistry.List());

        Assert.Equal(CustomToolStatus.NotFound, CustomToolRegistry.Remove("Para remover"));
    }

    [Fact]
    public void AFerramentaPersonalizadaEntraNaListaDoWindowsTools()
    {
        CustomToolRegistry.Add(new CustomTool { Name = "Meu Editor", Command = "notepad.exe" });

        var ferramentas = CustomToolRegistry.ListAsWindowsTools();

        Assert.Single(ferramentas);
        Assert.Equal("Meu Editor", ferramentas[0].Name);
        Assert.Equal("custom", ferramentas[0].CategoryId);
    }

    // ---------------------------------------------------------------
    // Motores de busca configuraveis (1106)
    // ---------------------------------------------------------------

    [Fact]
    public void OCatalogoDeMotoresTemOsQuatroDoRevoMaisOYahoo()
    {
        var ids = new List<string>();
        foreach (var motor in WebSearchEngines.BuiltIn) ids.Add(motor.Id);

        Assert.Contains("Google", ids);
        Assert.Contains("Bing", ids);
        Assert.Contains("DuckDuckGo", ids);
        Assert.Contains("Yandex", ids);
        Assert.Contains("Yahoo", ids);
    }

    [Fact]
    public void OValorAntigoDoMotorContinuaValendo()
    {
        Assert.Equal("Bing", WebSearchEngines.Resolve("Bing").Id);
        Assert.Equal("DuckDuckGo", WebSearchEngines.Resolve("duck").Id);
        Assert.Equal("Google", WebSearchEngines.Resolve(string.Empty).Id);
        Assert.Equal("Google", WebSearchEngines.Resolve("motor desconhecido").Id);
    }

    [Fact]
    public void ABuscaUsaOValorAntigoQuandoAListaEstaVazia()
    {
        var settings = new AppSettings { WebSearchEngine = "Bing" };

        var url = WebSearch.BuildUrl("teste", settings);

        Assert.Contains("bing.com", url);
    }

    [Fact]
    public void APrimeiraEngineDaListaVence()
    {
        var settings = new AppSettings
        {
            WebSearchEngine = "Bing",
            WebSearchEngines = new List<string> { "DuckDuckGo", "Yandex" },
        };

        var url = WebSearch.BuildUrl("teste", settings);

        Assert.Contains("duckduckgo.com", url);
        Assert.DoesNotContain("bing.com", url);
    }

    [Fact]
    public void MotorPersonalizadoEhConstruido()
    {
        var definicao = WebSearchEngines.FormatCustom(
            "MeuBusca", "https://exemplo.teste/busca?termo={0}");

        Assert.False(string.IsNullOrWhiteSpace(definicao));
        Assert.True(WebSearchEngines.IsCustomDefinition(definicao));

        var motor = WebSearchEngines.TryParse(definicao);
        Assert.NotNull(motor);
        Assert.True(motor!.IsCustom);
        Assert.Equal("MeuBusca", motor.DisplayName);
        Assert.Contains("exemplo.teste", motor.BuildUrl("um programa"));
    }

    [Fact]
    public void MotorPersonalizadoSemMarcadorEhIgnorado()
    {
        Assert.Equal(string.Empty, WebSearchEngines.FormatCustom("MeuBusca", "https://exemplo.teste/busca"));
        Assert.Null(WebSearchEngines.TryParse("MeuBusca|https://exemplo.teste/busca"));
    }

    [Fact]
    public void AListaDeMotoresIgnoraDefinicoesInvalidasERepetidas()
    {
        var motores = WebSearchEngines.ResolveAll(new[] { "Bing", "Bing", "nao-existe", "DuckDuckGo" });

        Assert.Equal(2, motores.Count);
        Assert.Equal("Bing", motores[0].Id);
        Assert.Equal("DuckDuckGo", motores[1].Id);
    }

    // ---------------------------------------------------------------
    // Excel instalado (17480 do modulo de exportacao)
    // ---------------------------------------------------------------

    [Fact]
    public void AVerificacaoDoExcelNaoEstoura()
    {
        // A verificacao apenas pergunta ao Windows se o Excel existe: ela
        // nao abre o Excel e nao pode estourar em uma maquina sem Office.
        ExcelComExporter.IsExcelAvailable();
        ExcelComExporter.IsExcelAvailable();
    }

    [Fact]
    public void SemExcelInstaladoOExportDevolveMotivoClaro()
    {
        if (ExcelComExporter.IsExcelAvailable())
            return; // Com o Excel instalado, este caminho nao e o exercitado.

        var resultado = ExcelComExporter.Export(
            new[] { new InstalledProgram { DisplayName = "Programa" } },
            Path.Combine(_temp.Root, "lista.xlsx"));

        Assert.Equal(ExcelExportStatus.ExcelNotInstalled, resultado.Status);
        Assert.Contains("Excel", resultado.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(resultado.UsedInstalledExcel);
    }

    [Fact]
    public void SemExcelInstaladoOExportCaiNoFormatoSpreadsheetMl()
    {
        if (ExcelComExporter.IsExcelAvailable())
            return; // Com o Excel instalado, este caminho nao e o exercitado.

        var caminho = Path.Combine(_temp.Root, "lista.xml");
        var resultado = ExcelComExporter.ExportWithFallback(
            new[] { new InstalledProgram { DisplayName = "Programa", DisplayVersion = "1.0" } },
            caminho);

        Assert.Equal(ExcelExportStatus.Ok, resultado.Status);
        Assert.True(resultado.UsedFallback);
        Assert.False(resultado.UsedInstalledExcel);
        Assert.True(File.Exists(caminho));
        Assert.Contains("Programa", File.ReadAllText(caminho));
    }

    // ---------------------------------------------------------------
    // Papeis de cor que faltavam (16133 e 16150)
    // ---------------------------------------------------------------

    [Fact]
    public void OsPapeisDeCorDoBotaoDeComandoEDoDialogoExistem()
    {
        Assert.Contains(ThemeColorRole.CommandButtonText, ThemePalettes.Roles);
        Assert.Contains(ThemeColorRole.DialogBackground, ThemePalettes.Roles);
    }

    [Fact]
    public void TodosOsPapeisTemCorEmTodosOsTemas()
    {
        foreach (var tema in ThemeSkinCatalog.All)
        {
            var paleta = ThemePalettes.Of(tema.Id);

            foreach (var papel in ThemePalettes.Roles)
            {
                var cor = paleta.ColorOf(papel);
                Assert.False(string.IsNullOrWhiteSpace(cor),
                    $"O tema {tema.Id} nao tem cor para o papel {papel}.");
            }
        }
    }

    [Fact]
    public void TodoPapelTemDescricao()
    {
        foreach (var papel in ThemePalettes.Roles)
            Assert.False(string.IsNullOrWhiteSpace(ThemePalettes.DescribeRole(papel)));
    }

    [Fact]
    public void ACorPersonalizadaDoDialogoSubstituiATema()
    {
        var custom = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DialogBackground"] = "#FF123456",
        };

        var paleta = ThemePalettes.Dark.Merge(custom);

        Assert.Equal("#FF123456", paleta.ColorOf(ThemeColorRole.DialogBackground));
    }
}
