using System;

namespace POS.Core.Configuration
{
    public static class FreeIssueTypeCodes
    {
        public const string ShopCost = "ShopCost";
        public const string SupplierClaim = "SupplierClaim";

        public static string Normalize(string? value) =>
            string.Equals(
                (value ?? string.Empty).Trim(),
                SupplierClaim,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                (value ?? string.Empty).Trim(),
                "Supplier Recoverable",
                StringComparison.OrdinalIgnoreCase)
                ? SupplierClaim
                : ShopCost;
    }

    public static class SupplierClaimStatusCodes
    {
        public const string Draft = "Draft";
        public const string Submitted = "Submitted";
        public const string Settled = "Settled";
        public const string Rejected = "Rejected";

        public static bool IsApprovedStatus(string? value) =>
            string.Equals(value, Draft, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, Submitted, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, Settled, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, Rejected, StringComparison.OrdinalIgnoreCase);
    }

    public static class FreeIssueSnapshotStatusCodes
    {
        public const string Complete = "Complete";
        public const string LegacyUnknown = "LegacyUnknown";
    }

    public static class FreeIssueApprovalRoleCodes
    {
        public const string Manager = "Manager";
        public const string Administrator = "Admin";
    }
}

namespace POS.Core.Configuration
{
    public readonly record struct FreeIssueQuantitySplit(
        decimal PaidQuantity,
        decimal FreeQuantity);

    public static class FreeIssueQuantitySplitter
    {
        public static FreeIssueQuantitySplit Split(
            decimal totalQuantity,
            decimal requestedFreeQuantity)
        {
            decimal total = Math.Round(totalQuantity, 3);
            decimal free = Math.Round(requestedFreeQuantity, 3);

            if (total <= 0m)
                throw new ArgumentOutOfRangeException(nameof(totalQuantity), "Total quantity must be greater than zero.");

            if (free <= 0m || free > total)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requestedFreeQuantity),
                    "Free quantity must be greater than zero and cannot exceed the line quantity.");
            }

            return new FreeIssueQuantitySplit(
                PaidQuantity: Math.Round(total - free, 3),
                FreeQuantity: free);
        }
    }
}
