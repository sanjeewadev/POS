using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models.DTOs;

namespace POS.Core.Repositories
{
    public class BarcodePrinterRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public BarcodePrinterRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =========================================================
        // RECENT POSTED GRNS
        // =========================================================

        public async Task<List<BarcodeRecentGrnDto>> GetRecentPostedGrnsAsync(
            int daysBack = 30,
            int take = 50)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            DateTime fromDate = DateTime.Today.AddDays(-Math.Abs(daysBack));

            return await context.GrnHeaders
                .AsNoTracking()
                .Where(g =>
                    g.Status == "Posted" &&
                    g.ReceivedDate >= fromDate)
                .OrderByDescending(g => g.ReceivedDate)
                .ThenByDescending(g => g.GrnNumber)
                .Take(take)
                .Select(g => new BarcodeRecentGrnDto
                {
                    GrnHeaderId = g.Id,
                    GrnNumber = g.GrnNumber,
                    SupplierInvoiceNo = g.SupplierInvoiceNo,
                    ReceivedDate = g.ReceivedDate
                })
                .ToListAsync();
        }

        // =========================================================
        // LOAD PRINT QUEUE FROM GRN
        // =========================================================

        public async Task<List<BarcodePrintQueueItemDto>> GetPrintQueueItemsForGrnAsync(int grnHeaderId)
        {
            if (grnHeaderId <= 0)
                return new List<BarcodePrintQueueItemDto>();

            using var context = await _contextFactory.CreateDbContextAsync();

            var grn = await context.GrnHeaders
                .AsNoTracking()
                .FirstOrDefaultAsync(g =>
                    g.Id == grnHeaderId &&
                    g.Status == "Posted");

            if (grn == null)
                throw new InvalidOperationException("Posted GRN was not found.");

            var lines = await context.GrnLines
                .Include(l => l.ItemVariant)
                    .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .Where(l =>
                    l.GrnHeaderId == grnHeaderId &&
                    l.ReceivedQty > 0)
                .OrderBy(l => l.ItemVariant!.ItemParent!.ItemCode)
                .ThenBy(l => l.ItemVariant!.VariantDescription)
                .ToListAsync();

            var result = new List<BarcodePrintQueueItemDto>();

            foreach (var line in lines)
            {
                var variant = line.ItemVariant;

                if (variant == null || variant.ItemParent == null)
                    continue;

                int printQty = ConvertReceivedQtyToLabelQty(line.ReceivedQty);

                if (printQty <= 0)
                    continue;

                var existing = result.FirstOrDefault(x => x.ItemVariantId == variant.Id);

                if (existing != null)
                {
                    existing.PrintQuantity += printQty;
                    continue;
                }

                result.Add(new BarcodePrintQueueItemDto
                {
                    ItemVariantId = variant.Id,
                    GrnHeaderId = grn.Id,
                    GrnLineId = line.Id,
                    SourceDocument = grn.GrnNumber,

                    ItemCode = variant.ItemParent.ItemCode,
                    SkuCode = variant.SkuCode,
                    ItemName = variant.ItemParent.ItemName,
                    VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                        ? "Standard"
                        : variant.VariantDescription,

                    Barcode = variant.Barcode ?? string.Empty,
                    Price = variant.RetailPrice,
                    PrintQuantity = printQty
                });
            }

            return result;
        }

        // =========================================================
        // MANUAL SEARCH FOR PRINTING
        // =========================================================

        public async Task<BarcodePrintQueueItemDto?> FindItemForPrintingAsync(string searchText)
        {
            string search = NormalizeText(searchText);

            if (string.IsNullOrWhiteSpace(search))
                return null;

            string upperSearch = search.ToUpperInvariant();

            using var context = await _contextFactory.CreateDbContextAsync();

            var variant = await context.ItemVariants
                .Include(v => v.ItemParent)
                .AsNoTracking()
                .Where(v =>
                    !v.IsDeactivated &&
                    !v.ItemParent.IsDeactivated &&
                    (
                        v.Barcode.ToUpper() == upperSearch ||
                        v.SkuCode.ToUpper() == upperSearch ||
                        v.ItemParent.ItemCode.ToUpper() == upperSearch
                    ))
                .OrderBy(v => v.ItemParent.ItemCode)
                .ThenBy(v => v.VariantDescription)
                .FirstOrDefaultAsync();

            if (variant == null)
                return null;

            return new BarcodePrintQueueItemDto
            {
                ItemVariantId = variant.Id,
                SourceDocument = "MANUAL",

                ItemCode = variant.ItemParent.ItemCode,
                SkuCode = variant.SkuCode,
                ItemName = variant.ItemParent.ItemName,
                VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                    ? "Standard"
                    : variant.VariantDescription,

                Barcode = variant.Barcode ?? string.Empty,
                Price = variant.RetailPrice,
                PrintQuantity = 1
            };
        }

        // =========================================================
        // QUEUE VALIDATION
        // =========================================================

        public static List<string> ValidatePrintQueue(List<BarcodePrintQueueItemDto> queue)
        {
            var errors = new List<string>();

            if (queue == null || !queue.Any())
            {
                errors.Add("Print queue is empty.");
                return errors;
            }

            foreach (var item in queue)
            {
                if (item.ItemVariantId <= 0)
                    errors.Add($"Invalid item variant in queue: {item.DisplayName}");

                if (string.IsNullOrWhiteSpace(item.Barcode))
                    errors.Add($"Missing barcode for item: {item.ItemCode} - {item.DisplayName}");

                if (item.PrintQuantity <= 0)
                    errors.Add($"Print quantity must be greater than zero for item: {item.ItemCode} - {item.DisplayName}");

                if (item.PrintQuantity > 5000)
                    errors.Add($"Print quantity is too high for item: {item.ItemCode} - {item.DisplayName}");
            }

            return errors;
        }

        private static int ConvertReceivedQtyToLabelQty(decimal receivedQty)
        {
            if (receivedQty <= 0)
                return 0;

            // Label quantity must be a whole number.
            // For decimal-stock items, round up to avoid under-printing.
            return (int)Math.Ceiling(receivedQty);
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}