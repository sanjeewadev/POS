[CmdletBinding()]
param(
    [string]$RepositoryPath = (Get-Location).Path,
    [string]$ExpectedBranch = "sanjeewadev",
    [string]$ExpectedBaseline = "9ee44e78aa5a7c4b79f74dca582abc7ba55d070b",
    [string]$UpgradeBaselineDb = "",
    [string]$ReportRoot = (Join-Path $HOME "Desktop\POS_Cashier_Audit_Reports")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$allowedChangedFiles = @(
    "POS.sln",
    "Run-Cashier-Audit.ps1",
    "POS.Cashier.AuditTests/POS.Cashier.AuditTests.csproj",
    "POS.Cashier.AuditTests/Program.cs",
    "POS.Cashier.AuditTests/AuditTestRunner.cs",
    "POS.Cashier.AuditTests/AuditTestCatalog.cs",
    "POS.Cashier.AuditTests/AuditDatabase.cs",
    "POS.Cashier.AuditTests/MigrationAuditTests.cs",
    "POS.Cashier.AuditTests/CashierConcurrencyAuditTests.cs",
    "POS.Cashier.AuditTests/CashierControlAuditTests.cs",
    "POS.Cashier.AuditTests/SourcePolicyAuditTests.cs",
    "POS.Cashier.UI/ViewModels/CashMovementViewModel.cs",
    "POS.Cashier.UI/ViewModels/SalesViewModel.cs",
    "POS.Cashier.UI/Views/SalesView.xaml.cs",
    "POS.Core/Repositories/SalesRepository.cs",
    "POS.Core/Repositories/TillRepository.cs",
    "POS.Core.CalculationTests/Program.cs"
    "POS.Cashier.UI/App.xaml.cs",
    "POS.Cashier.UI/Dialogs/StockInquiryDialog.xaml",
    "POS.Cashier.UI/Dialogs/StockInquiryDialog.xaml.cs",
    "POS.Cashier.UI/ViewModels/SalesViewModel.CartLifecycle.cs",
    "POS.Cashier.UI/ViewModels/StockInquiryViewModel.cs",
    "POS.Cashier.UI/Views/SalesView.xaml",
    "POS.Core/Models/DTOs/StockInquiryDtos.cs",
    "POS.Core/Repositories/StockInquiryRepository.cs"
)

$commandResults = [ordered]@{}
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$runSuffix = [Guid]::NewGuid().ToString("N").Substring(0, 8)
$runName = "Cashier_Audit_$timestamp`_$runSuffix"
$reportFolder = Join-Path $ReportRoot $runName
$zipPath = Join-Path $ReportRoot "Cashier_Audit_Report_$timestamp`_$runSuffix.zip"
$tempAuditRoot = Join-Path $env:TEMP "POS-Cashier-Audit\$runName"
$liveDbPath = Join-Path $env:LOCALAPPDATA "POS\pos_local.db"

function Normalize-GitPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    return ($Path -replace '\\', '/').Trim()
}

function Invoke-GitCapture {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    $output = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)"
    }
    return @($output | ForEach-Object { "$_" })
}

function Invoke-LoggedCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$LogPath,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [switch]$Append
    )

    $commandLine = "$FilePath $($Arguments -join ' ')"
    if ($Append) {
        Add-Content -LiteralPath $LogPath -Value "`r`n=== $Name ===`r`n`$ $commandLine" -Encoding UTF8
    }
    else {
        Set-Content -LiteralPath $LogPath -Value "=== $Name ===`r`n`$ $commandLine" -Encoding UTF8
    }

    Write-Host "`n=== $Name ===" -ForegroundColor Cyan
    Write-Host "`$ $commandLine"

    $output = & $FilePath @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    foreach ($line in @($output)) {
        Write-Host $line
        Add-Content -LiteralPath $LogPath -Value "$line" -Encoding UTF8
    }
    Add-Content -LiteralPath $LogPath -Value "EXIT_CODE=$exitCode" -Encoding UTF8
    $commandResults[$Name] = [int]$exitCode
    return [int]$exitCode
}

function Get-ChangedFiles {
    $all = New-Object System.Collections.Generic.List[string]
    foreach ($line in (Invoke-GitCapture -Arguments @("diff", "--name-only", "--"))) {
        if (-not [string]::IsNullOrWhiteSpace($line)) { $all.Add((Normalize-GitPath $line)) }
    }
    foreach ($line in (Invoke-GitCapture -Arguments @("diff", "--cached", "--name-only", "--"))) {
        if (-not [string]::IsNullOrWhiteSpace($line)) { $all.Add((Normalize-GitPath $line)) }
    }
    foreach ($line in (Invoke-GitCapture -Arguments @("ls-files", "--others", "--exclude-standard"))) {
        if (-not [string]::IsNullOrWhiteSpace($line)) { $all.Add((Normalize-GitPath $line)) }
    }
    foreach ($line in (Invoke-GitCapture -Arguments @("diff", "--name-only", "$ExpectedBaseline..HEAD", "--"))) {
        if (-not [string]::IsNullOrWhiteSpace($line)) { $all.Add((Normalize-GitPath $line)) }
    }
    return @($all | Sort-Object -Unique)
}

function Count-LogLines {
    param([string]$Path, [string]$Pattern)
    if (-not (Test-Path -LiteralPath $Path)) { return 0 }
    return @((Select-String -LiteralPath $Path -Pattern $Pattern -ErrorAction SilentlyContinue)).Count
}

New-Item -ItemType Directory -Path $reportFolder -Force | Out-Null
New-Item -ItemType Directory -Path $tempAuditRoot -Force | Out-Null

$failedTestsPath = Join-Path $reportFolder "FailedTests.txt"
Set-Content -LiteralPath $failedTestsPath -Value "" -Encoding UTF8

try {
    $repositoryFullPath = (Resolve-Path -LiteralPath $RepositoryPath).Path
    Set-Location -LiteralPath $repositoryFullPath

    if (-not (Test-Path -LiteralPath (Join-Path $repositoryFullPath "POS.sln"))) {
        throw "POS.sln was not found at repository path: $repositoryFullPath"
    }

    Get-Command git -ErrorAction Stop | Out-Null
    Get-Command dotnet -ErrorAction Stop | Out-Null

    $repoRoot = @(Invoke-GitCapture -Arguments @("rev-parse", "--show-toplevel"))[0]
    if ((Resolve-Path -LiteralPath $repoRoot).Path -ne $repositoryFullPath) {
        throw "RepositoryPath must be the Git repository root. Git root: $repoRoot"
    }

    $branch = @(Invoke-GitCapture -Arguments @("branch", "--show-current"))[0].Trim()
    $head = @(Invoke-GitCapture -Arguments @("rev-parse", "HEAD"))[0].Trim()
    if ($branch -ne $ExpectedBranch) {
        throw "Wrong branch. Expected '$ExpectedBranch', actual '$branch'."
    }

    & git cat-file -e "$ExpectedBaseline`^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Expected baseline commit does not exist locally: $ExpectedBaseline"
    }

    & git merge-base --is-ancestor $ExpectedBaseline HEAD
    if ($LASTEXITCODE -ne 0) {
        throw "HEAD is not the approved baseline or a descendant of it. Expected ancestor: $ExpectedBaseline; HEAD: $head"
    }

    $changedFiles = @(Get-ChangedFiles)
    $unexpectedFiles = @($changedFiles | Where-Object { $allowedChangedFiles -notcontains $_ })
    if ($unexpectedFiles.Count -gt 0) {
        throw "Unapproved repository changes were found:`r`n - $($unexpectedFiles -join "`r`n - ")"
    }

    $baselineLines = @(
        "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')",
        "Repository: $repositoryFullPath",
        "Branch: $branch",
        "HEAD: $head",
        "Approved baseline ancestor: $ExpectedBaseline",
        "Working/staged/committed audit file changes:",
        $(if ($changedFiles.Count -eq 0) { "  (none)" } else { $changedFiles | ForEach-Object { "  $_" } })
    )
    $baselineLines | Set-Content -LiteralPath (Join-Path $reportFolder "Baseline.txt") -Encoding UTF8

    $tempFull = [IO.Path]::GetFullPath($tempAuditRoot)
    $liveFull = [IO.Path]::GetFullPath($liveDbPath)
    $liveDirectory = [IO.Path]::GetFullPath((Split-Path -Parent $liveFull)).TrimEnd('\') + '\'
    if ($tempFull.StartsWith($liveDirectory, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Temporary audit directory is inside the live POS database directory."
    }

    $env:POS_AUDIT_REPO_ROOT = $repositoryFullPath
    $env:POS_AUDIT_TEMP_ROOT = $tempAuditRoot
    $env:POS_AUDIT_LIVE_DB_PATH = $liveDbPath
    if (-not [string]::IsNullOrWhiteSpace($UpgradeBaselineDb)) {
        $upgradeFull = (Resolve-Path -LiteralPath $UpgradeBaselineDb).Path
        if ($upgradeFull -eq $liveFull) {
            throw "The live POS database cannot be used directly as an upgrade baseline. Supply an approved copied test baseline."
        }
        $env:POS_AUDIT_UPGRADE_BASELINE = $upgradeFull
    }
    else {
        Remove-Item Env:POS_AUDIT_UPGRADE_BASELINE -ErrorAction SilentlyContinue
    }

    $environment = New-Object System.Collections.Generic.List[string]
    $environment.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')")
    $environment.Add("Computer: $env:COMPUTERNAME")
    $environment.Add("Windows: $([Environment]::OSVersion.VersionString)")
    $environment.Add("PowerShell: $($PSVersionTable.PSVersion)")
    $environment.Add("Repository: $repositoryFullPath")
    $environment.Add("Audit temp root: $tempAuditRoot")
    $environment.Add("Live database protected path: $liveDbPath")
    $environment.Add("Upgrade baseline: $(if ($env:POS_AUDIT_UPGRADE_BASELINE) { $env:POS_AUDIT_UPGRADE_BASELINE } else { '(not supplied)' })")
    $environment.Add("")
    $environment.Add("=== dotnet --info ===")
    $environment.AddRange([string[]]@(& dotnet --info 2>&1 | ForEach-Object { "$_" }))
    $environment.Add("")
    $environment.Add("=== dotnet --list-sdks ===")
    $environment.AddRange([string[]]@(& dotnet --list-sdks 2>&1 | ForEach-Object { "$_" }))
    $environment.Add("")
    $environment.Add("=== dotnet --list-runtimes ===")
    $environment.AddRange([string[]]@(& dotnet --list-runtimes 2>&1 | ForEach-Object { "$_" }))
    $environment | Set-Content -LiteralPath (Join-Path $reportFolder "Environment.txt") -Encoding UTF8

    $restoreLog = Join-Path $reportFolder "Restore.log"
    $debugBuildLog = Join-Path $reportFolder "DebugBuild.log"
    $regressionLog = Join-Path $reportFolder "RegressionTests.log"
    $auditLog = Join-Path $reportFolder "CashierAuditTests.log"
    $releaseBuildLog = Join-Path $reportFolder "ReleaseBuild.log"
    $migrationLog = Join-Path $reportFolder "MigrationSmoke.log"

    [void](Invoke-LoggedCommand "Restore" $restoreLog "dotnet" @("restore", ".\POS.sln"))
    [void](Invoke-LoggedCommand "Debug Build" $debugBuildLog "dotnet" @("build", ".\POS.sln", "--configuration", "Debug", "--no-restore"))
    [void](Invoke-LoggedCommand "Existing Regression Suite" $regressionLog "dotnet" @(
        "run", "--project", ".\POS.Core.CalculationTests\POS.Core.CalculationTests.csproj",
        "--configuration", "Debug", "--no-build"))
    [void](Invoke-LoggedCommand "Cashier Audit Tests" $auditLog "dotnet" @(
        "run", "--project", ".\POS.Cashier.AuditTests\POS.Cashier.AuditTests.csproj",
        "--configuration", "Debug", "--no-build", "--", "cashier"))
    [void](Invoke-LoggedCommand "Release Build" $releaseBuildLog "dotnet" @("build", ".\POS.sln", "--configuration", "Release", "--no-restore"))
    [void](Invoke-LoggedCommand "Migration From Empty" $migrationLog "dotnet" @(
        "run", "--project", ".\POS.Cashier.AuditTests\POS.Cashier.AuditTests.csproj",
        "--configuration", "Release", "--no-build", "--", "migration-empty"))
    [void](Invoke-LoggedCommand "Approved Baseline Upgrade" $migrationLog "dotnet" @(
        "run", "--project", ".\POS.Cashier.AuditTests\POS.Cashier.AuditTests.csproj",
        "--configuration", "Release", "--no-build", "--", "migration-upgrade") -Append)

    $regressionPass = Count-LogLines $regressionLog '^PASS:'
    $auditPass = Count-LogLines $auditLog '^PASS:'
    $auditFail = Count-LogLines $auditLog '^FAIL:'
    $auditSkip = Count-LogLines $auditLog '^SKIP:'
    $migrationPass = Count-LogLines $migrationLog '^PASS:'
    $migrationFail = Count-LogLines $migrationLog '^FAIL:'
    $migrationSkip = Count-LogLines $migrationLog '^SKIP:'

    $failureLines = New-Object System.Collections.Generic.List[string]
    foreach ($entry in $commandResults.GetEnumerator()) {
        if ([int]$entry.Value -ne 0) {
            $failureLines.Add("COMMAND FAILED: $($entry.Key) (exit $($entry.Value))")
        }
    }
    foreach ($log in @($regressionLog, $auditLog, $migrationLog)) {
        if (Test-Path -LiteralPath $log) {
            foreach ($match in @(Select-String -LiteralPath $log -Pattern '^FAIL:' -ErrorAction SilentlyContinue)) {
                $failureLines.Add("$($match.Path):$($match.LineNumber): $($match.Line)")
            }
        }
    }
    if ($failureLines.Count -eq 0) {
        $failureLines.Add("No failed tests or commands were recorded.")
    }
    $failureLines | Set-Content -LiteralPath $failedTestsPath -Encoding UTF8

    $resultRows = foreach ($entry in $commandResults.GetEnumerator()) {
        "| $($entry.Key) | $($entry.Value) | $(if ([int]$entry.Value -eq 0) { 'PASS' } else { 'FAIL' }) |"
    }
    $summary = @"
# Cashier Audit Summary

Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')

## Baseline

- Branch: $branch
- HEAD: $head
- Approved baseline ancestor: $ExpectedBaseline
- Live database protected path: $liveDbPath
- Temporary database root: $tempAuditRoot
- Live database opened by audit tests: **No â€” tests receive explicit temporary SQLite paths**

## Command results

| Command | Exit code | Result |
|---|---:|---|
$($resultRows -join "`r`n")

## Test counts

- Existing regression PASS lines: **$regressionPass** (expected registration count: 230)
- Cashier audit PASS: **$auditPass**
- Cashier audit FAIL: **$auditFail**
- Cashier audit SKIP: **$auditSkip**
- Migration PASS: **$migrationPass**
- Migration FAIL: **$migrationFail**
- Migration SKIP: **$migrationSkip**

## Interpretation

- A nonzero Cashier audit result is expected while a verified defect is still present. The failing test name identifies the production correction required.
- `Approved baseline database upgrades safely` is skipped when `-UpgradeBaselineDb` is not supplied.
- Physical receipt printer, cash drawer and barcode scanner checks remain short manual hardware tests.

## Output files

- Environment.txt
- Baseline.txt
- Restore.log
- DebugBuild.log
- RegressionTests.log
- CashierAuditTests.log
- ReleaseBuild.log
- MigrationSmoke.log
- FailedTests.txt
- CashierAuditSummary.md
"@
    $summary | Set-Content -LiteralPath (Join-Path $reportFolder "CashierAuditSummary.md") -Encoding UTF8
}
catch {
    $fatal = "FATAL AUDIT RUNNER ERROR: $($_.Exception.Message)`r`n$($_.ScriptStackTrace)"
    Write-Host $fatal -ForegroundColor Red
    Add-Content -LiteralPath $failedTestsPath -Value $fatal -Encoding UTF8
    if (-not (Test-Path -LiteralPath (Join-Path $reportFolder "CashierAuditSummary.md"))) {
        @"
# Cashier Audit Summary

The audit runner stopped before completion.

$fatal
"@ | Set-Content -LiteralPath (Join-Path $reportFolder "CashierAuditSummary.md") -Encoding UTF8
    }
    $commandResults["Runner"] = 1
}
finally {
    Remove-Item Env:POS_AUDIT_REPO_ROOT -ErrorAction SilentlyContinue
    Remove-Item Env:POS_AUDIT_TEMP_ROOT -ErrorAction SilentlyContinue
    Remove-Item Env:POS_AUDIT_LIVE_DB_PATH -ErrorAction SilentlyContinue
    Remove-Item Env:POS_AUDIT_UPGRADE_BASELINE -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $tempAuditRoot) {
        Remove-Item -LiteralPath $tempAuditRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    New-Item -ItemType Directory -Path $ReportRoot -Force | Out-Null
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -Path (Join-Path $reportFolder "*") -DestinationPath $zipPath -CompressionLevel Optimal -Force

    Write-Host "`nReport folder: $reportFolder" -ForegroundColor Green
    Write-Host "Report ZIP:    $zipPath" -ForegroundColor Green
}

$overallFailure = @($commandResults.GetEnumerator() | Where-Object { [int]$_.Value -ne 0 }).Count -gt 0
if ($overallFailure) { exit 1 }
exit 0
