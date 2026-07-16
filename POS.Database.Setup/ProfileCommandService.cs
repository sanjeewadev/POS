using POS.Core.Data.Configuration;

namespace POS.Database.Setup;

internal sealed class ProfileCommandService
{
    public void WriteSqlServerProfile(
        string profilePath,
        string host,
        int port,
        string databaseName,
        string applicationLogin,
        string applicationPassword)
    {
        var settings = DatabaseConnectionSettings.CreateSqlServer(
            host,
            port,
            databaseName,
            applicationLogin,
            applicationPassword);

        var store = new DatabaseConnectionSettingsStore(profilePath);
        store.Save(settings);

        DatabaseConnectionSettings loaded = store.LoadOrDefault();
        if (!loaded.IsCentralSqlServer ||
            !string.Equals(loaded.ServerHost, settings.ServerHost, StringComparison.Ordinal) ||
            loaded.ServerPort != settings.ServerPort ||
            !string.Equals(loaded.DatabaseName, settings.DatabaseName, StringComparison.Ordinal) ||
            !string.Equals(loaded.UserName, settings.UserName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The encrypted SQL Server profile did not round-trip correctly.");
        }
    }

    public void WriteStandaloneProfile(string profilePath)
    {
        var store = new DatabaseConnectionSettingsStore(profilePath);
        store.ResetToStandaloneSqlite();
    }
}
