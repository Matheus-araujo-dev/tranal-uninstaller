using System;
using System.IO;
using System.Linq;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Automation;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Tools;
using Xunit;

namespace Tranal.Tests;

/// <summary>
/// As features do lote 16: o manifesto que redireciona as DLLs de sistema,
/// o aviso sobre a qualidade do rastreio, o removedor de programas teimosos
/// e a descoberta dos dispositivos Bluetooth LE.
///
/// O que estes testes cobrem: o manifesto gerado e o mesmo que esta no disco
/// (uma regra so, dois lugares de verdade), o aviso do rastreio respeita o
/// limite de vezes, e o removedor mede o motivo em vez de adivinhar.
/// </summary>
public sealed class Lote16FeatureTests : IDisposable
{
    private readonly string _temporaria = Path.Combine(
        Path.GetTempPath(),
        "tranal-lote16-" + Guid.NewGuid().ToString("N")[..8]);

    public Lote16FeatureTests()
    {
        Directory.CreateDirectory(_temporaria);
        TraceQuality.FolderOverride = Path.Combine(_temporaria, "trace-quality");
    }

    public void Dispose()
    {
        TraceQuality.FolderOverride = null;

        try
        {
            if (Directory.Exists(_temporaria)) Directory.Delete(_temporaria, recursive: true);
        }
        catch
        {
            // A pasta temporaria que sobrou nao quebra o teste seguinte.
        }
    }

    // ------------------------------------------------------------------
    // O manifesto
    // ------------------------------------------------------------------

    [Fact]
    public void OManifestoDeclaraAsDllsDeSistema()
    {
        var manifesto = SxSManifest.Build();

        Assert.True(SxSManifest.IsValidXml(manifesto));
        Assert.Equal(6, SxSManifest.SystemDlls.Count);

        foreach (var dll in SxSManifest.SystemDlls)
        {
            Assert.Contains(dll, manifesto, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void OManifestoNoDiscoEOMQueOModuloGera()
    {
        var caminho = RepoFiles.Read("src", "Tranal.Uninstaller", "Assets", "tranal-sxs.manifest");
        var gerado = SxSManifest.Build();

        // O arquivo no disco e o que o modulo gera: uma regra so, dois
        // lugares de verdade.
        Assert.Equal(gerado.Replace("\r\n", "\n").Trim(), caminho.Replace("\r\n", "\n").Trim());
    }

    // ------------------------------------------------------------------
    // A qualidade do rastreio
    // ------------------------------------------------------------------

    [Fact]
    public void OAvisoDaQualidadeResponde()
    {
        var estado = TraceQuality.State();

        Assert.NotNull(estado);
        Assert.False(string.IsNullOrWhiteSpace(estado.Detail));
    }

    [Fact]
    public void OAvisoDaQualidadeTemLimiteDeVezes()
    {
        for (var i = 0; i < TraceQuality.MaxTimes + 1; i++)
        {
            TraceQuality.MarkAsked();
        }

        Assert.Equal(TraceQuality.MaxTimes + 1, TraceQuality.State().TimesAsked);
    }

    // ------------------------------------------------------------------
    // O removedor teimoso
    // ------------------------------------------------------------------

    [Fact]
    public void ORemovedorMedeOMotivoEmVezDeAdivinhar()
    {
        // Um caminho do sistema nao e removivel: e o proprio Windows que
        // protege, e o Tranal diz isso em vez de tentar.
        var sistema = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        Assert.Equal(StubbornReason.SystemPath, StubbornRemover.ReasonOf(sistema));

        // Um caminho que nao existe nao trava nada.
        Assert.Equal(StubbornReason.None, StubbornRemover.ReasonOf(Path.Combine(_temporaria, "nao-existe.txt")));
    }

    [Fact]
    public void ORemovedorTiraOMarcaDeSomenteLeitura()
    {
        var arquivo = Path.Combine(_temporaria, "somente-leitura.txt");
        File.WriteAllText(arquivo, "conteudo");
        File.SetAttributes(arquivo, FileAttributes.ReadOnly);

        Assert.Equal(StubbornReason.LockedFile, StubbornRemover.ReasonOf(arquivo));

        var resultado = StubbornRemover.Treat(arquivo, StubbornReason.LockedFile);

        Assert.True(resultado.Solved);
        Assert.Equal((FileAttributes)0, File.GetAttributes(arquivo) & FileAttributes.ReadOnly);
    }

    [Fact]
    public void ORemovedorDescreveAsAcoes()
    {
        Assert.NotEmpty(StubbornRemover.Actions);
        Assert.False(string.IsNullOrWhiteSpace(StubbornRemover.Describe()));
    }

    // ------------------------------------------------------------------
    // O Bluetooth
    // ------------------------------------------------------------------

    [Fact]
    public void OBluetoothLeEReconhecidoPeloServicoAnunciado()
    {
        // O servico 0x1800 (Generic Attribute) e da faixa LE.
        Assert.True(BluetoothLeDevices.IsLowEnergy(
            "USB\\VID_0000&PID_0000\\5&1a2b3c4d&0&2\\00001800-0000-1000-8000-00805f9b34fb"));

        // O servico 0x1101 (Serial Port) e do Bluetooth classico.
        Assert.False(BluetoothLeDevices.IsLowEnergy(
            "USB\\VID_0000&PID_0000\\5&1a2b3c4d&0&2\\00001101-0000-1000-8000-00805f9b34fb"));

        Assert.False(BluetoothLeDevices.IsLowEnergy(null));
        Assert.False(BluetoothLeDevices.IsLowEnergy("qualquer-coisa"));
    }

    [Fact]
    public void OBluetoothResponde()
    {
        var dispositivos = BluetoothLeDevices.List();

        Assert.NotNull(dispositivos);
        Assert.False(string.IsNullOrWhiteSpace(BluetoothLeDevices.Describe()));
    }
}
