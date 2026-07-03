using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;

namespace POS.Core.Repositories
{
    public class GrnRepository
    {
        private const string NonBatchStockBucketNo = "GENERAL";
        private const int DefaultTakeLimit = 500;
        private const int MaxTakeLimit = 2000;

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public GrnRepository(IDbContextFactory<AppDbContext> contextFactory)
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

        public async Task<bool> SupplierInvoiceExistsAsync(
            int supplierId,
            string supplierInvoiceNo)
        {
            string invoiceNo = NormalizeText(supplierInvoiceNo);

            if (supplierId <= 0 || string.IsNullOrWhiteSpace(invoiceNo))
                return false;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.GrnHeaders
                .AsNoTracking()
                .AnyAsync(g =>
                    g.SupplierId == supplierId &&
                    g.SupplierInvoiceNo.ToUpper() == invoiceNo.ToUpper() &&
                    g.Status != "Cancelled");
        }

        public async Task<List<GrnPoLookupDto>> GetOpenPurchaseOrderLookupsAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // New rule:
            // Only Approved POs are available for GRN.
            // After a PO-based GRN is posted, the PO becomes Closed.
            var rows = await context.PoHeaders
                .AsNoTracking()
                .Where(p => p.Status == "Approved")
                .Where(p => p.PoLines.Any(l => l.ReceivedQty < l.OrderQty))
                .OrderByDescending(p => p.OrderDate)
                .ThenBy(p => p.PoNumber)
                .Select(p => new
                {
                    p.Id,
                    p.PoNumber,
                    p.SupplierId,
                    SupplierName = p.Supplier.SupplierName,
                    p.OrderDate,
                    p.ExpectedDate,
                    p.NetPayable,
                    p.Status,
                    Lines = p.PoLines
                        .Select(l => new
                        {
                            l.OrderQty,
                            l.ReceivedQty
                        })
                        .ToList()
                })
                .ToListAsync();

            return rows
                .Select(p => new GrnPoLookupDto
                {
                    PoHeaderId = p.Id,
                    PoNumber = p.PoNumber,
                    SupplierId = p.SupplierId,
                    SupplierName = p.SupplierName,
                    OrderDate = p.OrderDate,
                    ExpectedDate = p.ExpectedDate,
                    NetPayable = p.NetPayable,
                    Status = p.Status,
                    TotalOrderedQty = p.Lines.Sum(l => l.OrderQty),
                    TotalReceivedQty = p.Lines.Sum(l => l.ReceivedQty)
                })
                .ToList();
        }

        // Compatibility for existing ViewModel code.
        public async Task<IEnumerable<PoHeader>> GetOpenPurchaseOrdersAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.PoHeaders
                .Include(p => p.Supplier)
                .Include(p => p.PoLines)
                .AsNoTracking()
                .Where(p => p.Status == "Approved")
                .Where(p => p.PoLines.Any(l => l.ReceivedQty < l.OrderQty))
                .OrderByDescending(p => p.OrderDate)
                .ThenBy(p => p.PoNumber)
                .ToListAsync();
        }

        public async Task<PoHeader?> GetApprovedPoDetailsAsync(int poId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var po = await context.PoHeaders
                .Include(p => p.Supplier)
                .Include(p => p.PoLines)
                    .ThenInclude(l => l.ItemVariant)
                        .ThenInclude(v => v.ItemParent)
                            .ThenInclude(p => p.UnitOfMeasure)
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.Id == poId &&
                    p.Status == "Approved");

            if (po == null)
                return null;

            po.PoLines = po.PoLines
                .Where(l => l.ReceivedQty < l.OrderQty)
                .OrderBy(l => l.ItemVariant.ItemParent.ItemCode)
                .ThenBy(l => l.ItemVariant.VariantDescription)
                .ToList();

            return po;
        }

        public async Task<List<GrnPoLineDto>> GetOutstandingPoLinesAsync(int poId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var poExists = await context.PoHeaders
                .AsNoTracking()
                .AnyAsync(p =>
                    p.Id == poId &&
                    p.Status == "Approved");

            if (!poExists)
                return new List<GrnPoLineDto>();

            return await context.PoLines
                .AsNoTracking()
                .Where(l =>
                    l.PoHeaderId == poId &&
                    l.ReceivedQty < l.OrderQty)
                .OrderBy(l => l.ItemVariant.ItemParent.ItemCode)
                .ThenBy(l => l.ItemVariant.VariantDescription)
                .Select(l => new GrnPoLineDto
                {
                    PoLineId = l.Id,
                    ItemVariantId = l.ItemVariantId,
                    ItemCode = l.ItemVariant.ItemParent.ItemCode,
                    SkuCode = l.ItemVariant.SkuCode,
                    Barcode = l.ItemVariant.Barcode ?? string.Empty,
                    Description = l.ItemVariant.ItemParent.ItemName,
                    PrintName = l.ItemVariant.ItemParent.PrintName,
                    VariantDescription = string.IsNullOrWhiteSpace(l.ItemVariant.VariantDescription)
                        ? "Standard"
                        : l.ItemVariant.VariantDescription,
                    Uom = string.IsNullOrWhiteSpace(l.Uom)
                        ? l.ItemVariant.ItemParent.UnitOfMeasure.UomCode
                        : l.Uom,
                    OrderedQty = l.OrderQty,
                    AlreadyReceivedQty = l.ReceivedQty,
                    ExpectedCost = l.ExpectedCost,

                    HasBatchTracking = l.ItemVariant.ItemParent.HasBatchTracking,
                    HasExpiryTracking = l.ItemVariant.ItemParent.HasExpiryTracking ||
                                        l.ItemVariant.ItemParent.HasBatchExpiry,
                    IsScaleItem = l.ItemVariant.ItemParent.IsScaleItem,
                    AllowDecimalQuantity = l.ItemVariant.ItemParent.UnitOfMeasure.AllowDecimals,

                    LineDiscountMode = string.IsNullOrWhiteSpace(l.LineDiscountMode)
                        ? "Amount"
                        : l.LineDiscountMode,
                    LineDiscountValue = l.LineDiscountValue,
                    LineDiscount = l.LineDiscount,

                    VatRatePercent = l.VatRatePercent,
                    IsVatIncluded = l.IsVatIncluded,
                    VatAmount = l.TaxAmount,

                    CurrentRetailPrice = l.ItemVariant.RetailPrice,
                    CurrentWholesalePrice = l.ItemVariant.WholesalePrice,
                    CurrentMinimumPrice = l.ItemVariant.MinimumPrice,
                    CurrentMaximumPrice = l.ItemVariant.MaximumPrice
                })
                .ToListAsync();
        }

        // =========================================================
        // SUPPLIER-APPROVED DIRECT GRN ITEM LOOKUPS
        // =========================================================

        public async Task<IReadOnlyList<ItemMasterSummaryDto>> GetReceivableItemParentsForSupplierAsync(
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

        public async Task<List<GrnVariantLookupDto>> GetReceivableVariantsByParentForSupplierAsync(
            int parentId,
            int supplierId)
        {
            if (parentId <= 0 || supplierId <= 0)
                return new List<GrnVariantLookupDto>();

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
                    LastSupplierCost = v.ItemSuppliers
                        .Where(s => s.SupplierId == supplierId)
                        .Select(s => s.LastCostPrice)
                        .FirstOrDefault(),
                    CurrentCost = v.CostPrice,
                    HasBatchTracking = v.ItemParent.HasBatchTracking,
                    HasExpiryTracking = v.ItemParent.HasExpiryTracking || v.ItemParent.HasBatchExpiry,
                    IsScaleItem = v.ItemParent.IsScaleItem,
                    AllowDecimalQuantity = v.ItemParent.UnitOfMeasure.AllowDecimals,
                    CurrentRetailPrice = v.RetailPrice,
                    CurrentWholesalePrice = v.WholesalePrice,
                    CurrentMinimumPrice = v.MinimumPrice,
                    CurrentMaximumPrice = v.MaximumPrice,
                    TaxCode = string.IsNullOrWhiteSpace(v.ItemParent.TaxCode)
                        ? "VAT"
                        : v.ItemParent.TaxCode
                })
                .ToListAsync();

            return rows
                .Select(r => new GrnVariantLookupDto
                {
                    ItemVariantId = r.ItemVariantId,
                    ItemParentId = r.ItemParentId,
                    ItemCode = r.ItemCode,
                    SkuCode = r.SkuCode,
                    Barcode = r.Barcode,
                    Description = r.Description,
                    PrintName = r.PrintName,
                    VariantDescription = r.VariantDescription,
                    Uom = string.IsNullOrWhiteSpace(r.Uom) ? "PCS" : r.Uom,
                    LastSupplierCost = r.LastSupplierCost,
                    CurrentCost = r.CurrentCost,
                    HasBatchTracking = r.HasBatchTracking,
                    HasExpiryTracking = r.HasExpiryTracking,
                    IsScaleItem = r.IsScaleItem,
                    AllowDecimalQuantity = r.AllowDecimalQuantity,
                    CurrentRetailPrice = r.CurrentRetailPrice,
                    CurrentWholesalePrice = r.CurrentWholesalePrice,
                    CurrentMinimumPrice = r.CurrentMinimumPrice,
                    CurrentMaximumPrice = r.CurrentMaximumPrice,
                    VatRatePercent = ResolveVatRatePercent(r.TaxCode),
                    IsVatIncluded = false
                })
                .OrderBy(v => v.FullDisplayName)
                .ThenBy(v => v.SkuCode)
                .ToList();
        }

        public async Task<GrnVariantLookupDto?> GetReceivableVariantByBarcodeOrSkuAsync(
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
                    LastSupplierCost = v.ItemSuppliers
                        .Where(s => s.SupplierId == supplierId)
                        .Select(s => s.LastCostPrice)
                        .FirstOrDefault(),
                    CurrentCost = v.CostPrice,
                    HasBatchTracking = v.ItemParent.HasBatchTracking,
                    HasExpiryTracking = v.ItemParent.HasExpiryTracking || v.ItemParent.HasBatchExpiry,
                    IsScaleItem = v.ItemParent.IsScaleItem,
                    AllowDecimalQuantity = v.ItemParent.UnitOfMeasure.AllowDecimals,
                    CurrentRetailPrice = v.RetailPrice,
                    CurrentWholesalePrice = v.WholesalePrice,
                    CurrentMinimumPrice = v.MinimumPrice,
                    CurrentMaximumPrice = v.MaximumPrice,
                    TaxCode = string.IsNullOrWhiteSpace(v.ItemParent.TaxCode)
                        ? "VAT"
                        : v.ItemParent.TaxCode
                })
                .FirstOrDefaultAsync();

            if (row == null)
                return null;

            return new GrnVariantLookupDto
            {
                ItemVariantId = row.ItemVariantId,
                ItemParentId = row.ItemParentId,
                ItemCode = row.ItemCode,
                SkuCode = row.SkuCode,
                Barcode = row.Barcode,
                Description = row.Description,
                PrintName = row.PrintName,
                VariantDescription = row.VariantDescription,
                Uom = string.IsNullOrWhiteSpace(row.Uom) ? "PCS" : row.Uom,
                LastSupplierCost = row.LastSupplierCost,
                CurrentCost = row.CurrentCost,
                HasBatchTracking = row.HasBatchTracking,
                HasExpiryTracking = row.HasExpiryTracking,
                IsScaleItem = row.IsScaleItem,
                AllowDecimalQuantity = row.AllowDecimalQuantity,
                CurrentRetailPrice = row.CurrentRetailPrice,
                CurrentWholesalePrice = row.CurrentWholesalePrice,
                CurrentMinimumPrice = row.CurrentMinimumPrice,
                CurrentMaximumPrice = row.CurrentMaximumPrice,
                VatRatePercent = ResolveVatRatePercent(row.TaxCode),
                IsVatIncluded = false
            };
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
            RecalculateHeaderTotals(header, lines);

            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                await ValidateHeaderAsync(context, header);
                await ValidateLinesAsync(context, header, lines);

                DateTime now = DateTime.Now;

                var variantIds = lines
                    .Select(l => l.ItemVariantId)
                    .Distinct()
                    .ToList();

                var variants = await context.ItemVariants
                    .Include(v => v.ItemParent)
                        .ThenInclude(p => p.UnitOfMeasure)
                    .Where(v => variantIds.Contains(v.Id))
                    .ToDictionaryAsync(v => v.Id);

                header.GrnNumber = await GenerateDocumentNumberAsync(context, "GRN");
                header.Status = "Posted";
                header.CreatedAt = now;
                header.UpdatedAt = now;
                header.PostedAt = now;

                if (string.IsNullOrWhiteSpace(header.CreatedBy))
                    header.CreatedBy = "Admin";

                if (string.IsNullOrWhiteSpace(header.PostedBy))
                    header.PostedBy = header.CreatedBy;

                header.Supplier = null!;
                header.PurchaseOrder = null;
                header.GrnLines = new List<GrnLine>();

                await context.GrnHeaders.AddAsync(header);
                await context.SaveChangesAsync();

                PrepareLinesForPosting(
                    lines,
                    variants,
                    header.Id,
                    header.GrnNumber,
                    now);

                ValidatePreparedLineDuplicates(lines);

                foreach (var line in lines)
                    await context.GrnLines.AddAsync(line);

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

                var stockCache = await LoadCurrentStockCacheAsync(context, variantIds);

                foreach (var line in lines)
                {
                    if (!variants.TryGetValue(line.ItemVariantId, out var variant))
                        throw new InvalidOperationException("One or more received item variants were not found.");

                    if (linkedPo != null)
                        ApplyPoReceivingAndCloseLine(linkedPo, line, variant.SkuCode, now);

                    await UpdateSupplierLastCostAsync(
                        context,
                        header.SupplierId,
                        line.ItemVariantId,
                        line.UnitCost,
                        now);

                    UpdateVariantCostAndSellingPrices(
                        variant,
                        line,
                        stockCache,
                        now);

                    var batch = await CreateOrUpdateBatchAsync(
                        context,
                        variant,
                        line,
                        header,
                        now);

                    line.ItemBatch = batch;
                    line.ItemBatchId = batch.Id;

                    var inventoryTx = new InventoryTransaction
                    {
                        ItemVariantId = variant.Id,
                        ItemBatch = batch,
                        TransactionDate = header.ReceivedDate.Date,
                        TransactionType = "GRN",
                        ReferenceDocument = header.GrnNumber,
                        ReferenceLineId = line.Id,
                        Quantity = line.ReceivedQty,
                        UnitCost = line.LandedCost,
                        CreatedBy = header.CreatedBy,
                        CreatedAt = now,
                        Remarks =
                            $"GRN Receipt | Supplier Invoice: {header.SupplierInvoiceNo} | Batch: {line.BatchNo}"
                    };

                    await context.InventoryTransactions.AddAsync(inventoryTx);
                }

                if (linkedPo != null)
                    CloseLinkedPurchaseOrderAfterGrn(linkedPo, now);

                await CreateSupplierLedgerEntryAsync(
                    context,
                    header,
                    now);

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

        private static async Task ValidateHeaderAsync(
            AppDbContext context,
            GrnHeader header)
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

            if (header.GlobalBillDiscount > header.NetPayable + header.GlobalBillDiscount)
                throw new InvalidOperationException("Global bill discount cannot be greater than GRN value.");

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

                if (po.Status != "Approved")
                    throw new InvalidOperationException("Only approved Purchase Orders can be received.");
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
                    .ThenInclude(p => p.UnitOfMeasure)
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

                bool allowDecimals = variant.ItemParent.UnitOfMeasure?.AllowDecimals ?? true;

                if (!allowDecimals && HasDecimalPart(line.ReceivedQty))
                {
                    throw new InvalidOperationException(
                        $"Decimal quantity is not allowed for item '{variant.SkuCode}' with UOM '{variant.ItemParent.BaseUom}'.");
                }

                if (line.UnitCost <= 0)
                    throw new InvalidOperationException($"Unit cost must be greater than zero for item '{variant.SkuCode}'.");

                if (!IsValidDiscountMode(line.LineDiscountMode))
                    throw new InvalidOperationException($"Invalid discount mode for item '{variant.SkuCode}'.");

                if (line.LineDiscountValue < 0)
                    throw new InvalidOperationException($"Discount value cannot be negative for item '{variant.SkuCode}'.");

                if (IsPercentDiscount(line.LineDiscountMode) && line.LineDiscountValue > 100)
                    throw new InvalidOperationException($"Discount percentage cannot be greater than 100 for item '{variant.SkuCode}'.");

                if (line.LineDiscount < 0)
                    throw new InvalidOperationException($"Line discount cannot be negative for item '{variant.SkuCode}'.");

                decimal gross = line.ReceivedQty * line.UnitCost;

                if (line.LineDiscount > gross)
                    throw new InvalidOperationException($"Line discount cannot be greater than line value for item '{variant.SkuCode}'.");

                if (line.VatRatePercent < 0 || line.VatRatePercent > 100)
                    throw new InvalidOperationException($"VAT rate must be between 0 and 100 for item '{variant.SkuCode}'.");

                if (line.BatchNo.Length > 50)
                    throw new InvalidOperationException($"Batch number is too long for item '{variant.SkuCode}'.");

                if (line.Uom.Length > 20)
                    throw new InvalidOperationException($"UOM is too long for item '{variant.SkuCode}'.");

                bool requiresExpiry = variant.ItemParent.HasExpiryTracking ||
                                      variant.ItemParent.HasBatchExpiry;

                if (requiresExpiry && !line.ExpiryDate.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Expiry date is required for item '{variant.SkuCode}'.");
                }

                if (!requiresExpiry)
                    line.ExpiryDate = null;

                if (line.ExpiryDate.HasValue &&
                    line.ExpiryDate.Value.Date < header.ReceivedDate.Date)
                {
                    throw new InvalidOperationException(
                        $"Expiry date cannot be before received date for item '{variant.SkuCode}'.");
                }

                if (line.UpdateSellingPrices)
                    ValidateSellingPrices(line, variant.SkuCode);

                if (header.PurchaseOrderId.HasValue && header.PurchaseOrderId.Value > 0)
                {
                    PoLine? poLine = null;

                    if (line.PoLineId.HasValue &&
                        poLines.TryGetValue(line.PoLineId.Value, out var exactPoLine))
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
                            $"Cannot receive {line.ReceivedQty:N3} for item '{variant.SkuCode}'. PO ordered remaining quantity is {outstanding:N3}.");
                    }
                }
            }
        }

        private static void ValidatePreparedLineDuplicates(List<GrnLine> lines)
        {
            var duplicate = lines
                .GroupBy(l => new
                {
                    l.ItemVariantId,
                    BatchNo = NormalizeText(l.BatchNo).ToUpperInvariant(),
                    PoLineId = l.PoLineId ?? 0
                })
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
            {
                throw new InvalidOperationException(
                    "Duplicate GRN line found. Merge the same item and stock bucket/batch into one row.");
            }
        }

        private static void ValidateSellingPrices(GrnLine line, string skuCode)
        {
            if (line.NewRetailPrice < 0 ||
                line.NewWholesalePrice < 0 ||
                line.NewMinimumPrice < 0 ||
                line.NewMaximumPrice < 0)
            {
                throw new InvalidOperationException(
                    $"Selling prices cannot be negative for item '{skuCode}'.");
            }

            if (line.NewMaximumPrice > 0 &&
                line.NewMinimumPrice > line.NewMaximumPrice)
            {
                throw new InvalidOperationException(
                    $"Minimum price cannot be greater than maximum price for item '{skuCode}'.");
            }

            if (line.RetailMarkupPercent < -100 ||
                line.WholesaleMarkupPercent < -100)
            {
                throw new InvalidOperationException(
                    $"Markup percentage is invalid for item '{skuCode}'.");
            }
        }

        // =========================================================
        // CALCULATION
        // =========================================================

        private static void RecalculateHeaderTotals(
            GrnHeader header,
            List<GrnLine> lines)
        {
            decimal subtotalGross = 0m;
            decimal lineDiscountTotal = 0m;
            decimal vatTotal = 0m;
            decimal lineTotalPayable = 0m;

            foreach (var line in lines)
            {
                NormalizeLine(line);

                decimal gross = line.ReceivedQty * line.UnitCost;

                line.LineDiscount = CalculateDiscountAmount(
                    gross,
                    line.LineDiscountMode,
                    line.LineDiscountValue);

                decimal afterLineDiscount = gross - line.LineDiscount;

                if (afterLineDiscount < 0)
                    afterLineDiscount = 0m;

                decimal vatRate = line.VatRatePercent / 100m;

                if (line.VatRatePercent <= 0)
                {
                    line.VatAmount = 0m;
                    line.LineTotal = Math.Round(afterLineDiscount, 2);
                }
                else if (line.IsVatIncluded)
                {
                    line.VatAmount = Math.Round(
                        afterLineDiscount - (afterLineDiscount / (1 + vatRate)),
                        2);

                    line.LineTotal = Math.Round(afterLineDiscount, 2);
                }
                else
                {
                    line.VatAmount = Math.Round(afterLineDiscount * vatRate, 2);
                    line.LineTotal = Math.Round(afterLineDiscount + line.VatAmount, 2);
                }

                subtotalGross += gross;
                lineDiscountTotal += line.LineDiscount;
                vatTotal += line.VatAmount;
                lineTotalPayable += line.LineTotal;
            }

            if (header.GlobalBillDiscount > lineTotalPayable)
                throw new InvalidOperationException("Global bill discount cannot be greater than GRN value.");

            header.Subtotal = Math.Round(subtotalGross, 2);
            header.TotalDiscountAmount = Math.Round(lineDiscountTotal + header.GlobalBillDiscount, 2);
            header.TotalVatAmount = Math.Round(vatTotal, 2);

            decimal netPayable = lineTotalPayable - header.GlobalBillDiscount + header.FreightAmount;
            header.NetPayable = Math.Round(netPayable < 0 ? 0m : netPayable, 2);

            AllocateLandedCost(header, lines);
        }

        private static void AllocateLandedCost(
            GrnHeader header,
            List<GrnLine> lines)
        {
            decimal totalCostBase = lines.Sum(GetLineCostBaseForLandedCost);

            if (totalCostBase <= 0)
            {
                foreach (var line in lines)
                    line.LandedCost = 0m;

                return;
            }

            foreach (var line in lines)
            {
                if (line.ReceivedQty <= 0)
                {
                    line.LandedCost = 0m;
                    continue;
                }

                decimal lineCostBase = GetLineCostBaseForLandedCost(line);
                decimal weight = lineCostBase / totalCostBase;

                decimal allocatedFreight = header.FreightAmount * weight;
                decimal allocatedGlobalDiscount = header.GlobalBillDiscount * weight;

                decimal landedLineTotal = lineCostBase + allocatedFreight - allocatedGlobalDiscount;

                if (landedLineTotal < 0)
                    landedLineTotal = 0m;

                line.LandedCost = Math.Round(landedLineTotal / line.ReceivedQty, 2);
            }
        }

        private static decimal GetLineCostBaseForLandedCost(GrnLine line)
        {
            decimal gross = line.ReceivedQty * line.UnitCost;
            decimal afterLineDiscount = gross - line.LineDiscount;

            if (afterLineDiscount < 0)
                afterLineDiscount = 0m;

            if (line.VatRatePercent <= 0)
                return afterLineDiscount;

            if (!line.IsVatIncluded)
                return afterLineDiscount;

            decimal vatRate = line.VatRatePercent / 100m;

            if (vatRate <= 0)
                return afterLineDiscount;

            return afterLineDiscount / (1 + vatRate);
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

        // =========================================================
        // POSTING HELPERS
        // =========================================================

        private static void PrepareLinesForPosting(
            List<GrnLine> lines,
            Dictionary<int, ItemVariant> variants,
            int grnHeaderId,
            string grnNumber,
            DateTime now)
        {
            int lineNumber = 1;

            foreach (var line in lines)
            {
                if (!variants.TryGetValue(line.ItemVariantId, out var variant))
                    throw new InvalidOperationException("One or more received item variants were not found.");

                bool hasBatchTracking = variant.ItemParent.HasBatchTracking;
                bool hasExpiryTracking = variant.ItemParent.HasExpiryTracking ||
                                         variant.ItemParent.HasBatchExpiry;

                line.Id = 0;
                line.GrnHeaderId = grnHeaderId;
                line.GrnHeader = null!;
                line.ItemVariant = null!;
                line.PoLine = null;
                line.ItemBatch = null;
                line.ItemBatchId = null;

                if (!hasBatchTracking)
                {
                    line.BatchNo = NonBatchStockBucketNo;
                    line.ExpiryDate = null;
                }
                else
                {
                    line.BatchNo = BuildBatchNo(line.BatchNo, grnNumber, lineNumber);

                    if (!hasExpiryTracking)
                        line.ExpiryDate = null;
                }

                line.LineStatus = "Posted";
                line.CreatedAt = now;
                line.UpdatedAt = now;

                lineNumber++;
            }
        }

        private static void ApplyPoReceivingAndCloseLine(
            PoHeader linkedPo,
            GrnLine line,
            string skuCode,
            DateTime now)
        {
            var poLine = ResolvePoLineForGrnLine(linkedPo, line);

            if (poLine == null)
            {
                throw new InvalidOperationException(
                    $"GRN line item '{skuCode}' does not match the linked Purchase Order.");
            }

            decimal outstanding = poLine.OrderQty - poLine.ReceivedQty;

            if (line.ReceivedQty > outstanding)
            {
                throw new InvalidOperationException(
                    $"Cannot receive {line.ReceivedQty:N3} for item '{skuCode}'. PO ordered remaining quantity is {outstanding:N3}.");
            }

            poLine.ReceivedQty += line.ReceivedQty;

            // New user workflow:
            // After one PO-based GRN, the PO is treated as completed/closed.
            poLine.LineStatus = "Closed";
            poLine.ClosedAt ??= now;
            poLine.UpdatedAt = now;
        }

        private static void CloseLinkedPurchaseOrderAfterGrn(
            PoHeader linkedPo,
            DateTime now)
        {
            linkedPo.Status = "Closed";
            linkedPo.ClosedAt ??= now;
            linkedPo.UpdatedAt = now;

            foreach (var poLine in linkedPo.PoLines)
            {
                poLine.LineStatus = "Closed";
                poLine.ClosedAt ??= now;
                poLine.UpdatedAt = now;
            }
        }

        private static async Task UpdateSupplierLastCostAsync(
            AppDbContext context,
            int supplierId,
            int itemVariantId,
            decimal unitCost,
            DateTime now)
        {
            var itemSupplier = await context.ItemSuppliers
                .FirstOrDefaultAsync(s =>
                    s.SupplierId == supplierId &&
                    s.ItemVariantId == itemVariantId);

            if (itemSupplier == null)
                return;

            itemSupplier.LastCostPrice = unitCost;
            itemSupplier.UpdatedAt = now;
        }

        private static void UpdateVariantCostAndSellingPrices(
            ItemVariant variant,
            GrnLine line,
            Dictionary<int, decimal> stockCache,
            DateTime now)
        {
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

            if (line.UpdateSellingPrices)
            {
                variant.RetailPrice = line.NewRetailPrice;
                variant.WholesalePrice = line.NewWholesalePrice;
                variant.MinimumPrice = line.NewMinimumPrice;
                variant.MaximumPrice = line.NewMaximumPrice;
            }

            variant.UpdatedAt = now;
            stockCache[variant.Id] = newTotalQty;
        }

        private static async Task<ItemBatch> CreateOrUpdateBatchAsync(
            AppDbContext context,
            ItemVariant variant,
            GrnLine line,
            GrnHeader header,
            DateTime now)
        {
            string batchNo = NormalizeText(line.BatchNo).ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(batchNo))
                throw new InvalidOperationException("Internal error: batch number was not prepared.");

            var batch = await context.ItemBatches
                .FirstOrDefaultAsync(b =>
                    b.ItemVariantId == variant.Id &&
                    b.BatchNo == batchNo);

            if (batch == null)
            {
                batch = new ItemBatch
                {
                    ItemVariantId = variant.Id,
                    BatchNo = batchNo,
                    ExpiryDate = line.ExpiryDate?.Date,
                    ReceivedDate = header.ReceivedDate.Date,
                    CostPrice = line.LandedCost,
                    RetailPrice = variant.RetailPrice,
                    WholesalePrice = variant.WholesalePrice,
                    CurrentStock = line.ReceivedQty,
                    IsDeactivated = false,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                await context.ItemBatches.AddAsync(batch);
                return batch;
            }

            if (batch.IsDeactivated)
            {
                throw new InvalidOperationException(
                    $"Batch '{batchNo}' for item '{variant.SkuCode}' is deactivated.");
            }

            if (line.ExpiryDate.HasValue &&
                batch.ExpiryDate.HasValue &&
                batch.ExpiryDate.Value.Date != line.ExpiryDate.Value.Date)
            {
                throw new InvalidOperationException(
                    $"Batch '{batchNo}' already exists with a different expiry date.");
            }

            if (!batch.ExpiryDate.HasValue && line.ExpiryDate.HasValue)
                batch.ExpiryDate = line.ExpiryDate.Value.Date;

            batch.CurrentStock += line.ReceivedQty;
            batch.CostPrice = line.LandedCost;
            batch.RetailPrice = variant.RetailPrice;
            batch.WholesalePrice = variant.WholesalePrice;
            batch.UpdatedAt = now;

            return batch;
        }

        private static async Task CreateSupplierLedgerEntryAsync(
            AppDbContext context,
            GrnHeader header,
            DateTime now)
        {
            var supplier = await context.Suppliers
                .FirstOrDefaultAsync(s => s.Id == header.SupplierId);

            if (supplier == null)
                throw new InvalidOperationException("Supplier was not found.");

            decimal newSupplierBalance = supplier.CurrentBalance + header.NetPayable;

            var supplierLedger = new SupplierLedger
            {
                SupplierId = supplier.Id,
                GrnHeaderId = header.Id,
                TransactionDate = header.ReceivedDate.Date,
                TransactionType = "GRN",
                ReferenceDocument = header.GrnNumber,
                ChargeAmount = header.NetPayable,
                PaymentAmount = 0m,
                BalanceAfterTransaction = newSupplierBalance,
                DueDate = header.DueDate.Date,
                IsPaid = false,
                CreatedBy = header.CreatedBy,
                CreatedAt = now,
                Remarks = $"Supplier Invoice: {header.SupplierInvoiceNo}"
            };

            supplier.CurrentBalance = newSupplierBalance;
            supplier.UpdatedAt = now;

            await context.SupplierLedgers.AddAsync(supplierLedger);
        }

        // =========================================================
        // GENERAL HELPERS
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

        private static PoLine? ResolvePoLineForGrnLine(
            PoHeader linkedPo,
            GrnLine line)
        {
            if (line.PoLineId.HasValue && line.PoLineId.Value > 0)
            {
                var exact = linkedPo.PoLines.FirstOrDefault(l => l.Id == line.PoLineId.Value);

                if (exact != null)
                    return exact;
            }

            return linkedPo.PoLines.FirstOrDefault(l => l.ItemVariantId == line.ItemVariantId);
        }

        private static string BuildBatchNo(
            string? batchNo,
            string grnNumber,
            int lineNumber)
        {
            string value = NormalizeText(batchNo);

            if (!string.IsNullOrWhiteSpace(value))
                return value.ToUpperInvariant();

            return $"SYS-{grnNumber}-L{lineNumber.ToString().PadLeft(3, '0')}";
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

        private static void NormalizeHeader(GrnHeader header)
        {
            header.SupplierInvoiceNo = NormalizeText(header.SupplierInvoiceNo);
            header.Remarks = NormalizeText(header.Remarks);
            header.CreatedBy = NormalizeText(header.CreatedBy);
            header.PostedBy = NormalizeText(header.PostedBy);

            header.InvoiceDate = header.InvoiceDate.Date;
            header.ReceivedDate = header.ReceivedDate.Date;
            header.DueDate = header.DueDate.Date;

            if (header.CreditDays == 0 && header.DueDate.Date >= header.InvoiceDate.Date)
            {
                header.CreditDays = Math.Max(
                    0,
                    (header.DueDate.Date - header.InvoiceDate.Date).Days);
            }

            if (string.IsNullOrWhiteSpace(header.CreatedBy))
                header.CreatedBy = "Admin";

            if (string.IsNullOrWhiteSpace(header.PostedBy))
                header.PostedBy = header.CreatedBy;
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
            line.LineDiscountMode = NormalizeDiscountMode(line.LineDiscountMode);

            if (string.IsNullOrWhiteSpace(line.Uom))
                line.Uom = "PCS";

            if (line.LineDiscountValue <= 0 &&
                line.LineDiscount > 0 &&
                line.LineDiscountMode == "Amount")
            {
                line.LineDiscountValue = line.LineDiscount;
            }
        }

        private static int NormalizeTakeLimit(int take)
        {
            if (take <= 0)
                return DefaultTakeLimit;

            if (take > MaxTakeLimit)
                return MaxTakeLimit;

            return take;
        }

        private static bool HasDecimalPart(decimal value)
        {
            return value != Math.Truncate(value);
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}