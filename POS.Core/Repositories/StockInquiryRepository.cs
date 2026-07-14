using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models.DTOs;

namespace POS.Core.Repositories
{
    public sealed class StockInquiryRepository
    {
        private const int MaximumResults = 100;

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public StockInquiryRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IReadOnlyList<StockInquiryResultDto>> SearchAsync(string? searchText)
        {
            string search = (searchText ?? string.Empty).Trim().ToUpperInvariant();

            await using AppDbContext context =
                await _contextFactory.CreateDbContextAsync();

            var query = context.ItemVariants
                .AsNoTracking()
                .Where(variant =>
                    !variant.IsDeactivated &&
                    !variant.ItemParent.IsDeactivated &&
                    variant.ItemParent.ItemType == ItemTypeCodes.StockItem);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(variant =>
                    variant.SkuCode.ToUpper().Contains(search) ||
                    variant.Barcode.ToUpper().Contains(search) ||
                    variant.ItemParent.ItemCode.ToUpper().Contains(search) ||
                    variant.ItemParent.ItemName.ToUpper().Contains(search) ||
                    variant.VariantDescription.ToUpper().Contains(search));
            }

            var variants = await query
                .OrderBy(variant => variant.ItemParent.ItemName)
                .ThenBy(variant => variant.VariantDescription)
                .ThenBy(variant => variant.SkuCode)
                .Take(MaximumResults)
                .Select(variant => new
                {
                    variant.Id,
                    variant.ItemParent.ItemCode,
                    variant.SkuCode,
                    variant.Barcode,
                    variant.ItemParent.ItemName,
                    variant.VariantDescription
                })
                .ToListAsync();

            if (variants.Count == 0)
                return Array.Empty<StockInquiryResultDto>();

            int[] variantIds = variants
                .Select(variant => variant.Id)
                .ToArray();

            var batchRows = await context.ItemBatches
                .AsNoTracking()
                .Where(batch =>
                    variantIds.Contains(batch.ItemVariantId) &&
                    !batch.IsDeactivated)
                .Select(batch => new
                {
                    batch.ItemVariantId,
                    batch.CurrentStock
                })
                .ToListAsync();

            Dictionary<int, decimal> stockByVariant = batchRows
                .GroupBy(batch => batch.ItemVariantId)
                .ToDictionary(
                    group => group.Key,
                    group => Math.Round(group.Sum(batch => batch.CurrentStock), 3));

            return variants
                .Select(variant => new StockInquiryResultDto
                {
                    ItemVariantId = variant.Id,
                    ItemCode = variant.ItemCode,
                    SkuCode = variant.SkuCode,
                    Barcode = variant.Barcode ?? string.Empty,
                    ItemName = variant.ItemName,
                    VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                        ? "Standard"
                        : variant.VariantDescription,
                    CurrentStock = stockByVariant.TryGetValue(variant.Id, out decimal stock)
                        ? stock
                        : 0m
                })
                .ToList();
        }
    }
}
