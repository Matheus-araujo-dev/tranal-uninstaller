param(
    # Roda a cadeia completa da auditoria: RECON -> catalogo -> mapa.
    #
    # A ORDEM IMPORTA e nao e preferencia estetica: o mapa de reconciliacao
    # ACUMULA por competidor e a medicao mais recente vence por linha do
    # catalogo. A ordem abaixo foi RESOLVIDA por restricao de precedencia
    # contra o mapa publicado (docs/auditoria/reconciliacao.json) e
    # reproduz as 506 entradas do mapa exatamente - mesmo estado, mesmo
    # numero de linha do RECON e mesmo modo de casamento.
    #
    # Trocar a ordem muda o resultado: e por isso que a ordem esta escrita
    # aqui, no repositorio, e nao na memoria de quem roda.
    [switch]$Silencioso
)

$ErrorActionPreference = 'Stop'

$raiz = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$docs = Join-Path $raiz 'docs\auditoria'
$aplicar = Join-Path $raiz 'tools\auditoria\aplicar-recon.ps1'

$pairs = @(
    @('RECON-ASHAMPOO-TOTALUNINSTALL.md', 'ASHAMPOO.md', 'ASHAMPOO'),
    @('RECON-ASHAMPOO-TOTALUNINSTALL-UNINSTALLTOOL-SOFTORGANIZER-WISE.md', 'ASHAMPOO.md', 'ASHAMPOO'),
    @('RECON-A-2026-11-08.md', 'ASHAMPOO.md', 'ASHAMPOO'),
    @('RECON-FEATURES-NOVAS-2026-11-08.md', 'ASHAMPOO.md', 'Ashampoo'),
    @('RECON-B-2026-11-08.md', 'BCU.md', 'BCU'),
    @('RECON-HIBIT-GEEK-IOBIT-UNINSTALR.md', 'GEEK.md', 'GEEK'),
    @('RECON-RODADA-2026-11-08.md', 'GEEK.md', 'Geek'),
    @('RECON-CATALOGO-HIBIT-GEEK.md', 'GEEK.md', 'GEEK'),
    @('RECON-A-2026-11-08.md', 'GEEK.md', 'GEEK'),
    @('RECON-FEATURES-NOVAS-2026-11-08.md', 'GEEK.md', 'Geek'),
    @('RECON-HIBIT-GEEK-IOBIT-UNINSTALR.md', 'HIBIT.md', 'HIBIT'),
    @('RECON-RODADA-2026-11-08.md', 'HIBIT.md', 'HiBit'),
    @('RECON-CATALOGO-HIBIT-GEEK.md', 'HIBIT.md', 'HIBIT'),
    @('RECON-B-2026-11-08.md', 'HIBIT.md', 'HIBIT'),
    @('RECON-FEATURES-NOVAS-2026-11-08.md', 'HIBIT.md', 'HiBit'),
    @('RECON-HIBIT-GEEK-IOBIT-UNINSTALR.md', 'IOBIT.md', 'IOBIT'),
    @('RECON-C-2026-11-08.md', 'IOBIT.md', 'IOBIT'),
    @('RECON-FEATURES-NOVAS-2026-11-08.md', 'IOBIT.md', 'IObit'),
    @('RECON-RODADA-2026-11-08.md', 'IOBIT.md', 'IObit'),
    @('RECON-UNINSTALLTOOL-SOFTORGANIZER-WISE.md', 'SOFTORGANIZER.md', 'Soft Organizer'),
    @('RECON-C-2026-11-08.md', 'SOFTORGANIZER.md', 'SOFTORGANIZER'),
    @('RECON-ASHAMPOO-TOTALUNINSTALL.md', 'TOTALUNINSTALL.md', 'Total Uninstall'),
    @('RECON-D-2026-11-08.md', 'TOTALUNINSTALL.md', 'TOTALUNINSTALL'),
    @('RECON-FEATURES-NOVAS-2026-11-08.md', 'TOTALUNINSTALL.md', 'Total Uninstall'),
    @('RECON-UNINSTALLTOOL-SOFTORGANIZER-WISE.md', 'UNINSTALLTOOL.md', 'Uninstall Tool'),
    @('RECON-D-2026-11-08.md', 'UNINSTALLTOOL.md', 'UNINSTALLTOOL'),
    @('RECON-HIBIT-GEEK-IOBIT-UNINSTALR.md', 'UNINSTALR.md', 'UNINSTALR'),
    @('RECON-RODADA-2026-11-08.md', 'UNINSTALR.md', 'Uninstalr'),
    @('RECON-E-2026-11-08.md', 'UNINSTALR.md', 'UNINSTALR'),
    @('RECON-UNINSTALLTOOL-SOFTORGANIZER-WISE.md', 'WISE.md', 'Wise'),
    @('RECON-E-2026-11-08.md', 'WISE.md', 'WISE')

)

foreach ($p in $pairs) {
    $recon = Join-Path $docs $p[0]
    $catalogo = Join-Path $docs $p[1]

    if (-not (Test-Path -LiteralPath $recon)) {
        throw "RECON nao encontrado: $($p[0])"
    }
    if (-not (Test-Path -LiteralPath $catalogo)) {
        throw "Catalogo nao encontrado: $($p[1])"
    }

    $saida = & powershell -NoProfile -ExecutionPolicy Bypass -File $aplicar `
        -Recon $recon -Catalogo $catalogo -Secao $p[2] 2>&1

    $primeira = ($saida | Select-Object -First 1)
    if (-not $Silencioso) {
        Write-Output ("{0} -> {1}: {2}" -f $p[0], $p[1], $primeira)
    }
}

if (-not $Silencioso) {
    Write-Output "Cadeia concluida. Proxima etapa: extrair-catalogo.ps1, provar-paridade.ps1, gerar-relatorio.py"
}
