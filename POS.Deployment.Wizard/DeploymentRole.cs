namespace POS.Deployment.Wizard;

public enum DeploymentRole
{
    Server,
    Cashier
}

internal enum ServerInstallMode
{
    NewStore,
    MigrateSqlite,
    RestoreBackup
}
