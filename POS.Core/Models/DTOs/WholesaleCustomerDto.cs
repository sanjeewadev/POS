using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace POS.Core.Models.DTOs
{
    public class WholesaleCustomerDto
    {
        // Identification
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;

        // Financial Status (For Credit Limit Validation)
        public decimal CreditLimit { get; set; }
        public decimal CurrentBalance { get; set; }

        // Security Flags (For POS Logic)
        public bool IsCreditLocked { get; set; }
        public bool IsActive { get; set; }

        // Helper property for UI logic (e.g., showing a warning message if limit is near)
        public decimal RemainingCredit => CreditLimit - CurrentBalance;

        // For applying assigned discounts
        public string ActiveDiscountName { get; set; } = "None";
        public DateTime? DiscountExpiry { get; set; }
    }
}
