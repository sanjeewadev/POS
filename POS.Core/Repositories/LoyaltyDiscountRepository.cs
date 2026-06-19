using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
// using POS.Core.Data; // Ensure this points to your AppDbContext namespace

namespace POS.Core.Repositories
{
    public class LoyaltyDiscountRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public LoyaltyDiscountRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ==========================================
        // DISCOUNT PROFILE MANAGEMENT
        // ==========================================
        public async Task<List<LoyaltyDiscountProfile>> GetAllActiveProfilesAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.LoyaltyDiscountProfiles
                .Where(p => p.IsActive)
                .OrderBy(p => p.ProfileName)
                .ToListAsync();
        }

        public async Task<LoyaltyDiscountProfile> SaveProfileAsync(LoyaltyDiscountProfile profile)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            if (profile.Id == 0)
            {
                context.LoyaltyDiscountProfiles.Add(profile);
            }
            else
            {
                context.LoyaltyDiscountProfiles.Update(profile);
            }

            await context.SaveChangesAsync();
            return profile;
        }

        public async Task DeleteProfileAsync(int profileId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var profile = await context.LoyaltyDiscountProfiles.FindAsync(profileId);
            if (profile != null)
            {
                // Soft delete to preserve historical invoice data
                profile.IsActive = false;
                await context.SaveChangesAsync();
            }
        }

        // ==========================================
        // CUSTOMER ASSIGNMENT ENGINE
        // ==========================================
        public async Task<List<CustomerMaster>> SearchRetailCustomersAsync(string searchTerm)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.CustomerMasters
                .Include(c => c.LoyaltyDiscountProfile)
                .Where(c => c.CustomerType == "Retail" && c.IsActive);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerTerm = searchTerm.ToLower().Trim();
                query = query.Where(c => c.FullName.ToLower().Contains(lowerTerm) || c.Phone.Contains(lowerTerm) || c.CustomerCode.ToLower().Contains(lowerTerm));
            }

            // Return top 100 to prevent crashing the UI if the user searches for ""
            return await query.OrderBy(c => c.FullName).Take(100).ToListAsync();
        }

        public async Task AssignDiscountToCustomerAsync(int customerId, int? discountProfileId, DateTime? expiryDate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var customer = await context.CustomerMasters.FindAsync(customerId);

            if (customer == null) throw new Exception("Customer not found.");

            customer.LoyaltyDiscountProfileId = discountProfileId;
            customer.LoyaltyDiscountExpiryDate = expiryDate;

            await context.SaveChangesAsync();
        }
    }
}