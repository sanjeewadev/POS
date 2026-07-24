using System;
using System.Collections.Generic;
using System.Linq;
using POS.Core.Models;

namespace POS.Core.Utilities
{
    public static class ItemVariantIdentityPolicy
    {
        public static bool IsSkuAligned(
            string? itemCode,
            ItemVariant? variant)
        {
            if (variant == null)
                return false;

            string normalizedItemCode = NormalizeCode(itemCode);
            string normalizedSku = NormalizeCode(variant.SkuCode);

            if (string.IsNullOrWhiteSpace(normalizedItemCode) ||
                string.IsNullOrWhiteSpace(normalizedSku))
            {
                return false;
            }

            if (variant.IsStandardVariant)
            {
                return string.Equals(
                    normalizedSku,
                    normalizedItemCode,
                    StringComparison.OrdinalIgnoreCase);
            }

            return normalizedSku.StartsWith(
                normalizedItemCode + "-",
                StringComparison.OrdinalIgnoreCase);
        }

        public static IReadOnlyList<string> GetMisalignedSkus(
            string? itemCode,
            IEnumerable<ItemVariant>? variants)
        {
            return (variants ?? Enumerable.Empty<ItemVariant>())
                .Where(variant => !IsSkuAligned(itemCode, variant))
                .Select(variant => NormalizeCode(variant.SkuCode))
                .Where(sku => !string.IsNullOrWhiteSpace(sku))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(sku => sku, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string BuildMisalignmentMessage(
            string? itemCode,
            IEnumerable<ItemVariant>? variants)
        {
            string normalizedItemCode = NormalizeCode(itemCode);
            IReadOnlyList<string> misaligned =
                GetMisalignedSkus(normalizedItemCode, variants);

            if (misaligned.Count == 0)
                return string.Empty;

            string examples = string.Join(
                ", ",
                misaligned.Take(3));

            if (misaligned.Count > 3)
                examples += $" and {misaligned.Count - 3} more";

            return
                $"Generated variant SKUs do not match the current item code " +
                $"'{normalizedItemCode}'. Generate the matrix again before saving. " +
                $"Affected SKU(s): {examples}.";
        }

        private static string NormalizeCode(string? value) =>
            (value ?? string.Empty).Trim().ToUpperInvariant();
    }
}
