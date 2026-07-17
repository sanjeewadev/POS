[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InstallRoot,
    [string]$DestinationFolder = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$configPath = Join-Path $env:ProgramData "Advanced POS\deployment.server.json"
if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    throw "The Advanced POS server deployment settings were not found: $configPath"
}

$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$setup = Join-Path ([IO.Path]::GetFullPath($InstallRoot)) "DatabaseSetup\POS.Database.Setup.exe"
if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) {
    throw "The database setup utility was not found: $setup"
}

if ([string]::IsNullOrWhiteSpace($DestinationFolder)) {
    $defaultFolder = Join-Path ([Environment]::GetFolderPath("Desktop")) "Advanced POS Backups"
    $entered = Read-Host "Backup destination folder [$defaultFolder]"
    $DestinationFolder = if ([string]::IsNullOrWhiteSpace($entered)) { $defaultFolder } else { $entered }
}

$destination = [IO.Path]::GetFullPath($DestinationFolder)
New-Item -ItemType Directory -Path $destination -Force | Out-Null

$fileName = "{0}_{1}.bak" -f $config.DatabaseName, (Get-Date -Format "yyyyMMdd_HHmmss")
$backupFile = Join-Path $destination $fileName

& $setup backup-copy `
    --instance ([string]$config.SqlServerInstance) `
    --database ([string]$config.DatabaseName) `
    --destination $backupFile

if ($LASTEXITCODE -ne 0) {
    throw "Advanced POS production backup failed."
}

$hash = (Get-FileHash -LiteralPath $backupFile -Algorithm SHA256).Hash
"$hash  $fileName" | Set-Content -LiteralPath "$backupFile.sha256" -Encoding ASCII

Write-Host "Advanced POS backup completed and verified." -ForegroundColor Green
Write-Host "Backup:  $backupFile"
Write-Host "SHA-256: $hash"
