namespace POS.Core.Configuration
{
    public static class CashierCartStatusCodes
    {
        public const string Active = "Active";
        public const string Held = "Held";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
    }

    public static class CashierCartLineTypeCodes
    {
        public const string StockItem = "StockItem";
        public const string Service = "Service";
        public const string GiftVoucherSale = "GiftVoucherSale";
    }

    public static class CashierCartCancellationReasons
    {
        public const string CustomerChangedMind = "CustomerChangedMind";
        public const string WrongItems = "WrongItems";
        public const string PricingIssue = "PricingIssue";
        public const string PaymentNotAvailable = "PaymentNotAvailable";
        public const string DuplicateEntry = "DuplicateEntry";
        public const string Other = "Other";

        // Internal lifecycle reason used when the cashier removes the final line.
        public const string CartEmptied = "CartEmptied";
    }
}
