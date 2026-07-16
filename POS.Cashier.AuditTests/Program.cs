namespace POS.Cashier.AuditTests;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        string mode = args.FirstOrDefault()?.Trim().ToLowerInvariant() ?? "cashier";
        IReadOnlyList<AuditTestCase> tests = AuditTestCatalog.Create();

        tests = mode switch
        {
            "cashier" => tests.Where(test => test.Group != AuditGroups.Migration).ToArray(),
            "cashier-sqlserver" => SelectSqlServerCashierTests(tests),
            "cashier-sqlserver-concurrency-targeted" => SelectSqlServerTargetedConcurrencyTests(tests),
            "migration-empty" => tests.Where(test => test.Name == "Migration from empty creates current Cashier schema").ToArray(),
            "migration-upgrade" => tests.Where(test => test.Name == "Approved baseline database upgrades safely").ToArray(),
            "all" => tests,
            _ => throw new InvalidOperationException(
                $"Unknown audit mode '{mode}'. Use cashier, cashier-sqlserver, cashier-sqlserver-concurrency-targeted, migration-empty, migration-upgrade, or all.")
        };

        var runner = new AuditTestRunner(Console.Out);
        AuditRunSummary summary = await runner.RunAsync(tests);
        return summary.Failed == 0 ? 0 : 1;
    }


    private static IReadOnlyList<AuditTestCase> SelectSqlServerTargetedConcurrencyTests(
        IReadOnlyList<AuditTestCase> tests)
    {
        _ = SelectSqlServerCashierTests(tests);

        string[] names =
        {
            "Concurrent same-token checkout creates one financial effect",
            "Concurrent customer return cannot over-return"
        };

        IReadOnlyList<AuditTestCase> selected = tests
            .Where(test => names.Contains(test.Name, StringComparer.Ordinal))
            .ToArray();

        if (selected.Count != names.Length)
        {
            throw new InvalidOperationException(
                $"Expected {names.Length} targeted SQL Server concurrency tests, found {selected.Count}.");
        }

        return selected;
    }

    private static IReadOnlyList<AuditTestCase> SelectSqlServerCashierTests(IReadOnlyList<AuditTestCase> tests)
    {
        string? instance = Environment.GetEnvironmentVariable("POS_AUDIT_SQLSERVER_INSTANCE");
        if (string.IsNullOrWhiteSpace(instance))
        {
            throw new InvalidOperationException(
                "POS_AUDIT_SQLSERVER_INSTANCE must be set before running cashier-sqlserver mode.");
        }

        return tests.Where(test => test.Group != AuditGroups.Migration).ToArray();
    }
}
