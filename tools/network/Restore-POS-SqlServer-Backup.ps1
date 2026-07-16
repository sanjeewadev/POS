[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SetupExecutable,
    [string]$SqlServerInstance = ".\SQLEXPRESS",
    [string]$DatabaseName = "POSNetwork",
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,
    [switch]$ConfirmDestructiveRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $ConfirmDestructiveRestore) {
    throw "Restore requires -ConfirmDestructiveRestore. All active POS connections will be terminated."
}

$exe = [IO.Path]::GetFullPath($SetupExecutable)
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw "POS database setup executable was not found: $exe"
}

& $exe restore `
    --instance $SqlServerInstance `
    --database $DatabaseName `
    --file ([IO.Path]::GetFullPath($BackupFile)) `
    --confirm-destructive-restore

if ($LASTEXITCODE -ne 0) {
    throw "POS SQL Server restore failed."
}
