param(
    [string]$SourceSkillDir = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")) "skills\wxbridge"),
    [string]$CodexHome = $env:CODEX_HOME
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($CodexHome)) {
    $CodexHome = Join-Path $HOME ".codex"
}

$targetRoot = Join-Path $CodexHome "skills"
$targetSkill = Join-Path $targetRoot "wxbridge"

if (-not (Test-Path (Join-Path $SourceSkillDir "SKILL.md"))) {
    Write-Error "Cannot find wxbridge skill at $SourceSkillDir"
    exit 1
}

New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null

if (Test-Path $targetSkill) {
    Remove-Item -LiteralPath $targetSkill -Recurse -Force
}

Copy-Item -LiteralPath $SourceSkillDir -Destination $targetSkill -Recurse

Write-Host "Installed wxbridge skill to $targetSkill"
Write-Host "Restart Codex or start a new thread if the skill is not immediately visible."
