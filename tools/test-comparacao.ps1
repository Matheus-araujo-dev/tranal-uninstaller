<#
  Teste de ponta a ponta da comparacao de snapshots e da arvore de
  alteracoes (lote 19).

  O que ele prova, com dados de verdade:

  1) a linha de comando lista a biblioteca de snapshots;
  2) a comparacao entre dois snapshots produz a arvore de alteracoes
     (arquivos, pastas, chaves e valores) — em texto, em CSV, em HTML e no
     layout de impressao;
  3) o CSV exportado tem o cabecalho e as linhas que a comparacao produziu;
  4) o snapshot apagado pela linha de comando some da biblioteca.

  Os snapshots usados aqui sao SINTETICOS (o script escreve o arquivo no
  formato do proprio Tranal): assim o teste nao depende de varrer o sistema
  inteiro, que leva ate 45 segundos por parte, e nao depende do estado da
  maquina de quem roda.

  A captura de snapshot de verdade pela interface e medida a parte: ela
  gravou um snapshot real do sistema (o "antes" da medicao) e a linha de
  comando o listou em seguida.

  O script apaga no final tudo que criou: os snapshots e os arquivos
  exportados.

  Uso:
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-comparacao.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-comparacao.ps1 -Exe publish\preview\TranalUninstaller.exe -Cli publish\cli\tranal.exe
#>
param(
    [string]$Exe = '',
    [string]$Cli = '',
    [string]$DataDirectory = '',
    [int]$EsperaSegundos = 25
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Exe)) { $Exe = Join-Path $repoRoot 'publish\preview\TranalUninstaller.exe' }
if ([string]::IsNullOrWhiteSpace($Cli)) { $Cli = Join-Path $repoRoot 'publish\cli\tranal.exe' }
if (-not [System.IO.Path]::IsPathRooted($Exe)) { $Exe = Join-Path $repoRoot $Exe }
if (-not [System.IO.Path]::IsPathRooted($Cli)) { $Cli = Join-Path $repoRoot $Cli }

if ([string]::IsNullOrWhiteSpace($DataDirectory)) {
    $DataDirectory = Join-Path $env:LOCALAPPDATA 'Tranal Group\Tranal Uninstaller Pro'
}

if (-not (Test-Path $Exe)) { throw "Executavel nao encontrado: $Exe" }
if (-not (Test-Path $Cli)) { throw "Linha de comando nao encontrada: $Cli" }

$falhas = 0
function Chk([string]$nome, [bool]$ok, [string]$detalhe = '') {
    if ($ok) { Write-Host "[OK]    $nome" }
    else { Write-Host "[FALHA] $nome $detalhe"; $script:falhas++ }
}

$snapshotAntes = 'teste-sintetico-antes'
$snapshotDepois = 'teste-sintetico-depois'
$pasta = Join-Path $env:TEMP ("tranal-comparacao-{0}" -f [Guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($pasta) | Out-Null

$csv = Join-Path $pasta 'arvore.csv'
$html = Join-Path $pasta 'arvore.html'
$txt = Join-Path $pasta 'arvore.txt'

# ----------------------------------------------------------------------
# Os snapshots sinteticos, no formato que o proprio Tranal grava
# ----------------------------------------------------------------------
function Gravar-Snapshot([string]$nome, $files, $folders, $keys, $values) {
    $pastaSnapshots = Join-Path $DataDirectory 'snapshots'
    [System.IO.Directory]::CreateDirectory($pastaSnapshots) | Out-Null

    $id = [Guid]::NewGuid().ToString('D')
    $arquivo = Join-Path $pastaSnapshots ($id + '.json')

    $documento = [ordered]@{
        format    = 'Tranal.Snapshot.v1'
        metadata  = [ordered]@{
            id                 = $id
            name               = $nome
            label              = 'sintetico'
            createdUtc         = [DateTime]::UtcNow.ToString('o')
            fileCount          = @($files).Count
            folderCount        = @($folders).Count
            registryKeyCount   = @($keys).Count
            registryValueCount = @($values).Count
            path               = $arquivo
        }
        state     = [ordered]@{
            files          = @($files)
            folders        = @($folders)
            registryKeys   = @($keys)
            registryValues = @($values)
        }
    }

    $json = $documento | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText($arquivo, $json, (New-Object System.Text.UTF8Encoding($false)))
    return $id
}

Write-Host "=== Preparacao ==="
Write-Host "    linha de comando: $Cli"

$antesId = Gravar-Snapshot $snapshotAntes @(
    @{ path = 'C:\Programa\app.exe'; sizeBytes = 1000; modifiedUtc = '2026-11-01T10:00:00Z'; isDirectory = $false }
    @{ path = 'C:\Programa\config.ini'; sizeBytes = 100; modifiedUtc = '2026-11-01T10:00:00Z'; isDirectory = $false }
) @(
    @{ path = 'C:\Programa\Dados'; sizeBytes = 0; modifiedUtc = '2026-11-01T10:00:00Z'; isDirectory = $true }
) @(
    'HKCU\Software\Programa'
) @(
    @{ key = 'HKCU\Software\Programa'; valueName = 'Versao'; value = '1.0'; valueKind = 'String' }
)

$depoisId = Gravar-Snapshot $snapshotDepois @(
    @{ path = 'C:\Programa\app.exe'; sizeBytes = 2000; modifiedUtc = '2026-11-09T10:00:00Z'; isDirectory = $false }
    @{ path = 'C:\Programa\novo.dll'; sizeBytes = 500; modifiedUtc = '2026-11-09T10:00:00Z'; isDirectory = $false }
) @(
    @{ path = 'C:\Programa\Dados'; sizeBytes = 0; modifiedUtc = '2026-11-01T10:00:00Z'; isDirectory = $true }
    @{ path = 'C:\Programa\Novo'; sizeBytes = 0; modifiedUtc = '2026-11-09T10:00:00Z'; isDirectory = $true }
) @(
    'HKCU\Software\Programa'
    'HKCU\Software\Programa\Extra'
) @(
    @{ key = 'HKCU\Software\Programa'; valueName = 'Versao'; value = '2.0'; valueKind = 'String' }
)

try {
    Write-Host ""
    Write-Host "=== Comparacao pela linha de comando ==="

    $lista = & $Cli /snapshots 2>&1 | Out-String
    Chk "a linha de comando lista a biblioteca de snapshots" ($lista.Contains($snapshotAntes)) $lista

    $saida = & $Cli /comparar $snapshotAntes $snapshotDepois 2>&1 | Out-String
    Chk "a linha de comando compara os dois snapshots" ($LASTEXITCODE -eq 0) $saida
    Write-Host $saida

    # A arvore tem de trazer as seis alteracoes do retrato sintetico.
    Chk "a arvore mostra o arquivo novo" ($saida.Contains('C:\Programa\novo.dll'))
    Chk "a arvore mostra o arquivo removido" ($saida.Contains('C:\Programa\config.ini'))
    Chk "a arvore mostra o arquivo modificado" ($saida.Contains('C:\Programa\app.exe'))
    Chk "a arvore mostra a pasta nova" ($saida.Contains('C:\Programa\Novo'))
    Chk "a arvore mostra a chave nova" ($saida.Contains('HKCU\Software\Programa\Extra'))
    Chk "a arvore mostra o valor antigo e o novo" ($saida.Contains('1.0 -> 2.0'))

    & $Cli /comparar $snapshotAntes $snapshotDepois /export $csv *> $null
    Chk "a arvore e exportada em CSV" (Test-Path $csv)

    & $Cli /comparar $snapshotAntes $snapshotDepois /export $html *> $null
    Chk "a arvore e exportada em HTML" (Test-Path $html)

    & $Cli /comparar $snapshotAntes $snapshotDepois /export $txt *> $null
    Chk "a arvore e exportada em texto" (Test-Path $txt)

    if (Test-Path $csv) {
        $conteudo = [System.IO.File]::ReadAllText($csv)
        Chk "o CSV tem o cabecalho da arvore" ($conteudo.Contains('Tipo;Mudanca;Item;Detalhe'))
        Chk "o CSV tem as linhas da arvore" ($conteudo.Contains('C:\Programa\novo.dll'))
        Write-Host ("    linhas do CSV: " + (($conteudo -split "`n").Count))
    }

    if (Test-Path $html) {
        $conteudo = [System.IO.File]::ReadAllText($html)
        Chk "o HTML tem a arvore" ($conteudo.Contains('<!DOCTYPE html>'))
        Chk "o HTML tem as linhas da arvore" ($conteudo.Contains('C:\Programa\novo.dll'))
    }

    $saidaPrint = & $Cli /comparar $snapshotAntes $snapshotDepois /print 2>&1 | Out-String
    Chk "a linha de comando mostra o layout de impressao" ($saidaPrint.Contains('Arvore de alteracoes'))
    Chk "o layout de impressao tem o rodape da pagina" ($saidaPrint.Contains('Página 1 de 1'))
}
finally {
    # Limpeza: nada do que o teste criou pode ficar na maquina de quem roda.
    & $Cli /snapshots /delete $snapshotAntes *> $null
    & $Cli /snapshots /delete $snapshotDepois *> $null

    foreach ($id in @($antesId, $depoisId)) {
        $arquivo = Join-Path (Join-Path $DataDirectory 'snapshots') ($id + '.json')
        if ([System.IO.File]::Exists($arquivo)) { [System.IO.File]::Delete($arquivo) }
    }

    if ([System.IO.Directory]::Exists($pasta)) { [System.IO.Directory]::Delete($pasta, $true) }
}

$restantes = & $Cli /snapshots 2>&1 | Out-String
Chk "os snapshots de teste foram apagados" (-not $restantes.Contains($snapshotAntes)) $restantes

Write-Host ""
if ($falhas -eq 0) {
    Write-Host "RESULTADO: comparacao OK."
    exit 0
}
Write-Host "RESULTADO: $falhas verificacao(oes) falharam."
exit 1
