#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build and optionally deploy Craftimizer to XIVLauncher.

.PARAMETER Configuration
    Build configuration. Default: Debug.

.PARAMETER Deploy
    Copy build output to XIVLauncher installedPlugins after building.

.PARAMETER NoBuild
    Skip build and just deploy whatever is already in the bin folder.

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Configuration Release -Deploy
    .\build.ps1 -Deploy -NoBuild
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$Deploy,
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root     = $PSScriptRoot
$csproj   = "$root\Craftimizer\Craftimizer.csproj"

# Prefer Scoop-installed SDK; fall back to PATH
$scoopDotnet = "C:\Users\aleja\scoop\apps\dotnet-sdk\current\dotnet.exe"
$dotnet = if (Test-Path $scoopDotnet) { $scoopDotnet } else { "dotnet" }

# --- read version from csproj ---
$xml     = [xml](Get-Content $csproj)
$version = $xml.Project.PropertyGroup[0].Version
$binDir  = "$root\Craftimizer\bin\$Configuration"

# --- build ---
if (-not $NoBuild) {
    Write-Host "Building Craftimizer $version ($Configuration)..." -ForegroundColor Cyan
    & $dotnet build $csproj -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
    Write-Host "Build succeeded." -ForegroundColor Green
}

# --- deploy ---
if ($Deploy) {
    $pluginDir = "$env:APPDATA\XIVLauncher\installedPlugins\Craftimizer\$version"
    if (-not (Test-Path $pluginDir)) {
        Write-Host "Creating plugin directory: $pluginDir" -ForegroundColor Yellow
        New-Item -ItemType Directory -Path $pluginDir | Out-Null
    }
    Write-Host "Deploying to $pluginDir ..." -ForegroundColor Cyan
    Copy-Item "$binDir\*" $pluginDir -Force
    Write-Host "Deploy complete." -ForegroundColor Green
}
