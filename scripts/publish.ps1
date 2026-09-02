param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'src\ImageToolkit.App\ImageToolkit.App.csproj'
$output = Join-Path $root 'artifacts\publish\win-x64'
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

if (Test-Path -LiteralPath $output) {
    $resolvedOutput = (Resolve-Path -LiteralPath $output).Path
    $artifactsRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
    if (-not $resolvedOutput.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a path outside the repository artifacts directory: $resolvedOutput"
    }

    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}

& $dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -m:1 `
    -p:PublishSingleFile=false `
    -p:UseSharedCompilation=false `
    -o $output
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$executable = Join-Path $output 'ImageToolkit.App.exe'
if (-not (Test-Path -LiteralPath $executable)) {
    throw "Published output does not contain the application executable: $executable"
}

Write-Host "Publish completed: $output"
