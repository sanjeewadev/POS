using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS.Core.Models
{
    // Indexing the ReturnNo for fast cashier/admin lookups
    [Index(nameof(ReturnNo), IsUnique = true)]
    public class CustomerReturnHeader
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string ReturnNo { get; set; } = string.Empty; // e.g., CR-20260615-001

        // Nullable because a "Blind Return" might not have an original receipt
        [MaxLength(50)]
        public string? OriginalInvoiceNo { get; set; }

        [Required]
        public int ShiftSessionId { get; set; }

        [Required]
        [MaxLength(20)]
        public string TerminalNo { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CashierName { get; set; } = string.Empty;

        // High-value cash refunds require a manager swipe
        [MaxLength(100)]
        public string AuthorizedBy { get; set; } = string.Empty;

        [Required]
        public DateTime ReturnDate { get; set; } = DateTime.Now;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalRefundAmount { get; set; }

        [Required]
        [MaxLength(30)]
        public string RefundMethod { get; set; } = "Cash"; // Cash, Card Reversal, Store Credit

        // Navigation Property mapped to the renamed Line table
        public virtual ICollection<CustomerReturnLine> Lines { get; set; } = new List<CustomerReturnLine>();
    }
}