using POS.Core.Data.Configuration;

namespace POS.Database.Setup;

internal static class Program
{
    private const string ProductName = "Advanced POS Database Setup";
    private const string VersionText = "Network Rehearsal 1.0";

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
                    await ProvisionAsync(options, rehearsalMode: false);
                    return 0;

                case "provision-rehearsal":
                    await ProvisionAsync(options, rehearsalMode: true);
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

                case "restore":
                    await RestoreAsync(options);
                    return 0;

                case "cleanup-rehearsal":
                    await CleanupAsync(options);
                    return 0;

                default:
                    throw new ArgumentException($"Unknown command: {options.Command}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("SETUP FAILED:");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task ProvisionAsync(
        CommandLineArguments options,
        bool rehearsalMode)
    {
        string instance = options.GetOptional("instance", @".\SQLEXPRESS");
        string database = options.GetRequired("database");
        string appLogin = options.GetRequired("app-login");
        string appPassword = options.GetRequiredSecret("app-password", "POS_SETUP_APP_PASSWORD");
        string sqlitePath = options.GetRequired("sqlite");
        string profilePath = options.GetRequired("profile");
        string host = options.GetOptional("host", "127.0.0.1");
        int port = options.GetInt("port", DatabaseConnectionSettings.DefaultSqlServerPort);
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
            !database.Contains("Rehearsal", StringComparison.OrdinalIgnoreCase))
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

            Console.WriteLine("Writing an isolated encrypted connection profile...");
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
            string backupDirectory = await backup.GetDefaultBackupDirectoryAsync(instance);
            string backupPath = Path.Combine(
                backupDirectory,
                $"{database}_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
            await backup.CreateAndVerifyBackupAsync(
                instance,
                database,
                backupPath);

            bool restoreDrillPerformed = false;
            if (rehearsalMode)
            {
                Console.WriteLine("Running a destructive restore drill on the disposable rehearsal database...");
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

            await reportWriter.WriteAsync(
                reportPath,
                new
                {
                    Status = "Passed",
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
                    TableCounts = copiedCounts
                });

            Console.WriteLine(
                rehearsalMode
                    ? "POS NETWORK REHEARSAL DATABASE PROVISIONING PASSED."
                    : "POS NETWORK PRODUCTION DATABASE PROVISIONING PASSED.");
            Console.WriteLine($"Database: {database}");
            Console.WriteLine($"Tables:   {copiedCounts.Count}");
            Console.WriteLine($"Rows:     {copiedCounts.Values.Sum()}");
            Console.WriteLine($"Profile:  {Path.GetFullPath(profilePath)}");
            Console.WriteLine($"Backup:   {backupPath}");
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

    private static async Task VerifyAsync(CommandLineArguments options)
    {
        var provisioning = new ServerProvisioningService();
        await provisioning.VerifyApplicationLoginAsync(
            options.GetRequired("host"),
            options.GetInt("port", DatabaseConnectionSettings.DefaultSqlServerPort),
            options.GetRequired("database"),
            options.GetRequired("app-login"),
            options.GetRequiredSecret("app-password", "POS_SETUP_APP_PASSWORD"));

        Console.WriteLine("SQL Server application connection verified.");
    }

    private static void WriteProfile(CommandLineArguments options)
    {
        new ProfileCommandService().WriteSqlServerProfile(
            options.GetRequired("profile"),
            options.GetRequired("host"),
            options.GetInt("port", DatabaseConnectionSettings.DefaultSqlServerPort),
            options.GetRequired("database"),
            options.GetRequired("app-login"),
            options.GetRequiredSecret("app-password", "POS_SETUP_APP_PASSWORD"));

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

    private static async Task CleanupAsync(CommandLineArguments options)
    {
        string database = options.GetRequired("database");
        string appLogin = options.GetRequired("app-login");

        if (!database.Contains("Rehearsal", StringComparison.OrdinalIgnoreCase) ||
            !appLogin.Contains("Rehearsal", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The cleanup-rehearsal command only accepts database and login names containing 'Rehearsal'.");
        }

        await new ServerProvisioningService().DropRehearsalDatabaseAndLoginAsync(
            options.GetOptional("instance", @".\SQLEXPRESS"),
            database,
            appLogin);
        Console.WriteLine("Rehearsal SQL Server database and login removed.");
    }

    private static void PrintHelp()
    {
        Console.WriteLine(ProductName);
        Console.WriteLine(VersionText);
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  provision             Create a new production network database without replacement");
        Console.WriteLine("  provision-rehearsal   Create or replace a disposable rehearsal database");
        Console.WriteLine("  verify                Verify restricted SQL Server application login");
        Console.WriteLine("  write-profile         Write an encrypted SQL Server connection profile");
        Console.WriteLine("  write-standalone-profile  Write an encrypted standalone SQLite profile");
        Console.WriteLine("  backup                Create and verify a SQL Server backup");
        Console.WriteLine("  restore               Restore a SQL Server backup with explicit confirmation");
        Console.WriteLine("  cleanup-rehearsal     Remove a disposable rehearsal database and login");
    }
}
