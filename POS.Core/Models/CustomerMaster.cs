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
        // POS SETTINGS & LOYALTY (UPGRADED)
        // ==========================================
        [Required]
        [MaxLength(20)]
        public string CustomerType { get; set; } = "Retail";

        public int? CustomerGroupId { get; set; }

        [MaxLength(50)]
        public string LoyaltyCardNumber { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal LoyaltyPointsBalance { get; set; } = 0m;

        // ------------------------------------------
        // NEW: MANUAL DISCOUNT ASSIGNMENT
        // ------------------------------------------
        public int? LoyaltyDiscountProfileId { get; set; } // Links to the specific discount rule

        public DateTime? LoyaltyDiscountExpiryDate { get; set; } // When does this customer's perk expire?

        [ForeignKey("LoyaltyDiscountProfileId")]
        public virtual LoyaltyDiscountProfile? LoyaltyDiscountProfile { get; set; } // Navigation property
        // ==========================================
        // FINANCIAL CONTROLS
        // ==========================================
        [Column(TypeName = "decimal(18,2)")]
        public decimal CreditLimit { get; set; } = 0m; // Maximum allowed debt

        public int CreditDays { get; set; } = 0; // Payment terms (e.g., 30 Days)

        // A live, cached snapshot of what they owe
        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentBalance { get; set; } = 0m;

        // ==========================================
        // SECURITY FLAGS
        // ==========================================
        public bool IsActive { get; set; } = true;

        // Admin override to stop them from buying on credit
        public bool IsCreditLocked { get; set; } = false;

        // Navigation Property
        public virtual ICollection<CustomerLedger> LedgerEntries { get; set; } = new List<CustomerLedger>();
    }
}