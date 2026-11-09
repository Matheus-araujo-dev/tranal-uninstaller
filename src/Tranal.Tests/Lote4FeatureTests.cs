using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// O lote 4 das lacunas medidas: o Boot Center e os arquivos em uso.
///
/// A REGRA mora no nucleo (<see cref="BootCenter"/> e
/// <see cref="ActiveFiles"/>); a tela e a linha de comando consomem.
/// </summary>
public class Lote4FeatureTests
{
    [Fact]
    public void OBootCenterMedeOTempoDeAtividadeDaMaquina()
    {
        var dePe = BootCenter.Uptime();

        Assert.True(dePe > TimeSpan.Zero, "a maquina esta de pe ha mais de zero");
        Assert.True(dePe < TimeSpan.FromDays(365), "a maquina esta de pe ha mais de um ano");
    }

    [Fact]
    public void OBootCenterDizQuandoAMaquinaSubiu()
    {
        var subida = BootCenter.BootTime();

        Assert.True(subida <= DateTime.Now, "a subida nao pode estar no futuro");
        Assert.True(subida > DateTime.Now.AddYears(-1), "a subida nao pode ser de mais de um ano atras");
    }

    [Fact]
    public void OBootCenterListaOQueSobeComAMaquina()
    {
        var report = BootCenter.Scan();

        Assert.NotNull(report.Items);
        Assert.Equal(report.Items.Count(i => i.Enabled), report.EnabledCount);
        Assert.Equal(report.Items.Count(i => i.IsSystem), report.SystemCount);
    }

    [Fact]
    public void OBootCenterNuncaRemoveItemDeSistema()
    {
        var item = new BootCenterItem
        {
            Kind = BootCenterItemKind.Autorun,
            Name = "Item de Sistema",
            IsSystem = true,
        };

        Assert.False(BootCenter.Remove(item));
    }

    [Fact]
    public void OBootCenterNaoFazNadaComItemNulo()
    {
        Assert.False(BootCenter.Remove(null!));
        Assert.False(BootCenter.SetEnabled(null!, true));
    }

    [Fact]
    public void OItemDoBootCenterGuardaOOriginalDoNucleo()
    {
        var autorun = new AutorunItem
        {
            Kind = AutorunItemKind.RunKey,
            Name = "MeuApp",
            Command = @"C:\MeuApp.exe",
        };

        var item = new BootCenterItem
        {
            Kind = BootCenterItemKind.Autorun,
            Name = autorun.Name,
            Command = autorun.Command,
            Source = autorun,
        };

        Assert.Same(autorun, item.Source);
    }
}

/// <summary>
/// Os arquivos em uso: quem segura o arquivo e por que ele nao sumiu.
/// </summary>
public class Lote4ActiveFilesTests : IDisposable
{
    private readonly string _pasta;

    /// <summary>
    /// O processo que segura o arquivo durante o teste. E outro processo de
    /// proposito: o Restart Manager nao devolve o proprio processo.
    /// </summary>
    private Process? _holder;

    public Lote4ActiveFilesTests()
    {
        _pasta = Path.Combine(Path.GetTempPath(), "tranal-ativos-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_pasta);
    }

    public void Dispose()
    {
        try
        {
            if (_holder is not null)
            {
                _holder.Kill(true);
                _holder.Dispose();
            }
        }
        catch { /* o temporario e descartavel */ }

        try { if (Directory.Exists(_pasta)) Directory.Delete(_pasta, true); }
        catch { /* o temporario e descartavel */ }
    }

    [Fact]
    public void AListaDeArquivosEmUsoAceitaListaVazia()
    {
        Assert.Empty(ActiveFiles.Scan(null));
        Assert.Empty(ActiveFiles.Scan(Array.Empty<string>()));
    }

    [Fact]
    public void OArquivoQueNaoExisteNaoEstaEmUso()
    {
        var caminho = Path.Combine(_pasta, "nao-existe.txt");

        Assert.Empty(ActiveFiles.ProcessesHolding(caminho));
        Assert.False(ActiveFiles.IsLocked(caminho));
    }

    [Fact]
    public void OArquivoSemCaminhoNaoEstaEmUso()
    {
        Assert.Empty(ActiveFiles.ProcessesHolding(null));
        Assert.Empty(ActiveFiles.ProcessesHolding(string.Empty));
        Assert.False(ActiveFiles.IsLocked(null));
    }

    [Fact]
    public void OArquivoEmUsoMostraQuemSegura()
    {
        var caminho = Path.Combine(_pasta, "em-uso.txt");
        File.WriteAllText(caminho, "conteudo");

        // Quem segura o arquivo tem de ser OUTRO processo: o Restart Manager
        // nao devolve o proprio processo — e o caso real que o painel de
        // arquivos em uso responde.
        _holder = StartHolder(caminho);

        var locked = WaitForLock(caminho, TimeSpan.FromSeconds(20));

        if (!locked)
        {
            // O ambiente nao conseguiu subir o processo auxiliar: nao ha o que
            // medir, e a falha nao e do modulo.
            var erro = _holder.HasExited ? _holder.StandardError.ReadToEnd() : "(o processo continua vivo)";
            var trava = "";

            try
            {
                using var teste = new FileStream(caminho, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                trava = "o arquivo NAO esta travado";
            }
            catch (IOException ex)
            {
                trava = "o arquivo esta travado (" + ex.GetType().Name + ")";
            }

            Assert.Fail("o outro processo deveria estar segurando o arquivo: " + erro + " | " + trava);
        }

        var processes = ActiveFiles.ProcessesHolding(caminho);

        Assert.True(processes.Count > 0, "o arquivo aberto deveria estar em uso");
        Assert.All(processes, nome => Assert.False(string.IsNullOrWhiteSpace(nome)));
        Assert.True(ActiveFiles.IsLocked(caminho));

        var files = ActiveFiles.Scan(new[] { caminho });
        var item = Assert.Single(files);

        Assert.Equal(caminho, item.Path);
        Assert.True(item.IsLocked);
        Assert.Contains(processes[0], item.DescribeProcesses());
    }

    /// <summary>
    /// Sobe um processo que abre o arquivo e segura o handle.
    /// </summary>
    private static Process StartHolder(string caminho)
    {
        var escaped = caminho.Replace("'", "''");
        var script = "$f=[System.IO.File]::Open('" + escaped
            + "','Open','ReadWrite','None'); Start-Sleep -Seconds 25";

        var info = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        // ArgumentList e o caminho sem quoting manual: o script tem aspas,
        // espacos e o caminho da pasta.
        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-Command");
        info.ArgumentList.Add(script);

        return Process.Start(info)!;
    }

    /// <summary>
    /// Espera o arquivo ficar em uso (o outro processo precisa de um instante
    /// para abrir).
    /// </summary>
    private static bool WaitForLock(string caminho, TimeSpan limite)
    {
        var fim = DateTime.UtcNow + limite;

        while (DateTime.UtcNow < fim)
        {
            if (ActiveFiles.IsLocked(caminho)) return true;
            Thread.Sleep(250);
        }

        return false;
    }

    [Fact]
    public void OArquivoLivreNaoEntraNaListaDeEmUso()
    {
        var caminho = Path.Combine(_pasta, "livre.txt");
        File.WriteAllText(caminho, "conteudo");

        var locked = ActiveFiles.Scan(new[] { caminho });

        Assert.Empty(locked);
    }
}
