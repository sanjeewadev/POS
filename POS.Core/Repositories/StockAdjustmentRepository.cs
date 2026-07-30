using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Enums;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public sealed class StockAdjustmentActorContext
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public UserRole Role { get; set; }
    }

    public sealed class StockAdjustmentBatchLookupDto
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

    public sealed class StockAdjustmentHistoryRowDto
    {
        public int Id { get; set; }
        public string AdjustmentNo { get; set; } = string.Empty;
        public DateTime AdjustmentDate { get; set; }
        public string AdjustmentMode { get; set; } = string.Empty;
        public string AuthorizedBy { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public decimal TotalImpact { get; set; }
        public decimal TotalIncreaseQty { get; set; }
        public decimal TotalDecreaseQty { get; set; }
        public int LineCount { get; set; }
        public string CancelledBy { get; set; } = string.Empty;
        public string CancellationReason { get; set; } = string.Empty;
        public DateTime? CancelledAt { get; set; }
    }

    public sealed class StockAdjustmentHistoryLineDto
    {
        public int Id { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;
        public string BatchNo { get; set; } = string.Empty;
        public decimal SystemQty { get; set; }
        public decimal ActualQty { get; set; }
        public decimal VarianceQty { get; set; }
        public string ReasonCode { get; set; } = string.Empty;
        public string LineRemarks { get; set; } = string.Empty;
        public decimal UnitCost { get; set; }
        public decimal CostImpact { get; set; }
        public string LineStatus { get; set; } = string.Empty;
    }

    public sealed class StockAdjustmentHistoryDetailDto
    {
        public StockAdjustmentHistoryRowDto Header { get; set; } = new();
        public List<StockAdjustmentHistoryLineDto> Lines { get; set; } = new();
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

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            List<int> exactBatchIds = await context.ItemBatches
                .AsNoTracking()
                .Where(b =>
                    !b.IsDeactivated &&
                    !b.ItemVariant.IsDeactivated &&
                    !b.ItemVariant.ItemParent.IsDeactivated &&
                    b.ItemVariant.ItemParent.ItemType == ItemTypeCodes.StockItem &&
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

            List<int> variantIds = await context.ItemVariants
                .AsNoTracking()
                .Where(v =>
                    !v.IsDeactivated &&
                    !v.ItemParent.IsDeactivated &&
                    v.ItemParent.ItemType == ItemTypeCodes.StockItem &&
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

        public async Task<List<StockAdjustmentHistoryRowDto>> SearchHistoryAsync(
    DateTime fromDate,
    DateTime toDate,
    string? searchText,
    string? status = null)
        {
            DateTime from = fromDate.Date;
            DateTime toExclusive = toDate.Date.AddDays(1);

            if (toExclusive <= from)
                throw new InvalidOperationException("History end date must be on or after the start date.");

            string search = NormalizeText(searchText);

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            IQueryable<StockAdjustmentHeader> query = context.StockAdjustmentHeaders
                .AsNoTracking()
                .Where(h => h.AdjustmentDate >= from && h.AdjustmentDate < toExclusive);

            if (!string.IsNullOrWhiteSpace(status))
            {
                string statusUpper = status.Trim().ToUpperInvariant();
                query = query.Where(h => h.Status.ToUpper() == statusUpper);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string upper = search.ToUpperInvariant();
                query = query.Where(h =>
                    h.AdjustmentNo.ToUpper().Contains(upper) ||
                    (h.Reference ?? string.Empty).ToUpper().Contains(upper) ||
                    h.AuthorizedBy.ToUpper().Contains(upper));
            }

            return await query
                .OrderByDescending(h => h.AdjustmentDate)
                .ThenByDescending(h => h.Id)
                .Select(h => new StockAdjustmentHistoryRowDto
                {
                    Id = h.Id,
                    AdjustmentNo = h.AdjustmentNo,
                    AdjustmentDate = h.AdjustmentDate,
                    AdjustmentMode = h.AdjustmentMode,
                    AuthorizedBy = h.AuthorizedBy,
                    Reference = h.Reference,
                    TotalImpact = h.TotalImpact,
                    TotalIncreaseQty = h.TotalIncreaseQty,
                    TotalDecreaseQty = h.TotalDecreaseQty,
                    LineCount = h.AdjustmentLines.Count,
                    CancelledBy = h.CancelledBy,
                    CancellationReason = h.CancellationReason,
                    CancelledAt = h.CancelledAt
                })
                .ToListAsync();
        }

        public async Task<StockAdjustmentHistoryDetailDto?> GetHistoryDetailAsync(int adjustmentId)
        {
            if (adjustmentId <= 0)
                return null;

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            StockAdjustmentHistoryRowDto? header = await context.StockAdjustmentHeaders
                .AsNoTracking()
                .Where(h => h.Id == adjustmentId)
                .Select(h => new StockAdjustmentHistoryRowDto
                {
                    Id = h.Id,
                    AdjustmentNo = h.AdjustmentNo,
                    AdjustmentDate = h.AdjustmentDate,
                    AdjustmentMode = h.AdjustmentMode,
                    AuthorizedBy = h.AuthorizedBy,
                    Reference = h.Reference,
                    TotalImpact = h.TotalImpact,
                    TotalIncreaseQty = h.TotalIncreaseQty,
                    TotalDecreaseQty = h.TotalDecreaseQty,
                    LineCount = h.AdjustmentLines.Count,
                    CancelledBy = h.CancelledBy,
                    CancellationReason = h.CancellationReason,
                    CancelledAt = h.CancelledAt
                })
                .SingleOrDefaultAsync();

            if (header == null)
                return null;

            List<StockAdjustmentHistoryLineDto> lines = await context.StockAdjustmentLines
                .AsNoTracking()
                .Where(l => l.StockAdjustmentHeaderId == adjustmentId)
                .OrderBy(l => l.Id)
                .Select(l => new StockAdjustmentHistoryLineDto
                {
                    Id = l.Id,
                    ItemCode = l.ItemVariant.ItemParent.ItemCode,
                    Description = l.ItemVariant.ItemParent.ItemName,
                    VariantDescription = l.ItemVariant.VariantDescription,
                    BatchNo = l.ItemBatch.BatchNo,
                    SystemQty = l.SystemQty,
                    ActualQty = l.ActualQty,
                    VarianceQty = l.VarianceQty,
                    ReasonCode = l.ReasonCode,
                    LineRemarks = l.LineRemarks,
                    UnitCost = l.UnitCost,
                    CostImpact = l.CostImpact,
                    LineStatus = l.LineStatus
                })
                .ToListAsync();

            return new StockAdjustmentHistoryDetailDto
            {
                Header = header,
                Lines = lines
            };
        }

        public async Task ReverseAdjustmentAsync(int adjustmentId, string reason, StockAdjustmentActorContext actor)
        {
            if (adjustmentId <= 0)
                throw new InvalidOperationException("Invalid adjustment ID.");

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                User authorizedUser = await ValidateActorAsync(context, actor);

                var header = await context.StockAdjustmentHeaders
                    .Include(h => h.AdjustmentLines)
                    .FirstOrDefaultAsync(h => h.Id == adjustmentId);

                if (header == null)
                    throw new InvalidOperationException("Stock adjustment record was not found.");

                // Idempotency check: If already cancelled, do not double-reverse
                if (string.Equals(header.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    await transaction.CommitAsync();
                    return;
                }

                DateTime now = DateTime.Now;

                List<int> batchIds = header.AdjustmentLines.Select(l => l.ItemBatchId).Distinct().ToList();
                Dictionary<int, ItemBatch> batchMap = await context.ItemBatches
                    .Include(b => b.ItemVariant)
                    .Where(b => batchIds.Contains(b.Id))
                    .ToDictionaryAsync(b => b.Id);

                // Verify reversal will not cause negative stock quantities
                foreach (var line in header.AdjustmentLines)
                {
                    if (!batchMap.TryGetValue(line.ItemBatchId, out ItemBatch? batch))
                        throw new InvalidOperationException("Linked stock batch not found.");

                    decimal newStock = batch.CurrentStock - line.VarianceQty;
                    if (newStock < 0)
                    {
                        throw new InvalidOperationException(
                            $"Reversal would make stock for batch '{BuildBatchDisplayName(batch)}' negative.");
                    }
                }

                // Process line reversals
                foreach (var line in header.AdjustmentLines)
                {
                    ItemBatch batch = batchMap[line.ItemBatchId];
                    batch.CurrentStock -= line.VarianceQty;
                    batch.UpdatedAt = now;

                    line.LineStatus = "Reversed";
                    line.UpdatedAt = now;

                    await context.InventoryTransactions.AddAsync(new InventoryTransaction
                    {
                        ItemVariantId = batch.ItemVariantId,
                        ItemBatchId = batch.Id,
                        TransactionDate = now,
                        TransactionType = "ADJUSTMENT_REVERSAL",
                        ReferenceDocument = header.AdjustmentNo,
                        ReferenceLineId = line.Id,
                        Quantity = -line.VarianceQty,
                        UnitCost = line.UnitCost,
                        CreatedBy = authorizedUser.Username,
                        CreatedAt = now,
                        Remarks = TrimToMax($"Reversal of {header.AdjustmentNo} | Reason: {reason}", 250)
                    });
                }

                header.Status = "Cancelled";
                header.CancelledBy = authorizedUser.Username;
                header.CancellationReason = NormalizeText(reason);
                header.CancelledAt = now;
                header.UpdatedAt = now;

                await context.SaveChangesAsync();

                List<int> affectedVariantIds = batchMap.Values.Select(b => b.ItemVariantId).Distinct().ToList();
                await RecalculateVariantAverageCostsAsync(context, affectedVariantIds, now);

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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
                    !b.ItemVariant.ItemParent.IsDeactivated &&
                    b.ItemVariant.ItemParent.ItemType == ItemTypeCodes.StockItem)
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

                    BatchNo = b.BatchNo ?? string.Empty,
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
            StockAdjustmentActorContext actor,
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

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                User authorizedUser = await ValidateActorAsync(context, actor);
                DateTime now = DateTime.Now;

                List<int> batchIds = lines
                    .Select(l => l.ItemBatchId)
                    .Distinct()
                    .ToList();

                Dictionary<int, ItemBatch> batchMap = await context.ItemBatches
                    .Include(b => b.ItemVariant)
                        .ThenInclude(v => v.ItemParent)
                    .Where(b => batchIds.Contains(b.Id))
                    .ToDictionaryAsync(b => b.Id);

                ApplyAuthorizedUser(header, authorizedUser.Username);
                ValidateHeader(header);
                ValidateLines(header, lines, batchMap);
                RecalculateTotals(header, lines);

                var targetHeader = new StockAdjustmentHeader
                {
                    AdjustmentNo = await GenerateDocumentNumberAsync(context, "ADJ"),
                    AdjustmentDate = header.AdjustmentDate,
                    AdjustmentMode = header.AdjustmentMode,
                    AuthorizedBy = authorizedUser.Username,
                    Reference = header.Reference,
                    Remarks = header.Remarks,

                    TotalImpact = header.TotalImpact,
                    TotalIncreaseQty = header.TotalIncreaseQty,
                    TotalDecreaseQty = header.TotalDecreaseQty,

                    Status = "Posted",
                    CreatedBy = authorizedUser.Username,
                    PostedBy = authorizedUser.Username,
                    CreatedAt = now,
                    UpdatedAt = now,
                    PostedAt = now
                };

                await context.StockAdjustmentHeaders.AddAsync(targetHeader);
                await context.SaveChangesAsync();

                var savedLines = new List<StockAdjustmentLine>();

                foreach (StockAdjustmentLine sourceLine in lines)
                {
                    ItemBatch batch = batchMap[sourceLine.ItemBatchId];

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

                foreach (StockAdjustmentLine line in savedLines)
                {
                    ItemBatch batch = batchMap[line.ItemBatchId];
                    ApplyPostedStockChange(batch, line, now);

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
                        CreatedBy = authorizedUser.Username,
                        CreatedAt = now,
                        Remarks = TrimToMax(
                            $"Mode: {targetHeader.AdjustmentMode} | Reason: {line.ReasonCode} | Stock Row: {BuildBatchDisplayName(batch)} | Ref: {targetHeader.Reference}",
                            250)
                    });
                }

                await context.SaveChangesAsync();

                List<int> affectedVariantIds = savedLines
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

        private static async Task<User> ValidateActorAsync(
            AppDbContext context,
            StockAdjustmentActorContext actor)
        {
            if (actor == null || actor.UserId <= 0 || string.IsNullOrWhiteSpace(actor.Username))
                throw new InvalidOperationException("An authenticated Manager or Administrator is required.");

            string username = NormalizeText(actor.Username);

            User? user = await context.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.Id == actor.UserId && u.Username == username);

            if (user == null)
                throw new InvalidOperationException("The authenticated user could not be verified.");

            if (!user.IsActive)
                throw new InvalidOperationException("The authenticated user account is suspended.");

            if (user.Role != UserRole.Manager && user.Role != UserRole.Admin)
                throw new InvalidOperationException("Manager or Administrator privileges are required for stock adjustments.");

            if (actor.Role != user.Role)
                throw new InvalidOperationException("The authenticated user role changed. Sign in again before posting.");

            return user;
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

        }

        private static void ValidateLines(
            StockAdjustmentHeader header,
            List<StockAdjustmentLine> lines,
            Dictionary<int, ItemBatch> batchMap)
        {
            foreach (StockAdjustmentLine line in lines)
            {
                NormalizeLine(line);

                if (!batchMap.TryGetValue(line.ItemBatchId, out ItemBatch? batch))
                    throw new InvalidOperationException("One or more stock rows were not found.");

                if (batch.IsDeactivated)
                    throw new InvalidOperationException($"Stock row '{BuildBatchDisplayName(batch)}' is deactivated.");

                if (batch.ItemVariant == null || batch.ItemVariant.ItemParent == null)
                    throw new InvalidOperationException($"Stock row '{BuildBatchDisplayName(batch)}' is not linked to a valid item.");

                if (batch.ItemVariant.IsDeactivated || batch.ItemVariant.ItemParent.IsDeactivated)
                    throw new InvalidOperationException($"Item linked to stock row '{BuildBatchDisplayName(batch)}' is deactivated.");

                if (!string.Equals(
                        batch.ItemVariant.ItemParent.ItemType,
                        ItemTypeCodes.StockItem,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Service linked to stock row '{BuildBatchDisplayName(batch)}' cannot be adjusted as inventory.");
                }

                bool isGeneral = IsGeneralBatch(batch.BatchNo);
                bool isBatchTracked = batch.ItemVariant.ItemParent.HasBatchTracking;

                if (isBatchTracked && isGeneral)
                    throw new InvalidOperationException("Batch-tracked items cannot be adjusted through GENERAL. Select the exact GRN batch.");

                if (!isBatchTracked && !isGeneral)
                    throw new InvalidOperationException("Average-cost items must be adjusted through the GENERAL stock bucket.");

                line.ItemVariantId = batch.ItemVariantId;
                line.VarianceQty = line.ActualQty - line.SystemQty;

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

                PrepareLineUnitCost(line, batch);
                line.CostImpact = Math.Round(line.VarianceQty * line.UnitCost, 2);

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

        private static void PrepareLineUnitCost(StockAdjustmentLine line, ItemBatch batch)
        {
            decimal existingCost = ResolveExistingCost(batch);
            bool isBatchTracked = batch.ItemVariant.ItemParent.HasBatchTracking;

            if (line.VarianceQty > 0)
            {
                if (isBatchTracked && batch.CurrentStock > 0 && existingCost > 0)
                {
                    line.UnitCost = existingCost;
                }
                else if (line.UnitCost <= 0)
                {
                    throw new InvalidOperationException(
                        $"A positive unit cost is required for stock increase on '{BuildBatchDisplayName(batch)}'.");
                }
            }
            else
            {
                if (existingCost > 0)
                    line.UnitCost = existingCost;
                else if (line.UnitCost <= 0)
                {
                    throw new InvalidOperationException(
                        $"The existing stock row '{BuildBatchDisplayName(batch)}' has no valid cost. Enter a positive unit cost before posting.");
                }
            }

            if (line.UnitCost <= 0)
                throw new InvalidOperationException($"Unit cost must be greater than zero for stock row '{BuildBatchDisplayName(batch)}'.");
        }

        private static void ApplyPostedStockChange(ItemBatch batch, StockAdjustmentLine line, DateTime now)
        {
            decimal oldQuantity = batch.CurrentStock;
            decimal newQuantity = oldQuantity + line.VarianceQty;

            if (newQuantity < 0)
                throw new InvalidOperationException($"Posting this adjustment would make stock row '{BuildBatchDisplayName(batch)}' negative.");

            bool isGeneral = IsGeneralBatch(batch.BatchNo);
            decimal existingCost = ResolveExistingCost(batch);

            if (isGeneral)
            {
                if (existingCost <= 0)
                {
                    batch.CostPrice = line.UnitCost;
                }
                else if (line.VarianceQty > 0 && newQuantity > 0)
                {
                    decimal oldValue = oldQuantity * existingCost;
                    decimal addedValue = line.VarianceQty * line.UnitCost;
                    batch.CostPrice = Math.Round((oldValue + addedValue) / newQuantity, 2);
                }
            }
            else if (line.VarianceQty > 0 && (oldQuantity <= 0 || batch.CostPrice <= 0))
            {
                batch.CostPrice = line.UnitCost;
            }

            batch.CurrentStock = newQuantity;
            batch.UpdatedAt = now;
        }

        private static void ValidateSubmittedLineDuplicates(List<StockAdjustmentLine> lines)
        {
            IGrouping<int, StockAdjustmentLine>? duplicate = lines
                .GroupBy(l => l.ItemBatchId)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
                throw new InvalidOperationException("Same stock row cannot appear twice in one stock adjustment.");
        }

        private static void RecalculateTotals(
            StockAdjustmentHeader header,
            List<StockAdjustmentLine> lines)
        {
            header.TotalImpact = Math.Round(lines.Sum(l => l.CostImpact), 2);
            header.TotalIncreaseQty = lines.Where(l => l.VarianceQty > 0).Sum(l => l.VarianceQty);
            header.TotalDecreaseQty = lines.Where(l => l.VarianceQty < 0).Sum(l => Math.Abs(l.VarianceQty));
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
                ItemVariant? variant = await context.ItemVariants
                    .FirstOrDefaultAsync(v => v.Id == variantId);

                if (variant == null)
                    continue;

                List<ItemBatch> activeBatches = await context.ItemBatches
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

                    if (variant.CostPrice <= 0 && variant.AverageCost > 0)
                        variant.CostPrice = variant.AverageCost;
                }

                variant.UpdatedAt = now;
            }
        }

        private static async Task<string> GenerateDocumentNumberAsync(AppDbContext context, string documentType)
        {
            DocumentSequence? sequence = await context.DocumentSequences
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

        private static void ApplyAuthorizedUser(StockAdjustmentHeader header, string username)
        {
            header.AuthorizedBy = username;
            header.CreatedBy = username;
            header.PostedBy = username;
        }

        private static void NormalizeHeader(StockAdjustmentHeader header)
        {
            header.AdjustmentMode = NormalizeText(header.AdjustmentMode);
            header.AuthorizedBy = NormalizeText(header.AuthorizedBy);
            header.Reference = NormalizeText(header.Reference);
            header.Remarks = NormalizeText(header.Remarks);
            header.CreatedBy = NormalizeText(header.CreatedBy);
            header.PostedBy = NormalizeText(header.PostedBy);
        }

        private static void NormalizeLines(List<StockAdjustmentLine> lines)
        {
            foreach (StockAdjustmentLine line in lines)
                NormalizeLine(line);
        }

        private static void NormalizeLine(StockAdjustmentLine line)
        {
            line.ReasonCode = NormalizeText(line.ReasonCode);
            line.LineRemarks = NormalizeText(line.LineRemarks);
        }

        private static decimal ResolveExistingCost(ItemBatch batch)
        {
            if (batch.CostPrice > 0)
                return batch.CostPrice;

            if (batch.ItemVariant.AverageCost > 0)
                return batch.ItemVariant.AverageCost;

            return batch.ItemVariant.CostPrice;
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
