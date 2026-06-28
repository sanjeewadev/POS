using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public class GrnRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public GrnRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =========================================================
        // LOOKUPS
        // =========================================================

        public async Task<IEnumerable<Supplier>> GetActiveSuppliersAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Suppliers
                .AsNoTracking()
                .Where(s => !s.IsDeactivated)
                .OrderBy(s => s.SupplierName)
                .ThenBy(s => s.SupplierCode)
                .ToListAsync();
        }

        public async Task<IEnumerable<PoHeader>> GetOpenPurchaseOrdersAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.PoHeaders
                .Include(p => p.Supplier)
                .Include(p => p.PoLines)
                .AsNoTracking()
                .Where(p =>
                    p.Status == "Approved" ||
                    p.Status == "Partially Received")
                .Where(p => p.PoLines.Any(l => l.ReceivedQty < l.OrderQty))
                .OrderByDescending(p => p.OrderDate)
                .ThenBy(p => p.PoNumber)
                .ToListAsync();
        }

        public async Task<PoHeader?> GetApprovedPoDetailsAsync(int poId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var po = await context.PoHeaders
                .Include(p => p.Supplier)
                .Include(p => p.PoLines)
                    .ThenInclude(l => l.ItemVariant)
                        .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.Id == poId &&
                    (
                        p.Status == "Approved" ||
                        p.Status == "Partially Received"
                    ));

            if (po == null)
                return null;

            po.PoLines = po.PoLines
                .Where(l => l.ReceivedQty < l.OrderQty)
                .OrderBy(l => l.ItemVariant.ItemParent.ItemCode)
                .ThenBy(l => l.ItemVariant.VariantDescription)
                .ToList();

            return po;
        }

        // =========================================================
        // POST GRN
        // =========================================================

        public async Task PostGrnAsync(GrnHeader header, List<GrnLine> lines)
        {
            if (header == null)
                throw new ArgumentNullException(nameof(header));

            if (lines == null || !lines.Any())
                throw new InvalidOperationException("GRN must contain at least one line.");

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

                header.GrnNumber = await GenerateDocumentNumberAsync(context, "GRN");
                header.Status = "Posted";
                header.CreatedAt = now;
                header.UpdatedAt = now;
                header.PostedAt = now;

                if (string.IsNullOrWhiteSpace(header.PostedBy))
                    header.PostedBy = header.CreatedBy;

                header.Supplier = null!;
                header.PurchaseOrder = null;
                header.GrnLines = new List<GrnLine>();

                await context.GrnHeaders.AddAsync(header);
                await context.SaveChangesAsync();

                PoHeader? linkedPo = null;

                if (header.PurchaseOrderId.HasValue && header.PurchaseOrderId.Value > 0)
                {
                    linkedPo = await context.PoHeaders
                        .Include(p => p.PoLines)
                        .FirstOrDefaultAsync(p => p.Id == header.PurchaseOrderId.Value);

                    if (linkedPo == null)
                        throw new InvalidOperationException("Linked Purchase Order was not found.");
                }

                foreach (var line in lines)
                {
                    PrepareNewLine(line, header.Id, header.GrnNumber, now);
                    await context.GrnLines.AddAsync(line);
                }

                await context.SaveChangesAsync();

                var variantIds = lines
                    .Select(l => l.ItemVariantId)
                    .Distinct()
                    .ToList();

                var variants = await context.ItemVariants
                    .Include(v => v.ItemParent)
                    .Where(v => variantIds.Contains(v.Id))
                    .ToDictionaryAsync(v => v.Id);

                var stockCache = await LoadCurrentStockCacheAsync(context, variantIds);

                foreach (var line in lines)
                {
                    if (!variants.TryGetValue(line.ItemVariantId, out var variant))
                        throw new InvalidOperationException("One or more received item variants were not found.");

                    // 1. Update linked PO line receiving.
                    if (linkedPo != null)
                    {
                        var poLine = ResolvePoLineForGrnLine(linkedPo, line);

                        if (poLine == null)
                        {
                            throw new InvalidOperationException(
                                $"GRN line item '{variant.SkuCode}' does not match the linked Purchase Order.");
                        }

                        decimal outstanding = poLine.OrderQty - poLine.ReceivedQty;

                        if (line.ReceivedQty > outstanding)
                        {
                            throw new InvalidOperationException(
                                $"Cannot receive {line.ReceivedQty:N3} for item '{variant.SkuCode}'. Outstanding PO quantity is only {outstanding:N3}.");
                        }

                        poLine.ReceivedQty += line.ReceivedQty;
                        poLine.LineStatus = CalculatePoLineStatus(poLine.OrderQty, poLine.ReceivedQty);
                        poLine.UpdatedAt = now;

                        if (poLine.LineStatus == "Closed")
                            poLine.ClosedAt ??= now;
                        else
                            poLine.ClosedAt = null;
                    }

                    // 2. Update supplier-specific last purchase cost.
                    var itemSupplier = await context.ItemSuppliers
                        .FirstOrDefaultAsync(s =>
                            s.SupplierId == header.SupplierId &&
                            s.ItemVariantId == line.ItemVariantId);

                    if (itemSupplier != null)
                    {
                        itemSupplier.LastCostPrice = line.UnitCost;
                        itemSupplier.UpdatedAt = now;
                    }

                    // 3. Moving average cost.
                    decimal currentStock = stockCache.TryGetValue(variant.Id, out var qty)
                        ? qty
                        : 0m;

                    decimal newTotalQty = currentStock + line.ReceivedQty;

                    if (newTotalQty > 0)
                    {
                        decimal oldTotalValue = currentStock * variant.AverageCost;
                        decimal newReceivedValue = line.ReceivedQty * line.LandedCost;

                        variant.AverageCost = Math.Round(
                            (oldTotalValue + newReceivedValue) / newTotalQty,
                            2);
                    }

                    variant.CostPrice = line.LandedCost;
                    variant.UpdatedAt = now;

                    stockCache[variant.Id] = newTotalQty;

                    // 4. Batch bucket.
                    string targetBatchNo = BuildBatchNo(line.BatchNo, header.GrnNumber);

                    var batch = await context.ItemBatches
                        .FirstOrDefaultAsync(b =>
                            b.ItemVariantId == variant.Id &&
                            b.BatchNo == targetBatchNo);

                    if (batch == null)
                    {
                        batch = new ItemBatch
                        {
                            ItemVariantId = variant.Id,
                            BatchNo = targetBatchNo,
                            ExpiryDate = line.ExpiryDate,
                            ReceivedDate = header.ReceivedDate,
                            CostPrice = line.LandedCost,
                            RetailPrice = variant.RetailPrice,
                            WholesalePrice = variant.WholesalePrice,
                            CurrentStock = line.ReceivedQty,
                            IsDeactivated = false,
                            CreatedAt = now,
                            UpdatedAt = now
                        };

                        await context.ItemBatches.AddAsync(batch);
                    }
                    else
                    {
                        if (line.ExpiryDate.HasValue && batch.ExpiryDate != line.ExpiryDate)
                        {
                            throw new InvalidOperationException(
                                $"Batch '{targetBatchNo}' already exists with a different expiry date.");
                        }

                        batch.CurrentStock += line.ReceivedQty;
                        batch.CostPrice = line.LandedCost;
                        batch.UpdatedAt = now;
                    }

                    // 5. Immutable stock transaction.
                    var inventoryTx = new InventoryTransaction
                    {
                        ItemVariantId = variant.Id,
                        ItemBatch = batch,
                        TransactionDate = header.ReceivedDate,
                        TransactionType = "GRN",
                        ReferenceDocument = header.GrnNumber,
                        ReferenceLineId = line.Id,
                        Quantity = line.ReceivedQty,
                        UnitCost = line.LandedCost,
                        CreatedBy = header.CreatedBy,
                        CreatedAt = now,
                        Remarks =
                            $"GRN Receipt | Supplier Invoice: {header.SupplierInvoiceNo} | Batch: {targetBatchNo}"
                    };

                    await context.InventoryTransactions.AddAsync(inventoryTx);
                }

                // 6. Update linked PO header status.
                if (linkedPo != null)
                {
                    linkedPo.Status = CalculatePoHeaderStatus(linkedPo.PoLines.ToList());
                    linkedPo.UpdatedAt = now;

                    if (linkedPo.Status == "Closed")
                        linkedPo.ClosedAt ??= now;
                    else
                        linkedPo.ClosedAt = null;
                }

                // 7. Supplier ledger + supplier current balance.
                var supplier = await context.Suppliers
                    .FirstOrDefaultAsync(s => s.Id == header.SupplierId);

                if (supplier == null)
                    throw new InvalidOperationException("Supplier was not found.");

                decimal newSupplierBalance = supplier.CurrentBalance + header.NetPayable;

                var supplierLedger = new SupplierLedger
                {
                    SupplierId = supplier.Id,
                    GrnHeaderId = header.Id,
                    TransactionDate = header.ReceivedDate,
                    TransactionType = "GRN",
                    ReferenceDocument = header.GrnNumber,
                    ChargeAmount = header.NetPayable,
                    PaymentAmount = 0m,
                    BalanceAfterTransaction = newSupplierBalance,
                    DueDate = header.DueDate,
                    IsPaid = false,
                    CreatedBy = header.CreatedBy,
                    CreatedAt = now,
                    Remarks = $"Supplier Invoice: {header.SupplierInvoiceNo}"
                };

                supplier.CurrentBalance = newSupplierBalance;
                supplier.UpdatedAt = now;

                await context.SupplierLedgers.AddAsync(supplierLedger);

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
        // VALIDATION
        // =========================================================

        private static async Task ValidateHeaderAsync(AppDbContext context, GrnHeader header)
        {
            if (header.SupplierId <= 0)
                throw new InvalidOperationException("Supplier is required.");

            bool supplierExists = await context.Suppliers.AnyAsync(s =>
                s.Id == header.SupplierId &&
                !s.IsDeactivated);

            if (!supplierExists)
                throw new InvalidOperationException("Selected supplier is inactive or missing.");

            if (string.IsNullOrWhiteSpace(header.SupplierInvoiceNo))
                throw new InvalidOperationException("Supplier invoice number is required.");

            if (header.SupplierInvoiceNo.Length > 50)
                throw new InvalidOperationException("Supplier invoice number cannot be longer than 50 characters.");

            bool duplicateInvoice = await context.GrnHeaders.AnyAsync(g =>
                g.SupplierId == header.SupplierId &&
                g.SupplierInvoiceNo.ToUpper() == header.SupplierInvoiceNo.ToUpper() &&
                g.Status != "Cancelled");

            if (duplicateInvoice)
            {
                throw new InvalidOperationException(
                    "This supplier invoice number has already been posted for the selected supplier.");
            }

            if (header.InvoiceDate.Date > DateTime.Now.Date.AddDays(1))
                throw new InvalidOperationException("Invoice date cannot be in the far future.");

            if (header.ReceivedDate.Date > DateTime.Now.Date.AddDays(1))
                throw new InvalidOperationException("Received date cannot be in the far future.");

            if (header.DueDate.Date < header.InvoiceDate.Date)
                throw new InvalidOperationException("Due date cannot be before invoice date.");

            if (header.CreditDays < 0 || header.CreditDays > 365)
                throw new InvalidOperationException("Credit days must be between 0 and 365.");

            if (header.GlobalBillDiscount < 0)
                throw new InvalidOperationException("Global bill discount cannot be negative.");

            if (header.FreightAmount < 0)
                throw new InvalidOperationException("Freight amount cannot be negative.");

            if (header.Remarks.Length > 500)
                throw new InvalidOperationException("Remarks cannot be longer than 500 characters.");

            if (header.PurchaseOrderId.HasValue && header.PurchaseOrderId.Value > 0)
            {
                var po = await context.PoHeaders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == header.PurchaseOrderId.Value);

                if (po == null)
                    throw new InvalidOperationException("Linked Purchase Order was not found.");

                if (po.SupplierId != header.SupplierId)
                    throw new InvalidOperationException("Linked Purchase Order supplier does not match selected supplier.");

                if (po.Status != "Approved" && po.Status != "Partially Received")
                    throw new InvalidOperationException("Only approved or partially received Purchase Orders can be received.");
            }
        }

        private static async Task ValidateLinesAsync(
            AppDbContext context,
            GrnHeader header,
            List<GrnLine> lines)
        {
            var variantIds = lines
                .Select(l => l.ItemVariantId)
                .Distinct()
                .ToList();

            var variants = await context.ItemVariants
                .Include(v => v.ItemParent)
                .Where(v => variantIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id);

            var supplierLinks = await context.ItemSuppliers
                .Where(s =>
                    variantIds.Contains(s.ItemVariantId) &&
                    s.SupplierId == header.SupplierId)
                .ToDictionaryAsync(s => s.ItemVariantId);

            Dictionary<int, PoLine> poLines = new();

            if (header.PurchaseOrderId.HasValue && header.PurchaseOrderId.Value > 0)
            {
                poLines = await context.PoLines
                    .Where(l => l.PoHeaderId == header.PurchaseOrderId.Value)
                    .ToDictionaryAsync(l => l.Id);
            }

            foreach (var line in lines)
            {
                NormalizeLine(line);

                if (line.ItemVariantId <= 0)
                    throw new InvalidOperationException("Invalid item variant in GRN line.");

                if (!variants.TryGetValue(line.ItemVariantId, out var variant))
                    throw new InvalidOperationException("One or more item variants do not exist.");

                if (variant.IsDeactivated || variant.ItemParent.IsDeactivated)
                    throw new InvalidOperationException($"Item '{variant.SkuCode}' is deactivated.");

                if (variant.ItemParent.IsPurchaseLocked)
                    throw new InvalidOperationException($"Item '{variant.SkuCode}' is purchase locked.");

                if (!supplierLinks.ContainsKey(line.ItemVariantId))
                {
                    throw new InvalidOperationException(
                        $"Item '{variant.SkuCode}' is not approved for the selected supplier.");
                }

                if (line.ReceivedQty <= 0)
                    throw new InvalidOperationException($"Received quantity must be greater than zero for item '{variant.SkuCode}'.");

                if (line.UnitCost <= 0)
                    throw new InvalidOperationException($"Unit cost must be greater than zero for item '{variant.SkuCode}'.");

                if (line.LineDiscount < 0)
                    throw new InvalidOperationException($"Line discount cannot be negative for item '{variant.SkuCode}'.");

                decimal gross = line.ReceivedQty * line.UnitCost;

                if (line.LineDiscount > gross)
                    throw new InvalidOperationException($"Line discount cannot be greater than line value for item '{variant.SkuCode}'.");

                if (line.BatchNo.Length > 50)
                    throw new InvalidOperationException($"Batch number is too long for item '{variant.SkuCode}'.");

                if (line.Uom.Length > 20)
                    throw new InvalidOperationException($"UOM is too long for item '{variant.SkuCode}'.");

                if (variant.ItemParent.HasBatchExpiry && !line.ExpiryDate.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Expiry date is required for item '{variant.SkuCode}'.");
                }

                if (line.ExpiryDate.HasValue &&
                    line.ExpiryDate.Value.Date < header.ReceivedDate.Date)
                {
                    throw new InvalidOperationException(
                        $"Expiry date cannot be before received date for item '{variant.SkuCode}'.");
                }

                if (header.PurchaseOrderId.HasValue && header.PurchaseOrderId.Value > 0)
                {
                    PoLine? poLine = null;

                    if (line.PoLineId.HasValue && poLines.TryGetValue(line.PoLineId.Value, out var exactPoLine))
                    {
                        poLine = exactPoLine;
                    }
                    else
                    {
                        poLine = poLines.Values.FirstOrDefault(l => l.ItemVariantId == line.ItemVariantId);
                    }

                    if (poLine == null)
                    {
                        throw new InvalidOperationException(
                            $"Item '{variant.SkuCode}' does not exist in the linked Purchase Order.");
                    }

                    if (poLine.ItemVariantId != line.ItemVariantId)
                    {
                        throw new InvalidOperationException(
                            $"GRN item '{variant.SkuCode}' does not match its linked PO line.");
                    }

                    decimal outstanding = poLine.OrderQty - poLine.ReceivedQty;

                    if (line.ReceivedQty > outstanding)
                    {
                        throw new InvalidOperationException(
                            $"Cannot receive {line.ReceivedQty:N3} for item '{variant.SkuCode}'. Outstanding PO quantity is {outstanding:N3}.");
                    }
                }
            }
        }

        private static void ValidateSubmittedLineDuplicates(List<GrnLine> lines)
        {
            var duplicate = lines
                .GroupBy(l => new
                {
                    l.ItemVariantId,
                    BatchNo = string.IsNullOrWhiteSpace(l.BatchNo)
                        ? "<AUTO>"
                        : l.BatchNo.Trim().ToUpperInvariant(),
                    ExpiryDate = l.ExpiryDate?.Date,
                    PoLineId = l.PoLineId ?? 0
                })
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
            {
                throw new InvalidOperationException(
                    "Duplicate GRN line found. Merge the same item, batch, expiry, and PO line into one row.");
            }
        }

        // =========================================================
        // CALCULATION
        // =========================================================

        private static void RecalculateHeaderTotals(GrnHeader header, List<GrnLine> lines)
        {
            decimal subtotal = 0m;
            decimal lineDiscountTotal = 0m;

            foreach (var line in lines)
            {
                decimal gross = line.ReceivedQty * line.UnitCost;
                decimal lineTotal = gross - line.LineDiscount;

                if (lineTotal < 0)
                    lineTotal = 0;

                line.LineTotal = Math.Round(lineTotal, 2);

                subtotal += line.LineTotal;
                lineDiscountTotal += line.LineDiscount;
            }

            if (header.GlobalBillDiscount > subtotal)
                throw new InvalidOperationException("Global bill discount cannot be greater than GRN value.");

            header.Subtotal = Math.Round(subtotal, 2);
            header.TotalDiscountAmount = Math.Round(lineDiscountTotal + header.GlobalBillDiscount, 2);
            header.NetPayable = Math.Round(subtotal - header.GlobalBillDiscount + header.FreightAmount, 2);

            AllocateLandedCost(header, lines);
        }

        private static void AllocateLandedCost(GrnHeader header, List<GrnLine> lines)
        {
            decimal totalBaseValue = lines.Sum(l => l.LineTotal);

            if (totalBaseValue <= 0)
            {
                foreach (var line in lines)
                    line.LandedCost = 0m;

                return;
            }

            foreach (var line in lines)
            {
                decimal weight = line.LineTotal / totalBaseValue;
                decimal allocatedFreight = header.FreightAmount * weight;
                decimal allocatedGlobalDiscount = header.GlobalBillDiscount * weight;
                decimal landedLineTotal = line.LineTotal + allocatedFreight - allocatedGlobalDiscount;

                line.LandedCost = line.ReceivedQty > 0
                    ? Math.Round(landedLineTotal / line.ReceivedQty, 2)
                    : 0m;
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

            string number = $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString().PadLeft(sequence.PaddingLength, '0')}";

            sequence.NextSequenceNumber++;
            sequence.UpdatedAt = DateTime.Now;

            await context.SaveChangesAsync();

            return number;
        }

        private static async Task<Dictionary<int, decimal>> LoadCurrentStockCacheAsync(
            AppDbContext context,
            List<int> variantIds)
        {
            if (variantIds == null || !variantIds.Any())
                return new Dictionary<int, decimal>();

            var stockRows = await context.InventoryTransactions
                .AsNoTracking()
                .Where(t => variantIds.Contains(t.ItemVariantId))
                .Select(t => new
                {
                    t.ItemVariantId,
                    t.Quantity
                })
                .ToListAsync();

            return stockRows
                .GroupBy(t => t.ItemVariantId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.Quantity));
        }

        private static void PrepareNewLine(
            GrnLine line,
            int headerId,
            string grnNumber,
            DateTime now)
        {
            line.Id = 0;
            line.GrnHeaderId = headerId;
            line.GrnHeader = null!;
            line.ItemVariant = null!;
            line.PoLine = null;

            line.BatchNo = BuildBatchNo(line.BatchNo, grnNumber);
            line.LineStatus = "Posted";
            line.CreatedAt = now;
            line.UpdatedAt = now;
        }

        private static PoLine? ResolvePoLineForGrnLine(PoHeader linkedPo, GrnLine line)
        {
            if (line.PoLineId.HasValue && line.PoLineId.Value > 0)
            {
                var exact = linkedPo.PoLines.FirstOrDefault(l => l.Id == line.PoLineId.Value);

                if (exact != null)
                    return exact;
            }

            return linkedPo.PoLines.FirstOrDefault(l => l.ItemVariantId == line.ItemVariantId);
        }

        private static string CalculatePoLineStatus(decimal orderQty, decimal receivedQty)
        {
            if (receivedQty <= 0)
                return "Open";

            if (receivedQty < orderQty)
                return "Partially Received";

            return "Closed";
        }

        private static string CalculatePoHeaderStatus(List<PoLine> lines)
        {
            if (lines.All(l => l.OrderQty > 0 && l.ReceivedQty >= l.OrderQty))
                return "Closed";

            if (lines.Any(l => l.ReceivedQty > 0))
                return "Partially Received";

            return "Approved";
        }

        private static string BuildBatchNo(string? batchNo, string grnNumber)
        {
            string value = NormalizeText(batchNo);

            if (!string.IsNullOrWhiteSpace(value))
                return value.ToUpperInvariant();

            return $"SYS-{grnNumber}";
        }

        private static void NormalizeHeader(GrnHeader header)
        {
            header.SupplierInvoiceNo = NormalizeText(header.SupplierInvoiceNo);
            header.Remarks = NormalizeText(header.Remarks);
            header.CreatedBy = NormalizeText(header.CreatedBy);
            header.PostedBy = NormalizeText(header.PostedBy);

            if (header.CreditDays == 0 && header.DueDate.Date >= header.InvoiceDate.Date)
            {
                header.CreditDays = Math.Max(
                    0,
                    (header.DueDate.Date - header.InvoiceDate.Date).Days);
            }
        }

        private static void NormalizeLines(List<GrnLine> lines)
        {
            foreach (var line in lines)
                NormalizeLine(line);
        }

        private static void NormalizeLine(GrnLine line)
        {
            line.BatchNo = NormalizeText(line.BatchNo);
            line.Uom = NormalizeText(line.Uom);
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}