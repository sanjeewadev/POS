using POS.Core.Repositories;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

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

        public string Format(IReadOnlyCollection<SupplierClaimExportRow> rows)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            var csv = new StringBuilder();
            csv.AppendLine(string.Join(",", Header.Select(Escape)));

            foreach (SupplierClaimExportRow row in rows)
            {
                csv.AppendLine(string.Join(",",
                    Escape(row.SupplierName),
                    Escape(row.PromotionReference),
                    Escape(row.ClaimStatus),
                    Escape(row.ClaimReferenceNo),
                    Escape(row.InvoiceNo),
                    Escape(row.InvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    Escape(row.Barcode),
                    Escape(row.SkuCode),
                    Escape(row.ItemDescription),
                    Escape(row.BatchNo),
                    Number(row.OriginalQuantity, "0.###"),
                    Number(row.ReturnedQuantity, "0.###"),
                    Number(row.NetQuantity, "0.###"),
                    Number(row.CostPrice, "0.00"),
                    Number(row.OriginalUnitPrice, "0.00"),
                    Number(row.OriginalClaimValue, "0.00"),
                    Number(row.ClaimValueReduction, "0.00"),
                    Number(row.NetClaimValue, "0.00"),
                    Escape(row.FreeReasonText),
                    Escape(row.CashierName),
                    Escape(row.ApprovedBy),
                    Escape(row.TerminalNo),
                    Escape(row.Remarks)));
            }

            return csv.ToString();
        }

        private static string Escape(string? value)
        {
            string text = (value ?? string.Empty)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        private static string Number(decimal value, string format) =>
            value.ToString(format, CultureInfo.InvariantCulture);
    }
}
