using System;

namespace POS.Core.Services.Pricing
{
    public static class SellingPriceCalculationMethods
    {
        public const string Manual = "Manual";
        public const string MarkupOnCost = "Markup on Cost";
        public const string TargetMargin = "Target Profit Margin";
        public const string DiscountFromRetail = "Discount from Retail";
    }

    public static class SellingPriceCostBasisCodes
    {
        public const string AverageCost = "Average Cost";
        public const string LastCost = "Last Cost";
    }

    public static class SellingPriceSuggestionCalculator
    {
        public static decimal FromMarkup(
            decimal cost,
            decimal markupPercent,
            decimal roundingIncrement = 0.01m)
        {
            RequirePositiveCost(cost);
            if (markupPercent < 0m)
                throw new ArgumentOutOfRangeException(nameof(markupPercent), "Markup cannot be negative.");

            decimal raw = cost * (1m + (markupPercent / 100m));
            return RoundToIncrement(raw, roundingIncrement);
        }

        public static decimal FromTargetMargin(
            decimal cost,
            decimal targetMarginPercent,
            decimal roundingIncrement = 0.01m)
        {
            RequirePositiveCost(cost);
            if (targetMarginPercent < 0m || targetMarginPercent >= 100m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetMarginPercent),
                    "Target margin must be at least zero and below 100 percent.");
            }

            decimal raw = cost / (1m - (targetMarginPercent / 100m));
            return RoundToIncrement(raw, roundingIncrement);
        }

        public static decimal FromRetailDiscount(
            decimal retailPrice,
            decimal discountPercent,
            decimal roundingIncrement = 0.01m)
        {
            if (retailPrice <= 0m)
                throw new ArgumentOutOfRangeException(nameof(retailPrice), "Retail price must be greater than zero.");
            if (discountPercent < 0m || discountPercent > 100m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(discountPercent),
                    "Retail discount must be between zero and 100 percent.");
            }

            decimal raw = retailPrice * (1m - (discountPercent / 100m));
            return RoundToIncrement(raw, roundingIncrement);
        }

        public static decimal ProfitPerUnit(decimal sellingPrice, decimal cost) =>
            Math.Round(sellingPrice - cost, 2, MidpointRounding.AwayFromZero);

        public static decimal MarginPercent(decimal sellingPrice, decimal cost)
        {
            if (sellingPrice <= 0m)
                return 0m;

            return Math.Round(
                ((sellingPrice - cost) / sellingPrice) * 100m,
                2,
                MidpointRounding.AwayFromZero);
        }

        public static decimal RoundToIncrement(decimal value, decimal increment)
        {
            if (increment <= 0m)
                increment = 0.01m;

            decimal roundedUnits = Math.Round(
                value / increment,
                0,
                MidpointRounding.AwayFromZero);

            return Math.Round(
                roundedUnits * increment,
                2,
                MidpointRounding.AwayFromZero);
        }

        private static void RequirePositiveCost(decimal cost)
        {
            if (cost <= 0m)
                throw new ArgumentOutOfRangeException(nameof(cost), "A positive cost is required for price calculation.");
        }
    }
}
