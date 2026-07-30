namespace POS.Cashier.UI.Models
{
    /// <summary>
    /// Represents the final, confirmed result from the product seek dialog.
    /// </summary>
    public class ProductSeekResult
    {
        public int VariantId { get; set; }
        public int? BatchId { get; set; }
    }
}