using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Configuration;

namespace POS.Core.Data.Configuration;

public sealed class DatabaseInitializationService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly DatabaseConnectionSettings _settings;

    public DatabaseInitializationService(
        IDbContextFactory<AppDbContext> contextFactory,
        DatabaseConnectionSettings settings)
    {
        _contextFactory = contextFactory;
        _settings = settings;
    }

    public Task InitializeBackOfficeAsync(
        CancellationToken cancellationToken = default) =>
        InitializeAsync(cancellationToken);

    public Task InitializeCashierAsync(
        CancellationToken cancellationToken = default) =>
        InitializeAsync(cancellationToken);

    private async Task InitializeAsync(
        CancellationToken cancellationToken)
    {
        await using AppDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        if (_settings.IsStandaloneSqlite)
        {
            await context.Database.MigrateAsync(cancellationToken);
            return;
        }

        bool canConnect;
        try
        {
            canConnect = await context.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw CreateServerUnavailableException(ex);
        }

        if (!canConnect)
            throw CreateServerUnavailableException();

        try
        {
            IReadOnlyList<string> appliedMigrations =
                (await context.Database
                    .GetAppliedMigrationsAsync(cancellationToken))
                .ToList();

            string? latestMigration =
                appliedMigrations
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .LastOrDefault();

            if (!string.Equals(
                    latestMigration,
                    ProductReleaseInfo.RequiredSqlServerMigration,
                    StringComparison.Ordinal))
            {
                throw new DatabaseConfigurationException(
                    $"The network database schema is not compatible with " +
                    $"{ProductReleaseInfo.ProductName} " +
                    $"{ProductReleaseInfo.ProductVersion}. " +
                    "Run the matching server setup or upgrade utility.");
            }

            // A successful connection and migration-history check are not
            // sufficient by themselves. Query a stable core table as a final
            // permission and schema check.
            _ = await context.Users
                .AsNoTracking()
                .Select(user => user.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (DatabaseConfigurationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DatabaseConfigurationException(
                "The store server was reached, but the POS network database schema is missing or incompatible. " +
                "Run the matching server setup or upgrade utility on the BackOffice server PC.",
                ex);
        }
    }

    private DatabaseConfigurationException CreateServerUnavailableException(
        Exception? innerException = null)
    {
        string message =
            $"Store server unavailable: {_settings.ServerHost},{_settings.ServerPort}. " +
            "Check that the server PC and SQL Server service are running and that this terminal is connected to the store network.";

        return innerException == null
            ? new DatabaseConfigurationException(message)
            : new DatabaseConfigurationException(message, innerException);
    }
}
