using System;

namespace POS.Core.Services.Pricing
{
    public static class GrnSellingPriceMethods
    {
        public const string KeepCurrent = "Keep Current";
        public const string SetExactPrice = "Set Exact Price";
        public const string MarkupFromLandedCost = "Markup % from Landed Cost";
        public const string ChangeCurrentByPercent = "Change Current Price by %";
    }

    public static class GrnSellingPriceRoundingModes
    {
        public const string None = "No Rounding";
        public const string NearestOne = "Nearest Rs. 1";
        public const string NearestFive = "Nearest Rs. 5";
        public const string NearestTen = "Nearest Rs. 10";
    }

    /// <summary>
    /// Calculates final VAT-inclusive selling prices proposed from a GRN.
    /// Landed cost is always tax-exclusive. Exact and current-price methods
    /// accept/return final VAT-inclusive selling prices.
    /// </summary>
    public sealed class GrnSellingPriceCalculator
    {
        public decimal Calculate(
            decimal currentVatInclusivePrice,
            decimal landedCostExcludingVat,
            decimal vatRatePercent,
            string method,
            decimal value,
            string roundingMode)
        {
            currentVatInclusivePrice = RoundMoney(currentVatInclusivePrice);
            landedCostExcludingVat = RoundMoney(landedCostExcludingVat);
            vatRatePercent = Math.Round(vatRatePercent, 4, MidpointRounding.AwayFromZero);
            method = Normalize(method);

            if (vatRatePercent < 0m || vatRatePercent > 100m)
                throw new InvalidOperationException("VAT rate must be between 0 and 100.");

            decimal calculated;

            if (method.Equals(GrnSellingPriceMethods.KeepCurrent, StringComparison.OrdinalIgnoreCase))
            {
                return currentVatInclusivePrice;
            }

            if (method.Equals(GrnSellingPriceMethods.SetExactPrice, StringComparison.OrdinalIgnoreCase))
            {
                if (value <= 0m)
                    throw new InvalidOperationException("Exact selling price must be greater than zero.");

                calculated = value;
            }
            else if (method.Equals(GrnSellingPriceMethods.MarkupFromLandedCost, StringComparison.OrdinalIgnoreCase))
            {
                if (landedCostExcludingVat <= 0m)
                    throw new InvalidOperationException("Landed cost must be greater than zero for markup pricing.");

                if (value < -100m)
                    throw new InvalidOperationException("Markup percentage cannot be less than -100%.");

                decimal priceBeforeVat = landedCostExcludingVat * (1m + value / 100m);
                decimal vatInclusivePrice = priceBeforeVat * (1m + vatRatePercent / 100m);
                calculated = vatInclusivePrice;
            }
            else if (method.Equals(GrnSellingPriceMethods.ChangeCurrentByPercent, StringComparison.OrdinalIgnoreCase))
            {
                if (currentVatInclusivePrice <= 0m)
                    throw new InvalidOperationException("Current selling price must be greater than zero for percentage change.");

                if (value <= -100m)
                    throw new InvalidOperationException("Price change percentage must be greater than -100%.");

                calculated = currentVatInclusivePrice * (1m + value / 100m);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported selling-price method '{method}'.");
            }

            calculated = ApplyRounding(calculated, roundingMode);

            if (calculated <= 0m)
                throw new InvalidOperationException("Calculated selling price must be greater than zero.");

            return RoundMoney(calculated);
        }

        public static decimal ApplyRounding(decimal value, string roundingMode)
        {
            decimal increment = ResolveIncrement(roundingMode);

            if (increment <= 0m)
                return RoundMoney(value);

            decimal units = value / increment;
            decimal roundedUnits = Math.Round(units, 0, MidpointRounding.AwayFromZero);
            return RoundMoney(roundedUnits * increment);
        }

        private static decimal ResolveIncrement(string? roundingMode)
        {
            string mode = Normalize(roundingMode);

            if (mode.Equals(GrnSellingPriceRoundingModes.NearestOne, StringComparison.OrdinalIgnoreCase))
                return 1m;

            if (mode.Equals(GrnSellingPriceRoundingModes.NearestFive, StringComparison.OrdinalIgnoreCase))
                return 5m;

            if (mode.Equals(GrnSellingPriceRoundingModes.NearestTen, StringComparison.OrdinalIgnoreCase))
                return 10m;

            return 0m;
        }

        private static decimal RoundMoney(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private static string Normalize(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
