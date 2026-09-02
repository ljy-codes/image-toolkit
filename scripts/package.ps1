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

New-Item -ItemType Directory -Force -Path $output | Out-Null
$arguments = @(
    "/DMyAppVersion=$Version",
    "/DSourceRoot=$root",
    "/DPublishDir=$(Join-Path $root 'artifacts\publish\win-x64')",
    "/DInstallerOutput=$output"
)
if ($SignToolCommand) {
    $arguments += "/DSignToolCommand=$SignToolCommand"
}
$arguments += $installerScript

& $iscc @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

$setup = Join-Path $output 'ImageToolkitSetup.exe'
if (-not (Test-Path -LiteralPath $setup)) {
    throw "Installer was not generated: $setup"
}

Write-Host "Installer completed: $setup"
