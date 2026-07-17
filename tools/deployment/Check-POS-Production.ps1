[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InstallRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$configPath = Join-Path $env:ProgramData "Advanced POS\deployment.server.json"
if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    throw "The Advanced POS server deployment settings were not found: $configPath"
}

$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$setup = Join-Path ([IO.Path]::GetFullPath($InstallRoot)) "DatabaseSetup\POS.Database.Setup.exe"

& $setup check `
    --instance ([string]$config.SqlServerInstance) `
    --database ([string]$config.DatabaseName)

if ($LASTEXITCODE -ne 0) {
    throw "Advanced POS database integrity verification failed."
}

Write-Host "Advanced POS production database is healthy." -ForegroundColor Green
