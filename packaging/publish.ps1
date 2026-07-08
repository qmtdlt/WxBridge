param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "dist"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "src\WxBridge.Cli\WxBridge.Cli.csproj"
$dist = Join-Path $repoRoot $OutputDir
$work = Join-Path $dist "_work"

if (Test-Path $dist) {
    Remove-Item -LiteralPath $dist -Recurse -Force
}

New-Item -ItemType Directory -Path $work | Out-Null

$frameworkDir = Join-Path $work "WxBridge-win-x64"
$singleFileDir = Join-Path $work "WxBridge-win-x64-single-file"

dotnet publish $project -c $Configuration -r $Runtime --self-contained false -o $frameworkDir
dotnet publish $project -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true -o $singleFileDir

Copy-Item -LiteralPath (Join-Path $repoRoot "wxbridge.ps1") -Destination (Join-Path $frameworkDir "wxbridge.ps1")
Copy-Item -LiteralPath (Join-Path $repoRoot "wxbridge.ps1") -Destination (Join-Path $singleFileDir "wxbridge.ps1")

Copy-Item -LiteralPath (Join-Path $repoRoot "packaging\install.ps1") -Destination (Join-Path $dist "install.ps1")
Copy-Item -LiteralPath (Join-Path $repoRoot "packaging\install-skill.ps1") -Destination (Join-Path $dist "install-skill.ps1")

$frameworkZip = Join-Path $dist "WxBridge-win-x64.zip"
$singleFileZip = Join-Path $dist "WxBridge-win-x64-single-file.zip"

Compress-Archive -Path (Join-Path $frameworkDir "*") -DestinationPath $frameworkZip -CompressionLevel Optimal
Compress-Archive -Path (Join-Path $singleFileDir "*") -DestinationPath $singleFileZip -CompressionLevel Optimal

$checksumPath = Join-Path $dist "checksums.txt"
Get-ChildItem -LiteralPath $dist -File |
    Where-Object { $_.Name -ne "checksums.txt" } |
    Sort-Object Name |
    ForEach-Object {
        $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName
        "$($hash.Hash)  $($_.Name)"
    } | Set-Content -LiteralPath $checksumPath -Encoding UTF8

Remove-Item -LiteralPath $work -Recurse -Force

Get-ChildItem -LiteralPath $dist -File |
    Sort-Object Name |
    Select-Object Name, Length, @{Name = "MB"; Expression = { [math]::Round($_.Length / 1MB, 2) }}
