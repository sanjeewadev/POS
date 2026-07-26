using System.Text.RegularExpressions;
using POS.Core.Utilities;

namespace POS.Cashier.AuditTests;

internal static class SqlServerCollationSourcePolicyAuditTests
{
    private const string CanonicalCollation =
        "Latin1_General_100_CI_AS_SC";

    public static Task OperationalTextCollationsAreControlledAsync()
    {
        string appDbContext = Read(
            "POS.Core",
            "Data",
            "AppDbContext.cs");

        AssertPropertyUsesCanonicalCollation(
            appDbContext,
            "i => i.PrintName",
            "Item Parent Print Name");
        AssertPropertyUsesCanonicalCollation(
            appDbContext,
            "i => i.BaseUom",
            "Item Parent legacy Base UOM");
        AssertPropertyUsesCanonicalCollation(
            appDbContext,
            "l => l.Uom",
            "operational line UOM",
            minimumOccurrences: 3);

        string provisioning = Read(
            "POS.Database.Setup",
            "ServerProvisioningService.cs");
        AuditAssert.Contains(
            provisioning,
            "CREATE DATABASE {SqlName.Quote(databaseName)}",
            "SQL Server database creation statement");
        AuditAssert.Contains(
            provisioning,
            "DatabaseProviderModelConventions.SqlServerCaseInsensitiveCollation",
            "canonical SQL Server database collation");

        string release = Read(
            "POS.Core",
            "Configuration",
            "ProductReleaseInfo.cs");
        AuditAssert.Contains(
            release,
            "20260726091000_AddBatchSellingPriceOverrideFoundation",
            "required latest SQL Server migration");

        string migration = Read(
            "POS.Database.Setup",
            "Migrations",
            "20260724043000_RepairOperationalTextCollations.cs");

        string[] repairedColumns =
        {
            "[ItemParents] ALTER COLUMN [PrintName]",
            "[ItemParents] ALTER COLUMN [BaseUom]",
            "[PoLines] ALTER COLUMN [Uom]",
            "[GrnLines] ALTER COLUMN [Uom]",
            "[SalesLines] ALTER COLUMN [Uom]"
        };

        foreach (string marker in repairedColumns)
        {
            AuditAssert.Contains(
                migration,
                marker,
                $"targeted collation repair for {marker}");
        }

        AuditAssert.Equal(
            5,
            Regex.Matches(
                migration,
                Regex.Escape($"COLLATE {CanonicalCollation}"),
                RegexOptions.CultureInvariant).Count,
            "canonical collation ALTER count");
        AuditAssert.Equal(
            5,
            Regex.Matches(
                migration,
                "COLLATE DATABASE_DEFAULT",
                RegexOptions.CultureInvariant).Count,
            "database-default rollback ALTER count");

        AssertRepositorySeparatesUomColumns(
            "GrnRepository.cs",
            requiredResolverCalls: 3);
        AssertRepositorySeparatesUomColumns(
            "PoRepository.cs",
            requiredResolverCalls: 2);
        AssertRepositorySeparatesUomColumns(
            "PriceManagementRepository.cs",
            requiredResolverCalls: 1);

        AuditAssert.Equal(
            "PCS",
            UomValueResolver.Resolve(null, null),
            "empty UOM fallback");
        AuditAssert.Equal(
            "EA",
            UomValueResolver.Resolve(" EA ", "PCS"),
            "preferred UOM normalization");
        AuditAssert.Equal(
            "BOX",
            UomValueResolver.Resolve("", " BOX "),
            "master UOM normalization");

        return Task.CompletedTask;
    }

    private static void AssertPropertyUsesCanonicalCollation(
        string source,
        string propertyExpression,
        string label,
        int minimumOccurrences = 1)
    {
        string pattern =
            Regex.Escape($"entity.Property({propertyExpression})") +
            @"[\s\S]{0,180}?\.UseCollation\(CaseInsensitiveCollation\)";

        int count = Regex.Matches(
            source,
            pattern,
            RegexOptions.CultureInvariant).Count;

        AuditAssert.True(
            count >= minimumOccurrences,
            $"{label}: expected at least {minimumOccurrences} canonical collation mappings, found {count}.");
    }

    private static void AssertRepositorySeparatesUomColumns(
        string fileName,
        int requiredResolverCalls)
    {
        string source = Read(
            "POS.Core",
            "Repositories",
            fileName);

        AuditAssert.Contains(
            source,
            "BaseUom =",
            $"{fileName} raw Base UOM projection");
        AuditAssert.Contains(
            source,
            "MasterUom =",
            $"{fileName} raw master UOM projection");

        int resolverCalls = Regex.Matches(
            source,
            Regex.Escape("UomValueResolver.Resolve("),
            RegexOptions.CultureInvariant).Count;

        AuditAssert.True(
            resolverCalls >= requiredResolverCalls,
            $"{fileName}: expected at least {requiredResolverCalls} UOM resolver calls, found {resolverCalls}.");

        AuditAssert.False(
            Regex.IsMatch(
                source,
                @"Uom\s*=\s*string\.IsNullOrWhiteSpace\([^\r\n]+BaseUom\)[\s\S]{0,120}?UnitOfMeasure\.UomCode[\s\S]{0,80}?:[\s\S]{0,80}?BaseUom",
                RegexOptions.CultureInvariant),
            $"{fileName} must not combine differently collated UOM columns inside a SQL CASE projection");
    }

    private static string Read(params string[] parts)
    {
        string path = Path.Combine(
            new[] { AuditPaths.RepositoryRoot }.Concat(parts).ToArray());
        return File.ReadAllText(path);
    }
}
