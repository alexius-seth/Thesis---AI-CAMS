<#
.SYNOPSIS
    Downloads and extracts minimal portable toolchains for ACPAMS.
    Run this script once before packaging the student build.
#>
param(
    [string]$DestinationDir = "$PSScriptRoot\..\ACPAMS\Tools"
)

$ErrorActionPreference = "Stop"

function Download-And-Unzip($url, $zipPath, $outDir) {
    if (-not (Test-Path $outDir)) {
        New-Item -ItemType Directory -Force -Path $outDir | Out-Null
        Write-Host "Downloading $url..."
        Invoke-WebRequest -Uri $url -OutFile $zipPath
        Write-Host "Extracting $zipPath..."
        Expand-Archive -Path $zipPath -DestinationPath $outDir -Force
        Remove-Item $zipPath -Force
    } else {
        Write-Host "Directory $outDir already exists. Skipping download."
    }
}

Write-Host "Target Tools Directory: $DestinationDir"

# 1. Python Embeddable (x64)
$pyDir = Join-Path $DestinationDir "Python"
if (-not (Test-Path (Join-Path $pyDir "python.exe"))) {
    Write-Host "`nSetting up Python..."
    $pyZip = Join-Path $DestinationDir "python-embed.zip"
    $pyUrl = "https://www.python.org/ftp/python/3.11.9/python-3.11.9-embed-amd64.zip"
    Download-And-Unzip $pyUrl $pyZip $pyDir
}

# 2. MinGW (WinLibs GCC minimal x64)
$cDir = Join-Path $DestinationDir "MinGW"
if (-not (Test-Path (Join-Path $cDir "bin\gcc.exe"))) {
    Write-Host "`nSetting up MinGW GCC..."
    $mingwZip = Join-Path $DestinationDir "mingw.zip"
    $mingwUrl = "https://github.com/brechtsanders/winlibs_mingw/releases/download/13.2.0posix-17.0.6-11.0.1-ucrt-r5/winlibs-x86_64-posix-seh-gcc-13.2.0-mingw-w64ucrt-11.0.1-r5.7z"
    Write-Host "Notice: For MinGW, download portable winlibs-x86_64 ZIP/7z and place contents into: $cDir"
}

# 3. OpenJDK 17 (Adoptium / Temurin)
$jdkDir = Join-Path $DestinationDir "JDK"
if (-not (Test-Path (Join-Path $jdkDir "bin\javac.exe"))) {
    Write-Host "`nSetting up OpenJDK..."
    Write-Host "Notice: Download OpenJDK 17 / 21 Windows ZIP from adoptium.net and extract into: $jdkDir"
}

Write-Host "`nDone. Check $DestinationDir for structure."

