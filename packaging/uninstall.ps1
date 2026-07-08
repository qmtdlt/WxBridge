param(
    [string]$InstallDir = $env:WXBRIDGE_HOME,
    [string]$CodexHome = $env:CODEX_HOME,
    [switch]$RemoveFromPath,
    [switch]$KeepSkill,
    [switch]$KeepCli,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

function Resolve-DefaultInstallDir($InstallDir) {
    if (-not [string]::IsNullOrWhiteSpace($InstallDir)) {
        return [System.IO.Path]::GetFullPath($InstallDir)
    }

    $defaultInstallDir = Join-Path $env:LOCALAPPDATA "WxBridge"
    return [System.IO.Path]::GetFullPath($defaultInstallDir)
}

function Resolve-CodexHome($CodexHome) {
    if (-not [string]::IsNullOrWhiteSpace($CodexHome)) {
        return [System.IO.Path]::GetFullPath($CodexHome)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $HOME ".codex"))
}

function Remove-UserPath($PathToRemove, [switch]$WhatIf) {
    $current = [Environment]::GetEnvironmentVariable("Path", "User")
    if ([string]::IsNullOrWhiteSpace($current)) {
        return
    }

    $normalizedTarget = [System.IO.Path]::GetFullPath($PathToRemove).TrimEnd('\')
    $parts = $current -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $kept = @()
    $removed = $false

    foreach ($part in $parts) {
        try {
            $normalizedPart = [System.IO.Path]::GetFullPath($part).TrimEnd('\')
        }
        catch {
            $normalizedPart = $part.TrimEnd('\')
        }

        if ($normalizedPart.Equals($normalizedTarget, [System.StringComparison]::OrdinalIgnoreCase)) {
            $removed = $true
            continue
        }

        $kept += $part
    }

    if ($removed) {
        if ($WhatIf) {
            Write-Host "Would remove from user PATH: $PathToRemove"
            return
        }

        [Environment]::SetEnvironmentVariable("Path", ($kept -join ';'), "User")
        Write-Host "Removed from user PATH: $PathToRemove"
    }
}

function Remove-DirectoryIfExists($Path, [switch]$WhatIf) {
    if (-not (Test-Path $Path)) {
        Write-Host "Not found: $Path"
        return
    }

    if ($WhatIf) {
        Write-Host "Would remove: $Path"
        return
    }

    Remove-Item -LiteralPath $Path -Recurse -Force
    Write-Host "Removed: $Path"
}

$resolvedInstallDir = Resolve-DefaultInstallDir $InstallDir
$resolvedCodexHome = Resolve-CodexHome $CodexHome
$skillDir = Join-Path $resolvedCodexHome "skills\wxbridge"

if (-not $KeepCli) {
    Remove-DirectoryIfExists $resolvedInstallDir -WhatIf:$WhatIf
}

if (-not $KeepSkill) {
    Remove-DirectoryIfExists $skillDir -WhatIf:$WhatIf
}

if ($RemoveFromPath) {
    Remove-UserPath $resolvedInstallDir -WhatIf:$WhatIf
}

$currentHome = [Environment]::GetEnvironmentVariable("WXBRIDGE_HOME", "User")
if (-not [string]::IsNullOrWhiteSpace($currentHome)) {
    try {
        $normalizedCurrentHome = [System.IO.Path]::GetFullPath($currentHome).TrimEnd('\')
        $normalizedInstallDir = $resolvedInstallDir.TrimEnd('\')
    }
    catch {
        $normalizedCurrentHome = $currentHome.TrimEnd('\')
        $normalizedInstallDir = $resolvedInstallDir.TrimEnd('\')
    }

    if ($normalizedCurrentHome.Equals($normalizedInstallDir, [System.StringComparison]::OrdinalIgnoreCase)) {
        if ($WhatIf) {
            Write-Host "Would clear user WXBRIDGE_HOME"
        }
        else {
            [Environment]::SetEnvironmentVariable("WXBRIDGE_HOME", $null, "User")
            Remove-Item Env:\WXBRIDGE_HOME -ErrorAction SilentlyContinue
            Write-Host "Cleared user WXBRIDGE_HOME"
        }
    }
}

Write-Host "WxBridge uninstall completed."
