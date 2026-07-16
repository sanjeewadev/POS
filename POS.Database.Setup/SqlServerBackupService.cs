using Microsoft.Data.SqlClient;

namespace POS.Database.Setup;

internal sealed class SqlServerBackupService
{
    public async Task<string> GetDefaultBackupDirectoryAsync(
        string instance,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(
            SqlServerConnectionFactory.BuildAdministratorConnectionString(instance));
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000));";

        string? directory = Convert.ToString(
            await command.ExecuteScalarAsync(cancellationToken));

        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("SQL Server did not report its default backup directory.");

        return directory.Trim();
    }

    public async Task CreateAndVerifyBackupAsync(
        string instance,
        string databaseName,
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        databaseName = SqlName.RequireSafeIdentifier(databaseName, "Database name");
        string fullPath = Path.GetFullPath(backupPath);

        await using var connection = new SqlConnection(
            SqlServerConnectionFactory.BuildAdministratorConnectionString(instance));
        await connection.OpenAsync(cancellationToken);

        string escapedPath = SqlName.EscapeLiteral(fullPath);
        await ExecuteAsync(
            connection,
            $"BACKUP DATABASE {SqlName.Quote(databaseName)} TO DISK=N'{escapedPath}' " +
            "WITH INIT, CHECKSUM, STATS=10;",
            cancellationToken);

        await ExecuteAsync(
            connection,
            $"RESTORE VERIFYONLY FROM DISK=N'{escapedPath}' WITH CHECKSUM;",
            cancellationToken);
    }

    public async Task RestoreBackupAsync(
        string instance,
        string databaseName,
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        databaseName = SqlName.RequireSafeIdentifier(databaseName, "Database name");
        string fullPath = Path.GetFullPath(backupPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The SQL Server backup file was not found.", fullPath);

        await using var connection = new SqlConnection(
            SqlServerConnectionFactory.BuildAdministratorConnectionString(instance));
        await connection.OpenAsync(cancellationToken);

        string escapedPath = SqlName.EscapeLiteral(fullPath);
        try
        {
            await ExecuteAsync(
                connection,
                $"IF DB_ID(N'{SqlName.EscapeLiteral(databaseName)}') IS NOT NULL " +
                $"ALTER DATABASE {SqlName.Quote(databaseName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"RESTORE DATABASE {SqlName.Quote(databaseName)} FROM DISK=N'{escapedPath}' " +
                "WITH REPLACE, CHECKSUM, RECOVERY, STATS=10;",
                cancellationToken);
        }
        finally
        {
            await ExecuteAsync(
                connection,
                $"IF DB_ID(N'{SqlName.EscapeLiteral(databaseName)}') IS NOT NULL " +
                $"ALTER DATABASE {SqlName.Quote(databaseName)} SET MULTI_USER;",
                cancellationToken);
        }
    }

    private static async Task ExecuteAsync(
        SqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 600;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
