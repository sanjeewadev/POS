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

        public int SupplierId { get; set; }

        public string SupplierName { get; set; } = string.Empty;

        public DateTime OrderDate { get; set; }

        public DateTime ExpectedDate { get; set; }

        public decimal Subtotal { get; set; }

        public decimal TotalDiscountAmount { get; set; }

        public decimal TotalTaxAmount { get; set; }

        public decimal NetPayable { get; set; }

        public string Status { get; set; } = string.Empty;

        public string CreatedBy { get; set; } = string.Empty;

        public decimal TotalOrderedQty { get; set; }

        public decimal TotalReceivedQty { get; set; }

        public string DisplayText =>
            $"{PoNumber} | {SupplierName} | {Status} | Rs. {NetPayable:N2}";
    }

    public class PoVariantLookupDto
    {
        public int ItemVariantId { get; set; }

        public int ItemParentId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string PrintName { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;

        public string Uom { get; set; } = string.Empty;

        public string TaxCode { get; set; } = "VAT";

        public decimal VatRatePercent { get; set; } = 0m;

        public bool IsVatIncluded { get; set; } = false;

        public decimal LastSupplierCost { get; set; } = 0m;

        public decimal CurrentCost { get; set; } = 0m;

        public string SupplierItemCode { get; set; } = string.Empty;

        public int Moq { get; set; } = 1;

        public bool AllowDecimalQuantity { get; set; } = false;

        public decimal CurrentSOH { get; set; } = 0m;

        public string FullDisplayName =>
            PoDisplayNameHelper.BuildDisplayName(Description, VariantDescription, SkuCode);

        public string ReceiptDisplayName =>
            PoDisplayNameHelper.BuildDisplayName(
                string.IsNullOrWhiteSpace(PrintName) ? Description : PrintName,
                VariantDescription,
                FullDisplayName);

        public string DisplayName => FullDisplayName;

        public string VariantDisplayName =>
            PoDisplayNameHelper.IsStandardVariantDescription(VariantDescription)
                ? "Standard"
                : PoDisplayNameHelper.NormalizeText(VariantDescription);
    }

    public class PoRepository
    {
        private const int DefaultTakeLimit = 500;
        private const int MaxTakeLimit = 2000;

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public PoRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        // =========================================================
        // LOOKUPS
        // =========================================================

        public async Task<IEnumerable<Supplier>> GetActiveSuppliersAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Suppliers
                .AsNoTracking()
                .Where(s => !s.IsDeactivated)
                .OrderBy(s => s.SupplierName)
                .ThenBy(s => s.SupplierCode)
                .ToListAsync();
        }

        public async Task<IEnumerable<PoHeader>> GetOpenPurchaseOrdersAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // New rule:
            // Only Approved POs are open for receiving.
            // Partially Received POs should behave as closed in the user workflow.
            return await context.PoHeaders
                .Include(p => p.Supplier)
                .Include(p => p.PoLines)
                    .ThenInclude(l => l.ItemVariant)
                        .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .Where(p => p.Status == "Approved")
                .Where(p => p.PoLines.Any(l => l.ReceivedQty < l.OrderQty))
                .OrderByDescending(p => p.OrderDate)
                .ThenBy(p => p.PoNumber)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<ItemMasterSummaryDto>> GetSupplierApprovedItemSummariesAsync(
            int supplierId,
            string searchTerm = "",
            int take = DefaultTakeLimit)
        {
            take = NormalizeTakeLimit(take);

            if (supplierId <= 0)
                return new List<ItemMasterSummaryDto>();

            await using var context = await _contextFactory.CreateDbContextAsync();

            IQueryable<ItemParent> query = context.ItemParents
                .AsNoTracking()
                .Where(p =>
                    !p.IsDeactivated &&
                    !p.IsPurchaseLocked &&
                    p.Variants.Any(v =>
                        !v.IsDeactivated &&
                        v.ItemSuppliers.Any(s =>
                            s.SupplierId == supplierId &&
                            !s.Supplier.IsDeactivated)));

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();

                query = query.Where(p =>
                    EF.Functions.Like(p.ItemCode, $"%{term}%") ||
                    EF.Functions.Like(p.ItemName, $"%{term}%") ||
                    EF.Functions.Like(p.Category.CategoryName, $"%{term}%") ||
                    p.Variants.Any(v =>
                        EF.Functions.Like(v.SkuCode, $"%{term}%") ||
                        EF.Functions.Like(v.Barcode, $"%{term}%") ||
                        EF.Functions.Like(v.VariantDescription, $"%{term}%")));
            }

            return await query
                .OrderBy(p => p.ItemName)
                .ThenBy(p => p.ItemCode)
                .Select(p => new ItemMasterSummaryDto
                {
                    ParentId = p.Id,
                    ItemCode = p.ItemCode,
                    ItemName = p.ItemName,
                    CategoryName = p.Category.CategoryName,
                    VariantCount = p.Variants.Count(v =>
                        !v.IsDeactivated &&
                        v.ItemSuppliers.Any(s =>
                            s.SupplierId == supplierId &&
                            !s.Supplier.IsDeactivated)),
                    TotalStockOnHand = 0m,
                    IsDeactivated = p.IsDeactivated,
                    StatusText = p.IsDeactivated ? "Deactivated" : "Active",
                    HasBatchTracking = p.HasBatchTracking,
                    HasExpiryTracking = p.HasExpiryTracking || p.HasBatchExpiry
                })
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<PoVariantLookupDto>> GetSupplierApprovedVariantsByParentAsync(
            int parentId,
            int supplierId)
        {
            if (parentId <= 0 || supplierId <= 0)
                return new List<PoVariantLookupDto>();

            await using var context = await _contextFactory.CreateDbContextAsync();

            var rows = await context.ItemVariants
                .AsNoTracking()
                .Where(v =>
                    v.ItemParentId == parentId &&
                    !v.IsDeactivated &&
                    !v.ItemParent.IsDeactivated &&
                    !v.ItemParent.IsPurchaseLocked &&
                    v.ItemSuppliers.Any(s =>
                        s.SupplierId == supplierId &&
                        !s.Supplier.IsDeactivated))
                .Select(v => new
                {
                    ItemVariantId = v.Id,
                    v.ItemParentId,
                    ItemCode = v.ItemParent.ItemCode,
                    v.SkuCode,
                    Barcode = v.Barcode ?? string.Empty,
                    Description = v.ItemParent.ItemName,
                    PrintName = v.ItemParent.PrintName,
                    VariantDescription = string.IsNullOrWhiteSpace(v.VariantDescription)
                        ? "Standard"
                        : v.VariantDescription,
                    Uom = string.IsNullOrWhiteSpace(v.ItemParent.BaseUom)
                        ? v.ItemParent.UnitOfMeasure.UomCode
                        : v.ItemParent.BaseUom,
                    TaxCode = string.IsNullOrWhiteSpace(v.ItemParent.TaxCode)
                        ? "VAT"
                        : v.ItemParent.TaxCode,
                    LastSupplierCost = v.ItemSuppliers
                        .Where(s => s.SupplierId == supplierId)
                        .Select(s => s.LastCostPrice)
                        .FirstOrDefault(),
                    CurrentCost = v.CostPrice,
                    SupplierItemCode = v.ItemSuppliers
                        .Where(s => s.SupplierId == supplierId)
                        .Select(s => s.SupplierItemCode)
                        .FirstOrDefault(),
                    Moq = v.ItemSuppliers
                        .Where(s => s.SupplierId == supplierId)
                        .Select(s => s.MinimumOrderQuantity)
                        .FirstOrDefault(),
                    AllowDecimalQuantity = v.ItemParent.UnitOfMeasure.AllowDecimals
                })
                .ToListAsync();

            return rows
                .Select(r => new PoVariantLookupDto
                {
                    ItemVariantId = r.ItemVariantId,
                    ItemParentId = r.ItemParentId,
                    ItemCode = r.ItemCode,
                    SkuCode = r.SkuCode,
                    Barcode = r.Barcode,
                    Description = r.Description,
                    PrintName = r.PrintName,
                    VariantDescription = r.VariantDescription,
                    Uom = r.Uom,
                    TaxCode = r.TaxCode,
                    VatRatePercent = ResolveVatRatePercent(r.TaxCode),
                    IsVatIncluded = false,
                    LastSupplierCost = r.LastSupplierCost,
                    CurrentCost = r.CurrentCost,
                    SupplierItemCode = r.SupplierItemCode ?? string.Empty,
                    Moq = r.Moq <= 0 ? 1 : r.Moq,
                    AllowDecimalQuantity = r.AllowDecimalQuantity,
                    CurrentSOH = 0m
                })
                .OrderBy(r => r.FullDisplayName)
                .ThenBy(r => r.SkuCode)
                .ToList();
        }

        public async Task<PoVariantLookupDto?> GetSupplierApprovedVariantByBarcodeOrSkuAsync(
            string barcodeOrSku,
            int supplierId)
        {
            string term = NormalizeText(barcodeOrSku);

            if (string.IsNullOrWhiteSpace(term) || supplierId <= 0)
                return null;

            string upperTerm = term.ToUpperInvariant();

            await using var context = await _contextFactory.CreateDbContextAsync();

            var row = await context.ItemVariants
                .AsNoTracking()
                .Where(v =>
                    !v.IsDeactivated &&
                    !v.ItemParent.IsDeactivated &&
                    !v.ItemParent.IsPurchaseLocked &&
                    (
                        v.SkuCode.ToUpper() == upperTerm ||
                        ((v.Barcode ?? string.Empty).ToUpper()) == upperTerm
                    ) &&
                    v.ItemSuppliers.Any(s =>
                        s.SupplierId == supplierId &&
                        !s.Supplier.IsDeactivated))
                .Select(v => new
                {
                    ItemVariantId = v.Id,
                    v.ItemParentId,
                    ItemCode = v.ItemParent.ItemCode,
                    v.SkuCode,
                    Barcode = v.Barcode ?? string.Empty,
                    Description = v.ItemParent.ItemName,
                    PrintName = v.ItemParent.PrintName,
                    VariantDescription = string.IsNullOrWhiteSpace(v.VariantDescription)
                        ? "Standard"
                        : v.VariantDescription,
                    Uom = string.IsNullOrWhiteSpace(v.ItemParent.BaseUom)
                        ? v.ItemParent.UnitOfMeasure.UomCode
                        : v.ItemParent.BaseUom,
                    TaxCode = string.IsNullOrWhiteSpace(v.ItemParent.TaxCode)
                        ? "VAT"
                        : v.ItemParent.TaxCode,
                    LastSupplierCost = v.ItemSuppliers
                        .Where(s => s.SupplierId == supplierId)
                        .Select(s => s.LastCostPrice)
                        .FirstOrDefault(),
                    CurrentCost = v.CostPrice,
                    SupplierItemCode = v.ItemSuppliers
                        .Where(s => s.SupplierId == supplierId)
                        .Select(s => s.SupplierItemCode)
                        .FirstOrDefault(),
                    Moq = v.ItemSuppliers
                        .Where(s => s.SupplierId == supplierId)
                        .Select(s => s.MinimumOrderQuantity)
                        .FirstOrDefault(),
                    AllowDecimalQuantity = v.ItemParent.UnitOfMeasure.AllowDecimals
                })
                .FirstOrDefaultAsync();

            if (row == null)
                return null;

            return new PoVariantLookupDto
            {
                ItemVariantId = row.ItemVariantId,
                ItemParentId = row.ItemParentId,
                ItemCode = row.ItemCode,
                SkuCode = row.SkuCode,
                Barcode = row.Barcode,
                Description = row.Description,
                PrintName = row.PrintName,
                VariantDescription = row.VariantDescription,
                Uom = row.Uom,
                TaxCode = row.TaxCode,
                VatRatePercent = ResolveVatRatePercent(row.TaxCode),
                IsVatIncluded = false,
                LastSupplierCost = row.LastSupplierCost,
                CurrentCost = row.CurrentCost,
                SupplierItemCode = row.SupplierItemCode ?? string.Empty,
                Moq = row.Moq <= 0 ? 1 : row.Moq,
                AllowDecimalQuantity = row.AllowDecimalQuantity,
                CurrentSOH = 0m
            };
        }

        // =========================================================
        // SAVE PURCHASE ORDER
        // =========================================================

        public async Task SavePurchaseOrderAsync(
            PoHeader header,
            List<PoLine> lines)
        {
            if (header == null)
                throw new ArgumentNullException(nameof(header));

            if (lines == null || !lines.Any())
                throw new InvalidOperationException("Purchase Order must contain at least one line.");

            NormalizeHeader(header);
            NormalizeLines(lines);
            ValidateSubmittedLineDuplicates(lines);
            RecalculateTotals(header, lines);

            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                await ValidateHeaderAsync(context, header);
                await ValidateLinesAsync(context, header.SupplierId, lines);

                DateTime now = DateTime.Now;

                if (header.Id == 0)
                {
                    header.PoNumber = await GenerateDocumentNumberAsync(context, "PO");
                    header.Status = "Approved";
                    header.CreatedAt = now;
                    header.UpdatedAt = now;
                    header.ApprovedAt = now;
                    header.ApprovedBy = string.IsNullOrWhiteSpace(header.ApprovedBy)
                        ? header.CreatedBy
                        : header.ApprovedBy;
                    header.ClosedAt = null;
                    header.CancelledAt = null;
                    header.CancelledBy = string.Empty;
                    header.CancellationReason = string.Empty;

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
                    await UpdateExistingPurchaseOrderAsync(context, header, lines, now);
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

        // Backward-compatible overload for old ViewModel code.
        // Draft is intentionally ignored because Draft workflow is removed.
        public Task SavePurchaseOrderAsync(
            PoHeader header,
            List<PoLine> lines,
            bool isDraft)
        {
            return SavePurchaseOrderAsync(header, lines);
        }

        private static async Task UpdateExistingPurchaseOrderAsync(
            AppDbContext context,
            PoHeader incomingHeader,
            List<PoLine> incomingLines,
            DateTime now)
        {
            var existingHeader = await context.PoHeaders
                .Include(p => p.PoLines)
                .FirstOrDefaultAsync(p => p.Id == incomingHeader.Id);

            if (existingHeader == null)
                throw new InvalidOperationException("Purchase Order was not found.");

            if (existingHeader.Status == "Cancelled")
                throw new InvalidOperationException("Cancelled Purchase Orders cannot be edited.");

            if (existingHeader.Status == "Closed")
                throw new InvalidOperationException("Closed Purchase Orders cannot be edited.");

            if (existingHeader.PoLines.Any(l => l.ReceivedQty > 0))
            {
                throw new InvalidOperationException(
                    "This Purchase Order already has received quantities and cannot be edited.");
            }

            existingHeader.SupplierId = incomingHeader.SupplierId;
            existingHeader.OrderDate = incomingHeader.OrderDate.Date;
            existingHeader.ExpectedDate = incomingHeader.ExpectedDate.Date;
            existingHeader.Terms = incomingHeader.Terms;
            existingHeader.CreditDays = incomingHeader.CreditDays;
            existingHeader.Remarks = incomingHeader.Remarks;

            existingHeader.Subtotal = incomingHeader.Subtotal;
            existingHeader.GlobalBillDiscount = incomingHeader.GlobalBillDiscount;
            existingHeader.TotalTaxAmount = incomingHeader.TotalTaxAmount;
            existingHeader.TotalDiscountAmount = incomingHeader.TotalDiscountAmount;
            existingHeader.NetPayable = incomingHeader.NetPayable;
            existingHeader.IsTaxInclusive = incomingHeader.IsTaxInclusive;

            existingHeader.Status = "Approved";
            existingHeader.UpdatedAt = now;

            if (existingHeader.ApprovedAt == null)
            {
                existingHeader.ApprovedAt = now;
                existingHeader.ApprovedBy = string.IsNullOrWhiteSpace(incomingHeader.ApprovedBy)
                    ? incomingHeader.CreatedBy
                    : incomingHeader.ApprovedBy;
            }

            existingHeader.ClosedAt = null;

            var existingLines = existingHeader.PoLines.ToList();

            var incomingLineIds = incomingLines
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

            foreach (var incomingLine in incomingLines)
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

                if (existingLine.ReceivedQty > 0)
                {
                    throw new InvalidOperationException(
                        $"Cannot edit PO line for variant ID {existingLine.ItemVariantId} because it has already been received.");
                }

                existingLine.ItemVariantId = incomingLine.ItemVariantId;
                existingLine.Uom = incomingLine.Uom;
                existingLine.SupplierItemCode = incomingLine.SupplierItemCode;
                existingLine.OrderQty = incomingLine.OrderQty;
                existingLine.ExpectedCost = incomingLine.ExpectedCost;

                existingLine.LineDiscountMode = incomingLine.LineDiscountMode;
                existingLine.LineDiscountValue = incomingLine.LineDiscountValue;
                existingLine.LineDiscount = incomingLine.LineDiscount;

                existingLine.TaxCode = incomingLine.TaxCode;
                existingLine.VatRatePercent = incomingLine.VatRatePercent;
                existingLine.IsVatIncluded = incomingLine.IsVatIncluded;
                existingLine.TaxAmount = incomingLine.TaxAmount;

                existingLine.LineTotal = incomingLine.LineTotal;
                existingLine.LineStatus = "Open";
                existingLine.UpdatedAt = now;
                existingLine.ClosedAt = null;
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

            string number =
                $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString().PadLeft(sequence.PaddingLength, '0')}";

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

            // GRN owns ReceivedQty.
            line.ReceivedQty = 0m;

            line.LineStatus = "Open";
            line.CreatedAt = now;
            line.UpdatedAt = now;
            line.ClosedAt = null;
        }

        // =========================================================
        // DASHBOARD / DETAIL / CANCEL
        // =========================================================

        public async Task<IEnumerable<PoSummaryDto>> GetPoSummariesAsync(
            string searchTerm = "",
            int? supplierId = null,
            string statusFilter = "All",
            DateTime? startDate = null,
            DateTime? endDate = null,
            bool showCancelled = false)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.PoHeaders
                .AsNoTracking()
                .AsQueryable();

            if (!showCancelled)
                query = query.Where(p => p.Status != "Cancelled");

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();

                query = query.Where(p =>
                    EF.Functions.Like(p.PoNumber, $"%{term}%") ||
                    EF.Functions.Like(p.Supplier.SupplierName, $"%{term}%") ||
                    EF.Functions.Like(p.Supplier.SupplierCode, $"%{term}%"));
            }

            if (supplierId.HasValue && supplierId.Value > 0)
                query = query.Where(p => p.SupplierId == supplierId.Value);

            if (!string.IsNullOrWhiteSpace(statusFilter) &&
                !statusFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.Status == statusFilter);
            }

            if (startDate.HasValue)
                query = query.Where(p => p.OrderDate >= startDate.Value.Date);

            if (endDate.HasValue)
            {
                DateTime endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(p => p.OrderDate <= endOfDay);
            }

            var rows = await query
                .OrderByDescending(p => p.OrderDate)
                .ThenByDescending(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    p.PoNumber,
                    p.SupplierId,
                    SupplierName = p.Supplier.SupplierName,
                    p.OrderDate,
                    p.ExpectedDate,
                    p.Subtotal,
                    p.TotalDiscountAmount,
                    p.TotalTaxAmount,
                    p.NetPayable,
                    p.Status,
                    p.CreatedBy,
                    Lines = p.PoLines
                        .Select(l => new
                        {
                            l.OrderQty,
                            l.ReceivedQty
                        })
                        .ToList()
                })
                .Take(500)
                .ToListAsync();

            return rows
                .Select(p => new PoSummaryDto
                {
                    PoHeaderId = p.Id,
                    PoNumber = p.PoNumber,
                    SupplierId = p.SupplierId,
                    SupplierName = p.SupplierName,
                    OrderDate = p.OrderDate,
                    ExpectedDate = p.ExpectedDate,
                    Subtotal = p.Subtotal,
                    TotalDiscountAmount = p.TotalDiscountAmount,
                    TotalTaxAmount = p.TotalTaxAmount,
                    NetPayable = p.NetPayable,
                    Status = p.Status,
                    CreatedBy = p.CreatedBy,
                    TotalOrderedQty = p.Lines.Sum(l => l.OrderQty),
                    TotalReceivedQty = p.Lines.Sum(l => l.ReceivedQty)
                })
                .ToList();
        }

        public async Task<PoHeader?> GetPurchaseOrderDetailsAsync(int poId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var po = await context.PoHeaders
                .Include(p => p.Supplier)
                .Include(p => p.PoLines)
                    .ThenInclude(l => l.ItemVariant)
                        .ThenInclude(v => v.ItemParent)
                            .ThenInclude(p => p.UnitOfMeasure)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == poId);

            if (po == null)
                return null;

            po.PoLines = po.PoLines
                .OrderBy(l => l.ItemVariant.ItemParent.ItemCode)
                .ThenBy(l => l.ItemVariant.VariantDescription)
                .ToList();

            foreach (var line in po.PoLines)
            {
                line.ItemCode = line.ItemVariant.ItemParent.ItemCode;
                line.Description = line.ItemVariant.ItemParent.ItemName;
                line.PrintName = line.ItemVariant.ItemParent.PrintName;
                line.VariantDescription = string.IsNullOrWhiteSpace(line.ItemVariant.VariantDescription)
                    ? "Standard"
                    : line.ItemVariant.VariantDescription;
                line.Barcode = line.ItemVariant.Barcode ?? string.Empty;
                line.SOH = 0m;

                if (line.VatRatePercent <= 0)
                    line.VatRatePercent = ResolveVatRatePercent(line.TaxCode);
            }

            return po;
        }

        public async Task CancelPurchaseOrderAsync(
            int poId,
            string cancelledBy = "",
            string reason = "")
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var po = await context.PoHeaders
                .Include(p => p.PoLines)
                .FirstOrDefaultAsync(p => p.Id == poId);

            if (po == null)
                return;

            if (po.Status == "Cancelled")
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
                line.ClosedAt = null;
            }

            await context.SaveChangesAsync();
        }

        public async Task ClosePurchaseOrderAsync(
            int poId,
            string closedBy = "")
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var po = await context.PoHeaders
                .Include(p => p.PoLines)
                .FirstOrDefaultAsync(p => p.Id == poId);

            if (po == null)
                throw new InvalidOperationException("Purchase Order was not found.");

            if (po.Status == "Cancelled")
                throw new InvalidOperationException("Cancelled Purchase Orders cannot be closed.");

            if (po.Status == "Closed")
                return;

            DateTime now = DateTime.Now;

            po.Status = "Closed";
            po.ClosedAt = now;
            po.UpdatedAt = now;

            foreach (var line in po.PoLines)
            {
                line.LineStatus = "Closed";
                line.ClosedAt ??= now;
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

            if (header.GlobalBillDiscount > header.Subtotal + header.TotalTaxAmount)
                throw new InvalidOperationException("Global bill discount cannot be greater than order value.");
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
                    .ThenInclude(p => p.UnitOfMeasure)
                .Where(v => variantIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id);

            var supplierLinks = await context.ItemSuppliers
                .Where(s =>
                    variantIds.Contains(s.ItemVariantId) &&
                    s.SupplierId == supplierId)
                .ToDictionaryAsync(s => s.ItemVariantId);

            foreach (var line in lines)
            {
                NormalizeLine(line);

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

                bool allowDecimals = variant.ItemParent.UnitOfMeasure?.AllowDecimals ?? true;

                if (!allowDecimals && HasDecimalPart(line.OrderQty))
                {
                    throw new InvalidOperationException(
                        $"Decimal quantity is not allowed for item '{variant.SkuCode}' with UOM '{variant.ItemParent.BaseUom}'.");
                }

                if (supplierLink.MinimumOrderQuantity > 0 &&
                    line.OrderQty < supplierLink.MinimumOrderQuantity)
                {
                    throw new InvalidOperationException(
                        $"Item '{variant.SkuCode}' minimum order quantity is {supplierLink.MinimumOrderQuantity}.");
                }

                if (line.ExpectedCost <= 0)
                    throw new InvalidOperationException($"Expected cost must be greater than zero for item '{variant.SkuCode}'.");

                if (!IsValidDiscountMode(line.LineDiscountMode))
                    throw new InvalidOperationException($"Invalid discount mode for item '{variant.SkuCode}'.");

                if (line.LineDiscountValue < 0)
                    throw new InvalidOperationException($"Discount value cannot be negative for item '{variant.SkuCode}'.");

                if (IsPercentDiscount(line.LineDiscountMode) && line.LineDiscountValue > 100)
                    throw new InvalidOperationException($"Discount percentage cannot be greater than 100 for item '{variant.SkuCode}'.");

                if (line.LineDiscount < 0)
                    throw new InvalidOperationException($"Line discount cannot be negative for item '{variant.SkuCode}'.");

                decimal gross = line.OrderQty * line.ExpectedCost;

                if (line.LineDiscount > gross)
                    throw new InvalidOperationException($"Line discount cannot be greater than line value for item '{variant.SkuCode}'.");

                if (line.VatRatePercent < 0 || line.VatRatePercent > 100)
                    throw new InvalidOperationException($"VAT rate must be between 0 and 100 for item '{variant.SkuCode}'.");

                if (line.Uom.Length > 20)
                    throw new InvalidOperationException($"UOM is too long for item '{variant.SkuCode}'.");

                if (line.SupplierItemCode.Length > 100)
                    throw new InvalidOperationException($"Supplier item code is too long for item '{variant.SkuCode}'.");

                if (line.TaxCode.Length > 20)
                    throw new InvalidOperationException($"Tax code is too long for item '{variant.SkuCode}'.");

                if (string.IsNullOrWhiteSpace(line.SupplierItemCode))
                    line.SupplierItemCode = supplierLink.SupplierItemCode ?? string.Empty;

                if (string.IsNullOrWhiteSpace(line.Uom))
                    line.Uom = string.IsNullOrWhiteSpace(variant.ItemParent.BaseUom)
                        ? variant.ItemParent.UnitOfMeasure?.UomCode ?? "PCS"
                        : variant.ItemParent.BaseUom;
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
                NormalizeLine(line);

                decimal gross = line.OrderQty * line.ExpectedCost;

                line.LineDiscount = CalculateDiscountAmount(
                    gross,
                    line.LineDiscountMode,
                    line.LineDiscountValue);

                decimal afterLineDiscount = gross - line.LineDiscount;

                if (afterLineDiscount < 0)
                    afterLineDiscount = 0;

                decimal vatRate = line.VatRatePercent / 100m;

                if (line.VatRatePercent <= 0)
                {
                    line.TaxAmount = 0m;
                    line.LineTotal = Math.Round(afterLineDiscount, 2);
                }
                else if (line.IsVatIncluded)
                {
                    line.TaxAmount = Math.Round(
                        afterLineDiscount - (afterLineDiscount / (1 + vatRate)),
                        2);

                    line.LineTotal = Math.Round(afterLineDiscount, 2);
                }
                else
                {
                    line.TaxAmount = Math.Round(afterLineDiscount * vatRate, 2);
                    line.LineTotal = Math.Round(afterLineDiscount + line.TaxAmount, 2);
                }

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

        private static decimal CalculateDiscountAmount(
            decimal gross,
            string? discountMode,
            decimal discountValue)
        {
            if (gross <= 0 || discountValue <= 0)
                return 0m;

            if (IsPercentDiscount(discountMode))
                return Math.Round(gross * discountValue / 100m, 2);

            return Math.Round(discountValue, 2);
        }

        private static bool IsValidDiscountMode(string? value)
        {
            string mode = NormalizeDiscountMode(value);

            return mode == "Amount" || mode == "Percent";
        }

        private static bool IsPercentDiscount(string? value)
        {
            return NormalizeDiscountMode(value) == "Percent";
        }

        private static string NormalizeDiscountMode(string? value)
        {
            string mode = NormalizeText(value);

            if (mode.Equals("Percent", StringComparison.OrdinalIgnoreCase) ||
                mode.Equals("%", StringComparison.OrdinalIgnoreCase))
            {
                return "Percent";
            }

            return "Amount";
        }

        private static decimal ResolveVatRatePercent(string? taxCode)
        {
            string value = NormalizeText(taxCode).ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(value) ||
                value == "TAX-FREE" ||
                value == "NONE" ||
                value == "NO VAT" ||
                value == "NOVAT")
            {
                return 0m;
            }

            if (value.Contains("18"))
                return 18m;

            if (value.Contains("15"))
                return 15m;

            if (value.Contains("12"))
                return 12m;

            if (value.Contains("8"))
                return 8m;

            if (value.Contains("5"))
                return 5m;

            return 0m;
        }

        private static bool HasDecimalPart(decimal value)
        {
            return value != Math.Truncate(value);
        }

        private static void NormalizeHeader(PoHeader header)
        {
            header.Terms = NormalizeText(header.Terms);

            if (string.IsNullOrWhiteSpace(header.Terms))
                header.Terms = "Credit";

            header.Remarks = NormalizeText(header.Remarks);
            header.CreatedBy = NormalizeText(header.CreatedBy);
            header.ApprovedBy = NormalizeText(header.ApprovedBy);

            if (string.IsNullOrWhiteSpace(header.CreatedBy))
                header.CreatedBy = "Admin";

            if (string.IsNullOrWhiteSpace(header.ApprovedBy))
                header.ApprovedBy = header.CreatedBy;

            header.OrderDate = header.OrderDate.Date;
            header.ExpectedDate = header.ExpectedDate.Date;
        }

        private static void NormalizeLines(List<PoLine> lines)
        {
            foreach (var line in lines)
                NormalizeLine(line);
        }

        private static void NormalizeLine(PoLine line)
        {
            line.Uom = NormalizeText(line.Uom);
            line.SupplierItemCode = NormalizeText(line.SupplierItemCode);
            line.TaxCode = NormalizeText(line.TaxCode);
            line.LineDiscountMode = NormalizeDiscountMode(line.LineDiscountMode);

            if (string.IsNullOrWhiteSpace(line.TaxCode))
                line.TaxCode = line.VatRatePercent > 0 ? "VAT" : "TAX-FREE";

            // Backward compatibility:
            // Old UI may still send fixed LineDiscount without LineDiscountValue.
            if (line.LineDiscountValue <= 0 &&
                line.LineDiscount > 0 &&
                line.LineDiscountMode == "Amount")
            {
                line.LineDiscountValue = line.LineDiscount;
            }

            if (line.VatRatePercent <= 0 && !string.IsNullOrWhiteSpace(line.TaxCode))
                line.VatRatePercent = ResolveVatRatePercent(line.TaxCode);
        }

        private static int NormalizeTakeLimit(int take)
        {
            if (take <= 0)
                return DefaultTakeLimit;

            if (take > MaxTakeLimit)
                return MaxTakeLimit;

            return take;
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }

    internal static class PoDisplayNameHelper
    {
        public static string BuildDisplayName(
            string? baseName,
            string? variantDescription,
            string? fallback)
        {
            string cleanBaseName = NormalizeText(baseName);
            string cleanVariant = NormalizeText(variantDescription);
            string cleanFallback = NormalizeText(fallback);

            if (IsStandardVariantDescription(cleanVariant))
            {
                if (!string.IsNullOrWhiteSpace(cleanBaseName))
                    return cleanBaseName;

                return cleanFallback;
            }

            if (string.IsNullOrWhiteSpace(cleanBaseName))
                return cleanVariant;

            return $"{cleanBaseName} - {cleanVariant}";
        }

        public static bool IsStandardVariantDescription(string? value)
        {
            string cleanValue = NormalizeText(value);

            return string.IsNullOrWhiteSpace(cleanValue) ||
                   cleanValue.Equals("Standard", StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}