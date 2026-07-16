using Microsoft.Data.SqlClient;
using POS.Core.Data;

namespace POS.Core.Data.Configuration;

public sealed class DatabaseConnectionSettings
{
    public const int CurrentFormatVersion = 1;
    public const int DefaultSqlServerPort = 1433;
    public const string DefaultDatabaseName = "EasyRobinPOS";
    public const string DefaultApplicationLogin = "EasyRobinPOS_App";

    public int FormatVersion { get; init; } = CurrentFormatVersion;
    public DatabaseProviderKind Provider { get; init; } = DatabaseProviderKind.Sqlite;
    public string ServerHost { get; init; } = string.Empty;
    public int ServerPort { get; init; } = DefaultSqlServerPort;
    public string DatabaseName { get; init; } = DefaultDatabaseName;
    public string UserName { get; init; } = DefaultApplicationLogin;
    public string Password { get; init; } = string.Empty;
    public int ConnectTimeoutSeconds { get; init; } = 5;
    public bool Encrypt { get; init; } = true;
    public bool TrustServerCertificate { get; init; } = true;

    public static DatabaseConnectionSettings CreateStandaloneSqlite() =>
        new();

    public static DatabaseConnectionSettings CreateSqlServer(
        string serverHost,
        int serverPort,
        string databaseName,
        string userName,
        string password) =>
        new()
        {
            Provider = DatabaseProviderKind.SqlServer,
            ServerHost = serverHost?.Trim() ?? string.Empty,
            ServerPort = serverPort,
            DatabaseName = databaseName?.Trim() ?? string.Empty,
            UserName = userName?.Trim() ?? string.Empty,
            Password = password ?? string.Empty
        };

    public bool IsStandaloneSqlite =>
        Provider == DatabaseProviderKind.Sqlite;

    public bool IsCentralSqlServer =>
        Provider == DatabaseProviderKind.SqlServer;

    public string DisplayName =>
        IsStandaloneSqlite
            ? $"Standalone SQLite ({DatabasePathProvider.DatabaseFilePath})"
            : $"SQL Server {ServerHost},{ServerPort} / {DatabaseName}";

    public void Validate()
    {
        if (FormatVersion != CurrentFormatVersion)
        {
            throw new DatabaseConfigurationException(
                $"Unsupported database configuration version: {FormatVersion}.");
        }

        if (!Enum.IsDefined(typeof(DatabaseProviderKind), Provider))
        {
            throw new DatabaseConfigurationException(
                "The database provider setting is invalid.");
        }

        if (ConnectTimeoutSeconds is < 2 or > 60)
        {
            throw new DatabaseConfigurationException(
                "The database connection timeout must be between 2 and 60 seconds.");
        }

        if (IsStandaloneSqlite)
            return;

        if (!Encrypt)
        {
            throw new DatabaseConfigurationException(
                "SQL Server transport encryption must remain enabled.");
        }

        ValidateRequiredLength(ServerHost, nameof(ServerHost), 1, 255);
        ValidateServerHost(ServerHost);
        ValidateRequiredLength(DatabaseName, nameof(DatabaseName), 1, 128);
        ValidateRequiredLength(UserName, nameof(UserName), 1, 128);
        ValidateRequiredLength(Password, nameof(Password), 1, 256);

        if (ServerPort is < 1 or > 65535)
        {
            throw new DatabaseConfigurationException(
                "The SQL Server TCP port must be between 1 and 65535.");
        }

        if (ServerHost.Contains(';', StringComparison.Ordinal) ||
            DatabaseName.Contains(';', StringComparison.Ordinal) ||
            UserName.Contains(';', StringComparison.Ordinal))
        {
            throw new DatabaseConfigurationException(
                "SQL Server settings cannot contain semicolons.");
        }
    }

    public string BuildConnectionString()
    {
        Validate();

        if (IsStandaloneSqlite)
            return DatabasePathProvider.ConnectionString;

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"tcp:{ServerHost.Trim()},{ServerPort}",
            InitialCatalog = DatabaseName.Trim(),
            UserID = UserName.Trim(),
            Password = Password,
            IntegratedSecurity = false,
            PersistSecurityInfo = false,
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = TrustServerCertificate,
            ConnectTimeout = ConnectTimeoutSeconds,
            ConnectRetryCount = 3,
            ConnectRetryInterval = 1,
            MultipleActiveResultSets = true,
            Pooling = true,
            ApplicationName = "EasyRobin POS",
            WorkstationID = Environment.MachineName
        };

        return builder.ConnectionString;
    }

    private static void ValidateServerHost(string value)
    {
        string host = value.Trim();
        UriHostNameType hostNameType = Uri.CheckHostName(host);

        if (hostNameType != UriHostNameType.Dns &&
            hostNameType != UriHostNameType.IPv4)
        {
            throw new DatabaseConfigurationException(
                "ServerHost must be a DNS computer name or an IPv4 address.");
        }

        if (host.Any(char.IsWhiteSpace) ||
            host.IndexOfAny(new[] { ';', ',', '\\', '/' }) >= 0)
        {
            throw new DatabaseConfigurationException(
                "ServerHost cannot contain spaces, separators, or a SQL Server instance name.");
        }
    }

    private static void ValidateRequiredLength(
        string value,
        string fieldName,
        int minimumLength,
        int maximumLength)
    {
        int length = value?.Trim().Length ?? 0;
        if (length < minimumLength || length > maximumLength)
        {
            throw new DatabaseConfigurationException(
                $"{fieldName} must contain between {minimumLength} and {maximumLength} characters.");
        }
    }
}
