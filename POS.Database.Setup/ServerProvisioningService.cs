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
        databaseName = SqlName.RequireSafeIdentifier(databaseName, "Database name");
        applicationLogin = SqlName.RequireSafeIdentifier(applicationLogin, "Application login");

        if (applicationPassword.Length < 16)
            throw new ArgumentException("The application password must contain at least 16 characters.");

        string masterConnectionString =
            SqlServerConnectionFactory.BuildAdministratorConnectionString(instance);

        await using (var master = new SqlConnection(masterConnectionString))
        {
            await master.OpenAsync(cancellationToken);

            int databaseExists = Convert.ToInt32(
                await ExecuteScalarAsync(
                    master,
                    $"SELECT CASE WHEN DB_ID(N'{SqlName.EscapeLiteral(databaseName)}') IS NULL THEN 0 ELSE 1 END;",
                    cancellationToken));
            int loginExists = Convert.ToInt32(
                await ExecuteScalarAsync(
                    master,
                    $"SELECT CASE WHEN SUSER_ID(N'{SqlName.EscapeLiteral(applicationLogin)}') IS NULL THEN 0 ELSE 1 END;",
                    cancellationToken));

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
            else if (databaseExists != 0 || loginExists != 0)
            {
                throw new InvalidOperationException(
                    $"Provisioning requires a new database and login. " +
                    $"DatabaseExists={databaseExists != 0}, LoginExists={loginExists != 0}.");
            }

            await ExecuteAsync(
                master,
                $"CREATE DATABASE {SqlName.Quote(databaseName)};",
                cancellationToken);
        }

        string adminDatabaseConnectionString =
            SqlServerConnectionFactory.BuildAdministratorConnectionString(
                instance,
                databaseName);

        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseSqlServer(
            adminDatabaseConnectionString,
            sql => sql.MigrationsAssembly(
                PosDatabaseOptionsConfigurator.SqlServerMigrationsAssembly));

        await using (var context = new AppDbContext(options.Options))
        {
            await context.Database.MigrateAsync(cancellationToken);
        }

        await ProvisionApplicationLoginAsync(
            masterConnectionString,
            adminDatabaseConnectionString,
            databaseName,
            applicationLogin,
            applicationPassword,
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
        string connectionString = SqlServerConnectionFactory.BuildApplicationConnectionString(
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

        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("The application database verification query returned no result.");

        long migrationCount = reader.GetInt64(2);
        if (migrationCount <= 0)
            throw new InvalidOperationException("The SQL Server migration history is empty.");

        await reader.DisposeAsync();

        await using SqlTransaction transaction =
            (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
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
        databaseName = SqlName.RequireSafeIdentifier(databaseName, "Database name");

        await using var connection = new SqlConnection(
            SqlServerConnectionFactory.BuildAdministratorConnectionString(
                instance,
                databaseName));
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(
            connection,
            $"DBCC CHECKDB ({SqlName.Quote(databaseName)}) WITH NO_INFOMSGS, ALL_ERRORMSGS;",
            cancellationToken);
    }

    public async Task DropRehearsalDatabaseAndLoginAsync(
        string instance,
        string databaseName,
        string applicationLogin,
        CancellationToken cancellationToken = default)
    {
        databaseName = SqlName.RequireSafeIdentifier(databaseName, "Database name");
        applicationLogin = SqlName.RequireSafeIdentifier(applicationLogin, "Application login");

        if (!databaseName.Contains("Rehearsal", StringComparison.OrdinalIgnoreCase) ||
            !applicationLogin.Contains("Rehearsal", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Rehearsal cleanup refuses database or login names that do not contain 'Rehearsal'.");
        }

        await using var master = new SqlConnection(
            SqlServerConnectionFactory.BuildAdministratorConnectionString(instance));
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

    private static async Task ProvisionApplicationLoginAsync(
        string masterConnectionString,
        string databaseConnectionString,
        string databaseName,
        string applicationLogin,
        string applicationPassword,
        CancellationToken cancellationToken)
    {
        await using (var master = new SqlConnection(masterConnectionString))
        {
            await master.OpenAsync(cancellationToken);

            string escapedPassword = SqlName.EscapeLiteral(applicationPassword);
            await ExecuteAsync(
                master,
                $"CREATE LOGIN {SqlName.Quote(applicationLogin)} WITH PASSWORD=N'{escapedPassword}', " +
                "CHECK_POLICY=ON, CHECK_EXPIRATION=OFF, DEFAULT_DATABASE=" +
                SqlName.Quote(databaseName) + ";",
                cancellationToken);
        }

        await using var database = new SqlConnection(databaseConnectionString);
        await database.OpenAsync(cancellationToken);

        await ExecuteAsync(
            database,
            $"IF USER_ID(N'{SqlName.EscapeLiteral(applicationLogin)}') IS NOT NULL " +
            $"DROP USER {SqlName.Quote(applicationLogin)}; " +
            $"CREATE USER {SqlName.Quote(applicationLogin)} FOR LOGIN {SqlName.Quote(applicationLogin)}; " +
            $"ALTER ROLE [db_datareader] ADD MEMBER {SqlName.Quote(applicationLogin)}; " +
            $"ALTER ROLE [db_datawriter] ADD MEMBER {SqlName.Quote(applicationLogin)};",
            cancellationToken);
    }

    private static async Task ExecuteAsync(
        SqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 120;
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
