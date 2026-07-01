using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS.Core.Models
{
    [Index(nameof(CustomerCode), IsUnique = true)]
    [Index(nameof(Phone))]
    [Index(nameof(CustomerType))]
    [Index(nameof(IsDiscountEligible))]
    [Index(nameof(IsCreditEnabled))]
    [Index(nameof(IsActive))]
    public class CustomerMaster
    {
        [Key]
        public int Id { get; set; }

        // Database primary key = Id.
        // CustomerCode is the visible customer/account number.
        // Example: CUST-000001
        [Required]
        [MaxLength(30)]
        public string CustomerCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        // Do not use phone as primary key.
        // It is only a searchable/indexed contact field.
        [MaxLength(30)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(300)]
        public string Address { get; set; } = string.Empty;

        // Optional birthday for future birthday discount rules.
        public DateTime? Birthday { get; set; }

        // Optional NIC for retail customers.
        [MaxLength(30)]
        public string NicNumber { get; set; } = string.Empty;

        // Optional company/business name for wholesale customers.
        [MaxLength(150)]
        public string CompanyName { get; set; } = string.Empty;

        // Optional BR number for wholesale/business customers.
        [MaxLength(50)]
        public string BusinessRegistrationNumber { get; set; } = string.Empty;

        // Optional VAT number if the business has VAT registration.
        [MaxLength(50)]
        public string VatRegistrationNumber { get; set; } = string.Empty;

        // Retail / Wholesale
        [Required]
        [MaxLength(20)]
        public string CustomerType { get; set; } = "Retail";

        // One internal flag.
        // Retail UI label    -> Loyalty
        // Wholesale UI label -> Discount
        public bool IsDiscountEligible { get; set; } = false;

        // Credit is admin-approved.
        // Cashier-created customers should default to false.
        public bool IsCreditEnabled { get; set; } = false;

        // None / PendingApproval / Active / Hold
        [Required]
        [MaxLength(30)]
        public string CreditStatus { get; set; } = "None";

        [Column(TypeName = "decimal(18,2)")]
        public decimal CreditLimit { get; set; } = 0m;

        public int CreditDays { get; set; } = 0;

        // Cached outstanding balance.
        // Later this should be controlled only through CustomerLedger updates.
        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentBalance { get; set; } = 0m;

        // Admin lock for stopping credit sales.
        public bool IsCreditLocked { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? DeactivatedAt { get; set; }

        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        [MaxLength(100)]
        public string UpdatedBy { get; set; } = string.Empty;

        // =========================================================
        // LIGHTWEIGHT LOYALTY / LEGACY COMPATIBILITY
        // =========================================================
        // Keep these for now because cashier/customer pages may still
        // display or search loyalty card and points.
        // The old LoyaltyDiscountProfile model is removed.

        public int? CustomerGroupId { get; set; }

        [MaxLength(50)]
        public string LoyaltyCardNumber { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal LoyaltyPointsBalance { get; set; } = 0m;

        // =========================================================
        // NAVIGATION
        // =========================================================

        public virtual ICollection<CustomerLedger> LedgerEntries { get; set; } = new List<CustomerLedger>();
    }
}