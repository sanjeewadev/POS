namespace POS.Core.Models.DTOs
{
    public class StockBalanceDto
    {
        public int VariantId { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Uom { get; set; } = string.Empty;

        public decimal QtyOnHand { get; set; }

        // Financials
        public decimal UnitCost { get; set; }
        public decimal TotalCostValue { get; set; }

        public decimal UnitRetail { get; set; }
        public decimal TotalRetailValue { get; set; }
    }
}