using System;
using System.Globalization;
using System.Linq;
using System.Text;
using POS.Core.Configuration;
using POS.Core.Models.DTOs;

namespace POS.Core.Services.Documents
{
    public sealed class SupplierDebitNoteTextFormatter
    {
        public string Format(SupplierDebitNoteDto document, int paperWidth = 80)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            int columns = paperWidth <= 58 ? 32 : 48;
            var text = new StringBuilder();

            AppendCentered(text, FirstNonEmpty(document.StoreName, "STORE"), columns);
            AppendWrappedCentered(text, document.StoreAddress, columns);

            if (!string.IsNullOrWhiteSpace(document.StorePhone))
                AppendCentered(text, $"Tel: {document.StorePhone.Trim()}", columns);

            if (!string.IsNullOrWhiteSpace(document.StoreTin))
                AppendCentered(text, $"TIN: {document.StoreTin.Trim()}", columns);

            if (!string.IsNullOrWhiteSpace(document.StoreVatNo))
                AppendCentered(text, $"VAT No: {document.StoreVatNo.Trim()}", columns);

            AppendBlank(text);
            AppendCentered(text, "SUPPLIER DEBIT NOTE", columns);
            AppendSeparator(text, columns);
            AppendLabel(text, "Debit Note", document.DebitNoteNumber, columns);
            AppendLabel(text, "Return Date", document.ReturnDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), columns);
            AppendLabel(text, "GRN", document.GrnNumber, columns);
            AppendLabel(text, "Supplier Inv", document.OriginalSupplierInvoiceNo, columns);
            AppendLabel(text, "Supplier", JoinNonEmpty(" - ", document.SupplierCode, document.SupplierName), columns);

            if (!string.IsNullOrWhiteSpace(document.SupplierVatNo))
                AppendLabel(text, "Supplier VAT", document.SupplierVatNo, columns);

            AppendWrappedLabel(text, "Address", document.SupplierAddress, columns);
            AppendLabel(text, "Authorized", document.AuthorizedBy, columns);
            AppendSeparator(text, columns);

            int lineNo = 1;

            foreach (SupplierDebitNoteLineDto line in document.Lines)
            {
                AppendWrapped(text, $"{lineNo}. {FirstNonEmpty(line.Description, line.ItemCode, "Item")}", columns);
                AppendTwoColumns(text, $"Qty {line.ReturnQuantity:0.###}", $"Rs. {Money(line.SupplierCredit)}", columns);
                AppendWrapped(text, $"Batch: {FirstNonEmpty(line.BatchNo, "-")} | Landed Cost: Rs. {Money(line.HistoricalLandedCost)}", columns);

                if (string.Equals(line.TaxSnapshotStatus, TaxSnapshotStatuses.Complete, StringComparison.Ordinal))
                {
                    string taxLabel = FirstNonEmpty(line.TaxName, line.TaxCategoryCode, "Tax");
                    string rate = line.TaxRatePercent.HasValue ? $" {line.TaxRatePercent.Value:0.####}%" : string.Empty;
                    AppendWrapped(text, $"{taxLabel}{rate} | Taxable Rs. {Money(line.TaxableAmount ?? 0m)} | VAT Rs. {Money(line.VatAmount ?? 0m)}", columns);
                }
                else
                {
                    AppendWrapped(text, "Tax snapshot unavailable (legacy GRN).", columns);
                }

                AppendWrapped(text, $"Reason: {line.ReasonCode}", columns);

                if (!string.IsNullOrWhiteSpace(line.Remarks))
                    AppendWrapped(text, $"Note: {line.Remarks}", columns);

                AppendBlank(text);
                lineNo++;
            }

            AppendSeparator(text, columns);
            AppendTwoColumns(text, "SUPPLIER CREDIT", $"Rs. {Money(document.NetCredit)}", columns);

            if (string.Equals(document.TaxSnapshotStatus, TaxSnapshotStatuses.Complete, StringComparison.Ordinal))
            {
                AppendTwoColumns(text, "Taxable", $"Rs. {Money(document.TaxableAmountTotal ?? 0m)}", columns);
                AppendTwoColumns(text, "VAT Reversed", $"Rs. {Money(document.TotalVatAmount ?? 0m)}", columns);
                AppendTwoColumns(text, "Standard VAT", $"Rs. {Money(document.StandardRatedAmount ?? 0m)}", columns);
                AppendTwoColumns(text, "Zero Rated", $"Rs. {Money(document.ZeroRatedAmount ?? 0m)}", columns);
                AppendTwoColumns(text, "Exempt", $"Rs. {Money(document.ExemptAmount ?? 0m)}", columns);
                AppendTwoColumns(text, "Out of Scope", $"Rs. {Money(document.OutOfScopeAmount ?? 0m)}", columns);
            }
            else
            {
                AppendWrapped(text, "VAT detail was not invented because the source GRN has a legacy/unknown tax snapshot.", columns);
            }

            if (!string.IsNullOrWhiteSpace(document.Remarks))
            {
                AppendSeparator(text, columns);
                AppendWrappedLabel(text, "Remarks", document.Remarks, columns);
            }

            AppendSeparator(text, columns);
            AppendCentered(text, "This document reverses the original GRN product value.", columns);
            AppendCentered(text, "Freight and restocking fees are not credited.", columns);

            return text.ToString();
        }

        private static void AppendLabel(StringBuilder text, string label, string? value, int columns)
        {
            AppendTwoColumns(text, label + ":", FirstNonEmpty(value, "-"), columns);
        }

        private static void AppendWrappedLabel(StringBuilder text, string label, string? value, int columns)
        {
            string normalized = FirstNonEmpty(value, "-");
            AppendWrapped(text, $"{label}: {normalized}", columns);
        }

        private static void AppendTwoColumns(StringBuilder text, string left, string right, int columns)
        {
            left = (left ?? string.Empty).Trim();
            right = (right ?? string.Empty).Trim();

            if (left.Length + right.Length + 1 > columns)
            {
                AppendWrapped(text, left, columns);
                AppendWrapped(text, right, columns);
                return;
            }

            text.Append(left);
            text.Append(' ', Math.Max(1, columns - left.Length - right.Length));
            text.AppendLine(right);
        }

        private static void AppendWrapped(StringBuilder text, string? value, int columns)
        {
            string normalized = (value ?? string.Empty).Trim();

            if (normalized.Length == 0)
                return;

            while (normalized.Length > columns)
            {
                int split = normalized.LastIndexOf(' ', columns);
                if (split <= 0)
                    split = columns;

                text.AppendLine(normalized.Substring(0, split).TrimEnd());
                normalized = normalized.Substring(split).TrimStart();
            }

            if (normalized.Length > 0)
                text.AppendLine(normalized);
        }

        private static void AppendWrappedCentered(StringBuilder text, string? value, int columns)
        {
            var buffer = new StringBuilder();
            AppendWrapped(buffer, value, columns);

            foreach (string line in buffer.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                AppendCentered(text, line, columns);
        }

        private static void AppendCentered(StringBuilder text, string? value, int columns)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.Length > columns)
                normalized = normalized.Substring(0, columns);

            int leftPadding = Math.Max(0, (columns - normalized.Length) / 2);
            text.Append(' ', leftPadding);
            text.AppendLine(normalized);
        }

        private static void AppendSeparator(StringBuilder text, int columns)
        {
            text.AppendLine(new string('-', columns));
        }

        private static void AppendBlank(StringBuilder text)
        {
            text.AppendLine();
        }

        private static string Money(decimal value)
        {
            return value.ToString("N2", CultureInfo.InvariantCulture);
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
        }

        private static string JoinNonEmpty(string separator, params string?[] values)
        {
            return string.Join(separator, values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));
        }
    }
}
