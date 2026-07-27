using POS.Core.Services.Pricing;

namespace POS.Core.CalculationTests
{
    internal static partial class Program
    {
        private static void BatchPricingSuggestionUsesMarkupAndRounding()
        {
            decimal result = SellingPriceSuggestionCalculator.FromMarkup(
                538.67m,
                30m,
                5m);

            AssertMoney(700m, result, "30 percent markup rounded to Rs. 5");
        }

        private static void BatchPricingSuggestionUsesTargetMargin()
        {
            decimal result = SellingPriceSuggestionCalculator.FromTargetMargin(
                538.67m,
                25m,
                10m);

            AssertMoney(720m, result, "25 percent target margin rounded to Rs. 10");

            bool rejected = false;
            try
            {
                SellingPriceSuggestionCalculator.FromTargetMargin(538.67m, 100m);
            }
            catch (ArgumentOutOfRangeException)
            {
                rejected = true;
            }

            AssertTrue(rejected, "100 percent target margin is rejected");
        }

        private static void WholesaleSuggestionUsesRetailDiscount()
        {
            decimal result = SellingPriceSuggestionCalculator.FromRetailDiscount(
                700m,
                10m,
                5m);

            AssertMoney(630m, result, "10 percent Retail discount rounded to Rs. 5");
        }

        private static void PricingProfitAndMarginPreviewIsExact()
        {
            decimal profit = SellingPriceSuggestionCalculator.ProfitPerUnit(620m, 538.67m);
            decimal margin = SellingPriceSuggestionCalculator.MarginPercent(620m, 538.67m);

            AssertMoney(81.33m, profit, "Wholesale profit per unit");
            AssertMoney(13.12m, margin, "Wholesale margin percentage");
        }
    }
}
