using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Core.Repositories
{
    public class FreeItemClaimRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public FreeItemClaimRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =================================================================================
        // 1. CASHIER ACTION: Log a new free item at the till (Phase 2 uses this)
        // =================================================================================
        public async Task LogFreeItemAsync(FreeItemClaimLog claim)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            claim.Timestamp = DateTime.Now; // Enforce server-side timestamp security

            context.FreeItemClaims.Add(claim);
            await context.SaveChangesAsync();
        }

        // =================================================================================
        // 2. ADMIN ACTION: Fetch recoverable claims for supplier billing (Phase 3 uses this)
        // =================================================================================
        public async Task<List<FreeItemClaimLog>> GetRecoverableClaimsAsync(DateTime startDate, DateTime endDate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.FreeItemClaims
                .AsNoTracking()
                .Where(c => c.IsRecoverable == true && c.Timestamp >= startDate && c.Timestamp <= endDate)
                .OrderByDescending(c => c.Timestamp)
                .ToListAsync();
        }
    }
}