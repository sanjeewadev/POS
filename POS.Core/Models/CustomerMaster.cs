using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS.Core.Models
{
    // Indexing the Code for fast scanning, and Phone for quick cashier lookups
    [Index(nameof(CustomerCode), IsUnique = true)]
    [Index(nameof(Phone))]
    public class CustomerMaster
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string CustomerCode { get; set; } = string.Empty; // e.g., CUST-1001

        [Required]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(255)]
        public string Address { get; set; } = string.Empty;

        // ==========================================
        // B2B & WHOLESALE INFORMATION
        // ==========================================
        [MaxLength(150)]
        public string CompanyName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string VatRegistrationNumber { get; set; } = string.Empty;

        // ==========================================
        // POS SETTINGS & LOYALTY
        // ==========================================
        [Required]
        [MaxLength(20)]
        public string CustomerType { get; set; } = "Retail"; // "Retail" or "Wholesale" (Triggers UI/Price changes)

        // Nullable because a standard walk-in might not be part of a loyalty tier
        public int? CustomerGroupId { get; set; }

        // ==========================================
        // FINANCIAL CONTROLS
        // ==========================================
        [Column(TypeName = "decimal(18,2)")]
        public decimal CreditLimit { get; set; } = 0m; // Maximum allowed debt

        public int CreditDays { get; set; } = 0; // Payment terms (e.g., 30 Days)

        // A live, cached snapshot of what they owe to prevent the POS from calculating the whole ledger every time
        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentBalance { get; set; } = 0m;

        // ==========================================
        // SECURITY FLAGS
        // ==========================================
        public bool IsActive { get; set; } = true;

        // Admin override to stop them from buying on credit if they default on payments
        public bool IsCreditLocked { get; set; } = false;

        // Navigation Property
        public virtual ICollection<CustomerLedger> LedgerEntries { get; set; } = new List<CustomerLedger>();
    }
}