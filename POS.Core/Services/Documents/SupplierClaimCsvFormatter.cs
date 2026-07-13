using POS.Core.Repositories;
using POS.Core.Services.Exports;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace POS.Core.Services.Documents
{
    public sealed class SupplierClaimCsvFormatter
    {
        private static readonly string[] Header =
        {
            "Supplier",
            "Promotion Reference",
            "Status",
            "Claim Reference",
            "Invoice",
            "Invoice Date",
            "Barcode",
            "SKU",
            "Item",
            "Batch",
            "Original Quantity",
            "Returned Quantity",
            "Net Quantity",
            "Cost Price",
            "Original Unit Price",
            "Original Claim Value",
            "Claim Reduction",
            "Net Claim Value",
            "Reason",
            "Cashier",
            "Approved By",
            "Terminal",
            "Remarks"
        };

        private readonly CsvExportService _csv;

        public SupplierClaimCsvFormatter(CsvExportService? csv = null)
        {
            _csv = csv ?? new CsvExportService();
        }

        public string Format(IReadOnlyCollection<SupplierClaimExportRow> rows)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            return _csv.Format(
                Header,
                rows.Select(row => (IReadOnlyList<string?>)new string?[]
                {
                    row.SupplierName,
                    row.PromotionReference,
                    row.ClaimStatus,
                    row.ClaimReferenceNo,
                    row.InvoiceNo,
                    row.InvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    row.Barcode,
                    row.SkuCode,
                    row.ItemDescription,
                    row.BatchNo,
                    Number(row.OriginalQuantity, "0.###"),
                    Number(row.ReturnedQuantity, "0.###"),
                    Number(row.NetQuantity, "0.###"),
                    Number(row.CostPrice, "0.00"),
                    Number(row.OriginalUnitPrice, "0.00"),
                    Number(row.OriginalClaimValue, "0.00"),
                    Number(row.ClaimValueReduction, "0.00"),
                    Number(row.NetClaimValue, "0.00"),
                    row.FreeReasonText,
                    row.CashierName,
                    row.ApprovedBy,
                    row.TerminalNo,
                    row.Remarks
                }));
        }

        private static string Number(decimal value, string format) =>
            value.ToString(format, CultureInfo.InvariantCulture);
    }
}
