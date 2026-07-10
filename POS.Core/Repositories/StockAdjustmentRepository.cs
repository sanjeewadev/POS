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

        public bool HasBatchTracking { get; set; }
        public bool HasExpiryTracking { get; set; }
        public bool IsGeneralStockBucket { get; set; }

        public string TrackingText => !HasBatchTracking ? "Average Cost" : HasExpiryTracking ? "Batch + Expiry" : "Batch";

        public string BatchNo { get; set; } = string.Empty;
        public string InternalBatchBarcode { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }

        public string BatchDisplayText => IsGeneralStockBucket ? "GENERAL" : BatchNo;
        public string BatchBarcodeDisplayText => IsGeneralStockBucket ? "-" : InternalBatchBarcode;

        public decimal SystemQty { get; set; }
        public decimal UnitCost { get; set; }
    }

    public class StockAdjustmentRepository
    {
        private const string GeneralBatchNo = "GENERAL";

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
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<List<StockAdjustmentBatchLookupDto>> GetActiveBatchesByBarcodeAsync(string searchTerm)
        {
            string search = NormalizeText(searchTerm);

            if (string.IsNullOrWhiteSpace(search))
                return new List<StockAdjustmentBatchLookupDto>();

            string upperSearch = search.ToUpperInvariant();

            using var context = await _contextFactory.CreateDbContextAsync();

            var exactBatchIds = await context.ItemBatches
                .AsNoTracking()
                .Where(b =>
                    !b.IsDeactivated &&
                    !b.ItemVariant.IsDeactivated &&
                    !b.ItemVariant.ItemParent.IsDeactivated &&
                    (
                        ((b.InternalBatchBarcode ?? string.Empty) != string.Empty &&
                         (b.InternalBatchBarcode ?? string.Empty).ToUpper() == upperSearch) ||
                        (upperSearch != GeneralBatchNo &&
                         (b.BatchNo ?? string.Empty).ToUpper() == upperSearch)
                    ))
                .Select(b => b.Id)
                .ToListAsync();

            if (exactBatchIds.Any())
                return await LoadRowsByBatchIdsAsync(context, exactBatchIds);

            var variantIds = await context.ItemVariants
                .AsNoTracking()
                .Where(v =>
                    !v.IsDeactivated &&
                    !v.ItemParent.IsDeactivated &&
                    (
                        ((v.Barcode ?? string.Empty).ToUpper() == upperSearch) ||
                        v.SkuCode.ToUpper() == upperSearch ||
                        v.ItemParent.ItemCode.ToUpper() == upperSearch
                    ))
                .OrderBy(v => v.ItemParent.ItemCode)
                .ThenBy(v => v.VariantDescription)
                .Select(v => v.Id)
                .ToListAsync();

            if (!variantIds.Any())
                return new List<StockAdjustmentBatchLookupDto>();

            return await LoadRowsByVariantIdsAsync(context, variantIds);
        }

        private static async Task<List<StockAdjustmentBatchLookupDto>> LoadRowsByBatchIdsAsync(
            AppDbContext context,
            List<int> batchIds)
        {
            return await ProjectValidStockRows(context.ItemBatches
                    .Where(b => batchIds.Contains(b.Id)))
                .ToListAsync();
        }

        private static async Task<List<StockAdjustmentBatchLookupDto>> LoadRowsByVariantIdsAsync(
            AppDbContext context,
            List<int> variantIds)
        {
            return await ProjectValidStockRows(context.ItemBatches
                    .Where(b =>
                        variantIds.Contains(b.ItemVariantId) &&
                        (
                            (b.ItemVariant.ItemParent.HasBatchTracking &&
                             (b.BatchNo ?? string.Empty).ToUpper() != GeneralBatchNo) ||
                            (!b.ItemVariant.ItemParent.HasBatchTracking &&
                             (b.BatchNo ?? string.Empty).ToUpper() == GeneralBatchNo)
                        )))
                .ToListAsync();
        }

        private static IQueryable<StockAdjustmentBatchLookupDto> ProjectValidStockRows(IQueryable<ItemBatch> query)
        {
            return query
                .Include(b => b.ItemVariant)
                    .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .Where(b =>
                    !b.IsDeactivated &&
                    !b.ItemVariant.IsDeactivated &&
                    !b.ItemVariant.ItemParent.IsDeactivated)
                .OrderBy(b => b.ItemVariant.ItemParent.ItemCode)
                .ThenBy(b => b.ItemVariant.VariantDescription)
                .ThenBy(b => b.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(b => b.BatchNo)
                .Select(b => new StockAdjustmentBatchLookupDto
                {
                    ItemVariantId = b.ItemVariantId,
                    ItemBatchId = b.Id,

                    ItemCode = b.ItemVariant.ItemParent.ItemCode,
                    VariantDescription = string.IsNullOrWhiteSpace(b.ItemVariant.VariantDescription)
                        ? "Standard"
                        : b.ItemVariant.VariantDescription,
                    Description = b.ItemVariant.ItemParent.ItemName,

                    HasBatchTracking = b.ItemVariant.ItemParent.HasBatchTracking,
                    HasExpiryTracking = b.ItemVariant.ItemParent.HasExpiryTracking || b.ItemVariant.ItemParent.HasBatchExpiry,
                    IsGeneralStockBucket = (b.BatchNo ?? string.Empty).ToUpper() == GeneralBatchNo,

                    BatchNo = b.BatchNo,
                    InternalBatchBarcode = b.InternalBatchBarcode ?? string.Empty,
                    ExpiryDate = b.ExpiryDate,

                    SystemQty = b.CurrentStock,
                    UnitCost = b.CostPrice > 0
                        ? b.CostPrice
                        : b.ItemVariant.AverageCost > 0
                            ? b.ItemVariant.AverageCost
                            : b.ItemVariant.CostPrice
                });
        }

        public async Task<StockAdjustmentHeader> SaveAdjustmentAsync(
            StockAdjustmentHeader header,
            List<StockAdjustmentLine> lines,
            bool isDraft)
        {
            if (isDraft)
                throw new InvalidOperationException("Stock adjustment drafts are disabled. Please post the adjustment or clear the form.");

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

                ValidateHeader(header);
                ValidateLines(header, lines, batchMap);
                RecalculateTotals(header, lines, batchMap);

                var targetHeader = new StockAdjustmentHeader
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

                    Status = "Posted",
                    CreatedBy = string.IsNullOrWhiteSpace(header.CreatedBy) ? header.AuthorizedBy : header.CreatedBy,
                    PostedBy = string.IsNullOrWhiteSpace(header.PostedBy) ? header.AuthorizedBy : header.PostedBy,
                    CreatedAt = now,
                    UpdatedAt = now,
                    PostedAt = now
                };

                await context.StockAdjustmentHeaders.AddAsync(targetHeader);
                await context.SaveChangesAsync();

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

                        LineStatus = "Posted",
                        CreatedAt = now,
                        UpdatedAt = now
                    };

                    await context.StockAdjustmentLines.AddAsync(line);
                    savedLines.Add(line);
                }

                await context.SaveChangesAsync();

                foreach (var line in savedLines)
                {
                    var batch = batchMap[line.ItemBatchId];
                    decimal newBatchQty = batch.CurrentStock + line.VarianceQty;

                    if (newBatchQty < 0)
                        throw new InvalidOperationException($"Posting this adjustment would make stock row '{BuildBatchDisplayName(batch)}' negative.");

                    batch.CurrentStock = newBatchQty;
                    batch.UpdatedAt = now;

                    await context.InventoryTransactions.AddAsync(new InventoryTransaction
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
                        Remarks = TrimToMax(
                            $"Mode: {targetHeader.AdjustmentMode} | Reason: {line.ReasonCode} | Stock Row: {BuildBatchDisplayName(batch)} | Ref: {targetHeader.Reference}",
                            250)
                    });
                }

                await context.SaveChangesAsync();

                var affectedVariantIds = savedLines
                    .Select(l => l.ItemVariantId)
                    .Distinct()
                    .ToList();

                await RecalculateVariantAverageCostsAsync(context, affectedVariantIds, now);

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

        private static void ValidateHeader(StockAdjustmentHeader header)
        {
            if (string.IsNullOrWhiteSpace(header.AdjustmentMode))
                throw new InvalidOperationException("Adjustment mode is required.");

            if (!AllowedModes.Contains(header.AdjustmentMode))
                throw new InvalidOperationException("Invalid stock adjustment mode.");

            if (header.AdjustmentDate.Date > DateTime.Now.Date.AddDays(1))
                throw new InvalidOperationException("Adjustment date cannot be in the far future.");

            if (header.AdjustmentDate.Date < new DateTime(2000, 1, 1))
                throw new InvalidOperationException("Adjustment date is not valid.");

            if (string.IsNullOrWhiteSpace(header.AuthorizedBy))
                throw new InvalidOperationException("Authorized By is required.");

            if (header.AuthorizedBy.Length > 50)
                throw new InvalidOperationException("Authorized By cannot be longer than 50 characters.");

            if (string.IsNullOrWhiteSpace(header.Reference))
                throw new InvalidOperationException("Reference / reason document is required before posting.");

            if (header.Reference.Length > 100)
                throw new InvalidOperationException("Reference cannot be longer than 100 characters.");

            if (header.Remarks.Length > 500)
                throw new InvalidOperationException("Remarks cannot be longer than 500 characters.");
        }

        private static void ValidateLines(
            StockAdjustmentHeader header,
            List<StockAdjustmentLine> lines,
            Dictionary<int, ItemBatch> batchMap)
        {
            foreach (var line in lines)
            {
                NormalizeLine(line);

                if (!batchMap.TryGetValue(line.ItemBatchId, out var batch))
                    throw new InvalidOperationException("One or more stock rows were not found.");

                if (batch.IsDeactivated)
                    throw new InvalidOperationException($"Stock row '{BuildBatchDisplayName(batch)}' is deactivated.");

                if (batch.ItemVariant == null || batch.ItemVariant.ItemParent == null)
                    throw new InvalidOperationException($"Stock row '{BuildBatchDisplayName(batch)}' is not linked to a valid item.");

                if (batch.ItemVariant.IsDeactivated || batch.ItemVariant.ItemParent.IsDeactivated)
                    throw new InvalidOperationException($"Item linked to stock row '{BuildBatchDisplayName(batch)}' is deactivated.");

                bool isGeneral = IsGeneralBatch(batch.BatchNo);
                bool isBatchTracked = batch.ItemVariant.ItemParent.HasBatchTracking;

                if (isBatchTracked && isGeneral)
                    throw new InvalidOperationException("Batch-tracked items cannot be adjusted through GENERAL. Select the exact GRN batch.");

                if (!isBatchTracked && !isGeneral)
                    throw new InvalidOperationException("Average-cost items must be adjusted through the GENERAL stock bucket.");

                line.ItemVariantId = batch.ItemVariantId;

                if (line.UnitCost <= 0)
                {
                    line.UnitCost = batch.CostPrice > 0
                        ? batch.CostPrice
                        : batch.ItemVariant.AverageCost > 0
                            ? batch.ItemVariant.AverageCost
                            : batch.ItemVariant.CostPrice;
                }

                if (line.UnitCost < 0)
                    throw new InvalidOperationException($"Unit cost cannot be negative for stock row '{BuildBatchDisplayName(batch)}'.");

                line.VarianceQty = line.ActualQty - line.SystemQty;
                line.CostImpact = Math.Round(line.VarianceQty * line.UnitCost, 2);

                if (line.VarianceQty == 0)
                    throw new InvalidOperationException($"Stock row '{BuildBatchDisplayName(batch)}' has no variance.");

                if (line.ActualQty < 0)
                    throw new InvalidOperationException($"Actual quantity cannot be negative for stock row '{BuildBatchDisplayName(batch)}'.");

                if (batch.CurrentStock != line.SystemQty)
                    throw new InvalidOperationException($"Stock row '{BuildBatchDisplayName(batch)}' changed after it was scanned. Please rescan and try again.");

                decimal newBatchQty = batch.CurrentStock + line.VarianceQty;

                if (newBatchQty < 0)
                    throw new InvalidOperationException($"Adjustment would make stock row '{BuildBatchDisplayName(batch)}' negative.");

                if (header.AdjustmentMode == "Stock Increase" && line.VarianceQty <= 0)
                    throw new InvalidOperationException("Stock Increase mode can only contain positive variance lines.");

                if (header.AdjustmentMode == "Stock Decrease" && line.VarianceQty >= 0)
                    throw new InvalidOperationException("Stock Decrease mode can only contain negative variance lines.");

                if (string.IsNullOrWhiteSpace(line.ReasonCode))
                    throw new InvalidOperationException($"Reason code is required for stock row '{BuildBatchDisplayName(batch)}'.");

                if (!AllowedReasonCodes.Contains(line.ReasonCode))
                    throw new InvalidOperationException($"Invalid reason code '{line.ReasonCode}' for stock row '{BuildBatchDisplayName(batch)}'.");

                if (line.ReasonCode.Length > 50)
                    throw new InvalidOperationException($"Reason code is too long for stock row '{BuildBatchDisplayName(batch)}'.");

                if (line.LineRemarks.Length > 250)
                    throw new InvalidOperationException($"Line remarks are too long for stock row '{BuildBatchDisplayName(batch)}'.");
            }
        }

        private static void ValidateSubmittedLineDuplicates(List<StockAdjustmentLine> lines)
        {
            var duplicate = lines
                .GroupBy(l => l.ItemBatchId)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
                throw new InvalidOperationException("Same stock row cannot appear twice in one stock adjustment.");
        }

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
                {
                    line.UnitCost = batch.CostPrice > 0
                        ? batch.CostPrice
                        : batch.ItemVariant.AverageCost > 0
                            ? batch.ItemVariant.AverageCost
                            : batch.ItemVariant.CostPrice;
                }

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

        private static async Task RecalculateVariantAverageCostsAsync(
            AppDbContext context,
            List<int> variantIds,
            DateTime now)
        {
            if (variantIds == null || !variantIds.Any())
                return;

            foreach (int variantId in variantIds.Distinct())
            {
                var variant = await context.ItemVariants
                    .FirstOrDefaultAsync(v => v.Id == variantId);

                if (variant == null)
                    continue;

                var activeBatches = await context.ItemBatches
                    .Where(b =>
                        b.ItemVariantId == variantId &&
                        !b.IsDeactivated &&
                        b.CurrentStock > 0)
                    .ToListAsync();

                decimal totalQty = activeBatches.Sum(b => b.CurrentStock);

                if (totalQty > 0)
                {
                    decimal totalValue = activeBatches.Sum(b => b.CurrentStock * b.CostPrice);
                    variant.AverageCost = Math.Round(totalValue / totalQty, 2);
                }

                variant.UpdatedAt = now;
            }
        }

        private static async Task<string> GenerateDocumentNumberAsync(AppDbContext context, string documentType)
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

        private static string TrimToMax(string value, int maxLength)
        {
            value = NormalizeText(value);
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        private static bool IsGeneralBatch(string? batchNo)
        {
            return string.Equals(NormalizeText(batchNo), GeneralBatchNo, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildBatchDisplayName(ItemBatch batch)
        {
            if (IsGeneralBatch(batch.BatchNo))
                return "GENERAL";

            if (!string.IsNullOrWhiteSpace(batch.InternalBatchBarcode))
                return $"{batch.BatchNo} / {batch.InternalBatchBarcode}";

            return batch.BatchNo;
        }
    }
}
