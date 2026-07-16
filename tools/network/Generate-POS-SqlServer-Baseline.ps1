[CmdletBinding()]
param(
    [string]$RepositoryPath = $PSScriptRoot,
    [string]$OutputScriptPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-Native {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Write-Host ("$ " + $FilePath + " " + ($Arguments -join " ")) -ForegroundColor DarkGray

    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & $FilePath @Arguments
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }

    if ($exitCode -ne 0) {
        throw ("Command failed with exit code {0}: {1} {2}" -f `
            $exitCode, `
            $FilePath, `
            ($Arguments -join ' '))
    }
}

function Assert-Contains {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string]$Expected,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    if ($Text.IndexOf($Expected, [StringComparison]::Ordinal) -lt 0) {
        throw "$Label verification failed. Expected text was not found: $Expected"
    }
}

function Assert-Regex {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string]$Pattern,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    if (-not [regex]::IsMatch(
        $Text,
        $Pattern,
        [Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        throw "$Label verification failed. Expected pattern was not found: $Pattern"
    }
}

function Assert-NoSqliteTokens {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    $forbiddenTokens = @(
        "Sqlite:Autoincrement",
        "AUTOINCREMENT",
        "COLLATE NOCASE",
        "sqlite_master"
    )

    foreach ($token in $forbiddenTokens) {
        if ($Text.IndexOf($token, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "$Label contains SQLite-only token: $token"
        }
    }

    if ([regex]::IsMatch(
        $Text,
        'migrationBuilder\.Sql\s*\(\s*@?"[^"]*\bPRAGMA\b',
        [Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        throw "$Label contains SQLite PRAGMA SQL."
    }
}

$RepositoryPath = [IO.Path]::GetFullPath($RepositoryPath)
if (-not (Test-Path -LiteralPath (Join-Path $RepositoryPath "POS.sln") -PathType Leaf)) {
    throw "POS.sln was not found at repository path: $RepositoryPath"
}

Set-Location -LiteralPath $RepositoryPath

$migrationsDirectory = Join-Path $RepositoryPath "POS.Database.Setup\Migrations"
if (-not (Test-Path -LiteralPath $migrationsDirectory -PathType Container)) {
    throw "The fixed SQL Server migrations directory was not found: $migrationsDirectory"
}

if ([string]::IsNullOrWhiteSpace($OutputScriptPath)) {
    $reportRoot = Join-Path $HOME "Desktop\POS_SQLServer_Schema_Reports"
    New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
    $OutputScriptPath = Join-Path $reportRoot (
        "POS_Network_Initial_SQLServer_Schema_" +
        (Get-Date -Format "yyyyMMdd_HHmmss") +
        ".sql")
}
else {
    $OutputScriptPath = [IO.Path]::GetFullPath($OutputScriptPath)
    $outputFolder = Split-Path -Parent $OutputScriptPath
    if (-not [string]::IsNullOrWhiteSpace($outputFolder)) {
        New-Item -ItemType Directory -Path $outputFolder -Force | Out-Null
    }
}

$allMigrationFiles = @(
    Get-ChildItem -LiteralPath $migrationsDirectory -Filter "*.cs" -File |
        Sort-Object Name
)
$primaryMigrations = @(
    $allMigrationFiles |
        Where-Object {
            -not $_.Name.EndsWith(".Designer.cs", [StringComparison]::OrdinalIgnoreCase) -and
            -not $_.Name.EndsWith("ModelSnapshot.cs", [StringComparison]::OrdinalIgnoreCase)
        }
)
$designerFiles = @(
    $allMigrationFiles |
        Where-Object { $_.Name.EndsWith(".Designer.cs", [StringComparison]::OrdinalIgnoreCase) }
)
$snapshotFiles = @(
    $allMigrationFiles |
        Where-Object { $_.Name.EndsWith("ModelSnapshot.cs", [StringComparison]::OrdinalIgnoreCase) }
)

if ($primaryMigrations.Count -ne 1 -or
    $designerFiles.Count -ne 1 -or
    $snapshotFiles.Count -ne 1) {
    throw "Expected one fixed SQL Server migration, one designer, and one model snapshot. Found migration=$($primaryMigrations.Count), designer=$($designerFiles.Count), snapshot=$($snapshotFiles.Count)."
}

$migrationText = Get-Content -LiteralPath $primaryMigrations[0].FullName -Raw
$designerText = Get-Content -LiteralPath $designerFiles[0].FullName -Raw
$snapshotText = Get-Content -LiteralPath $snapshotFiles[0].FullName -Raw
$combinedText = $migrationText + $designerText + $snapshotText

Assert-Contains $migrationText "migrationBuilder.CreateTable" "SQL Server CreateTable operations"
Assert-Contains $migrationText 'name: "Users"' "Users table"
Assert-Contains $migrationText 'name: "SalesHeaders"' "SalesHeaders table"
Assert-Contains $migrationText 'name: "ItemVariants"' "ItemVariants table"
Assert-Contains $migrationText 'name: "StoreSettings"' "StoreSettings table"
Assert-Contains $migrationText '.Annotation("SqlServer:Identity", "1, 1")' "SQL Server identity migration metadata"
Assert-Contains $snapshotText "SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);" "SQL Server model identity convention"
Assert-Contains $snapshotText "SqlServerPropertyBuilderExtensions.UseIdentityColumn" "SQL Server property identity metadata"
Assert-Contains $designerText "SqlServerPropertyBuilderExtensions.UseIdentityColumn" "SQL Server designer identity metadata"
Assert-Contains $designerText "Microsoft.EntityFrameworkCore.Metadata" "migration designer metadata"
Assert-Contains $migrationText 'collation: "Latin1_General_100_CI_AS_SC"' "approved SQL Server collation"
Assert-Contains $migrationText 'type: "nvarchar(' "SQL Server Unicode text types"

$createTableCount = ([regex]::Matches(
    $migrationText,
    [regex]::Escape("migrationBuilder.CreateTable"))).Count
if ($createTableCount -ne 59) {
    throw "The fixed SQL Server baseline must create exactly 59 application tables; actual count: $createTableCount."
}

$identityAnnotationCount = ([regex]::Matches(
    $migrationText,
    [regex]::Escape('.Annotation("SqlServer:Identity", "1, 1")'))).Count
if ($identityAnnotationCount -lt 40) {
    throw "The fixed SQL Server baseline contains only $identityAnnotationCount SQL Server identity annotations."
}

Assert-NoSqliteTokens $combinedText "The fixed SQL Server migration"

$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$project = ".\POS.Database.Setup\POS.Database.Setup.csproj"
$context = "POS.Core.Data.AppDbContext"

Invoke-Native "dotnet" @("tool", "restore")
Invoke-Native "dotnet" @("restore", ".\POS.sln")
Invoke-Native "dotnet" @(
    "build",
    $project,
    "--configuration",
    "Debug",
    "--no-restore")

Invoke-Native "dotnet" @(
    "tool", "run", "dotnet-ef", "--",
    "migrations", "script",
    "--idempotent",
    "--project", $project,
    "--startup-project", $project,
    "--context", $context,
    "--output", $OutputScriptPath,
    "--no-build")

if (-not (Test-Path -LiteralPath $OutputScriptPath -PathType Leaf)) {
    throw "EF Core did not create the SQL Server schema script."
}

$sql = Get-Content -LiteralPath $OutputScriptPath -Raw
Assert-Regex $sql 'CREATE\s+TABLE\s+\[Users\]' "SQL Server Users DDL"
Assert-Regex $sql 'CREATE\s+TABLE\s+\[SalesHeaders\]' "SQL Server SalesHeaders DDL"
Assert-Regex $sql 'CREATE\s+TABLE\s+\[ItemVariants\]' "SQL Server ItemVariants DDL"
Assert-Regex $sql 'CREATE\s+TABLE\s+\[StoreSettings\]' "SQL Server StoreSettings DDL"
Assert-Contains $sql "[__EFMigrationsHistory]" "EF migration history DDL"
$identityDdlPattern = '\]\s+(?:bigint|int|smallint|tinyint)\s+NOT\s+NULL\s+IDENTITY(?:\s*\(\s*\d+\s*,\s*\d+\s*\))?'
$identityDdlCount = ([regex]::Matches(
    $sql,
    $identityDdlPattern,
    [Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
if ($identityDdlCount -lt 40) {
    throw "The generated SQL Server script contains only $identityDdlCount identity column definitions."
}

$sqlCreateTableCount = ([regex]::Matches(
    $sql,
    'CREATE\s+TABLE\s+\[',
    [Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
if ($sqlCreateTableCount -lt 59) {
    throw "The generated idempotent SQL script contains only $sqlCreateTableCount CREATE TABLE statements."
}

Assert-NoSqliteTokens $sql "The generated SQL Server schema script"

Write-Host
Write-Host "POS FIXED SQL SERVER BASELINE VALIDATED." -ForegroundColor Green
Write-Host "Migration:  $($primaryMigrations[0].Name)"
Write-Host "Tables:     $createTableCount"
Write-Host "Migration identities: $identityAnnotationCount"
Write-Host "SQL identities:       $identityDdlCount"
Write-Host "SQL script: $OutputScriptPath"
