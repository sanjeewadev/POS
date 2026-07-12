using System;

namespace POS.Core.Configuration
{
    public static class ShiftStatusCodes
    {
        public const string Open = "Open";
        public const string Closing = "Closing";
        public const string Closed = "Closed";
    }

    public static class CashMovementTypeCodes
    {
        public const string PaidIn = "Paid In";
        public const string PaidOut = "Paid Out";
    }

    public static class CashMovementReasonCodes
    {
        public const string FloatIn = "Float In / Opening Adjustment";
        public const string FloatOut = "Float Out / Safe Drop";
        public const string StoreExpense = "Petty Cash / Store Expense";
        public const string ChangeFundAdjustment = "Change Fund Adjustment";
        public const string CustomerRefund = "Customer Refund";
        public const string Other = "Other";

        public static bool IsFloatIn(string? value) =>
            EqualsCode(value, FloatIn) ||
            EqualsCode(value, "Opening Float");

        public static bool IsFloatOut(string? value) =>
            EqualsCode(value, FloatOut);

        public static bool IsCustomerRefund(string? value) =>
            EqualsCode(value, CustomerRefund);

        private static bool EqualsCode(string? left, string right) =>
            string.Equals(
                (left ?? string.Empty).Trim(),
                right,
                StringComparison.OrdinalIgnoreCase);
    }

    public static class CashDrawerEventTypeCodes
    {
        public const string CashSale = "Cash Sale";
        public const string PaidIn = "Paid In";
        public const string PaidOut = "Paid Out";
        public const string FloatIn = "Float In";
        public const string FloatOut = "Float Out";
        public const string NoSale = "No Sale";
        public const string ManualOpen = "Manual Open";
    }

    public static class PaymentTypeCodes
    {
        public const string Cash = "Cash";
        public const string Card = "Card";
        public const string Cheque = "Cheque";
        public const string GiftVoucher = "GiftVoucher";
        public const string CustomerCredit = "CustomerCredit";
    }

    public static class ShiftDocumentSequenceCodes
    {
        public const string PaidIn = "PAIDIN";
        public const string PaidOut = "PAIDOUT";
        public const string ZReport = "ZREPORT";
    }
}
