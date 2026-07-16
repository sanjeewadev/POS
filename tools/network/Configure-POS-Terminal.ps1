[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SetupExecutable,

    [Parameter(Mandatory = $true)]
    [string]$ServerHost,

    [ValidateRange(1, 65535)]
    [int]$Port = 1433,

    [Parameter(Mandatory = $true)]
    [string]$DatabaseName,

    [Parameter(Mandatory = $true)]
    [string]$ApplicationLogin,

    [Parameter(Mandatory = $true)]
    [Security.SecureString]$ApplicationPassword,

    [string]$ProfilePath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$exe = [IO.Path]::GetFullPath($SetupExecutable)
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw "POS database setup executable was not found: $exe"
}

if ([string]::IsNullOrWhiteSpace($ProfilePath)) {
    $ProfilePath = Join-Path $env:LOCALAPPDATA "POS\database.connection.dat"
}
else {
    $ProfilePath = [IO.Path]::GetFullPath($ProfilePath)
}

$networkTest = Test-NetConnection `
    -ComputerName $ServerHost `
    -Port $Port `
    -WarningAction SilentlyContinue

if (-not $networkTest.TcpTestSucceeded) {
    throw "Cannot reach SQL Server at $ServerHost`:$Port from this terminal."
}

$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($ApplicationPassword)
try {
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)

    $previousSecret = [Environment]::GetEnvironmentVariable(
        "POS_SETUP_APP_PASSWORD",
        "Process")
    $env:POS_SETUP_APP_PASSWORD = $plainPassword

    try {
        & $exe write-profile `
            --profile $ProfilePath `
            --host $ServerHost `
            --port ([string]$Port) `
            --database $DatabaseName `
            --app-login $ApplicationLogin

        if ($LASTEXITCODE -ne 0) {
            throw "The encrypted terminal database profile could not be written."
        }

        & $exe verify `
            --host $ServerHost `
            --port ([string]$Port) `
            --database $DatabaseName `
            --app-login $ApplicationLogin

        if ($LASTEXITCODE -ne 0) {
            throw "The terminal SQL Server connection verification failed."
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

Write-Host "POS terminal database connection configured and verified." -ForegroundColor Green
Write-Host "Profile: $ProfilePath"
