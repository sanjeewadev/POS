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
$instanceName = ([string]$config.SqlServerInstance) -replace '^\.\\', ''
$serviceName = "MSSQL`$$instanceName"

Write-Host "Advanced POS Server Status" -ForegroundColor Cyan
Write-Host "Computer: $env:COMPUTERNAME"
Write-Host "Endpoint: $($config.ServerHost):$($config.Port)"
Write-Host "Database: $($config.DatabaseName)"
Write-Host

Get-Service -Name $serviceName | Select-Object Name, Status, StartType | Format-Table -AutoSize
Test-NetConnection -ComputerName ([string]$config.ServerHost) -Port ([int]$config.Port) |
    Select-Object ComputerName, RemotePort, TcpTestSucceeded | Format-List

& $setup status `
    --instance ([string]$config.SqlServerInstance) `
    --database ([string]$config.DatabaseName)

if ($LASTEXITCODE -ne 0) {
    throw "Advanced POS server status verification failed."
}
