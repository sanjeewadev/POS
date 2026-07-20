using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;

namespace POS.Core.Data.Configuration;

public sealed class DatabaseInitializationService
{
    private const int SqlServerStartupAttemptCount = 3;
    private static readonly TimeSpan SqlServerRetryDelay =
        TimeSpan.FromSeconds(2);

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
        if (_settings.IsStandaloneSqlite)
        {
            await using AppDbContext sqliteContext =
                await _contextFactory.CreateDbContextAsync(
                    cancellationToken);

            await sqliteContext.Database
                .MigrateAsync(cancellationToken);
            return;
        }

        Exception? lastConnectionException = null;

        for (int attempt = 1;
             attempt <= SqlServerStartupAttemptCount;
             attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using AppDbContext context =
                await _contextFactory.CreateDbContextAsync(
                    cancellationToken);

            bool canConnect = false;

            try
            {
                canConnect = await context.Database
                    .CanConnectAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                lastConnectionException = ex;
            }

            if (canConnect)
            {
                await ValidateSqlServerSchemaAsync(
                    context,
                    cancellationToken);
                return;
            }

            if (attempt < SqlServerStartupAttemptCount)
            {
                await Task.Delay(
                    SqlServerRetryDelay,
                    cancellationToken);
            }
        }

        throw CreateServerUnavailableException(
            lastConnectionException);
    }

    private static async Task ValidateSqlServerSchemaAsync(
        AppDbContext context,
        CancellationToken cancellationToken)
    {
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
            $"Advanced POS made {SqlServerStartupAttemptCount} controlled connection attempts. " +
            "Check that the server PC and SQL Server service are running and that this terminal is connected to the store network. " +
            "Use Repair Connection when the router, server computer name, server IP address, or SQL port changed.";

        return innerException == null
            ? new DatabaseConfigurationException(message)
            : new DatabaseConfigurationException(message, innerException);
    }
}
