using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models.DTOs;
using POS.Core.Services.Pricing;

namespace POS.Core.Repositories
{
    public class BarcodePrinterRepository
    {
        private const string GeneralBatchNo = "GENERAL";

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public BarcodePrinterRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        // =========================================================
        // RECENT POSTED GRNS
        // =========================================================

        public async Task<List<BarcodeRecentGrnDto>> GetRecentPostedGrnsAsync(
            int daysBack = 30,
            int take = 50)
        {
            if (take <= 0)
                take = 50;

            if (take > 100)
                take = 100;

            await using var context = await _contextFactory.CreateDbContextAsync();

            DateTime fromDate = DateTime.Today.AddDays(-Math.Abs(daysBack));

            var grns = await context.GrnHeaders
                .AsNoTracking()
                .Where(g =>
                    g.Status == "Posted" &&
                    g.ReceivedDate >= fromDate)
                .OrderByDescending(g => g.ReceivedDate)
                .ThenByDescending(g => g.GrnNumber)
                .Take(take)
                .Select(g => new
                {
                    GrnHeaderId = g.Id,
                    g.GrnNumber,
                    g.SupplierInvoiceNo,
                    SupplierName = g.Supplier.SupplierName,
                    g.ReceivedDate
                })
                .ToListAsync();

            if (!grns.Any())
                return new List<BarcodeRecentGrnDto>();

            var grnIds = grns
                .Select(g => g.GrnHeaderId)
                .ToList();

            var batchLabelLines = await context.GrnLines
                .AsNoTracking()
                .Where(l =>
                    grnIds.Contains(l.GrnHeaderId) &&
                    l.ReceivedQty > 0 &&
                    l.ItemBatchId.HasValue &&
                    l.ItemBatch != null &&
                    l.ItemVariant.ItemParent.HasBatchTracking &&
                    l.ItemBatch.BatchNo.ToUpper() != GeneralBatchNo &&
                    l.ItemBatch.InternalBatchBarcode != null &&
                    l.ItemBatch.InternalBatchBarcode != "")
                .Select(l => new
                {
                    l.GrnHeaderId,
                    l.ReceivedQty
                })
                .ToListAsync();

            var results = new List<BarcodeRecentGrnDto>();

            foreach (var grn in grns)
            {
                var linesForGrn = batchLabelLines
                    .Where(l => l.GrnHeaderId == grn.GrnHeaderId)
                    .ToList();

                results.Add(new BarcodeRecentGrnDto
                {
                    GrnHeaderId = grn.GrnHeaderId,
                    GrnNumber = grn.GrnNumber,
                    SupplierInvoiceNo = grn.SupplierInvoiceNo,
                    SupplierName = grn.SupplierName,
                    ReceivedDate = grn.ReceivedDate,
                    BatchLabelLineCount = linesForGrn.Count,
                    TotalLabelsSuggested = linesForGrn.Sum(l => ConvertReceivedQtyToLabelQty(l.ReceivedQty))
                });
            }

            return results;
        }

        // =========================================================
        // LOAD GRN BATCH LABEL QUEUE
        // =========================================================

        public async Task<List<BarcodePrintQueueItemDto>> GetPrintQueueItemsForGrnAsync(int grnHeaderId)
        {
            if (grnHeaderId <= 0)
                return new List<BarcodePrintQueueItemDto>();

            await using var context = await _contextFactory.CreateDbContextAsync();

            var grn = await context.GrnHeaders
                .AsNoTracking()
                .Where(g =>
                    g.Id == grnHeaderId &&
                    g.Status == "Posted")
                .Select(g => new
                {
                    g.Id,
                    g.GrnNumber,
                    g.SupplierInvoiceNo,
                    g.ReceivedDate
                })
                .FirstOrDefaultAsync();

            if (grn == null)
                throw new InvalidOperationException("Posted GRN was not found.");

            var rows = await context.GrnLines
                .AsNoTracking()
                .Where(l =>
                    l.GrnHeaderId == grnHeaderId &&
                    l.ReceivedQty > 0 &&
                    l.ItemBatchId.HasValue &&
                    l.ItemBatch != null &&
                    l.ItemVariant.ItemParent.HasBatchTracking &&
                    l.ItemBatch.BatchNo.ToUpper() != GeneralBatchNo &&
                    l.ItemBatch.InternalBatchBarcode != null &&
                    l.ItemBatch.InternalBatchBarcode != "")
                .OrderBy(l => l.ItemVariant.ItemParent.ItemCode)
                .ThenBy(l => l.ItemVariant.VariantDescription)
                .ThenBy(l => l.ItemBatch!.ExpiryDate)
                .ThenBy(l => l.ItemBatch!.BatchNo)
                .Select(l => new
                {
                    GrnHeaderId = grn.Id,
                    GrnNumber = grn.GrnNumber,
                    GrnLineId = l.Id,

                    ItemVariantId = l.ItemVariantId,
                    ItemBatchId = l.ItemBatchId!.Value,

                    ItemCode = l.ItemVariant.ItemParent.ItemCode,
                    SkuCode = l.ItemVariant.SkuCode,
                    ItemName = l.ItemVariant.ItemParent.ItemName,
                    VariantDescription = string.IsNullOrWhiteSpace(l.ItemVariant.VariantDescription)
                        ? "Standard"
                        : l.ItemVariant.VariantDescription,
                    Uom = string.IsNullOrWhiteSpace(l.Uom)
                        ? "PCS"
                        : l.Uom,

                    BatchNo = l.ItemBatch!.BatchNo,
                    ExpiryDate = l.ItemBatch.ExpiryDate,
                    ReceivedDate = l.ItemBatch.ReceivedDate,
                    ReceivedQty = l.ReceivedQty,
                    AvailableQty = l.ItemBatch.CurrentStock,

                    InternalBatchBarcode = l.ItemBatch.InternalBatchBarcode ?? string.Empty,

                    CostPrice = l.ItemBatch.CostPrice,
                    BatchRetailPrice = l.ItemBatch.RetailPrice,
                    BatchWholesalePrice = l.ItemBatch.WholesalePrice,
                    HasSellingPriceOverride = l.ItemBatch.HasSellingPriceOverride,
                    VariantRetailPrice = l.ItemVariant.RetailPrice,
                    VariantWholesalePrice = l.ItemVariant.WholesalePrice,
                    ItemType = l.ItemVariant.ItemParent.ItemType,
                    HasBatchTracking = l.ItemVariant.ItemParent.HasBatchTracking,

                    BarcodePrintedCount = l.ItemBatch.BarcodePrintedCount,
                    LastBarcodePrintedAt = l.ItemBatch.LastBarcodePrintedAt,
                    LastBarcodePrintedBy = l.ItemBatch.LastBarcodePrintedBy
                })
                .ToListAsync();

            var result = new List<BarcodePrintQueueItemDto>();

            foreach (var row in rows)
            {
                EffectiveSellingPrice effectivePrice =
                    EffectiveSellingPriceResolver.Resolve(
                        row.ItemType,
                        row.HasBatchTracking,
                        row.BatchNo,
                        false,
                        row.HasSellingPriceOverride,
                        row.BatchRetailPrice,
                        row.BatchWholesalePrice,
                        row.VariantRetailPrice,
                        row.VariantWholesalePrice);

                int suggestedQty = ConvertReceivedQtyToLabelQty(row.ReceivedQty);
                int remainingToPrint = suggestedQty - row.BarcodePrintedCount;

                if (remainingToPrint < 0)
                    remainingToPrint = 0;

                result.Add(new BarcodePrintQueueItemDto
                {
                    ItemVariantId = row.ItemVariantId,
                    ItemBatchId = row.ItemBatchId,
                    GrnHeaderId = row.GrnHeaderId,
                    GrnLineId = row.GrnLineId,

                    SourceDocument = row.GrnNumber,
                    SourceType = "GRN",
                    IsBatchLabel = true,

                    ItemCode = row.ItemCode,
                    SkuCode = row.SkuCode,
                    ItemName = row.ItemName,
                    VariantDescription = row.VariantDescription,
                    Uom = string.IsNullOrWhiteSpace(row.Uom) ? "PCS" : row.Uom,

                    BatchNo = row.BatchNo,
                    ExpiryDate = row.ExpiryDate,
                    ReceivedDate = row.ReceivedDate,
                    ReceivedQty = row.ReceivedQty,
                    AvailableQty = row.AvailableQty,

                    InternalBatchBarcode = row.InternalBatchBarcode,
                    Barcode = row.InternalBatchBarcode,

                    Price = effectivePrice.RetailPrice,

                    CostPrice = row.CostPrice,

                    BarcodePrintedCount = row.BarcodePrintedCount,
                    LastBarcodePrintedAt = row.LastBarcodePrintedAt,
                    LastBarcodePrintedBy = row.LastBarcodePrintedBy ?? string.Empty,

                    PrintQuantity = remainingToPrint > 0
                        ? remainingToPrint
                        : 0,

                    IsSelected = remainingToPrint > 0
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

            await using var context = await _contextFactory.CreateDbContextAsync();

            // First: allow manual reprint by exact GRN batch barcode.
            var batch = await context.ItemBatches
                .AsNoTracking()
                .Where(b =>
                    !b.IsDeactivated &&
                    b.ItemVariant != null &&
                    !b.ItemVariant.IsDeactivated &&
                    b.ItemVariant.ItemParent != null &&
                    !b.ItemVariant.ItemParent.IsDeactivated &&
                    b.ItemVariant.ItemParent.HasBatchTracking &&
                    b.BatchNo.ToUpper() != GeneralBatchNo &&
                    b.InternalBatchBarcode != null &&
                    b.InternalBatchBarcode.ToUpper() == upperSearch)
                .Select(b => new
                {
                    ItemBatchId = b.Id,
                    b.ItemVariantId,

                    ItemCode = b.ItemVariant.ItemParent.ItemCode,
                    SkuCode = b.ItemVariant.SkuCode,
                    ItemName = b.ItemVariant.ItemParent.ItemName,
                    VariantDescription = string.IsNullOrWhiteSpace(b.ItemVariant.VariantDescription)
                        ? "Standard"
                        : b.ItemVariant.VariantDescription,

                    BatchNo = b.BatchNo,
                    b.ExpiryDate,
                    b.ReceivedDate,
                    AvailableQty = b.CurrentStock,
                    InternalBatchBarcode = b.InternalBatchBarcode ?? string.Empty,

                    b.CostPrice,
                    BatchRetailPrice = b.RetailPrice,
                    BatchWholesalePrice = b.WholesalePrice,
                    b.HasSellingPriceOverride,
                    VariantRetailPrice = b.ItemVariant.RetailPrice,
                    VariantWholesalePrice = b.ItemVariant.WholesalePrice,
                    ItemType = b.ItemVariant.ItemParent.ItemType,
                    HasBatchTracking = b.ItemVariant.ItemParent.HasBatchTracking,

                    b.BarcodePrintedCount,
                    b.LastBarcodePrintedAt,
                    b.LastBarcodePrintedBy
                })
                .FirstOrDefaultAsync();

            if (batch != null)
            {
                EffectiveSellingPrice effectivePrice =
                    EffectiveSellingPriceResolver.Resolve(
                        batch.ItemType,
                        batch.HasBatchTracking,
                        batch.BatchNo,
                        false,
                        batch.HasSellingPriceOverride,
                        batch.BatchRetailPrice,
                        batch.BatchWholesalePrice,
                        batch.VariantRetailPrice,
                        batch.VariantWholesalePrice);

                return new BarcodePrintQueueItemDto
                {
                    ItemVariantId = batch.ItemVariantId,
                    ItemBatchId = batch.ItemBatchId,

                    SourceDocument = "MANUAL",
                    SourceType = "MANUAL-BATCH",
                    IsBatchLabel = true,

                    ItemCode = batch.ItemCode,
                    SkuCode = batch.SkuCode,
                    ItemName = batch.ItemName,
                    VariantDescription = batch.VariantDescription,
                    Uom = "PCS",

                    BatchNo = batch.BatchNo,
                    ExpiryDate = batch.ExpiryDate,
                    ReceivedDate = batch.ReceivedDate,
                    ReceivedQty = 0m,
                    AvailableQty = batch.AvailableQty,

                    InternalBatchBarcode = batch.InternalBatchBarcode,
                    Barcode = batch.InternalBatchBarcode,

                    Price = effectivePrice.RetailPrice,

                    CostPrice = batch.CostPrice,

                    BarcodePrintedCount = batch.BarcodePrintedCount,
                    LastBarcodePrintedAt = batch.LastBarcodePrintedAt,
                    LastBarcodePrintedBy = batch.LastBarcodePrintedBy ?? string.Empty,

                    PrintQuantity = 1,
                    IsSelected = true
                };
            }

            // Second: normal item barcode printing.
            // This is for ItemVariant.Barcode, not GRN batch barcode.
            var variant = await context.ItemVariants
                .AsNoTracking()
                .Where(v =>
                    !v.IsDeactivated &&
                    !v.ItemParent.IsDeactivated &&
                    (
                        (v.Barcode ?? string.Empty).ToUpper() == upperSearch ||
                        v.SkuCode.ToUpper() == upperSearch ||
                        v.ItemParent.ItemCode.ToUpper() == upperSearch
                    ))
                .OrderBy(v => v.ItemParent.ItemCode)
                .ThenBy(v => v.VariantDescription)
                .Select(v => new
                {
                    VariantId = v.Id,
                    v.SkuCode,
                    Barcode = v.Barcode ?? string.Empty,
                    VariantDescription = string.IsNullOrWhiteSpace(v.VariantDescription)
                        ? "Standard"
                        : v.VariantDescription,
                    v.RetailPrice,

                    ItemCode = v.ItemParent.ItemCode,
                    ItemName = v.ItemParent.ItemName,
                    HasBatchTracking = v.ItemParent.HasBatchTracking
                })
                .FirstOrDefaultAsync();

            if (variant == null)
                return null;

            return new BarcodePrintQueueItemDto
            {
                ItemVariantId = variant.VariantId,
                ItemBatchId = null,

                SourceDocument = "MANUAL",
                SourceType = "MANUAL-ITEM",
                IsBatchLabel = false,

                ItemCode = variant.ItemCode,
                SkuCode = variant.SkuCode,
                ItemName = variant.ItemName,
                VariantDescription = variant.VariantDescription,
                Uom = "PCS",

                BatchNo = string.Empty,
                ExpiryDate = null,
                ReceivedDate = null,
                ReceivedQty = 0m,
                AvailableQty = 0m,

                Barcode = variant.Barcode,
                InternalBatchBarcode = string.Empty,

                Price = variant.RetailPrice,
                CostPrice = 0m,

                BarcodePrintedCount = 0,
                LastBarcodePrintedAt = null,
                LastBarcodePrintedBy = string.Empty,

                PrintQuantity = 1,
                IsSelected = true
            };
        }

        // =========================================================
        // PRINT AUDIT UPDATE
        // =========================================================

        public async Task MarkBatchLabelsPrintedAsync(
            IEnumerable<BarcodePrintQueueItemDto> printedItems,
            string printedBy)
        {
            if (printedItems == null)
                return;

            var batchPrintRows = printedItems
                .Where(i =>
                    i.IsBatchLabel &&
                    i.ItemBatchId.HasValue &&
                    i.ItemBatchId.Value > 0 &&
                    i.PrintQuantity > 0)
                .GroupBy(i => i.ItemBatchId!.Value)
                .Select(g => new
                {
                    ItemBatchId = g.Key,
                    PrintedQty = g.Sum(x => x.PrintQuantity)
                })
                .ToList();

            if (!batchPrintRows.Any())
                return;

            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                DateTime now = DateTime.Now;
                string user = string.IsNullOrWhiteSpace(printedBy)
                    ? "Admin"
                    : printedBy.Trim();

                var batchIds = batchPrintRows
                    .Select(x => x.ItemBatchId)
                    .ToList();

                var batches = await context.ItemBatches
                    .Where(b => batchIds.Contains(b.Id))
                    .ToListAsync();

                foreach (var row in batchPrintRows)
                {
                    var batch = batches.FirstOrDefault(b => b.Id == row.ItemBatchId);

                    if (batch == null)
                        continue;

                    if (row.PrintedQty <= 0)
                        continue;

                    batch.BarcodePrintedCount += row.PrintedQty;
                    batch.LastBarcodePrintedAt = now;
                    batch.LastBarcodePrintedBy = user;
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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

            var selectedRows = queue
                .Where(i => i.IsSelected && i.PrintQuantity > 0)
                .ToList();

            if (!selectedRows.Any())
            {
                errors.Add("No selected labels with print quantity greater than zero.");
                return errors;
            }

            foreach (var item in selectedRows)
            {
                if (item.ItemVariantId <= 0)
                    errors.Add($"Invalid item variant in queue: {item.DisplayName}");

                if (item.IsBatchLabel && (!item.ItemBatchId.HasValue || item.ItemBatchId.Value <= 0))
                    errors.Add($"Invalid item batch in queue: {item.DisplayName}");

                if (string.IsNullOrWhiteSpace(item.EffectiveBarcode))
                    errors.Add($"Missing barcode for item: {item.ItemCode} - {item.DisplayName}");

                if (item.PrintQuantity <= 0)
                    errors.Add($"Print quantity must be greater than zero for item: {item.ItemCode} - {item.DisplayName}");

                if (item.PrintQuantity > 5000)
                    errors.Add($"Print quantity is too high for item: {item.ItemCode} - {item.DisplayName}");

                if (item.IsBatchLabel && string.IsNullOrWhiteSpace(item.BatchNo))
                    errors.Add($"Batch number is missing for batch label: {item.ItemCode} - {item.DisplayName}");
            }

            return errors;
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private static int ConvertReceivedQtyToLabelQty(decimal receivedQty)
        {
            if (receivedQty <= 0)
                return 0;

            return (int)Math.Ceiling(receivedQty);
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}