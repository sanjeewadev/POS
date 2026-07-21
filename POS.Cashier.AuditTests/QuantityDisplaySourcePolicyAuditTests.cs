using System.Globalization;
using System.Text.RegularExpressions;
using POS.Core.Utilities;

namespace POS.Cashier.AuditTests;

internal static class QuantityDisplaySourcePolicyAuditTests
{
    public static Task QuantityDisplayIsCompactAndConsistentAsync()
    {
        VerifyFormatterExamples();
        VerifyUserFacingSourcesDoNotUseFixedThreeDecimals();
        VerifyDisplayAndInputPolicies();

        return Task.CompletedTask;
    }

    private static void VerifyFormatterExamples()
    {
        CultureInfo culture = CultureInfo.InvariantCulture;

        AuditAssert.Equal(
            "10",
            QuantityDisplayFormatter.Format(10.000m, culture),
            "Whole quantity display");
        AuditAssert.Equal(
            "10.5",
            QuantityDisplayFormatter.Format(10.500m, culture),
            "Half quantity display");
        AuditAssert.Equal(
            "10.125",
            QuantityDisplayFormatter.Format(10.125m, culture),
            "Three-decimal quantity display");
        AuditAssert.Equal(
            "1,250",
            QuantityDisplayFormatter.Format(1250.000m, culture),
            "Grouped whole quantity display");
        AuditAssert.Equal(
            "1250.5",
            QuantityDisplayFormatter.FormatInvariantData(1250.500m),
            "Invariant data quantity display");
    }

    private static void VerifyUserFacingSourcesDoNotUseFixedThreeDecimals()
    {
        string[] roots =
        {
            Path.Combine(AuditPaths.RepositoryRoot, "POS.BackOffice.UI"),
            Path.Combine(AuditPaths.RepositoryRoot, "POS.Cashier.UI"),
            Path.Combine(AuditPaths.RepositoryRoot, "POS.Core")
        };

        var prohibited = new Regex(
            "StringFormat\\s*=\\s*(?:\\{\\}\\{0:)?N3|:[Nn]3\\}|ToString\\(\"N3\"",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        var violations = new List<string>();

        foreach (string root in roots)
        {
            foreach (string file in Directory.EnumerateFiles(
                         root,
                         "*.*",
                         SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(file);
                if (!extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".xaml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (file.Contains(
                        $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                        StringComparison.OrdinalIgnoreCase) ||
                    file.Contains(
                        $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string text = File.ReadAllText(file);
                if (prohibited.IsMatch(text))
                {
                    violations.Add(Path.GetRelativePath(AuditPaths.RepositoryRoot, file));
                }
            }
        }

        AuditAssert.Equal(
            0,
            violations.Count,
            "Fixed three-decimal user-facing quantity files: " +
            string.Join(", ", violations));
    }

    private static void VerifyDisplayAndInputPolicies()
    {
        string stockInquiry = Read(
            "POS.Cashier.UI",
            "Dialogs",
            "StockInquiryDialog.xaml");
        string grnView = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "GrnView.xaml");
        string salesFormatter = Read(
            "POS.Core",
            "Services",
            "Documents",
            "SalesDocumentTextFormatter.cs");
        string cashierSales = Read(
            "POS.Cashier.UI",
            "ViewModels",
            "SalesViewModel.cs");
        string smartConverter = Read(
            "POS.BackOffice.UI",
            "Converters",
            "SmartQuantityConverter.cs");
        string stockBalance = Read(
            "POS.BackOffice.UI",
            "Views",
            "Pages",
            "InventoryOperations",
            "StockBalanceView.xaml");

        AuditAssert.Contains(
            stockInquiry,
            "StringFormat={}{0:#,##0.###}",
            "Grouped read-only Cashier stock quantity");
        AuditAssert.Contains(
            grnView,
            "StringFormat={}{0:0.###}",
            "Ungrouped editable GRN quantity");
        AuditAssert.Contains(
            salesFormatter,
            "QuantityDisplayFormatter.Format(line.Quantity)",
            "Receipt quantity formatter");
        AuditAssert.Contains(
            cashierSales,
            "QuantityDisplayFormatter.Format(selectedBatch.AvailableQty)",
            "Cashier notification quantity formatter");
        AuditAssert.Contains(
            smartConverter,
            "IsDisplayMode(parameter)",
            "BackOffice display converter mode");
        AuditAssert.Contains(
            stockBalance,
            "ConverterParameter=Display",
            "Grouped Stock Balance quantities");
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(
            Path.Combine(
                new[] { AuditPaths.RepositoryRoot }
                    .Concat(segments)
                    .ToArray()));
}
