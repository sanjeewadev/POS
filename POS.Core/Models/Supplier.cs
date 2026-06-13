using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class Supplier
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string SupplierCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string SupplierName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string CompanyName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string ContactPerson { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Phone1 { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Phone2 { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(250)]
        public string Address { get; set; } = string.Empty;

        // Financial & Tax Setup
        public bool HasVat { get; set; }

        [MaxLength(50)]
        public string VatNumber { get; set; } = string.Empty;

        public int DefaultCreditDays { get; set; } = 30;

        // Driven by accounting, read-only on the Supplier Master UI
        public decimal CurrentBalance { get; set; } = 0m;

        public bool IsDeactivated { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Computed Property for the DataGrid - Not saved to the DB
        [NotMapped]
        public string StatusText => IsDeactivated ? "Suspended" : "Active";
    }
}