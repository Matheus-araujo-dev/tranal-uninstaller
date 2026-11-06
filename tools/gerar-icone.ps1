# Gera o icone do Tranal Uninstaller (.ico multi-resolucao).
#
# O icone e GERADO, nao importado: o desenho mora neste script, entao ele
# pode ser refinado e reproduzido a qualquer momento ("tools/gerar-icone.ps1").
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\gerar-icone.ps1
#
# Saida: src\Tranal.Uninstaller\Assets\Tranal.ico
#
# O desenho: um quadrado arredondado com gradiente (o "cartao" do programa) e
# dentro dele uma lixeira em branco, geometrica e sem detalhe fino — e o que
# continua legivel a 16 pixels, que e onde o icone costuma ser pior.
#
# Nota de sintaxe: aqui as constructions usam "::new(...)" e nao
# "New-Object Tipo(...)". Com o par de parenteses do New-Object, o PowerShell
# trata "($a * b, $c * d)" como um unico argumento-array e a multiplicacao
# quebra ("op_Multiply" em Object[]).

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$raiz = Split-Path -Parent $PSScriptRoot
$destino = Join-Path $raiz 'src\Tranal.Uninstaller\Assets\Tranal.ico'
$dirDestino = Split-Path -Parent $destino
if (-not (Test-Path $dirDestino)) { [System.IO.Directory]::CreateDirectory($dirDestino) | Out-Null }

$larguras = @(16, 24, 32, 48, 64, 128, 256)

function New-Badge {
    param([int]$Tamanho)

    $bmp = [System.Drawing.Bitmap]::new($Tamanho, $Tamanho, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $f = [float]$Tamanho

    # --- o cartao arredondado ---
    # Cada arco de canto tem diametro igual ao DOIS vezes o raio: e o que
    # fecha o retangulo arredondado. Usar o diametro inteiro da imagem
    # produz uma ampulheta em vez de um cartao.
    $raio = $f * 0.22
    $d = $raio * 2
    $caminho = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $caminho.AddArc(0, 0, $d, $d, 180, 90)
    $caminho.AddArc($f - $d, 0, $d, $d, 270, 90)
    $caminho.AddArc($f - $d, $f - $d, $d, $d, 0, 90)
    $caminho.AddArc(0, $f - $d, $d, $d, 90, 90)
    $caminho.CloseFigure()

    $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.Point]::new(0, 0),
        [System.Drawing.Point]::new(0, $Tamanho),
        [System.Drawing.Color]::FromArgb(255, 58, 140, 255),
        [System.Drawing.Color]::FromArgb(255, 14, 79, 168))
    $g.FillPath($brush, $caminho)

    # --- a lixeira, em branco ---
    $branco = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 255, 255, 255))

    # tampa
    $tampaL = $f * 0.24
    $tampaR = $f * 0.76
    $g.FillRectangle($branco, $tampaL, $f * 0.30, $tampaR - $tampaL, $f * 0.085)

    # alca
    $alcaL = $f * 0.41
    $alcaR = $f * 0.59
    $g.FillRectangle($branco, $alcaL, $f * 0.205, $alcaR - $alcaL, $f * 0.075)

    # corpo (trapezio)
    $corpo = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $corpo.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new($f * 0.285, $f * 0.415),
        [System.Drawing.PointF]::new($f * 0.715, $f * 0.415),
        [System.Drawing.PointF]::new($f * 0.645, $f * 0.805),
        [System.Drawing.PointF]::new($f * 0.355, $f * 0.805)
    ))
    $g.FillPath($branco, $corpo)

    # frestas: so a partir de 24 px, abaixo disso viraria borra
    if ($Tamanho -ge 24) {
        $fresta = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 14, 79, 168))
        $g.FillRectangle($fresta, $f * 0.415, $f * 0.49, $f * 0.05, $f * 0.235)
        $g.FillRectangle($fresta, $f * 0.535, $f * 0.49, $f * 0.05, $f * 0.235)
    }

    $g.Dispose()
    return $bmp
}

# --- monta o .ico (formato ICO com payload PNG, aceito pelo Windows Vista+) ---
$imagens = @()
foreach ($t in $larguras) {
    $bmp = New-Badge -Tamanho $t
    $ms = [System.IO.MemoryStream]::new()
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $imagens += ,@{ Bytes = $ms.ToArray(); Size = $t }
    $ms.Dispose()
    $bmp.Dispose()
}

$offset = 6 + ($imagens.Count * 16)
$ms = [System.IO.MemoryStream]::new()
$w = [System.IO.BinaryWriter]::new($ms)

$w.Write([uint16]0)                       # reservado
$w.Write([uint16]1)                       # tipo: 1 = icone
$w.Write([uint16]$imagens.Count)

foreach ($img in $imagens) {
    $b = $img.Bytes
    $dim = if ($img.Size -ge 256) { 0 } else { $img.Size }
    $w.Write([byte]$dim)                   # largura (0 = 256)
    $w.Write([byte]$dim)                   # altura
    $w.Write([byte]0)                      # cores da paleta (0 = truecolor)
    $w.Write([byte]0)                      # reservado
    $w.Write([uint16]1)                    # planos
    $w.Write([uint16]32)                   # bits por pixel
    $w.Write([uint32]$b.Length)            # tamanho do dado
    $w.Write([uint32]$offset)              # deslocamento
    $offset += $b.Length
}

foreach ($img in $imagens) { $w.Write($img.Bytes) }
$w.Flush()
$w.Close()

[System.IO.File]::WriteAllBytes($destino, $ms.ToArray())
$ms.Dispose()

$tam = (Get-Item $destino).Length
Write-Output ("Icone gravado: {0} ({1} bytes, {2} resolucoes)" -f $destino, $tam, $larguras.Count)
