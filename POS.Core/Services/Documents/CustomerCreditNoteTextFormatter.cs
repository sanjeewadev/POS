using System;
using System.Globalization;
using System.Linq;
using System.Text;
using POS.Core.Configuration;
using POS.Core.Models;

namespace POS.Core.Services.Documents
{
    public sealed class CustomerCreditNoteTextFormatter
    {
        public string FormatCreditNote(
            CustomerReturnHeader returnHeader,
            StoreSettings settings,
            int paperWidth,
            string copyLabel = SalesDocumentCopyLabels.Original)
        {
            if (returnHeader == null)
                throw new ArgumentNullException(nameof(returnHeader));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            int columns = paperWidth <= 58 ? 32 : 48;
            SalesHeader? sale = returnHeader.OriginalSalesHeader;
            var text = new StringBuilder();

            AppendCentered(text, FirstNonEmpty(settings.StoreName, settings.LegalName, "My Store"), columns);
            AppendWrappedCentered(text, settings.AddressLine1, columns);
            AppendWrappedCentered(text, settings.AddressLine2, columns);
            AppendWrappedCentered(text, JoinNonEmpty(", ", settings.City, settings.PostalCode), columns);

            string tin = FirstNonEmpty(sale?.SupplierTinSnapshot, settings.TaxpayerIdentificationNumber);
            string vatNo = FirstNonEmpty(sale?.SupplierVatNoSnapshot, settings.VatRegistrationNumber);

            if (!string.IsNullOrWhiteSpace(tin))
                AppendCentered(text, $"TIN: {tin}", columns);
            if (!string.IsNullOrWhiteSpace(vatNo))
                AppendCentered(text, $"VAT No: {vatNo}", columns);

            text.AppendLine();
            AppendCentered(text, "CREDIT NOTE", columns);
            AppendCentered(text, NormalizeCopyLabel(copyLabel), columns);
            text.AppendLine();

            AppendLabel(text, "Credit No", returnHeader.CreditNoteNo ?? returnHeader.ReturnNo, columns);
            AppendLabel(text, "Receipt", returnHeader.OriginalInvoiceNo ?? string.Empty, columns);
            if (sale != null)
                AppendLabel(text, "Sale Date", sale.TransactionDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), columns);
            AppendLabel(text, "Return", returnHeader.ReturnDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), columns);
            AppendLabel(text, "Terminal", returnHeader.TerminalNo, columns);
            AppendLabel(text, "Cashier", returnHeader.CashierName, columns);
            AppendLabel(text, "Authorized", returnHeader.AuthorizedBy, columns);

            if (sale != null)
            {
                AppendWrappedLabel(text, "Customer", sale.CustomerName, columns);
                if (!string.IsNullOrWhiteSpace(sale.CustomerAddressSnapshot))
                    AppendWrappedLabel(text, "Address", sale.CustomerAddressSnapshot, columns);
            }

            AppendSeparator(text, columns);

            int lineNo = 1;
            foreach (CustomerReturnLine line in returnHeader.Lines.OrderBy(row => row.Id))
            {
                AppendWrapped(text, $"{lineNo}. {FirstNonEmpty(line.ItemDescription, "Item")}", columns);

                string qtyText = $"{line.QuantityReturned:0.###} x {FormatMoney(line.RefundValue, settings)}";
                AppendTwoColumns(text, qtyText, FormatMoney(line.LineTotalRefund, settings), columns);

                SalesLine? source = line.SalesLine;
                if (source != null)
                {
                    decimal gross = Money(source.UnitPrice * line.QuantityReturned);
                    decimal reversedDiscount = Math.Max(0m, Money(gross - line.LineTotalRefund));
                    if (reversedDiscount > 0m)
                        AppendTwoColumns(text, "Discount reversed", FormatMoney(reversedDiscount, settings), columns);
                }

                if (string.Equals(
                        line.TaxSnapshotStatus,
                        TaxSnapshotStatuses.Complete,
                        StringComparison.Ordinal) &&
                    line.TaxInclusiveAmountSnapshot.HasValue)
                {
                    string taxLabel = FirstNonEmpty(
                        line.TaxNameSnapshot,
                        line.TaxCategoryCodeSnapshot,
                        "Tax");
                    string rate = line.TaxRatePercentSnapshot.HasValue
                        ? $" {line.TaxRatePercentSnapshot.Value:0.####}%"
                        : string.Empty;

                    AppendTwoColumns(
                        text,
                        taxLabel + rate,
                        FormatMoney(line.VatAmountSnapshot ?? 0m, settings),
                        columns);
                    AppendTwoColumns(
                        text,
                        "Taxable",
                        FormatMoney(line.TaxableAmountSnapshot ?? 0m, settings),
                        columns);
                }
                else
                {
                    AppendWrapped(text, "VAT detail unavailable for this historical line.", columns);
                }

                if (!string.IsNullOrWhiteSpace(line.ReturnReason))
                    AppendWrappedLabel(text, "Reason", line.ReturnReason, columns);

                AppendSeparator(text, columns);
                lineNo++;
            }

            AppendTwoColumns(text, "TOTAL CREDIT NOTE", FormatMoney(returnHeader.TotalRefundAmount, settings), columns);
            if (returnHeader.AccountCreditAmount > 0m)
                AppendTwoColumns(text, "ACCOUNT CREDIT", FormatMoney(returnHeader.AccountCreditAmount, settings), columns);
            if (returnHeader.CashRefundAmount > 0m)
                AppendTwoColumns(text, "CASH REFUND", FormatMoney(returnHeader.CashRefundAmount, settings), columns);
            AppendSeparator(text, columns);

            if (string.Equals(
                    returnHeader.TaxSnapshotStatus,
                    TaxSnapshotStatuses.Complete,
                    StringComparison.Ordinal))
            {
                AppendTwoColumns(text, "Taxable value", FormatMoney(returnHeader.TaxableAmountTotal ?? 0m, settings), columns);
                AppendTwoColumns(text, "VAT reversed", FormatMoney(returnHeader.TotalVatAmount ?? 0m, settings), columns);
                AppendTwoColumns(text, "Standard VAT", FormatMoney(returnHeader.StandardRatedAmount ?? 0m, settings), columns);
                AppendTwoColumns(text, "Zero Rated", FormatMoney(returnHeader.ZeroRatedAmount ?? 0m, settings), columns);
                AppendTwoColumns(text, "Exempt", FormatMoney(returnHeader.ExemptAmount ?? 0m, settings), columns);
                AppendTwoColumns(text, "Out of Scope", FormatMoney(returnHeader.OutOfScopeAmount ?? 0m, settings), columns);
            }
            else
            {
                AppendWrapped(text, "Detailed VAT snapshots are unavailable. No VAT values were invented.", columns);
            }

            AppendSeparator(text, columns);
            AppendWrapped(text, "This credit note reverses the referenced original sale values.", columns);
            AppendWrappedCentered(text, settings.ReceiptFooter, columns);

            return text.ToString();
        }

        private static string NormalizeCopyLabel(string value) =>
            string.Equals(value, SalesDocumentCopyLabels.Reprint, StringComparison.OrdinalIgnoreCase)
                ? SalesDocumentCopyLabels.Reprint
                : SalesDocumentCopyLabels.Original;

        private static string FormatMoney(decimal amount, StoreSettings settings)
        {
            string symbol = FirstNonEmpty(settings.CurrencySymbol, "Rs.");
            return $"{symbol} {amount:N2}";
        }

        private static decimal Money(decimal value) =>
            decimal.Round(value, 2, MidpointRounding.AwayFromZero);

        private static void AppendLabel(StringBuilder text, string label, string? value, int columns) =>
            AppendWrapped(text, $"{label}: {FirstNonEmpty(value, "-")}", columns);

        private static void AppendWrappedLabel(StringBuilder text, string label, string? value, int columns) =>
            AppendWrapped(text, $"{label}: {FirstNonEmpty(value, "-")}", columns);

        private static void AppendSeparator(StringBuilder text, int columns) =>
            text.AppendLine(new string('-', columns));

        private static void AppendCentered(StringBuilder text, string? value, int columns)
        {
            string safe = FirstNonEmpty(value);
            if (safe.Length > columns)
            {
                AppendWrappedCentered(text, safe, columns);
                return;
            }

            int padding = Math.Max(0, (columns - safe.Length) / 2);
            text.AppendLine(new string(' ', padding) + safe);
        }

        private static void AppendWrappedCentered(StringBuilder text, string? value, int columns)
        {
            foreach (string line in Wrap(value, columns))
                AppendCentered(text, line, columns);
        }

        private static void AppendWrapped(StringBuilder text, string? value, int columns)
        {
            foreach (string line in Wrap(value, columns))
                text.AppendLine(line);
        }

        private static void AppendTwoColumns(
            StringBuilder text,
            string left,
            string right,
            int columns)
        {
            string safeRight = right ?? string.Empty;
            int leftWidth = Math.Max(1, columns - safeRight.Length - 1);
            string safeLeft = left ?? string.Empty;

            if (safeLeft.Length > leftWidth)
            {
                foreach (string wrapped in Wrap(safeLeft, columns))
                    text.AppendLine(wrapped);
                text.AppendLine(safeRight.PadLeft(columns));
                return;
            }

            text.AppendLine(safeLeft.PadRight(leftWidth) + " " + safeRight);
        }

        private static string[] Wrap(string? value, int columns)
        {
            string safe = FirstNonEmpty(value);
            if (string.IsNullOrWhiteSpace(safe))
                return Array.Empty<string>();

            var lines = new System.Collections.Generic.List<string>();
            string remaining = safe;

            while (remaining.Length > columns)
            {
                int split = remaining.LastIndexOf(' ', columns);
                if (split <= 0)
                    split = columns;

                lines.Add(remaining[..split].Trim());
                remaining = remaining[split..].Trim();
            }

            if (remaining.Length > 0)
                lines.Add(remaining);

            return lines.ToArray();
        }

        private static string JoinNonEmpty(string separator, params string?[] values) =>
            string.Join(separator, values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));

        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }
}
