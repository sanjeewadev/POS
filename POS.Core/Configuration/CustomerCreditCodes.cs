using System;

namespace POS.Core.Configuration
{
    public static class CustomerCreditCodes
    {
        public const string PaymentType = "CustomerCredit";
        public const string PaymentDisplayName = "Customer Credit";

        public const string CreditSale = "Credit Sale";
        public const string PaymentReceived = "Payment Received";
        public const string ReturnCredit = "Return Credit";

        public const string Open = "Open";
        public const string PartPaid = "Part Paid";
        public const string Paid = "Paid";
        public const string Overdue = "Overdue";

        public const string ReceiptSequenceType = "CPR";
        public const string ReceiptPrefix = "CPR-";
        public const int ReceiptPaddingLength = 6;

        public static bool IsCustomerCreditPayment(string? value) =>
            string.Equals(value, PaymentType, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, PaymentDisplayName, StringComparison.OrdinalIgnoreCase);
    }
}
