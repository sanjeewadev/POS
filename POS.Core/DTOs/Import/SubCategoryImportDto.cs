using CsvHelper.Configuration.Attributes;

namespace POS.Core.DTOs.Import
{
    public class SubCategoryImportDto
    {
        [Name("Parent Category Name (Required)")]
        public string ParentCategoryName { get; set; } = string.Empty;

        [Name("SubCategory Code (Optional)")]
        public string SubCategoryCode { get; set; } = string.Empty;

        [Name("SubCategory Name (Required)")]
        public string SubCategoryName { get; set; } = string.Empty;

        [Name("Display Order (Optional)")]
        public int? DisplayOrder { get; set; }
    }
}
