namespace POS.Core.Models.DTOs
{
    public class ItemSupplierDto
    {
        public int Id { get; set; }

        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty; // Fetched for UI display

        public string? SupplierItemCode { get; set; }
        public decimal LastCostPrice { get; set; }
        public bool IsPrimary { get; set; }
        public int MinimumOrderQuantity { get; set; } = 1;
    }
}