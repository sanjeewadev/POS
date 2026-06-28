using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public class StockAdjustmentBatchLookupDto
    {
        public int ItemVariantId { get; set; }
        public int ItemBatchId { get; set; }

        public string ItemCode { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public string BatchNo { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }

        public decimal SystemQty { get; set; }
        public decimal UnitCost { get; set; }
    }

    public class StockAdjustmentRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        private static readonly HashSet<string> AllowedModes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Physical Count Correction",
            "Stock Increase",
            "Stock Decrease"
        };

        private static readonly HashSet<string> AllowedReasonCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Data Entry Error",
            "Damaged / Broken",
            "Expired / Spoiled",
            "Stolen / Missing",
            "Found Stock",
            "Opening Balance",
            "Audit Correction",
            "Internal Use",
            "Promotional Giveaway",
            "Supplier Free Issue"
        };

        public StockAdjustmentRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =========================================================
        // LOOKUP: BARCODE / SKU / ITEM CODE -> ACTIVE BATCHES
        // =========================================================

        public async Task<List<StockAdjustmentBatchLookupDto>> GetActiveBatchesByBarcodeAsync(string searchTerm)
        {
            searchTerm = NormalizeText(searchTerm);

            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<StockAdjustmentBatchLookupDto>();

            string upperSearch = searchTerm.ToUpperInvariant();

            using var context = await _contextFactory.CreateDbContextAsync();

            var variant = await context.ItemVariants
                .Include(v => v.ItemParent)
                .Include(v => v.ItemBatches)
                .AsNoTracking()
                .Where(v => !v.IsDeactivated && !v.ItemParent.IsDeactivated)
                .FirstOrDefaultAsync(v =>
                    v.Barcode.ToUpper() == upperSearch ||
                    v.SkuCode.ToUpper() == upperSearch ||
                    v.ItemParent.ItemCode.ToUpper() == upperSearch);

            if (variant == null)
                return new List<StockAdjustmentBatchLookupDto>();

            return variant.ItemBatches
                .Where(b => !b.IsDeactivated)
                .OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(b => b.BatchNo)
                .Select(b => new StockAdjustmentBatchLookupDto
                {
                    ItemVariantId = variant.Id,
                    ItemBatchId = b.Id,
                    ItemCode = variant.ItemParent?.ItemCode ?? string.Empty,
                    VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                        ? "Standard"
                        : variant.VariantDescription,
                    Description = variant.ItemParent?.ItemName ?? string.Empty,
                    BatchNo = b.BatchNo,
                    ExpiryDate = b.ExpiryDate,
                    SystemQty = b.CurrentStock,
                    UnitCost = b.CostPrice > 0 ? b.CostPrice : variant.AverageCost
                })
                .ToList();
        }

        // =========================================================
        // SAVE DRAFT / POST ADJUSTMENT
        // =========================================================

        public async Task<StockAdjustmentHeader> SaveAdjustmentAsync(
            StockAdjustmentHeader header,
            List<StockAdjustmentLine> lines,
            bool isDraft)
        {
            if (header == null)
                throw new ArgumentNullException(nameof(header));

            if (lines == null || !lines.Any())
                throw new InvalidOperationException("Stock adjustment must contain at least one line.");

            NormalizeHeader(header);
            NormalizeLines(lines);
            ValidateSubmittedLineDuplicates(lines);

            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                DateTime now = DateTime.Now;

                var batchIds = lines
                    .Select(l => l.ItemBatchId)
                    .Distinct()
                    .ToList();

                var batchMap = await context.ItemBatches
                    .Include(b => b.ItemVariant)
                        .ThenInclude(v => v.ItemParent)
                    .Where(b => batchIds.Contains(b.Id))
                    .ToDictionaryAsync(b => b.Id);

                await ValidateHeaderAsync(header, isDraft);
                ValidateLines(header, lines, batchMap, isDraft);
                RecalculateTotals(header, lines, batchMap);

                StockAdjustmentHeader targetHeader;

                if (header.Id == 0)
                {
                    targetHeader = new StockAdjustmentHeader
                    {
                        AdjustmentNo = await GenerateDocumentNumberAsync(context, "ADJ"),
                        AdjustmentDate = header.AdjustmentDate,
                        AdjustmentMode = header.AdjustmentMode,
                        AuthorizedBy = header.AuthorizedBy,
                        Reference = header.Reference,
                        Remarks = header.Remarks,
                        TotalImpact = header.TotalImpact,
                        TotalIncreaseQty = header.TotalIncreaseQty,
                        TotalDecreaseQty = header.TotalDecreaseQty,
                        Status = isDraft ? "Draft" : "Posted",
                        CreatedBy = header.CreatedBy,
                        PostedBy = isDraft ? string.Empty : header.PostedBy,
                        CreatedAt = now,
                        UpdatedAt = now,
                        PostedAt = isDraft ? null : now
                    };

                    if (!isDraft && string.IsNullOrWhiteSpace(targetHeader.PostedBy))
                        targetHeader.PostedBy = targetHeader.AuthorizedBy;

                    await context.StockAdjustmentHeaders.AddAsync(targetHeader);
                    await context.SaveChangesAsync();
                }
                else
                {
                    targetHeader = await context.StockAdjustmentHeaders
                        .Include(h => h.AdjustmentLines)
                        .FirstOrDefaultAsync(h => h.Id == header.Id)
                        ?? throw new InvalidOperationException("Stock adjustment document was not found.");

                    if (targetHeader.Status == "Posted")
                        throw new InvalidOperationException("This stock adjustment is already posted and cannot be modified.");

                    if (targetHeader.Status == "Cancelled")
                        throw new InvalidOperationException("This stock adjustment is cancelled and cannot be modified.");

                    targetHeader.AdjustmentDate = header.AdjustmentDate;
                    targetHeader.AdjustmentMode = header.AdjustmentMode;
                    targetHeader.AuthorizedBy = header.AuthorizedBy;
                    targetHeader.Reference = header.Reference;
                    targetHeader.Remarks = header.Remarks;
                    targetHeader.TotalImpact = header.TotalImpact;
                    targetHeader.TotalIncreaseQty = header.TotalIncreaseQty;
                    targetHeader.TotalDecreaseQty = header.TotalDecreaseQty;
                    targetHeader.Status = isDraft ? "Draft" : "Posted";
                    targetHeader.PostedBy = isDraft ? string.Empty : header.PostedBy;
                    targetHeader.UpdatedAt = now;
                    targetHeader.PostedAt = isDraft ? null : now;

                    if (!isDraft && string.IsNullOrWhiteSpace(targetHeader.PostedBy))
                        targetHeader.PostedBy = targetHeader.AuthorizedBy;

                    var oldLines = await context.StockAdjustmentLines
                        .Where(l => l.StockAdjustmentHeaderId == targetHeader.Id)
                        .ToListAsync();

                    context.StockAdjustmentLines.RemoveRange(oldLines);
                    await context.SaveChangesAsync();
                }

                var savedLines = new List<StockAdjustmentLine>();

                foreach (var sourceLine in lines)
                {
                    var batch = batchMap[sourceLine.ItemBatchId];

                    var line = new StockAdjustmentLine
                    {
                        StockAdjustmentHeaderId = targetHeader.Id,
                        ItemBatchId = batch.Id,
                        ItemVariantId = batch.ItemVariantId,

                        SystemQty = sourceLine.SystemQty,
                        ActualQty = sourceLine.ActualQty,
                        VarianceQty = sourceLine.VarianceQty,

                        ReasonCode = NormalizeText(sourceLine.ReasonCode),
                        LineRemarks = NormalizeText(sourceLine.LineRemarks),

                        UnitCost = sourceLine.UnitCost,
                        CostImpact = sourceLine.CostImpact,

                        LineStatus = isDraft ? "Open" : "Posted",
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    await context.StockAdjustmentLines.AddAsync(line);
                    savedLines.Add(line);
                }

                await context.SaveChangesAsync();

                if (!isDraft)
                {
                    foreach (var line in savedLines)
                    {
                        var batch = batchMap[line.ItemBatchId];

                        decimal newBatchQty = batch.CurrentStock + line.VarianceQty;

                        if (newBatchQty < 0)
                        {
                            throw new InvalidOperationException(
                                $"Posting this adjustment would make batch '{batch.BatchNo}' negative.");
                        }

                        batch.CurrentStock = newBatchQty;
                        batch.UpdatedAt = now;

                        var inventoryTx = new InventoryTransaction
                        {
                            ItemVariantId = batch.ItemVariantId,
                            ItemBatchId = batch.Id,
                            TransactionDate = targetHeader.AdjustmentDate,
                            TransactionType = "ADJUSTMENT",
                            ReferenceDocument = targetHeader.AdjustmentNo,
                            ReferenceLineId = line.Id,
                            Quantity = line.VarianceQty,
                            UnitCost = line.UnitCost,
                            CreatedBy = targetHeader.AuthorizedBy,
                            CreatedAt = now,
                            Remarks =
                                $"Mode: {targetHeader.AdjustmentMode} | Reason: {line.ReasonCode} | Batch: {batch.BatchNo} | Ref: {targetHeader.Reference}"
                        };

                        await context.InventoryTransactions.AddAsync(inventoryTx);
                    }
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return targetHeader;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // =========================================================
        // VALIDATION
        // =========================================================

        private static Task ValidateHeaderAsync(StockAdjustmentHeader header, bool isDraft)
        {
            if (string.IsNullOrWhiteSpace(header.AdjustmentMode))
                throw new InvalidOperationException("Adjustment mode is required.");

            if (!AllowedModes.Contains(header.AdjustmentMode))
                throw new InvalidOperationException("Invalid stock adjustment mode.");

            if (header.AdjustmentDate.Date > DateTime.Now.Date.AddDays(1))
                throw new InvalidOperationException("Adjustment date cannot be in the far future.");

            if (string.IsNullOrWhiteSpace(header.AuthorizedBy))
                throw new InvalidOperationException("Authorized By is required.");

            if (header.AuthorizedBy.Length > 50)
                throw new InvalidOperationException("Authorized By cannot be longer than 50 characters.");

            if (header.Reference.Length > 100)
                throw new InvalidOperationException("Reference cannot be longer than 100 characters.");

            if (header.Remarks.Length > 500)
                throw new InvalidOperationException("Remarks cannot be longer than 500 characters.");

            if (!isDraft && string.IsNullOrWhiteSpace(header.Reference))
                throw new InvalidOperationException("Reference / reason document is required before posting.");

            return Task.CompletedTask;
        }

        private static void ValidateLines(
            StockAdjustmentHeader header,
            List<StockAdjustmentLine> lines,
            Dictionary<int, ItemBatch> batchMap,
            bool isDraft)
        {
            foreach (var line in lines)
            {
                NormalizeLine(line);

                if (line.ItemBatchId <= 0)
                    throw new InvalidOperationException("Invalid batch in adjustment line.");

                if (!batchMap.TryGetValue(line.ItemBatchId, out var batch))
                    throw new InvalidOperationException("One or more stock batches were not found.");

                if (batch.IsDeactivated)
                    throw new InvalidOperationException($"Batch '{batch.BatchNo}' is deactivated.");

                if (batch.ItemVariant == null)
                    throw new InvalidOperationException($"Batch '{batch.BatchNo}' is not linked to a valid item.");

                if (batch.ItemVariant.IsDeactivated || batch.ItemVariant.ItemParent.IsDeactivated)
                    throw new InvalidOperationException($"Item linked to batch '{batch.BatchNo}' is deactivated.");

                line.ItemVariantId = batch.ItemVariantId;

                if (line.UnitCost <= 0)
                    line.UnitCost = batch.CostPrice > 0 ? batch.CostPrice : batch.ItemVariant.AverageCost;

                if (line.UnitCost < 0)
                    throw new InvalidOperationException($"Unit cost cannot be negative for batch '{batch.BatchNo}'.");

                line.VarianceQty = line.ActualQty - line.SystemQty;
                line.CostImpact = Math.Round(line.VarianceQty * line.UnitCost, 2);

                if (line.VarianceQty == 0)
                    throw new InvalidOperationException($"Batch '{batch.BatchNo}' has no variance.");

                if (line.ActualQty < 0)
                    throw new InvalidOperationException($"Actual quantity cannot be negative for batch '{batch.BatchNo}'.");

                if (header.AdjustmentMode == "Stock Increase" && line.VarianceQty <= 0)
                    throw new InvalidOperationException("Stock Increase mode can only contain positive variance lines.");

                if (header.AdjustmentMode == "Stock Decrease" && line.VarianceQty >= 0)
                    throw new InvalidOperationException("Stock Decrease mode can only contain negative variance lines.");

                if (!isDraft)
                {
                    if (header.AdjustmentMode == "Physical Count Correction" &&
                        batch.CurrentStock != line.SystemQty)
                    {
                        throw new InvalidOperationException(
                            $"Batch '{batch.BatchNo}' stock changed after it was scanned. Please rescan and try again.");
                    }

                    decimal newBatchQty = batch.CurrentStock + line.VarianceQty;

                    if (newBatchQty < 0)
                    {
                        throw new InvalidOperationException(
                            $"Adjustment would make batch '{batch.BatchNo}' stock negative.");
                    }

                    if (string.IsNullOrWhiteSpace(line.ReasonCode))
                        throw new InvalidOperationException($"Reason code is required for batch '{batch.BatchNo}'.");

                    if (!AllowedReasonCodes.Contains(line.ReasonCode))
                        throw new InvalidOperationException($"Invalid reason code '{line.ReasonCode}' for batch '{batch.BatchNo}'.");
                }

                if (line.ReasonCode.Length > 50)
                    throw new InvalidOperationException($"Reason code is too long for batch '{batch.BatchNo}'.");

                if (line.LineRemarks.Length > 250)
                    throw new InvalidOperationException($"Line remarks are too long for batch '{batch.BatchNo}'.");
            }
        }

        private static void ValidateSubmittedLineDuplicates(List<StockAdjustmentLine> lines)
        {
            var duplicate = lines
                .GroupBy(l => l.ItemBatchId)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
                throw new InvalidOperationException("Same batch cannot appear twice in one stock adjustment.");
        }

        // =========================================================
        // TOTALS
        // =========================================================

        private static void RecalculateTotals(
            StockAdjustmentHeader header,
            List<StockAdjustmentLine> lines,
            Dictionary<int, ItemBatch> batchMap)
        {
            decimal totalImpact = 0m;
            decimal totalIncreaseQty = 0m;
            decimal totalDecreaseQty = 0m;

            foreach (var line in lines)
            {
                var batch = batchMap[line.ItemBatchId];

                if (line.UnitCost <= 0)
                    line.UnitCost = batch.CostPrice > 0 ? batch.CostPrice : batch.ItemVariant.AverageCost;

                line.VarianceQty = line.ActualQty - line.SystemQty;
                line.CostImpact = Math.Round(line.VarianceQty * line.UnitCost, 2);

                totalImpact += line.CostImpact;

                if (line.VarianceQty > 0)
                    totalIncreaseQty += line.VarianceQty;

                if (line.VarianceQty < 0)
                    totalDecreaseQty += Math.Abs(line.VarianceQty);
            }

            header.TotalImpact = Math.Round(totalImpact, 2);
            header.TotalIncreaseQty = totalIncreaseQty;
            header.TotalDecreaseQty = totalDecreaseQty;
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private static async Task<string> GenerateDocumentNumberAsync(
            AppDbContext context,
            string documentType)
        {
            var sequence = await context.DocumentSequences
                .FirstOrDefaultAsync(s => s.DocumentType == documentType);

            if (sequence == null)
            {
                sequence = new DocumentSequence
                {
                    DocumentType = documentType,
                    Prefix = $"{documentType}-",
                    NextSequenceNumber = 1,
                    PaddingLength = 5,
                    UpdatedAt = DateTime.Now
                };

                await context.DocumentSequences.AddAsync(sequence);
                await context.SaveChangesAsync();
            }

            string number = $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString().PadLeft(sequence.PaddingLength, '0')}";

            sequence.NextSequenceNumber++;
            sequence.UpdatedAt = DateTime.Now;

            await context.SaveChangesAsync();

            return number;
        }

        private static void NormalizeHeader(StockAdjustmentHeader header)
        {
            header.AdjustmentMode = NormalizeText(header.AdjustmentMode);
            header.AuthorizedBy = NormalizeText(header.AuthorizedBy);
            header.Reference = NormalizeText(header.Reference);
            header.Remarks = NormalizeText(header.Remarks);
            header.CreatedBy = NormalizeText(header.CreatedBy);
            header.PostedBy = NormalizeText(header.PostedBy);

            if (string.IsNullOrWhiteSpace(header.CreatedBy))
                header.CreatedBy = header.AuthorizedBy;

            if (string.IsNullOrWhiteSpace(header.PostedBy))
                header.PostedBy = header.AuthorizedBy;
        }

        private static void NormalizeLines(List<StockAdjustmentLine> lines)
        {
            foreach (var line in lines)
                NormalizeLine(line);
        }

        private static void NormalizeLine(StockAdjustmentLine line)
        {
            line.ReasonCode = NormalizeText(line.ReasonCode);
            line.LineRemarks = NormalizeText(line.LineRemarks);
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}