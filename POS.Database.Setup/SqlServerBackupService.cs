using Microsoft.Data.SqlClient;

namespace POS.Database.Setup;

internal sealed class SqlServerBackupService
{
    public async Task<string> GetDefaultBackupDirectoryAsync(
        string instance,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(
            SqlServerConnectionFactory.BuildAdministratorConnectionString(
                instance));

        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000));";

        string? directory = Convert.ToString(
            await command.ExecuteScalarAsync(cancellationToken));

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException(
                "SQL Server did not report its default backup directory.");
        }

        return directory.Trim();
    }

    public async Task<string> StageBackupForSqlServerAsync(
        string instance,
        string sourceBackupPath,
        CancellationToken cancellationToken = default)
    {
        string source = Path.GetFullPath(sourceBackupPath);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException(
                "The SQL Server backup file was not found.",
                source);
        }

        string backupDirectory =
            await GetDefaultBackupDirectoryAsync(instance, cancellationToken);

        Directory.CreateDirectory(backupDirectory);

        string safeFileName =
            Path.GetFileNameWithoutExtension(source)
                .Replace(' ', '_');

        if (string.IsNullOrWhiteSpace(safeFileName))
            safeFileName = "POS_Restore";

        string destination = Path.Combine(
            backupDirectory,
            $"{safeFileName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak");

        if (!string.Equals(
                source,
                destination,
                StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(source, destination, overwrite: false);
        }

        if (!File.Exists(destination) ||
            new FileInfo(destination).Length != new FileInfo(source).Length)
        {
            throw new IOException(
                "The backup could not be staged in the SQL Server backup directory.");
        }

        return destination;
    }

    public async Task CreateAndVerifyBackupAsync(
        string instance,
        string databaseName,
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        databaseName = SqlName.RequireSafeIdentifier(
            databaseName,
            "Database name");

        string fullPath = Path.GetFullPath(backupPath);

        await using var connection = new SqlConnection(
            SqlServerConnectionFactory.BuildAdministratorConnectionString(
                instance));

        await connection.OpenAsync(cancellationToken);

        string escapedPath = SqlName.EscapeLiteral(fullPath);

        await ExecuteAsync(
            connection,
            $"BACKUP DATABASE {SqlName.Quote(databaseName)} " +
            $"TO DISK=N'{escapedPath}' WITH INIT, CHECKSUM, STATS=10;",
            cancellationToken);

        await ExecuteAsync(
            connection,
            $"RESTORE VERIFYONLY FROM DISK=N'{escapedPath}' WITH CHECKSUM;",
            cancellationToken);
    }

    public async Task<BackupCopyResult> CreateVerifiedBackupCopyAsync(
        string instance,
        string databaseName,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        string destination = Path.GetFullPath(destinationPath);
        string? destinationFolder = Path.GetDirectoryName(destination);

        if (string.IsNullOrWhiteSpace(destinationFolder))
            throw new ArgumentException("The backup destination folder is invalid.");

        Directory.CreateDirectory(destinationFolder);

        string sqlBackupDirectory =
            await GetDefaultBackupDirectoryAsync(instance, cancellationToken);

        string safeDatabase = SqlName.RequireSafeIdentifier(
            databaseName,
            "Database name");

        string sqlBackupPath = Path.Combine(
            sqlBackupDirectory,
            $"{safeDatabase}_{DateTime.Now:yyyyMMdd_HHmmss}.bak");

        await CreateAndVerifyBackupAsync(
            instance,
            safeDatabase,
            sqlBackupPath,
            cancellationToken);

        File.Copy(sqlBackupPath, destination, overwrite: true);

        if (!File.Exists(destination) ||
            new FileInfo(destination).Length <= 0)
        {
            throw new IOException(
                "The verified SQL Server backup could not be copied to the selected destination.");
        }

        string hash;
        await using (FileStream stream = new FileStream(
            destination,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            useAsync: true))
        {
            byte[] hashBytes =
                await System.Security.Cryptography.SHA256.HashDataAsync(
                    stream,
                    cancellationToken);

            hash = Convert.ToHexString(hashBytes);
        }

        return new BackupCopyResult(
            sqlBackupPath,
            destination,
            hash);
    }

    public async Task RestoreBackupAsync(
        string instance,
        string databaseName,
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        databaseName = SqlName.RequireSafeIdentifier(
            databaseName,
            "Database name");

        string fullPath = Path.GetFullPath(backupPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "The SQL Server backup file was not found.",
                fullPath);
        }

        await using var connection = new SqlConnection(
            SqlServerConnectionFactory.BuildAdministratorConnectionString(
                instance));

        await connection.OpenAsync(cancellationToken);

        IReadOnlyList<BackupFileEntry> files =
            await ReadBackupFilesAsync(
                connection,
                fullPath,
                cancellationToken);

        (string dataDirectory, string logDirectory) =
            await ReadDefaultDataDirectoriesAsync(
                connection,
                cancellationToken);

        string moveClauses = BuildMoveClauses(
            databaseName,
            files,
            dataDirectory,
            logDirectory);

        string escapedPath = SqlName.EscapeLiteral(fullPath);

        try
        {
            await ExecuteAsync(
                connection,
                $"IF DB_ID(N'{SqlName.EscapeLiteral(databaseName)}') IS NOT NULL " +
                $"ALTER DATABASE {SqlName.Quote(databaseName)} " +
                "SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"RESTORE DATABASE {SqlName.Quote(databaseName)} " +
                $"FROM DISK=N'{escapedPath}' WITH REPLACE, CHECKSUM, " +
                $"RECOVERY, STATS=10{moveClauses};",
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

    private static async Task<IReadOnlyList<BackupFileEntry>>
        ReadBackupFilesAsync(
            SqlConnection connection,
            string backupPath,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"RESTORE FILELISTONLY FROM DISK=N'{SqlName.EscapeLiteral(backupPath)}';";
        command.CommandTimeout = 120;

        var result = new List<BackupFileEntry>();

        await using SqlDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);

        int logicalOrdinal = reader.GetOrdinal("LogicalName");
        int typeOrdinal = reader.GetOrdinal("Type");
        int fileIdOrdinal = reader.GetOrdinal("FileId");

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(
                new BackupFileEntry(
                    reader.GetString(logicalOrdinal),
                    reader.GetString(typeOrdinal),
                    Convert.ToInt32(reader.GetValue(fileIdOrdinal))));
        }

        if (result.Count == 0)
            throw new InvalidOperationException("The backup contains no database files.");

        return result;
    }

    private static async Task<(string DataDirectory, string LogDirectory)>
        ReadDefaultDataDirectoriesAsync(
            SqlConnection connection,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT " +
            "CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)), " +
            "CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000));";

        await using SqlDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "SQL Server did not report its default data directories.");
        }

        string dataDirectory = reader.IsDBNull(0)
            ? string.Empty
            : reader.GetString(0).Trim();

        string logDirectory = reader.IsDBNull(1)
            ? string.Empty
            : reader.GetString(1).Trim();

        if (string.IsNullOrWhiteSpace(dataDirectory) ||
            string.IsNullOrWhiteSpace(logDirectory))
        {
            throw new InvalidOperationException(
                "SQL Server default data or log directory is unavailable.");
        }

        return (dataDirectory, logDirectory);
    }

    private static string BuildMoveClauses(
        string databaseName,
        IReadOnlyList<BackupFileEntry> files,
        string dataDirectory,
        string logDirectory)
    {
        var clauses = new List<string>();
        int dataIndex = 0;
        int logIndex = 0;

        foreach (BackupFileEntry file in files.OrderBy(entry => entry.FileId))
        {
            bool isLog = string.Equals(
                file.Type,
                "L",
                StringComparison.OrdinalIgnoreCase);

            string targetDirectory = isLog
                ? logDirectory
                : dataDirectory;

            int index = isLog ? logIndex++ : dataIndex++;
            string extension = isLog
                ? ".ldf"
                : index == 0 ? ".mdf" : ".ndf";

            string suffix = index == 0
                ? string.Empty
                : $"_{index + 1}";

            string targetPath = Path.Combine(
                targetDirectory,
                databaseName + suffix + extension);

            clauses.Add(
                $", MOVE N'{SqlName.EscapeLiteral(file.LogicalName)}' " +
                $"TO N'{SqlName.EscapeLiteral(targetPath)}'");
        }

        return string.Concat(clauses);
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

internal sealed record BackupFileEntry(
    string LogicalName,
    string Type,
    int FileId);

internal sealed record BackupCopyResult(
    string SqlServerBackupPath,
    string DestinationPath,
    string Sha256);
