using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public class PoSummaryDto
    {
        public int PoHeaderId { get; set; }
        public string PoNumber { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public DateTime ExpectedDate { get; set; }
        public decimal NetPayable { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
    }

    public class PoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public PoRepository(IDbContextFactory<AppDbContext> contextFactory)
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
                    .ThenInclude(l => l.ItemVariant)
                        .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .Where(p =>
                    p.Status == "Approved" ||
                    p.Status == "Partially Received")
                .Where(p => p.PoLines.Any(l => l.ReceivedQty < l.OrderQty))
                .OrderByDescending(p => p.OrderDate)
                .ThenBy(p => p.PoNumber)
                .ToListAsync();
        }

        // =========================================================
        // SAVE / UPDATE PURCHASE ORDER
        // =========================================================

        public async Task SavePurchaseOrderAsync(
            PoHeader header,
            List<PoLine> lines,
            bool isDraft)
        {
            if (header == null)
                throw new ArgumentNullException(nameof(header));

            if (lines == null || !lines.Any())
                throw new InvalidOperationException("Purchase Order must contain at least one line.");

            NormalizeHeader(header);
            NormalizeLines(lines);
            ValidateSubmittedLineDuplicates(lines);
            RecalculateTotals(header, lines);

            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                await ValidateHeaderAsync(context, header);
                await ValidateLinesAsync(context, header.SupplierId, lines);

                DateTime now = DateTime.Now;

                if (header.Id == 0)
                {
                    header.PoNumber = await GenerateDocumentNumberAsync(context, "PO");
                    header.Status = isDraft ? "Draft" : "Approved";
                    header.CreatedAt = now;
                    header.UpdatedAt = now;

                    if (!isDraft)
                    {
                        header.ApprovedAt = now;
                        header.ApprovedBy = string.IsNullOrWhiteSpace(header.ApprovedBy)
                            ? header.CreatedBy
                            : header.ApprovedBy;
                    }

                    header.Supplier = null!;
                    header.PoLines = new List<PoLine>();

                    await context.PoHeaders.AddAsync(header);
                    await context.SaveChangesAsync();

                    foreach (var line in lines)
                    {
                        PrepareNewLine(line, header.Id, now);
                        await context.PoLines.AddAsync(line);
                    }
                }
                else
                {
                    var existingHeader = await context.PoHeaders
                        .Include(p => p.PoLines)
                        .FirstOrDefaultAsync(p => p.Id == header.Id);

                    if (existingHeader == null)
                        throw new InvalidOperationException("Purchase Order was not found.");

                    if (existingHeader.Status == "Cancelled")
                        throw new InvalidOperationException("Cancelled Purchase Orders cannot be edited.");

                    if (existingHeader.Status == "Closed")
                        throw new InvalidOperationException("Closed Purchase Orders cannot be edited.");

                    existingHeader.SupplierId = header.SupplierId;
                    existingHeader.OrderDate = header.OrderDate;
                    existingHeader.ExpectedDate = header.ExpectedDate;
                    existingHeader.Terms = header.Terms;
                    existingHeader.CreditDays = header.CreditDays;
                    existingHeader.Remarks = header.Remarks;

                    existingHeader.Subtotal = header.Subtotal;
                    existingHeader.GlobalBillDiscount = header.GlobalBillDiscount;
                    existingHeader.TotalTaxAmount = header.TotalTaxAmount;
                    existingHeader.TotalDiscountAmount = header.TotalDiscountAmount;
                    existingHeader.NetPayable = header.NetPayable;
                    existingHeader.IsTaxInclusive = header.IsTaxInclusive;

                    existingHeader.Status = CalculateHeaderStatus(lines, isDraft);
                    existingHeader.UpdatedAt = now;

                    if (existingHeader.Status == "Approved" && existingHeader.ApprovedAt == null)
                    {
                        existingHeader.ApprovedAt = now;
                        existingHeader.ApprovedBy = string.IsNullOrWhiteSpace(header.ApprovedBy)
                            ? header.CreatedBy
                            : header.ApprovedBy;
                    }

                    if (existingHeader.Status == "Closed")
                    {
                        existingHeader.ClosedAt ??= now;
                    }
                    else
                    {
                        existingHeader.ClosedAt = null;
                    }

                    var existingLines = existingHeader.PoLines.ToList();

                    var incomingLineIds = lines
                        .Where(l => l.Id > 0)
                        .Select(l => l.Id)
                        .ToHashSet();

                    var linesToRemove = existingLines
                        .Where(l => !incomingLineIds.Contains(l.Id))
                        .ToList();

                    foreach (var removedLine in linesToRemove)
                    {
                        if (removedLine.ReceivedQty > 0)
                        {
                            throw new InvalidOperationException(
                                $"Cannot remove PO line for variant ID {removedLine.ItemVariantId} because it has already been received.");
                        }

                        context.PoLines.Remove(removedLine);
                    }

                    foreach (var incomingLine in lines)
                    {
                        if (incomingLine.Id == 0)
                        {
                            PrepareNewLine(incomingLine, existingHeader.Id, now);
                            await context.PoLines.AddAsync(incomingLine);
                            continue;
                        }

                        var existingLine = existingLines.FirstOrDefault(l => l.Id == incomingLine.Id);

                        if (existingLine == null)
                            throw new InvalidOperationException("One or more PO lines were not found.");

                        if (incomingLine.OrderQty < existingLine.ReceivedQty)
                        {
                            throw new InvalidOperationException(
                                $"Order quantity cannot be less than already received quantity for variant ID {existingLine.ItemVariantId}.");
                        }

                        existingLine.ItemVariantId = incomingLine.ItemVariantId;
                        existingLine.Uom = incomingLine.Uom;
                        existingLine.SupplierItemCode = incomingLine.SupplierItemCode;
                        existingLine.OrderQty = incomingLine.OrderQty;
                        existingLine.ExpectedCost = incomingLine.ExpectedCost;
                        existingLine.LineDiscount = incomingLine.LineDiscount;
                        existingLine.TaxCode = incomingLine.TaxCode;
                        existingLine.TaxAmount = incomingLine.TaxAmount;
                        existingLine.LineTotal = incomingLine.LineTotal;
                        existingLine.LineStatus = CalculateLineStatus(existingLine.OrderQty, existingLine.ReceivedQty);
                        existingLine.UpdatedAt = now;

                        if (existingLine.LineStatus == "Closed")
                            existingLine.ClosedAt ??= now;
                        else
                            existingLine.ClosedAt = null;
                    }
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

        private static void PrepareNewLine(PoLine line, int headerId, DateTime now)
        {
            line.Id = 0;
            line.PoHeaderId = headerId;
            line.PoHeader = null!;
            line.ItemVariant = null!;

            line.CreatedAt = now;
            line.UpdatedAt = now;
            line.LineStatus = CalculateLineStatus(line.OrderQty, line.ReceivedQty);
            line.ClosedAt = line.LineStatus == "Closed" ? now : null;
        }

        // =========================================================
        // DASHBOARD / DETAIL / CANCEL
        // =========================================================

        public async Task<IEnumerable<PoSummaryDto>> GetPoSummariesAsync(
            string searchTerm = "",
            int? supplierId = null,
            string statusFilter = "All",
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.PoHeaders
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();

                query = query.Where(p =>
                    EF.Functions.Like(p.PoNumber, $"%{term}%") ||
                    EF.Functions.Like(p.Supplier.SupplierName, $"%{term}%"));
            }

            if (supplierId.HasValue && supplierId.Value > 0)
            {
                query = query.Where(p => p.SupplierId == supplierId.Value);
            }

            if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "All")
            {
                query = query.Where(p => p.Status == statusFilter);
            }

            if (startDate.HasValue)
            {
                query = query.Where(p => p.OrderDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                DateTime endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(p => p.OrderDate <= endOfDay);
            }

            return await query
                .OrderByDescending(p => p.OrderDate)
                .ThenByDescending(p => p.Id)
                .Select(p => new PoSummaryDto
                {
                    PoHeaderId = p.Id,
                    PoNumber = p.PoNumber,
                    SupplierName = p.Supplier.SupplierName,
                    OrderDate = p.OrderDate,
                    ExpectedDate = p.ExpectedDate,
                    NetPayable = p.NetPayable,
                    Status = p.Status,
                    CreatedBy = p.CreatedBy
                })
                .Take(500)
                .ToListAsync();
        }

        public async Task<PoHeader?> GetPurchaseOrderDetailsAsync(int poId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var po = await context.PoHeaders
                .Include(p => p.Supplier)
                .Include(p => p.PoLines)
                    .ThenInclude(l => l.ItemVariant)
                        .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == poId);

            if (po == null)
                return null;

            foreach (var line in po.PoLines)
            {
                line.ItemCode = line.ItemVariant.ItemParent.ItemCode;
                line.Description = line.ItemVariant.ItemParent.ItemName;
                line.VariantDescription = line.ItemVariant.VariantDescription;
                line.Barcode = line.ItemVariant.Barcode;
                line.SOH = 0m;
            }

            return po;
        }

        public async Task CancelPurchaseOrderAsync(
            int poId,
            string cancelledBy = "",
            string reason = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var po = await context.PoHeaders
                .Include(p => p.PoLines)
                .FirstOrDefaultAsync(p => p.Id == poId);

            if (po == null)
                return;

            if (po.Status == "Closed")
                throw new InvalidOperationException("Cannot cancel a closed Purchase Order.");

            if (po.PoLines.Any(l => l.ReceivedQty > 0))
                throw new InvalidOperationException("Cannot cancel a Purchase Order that has received quantities.");

            DateTime now = DateTime.Now;

            po.Status = "Cancelled";
            po.CancelledAt = now;
            po.CancelledBy = NormalizeText(cancelledBy);
            po.CancellationReason = NormalizeText(reason);
            po.UpdatedAt = now;

            foreach (var line in po.PoLines)
            {
                line.LineStatus = "Cancelled";
                line.UpdatedAt = now;
            }

            await context.SaveChangesAsync();
        }

        // =========================================================
        // VALIDATION
        // =========================================================

        private static async Task ValidateHeaderAsync(AppDbContext context, PoHeader header)
        {
            if (header.SupplierId <= 0)
                throw new InvalidOperationException("Supplier is required.");

            bool supplierExists = await context.Suppliers.AnyAsync(s =>
                s.Id == header.SupplierId &&
                !s.IsDeactivated);

            if (!supplierExists)
                throw new InvalidOperationException("Selected supplier is inactive or missing.");

            if (header.OrderDate.Date > DateTime.Now.Date.AddDays(1))
                throw new InvalidOperationException("Order date cannot be in the far future.");

            if (header.ExpectedDate.Date < header.OrderDate.Date)
                throw new InvalidOperationException("Expected date cannot be before order date.");

            if (header.CreditDays < 0 || header.CreditDays > 365)
                throw new InvalidOperationException("Credit days must be between 0 and 365.");

            if (header.Remarks.Length > 500)
                throw new InvalidOperationException("Remarks cannot be longer than 500 characters.");

            if (header.GlobalBillDiscount < 0)
                throw new InvalidOperationException("Global bill discount cannot be negative.");
        }

        private static async Task ValidateLinesAsync(
            AppDbContext context,
            int supplierId,
            List<PoLine> lines)
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
                    s.SupplierId == supplierId)
                .ToDictionaryAsync(s => s.ItemVariantId);

            foreach (var line in lines)
            {
                if (line.ItemVariantId <= 0)
                    throw new InvalidOperationException("Invalid item variant in PO line.");

                if (!variants.TryGetValue(line.ItemVariantId, out var variant))
                    throw new InvalidOperationException("One or more item variants do not exist.");

                if (variant.IsDeactivated || variant.ItemParent.IsDeactivated)
                    throw new InvalidOperationException($"Item '{variant.SkuCode}' is deactivated.");

                if (variant.ItemParent.IsPurchaseLocked)
                    throw new InvalidOperationException($"Item '{variant.SkuCode}' is purchase locked.");

                if (!supplierLinks.TryGetValue(line.ItemVariantId, out var supplierLink))
                    throw new InvalidOperationException($"Item '{variant.SkuCode}' is not approved for the selected supplier.");

                if (line.OrderQty <= 0)
                    throw new InvalidOperationException($"Order quantity must be greater than zero for item '{variant.SkuCode}'.");

                if (supplierLink.MinimumOrderQuantity > 0 &&
                    line.OrderQty < supplierLink.MinimumOrderQuantity)
                {
                    throw new InvalidOperationException(
                        $"Item '{variant.SkuCode}' minimum order quantity is {supplierLink.MinimumOrderQuantity}.");
                }

                if (line.ExpectedCost <= 0)
                    throw new InvalidOperationException($"Expected cost must be greater than zero for item '{variant.SkuCode}'.");

                if (line.LineDiscount < 0)
                    throw new InvalidOperationException($"Line discount cannot be negative for item '{variant.SkuCode}'.");

                decimal gross = line.OrderQty * line.ExpectedCost;

                if (line.LineDiscount > gross)
                    throw new InvalidOperationException($"Line discount cannot be greater than line value for item '{variant.SkuCode}'.");

                if (line.Uom.Length > 20)
                    throw new InvalidOperationException($"UOM is too long for item '{variant.SkuCode}'.");

                if (line.SupplierItemCode.Length > 100)
                    throw new InvalidOperationException($"Supplier item code is too long for item '{variant.SkuCode}'.");

                if (line.TaxCode.Length > 20)
                    throw new InvalidOperationException($"Tax code is too long for item '{variant.SkuCode}'.");

                if (string.IsNullOrWhiteSpace(line.SupplierItemCode))
                    line.SupplierItemCode = supplierLink.SupplierItemCode ?? string.Empty;
            }
        }

        private static void ValidateSubmittedLineDuplicates(List<PoLine> lines)
        {
            var duplicate = lines
                .GroupBy(l => l.ItemVariantId)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
            {
                throw new InvalidOperationException(
                    "The same item variant cannot appear twice in one Purchase Order. Merge quantities into one line.");
            }
        }

        // =========================================================
        // CALCULATION / NORMALIZATION
        // =========================================================

        private static void RecalculateTotals(PoHeader header, List<PoLine> lines)
        {
            decimal subtotal = 0m;
            decimal lineDiscountTotal = 0m;
            decimal taxTotal = 0m;
            decimal lineNetTotal = 0m;

            foreach (var line in lines)
            {
                decimal taxRate = GetTaxRate(line.TaxCode);
                decimal gross = line.OrderQty * line.ExpectedCost;
                decimal afterLineDiscount = gross - line.LineDiscount;

                if (afterLineDiscount < 0)
                    afterLineDiscount = 0;

                decimal taxAmount;
                decimal lineTotal;

                if (header.IsTaxInclusive && taxRate > 0)
                {
                    taxAmount = afterLineDiscount - (afterLineDiscount / (1 + taxRate));
                    lineTotal = afterLineDiscount;
                }
                else
                {
                    taxAmount = afterLineDiscount * taxRate;
                    lineTotal = afterLineDiscount + taxAmount;
                }

                line.TaxAmount = Math.Round(taxAmount, 2);
                line.LineTotal = Math.Round(lineTotal, 2);
                line.LineStatus = CalculateLineStatus(line.OrderQty, line.ReceivedQty);

                subtotal += gross;
                lineDiscountTotal += line.LineDiscount;
                taxTotal += line.TaxAmount;
                lineNetTotal += line.LineTotal;
            }

            if (header.GlobalBillDiscount > lineNetTotal)
                throw new InvalidOperationException("Global bill discount cannot be greater than order value.");

            header.Subtotal = Math.Round(subtotal, 2);
            header.TotalTaxAmount = Math.Round(taxTotal, 2);
            header.TotalDiscountAmount = Math.Round(lineDiscountTotal + header.GlobalBillDiscount, 2);
            header.NetPayable = Math.Round(lineNetTotal - header.GlobalBillDiscount, 2);
        }

        private static string CalculateHeaderStatus(List<PoLine> lines, bool isDraft)
        {
            if (isDraft)
                return "Draft";

            if (lines.All(l => l.OrderQty > 0 && l.ReceivedQty >= l.OrderQty))
                return "Closed";

            if (lines.Any(l => l.ReceivedQty > 0))
                return "Partially Received";

            return "Approved";
        }

        private static string CalculateLineStatus(decimal orderQty, decimal receivedQty)
        {
            if (receivedQty <= 0)
                return "Open";

            if (receivedQty < orderQty)
                return "Partially Received";

            return "Closed";
        }

        private static decimal GetTaxRate(string taxCode)
        {
            string value = NormalizeText(taxCode).ToUpperInvariant();

            if (value.Contains("18"))
                return 0.18m;

            if (value.Contains("5"))
                return 0.05m;

            return 0m;
        }

        private static void NormalizeHeader(PoHeader header)
        {
            header.Terms = NormalizeText(header.Terms);
            header.Remarks = NormalizeText(header.Remarks);
            header.CreatedBy = NormalizeText(header.CreatedBy);
            header.ApprovedBy = NormalizeText(header.ApprovedBy);
        }

        private static void NormalizeLines(List<PoLine> lines)
        {
            foreach (var line in lines)
            {
                line.Uom = NormalizeText(line.Uom);
                line.SupplierItemCode = NormalizeText(line.SupplierItemCode);
                line.TaxCode = NormalizeText(line.TaxCode);
            }
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}