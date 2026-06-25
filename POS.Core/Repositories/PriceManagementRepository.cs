using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    public class PriceManagementRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public PriceManagementRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ==============================================================================
        // --- MASTER GRID ENGINE (With Exception Filtering) ---
        // ==============================================================================
        public async Task<List<PriceManagementSummaryDto>> GetPricingSummariesAsync(
            string marginFilter = "All",
            string expiryFilter = "All",
            string searchText = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.ItemVariants
                .Include(v => v.ItemParent)
                .Include(v => v.ItemBatches)
                .AsNoTracking()
                .AsQueryable();

            // 1. Text Search
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var search = searchText.ToLower();
                query = query.Where(v =>
                    v.ItemParent.ItemCode.ToLower().Contains(search) ||
                    v.ItemParent.ItemName.ToLower().Contains(search) ||
                    v.Barcode.ToLower().Contains(search));
            }

            // 2. Expiry Alerts Filter
            if (expiryFilter == "Expiring Soon")
            {
                // Finds any variant that has stock expiring within the next 30 days
                var thresholdDate = DateTime.Now.AddDays(30);
                query = query.Where(v => v.ItemBatches.Any(b => b.CurrentStock > 0 && b.ExpiryDate <= thresholdDate));
            }

            // Execute query and project to DTO
            var results = await query.Select(v => new PriceManagementSummaryDto
            {
                ItemVariantId = v.Id,
                ItemCode = v.ItemParent.ItemCode,
                Description = v.ItemParent.ItemName,
                VariantAttributes = v.VariantDescription,

                // Aggregate active stock across all batch buckets
                TotalSoh = v.ItemBatches.Where(b => b.CurrentStock > 0).Sum(b => b.CurrentStock),

                MovingAverageCost = v.AverageCost,
                LastLandedCost = v.CostPrice, // Updated by the GRN engine

                RetailPrice = v.RetailPrice,
                WholesalePrice = v.WholesalePrice,
                MinimumPrice = v.MinimumPrice
            }).ToListAsync();

            // 3. Margin Alerts Filter (Evaluated in memory via the DTO property to prevent SQL division errors)
            if (marginFilter == "Low Margin Alerts (< 20%)")
            {
                results = results.Where(r => r.GrossMarginPercentage < 20m).ToList();
            }
            else if (marginFilter == "Healthy Margins")
            {
                results = results.Where(r => r.GrossMarginPercentage >= 20m).ToList();
            }

            return results.OrderBy(r => r.Description).ToList();
        }

        // ==============================================================================
        // --- BATCH INSPECTOR ENGINE ---
        // ==============================================================================
        public async Task<List<ItemBatch>> GetActiveBatchesAsync(int itemVariantId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.ItemBatches
                .Where(b => b.ItemVariantId == itemVariantId && b.CurrentStock > 0)
                .OrderBy(b => b.ExpiryDate)
                .AsNoTracking() // We use NoTracking because we will manually update them later
                .ToListAsync();
        }

        // ==============================================================================
        // --- ATOMIC SAVE ENGINE ---
        // ==============================================================================
        public async Task UpdatePricingAsync(PriceManagementSummaryDto globalPricing, List<ItemBatch> batchOverrides)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // 1. Update the Global Master Prices
                var variant = await context.ItemVariants.FindAsync(globalPricing.ItemVariantId);
                if (variant != null)
                {
                    variant.RetailPrice = globalPricing.RetailPrice;
                    variant.WholesalePrice = globalPricing.WholesalePrice;
                    variant.MinimumPrice = globalPricing.MinimumPrice;

                    context.ItemVariants.Update(variant);
                }

                // 2. Update Specific Batch Overrides (Clearance pricing)
                foreach (var batch in batchOverrides)
                {
                    var existingBatch = await context.ItemBatches.FindAsync(batch.Id);
                    if (existingBatch != null)
                    {
                        // We only allow overriding the selling prices here, never the logistics (Cost/Qty/Expiry)
                        existingBatch.RetailPrice = batch.RetailPrice;
                        existingBatch.WholesalePrice = batch.WholesalePrice;

                        context.ItemBatches.Update(existingBatch);
                    }
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}