using System;

namespace POS.Core.Models.DTOs
{
    public class CustomerSearchDto
    {
        public int Id { get; set; }

        public string CustomerCode { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        public string CustomerType { get; set; } = "Retail";

        public DateTime? Birthday { get; set; }

        public string NicNumber { get; set; } = string.Empty;

        public string BusinessRegistrationNumber { get; set; } = string.Empty;

        public string VatRegistrationNumber { get; set; } = string.Empty;

        public bool IsDiscountEligible { get; set; } = false;

        public bool IsCreditEnabled { get; set; } = false;

        public string CreditStatus { get; set; } = "None";

        public decimal CreditLimit { get; set; } = 0m;

        public decimal CurrentBalance { get; set; } = 0m;

        public bool IsCreditLocked { get; set; } = false;

        public bool IsActive { get; set; } = true;

        // =========================================================
        // TEMPORARY LEGACY COMPATIBILITY
        // =========================================================
        // Keep these because some old generated pages/viewmodels still reference them.
        // Later we can remove these after cleaning old loyalty/profile files.

        public int? LoyaltyDiscountProfileId { get; set; }

        public string ActiveDiscountName
        {
            get
            {
                if (!IsDiscountEligible)
                    return "None";

                if (CustomerType.Equals("Wholesale", StringComparison.OrdinalIgnoreCase))
                    return "Discount Enabled";

                return "Loyalty Enabled";
            }
        }

        public string DiscountScope
        {
            get
            {
                if (!IsDiscountEligible)
                    return "-";

                return "Manual Rules";
            }
        }

        public DateTime? DiscountExpiry { get; set; }

        // Some old grids may still use this.
        public string Name => FullName;

        // Some old grids may still use this.
        public decimal Points => 0m;

        // Some old grids may still use this.
        public decimal LoyaltyPoints => 0m;

        // =========================================================
        // DISPLAY HELPERS
        // =========================================================

        public string DisplayName
        {
            get
            {
                if (CustomerType.Equals("Wholesale", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(CompanyName))
                {
                    return CompanyName;
                }

                return FullName;
            }
        }

        public string DisplaySubName
        {
            get
            {
                if (CustomerType.Equals("Wholesale", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(CompanyName) &&
                    !string.IsNullOrWhiteSpace(FullName))
                {
                    return FullName;
                }

                return Phone;
            }
        }

        public string DisplayCustomerType
        {
            get
            {
                if (CustomerType.Equals("Wholesale", StringComparison.OrdinalIgnoreCase))
                    return "Wholesale";

                return "Retail";
            }
        }

        public string DiscountLabel
        {
            get
            {
                if (CustomerType.Equals("Wholesale", StringComparison.OrdinalIgnoreCase))
                    return IsDiscountEligible ? "Discount: Yes" : "Discount: No";

                return IsDiscountEligible ? "Loyalty: Yes" : "Loyalty: No";
            }
        }

        public bool IsRetail =>
            CustomerType.Equals("Retail", StringComparison.OrdinalIgnoreCase);

        public bool IsWholesale =>
            CustomerType.Equals("Wholesale", StringComparison.OrdinalIgnoreCase);

        public bool HasCreditFacility =>
            IsCreditEnabled &&
            CreditStatus.Equals("Active", StringComparison.OrdinalIgnoreCase) &&
            CreditLimit > 0m;

        public decimal RemainingCredit
        {
            get
            {
                decimal remaining = CreditLimit - CurrentBalance;
                return remaining < 0m ? 0m : remaining;
            }
        }

        public bool CanUseCredit
        {
            get
            {
                if (!IsActive)
                    return false;

                if (!IsCreditEnabled)
                    return false;

                if (!CreditStatus.Equals("Active", StringComparison.OrdinalIgnoreCase))
                    return false;

                if (IsCreditLocked)
                    return false;

                if (CreditLimit <= 0m)
                    return false;

                return RemainingCredit > 0m;
            }
        }

        public string CreditStatusText
        {
            get
            {
                if (!IsCreditEnabled)
                    return "No Credit";

                if (IsCreditLocked)
                    return "Credit Locked";

                if (CreditStatus.Equals("Hold", StringComparison.OrdinalIgnoreCase))
                    return "Credit Hold";

                if (CreditStatus.Equals("PendingApproval", StringComparison.OrdinalIgnoreCase))
                    return "Pending Approval";

                if (CreditStatus.Equals("Active", StringComparison.OrdinalIgnoreCase))
                    return $"Available Rs. {RemainingCredit:N2}";

                return "No Credit";
            }
        }

        public string CreditWarningText
        {
            get
            {
                if (!IsActive)
                    return "Customer account is inactive.";

                if (!IsCreditEnabled)
                    return "Credit is not enabled for this customer.";

                if (IsCreditLocked)
                    return "Credit account is locked by management.";

                if (CreditStatus.Equals("Hold", StringComparison.OrdinalIgnoreCase))
                    return "Credit account is on hold.";

                if (CreditStatus.Equals("PendingApproval", StringComparison.OrdinalIgnoreCase))
                    return "Credit account is pending admin approval.";

                if (CreditLimit <= 0m)
                    return "Credit limit is zero.";

                if (RemainingCredit <= 0m)
                    return "Credit limit exceeded.";

                return string.Empty;
            }
        }
    }
}