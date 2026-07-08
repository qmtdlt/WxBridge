param(
    [string]$SourceSkillDir = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")) "skills\wxbridge"),
    [string]$CodexHome = $env:CODEX_HOME,
    [string]$InstallDir = $env:WXBRIDGE_HOME
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($CodexHome)) {
    $CodexHome = Join-Path $HOME ".codex"
}

$targetRoot = Join-Path $CodexHome "skills"
$targetSkill = Join-Path $targetRoot "wxbridge"

function Write-WxBridgeInstallMetadata($SkillDir, $InstallDir) {
    if ([string]::IsNullOrWhiteSpace($InstallDir)) {
        $defaultInstallDir = Join-Path $env:LOCALAPPDATA "WxBridge"
        if (Test-Path $defaultInstallDir) {
            $InstallDir = $defaultInstallDir
        }
    }

    if ([string]::IsNullOrWhiteSpace($InstallDir) -or -not (Test-Path $InstallDir)) {
        Write-Host "No installed WxBridge CLI was detected; skipped local-install.json."
        return
    }

    $resolvedInstallDir = [System.IO.Path]::GetFullPath($InstallDir)
    $referencesDir = Join-Path $SkillDir "references"
    New-Item -ItemType Directory -Path $referencesDir -Force | Out-Null

    $metadata = [ordered]@{
        installDir = $resolvedInstallDir
        command = (Join-Path $resolvedInstallDir "wxbridge.cmd")
        script = (Join-Path $resolvedInstallDir "wxbridge.ps1")
        exe = (Join-Path $resolvedInstallDir "WxBridge.Cli.exe")
        installedAt = [DateTimeOffset]::Now.ToString("o")
    }

    $metadataPath = Join-Path $referencesDir "local-install.json"
    $metadata | ConvertTo-Json | Set-Content -LiteralPath $metadataPath -Encoding UTF8
    Write-Host "Wrote wxbridge install metadata to $metadataPath"
}

if (-not (Test-Path (Join-Path $SourceSkillDir "SKILL.md"))) {
    Write-Error "Cannot find wxbridge skill at $SourceSkillDir"
    exit 1
}

New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null

if (Test-Path $targetSkill) {
    Remove-Item -LiteralPath $targetSkill -Recurse -Force
}

Copy-Item -LiteralPath $SourceSkillDir -Destination $targetSkill -Recurse
Write-WxBridgeInstallMetadata $targetSkill $InstallDir

Write-Host "Installed wxbridge skill to $targetSkill"
Write-Host "Restart Codex or start a new thread if the skill is not immediately visible."
