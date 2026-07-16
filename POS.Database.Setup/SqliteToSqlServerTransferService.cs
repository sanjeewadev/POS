using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace POS.Database.Setup;

internal sealed class SqliteToSqlServerTransferService
{
    private sealed record DestinationColumn(
        string Name,
        string SqlType,
        bool IsNullable,
        bool IsIdentity,
        bool IsComputed);

    public async Task<IReadOnlyDictionary<string, long>> TransferAsync(
        string sqlitePath,
        string sqlServerAdministratorConnectionString,
        CancellationToken cancellationToken = default)
    {
        string sourcePath = Path.GetFullPath(sqlitePath);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("The SQLite source database was not found.", sourcePath);

        var sourceBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            ForeignKeys = true
        };

        await using var source = new SqliteConnection(sourceBuilder.ConnectionString);
        await source.OpenAsync(cancellationToken);
        await ValidateSourceDatabaseAsync(source, cancellationToken);

        await using var destination = new SqlConnection(sqlServerAdministratorConnectionString);
        await destination.OpenAsync(cancellationToken);

        IReadOnlyList<string> sourceTables = await GetSourceTablesAsync(source, cancellationToken);
        IReadOnlySet<string> destinationTables = await GetDestinationTablesAsync(destination, cancellationToken);

        string[] missingTables = sourceTables
            .Where(table => !destinationTables.Contains(table))
            .ToArray();
        if (missingTables.Length > 0)
        {
            throw new InvalidOperationException(
                "The SQL Server schema is missing source tables: " +
                string.Join(", ", missingTables));
        }

        await using SqlTransaction transaction =
            (SqlTransaction)await destination.BeginTransactionAsync(cancellationToken);

        var copiedCounts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await SetConstraintsAndTriggersAsync(
                destination,
                transaction,
                sourceTables,
                enable: false,
                cancellationToken);

            foreach (string table in sourceTables)
            {
                await DeleteDestinationRowsAsync(
                    destination,
                    transaction,
                    table,
                    cancellationToken);
            }

            foreach (string table in sourceTables)
            {
                long copied = await CopyTableAsync(
                    source,
                    destination,
                    transaction,
                    table,
                    cancellationToken);

                copiedCounts[table] = copied;
            }

            foreach (string table in sourceTables)
            {
                await ReseedIdentityAsync(
                    destination,
                    transaction,
                    table,
                    cancellationToken);
            }

            await SetConstraintsAndTriggersAsync(
                destination,
                transaction,
                sourceTables,
                enable: true,
                cancellationToken);

            foreach ((string table, long sourceCount) in copiedCounts)
            {
                long destinationCount = await CountDestinationRowsAsync(
                    destination,
                    transaction,
                    table,
                    cancellationToken);

                if (destinationCount != sourceCount)
                {
                    throw new InvalidOperationException(
                        $"Data-transfer count mismatch for {table}. " +
                        $"SQLite={sourceCount}, SQL Server={destinationCount}.");
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return copiedCounts;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }


    private static async Task ValidateSourceDatabaseAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using (var integrityCommand = connection.CreateCommand())
        {
            integrityCommand.CommandText = "PRAGMA integrity_check;";
            string result = Convert.ToString(
                await integrityCommand.ExecuteScalarAsync(cancellationToken),
                CultureInfo.InvariantCulture) ?? string.Empty;

            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"SQLite integrity_check failed: {result}");
            }
        }

        await using var foreignKeyCommand = connection.CreateCommand();
        foreignKeyCommand.CommandText = "PRAGMA foreign_key_check;";
        await using SqliteDataReader reader =
            await foreignKeyCommand.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "SQLite foreign_key_check reported one or more violations.");
        }
    }

    private static async Task<IReadOnlyList<string>> GetSourceTablesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT name FROM sqlite_master " +
            "WHERE type='table' AND name NOT LIKE 'sqlite_%' " +
            "AND name <> '__EFMigrationsHistory' ORDER BY name;";

        var tables = new List<string>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            tables.Add(reader.GetString(0));

        if (tables.Count == 0)
            throw new InvalidOperationException("The SQLite source database contains no application tables.");

        return tables;
    }

    private static async Task<IReadOnlySet<string>> GetDestinationTablesAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT t.name FROM sys.tables t " +
            "INNER JOIN sys.schemas s ON s.schema_id=t.schema_id " +
            "WHERE s.name=N'dbo';";

        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            tables.Add(reader.GetString(0));

        return tables;
    }

    private static async Task<long> CopyTableAsync(
        SqliteConnection source,
        SqlConnection destination,
        SqlTransaction transaction,
        string table,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> sourceColumns = await GetSourceColumnsAsync(
            source,
            table,
            cancellationToken);
        IReadOnlyList<DestinationColumn> destinationColumns = await GetDestinationColumnsAsync(
            destination,
            transaction,
            table,
            cancellationToken);

        DestinationColumn[] columns = destinationColumns
            .Where(column =>
                !column.IsComputed &&
                sourceColumns.Contains(column.Name, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (columns.Length == 0)
            throw new InvalidOperationException($"No transferable columns were found for table {table}.");

        string selectColumns = string.Join(
            ", ",
            columns.Select(column => QuoteSqliteIdentifier(column.Name)));

        await using var command = source.CreateCommand();
        command.CommandText =
            $"SELECT {selectColumns} FROM {QuoteSqliteIdentifier(table)};";

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        var data = new DataTable(table);
        foreach (DestinationColumn column in columns)
        {
            var dataColumn = new DataColumn(column.Name, GetDotNetType(column.SqlType))
            {
                AllowDBNull = column.IsNullable
            };
            data.Columns.Add(dataColumn);
        }

        while (await reader.ReadAsync(cancellationToken))
        {
            DataRow row = data.NewRow();
            for (int index = 0; index < columns.Length; index++)
            {
                object value = reader.IsDBNull(index)
                    ? DBNull.Value
                    : ConvertValue(reader.GetValue(index), columns[index]);
                row[index] = value;
            }
            data.Rows.Add(row);
        }

        if (data.Rows.Count == 0)
            return 0;

        SqlBulkCopyOptions options =
            SqlBulkCopyOptions.KeepIdentity |
            SqlBulkCopyOptions.KeepNulls |
            SqlBulkCopyOptions.TableLock;

        using var bulkCopy = new SqlBulkCopy(destination, options, transaction)
        {
            DestinationTableName = $"[dbo].{SqlName.Quote(table)}",
            BatchSize = 1000,
            BulkCopyTimeout = 180,
            EnableStreaming = false
        };

        foreach (DestinationColumn column in columns)
            bulkCopy.ColumnMappings.Add(column.Name, column.Name);

        await bulkCopy.WriteToServerAsync(data, cancellationToken);
        return data.Rows.Count;
    }

    private static async Task<IReadOnlyList<string>> GetSourceColumnsAsync(
        SqliteConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({QuoteSqliteIdentifier(table)});";

        var columns = new List<string>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            columns.Add(reader.GetString(1));

        return columns;
    }

    private static async Task<IReadOnlyList<DestinationColumn>> GetDestinationColumnsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT c.name, ty.name, c.is_nullable, c.is_identity, c.is_computed " +
            "FROM sys.columns c " +
            "INNER JOIN sys.tables t ON t.object_id=c.object_id " +
            "INNER JOIN sys.schemas s ON s.schema_id=t.schema_id " +
            "INNER JOIN sys.types ty ON ty.user_type_id=c.user_type_id " +
            "WHERE s.name=N'dbo' AND t.name=@table ORDER BY c.column_id;";
        command.Parameters.AddWithValue("@table", table);

        var columns = new List<DestinationColumn>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(new DestinationColumn(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetBoolean(2),
                reader.GetBoolean(3),
                reader.GetBoolean(4)));
        }

        if (columns.Count == 0)
            throw new InvalidOperationException($"SQL Server table dbo.{table} was not found.");

        return columns;
    }

    private static async Task DeleteDestinationRowsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"DELETE FROM [dbo].{SqlName.Quote(table)};";
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SetConstraintsAndTriggersAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IEnumerable<string> tables,
        bool enable,
        CancellationToken cancellationToken)
    {
        foreach (string table in tables)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandTimeout = 60;
            command.CommandText = enable
                ? $"ALTER TABLE [dbo].{SqlName.Quote(table)} WITH CHECK CHECK CONSTRAINT ALL; " +
                  $"ENABLE TRIGGER ALL ON [dbo].{SqlName.Quote(table)};"
                : $"ALTER TABLE [dbo].{SqlName.Quote(table)} NOCHECK CONSTRAINT ALL; " +
                  $"DISABLE TRIGGER ALL ON [dbo].{SqlName.Quote(table)};";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task ReseedIdentityAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string table,
        CancellationToken cancellationToken)
    {
        await using var identityCommand = connection.CreateCommand();
        identityCommand.Transaction = transaction;
        identityCommand.CommandText =
            "SELECT c.name FROM sys.identity_columns c " +
            "INNER JOIN sys.tables t ON t.object_id=c.object_id " +
            "INNER JOIN sys.schemas s ON s.schema_id=t.schema_id " +
            "WHERE s.name=N'dbo' AND t.name=@table;";
        identityCommand.Parameters.AddWithValue("@table", table);

        string? identityColumn = Convert.ToString(
            await identityCommand.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);

        if (string.IsNullOrWhiteSpace(identityColumn))
            return;

        await using var maxCommand = connection.CreateCommand();
        maxCommand.Transaction = transaction;
        maxCommand.CommandText =
            $"SELECT MAX({SqlName.Quote(identityColumn)}) FROM [dbo].{SqlName.Quote(table)};";
        object? maximum = await maxCommand.ExecuteScalarAsync(cancellationToken);
        if (maximum is null or DBNull)
            return;

        long maxValue = Convert.ToInt64(maximum, CultureInfo.InvariantCulture);

        await using var reseedCommand = connection.CreateCommand();
        reseedCommand.Transaction = transaction;
        reseedCommand.CommandText =
            $"DBCC CHECKIDENT ('dbo.{table.Replace("'", "''", StringComparison.Ordinal)}', RESEED, {maxValue});";
        await reseedCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> CountDestinationRowsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT COUNT_BIG(*) FROM [dbo].{SqlName.Quote(table)};";
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
    }

    private static Type GetDotNetType(string sqlType) =>
        sqlType.ToLowerInvariant() switch
        {
            "bit" => typeof(bool),
            "tinyint" => typeof(byte),
            "smallint" => typeof(short),
            "int" => typeof(int),
            "bigint" => typeof(long),
            "decimal" or "numeric" or "money" or "smallmoney" => typeof(decimal),
            "real" => typeof(float),
            "float" => typeof(double),
            "date" or "datetime" or "datetime2" or "smalldatetime" => typeof(DateTime),
            "datetimeoffset" => typeof(DateTimeOffset),
            "time" => typeof(TimeSpan),
            "uniqueidentifier" => typeof(Guid),
            "binary" or "varbinary" or "image" or "rowversion" or "timestamp" => typeof(byte[]),
            _ => typeof(string)
        };

    private static object ConvertValue(object value, DestinationColumn column)
    {
        if (value is DBNull)
            return DBNull.Value;

        string sqlType = column.SqlType.ToLowerInvariant();
        string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text) && column.IsNullable &&
            sqlType is "date" or "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset" or "time" or "uniqueidentifier")
        {
            return DBNull.Value;
        }

        return sqlType switch
        {
            "bit" => value is bool boolean
                ? boolean
                : text == "1" || bool.TryParse(text, out bool parsedBoolean) && parsedBoolean,
            "tinyint" => Convert.ToByte(value, CultureInfo.InvariantCulture),
            "smallint" => Convert.ToInt16(value, CultureInfo.InvariantCulture),
            "int" => Convert.ToInt32(value, CultureInfo.InvariantCulture),
            "bigint" => Convert.ToInt64(value, CultureInfo.InvariantCulture),
            "decimal" or "numeric" or "money" or "smallmoney" =>
                decimal.Parse(text, NumberStyles.Any, CultureInfo.InvariantCulture),
            "real" => Convert.ToSingle(value, CultureInfo.InvariantCulture),
            "float" => Convert.ToDouble(value, CultureInfo.InvariantCulture),
            "date" or "datetime" or "datetime2" or "smalldatetime" =>
                DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            "datetimeoffset" =>
                DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            "time" => TimeSpan.Parse(text, CultureInfo.InvariantCulture),
            "uniqueidentifier" => Guid.Parse(text),
            "binary" or "varbinary" or "image" or "rowversion" or "timestamp" =>
                value is byte[] bytes ? bytes : Convert.FromBase64String(text),
            _ => text
        };
    }

    private static string QuoteSqliteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
