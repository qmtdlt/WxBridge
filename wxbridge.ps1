param(
    [switch]$Rebuild,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$WxBridgeArgs
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishedExe = Join-Path $root 'WxBridge.Cli.exe'
$project = Join-Path $root 'src\WxBridge.Cli\WxBridge.Cli.csproj'
$exe = Join-Path $root 'src\WxBridge.Cli\bin\Debug\net9.0-windows\WxBridge.Cli.exe'

if ((Test-Path -LiteralPath $publishedExe) -and -not $Rebuild) {
    & $publishedExe @WxBridgeArgs
    exit $LASTEXITCODE
}

if (-not (Test-Path -LiteralPath $project)) {
    throw "WxBridge executable was not found: $publishedExe"
}

if ($Rebuild -or -not (Test-Path -LiteralPath $exe)) {
    dotnet build $project | Out-Host
}

if (-not (Test-Path -LiteralPath $exe)) {
    throw "WxBridge executable was not found after build: $exe"
}

& $exe @WxBridgeArgs
exit $LASTEXITCODE
