using CsvHelper.Configuration.Attributes;

namespace POS.Core.DTOs.Import
{
    public class ItemImportDto
    {
        [Name("Item Code (Optional)")]
        public string ItemCode { get; set; } = string.Empty;

        [Name("Item Name (Required)")]
        public string ItemName { get; set; } = string.Empty;

        [Name("Category Name (Required)")]
        public string CategoryName { get; set; } = string.Empty;

        [Name("SubCategory Name (Optional)")]
        public string SubCategoryName { get; set; } = string.Empty;

        [Name("Barcode (Optional)")]
        public string Barcode { get; set; } = string.Empty;

        [Name("Cost Price (Optional)")]
        public decimal? CostPrice { get; set; }

        [Name("Retail Price (Optional)")]
        public decimal? RetailPrice { get; set; }

        [Name("Wholesale Price (Optional)")]
        public decimal? WholesalePrice { get; set; }
    }
}
