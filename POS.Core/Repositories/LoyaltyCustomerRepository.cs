using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
// using POS.Core.Data; // Ensure this points to your AppDbContext namespace

namespace POS.Core.Repositories
{
    // The lightweight data object that perfectly matches your DataGrid columns
    public class LoyaltyCustomerDto
    {
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public DateTime? Birthday { get; set; }
        public string Email { get; set; } = string.Empty;

        // These will map to your manual discount assignments later
        public string ActiveDiscountName { get; set; } = "None";
        public string DiscountScope { get; set; } = "-";
        public DateTime? DiscountExpiry { get; set; }
    }

    public class LoyaltyCustomerRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public LoyaltyCustomerRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // 1. POPULATE THE DATAGRID (Search by Name or Phone)
        public async Task<List<LoyaltyCustomerDto>> SearchLoyaltyCustomersAsync(string searchTerm)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.CustomerMasters.AsNoTracking().Where(c => c.IsActive && c.CustomerType == "Retail");

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerTerm = searchTerm.ToLower().Trim();
                query = query.Where(c => c.Phone.Contains(lowerTerm) || c.FullName.ToLower().Contains(lowerTerm));
            }

            // Fetch top 50 to keep the POS terminal lightning fast
            var customers = await query
                .OrderByDescending(c => c.Id)
                .Take(50)
                .Select(c => new LoyaltyCustomerDto
                {
                    CustomerId = c.Id,
                    CustomerCode = c.CustomerCode,
                    FullName = c.FullName,
                    Phone = c.Phone ?? string.Empty,
                    Email = c.Email ?? string.Empty,
                    // Note: You will need to add Birthday to CustomerMaster.cs if you haven't already!
                    // Birthday = c.Birthday, 

                    // Placeholders until you link the manual discount tables
                    ActiveDiscountName = "No Active Discount",
                    DiscountScope = "-",
                    DiscountExpiry = null
                })
                .ToListAsync();

            return customers;
        }

        // 2. REGISTER A NEW WALK-IN
        public async Task<LoyaltyCustomerDto> RegisterLoyaltyCustomerAsync(string fullName, string phone, string email, DateTime? birthday)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Hard validation to prevent duplicate accounts
            bool exists = await context.CustomerMasters.AnyAsync(c => c.Phone == phone);
            if (exists) throw new InvalidOperationException($"Phone number {phone} is already registered.");

            int totalCustomers = await context.CustomerMasters.CountAsync();
            string newCustomerCode = $"CUST-{10000 + totalCustomers + 1}";

            var newCustomer = new CustomerMaster
            {
                CustomerCode = newCustomerCode,
                FullName = fullName.Trim(),
                Phone = phone.Trim(),
                Email = email?.Trim() ?? string.Empty,
                CustomerType = "Retail",
                LoyaltyPointsBalance = 0m,
                CreditLimit = 0m,
                IsActive = true
            };

            context.CustomerMasters.Add(newCustomer);
            await context.SaveChangesAsync();

            return new LoyaltyCustomerDto
            {
                CustomerId = newCustomer.Id,
                CustomerCode = newCustomer.CustomerCode,
                FullName = newCustomer.FullName,
                Phone = newCustomer.Phone,
                Email = newCustomer.Email,
                Birthday = null, // Update if Birthday is added to CustomerMaster
                ActiveDiscountName = "None",
                DiscountScope = "-"
            };
        }

        // ==========================================
        // WHOLESALE / B2B LOOKUP FOR CASHIER
        // ==========================================
        public async Task<List<WholesaleCustomerDto>> SearchWholesaleCustomersAsync(string searchTerm)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Only pull Wholesale accounts that are active
            var query = context.CustomerMasters
                .Include(c => c.LoyaltyDiscountProfile)
                .AsNoTracking()
                .Where(c => c.IsActive && c.CustomerType == "Wholesale");

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerTerm = searchTerm.ToLower().Trim();
                query = query.Where(c =>
                    c.Phone.Contains(lowerTerm) ||
                    c.FullName.ToLower().Contains(lowerTerm) ||
                    (!string.IsNullOrWhiteSpace(c.CompanyName) && c.CompanyName.ToLower().Contains(lowerTerm)) ||
                    (!string.IsNullOrWhiteSpace(c.VatRegistrationNumber) && c.VatRegistrationNumber.ToLower().Contains(lowerTerm))
                );
            }

            return await query
                .OrderBy(c => c.CompanyName)
                .Take(50)
                .Select(c => new WholesaleCustomerDto
                {
                    CustomerId = c.Id,
                    CustomerCode = c.CustomerCode,
                    FullName = c.FullName,
                    CompanyName = c.CompanyName ?? "N/A",
                    CreditLimit = c.CreditLimit,
                    CurrentBalance = c.CurrentBalance,
                    IsCreditLocked = c.IsCreditLocked,
                    IsActive = c.IsActive,
                    ActiveDiscountName = c.LoyaltyDiscountProfile != null ? c.LoyaltyDiscountProfile.ProfileName : "None",
                    DiscountExpiry = c.LoyaltyDiscountExpiryDate
                })
                .ToListAsync();
        }
    }
}