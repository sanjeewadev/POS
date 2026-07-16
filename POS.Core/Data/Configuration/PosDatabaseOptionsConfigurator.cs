using Microsoft.EntityFrameworkCore;

namespace POS.Core.Data.Configuration;

public static class PosDatabaseOptionsConfigurator
{
    public const string SqlServerMigrationsAssembly = "POS.Database.Setup";

    public static void Configure(
        DbContextOptionsBuilder optionsBuilder,
        DatabaseConnectionSettings settings,
        string? sqlServerMigrationsAssembly = null)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(settings);

        settings.Validate();

        if (settings.IsStandaloneSqlite)
        {
            optionsBuilder.UseSqlite(
                settings.BuildConnectionString());
            return;
        }

        optionsBuilder.UseSqlServer(
            settings.BuildConnectionString(),
            sqlServerOptions =>
            {
                if (!string.IsNullOrWhiteSpace(sqlServerMigrationsAssembly))
                {
                    sqlServerOptions.MigrationsAssembly(
                        sqlServerMigrationsAssembly.Trim());
                }

                // Explicit repository transactions are used throughout the POS.
                // Do not enable EF Core's retrying execution strategy here, because
                // it rejects user-initiated transactions unless every transaction is
                // wrapped by the execution strategy. Connection-open retries remain
                // configured in the SQL Server connection string.
                sqlServerOptions.CommandTimeout(30);
            });
    }
}
