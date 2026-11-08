# Insere chaves novas nos catalogos de idioma, preservando o formato do arquivo.
#
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tools\lang-inserir.ps1
#
# O formato de chave|pt-BR|en-US e o mesmo de tools\lang-adicionar.ps1, e a
# fonte dos textos continua sendo tools\lang-novas.txt. A diferenca esta no
# jeito de gravar: este script NAO reescreve o arquivo inteiro (o ConvertTo-Json
# trocaria a formatacao alinhada do catalogo e poluiria o diff), ele apenas
# acrescenta as linhas novas antes do fechamento do objeto.
#
# Duas regras que o script garante, porque cada uma delas ja quebrou o
# catalogo uma vez:
#
#   - a ultima entrada do arquivo NAO tem virgula, e a ultima chave inserida
#     também nao (a virgula fica na entrada anterior);
#   - o texto de um valor nao pode conter o separador "|" (ele quebraria o
#     proprio formato da linha de dados).
[CmdletBinding()]
param(
    [string]$DataFile,
    [string]$LangDirectory
)

$ErrorActionPreference = 'Stop'

$toolRoot = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($toolRoot)) { $toolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path }
if ([string]::IsNullOrWhiteSpace($DataFile)) { $DataFile = Join-Path $toolRoot 'lang-novas.txt' }
if ([string]::IsNullOrWhiteSpace($LangDirectory)) {
    $LangDirectory = Join-Path (Split-Path $toolRoot -Parent) 'src\Tranal.Uninstaller\Lang'
}

if (-not (Test-Path -LiteralPath $DataFile)) { throw "Arquivo de dados ausente: $DataFile" }

function Escape-Json {
    param([string]$Value)
    return $Value.Replace('\', '\\').Replace('"', '\"')
}

function Inserir-Catalogo {
    param([string]$Path, [string]$Language, [hashtable]$Entries)

    if (-not (Test-Path -LiteralPath $Path)) { throw "Catalogo ausente: $Path" }

    $linhas = Get-Content -LiteralPath $Path -Encoding UTF8
    $texto = ($linhas -join "`r`n")

    $novas = @()
    foreach ($key in ($Entries.Keys | Sort-Object)) {
        if ($texto.Contains('"' + $key + '"')) { continue }

        $valor = if ($Language -eq 'en-US') { $Entries[$key].En } else { $Entries[$key].Pt }
        $novas += ,@($key, $valor)
    }

    if ($novas.Count -eq 0) {
        "  {0}: nada novo." -f (Split-Path $Path -Leaf)
        return
    }

    # A ultima linha e o fechamento do objeto.
    $ultima = $linhas[$linhas.Count - 1]
    if ($ultima.Trim() -ne '}') { throw "Formato inesperado em $Path (ultima linha: $ultima)" }
    $linhas = $linhas[0..($linhas.Count - 2)]

    # A entrada que era a ultima precisa de virgula: agora ela tem vizinhos.
    $anterior = $linhas[$linhas.Count - 1].TrimEnd()
    if (-not $anterior.EndsWith(',')) { $linhas[$linhas.Count - 1] = $anterior + ',' }

    for ($i = 0; $i -lt $novas.Count; $i++) {
        $key = $novas[$i][0]
        $valor = Escape-Json $novas[$i][1]
        $virgula = if ($i -lt $novas.Count - 1) { ',' } else { '' }
        $linhas += ('    "{0}":  "{1}"{2}' -f $key, $valor, $virgula)
    }

    $linhas += '}'

    $saida = $linhas -join "`r`n"
    if ($texto.EndsWith("`r`n")) { $saida += "`r`n" }
    [System.IO.File]::WriteAllText($Path, $saida, (New-Object System.Text.UTF8Encoding($false)))

    "  {0}: {1} chave(s) nova(s)." -f (Split-Path $Path -Leaf), $novas.Count
}

$entries = @{}
foreach ($line in Get-Content -LiteralPath $DataFile -Encoding UTF8) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    if ($line.TrimStart().StartsWith('#')) { continue }

    $parts = $line.Split('|')
    if ($parts.Count -ne 3) {
        throw ("Linha invalida (o texto do valor nao pode conter '|'): {0}" -f $line)
    }

    $key = $parts[0].Trim()
    $entries[$key] = @{ Pt = $parts[1]; En = $parts[2] }
}

"Inserindo $([System.IO.Path]::GetFileName($DataFile)) ->"
Inserir-Catalogo -Path (Join-Path $LangDirectory 'pt-BR.json') -Language 'pt-BR' -Entries $entries
Inserir-Catalogo -Path (Join-Path $LangDirectory 'en-US.json') -Language 'en-US' -Entries $entries
"Concluido."
