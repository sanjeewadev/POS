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
    // Renamed from CustomerAdminRepository to be the unified Hub for both Cashier and Admin
    public class CustomerRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public CustomerRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ==========================================
        // 1. CASHIER ENGINE: LIGHTNING FAST SEARCH
        // ==========================================
        public async Task<List<CustomerSearchDto>> SearchB2BCustomersAsync(string searchTerm)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Only search Active customers for the POS terminal
            var query = context.CustomerMasters.AsNoTracking().Where(c => c.IsActive);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.ToLower().Trim();
                query = query.Where(c =>
                    c.FullName.ToLower().Contains(term) ||
                    c.Phone.Contains(term) ||
                    c.CustomerCode.ToLower().Contains(term) ||
                    (!string.IsNullOrWhiteSpace(c.CompanyName) && c.CompanyName.ToLower().Contains(term))
                );
            }

            // Map to the lightweight DTO and limit to 50 results to prevent UI lag
            return await query.Take(50).Select(c => new CustomerSearchDto
            {
                Id = c.Id,
                CustomerCode = c.CustomerCode,
                FullName = c.FullName,
                CompanyName = c.CompanyName ?? string.Empty,
                Phone = c.Phone,
                CustomerType = c.CustomerType,
                CreditLimit = c.CreditLimit,
                CurrentBalance = c.CurrentBalance,
                IsCreditLocked = c.IsCreditLocked,
                LoyaltyDiscountProfileId = c.LoyaltyDiscountProfileId
            }).ToListAsync();
        }

        // ==========================================
        // 2. ADMIN ENGINE: GRID POPULATION & FILTERING
        // ==========================================
        public async Task<List<CustomerMaster>> GetFilteredCustomersAsync(string filterType, string searchTerm)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.CustomerMasters.AsNoTracking().AsQueryable();

            // Apply Type Filter
            if (filterType == "Retail")
                query = query.Where(c => c.CustomerType == "Retail");
            else if (filterType == "Wholesale")
                query = query.Where(c => c.CustomerType == "Wholesale");

            // Apply Search Keyword
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.ToLower().Trim();
                query = query.Where(c =>
                    c.FullName.ToLower().Contains(term) ||
                    c.Phone.Contains(term) ||
                    c.CustomerCode.ToLower().Contains(term) ||
                    (!string.IsNullOrWhiteSpace(c.CompanyName) && c.CompanyName.ToLower().Contains(term)) ||
                    (!string.IsNullOrWhiteSpace(c.VatRegistrationNumber) && c.VatRegistrationNumber.ToLower().Contains(term))
                );
            }

            return await query.OrderBy(c => c.FullName).ToListAsync();
        }

        // ==========================================
        // 3. ADMIN ENGINE: PROFILE SAVING 
        // ==========================================
        public async Task<CustomerMaster> SaveCustomerProfileAsync(CustomerMaster customer)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            if (customer.Id == 0)
            {
                // Auto-generate Customer Code for new entries
                if (string.IsNullOrWhiteSpace(customer.CustomerCode))
                {
                    int totalCustomers = await context.CustomerMasters.CountAsync();
                    customer.CustomerCode = $"CUST-{10000 + totalCustomers + 1}";
                }

                context.CustomerMasters.Add(customer);
            }
            else
            {
                var existing = await context.CustomerMasters.FindAsync(customer.Id);
                if (existing == null) throw new Exception("Customer record not found in database.");

                existing.FullName = customer.FullName;
                existing.Phone = customer.Phone;
                existing.Email = customer.Email;
                existing.Address = customer.Address;
                existing.CompanyName = customer.CompanyName;
                existing.VatRegistrationNumber = customer.VatRegistrationNumber;
                existing.CustomerType = customer.CustomerType;
                existing.CustomerGroupId = customer.CustomerGroupId;
                existing.IsActive = customer.IsActive;

                // BUG FIX: Actually save the Loyalty/Discount Profile ID!
                existing.LoyaltyDiscountProfileId = customer.LoyaltyDiscountProfileId;

                // Financial Updates
                existing.CreditLimit = customer.CreditLimit;
                existing.CreditDays = customer.CreditDays;
                existing.IsCreditLocked = customer.IsCreditLocked;
            }

            await context.SaveChangesAsync();
            return customer;
        }

        // ==========================================
        // 4. ADMIN ENGINE: LEDGER HISTORY
        // ==========================================
        public async Task<List<CustomerLedger>> GetCustomerLedgerAsync(int customerId, DateTime? startDate = null, DateTime? endDate = null)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.CustomerLedgers.AsNoTracking().Where(l => l.CustomerMasterId == customerId);

            if (startDate.HasValue)
                query = query.Where(l => l.TransactionDate >= startDate.Value.Date);

            if (endDate.HasValue)
                query = query.Where(l => l.TransactionDate <= endDate.Value.Date.AddDays(1).AddTicks(-1));

            return await query.OrderByDescending(l => l.TransactionDate).ToListAsync();
        }
    }
}