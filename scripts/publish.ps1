<#
.SYNOPSIS
    Builds a Velopack release locally (self-contained single-file + vpk pack).

.DESCRIPTION
    Produces installer + update artifacts in releases\ (including the Setup.exe that installs
    Chromata and registers it for updates). These are also what the app's "Check for updates"
    reads once uploaded to the GitHub Releases page (see AppInfo.RepoUrl).
    For day-to-day development just run the app directly: dotnet run --project src

.PARAMETER Version
    Release version (e.g. 0.1.0). Defaults to the <Version> in Chromata.csproj.
#>
param(
    [string]$Version
)
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "src\Chromata.csproj"

if (-not $Version) {
    $Version = (Select-Xml -Path $proj -XPath "//Version").Node.InnerText
}
if (-not $Version) { throw "Could not determine version. Pass -Version 1.2.3" }

# Ensure the vpk CLI is available.
if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    Write-Host "Installing vpk CLI..."
    dotnet tool install -g vpk
    $env:PATH += ";$env:USERPROFILE\.dotnet\tools"
}

$publish = Join-Path $root "publish"
$releases = Join-Path $root "releases"

Write-Host "Building Chromata v$Version..."
dotnet publish $proj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=embedded -p:Version=$Version -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Write-Host "Packaging with Velopack..."
vpk pack --packId Chromata --packTitle "Chromata" --packVersion $Version --packDir $publish --mainExe Chromata.exe --outputDir $releases
if ($LASTEXITCODE -ne 0) { throw "vpk pack failed." }

Write-Host ""
Write-Host "Release artifacts ready in: $releases"
Write-Host "Upload them to: https://github.com/ArcticGizmo/chromata/releases/new?tag=v$Version"
