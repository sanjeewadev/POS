namespace POS.Core.Enums
{
    public enum TransactionType
    {
        GRN,          // Goods Received Note (Stock In)
        Sale,         // Sold to customer (Stock Out)
        Adjustment,   // Damaged or stolen (Stock Changed)
        Return        // Customer returned item (Stock In)
    }
}