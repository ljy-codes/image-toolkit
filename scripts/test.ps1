param(
    [string]$Configuration = 'Release',
    [switch]$IncludeUserAssets
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$solution = Join-Path $root 'ImageToolkit.sln'
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnet = if ($dotnetCommand) {
    $dotnetCommand.Source
}
else {
    Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
}
if (-not (Test-Path -LiteralPath $dotnet)) {
    throw '.NET SDK was not found. Install the .NET 10 SDK.'
}
$filter = if ($IncludeUserAssets) {
    $null
}
else {
    'Category!=UserAssets'
}

$arguments = @(
    'test',
    $solution,
    '-c', $Configuration,
    '--no-build',
    '-m:1',
    '-p:UseSharedCompilation=false'
)
if ($filter) {
    $arguments += @('--filter', $filter)
}

& $dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet test failed with exit code $LASTEXITCODE."
}
