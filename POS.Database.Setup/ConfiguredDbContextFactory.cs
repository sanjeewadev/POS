using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Data.Configuration;

namespace POS.Database.Setup;

internal sealed class ConfiguredDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public ConfiguredDbContextFactory(DatabaseConnectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var builder = new DbContextOptionsBuilder<AppDbContext>();
        PosDatabaseOptionsConfigurator.Configure(builder, settings);
        _options = builder.Options;
    }

    public AppDbContext CreateDbContext() =>
        new(_options);

    public Task<AppDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());
}
