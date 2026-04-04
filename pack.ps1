param(
    [switch]$Push,
    [string]$Source = "https://api.nuget.org/v3/index.json",
    [string]$ApiKey
)

$ErrorActionPreference = "Stop"

# Resolve API key: explicit parameter > NUGET_API_KEY environment variable
if (-not $ApiKey) {
    $envKey = $env:NUGET_API_KEY
    if ($envKey) {
        $ApiKey = $envKey
        Write-Host "Using API key from NUGET_API_KEY environment variable." -ForegroundColor Yellow
    }
}

# Build the React client so its output is available for embedding in ActionView.Api
$clientDir = "$PSScriptRoot/src/client"
Write-Host "Building client..." -ForegroundColor Cyan
Push-Location $clientDir
try {
    npm ci
    if ($LASTEXITCODE -ne 0) { Write-Error "npm ci failed"; exit 1 }
    npm run build
    if ($LASTEXITCODE -ne 0) { Write-Error "Client build failed"; exit 1 }
}
finally {
    Pop-Location
}
Write-Host "Client built successfully." -ForegroundColor Green

$projects = @(
    "src/ActionView.Cli/ActionView.Cli.csproj",
    "src/ActionView.Api/ActionView.Api.csproj",
    "src/ActionView.Mcp/ActionView.Mcp.csproj"
)

$outputDir = "$PSScriptRoot/artifacts"

if (Test-Path $outputDir) {
    Remove-Item $outputDir -Recurse -Force
}

foreach ($project in $projects) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($project)
    Write-Host "Packing $name..." -ForegroundColor Cyan
    dotnet pack $project -c Release -o $outputDir
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to pack $name"
        exit 1
    }
}

Write-Host ""
Write-Host "Packages created in $outputDir" -ForegroundColor Green
Get-ChildItem $outputDir -Filter *.nupkg | ForEach-Object { Write-Host "  $_" }

if ($Push) {
    Write-Host ""
    Write-Host "Pushing packages to $Source..." -ForegroundColor Cyan
    foreach ($pkg in Get-ChildItem $outputDir -Filter *.nupkg) {
        Write-Host "  Pushing $($pkg.Name)..."
        $pushArgs = @($pkg.FullName, "--source", $Source)
        if ($ApiKey) {
            $pushArgs += @("--api-key", $ApiKey)
        }
        dotnet nuget push @pushArgs
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to push $($pkg.Name)"
            exit 1
        }
    }
    Write-Host "All packages pushed." -ForegroundColor Green
}
