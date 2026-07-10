using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public class SupplierLookupDto
    {
        public int Id { get; set; }

        public string SupplierCode { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;

        public decimal CurrentBalance { get; set; }

        public string DisplayText
        {
            get
            {
                string code = NormalizeText(SupplierCode);
                string name = NormalizeText(SupplierName);
                string company = NormalizeText(CompanyName);

                string main = string.IsNullOrWhiteSpace(code)
                    ? name
                    : $"{code} - {name}";

                if (!string.IsNullOrWhiteSpace(company) &&
                    !company.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{main} ({company})";
                }

                return main;
            }
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }

    public class SupplierInvoiceLookupDto
    {
        public int Id { get; set; }

        public string GrnNumber { get; set; } = string.Empty;
        public string SupplierInvoiceNo { get; set; } = string.Empty;

        public DateTime InvoiceDate { get; set; }
        public DateTime ReceivedDate { get; set; }

        public decimal NetPayable { get; set; }

        public int ReturnableLineCount { get; set; }
        public decimal ReturnableQty { get; set; }

        public string DisplayText =>
            $"{SupplierInvoiceNo} | {GrnNumber} | {ReceivedDate:yyyy-MM-dd} | Returnable: {ReturnableQty:0.###}";
    }

    public class SupplierReturnSourceDto
    {
        public int GrnHeaderId { get; set; }
        public int GrnLineId { get; set; }

        public int ItemVariantId { get; set; }
        public int ItemBatchId { get; set; }

        public string GrnNumber { get; set; } = string.Empty;
        public string SupplierInvoiceNo { get; set; } = string.Empty;

        public string ItemCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string VariantDescription { get; set; } = string.Empty;

        public bool HasBatchTracking { get; set; }
        public bool HasExpiryTracking { get; set; }
        public bool IsGeneralStockBucket { get; set; }

        public string TrackingText
        {
            get
            {
                if (!HasBatchTracking)
                    return "Average Cost";

                return HasExpiryTracking ? "Batch + Expiry" : "Batch";
            }
        }

        public string BatchNo { get; set; } = string.Empty;
        public string InternalBatchBarcode { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }

        public string BatchDisplayText =>
            IsGeneralStockBucket ? "GENERAL" : BatchNo;

        public string BatchBarcodeDisplayText =>
            IsGeneralStockBucket ? "-" : InternalBatchBarcode;

        public decimal ReceivedQty { get; set; }
        public decimal AlreadyReturnedQty { get; set; }
        public decimal CurrentBatchStock { get; set; }
        public decimal MaxReturnQty { get; set; }

        public decimal HistoricalCost { get; set; }

        public decimal CreditValue => Math.Round(MaxReturnQty * HistoricalCost, 2);
    }

    public class SupplierReturnRepository
    {
        private const string GeneralBatchNo = "GENERAL";

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SupplierReturnRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        // =========================================================
        // LOOKUPS
        // =========================================================

        public async Task<List<SupplierLookupDto>> GetActiveSuppliersAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Suppliers
                .AsNoTracking()
                .Where(s => !s.IsDeactivated)
                .OrderBy(s => s.SupplierCode)
                .ThenBy(s => s.SupplierName)
                .Select(s => new SupplierLookupDto
                {
                    Id = s.Id,
                    SupplierCode = s.SupplierCode,
                    SupplierName = s.SupplierName,
                    CompanyName = s.CompanyName,
                    CurrentBalance = s.CurrentBalance
                })
                .ToListAsync();
        }

        public async Task<List<SupplierInvoiceLookupDto>> GetSupplierInvoicesAsync(int supplierId)
        {
            if (supplierId <= 0)
                return new List<SupplierInvoiceLookupDto>();

            using var context = await _contextFactory.CreateDbContextAsync();

            var grns = await context.GrnHeaders
                .AsNoTracking()
                .Where(g =>
                    g.SupplierId == supplierId &&
                    g.Status == "Posted")
                .OrderByDescending(g => g.ReceivedDate)
                .ThenByDescending(g => g.GrnNumber)
                .Take(300)
                .Select(g => new
                {
                    g.Id,
                    g.GrnNumber,
                    g.SupplierInvoiceNo,
                    g.InvoiceDate,
                    g.ReceivedDate,
                    g.NetPayable
                })
                .ToListAsync();

            if (!grns.Any())
                return new List<SupplierInvoiceLookupDto>();

            var grnIds = grns
                .Select(g => g.Id)
                .ToList();

            var grnLines = await context.GrnLines
                .AsNoTracking()
                .Where(l =>
                    grnIds.Contains(l.GrnHeaderId) &&
                    l.ReceivedQty > 0 &&
                    l.ItemBatchId.HasValue)
                .Select(l => new
                {
                    l.Id,
                    l.GrnHeaderId,
                    l.ItemVariantId,
                    ItemBatchId = l.ItemBatchId!.Value,
                    l.ReceivedQty
                })
                .ToListAsync();

            if (!grnLines.Any())
                return new List<SupplierInvoiceLookupDto>();

            var grnLineIds = grnLines
                .Select(l => l.Id)
                .ToList();

            var batchIds = grnLines
                .Select(l => l.ItemBatchId)
                .Distinct()
                .ToList();

            var previousReturnRows = await context.SupplierReturnLines
                .Include(l => l.ReturnHeader)
                .AsNoTracking()
                .Where(l =>
                    l.GrnLineId.HasValue &&
                    grnLineIds.Contains(l.GrnLineId.Value) &&
                    l.ReturnHeader != null &&
                    l.ReturnHeader.Status == "Posted")
                .Select(l => new
                {
                    GrnLineId = l.GrnLineId!.Value,
                    l.ReturnQty
                })
                .ToListAsync();

            var returnedByGrnLine = previousReturnRows
                .GroupBy(x => x.GrnLineId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.ReturnQty));

            var batches = await context.ItemBatches
                .Include(b => b.ItemVariant)
                    .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .Where(b =>
                    batchIds.Contains(b.Id) &&
                    !b.IsDeactivated &&
                    !b.ItemVariant.IsDeactivated &&
                    !b.ItemVariant.ItemParent.IsDeactivated)
                .ToDictionaryAsync(b => b.Id);

            var result = new List<SupplierInvoiceLookupDto>();

            foreach (var grn in grns)
            {
                var linesForGrn = grnLines
                    .Where(l => l.GrnHeaderId == grn.Id)
                    .ToList();

                int returnableLineCount = 0;
                decimal totalReturnableQty = 0m;

                foreach (var line in linesForGrn)
                {
                    if (!batches.TryGetValue(line.ItemBatchId, out var batch))
                        continue;

                    if (!IsValidStockBucketForItem(batch))
                        continue;

                    decimal alreadyReturned = returnedByGrnLine.TryGetValue(line.Id, out decimal returned)
                        ? returned
                        : 0m;

                    decimal remainingFromReceipt = line.ReceivedQty - alreadyReturned;

                    if (remainingFromReceipt <= 0)
                        continue;

                    if (batch.CurrentStock <= 0)
                        continue;

                    decimal returnableQty = Math.Min(remainingFromReceipt, batch.CurrentStock);

                    if (returnableQty <= 0)
                        continue;

                    returnableLineCount++;
                    totalReturnableQty += returnableQty;
                }

                if (returnableLineCount <= 0 || totalReturnableQty <= 0)
                    continue;

                result.Add(new SupplierInvoiceLookupDto
                {
                    Id = grn.Id,
                    GrnNumber = grn.GrnNumber,
                    SupplierInvoiceNo = grn.SupplierInvoiceNo,
                    InvoiceDate = grn.InvoiceDate,
                    ReceivedDate = grn.ReceivedDate,
                    NetPayable = grn.NetPayable,
                    ReturnableLineCount = returnableLineCount,
                    ReturnableQty = totalReturnableQty
                });
            }

            return result
                .OrderByDescending(g => g.ReceivedDate)
                .ThenByDescending(g => g.GrnNumber)
                .Take(100)
                .ToList();
        }

        public async Task<List<SupplierReturnSourceDto>> GetReturnableBatchesForGrnAsync(int grnHeaderId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var grn = await context.GrnHeaders
                .AsNoTracking()
                .FirstOrDefaultAsync(g =>
                    g.Id == grnHeaderId &&
                    g.Status == "Posted");

            if (grn == null)
                throw new InvalidOperationException("Posted GRN was not found.");

            var grnLines = await context.GrnLines
                .Include(l => l.ItemVariant)
                    .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .Where(l =>
                    l.GrnHeaderId == grnHeaderId &&
                    l.ReceivedQty > 0)
                .OrderBy(l => l.ItemVariant!.ItemParent!.ItemCode)
                .ThenBy(l => l.ItemVariant!.VariantDescription)
                .ToListAsync();

            if (!grnLines.Any())
                return new List<SupplierReturnSourceDto>();

            var exactBatchIds = grnLines
                .Where(l => l.ItemBatchId.HasValue)
                .Select(l => l.ItemBatchId!.Value)
                .Distinct()
                .ToList();

            var variantIds = grnLines
                .Select(l => l.ItemVariantId)
                .Distinct()
                .ToList();

            var batchNos = grnLines
                .Select(l => NormalizeText(l.BatchNo).ToUpperInvariant())
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Distinct()
                .ToList();

            var batches = await context.ItemBatches
                .Include(b => b.ItemVariant)
                    .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .Where(b =>
                    !b.IsDeactivated &&
                    !b.ItemVariant.IsDeactivated &&
                    !b.ItemVariant.ItemParent.IsDeactivated &&
                    (
                        exactBatchIds.Contains(b.Id) ||
                        (
                            variantIds.Contains(b.ItemVariantId) &&
                            batchNos.Contains(b.BatchNo.ToUpper())
                        )
                    ))
                .ToListAsync();

            var grnLineIds = grnLines
                .Select(l => l.Id)
                .ToList();

            var previousReturnRows = await context.SupplierReturnLines
                .Include(l => l.ReturnHeader)
                .AsNoTracking()
                .Where(l =>
                    l.GrnLineId.HasValue &&
                    grnLineIds.Contains(l.GrnLineId.Value) &&
                    l.ReturnHeader != null &&
                    l.ReturnHeader.Status == "Posted")
                .Select(l => new
                {
                    GrnLineId = l.GrnLineId!.Value,
                    l.ReturnQty
                })
                .ToListAsync();

            var returnedByGrnLine = previousReturnRows
                .GroupBy(x => x.GrnLineId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.ReturnQty));

            var result = new List<SupplierReturnSourceDto>();

            foreach (var line in grnLines)
            {
                ItemBatch? batch = null;

                if (line.ItemBatchId.HasValue)
                {
                    batch = batches.FirstOrDefault(b => b.Id == line.ItemBatchId.Value);
                }

                if (batch == null)
                {
                    string lineBatchNo = NormalizeText(line.BatchNo);

                    batch = batches.FirstOrDefault(b =>
                        b.ItemVariantId == line.ItemVariantId &&
                        b.BatchNo.Equals(lineBatchNo, StringComparison.OrdinalIgnoreCase));
                }

                if (batch == null)
                    continue;

                if (!IsValidStockBucketForItem(batch))
                    continue;

                decimal alreadyReturned = returnedByGrnLine.TryGetValue(line.Id, out decimal returned)
                    ? returned
                    : 0m;

                decimal remainingFromOriginalReceipt = line.ReceivedQty - alreadyReturned;

                if (remainingFromOriginalReceipt <= 0)
                    continue;

                decimal maxReturnQty = Math.Min(remainingFromOriginalReceipt, batch.CurrentStock);

                if (maxReturnQty <= 0)
                    continue;

                decimal historicalCost = line.LandedCost > 0
                    ? line.LandedCost
                    : line.UnitCost;

                if (historicalCost <= 0)
                {
                    historicalCost = batch.CostPrice > 0
                        ? batch.CostPrice
                        : line.ItemVariant?.AverageCost ?? 0m;
                }

                bool hasBatchTracking = batch.ItemVariant.ItemParent.HasBatchTracking;
                bool hasExpiryTracking = batch.ItemVariant.ItemParent.HasExpiryTracking || batch.ItemVariant.ItemParent.HasBatchExpiry;
                bool isGeneral = IsGeneralBatch(batch.BatchNo);

                result.Add(new SupplierReturnSourceDto
                {
                    GrnHeaderId = grn.Id,
                    GrnLineId = line.Id,

                    ItemVariantId = line.ItemVariantId,
                    ItemBatchId = batch.Id,

                    GrnNumber = grn.GrnNumber,
                    SupplierInvoiceNo = grn.SupplierInvoiceNo,

                    ItemCode = line.ItemVariant?.ItemParent?.ItemCode ?? string.Empty,
                    Description = line.ItemVariant?.ItemParent?.ItemName ?? string.Empty,
                    VariantDescription = string.IsNullOrWhiteSpace(line.ItemVariant?.VariantDescription)
                        ? "Standard"
                        : line.ItemVariant!.VariantDescription,

                    HasBatchTracking = hasBatchTracking,
                    HasExpiryTracking = hasExpiryTracking,
                    IsGeneralStockBucket = isGeneral,

                    BatchNo = batch.BatchNo,
                    InternalBatchBarcode = batch.InternalBatchBarcode ?? string.Empty,
                    ExpiryDate = batch.ExpiryDate,

                    ReceivedQty = line.ReceivedQty,
                    AlreadyReturnedQty = alreadyReturned,
                    CurrentBatchStock = batch.CurrentStock,
                    MaxReturnQty = maxReturnQty,

                    HistoricalCost = Math.Round(historicalCost, 2)
                });
            }

            return result
                .OrderBy(r => r.ItemCode)
                .ThenBy(r => r.VariantDescription)
                .ThenBy(r => r.IsGeneralStockBucket ? 0 : 1)
                .ThenBy(r => r.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(r => r.BatchNo)
                .ToList();
        }

        public List<string> GetReasonCodes()
        {
            return new List<string>
            {
                "Damaged / Defective",
                "Expired / Spoiled",
                "Wrong Item Supplied",
                "Excess Quantity Supplied",
                "Supplier Recall",
                "Quality Issue",
                "Price Dispute",
                "Other"
            };
        }

        // =========================================================
        // POST SUPPLIER RETURN
        // =========================================================

        public async Task PostSupplierReturnAsync(
            SupplierReturnHeader header,
            List<SupplierReturnLine> lines)
        {
            if (header == null)
                throw new ArgumentNullException(nameof(header));

            if (lines == null || !lines.Any())
                throw new InvalidOperationException("Supplier return must contain at least one line.");

            NormalizeHeader(header);
            NormalizeLines(lines);
            ValidateSubmittedLineDuplicates(lines);
            RecalculateHeaderTotals(header, lines);

            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                await ValidateHeaderAsync(context, header);
                await ValidateLinesAsync(context, header, lines);

                DateTime now = DateTime.Now;

                header.Id = 0;
                header.ReturnNumber = await GenerateDocumentNumberAsync(context, "RTN");
                header.Status = "Posted";
                header.CreatedAt = now;
                header.UpdatedAt = now;
                header.PostedAt = now;

                if (string.IsNullOrWhiteSpace(header.CreatedBy))
                    header.CreatedBy = header.AuthorizedBy;

                if (string.IsNullOrWhiteSpace(header.PostedBy))
                    header.PostedBy = header.AuthorizedBy;

                header.Supplier = null!;
                header.GrnHeader = null;
                header.ReturnLines = new List<SupplierReturnLine>();

                await context.SupplierReturnHeaders.AddAsync(header);
                await context.SaveChangesAsync();

                foreach (var line in lines)
                {
                    PrepareNewLine(line, header.Id, now);
                    await context.SupplierReturnLines.AddAsync(line);
                }

                await context.SaveChangesAsync();

                await PostInventoryAndLedgerAsync(context, header, lines, now);

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Compatibility method for older callers.
        public async Task SaveSupplierReturnAsync(
            SupplierReturnHeader header,
            List<SupplierReturnLine> lines,
            bool isDraft)
        {
            if (isDraft)
                throw new InvalidOperationException("Draft supplier returns are not supported. Please post the supplier return directly.");

            await PostSupplierReturnAsync(header, lines);
        }

        private static async Task PostInventoryAndLedgerAsync(
            AppDbContext context,
            SupplierReturnHeader header,
            List<SupplierReturnLine> lines,
            DateTime now)
        {
            var batchIds = lines
                .Select(l => l.ItemBatchId)
                .Distinct()
                .ToList();

            var batches = await context.ItemBatches
                .Include(b => b.ItemVariant)
                    .ThenInclude(v => v.ItemParent)
                .Where(b => batchIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id);

            foreach (var line in lines)
            {
                if (!batches.TryGetValue(line.ItemBatchId, out var batch))
                    throw new InvalidOperationException($"Stock row ID {line.ItemBatchId} was not found.");

                if (batch.IsDeactivated)
                    throw new InvalidOperationException($"Stock row '{BuildBatchDisplayName(batch)}' is deactivated.");

                if (!IsValidStockBucketForItem(batch))
                {
                    throw new InvalidOperationException(
                        $"Stock row '{BuildBatchDisplayName(batch)}' does not match the item tracking method.");
                }

                if (batch.CurrentStock < line.ReturnQty)
                {
                    throw new InvalidOperationException(
                        $"Insufficient stock in '{BuildBatchDisplayName(batch)}'. Current stock is {batch.CurrentStock:N3}, return quantity is {line.ReturnQty:N3}.");
                }

                batch.CurrentStock -= line.ReturnQty;
                batch.UpdatedAt = now;

                line.LineStatus = "Posted";
                line.UpdatedAt = now;

                var inventoryTx = new InventoryTransaction
                {
                    ItemVariantId = batch.ItemVariantId,
                    ItemBatchId = batch.Id,
                    TransactionDate = header.ReturnDate,
                    TransactionType = "SUPPLIER_RETURN",
                    ReferenceDocument = header.ReturnNumber,
                    ReferenceLineId = line.Id,
                    Quantity = -line.ReturnQty,
                    UnitCost = line.HistoricalCost,
                    CreatedBy = header.AuthorizedBy,
                    CreatedAt = now,
                    Remarks = TrimToMax(
                        $"Supplier Return | Reason: {line.ReasonCode} | Stock Row: {BuildBatchDisplayName(batch)}",
                        250)
                };

                await context.InventoryTransactions.AddAsync(inventoryTx);
            }

            var supplier = await context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == header.SupplierId)
                ?? throw new InvalidOperationException("Supplier was not found.");

            decimal newBalance = supplier.CurrentBalance - header.NetCredit;

            var ledger = new SupplierLedger
            {
                SupplierId = supplier.Id,
                GrnHeaderId = header.GrnHeaderId,
                TransactionDate = header.ReturnDate,
                TransactionType = "DEBIT_NOTE",
                ReferenceDocument = header.ReturnNumber,
                ChargeAmount = 0m,
                PaymentAmount = header.NetCredit,
                BalanceAfterTransaction = newBalance,
                DueDate = header.ReturnDate,
                IsPaid = true,
                CreatedBy = header.AuthorizedBy,
                CreatedAt = now,
                Remarks = TrimToMax(
                    $"Supplier Return | Original Invoice: {header.OriginalInvoiceNo} | Gross: {header.GrossCredit:N2} | Restocking Fee: {header.RestockingFee:N2}",
                    250)
            };

            supplier.CurrentBalance = newBalance;
            supplier.UpdatedAt = now;

            await context.SupplierLedgers.AddAsync(ledger);

            var affectedVariantIds = lines
                .Select(l => l.ItemVariantId)
                .Distinct()
                .ToList();

            await RecalculateVariantAverageCostsAsync(context, affectedVariantIds, now);
        }

        // =========================================================
        // VALIDATION
        // =========================================================

        private static async Task ValidateHeaderAsync(
            AppDbContext context,
            SupplierReturnHeader header)
        {
            if (header.SupplierId <= 0)
                throw new InvalidOperationException("Supplier is required.");

            bool supplierExists = await context.Suppliers.AnyAsync(s =>
                s.Id == header.SupplierId &&
                !s.IsDeactivated);

            if (!supplierExists)
                throw new InvalidOperationException("Selected supplier is inactive or missing.");

            if (!header.GrnHeaderId.HasValue || header.GrnHeaderId.Value <= 0)
                throw new InvalidOperationException("Supplier return must be linked to a posted GRN.");

            var grn = await context.GrnHeaders
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == header.GrnHeaderId.Value);

            if (grn == null)
                throw new InvalidOperationException("Linked GRN was not found.");

            if (grn.Status != "Posted")
                throw new InvalidOperationException("Only posted GRNs can be used for supplier returns.");

            if (grn.SupplierId != header.SupplierId)
                throw new InvalidOperationException("Linked GRN supplier does not match selected supplier.");

            if (header.ReturnDate.Date > DateTime.Now.Date.AddDays(1))
                throw new InvalidOperationException("Return date cannot be in the far future.");

            if (string.IsNullOrWhiteSpace(header.AuthorizedBy))
                throw new InvalidOperationException("Authorized by is required.");

            if (header.AuthorizedBy.Length > 50)
                throw new InvalidOperationException("Authorized by cannot be longer than 50 characters.");

            if (header.Remarks.Length > 500)
                throw new InvalidOperationException("Remarks cannot be longer than 500 characters.");

            if (header.OriginalInvoiceNo.Length > 50)
                throw new InvalidOperationException("Original invoice number cannot be longer than 50 characters.");

            if (header.RestockingFee < 0)
                throw new InvalidOperationException("Restocking fee cannot be negative.");

            if (header.GrossCredit <= 0)
                throw new InvalidOperationException("Gross return value must be greater than zero.");

            if (header.RestockingFee > header.GrossCredit)
                throw new InvalidOperationException("Restocking fee cannot be greater than gross return value.");

            if (header.NetCredit < 0)
                throw new InvalidOperationException("Net credit cannot be negative.");
        }

        private static async Task ValidateLinesAsync(
            AppDbContext context,
            SupplierReturnHeader header,
            List<SupplierReturnLine> lines)
        {
            var variantIds = lines
                .Select(l => l.ItemVariantId)
                .Distinct()
                .ToList();

            var batchIds = lines
                .Select(l => l.ItemBatchId)
                .Distinct()
                .ToList();

            var grnLineIds = lines
                .Where(l => l.GrnLineId.HasValue)
                .Select(l => l.GrnLineId!.Value)
                .Distinct()
                .ToList();

            if (grnLineIds.Count != lines.Count)
                throw new InvalidOperationException("Every supplier return line must be linked to a GRN line.");

            var variants = await context.ItemVariants
                .Include(v => v.ItemParent)
                .Where(v => variantIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id);

            var batches = await context.ItemBatches
                .Include(b => b.ItemVariant)
                    .ThenInclude(v => v.ItemParent)
                .Where(b => batchIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id);

            var grnLines = await context.GrnLines
                .Where(l => grnLineIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id);

            var previousReturnRows = await context.SupplierReturnLines
                .Include(l => l.ReturnHeader)
                .AsNoTracking()
                .Where(l =>
                    l.GrnLineId.HasValue &&
                    grnLineIds.Contains(l.GrnLineId.Value) &&
                    l.ReturnHeader != null &&
                    l.ReturnHeader.Status == "Posted")
                .Select(l => new
                {
                    GrnLineId = l.GrnLineId!.Value,
                    l.ReturnQty
                })
                .ToListAsync();

            var returnedByGrnLine = previousReturnRows
                .GroupBy(x => x.GrnLineId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.ReturnQty));

            foreach (var line in lines)
            {
                NormalizeLine(line);

                if (line.ItemVariantId <= 0)
                    throw new InvalidOperationException("Invalid item variant in supplier return line.");

                if (!variants.TryGetValue(line.ItemVariantId, out var variant))
                    throw new InvalidOperationException("One or more item variants were not found.");

                if (variant.IsDeactivated || variant.ItemParent.IsDeactivated)
                    throw new InvalidOperationException($"Item '{variant.SkuCode}' is inactive.");

                if (line.ItemBatchId <= 0)
                    throw new InvalidOperationException($"Stock row is required for item '{variant.SkuCode}'.");

                if (!batches.TryGetValue(line.ItemBatchId, out var batch))
                    throw new InvalidOperationException($"Stock row was not found for item '{variant.SkuCode}'.");

                if (batch.ItemVariantId != line.ItemVariantId)
                    throw new InvalidOperationException($"Stock row does not match item '{variant.SkuCode}'.");

                if (batch.IsDeactivated)
                    throw new InvalidOperationException($"Stock row '{BuildBatchDisplayName(batch)}' is deactivated.");

                if (!IsValidStockBucketForItem(batch))
                {
                    throw new InvalidOperationException(
                        $"Stock row '{BuildBatchDisplayName(batch)}' does not match the item tracking method.");
                }

                if (!line.GrnLineId.HasValue || line.GrnLineId.Value <= 0)
                    throw new InvalidOperationException($"GRN line is required for item '{variant.SkuCode}'.");

                if (!grnLines.TryGetValue(line.GrnLineId.Value, out var grnLine))
                    throw new InvalidOperationException($"Linked GRN line was not found for item '{variant.SkuCode}'.");

                if (!header.GrnHeaderId.HasValue ||
                    grnLine.GrnHeaderId != header.GrnHeaderId.Value)
                {
                    throw new InvalidOperationException($"GRN line does not belong to the selected GRN for item '{variant.SkuCode}'.");
                }

                if (grnLine.ItemVariantId != line.ItemVariantId)
                    throw new InvalidOperationException($"GRN line item does not match return item '{variant.SkuCode}'.");

                if (grnLine.ItemBatchId.HasValue &&
                    grnLine.ItemBatchId.Value != line.ItemBatchId)
                {
                    throw new InvalidOperationException($"Return stock row does not match the original GRN stock row for item '{variant.SkuCode}'.");
                }

                if (line.ReturnQty <= 0)
                    throw new InvalidOperationException($"Return quantity must be greater than zero for item '{variant.SkuCode}'.");

                if (line.ReturnQty > batch.CurrentStock)
                {
                    throw new InvalidOperationException(
                        $"Cannot return {line.ReturnQty:N3} for item '{variant.SkuCode}'. Stock row has only {batch.CurrentStock:N3}.");
                }

                decimal alreadyReturned = returnedByGrnLine.TryGetValue(grnLine.Id, out decimal returned)
                    ? returned
                    : 0m;

                decimal remainingFromReceipt = grnLine.ReceivedQty - alreadyReturned;

                if (remainingFromReceipt < 0)
                    remainingFromReceipt = 0m;

                if (line.ReturnQty > remainingFromReceipt)
                {
                    throw new InvalidOperationException(
                        $"Cannot return {line.ReturnQty:N3} for item '{variant.SkuCode}'. Remaining returnable GRN quantity is {remainingFromReceipt:N3}.");
                }

                decimal historicalCost = grnLine.LandedCost > 0
                    ? grnLine.LandedCost
                    : grnLine.UnitCost;

                if (historicalCost <= 0)
                {
                    historicalCost = batch.CostPrice > 0
                        ? batch.CostPrice
                        : variant.AverageCost > 0
                            ? variant.AverageCost
                            : variant.CostPrice;
                }

                if (historicalCost <= 0)
                    throw new InvalidOperationException($"Historical cost must be greater than zero for item '{variant.SkuCode}'.");

                if (string.IsNullOrWhiteSpace(line.ReasonCode))
                    throw new InvalidOperationException($"Reason code is required for item '{variant.SkuCode}'.");

                if (line.ReasonCode.Length > 50)
                    throw new InvalidOperationException($"Reason code is too long for item '{variant.SkuCode}'.");

                if (line.LineRemarks.Length > 250)
                    throw new InvalidOperationException($"Line remarks are too long for item '{variant.SkuCode}'.");

                line.BatchNo = batch.BatchNo;
                line.ExpiryDate = batch.ExpiryDate;
                line.HistoricalCost = Math.Round(historicalCost, 2);
                line.CreditValue = Math.Round(line.ReturnQty * line.HistoricalCost, 2);
            }
        }

        private static void ValidateSubmittedLineDuplicates(List<SupplierReturnLine> lines)
        {
            var duplicate = lines
                .GroupBy(l => new
                {
                    l.GrnLineId,
                    l.ItemBatchId
                })
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
                throw new InvalidOperationException("Duplicate return line found. The same GRN stock row can appear only once in one supplier return.");
        }

        // =========================================================
        // CALCULATION
        // =========================================================

        private static void RecalculateHeaderTotals(
            SupplierReturnHeader header,
            List<SupplierReturnLine> lines)
        {
            decimal gross = 0m;

            foreach (var line in lines)
            {
                line.CreditValue = Math.Round(line.ReturnQty * line.HistoricalCost, 2);
                gross += line.CreditValue;
            }

            header.GrossCredit = Math.Round(gross, 2);

            if (header.RestockingFee < 0)
                throw new InvalidOperationException("Restocking fee cannot be negative.");

            if (header.RestockingFee > header.GrossCredit)
                throw new InvalidOperationException("Restocking fee cannot be greater than gross return value.");

            header.NetCredit = Math.Round(header.GrossCredit - header.RestockingFee, 2);
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

            string number =
                $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString().PadLeft(sequence.PaddingLength, '0')}";

            sequence.NextSequenceNumber++;
            sequence.UpdatedAt = DateTime.Now;

            await context.SaveChangesAsync();

            return number;
        }

        private static void PrepareNewLine(
            SupplierReturnLine line,
            int headerId,
            DateTime now)
        {
            line.Id = 0;
            line.ReturnHeaderId = headerId;

            line.ReturnHeader = null;
            line.GrnLine = null;
            line.ItemVariant = null;
            line.ItemBatch = null;

            line.LineStatus = "Posted";
            line.CreatedAt = now;
            line.UpdatedAt = now;
        }

        private static void NormalizeHeader(SupplierReturnHeader header)
        {
            header.ReturnNumber = NormalizeText(header.ReturnNumber);
            header.OriginalInvoiceNo = NormalizeText(header.OriginalInvoiceNo);
            header.AuthorizedBy = NormalizeText(header.AuthorizedBy);
            header.Remarks = NormalizeText(header.Remarks);
            header.CreatedBy = NormalizeText(header.CreatedBy);
            header.PostedBy = NormalizeText(header.PostedBy);
            header.CancelledBy = NormalizeText(header.CancelledBy);
            header.CancellationReason = NormalizeText(header.CancellationReason);
        }

        private static void NormalizeLines(List<SupplierReturnLine> lines)
        {
            foreach (var line in lines)
                NormalizeLine(line);
        }

        private static void NormalizeLine(SupplierReturnLine line)
        {
            line.BatchNo = NormalizeText(line.BatchNo);
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

            if (value.Length <= maxLength)
                return value;

            return value.Substring(0, maxLength);
        }

        private static bool IsGeneralBatch(string? batchNo)
        {
            return string.Equals(
                NormalizeText(batchNo),
                GeneralBatchNo,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidStockBucketForItem(ItemBatch batch)
        {
            bool isBatchTracked = batch.ItemVariant.ItemParent.HasBatchTracking;
            bool isGeneral = IsGeneralBatch(batch.BatchNo);

            if (isBatchTracked)
                return !isGeneral;

            return isGeneral;
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
