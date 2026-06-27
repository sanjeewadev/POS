using System;

namespace POS.Core.Models.DTOs
{
    /// <summary>
    /// A lightweight data transfer object used by the Cashier Terminal 
    /// to quickly search and display customers without loading heavy ledger data.
    /// </summary>
    public class CustomerSearchDto
    {
        public int Id { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string CustomerType { get; set; } = string.Empty; // "Retail" or "Wholesale"

        public decimal CreditLimit { get; set; }
        public decimal CurrentBalance { get; set; }
        public bool IsCreditLocked { get; set; }

        // Tells the POS what discount rule to auto-apply
        public int? LoyaltyDiscountProfileId { get; set; }

        // Smart UI Helper: Calculates available credit safely on the fly
        public decimal RemainingCredit => CreditLimit > 0 ? (CreditLimit - CurrentBalance) : 0m;
    }
}