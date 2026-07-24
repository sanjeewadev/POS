[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$')]
    [string]$Version = "",

    [string]$OutputDirectory = "",
    [string]$InnoCompilerPath = "",
    [string]$SqlServerExpressInstallerPath = "",
    [switch]$AllowServerInstallerWithoutSqlExpress,
    [switch]$SkipSqlServerAudit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repository = [IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot "..\.."))

$centralVersionFile = Join-Path `
    $repository `
    "Directory.Build.props"

if (-not (Test-Path -LiteralPath $centralVersionFile -PathType Leaf)) {
    throw "Central version file was not found: $centralVersionFile"
}

[xml]$centralVersionXml = Get-Content `
    -LiteralPath $centralVersionFile `
    -Raw

$centralVersionNode = $centralVersionXml.SelectSingleNode(
    "/Project/PropertyGroup/Version")

if ($null -eq $centralVersionNode -or
    [string]::IsNullOrWhiteSpace($centralVersionNode.InnerText)) {
    throw "Directory.Build.props does not define the central Version value."
}

$centralVersion = $centralVersionNode.InnerText.Trim()

$productReleaseInfoFile = Join-Path `
    $repository `
    "POS.Core\Configuration\ProductReleaseInfo.cs"

if (-not (Test-Path -LiteralPath $productReleaseInfoFile -PathType Leaf)) {
    throw "Product release information file was not found: $productReleaseInfoFile"
}

$productReleaseInfoSource = Get-Content `
    -LiteralPath $productReleaseInfoFile `
    -Raw

$requiredMigrationMatch = [regex]::Match(
    $productReleaseInfoSource,
    'RequiredSqlServerMigration\s*=\s*\r?\n?\s*"(?<id>\d+_[A-Za-z0-9_]+)"',
    [Text.RegularExpressions.RegexOptions]::CultureInvariant)

if (-not $requiredMigrationMatch.Success) {
    throw "ProductReleaseInfo does not define RequiredSqlServerMigration."
}

$requiredMigrationId =
    $requiredMigrationMatch.Groups["id"].Value

$requiredMigrationFile = Join-Path `
    $repository `
    "POS.Database.Setup\Migrations\$requiredMigrationId.cs"

if (-not (Test-Path -LiteralPath $requiredMigrationFile -PathType Leaf)) {
    throw @"
The required SQL Server migration is not present in the release source.

Migration: $requiredMigrationId
Expected:  $requiredMigrationFile
"@
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $centralVersion
}
elseif ($Version -ne $centralVersion) {
    throw @"
The requested installer version does not match the committed source version.

Requested: $Version
Source:    $centralVersion

Use Build-AdvancedPOS-Release.ps1 to prepare a new release version first.
"@
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path `
        ([Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop)) `
        "Advanced_POS_$Version"
}
else {
    $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$buildRoot = Join-Path $env:TEMP "AdvancedPOS-Deployment-$timestamp"
$stagingRoot = Join-Path $buildRoot "Staging"
$publishRoot = Join-Path $buildRoot "Publish"
$serverStage = Join-Path $stagingRoot "Server"
$cashierStage = Join-Path $stagingRoot "Cashier"

$env:MSBUILDDISABLENODEREUSE = "1"
$env:DOTNET_CLI_USE_MSBUILD_SERVER = "0"
$env:UseSharedCompilation = "false"

$previousSqlAudit = [Environment]::GetEnvironmentVariable(
    "POS_AUDIT_SQLSERVER_INSTANCE",
    "Process")

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Label,
        [Parameter(Mandatory = $true)]
        [string]$Command,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Write-Host
    Write-Host "=== $Label ===" -ForegroundColor Cyan
    Write-Host "$Command $($Arguments -join ' ')" -ForegroundColor DarkGray

    & $Command @Arguments
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        throw "$Label failed with exit code $exitCode."
    }
}

function Resolve-InnoCompiler {
    if (-not [string]::IsNullOrWhiteSpace($InnoCompilerPath)) {
        $candidate = [IO.Path]::GetFullPath($InnoCompilerPath)
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
        throw "Inno Setup compiler was not found: $candidate"
    }

    $candidates = @()

    $programFilesX86 = [Environment]::GetFolderPath(
        [Environment+SpecialFolder]::ProgramFilesX86)

    $programFiles = [Environment]::GetFolderPath(
        [Environment+SpecialFolder]::ProgramFiles)

    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $candidates += Join-Path $programFilesX86 "Inno Setup 6\ISCC.exe"
    }

    if (-not [string]::IsNullOrWhiteSpace($programFiles)) {
        $candidates += Join-Path $programFiles "Inno Setup 6\ISCC.exe"
    }

    foreach ($candidate in $candidates) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and
            (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return $candidate
        }
    }

    throw "Inno Setup 6 compiler (ISCC.exe) is required to build the two production setup files."
}

function Resolve-SqlServerExpressInstaller {
    $candidate = $null

    if (-not [string]::IsNullOrWhiteSpace($SqlServerExpressInstallerPath)) {
        $candidate = [IO.Path]::GetFullPath($SqlServerExpressInstallerPath)
    }
    else {
        $searchFolders = @(
            [Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop),
            (Join-Path $HOME "Downloads")
        ) | Where-Object {
            -not [string]::IsNullOrWhiteSpace($_) -and
            (Test-Path -LiteralPath $_ -PathType Container)
        }

        $candidate = $searchFolders | ForEach-Object {
            Get-ChildItem `
                -LiteralPath $_ `
                -File `
                -Filter "SQLEXPR_x64_ENU.exe" `
                -ErrorAction SilentlyContinue
        } | Sort-Object LastWriteTime -Descending |
            Select-Object -ExpandProperty FullName -First 1
    }

    if ([string]::IsNullOrWhiteSpace($candidate)) {
        if ($AllowServerInstallerWithoutSqlExpress) {
            Write-Warning `
                "Building a diagnostic Server installer without the SQL Server Express prerequisite."
            return ""
        }

        throw ((@(
            "A full production Server installer must include SQL Server 2022 Express.",
            "Download the offline Express Core package named SQLEXPR_x64_ENU.exe",
            "and place it on the Desktop or in Downloads, or pass",
            "-SqlServerExpressInstallerPath with its full path.",
            "Use -AllowServerInstallerWithoutSqlExpress only for a non-customer diagnostic build."
        )) -join " ")
    }

    $candidate = [IO.Path]::GetFullPath($candidate)

    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "SQL Server Express installer was not found: $candidate"
    }

    if ([IO.Path]::GetFileName($candidate) -ne "SQLEXPR_x64_ENU.exe") {
        throw `
            "The offline SQL prerequisite must be the Express Core package named SQLEXPR_x64_ENU.exe. Selected: $candidate"
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $candidate
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or
        $null -eq $signature.SignerCertificate -or
        $signature.SignerCertificate.Subject -notmatch "Microsoft") {
        throw `
            "The SQL Server Express prerequisite does not have a valid Microsoft Authenticode signature: $candidate"
    }

    $versionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($candidate)
    if ($versionInfo.FileMajorPart -ne 16) {
        throw `
            "Advanced POS 1.0 requires the tested SQL Server 2022 Express major version 16 package. Detected version: $($versionInfo.FileVersion)"
    }

    Write-Host `
        "SQL Server Express prerequisite: $candidate" `
        -ForegroundColor Green

    return $candidate
}

function Test-PowerShellSources {
    $files = Get-ChildItem -LiteralPath $repository -Recurse -File -Filter "*.ps1" |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }

    foreach ($file in $files) {
        $tokens = $null
        $errors = $null
        [System.Management.Automation.Language.Parser]::ParseFile(
            $file.FullName,
            [ref]$tokens,
            [ref]$errors) | Out-Null

        if ($errors.Count -gt 0) {
            $messages = ($errors | ForEach-Object { $_.Message }) -join "; "
            throw "PowerShell parser failure in $($file.FullName): $messages"
        }
    }

    Write-Host "PowerShell parser checks passed: $($files.Count) files" -ForegroundColor Green
}

function Test-XamlSources {
    $files = Get-ChildItem -LiteralPath $repository -Recurse -File -Filter "*.xaml" |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }

    foreach ($file in $files) {
        try {
            [xml](Get-Content -LiteralPath $file.FullName -Raw) | Out-Null
        }
        catch {
            throw "XAML XML validation failed in $($file.FullName): $($_.Exception.Message)"
        }
    }

    Write-Host "XAML XML checks passed: $($files.Count) files" -ForegroundColor Green
}

function Publish-Project {
    param(
        [string]$Project,
        [string]$Destination
    )

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null

    Invoke-NativeCommand `
        -Label "Publish $Project" `
        -Command "dotnet" `
        -Arguments @(
            "publish",
            (Join-Path $repository $Project),
            "--configuration", "Release",
            "--runtime", "win-x64",
            "--self-contained", "true",
            "--no-restore",
            "--output", $Destination,
            "-p:Version=$Version",
            "-p:DebugType=None",
            "-p:DebugSymbols=false"
        )
}

function Copy-DirectoryContents {
    param(
        [string]$Source,
        [string]$Destination
    )

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Copy-Item -Path (Join-Path $Source "*") -Destination $Destination -Recurse -Force
}

try {
    if ($env:OS -ne "Windows_NT") {
        throw "Production installers must be built on Windows."
    }

    Set-Location -LiteralPath $repository

    $branch = (git branch --show-current).Trim()
    $commit = (git rev-parse HEAD).Trim()
    $tree = (git rev-parse "HEAD^{tree}").Trim()
    $status = @(git status --porcelain)

    Write-Host "Branch: $branch"
    Write-Host "Commit: $commit"
    Write-Host "Tree:   $tree"

    if ($status.Count -gt 0) {
        git status --short
        throw "Commit the approved release source before building customer installers."
    }

    $dotnet = Get-Command dotnet -ErrorAction Stop
    $inno = Resolve-InnoCompiler

    $SqlServerExpressInstallerPath = Resolve-SqlServerExpressInstaller

    Test-PowerShellSources
    Test-XamlSources

    if (Test-Path -LiteralPath $OutputDirectory -PathType Container) {
        $existingOutput = @(
            Get-ChildItem -LiteralPath $OutputDirectory -Force
        )

        if ($existingOutput.Count -gt 0) {
            throw "The installer output directory must be empty: $OutputDirectory"
        }
    }

    New-Item -ItemType Directory -Path $buildRoot, $OutputDirectory -Force | Out-Null

    & dotnet build-server shutdown | Out-Host

    Invoke-NativeCommand `
        -Label "Restore local tools" `
        -Command "dotnet" `
        -Arguments @("tool", "restore")

    Invoke-NativeCommand `
        -Label "Restore solution" `
        -Command "dotnet" `
        -Arguments @("restore", (Join-Path $repository "POS.sln"), "--runtime", "win-x64")

    Invoke-NativeCommand `
        -Label "Debug solution build" `
        -Command "dotnet" `
        -Arguments @("build", (Join-Path $repository "POS.sln"), "--configuration", "Debug", "--no-restore")

    Invoke-NativeCommand `
        -Label "Core regression suite" `
        -Command "dotnet" `
        -Arguments @(
            "run", "--project",
            (Join-Path $repository "POS.Core.CalculationTests\POS.Core.CalculationTests.csproj"),
            "--configuration", "Debug", "--no-build"
        )

    Invoke-NativeCommand `
        -Label "SQLite Cashier audit suite" `
        -Command "dotnet" `
        -Arguments @(
            "run", "--project",
            (Join-Path $repository "POS.Cashier.AuditTests\POS.Cashier.AuditTests.csproj"),
            "--configuration", "Debug", "--no-build", "--", "cashier"
        )

    if (-not $SkipSqlServerAudit) {
        $service = Get-Service -Name "MSSQL`$SQLEXPRESS" -ErrorAction SilentlyContinue
        if ($null -eq $service) {
            throw "SQL Server Express is required for the production SQL Server audit. Use -SkipSqlServerAudit only for a non-release diagnostic build."
        }

        if ($service.Status -ne "Running") {
            Start-Service -Name "MSSQL`$SQLEXPRESS"
            $service.WaitForStatus(
                [System.ServiceProcess.ServiceControllerStatus]::Running,
                [TimeSpan]::FromSeconds(60))
        }

        $env:POS_AUDIT_SQLSERVER_INSTANCE = ".\SQLEXPRESS"

        try {
            Invoke-NativeCommand `
                -Label "SQL Server Cashier audit suite" `
                -Command "dotnet" `
                -Arguments @(
                    "run", "--project",
                    (Join-Path $repository "POS.Cashier.AuditTests\POS.Cashier.AuditTests.csproj"),
                    "--configuration", "Debug", "--no-build", "--", "cashier-sqlserver"
                )
        }
        finally {
            if ($null -eq $previousSqlAudit) {
                Remove-Item Env:POS_AUDIT_SQLSERVER_INSTANCE -ErrorAction SilentlyContinue
            }
            else {
                $env:POS_AUDIT_SQLSERVER_INSTANCE = $previousSqlAudit
            }
        }
    }

    Invoke-NativeCommand `
        -Label "Release solution build" `
        -Command "dotnet" `
        -Arguments @("build", (Join-Path $repository "POS.sln"), "--configuration", "Release", "--no-restore")

    Publish-Project "POS.BackOffice.UI\POS.BackOffice.UI.csproj" (Join-Path $publishRoot "BackOffice")
    Publish-Project "POS.Cashier.UI\POS.Cashier.UI.csproj" (Join-Path $publishRoot "Cashier")
    Publish-Project "POS.Database.Setup\POS.Database.Setup.csproj" (Join-Path $publishRoot "DatabaseSetup")
    Publish-Project "POS.Deployment.Wizard\POS.Deployment.Wizard.csproj" (Join-Path $publishRoot "DeploymentWizard")

    foreach ($folder in @(
        (Join-Path $serverStage "BackOffice"),
        (Join-Path $serverStage "Cashier"),
        (Join-Path $serverStage "DatabaseSetup"),
        (Join-Path $serverStage "DeploymentWizard"),
        (Join-Path $serverStage "Tools"),
        (Join-Path $serverStage "Docs"),
        (Join-Path $cashierStage "Cashier"),
        (Join-Path $cashierStage "DatabaseSetup"),
        (Join-Path $cashierStage "DeploymentWizard"),
        (Join-Path $cashierStage "Tools"),
        (Join-Path $cashierStage "Docs")
    )) {
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
    }

    Copy-DirectoryContents (Join-Path $publishRoot "BackOffice") (Join-Path $serverStage "BackOffice")
    Copy-DirectoryContents (Join-Path $publishRoot "Cashier") (Join-Path $serverStage "Cashier")
    Copy-DirectoryContents (Join-Path $publishRoot "DatabaseSetup") (Join-Path $serverStage "DatabaseSetup")
    Copy-DirectoryContents (Join-Path $publishRoot "DeploymentWizard") (Join-Path $serverStage "DeploymentWizard")

    Copy-DirectoryContents (Join-Path $publishRoot "Cashier") (Join-Path $cashierStage "Cashier")
    Copy-DirectoryContents (Join-Path $publishRoot "DatabaseSetup") (Join-Path $cashierStage "DatabaseSetup")
    Copy-DirectoryContents (Join-Path $publishRoot "DeploymentWizard") (Join-Path $cashierStage "DeploymentWizard")

    $serverTools = @(
        "tools\network\Configure-POS-SqlServer-Network.ps1",
        "tools\network\Restore-POS-SqlServer-Network.ps1",
        "tools\deployment\Backup-POS-Production.ps1",
        "tools\deployment\Restore-POS-Production.ps1",
        "tools\deployment\Check-POS-Production.ps1",
        "tools\deployment\Show-POS-Server-Status.ps1",
        "tools\deployment\Run-POS-Deployment-Wizard.ps1"
    )

    foreach ($relative in $serverTools) {
        Copy-Item -LiteralPath (Join-Path $repository $relative) -Destination (Join-Path $serverStage "Tools") -Force
    }

    Copy-Item -LiteralPath (Join-Path $repository "tools\deployment\Run-POS-Deployment-Wizard.ps1") `
        -Destination (Join-Path $cashierStage "Tools") -Force

    $documents = @(
        "docs\POS_Production_Server_Installation.md",
        "docs\POS_Production_Cashier_Installation.md",
        "docs\POS_Production_Backup_and_Recovery.md",
        "docs\POS_Production_Customer_Acceptance_Checklist.md",
        "docs\POS_Production_Terminal_Replacement.md",
        "docs\POS_Production_Upgrade_Guide.md",
        "docs\Phase11D_Production_Deployment_and_Recovery.md",
        "docs\Phase11D1_Production_Installer_Hotfix.md",
        "docs\Phase11D2_Installed_System_Licence_Recovery_and_Upgrade.md",
        "docs\Phase11D3_Store_Connection_Resilience_and_Recovery.md"
    )

    foreach ($relative in $documents) {
        $source = Join-Path $repository $relative
        Copy-Item -LiteralPath $source -Destination (Join-Path $serverStage "Docs") -Force
        Copy-Item -LiteralPath $source -Destination (Join-Path $cashierStage "Docs") -Force
    }

    $serverInstallerScript = Join-Path $repository "installers\AdvancedPOSServer.iss"
    $cashierInstallerScript = Join-Path $repository "installers\AdvancedPOSCashier.iss"

    $commonDefines = @(
        "/DSourceRoot=$stagingRoot",
        "/DOutputDir=$OutputDirectory",
        "/DAppVersion=$Version"
    )

    $serverDefines = @($commonDefines)
    if (-not [string]::IsNullOrWhiteSpace($SqlServerExpressInstallerPath)) {
        $serverDefines += "/DSqlExpressInstaller=$SqlServerExpressInstallerPath"
    }

    Invoke-NativeCommand `
        -Label "Build Advanced POS Server installer" `
        -Command $inno `
        -Arguments ($serverDefines + @($serverInstallerScript))

    Invoke-NativeCommand `
        -Label "Build Advanced POS Cashier installer" `
        -Command $inno `
        -Arguments ($commonDefines + @($cashierInstallerScript))

    $serverInstaller = Join-Path $OutputDirectory "Advanced_POS_Server_Setup_$Version.exe"
    $cashierInstaller = Join-Path $OutputDirectory "Advanced_POS_Cashier_Setup_$Version.exe"

    foreach ($file in @($serverInstaller, $cashierInstaller)) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
            throw "Expected installer was not created: $file"
        }
    }

    foreach ($relative in $documents) {
        Copy-Item -LiteralPath (Join-Path $repository $relative) -Destination $OutputDirectory -Force
    }

    $hashLines = @()
    Get-ChildItem -LiteralPath $OutputDirectory -File | Sort-Object Name | ForEach-Object {
        if ($_.Name -ne "SHA256SUMS.txt") {
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            $hashLines += "$hash  $($_.Name)"
        }
    }
    $hashLines | Set-Content -LiteralPath (Join-Path $OutputDirectory "SHA256SUMS.txt") -Encoding ASCII

    $manifest = [ordered]@{
        Product = "Advanced POS"
        Version = $Version
        GeneratedAt = (Get-Date).ToString("o")
        Branch = $branch
        Commit = $commit
        Tree = $tree
        ServerInstaller = [IO.Path]::GetFileName($serverInstaller)
        CashierInstaller = [IO.Path]::GetFileName($cashierInstaller)
        SelfContainedRuntime = "win-x64"
        SqlServerExpressBundled = -not [string]::IsNullOrWhiteSpace($SqlServerExpressInstallerPath)
        SqlServerAuditSkipped = [bool]$SkipSqlServerAudit
        CustomerReadyServerInstaller = `
            -not [string]::IsNullOrWhiteSpace($SqlServerExpressInstallerPath)
    }

    if (-not [string]::IsNullOrWhiteSpace($SqlServerExpressInstallerPath)) {
        $manifest["SqlServerExpressInstallerSha256"] = (
            Get-FileHash -LiteralPath $SqlServerExpressInstallerPath -Algorithm SHA256).Hash
        $manifest["SqlServerExpressInstallerVersion"] = (
            [Diagnostics.FileVersionInfo]::GetVersionInfo(
                $SqlServerExpressInstallerPath)).FileVersion
    }

    $manifest | ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath (Join-Path $OutputDirectory "RELEASE_MANIFEST.json") -Encoding UTF8

    Write-Host
    Write-Host "============================================================" -ForegroundColor Green
    Write-Host "ADVANCED POS PRODUCTION INSTALLERS CREATED" -ForegroundColor Green
    Write-Host "============================================================" -ForegroundColor Green
    Write-Host "Server:  $serverInstaller"
    Write-Host "Cashier: $cashierInstaller"
    Write-Host "Hashes:  $(Join-Path $OutputDirectory 'SHA256SUMS.txt')"
    Write-Host "Manifest:$(Join-Path $OutputDirectory 'RELEASE_MANIFEST.json')"
}
finally {
    if ($null -ne $previousSqlAudit) {
        $env:POS_AUDIT_SQLSERVER_INSTANCE = $previousSqlAudit
    }
    else {
        Remove-Item Env:POS_AUDIT_SQLSERVER_INSTANCE -ErrorAction SilentlyContinue
    }

    if ($null -ne (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        & dotnet build-server shutdown 2>$null | Out-Host
    }

    if (Test-Path -LiteralPath $buildRoot) {
        Remove-Item -LiteralPath $buildRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
