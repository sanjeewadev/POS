using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public class PriceChangeHistoryRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public PriceChangeHistoryRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =========================================================
        // PRICE CHANGE HISTORY SEARCH
        // =========================================================

        public async Task<List<PriceChangeHistory>> GetPriceChangeHistoryAsync(
            string searchText = "",
            string priceLevel = "All",
            string changeSource = "All",
            string changedBy = "All",
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            int maxRows = 500)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            string search = NormalizeText(searchText).ToUpperInvariant();
            string level = NormalizeText(priceLevel);
            string source = NormalizeText(changeSource);
            string user = NormalizeText(changedBy);

            if (maxRows <= 0)
                maxRows = 500;

            if (maxRows > 5000)
                maxRows = 5000;

            var query = context.PriceChangeHistories
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(p =>
                    p.PriceChangeNo.ToUpper().Contains(search) ||
                    p.ItemCode.ToUpper().Contains(search) ||
                    p.SkuCode.ToUpper().Contains(search) ||
                    p.Barcode.ToUpper().Contains(search) ||
                    p.ItemDescription.ToUpper().Contains(search) ||
                    p.VariantDescription.ToUpper().Contains(search) ||
                    p.BatchNo.ToUpper().Contains(search) ||
                    p.ChangeReason.ToUpper().Contains(search) ||
                    p.Remarks.ToUpper().Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(level) &&
                !level.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.PriceLevel == level);
            }

            if (!string.IsNullOrWhiteSpace(source) &&
                !source.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.ChangeSource == source);
            }

            if (!string.IsNullOrWhiteSpace(user) &&
                !user.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.ChangedBy == user);
            }

            if (dateFrom.HasValue)
            {
                DateTime from = dateFrom.Value.Date;
                query = query.Where(p => p.ChangedAt >= from);
            }

            if (dateTo.HasValue)
            {
                DateTime toExclusive = dateTo.Value.Date.AddDays(1);
                query = query.Where(p => p.ChangedAt < toExclusive);
            }

            return await query
                .OrderByDescending(p => p.ChangedAt)
                .ThenByDescending(p => p.Id)
                .Take(maxRows)
                .ToListAsync();
        }

        // =========================================================
        // FILTER LOOKUPS
        // =========================================================

        public async Task<List<string>> GetChangeSourcesAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.PriceChangeHistories
                .AsNoTracking()
                .Where(p => p.ChangeSource != "")
                .Select(p => p.ChangeSource)
                .Distinct()
                .OrderBy(p => p)
                .ToListAsync();
        }

        public async Task<List<string>> GetChangedByUsersAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.PriceChangeHistories
                .AsNoTracking()
                .Where(p => p.ChangedBy != "")
                .Select(p => p.ChangedBy)
                .Distinct()
                .OrderBy(p => p)
                .ToListAsync();
        }

        // =========================================================
        // SINGLE DOCUMENT LOOKUP
        // =========================================================

        public async Task<List<PriceChangeHistory>> GetByPriceChangeNoAsync(string priceChangeNo)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            string docNo = NormalizeText(priceChangeNo);

            if (string.IsNullOrWhiteSpace(docNo))
                return new List<PriceChangeHistory>();

            return await context.PriceChangeHistories
                .AsNoTracking()
                .Where(p => p.PriceChangeNo == docNo)
                .OrderBy(p => p.PriceLevel)
                .ThenBy(p => p.ItemCode)
                .ThenBy(p => p.BatchNo)
                .ThenBy(p => p.Id)
                .ToListAsync();
        }

        // =========================================================
        // DASHBOARD / SUMMARY HELPERS
        // =========================================================

        public async Task<int> GetTodayChangeCountAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);

            return await context.PriceChangeHistories
                .AsNoTracking()
                .CountAsync(p =>
                    p.ChangedAt >= today &&
                    p.ChangedAt < tomorrow);
        }

        public async Task<PriceChangeHistory?> GetLatestChangeAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.PriceChangeHistories
                .AsNoTracking()
                .OrderByDescending(p => p.ChangedAt)
                .ThenByDescending(p => p.Id)
                .FirstOrDefaultAsync();
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}