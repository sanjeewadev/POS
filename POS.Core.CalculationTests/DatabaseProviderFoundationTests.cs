using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using POS.Core.Data;
using POS.Core.Data.Configuration;
using POS.Core.Models;
using POS.Core.Models.Licensing;
using System.Text;

namespace POS.Core.CalculationTests;

internal static class DatabaseProviderFoundationTests
{
    public static void MissingProfileDefaultsToStandaloneSqlite()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(root, "database.connection.dat");
            var store = new DatabaseConnectionSettingsStore(path);

            DatabaseConnectionSettings settings = store.LoadOrDefault();

            Require(settings.IsStandaloneSqlite,
                "missing connection profile should preserve standalone SQLite");
            Require(!File.Exists(path),
                "reading the default profile must not create a settings file");
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    public static void SqlServerProfileIsEncryptedAndRoundTrips()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(root, "database.connection.dat");
            var store = new DatabaseConnectionSettingsStore(path);
            DatabaseConnectionSettings expected =
                DatabaseConnectionSettings.CreateSqlServer(
                    "STORE-SERVER",
                    14333,
                    "EasyRobinPOS",
                    "EasyRobinPOS_App",
                    "Test-Only-Secret-987!");

            store.Save(expected);

            byte[] raw = File.ReadAllBytes(path);
            string rawText = Encoding.UTF8.GetString(raw);
            Require(!rawText.Contains(expected.Password, StringComparison.Ordinal),
                "encrypted connection profile exposed the password");
            Require(!rawText.Contains(expected.UserName, StringComparison.Ordinal),
                "encrypted connection profile exposed the login name");
            Require(!rawText.Contains(expected.ServerHost, StringComparison.Ordinal),
                "encrypted connection profile exposed the server name");

            DatabaseConnectionSettings actual = store.LoadOrDefault();
            Require(actual.IsCentralSqlServer,
                "encrypted SQL Server provider did not round-trip");
            Require(actual.ServerHost == expected.ServerHost,
                "SQL Server host did not round-trip");
            Require(actual.ServerPort == expected.ServerPort,
                "SQL Server port did not round-trip");
            Require(actual.DatabaseName == expected.DatabaseName,
                "SQL Server database did not round-trip");
            Require(actual.UserName == expected.UserName,
                "SQL Server login did not round-trip");
            Require(actual.Password == expected.Password,
                "SQL Server password did not round-trip");
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    public static void ProviderOptionsAndCollationsArePortable()
    {
        var sqliteBuilder = new DbContextOptionsBuilder<AppDbContext>();
        PosDatabaseOptionsConfigurator.Configure(
            sqliteBuilder,
            DatabaseConnectionSettings.CreateStandaloneSqlite());

        using var sqliteContext = new AppDbContext(sqliteBuilder.Options);
        Require(sqliteContext.Database.IsSqlite(),
            "standalone settings did not configure SQLite");
        Require(
            GetPortableCaseInsensitiveCollation(sqliteContext) == "NOCASE",
            "SQLite model did not retain the existing NOCASE collation");
        Require(
            GetLicensePayloadColumnType(sqliteContext) == "TEXT",
            "SQLite model did not retain the TEXT large-value column type");

        DatabaseConnectionSettings sqlServerSettings =
            DatabaseConnectionSettings.CreateSqlServer(
                "127.0.0.1",
                1433,
                "EasyRobinPOS",
                "EasyRobinPOS_App",
                "Test-Only-Secret-987!");

        var sqlServerBuilder = new DbContextOptionsBuilder<AppDbContext>();
        PosDatabaseOptionsConfigurator.Configure(
            sqlServerBuilder,
            sqlServerSettings);

        using var sqlServerContext =
            new AppDbContext(sqlServerBuilder.Options);
        Require(sqlServerContext.Database.IsSqlServer(),
            "central settings did not configure SQL Server");
        Require(
            GetPortableCaseInsensitiveCollation(sqlServerContext) ==
                "Latin1_General_100_CI_AS_SC",
            "SQL Server model did not use the approved case-insensitive collation");
        Require(
            GetLicensePayloadColumnType(sqlServerContext) == "nvarchar(max)",
            "SQL Server model did not use nvarchar(max) for large Unicode text");

        var parsedConnection = new SqlConnectionStringBuilder(
            sqlServerSettings.BuildConnectionString());
        Require(parsedConnection.DataSource == "tcp:127.0.0.1,1433",
            "SQL Server connection string omitted the fixed TCP endpoint");
        Require(parsedConnection.Encrypt == SqlConnectionEncryptOption.Mandatory,
            "SQL Server connection encryption was not enabled");
        Require(parsedConnection.TrustServerCertificate,
            "SQL Server local certificate policy was not configured");
        Require(!parsedConnection.PersistSecurityInfo,
            "SQL Server connection string may persist credentials");
    }

    public static void InvalidOrCorruptProfilesFailClosed()
    {
        DatabaseConnectionSettings invalid =
            DatabaseConnectionSettings.CreateSqlServer(
                "STORE-SERVER;Injected=True",
                1433,
                "EasyRobinPOS",
                "EasyRobinPOS_App",
                "secret");

        RequireThrows<DatabaseConfigurationException>(
            invalid.Validate,
            "unsafe SQL Server host was accepted");

        DatabaseConnectionSettings namedInstance =
            DatabaseConnectionSettings.CreateSqlServer(
                "STORE-SERVER\\SQLEXPRESS",
                1433,
                "EasyRobinPOS",
                "EasyRobinPOS_App",
                "secret");

        RequireThrows<DatabaseConfigurationException>(
            namedInstance.Validate,
            "named SQL Server instance bypassed the fixed TCP endpoint policy");

        var unencrypted = new DatabaseConnectionSettings
        {
            Provider = DatabaseProviderKind.SqlServer,
            ServerHost = "STORE-SERVER",
            ServerPort = 1433,
            DatabaseName = "EasyRobinPOS",
            UserName = "EasyRobinPOS_App",
            Password = "secret",
            Encrypt = false
        };
        RequireThrows<DatabaseConfigurationException>(
            unencrypted.Validate,
            "unencrypted SQL Server transport was accepted");

        string root = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(root, "database.connection.dat");
            File.WriteAllText(path, "not-an-encrypted-profile");
            var store = new DatabaseConnectionSettingsStore(path);

            RequireThrows<DatabaseConfigurationException>(
                () => store.LoadOrDefault(),
                "corrupt database profile silently fell back to SQLite");
        }
        finally
        {
            DeleteTemporaryDirectory(root);
        }
    }

    private static string? GetPortableCaseInsensitiveCollation(AppDbContext context) =>
        GetDesignTimeModel(context)
            .FindEntityType(typeof(Category))?
            .FindProperty(nameof(Category.CategoryCode))?
            .GetCollation();

    private static string? GetLicensePayloadColumnType(AppDbContext context) =>
        GetDesignTimeModel(context)
            .FindEntityType(typeof(InstalledLicense))?
            .FindProperty(nameof(InstalledLicense.RawLicenseJson))?
            .GetColumnType();

    private static IModel GetDesignTimeModel(AppDbContext context) =>
        context.GetService<IDesignTimeModel>().Model;

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "POS-DatabaseFoundationTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(
        Action action,
        string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}
