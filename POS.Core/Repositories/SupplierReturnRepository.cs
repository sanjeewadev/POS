using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
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

        public string BatchNo { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }

        public decimal ReceivedQty { get; set; }
        public decimal AlreadyReturnedQty { get; set; }
        public decimal CurrentBatchStock { get; set; }
        public decimal MaxReturnQty { get; set; }

        public decimal HistoricalCost { get; set; }
        public decimal CreditValue => Math.Round(MaxReturnQty * HistoricalCost, 2);
    }

    public class SupplierReturnRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SupplierReturnRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =========================================================
        // LOOKUPS
        // =========================================================

        public async Task<List<Supplier>> GetActiveSuppliersAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Suppliers
                .AsNoTracking()
                .Where(s => !s.IsDeactivated)
                .OrderBy(s => string.IsNullOrWhiteSpace(s.CompanyName) ? s.SupplierName : s.CompanyName)
                .ThenBy(s => s.SupplierCode)
                .ToListAsync();
        }

        public async Task<List<GrnHeader>> GetSupplierInvoicesAsync(int supplierId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.GrnHeaders
                .AsNoTracking()
                .Where(g =>
                    g.SupplierId == supplierId &&
                    g.Status == "Posted")
                .OrderByDescending(g => g.ReceivedDate)
                .ThenByDescending(g => g.GrnNumber)
                .ToListAsync();
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
                .Where(l => l.GrnHeaderId == grnHeaderId)
                .OrderBy(l => l.ItemVariant!.ItemParent!.ItemCode)
                .ThenBy(l => l.ItemVariant!.VariantDescription)
                .ToListAsync();

            if (!grnLines.Any())
                return new List<SupplierReturnSourceDto>();

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
                .AsNoTracking()
                .Where(b =>
                    variantIds.Contains(b.ItemVariantId) &&
                    batchNos.Contains(b.BatchNo.ToUpper()) &&
                    !b.IsDeactivated)
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

            // SQLite decimal-safe aggregation: group after ToListAsync().
            var returnedByGrnLine = previousReturnRows
                .GroupBy(x => x.GrnLineId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.ReturnQty));

            var result = new List<SupplierReturnSourceDto>();

            foreach (var line in grnLines)
            {
                string lineBatchNo = NormalizeText(line.BatchNo).ToUpperInvariant();

                var batch = batches.FirstOrDefault(b =>
                    b.ItemVariantId == line.ItemVariantId &&
                    string.Equals(b.BatchNo, lineBatchNo, StringComparison.OrdinalIgnoreCase));

                if (batch == null)
                    continue;

                decimal alreadyReturned = returnedByGrnLine.TryGetValue(line.Id, out decimal returned)
                    ? returned
                    : 0m;

                decimal remainingFromOriginalReceipt = line.ReceivedQty - alreadyReturned;

                if (remainingFromOriginalReceipt < 0)
                    remainingFromOriginalReceipt = 0m;

                decimal maxReturnQty = Math.Min(remainingFromOriginalReceipt, batch.CurrentStock);

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

                    BatchNo = batch.BatchNo,
                    ExpiryDate = batch.ExpiryDate,

                    ReceivedQty = line.ReceivedQty,
                    AlreadyReturnedQty = alreadyReturned,
                    CurrentBatchStock = batch.CurrentStock,
                    MaxReturnQty = maxReturnQty,

                    HistoricalCost = line.LandedCost > 0 ? line.LandedCost : line.UnitCost
                });
            }

            return result
                .OrderBy(r => r.ItemCode)
                .ThenBy(r => r.VariantDescription)
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
        // SAVE / POST
        // =========================================================

        public async Task SaveSupplierReturnAsync(
            SupplierReturnHeader header,
            List<SupplierReturnLine> lines,
            bool isDraft)
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
                await ValidateLinesAsync(context, header, lines, isDraft);

                DateTime now = DateTime.Now;

                SupplierReturnHeader dbHeader;

                if (header.Id == 0)
                {
                    header.ReturnNumber = await GenerateDocumentNumberAsync(context, "RTN");
                    header.Status = isDraft ? "Draft" : "Posted";
                    header.CreatedAt = now;
                    header.UpdatedAt = now;

                    if (!isDraft)
                    {
                        header.PostedAt = now;

                        if (string.IsNullOrWhiteSpace(header.PostedBy))
                            header.PostedBy = header.AuthorizedBy;
                    }

                    header.Supplier = null!;
                    header.GrnHeader = null;
                    header.ReturnLines = new List<SupplierReturnLine>();

                    await context.SupplierReturnHeaders.AddAsync(header);
                    await context.SaveChangesAsync();

                    dbHeader = header;
                }
                else
                {
                    dbHeader = await context.SupplierReturnHeaders
                        .FirstOrDefaultAsync(h => h.Id == header.Id)
                        ?? throw new InvalidOperationException("Supplier return document was not found.");

                    if (dbHeader.Status == "Posted")
                        throw new InvalidOperationException("Posted supplier return cannot be modified.");

                    if (dbHeader.Status == "Cancelled")
                        throw new InvalidOperationException("Cancelled supplier return cannot be modified.");

                    dbHeader.SupplierId = header.SupplierId;
                    dbHeader.GrnHeaderId = header.GrnHeaderId;
                    dbHeader.OriginalInvoiceNo = header.OriginalInvoiceNo;
                    dbHeader.ReturnDate = header.ReturnDate;
                    dbHeader.AuthorizedBy = header.AuthorizedBy;
                    dbHeader.Remarks = header.Remarks;
                    dbHeader.GrossCredit = header.GrossCredit;
                    dbHeader.RestockingFee = header.RestockingFee;
                    dbHeader.NetCredit = header.NetCredit;
                    dbHeader.Status = isDraft ? "Draft" : "Posted";
                    dbHeader.UpdatedAt = now;

                    if (!isDraft)
                    {
                        dbHeader.PostedAt = now;

                        if (string.IsNullOrWhiteSpace(dbHeader.PostedBy))
                            dbHeader.PostedBy = dbHeader.AuthorizedBy;
                    }

                    var existingLines = await context.SupplierReturnLines
                        .Where(l => l.ReturnHeaderId == dbHeader.Id)
                        .ToListAsync();

                    context.SupplierReturnLines.RemoveRange(existingLines);

                    await context.SaveChangesAsync();
                }

                foreach (var line in lines)
                {
                    PrepareNewLine(line, dbHeader.Id, isDraft, now);
                    await context.SupplierReturnLines.AddAsync(line);
                }

                await context.SaveChangesAsync();

                if (!isDraft)
                {
                    await PostInventoryAndLedgerAsync(context, dbHeader, lines, now);
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
                .Where(b => batchIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id);

            foreach (var line in lines)
            {
                if (!batches.TryGetValue(line.ItemBatchId, out var batch))
                    throw new InvalidOperationException($"Batch ID {line.ItemBatchId} was not found.");

                if (batch.CurrentStock < line.ReturnQty)
                {
                    throw new InvalidOperationException(
                        $"Insufficient stock in batch '{batch.BatchNo}'. Current stock is {batch.CurrentStock:N3}, return quantity is {line.ReturnQty:N3}.");
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
                    Remarks = $"Supplier Return | Reason: {line.ReasonCode} | Batch: {batch.BatchNo}"
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
                Remarks =
                    $"Supplier Return | Original Invoice: {header.OriginalInvoiceNo} | Gross: {header.GrossCredit:N2} | Restocking Fee: {header.RestockingFee:N2}"
            };

            supplier.CurrentBalance = newBalance;
            supplier.UpdatedAt = now;

            await context.SupplierLedgers.AddAsync(ledger);
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

            if (header.GrnHeaderId.HasValue && header.GrnHeaderId.Value > 0)
            {
                var grn = await context.GrnHeaders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(g => g.Id == header.GrnHeaderId.Value);

                if (grn == null)
                    throw new InvalidOperationException("Linked GRN was not found.");

                if (grn.Status != "Posted")
                    throw new InvalidOperationException("Only posted GRNs can be used for supplier returns.");

                if (grn.SupplierId != header.SupplierId)
                    throw new InvalidOperationException("Linked GRN supplier does not match selected supplier.");
            }

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
            List<SupplierReturnLine> lines,
            bool isDraft)
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

            var variants = await context.ItemVariants
                .Include(v => v.ItemParent)
                .Where(v => variantIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id);

            var batches = await context.ItemBatches
                .Where(b => batchIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id);

            var grnLines = grnLineIds.Any()
                ? await context.GrnLines
                    .Where(l => grnLineIds.Contains(l.Id))
                    .ToDictionaryAsync(l => l.Id)
                : new Dictionary<int, GrnLine>();

            var previousReturnRows = await context.SupplierReturnLines
                .Include(l => l.ReturnHeader)
                .AsNoTracking()
                .Where(l =>
                    l.GrnLineId.HasValue &&
                    grnLineIds.Contains(l.GrnLineId.Value) &&
                    l.ReturnHeaderId != header.Id &&
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
                    throw new InvalidOperationException($"Batch is required for item '{variant.SkuCode}'.");

                if (!batches.TryGetValue(line.ItemBatchId, out var batch))
                    throw new InvalidOperationException($"Batch was not found for item '{variant.SkuCode}'.");

                if (batch.ItemVariantId != line.ItemVariantId)
                    throw new InvalidOperationException($"Batch does not match item '{variant.SkuCode}'.");

                if (batch.IsDeactivated)
                    throw new InvalidOperationException($"Batch '{batch.BatchNo}' is deactivated.");

                if (line.ReturnQty <= 0)
                    throw new InvalidOperationException($"Return quantity must be greater than zero for item '{variant.SkuCode}'.");

                if (line.ReturnQty > batch.CurrentStock)
                {
                    throw new InvalidOperationException(
                        $"Cannot return {line.ReturnQty:N3} for item '{variant.SkuCode}'. Batch stock is only {batch.CurrentStock:N3}.");
                }

                if (line.HistoricalCost <= 0)
                    throw new InvalidOperationException($"Historical cost must be greater than zero for item '{variant.SkuCode}'.");

                if (string.IsNullOrWhiteSpace(line.ReasonCode))
                    throw new InvalidOperationException($"Reason code is required for item '{variant.SkuCode}'.");

                if (line.ReasonCode.Length > 50)
                    throw new InvalidOperationException($"Reason code is too long for item '{variant.SkuCode}'.");

                if (line.LineRemarks.Length > 250)
                    throw new InvalidOperationException($"Line remarks are too long for item '{variant.SkuCode}'.");

                if (line.GrnLineId.HasValue && line.GrnLineId.Value > 0)
                {
                    if (!grnLines.TryGetValue(line.GrnLineId.Value, out var grnLine))
                        throw new InvalidOperationException($"Linked GRN line was not found for item '{variant.SkuCode}'.");

                    if (header.GrnHeaderId.HasValue &&
                        grnLine.GrnHeaderId != header.GrnHeaderId.Value)
                    {
                        throw new InvalidOperationException($"GRN line does not belong to the selected GRN for item '{variant.SkuCode}'.");
                    }

                    if (grnLine.ItemVariantId != line.ItemVariantId)
                        throw new InvalidOperationException($"GRN line item does not match return item '{variant.SkuCode}'.");

                    decimal alreadyReturned = returnedByGrnLine.TryGetValue(grnLine.Id, out decimal returned)
                        ? returned
                        : 0m;

                    decimal remainingFromReceipt = grnLine.ReceivedQty - alreadyReturned;

                    if (line.ReturnQty > remainingFromReceipt)
                    {
                        throw new InvalidOperationException(
                            $"Cannot return {line.ReturnQty:N3} for item '{variant.SkuCode}'. Remaining returnable GRN quantity is {remainingFromReceipt:N3}.");
                    }
                }

                line.BatchNo = batch.BatchNo;
                line.ExpiryDate = batch.ExpiryDate;
                line.CreditValue = Math.Round(line.ReturnQty * line.HistoricalCost, 2);
            }
        }

        private static void ValidateSubmittedLineDuplicates(List<SupplierReturnLine> lines)
        {
            var duplicate = lines
                .GroupBy(l => l.ItemBatchId)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
                throw new InvalidOperationException("Duplicate batch found. The same physical batch can appear only once in one supplier return.");
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

        private static void PrepareNewLine(
            SupplierReturnLine line,
            int headerId,
            bool isDraft,
            DateTime now)
        {
            line.Id = 0;
            line.ReturnHeaderId = headerId;

            line.ReturnHeader = null!;
            line.GrnLine = null;
            line.ItemVariant = null;
            line.ItemBatch = null;

            line.LineStatus = isDraft ? "Open" : "Posted";
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
    }
}