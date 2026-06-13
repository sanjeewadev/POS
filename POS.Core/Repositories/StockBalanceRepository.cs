using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models.DTOs;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    public class StockBalanceRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public StockBalanceRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IEnumerable<StockBalanceDto>> GetStockBalancesAsync(
            string searchText = "",
            int? categoryId = null,
            int? supplierId = null,
            bool hideZeroStock = false,
            bool showNegativeOnly = false)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Start with the Variants and JOIN up to the Parent to get Categories and Base UOM
            var query = context.ItemVariants
                .Include(v => v.ItemParent)
                .AsNoTracking()
                .AsQueryable();

            // --- 1. APPLY FILTERS ---

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var lowerSearch = searchText.ToLower();
                query = query.Where(v =>
                    v.SkuCode.ToLower().Contains(lowerSearch) ||
                    v.ItemParent.ItemName.ToLower().Contains(lowerSearch) ||
                    v.ItemParent.ItemCode.ToLower().Contains(lowerSearch));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(v => v.ItemParent.CategoryId == categoryId.Value);
            }

            // Note: If you want to filter by Supplier, you need a default/primary SupplierId on the ItemParent model.
            // If you added that to your DB schema, uncomment this:
            // if (supplierId.HasValue && supplierId.Value > 0)
            // {
            //     query = query.Where(v => v.ItemParent.PrimarySupplierId == supplierId.Value);
            // }

            if (showNegativeOnly)
            {
                query = query.Where(v => v.TotalStockOnHand < 0);
            }
            else if (hideZeroStock)
            {
                query = query.Where(v => v.TotalStockOnHand != 0);
            }

            // --- 2. PROJECT DIRECTLY TO DTO (SQL-Side Math) ---

            var result = await query.Select(v => new StockBalanceDto
            {
                VariantId = v.Id,
                ItemCode = v.ItemParent.ItemCode,
                VariantDescription = v.VariantDescription,
                Description = v.ItemParent.ItemName,
                Uom = v.ItemParent.BaseUom,
                QtyOnHand = v.TotalStockOnHand,

                UnitCost = v.CostPrice,
                TotalCostValue = v.TotalStockOnHand * v.CostPrice, // SQL handles this math

                UnitRetail = v.RetailPrice,
                TotalRetailValue = v.TotalStockOnHand * v.RetailPrice // SQL handles this math
            }).ToListAsync();

            return result;
        }
    }
}