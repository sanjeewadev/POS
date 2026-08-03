using System;

namespace POS.Core.Configuration
{
    public static class GrnSellingPriceActionCodes
    {
        public const string UseCurrentMasterPrice = "UseCurrentMasterPrice";
        public const string UpdateMasterPrice = "UpdateMasterPrice";
        public const string SetBatchPriceOverride = "SetBatchPriceOverride";

        public static bool IsValid(string? value)
        {
            return string.Equals(value, UseCurrentMasterPrice, StringComparison.Ordinal) ||
                   string.Equals(value, UpdateMasterPrice, StringComparison.Ordinal) ||
                   string.Equals(value, SetBatchPriceOverride, StringComparison.Ordinal);
        }

        public static string Normalize(string? value, bool legacyUpdateSellingPrices = false)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (IsValid(normalized))
                return normalized;

            return legacyUpdateSellingPrices
                ? UpdateMasterPrice
                : UseCurrentMasterPrice;
        }

        public static string ToDisplayText(string? value)
        {
            return Normalize(value) switch
            {
                UpdateMasterPrice => "Update Master Price",
                SetBatchPriceOverride => "Set Price for This Batch Only",
                _ => "Keep Current Price"
            };
        }
    }
}
