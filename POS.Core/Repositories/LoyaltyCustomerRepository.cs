using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Core.Repositories
{
    // Legacy DTO kept for old pages/dialogs that may still reference LoyaltyCustomerDto.
    // New cashier loyalty lookup should use CustomerSearchDto through CustomerRepository.
    public class LoyaltyCustomerDto
    {
        public int CustomerId { get; set; }

        public string CustomerCode { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        public DateTime? Birthday { get; set; }

        public string Email { get; set; } = string.Empty;

        public string ActiveDiscountName { get; set; } = "Loyalty Enabled";

        public string DiscountScope { get; set; } = "Manual Rules";

        public DateTime? DiscountExpiry { get; set; }
    }

    public class LoyaltyCustomerRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public LoyaltyCustomerRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =========================================================
        // LEGACY LOYALTY LOOKUP
        // =========================================================
        // New rule:
        // Loyalty customer = Retail customer + IsDiscountEligible = true.
        public async Task<List<LoyaltyCustomerDto>> SearchLoyaltyCustomersAsync(string searchTerm)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.CustomerMasters
                .AsNoTracking()
                .Where(c =>
                    c.IsActive &&
                    c.CustomerType == "Retail" &&
                    c.IsDiscountEligible);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim().ToLower();

                query = query.Where(c =>
                    c.CustomerCode.ToLower().Contains(term) ||
                    c.FullName.ToLower().Contains(term) ||
                    c.Phone.Contains(term) ||
                    (!string.IsNullOrWhiteSpace(c.Email) && c.Email.ToLower().Contains(term)) ||
                    (!string.IsNullOrWhiteSpace(c.NicNumber) && c.NicNumber.ToLower().Contains(term)));
            }

            return await query
                .OrderBy(c => c.FullName)
                .Take(50)
                .Select(c => new LoyaltyCustomerDto
                {
                    CustomerId = c.Id,
                    CustomerCode = c.CustomerCode,
                    FullName = c.FullName,
                    Phone = c.Phone,
                    Email = c.Email,
                    Birthday = c.Birthday,

                    // Temporary display text until the real manual discount rule engine is built.
                    ActiveDiscountName = c.IsDiscountEligible ? "Loyalty Enabled" : "No Loyalty",
                    DiscountScope = "Manual Rules",
                    DiscountExpiry = null
                })
                .ToListAsync();
        }

        // =========================================================
        // LEGACY RETAIL LOYALTY REGISTRATION
        // =========================================================
        // This is kept for old dialogs only.
        // Cashier quick registration should later move to CustomerRepository.RegisterQuickCustomerAsync().
        public async Task<LoyaltyCustomerDto> RegisterLoyaltyCustomerAsync(
            string fullName,
            string phone,
            string email,
            DateTime? birthday)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            string safeName = (fullName ?? string.Empty).Trim();
            string safePhone = NormalizePhone(phone);
            string safeEmail = (email ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(safeName))
                throw new InvalidOperationException("Customer name is required.");

            if (string.IsNullOrWhiteSpace(safePhone))
                throw new InvalidOperationException("Phone number is required.");

            bool exists = await context.CustomerMasters
                .AnyAsync(c => c.Phone == safePhone && c.IsActive);

            if (exists)
                throw new InvalidOperationException($"Phone number {safePhone} is already registered.");

            var customer = new CustomerMaster
            {
                CustomerCode = await GenerateCustomerCodeAsync(context),
                FullName = safeName,
                Phone = safePhone,
                Email = safeEmail,
                Birthday = birthday,

                CustomerType = "Retail",
                IsDiscountEligible = true,

                // Cashier registration should not approve credit.
                IsCreditEnabled = false,
                CreditStatus = "None",
                CreditLimit = 0m,
                CreditDays = 0,
                CurrentBalance = 0m,
                IsCreditLocked = false,

                IsActive = true,
                CreatedAt = DateTime.Now,

                // Legacy compatibility.
                LoyaltyPointsBalance = 0m
            };

            context.CustomerMasters.Add(customer);
            await context.SaveChangesAsync();

            return new LoyaltyCustomerDto
            {
                CustomerId = customer.Id,
                CustomerCode = customer.CustomerCode,
                FullName = customer.FullName,
                Phone = customer.Phone,
                Email = customer.Email,
                Birthday = customer.Birthday,
                ActiveDiscountName = "Loyalty Enabled",
                DiscountScope = "Manual Rules",
                DiscountExpiry = null
            };
        }

        // =========================================================
        // LEGACY WHOLESALE LOOKUP
        // =========================================================
        // Kept only if some old file still calls this method.
        // New wholesale lookup should use CustomerRepository.SearchWholesaleCustomersAsync().
        public async Task<List<WholesaleCustomerDto>> SearchWholesaleCustomersAsync(string searchTerm)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.CustomerMasters
                .AsNoTracking()
                .Where(c =>
                    c.IsActive &&
                    c.CustomerType == "Wholesale");

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim().ToLower();

                query = query.Where(c =>
                    c.CustomerCode.ToLower().Contains(term) ||
                    c.FullName.ToLower().Contains(term) ||
                    c.Phone.Contains(term) ||
                    (!string.IsNullOrWhiteSpace(c.CompanyName) && c.CompanyName.ToLower().Contains(term)) ||
                    (!string.IsNullOrWhiteSpace(c.BusinessRegistrationNumber) && c.BusinessRegistrationNumber.ToLower().Contains(term)) ||
                    (!string.IsNullOrWhiteSpace(c.VatRegistrationNumber) && c.VatRegistrationNumber.ToLower().Contains(term)));
            }

            return await query
                .OrderBy(c => string.IsNullOrWhiteSpace(c.CompanyName) ? c.FullName : c.CompanyName)
                .Take(50)
                .Select(c => new WholesaleCustomerDto
                {
                    CustomerId = c.Id,
                    CustomerCode = c.CustomerCode,
                    FullName = c.FullName,
                    CompanyName = string.IsNullOrWhiteSpace(c.CompanyName) ? c.FullName : c.CompanyName,
                    CreditLimit = c.CreditLimit,
                    CurrentBalance = c.CurrentBalance,
                    IsCreditLocked = c.IsCreditLocked,
                    IsActive = c.IsActive,

                    // Temporary text until real manual discount engine is added.
                    ActiveDiscountName = c.IsDiscountEligible ? "Discount Enabled" : "None",
                    DiscountExpiry = null
                })
                .ToListAsync();
        }

        // =========================================================
        // CONVENIENCE: NEW DTO SHAPE
        // =========================================================
        // Useful if any future loyalty view wants CustomerSearchDto directly.
        public async Task<List<CustomerSearchDto>> SearchRetailLoyaltyCustomersAsSearchDtoAsync(
            string searchTerm,
            int take = 50)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.CustomerMasters
                .AsNoTracking()
                .Where(c =>
                    c.IsActive &&
                    c.CustomerType == "Retail" &&
                    c.IsDiscountEligible);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim().ToLower();

                query = query.Where(c =>
                    c.CustomerCode.ToLower().Contains(term) ||
                    c.FullName.ToLower().Contains(term) ||
                    c.Phone.Contains(term) ||
                    (!string.IsNullOrWhiteSpace(c.NicNumber) && c.NicNumber.ToLower().Contains(term)));
            }

            return await query
                .OrderBy(c => c.FullName)
                .Take(take)
                .Select(c => new CustomerSearchDto
                {
                    Id = c.Id,
                    CustomerCode = c.CustomerCode,
                    FullName = c.FullName,
                    CompanyName = c.CompanyName,
                    Phone = c.Phone,
                    CustomerType = c.CustomerType,
                    Birthday = c.Birthday,
                    NicNumber = c.NicNumber,
                    BusinessRegistrationNumber = c.BusinessRegistrationNumber,
                    VatRegistrationNumber = c.VatRegistrationNumber,
                    IsDiscountEligible = c.IsDiscountEligible,
                    IsCreditEnabled = c.IsCreditEnabled,
                    CreditStatus = c.CreditStatus,
                    CreditLimit = c.CreditLimit,
                    CurrentBalance = c.CurrentBalance,
                    IsCreditLocked = c.IsCreditLocked,
                    IsActive = c.IsActive,

                    // Legacy.
                    LoyaltyDiscountProfileId = c.LoyaltyDiscountProfileId
                })
                .ToListAsync();
        }

        // =========================================================
        // PRIVATE HELPERS
        // =========================================================

        private async Task<string> GenerateCustomerCodeAsync(AppDbContext context)
        {
            var sequence = await context.DocumentSequences
                .FirstOrDefaultAsync(d => d.DocumentType == "CUS");

            if (sequence == null)
            {
                sequence = new DocumentSequence
                {
                    DocumentType = "CUS",
                    Prefix = "CUST-",
                    NextSequenceNumber = 1,
                    PaddingLength = 6,
                    UpdatedAt = DateTime.Now
                };

                context.DocumentSequences.Add(sequence);
            }

            int attempts = 0;

            while (attempts < 1000)
            {
                int nextNumber = sequence.NextSequenceNumber;
                int padding = sequence.PaddingLength <= 0 ? 6 : sequence.PaddingLength;

                string code = $"{sequence.Prefix}{nextNumber.ToString($"D{padding}")}";

                sequence.NextSequenceNumber++;
                sequence.UpdatedAt = DateTime.Now;

                bool exists = await context.CustomerMasters
                    .AnyAsync(c => c.CustomerCode == code);

                if (!exists)
                    return code;

                attempts++;
            }

            throw new InvalidOperationException("Unable to generate a unique customer code.");
        }

        private static string NormalizePhone(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim()
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty);
        }
    }
}