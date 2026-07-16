using Microsoft.EntityFrameworkCore;
using POS.Core.Data;

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
            // A successful connection alone is insufficient. Querying a stable
            // core table also detects a missing or incompatible central schema.
            _ = await context.Users
                .AsNoTracking()
                .Select(user => user.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new DatabaseConfigurationException(
                "The store server was reached, but the POS network database schema is missing or incompatible. " +
                "Run the approved server database setup/update utility on the BackOffice server PC.",
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
