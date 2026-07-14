using Microsoft.EntityFrameworkCore;
using POS.Core.Data;

namespace POS.Cashier.AuditTests;

internal static class MigrationAuditTests
{
    private const string LatestMigration = "20260713120000_AddFreeIssueSupplierClaimCompletion";

    public static Task MigrationFromEmptyCreatesCurrentSchemaAsync()
    {
        using var factory = new AuditDbContextFactory(migrate: true);
        using AppDbContext context = factory.CreateDbContext();

        string[] applied = context.Database.GetAppliedMigrations().ToArray();
        AuditAssert.True(applied.Contains(LatestMigration), $"Latest migration '{LatestMigration}' was not applied.");

        string[] requiredTables =
        {
            "SalesHeaders",
            "SalesLines",
            "SalesPayments",
            "CashierCartSessions",
            "ShiftSessions",
            "ShiftCloseSnapshots",
            "CustomerReturnHeaders",
            "GiftVouchers",
            "GiftVoucherTransactions",
            "FreeIssueRules",
            "FreeItemClaimLogs"
        };

        foreach (string table in requiredTables)
            AuditAssert.True(SqliteObjectExists(context, "table", table), $"Required table '{table}' is missing.");

        AuditAssert.True(SqliteObjectExists(context, "index", "IX_SalesHeaders_CheckoutToken"),
            "Checkout-token index is missing.");
        return Task.CompletedTask;
    }

    public static Task ApprovedBaselineUpgradesSafelyAsync()
    {
        string? baseline = Environment.GetEnvironmentVariable("POS_AUDIT_UPGRADE_BASELINE");
        if (string.IsNullOrWhiteSpace(baseline) || !File.Exists(baseline))
            throw new AuditSkippedException("No approved upgrade baseline database was supplied.");

        string target = Path.Combine(AuditPaths.GetAuditTempRoot(), $"upgrade-{Guid.NewGuid():N}.db");
        AuditPaths.AssertSafeDatabasePath(target);
        File.Copy(baseline, target, overwrite: false);

        try
        {
            using var factory = new AuditDbContextFactory(migrate: true, existingDatabasePath: target);
            using AppDbContext context = factory.CreateDbContext();
            string[] applied = context.Database.GetAppliedMigrations().ToArray();
            AuditAssert.True(applied.Contains(LatestMigration),
                $"Approved baseline did not upgrade through '{LatestMigration}'.");
            AuditAssert.True(SqliteObjectExists(context, "table", "SalesHeaders"),
                "SalesHeaders table is missing after upgrade.");
            AuditAssert.True(SqliteObjectExists(context, "table", "ShiftCloseSnapshots"),
                "ShiftCloseSnapshots table is missing after upgrade.");
        }
        finally
        {
            DeleteIfPresent(target);
            DeleteIfPresent(target + "-shm");
            DeleteIfPresent(target + "-wal");
        }

        return Task.CompletedTask;
    }

    private static bool SqliteObjectExists(AppDbContext context, string objectType, string objectName)
    {
        using var command = context.Database.GetDbConnection().CreateCommand();
        if (command.Connection?.State != System.Data.ConnectionState.Open)
            command.Connection?.Open();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = $type AND name = $name;";
        var typeParameter = command.CreateParameter();
        typeParameter.ParameterName = "$type";
        typeParameter.Value = objectType;
        command.Parameters.Add(typeParameter);
        var nameParameter = command.CreateParameter();
        nameParameter.ParameterName = "$name";
        nameParameter.Value = objectName;
        command.Parameters.Add(nameParameter);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
