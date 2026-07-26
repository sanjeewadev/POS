using System;
using POS.Core.Configuration;
using POS.Core.Models;

namespace POS.Core.Services.Pricing
{
    public static class SellingPriceSourceCodes
    {
        public const string Master = "Master";
        public const string BatchOverride = "BatchOverride";
        public const string LegacyUnknown = "LegacyUnknown";
    }

    public readonly record struct EffectiveSellingPrice(
        decimal RetailPrice,
        decimal WholesalePrice,
        string PriceSource)
    {
        public decimal ResolveForMode(bool isWholesaleMode)
        {
            return isWholesaleMode
                ? WholesalePrice
                : RetailPrice;
        }
    }

    public static class EffectiveSellingPriceResolver
    {
        private const string GeneralBatchNo = "GENERAL";

        public static EffectiveSellingPrice Resolve(
            ItemVariant variant,
            ItemBatch? batch = null)
        {
            if (variant == null)
                throw new ArgumentNullException(nameof(variant));

            ItemParent? parent = variant.ItemParent;

            return Resolve(
                parent?.ItemType ?? ItemTypeCodes.StockItem,
                parent?.HasBatchTracking == true,
                batch?.BatchNo,
                batch?.IsDeactivated == true,
                batch != null && batch.ItemVariantId == variant.Id && batch.HasSellingPriceOverride,
                batch?.RetailPrice ?? 0m,
                batch?.WholesalePrice ?? 0m,
                variant.RetailPrice,
                variant.WholesalePrice);
        }

        public static EffectiveSellingPrice Resolve(
            string itemType,
            bool hasBatchTracking,
            string? batchNo,
            bool isBatchDeactivated,
            bool hasSellingPriceOverride,
            decimal batchRetailPrice,
            decimal batchWholesalePrice,
            decimal masterRetailPrice,
            decimal masterWholesalePrice)
        {
            bool useBatchOverride =
                hasSellingPriceOverride &&
                !isBatchDeactivated &&
                string.Equals(
                    itemType,
                    ItemTypeCodes.StockItem,
                    StringComparison.Ordinal) &&
                hasBatchTracking &&
                !IsGeneralBatch(batchNo);

            decimal retailPrice = useBatchOverride
                ? RoundMoney(batchRetailPrice)
                : RoundMoney(masterRetailPrice);

            decimal wholesalePrice = useBatchOverride
                ? RoundMoney(batchWholesalePrice)
                : ResolveMasterWholesalePrice(
                    masterRetailPrice,
                    masterWholesalePrice);

            return new EffectiveSellingPrice(
                retailPrice,
                wholesalePrice,
                useBatchOverride
                    ? SellingPriceSourceCodes.BatchOverride
                    : SellingPriceSourceCodes.Master);
        }

        public static decimal ResolveForMode(
            ItemVariant variant,
            ItemBatch? batch,
            bool isWholesaleMode)
        {
            return Resolve(variant, batch)
                .ResolveForMode(isWholesaleMode);
        }

        public static bool IsOverrideEligible(
            ItemVariant variant,
            ItemBatch? batch)
        {
            if (variant == null || batch == null)
                return false;

            ItemParent? parent = variant.ItemParent;
            if (parent == null)
                return false;

            return
                batch.ItemVariantId == variant.Id &&
                !batch.IsDeactivated &&
                string.Equals(
                    parent.ItemType,
                    ItemTypeCodes.StockItem,
                    StringComparison.Ordinal) &&
                parent.HasBatchTracking &&
                !IsGeneralBatch(batch.BatchNo);
        }

        public static void ValidateOverride(
            ItemVariant variant,
            ItemBatch batch,
            decimal retailPrice,
            decimal wholesalePrice)
        {
            if (variant == null)
                throw new ArgumentNullException(nameof(variant));

            if (batch == null)
                throw new ArgumentNullException(nameof(batch));

            if (!IsOverrideEligible(variant, batch))
            {
                throw new InvalidOperationException(
                    "Only an active physical batch of a batch-tracked Stock Item can use a selling-price override.");
            }

            ValidateOverride(
                retailPrice,
                wholesalePrice,
                variant.MinimumPrice,
                variant.MaximumPrice);
        }

        public static void ValidateOverride(
            decimal retailPrice,
            decimal wholesalePrice,
            decimal minimumPrice,
            decimal maximumPrice)
        {
            retailPrice = RoundMoney(retailPrice);
            wholesalePrice = RoundMoney(wholesalePrice);
            minimumPrice = RoundMoney(minimumPrice);
            maximumPrice = RoundMoney(maximumPrice);

            if (retailPrice <= 0m)
                throw new InvalidOperationException("Batch override Retail price must be greater than zero.");

            if (wholesalePrice <= 0m)
                throw new InvalidOperationException("Batch override Wholesale price must be greater than zero.");

            if (minimumPrice > 0m)
            {
                if (retailPrice < minimumPrice)
                    throw new InvalidOperationException("Batch override Retail price cannot be below the variant Minimum price.");

                if (wholesalePrice < minimumPrice)
                    throw new InvalidOperationException("Batch override Wholesale price cannot be below the variant Minimum price.");
            }

            if (maximumPrice > 0m)
            {
                if (retailPrice > maximumPrice)
                    throw new InvalidOperationException("Batch override Retail price cannot be above the variant Maximum price.");

                if (wholesalePrice > maximumPrice)
                    throw new InvalidOperationException("Batch override Wholesale price cannot be above the variant Maximum price.");
            }
        }

        public static void ValidateActiveOverridesAgainstMasterBounds(
            ItemVariant variant,
            System.Collections.Generic.IEnumerable<ItemBatch> batches,
            decimal minimumPrice,
            decimal maximumPrice)
        {
            if (variant == null)
                throw new ArgumentNullException(nameof(variant));

            if (batches == null)
                throw new ArgumentNullException(nameof(batches));

            minimumPrice = RoundMoney(minimumPrice);
            maximumPrice = RoundMoney(maximumPrice);

            var conflicts = new System.Collections.Generic.List<string>();

            foreach (ItemBatch batch in batches)
            {
                if (!batch.HasSellingPriceOverride ||
                    !IsOverrideEligible(variant, batch))
                {
                    continue;
                }

                decimal retailPrice = RoundMoney(batch.RetailPrice);
                decimal wholesalePrice = RoundMoney(batch.WholesalePrice);

                bool conflictsWithMinimum =
                    minimumPrice > 0m &&
                    (retailPrice < minimumPrice || wholesalePrice < minimumPrice);

                bool conflictsWithMaximum =
                    maximumPrice > 0m &&
                    (retailPrice > maximumPrice || wholesalePrice > maximumPrice);

                if (conflictsWithMinimum || conflictsWithMaximum)
                {
                    string batchNo = string.IsNullOrWhiteSpace(batch.BatchNo)
                        ? $"ID {batch.Id}"
                        : batch.BatchNo.Trim();

                    conflicts.Add(batchNo);
                }
            }

            if (conflicts.Count > 0)
            {
                throw new InvalidOperationException(
                    "The new Minimum or Maximum price conflicts with active batch overrides: " +
                    string.Join(", ", conflicts) +
                    ". Update or remove those overrides before changing the master boundary.");
            }
        }

        public static void SynchronizeMasterMirror(
            ItemVariant variant,
            ItemBatch batch,
            DateTime updatedAt)
        {
            if (variant == null)
                throw new ArgumentNullException(nameof(variant));

            if (batch == null)
                throw new ArgumentNullException(nameof(batch));

            if (batch.HasSellingPriceOverride &&
                IsOverrideEligible(variant, batch))
            {
                return;
            }

            batch.HasSellingPriceOverride = false;
            batch.RetailPrice = RoundMoney(variant.RetailPrice);
            batch.WholesalePrice = ResolveMasterWholesalePrice(
                variant.RetailPrice,
                variant.WholesalePrice);
            batch.UpdatedAt = updatedAt;
        }

        private static decimal ResolveMasterWholesalePrice(
            decimal masterRetailPrice,
            decimal masterWholesalePrice)
        {
            decimal wholesalePrice = RoundMoney(masterWholesalePrice);
            if (wholesalePrice > 0m)
                return wholesalePrice;

            return RoundMoney(masterRetailPrice);
        }

        private static decimal RoundMoney(decimal value)
        {
            return Math.Round(value, 2);
        }

        private static bool IsGeneralBatch(string? batchNo)
        {
            return string.Equals(
                (batchNo ?? string.Empty).Trim(),
                GeneralBatchNo,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
