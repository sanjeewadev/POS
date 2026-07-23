using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Data.Configuration;

namespace POS.Database.Setup;

internal sealed class ServerProvisioningService
{
    public async Task ProvisionAsync(
        string instance,
        string databaseName,
        string applicationLogin,
        string applicationPassword,
        bool replaceExisting,
        CancellationToken cancellationToken = default)
    {
        databaseName = SqlName.RequireSafeIdentifier(
            databaseName,
            "Database name");

        applicationLogin = SqlName.RequireSafeIdentifier(
            applicationLogin,
            "Application login");

        ValidateApplicationPassword(applicationPassword);

        string masterConnectionString =
            SqlServerConnectionFactory.BuildAdministratorConnectionString(
                instance);

        bool databaseCreatedByThisRun = false;

        try
        {
            await using (var master = new SqlConnection(masterConnectionString))
            {
                await master.OpenAsync(cancellationToken);

                bool databaseExists =
                    Convert.ToInt32(
                        await ExecuteScalarAsync(
                            master,
                            $"SELECT CASE WHEN DB_ID(N'{SqlName.EscapeLiteral(databaseName)}') IS NULL THEN 0 ELSE 1 END;",
                            cancellationToken)) != 0;

                bool loginExists =
                    Convert.ToInt32(
                        await ExecuteScalarAsync(
                            master,
                            $"SELECT CASE WHEN SUSER_ID(N'{SqlName.EscapeLiteral(applicationLogin)}') IS NULL THEN 0 ELSE 1 END;",
                            cancellationToken)) != 0;

                if (replaceExisting)
                {
                    await ExecuteAsync(
                        master,
                        $"IF DB_ID(N'{SqlName.EscapeLiteral(databaseName)}') IS NOT NULL " +
                        $"BEGIN ALTER DATABASE {SqlName.Quote(databaseName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                        $"DROP DATABASE {SqlName.Quote(databaseName)}; END; " +
                        $"IF SUSER_ID(N'{SqlName.EscapeLiteral(applicationLogin)}') IS NOT NULL " +
                        $"DROP LOGIN {SqlName.Quote(applicationLogin)};",
                        cancellationToken);
                }
                else if (databaseExists && loginExists)
                {
                    throw new SetupUserException(
                        "EXISTING_INSTALLATION_DETECTED",
                        "An existing Advanced POS database and application login were detected. " +
                        "Choose 'Upgrade or repair an existing Advanced POS store' instead of New Store. " +
                        "The existing database was not changed.");
                }
                else if (databaseExists)
                {
                    throw new SetupUserException(
                        "EXISTING_DATABASE_DETECTED",
                        "The selected production database already exists, but the application login is missing. " +
                        "Choose 'Upgrade or repair an existing Advanced POS store' to verify the database and safely recreate the login. " +
                        "The existing database was not changed.");
                }
                else if (loginExists)
                {
                    throw new SetupUserException(
                        "PARTIAL_SETUP_LOGIN_EXISTS",
                        "The application login already exists but the selected production database does not. " +
                        "This may be left from an incomplete setup. Use a different login name, restore a verified backup, " +
                        "or have a technician review the existing login before creating a new store.");
                }

                await ExecuteAsync(
                    master,
                    $"CREATE DATABASE {SqlName.Quote(databaseName)} " +
                    $"COLLATE {DatabaseProviderModelConventions.SqlServerCaseInsensitiveCollation};",
                    cancellationToken);

                databaseCreatedByThisRun = true;
            }

            await ApplyMigrationsAsync(
                instance,
                databaseName,
                cancellationToken);

            await EnsureApplicationLoginAsync(
                instance,
                databaseName,
                applicationLogin,
                applicationPassword,
                cancellationToken);
        }
        catch (Exception provisioningError)
        {
            if (!databaseCreatedByThisRun)
                throw;

            try
            {
                await using var master = new SqlConnection(masterConnectionString);
                await master.OpenAsync(cancellationToken);

                await ExecuteAsync(
                    master,
                    $"IF DB_ID(N'{SqlName.EscapeLiteral(databaseName)}') IS NOT NULL " +
                    $"BEGIN ALTER DATABASE {SqlName.Quote(databaseName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                    $"DROP DATABASE {SqlName.Quote(databaseName)}; END; " +
                    $"IF SUSER_ID(N'{SqlName.EscapeLiteral(applicationLogin)}') IS NOT NULL " +
                    $"DROP LOGIN {SqlName.Quote(applicationLogin)};",
                    cancellationToken);
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(
                    "Production provisioning failed and automatic cleanup of the newly created database also failed.",
                    provisioningError,
                    cleanupError);
            }

            throw;
        }
    }

    public async Task<ServerInstallationInspection> InspectInstallationAsync(
        string instance,
        string databaseName,
        string applicationLogin,
        CancellationToken cancellationToken = default)
    {
        databaseName = SqlName.RequireSafeIdentifier(
            databaseName,
            "Database name");

        applicationLogin = SqlName.RequireSafeIdentifier(
            applicationLogin,
            "Application login");

        await using var master = new SqlConnection(
            SqlServerConnectionFactory.BuildAdministratorConnectionString(
                instance));

        try
        {
            await master.OpenAsync(cancellationToken);
        }
        catch (SqlException ex)
        {
            throw new SetupUserException(
                "SQL_SERVER_UNAVAILABLE",
                "The local SQL Server instance could not be opened. Confirm that SQLEXPRESS is installed and running, then retry setup.",
                ex);
        }

        bool databaseExists =
            Convert.ToInt32(
                await ExecuteScalarAsync(
                    master,
                    $"SELECT CASE WHEN DB_ID(N'{SqlName.EscapeLiteral(databaseName)}') IS NULL THEN 0 ELSE 1 END;",
                    cancellationToken)) != 0;

        bool loginExists =
            Convert.ToInt32(
                await ExecuteScalarAsync(
                    master,
                    $"SELECT CASE WHEN SUSER_ID(N'{SqlName.EscapeLiteral(applicationLogin)}') IS NULL THEN 0 ELSE 1 END;",
                    cancellationToken)) != 0;

        if (!databaseExists)
        {
            return new ServerInstallationInspection(
                DatabaseExists: false,
                LoginExists: loginExists,
                IsEmptyDatabase: false,
                IsAdvancedPosDatabase: false,
                IsRequiredMigrationApplied: false,
                DatabaseIsNewerThanApplication: false,
                LatestMigration: string.Empty,
                UserTableCount: 0);
        }

        await using var database = new SqlConnection(
            SqlServerConnectionFactory.BuildAdministratorConnectionString(
                instance,
                databaseName));

        try
        {
            await database.OpenAsync(cancellationToken);
        }
        catch (SqlException ex)
        {
            throw new SetupUserException(
                "DATABASE_UNAVAILABLE",
                "The selected production database exists but could not be opened. It was not modified. Check SQL Server status, database state, and permissions before retrying.",
                ex);
        }

        long userTableCount = Convert.ToInt64(
            await ExecuteScalarAsync(
                database,
                "SELECT COUNT_BIG(*) FROM sys.tables WHERE is_ms_shipped=0;",
                cancellationToken));

        bool usersTableExists = Convert.ToInt32(
            await ExecuteScalarAsync(
                database,
                "SELECT CASE WHEN OBJECT_ID(N'[dbo].[Users]', N'U') IS NULL THEN 0 ELSE 1 END;",
                cancellationToken)) != 0;

        bool storeSettingsTableExists = Convert.ToInt32(
            await ExecuteScalarAsync(
                database,
                "SELECT CASE WHEN OBJECT_ID(N'[dbo].[StoreSettings]', N'U') IS NULL THEN 0 ELSE 1 END;",
                cancellationToken)) != 0;

        bool migrationTableExists = Convert.ToInt32(
            await ExecuteScalarAsync(
                database,
                "SELECT CASE WHEN OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NULL THEN 0 ELSE 1 END;",
                cancellationToken)) != 0;

        string latestMigration = string.Empty;
        bool requiredMigrationApplied = false;

        if (migrationTableExists)
        {
            object? latest = await ExecuteScalarAsync(
                database,
                "SELECT TOP (1) [MigrationId] FROM [__EFMigrationsHistory] ORDER BY [MigrationId] DESC;",
                cancellationToken);

            latestMigration = latest == null || latest == DBNull.Value
                ? string.Empty
                : Convert.ToString(latest) ?? string.Empty;

            string requiredMigration =
                SqlName.EscapeLiteral(
                    ProductReleaseInfo.RequiredSqlServerMigration);

            requiredMigrationApplied = Convert.ToInt32(
                await ExecuteScalarAsync(
                    database,
                    $"SELECT CASE WHEN EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId]=N'{requiredMigration}') THEN 1 ELSE 0 END;",
                    cancellationToken)) != 0;
        }

        bool isAdvancedPosDatabase =
            usersTableExists &&
            storeSettingsTableExists &&
            migrationTableExists;

        bool databaseIsNewerThanApplication =
            !string.IsNullOrWhiteSpace(latestMigration) &&
            string.CompareOrdinal(
                latestMigration,
                ProductReleaseInfo.RequiredSqlServerMigration) > 0;

        return new ServerInstallationInspection(
            DatabaseExists: true,
            LoginExists: loginExists,
            IsEmptyDatabase: userTableCount == 0,
            IsAdvancedPosDatabase: isAdvancedPosDatabase,
            IsRequiredMigrationApplied: requiredMigrationApplied,
            DatabaseIsNewerThanApplication: databaseIsNewerThanApplication,
            LatestMigration: latestMigration,
            UserTableCount: userTableCount);
    }

    public static void EnsureExistingInstallationCanBeUpgraded(
        ServerInstallationInspection inspection)
    {
        if (!inspection.DatabaseExists && !inspection.LoginExists)
        {
            throw new SetupUserException(
                "NO_EXISTING_INSTALLATION",
                "No existing Advanced POS production database or application login was found. " +
                "Choose New Store for a clean installation, or Restore Backup when recovering a previous store.");
        }

        if (!inspection.DatabaseExists)
        {
            throw new SetupUserException(
                "PARTIAL_SETUP_DATABASE_MISSING",
                "The application login exists, but the production database is missing. " +
                "Do not create an empty replacement when store data may have existed. Restore a verified backup or contact support.");
        }

        if (inspection.IsEmptyDatabase)
        {
            throw new SetupUserException(
                "EMPTY_DATABASE_REQUIRES_REVIEW",
                "The selected database exists but contains no application tables. " +
                "It was not modified. Confirm that this is an incomplete setup before removing it or choose a verified backup to restore.");
        }

        if (!inspection.IsAdvancedPosDatabase)
        {
            throw new SetupUserException(
                "DATABASE_IDENTITY_MISMATCH",
                "The selected database does not contain the expected Advanced POS identity tables and migration history. " +
                "Setup refused to modify it. Verify the database name or restore the correct Advanced POS backup.");
        }

        if (inspection.DatabaseIsNewerThanApplication)
        {
            throw new SetupUserException(
                "DATABASE_NEWER_THAN_APPLICATION",
                "The production database was created by a newer Advanced POS schema. " +
                "Install the matching or newer Server version. Database downgrade is not allowed.");
        }
    }

    public async Task ApplyMigrationsAsync(
        string instance,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        databaseName = SqlName.RequireSafeIdentifier(
            databaseName,
            "Database name");

        string adminDatabaseConnectionString =
            SqlServerConnectionFactory.BuildAdministratorConnectionString(
                instance,
                databaseName);

        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseSqlServer(
            adminDatabaseConnectionString,
            sql => sql.MigrationsAssembly(
                PosDatabaseOptionsConfigurator.SqlServerMigrationsAssembly));

        await using var context = new AppDbContext(options.Options);
        await context.Database.MigrateAsync(cancellationToken);
    }

    public async Task EnsureApplicationLoginAsync(
        string instance,
        string databaseName,
        string applicationLogin,
        string applicationPassword,
        CancellationToken cancellationToken = default)
    {
        databaseName = SqlName.RequireSafeIdentifier(
            databaseName,
            "Database name");

        applicationLogin = SqlName.RequireSafeIdentifier(
            applicationLogin,
            "Application login");

        ValidateApplicationPassword(applicationPassword);

        string masterConnectionString =
            SqlServerConnectionFactory.BuildAdministratorConnectionString(
                instance);

        string databaseConnectionString =
            SqlServerConnectionFactory.BuildAdministratorConnectionString(
                instance,
                databaseName);

        await using (var master = new SqlConnection(masterConnectionString))
        {
            await master.OpenAsync(cancellationToken);

            string escapedPassword =
                SqlName.EscapeLiteral(applicationPassword);

            string escapedLogin =
                SqlName.EscapeLiteral(applicationLogin);

            await ExecuteAsync(
                master,
                $"IF SUSER_ID(N'{escapedLogin}') IS NULL " +
                $"BEGIN CREATE LOGIN {SqlName.Quote(applicationLogin)} " +
                $"WITH PASSWORD=N'{escapedPassword}', CHECK_POLICY=ON, " +
                $"CHECK_EXPIRATION=OFF, DEFAULT_DATABASE={SqlName.Quote(databaseName)}; END " +
                $"ELSE BEGIN ALTER LOGIN {SqlName.Quote(applicationLogin)} " +
                $"WITH PASSWORD=N'{escapedPassword}', CHECK_POLICY=ON, " +
                $"CHECK_EXPIRATION=OFF, DEFAULT_DATABASE={SqlName.Quote(databaseName)}; END;",
                cancellationToken);
        }

        await using var database = new SqlConnection(databaseConnectionString);
        await database.OpenAsync(cancellationToken);

        string loginLiteral = SqlName.EscapeLiteral(applicationLogin);
        string loginIdentifier = SqlName.Quote(applicationLogin);

        await ExecuteAsync(
            database,
            $"IF USER_ID(N'{loginLiteral}') IS NULL " +
            $"CREATE USER {loginIdentifier} FOR LOGIN {loginIdentifier}; " +
            $"ELSE ALTER USER {loginIdentifier} WITH LOGIN={loginIdentifier}; " +
            $"IF IS_ROLEMEMBER(N'db_datareader', N'{loginLiteral}') <> 1 " +
            $"ALTER ROLE [db_datareader] ADD MEMBER {loginIdentifier}; " +
            $"IF IS_ROLEMEMBER(N'db_datawriter', N'{loginLiteral}') <> 1 " +
            $"ALTER ROLE [db_datawriter] ADD MEMBER {loginIdentifier};",
            cancellationToken);
    }

    public async Task VerifyApplicationLoginAsync(
        string host,
        int port,
        string databaseName,
        string applicationLogin,
        string applicationPassword,
        CancellationToken cancellationToken = default)
    {
        string connectionString =
            SqlServerConnectionFactory.BuildApplicationConnectionString(
                host,
                port,
                databaseName,
                applicationLogin,
                applicationPassword);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT " +
            "(SELECT COUNT_BIG(*) FROM [Users]), " +
            "(SELECT COUNT_BIG(*) FROM [StoreSettings]), " +
            "(SELECT COUNT_BIG(*) FROM [__EFMigrationsHistory]);";
        command.CommandTimeout = 30;

        await using SqlDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "The application database verification query returned no result.");
        }

        long migrationCount = reader.GetInt64(2);
        if (migrationCount <= 0)
        {
            throw new InvalidOperationException(
                "The SQL Server migration history is empty.");
        }

        await reader.DisposeAsync();

        await using SqlTransaction transaction =
            (SqlTransaction)await connection.BeginTransactionAsync(
                cancellationToken);

        try
        {
            await using var writeCheck = connection.CreateCommand();
            writeCheck.Transaction = transaction;
            writeCheck.CommandText =
                "UPDATE [StoreSettings] SET [StoreName]=[StoreName] WHERE 1=0;";

            await writeCheck.ExecuteNonQueryAsync(cancellationToken);
            await transaction.RollbackAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task VerifyDatabaseIntegrityAsync(
        string instance,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        databaseName = SqlName.RequireSafeIdentifier(
            databaseName,
            "Database name");

        await using var connection = new SqlConnection(
            SqlServerConnectionFactory.BuildAdministratorConnectionString(
                instance,
                databaseName));

        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(
            connection,
            $"DBCC CHECKDB ({SqlName.Quote(databaseName)}) " +
            "WITH NO_INFOMSGS, ALL_ERRORMSGS;",
            cancellationToken,
            commandTimeoutSeconds: 600);
    }

    public async Task<DatabaseSummary> GetDatabaseSummaryAsync(
        string instance,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        databaseName = SqlName.RequireSafeIdentifier(
            databaseName,
            "Database name");

        await using var connection = new SqlConnection(
            SqlServerConnectionFactory.BuildAdministratorConnectionString(
                instance,
                databaseName));

        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT " +
            "(SELECT COUNT_BIG(*) FROM sys.tables WHERE is_ms_shipped=0), " +
            "(SELECT COALESCE(SUM(CONVERT(bigint, p.rows)), 0) " +
            " FROM sys.tables t " +
            " JOIN sys.partitions p ON p.object_id=t.object_id " +
            " WHERE t.is_ms_shipped=0 AND p.index_id IN (0,1)), " +
            "(SELECT TOP (1) [MigrationId] FROM [__EFMigrationsHistory] " +
            " ORDER BY [MigrationId] DESC);";
        command.CommandTimeout = 30;

        await using SqlDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Database summary query returned no result.");

        long tableCount = reader.GetInt64(0);
        long rowCount = reader.GetInt64(1);
        string migration = reader.IsDBNull(2)
            ? string.Empty
            : reader.GetString(2);

        return new DatabaseSummary(tableCount, rowCount, migration);
    }

    public async Task<bool> DatabaseExistsAsync(
        string instance,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        databaseName = SqlName.RequireSafeIdentifier(
            databaseName,
            "Database name");

        await using var connection = new SqlConnection(
            SqlServerConnectionFactory.BuildAdministratorConnectionString(
                instance));

        await connection.OpenAsync(cancellationToken);

        return Convert.ToInt32(
            await ExecuteScalarAsync(
                connection,
                $"SELECT CASE WHEN DB_ID(N'{SqlName.EscapeLiteral(databaseName)}') IS NULL THEN 0 ELSE 1 END;",
                cancellationToken)) != 0;
    }

    public async Task DropRehearsalDatabaseAndLoginAsync(
        string instance,
        string databaseName,
        string applicationLogin,
        CancellationToken cancellationToken = default)
    {
        databaseName = SqlName.RequireSafeIdentifier(
            databaseName,
            "Database name");

        applicationLogin = SqlName.RequireSafeIdentifier(
            applicationLogin,
            "Application login");

        if (!databaseName.Contains(
                "Rehearsal",
                StringComparison.OrdinalIgnoreCase) ||
            !applicationLogin.Contains(
                "Rehearsal",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Rehearsal cleanup refuses database or login names that do not contain 'Rehearsal'.");
        }

        await using var master = new SqlConnection(
            SqlServerConnectionFactory.BuildAdministratorConnectionString(
                instance));

        await master.OpenAsync(cancellationToken);

        await ExecuteAsync(
            master,
            $"IF DB_ID(N'{SqlName.EscapeLiteral(databaseName)}') IS NOT NULL " +
            $"BEGIN ALTER DATABASE {SqlName.Quote(databaseName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
            $"DROP DATABASE {SqlName.Quote(databaseName)}; END; " +
            $"IF SUSER_ID(N'{SqlName.EscapeLiteral(applicationLogin)}') IS NOT NULL " +
            $"DROP LOGIN {SqlName.Quote(applicationLogin)};",
            cancellationToken);
    }

    private static void ValidateApplicationPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 16)
        {
            throw new ArgumentException(
                "The application password must contain at least 16 characters.");
        }

        if (!password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            password.All(char.IsLetterOrDigit))
        {
            throw new ArgumentException(
                "The application password must include uppercase, lowercase, a number, and a symbol.");
        }
    }

    private static async Task ExecuteAsync(
        SqlConnection connection,
        string sql,
        CancellationToken cancellationToken,
        int commandTimeoutSeconds = 120)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = commandTimeoutSeconds;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<object?> ExecuteScalarAsync(
        SqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 30;
        return await command.ExecuteScalarAsync(cancellationToken);
    }
}

internal sealed record DatabaseSummary(
    long TableCount,
    long RowCount,
    string LatestMigration);

internal sealed record ServerInstallationInspection(
    bool DatabaseExists,
    bool LoginExists,
    bool IsEmptyDatabase,
    bool IsAdvancedPosDatabase,
    bool IsRequiredMigrationApplied,
    bool DatabaseIsNewerThanApplication,
    string LatestMigration,
    long UserTableCount);
