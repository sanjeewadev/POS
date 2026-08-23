namespace POS.Core.DTOs.Import
{
    public class CategoryPreviewRow
    {
        public int RowNumber { get; set; }
        public CategoryImportDto Data { get; set; } = new();
        public bool IsValid { get; set; } = true;
        public string ErrorMessage { get; set; } = string.Empty;
        
        // For UI Binding
        public string StatusText => IsValid ? "Valid" : "Error";
    }
}
