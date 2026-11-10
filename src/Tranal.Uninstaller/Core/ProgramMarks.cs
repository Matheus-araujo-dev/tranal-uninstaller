using System;
using System.Collections.Generic;
using Tranal.Uninstaller.Core.Discovery;
using Tranal.Uninstaller.Core.Localization;

namespace Tranal.Uninstaller.Core;

/// <summary>
/// A marca que o programa carrega: o que a lista precisa dizer sobre ele
/// antes de o usuario mexer.
///
/// Cada marca e um **fato medido**: o proprio programa, o proprio registro ou
/// o proprio disco dizem. Nada aqui e palpite — quando o Tranal nao sabe, a
/// marca nao existe.
/// </summary>
public enum ProgramMark
{
    /// <summary>Programa de 32 bits em um Windows de 64 bits.</summary>
    ThirtyTwoBits,

    /// <summary>O programa e uma atualizacao de outro aplicativo.</summary>
    Update,

    /// <summary>
    /// A entrada nao veio do registro: o executavel foi achado no disco, sem
    /// entrada de desinstalacao que o registre.
    /// </summary>
    Orphan,

    /// <summary>
    /// O desinstalador registrado aponta para um arquivo que nao existe mais.
    /// </summary>
    Invalid,

    /// <summary>
    /// O proprio programa pede para nao ser removido (a tag <c>NoRemove</c>).
    /// </summary>
    Protected,
}

/// <summary>
/// O que o Tranal entendeu sobre um programa, em marcas.
///
/// A REGRA mora em <see cref="ProgramMarks"/>: a lista, a exportacao e a
/// linha de comando consomem o resultado e nunca montam a marcacao por conta
/// propria.
/// </summary>
public sealed class ProgramMarkInfo
{
    /// <summary>True quando o programa e de 32 bits num Windows de 64 bits.</summary>
    public bool Is32Bits { get; init; }

    /// <summary>True quando o programa e uma atualizacao de outro aplicativo.</summary>
    public bool IsUpdate { get; init; }

    /// <summary>True quando a entrada nao veio do registro.</summary>
    public bool IsOrphan { get; init; }

    /// <summary>True quando o desinstalador aponta para um arquivo inexistente.</summary>
    public bool IsInvalid { get; init; }

    /// <summary>True quando o proprio programa pede para nao ser removido.</summary>
    public bool IsProtected { get; init; }

    /// <summary>
    /// Por que o programa foi entendido como atualizacao. Vazio quando ele
    /// nao e uma atualizacao.
    /// </summary>
    public string UpdateDetail { get; init; } = string.Empty;

    /// <summary>As marcas, na ordem em que a tela mostra.</summary>
    public IReadOnlyList<ProgramMark> Marks { get; init; } = Array.Empty<ProgramMark>();

    /// <summary>True quando o programa carrega pelo menos uma marca.</summary>
    public bool Any => Marks.Count > 0;
}

/// <summary>
/// As marcas do programa.
///
/// Tudo o que a lista precisa dizer sobre um programa antes de o usuario
/// mexer nele ja era medido pelo proprio Windows: a tag <c>NoRemove</c> do
/// registro, o comando de desinstalacao apontando para um arquivo que nao
/// existe mais, o valor <c>IsUpgrade</c>, a maquina do executavel. O que
/// faltava era a **camada que mostra**.
///
/// A regra esta aqui, em um lugar so: a coluna "Marcas" da lista, o nome
/// exibido e a exportacao consomem este resultado.
/// </summary>
public static class ProgramMarks
{
    /// <summary>
    /// As marcas do programa.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    public static ProgramMarkInfo Detect(InstalledProgram? program)
    {
        if (program is null) return new ProgramMarkInfo();

        // A maquina do executavel foi lida no disco pelo scanner. A marca so
        // existe quando o Windows e de 64 bits: em um Windows de 32 bits,
        // todo programa e de 32 bits e a marca nao diria nada.
        var is32Bits = string.Equals(program.Bits, "32-bits", StringComparison.OrdinalIgnoreCase)
            && Environment.Is64BitOperatingSystem;

        // A atualizacao tem detector proprio (o registro, o nome, a pasta e o
        // comando falam). A regra dele nao e reescrita aqui.
        var atualizacao = UpdateMarker.Detect(program);

        var isOrphan = IsOrphan(program);
        var isInvalid = program.UninstallerHealth == UninstallerHealth.Broken;
        var isProtected = program.IsProtected;

        var marcas = new List<ProgramMark>();
        if (is32Bits) marcas.Add(ProgramMark.ThirtyTwoBits);
        if (atualizacao.IsUpdate) marcas.Add(ProgramMark.Update);
        if (isOrphan) marcas.Add(ProgramMark.Orphan);
        if (isInvalid) marcas.Add(ProgramMark.Invalid);
        if (isProtected) marcas.Add(ProgramMark.Protected);

        return new ProgramMarkInfo
        {
            Is32Bits = is32Bits,
            IsUpdate = atualizacao.IsUpdate,
            IsOrphan = isOrphan,
            IsInvalid = isInvalid,
            IsProtected = isProtected,
            UpdateDetail = atualizacao.Detail,
            Marks = marcas,
        };
    }

    /// <summary>
    /// True quando a entrada nao veio do registro: o executavel foi achado no
    /// disco, sem entrada de desinstalacao que o registre.
    ///
    /// E o "orfao" do BCU: um executavel que parece desinstalador e que
    /// ninguem registrou.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    public static bool IsOrphan(InstalledProgram? program)
    {
        if (program is null) return false;
        if (program.IsWindowsInstaller || program.IsMsi) return false;
        if (program.Kind == UninstallerKind.StoreApp) return false;

        return string.IsNullOrWhiteSpace(program.RegistryPath)
            && string.IsNullOrWhiteSpace(program.RegistryKey);
    }

    /// <summary>
    /// O texto da coluna "Marcas": as etiquetas traduzidas, separadas por
    /// virgula. Vazio quando o programa nao carrega nenhuma marca.
    /// </summary>
    /// <param name="program">O programa inspecionado.</param>
    public static string Text(InstalledProgram? program)
    {
        var info = Detect(program);
        if (!info.Any) return string.Empty;

        var partes = new List<string>();
        foreach (var marca in info.Marks) partes.Add(Label(marca));

        return string.Join(", ", partes);
    }

    /// <summary>
    /// O rotulo traduzido de uma marca.
    /// </summary>
    /// <param name="mark">A marca.</param>
    public static string Label(ProgramMark mark) => Loc.Get("ProgramMark." + Chave(mark));

    /// <summary>
    /// O nome exibido, com a marca de 32 bits embutida.
    ///
    /// E o que o Uninstall Tool faz: o programa de 32 bits aparece como
    /// "Nome (32-bit)". A identidade do programa (a chave de registro, o
    /// nome cru) nao muda — o que muda e o texto que a pessoa le.
    /// </summary>
    /// <param name="name">O nome do programa.</param>
    /// <param name="info">As marcas do programa.</param>
    public static string WithBits(string name, ProgramMarkInfo? info)
    {
        if (info is null || !info.Is32Bits) return name;
        if (string.IsNullOrWhiteSpace(name)) return name;

        return name + " " + Loc.Get("ProgramMark.BitsSuffix");
    }

    /// <summary>
    /// A chave da marca no catalogo de idiomas.
    /// </summary>
    /// <param name="mark">A marca.</param>
    private static string Chave(ProgramMark mark) => mark switch
    {
        ProgramMark.ThirtyTwoBits => "32Bits",
        ProgramMark.Update => "Update",
        ProgramMark.Orphan => "Orphan",
        ProgramMark.Invalid => "Invalid",
        ProgramMark.Protected => "Protected",
        _ => "Update",
    };
}
