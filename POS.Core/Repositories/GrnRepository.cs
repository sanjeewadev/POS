using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Services.Tax;
using POS.Core.Services.Pricing;
using POS.Core.Utilities;

namespace POS.Core.Repositories
{
    public class GrnRepository
    {
        private const string NonBatchStockBucketNo = "GENERAL";
        private const int DefaultTakeLimit = 500;
        private const int MaxTakeLimit = 2000;

        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly PurchasingTaxService _purchasingTaxService = new();

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

            var rows = await context.PoLines
                .AsNoTracking()
                .Where(l =>
                    l.PoHeaderId == poId &&
                    l.ReceivedQty < l.OrderQty)
                .OrderBy(l => l.ItemVariant.ItemParent.ItemCode)
                .ThenBy(l => l.ItemVariant.VariantDescription)
                .Select(l => new
                {
                    PoLineId = l.Id,
                    l.ItemVariantId,
                    ItemCode = l.ItemVariant.ItemParent.ItemCode,
                    l.ItemVariant.SkuCode,
                    Barcode = l.ItemVariant.Barcode ?? string.Empty,
                    Description = l.ItemVariant.ItemParent.ItemName,
                    PrintName = l.ItemVariant.ItemParent.PrintName,
                    VariantDescription = string.IsNullOrWhiteSpace(l.ItemVariant.VariantDescription)
                        ? "Standard"
                        : l.ItemVariant.VariantDescription,
                    LineUom = l.Uom,
                    MasterUom = l.ItemVariant.ItemParent.UnitOfMeasure.UomCode,
                    OrderedQty = l.OrderQty,
                    AlreadyReceivedQty = l.ReceivedQty,
                    l.ExpectedCost,
                    HasBatchTracking = l.ItemVariant.ItemParent.HasBatchTracking,
                    HasExpiryTracking = l.ItemVariant.ItemParent.HasExpiryTracking ||
                                        l.ItemVariant.ItemParent.HasBatchExpiry,
                    IsScaleItem = l.ItemVariant.ItemParent.IsScaleItem,
                    AllowDecimalQuantity = l.ItemVariant.ItemParent.UnitOfMeasure.AllowDecimals,
                    LineDiscountMode = string.IsNullOrWhiteSpace(l.LineDiscountMode)
                        ? "Amount"
                        : l.LineDiscountMode,
                    l.LineDiscountValue,
                    l.LineDiscount,
                    VatRatePercent = l.TaxSnapshotStatus == TaxSnapshotStatuses.Complete
                        ? l.TaxRatePercentSnapshot ?? l.VatRatePercent
                        : l.VatRatePercent,
                    IsVatIncluded = l.TaxSnapshotStatus == TaxSnapshotStatuses.Complete
                        ? l.IsTaxInclusiveSnapshot ?? l.IsVatIncluded
                        : l.IsVatIncluded,
                    VatAmount = l.TaxSnapshotStatus == TaxSnapshotStatuses.Complete
                        ? l.VatAmountSnapshot ?? l.TaxAmount
                        : l.TaxAmount,
                    TaxCategoryCode = l.TaxCategoryCodeSnapshot ?? string.Empty,
                    TaxCategoryName = l.TaxNameSnapshot ?? l.TaxCategoryCodeSnapshot ?? string.Empty,
                    CurrentRetailPrice = l.ItemVariant.RetailPrice,
                    CurrentWholesalePrice = l.ItemVariant.WholesalePrice,
                    CurrentMinimumPrice = l.ItemVariant.MinimumPrice,
                    CurrentMaximumPrice = l.ItemVariant.MaximumPrice
                })
                .ToListAsync();

            return rows
                .Select(l => new GrnPoLineDto
                {
                    PoLineId = l.PoLineId,
                    ItemVariantId = l.ItemVariantId,
                    ItemCode = l.ItemCode,
                    SkuCode = l.SkuCode,
                    Barcode = l.Barcode,
                    Description = l.Description,
                    PrintName = l.PrintName,
                    VariantDescription = l.VariantDescription,
                    Uom = UomValueResolver.Resolve(l.LineUom, l.MasterUom),
                    OrderedQty = l.OrderedQty,
                    AlreadyReceivedQty = l.AlreadyReceivedQty,
                    ExpectedCost = l.ExpectedCost,
                    HasBatchTracking = l.HasBatchTracking,
                    HasExpiryTracking = l.HasExpiryTracking,
                    IsScaleItem = l.IsScaleItem,
                    AllowDecimalQuantity = l.AllowDecimalQuantity,
                    LineDiscountMode = l.LineDiscountMode,
                    LineDiscountValue = l.LineDiscountValue,
                    LineDiscount = l.LineDiscount,
                    VatRatePercent = l.VatRatePercent,
                    IsVatIncluded = l.IsVatIncluded,
                    VatAmount = l.VatAmount,
                    TaxCategoryCode = l.TaxCategoryCode,
                    TaxCategoryName = l.TaxCategoryName,
                    CurrentRetailPrice = l.CurrentRetailPrice,
                    CurrentWholesalePrice = l.CurrentWholesalePrice,
                    CurrentMinimumPrice = l.CurrentMinimumPrice,
                    CurrentMaximumPrice = l.CurrentMaximumPrice
                })
                .ToList();
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
                    p.ItemType == ItemTypeCodes.StockItem &&
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
                    ItemType = p.ItemType,
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
            int supplierId,
            DateTime transactionDate)
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
                    v.ItemParent.ItemType == ItemTypeCodes.StockItem &&
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
                    BaseUom = v.ItemParent.BaseUom,
                    MasterUom = v.ItemParent.UnitOfMeasure.UomCode,
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
                    TaxCategoryCode = v.ItemParent.TaxCategory != null
                        ? v.ItemParent.TaxCategory.CategoryCode
                        : string.Empty,
                    TaxCategoryName = v.ItemParent.TaxCategory != null
                        ? v.ItemParent.TaxCategory.CategoryName
                        : string.Empty
                })
                .ToListAsync();

            bool supplierIsVatRegistered = await GetSupplierVatRegistrationAsync(
                context,
                supplierId);

            var taxProfiles = await _purchasingTaxService.ResolveProfilesForSupplierAsync(
                context,
                rows.Select(r => r.ItemVariantId).ToList(),
                transactionDate.Date,
                supplierIsVatRegistered);

            return rows
                .Select(r =>
                {
                    var profile = taxProfiles[r.ItemVariantId];

                    return new GrnVariantLookupDto
                    {
                        ItemVariantId = r.ItemVariantId,
                        ItemParentId = r.ItemParentId,
                        ItemCode = r.ItemCode,
                        SkuCode = r.SkuCode,
                        Barcode = r.Barcode,
                        Description = r.Description,
                        PrintName = r.PrintName,
                        VariantDescription = r.VariantDescription,
                        Uom = UomValueResolver.Resolve(r.BaseUom, r.MasterUom),
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
                        TaxCategoryCode = profile.TaxCategoryCode,
                        TaxCategoryName = profile.TaxCategoryName,
                        VatRatePercent = profile.RatePercent,
                        IsVatIncluded = false
                    };
                })
                .OrderBy(v => v.FullDisplayName)
                .ThenBy(v => v.SkuCode)
                .ToList();
        }

        public async Task<GrnVariantLookupDto?> GetReceivableVariantByBarcodeOrSkuAsync(
            string barcodeOrSku,
            int supplierId,
            DateTime transactionDate)
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
                    v.ItemParent.ItemType == ItemTypeCodes.StockItem &&
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
                    BaseUom = v.ItemParent.BaseUom,
                    MasterUom = v.ItemParent.UnitOfMeasure.UomCode,
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
                    TaxCategoryCode = v.ItemParent.TaxCategory != null
                        ? v.ItemParent.TaxCategory.CategoryCode
                        : string.Empty,
                    TaxCategoryName = v.ItemParent.TaxCategory != null
                        ? v.ItemParent.TaxCategory.CategoryName
                        : string.Empty
                })
                .FirstOrDefaultAsync();

            if (row == null)
                return null;

            bool supplierIsVatRegistered = await GetSupplierVatRegistrationAsync(
                context,
                supplierId);

            var taxProfiles = await _purchasingTaxService.ResolveProfilesForSupplierAsync(
                context,
                new[] { row.ItemVariantId },
                transactionDate.Date,
                supplierIsVatRegistered);

            var profile = taxProfiles[row.ItemVariantId];

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
                Uom = UomValueResolver.Resolve(row.BaseUom, row.MasterUom),
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
                TaxCategoryCode = profile.TaxCategoryCode,
                TaxCategoryName = profile.TaxCategoryName,
                VatRatePercent = profile.RatePercent,
                IsVatIncluded = false
            };
        }

        // =========================================================
        // AUTHORITATIVE LIVE PREVIEW
        // =========================================================

        public async Task<GrnTaxPreviewDto> CalculateGrnPreviewAsync(
            DateTime invoiceDate,
            int supplierId,
            bool supplierPricesIncludeVat,
            decimal globalBillDiscount,
            decimal freightAmount,
            IReadOnlyList<GrnLineEntryDto> sourceLines)
        {
            if (sourceLines == null)
                throw new ArgumentNullException(nameof(sourceLines));

            if (globalBillDiscount < 0m)
                throw new InvalidOperationException("Global bill discount cannot be negative.");

            if (freightAmount < 0m)
                throw new InvalidOperationException("Freight amount cannot be negative.");

            var indexedLines = sourceLines
                .Select((line, index) => new { Line = line, Index = index })
                .Where(row =>
                    row.Line.ItemVariantId > 0 &&
                    row.Line.ReceivedQty > 0m &&
                    row.Line.UnitCost > 0m)
                .ToList();

            if (indexedLines.Count == 0)
            {
                return new GrnTaxPreviewDto
                {
                    FreightAmount = Math.Round(
                        freightAmount,
                        2,
                        MidpointRounding.AwayFromZero),
                    NetPayable = Math.Round(
                        freightAmount,
                        2,
                        MidpointRounding.AwayFromZero)
                };
            }

            await using var context = await _contextFactory.CreateDbContextAsync();

            bool supplierIsVatRegistered = await GetSupplierVatRegistrationAsync(
                context,
                supplierId);
            bool documentIsTaxInclusive =
                supplierIsVatRegistered && supplierPricesIncludeVat;

            var profiles = await _purchasingTaxService.ResolveProfilesForSupplierAsync(
                context,
                indexedLines
                    .Select(row => row.Line.ItemVariantId)
                    .Distinct()
                    .ToList(),
                invoiceDate.Date,
                supplierIsVatRegistered);

            var calculation = _purchasingTaxService.CalculateDocument(
                indexedLines
                    .Select(row => new PurchasingTaxLineInput
                    {
                        ItemVariantId = row.Line.ItemVariantId,
                        Quantity = row.Line.ReceivedQty,
                        UnitPrice = row.Line.UnitCost,
                        DiscountMode = row.Line.LineDiscountMode,
                        DiscountValue = row.Line.LineDiscountValue,
                        TaxProfile = profiles[row.Line.ItemVariantId]
                    })
                    .ToList(),
                globalBillDiscount,
                documentIsTaxInclusive);

            decimal totalTaxExclusiveBase = calculation.Lines.Sum(line => line.TaxableAmount);
            decimal allocatedFreightTotal = 0m;
            var previewLines = new List<GrnTaxPreviewLineDto>(calculation.Lines.Count);

            for (int index = 0; index < calculation.Lines.Count; index++)
            {
                var result = calculation.Lines[index];
                var source = indexedLines[index];
                bool isLast = index == calculation.Lines.Count - 1;

                decimal allocatedFreight = 0m;

                if (totalTaxExclusiveBase > 0m && freightAmount > 0m)
                {
                    allocatedFreight = isLast
                        ? freightAmount - allocatedFreightTotal
                        : Math.Round(
                            freightAmount * result.TaxableAmount / totalTaxExclusiveBase,
                            2,
                            MidpointRounding.AwayFromZero);

                    allocatedFreight = Math.Max(
                        0m,
                        Math.Round(
                            allocatedFreight,
                            2,
                            MidpointRounding.AwayFromZero));

                    allocatedFreightTotal = Math.Round(
                        allocatedFreightTotal + allocatedFreight,
                        2,
                        MidpointRounding.AwayFromZero);
                }

                decimal landedCost = source.Line.ReceivedQty > 0m
                    ? Math.Round(
                        (result.TaxableAmount + allocatedFreight) /
                        source.Line.ReceivedQty,
                        2,
                        MidpointRounding.AwayFromZero)
                    : 0m;

                previewLines.Add(new GrnTaxPreviewLineDto
                {
                    SourceIndex = source.Index,
                    ItemVariantId = source.Line.ItemVariantId,
                    TaxCategoryCode = result.TaxProfile.TaxCategoryCode,
                    TaxCategoryName = result.TaxProfile.TaxCategoryName,
                    TaxCode = result.TaxProfile.TaxCode,
                    VatRatePercent = result.TaxProfile.RatePercent,
                    GrossAmount = result.GrossAmount,
                    LineDiscountAmount = result.LineDiscountAmount,
                    GlobalDiscountAllocation = result.GlobalDiscountAllocation,
                    TaxableAmount = result.TaxableAmount,
                    VatAmount = result.VatAmount,
                    TaxInclusiveAmount = result.TaxInclusiveAmount,
                    LandedCost = landedCost
                });
            }

            return new GrnTaxPreviewDto
            {
                Lines = previewLines,
                Subtotal = calculation.Subtotal,
                LineDiscountTotal = calculation.LineDiscountTotal,
                GlobalDiscount = calculation.GlobalDiscount,
                TotalDiscount = calculation.TotalDiscount,
                TotalVat = calculation.TotalVat,
                FreightAmount = Math.Round(
                    freightAmount,
                    2,
                    MidpointRounding.AwayFromZero),
                NetPayable = Math.Round(
                    calculation.NetPayable + freightAmount,
                    2,
                    MidpointRounding.AwayFromZero),
                TaxableAmountTotal = calculation.TaxableAmountTotal,
                StandardRatedAmount = calculation.StandardRatedAmount,
                ZeroRatedAmount = calculation.ZeroRatedAmount,
                ExemptAmount = calculation.ExemptAmount,
                OutOfScopeAmount = calculation.OutOfScopeAmount
            };
        }

        public async Task<GrnBatchPriceContextDto> GetBatchPriceContextAsync(
            int itemVariantId,
            string? batchNo)
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            ItemVariant variant = await context.ItemVariants
                .AsNoTracking()
                .Include(row => row.ItemParent)
                .SingleOrDefaultAsync(row => row.Id == itemVariantId)
                ?? throw new InvalidOperationException("The selected item variant was not found.");

            string normalizedBatch = NormalizeText(batchNo).ToUpperInvariant();
            ItemBatch? batch = null;

            if (!string.IsNullOrWhiteSpace(normalizedBatch))
            {
                batch = await context.ItemBatches
                    .AsNoTracking()
                    .SingleOrDefaultAsync(row =>
                        row.ItemVariantId == itemVariantId &&
                        row.BatchNo == normalizedBatch &&
                        !row.IsDeactivated);
            }

            EffectiveSellingPrice effective = EffectiveSellingPriceResolver.Resolve(variant, batch);

            return new GrnBatchPriceContextDto
            {
                RetailPrice = effective.RetailPrice,
                WholesalePrice = effective.WholesalePrice,
                MinimumPrice = RoundMoney(variant.MinimumPrice),
                MaximumPrice = RoundMoney(variant.MaximumPrice),
                PriceSource = effective.PriceSource == SellingPriceSourceCodes.BatchOverride
                    ? "Batch Override"
                    : "Master Price"
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

            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                await ValidateLinesAsync(context, header, lines);
                await ApplyAuthoritativeTaxAsync(context, header, lines);
                await ValidateHeaderAsync(context, header);

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

                await ValidateGrnMasterPriceChangesAsync(
                    context,
                    lines,
                    variants);

                header.GrnNumber = await GenerateDocumentNumberAsync(context, "GRN");
                header.Status = "Posted";
                header.CreatedAt = now;
                header.UpdatedAt = now;
                header.PostedAt = now;

                if (string.IsNullOrWhiteSpace(header.CreatedBy) ||
                    string.IsNullOrWhiteSpace(header.PostedBy))
                {
                    throw new InvalidOperationException(
                        "An authenticated BackOffice username is required to post a GRN.");
                }

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

                string? priceChangeNo = lines.Any(line =>
                    GetSellingPriceAction(line) != GrnSellingPriceActionCodes.UseCurrentMasterPrice)
                    ? await GenerateDocumentNumberAsync(context, "PCH")
                    : null;

                if (!string.IsNullOrWhiteSpace(priceChangeNo))
                {
                    await CreateGrnMasterPriceChangeHistoryAsync(
                        context,
                        header,
                        lines,
                        variants,
                        priceChangeNo,
                        now);
                }

                ApplyProposedMasterPrices(lines, variants, now);

                var batchPriceBefore = new Dictionary<int, EffectiveSellingPrice>();

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

                    if (GetSellingPriceAction(line) == GrnSellingPriceActionCodes.SetBatchPriceOverride)
                    {
                        EffectiveSellingPrice before = EffectiveSellingPriceResolver.Resolve(variant, batch);
                        EffectiveSellingPriceResolver.ValidateOverride(
                            variant,
                            batch,
                            line.NewRetailPrice,
                            line.NewWholesalePrice);

                        batchPriceBefore[line.Id] = before;
                        batch.HasSellingPriceOverride = true;
                        batch.RetailPrice = RoundMoney(line.NewRetailPrice);
                        batch.WholesalePrice = RoundMoney(line.NewWholesalePrice);
                        batch.UpdatedAt = now;
                    }

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

                await SynchronizeUpdatedMasterBatchMirrorsAsync(
                    context,
                    lines,
                    variants,
                    now);

                if (!string.IsNullOrWhiteSpace(priceChangeNo))
                {
                    await CreateGrnBatchPriceChangeHistoryAsync(
                        context,
                        header,
                        lines,
                        variants,
                        batchPriceBefore,
                        priceChangeNo,
                        now);
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

        private static async Task<bool> GetSupplierVatRegistrationAsync(
            AppDbContext context,
            int supplierId)
        {
            bool? supplierIsVatRegistered = await context.Suppliers
                .Where(supplier => supplier.Id == supplierId)
                .Select(supplier => (bool?)supplier.HasVat)
                .SingleOrDefaultAsync();

            if (!supplierIsVatRegistered.HasValue)
            {
                throw new InvalidOperationException(
                    "The selected GRN supplier was not found.");
            }

            return supplierIsVatRegistered.Value;
        }

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

                if (!string.Equals(
                        variant.ItemParent.ItemType,
                        ItemTypeCodes.StockItem,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Service '{variant.SkuCode}' cannot be received through GRN.");
                }

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

                string priceAction = GetSellingPriceAction(line);

                if (priceAction == GrnSellingPriceActionCodes.UseCurrentMasterPrice)
                {
                    if (variant.RetailPrice <= 0m)
                    {
                        throw new InvalidOperationException(
                            $"Cannot receive item '{variant.SkuCode}' because its current master retail price is zero. Please update the price before posting.");
                    }
                }
                else if (priceAction == GrnSellingPriceActionCodes.UpdateMasterPrice)
                {
                    ValidateSellingPrices(line, variant.SkuCode);
                }
                else if (priceAction == GrnSellingPriceActionCodes.SetBatchPriceOverride)
                {
                    if (variant.ItemParent.HasBatchTracking != true)
                    {
                        throw new InvalidOperationException(
                            $"Batch-only pricing is not available for item '{variant.SkuCode}'.");
                    }

                    if (line.NewRetailPrice <= 0m)
                    {
                        throw new InvalidOperationException(
                            $"Batch override Retail price must be greater than zero for item '{variant.SkuCode}'.");
                    }

                    if (line.NewWholesalePrice < 0m)
                    {
                        throw new InvalidOperationException(
                            $"Batch override Wholesale price cannot be negative for item '{variant.SkuCode}'.");
                    }
                }

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
                            $"Cannot receive {QuantityDisplayFormatter.Format(line.ReceivedQty)} for item '{variant.SkuCode}'. PO ordered remaining quantity is {QuantityDisplayFormatter.Format(outstanding)}.");
                    }
                }
            }

            var inconsistentPriceGroup = lines
                .Where(line => GetSellingPriceAction(line) == GrnSellingPriceActionCodes.UpdateMasterPrice)
                .GroupBy(line => line.ItemVariantId)
                .FirstOrDefault(group => group
                    .Select(line => new
                    {
                        Retail = RoundMoney(line.NewRetailPrice),
                        Wholesale = RoundMoney(line.NewWholesalePrice),
                        Minimum = RoundMoney(line.NewMinimumPrice),
                        Maximum = RoundMoney(line.NewMaximumPrice)
                    })
                    .Distinct()
                    .Count() > 1);

            if (inconsistentPriceGroup != null &&
                variants.TryGetValue(inconsistentPriceGroup.Key, out var inconsistentVariant))
            {
                throw new InvalidOperationException(
                    $"All GRN rows for item '{inconsistentVariant.SkuCode}' must use the same proposed selling prices.");
            }

            var proposedMasterByVariant = lines
                .Where(line => GetSellingPriceAction(line) == GrnSellingPriceActionCodes.UpdateMasterPrice)
                .GroupBy(line => line.ItemVariantId)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (GrnLine batchLine in lines.Where(line =>
                         GetSellingPriceAction(line) == GrnSellingPriceActionCodes.SetBatchPriceOverride))
            {
                if (!variants.TryGetValue(batchLine.ItemVariantId, out ItemVariant? variant))
                    continue;

                decimal minimum = variant.MinimumPrice;
                decimal maximum = variant.MaximumPrice;

                if (proposedMasterByVariant.TryGetValue(batchLine.ItemVariantId, out GrnLine? masterLine))
                {
                    minimum = masterLine.NewMinimumPrice;
                    maximum = masterLine.NewMaximumPrice;
                }

                try
                {
                    EffectiveSellingPriceResolver.ValidateOverride(
                        batchLine.NewRetailPrice,
                        batchLine.NewWholesalePrice,
                        minimum,
                        maximum);
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException(
                        $"Invalid batch-price override for item '{variant.SkuCode}': {ex.Message}",
                        ex);
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

            var conflictingBatchOverride = lines
                .Where(line => GetSellingPriceAction(line) == GrnSellingPriceActionCodes.SetBatchPriceOverride)
                .GroupBy(line => new
                {
                    line.ItemVariantId,
                    BatchNo = NormalizeText(line.BatchNo).ToUpperInvariant()
                })
                .FirstOrDefault(group => group
                    .Select(line => new
                    {
                        Retail = RoundMoney(line.NewRetailPrice),
                        Wholesale = RoundMoney(line.NewWholesalePrice)
                    })
                    .Distinct()
                    .Count() > 1);

            if (conflictingBatchOverride != null)
            {
                throw new InvalidOperationException(
                    "Rows resolving to the same exact batch cannot use conflicting batch-price overrides.");
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

            bool wholesaleChanged = RoundMoney(line.CurrentWholesalePrice) != RoundMoney(line.NewWholesalePrice);

            if (line.NewRetailPrice <= 0m)
            {
                throw new InvalidOperationException(
                    $"New retail price must be greater than zero for item '{skuCode}'.");
            }

            // Allow wholesale to be zero for retail-only stores, but not negative.
            if (wholesaleChanged && line.NewWholesalePrice < 0m)
            {
                throw new InvalidOperationException(
                    $"New wholesale price cannot be negative for item '{skuCode}'.");
            }

            if (line.NewMaximumPrice > 0 &&
                line.NewMinimumPrice > line.NewMaximumPrice)
            {
                throw new InvalidOperationException(
                    $"Minimum price cannot be greater than maximum price for item '{skuCode}'.");
            }

            if (line.NewMinimumPrice > 0m &&
                (line.NewRetailPrice < line.NewMinimumPrice ||
                 (line.NewWholesalePrice > 0m && line.NewWholesalePrice < line.NewMinimumPrice)))
            {
                throw new InvalidOperationException(
                    $"Retail and Wholesale prices cannot be below the Minimum price for item '{skuCode}'.");
            }

            if (line.NewMaximumPrice > 0m &&
                (line.NewRetailPrice > line.NewMaximumPrice ||
                 line.NewWholesalePrice > line.NewMaximumPrice))
            {
                throw new InvalidOperationException(
                    $"Retail and Wholesale prices cannot be above the Maximum price for item '{skuCode}'.");
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

        private async Task ApplyAuthoritativeTaxAsync(
            AppDbContext context,
            GrnHeader header,
            List<GrnLine> lines)
        {
            bool supplierIsVatRegistered = await GetSupplierVatRegistrationAsync(
                context,
                header.SupplierId);

            bool documentIsTaxInclusive =
                supplierIsVatRegistered &&
                (header.IsTaxInclusive ?? ResolveDocumentTaxMode(lines));

            foreach (GrnLine line in lines)
                line.IsVatIncluded = documentIsTaxInclusive;

            header.IsTaxInclusive = documentIsTaxInclusive;

            var variantIds = lines
                .Select(line => line.ItemVariantId)
                .Distinct()
                .ToList();

            var profiles = await _purchasingTaxService.ResolveProfilesForSupplierAsync(
                context,
                variantIds,
                header.InvoiceDate,
                supplierIsVatRegistered);

            var calculation = _purchasingTaxService.CalculateDocument(
                lines.Select(line => new PurchasingTaxLineInput
                {
                    ItemVariantId = line.ItemVariantId,
                    Quantity = line.ReceivedQty,
                    UnitPrice = line.UnitCost,
                    DiscountMode = line.LineDiscountMode,
                    DiscountValue = line.LineDiscountValue,
                    TaxProfile = profiles[line.ItemVariantId]
                }).ToList(),
                header.GlobalBillDiscount,
                documentIsTaxInclusive);

            for (int index = 0; index < lines.Count; index++)
            {
                ApplyTaxResult(lines[index], calculation.Lines[index]);
            }

            header.Subtotal = calculation.Subtotal;
            header.TotalDiscountAmount = calculation.TotalDiscount;
            header.TotalVatAmount = calculation.TotalVat;
            header.NetPayable = Math.Round(
                calculation.NetPayable + header.FreightAmount,
                2,
                MidpointRounding.AwayFromZero);
            header.TaxableAmountTotal = calculation.TaxableAmountTotal;
            header.StandardRatedAmount = calculation.StandardRatedAmount;
            header.ZeroRatedAmount = calculation.ZeroRatedAmount;
            header.ExemptAmount = calculation.ExemptAmount;
            header.OutOfScopeAmount = calculation.OutOfScopeAmount;
            header.TaxSnapshotStatus = TaxSnapshotStatuses.Complete;

            // Freight VAT treatment is deliberately not invented in this phase.
            header.FreightTaxCategoryCodeSnapshot = null;
            header.FreightTaxableAmount = null;
            header.FreightVatAmount = null;
            header.FreightTaxSnapshotStatus = TaxSnapshotStatuses.LegacyUnknown;

            AllocateLandedCost(header, lines);
        }

        private static void ApplyTaxResult(
            GrnLine line,
            PurchasingTaxLineResult result)
        {
            var profile = result.TaxProfile;

            line.LineDiscount = result.LineDiscountAmount;
            line.VatRatePercent = profile.RatePercent;
            line.VatAmount = result.VatAmount;
            line.LineTotal = result.TaxInclusiveAmount;

            line.TaxCategoryId = profile.TaxCategoryId;
            line.TaxRateId = profile.TaxRateId;
            line.TaxCategoryCodeSnapshot = profile.TaxCategoryCode;
            line.TaxCodeSnapshot = profile.TaxCode;
            line.TaxNameSnapshot = profile.TaxName;
            line.TaxRatePercentSnapshot = profile.RatePercent;
            line.IsTaxInclusiveSnapshot = line.IsVatIncluded;
            line.TaxableAmountSnapshot = result.TaxableAmount;
            line.VatAmountSnapshot = result.VatAmount;
            line.TaxInclusiveAmountSnapshot = result.TaxInclusiveAmount;
            line.TaxSnapshotStatus = TaxSnapshotStatuses.Complete;
        }

        private static bool ResolveDocumentTaxMode(List<GrnLine> lines)
        {
            bool mode = lines[0].IsVatIncluded;

            if (lines.Any(line => line.IsVatIncluded != mode))
            {
                throw new InvalidOperationException(
                    "All GRN lines must use the same supplier-price VAT mode. Use either VAT Inclusive or VAT Exclusive for the whole document.");
            }

            foreach (var line in lines)
                line.IsVatIncluded = mode;

            return mode;
        }

        private static void AllocateLandedCost(
            GrnHeader header,
            List<GrnLine> lines)
        {
            var eligibleLines = lines
                .Where(line =>
                    line.ReceivedQty > 0m &&
                    (line.TaxableAmountSnapshot ?? 0m) > 0m)
                .ToList();

            decimal totalCostBase = eligibleLines.Sum(line =>
                line.TaxableAmountSnapshot ?? 0m);

            if (totalCostBase <= 0m)
            {
                foreach (var line in lines)
                    line.LandedCost = 0m;

                return;
            }

            decimal allocatedFreightTotal = 0m;

            foreach (var line in lines.Except(eligibleLines))
                line.LandedCost = 0m;

            for (int index = 0; index < eligibleLines.Count; index++)
            {
                var line = eligibleLines[index];
                decimal lineCostBase = line.TaxableAmountSnapshot ?? 0m;
                bool isLastEligible = index == eligibleLines.Count - 1;

                decimal allocatedFreight = isLastEligible
                    ? header.FreightAmount - allocatedFreightTotal
                    : Math.Round(
                        header.FreightAmount * lineCostBase / totalCostBase,
                        2,
                        MidpointRounding.AwayFromZero);

                allocatedFreight = Math.Max(
                    0m,
                    Math.Round(
                        allocatedFreight,
                        2,
                        MidpointRounding.AwayFromZero));

                allocatedFreightTotal = Math.Round(
                    allocatedFreightTotal + allocatedFreight,
                    2,
                    MidpointRounding.AwayFromZero);

                decimal landedLineTotal = lineCostBase + allocatedFreight;
                line.LandedCost = Math.Round(
                    landedLineTotal / line.ReceivedQty,
                    2,
                    MidpointRounding.AwayFromZero);
            }
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
                line.TaxCategory = null;
                line.TaxRate = null;
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
                    $"Cannot receive {QuantityDisplayFormatter.Format(line.ReceivedQty)} for item '{skuCode}'. PO ordered remaining quantity is {QuantityDisplayFormatter.Format(outstanding)}.");
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

        private static async Task CreateGrnMasterPriceChangeHistoryAsync(
            AppDbContext context,
            GrnHeader header,
            List<GrnLine> lines,
            IReadOnlyDictionary<int, ItemVariant> variants,
            string priceChangeNo,
            DateTime now)
        {
            var rows = new List<PriceChangeHistory>();

            foreach (IGrouping<int, GrnLine> group in lines
                         .Where(line => GetSellingPriceAction(line) == GrnSellingPriceActionCodes.UpdateMasterPrice)
                         .GroupBy(line => line.ItemVariantId))
            {
                if (!variants.TryGetValue(group.Key, out ItemVariant? variant))
                    throw new InvalidOperationException("One or more price-update variants were not found.");

                GrnLine sourceLine = group.OrderBy(line => line.Id).First();
                decimal oldRetail = RoundMoney(variant.RetailPrice);
                decimal oldWholesale = RoundMoney(variant.WholesalePrice);
                decimal oldMinimum = RoundMoney(variant.MinimumPrice);
                decimal oldMaximum = RoundMoney(variant.MaximumPrice);
                decimal newRetail = RoundMoney(sourceLine.NewRetailPrice);
                decimal newWholesale = RoundMoney(sourceLine.NewWholesalePrice);
                decimal newMinimum = RoundMoney(sourceLine.NewMinimumPrice);
                decimal newMaximum = RoundMoney(sourceLine.NewMaximumPrice);

                bool anyChanged =
                    oldRetail != newRetail ||
                    oldWholesale != newWholesale ||
                    oldMinimum != newMinimum ||
                    oldMaximum != newMaximum;

                foreach (GrnLine line in group)
                {
                    line.CurrentRetailPrice = oldRetail;
                    line.CurrentWholesalePrice = oldWholesale;
                    line.CurrentMinimumPrice = oldMinimum;
                    line.CurrentMaximumPrice = oldMaximum;
                    line.NewRetailPrice = newRetail;
                    line.NewWholesalePrice = newWholesale;
                    line.NewMinimumPrice = newMinimum;
                    line.NewMaximumPrice = newMaximum;
                    line.UpdateSellingPrices = anyChanged;
                }

                if (!anyChanged)
                    continue;

                rows.Add(new PriceChangeHistory
                {
                    PriceChangeNo = priceChangeNo,
                    PriceLevel = "Master",
                    ChangeSource = "GRN",
                    ChangeAction = PriceChangeActionCodes.MasterPriceUpdated,
                    OldPriceSource = SellingPriceSourceCodes.Master,
                    NewPriceSource = SellingPriceSourceCodes.Master,
                    ItemVariantId = variant.Id,
                    SourceDocumentType = "GRN",
                    SourceDocumentId = header.Id,
                    SourceDocumentLineId = sourceLine.Id,
                    SourceDocumentNo = header.GrnNumber,
                    ItemCode = variant.ItemParent.ItemCode,
                    SkuCode = variant.SkuCode,
                    Barcode = variant.Barcode ?? string.Empty,
                    ItemDescription = variant.ItemParent.ItemName,
                    VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                        ? "Standard"
                        : variant.VariantDescription,
                    EffectiveCost = RoundMoney(sourceLine.LandedCost),
                    OldMinimumPrice = oldMinimum,
                    NewMinimumPrice = newMinimum,
                    OldRetailPrice = oldRetail,
                    NewRetailPrice = newRetail,
                    OldWholesalePrice = oldWholesale,
                    NewWholesalePrice = newWholesale,
                    OldMaximumPrice = oldMaximum,
                    NewMaximumPrice = newMaximum,
                    ChangedBy = header.PostedBy,
                    ChangedAt = now,
                    ReasonCode = "GRN_MASTER_PRICE_UPDATE",
                    ChangeReason = $"Master price update from GRN {header.GrnNumber}",
                    Remarks = "Variant master pricing updated during GRN posting."
                });
            }

            if (rows.Count > 0)
                await context.PriceChangeHistories.AddRangeAsync(rows);
        }

        private static async Task CreateGrnBatchPriceChangeHistoryAsync(
            AppDbContext context,
            GrnHeader header,
            IReadOnlyCollection<GrnLine> lines,
            IReadOnlyDictionary<int, ItemVariant> variants,
            IReadOnlyDictionary<int, EffectiveSellingPrice> batchPriceBefore,
            string priceChangeNo,
            DateTime now)
        {
            var rows = new List<PriceChangeHistory>();

            foreach (GrnLine line in lines.Where(line =>
                         GetSellingPriceAction(line) == GrnSellingPriceActionCodes.SetBatchPriceOverride))
            {
                if (!variants.TryGetValue(line.ItemVariantId, out ItemVariant? variant) ||
                    line.ItemBatch == null ||
                    !batchPriceBefore.TryGetValue(line.Id, out EffectiveSellingPrice before))
                {
                    throw new InvalidOperationException(
                        "The GRN batch-price history could not resolve its exact batch identity.");
                }

                EffectiveSellingPrice after = EffectiveSellingPriceResolver.Resolve(variant, line.ItemBatch);
                string action = before.PriceSource == SellingPriceSourceCodes.BatchOverride
                    ? PriceChangeActionCodes.BatchOverrideUpdated
                    : PriceChangeActionCodes.BatchOverrideCreated;

                rows.Add(new PriceChangeHistory
                {
                    PriceChangeNo = priceChangeNo,
                    PriceLevel = "Batch",
                    ChangeSource = "GRN",
                    ChangeAction = action,
                    OldPriceSource = before.PriceSource,
                    NewPriceSource = after.PriceSource,
                    ItemVariantId = variant.Id,
                    ItemBatchId = line.ItemBatch.Id,
                    SourceDocumentType = "GRN",
                    SourceDocumentId = header.Id,
                    SourceDocumentLineId = line.Id,
                    SourceDocumentNo = header.GrnNumber,
                    ItemCode = variant.ItemParent.ItemCode,
                    SkuCode = variant.SkuCode,
                    Barcode = variant.Barcode ?? string.Empty,
                    ItemDescription = variant.ItemParent.ItemName,
                    VariantDescription = string.IsNullOrWhiteSpace(variant.VariantDescription)
                        ? "Standard"
                        : variant.VariantDescription,
                    BatchNo = line.ItemBatch.BatchNo,
                    BatchExpiryDate = line.ItemBatch.ExpiryDate,
                    EffectiveCost = RoundMoney(line.LandedCost),
                    OldMinimumPrice = RoundMoney(variant.MinimumPrice),
                    NewMinimumPrice = RoundMoney(variant.MinimumPrice),
                    OldRetailPrice = before.RetailPrice,
                    NewRetailPrice = after.RetailPrice,
                    OldWholesalePrice = before.WholesalePrice,
                    NewWholesalePrice = after.WholesalePrice,
                    OldMaximumPrice = RoundMoney(variant.MaximumPrice),
                    NewMaximumPrice = RoundMoney(variant.MaximumPrice),
                    ChangedBy = header.PostedBy,
                    ChangedAt = now,
                    ReasonCode = action == PriceChangeActionCodes.BatchOverrideCreated
                        ? "GRN_BATCH_OVERRIDE_CREATED"
                        : "GRN_BATCH_OVERRIDE_UPDATED",
                    ChangeReason = $"Batch price override from GRN {header.GrnNumber}",
                    Remarks = $"Exact batch {line.ItemBatch.BatchNo} received a batch selling-price override."
                });
            }

            if (rows.Count > 0)
                await context.PriceChangeHistories.AddRangeAsync(rows);
        }

        private static async Task ValidateGrnMasterPriceChangesAsync(
            AppDbContext context,
            IReadOnlyCollection<GrnLine> lines,
            IReadOnlyDictionary<int, ItemVariant> variants)
        {
            foreach (IGrouping<int, GrnLine> group in lines
                         .Where(line => GetSellingPriceAction(line) == GrnSellingPriceActionCodes.UpdateMasterPrice)
                         .GroupBy(line => line.ItemVariantId))
            {
                if (!variants.TryGetValue(group.Key, out ItemVariant? variant))
                    throw new InvalidOperationException("One or more price-update variants were not found.");

                GrnLine sourceLine = group.First();

                List<ItemBatch> activeBatches = await context.ItemBatches
                    .Where(batch =>
                        batch.ItemVariantId == variant.Id &&
                        !batch.IsDeactivated)
                    .ToListAsync();

                EffectiveSellingPriceResolver.ValidateActiveOverridesAgainstMasterBounds(
                    variant,
                    activeBatches,
                    sourceLine.NewMinimumPrice,
                    sourceLine.NewMaximumPrice);
            }
        }

        private static async Task SynchronizeUpdatedMasterBatchMirrorsAsync(
            AppDbContext context,
            IReadOnlyCollection<GrnLine> lines,
            IReadOnlyDictionary<int, ItemVariant> variants,
            DateTime now)
        {
            int[] updatedVariantIds = lines
                .Where(line => GetSellingPriceAction(line) == GrnSellingPriceActionCodes.UpdateMasterPrice)
                .Select(line => line.ItemVariantId)
                .Distinct()
                .ToArray();

            if (updatedVariantIds.Length == 0)
                return;

            List<ItemBatch> batches = await context.ItemBatches
                .Where(batch =>
                    updatedVariantIds.Contains(batch.ItemVariantId) &&
                    !batch.IsDeactivated)
                .ToListAsync();

            foreach (ItemBatch batch in batches)
            {
                if (!variants.TryGetValue(batch.ItemVariantId, out ItemVariant? variant))
                    continue;

                EffectiveSellingPriceResolver.SynchronizeMasterMirror(
                    variant,
                    batch,
                    now);
            }
        }

        private static decimal ResolveMasterWholesalePrice(
            ItemVariant variant)
        {
            decimal wholesale = Math.Round(variant.WholesalePrice, 2);
            return wholesale > 0m
                ? wholesale
                : Math.Round(variant.RetailPrice, 2);
        }

        private static void ApplyProposedMasterPrices(
            IReadOnlyCollection<GrnLine> lines,
            IReadOnlyDictionary<int, ItemVariant> variants,
            DateTime now)
        {
            foreach (IGrouping<int, GrnLine> group in lines
                         .Where(line => GetSellingPriceAction(line) == GrnSellingPriceActionCodes.UpdateMasterPrice)
                         .GroupBy(line => line.ItemVariantId))
            {
                if (!variants.TryGetValue(group.Key, out ItemVariant? variant))
                    throw new InvalidOperationException("One or more master-price variants were not found.");

                GrnLine line = group.First();
                variant.RetailPrice = RoundMoney(line.NewRetailPrice);
                variant.WholesalePrice = RoundMoney(line.NewWholesalePrice);
                variant.MinimumPrice = RoundMoney(line.NewMinimumPrice);
                variant.MaximumPrice = RoundMoney(line.NewMaximumPrice);
                variant.UpdatedAt = now;
            }
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

            bool hasBatchTracking = variant.ItemParent?.HasBatchTracking == true;

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
                    RetailPrice = Math.Round(variant.RetailPrice, 2),
                    WholesalePrice = ResolveMasterWholesalePrice(variant),
                    HasSellingPriceOverride = false,
                    CurrentStock = line.ReceivedQty,
                    InternalBatchBarcode = string.Empty,
                    BarcodePrintedCount = 0,
                    LastBarcodePrintedAt = null,
                    LastBarcodePrintedBy = string.Empty,
                    IsDeactivated = false,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                await context.ItemBatches.AddAsync(batch);

                // Need the database-generated ItemBatch.Id before creating B0000000001 style barcode.
                await context.SaveChangesAsync();

                if (hasBatchTracking)
                {
                    batch.InternalBatchBarcode = BuildInternalBatchBarcode(batch.Id);
                    batch.UpdatedAt = now;
                }
                else
                {
                    // Average-cost GENERAL stock bucket must not get a cashier GRN barcode.
                    batch.InternalBatchBarcode = string.Empty;
                }

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

            if (hasBatchTracking && string.IsNullOrWhiteSpace(batch.InternalBatchBarcode))
                batch.InternalBatchBarcode = BuildInternalBatchBarcode(batch.Id);

            if (!hasBatchTracking)
                batch.InternalBatchBarcode = string.Empty;

            batch.CurrentStock += line.ReceivedQty;
            batch.CostPrice = line.LandedCost;

            EffectiveSellingPriceResolver.SynchronizeMasterMirror(
                variant,
                batch,
                now);

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

        private static string BuildInternalBatchBarcode(int itemBatchId)
        {
            if (itemBatchId <= 0)
                throw new InvalidOperationException("Cannot create batch barcode before item batch is saved.");

            return $"B{itemBatchId.ToString().PadLeft(10, '0')}";
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

            if (string.IsNullOrWhiteSpace(header.CreatedBy) ||
                string.IsNullOrWhiteSpace(header.PostedBy))
            {
                throw new InvalidOperationException(
                    "An authenticated BackOffice username is required to post a GRN.");
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
            line.LineDiscountMode = NormalizeDiscountMode(line.LineDiscountMode);
            line.SellingPriceAction = GrnSellingPriceActionCodes.Normalize(
                line.SellingPriceAction,
                line.UpdateSellingPrices);
            line.UpdateSellingPrices =
                line.SellingPriceAction == GrnSellingPriceActionCodes.UpdateMasterPrice;

            if (string.IsNullOrWhiteSpace(line.Uom))
                line.Uom = "PCS";

            if (line.LineDiscountValue <= 0 &&
                line.LineDiscount > 0 &&
                line.LineDiscountMode == "Amount")
            {
                line.LineDiscountValue = line.LineDiscount;
            }
        }

        private static string GetSellingPriceAction(GrnLine line)
        {
            return GrnSellingPriceActionCodes.Normalize(
                line.SellingPriceAction,
                line.UpdateSellingPrices);
        }

        private static int NormalizeTakeLimit(int take)
        {
            if (take <= 0)
                return DefaultTakeLimit;

            if (take > MaxTakeLimit)
                return MaxTakeLimit;

            return take;
        }

        private static decimal RoundMoney(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
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