namespace POS.Core.Models
{
    public class ProductUOM
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string UOMName { get; set; } = string.Empty;
        public decimal ConversionFactor { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public decimal SellingPrice { get; set; }
    }
}