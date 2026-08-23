using CsvHelper.Configuration.Attributes;

namespace POS.Core.DTOs.Import
{
    public class SupplierImportDto
    {
        [Name("Supplier Code (Optional)")]
        public string SupplierCode { get; set; } = string.Empty;

        [Name("Supplier Name (Required)")]
        public string SupplierName { get; set; } = string.Empty;

        [Name("Company Name (Optional)")]
        public string CompanyName { get; set; } = string.Empty;

        [Name("Contact Person (Optional)")]
        public string ContactPerson { get; set; } = string.Empty;

        [Name("Phone 1 (Optional)")]
        public string Phone1 { get; set; } = string.Empty;

        [Name("Phone 2 (Optional)")]
        public string Phone2 { get; set; } = string.Empty;

        [Name("Email (Optional)")]
        public string Email { get; set; } = string.Empty;

        [Name("Address (Optional)")]
        public string Address { get; set; } = string.Empty;
    }
}
