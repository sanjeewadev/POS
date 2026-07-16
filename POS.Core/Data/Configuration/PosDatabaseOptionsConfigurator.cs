using Microsoft.EntityFrameworkCore;

namespace POS.Core.Data.Configuration;

public static class PosDatabaseOptionsConfigurator
{
    public static void Configure(
        DbContextOptionsBuilder optionsBuilder,
        DatabaseConnectionSettings settings)
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
                sqlServerOptions.CommandTimeout(30);
                sqlServerOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(2),
                    errorNumbersToAdd: null);
            });
    }
}
