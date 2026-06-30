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
    public class CustomerRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public CustomerRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =========================================================
        // CASHIER: OLD B2B LOOKUP COMPATIBILITY
        // =========================================================
        // Existing B2BCustomerDialogView uses this method.
        // Keep it for now, but make it safer:
        // - Wholesale customers
        // - OR credit enabled customers
        // - OR customers with a credit limit
        public async Task<List<CustomerSearchDto>> SearchB2BCustomersAsync(string searchTerm)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.CustomerMasters
                .AsNoTracking()
                .Where(c =>
                    c.IsActive &&
                    (
                        c.CustomerType == "Wholesale" ||
                        c.IsCreditEnabled ||
                        c.CreditLimit > 0m
                    ));

            query = ApplySearch(query, searchTerm);

            return await query
                .OrderBy(c => c.CompanyName == string.Empty ? c.FullName : c.CompanyName)
                .Take(50)
                .Select(c => BuildCustomerSearchDto(c))
                .ToListAsync();
        }

        // =========================================================
        // CASHIER: GENERAL CUSTOMER LOOKUP
        // =========================================================
        public async Task<List<CustomerSearchDto>> SearchActiveCustomersAsync(string searchTerm, int take = 50)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.CustomerMasters
                .AsNoTracking()
                .Where(c => c.IsActive);

            query = ApplySearch(query, searchTerm);

            return await query
                .OrderBy(c => c.FullName)
                .Take(take)
                .Select(c => BuildCustomerSearchDto(c))
                .ToListAsync();
        }

        // Loyalty button shortcut:
        // Retail customers only where IsDiscountEligible = true.
        public async Task<List<CustomerSearchDto>> SearchRetailLoyaltyCustomersAsync(string searchTerm, int take = 50)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.CustomerMasters
                .AsNoTracking()
                .Where(c =>
                    c.IsActive &&
                    c.CustomerType == "Retail" &&
                    c.IsDiscountEligible);

            query = ApplySearch(query, searchTerm);

            return await query
                .OrderBy(c => c.FullName)
                .Take(take)
                .Select(c => BuildCustomerSearchDto(c))
                .ToListAsync();
        }

        // Wholesale shortcut if needed later.
        public async Task<List<CustomerSearchDto>> SearchWholesaleCustomersAsync(string searchTerm, int take = 50)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.CustomerMasters
                .AsNoTracking()
                .Where(c =>
                    c.IsActive &&
                    c.CustomerType == "Wholesale");

            query = ApplySearch(query, searchTerm);

            return await query
                .OrderBy(c => c.CompanyName == string.Empty ? c.FullName : c.CompanyName)
                .Take(take)
                .Select(c => BuildCustomerSearchDto(c))
                .ToListAsync();
        }

        public async Task<CustomerMaster?> GetCustomerByIdAsync(int customerId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.CustomerMasters
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerId);
        }

        // =========================================================
        // ADMIN: GRID POPULATION / FILTERING
        // =========================================================
        public async Task<List<CustomerMaster>> GetFilteredCustomersAsync(string filterType, string searchTerm)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.CustomerMasters
                .Include(c => c.LoyaltyDiscountProfile)
                .AsNoTracking()
                .AsQueryable();

            string filter = (filterType ?? "All Customers").Trim();

            if (filter.Equals("Retail", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c => c.CustomerType == "Retail");
            }
            else if (filter.Equals("Wholesale", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c => c.CustomerType == "Wholesale");
            }
            else if (filter.Equals("Discount Eligible", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c => c.IsDiscountEligible);
            }
            else if (filter.Equals("Credit Enabled", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c => c.IsCreditEnabled);
            }
            else if (filter.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c => !c.IsActive);
            }

            query = ApplySearch(query, searchTerm);

            return await query
                .OrderBy(c => c.FullName)
                .ToListAsync();
        }

        // =========================================================
        // ADMIN: SAVE CUSTOMER PROFILE
        // =========================================================
        public async Task<CustomerMaster> SaveCustomerProfileAsync(CustomerMaster customer)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            NormalizeCustomer(customer);
            ValidateCustomer(customer);

            if (customer.Id == 0)
            {
                if (string.IsNullOrWhiteSpace(customer.CustomerCode))
                    customer.CustomerCode = await GenerateCustomerCodeAsync(context);

                bool codeExists = await context.CustomerMasters
                    .AnyAsync(c => c.CustomerCode == customer.CustomerCode);

                if (codeExists)
                    throw new InvalidOperationException($"Customer code already exists: {customer.CustomerCode}");

                customer.CreatedAt = DateTime.Now;
                customer.UpdatedAt = null;
                customer.DeactivatedAt = customer.IsActive ? null : DateTime.Now;

                context.CustomerMasters.Add(customer);
                await context.SaveChangesAsync();

                return customer;
            }

            var existing = await context.CustomerMasters
                .FirstOrDefaultAsync(c => c.Id == customer.Id);

            if (existing == null)
                throw new InvalidOperationException("Customer record not found in database.");

            existing.FullName = customer.FullName;
            existing.Phone = customer.Phone;
            existing.Email = customer.Email;
            existing.Address = customer.Address;
            existing.Birthday = customer.Birthday;
            existing.NicNumber = customer.NicNumber;

            existing.CompanyName = customer.CompanyName;
            existing.BusinessRegistrationNumber = customer.BusinessRegistrationNumber;
            existing.VatRegistrationNumber = customer.VatRegistrationNumber;

            existing.CustomerType = customer.CustomerType;
            existing.IsDiscountEligible = customer.IsDiscountEligible;

            existing.IsCreditEnabled = customer.IsCreditEnabled;
            existing.CreditStatus = customer.CreditStatus;
            existing.CreditLimit = customer.CreditLimit;
            existing.CreditDays = customer.CreditDays;
            existing.IsCreditLocked = customer.IsCreditLocked;

            existing.IsActive = customer.IsActive;
            existing.UpdatedAt = DateTime.Now;
            existing.UpdatedBy = customer.UpdatedBy;

            if (!existing.IsActive && existing.DeactivatedAt == null)
                existing.DeactivatedAt = DateTime.Now;

            if (existing.IsActive)
                existing.DeactivatedAt = null;

            // Temporary legacy compatibility.
            existing.CustomerGroupId = customer.CustomerGroupId;
            existing.LoyaltyCardNumber = customer.LoyaltyCardNumber;
            existing.LoyaltyDiscountProfileId = customer.LoyaltyDiscountProfileId;
            existing.LoyaltyDiscountExpiryDate = customer.LoyaltyDiscountExpiryDate;

            await context.SaveChangesAsync();

            return existing;
        }

        // =========================================================
        // CASHIER: QUICK BASIC REGISTRATION
        // =========================================================
        // Cashier should create only basic customer data.
        // Credit limit, discount rules, and financial approval stay in BackOffice.
        public async Task<CustomerMaster> RegisterQuickCustomerAsync(
            string fullName,
            string phone,
            string customerType,
            DateTime? birthday = null,
            string address = "",
            string nicOrBrNumber = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            string safeName = (fullName ?? string.Empty).Trim();
            string safePhone = NormalizePhone(phone);
            string safeType = NormalizeCustomerType(customerType);

            if (string.IsNullOrWhiteSpace(safeName))
                throw new InvalidOperationException("Customer name is required.");

            if (string.IsNullOrWhiteSpace(safePhone))
                throw new InvalidOperationException("Phone number is required for quick cashier registration.");

            bool phoneAlreadyExists = await context.CustomerMasters
                .AnyAsync(c => c.Phone == safePhone && c.IsActive);

            if (phoneAlreadyExists)
                throw new InvalidOperationException($"Phone number {safePhone} is already registered. Search and attach the existing customer.");

            var customer = new CustomerMaster
            {
                CustomerCode = await GenerateCustomerCodeAsync(context),
                FullName = safeName,
                Phone = safePhone,
                CustomerType = safeType,
                Birthday = birthday,
                Address = (address ?? string.Empty).Trim(),

                NicNumber = safeType == "Retail"
                    ? (nicOrBrNumber ?? string.Empty).Trim()
                    : string.Empty,

                BusinessRegistrationNumber = safeType == "Wholesale"
                    ? (nicOrBrNumber ?? string.Empty).Trim()
                    : string.Empty,

                IsDiscountEligible = false,

                // Cashier cannot approve credit.
                IsCreditEnabled = false,
                CreditStatus = "None",
                CreditLimit = 0m,
                CreditDays = 0,
                CurrentBalance = 0m,
                IsCreditLocked = false,

                IsActive = true,
                CreatedAt = DateTime.Now
            };

            context.CustomerMasters.Add(customer);
            await context.SaveChangesAsync();

            return customer;
        }

        // =========================================================
        // LEDGER
        // =========================================================
        public async Task<List<CustomerLedger>> GetCustomerLedgerAsync(
            int customerId,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.CustomerLedgers
                .AsNoTracking()
                .Where(l => l.CustomerMasterId == customerId);

            if (startDate.HasValue)
                query = query.Where(l => l.TransactionDate >= startDate.Value.Date);

            if (endDate.HasValue)
                query = query.Where(l => l.TransactionDate <= endDate.Value.Date.AddDays(1).AddTicks(-1));

            return await query
                .OrderByDescending(l => l.TransactionDate)
                .ToListAsync();
        }

        // =========================================================
        // PRIVATE HELPERS
        // =========================================================

        private static IQueryable<CustomerMaster> ApplySearch(IQueryable<CustomerMaster> query, string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return query;

            string term = searchTerm.Trim().ToLower();

            return query.Where(c =>
                c.CustomerCode.ToLower().Contains(term) ||
                c.FullName.ToLower().Contains(term) ||
                c.Phone.Contains(term) ||
                (!string.IsNullOrWhiteSpace(c.Email) && c.Email.ToLower().Contains(term)) ||
                (!string.IsNullOrWhiteSpace(c.NicNumber) && c.NicNumber.ToLower().Contains(term)) ||
                (!string.IsNullOrWhiteSpace(c.CompanyName) && c.CompanyName.ToLower().Contains(term)) ||
                (!string.IsNullOrWhiteSpace(c.BusinessRegistrationNumber) && c.BusinessRegistrationNumber.ToLower().Contains(term)) ||
                (!string.IsNullOrWhiteSpace(c.VatRegistrationNumber) && c.VatRegistrationNumber.ToLower().Contains(term))
            );
        }

        private static CustomerSearchDto BuildCustomerSearchDto(CustomerMaster c)
        {
            return new CustomerSearchDto
            {
                Id = c.Id,
                CustomerCode = c.CustomerCode,
                FullName = c.FullName,
                CompanyName = c.CompanyName ?? string.Empty,
                Phone = c.Phone,
                CustomerType = c.CustomerType,
                Birthday = c.Birthday,
                NicNumber = c.NicNumber ?? string.Empty,
                BusinessRegistrationNumber = c.BusinessRegistrationNumber ?? string.Empty,
                VatRegistrationNumber = c.VatRegistrationNumber ?? string.Empty,
                IsDiscountEligible = c.IsDiscountEligible,
                IsCreditEnabled = c.IsCreditEnabled,
                CreditStatus = string.IsNullOrWhiteSpace(c.CreditStatus) ? "None" : c.CreditStatus,
                CreditLimit = c.CreditLimit,
                CurrentBalance = c.CurrentBalance,
                IsCreditLocked = c.IsCreditLocked,
                IsActive = c.IsActive,

                // Temporary legacy compatibility.
                LoyaltyDiscountProfileId = c.LoyaltyDiscountProfileId,
                DiscountExpiry = c.LoyaltyDiscountExpiryDate
            };
        }

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

        private static void NormalizeCustomer(CustomerMaster customer)
        {
            customer.CustomerCode = (customer.CustomerCode ?? string.Empty).Trim();
            customer.FullName = (customer.FullName ?? string.Empty).Trim();
            customer.Phone = NormalizePhone(customer.Phone);
            customer.Email = (customer.Email ?? string.Empty).Trim();
            customer.Address = (customer.Address ?? string.Empty).Trim();

            customer.NicNumber = (customer.NicNumber ?? string.Empty).Trim();
            customer.CompanyName = (customer.CompanyName ?? string.Empty).Trim();
            customer.BusinessRegistrationNumber = (customer.BusinessRegistrationNumber ?? string.Empty).Trim();
            customer.VatRegistrationNumber = (customer.VatRegistrationNumber ?? string.Empty).Trim();

            customer.CustomerType = NormalizeCustomerType(customer.CustomerType);

            customer.CreditStatus = NormalizeCreditStatus(customer.CreditStatus);

            if (!customer.IsCreditEnabled)
            {
                customer.CreditStatus = "None";
                customer.CreditLimit = 0m;
                customer.CreditDays = 0;
                customer.IsCreditLocked = false;
            }
            else
            {
                if (customer.CreditStatus == "None")
                    customer.CreditStatus = "Active";

                if (customer.CreditLimit < 0m)
                    customer.CreditLimit = 0m;

                if (customer.CreditDays < 0)
                    customer.CreditDays = 0;
            }

            customer.LoyaltyCardNumber = (customer.LoyaltyCardNumber ?? string.Empty).Trim();
        }

        private static void ValidateCustomer(CustomerMaster customer)
        {
            if (string.IsNullOrWhiteSpace(customer.FullName))
                throw new InvalidOperationException("Customer name is required.");

            if (string.IsNullOrWhiteSpace(customer.Phone))
                throw new InvalidOperationException("Phone number is required.");

            if (customer.CustomerType != "Retail" && customer.CustomerType != "Wholesale")
                throw new InvalidOperationException("Customer type must be Retail or Wholesale.");

            if (customer.CreditLimit < 0m)
                throw new InvalidOperationException("Credit limit cannot be negative.");

            if (customer.CurrentBalance < 0m)
                throw new InvalidOperationException("Current balance cannot be negative.");

            if (customer.CreditDays < 0)
                throw new InvalidOperationException("Credit days cannot be negative.");
        }

        private static string NormalizeCustomerType(string? value)
        {
            string type = (value ?? "Retail").Trim();

            if (type.Equals("Wholesale", StringComparison.OrdinalIgnoreCase))
                return "Wholesale";

            return "Retail";
        }

        private static string NormalizeCreditStatus(string? value)
        {
            string status = (value ?? "None").Trim();

            if (status.Equals("PendingApproval", StringComparison.OrdinalIgnoreCase))
                return "PendingApproval";

            if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                return "Active";

            if (status.Equals("Hold", StringComparison.OrdinalIgnoreCase))
                return "Hold";

            return "None";
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