param(
    [string]$Owner,
    [string]$Repo = "WxBridge",
    [string]$Version = "latest",
    [switch]$SingleFile,
    [string]$InstallDir = "$env:LOCALAPPDATA\WxBridge",
    [switch]$AddToPath
)

$ErrorActionPreference = "Stop"

function Fail($message) {
    Write-Error $message
    exit 1
}

function Get-LatestReleaseTag($Owner, $Repo) {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Owner/$Repo/releases/latest" -Headers @{ "User-Agent" = "WxBridge-Installer" }
    return $release.tag_name
}

function Test-WindowsDesktopRuntime9 {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        return $false
    }

    $runtimes = & dotnet --list-runtimes 2>$null
    return ($runtimes -match '^Microsoft\.WindowsDesktop\.App\s+9\.')
}

function Add-UserPath($PathToAdd) {
    $current = [Environment]::GetEnvironmentVariable("Path", "User")
    $parts = @()
    if (-not [string]::IsNullOrWhiteSpace($current)) {
        $parts = $current -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    }

    if ($parts -notcontains $PathToAdd) {
        $newPath = (($parts + $PathToAdd) -join ';')
        [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
        Write-Host "Added to user PATH: $PathToAdd"
    }
}

$isWindowsOs = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)
if (-not $isWindowsOs) {
    Fail "WxBridge only supports Windows."
}

if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne [System.Runtime.InteropServices.Architecture]::X64) {
    Fail "This installer currently supports Windows x64 only."
}

if ([string]::IsNullOrWhiteSpace($Owner)) {
    Fail "Missing GitHub owner. Usage: .\install.ps1 -Owner <github-owner> [-Repo WxBridge]"
}

$assetName = if ($SingleFile) { "WxBridge-win-x64-single-file.zip" } else { "WxBridge-win-x64.zip" }

if (-not $SingleFile -and -not (Test-WindowsDesktopRuntime9)) {
    Write-Warning ".NET 9 Desktop Runtime was not found."
    Write-Host "Install it from: https://dotnet.microsoft.com/download/dotnet/9.0"
    Write-Host "Or rerun this installer with -SingleFile to install the bundled build."
    Fail "Missing .NET 9 Desktop Runtime."
}

$tag = if ($Version -eq "latest") { Get-LatestReleaseTag $Owner $Repo } else { $Version }
$downloadUrl = "https://github.com/$Owner/$Repo/releases/download/$tag/$assetName"
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("wxbridge-install-" + [Guid]::NewGuid().ToString("N"))
$zipPath = Join-Path $tempRoot $assetName

New-Item -ItemType Directory -Path $tempRoot | Out-Null
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

try {
    Write-Host "Downloading $downloadUrl"
    Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath

    $extractDir = Join-Path $tempRoot "extract"
    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractDir -Force

    Get-ChildItem -LiteralPath $InstallDir -Force | Remove-Item -Recurse -Force
    Copy-Item -Path (Join-Path $extractDir "*") -Destination $InstallDir -Recurse -Force

    $cmdPath = Join-Path $InstallDir "wxbridge.cmd"
    $cmd = @"
@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0wxbridge.ps1" %*
"@
    Set-Content -LiteralPath $cmdPath -Value $cmd -Encoding ASCII

    if ($AddToPath) {
        Add-UserPath $InstallDir
    }

    Write-Host "Installed WxBridge to $InstallDir"
    Write-Host "Run:"
    Write-Host "  powershell -NoProfile -ExecutionPolicy Bypass -File `"$InstallDir\wxbridge.ps1`" status"

    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $InstallDir "wxbridge.ps1") status
}
finally {
    if (Test-Path $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
