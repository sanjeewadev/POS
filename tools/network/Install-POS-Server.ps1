[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SetupExecutable,

    [string]$SqlServerInstance = ".\SQLEXPRESS",
    [string]$ServerHost = "127.0.0.1",

    [ValidateRange(1, 65535)]
    [int]$Port = 1433,

    [string]$DatabaseName = "POSNetwork",
    [string]$ApplicationLogin = "POS_App",

    [Parameter(Mandatory = $true)]
    [Security.SecureString]$ApplicationPassword,

    [Parameter(Mandatory = $true)]
    [string]$SourceSqliteDatabase,

    [string]$ProfilePath = "",
    [string]$ReportPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$exe = [IO.Path]::GetFullPath($SetupExecutable)
$sqlite = [IO.Path]::GetFullPath($SourceSqliteDatabase)

if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw "POS database setup executable was not found: $exe"
}
if (-not (Test-Path -LiteralPath $sqlite -PathType Leaf)) {
    throw "SQLite source database was not found: $sqlite"
}

if ([string]::IsNullOrWhiteSpace($ProfilePath)) {
    $ProfilePath = Join-Path $env:LOCALAPPDATA "POS\database.connection.dat"
}
else {
    $ProfilePath = [IO.Path]::GetFullPath($ProfilePath)
}

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $HOME (
        "Desktop\POS_Server_Install_" +
        (Get-Date -Format "yyyyMMdd_HHmmss") +
        ".json")
}
else {
    $ReportPath = [IO.Path]::GetFullPath($ReportPath)
}

$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($ApplicationPassword)
try {
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)

    $previousSecret = [Environment]::GetEnvironmentVariable(
        "POS_SETUP_APP_PASSWORD",
        "Process")
    $env:POS_SETUP_APP_PASSWORD = $plainPassword

    try {
        & $exe provision `
            --instance $SqlServerInstance `
            --host $ServerHost `
            --port ([string]$Port) `
            --database $DatabaseName `
            --app-login $ApplicationLogin `
            --sqlite $sqlite `
            --profile $ProfilePath `
            --report $ReportPath

        if ($LASTEXITCODE -ne 0) {
            throw "POS server database installation failed."
        }
    }
    finally {
        if ($null -eq $previousSecret) {
            Remove-Item Env:POS_SETUP_APP_PASSWORD -ErrorAction SilentlyContinue
        }
        else {
            $env:POS_SETUP_APP_PASSWORD = $previousSecret
        }
    }
}
finally {
    if ($pointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
    $plainPassword = $null
}

Write-Host "POS server database installed and verified." -ForegroundColor Green
Write-Host "Report:  $ReportPath"
Write-Host "Profile: $ProfilePath"
