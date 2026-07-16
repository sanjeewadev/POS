using Microsoft.Data.SqlClient;

namespace POS.Database.Setup;

internal static class SqlServerConnectionFactory
{
    public static string BuildAdministratorConnectionString(
        string instance,
        string database = "master")
    {
        if (string.IsNullOrWhiteSpace(instance))
            throw new ArgumentException("A SQL Server instance is required.", nameof(instance));

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = instance.Trim(),
            InitialCatalog = database.Trim(),
            IntegratedSecurity = true,
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = true,
            ConnectTimeout = 15,
            MultipleActiveResultSets = true,
            ApplicationName = "Advanced POS Database Setup"
        };

        return builder.ConnectionString;
    }

    public static string BuildApplicationConnectionString(
        string host,
        int port,
        string database,
        string login,
        string password)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"tcp:{host.Trim()},{port}",
            InitialCatalog = database.Trim(),
            UserID = login.Trim(),
            Password = password,
            IntegratedSecurity = false,
            PersistSecurityInfo = false,
            Encrypt = SqlConnectionEncryptOption.Mandatory,
            TrustServerCertificate = true,
            ConnectTimeout = 10,
            ConnectRetryCount = 3,
            ConnectRetryInterval = 1,
            MultipleActiveResultSets = true,
            Pooling = true,
            ApplicationName = "Advanced POS",
            WorkstationID = Environment.MachineName
        };

        return builder.ConnectionString;
    }
}
