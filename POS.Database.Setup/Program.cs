using System.Text.Json;
using POS.Core.Configuration;
using POS.Core.Data.Configuration;
using POS.Core.Models.Licensing;

namespace POS.Database.Setup;

internal static class Program
{
    private const string ProductName = "Advanced POS Database Setup";
    private const string VersionText = "Production 1.0.0";

    public static async Task<int> Main(string[] args)
    {
        try
        {
            CommandLineArguments options = CommandLineArguments.Parse(args);

            switch (options.Command)
            {
                case "help":
                case "--help":
                case "-h":
                    PrintHelp();
                    return 0;

                case "version":
                    Console.WriteLine($"{ProductName} - {VersionText}");
                    return 0;

                case "provision":
                    await ProvisionFromSqliteAsync(options, rehearsalMode: false);
                    return 0;

                case "provision-rehearsal":
                    await ProvisionFromSqliteAsync(options, rehearsalMode: true);
                    return 0;

                case "provision-empty":
                    await ProvisionEmptyAsync(options);
                    return 0;

                case "provision-restore":
                    await ProvisionRestoreAsync(options);
                    return 0;

                case "configure-terminal":
                    await ConfigureTerminalAsync(options);
                    return 0;

                case "import-license":
                    await ImportLicenseAsync(options);
                    return 0;

                case "verify":
                    await VerifyAsync(options);
                    return 0;

                case "write-profile":
                    WriteProfile(options);
                    return 0;

                case "write-standalone-profile":
                    WriteStandaloneProfile(options);
                    return 0;

                case "backup":
                    await BackupAsync(options);
                    return 0;

                case "backup-copy":
                    await BackupCopyAsync(options);
                    return 0;

                case "restore":
                    await RestoreAsync(options);
                    return 0;

                case "check":
                    await CheckAsync(options);
                    return 0;

                case "status":
                    await StatusAsync(options);
                    return 0;

                case "cleanup-rehearsal":
                    await CleanupAsync(options);
                    return 0;

                default:
                    throw new ArgumentException(
                        $"Unknown command: {options.Command}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("SETUP FAILED:");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task ProvisionFromSqliteAsync(
        CommandLineArguments options,
        bool rehearsalMode)
    {
        string instance = options.GetOptional("instance", @".\SQLEXPRESS");
        string database = options.GetRequired("database");
        string appLogin = options.GetRequired("app-login");
        string appPassword = options.GetRequiredSecret(
            "app-password",
            "POS_SETUP_APP_PASSWORD");
        string sqlitePath = options.GetRequired("sqlite");
        string profilePath = options.GetRequired("profile");
        string host = options.GetOptional("host", "127.0.0.1");
        int port = options.GetInt(
            "port",
            DatabaseConnectionSettings.DefaultSqlServerPort);
        string reportPath = options.GetRequired("report");
        bool replace = options.HasFlag("replace");
        bool cleanupOnFailure = options.HasFlag("cleanup-on-failure");

        if (!rehearsalMode && replace)
        {
            throw new InvalidOperationException(
                "The production provision command never permits --replace.");
        }

        if (!rehearsalMode && cleanupOnFailure)
        {
            throw new InvalidOperationException(
                "The production provision command never permits --cleanup-on-failure.");
        }

        if (rehearsalMode &&
            !database.Contains(
                "Rehearsal",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "A rehearsal database name must contain the word 'Rehearsal'.");
        }

        var provisioning = new ServerProvisioningService();
        var transfer = new SqliteToSqlServerTransferService();
        var profile = new ProfileCommandService();
        var backup = new SqlServerBackupService();
        var reportWriter = new SetupReportWriter();

        try
        {
            string modeLabel = rehearsalMode ? "rehearsal" : "production";
            Console.WriteLine($"Creating the {modeLabel} SQL Server database...");

            await provisioning.ProvisionAsync(
                instance,
                database,
                appLogin,
                appPassword,
                replace);

            string adminDatabaseConnection =
                SqlServerConnectionFactory.BuildAdministratorConnectionString(
                    instance,
                    database);

            Console.WriteLine("Transferring SQLite data to SQL Server...");

            IReadOnlyDictionary<string, long> copiedCounts =
                await transfer.TransferAsync(
                    sqlitePath,
                    adminDatabaseConnection);

            Console.WriteLine("Verifying the restricted application login...");
            await provisioning.VerifyApplicationLoginAsync(
                host,
                port,
                database,
                appLogin,
                appPassword);

            Console.WriteLine("Writing the encrypted connection profile...");
            profile.WriteSqlServerProfile(
                profilePath,
                host,
                port,
                database,
                appLogin,
                appPassword);

            Console.WriteLine("Checking SQL Server database integrity...");
            await provisioning.VerifyDatabaseIntegrityAsync(
                instance,
                database);

            Console.WriteLine("Creating and verifying a SQL Server backup...");
            string backupPath = await CreateDefaultBackupAsync(
                backup,
                instance,
                database);

            bool restoreDrillPerformed = false;
            if (rehearsalMode)
            {
                Console.WriteLine(
                    "Running a destructive restore drill on the disposable rehearsal database...");

                await backup.RestoreBackupAsync(
                    instance,
                    database,
                    backupPath);

                await provisioning.VerifyDatabaseIntegrityAsync(
                    instance,
                    database);

                await provisioning.VerifyApplicationLoginAsync(
                    host,
                    port,
                    database,
                    appLogin,
                    appPassword);

                restoreDrillPerformed = true;
            }

            DatabaseSummary summary =
                await provisioning.GetDatabaseSummaryAsync(
                    instance,
                    database);

            await reportWriter.WriteAsync(
                reportPath,
                new
                {
                    Status = "Passed",
                    Mode = rehearsalMode
                        ? "RehearsalMigration"
                        : "ProductionMigration",
                    ProductVersion = ProductReleaseInfo.ProductVersion,
                    GeneratedAt = DateTimeOffset.Now,
                    Instance = instance,
                    Host = host,
                    Port = port,
                    Database = database,
                    ApplicationLogin = appLogin,
                    SourceSqlite = Path.GetFullPath(sqlitePath),
                    EncryptedProfile = Path.GetFullPath(profilePath),
                    BackupPath = backupPath,
                    RestoreDrillPerformed = restoreDrillPerformed,
                    TablesTransferred = copiedCounts.Count,
                    RowsTransferred = copiedCounts.Values.Sum(),
                    DatabaseTableCount = summary.TableCount,
                    DatabaseRowCount = summary.RowCount,
                    LatestMigration = summary.LatestMigration,
                    TableCounts = copiedCounts
                });

            Console.WriteLine(
                rehearsalMode
                    ? "POS NETWORK REHEARSAL DATABASE PROVISIONING PASSED."
                    : "POS NETWORK PRODUCTION DATABASE PROVISIONING PASSED.");

            PrintProvisioningSummary(
                database,
                summary,
                profilePath,
                backupPath);
        }
        catch
        {
            if (cleanupOnFailure)
            {
                try
                {
                    await provisioning.DropRehearsalDatabaseAndLoginAsync(
                        instance,
                        database,
                        appLogin);
                }
                catch (Exception cleanupError)
                {
                    Console.Error.WriteLine(
                        "Automatic rehearsal cleanup also failed: " +
                        cleanupError.Message);
                }
            }

            throw;
        }
    }

    private static async Task ProvisionEmptyAsync(
        CommandLineArguments options)
    {
        string instance = options.GetOptional("instance", @".\SQLEXPRESS");
        string database = options.GetRequired("database");
        string appLogin = options.GetRequired("app-login");
        string appPassword = options.GetRequiredSecret(
            "app-password",
            "POS_SETUP_APP_PASSWORD");
        string profilePath = options.GetRequired("profile");
        string host = options.GetOptional("host", "127.0.0.1");
        int port = options.GetInt(
            "port",
            DatabaseConnectionSettings.DefaultSqlServerPort);
        string reportPath = options.GetRequired("report");

        var provisioning = new ServerProvisioningService();
        var profile = new ProfileCommandService();
        var backup = new SqlServerBackupService();

        Console.WriteLine("Creating a new production SQL Server database...");
        await provisioning.ProvisionAsync(
            instance,
            database,
            appLogin,
            appPassword,
            replaceExisting: false);

        await provisioning.VerifyApplicationLoginAsync(
            host,
            port,
            database,
            appLogin,
            appPassword);

        profile.WriteSqlServerProfile(
            profilePath,
            host,
            port,
            database,
            appLogin,
            appPassword);

        await provisioning.VerifyDatabaseIntegrityAsync(
            instance,
            database);

        string backupPath = await CreateDefaultBackupAsync(
            backup,
            instance,
            database);

        DatabaseSummary summary =
            await provisioning.GetDatabaseSummaryAsync(
                instance,
                database);

        await new SetupReportWriter().WriteAsync(
            reportPath,
            new
            {
                Status = "Passed",
                Mode = "NewStore",
                ProductVersion = ProductReleaseInfo.ProductVersion,
                GeneratedAt = DateTimeOffset.Now,
                Instance = instance,
                Host = host,
                Port = port,
                Database = database,
                ApplicationLogin = appLogin,
                EncryptedProfile = Path.GetFullPath(profilePath),
                BackupPath = backupPath,
                DatabaseTableCount = summary.TableCount,
                DatabaseRowCount = summary.RowCount,
                LatestMigration = summary.LatestMigration
            });

        Console.WriteLine("POS NETWORK NEW-STORE PROVISIONING PASSED.");
        PrintProvisioningSummary(
            database,
            summary,
            profilePath,
            backupPath);
    }

    private static async Task ProvisionRestoreAsync(
        CommandLineArguments options)
    {
        if (!options.HasFlag("confirm-destructive-restore"))
        {
            throw new InvalidOperationException(
                "Production restore requires --confirm-destructive-restore.");
        }

        string instance = options.GetOptional("instance", @".\SQLEXPRESS");
        string database = options.GetRequired("database");
        string appLogin = options.GetRequired("app-login");
        string appPassword = options.GetRequiredSecret(
            "app-password",
            "POS_SETUP_APP_PASSWORD");
        string sourceBackupFile = options.GetRequired("file");
        string profilePath = options.GetRequired("profile");
        string host = options.GetOptional("host", "127.0.0.1");
        int port = options.GetInt(
            "port",
            DatabaseConnectionSettings.DefaultSqlServerPort);
        string reportPath = options.GetRequired("report");

        var backup = new SqlServerBackupService();
        var provisioning = new ServerProvisioningService();

        string preRestoreBackup = string.Empty;
        if (await provisioning.DatabaseExistsAsync(instance, database))
        {
            Console.WriteLine(
                "Creating a verified safety backup of the current production database...");

            preRestoreBackup = await CreateDefaultBackupAsync(
                backup,
                instance,
                database);
        }

        Console.WriteLine(
            "Staging the selected backup in the SQL Server backup directory...");

        string stagedBackup =
            await backup.StageBackupForSqlServerAsync(
                instance,
                sourceBackupFile);

        Console.WriteLine("Restoring the production SQL Server backup...");
        await backup.RestoreBackupAsync(
            instance,
            database,
            stagedBackup);

        Console.WriteLine("Applying the matching production schema...");
        await provisioning.ApplyMigrationsAsync(
            instance,
            database);

        Console.WriteLine("Creating or repairing the restricted application login...");
        await provisioning.EnsureApplicationLoginAsync(
            instance,
            database,
            appLogin,
            appPassword);

        await provisioning.VerifyApplicationLoginAsync(
            host,
            port,
            database,
            appLogin,
            appPassword);

        new ProfileCommandService().WriteSqlServerProfile(
            profilePath,
            host,
            port,
            database,
            appLogin,
            appPassword);

        await provisioning.VerifyDatabaseIntegrityAsync(
            instance,
            database);

        string verificationBackup = await CreateDefaultBackupAsync(
            backup,
            instance,
            database);

        DatabaseSummary summary =
            await provisioning.GetDatabaseSummaryAsync(
                instance,
                database);

        await new SetupReportWriter().WriteAsync(
            reportPath,
            new
            {
                Status = "Passed",
                Mode = "Restore",
                ProductVersion = ProductReleaseInfo.ProductVersion,
                GeneratedAt = DateTimeOffset.Now,
                Instance = instance,
                Host = host,
                Port = port,
                Database = database,
                ApplicationLogin = appLogin,
                SourceBackup = Path.GetFullPath(sourceBackupFile),
                StagedBackup = stagedBackup,
                PreRestoreSafetyBackup = preRestoreBackup,
                EncryptedProfile = Path.GetFullPath(profilePath),
                VerificationBackup = verificationBackup,
                DatabaseTableCount = summary.TableCount,
                DatabaseRowCount = summary.RowCount,
                LatestMigration = summary.LatestMigration
            });

        Console.WriteLine("POS NETWORK PRODUCTION RESTORE PASSED.");
        PrintProvisioningSummary(
            database,
            summary,
            profilePath,
            verificationBackup);
    }

    private static async Task ConfigureTerminalAsync(
        CommandLineArguments options)
    {
        string appPassword = options.GetRequiredSecret(
            "app-password",
            "POS_SETUP_APP_PASSWORD");

        TerminalProvisioningResult result =
            await new TerminalProvisioningService().ConfigureAsync(
                options.GetRequired("host"),
                options.GetInt(
                    "port",
                    DatabaseConnectionSettings.DefaultSqlServerPort),
                options.GetRequired("database"),
                options.GetRequired("app-login"),
                appPassword,
                options.GetRequired("terminal-no"),
                options.GetOptional("terminal-name", string.Empty),
                options.GetOptional("location", "Main Store"),
                options.GetRequired("profile"),
                options.GetOptional(
                    "updated-by",
                    "Production terminal installer"));

        string reportPath = options.GetOptional("report", string.Empty);
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            await new SetupReportWriter().WriteAsync(
                reportPath,
                new
                {
                    Status = "Passed",
                    Mode = "Terminal",
                    ProductVersion = ProductReleaseInfo.ProductVersion,
                    GeneratedAt = DateTimeOffset.Now,
                    Host = options.GetRequired("host"),
                    Port = options.GetInt(
                        "port",
                        DatabaseConnectionSettings.DefaultSqlServerPort),
                    Database = options.GetRequired("database"),
                    ApplicationLogin = options.GetRequired("app-login"),
                    result.TerminalNo,
                    result.TerminalName,
                    result.MachineName,
                    result.MachineCode,
                    result.Location,
                    result.ProfilePath
                });
        }

        Console.WriteLine("POS NETWORK TERMINAL CONFIGURATION PASSED.");
        Console.WriteLine($"Terminal:     {result.TerminalNo}");
        Console.WriteLine($"Machine:      {result.MachineName}");
        Console.WriteLine($"Machine code: {result.MachineCode}");
        Console.WriteLine($"Profile:      {result.ProfilePath}");
    }

    private static async Task ImportLicenseAsync(
        CommandLineArguments options)
    {
        InstalledLicense installed =
            await new LicenseImportCommandService().ImportAsync(
                options.GetRequired("profile"),
                options.GetRequired("file"),
                options.GetOptional("imported-by", "Production installer"));

        string reportPath = options.GetOptional("report", string.Empty);
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            await new SetupReportWriter().WriteAsync(
                reportPath,
                new
                {
                    Status = "Passed",
                    Mode = "LicenceImport",
                    ProductVersion = ProductReleaseInfo.ProductVersion,
                    GeneratedAt = DateTimeOffset.Now,
                    installed.LicenseId,
                    LicenseType = installed.LicenseType.ToString(),
                    installed.StoreId,
                    installed.StoreName,
                    installed.TerminalNo,
                    installed.MachineCode,
                    installed.ExpiresOn
                });
        }

        Console.WriteLine("POS LICENCE IMPORT PASSED.");
        Console.WriteLine($"Licence:  {installed.LicenseId}");
        Console.WriteLine($"Type:     {installed.LicenseType}");
        Console.WriteLine($"Expires:  {installed.ExpiresOn:yyyy-MM-dd}");
    }

    private static async Task VerifyAsync(CommandLineArguments options)
    {
        await new ServerProvisioningService().VerifyApplicationLoginAsync(
            options.GetRequired("host"),
            options.GetInt(
                "port",
                DatabaseConnectionSettings.DefaultSqlServerPort),
            options.GetRequired("database"),
            options.GetRequired("app-login"),
            options.GetRequiredSecret(
                "app-password",
                "POS_SETUP_APP_PASSWORD"));

        Console.WriteLine("SQL Server application connection verified.");
    }

    private static void WriteProfile(CommandLineArguments options)
    {
        new ProfileCommandService().WriteSqlServerProfile(
            options.GetRequired("profile"),
            options.GetRequired("host"),
            options.GetInt(
                "port",
                DatabaseConnectionSettings.DefaultSqlServerPort),
            options.GetRequired("database"),
            options.GetRequired("app-login"),
            options.GetRequiredSecret(
                "app-password",
                "POS_SETUP_APP_PASSWORD"));

        Console.WriteLine("Encrypted SQL Server profile written and verified.");
    }

    private static void WriteStandaloneProfile(CommandLineArguments options)
    {
        new ProfileCommandService().WriteStandaloneProfile(
            options.GetRequired("profile"));

        Console.WriteLine("Standalone SQLite profile written and verified.");
    }

    private static async Task BackupAsync(CommandLineArguments options)
    {
        await new SqlServerBackupService().CreateAndVerifyBackupAsync(
            options.GetOptional("instance", @".\SQLEXPRESS"),
            options.GetRequired("database"),
            options.GetRequired("file"));

        Console.WriteLine("SQL Server backup created and verified.");
    }

    private static async Task BackupCopyAsync(CommandLineArguments options)
    {
        BackupCopyResult result =
            await new SqlServerBackupService().CreateVerifiedBackupCopyAsync(
                options.GetOptional("instance", @".\SQLEXPRESS"),
                options.GetRequired("database"),
                options.GetRequired("destination"));

        Console.WriteLine("SQL Server backup created, verified, and copied.");
        Console.WriteLine($"SQL path:    {result.SqlServerBackupPath}");
        Console.WriteLine($"Destination: {result.DestinationPath}");
        Console.WriteLine($"SHA-256:     {result.Sha256}");
    }

    private static async Task RestoreAsync(CommandLineArguments options)
    {
        if (!options.HasFlag("confirm-destructive-restore"))
        {
            throw new InvalidOperationException(
                "Restore requires --confirm-destructive-restore because all active database connections will be terminated.");
        }

        await new SqlServerBackupService().RestoreBackupAsync(
            options.GetOptional("instance", @".\SQLEXPRESS"),
            options.GetRequired("database"),
            options.GetRequired("file"));

        Console.WriteLine("SQL Server backup restored.");
    }

    private static async Task CheckAsync(CommandLineArguments options)
    {
        string instance = options.GetOptional("instance", @".\SQLEXPRESS");
        string database = options.GetRequired("database");

        var service = new ServerProvisioningService();
        await service.VerifyDatabaseIntegrityAsync(instance, database);
        DatabaseSummary summary =
            await service.GetDatabaseSummaryAsync(instance, database);

        Console.WriteLine("SQL Server database integrity check passed.");
        PrintDatabaseSummary(database, summary);
    }

    private static async Task StatusAsync(CommandLineArguments options)
    {
        string instance = options.GetOptional("instance", @".\SQLEXPRESS");
        string database = options.GetRequired("database");

        DatabaseSummary summary =
            await new ServerProvisioningService()
                .GetDatabaseSummaryAsync(instance, database);

        var result = new
        {
            Status = "Available",
            ProductVersion = ProductReleaseInfo.ProductVersion,
            Instance = instance,
            Database = database,
            summary.TableCount,
            summary.RowCount,
            summary.LatestMigration,
            GeneratedAt = DateTimeOffset.Now
        };

        Console.WriteLine(
            JsonSerializer.Serialize(
                result,
                new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task CleanupAsync(CommandLineArguments options)
    {
        string database = options.GetRequired("database");
        string appLogin = options.GetRequired("app-login");

        if (!database.Contains(
                "Rehearsal",
                StringComparison.OrdinalIgnoreCase) ||
            !appLogin.Contains(
                "Rehearsal",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The cleanup-rehearsal command only accepts database and login names containing 'Rehearsal'.");
        }

        await new ServerProvisioningService()
            .DropRehearsalDatabaseAndLoginAsync(
                options.GetOptional("instance", @".\SQLEXPRESS"),
                database,
                appLogin);

        Console.WriteLine("Rehearsal SQL Server database and login removed.");
    }

    private static async Task<string> CreateDefaultBackupAsync(
        SqlServerBackupService backup,
        string instance,
        string database)
    {
        string backupDirectory =
            await backup.GetDefaultBackupDirectoryAsync(instance);

        string backupPath = Path.Combine(
            backupDirectory,
            $"{database}_{DateTime.Now:yyyyMMdd_HHmmss}.bak");

        await backup.CreateAndVerifyBackupAsync(
            instance,
            database,
            backupPath);

        return backupPath;
    }

    private static void PrintProvisioningSummary(
        string database,
        DatabaseSummary summary,
        string profilePath,
        string backupPath)
    {
        PrintDatabaseSummary(database, summary);
        Console.WriteLine($"Profile:   {Path.GetFullPath(profilePath)}");
        Console.WriteLine($"Backup:    {backupPath}");
    }

    private static void PrintDatabaseSummary(
        string database,
        DatabaseSummary summary)
    {
        Console.WriteLine($"Database:  {database}");
        Console.WriteLine($"Tables:    {summary.TableCount}");
        Console.WriteLine($"Rows:      {summary.RowCount}");
        Console.WriteLine($"Migration: {summary.LatestMigration}");
    }

    private static void PrintHelp()
    {
        Console.WriteLine(ProductName);
        Console.WriteLine(VersionText);
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  provision             Create a production database by migrating SQLite");
        Console.WriteLine("  provision-rehearsal   Create or replace a disposable rehearsal database");
        Console.WriteLine("  provision-empty       Create a clean new-store production database");
        Console.WriteLine("  provision-restore     Restore and repair a production SQL backup");
        Console.WriteLine("  configure-terminal    Bind this computer to a terminal and write its profile");
        Console.WriteLine("  import-license        Import a signed store or terminal licence");
        Console.WriteLine("  verify                Verify restricted SQL Server application login");
        Console.WriteLine("  write-profile         Write an encrypted SQL Server connection profile");
        Console.WriteLine("  write-standalone-profile  Write an encrypted standalone SQLite profile");
        Console.WriteLine("  backup                Create and verify a SQL Server backup");
        Console.WriteLine("  backup-copy           Create a verified backup and copy it to technician storage");
        Console.WriteLine("  restore               Restore a SQL Server backup with explicit confirmation");
        Console.WriteLine("  check                 Run DBCC CHECKDB and print database counts");
        Console.WriteLine("  status                Print production database status as JSON");
        Console.WriteLine("  cleanup-rehearsal     Remove a disposable rehearsal database and login");
    }
}
