param(
    [string]$Version = '1.0.0',
    [string]$Configuration = 'Release',
    [string]$SignToolCommand
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$publishScript = Join-Path $PSScriptRoot 'publish.ps1'
$installerScript = Join-Path $root 'installer\ImageToolkit.iss'
$output = Join-Path $root 'artifacts\installer'
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
$staging = Join-Path $artifactsRoot "installer-staging-$PID"

& $publishScript -Configuration $Configuration

$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty Source
if (-not $iscc) {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe'
    )
    $iscc = $candidates | Where-Object {
        Test-Path -LiteralPath $_
    } | Select-Object -First 1
}
if (-not $iscc) {
    throw 'Inno Setup 6 was not found. Install it and run package.ps1 again.'
}

try {
    New-Item -ItemType Directory -Force -Path $staging | Out-Null
    $arguments = @(
        "/DMyAppVersion=$Version",
        "/DSourceRoot=$root",
        "/DPublishDir=$(Join-Path $root 'artifacts\publish\win-x64')",
        "/DInstallerOutput=$staging"
    )
    if ($SignToolCommand) {
        $arguments += "/DSignToolCommand=$SignToolCommand"
    }
    $arguments += $installerScript

    & $iscc @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
    }

    $stagedSetup = Join-Path $staging 'ImageToolkitSetup.exe'
    if (-not (Test-Path -LiteralPath $stagedSetup)) {
        throw "Installer was not generated: $stagedSetup"
    }

    New-Item -ItemType Directory -Force -Path $output | Out-Null
    $setup = Join-Path $output 'ImageToolkitSetup.exe'
    Copy-Item -LiteralPath $stagedSetup -Destination $setup -Force
    Write-Host "Installer completed: $setup"
}
finally {
    if (Test-Path -LiteralPath $staging) {
        $resolvedStaging = (Resolve-Path -LiteralPath $staging).Path
        if (-not $resolvedStaging.StartsWith(
            $artifactsRoot,
            [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean a path outside the repository artifacts directory: $resolvedStaging"
        }

        Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
    }
}
