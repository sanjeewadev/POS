using System;
using System.Collections.Generic;
using POS.Core.Models.DTOs;

namespace POS.Core.Services.Documents
{
    public sealed class ShiftReportTextFormatter
    {
        public string FormatXReport(ShiftCashSummaryDto summary, int paperWidth)
        {
            return Format(summary, paperWidth, "X REPORT", false);
        }

        public string FormatZReport(ShiftCashSummaryDto summary, int paperWidth)
        {
            return Format(summary, paperWidth, "Z REPORT", true);
        }

        private static string Format(
            ShiftCashSummaryDto summary,
            int paperWidth,
            string title,
            bool includeClose)
        {
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));

            int columns = paperWidth == 58 ? 32 : 42;
            var lines = new List<string>
            {
                Center(title, columns),
                new string('-', columns),
                Pair("Terminal", summary.TerminalNo, columns),
                Pair("Cashier", summary.CashierName, columns),
                Pair("Shift", summary.ShiftSessionId.ToString(), columns),
                Pair("Opened", summary.OpenedAt.ToString("yyyy-MM-dd HH:mm"), columns)
            };

            if (includeClose)
            {
                lines.Add(Pair("Z No", summary.ZReportNo, columns));
                lines.Add(Pair("Closed", summary.ClosedAt?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty, columns));
            }

            lines.Add(new string('-', columns));
            lines.Add(Pair("Completed sales", summary.CompletedSaleCount.ToString(), columns));
            lines.Add(Pair("Customer returns", summary.CustomerReturnCount.ToString(), columns));
            lines.Add(Pair("Gross sales", Money(summary.GrossSales), columns));
            lines.Add(Pair("Discounts", Money(summary.TotalDiscount), columns));
            lines.Add(Pair("Net sales", Money(summary.NetSales), columns));
            lines.Add(Pair("VAT", Money(summary.VatTotal), columns));
            lines.Add(new string('-', columns));
            lines.Add(Pair("Cash", Money(summary.CashTenderTotal), columns));
            lines.Add(Pair("Card", Money(summary.CardTenderTotal), columns));
            lines.Add(Pair("Cheque", Money(summary.ChequeTenderTotal), columns));
            lines.Add(Pair("Gift voucher", Money(summary.GiftVoucherTenderTotal), columns));
            lines.Add(Pair("Customer credit", Money(summary.CustomerCreditTenderTotal), columns));
            lines.Add(Pair("Other tenders", Money(summary.OtherTenderTotal), columns));
            lines.Add(new string('-', columns));
            lines.Add(Pair("Opening cash", Money(summary.OpeningCash), columns));
            lines.Add(Pair("Paid in", Money(summary.PaidInTotal), columns));
            lines.Add(Pair("Float in", Money(summary.FloatInTotal), columns));
            lines.Add(Pair("Paid out", Money(summary.PaidOutTotal), columns));
            lines.Add(Pair("Float out", Money(summary.FloatOutTotal), columns));
            lines.Add(Pair("Cash refunds", Money(summary.CashRefundTotal), columns));
            lines.Add(Pair("EXPECTED CASH", Money(summary.ExpectedCash), columns));

            if (includeClose)
            {
                lines.Add(Pair("COUNTED CASH", Money(summary.CountedCash), columns));
                lines.Add(Pair("VARIANCE", Money(summary.Variance), columns));
                if (!string.IsNullOrWhiteSpace(summary.AuthorizedBy))
                    lines.Add(Pair("Authorized by", summary.AuthorizedBy, columns));
                if (!string.IsNullOrWhiteSpace(summary.VarianceNote))
                    lines.Add("Note: " + summary.VarianceNote);
            }

            lines.Add(new string('-', columns));
            lines.Add(Center(includeClose ? "SHIFT CLOSED" : "SHIFT REMAINS OPEN", columns));
            return string.Join(Environment.NewLine, lines) + Environment.NewLine;
        }

        private static string Money(decimal value) => $"Rs. {value:N2}";

        private static string Pair(string left, string right, int width)
        {
            left = (left ?? string.Empty).Trim();
            right = (right ?? string.Empty).Trim();
            int gap = Math.Max(1, width - left.Length - right.Length);
            if (left.Length + right.Length + gap > width)
            {
                int maxLeft = Math.Max(1, width - right.Length - 1);
                left = left.Length <= maxLeft ? left : left[..maxLeft];
                gap = Math.Max(1, width - left.Length - right.Length);
            }
            return left + new string(' ', gap) + right;
        }

        private static string Center(string value, int width)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length >= width)
                return value[..width];
            int left = (width - value.Length) / 2;
            return new string(' ', left) + value;
        }
    }
}
