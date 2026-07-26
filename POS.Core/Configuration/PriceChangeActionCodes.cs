namespace POS.Core.Configuration
{
    public static class PriceChangeActionCodes
    {
        public const string MasterPriceUpdated = "MasterPriceUpdated";
        public const string BatchOverrideCreated = "BatchOverrideCreated";
        public const string BatchOverrideUpdated = "BatchOverrideUpdated";
        public const string BatchOverrideRemoved = "BatchOverrideRemoved";
        public const string LegacyMasterChange = "LegacyMasterChange";
        public const string LegacyBatchChange = "LegacyBatchChange";
        public const string Legacy = "Legacy";
    }
}
