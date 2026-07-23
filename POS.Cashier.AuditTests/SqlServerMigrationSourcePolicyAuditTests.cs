using System.Text.RegularExpressions;

namespace POS.Cashier.AuditTests;

internal static class SqlServerMigrationSourcePolicyAuditTests
{
    public static Task DedicatedSqlServerBaselineIsControlledAsync()
    {
        string solution = Read("POS.sln");
        AuditAssert.Contains(
            solution,
            "POS.Database.Setup\\POS.Database.Setup.csproj",
            "dedicated SQL Server setup project in solution");

        string project = Read(
            "POS.Database.Setup",
            "POS.Database.Setup.csproj");
        AuditAssert.Contains(
            project,
            "Microsoft.EntityFrameworkCore.Design\" Version=\"8.0.28",
            "locked EF Core design package");
        AuditAssert.Contains(
            project,
            "Microsoft.EntityFrameworkCore.SqlServer\" Version=\"8.0.28",
            "locked SQL Server provider package");
        AuditAssert.Contains(
            project,
            "Microsoft.Data.Sqlite\" Version=\"8.0.28",
            "locked SQLite transfer provider");
        AuditAssert.Contains(
            project,
            "..\\POS.Core\\POS.Core.csproj",
            "setup project reference to the production model");

        string factory = Read(
            "POS.Database.Setup",
            "SqlServerDesignTimeDbContextFactory.cs");
        AuditAssert.Contains(
            factory,
            "IDesignTimeDbContextFactory<AppDbContext>",
            "SQL Server design-time context factory");
        AuditAssert.Contains(
            factory,
            "PosDatabaseOptionsConfigurator.SqlServerMigrationsAssembly",
            "dedicated migrations assembly selection");
        AuditAssert.False(
            factory.Contains("Database.Migrate", StringComparison.Ordinal),
            "design-time factory must not update a live database");

        string configurator = Read(
            "POS.Core",
            "Data",
            "Configuration",
            "PosDatabaseOptionsConfigurator.cs");
        AuditAssert.Contains(
            configurator,
            "SqlServerMigrationsAssembly = \"POS.Database.Setup\"",
            "approved SQL Server migrations assembly name");
        AuditAssert.Contains(
            configurator,
            "sqlServerOptions.MigrationsAssembly",
            "explicit SQL Server migrations assembly configuration");

        string setupProgram = Read(
            "POS.Database.Setup",
            "Program.cs");
        AuditAssert.Contains(
            setupProgram,
            "The production provision command never permits --replace",
            "production database replacement protection");
        AuditAssert.Contains(
            setupProgram,
            "The production provision command never permits --cleanup-on-failure",
            "production database cleanup protection");
        AuditAssert.Contains(
            setupProgram,
            "Restore requires --confirm-destructive-restore",
            "explicit destructive restore confirmation");
        AuditAssert.Contains(
            setupProgram,
            "provision-rehearsal",
            "disposable rehearsal provisioning command");
        AuditAssert.Contains(
            setupProgram,
            "cleanup-rehearsal command only accepts",
            "rehearsal cleanup name guard");
        AuditAssert.Contains(
            setupProgram,
            "RestoreDrillPerformed",
            "disposable backup restore drill reporting");

        string provisioning = Read(
            "POS.Database.Setup",
            "ServerProvisioningService.cs");
        AuditAssert.Contains(
            provisioning,
            "Database.MigrateAsync",
            "controlled SQL Server schema application");
        AuditAssert.Contains(
            provisioning,
            "db_datareader",
            "restricted application read role");
        AuditAssert.Contains(
            provisioning,
            "db_datawriter",
            "restricted application write role");
        AuditAssert.False(
            provisioning.Contains("db_backupoperator", StringComparison.Ordinal),
            "application login must not receive SQL Server backup privileges");
        AuditAssert.Contains(
            provisioning,
            "DBCC CHECKDB",
            "SQL Server integrity verification");
        AuditAssert.Contains(
            provisioning,
            "UPDATE [StoreSettings] SET [StoreName]=[StoreName] WHERE 1=0",
            "restricted application write-permission verification");

        string transfer = Read(
            "POS.Database.Setup",
            "SqliteToSqlServerTransferService.cs");
        AuditAssert.Contains(
            transfer,
            "PRAGMA integrity_check",
            "SQLite source integrity verification");
        AuditAssert.Contains(
            transfer,
            "PRAGMA foreign_key_check",
            "SQLite source foreign-key verification");
        AuditAssert.Contains(
            transfer,
            "SqlBulkCopyOptions.KeepIdentity",
            "identity-preserving SQLite transfer");
        AuditAssert.Contains(
            transfer,
            "WITH CHECK CHECK CONSTRAINT ALL",
            "post-transfer foreign-key validation");
        AuditAssert.Contains(
            transfer,
            "Data-transfer count mismatch",
            "table row-count reconciliation");

        string settingsStore = Read(
            "POS.Core",
            "Data",
            "Configuration",
            "DatabaseConnectionSettingsStore.cs");
        AuditAssert.Contains(
            settingsStore,
            "POS_DATABASE_SETTINGS_PATH",
            "isolated rehearsal profile override");

        string networkScript = Read(
            "tools",
            "network",
            "Configure-POS-SqlServer-Network.ps1");
        AuditAssert.Contains(
            networkScript,
            "Write-RegistryBackup",
            "SQL Server network configuration backup");
        AuditAssert.Contains(
            networkScript,
            "-Profile Private",
            "private-network-only firewall rule");
        AuditAssert.Contains(
            networkScript,
            "Test-NetConnection",
            "TCP listener verification");

        string repositoryDirectory = Path.Combine(
            AuditPaths.RepositoryRoot,
            "POS.Core",
            "Repositories");
        foreach (string repositoryFile in Directory.GetFiles(
                     repositoryDirectory,
                     "*.cs",
                     SearchOption.TopDirectoryOnly))
        {
            string repositoryText = File.ReadAllText(repositoryFile);
            AuditAssert.False(
                Regex.IsMatch(
                    repositoryText,
                    @"BeginTransactionAsync\s*\(\s*\)"),
                $"repository transaction must explicitly use Serializable isolation: {Path.GetFileName(repositoryFile)}");
        }

        string auditProject = Read(
            "POS.Cashier.AuditTests",
            "POS.Cashier.AuditTests.csproj");
        AuditAssert.Contains(
            auditProject,
            "Microsoft.EntityFrameworkCore.SqlServer\" Version=\"8.0.28",
            "SQL Server provider in Cashier audit project");

        string auditDatabase = Read(
            "POS.Cashier.AuditTests",
            "AuditDatabase.cs");
        AuditAssert.Contains(
            auditDatabase,
            "POS_AUDIT_SQLSERVER_INSTANCE",
            "isolated SQL Server audit provider selection");
        AuditAssert.Contains(
            auditDatabase,
            "UseSqlServer",
            "SQL Server-backed Cashier audit context");
        AuditAssert.Contains(
            auditDatabase,
            "POSAudit_Rehearsal_",
            "disposable SQL Server audit database naming");
        AuditAssert.Contains(
            auditDatabase,
            "SET SINGLE_USER WITH ROLLBACK IMMEDIATE",
            "disposable SQL Server audit cleanup");

        string auditProgram = Read(
            "POS.Cashier.AuditTests",
            "Program.cs");
        AuditAssert.Contains(
            auditProgram,
            "cashier-sqlserver",
            "explicit SQL Server Cashier audit mode");

        string databaseConfigurator = Read(
            "POS.Core",
            "Data",
            "Configuration",
            "PosDatabaseOptionsConfigurator.cs");
        AuditAssert.False(
            databaseConfigurator.Contains(
                "EnableRetryOnFailure",
                StringComparison.Ordinal),
            "SQL Server retrying execution strategy must remain disabled for explicit transactions");

        string migrationsDirectory = Path.Combine(
            AuditPaths.RepositoryRoot,
            "POS.Database.Setup",
            "Migrations");
        AuditAssert.True(
            Directory.Exists(migrationsDirectory),
            "SQL Server migrations directory was not generated");

        string[] migrationFiles = Directory
            .GetFiles(migrationsDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        string[] primaryMigrations = migrationFiles
            .Where(path =>
                !path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        string[] designers = migrationFiles
            .Where(path => path.EndsWith(
                ".Designer.cs",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        string[] snapshots = migrationFiles
            .Where(path => path.EndsWith(
                "ModelSnapshot.cs",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        AuditAssert.True(
            primaryMigrations.Length == 2,
            $"expected the SQL Server baseline and collation repair migrations, found {primaryMigrations.Length}");
        AuditAssert.True(
            designers.Length == 2,
            $"expected two SQL Server migration designers, found {designers.Length}");
        AuditAssert.True(
            snapshots.Length == 1,
            $"expected one SQL Server model snapshot, found {snapshots.Length}");

        string baselineMigrationPath = primaryMigrations.Single(path =>
            Path.GetFileName(path).Contains(
                "Phase11B1_InitialSqlServerBaseline",
                StringComparison.Ordinal));
        string repairMigrationPath = primaryMigrations.Single(path =>
            Path.GetFileName(path).Contains(
                "RepairOperationalTextCollations",
                StringComparison.Ordinal));

        string migration = File.ReadAllText(baselineMigrationPath);
        string repairMigration = File.ReadAllText(repairMigrationPath);
        string designer = string.Join(
            Environment.NewLine,
            designers.Select(File.ReadAllText));
        string snapshot = File.ReadAllText(snapshots[0]);
        string combined = migration + repairMigration + designer + snapshot;

        AuditAssert.Contains(
            migration,
            "migrationBuilder.CreateTable",
            "generated SQL Server table creation operations");
        AuditAssert.Contains(
            migration,
            "name: \"Users\"",
            "Users table in SQL Server baseline");
        AuditAssert.Contains(
            migration,
            "name: \"SalesHeaders\"",
            "SalesHeaders table in SQL Server baseline");
        AuditAssert.Contains(
            migration,
            ".Annotation(\"SqlServer:Identity\", \"1, 1\")",
            "SQL Server identity metadata in migration");
        AuditAssert.Contains(
            snapshot,
            "SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);",
            "SQL Server identity convention in model snapshot");
        AuditAssert.Contains(
            snapshot,
            "SqlServerPropertyBuilderExtensions.UseIdentityColumn",
            "SQL Server property identity metadata in model snapshot");
        AuditAssert.Contains(
            designer,
            "SqlServerPropertyBuilderExtensions.UseIdentityColumn",
            "SQL Server property identity metadata in migration designer");
        AuditAssert.Contains(
            designer,
            "Microsoft.EntityFrameworkCore.Metadata",
            "generated SQL Server migration metadata");

        int createTableCount = Regex.Matches(
            migration,
            Regex.Escape("migrationBuilder.CreateTable")).Count;
        AuditAssert.True(
            createTableCount == 59,
            $"expected 59 SQL Server application tables, found {createTableCount}");

        string[] forbidden =
        {
            "Sqlite:Autoincrement",
            "AUTOINCREMENT",
            "COLLATE NOCASE",
            "sqlite_master"
        };

        foreach (string token in forbidden)
        {
            AuditAssert.False(
                combined.Contains(token, StringComparison.OrdinalIgnoreCase),
                $"SQL Server migration contains SQLite-only token '{token}'");
        }

        AuditAssert.False(
            Regex.IsMatch(
                combined,
                @"migrationBuilder\.Sql\s*\(\s*@?""[^""]*\bPRAGMA\b",
                RegexOptions.IgnoreCase),
            "SQL Server migration contains SQLite PRAGMA SQL");

        return Task.CompletedTask;
    }

    private static string Read(params string[] parts)
    {
        string path = Path.Combine(
            new[] { AuditPaths.RepositoryRoot }.Concat(parts).ToArray());
        return File.ReadAllText(path);
    }
}
