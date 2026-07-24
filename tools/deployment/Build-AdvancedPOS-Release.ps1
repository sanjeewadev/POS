[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [string]$RepositoryPath = "",
    [string]$SqlServerExpressInstallerPath = "",
    [string]$OutputDirectory = "",
    [string]$ExpectedBranch = "sanjeewadev"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-NativeSuccess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Step
    )

    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE."
    }
}

function Get-CentralVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VersionFile
    )

    [xml]$xml = Get-Content `
        -LiteralPath $VersionFile `
        -Raw

    $node = $xml.SelectSingleNode(
        "/Project/PropertyGroup/Version")

    if ($null -eq $node -or
        [string]::IsNullOrWhiteSpace($node.InnerText)) {
        throw "Directory.Build.props does not define Version."
    }

    return $node.InnerText.Trim()
}

function Set-CentralVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VersionFile,

        [Parameter(Mandatory = $true)]
        [string]$OldVersion,

        [Parameter(Mandatory = $true)]
        [string]$NewVersion
    )

    [string]$text = Get-Content `
        -LiteralPath $VersionFile `
        -Raw

    [string]$oldMarker =
        "<Version>$OldVersion</Version>"

    [string]$newMarker =
        "<Version>$NewVersion</Version>"

    [int]$occurrences =
        ([regex]::Matches(
            $text,
            [regex]::Escape($oldMarker))).Count

    if ($occurrences -ne 1) {
        throw @"
Expected exactly one central version marker.

Marker: $oldMarker
Found:  $occurrences
"@
    }

    $text = $text.Replace(
        $oldMarker,
        $newMarker)

    $utf8NoBom = New-Object `
        System.Text.UTF8Encoding($false)

    [IO.File]::WriteAllText(
        $VersionFile,
        $text,
        $utf8NoBom)
}

if ([string]::IsNullOrWhiteSpace($RepositoryPath)) {
    $RepositoryPath = [IO.Path]::GetFullPath(
        (Join-Path $PSScriptRoot "..\.."))
}
else {
    $RepositoryPath =
        [IO.Path]::GetFullPath($RepositoryPath)
}

$versionFile = Join-Path `
    $RepositoryPath `
    "Directory.Build.props"

$productionBuilder = Join-Path `
    $RepositoryPath `
    "tools\deployment\Build-POS-Production-Installers.ps1"

$auditScript = Join-Path `
    $RepositoryPath `
    "Run-Cashier-Audit.ps1"

foreach ($requiredFile in @(
        $versionFile,
        $productionBuilder,
        $auditScript
    )) {
    if (-not (
        Test-Path `
            -LiteralPath $requiredFile `
            -PathType Leaf
    )) {
        throw "Required release file was not found: $requiredFile"
    }
}

Set-Location -LiteralPath $RepositoryPath

Write-Host "`n=== VERIFY RELEASE SOURCE ===" `
    -ForegroundColor Cyan

$currentBranch = (
    & git branch --show-current
).Trim()
Assert-NativeSuccess "Read current branch"

$currentCommit = (
    & git rev-parse HEAD
).Trim()
Assert-NativeSuccess "Read current commit"

$status = @(& git status --porcelain)
Assert-NativeSuccess "Read repository status"

if ($currentBranch -ne $ExpectedBranch) {
    throw @"
Unexpected release branch.

Expected: $ExpectedBranch
Actual:   $currentBranch
"@
}

if ($status.Count -ne 0) {
    $status | ForEach-Object {
        Write-Host $_ -ForegroundColor Yellow
    }

    throw "The repository must be clean before release preparation."
}

$remotes = @(& git remote)
Assert-NativeSuccess "Enumerate remotes"

if ($remotes -notcontains "origin") {
    throw "The release repository has no origin remote."
}

& git fetch origin --prune
Assert-NativeSuccess "Fetch origin"

$remoteCommit = (
    & git rev-parse "origin/$ExpectedBranch"
).Trim()
Assert-NativeSuccess "Read remote release branch"

if ($remoteCommit -ne $currentCommit) {
    throw @"
Local and remote release commits do not match.

Local:  $currentCommit
Remote: $remoteCommit
"@
}

$currentVersion = Get-CentralVersion `
    -VersionFile $versionFile

Write-Host "Branch:         $currentBranch"
Write-Host "Commit:         $currentCommit"
Write-Host "Source version: $currentVersion"
Write-Host "Target version: $Version"

if ($currentVersion -ne $Version) {
    $currentSemanticVersion = [version]$currentVersion
    $targetSemanticVersion = [version]$Version

    if ($targetSemanticVersion -le $currentSemanticVersion) {
        throw @"
The target release version must be greater than the current version.

Current: $currentVersion
Target:  $Version
"@
    }

    $confirmation = Read-Host `
        "Type RELEASE to prepare, verify, commit, and push version $Version"

    if ($confirmation -cne "RELEASE") {
        throw "Release confirmation was not provided."
    }

    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $workBranch =
        "work/release-$($Version.Replace('.', '-'))-$timestamp"
    $recoveryBranch =
        "backup/pre-release-$($Version.Replace('.', '-'))-$timestamp"
    $recoveryZip = Join-Path `
        ([Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop)) `
        "POS_PreRelease_${currentVersion}_${timestamp}.zip"

    try {
        Write-Host "`n=== CREATE RELEASE RECOVERY POINT ===" `
            -ForegroundColor Cyan

        & git branch $recoveryBranch $currentCommit
        Assert-NativeSuccess "Create release recovery branch"

        & git archive `
            --format=zip `
            "--output=$recoveryZip" `
            $currentCommit
        Assert-NativeSuccess "Create pre-release source ZIP"

        & git switch -c $workBranch
        Assert-NativeSuccess "Create release work branch"

        Set-CentralVersion `
            -VersionFile $versionFile `
            -OldVersion $currentVersion `
            -NewVersion $Version

        & git add -- "Directory.Build.props"
        Assert-NativeSuccess "Stage central version"

        $changedFiles = @(
            & git diff --cached --name-only
        )
        Assert-NativeSuccess "Read staged version files"

        if ($changedFiles.Count -ne 1 -or
            $changedFiles[0] -ne "Directory.Build.props") {
            throw "Release preparation changed an unexpected file."
        }

        & git diff --cached --check
        Assert-NativeSuccess "Check staged version change"

        Write-Host "`n=== VERIFY RELEASE VERSION ===" `
            -ForegroundColor Cyan

        $reportRoot = Join-Path `
            ([Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop)) `
            "POS_Cashier_Audit_Reports"

        & powershell.exe `
            -NoProfile `
            -ExecutionPolicy Bypass `
            -File $auditScript `
            -RepositoryPath $RepositoryPath `
            -ExpectedBranch $workBranch `
            -ExpectedBaseline $currentCommit `
            -ReportRoot $reportRoot

        if ($LASTEXITCODE -ne 0) {
            throw "Release-version verification failed."
        }

        & git commit `
            -m "Release Advanced POS $Version"
        Assert-NativeSuccess "Commit release version"

        $releaseCommit = (
            & git rev-parse HEAD
        ).Trim()
        Assert-NativeSuccess "Read release commit"

        & git switch $ExpectedBranch
        Assert-NativeSuccess "Switch to target branch"

        & git pull --ff-only origin $ExpectedBranch
        Assert-NativeSuccess "Refresh target branch"

        $targetHead = (
            & git rev-parse HEAD
        ).Trim()
        Assert-NativeSuccess "Read target branch before merge"

        if ($targetHead -ne $currentCommit) {
            throw @"
The target branch changed during release preparation.

Expected: $currentCommit
Actual:   $targetHead

The verified release commit remains on $workBranch.
"@
        }

        & git merge --ff-only $workBranch
        Assert-NativeSuccess "Fast-forward release version"

        & git push origin $ExpectedBranch
        Assert-NativeSuccess "Push release version"

        & git fetch origin
        Assert-NativeSuccess "Refresh release remote"

        $remoteReleaseCommit = (
            & git rev-parse "origin/$ExpectedBranch"
        ).Trim()
        Assert-NativeSuccess "Verify pushed release commit"

        if ($remoteReleaseCommit -ne $releaseCommit) {
            throw "Remote release commit verification failed."
        }

        & git branch -d $workBranch
        Assert-NativeSuccess "Delete release work branch"

        $currentCommit = $releaseCommit

        Write-Host "Release commit:   $releaseCommit" `
            -ForegroundColor Green
        Write-Host "Recovery branch: $recoveryBranch" `
            -ForegroundColor Yellow
        Write-Host "Recovery ZIP:    $recoveryZip" `
            -ForegroundColor Yellow
    }
    catch {
        Write-Host "`nRELEASE PREPARATION STOPPED" `
            -ForegroundColor Red
        Write-Host $_.Exception.Message `
            -ForegroundColor Red
        Write-Host `
            "Do not reset or start another release until the Git state is reviewed." `
            -ForegroundColor Yellow
        throw
    }
}
else {
    Write-Host `
        "The committed source is already aligned to version $Version." `
        -ForegroundColor Green
}

$finalStatus = @(& git status --porcelain)
Assert-NativeSuccess "Verify clean release source"

if ($finalStatus.Count -ne 0) {
    throw "The source is not clean before installer creation."
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $OutputDirectory = Join-Path `
        ([Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop)) `
        "Advanced_POS_${Version}_Production_Release_${timestamp}"
}

Write-Host "`n=== BUILD PRODUCTION INSTALLERS ===" `
    -ForegroundColor Cyan

$buildParameters = @{
    Version = $Version
    OutputDirectory = $OutputDirectory
}

if (-not [string]::IsNullOrWhiteSpace(
        $SqlServerExpressInstallerPath)) {
    $buildParameters["SqlServerExpressInstallerPath"] =
        $SqlServerExpressInstallerPath
}

& $productionBuilder @buildParameters

if ($LASTEXITCODE -ne 0) {
    throw "Production installer build failed with exit code $LASTEXITCODE."
}

$serverInstaller = Join-Path `
    $OutputDirectory `
    "Advanced_POS_Server_Setup_${Version}.exe"

$cashierInstaller = Join-Path `
    $OutputDirectory `
    "Advanced_POS_Cashier_Setup_${Version}.exe"

foreach ($installer in @(
        $serverInstaller,
        $cashierInstaller
    )) {
    if (-not (
        Test-Path `
            -LiteralPath $installer `
            -PathType Leaf
    )) {
        throw "Expected installer was not created: $installer"
    }
}

Write-Host "`nADVANCED POS RELEASE COMPLETED." `
    -ForegroundColor Green
Write-Host "Version: $Version"
Write-Host "Commit:  $currentCommit"
Write-Host "Output:  $OutputDirectory"
Write-Host "Server:  $serverInstaller"
Write-Host "Cashier: $cashierInstaller"
