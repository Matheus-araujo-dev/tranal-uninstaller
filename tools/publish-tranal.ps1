<#
  Publica o Tranal Uninstaller com verificacao do manifesto embutido.

  Por que existe: no publish single-file, o apphost gerado (com o RT_MANIFEST)
  fica CACHED no obj intermediate. Trocar o valor de ApplicationManifest sem
  limpar o obj faz o exe seguinte sair com o manifesto do publish anterior —
  foi assim que um build requireAdministrator saiu como asInvoker.

  Variantes:
    gui     -> publish\gui      (requireAdministrator, autocontido)
    preview -> publish\preview  (asInvoker, so para inspecao visual)
    cli     -> publish\cli      (a mesma ferramenta em linha de comando)
    setup   -> publish\setup    (o instalador, com o programa leve + o runtime
                                  do .NET embutido; nada externo e preciso)

  Uso:
    publish-tranal.ps1 -Variant gui
    publish-tranal.ps1 -Variant setup
#>
param(
    [ValidateSet('gui', 'preview', 'cli', 'setup')]
    [string]$Variant = 'gui',

    # Versao do .NET Desktop Runtime embutida no instalador.
    [string]$RuntimeVersion = '10.0.14',

    # Certificado que assina o instalador. Se vier vazio, o certificado de
    # assinatura da Tranal no repositorio de certificados do usuario e usado.
    [string]$SigningThumbprint = ''
)

$ErrorActionPreference = 'Stop'

# A raiz do repositorio e o proprio projeto (src\Tranal.Uninstaller, src\Tranal.Cli).
# Cuidado: o PowerShell nao diferencia maiusculas de minusculas em nomes de variavel
# — nao use $repoRoot e $repoRoot2 nem $proj e $Proj no mesmo script.
$repoRoot = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $repoRoot 'src\Tranal.Uninstaller'
$cliProj = Join-Path $repoRoot 'src\Tranal.Cli'

function Reset-Intermediate([string]$projectDir) {
    $obj = Join-Path $projectDir 'obj'
    if (Test-Path $obj) {
        # A pasta de destino precisa existir: Move-Item nao cria diretorios
        # intermediarios e falha com "parte do caminho nao foi encontrada".
        $stashRoot = Join-Path $repoRoot 'publish'
        if (-not (Test-Path $stashRoot)) { [void][System.IO.Directory]::CreateDirectory($stashRoot) }

        # Um unico stash: com um nome por execucao, as pastas _obj-* se
        # acumulavam a cada publish e nenhuma era removida.
        $stash = Join-Path $stashRoot '_obj-anterior'
        if (Test-Path $stash) {
            try { [System.IO.Directory]::Delete($stash, $true) } catch { }
        }

        Move-Item $obj $stash -Force
        Write-Host "obj anterior movido para $stash"
    }
}

# O redistribuivel do .NET Desktop Runtime. O WPF precisa dele; o programa
# instalado e a versao leve (1,3 MB), entao o runtime viaja dentro do setup.
# O download fica em cache: o instalador e compilado varias vezes por rodada.
function Get-RuntimeRedist {
    $cache = Join-Path $repoRoot 'publish\_cache'
    if (-not (Test-Path $cache)) { [void][System.IO.Directory]::CreateDirectory($cache) }

    $file = Join-Path $cache "windowsdesktop-runtime-$RuntimeVersion-win-x64.exe"
    if (Test-Path $file) {
        Write-Host "runtime ja em cache: $file"
        return $file
    }

    $url = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/$RuntimeVersion/windowsdesktop-runtime-$RuntimeVersion-win-x64.exe"
    Write-Host "baixando o runtime: $url"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $url -OutFile $file -UseBasicParsing -TimeoutSec 600

    if (-not (Test-Path $file)) { throw "Falha ao baixar o redistribuivel do runtime" }
    Write-Host ("runtime baixado: {0:N0} bytes" -f (Get-Item $file).Length)
    return $file
}

# O compilador do Inno Setup. Ele nao vem no PATH (o winget instala por
# usuario): procura nos lugares known.
function Get-Iscc {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe'
    )

    foreach ($c in $candidates) {
        if (Test-Path $c) { return $c }
    }

    $onPath = Get-Command iscc -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    throw 'Inno Setup nao encontrado (ISCC.exe). Instale com: winget install JRSoftware.InnoSetup'
}

# O certificado que assina o instalador. A chave privada NUNCA entra no
# repositorio do projeto: no CI ela chega por variavel de ambiente (secret),
# na maquina de desenvolvimento ela fica no repositorio de certificados do
# usuario.
function Get-CertificadoDeAssinatura([string]$thumbprint) {
    if ($thumbprint) {
        $cert = Get-Item "Cert:\CurrentUser\My\$thumbprint" -ErrorAction SilentlyContinue
        if ($cert) { return $cert }
    }

    # No CI o certificado chega como .pfx em base64 no ambiente.
    $pfxBase64 = $env:TRANAL_CERT_PFX
    $senha = $env:TRANAL_CERT_PASSWORD
    if ($pfxBase64) {
        $arquivo = Join-Path ([IO.Path]::GetTempPath()) 'tranal-codesign.pfx'
        [IO.File]::WriteAllBytes($arquivo, [Convert]::FromBase64String($pfxBase64))
        $segura = ConvertTo-SecureString $senha -AsPlainText -Force
        $cert = Import-PfxCertificate -FilePath $arquivo -CertStoreLocation Cert:\CurrentUser\My -Password $segura
        return $cert
    }

    # Na maquina de desenvolvimento: o certificado da Tranal ja instalado.
    # A regra e "o certificado com O=Tranal no titular": e o que distingue o
    # certificado de assinatura da Tranal de qualquer outro certificado de
    # teste que esteja na loja.
    $candidatos = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -like '*Tranal*' -and $_.HasPrivateKey }
    $cert = $candidatos | Where-Object { $_.Subject -like '*O=Tranal*' } | Select-Object -First 1
    if (-not $cert) { $cert = $candidatos | Select-Object -First 1 }
    return $cert
}

# Assina o instalador. O "UnknownError" que o Windows devolve no status e o
# esperado: a raiz e autossinada e nao esta nas authorities confiáveis da
# maquina. O que interessa e o assinante: tem de ser a Tranal.
function Assinar-Instalador([string]$arquivo, [string]$thumbprint) {
    $cert = Get-CertificadoDeAssinatura $thumbprint
    if (-not $cert) {
        Write-Host "sem certificado de assinatura: o instalador sera publicado sem assinatura"
        return
    }

    Set-AuthenticodeSignature -FilePath $arquivo -Certificate $cert -HashAlgorithm SHA256 | Out-Null

    $assinante = (Get-AuthenticodeSignature $arquivo).SignerCertificate
    if (-not $assinante -or $assinante.Subject -notlike '*Tranal*') {
        throw "A assinatura do instalador nao foi aplicada"
    }
    Write-Host "instalador assinado por: $($assinante.Subject)"
}

Push-Location $repoRoot
try {
    switch ($Variant) {
        'gui' {
            Reset-Intermediate $proj
            dotnet publish $proj\Tranal.Uninstaller.csproj -c Release -r win-x64 --self-contained true `
                -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
                -o publish\gui
            if ($LASTEXITCODE -ne 0) { throw "Falha no publish do GUI (exit $LASTEXITCODE)" }
            $expected = 'requireAdministrator'
            $target = 'publish\gui\TranalUninstaller.exe'
        }
        'preview' {
            Reset-Intermediate $proj
            dotnet publish $proj\Tranal.Uninstaller.csproj -c Release -r win-x64 --self-contained true `
                -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
                -p:ApplicationManifest=app-preview.manifest `
                -o publish\preview
            if ($LASTEXITCODE -ne 0) { throw "Falha no publish do preview (exit $LASTEXITCODE)" }
            $expected = 'asInvoker'
            $target = 'publish\preview\TranalUninstaller.exe'
        }
        'cli' {
            Reset-Intermediate $cliProj
            dotnet publish $cliProj\Tranal.Cli.csproj -c Release -r win-x64 --self-contained true `
                -p:PublishSingleFile=true `
                -o publish\cli
            if ($LASTEXITCODE -ne 0) { throw "Falha no publish da CLI (exit $LASTEXITCODE)" }
            $expected = $null
            $target = 'publish\cli\tranal.exe'
        }
        'setup' {
            # O programa instalado e a versao leve: o runtime do .NET viaja
            # dentro do setup (60 MB) e so e instalado quando falta. O
            # resultado: o setup e menor que o pacote autocontido e o
            # programa instalado ocupa 1,3 MB em vez de 132 MB.
            Reset-Intermediate $proj
            dotnet publish $proj\Tranal.Uninstaller.csproj -c Release -r win-x64 --self-contained false `
                -p:PublishSingleFile=false `
                -o publish\setup-payload
            if ($LASTEXITCODE -ne 0) { throw "Falha no publish do payload do setup (exit $LASTEXITCODE)" }

            $runtime = Get-RuntimeRedist
            $iscc = Get-Iscc

            # A versao vem do proprio projeto: um numero escrito a mao no
            # script de publicacao seria uma segunda fonte da verdade.
            $versao = (Select-String -Path (Join-Path $proj 'Tranal.Uninstaller.csproj') `
                -Pattern '<Version>([^<]+)</Version>').Matches[0].Groups[1].Value

            # A senha do instalador vem do ambiente (e nao da linha de
            # comando: a linha de comando de qualquer processo e visivel).
            # O .iss le a mesma variavel — a regra fica em um lugar so.
            $senha = $env:TRANAL_SETUP_PASSWORD
            if ($senha) {
                if ($senha.Contains('"')) {
                    throw 'A senha do instalador nao pode conter aspas duplas.'
                }
                if ($senha -match "[`r`n]") {
                    throw 'A senha do instalador nao pode conter quebra de linha.'
                }
                if ($senha.Length -lt 6) {
                    throw 'A senha do instalador precisa de pelo menos 6 caracteres.'
                }
                Write-Host 'instalador: com senha (o conteudo do setup sera criptografado)'
            }
            else {
                Write-Host 'instalador: sem senha (TRANAL_SETUP_PASSWORD nao definida)'
            }

            $iss = Join-Path $repoRoot 'installer\tranal.iss'
            $defines = @(
                "/DAppPayloadDir=$repoRoot\publish\setup-payload",
                "/DRuntimeFile=$runtime",
                "/DRuntimeFileName=$([System.IO.Path]::GetFileName($runtime))",
                "/DOutputDir=$repoRoot\publish\setup",
                "/DAppVersion=$versao",
                "/DLicenseFile=$repoRoot\LICENSE",
                "/DRepoRoot=$repoRoot"
            )

            & $iscc $defines $iss
            if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar o instalador (exit $LASTEXITCODE)" }

            # A assinatura vem depois da compilacao: assinar antes faria o
            # Inno Setup regravar o executable e a assinatura seria perdida.
            Assinar-Instalador (Join-Path $repoRoot 'publish\setup\TranalUninstaller-Setup.exe') $SigningThumbprint

            $expected = $null
            $target = 'publish\setup\TranalUninstaller-Setup.exe'
        }
    }

    if ($expected) {
        $env:PYTHONIOENCODING = 'utf-8'
        $level = (& python (Join-Path $repoRoot 'tools\dump_manifest.py') $target |
            Select-String -Pattern 'requestedExecutionLevel' |
            ForEach-Object { if ($_ -match 'level="([^"]+)"') { $Matches[1] } } | Select-Object -First 1)
        Write-Host "manifesto: $level (esperado: $expected)"
        if ($level -ne $expected) {
            throw "MANIFESTO DIVERGENTE: obtido '$level', esperado '$expected'"
        }
    }

    Get-Item (Join-Path $repoRoot $target) |
        Select-Object @{n='Arquivo'; e={$_.FullName.Substring($repoRoot.Length + 1)}},
                      @{n='Tamanho'; e={'{0:N0}' -f $_.Length}},
                      LastWriteTime
}
finally {
    Pop-Location
}
