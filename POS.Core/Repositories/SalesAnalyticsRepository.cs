using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Core.Repositories
{
    public sealed class SalesAnalyticsRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SalesAnalyticsRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<ItemSalesAnalyticsResultDto> GetAnalyticsAsync(
            DateTime startDate,
            DateTime endDate,
            string searchText,
            int slowMovingDays = 90)
        {
            if (startDate.Date > endDate.Date)
                throw new ArgumentException("Start date cannot be later than end date.");

            DateTime start = startDate.Date;
            DateTime endExclusive = endDate.Date.AddDays(1);
            DateTime slowThreshold = endDate.Date.AddDays(-Math.Max(1, slowMovingDays));
            string search = Normalize(searchText);

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            var variants = await context.ItemVariants
                .AsNoTracking()
                .Where(variant => !variant.IsDeactivated && !variant.ItemParent.IsDeactivated)
                .Select(variant => new
                {
                    variant.Id,
                    variant.SkuCode,
                    variant.VariantDescription,
                    ItemCode = variant.ItemParent.ItemCode,
                    ParentName = variant.ItemParent.ItemName,
                    ItemType = variant.ItemParent.ItemType,
                    CategoryName = variant.ItemParent.Category.CategoryName
                })
                .ToListAsync();

            var stockRows = await context.ItemBatches
                .AsNoTracking()
                .Where(batch => !batch.IsDeactivated)
                .Select(batch => new
                {
                    batch.ItemVariantId,
                    batch.CurrentStock
                })
                .ToListAsync();

            var stockByVariant = stockRows
                .GroupBy(row => row.ItemVariantId)
                .ToDictionary(group => group.Key, group => group.Sum(row => row.CurrentStock));

            var sales = await context.SalesLines
                .AsNoTracking()
                .Where(line =>
                    line.ItemVariantId.HasValue &&
                    line.SalesHeader.Status == "Completed" &&
                    !line.SalesHeader.IsVoided &&
                    line.SalesHeader.TransactionDate >= start &&
                    line.SalesHeader.TransactionDate < endExclusive)
                .Select(line => new
                {
                    ItemVariantId = line.ItemVariantId!.Value,
                    line.Quantity,
                    line.GrossAmount,
                    line.DiscountAmount,
                    line.LineTotal,
                    line.CostPrice,
                    line.SalesHeader.TransactionDate,
                    line.SalesHeader.InvoiceNo,
                    line.SalesHeader.CustomerName
                })
                .ToListAsync();

            var returns = await context.CustomerReturnLines
                .AsNoTracking()
                .Where(line =>
                    line.SalesLineId.HasValue &&
                    line.SalesLine != null &&
                    line.SalesLine.ItemVariantId.HasValue &&
                    line.CustomerReturnHeader != null &&
                    line.CustomerReturnHeader.ReturnDate >= start &&
                    line.CustomerReturnHeader.ReturnDate < endExclusive)
                .Select(line => new
                {
                    ItemVariantId = line.SalesLine!.ItemVariantId!.Value,
                    line.QuantityReturned,
                    line.LineTotalRefund,
                    CostPrice = line.SalesLine.CostPrice,
                    line.CustomerReturnHeader!.ReturnDate,
                    DocumentNo = line.CustomerReturnHeader.CreditNoteNo ?? line.CustomerReturnHeader.ReturnNo,
                    CustomerName = line.CustomerReturnHeader.OriginalSalesHeader == null
                        ? "Walk-In"
                        : line.CustomerReturnHeader.OriginalSalesHeader.CustomerName
                })
                .ToListAsync();

            var lastSales = await context.SalesLines
                .AsNoTracking()
                .Where(line =>
                    line.ItemVariantId.HasValue &&
                    line.SalesHeader.Status == "Completed" &&
                    !line.SalesHeader.IsVoided &&
                    line.SalesHeader.TransactionDate < endExclusive)
                .GroupBy(line => line.ItemVariantId!.Value)
                .Select(group => new
                {
                    ItemVariantId = group.Key,
                    LastSaleDate = group.Max(line => line.SalesHeader.TransactionDate)
                })
                .ToListAsync();

            var salesByVariant = sales
                .GroupBy(row => row.ItemVariantId)
                .ToDictionary(group => group.Key, group => group.ToList());
            var returnsByVariant = returns
                .GroupBy(row => row.ItemVariantId)
                .ToDictionary(group => group.Key, group => group.ToList());
            var lastSaleByVariant = lastSales
                .ToDictionary(row => row.ItemVariantId, row => (DateTime?)row.LastSaleDate);

            var items = new List<ItemPerformanceDto>();

            foreach (var variant in variants)
            {
                salesByVariant.TryGetValue(variant.Id, out var itemSales);
                returnsByVariant.TryGetValue(variant.Id, out var itemReturns);
                lastSaleByVariant.TryGetValue(variant.Id, out DateTime? lastSaleDate);

                itemSales ??= new();
                itemReturns ??= new();

                string itemName = BuildDisplayName(
                    variant.ParentName,
                    variant.VariantDescription,
                    variant.SkuCode);

                stockByVariant.TryGetValue(variant.Id, out decimal currentStock);

                var row = new ItemPerformanceDto
                {
                    ItemVariantId = variant.Id,
                    ItemCode = variant.ItemCode,
                    SkuCode = variant.SkuCode,
                    ItemName = itemName,
                    ItemType = variant.ItemType,
                    CategoryName = variant.CategoryName,
                    CurrentStock = string.Equals(variant.ItemType, ItemTypeCodes.Service, StringComparison.Ordinal)
                        ? null
                        : RoundQuantity(currentStock),
                    SoldQuantity = RoundQuantity(itemSales.Sum(item => item.Quantity)),
                    ReturnedQuantity = RoundQuantity(itemReturns.Sum(item => item.QuantityReturned)),
                    GrossSales = Money(itemSales.Sum(item => item.GrossAmount)),
                    Discounts = Money(itemSales.Sum(item => item.DiscountAmount)),
                    ReturnValue = Money(itemReturns.Sum(item => item.LineTotalRefund)),
                    SaleCost = Money(itemSales.Sum(item => item.CostPrice * item.Quantity)),
                    ReturnedCost = Money(itemReturns.Sum(item => item.CostPrice * item.QuantityReturned)),
                    LastSaleDate = lastSaleDate,
                    IsSlowOrNonSelling = !string.Equals(variant.ItemType, ItemTypeCodes.Service, StringComparison.Ordinal) &&
                        currentStock > 0m &&
                        (!lastSaleDate.HasValue || lastSaleDate.Value < slowThreshold)
                };

                if (!string.IsNullOrWhiteSpace(search) &&
                    !Contains(row.ItemCode, search) &&
                    !Contains(row.SkuCode, search) &&
                    !Contains(row.ItemName, search) &&
                    !Contains(row.CategoryName, search) &&
                    !Contains(row.ItemType, search))
                {
                    continue;
                }

                items.Add(row);
            }

            items = items
                .OrderByDescending(row => row.NetSales)
                .ThenByDescending(row => row.NetQuantity)
                .ThenBy(row => row.ItemName)
                .ToList();

            for (int index = 0; index < items.Count; index++)
                items[index].Rank = index + 1;

            return new ItemSalesAnalyticsResultDto
            {
                Items = items,
                Summary = new AnalyticsKpiDto
                {
                    GrossSales = Money(items.Sum(row => row.GrossSales)),
                    Discounts = Money(items.Sum(row => row.Discounts)),
                    ReturnValue = Money(items.Sum(row => row.ReturnValue)),
                    NetSales = Money(items.Sum(row => row.NetSales)),
                    NetCost = Money(items.Sum(row => row.NetCost)),
                    SoldQuantity = RoundQuantity(items.Sum(row => row.SoldQuantity)),
                    ReturnedQuantity = RoundQuantity(items.Sum(row => row.ReturnedQuantity)),
                    SellingItemCount = items.Count(row => row.SoldQuantity > 0m),
                    SlowOrNonSellingStockItemCount = items.Count(row => row.IsSlowOrNonSelling)
                }
            };
        }

        public async Task<List<ItemSalesTransactionDto>> GetItemTransactionsAsync(
            int itemVariantId,
            DateTime startDate,
            DateTime endDate)
        {
            if (itemVariantId <= 0)
                return new List<ItemSalesTransactionDto>();
            if (startDate.Date > endDate.Date)
                throw new ArgumentException("Start date cannot be later than end date.");

            DateTime start = startDate.Date;
            DateTime endExclusive = endDate.Date.AddDays(1);

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            var sales = await context.SalesLines
                .AsNoTracking()
                .Where(line =>
                    line.ItemVariantId == itemVariantId &&
                    line.SalesHeader.Status == "Completed" &&
                    !line.SalesHeader.IsVoided &&
                    line.SalesHeader.TransactionDate >= start &&
                    line.SalesHeader.TransactionDate < endExclusive)
                .Select(line => new ItemSalesTransactionDto
                {
                    TransactionDate = line.SalesHeader.TransactionDate,
                    TransactionType = line.IsFreeItem ? "Free Issue" : "Sale",
                    DocumentNo = line.SalesHeader.InvoiceNo,
                    CustomerName = line.SalesHeader.CustomerName,
                    Quantity = line.Quantity,
                    Value = line.LineTotal
                })
                .ToListAsync();

            var returns = await context.CustomerReturnLines
                .AsNoTracking()
                .Where(line =>
                    line.SalesLine != null &&
                    line.SalesLine.ItemVariantId == itemVariantId &&
                    line.CustomerReturnHeader != null &&
                    line.CustomerReturnHeader.ReturnDate >= start &&
                    line.CustomerReturnHeader.ReturnDate < endExclusive)
                .Select(line => new ItemSalesTransactionDto
                {
                    TransactionDate = line.CustomerReturnHeader!.ReturnDate,
                    TransactionType = "Customer Return",
                    DocumentNo = line.CustomerReturnHeader.CreditNoteNo ?? line.CustomerReturnHeader.ReturnNo,
                    CustomerName = line.CustomerReturnHeader.OriginalSalesHeader == null
                        ? "Walk-In"
                        : line.CustomerReturnHeader.OriginalSalesHeader.CustomerName,
                    Quantity = -line.QuantityReturned,
                    Value = -line.LineTotalRefund
                })
                .ToListAsync();

            return sales
                .Concat(returns)
                .OrderByDescending(row => row.TransactionDate)
                .ThenBy(row => row.DocumentNo)
                .ToList();
        }

        private static bool Contains(string value, string search) =>
            (value ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase);

        private static string BuildDisplayName(string parentName, string variantDescription, string fallback)
        {
            string parent = Normalize(parentName);
            string variant = Normalize(variantDescription);
            if (string.IsNullOrWhiteSpace(variant) ||
                variant.Equals("Standard", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(parent) ? Normalize(fallback) : parent;
            }

            return string.IsNullOrWhiteSpace(parent) ? variant : $"{parent} - {variant}";
        }

        private static string Normalize(string? value) => (value ?? string.Empty).Trim();
        private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        private static decimal RoundQuantity(decimal value) => decimal.Round(value, 3, MidpointRounding.AwayFromZero);
    }
}
