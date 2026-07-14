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
            "migration-empty" => tests.Where(test => test.Name == "Migration from empty creates current Cashier schema").ToArray(),
            "migration-upgrade" => tests.Where(test => test.Name == "Approved baseline database upgrades safely").ToArray(),
            "all" => tests,
            _ => throw new InvalidOperationException(
                $"Unknown audit mode '{mode}'. Use cashier, migration-empty, migration-upgrade, or all.")
        };

        var runner = new AuditTestRunner(Console.Out);
        AuditRunSummary summary = await runner.RunAsync(tests);
        return summary.Failed == 0 ? 0 : 1;
    }
}
