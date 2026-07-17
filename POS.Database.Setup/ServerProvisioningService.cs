using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
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
                else if (databaseExists || loginExists)
                {
                    throw new InvalidOperationException(
                        "Provisioning requires a new database and application login. " +
                        $"DatabaseExists={databaseExists}, LoginExists={loginExists}.");
                }

                await ExecuteAsync(
                    master,
                    $"CREATE DATABASE {SqlName.Quote(databaseName)};",
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
