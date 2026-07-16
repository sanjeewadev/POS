namespace POS.Cashier.AuditTests;

internal static class DatabaseFoundationSourcePolicyAuditTests
{
    public static Task DualProviderFoundationIsControlledAsync()
    {
        string coreProject = Read("POS.Core", "POS.Core.csproj");
        AuditAssert.Contains(
            coreProject,
            "Microsoft.EntityFrameworkCore.SqlServer\" Version=\"8.0.28",
            "approved SQL Server EF Core provider");
        AuditAssert.Contains(
            coreProject,
            "System.Security.Cryptography.ProtectedData",
            "Windows encrypted settings dependency");

        string settings = Read(
            "POS.Core",
            "Data",
            "Configuration",
            "DatabaseConnectionSettings.cs");
        AuditAssert.Contains(settings, "DatabaseProviderKind.Sqlite",
            "standalone SQLite provider option");
        AuditAssert.Contains(settings, "DatabaseProviderKind.SqlServer",
            "central SQL Server provider option");
        AuditAssert.Contains(settings, "Encrypt = SqlConnectionEncryptOption.Mandatory",
            "mandatory encrypted SQL Server connection");
        AuditAssert.Contains(settings, "SQL Server transport encryption must remain enabled",
            "fail-closed SQL Server encryption policy");
        AuditAssert.Contains(settings, "PersistSecurityInfo = false",
            "non-persistent SQL credentials");
        AuditAssert.Contains(settings, "DataSource = $\"tcp:{ServerHost.Trim()},{ServerPort}\"",
            "explicit fixed SQL Server TCP endpoint");

        string store = Read(
            "POS.Core",
            "Data",
            "Configuration",
            "DatabaseConnectionSettingsStore.cs");
        AuditAssert.Contains(store, "ProtectedData.Protect",
            "encrypted database settings write");
        AuditAssert.Contains(store, "ProtectedData.Unprotect",
            "encrypted database settings read");
        AuditAssert.Contains(store, "DataProtectionScope.CurrentUser",
            "current-Windows-user encryption scope");
        AuditAssert.Contains(store, "database.connection.dat",
            "local encrypted database settings file");
        AuditAssert.False(
            store.Contains("appsettings.json", StringComparison.OrdinalIgnoreCase),
            "database password must not be placed in a plaintext appsettings file");

        string configurator = Read(
            "POS.Core",
            "Data",
            "Configuration",
            "PosDatabaseOptionsConfigurator.cs");
        AuditAssert.Contains(configurator, "UseSqlite",
            "SQLite provider configuration");
        AuditAssert.Contains(configurator, "UseSqlServer",
            "SQL Server provider configuration");
        AuditAssert.Contains(configurator, "EnableRetryOnFailure",
            "SQL Server transient retry policy");

        string collations = Read(
            "POS.Core",
            "Data",
            "Configuration",
            "DatabaseProviderModelConventions.cs");
        AuditAssert.Contains(collations, "database.IsSqlServer()",
            "SQL Server collation provider selection");
        AuditAssert.Contains(collations, "database.IsSqlite()",
            "SQLite collation provider selection");
        AuditAssert.Contains(collations, "Latin1_General_100_CI_AS_SC",
            "SQL Server case-insensitive collation");
        AuditAssert.Contains(collations, "NOCASE",
            "SQLite case-insensitive collation");
        AuditAssert.Contains(collations, "nvarchar(max)",
            "SQL Server large-text column type");
        AuditAssert.Contains(collations, "TEXT",
            "SQLite large-text column type");

        string context = Read("POS.Core", "Data", "AppDbContext.cs");
        AuditAssert.Contains(context, "DatabaseProviderModelConventions.GetCaseInsensitive",
            "provider-aware model collation configuration");
        AuditAssert.Contains(context, "DatabaseProviderModelConventions.GetLargeTextColumnType",
            "provider-aware large-text column configuration");
        AuditAssert.False(
            context.Contains("UseCollation(\"NOCASE\")", StringComparison.Ordinal),
            "provider-specific NOCASE must not remain hard-coded in entity mappings");

        string repositoriesRoot = Path.Combine(
            AuditPaths.RepositoryRoot,
            "POS.Core",
            "Repositories");
        string[] hardCodedRepositoryCollations = Directory
            .GetFiles(repositoriesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "\"NOCASE\"",
                StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path) ?? path)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        AuditAssert.True(
            hardCodedRepositoryCollations.Length == 0,
            "repositories still hard-code SQLite NOCASE: " +
            string.Join(", ", hardCodedRepositoryCollations));

        string initialization = Read(
            "POS.Core",
            "Data",
            "Configuration",
            "DatabaseInitializationService.cs");
        AuditAssert.Contains(initialization, "if (_settings.IsStandaloneSqlite)",
            "provider-specific database initialization policy");
        AuditAssert.Contains(initialization, "MigrateAsync",
            "legacy standalone SQLite migration path");
        AuditAssert.Contains(initialization, "CanConnectAsync",
            "central store-server connectivity check");
        AuditAssert.Contains(initialization, "schema is missing or incompatible",
            "central schema compatibility failure message");

        string cashierApp = Read("POS.Cashier.UI", "App.xaml.cs");
        AuditAssert.Contains(cashierApp, "DatabaseConnectionSettingsStore",
            "Cashier encrypted database profile loading");
        AuditAssert.Contains(cashierApp, "DatabaseInitializationService",
            "Cashier controlled database initialization");
        AuditAssert.False(
            cashierApp.Contains("options.UseSqlite", StringComparison.Ordinal),
            "Cashier startup still hard-codes SQLite");
        AuditAssert.False(
            cashierApp.Contains("Database.MigrateAsync", StringComparison.Ordinal),
            "Cashier startup must not directly migrate a central database");

        string backOfficeApp = Read("POS.BackOffice.UI", "App.xaml.cs");
        AuditAssert.Contains(backOfficeApp, "DatabaseConnectionSettingsStore",
            "BackOffice encrypted database profile loading");
        AuditAssert.Contains(backOfficeApp, "DatabaseInitializationService",
            "BackOffice controlled database initialization");
        AuditAssert.False(
            backOfficeApp.Contains("options.UseSqlite", StringComparison.Ordinal),
            "BackOffice startup still hard-codes SQLite");

        string backupService = Read(
            "POS.Core",
            "Services",
            "Backup",
            "BackupService.cs");
        AuditAssert.Contains(backupService, "IsCentralSqlServer",
            "central-mode local backup safety guard");
        AuditAssert.Contains(backupService,
            "Local SQLite backup and restore are unavailable",
            "central-mode backup failure message");

        return Task.CompletedTask;
    }

    private static string Read(params string[] parts)
    {
        string path = Path.Combine(
            new[] { AuditPaths.RepositoryRoot }.Concat(parts).ToArray());
        return File.ReadAllText(path);
    }
}
