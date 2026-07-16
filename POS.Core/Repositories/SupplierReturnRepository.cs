using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Services.Returns;

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
                string main = string.IsNullOrWhiteSpace(code) ? name : $"{code} - {name}";

                if (!string.IsNullOrWhiteSpace(company) &&
                    !company.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{main} ({company})";
                }

                return main;
            }
        }

        private static string NormalizeText(string? value) => (value ?? string.Empty).Trim();
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
        public string BatchNo { get; set; } = string.Empty;
        public string InternalBatchBarcode { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public decimal ReceivedQty { get; set; }
        public decimal AlreadyReturnedQty { get; set; }
        public decimal CurrentBatchStock { get; set; }
        public decimal MaxReturnQty { get; set; }
        public decimal HistoricalCost { get; set; }
        public decimal OriginalCreditAmount { get; set; }
        public decimal PreviouslyReturnedCreditAmount { get; set; }
        public decimal MaxReturnCredit { get; set; }
        public decimal CreditUnitValue { get; set; }
        public string TaxCategoryCode { get; set; } = string.Empty;
        public string TaxName { get; set; } = string.Empty;
        public decimal? TaxRatePercent { get; set; }
        public string TaxSnapshotStatus { get; set; } = TaxSnapshotStatuses.LegacyUnknown;

        public string TrackingText => !HasBatchTracking
            ? "Average Cost"
            : HasExpiryTracking ? "Batch + Expiry" : "Batch";

        public string BatchDisplayText => IsGeneralStockBucket ? "GENERAL" : BatchNo;
        public string BatchBarcodeDisplayText => IsGeneralStockBucket ? "-" : InternalBatchBarcode;
        public decimal CreditValue => MaxReturnCredit;
        public string TaxDisplayText =>
            string.Equals(TaxSnapshotStatus, TaxSnapshotStatuses.Complete, StringComparison.Ordinal)
                ? $"{TaxCategoryCode} {(TaxRatePercent ?? 0m):0.####}%"
                : "Legacy / Unknown";
    }

    public class SupplierReturnRepository
    {
        private const string GeneralBatchNo = "GENERAL";

        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly SupplierReturnAllocationCalculator _allocationCalculator;

        public SupplierReturnRepository(
            IDbContextFactory<AppDbContext> contextFactory,
            SupplierReturnAllocationCalculator allocationCalculator)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _allocationCalculator = allocationCalculator ?? throw new ArgumentNullException(nameof(allocationCalculator));
        }

        public async Task<List<SupplierLookupDto>> GetActiveSuppliersAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Suppliers
                .AsNoTracking()
                .Where(s => context.GrnHeaders.Any(g =>
                    g.SupplierId == s.Id &&
                    g.Status == SupplierReturnCodes.PostedStatus))
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

            List<GrnHeader> grns = await context.GrnHeaders
                .AsNoTracking()
                .Where(g =>
                    g.SupplierId == supplierId &&
                    g.Status == SupplierReturnCodes.PostedStatus)
                .OrderByDescending(g => g.ReceivedDate)
                .ThenByDescending(g => g.GrnNumber)
                .Take(300)
                .ToListAsync();

            if (grns.Count == 0)
                return new List<SupplierInvoiceLookupDto>();

            int[] grnIds = grns.Select(g => g.Id).ToArray();

            List<GrnLine> grnLines = await context.GrnLines
                .Include(l => l.ItemVariant)
                    .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .Where(l =>
                    grnIds.Contains(l.GrnHeaderId) &&
                    l.ReceivedQty > 0m &&
                    l.ItemBatchId.HasValue &&
                    l.ItemVariant.ItemParent.ItemType == ItemTypeCodes.StockItem)
                .ToListAsync();

            if (grnLines.Count == 0)
                return new List<SupplierInvoiceLookupDto>();

            int[] lineIds = grnLines.Select(l => l.Id).ToArray();
            int[] batchIds = grnLines.Select(l => l.ItemBatchId!.Value).Distinct().ToArray();

            Dictionary<int, PreviousReturnAggregate> prior =
                await LoadPriorReturnAggregatesAsync(context, lineIds);

            Dictionary<int, ItemBatch> batches = await context.ItemBatches
                .Include(b => b.ItemVariant)
                    .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .Where(b => batchIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id);

            var result = new List<SupplierInvoiceLookupDto>();

            foreach (GrnHeader grn in grns)
            {
                int count = 0;
                decimal quantity = 0m;

                foreach (GrnLine line in grnLines.Where(l => l.GrnHeaderId == grn.Id))
                {
                    if (!line.ItemBatchId.HasValue ||
                        !batches.TryGetValue(line.ItemBatchId.Value, out ItemBatch? batch) ||
                        !IsValidStockBucketForItem(batch))
                    {
                        continue;
                    }

                    decimal returned = prior.TryGetValue(line.Id, out PreviousReturnAggregate? aggregate)
                        ? aggregate.Quantity
                        : 0m;

                    decimal remaining = Math.Max(0m, line.ReceivedQty - returned);
                    decimal returnable = Math.Min(remaining, batch.CurrentStock);

                    if (returnable <= 0m)
                        continue;

                    count++;
                    quantity += returnable;
                }

                if (count == 0)
                    continue;

                result.Add(new SupplierInvoiceLookupDto
                {
                    Id = grn.Id,
                    GrnNumber = grn.GrnNumber,
                    SupplierInvoiceNo = grn.SupplierInvoiceNo,
                    InvoiceDate = grn.InvoiceDate,
                    ReceivedDate = grn.ReceivedDate,
                    NetPayable = grn.NetPayable,
                    ReturnableLineCount = count,
                    ReturnableQty = quantity
                });
            }

            return result
                .OrderByDescending(row => row.ReceivedDate)
                .ThenByDescending(row => row.GrnNumber)
                .Take(100)
                .ToList();
        }

        public async Task<List<SupplierReturnSourceDto>> GetReturnableBatchesForGrnAsync(int grnHeaderId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            GrnHeader? grn = await context.GrnHeaders
                .AsNoTracking()
                .FirstOrDefaultAsync(g =>
                    g.Id == grnHeaderId &&
                    g.Status == SupplierReturnCodes.PostedStatus);

            if (grn == null)
                throw new InvalidOperationException("Posted GRN was not found.");

            List<GrnLine> grnLines = await context.GrnLines
                .Include(l => l.ItemVariant)
                    .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .Where(l =>
                    l.GrnHeaderId == grnHeaderId &&
                    l.ReceivedQty > 0m &&
                    l.ItemBatchId.HasValue &&
                    l.ItemVariant.ItemParent.ItemType == ItemTypeCodes.StockItem)
                .OrderBy(l => l.ItemVariant.ItemParent.ItemCode)
                .ThenBy(l => l.ItemVariant.VariantDescription)
                .ToListAsync();

            if (grnLines.Count == 0)
                return new List<SupplierReturnSourceDto>();

            int[] batchIds = grnLines.Select(l => l.ItemBatchId!.Value).Distinct().ToArray();
            int[] lineIds = grnLines.Select(l => l.Id).ToArray();

            Dictionary<int, ItemBatch> batches = await context.ItemBatches
                .Include(b => b.ItemVariant)
                    .ThenInclude(v => v.ItemParent)
                .AsNoTracking()
                .Where(b => batchIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id);

            Dictionary<int, PreviousReturnAggregate> prior =
                await LoadPriorReturnAggregatesAsync(context, lineIds);

            var result = new List<SupplierReturnSourceDto>();

            foreach (GrnLine line in grnLines)
            {
                if (!line.ItemBatchId.HasValue ||
                    !batches.TryGetValue(line.ItemBatchId.Value, out ItemBatch? batch) ||
                    !IsValidStockBucketForItem(batch))
                {
                    continue;
                }

                PreviousReturnAggregate previous = prior.TryGetValue(line.Id, out PreviousReturnAggregate? found)
                    ? found
                    : new PreviousReturnAggregate();

                decimal remainingQuantity = Math.Max(0m, line.ReceivedQty - previous.Quantity);
                decimal maxReturnQuantity = Math.Min(remainingQuantity, batch.CurrentStock);

                if (maxReturnQuantity <= 0m)
                    continue;

                decimal historicalCost = ResolveHistoricalCost(line, batch, line.ItemVariant);
                decimal originalCredit = ResolveOriginalCreditAmount(line);

                SupplierReturnAllocationResult allocation = _allocationCalculator.Calculate(
                    BuildAllocationInput(line, previous, maxReturnQuantity, originalCredit));

                bool isGeneral = IsGeneralBatch(batch.BatchNo);
                ItemParent parent = batch.ItemVariant.ItemParent;

                result.Add(new SupplierReturnSourceDto
                {
                    GrnHeaderId = grn.Id,
                    GrnLineId = line.Id,
                    ItemVariantId = line.ItemVariantId,
                    ItemBatchId = batch.Id,
                    GrnNumber = grn.GrnNumber,
                    SupplierInvoiceNo = grn.SupplierInvoiceNo,
                    ItemCode = parent.ItemCode,
                    Description = parent.ItemName,
                    VariantDescription = string.IsNullOrWhiteSpace(batch.ItemVariant.VariantDescription)
                        ? "Standard"
                        : batch.ItemVariant.VariantDescription,
                    HasBatchTracking = parent.HasBatchTracking,
                    HasExpiryTracking = parent.HasExpiryTracking || parent.HasBatchExpiry,
                    IsGeneralStockBucket = isGeneral,
                    BatchNo = batch.BatchNo,
                    InternalBatchBarcode = batch.InternalBatchBarcode ?? string.Empty,
                    ExpiryDate = batch.ExpiryDate,
                    ReceivedQty = line.ReceivedQty,
                    AlreadyReturnedQty = previous.Quantity,
                    CurrentBatchStock = batch.CurrentStock,
                    MaxReturnQty = maxReturnQuantity,
                    HistoricalCost = Money(historicalCost),
                    OriginalCreditAmount = Money(originalCredit),
                    PreviouslyReturnedCreditAmount = Money(previous.CreditAmount),
                    MaxReturnCredit = allocation.CreditAmount,
                    CreditUnitValue = line.ReceivedQty > 0m
                        ? Money(originalCredit / line.ReceivedQty)
                        : 0m,
                    TaxCategoryCode = NormalizeText(line.TaxCategoryCodeSnapshot),
                    TaxName = NormalizeText(line.TaxNameSnapshot),
                    TaxRatePercent = line.TaxRatePercentSnapshot,
                    TaxSnapshotStatus = allocation.TaxSnapshotStatus
                });
            }

            return result
                .OrderBy(row => row.ItemCode)
                .ThenBy(row => row.VariantDescription)
                .ThenBy(row => row.IsGeneralStockBucket ? 0 : 1)
                .ThenBy(row => row.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(row => row.BatchNo)
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

        public async Task<SupplierReturnPostResult> PostSupplierReturnAsync(
            SupplierReturnHeader header,
            List<SupplierReturnLine> lines)
        {
            if (header == null)
                throw new ArgumentNullException(nameof(header));

            if (lines == null || lines.Count == 0)
                throw new InvalidOperationException("Supplier return must contain at least one line.");

            NormalizeHeader(header);
            NormalizeLines(lines);
            ValidateSubmittedLineDuplicates(lines);

            using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                GrnHeader sourceGrn = await ValidateAndLoadHeaderSourceAsync(context, header);
                await ValidateAndCalculateLinesAsync(context, header, lines);
                RecalculateHeaderTotals(header, lines);
                ValidateCalculatedHeader(header);

                DateTime now = DateTime.Now;
                header.Id = 0;
                header.ReturnNumber = await GenerateDocumentNumberAsync(
                    context,
                    SupplierReturnCodes.DocumentSequenceType,
                    SupplierReturnCodes.DocumentPrefix);
                header.OriginalInvoiceNo = sourceGrn.SupplierInvoiceNo;
                header.Status = SupplierReturnCodes.PostedStatus;
                header.RestockingFee = 0m;
                header.CreatedAt = now;
                header.UpdatedAt = now;
                header.PostedAt = now;
                header.CreatedBy = FirstNonEmpty(header.CreatedBy, header.AuthorizedBy);
                header.PostedBy = FirstNonEmpty(header.PostedBy, header.AuthorizedBy);
                header.Supplier = null!;
                header.GrnHeader = null;
                header.ReturnLines = new List<SupplierReturnLine>();

                context.SupplierReturnHeaders.Add(header);
                await context.SaveChangesAsync();

                foreach (SupplierReturnLine line in lines)
                {
                    PrepareNewLine(line, header.Id, now);
                    context.SupplierReturnLines.Add(line);
                }

                await context.SaveChangesAsync();
                await PostInventoryAndLedgerAsync(context, header, lines, now);
                await context.SaveChangesAsync();
                SupplierDebitNoteDto debitNote = await BuildSupplierDebitNoteAsync(context, header.Id);
                await transaction.CommitAsync();

                return new SupplierReturnPostResult
                {
                    ReturnHeader = header,
                    DebitNote = debitNote
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task SaveSupplierReturnAsync(
            SupplierReturnHeader header,
            List<SupplierReturnLine> lines,
            bool isDraft)
        {
            if (isDraft)
                throw new InvalidOperationException("Draft supplier returns are not supported. Please post the supplier return directly.");

            await PostSupplierReturnAsync(header, lines);
        }

        public async Task<SupplierDebitNoteDto> GetSupplierDebitNoteAsync(int supplierReturnHeaderId)
        {
            using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            return await BuildSupplierDebitNoteAsync(context, supplierReturnHeaderId);
        }

        private static async Task<SupplierDebitNoteDto> BuildSupplierDebitNoteAsync(
            AppDbContext context,
            int supplierReturnHeaderId)
        {
            SupplierReturnHeader header = await context.SupplierReturnHeaders
                .Include(row => row.Supplier)
                .Include(row => row.GrnHeader)
                .Include(row => row.ReturnLines)
                    .ThenInclude(line => line.ItemVariant)
                        .ThenInclude(variant => variant!.ItemParent)
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == supplierReturnHeaderId)
                ?? throw new InvalidOperationException("Supplier return debit note was not found.");

            StoreSettings? store = await context.StoreSettings
                .AsNoTracking()
                .OrderByDescending(row => row.IsActive)
                .ThenBy(row => row.Id)
                .FirstOrDefaultAsync();

            string storeAddress = JoinNonEmpty(
                ", ",
                store?.AddressLine1,
                store?.AddressLine2,
                store?.City,
                store?.PostalCode,
                store?.Country);

            return new SupplierDebitNoteDto
            {
                StoreName = FirstNonEmpty(store?.StoreName, store?.LegalName, "Store"),
                StoreAddress = storeAddress,
                StorePhone = NormalizeText(store?.Phone),
                StoreTin = NormalizeText(store?.TaxpayerIdentificationNumber),
                StoreVatNo = NormalizeText(store?.VatRegistrationNumber),
                DebitNoteNumber = header.ReturnNumber,
                ReturnDate = header.ReturnDate,
                SupplierName = FirstNonEmpty(header.Supplier?.CompanyName, header.Supplier?.SupplierName),
                SupplierCode = NormalizeText(header.Supplier?.SupplierCode),
                SupplierVatNo = NormalizeText(header.Supplier?.VatNumber),
                SupplierAddress = NormalizeText(header.Supplier?.Address),
                GrnNumber = NormalizeText(header.GrnHeader?.GrnNumber),
                OriginalSupplierInvoiceNo = header.OriginalInvoiceNo,
                AuthorizedBy = header.AuthorizedBy,
                Remarks = header.Remarks,
                GrossCredit = header.GrossCredit,
                NetCredit = header.NetCredit,
                TaxableAmountTotal = header.TaxableAmountTotal,
                TotalVatAmount = header.TotalVatAmount,
                StandardRatedAmount = header.StandardRatedAmount,
                ZeroRatedAmount = header.ZeroRatedAmount,
                ExemptAmount = header.ExemptAmount,
                OutOfScopeAmount = header.OutOfScopeAmount,
                TaxSnapshotStatus = header.TaxSnapshotStatus,
                Lines = header.ReturnLines
                    .OrderBy(line => line.Id)
                    .Select(line => new SupplierDebitNoteLineDto
                    {
                        ItemCode = line.ItemVariant?.ItemParent?.ItemCode ?? string.Empty,
                        Description = BuildItemDescription(line.ItemVariant),
                        BatchNo = line.BatchNo,
                        ReturnQuantity = line.ReturnQty,
                        HistoricalLandedCost = line.HistoricalCost,
                        SupplierCredit = line.CreditValue,
                        TaxCategoryCode = NormalizeText(line.TaxCategoryCodeSnapshot),
                        TaxName = NormalizeText(line.TaxNameSnapshot),
                        TaxRatePercent = line.TaxRatePercentSnapshot,
                        TaxableAmount = line.TaxableAmountSnapshot,
                        VatAmount = line.VatAmountSnapshot,
                        TaxInclusiveAmount = line.TaxInclusiveAmountSnapshot,
                        TaxSnapshotStatus = line.TaxSnapshotStatus,
                        ReasonCode = line.ReasonCode,
                        Remarks = line.LineRemarks
                    })
                    .ToList()
            };
        }

        private static async Task<GrnHeader> ValidateAndLoadHeaderSourceAsync(
            AppDbContext context,
            SupplierReturnHeader header)
        {
            if (header.SupplierId <= 0)
                throw new InvalidOperationException("Supplier is required.");

            Supplier? supplier = await context.Suppliers
                .FirstOrDefaultAsync(row => row.Id == header.SupplierId);

            if (supplier == null)
                throw new InvalidOperationException("Selected supplier is missing.");

            if (!header.GrnHeaderId.HasValue || header.GrnHeaderId.Value <= 0)
                throw new InvalidOperationException("Supplier return must be linked to a posted GRN.");

            GrnHeader? grn = await context.GrnHeaders
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == header.GrnHeaderId.Value);

            if (grn == null)
                throw new InvalidOperationException("Linked GRN was not found.");

            if (!string.Equals(grn.Status, SupplierReturnCodes.PostedStatus, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only posted GRNs can be used for supplier returns.");

            if (grn.SupplierId != header.SupplierId)
                throw new InvalidOperationException("Linked GRN supplier does not match selected supplier.");

            if (header.ReturnDate.Date > DateTime.Now.Date.AddDays(1))
                throw new InvalidOperationException("Return date cannot be in the far future.");

            if (string.IsNullOrWhiteSpace(header.AuthorizedBy))
                throw new InvalidOperationException("Authenticated user is required.");

            if (header.AuthorizedBy.Length > 50)
                throw new InvalidOperationException("Authorized by cannot be longer than 50 characters.");

            if (header.Remarks.Length > 500)
                throw new InvalidOperationException("Remarks cannot be longer than 500 characters.");

            return grn;
        }

        private async Task ValidateAndCalculateLinesAsync(
            AppDbContext context,
            SupplierReturnHeader header,
            List<SupplierReturnLine> lines)
        {
            int[] variantIds = lines.Select(line => line.ItemVariantId).Distinct().ToArray();
            int[] batchIds = lines.Select(line => line.ItemBatchId).Distinct().ToArray();
            int[] grnLineIds = lines
                .Where(line => line.GrnLineId.HasValue)
                .Select(line => line.GrnLineId!.Value)
                .Distinct()
                .ToArray();

            if (grnLineIds.Length != lines.Count)
                throw new InvalidOperationException("Every supplier return line must be linked to one GRN line.");

            Dictionary<int, ItemVariant> variants = await context.ItemVariants
                .Include(variant => variant.ItemParent)
                .Where(variant => variantIds.Contains(variant.Id))
                .ToDictionaryAsync(variant => variant.Id);

            Dictionary<int, ItemBatch> batches = await context.ItemBatches
                .Include(batch => batch.ItemVariant)
                    .ThenInclude(variant => variant.ItemParent)
                .Where(batch => batchIds.Contains(batch.Id))
                .ToDictionaryAsync(batch => batch.Id);

            Dictionary<int, GrnLine> grnLines = await context.GrnLines
                .Where(line => grnLineIds.Contains(line.Id))
                .ToDictionaryAsync(line => line.Id);

            Dictionary<int, PreviousReturnAggregate> prior =
                await LoadPriorReturnAggregatesAsync(context, grnLineIds);

            foreach (SupplierReturnLine line in lines)
            {
                NormalizeLine(line);

                if (!variants.TryGetValue(line.ItemVariantId, out ItemVariant? variant))
                    throw new InvalidOperationException("One or more supplier return items were not found.");

                if (!string.Equals(variant.ItemParent.ItemType, ItemTypeCodes.StockItem, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Service '{variant.SkuCode}' cannot be returned to a supplier.");

                if (!batches.TryGetValue(line.ItemBatchId, out ItemBatch? batch))
                    throw new InvalidOperationException($"Original stock row was not found for item '{variant.SkuCode}'.");

                if (batch.ItemVariantId != line.ItemVariantId)
                    throw new InvalidOperationException($"Stock row does not match item '{variant.SkuCode}'.");

                if (!IsValidStockBucketForItem(batch))
                    throw new InvalidOperationException($"Stock row '{BuildBatchDisplayName(batch)}' does not match the item tracking method.");

                if (!line.GrnLineId.HasValue ||
                    !grnLines.TryGetValue(line.GrnLineId.Value, out GrnLine? grnLine))
                {
                    throw new InvalidOperationException($"Linked GRN line was not found for item '{variant.SkuCode}'.");
                }

                if (!header.GrnHeaderId.HasValue || grnLine.GrnHeaderId != header.GrnHeaderId.Value)
                    throw new InvalidOperationException($"GRN line does not belong to the selected GRN for item '{variant.SkuCode}'.");

                if (grnLine.ItemVariantId != line.ItemVariantId)
                    throw new InvalidOperationException($"GRN line item does not match return item '{variant.SkuCode}'.");

                if (!grnLine.ItemBatchId.HasValue || grnLine.ItemBatchId.Value != line.ItemBatchId)
                    throw new InvalidOperationException($"Return stock row does not match the original GRN stock row for item '{variant.SkuCode}'.");

                if (line.ReturnQty <= 0m)
                    throw new InvalidOperationException($"Return quantity must be greater than zero for item '{variant.SkuCode}'.");

                if (line.ReturnQty > batch.CurrentStock)
                {
                    throw new InvalidOperationException(
                        $"Cannot return {line.ReturnQty:N3} for item '{variant.SkuCode}'. Current stock is only {batch.CurrentStock:N3}.");
                }

                PreviousReturnAggregate previous = prior.TryGetValue(grnLine.Id, out PreviousReturnAggregate? found)
                    ? found
                    : new PreviousReturnAggregate();

                decimal remainingQuantity = Math.Max(0m, grnLine.ReceivedQty - previous.Quantity);

                if (line.ReturnQty > remainingQuantity)
                {
                    throw new InvalidOperationException(
                        $"Cannot return {line.ReturnQty:N3} for item '{variant.SkuCode}'. Remaining returnable GRN quantity is {remainingQuantity:N3}.");
                }

                if (string.IsNullOrWhiteSpace(line.ReasonCode))
                    throw new InvalidOperationException($"Reason code is required for item '{variant.SkuCode}'.");

                if (line.ReasonCode.Length > 50)
                    throw new InvalidOperationException($"Reason code is too long for item '{variant.SkuCode}'.");

                if (line.LineRemarks.Length > 250)
                    throw new InvalidOperationException($"Line remarks are too long for item '{variant.SkuCode}'.");

                decimal historicalCost = ResolveHistoricalCost(grnLine, batch, variant);
                decimal originalCredit = ResolveOriginalCreditAmount(grnLine);

                SupplierReturnAllocationResult allocation = _allocationCalculator.Calculate(
                    BuildAllocationInput(grnLine, previous, line.ReturnQty, originalCredit));

                line.BatchNo = batch.BatchNo;
                line.ExpiryDate = batch.ExpiryDate;
                line.HistoricalCost = Money(historicalCost);
                line.CreditValue = allocation.CreditAmount;
                line.TaxCategoryId = grnLine.TaxCategoryId;
                line.TaxRateId = grnLine.TaxRateId;
                line.TaxCategoryCodeSnapshot = NormalizeNullable(grnLine.TaxCategoryCodeSnapshot);
                line.TaxCodeSnapshot = NormalizeNullable(grnLine.TaxCodeSnapshot);
                line.TaxNameSnapshot = NormalizeNullable(grnLine.TaxNameSnapshot);
                line.TaxRatePercentSnapshot = grnLine.TaxRatePercentSnapshot;
                line.IsTaxInclusiveSnapshot = grnLine.IsTaxInclusiveSnapshot;
                line.TaxableAmountSnapshot = allocation.TaxableAmount;
                line.VatAmountSnapshot = allocation.VatAmount;
                line.TaxInclusiveAmountSnapshot = allocation.TaxInclusiveAmount;
                line.OriginalTaxableAmount = grnLine.TaxableAmountSnapshot;
                line.OriginalVatAmount = grnLine.VatAmountSnapshot;
                line.OriginalTaxInclusiveAmount = grnLine.TaxInclusiveAmountSnapshot;
                line.TaxSnapshotStatus = allocation.TaxSnapshotStatus;
                line.ItemCode = variant.ItemParent.ItemCode;
                line.Description = variant.ItemParent.ItemName;
                line.VariantDescription = variant.VariantDescription;
                line.CurrentBatchStock = batch.CurrentStock;
                line.MaxReturnQty = Math.Min(remainingQuantity, batch.CurrentStock);
            }
        }

        private static void RecalculateHeaderTotals(
            SupplierReturnHeader header,
            IReadOnlyCollection<SupplierReturnLine> lines)
        {
            header.RestockingFee = 0m;
            header.GrossCredit = Money(lines.Sum(line => line.CreditValue));
            header.NetCredit = header.GrossCredit;

            bool complete = lines.All(line =>
                string.Equals(line.TaxSnapshotStatus, TaxSnapshotStatuses.Complete, StringComparison.Ordinal) &&
                line.TaxableAmountSnapshot.HasValue &&
                line.VatAmountSnapshot.HasValue &&
                line.TaxInclusiveAmountSnapshot.HasValue);

            header.TaxSnapshotStatus = complete
                ? TaxSnapshotStatuses.Complete
                : TaxSnapshotStatuses.LegacyUnknown;

            if (!complete)
            {
                header.TaxableAmountTotal = null;
                header.TotalVatAmount = null;
                header.StandardRatedAmount = null;
                header.ZeroRatedAmount = null;
                header.ExemptAmount = null;
                header.OutOfScopeAmount = null;
                return;
            }

            header.TaxableAmountTotal = Money(lines.Sum(line => line.TaxableAmountSnapshot ?? 0m));
            header.TotalVatAmount = Money(lines.Sum(line => line.VatAmountSnapshot ?? 0m));
            header.StandardRatedAmount = SumCategory(lines, TaxCategoryCodes.Standard);
            header.ZeroRatedAmount = SumCategory(lines, TaxCategoryCodes.ZeroRated);
            header.ExemptAmount = SumCategory(lines, TaxCategoryCodes.Exempt);
            header.OutOfScopeAmount = SumCategory(lines, TaxCategoryCodes.OutOfScope);
        }

        private static void ValidateCalculatedHeader(SupplierReturnHeader header)
        {
            if (header.GrossCredit <= 0m)
                throw new InvalidOperationException("Supplier credit must be greater than zero.");

            if (header.RestockingFee != 0m)
                throw new InvalidOperationException("Restocking fees are not supported for supplier returns.");

            if (header.NetCredit != header.GrossCredit)
                throw new InvalidOperationException("Supplier credit must exactly equal the reversed original GRN product value.");
        }

        private static async Task PostInventoryAndLedgerAsync(
            AppDbContext context,
            SupplierReturnHeader header,
            IReadOnlyCollection<SupplierReturnLine> lines,
            DateTime now)
        {
            int[] batchIds = lines.Select(line => line.ItemBatchId).Distinct().ToArray();

            Dictionary<int, ItemBatch> batches = await context.ItemBatches
                .Include(batch => batch.ItemVariant)
                    .ThenInclude(variant => variant.ItemParent)
                .Where(batch => batchIds.Contains(batch.Id))
                .ToDictionaryAsync(batch => batch.Id);

            foreach (SupplierReturnLine line in lines)
            {
                if (!batches.TryGetValue(line.ItemBatchId, out ItemBatch? batch))
                    throw new InvalidOperationException($"Stock row ID {line.ItemBatchId} was not found.");

                if (!IsValidStockBucketForItem(batch))
                    throw new InvalidOperationException($"Stock row '{BuildBatchDisplayName(batch)}' does not match the item tracking method.");

                if (batch.CurrentStock < line.ReturnQty)
                {
                    throw new InvalidOperationException(
                        $"Insufficient stock in '{BuildBatchDisplayName(batch)}'. Current stock is {batch.CurrentStock:N3}, return quantity is {line.ReturnQty:N3}.");
                }

                batch.CurrentStock -= line.ReturnQty;
                batch.UpdatedAt = now;
                line.LineStatus = SupplierReturnCodes.PostedStatus;
                line.UpdatedAt = now;

                context.InventoryTransactions.Add(new InventoryTransaction
                {
                    ItemVariantId = batch.ItemVariantId,
                    ItemBatchId = batch.Id,
                    TransactionDate = header.ReturnDate,
                    TransactionType = SupplierReturnCodes.InventoryTransactionType,
                    ReferenceDocument = header.ReturnNumber,
                    ReferenceLineId = line.Id,
                    Quantity = -line.ReturnQty,
                    UnitCost = line.HistoricalCost,
                    CreatedBy = header.AuthorizedBy,
                    CreatedAt = now,
                    Remarks = TrimToMax(
                        $"Supplier Return | Reason: {line.ReasonCode} | Stock Row: {BuildBatchDisplayName(batch)}",
                        250)
                });
            }

            Supplier supplier = await context.Suppliers
                .FirstOrDefaultAsync(row => row.Id == header.SupplierId)
                ?? throw new InvalidOperationException("Supplier was not found.");

            decimal newBalance = Money(supplier.CurrentBalance - header.NetCredit);

            context.SupplierLedgers.Add(new SupplierLedger
            {
                SupplierId = supplier.Id,
                GrnHeaderId = header.GrnHeaderId,
                TransactionDate = header.ReturnDate,
                TransactionType = SupplierReturnCodes.DebitNoteTransactionType,
                ReferenceDocument = header.ReturnNumber,
                ChargeAmount = 0m,
                PaymentAmount = header.NetCredit,
                BalanceAfterTransaction = newBalance,
                DueDate = header.ReturnDate,
                IsPaid = true,
                CreatedBy = header.AuthorizedBy,
                CreatedAt = now,
                Remarks = TrimToMax(
                    $"Supplier Return | Original Invoice: {header.OriginalInvoiceNo} | Product Credit: {header.NetCredit:N2}",
                    250)
            });

            supplier.CurrentBalance = newBalance;
            supplier.UpdatedAt = now;

            await RecalculateVariantAverageCostsAsync(
                context,
                lines.Select(line => line.ItemVariantId).Distinct().ToArray(),
                now);
        }

        private static async Task<Dictionary<int, PreviousReturnAggregate>> LoadPriorReturnAggregatesAsync(
            AppDbContext context,
            IReadOnlyCollection<int> grnLineIds)
        {
            if (grnLineIds.Count == 0)
                return new Dictionary<int, PreviousReturnAggregate>();

            int[] ids = grnLineIds.Distinct().ToArray();

            List<SupplierReturnLine> rows = await context.SupplierReturnLines
                .Include(line => line.ReturnHeader)
                .AsNoTracking()
                .Where(line =>
                    line.GrnLineId.HasValue &&
                    ids.Contains(line.GrnLineId.Value) &&
                    line.ReturnHeader != null &&
                    line.ReturnHeader.Status == SupplierReturnCodes.PostedStatus)
                .ToListAsync();

            return rows
                .GroupBy(line => line.GrnLineId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => new PreviousReturnAggregate
                    {
                        Quantity = group.Sum(line => line.ReturnQty),
                        CreditAmount = group.Sum(line => line.CreditValue),
                        TaxableAmount = group.Sum(line => line.TaxableAmountSnapshot ?? 0m),
                        VatAmount = group.Sum(line => line.VatAmountSnapshot ?? 0m),
                        TaxInclusiveAmount = group.Sum(line => line.TaxInclusiveAmountSnapshot ?? 0m),
                        HasIncompleteTaxSnapshot = group.Any(line =>
                            !string.Equals(line.TaxSnapshotStatus, TaxSnapshotStatuses.Complete, StringComparison.Ordinal) ||
                            !line.TaxableAmountSnapshot.HasValue ||
                            !line.VatAmountSnapshot.HasValue ||
                            !line.TaxInclusiveAmountSnapshot.HasValue)
                    });
        }

        private static SupplierReturnAllocationInput BuildAllocationInput(
            GrnLine line,
            PreviousReturnAggregate previous,
            decimal requestedQuantity,
            decimal originalCredit)
        {
            return new SupplierReturnAllocationInput
            {
                OriginalQuantity = line.ReceivedQty,
                PreviouslyReturnedQuantity = previous.Quantity,
                RequestedQuantity = requestedQuantity,
                OriginalCreditAmount = originalCredit,
                PreviouslyReturnedCreditAmount = previous.CreditAmount,
                OriginalTaxableAmount = line.TaxableAmountSnapshot,
                OriginalVatAmount = line.VatAmountSnapshot,
                OriginalTaxInclusiveAmount = line.TaxInclusiveAmountSnapshot,
                PreviouslyReturnedTaxableAmount = previous.TaxableAmount,
                PreviouslyReturnedVatAmount = previous.VatAmount,
                PreviouslyReturnedTaxInclusiveAmount = previous.TaxInclusiveAmount,
                TaxSnapshotStatus = previous.HasIncompleteTaxSnapshot
                    ? TaxSnapshotStatuses.LegacyUnknown
                    : line.TaxSnapshotStatus
            };
        }

        private static decimal ResolveOriginalCreditAmount(GrnLine line)
        {
            bool complete = string.Equals(
                line.TaxSnapshotStatus,
                TaxSnapshotStatuses.Complete,
                StringComparison.Ordinal) &&
                line.TaxInclusiveAmountSnapshot.HasValue;

            decimal amount = complete
                ? line.TaxInclusiveAmountSnapshot!.Value
                : line.LineTotal;

            if (amount <= 0m)
                throw new InvalidOperationException("Original GRN product payable amount is missing.");

            return Money(amount);
        }

        private static decimal ResolveHistoricalCost(
            GrnLine line,
            ItemBatch batch,
            ItemVariant variant)
        {
            decimal cost = line.LandedCost > 0m ? line.LandedCost : line.UnitCost;

            if (cost <= 0m)
                cost = batch.CostPrice > 0m ? batch.CostPrice : variant.AverageCost;

            if (cost <= 0m)
                cost = variant.CostPrice;

            if (cost <= 0m)
                throw new InvalidOperationException($"Historical landed cost is missing for item '{variant.SkuCode}'.");

            return cost;
        }

        private static decimal SumCategory(
            IEnumerable<SupplierReturnLine> lines,
            string categoryCode)
        {
            return Money(lines
                .Where(line => string.Equals(
                    NormalizeText(line.TaxCategoryCodeSnapshot),
                    categoryCode,
                    StringComparison.OrdinalIgnoreCase))
                .Sum(line => line.TaxableAmountSnapshot ?? 0m));
        }

        private static async Task RecalculateVariantAverageCostsAsync(
            AppDbContext context,
            IReadOnlyCollection<int> variantIds,
            DateTime now)
        {
            foreach (int variantId in variantIds.Distinct())
            {
                ItemVariant? variant = await context.ItemVariants
                    .FirstOrDefaultAsync(row => row.Id == variantId);

                if (variant == null)
                    continue;

                List<ItemBatch> activeBatches = await context.ItemBatches
                    .Where(batch =>
                        batch.ItemVariantId == variantId &&
                        !batch.IsDeactivated &&
                        batch.CurrentStock > 0m)
                    .ToListAsync();

                decimal totalQuantity = activeBatches.Sum(batch => batch.CurrentStock);

                if (totalQuantity > 0m)
                {
                    decimal totalValue = activeBatches.Sum(batch => batch.CurrentStock * batch.CostPrice);
                    variant.AverageCost = Money(totalValue / totalQuantity);
                }

                variant.UpdatedAt = now;
            }
        }

        private static async Task<string> GenerateDocumentNumberAsync(
            AppDbContext context,
            string documentType,
            string prefix)
        {
            DocumentSequence? sequence = await context.DocumentSequences
                .FirstOrDefaultAsync(row => row.DocumentType == documentType);

            if (sequence == null)
            {
                sequence = new DocumentSequence
                {
                    DocumentType = documentType,
                    Prefix = prefix,
                    NextSequenceNumber = 1,
                    PaddingLength = 5,
                    UpdatedAt = DateTime.Now
                };

                context.DocumentSequences.Add(sequence);
                await context.SaveChangesAsync();
            }

            if (sequence.NextSequenceNumber <= 0)
                sequence.NextSequenceNumber = 1;

            string number =
                $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString().PadLeft(sequence.PaddingLength, '0')}";

            sequence.NextSequenceNumber++;
            sequence.UpdatedAt = DateTime.Now;
            await context.SaveChangesAsync();
            return number;
        }

        private static void PrepareNewLine(SupplierReturnLine line, int headerId, DateTime now)
        {
            line.Id = 0;
            line.ReturnHeaderId = headerId;
            line.ReturnHeader = null;
            line.GrnLine = null;
            line.ItemVariant = null;
            line.ItemBatch = null;
            line.TaxCategory = null;
            line.TaxRate = null;
            line.LineStatus = SupplierReturnCodes.PostedStatus;
            line.CreatedAt = now;
            line.UpdatedAt = now;
        }

        private static void ValidateSubmittedLineDuplicates(IEnumerable<SupplierReturnLine> lines)
        {
            bool duplicate = lines
                .GroupBy(line => new { line.GrnLineId, line.ItemBatchId })
                .Any(group => group.Count() > 1);

            if (duplicate)
                throw new InvalidOperationException("The same GRN stock row can appear only once in one supplier return.");
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

        private static void NormalizeLines(IEnumerable<SupplierReturnLine> lines)
        {
            foreach (SupplierReturnLine line in lines)
                NormalizeLine(line);
        }

        private static void NormalizeLine(SupplierReturnLine line)
        {
            line.BatchNo = NormalizeText(line.BatchNo);
            line.ReasonCode = NormalizeText(line.ReasonCode);
            line.LineRemarks = NormalizeText(line.LineRemarks);
        }

        private static string BuildItemDescription(ItemVariant? variant)
        {
            if (variant == null)
                return string.Empty;

            string name = variant.ItemParent?.ItemName ?? string.Empty;
            string description = NormalizeText(variant.VariantDescription);

            if (string.IsNullOrWhiteSpace(description) ||
                description.Equals("Standard", StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }

            return string.IsNullOrWhiteSpace(name) ? description : $"{name} - {description}";
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
            bool batchTracked = batch.ItemVariant.ItemParent.HasBatchTracking;
            bool general = IsGeneralBatch(batch.BatchNo);
            return batchTracked ? !general : general;
        }

        private static string BuildBatchDisplayName(ItemBatch batch)
        {
            if (IsGeneralBatch(batch.BatchNo))
                return GeneralBatchNo;

            if (!string.IsNullOrWhiteSpace(batch.InternalBatchBarcode))
                return $"{batch.BatchNo} / {batch.InternalBatchBarcode}";

            return batch.BatchNo;
        }

        private static decimal Money(decimal value)
        {
            return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private static string NormalizeText(string? value) => (value ?? string.Empty).Trim();

        private static string? NormalizeNullable(string? value)
        {
            string normalized = NormalizeText(value);
            return normalized.Length == 0 ? null : normalized;
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
        }

        private static string JoinNonEmpty(string separator, params string?[] values)
        {
            return string.Join(separator, values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
        }

        private static string TrimToMax(string value, int maxLength)
        {
            value = NormalizeText(value);
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        private sealed class PreviousReturnAggregate
        {
            public decimal Quantity { get; init; }
            public decimal CreditAmount { get; init; }
            public decimal TaxableAmount { get; init; }
            public decimal VatAmount { get; init; }
            public decimal TaxInclusiveAmount { get; init; }
            public bool HasIncompleteTaxSnapshot { get; init; }
        }
    }
}
