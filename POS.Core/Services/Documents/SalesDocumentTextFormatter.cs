using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using POS.Core.Configuration;
using POS.Core.Models;

namespace POS.Core.Services.Documents
{
    public sealed class SalesDocumentTextFormatter
    {
        public string FormatReceipt(
            SalesHeader sale,
            StoreSettings settings,
            int paperWidth,
            string copyLabel)
        {
            ValidateCommon(sale, settings);

            int columns = GetColumns(paperWidth);
            var text = new StringBuilder();

            AppendStoreHeader(text, settings, columns, true, sale);
            AppendCentered(text, "SALES RECEIPT", columns);
            AppendCentered(text, "NOT A TAX INVOICE", columns);
            AppendCentered(text, NormalizeCopyLabel(copyLabel), columns);
            AppendBlank(text);

            AppendLabel(text, "Invoice", sale.InvoiceNo, columns);
            AppendLabel(text, "Date", sale.TransactionDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), columns);
            AppendLabel(text, "Terminal", sale.TerminalNo, columns);
            AppendLabel(text, "Cashier", sale.CashierName, columns);
            AppendLabel(text, "Customer", FirstNonEmpty(sale.CustomerName, "Walk-In"), columns);
            AppendSeparator(text, columns);

            AppendLines(text, sale, settings, columns, includeTaxColumns: false);
            AppendTotals(text, sale, settings, columns);
            AppendPaymentSummary(text, sale, settings, columns);
            AppendTaxSummary(text, sale, settings, columns, formalTaxInvoice: false);
            AppendStoreFooter(text, settings, columns);

            return text.ToString();
        }

        public string FormatTaxInvoice(
            SalesHeader sale,
            StoreSettings settings,
            DateTime issuedAtUtc,
            int paperWidth,
            string copyLabel)
        {
            ValidateCommon(sale, settings);

            if (string.IsNullOrWhiteSpace(sale.TaxInvoiceNo))
            {
                throw new InvalidOperationException(
                    "Tax Invoice number is missing.");
            }

            if (!string.Equals(
                    sale.TaxSnapshotStatus,
                    TaxSnapshotStatuses.Complete,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Tax Invoice cannot be formatted because the sale tax snapshot is incomplete.");
            }

            int columns = GetColumns(paperWidth);
            var text = new StringBuilder();

            AppendStoreHeader(text, settings, columns, true, sale);
            AppendCentered(text, "TAX INVOICE", columns);
            AppendCentered(text, NormalizeCopyLabel(copyLabel), columns);
            AppendBlank(text);

            AppendLabel(text, "Tax Inv", sale.TaxInvoiceNo, columns);
            AppendLabel(text, "Receipt", sale.InvoiceNo, columns);
            AppendLabel(text, "Sale Date", sale.TransactionDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), columns);
            AppendLabel(text, "Issued", issuedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), columns);
            AppendLabel(text, "Terminal", sale.TerminalNo, columns);
            AppendLabel(text, "Cashier", sale.CashierName, columns);
            AppendSeparator(text, columns);

            AppendWrappedLabel(text, "Customer", sale.CustomerName, columns);
            AppendWrappedLabel(text, "Address", sale.CustomerAddressSnapshot, columns);

            if (!string.IsNullOrWhiteSpace(sale.CustomerTinSnapshot))
                AppendLabel(text, "Cust TIN", sale.CustomerTinSnapshot, columns);

            if (!string.IsNullOrWhiteSpace(sale.CustomerVatNoSnapshot))
                AppendLabel(text, "Cust VAT", sale.CustomerVatNoSnapshot, columns);

            AppendSeparator(text, columns);
            AppendLines(text, sale, settings, columns, includeTaxColumns: true);
            AppendTotals(text, sale, settings, columns);
            AppendTaxSummary(text, sale, settings, columns, formalTaxInvoice: true);
            AppendPaymentSummary(text, sale, settings, columns);
            AppendStoreFooter(text, settings, columns);

            return text.ToString();
        }

        private static void AppendStoreHeader(
            StringBuilder text,
            StoreSettings settings,
            int columns,
            bool useSavedTaxNumbers,
            SalesHeader sale)
        {
            AppendCentered(
                text,
                FirstNonEmpty(
                    settings.StoreName,
                    settings.LegalName,
                    "My Store"),
                columns);

            if (!string.IsNullOrWhiteSpace(settings.LegalName) &&
                !string.Equals(
                    settings.LegalName.Trim(),
                    settings.StoreName?.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                AppendCentered(text, settings.LegalName, columns);
            }

            AppendWrappedCentered(text, settings.ReceiptHeader, columns);
            AppendWrappedCentered(text, settings.AddressLine1, columns);
            AppendWrappedCentered(text, settings.AddressLine2, columns);
            AppendWrappedCentered(
                text,
                JoinNonEmpty(", ", settings.City, settings.PostalCode),
                columns);
            AppendWrappedCentered(text, settings.Country, columns);

            if (!string.IsNullOrWhiteSpace(settings.Phone))
                AppendCentered(text, $"Tel: {settings.Phone.Trim()}", columns);

            if (!string.IsNullOrWhiteSpace(settings.Email))
                AppendCentered(text, $"Email: {settings.Email.Trim()}", columns);

            string tin = useSavedTaxNumbers
                ? sale.SupplierTinSnapshot
                : settings.TaxpayerIdentificationNumber;

            string vatNo = useSavedTaxNumbers
                ? sale.SupplierVatNoSnapshot
                : settings.VatRegistrationNumber;

            if (!string.IsNullOrWhiteSpace(tin))
                AppendCentered(text, $"TIN: {tin.Trim()}", columns);

            if (!string.IsNullOrWhiteSpace(vatNo))
                AppendCentered(text, $"VAT No: {vatNo.Trim()}", columns);

            AppendBlank(text);
        }

        private static void AppendLines(
            StringBuilder text,
            SalesHeader sale,
            StoreSettings settings,
            int columns,
            bool includeTaxColumns)
        {
            int lineNo = 1;

            foreach (SalesLine line in sale.SalesLines.OrderBy(row => row.Id))
            {
                AppendWrapped(
                    text,
                    $"{lineNo}. {FirstNonEmpty(line.ItemDescription, line.SkuCode, "Item")}",
                    columns);

                string quantityAndPrice =
                    $"{line.Quantity:0.###} {FirstNonEmpty(line.Uom, "PCS")} x {FormatMoney(line.UnitPrice, settings)}";

                AppendTwoColumns(
                    text,
                    quantityAndPrice,
                    FormatMoney(line.LineTotal, settings),
                    columns);

                if (line.DiscountAmount > 0m)
                {
                    AppendTwoColumns(
                        text,
                        "Discount",
                        FormatMoney(line.DiscountAmount, settings),
                        columns);
                }

                if (includeTaxColumns)
                {
                    string category = FirstNonEmpty(
                        line.TaxNameSnapshot,
                        line.TaxCategoryCodeSnapshot,
                        "Tax");

                    string rate = line.TaxRatePercentSnapshot.HasValue
                        ? $"{line.TaxRatePercentSnapshot.Value:0.####}%"
                        : "0%";

                    AppendTwoColumns(
                        text,
                        $"{category} {rate}",
                        FormatMoney(line.VatAmountSnapshot ?? 0m, settings),
                        columns);

                    AppendTwoColumns(
                        text,
                        "Taxable",
                        FormatMoney(line.TaxableAmountSnapshot ?? 0m, settings),
                        columns);
                }

                AppendSeparator(text, columns);
                lineNo++;
            }
        }

        private static void AppendTotals(
            StringBuilder text,
            SalesHeader sale,
            StoreSettings settings,
            int columns)
        {
            AppendTwoColumns(text, "Gross Total", FormatMoney(sale.GrossTotal, settings), columns);

            if (sale.TotalDiscount > 0m)
                AppendTwoColumns(text, "Discount", FormatMoney(sale.TotalDiscount, settings), columns);

            AppendTwoColumns(text, "NET TOTAL", FormatMoney(sale.NetTotal, settings), columns);
            AppendSeparator(text, columns);
        }

        private static void AppendPaymentSummary(
            StringBuilder text,
            SalesHeader sale,
            StoreSettings settings,
            int columns)
        {
            if (sale.SalesPayments.Count > 0)
            {
                AppendWrapped(text, "PAYMENTS", columns);

                foreach (SalesPayment payment in
                         sale.SalesPayments.OrderBy(row => row.Id))
                {
                    string label = FirstNonEmpty(payment.PaymentType, "Payment");

                    if (!string.IsNullOrWhiteSpace(payment.ReferenceNo))
                        label += $" ({payment.ReferenceNo.Trim()})";

                    AppendTwoColumns(
                        text,
                        label,
                        FormatMoney(payment.Amount, settings),
                        columns);
                }
            }
            else
            {
                AppendTwoColumns(
                    text,
                    FirstNonEmpty(sale.PaymentMethod, "Payment"),
                    FormatMoney(sale.NetTotal, settings),
                    columns);
            }

            if (sale.AmountTendered > 0m)
                AppendTwoColumns(text, "Tendered", FormatMoney(sale.AmountTendered, settings), columns);

            if (sale.BalanceReturned > 0m)
                AppendTwoColumns(text, "Change", FormatMoney(sale.BalanceReturned, settings), columns);

            AppendSeparator(text, columns);
        }

        private static void AppendTaxSummary(
            StringBuilder text,
            SalesHeader sale,
            StoreSettings settings,
            int columns,
            bool formalTaxInvoice)
        {
            AppendWrapped(text, "TAX SUMMARY", columns);

            if (!string.Equals(
                    sale.TaxSnapshotStatus,
                    TaxSnapshotStatuses.Complete,
                    StringComparison.Ordinal))
            {
                AppendWrapped(
                    text,
                    "Detailed VAT snapshots are unavailable for this legacy transaction.",
                    columns);
                AppendSeparator(text, columns);
                return;
            }

            AppendTwoColumns(
                text,
                "Standard taxable",
                FormatMoney(sale.StandardRatedAmount ?? 0m, settings),
                columns);
            AppendTwoColumns(
                text,
                "Output VAT",
                FormatMoney(sale.TotalVatAmount ?? 0m, settings),
                columns);
            AppendTwoColumns(
                text,
                "Zero Rated",
                FormatMoney(sale.ZeroRatedAmount ?? 0m, settings),
                columns);
            AppendTwoColumns(
                text,
                "Exempt",
                FormatMoney(sale.ExemptAmount ?? 0m, settings),
                columns);
            AppendTwoColumns(
                text,
                "Out of Scope",
                FormatMoney(sale.OutOfScopeAmount ?? 0m, settings),
                columns);

            if (formalTaxInvoice)
            {
                AppendTwoColumns(
                    text,
                    "Taxable total",
                    FormatMoney(sale.TaxableAmountTotal ?? 0m, settings),
                    columns);
            }

            AppendSeparator(text, columns);
        }

        private static void AppendStoreFooter(
            StringBuilder text,
            StoreSettings settings,
            int columns)
        {
            AppendBlank(text);
            AppendWrappedCentered(
                text,
                FirstNonEmpty(
                    settings.ReceiptFooter,
                    "Thank You! Come Again."),
                columns);
            AppendBlank(text);
        }

        private static void ValidateCommon(
            SalesHeader sale,
            StoreSettings settings)
        {
            if (sale == null)
                throw new ArgumentNullException(nameof(sale));

            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            if (sale.SalesLines == null || sale.SalesLines.Count == 0)
            {
                throw new InvalidOperationException(
                    "Sales document has no lines.");
            }
        }

        private static int GetColumns(int paperWidth)
        {
            return paperWidth == 58 ? 32 : 42;
        }

        private static string NormalizeCopyLabel(string? value)
        {
            return string.Equals(
                    value?.Trim(),
                    SalesDocumentCopyLabels.Reprint,
                    StringComparison.OrdinalIgnoreCase)
                ? SalesDocumentCopyLabels.Reprint
                : SalesDocumentCopyLabels.Original;
        }

        private static string FormatMoney(
            decimal value,
            StoreSettings settings)
        {
            string symbol = FirstNonEmpty(settings.CurrencySymbol, "Rs.");
            return $"{symbol} {value:0.00}";
        }

        private static void AppendLabel(
            StringBuilder text,
            string label,
            string? value,
            int columns)
        {
            string prefix = label.Trim() + ": ";
            string safeValue = Sanitize(value);
            int available = Math.Max(1, columns - prefix.Length);
            IReadOnlyList<string> lines = Wrap(safeValue, available);

            if (lines.Count == 0)
            {
                text.AppendLine(prefix.TrimEnd());
                return;
            }

            text.Append(prefix);
            text.AppendLine(lines[0]);

            for (int index = 1; index < lines.Count; index++)
            {
                text.Append(new string(' ', prefix.Length));
                text.AppendLine(lines[index]);
            }
        }

        private static void AppendWrappedLabel(
            StringBuilder text,
            string label,
            string? value,
            int columns)
        {
            AppendLabel(text, label, value, columns);
        }

        private static void AppendTwoColumns(
            StringBuilder text,
            string? left,
            string? right,
            int columns)
        {
            string safeLeft = Sanitize(left);
            string safeRight = Sanitize(right);

            if (safeRight.Length >= columns)
            {
                AppendWrapped(text, safeLeft, columns);
                AppendWrapped(text, safeRight, columns);
                return;
            }

            int availableLeft = Math.Max(0, columns - safeRight.Length - 1);
            safeLeft = Truncate(safeLeft, availableLeft);
            int spaces = Math.Max(1, columns - safeLeft.Length - safeRight.Length);

            text.Append(safeLeft);
            text.Append(new string(' ', spaces));
            text.AppendLine(safeRight);
        }

        private static void AppendSeparator(
            StringBuilder text,
            int columns)
        {
            text.AppendLine(new string('-', columns));
        }

        private static void AppendCentered(
            StringBuilder text,
            string? value,
            int columns)
        {
            string line = Truncate(Sanitize(value), columns);

            if (string.IsNullOrWhiteSpace(line))
                return;

            int leftPadding = Math.Max(0, (columns - line.Length) / 2);
            text.Append(new string(' ', leftPadding));
            text.AppendLine(line);
        }

        private static void AppendWrappedCentered(
            StringBuilder text,
            string? value,
            int columns)
        {
            foreach (string line in Wrap(Sanitize(value), columns))
                AppendCentered(text, line, columns);
        }

        private static void AppendWrapped(
            StringBuilder text,
            string? value,
            int columns)
        {
            foreach (string line in Wrap(Sanitize(value), columns))
                text.AppendLine(line);
        }

        private static void AppendBlank(StringBuilder text)
        {
            text.AppendLine();
        }

        private static IReadOnlyList<string> Wrap(
            string value,
            int width)
        {
            var output = new List<string>();
            string remaining = Sanitize(value);

            if (string.IsNullOrWhiteSpace(remaining) || width <= 0)
                return output;

            while (remaining.Length > width)
            {
                int split = remaining.LastIndexOf(' ', width);

                if (split <= 0)
                    split = width;

                output.Add(remaining.Substring(0, split).Trim());
                remaining = remaining.Substring(split).TrimStart();
            }

            if (!string.IsNullOrWhiteSpace(remaining))
                output.Add(remaining);

            return output;
        }

        private static string Sanitize(string? value)
        {
            return (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
        }

        private static string Truncate(
            string value,
            int maxLength)
        {
            if (maxLength <= 0)
                return string.Empty;

            return value.Length <= maxLength
                ? value
                : value.Substring(0, maxLength);
        }

        private static string JoinNonEmpty(
            string separator,
            params string?[] values)
        {
            return string.Join(
                separator,
                values
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim()));
        }

        private static string FirstNonEmpty(
            params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return string.Empty;
        }
    }
}
