using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
// using POS.Core.Data; // Ensure this points to your AppDbContext

namespace POS.Core.Repositories
{
    public class CustomerAdminRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public CustomerAdminRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ==========================================
        // 1. DATA GRID POPULATION & FILTERING
        // ==========================================
        public async Task<List<CustomerMaster>> GetFilteredCustomersAsync(string filterType, string searchTerm)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Start with active customers
            var query = context.CustomerMasters.AsNoTracking().Where(c => c.IsActive);

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
        // 2. PROFILE SAVING & FINANCIAL UPDATES
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
                // We do not want to accidentally overwrite CurrentBalance when saving demographics,
                // so we fetch the existing record and update only the safe fields.
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

                // Financial Updates
                existing.CreditLimit = customer.CreditLimit;
                existing.CreditDays = customer.CreditDays;
                existing.IsCreditLocked = customer.IsCreditLocked;
            }

            await context.SaveChangesAsync();
            return customer;
        }

        public async Task<List<CustomerLedger>> GetCustomerLedgerAsync(int customerId, DateTime? startDate = null, DateTime? endDate = null)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.CustomerLedgers.AsNoTracking().Where(l => l.CustomerMasterId == customerId);

            // If a start date is provided, filter out older records
            if (startDate.HasValue)
                query = query.Where(l => l.TransactionDate >= startDate.Value.Date);

            // If an end date is provided, filter out newer records
            if (endDate.HasValue)
                query = query.Where(l => l.TransactionDate <= endDate.Value.Date.AddDays(1).AddTicks(-1));

            // Return the list sorted by date (newest first)
            return await query.OrderByDescending(l => l.TransactionDate).ToListAsync();
        }
    }
}