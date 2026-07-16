[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SetupExecutable,
    [string]$SqlServerInstance = ".\SQLEXPRESS",
    [string]$DatabaseName = "POSNetwork",
    [Parameter(Mandatory = $true)]
    [string]$BackupFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$exe = [IO.Path]::GetFullPath($SetupExecutable)
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw "POS database setup executable was not found: $exe"
}

& $exe backup `
    --instance $SqlServerInstance `
    --database $DatabaseName `
    --file ([IO.Path]::GetFullPath($BackupFile))

if ($LASTEXITCODE -ne 0) {
    throw "POS SQL Server backup failed."
}
