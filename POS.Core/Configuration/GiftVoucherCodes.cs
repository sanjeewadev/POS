using System;

namespace POS.Core.Configuration
{
    public static class GiftVoucherStatusCodes
    {
        public const string Created = "Created";
        public const string Active = "Active";
        public const string Redeemed = "Redeemed";
        public const string Expired = "Expired";
        public const string Blocked = "Blocked";
        public const string Voided = "Voided";
        public const string LegacyCancelled = "Cancelled";

        public static bool IsTerminal(string? status) =>
            Equals(status, Redeemed) ||
            Equals(status, Voided) ||
            Equals(status, LegacyCancelled);

        public static bool IsVoided(string? status) =>
            Equals(status, Voided) || Equals(status, LegacyCancelled);

        public static bool Equals(string? value, string expected) =>
            string.Equals(value?.Trim(), expected, StringComparison.OrdinalIgnoreCase);
    }

    public static class GiftVoucherTransactionCodes
    {
        public const string Created = "Created";
        public const string Printed = "Printed";
        public const string Reprinted = "Reprinted";
        public const string Activated = "Activated";
        public const string Redeemed = "Redeemed";
        public const string Blocked = "Blocked";
        public const string Unblocked = "Unblocked";
        public const string Voided = "Voided";
        public const string ReturnVoucherIssued = "ReturnVoucherIssued";
    }
}
