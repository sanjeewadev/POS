[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InstallRoot,
    [string]$BackupFile = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$configPath = Join-Path $env:ProgramData "Advanced POS\deployment.server.json"
if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    throw "The Advanced POS server deployment settings were not found: $configPath"
}

$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$root = [IO.Path]::GetFullPath($InstallRoot)
$setup = Join-Path $root "DatabaseSetup\POS.Database.Setup.exe"

if ([string]::IsNullOrWhiteSpace($BackupFile)) {
    $BackupFile = Read-Host "Full path of the verified .bak file"
}

$backup = [IO.Path]::GetFullPath($BackupFile)
if (-not (Test-Path -LiteralPath $backup -PathType Leaf)) {
    throw "The selected SQL Server backup was not found: $backup"
}

$hashFile = "$backup.sha256"
if (Test-Path -LiteralPath $hashFile -PathType Leaf) {
    $expectedHash = (
        (Get-Content -LiteralPath $hashFile -Raw).Trim() -split "\s+"
    )[0].ToUpperInvariant()

    $actualHash = (
        Get-FileHash -LiteralPath $backup -Algorithm SHA256
    ).Hash.ToUpperInvariant()

    Write-Host "Expected SHA-256: $expectedHash"
    Write-Host "Actual SHA-256:   $actualHash"

    if ($actualHash -ne $expectedHash) {
        throw "The selected backup failed SHA-256 verification."
    }
}
else {
    Write-Warning "No .sha256 sidecar was found. Restore will rely on SQL Server backup checksums and the explicit RESTORE confirmation."
}

$running = @(Get-Process -Name "POS.BackOffice.UI", "POS.Cashier.UI" -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    $running | Select-Object ProcessName, Id | Format-Table -AutoSize
    throw "Close BackOffice and all Cashier applications before restoring the production database."
}

$confirmation = Read-Host "Type RESTORE to replace the production database"
if ($confirmation -cne "RESTORE") {
    throw "Production restore was cancelled."
}

$password = Read-Host "SQL application password" -AsSecureString
$pointer = [IntPtr]::Zero

try {
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    $previous = [Environment]::GetEnvironmentVariable("POS_SETUP_APP_PASSWORD", "Process")
    $env:POS_SETUP_APP_PASSWORD = $plainPassword

    $reportFolder = Join-Path $env:ProgramData "Advanced POS\Reports"
    New-Item -ItemType Directory -Path $reportFolder -Force | Out-Null
    $report = Join-Path $reportFolder ("Restore_{0}.json" -f (Get-Date -Format "yyyyMMdd_HHmmss"))

    try {
        & $setup provision-restore `
            --instance ([string]$config.SqlServerInstance) `
            --host localhost `
            --port ([string]$config.Port) `
            --database ([string]$config.DatabaseName) `
            --app-login ([string]$config.ApplicationLogin) `
            --file $backup `
            --profile ([string]$config.ProfilePath) `
            --report $report `
            --confirm-destructive-restore

        if ($LASTEXITCODE -ne 0) {
            throw "Advanced POS production restore failed."
        }
    }
    finally {
        if ($null -eq $previous) {
            Remove-Item Env:POS_SETUP_APP_PASSWORD -ErrorAction SilentlyContinue
        }
        else {
            $env:POS_SETUP_APP_PASSWORD = $previous
        }
    }
}
finally {
    if ($pointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
    $plainPassword = $null
}

Write-Host "Advanced POS production database restored and verified." -ForegroundColor Green
