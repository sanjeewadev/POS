using CsvHelper.Configuration.Attributes;

namespace POS.Core.DTOs.Import
{
    public class CategoryImportDto
    {
        [Name("Category Code (Optional)")]
        public string CategoryCode { get; set; } = string.Empty;

        [Name("Category Name (Required)")]
        public string CategoryName { get; set; } = string.Empty;

        [Name("Description (Optional)")]
        public string Description { get; set; } = string.Empty;

        [Name("Display Order (Optional)")]
        public int? DisplayOrder { get; set; }
    }
}
