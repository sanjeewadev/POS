using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Core.Models
{
    public class GiftVoucher
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string Barcode { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal InitialAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentBalance { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Inactive"; // "Inactive", "Active", "Exhausted", "Expired", "Blocked"

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? ActivationDate { get; set; }

        public DateTime? ExpiryDate { get; set; }

        [MaxLength(100)]
        public string? CustomerName { get; set; }

        [MaxLength(50)]
        public string? SoldInInvoiceNo { get; set; } // Tracks which receipt this liability was paid for
    }
}