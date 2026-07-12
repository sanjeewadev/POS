namespace POS.Core.Configuration
{
    public static class CustomerReturnDocumentTypes
    {
        public const string CreditNote = "CreditNote";
    }

    public static class CustomerReturnRefundMethods
    {
        public const string Cash = "Cash";
    }

    public static class CustomerReturnInventoryActions
    {
        public const string RestoredToOriginalBatch = "RestoredToOriginalBatch";
        public const string NoInventory = "NoInventory";
    }

    public static class CustomerReturnSequenceCodes
    {
        public const string DocumentType = "CRN";
        public const string Prefix = "CN-";
        public const int PaddingLength = 6;
    }

    public static class CustomerReturnCashMovementCodes
    {
        public const string MovementType = "Paid Out";
        public const string ReasonCategory = "Customer Refund";
    }
}
