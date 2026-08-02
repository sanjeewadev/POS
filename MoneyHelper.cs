POS.Core\Utilities\MoneyHelper.cs
namespace POS.Core.Utilities
{
    public static class MoneyHelper
    {
        // Centralized rounding for monetary values in the application.
        // Uses AwayFromZero rounding to match common retail expectations.
        public static decimal RoundToAccounting(decimal value) =>
            decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}