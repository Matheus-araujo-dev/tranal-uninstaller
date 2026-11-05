# Mescla os arquivos dos workers (copias isoladas) de volta no projeto principal.
# Cada worker tem propriedade exclusiva de um conjunto de arquivos, entao a
# mescla e uma copia direta — sem conflito.
[CmdletBinding()]
param(
    [string]$Project = 'E:\Projetos\tranal-uninstaller'
)

$ErrorActionPreference = 'Stop'

$plan = @(
    @{ Wip = 'a'; Files = @(
        'src\Tranal.Uninstaller\Ui\UninstallWizardWindow.xaml',
        'src\Tranal.Uninstaller\Ui\UninstallWizardWindow.xaml.cs',
        'src\Tranal.Uninstaller\Ui\BackupManagerWindow.xaml',
        'src\Tranal.Uninstaller\Ui\BackupManagerWindow.xaml.cs',
        'src\Tranal.Uninstaller\Ui\OptionsWindow.xaml',
        'src\Tranal.Uninstaller\Ui\OptionsWindow.xaml.cs'
    )},
    @{ Wip = 'b'; Files = @(
        'src\Tranal.Cli\Program.cs',
        'src\Tranal.Cli\ExtraCommands.cs'
    )},
    @{ Wip = 'c'; Files = @(
        'src\Tranal.Uninstaller\Core\Topmost\TopmostTargetResolver.cs',
        'src\Tranal.Uninstaller\Core\Topmost\ExplorerContextMenu.cs',
        'src\Tranal.Uninstaller\Core\Tools\RegistryOptimizer.cs',
        'src\Tranal.Uninstaller\Core\Updates\AutoUpdateClient.cs',
        'src\Tranal.Uninstaller\Core\PortableMode.cs',
        'src\Tranal.Uninstaller\Core\Modules\InstallMonitor.cs',
        'src\Tranal.Uninstaller\Ui\Tools\TopmostOverlayWindow.xaml',
        'src\Tranal.Uninstaller\Ui\Tools\TopmostOverlayWindow.xaml.cs'
    )}
)

foreach ($group in $plan) {
    $wip = "E:\Projetos\_wip-$($group.Wip)"
    foreach ($file in $group.Files) {
        $source = Join-Path $wip $file
        $target = Join-Path $Project $file

        if (-not (Test-Path -LiteralPath $source)) {
            "  [ausente no worker $($group.Wip)] $file"
            continue
        }

        $parent = [System.IO.Path]::GetDirectoryName($target)
        if (-not [System.IO.Directory]::Exists($parent)) {
            [void][System.IO.Directory]::CreateDirectory($parent)
        }

        [System.IO.File]::Copy($source, $target, $true)
        "  [worker $($group.Wip)] $file  (" + (New-Object System.IO.FileInfo($target)).Length + " bytes)"
    }
}

"Mescla concluida."
