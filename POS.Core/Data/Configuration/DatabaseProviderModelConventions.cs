using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace POS.Core.Data.Configuration;

public static class DatabaseProviderModelConventions
{
    public const string SqliteCaseInsensitiveCollation = "NOCASE";
    public const string SqlServerCaseInsensitiveCollation = "Latin1_General_100_CI_AS_SC";
    public const string SqliteLargeTextColumnType = "TEXT";
    public const string SqlServerLargeTextColumnType = "nvarchar(max)";

    public static string GetCaseInsensitive(DatabaseFacade database)
    {
        DatabaseProviderKind provider = GetProvider(database);
        return provider == DatabaseProviderKind.SqlServer
            ? SqlServerCaseInsensitiveCollation
            : SqliteCaseInsensitiveCollation;
    }

    public static string GetLargeTextColumnType(DatabaseFacade database)
    {
        DatabaseProviderKind provider = GetProvider(database);
        return provider == DatabaseProviderKind.SqlServer
            ? SqlServerLargeTextColumnType
            : SqliteLargeTextColumnType;
    }

    private static DatabaseProviderKind GetProvider(DatabaseFacade database)
    {
        ArgumentNullException.ThrowIfNull(database);

        if (database.IsSqlServer())
            return DatabaseProviderKind.SqlServer;

        if (database.IsSqlite())
            return DatabaseProviderKind.Sqlite;

        throw new DatabaseConfigurationException(
            $"Unsupported EF Core database provider: {database.ProviderName ?? "(not configured)"}.");
    }
}
