using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using POS.Core.Data;
using POS.Core.Data.Configuration;

namespace POS.Database.Setup;

public sealed class SqlServerDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        DatabaseConnectionSettings settings =
            DatabaseConnectionSettings.CreateSqlServer(
                serverHost: "127.0.0.1",
                serverPort: DatabaseConnectionSettings.DefaultSqlServerPort,
                databaseName: DatabaseConnectionSettings.DefaultDatabaseName,
                userName: "POS_DesignTime",
                password: "Design-Time-Only-Not-Used-987!");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        PosDatabaseOptionsConfigurator.Configure(
            optionsBuilder,
            settings,
            PosDatabaseOptionsConfigurator.SqlServerMigrationsAssembly);

        return new AppDbContext(optionsBuilder.Options);
    }
}
