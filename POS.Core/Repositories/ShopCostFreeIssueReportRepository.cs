using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    public class ShopCostFreeIssueDto
    {
        public int SalesLineId { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemDescription { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string AppliedBy { get; set; } = string.Empty;
        public string? ApprovedBy { get; set; }
    }

    public class ShopCostFreeIssueReportRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public ShopCostFreeIssueReportRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<List<ShopCostFreeIssueDto>> GetReportAsync(DateTime fromDate, DateTime toDate)
        {
            var from = fromDate.Date;
            var to = toDate.Date.AddDays(1);

            await using var context = await _contextFactory.CreateDbContextAsync();

            var results = await context.SalesLines
                .Include(sl => sl.SalesHeader)
                .Where(sl =>
                    sl.SalesHeader.TransactionDate >= from &&
                    sl.SalesHeader.TransactionDate < to &&
                    sl.IsFreeItem &&
                    sl.FreeIssueRuleSnapshotJson != null &&
                    sl.FreeIssueRuleSnapshotJson.Contains($"\"FreeIssueType\":\"{FreeIssueTypeCodes.ShopCost}\""))
                .OrderByDescending(sl => sl.SalesHeader.TransactionDate)
                .ThenByDescending(sl => sl.SalesHeader.InvoiceNo)
                .Take(2000)
                .Select(sl => new ShopCostFreeIssueDto
                {
                    SalesLineId = sl.Id,
                    InvoiceNo = sl.SalesHeader.InvoiceNo,
                    TransactionDate = sl.SalesHeader.TransactionDate,

                    ItemCode = sl.SkuCode,
                    ItemDescription = sl.ItemDescription,
                    Quantity = sl.Quantity,
                    UnitCost = sl.CostPrice,
                    TotalCost = sl.Quantity * sl.CostPrice,
                    AppliedBy = sl.FreeIssueAppliedBy ?? "Unknown",
                    ApprovedBy = sl.FreeApprovedBy,

                    Reason = sl.FreeIssueRuleSnapshotJson ?? string.Empty
                })
                .ToListAsync();

            foreach (var item in results)
            {
                item.Reason = TryGetReasonFromJson(item.Reason);
            }

            return results;
        }

        private string TryGetReasonFromJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return "Unknown";
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("ReasonName", out var reasonProperty))
                {
                    return reasonProperty.GetString() ?? "N/A";
                }
            }
            catch (JsonException)
            {
                // Ignore if JSON is malformed
            }

            return "Legacy/Error";
        }
    }
}