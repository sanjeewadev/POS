using System;
using System.Globalization;

namespace POS.Core.Utilities
{
    /// <summary>
    /// Formats user-facing quantities without unnecessary trailing zeroes while
    /// preserving up to three decimal places for fractional units.
    /// </summary>
    public static class QuantityDisplayFormatter
    {
        public const string DisplayFormat = "#,##0.###";
        public const string InputFormat = "0.###";
        public const string InvariantDataFormat = "0.###";

        public static string Format(decimal quantity) =>
            Format(quantity, CultureInfo.CurrentCulture);

        public static string Format(
            decimal quantity,
            IFormatProvider? formatProvider) =>
            quantity.ToString(
                DisplayFormat,
                formatProvider ?? CultureInfo.CurrentCulture);

        public static string FormatNullable(
            decimal? quantity,
            string nullText = "") =>
            quantity.HasValue
                ? Format(quantity.Value)
                : nullText;

        public static string FormatInvariantData(decimal quantity) =>
            quantity.ToString(
                InvariantDataFormat,
                CultureInfo.InvariantCulture);
    }
}
