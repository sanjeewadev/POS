using System;

namespace POS.Core.Models
{
    public class Supplier
    {
        public int Id { get; set; }
        public string SupplierCode { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string Phone1 { get; set; } = string.Empty;

        // Nullable strings for optional fields to keep the database clean
        public string? Phone2 { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }

        public bool HasVat { get; set; } = false;
        public string? VatNumber { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int DefaultCreditDays { get; set; } = 30; // Auto-calculates GRN Due Date
    }
}