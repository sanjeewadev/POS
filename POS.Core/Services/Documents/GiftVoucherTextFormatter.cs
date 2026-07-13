using System;
using System.Linq;
using System.Text;
using POS.Core.Configuration;
using POS.Core.Models;

namespace POS.Core.Services.Documents
{
    public sealed class GiftVoucherTextFormatter
    {
        public string Format(
            GiftVoucher voucher,
            StoreSettings settings,
            bool isReprint)
        {
            if (voucher == null)
                throw new ArgumentNullException(nameof(voucher));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            const int columns = 42;
            var text = new StringBuilder();
            AppendCentered(text, FirstNonEmpty(settings.StoreName, settings.LegalName, "My Store"), columns);
            AppendCentered(text, "ONE-TIME GIFT VOUCHER", columns);
            AppendCentered(text, isReprint ? "REPRINT" : "ORIGINAL", columns);
            AppendSeparator(text, columns);
            AppendLabel(text, "Voucher", voucher.VoucherNo, columns);
            AppendLabel(text, "Barcode", voucher.Barcode, columns);
            AppendLabel(text, "Value", $"{FirstNonEmpty(settings.CurrencySymbol, "Rs.")} {voucher.VoucherAmount:N2}", columns);
            AppendLabel(text, "Status", voucher.DisplayStatus, columns);
            AppendLabel(text, "Created", voucher.CreatedAt.ToString("yyyy-MM-dd HH:mm"), columns);
            AppendLabel(text, "Expiry", voucher.ExpiryDate?.ToString("yyyy-MM-dd") ?? "No expiry", columns);
            if (!string.IsNullOrWhiteSpace(voucher.BatchNo))
                AppendLabel(text, "Batch", voucher.BatchNo, columns);
            AppendSeparator(text, columns);

            if (GiftVoucherStatusCodes.Equals(voucher.Status, GiftVoucherStatusCodes.Created))
                AppendWrapped(text, "NOT ACTIVE UNTIL SOLD THROUGH THE POS.", columns);
            else if (GiftVoucherStatusCodes.Equals(voucher.Status, GiftVoucherStatusCodes.Active))
                AppendWrapped(text, "VALID FOR ONE REDEMPTION ONLY.", columns);
            else if (GiftVoucherStatusCodes.Equals(voucher.Status, GiftVoucherStatusCodes.Redeemed))
                AppendWrapped(text, "THIS VOUCHER HAS ALREADY BEEN REDEEMED.", columns);
            else
                AppendWrapped(text, $"THIS VOUCHER IS NOT USABLE. STATUS: {voucher.DisplayStatus}.", columns);

            AppendWrapped(text, "Unused value is forfeited when the voucher is redeemed. Manager approval is required when forfeiture occurs.", columns);
            AppendWrapped(text, "Not exchangeable for cash and cannot be used to purchase another gift voucher.", columns);
            AppendWrapped(text, "Keep this voucher number secure. The store is not responsible for lost vouchers.", columns);
            AppendSeparator(text, columns);
            AppendCentered(text, FirstNonEmpty(settings.ReceiptFooter, "Thank you"), columns);
            return text.ToString();
        }

        private static void AppendLabel(StringBuilder text, string label, string value, int columns) =>
            AppendWrapped(text, $"{label}: {FirstNonEmpty(value, "-")}", columns);

        private static void AppendSeparator(StringBuilder text, int columns) =>
            text.AppendLine(new string('-', columns));

        private static void AppendCentered(StringBuilder text, string value, int columns)
        {
            string safe = FirstNonEmpty(value);
            if (safe.Length > columns)
            {
                AppendWrapped(text, safe, columns);
                return;
            }

            text.AppendLine(new string(' ', Math.Max(0, (columns - safe.Length) / 2)) + safe);
        }

        private static void AppendWrapped(StringBuilder text, string value, int columns)
        {
            string remaining = FirstNonEmpty(value);
            while (remaining.Length > columns)
            {
                int split = remaining.LastIndexOf(' ', columns);
                if (split <= 0)
                    split = columns;
                text.AppendLine(remaining[..split].Trim());
                remaining = remaining[split..].Trim();
            }

            if (remaining.Length > 0)
                text.AppendLine(remaining);
        }

        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }
}
