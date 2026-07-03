using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public class GrnSummaryDto
    {
        public int GrnHeaderId { get; set; }

        public string GrnNumber { get; set; } = string.Empty;

        public int SupplierId { get; set; }

        public string SupplierName { get; set; } = string.Empty;

        public string SupplierInvoiceNo { get; set; } = string.Empty;

        public string PurchaseOrderNo { get; set; } = string.Empty;

        public DateTime InvoiceDate { get; set; }

        public DateTime ReceivedDate { get; set; }

        public DateTime DueDate { get; set; }

        public decimal Subtotal { get; set; }

        public decimal TotalDiscountAmount { get; set; }

        public decimal TotalVatAmount { get; set; }

        public decimal FreightAmount { get; set; }

        public decimal NetPayable { get; set; }

        public decimal TotalReceivedQty { get; set; }

        public int LineCount { get; set; }

        public string Status { get; set; } = string.Empty;

        public string CreatedBy { get; set; } = string.Empty;

        public string PostedBy { get; set; } = string.Empty;

        public string DisplayText =>
            $"{GrnNumber} | {SupplierName} | Invoice: {SupplierInvoiceNo} | Rs. {NetPayable:N2}";
    }

    public class GrnHistoryLineDto
    {
        public int GrnLineId { get; set; }

        public int ItemVariantId { get; set; }

        public string ItemCode { get; set; } = string.Empty;

        public string SkuCode { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string PrintName { get; set; } = string.Empty;

        public string VariantDescription { get; set; } = string.Empty;

        public string BatchNo { get; set; } = string.Empty;

        public DateTime? ExpiryDate { get; set; }

        public string Uom { get; set; } = string.Empty;

        public decimal OrderedQty { get; set; }

        public decimal ReceivedQty { get; set; }

        public decimal UnitCost { get; set; }

        public string LineDiscountMode { get; set; } = "Amount";

        public decimal LineDiscountValue { get; set; }

        public decimal LineDiscount { get; set; }

        public decimal VatRatePercent { get; set; }

        public bool IsVatIncluded { get; set; }

        public decimal VatAmount { get; set; }

        public decimal LandedCost { get; set; }

        public decimal LineTotal { get; set; }

        public bool UpdateSellingPrices { get; set; }

        public decimal CurrentRetailPrice { get; set; }

        public decimal NewRetailPrice { get; set; }

        public decimal CurrentWholesalePrice { get; set; }

        public decimal NewWholesalePrice { get; set; }

        public decimal CurrentMinimumPrice { get; set; }

        public decimal NewMinimumPrice { get; set; }

        public decimal CurrentMaximumPrice { get; set; }

        public decimal NewMaximumPrice { get; set; }

        public decimal RetailMarkupPercent { get; set; }

        public decimal WholesaleMarkupPercent { get; set; }

        public string LineStatus { get; set; } = string.Empty;

        public string PoNumber { get; set; } = string.Empty;

        public string FullDisplayName =>
            GrnHistoryDisplayNameHelper.BuildDisplayName(Description, VariantDescription, SkuCode);

        public string ReceiptDisplayName =>
            GrnHistoryDisplayNameHelper.BuildDisplayName(
                string.IsNullOrWhiteSpace(PrintName) ? Description : PrintName,
                VariantDescription,
                FullDisplayName);

        public string DisplayName => FullDisplayName;

        public string BatchDisplayText =>
            string.IsNullOrWhiteSpace(BatchNo) ? "[AUTO]" : BatchNo.Trim();

        public string ExpiryDisplayText =>
            ExpiryDate.HasValue ? ExpiryDate.Value.ToString("yyyy-MM-dd") : "No Expiry";

        public string VatDisplayText
        {
            get
            {
                if (VatRatePercent <= 0)
                    return "No VAT";

                return IsVatIncluded
                    ? $"VAT {VatRatePercent:N2}% Included"
                    : $"VAT {VatRatePercent:N2}% Added";
            }
        }
    }

    public class GrnDetailDto
    {
        public int GrnHeaderId { get; set; }

        public string GrnNumber { get; set; } = string.Empty;

        public int? PurchaseOrderId { get; set; }

        public string PurchaseOrderNo { get; set; } = string.Empty;

        public int SupplierId { get; set; }

        public string SupplierCode { get; set; } = string.Empty;

        public string SupplierName { get; set; } = string.Empty;

        public string ContactPerson { get; set; } = string.Empty;

        public string SupplierInvoiceNo { get; set; } = string.Empty;

        public DateTime InvoiceDate { get; set; }

        public DateTime ReceivedDate { get; set; }

        public DateTime DueDate { get; set; }

        public int CreditDays { get; set; }

        public string Remarks { get; set; } = string.Empty;

        public decimal Subtotal { get; set; }

        public decimal GlobalBillDiscount { get; set; }

        public decimal FreightAmount { get; set; }

        public decimal TotalDiscountAmount { get; set; }

        public decimal TotalVatAmount { get; set; }

        public decimal NetPayable { get; set; }

        public string Status { get; set; } = string.Empty;

        public string CreatedBy { get; set; } = string.Empty;

        public string PostedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? PostedAt { get; set; }

        public string CancelledBy { get; set; } = string.Empty;

        public string CancellationReason { get; set; } = string.Empty;

        public DateTime? CancelledAt { get; set; }

        public List<GrnHistoryLineDto> Lines { get; set; } = new();

        public int LineCount => Lines.Count;

        public decimal TotalReceivedQty => Lines.Sum(l => l.ReceivedQty);
    }

    public class GrnHistoryRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public GrnHistoryRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<IEnumerable<GrnSummaryDto>> GetGrnSummariesAsync(
            string searchTerm = "",
            int? supplierId = null,
            string statusFilter = "All",
            DateTime? startDate = null,
            DateTime? endDate = null,
            bool showCancelled = false)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.GrnHeaders
                .AsNoTracking()
                .AsQueryable();

            if (!showCancelled)
                query = query.Where(g => g.Status != "Cancelled");

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string term = searchTerm.Trim();

                query = query.Where(g =>
                    EF.Functions.Like(g.GrnNumber, $"%{term}%") ||
                    EF.Functions.Like(g.SupplierInvoiceNo, $"%{term}%") ||
                    EF.Functions.Like(g.Supplier.SupplierName, $"%{term}%") ||
                    EF.Functions.Like(g.Supplier.SupplierCode, $"%{term}%") ||
                    (g.PurchaseOrder != null &&
                     EF.Functions.Like(g.PurchaseOrder.PoNumber, $"%{term}%")));
            }

            if (supplierId.HasValue && supplierId.Value > 0)
                query = query.Where(g => g.SupplierId == supplierId.Value);

            if (!string.IsNullOrWhiteSpace(statusFilter) &&
                !statusFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(g => g.Status == statusFilter);
            }

            if (startDate.HasValue)
                query = query.Where(g => g.ReceivedDate >= startDate.Value.Date);

            if (endDate.HasValue)
            {
                DateTime endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(g => g.ReceivedDate <= endOfDay);
            }

            var headerRows = await query
                .OrderByDescending(g => g.ReceivedDate)
                .ThenByDescending(g => g.Id)
                .Select(g => new
                {
                    g.Id,
                    g.GrnNumber,
                    g.SupplierId,
                    SupplierName = g.Supplier.SupplierName,
                    g.SupplierInvoiceNo,
                    PurchaseOrderNo = g.PurchaseOrder == null
                        ? string.Empty
                        : g.PurchaseOrder.PoNumber,
                    g.InvoiceDate,
                    g.ReceivedDate,
                    g.DueDate,
                    g.Subtotal,
                    g.TotalDiscountAmount,
                    g.TotalVatAmount,
                    g.FreightAmount,
                    g.NetPayable,
                    g.Status,
                    g.CreatedBy,
                    g.PostedBy
                })
                .Take(500)
                .ToListAsync();

            if (!headerRows.Any())
                return new List<GrnSummaryDto>();

            var grnHeaderIds = headerRows
                .Select(g => g.Id)
                .ToList();

            var lineRows = await context.GrnLines
                .AsNoTracking()
                .Where(l => grnHeaderIds.Contains(l.GrnHeaderId))
                .Select(l => new
                {
                    l.GrnHeaderId,
                    l.ReceivedQty
                })
                .ToListAsync();

            var lineStatsByHeaderId = lineRows
                .GroupBy(l => l.GrnHeaderId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        LineCount = g.Count(),
                        TotalReceivedQty = g.Sum(x => x.ReceivedQty)
                    });

            return headerRows.Select(g =>
            {
                lineStatsByHeaderId.TryGetValue(
                    g.Id,
                    out var lineStats);

                return new GrnSummaryDto
                {
                    GrnHeaderId = g.Id,
                    GrnNumber = g.GrnNumber,
                    SupplierId = g.SupplierId,
                    SupplierName = g.SupplierName,
                    SupplierInvoiceNo = g.SupplierInvoiceNo,
                    PurchaseOrderNo = g.PurchaseOrderNo,
                    InvoiceDate = g.InvoiceDate,
                    ReceivedDate = g.ReceivedDate,
                    DueDate = g.DueDate,
                    Subtotal = g.Subtotal,
                    TotalDiscountAmount = g.TotalDiscountAmount,
                    TotalVatAmount = g.TotalVatAmount,
                    FreightAmount = g.FreightAmount,
                    NetPayable = g.NetPayable,
                    Status = g.Status,
                    CreatedBy = g.CreatedBy,
                    PostedBy = g.PostedBy,
                    LineCount = lineStats?.LineCount ?? 0,
                    TotalReceivedQty = lineStats?.TotalReceivedQty ?? 0m
                };
            }).ToList();
        }

        public async Task<GrnDetailDto?> GetGrnDetailsAsync(int grnHeaderId)
        {
            if (grnHeaderId <= 0)
                return null;

            await using var context = await _contextFactory.CreateDbContextAsync();

            var header = await context.GrnHeaders
                .AsNoTracking()
                .Where(g => g.Id == grnHeaderId)
                .Select(g => new GrnDetailDto
                {
                    GrnHeaderId = g.Id,
                    GrnNumber = g.GrnNumber,
                    PurchaseOrderId = g.PurchaseOrderId,
                    PurchaseOrderNo = g.PurchaseOrder == null
                        ? string.Empty
                        : g.PurchaseOrder.PoNumber,
                    SupplierId = g.SupplierId,
                    SupplierCode = g.Supplier.SupplierCode,
                    SupplierName = g.Supplier.SupplierName,
                    ContactPerson = g.Supplier.ContactPerson,
                    SupplierInvoiceNo = g.SupplierInvoiceNo,
                    InvoiceDate = g.InvoiceDate,
                    ReceivedDate = g.ReceivedDate,
                    DueDate = g.DueDate,
                    CreditDays = g.CreditDays,
                    Remarks = g.Remarks,
                    Subtotal = g.Subtotal,
                    GlobalBillDiscount = g.GlobalBillDiscount,
                    FreightAmount = g.FreightAmount,
                    TotalDiscountAmount = g.TotalDiscountAmount,
                    TotalVatAmount = g.TotalVatAmount,
                    NetPayable = g.NetPayable,
                    Status = g.Status,
                    CreatedBy = g.CreatedBy,
                    PostedBy = g.PostedBy,
                    CreatedAt = g.CreatedAt,
                    PostedAt = g.PostedAt,
                    CancelledBy = g.CancelledBy,
                    CancellationReason = g.CancellationReason,
                    CancelledAt = g.CancelledAt
                })
                .FirstOrDefaultAsync();

            if (header == null)
                return null;

            var lines = await context.GrnLines
                .AsNoTracking()
                .Where(l => l.GrnHeaderId == grnHeaderId)
                .OrderBy(l => l.ItemVariant.ItemParent.ItemCode)
                .ThenBy(l => l.ItemVariant.VariantDescription)
                .Select(l => new GrnHistoryLineDto
                {
                    GrnLineId = l.Id,
                    ItemVariantId = l.ItemVariantId,
                    ItemCode = l.ItemVariant.ItemParent.ItemCode,
                    SkuCode = l.ItemVariant.SkuCode,
                    Barcode = l.ItemVariant.Barcode ?? string.Empty,
                    Description = l.ItemVariant.ItemParent.ItemName,
                    PrintName = l.ItemVariant.ItemParent.PrintName,
                    VariantDescription = string.IsNullOrWhiteSpace(l.ItemVariant.VariantDescription)
                        ? "Standard"
                        : l.ItemVariant.VariantDescription,
                    BatchNo = l.BatchNo,
                    ExpiryDate = l.ExpiryDate,
                    Uom = l.Uom,
                    OrderedQty = l.OrderedQty,
                    ReceivedQty = l.ReceivedQty,
                    UnitCost = l.UnitCost,
                    LineDiscountMode = l.LineDiscountMode,
                    LineDiscountValue = l.LineDiscountValue,
                    LineDiscount = l.LineDiscount,
                    VatRatePercent = l.VatRatePercent,
                    IsVatIncluded = l.IsVatIncluded,
                    VatAmount = l.VatAmount,
                    LandedCost = l.LandedCost,
                    LineTotal = l.LineTotal,
                    UpdateSellingPrices = l.UpdateSellingPrices,
                    CurrentRetailPrice = l.CurrentRetailPrice,
                    NewRetailPrice = l.NewRetailPrice,
                    CurrentWholesalePrice = l.CurrentWholesalePrice,
                    NewWholesalePrice = l.NewWholesalePrice,
                    CurrentMinimumPrice = l.CurrentMinimumPrice,
                    NewMinimumPrice = l.NewMinimumPrice,
                    CurrentMaximumPrice = l.CurrentMaximumPrice,
                    NewMaximumPrice = l.NewMaximumPrice,
                    RetailMarkupPercent = l.RetailMarkupPercent,
                    WholesaleMarkupPercent = l.WholesaleMarkupPercent,
                    LineStatus = l.LineStatus,
                    PoNumber = l.PoLine == null
                        ? string.Empty
                        : l.PoLine.PoHeader.PoNumber
                })
                .ToListAsync();

            header.Lines = lines;

            return header;
        }
    }

    internal static class GrnHistoryDisplayNameHelper
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