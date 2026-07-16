using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Data.Configuration;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using POS.Core.Services.Returns;

namespace POS.Core.Repositories
{
    public sealed class CustomerReturnRepository
    {
        private const decimal QuantityTolerance = 0.0005m;

        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly CustomerReturnAllocationCalculator _allocationCalculator;

        public CustomerReturnRepository(
            IDbContextFactory<AppDbContext> contextFactory,
            CustomerReturnAllocationCalculator allocationCalculator)
        {
            _contextFactory = contextFactory ??
                throw new ArgumentNullException(nameof(contextFactory));
            _allocationCalculator = allocationCalculator ??
                throw new ArgumentNullException(nameof(allocationCalculator));
        }

        public async Task<CustomerReturnInvoiceDto?> FindCompletedSaleAsync(
            string invoiceNo)
        {
            string safeInvoiceNo = Normalize(invoiceNo);
            if (string.IsNullOrWhiteSpace(safeInvoiceNo))
                return null;

            await using AppDbContext context =
                await _contextFactory.CreateDbContextAsync();
            string caseInsensitiveCollation =
                DatabaseProviderModelConventions.GetCaseInsensitive(context.Database);

            SalesHeader? sale = await context.SalesHeaders
                .Include(header => header.SalesLines)
                .Include(header => header.SalesPayments)
                .AsNoTracking()
                .Where(header =>
                    EF.Functions.Collate(header.InvoiceNo, caseInsensitiveCollation) == safeInvoiceNo &&
                    header.Status == "Completed" &&
                    !header.IsVoided)
                .OrderByDescending(header => header.Id)
                .FirstOrDefaultAsync();

            if (sale == null)
                return null;

            int[] salesLineIds = sale.SalesLines
                .Select(line => line.Id)
                .ToArray();

            Dictionary<int, ReturnAggregate> priorReturns =
                await LoadPriorReturnAggregatesAsync(
                    context,
                    salesLineIds);

            var result = new CustomerReturnInvoiceDto
            {
                SalesHeaderId = sale.Id,
                InvoiceNo = sale.InvoiceNo,
                TransactionDate = sale.TransactionDate,
                CustomerName = sale.CustomerName,
                TerminalNo = sale.TerminalNo,
                CashierName = sale.CashierName,
                TaxSnapshotStatus = sale.TaxSnapshotStatus,
                NetTotal = sale.NetTotal,
                OriginalGiftVoucherPaymentAmount = Money(
                    sale.SalesPayments
                        .Where(payment => string.Equals(
                            payment.PaymentType,
                            PaymentTypeCodes.GiftVoucher,
                            StringComparison.OrdinalIgnoreCase))
                        .Sum(payment => payment.Amount))
            };

            foreach (SalesLine line in sale.SalesLines.OrderBy(row => row.Id))
            {
                priorReturns.TryGetValue(
                    line.Id,
                    out ReturnAggregate? prior);

                prior ??= new ReturnAggregate();

                decimal remaining = RoundQuantity(
                    line.Quantity - prior.Quantity);

                string itemType = ResolveItemType(line);
                string blockReason = GetBlockReason(line, remaining, itemType);

                result.Lines.Add(
                    MapReturnableLine(
                        line,
                        prior,
                        remaining,
                        itemType,
                        blockReason));
            }

            return result;
        }

        public async Task<CustomerReturnProcessResult> ProcessReturnAsync(
            CustomerReturnRequest request)
        {
            ValidateRequest(request);

            await using AppDbContext context =
                await _contextFactory.CreateDbContextAsync();
            await using var transaction =
                await context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                // SQLite transactions are deferred by default. This harmless update
                // acquires the write lock before return quantities are rechecked.
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE SalesHeaders SET Id = Id WHERE Id = {request.SalesHeaderId}");

                SalesHeader sale = await context.SalesHeaders
                    .Include(header => header.SalesLines)
                    .Include(header => header.SalesPayments)
                    .FirstOrDefaultAsync(header =>
                        header.Id == request.SalesHeaderId)
                    ?? throw new InvalidOperationException(
                        "The original sale could not be found.");

                ValidateSale(sale);

                ShiftSession shift = await context.ShiftSessions
                    .FirstOrDefaultAsync(row =>
                        row.Id == request.ShiftSessionId)
                    ?? throw new InvalidOperationException(
                        "The active cashier shift could not be found.");

                if (!string.Equals(shift.Status, "Open", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Customer returns require an open shift.");

                if (!string.Equals(
                        shift.TerminalNo,
                        Normalize(request.TerminalNo),
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "The return terminal does not match the active shift.");
                }

                int[] requestedLineIds = request.Lines
                    .Select(line => line.SalesLineId)
                    .ToArray();

                if (requestedLineIds.Distinct().Count() != requestedLineIds.Length)
                    throw new InvalidOperationException("A sales line was selected more than once.");

                Dictionary<int, SalesLine> saleLines = sale.SalesLines
                    .Where(line => requestedLineIds.Contains(line.Id))
                    .ToDictionary(line => line.Id);

                if (saleLines.Count != requestedLineIds.Length)
                    throw new InvalidOperationException("One or more selected lines do not belong to the original sale.");

                Dictionary<int, ReturnAggregate> priorReturns =
                    await LoadPriorReturnAggregatesAsync(
                        context,
                        requestedLineIds);

                var plans = new List<ReturnPlan>();

                foreach (CustomerReturnRequestLine requestedLine in request.Lines)
                {
                    SalesLine source = saleLines[requestedLine.SalesLineId];
                    priorReturns.TryGetValue(source.Id, out ReturnAggregate? prior);
                    prior ??= new ReturnAggregate();

                    decimal remaining = RoundQuantity(source.Quantity - prior.Quantity);
                    string itemType = ResolveItemType(source);
                    string blockReason = GetBlockReason(source, remaining, itemType);

                    if (!string.IsNullOrWhiteSpace(blockReason))
                        throw new InvalidOperationException(blockReason);

                    CustomerReturnAllocationResult allocation =
                        _allocationCalculator.Calculate(
                            new CustomerReturnAllocationInput
                            {
                                SoldQuantity = source.Quantity,
                                PreviouslyReturnedQuantity = prior.Quantity,
                                RequestedQuantity = requestedLine.Quantity,
                                OriginalGrossAmount = source.GrossAmount,
                                OriginalDiscountAmount = source.DiscountAmount,
                                OriginalRefundAmount = source.LineTotal,
                                PreviouslyRefundedAmount = prior.RefundAmount,
                                OriginalTaxableAmount = source.TaxableAmountSnapshot,
                                OriginalVatAmount = source.VatAmountSnapshot,
                                OriginalTaxInclusiveAmount = source.TaxInclusiveAmountSnapshot,
                                PreviouslyReturnedTaxableAmount = prior.TaxableAmount,
                                PreviouslyReturnedVatAmount = prior.VatAmount,
                                PreviouslyReturnedTaxInclusiveAmount = prior.TaxInclusiveAmount,
                                TaxSnapshotStatus = source.TaxSnapshotStatus
                            });

                    plans.Add(new ReturnPlan
                    {
                        SourceLine = source,
                        ItemType = itemType,
                        Prior = prior,
                        Allocation = allocation
                    });
                }

                Dictionary<int, ItemBatch> stockBatches =
                    await LoadAndValidateStockBatchesAsync(
                        context,
                        plans);

                DocumentSequence sequence =
                    await GetOrCreateCreditNoteSequenceAsync(context);

                string creditNoteNo =
                    $"{sequence.Prefix}" +
                    sequence.NextSequenceNumber
                        .ToString()
                        .PadLeft(sequence.PaddingLength, '0');

                sequence.NextSequenceNumber++;
                sequence.UpdatedAt = DateTime.Now;

                bool completeTax = plans.All(plan =>
                    string.Equals(
                        plan.SourceLine.TaxSnapshotStatus,
                        TaxSnapshotStatuses.Complete,
                        StringComparison.Ordinal) &&
                    plan.Allocation.TaxableAmount.HasValue &&
                    plan.Allocation.VatAmount.HasValue &&
                    plan.Allocation.TaxInclusiveAmount.HasValue);

                decimal totalReturnAmount = Money(
                    plans.Sum(plan => plan.Allocation.RefundAmount));

                CustomerLedger? originalCreditLedger = null;
                CustomerMaster? creditCustomer = null;
                decimal accountCreditAmount = 0m;

                if (sale.CustomerMasterId.HasValue)
                {
                    originalCreditLedger = await context.CustomerLedgers
                        .FirstOrDefaultAsync(row =>
                            row.SalesHeaderId == sale.Id &&
                            row.OutstandingAmount > 0m);

                    if (originalCreditLedger != null)
                    {
                        accountCreditAmount = Money(
                            Math.Min(
                                totalReturnAmount,
                                originalCreditLedger.OutstandingAmount));

                        creditCustomer = await context.CustomerMasters
                            .FirstOrDefaultAsync(row =>
                                row.Id == sale.CustomerMasterId.Value);
                    }
                }

                decimal originalGiftVoucherPaymentAmount = Money(
                    sale.SalesPayments
                        .Where(payment => string.Equals(
                            payment.PaymentType,
                            PaymentTypeCodes.GiftVoucher,
                            StringComparison.OrdinalIgnoreCase))
                        .Sum(payment => payment.Amount));

                List<decimal> priorVoucherRefundAmounts = await context.CustomerReturnHeaders
                    .AsNoTracking()
                    .Where(row =>
                        row.OriginalSalesHeaderId == sale.Id &&
                        row.GiftVoucherRefundAmount > 0m)
                    .Select(row => row.GiftVoucherRefundAmount)
                    .ToListAsync();

                decimal priorGiftVoucherRefundAmount = Money(
                    priorVoucherRefundAmounts.Sum());
                decimal remainingVoucherFundedAmount = Money(
                    Math.Max(
                        0m,
                        originalGiftVoucherPaymentAmount - priorGiftVoucherRefundAmount));
                decimal unsettledAfterAccountCredit = Money(
                    totalReturnAmount - accountCreditAmount);
                decimal giftVoucherRefundAmount = Money(
                    Math.Min(
                        unsettledAfterAccountCredit,
                        remainingVoucherFundedAmount));
                decimal cashRefundAmount = Money(
                    unsettledAfterAccountCredit - giftVoucherRefundAmount);

                var returnHeader = new CustomerReturnHeader
                {
                    ReturnNo = creditNoteNo,
                    OriginalInvoiceNo = sale.InvoiceNo,
                    OriginalSalesHeaderId = sale.Id,
                    ShiftSessionId = shift.Id,
                    TerminalNo = Truncate(Normalize(request.TerminalNo), 20),
                    CashierName = Truncate(Normalize(request.CashierName), 100),
                    AuthorizedBy = Truncate(Normalize(request.AuthorizedBy), 100),
                    ReturnDate = DateTime.Now,
                    TotalRefundAmount = totalReturnAmount,
                    AccountCreditAmount = accountCreditAmount,
                    GiftVoucherRefundAmount = giftVoucherRefundAmount,
                    CashRefundAmount = cashRefundAmount,
                    RefundMethod = BuildRefundMethod(
                        accountCreditAmount,
                        giftVoucherRefundAmount,
                        cashRefundAmount),
                    DocumentType = CustomerReturnDocumentTypes.CreditNote,
                    CreditNoteNo = creditNoteNo,
                    TaxSnapshotStatus = completeTax
                        ? TaxSnapshotStatuses.Complete
                        : TaxSnapshotStatuses.LegacyUnknown
                };

                if (completeTax)
                {
                    returnHeader.TaxableAmountTotal = Money(
                        plans.Sum(plan => plan.Allocation.TaxableAmount ?? 0m));
                    returnHeader.TotalVatAmount = Money(
                        plans.Sum(plan => plan.Allocation.VatAmount ?? 0m));
                    returnHeader.StandardRatedAmount = Money(
                        plans
                            .Where(plan => IsCategory(plan.SourceLine, TaxCategoryCodes.Standard))
                            .Sum(plan => plan.Allocation.TaxableAmount ?? 0m));
                    returnHeader.ZeroRatedAmount = Money(
                        plans
                            .Where(plan => IsCategory(plan.SourceLine, TaxCategoryCodes.ZeroRated))
                            .Sum(plan => plan.Allocation.TaxableAmount ?? 0m));
                    returnHeader.ExemptAmount = Money(
                        plans
                            .Where(plan => IsCategory(plan.SourceLine, TaxCategoryCodes.Exempt))
                            .Sum(plan => plan.Allocation.TaxableAmount ?? 0m));
                    returnHeader.OutOfScopeAmount = Money(
                        plans
                            .Where(plan => IsCategory(plan.SourceLine, TaxCategoryCodes.OutOfScope))
                            .Sum(plan => plan.Allocation.TaxableAmount ?? 0m));
                }

                context.CustomerReturnHeaders.Add(returnHeader);

                foreach (ReturnPlan plan in plans)
                {
                    SalesLine source = plan.SourceLine;
                    CustomerReturnAllocationResult allocation = plan.Allocation;

                    returnHeader.Lines.Add(
                        new CustomerReturnLine
                        {
                            SalesLineId = source.Id,
                            ItemVariantId = source.ItemVariantId!.Value,
                            ItemBatchId = string.Equals(
                                    plan.ItemType,
                                    ItemTypeCodes.StockItem,
                                    StringComparison.Ordinal)
                                ? source.ItemBatchId
                                : null,
                            ItemDescription = Truncate(
                                FirstNonEmpty(source.ItemDescription, source.SkuCode, "Item"),
                                255),
                            QuantityReturned = allocation.Quantity,
                            RefundValue = allocation.RefundUnitValue,
                            LineTotalRefund = allocation.RefundAmount,
                            ReturnReason = Truncate(Normalize(request.ReturnReason), 100),
                            InventoryAction = string.Equals(
                                    plan.ItemType,
                                    ItemTypeCodes.StockItem,
                                    StringComparison.Ordinal)
                                ? CustomerReturnInventoryActions.RestoredToOriginalBatch
                                : CustomerReturnInventoryActions.NoInventory,
                            ItemTypeSnapshot = plan.ItemType,
                            TaxCategoryId = source.TaxCategoryId,
                            TaxRateId = source.TaxRateId,
                            TaxCategoryCodeSnapshot = source.TaxCategoryCodeSnapshot,
                            TaxCodeSnapshot = source.TaxCodeSnapshot,
                            TaxNameSnapshot = source.TaxNameSnapshot,
                            TaxRatePercentSnapshot = source.TaxRatePercentSnapshot,
                            IsTaxInclusiveSnapshot = source.IsTaxInclusiveSnapshot,
                            TaxableAmountSnapshot = allocation.TaxableAmount,
                            VatAmountSnapshot = allocation.VatAmount,
                            TaxInclusiveAmountSnapshot = allocation.TaxInclusiveAmount,
                            OriginalTaxableAmount = source.TaxableAmountSnapshot,
                            OriginalVatAmount = source.VatAmountSnapshot,
                            OriginalTaxInclusiveAmount = source.TaxInclusiveAmountSnapshot,
                            TaxSnapshotStatus = source.TaxSnapshotStatus
                        });
                }

                await context.SaveChangesAsync();

                if (giftVoucherRefundAmount > 0m)
                {
                    GiftVoucher replacementVoucher =
                        await GiftVoucherRepository.CreateReturnVoucherAsync(
                            context,
                            returnHeader,
                            giftVoucherRefundAmount,
                            Truncate(Normalize(request.CashierName), 100),
                            Truncate(Normalize(request.TerminalNo), 20),
                            DateTime.Today.AddYears(1),
                            $"One-time replacement voucher for return {creditNoteNo} against {sale.InvoiceNo}.");

                    returnHeader.ReplacementGiftVoucherId = replacementVoucher.Id;
                    returnHeader.ReplacementGiftVoucherNo = replacementVoucher.VoucherNo;
                    await context.SaveChangesAsync();
                }

                foreach (ReturnPlan plan in plans)
                {
                    SalesLine source = plan.SourceLine;
                    CustomerReturnAllocationResult allocation = plan.Allocation;
                    CustomerReturnLine savedLine = returnHeader.Lines
                        .Single(line => line.SalesLineId == source.Id);

                    await FreeItemClaimRepository.CreateReturnAdjustmentAsync(
                        context,
                        source,
                        savedLine,
                        creditNoteNo,
                        request.CashierName);

                    if (string.Equals(
                            plan.ItemType,
                            ItemTypeCodes.StockItem,
                            StringComparison.Ordinal))
                    {
                        ItemBatch batch = stockBatches[source.ItemBatchId!.Value];
                        batch.CurrentStock = RoundQuantity(
                            batch.CurrentStock + allocation.Quantity);
                        batch.UpdatedAt = DateTime.Now;

                        context.InventoryTransactions.Add(
                            new InventoryTransaction
                            {
                                ItemVariantId = source.ItemVariantId!.Value,
                                ItemBatchId = batch.Id,
                                TransactionDate = returnHeader.ReturnDate,
                                TransactionType = "RETURN",
                                ReferenceDocument = creditNoteNo,
                                ReferenceLineId = savedLine.Id,
                                Quantity = allocation.Quantity,
                                UnitCost = source.CostPrice,
                                CreatedBy = Truncate(Normalize(request.CashierName), 50),
                                Remarks = Truncate(
                                    $"Customer return against {sale.InvoiceNo}",
                                    250),
                                CreatedAt = returnHeader.ReturnDate
                            });
                    }

                    decimal newReturnedQuantity = RoundQuantity(
                        plan.Prior.Quantity + allocation.Quantity);
                    source.IsReturned =
                        source.Quantity - newReturnedQuantity <= QuantityTolerance;
                }

                if (accountCreditAmount > 0m &&
                    originalCreditLedger != null &&
                    creditCustomer != null)
                {
                    originalCreditLedger.AllocatedAmount = Money(
                        originalCreditLedger.AllocatedAmount + accountCreditAmount);
                    originalCreditLedger.OutstandingAmount = Money(
                        originalCreditLedger.OutstandingAmount - accountCreditAmount);
                    originalCreditLedger.Status = originalCreditLedger.OutstandingAmount <= 0m
                        ? CustomerCreditCodes.Paid
                        : CustomerCreditCodes.PartPaid;

                    var returnCreditLedger = new CustomerLedger
                    {
                        CustomerMasterId = creditCustomer.Id,
                        CustomerReturnHeaderId = returnHeader.Id,
                        TransactionDate = returnHeader.ReturnDate,
                        DocumentRef = creditNoteNo,
                        TransactionType = CustomerCreditCodes.ReturnCredit,
                        DebitAmount = 0m,
                        CreditAmount = accountCreditAmount,
                        OriginalAmount = accountCreditAmount,
                        AllocatedAmount = accountCreditAmount,
                        OutstandingAmount = 0m,
                        Status = CustomerCreditCodes.Paid,
                        ProcessedBy = Truncate(Normalize(request.CashierName), 100),
                        Remarks = Truncate($"Credit Note against {sale.InvoiceNo}", 255)
                    };
                    context.CustomerLedgers.Add(returnCreditLedger);
                    await context.SaveChangesAsync();

                    context.CustomerLedgerAllocations.Add(
                        new CustomerLedgerAllocation
                        {
                            CustomerMasterId = creditCustomer.Id,
                            DebitLedgerId = originalCreditLedger.Id,
                            CreditLedgerId = returnCreditLedger.Id,
                            Amount = accountCreditAmount,
                            CreatedAt = returnHeader.ReturnDate,
                            CreatedBy = Truncate(Normalize(request.CashierName), 100)
                        });

                    creditCustomer.CurrentBalance = Money(
                        Math.Max(0m, creditCustomer.CurrentBalance - accountCreditAmount));
                    creditCustomer.UpdatedAt = returnHeader.ReturnDate;
                    creditCustomer.UpdatedBy = Truncate(Normalize(request.CashierName), 100);
                }

                if (cashRefundAmount > 0m)
                {
                    context.CashMovements.Add(
                        new CashMovement
                        {
                            ShiftSessionId = shift.Id,
                            MovementType = CustomerReturnCashMovementCodes.MovementType,
                            Amount = cashRefundAmount,
                            ReasonCategory = CustomerReturnCashMovementCodes.ReasonCategory,
                            Remarks = Truncate(
                                $"Cash refund for {sale.InvoiceNo}",
                                255),
                            CashierName = Truncate(Normalize(request.CashierName), 100),
                            AuthorizedBy = Truncate(Normalize(request.AuthorizedBy), 100),
                            Timestamp = returnHeader.ReturnDate,
                            ReferenceVoucherNo = creditNoteNo
                        });
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                CustomerReturnHeader savedReturn =
                    await GetReturnByIdAsync(returnHeader.Id);

                return new CustomerReturnProcessResult
                {
                    ReturnHeader = savedReturn,
                    CreditNoteNo = creditNoteNo,
                    TotalRefundAmount = savedReturn.TotalRefundAmount,
                    AccountCreditAmount = savedReturn.AccountCreditAmount,
                    GiftVoucherRefundAmount = savedReturn.GiftVoucherRefundAmount,
                    ReplacementGiftVoucherNo = savedReturn.ReplacementGiftVoucherNo,
                    CashRefundAmount = savedReturn.CashRefundAmount
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<CustomerReturnHeader> GetReturnByIdAsync(int returnId)
        {
            await using AppDbContext context =
                await _contextFactory.CreateDbContextAsync();

            return await context.CustomerReturnHeaders
                .Include(header => header.OriginalSalesHeader)
                .Include(header => header.ReplacementGiftVoucher)
                .Include(header => header.Lines)
                    .ThenInclude(line => line.SalesLine)
                .AsNoTracking()
                .FirstOrDefaultAsync(header => header.Id == returnId)
                ?? throw new InvalidOperationException(
                    "The completed customer return could not be loaded.");
        }

        private static string BuildRefundMethod(
            decimal accountCreditAmount,
            decimal giftVoucherRefundAmount,
            decimal cashRefundAmount)
        {
            int methodCount = 0;
            if (accountCreditAmount > 0m) methodCount++;
            if (giftVoucherRefundAmount > 0m) methodCount++;
            if (cashRefundAmount > 0m) methodCount++;

            if (methodCount == 0)
                return "No Refund";
            if (methodCount > 1)
                return "Split";
            if (accountCreditAmount > 0m)
                return "Account Credit";
            if (giftVoucherRefundAmount > 0m)
                return "Gift Voucher";
            return CustomerReturnRefundMethods.Cash;
        }

        private static CustomerReturnableLineDto MapReturnableLine(
            SalesLine line,
            ReturnAggregate prior,
            decimal remaining,
            string itemType,
            string blockReason)
        {
            return new CustomerReturnableLineDto
            {
                SalesLineId = line.Id,
                ItemVariantId = line.ItemVariantId ?? 0,
                ItemBatchId = line.ItemBatchId,
                ItemType = itemType,
                ItemDescription = FirstNonEmpty(line.ItemDescription, line.SkuCode, "Item"),
                SkuCode = line.SkuCode,
                BatchNo = line.BatchNo,
                Uom = line.Uom,
                SoldQuantity = line.Quantity,
                PreviouslyReturnedQuantity = prior.Quantity,
                RemainingQuantity = Math.Max(0m, remaining),
                UnitPrice = line.UnitPrice,
                OriginalGrossAmount = line.GrossAmount,
                OriginalDiscountAmount = line.DiscountAmount,
                OriginalRefundAmount = line.LineTotal,
                PreviouslyRefundedAmount = prior.RefundAmount,
                OriginalTaxableAmount = line.TaxableAmountSnapshot,
                OriginalVatAmount = line.VatAmountSnapshot,
                OriginalTaxInclusiveAmount = line.TaxInclusiveAmountSnapshot,
                PreviouslyReturnedTaxableAmount = prior.TaxableAmount,
                PreviouslyReturnedVatAmount = prior.VatAmount,
                PreviouslyReturnedTaxInclusiveAmount = prior.TaxInclusiveAmount,
                TaxCategoryId = line.TaxCategoryId,
                TaxRateId = line.TaxRateId,
                TaxCategoryCodeSnapshot = line.TaxCategoryCodeSnapshot,
                TaxCodeSnapshot = line.TaxCodeSnapshot,
                TaxNameSnapshot = line.TaxNameSnapshot,
                TaxRatePercentSnapshot = line.TaxRatePercentSnapshot,
                IsTaxInclusiveSnapshot = line.IsTaxInclusiveSnapshot,
                TaxSnapshotStatus = line.TaxSnapshotStatus,
                IsReturnable = string.IsNullOrWhiteSpace(blockReason),
                BlockReason = blockReason
            };
        }

        private static async Task<Dictionary<int, ItemBatch>> LoadAndValidateStockBatchesAsync(
            AppDbContext context,
            IEnumerable<ReturnPlan> plans)
        {
            int[] batchIds = plans
                .Where(plan => string.Equals(
                    plan.ItemType,
                    ItemTypeCodes.StockItem,
                    StringComparison.Ordinal))
                .Select(plan => plan.SourceLine.ItemBatchId ?? 0)
                .Distinct()
                .ToArray();

            if (batchIds.Contains(0))
                throw new InvalidOperationException("A Stock Item return is missing its original batch.");

            Dictionary<int, ItemBatch> batches = await context.ItemBatches
                .Where(batch => batchIds.Contains(batch.Id))
                .ToDictionaryAsync(batch => batch.Id);

            if (batches.Count != batchIds.Length)
                throw new InvalidOperationException("An original Stock Item batch could not be loaded.");

            foreach (ReturnPlan plan in plans.Where(plan =>
                         string.Equals(
                             plan.ItemType,
                             ItemTypeCodes.StockItem,
                             StringComparison.Ordinal)))
            {
                ItemBatch batch = batches[plan.SourceLine.ItemBatchId!.Value];
                if (batch.ItemVariantId != plan.SourceLine.ItemVariantId)
                    throw new InvalidOperationException("The original return batch does not match the sold item.");
            }

            return batches;
        }

        private static async Task<Dictionary<int, ReturnAggregate>> LoadPriorReturnAggregatesAsync(
            AppDbContext context,
            IReadOnlyCollection<int> salesLineIds)
        {
            if (salesLineIds.Count == 0)
                return new Dictionary<int, ReturnAggregate>();

            int[] ids = salesLineIds.Distinct().ToArray();

            // SQLite cannot reliably aggregate decimal values in SQL. Load only
            // the matching immutable return snapshots, then aggregate in memory.
            List<CustomerReturnLine> priorLines = await context.CustomerReturnLines
                .AsNoTracking()
                .Where(line =>
                    line.SalesLineId.HasValue &&
                    ids.Contains(line.SalesLineId.Value))
                .ToListAsync();

            return priorLines
                .GroupBy(line => line.SalesLineId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => new ReturnAggregate
                    {
                        Quantity = group.Sum(line => line.QuantityReturned),
                        RefundAmount = group.Sum(line => line.LineTotalRefund),
                        TaxableAmount = group.Sum(line => line.TaxableAmountSnapshot ?? 0m),
                        VatAmount = group.Sum(line => line.VatAmountSnapshot ?? 0m),
                        TaxInclusiveAmount = group.Sum(line => line.TaxInclusiveAmountSnapshot ?? 0m)
                    });
        }

        private static async Task<DocumentSequence> GetOrCreateCreditNoteSequenceAsync(
            AppDbContext context)
        {
            DocumentSequence? sequence = await context.DocumentSequences
                .FirstOrDefaultAsync(row =>
                    row.DocumentType == CustomerReturnSequenceCodes.DocumentType);

            if (sequence != null)
            {
                if (sequence.NextSequenceNumber <= 0)
                    sequence.NextSequenceNumber = 1;

                return sequence;
            }

            sequence = new DocumentSequence
            {
                DocumentType = CustomerReturnSequenceCodes.DocumentType,
                Prefix = CustomerReturnSequenceCodes.Prefix,
                NextSequenceNumber = 1,
                PaddingLength = CustomerReturnSequenceCodes.PaddingLength,
                UpdatedAt = DateTime.Now
            };

            context.DocumentSequences.Add(sequence);
            return sequence;
        }

        private static void ValidateRequest(CustomerReturnRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.SalesHeaderId <= 0)
                throw new InvalidOperationException("Select an original completed invoice.");

            if (request.ShiftSessionId <= 0)
                throw new InvalidOperationException("An open cashier shift is required.");

            if (string.IsNullOrWhiteSpace(request.TerminalNo))
                throw new InvalidOperationException("Terminal number is required.");

            if (string.IsNullOrWhiteSpace(request.CashierName))
                throw new InvalidOperationException("Cashier name is required.");

            if (string.IsNullOrWhiteSpace(request.AuthorizedBy))
                throw new InvalidOperationException("Authorized user is required.");

            if (string.IsNullOrWhiteSpace(request.ReturnReason))
                throw new InvalidOperationException("Return reason is required.");

            if (request.Lines == null || request.Lines.Count == 0)
                throw new InvalidOperationException("Select at least one line to return.");
        }

        private static void ValidateSale(SalesHeader sale)
        {
            if (!string.Equals(sale.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
                sale.IsVoided)
            {
                throw new InvalidOperationException("Only a completed, non-voided sale can be returned.");
            }
        }

        private static string GetBlockReason(
            SalesLine line,
            decimal remainingQuantity,
            string itemType)
        {
            if (line.IsGiftVoucherSale)
                return "Gift voucher sale lines cannot be returned in this workflow.";


            if (!line.ItemVariantId.HasValue)
                return "The sale line is not linked to a normal Stock Item or Service.";

            if (remainingQuantity <= QuantityTolerance)
                return "This sale line has already been fully returned.";

            if (string.Equals(itemType, ItemTypeCodes.StockItem, StringComparison.Ordinal) &&
                !line.ItemBatchId.HasValue)
            {
                return "The original Stock Item batch is missing.";
            }

            if (!ItemTypeCodes.IsValid(itemType))
                return "The original item type is not supported for return.";

            return string.Empty;
        }

        public async Task<List<CustomerReturnHistoryRowDto>> GetReturnHistoryAsync(
            DateTime startDate,
            DateTime endDate,
            string searchText)
        {
            if (startDate.Date > endDate.Date)
                throw new ArgumentException("Start date cannot be later than end date.");

            DateTime start = startDate.Date;
            DateTime endExclusive = endDate.Date.AddDays(1);
            string search = Normalize(searchText);

            await using AppDbContext context =
                await _contextFactory.CreateDbContextAsync();

            var query = context.CustomerReturnHeaders
                .AsNoTracking()
                .Where(row => row.ReturnDate >= start && row.ReturnDate < endExclusive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(row =>
                    row.ReturnNo.Contains(search) ||
                    (row.CreditNoteNo ?? string.Empty).Contains(search) ||
                    (row.OriginalInvoiceNo ?? string.Empty).Contains(search) ||
                    row.CashierName.Contains(search) ||
                    row.TerminalNo.Contains(search) ||
                    (row.OriginalSalesHeader != null &&
                        (row.OriginalSalesHeader.CustomerName.Contains(search) ||
                         row.OriginalSalesHeader.CustomerCode.Contains(search))));
            }

            return await query
                .OrderByDescending(row => row.ReturnDate)
                .ThenByDescending(row => row.Id)
                .Select(row => new CustomerReturnHistoryRowDto
                {
                    Id = row.Id,
                    ReturnNo = row.ReturnNo,
                    CreditNoteNo = row.CreditNoteNo ?? row.ReturnNo,
                    OriginalInvoiceNo = row.OriginalInvoiceNo ?? string.Empty,
                    ReturnDate = row.ReturnDate,
                    CustomerName = row.OriginalSalesHeader == null
                        ? "Walk-In"
                        : row.OriginalSalesHeader.CustomerName,
                    CashierName = row.CashierName,
                    TerminalNo = row.TerminalNo,
                    AuthorizedBy = row.AuthorizedBy,
                    RefundMethod = row.RefundMethod,
                    TotalRefundAmount = row.TotalRefundAmount,
                    AccountCreditAmount = row.AccountCreditAmount,
                    GiftVoucherRefundAmount = row.GiftVoucherRefundAmount,
                    CashRefundAmount = row.CashRefundAmount,
                    TaxableAmountTotal = row.TaxableAmountTotal,
                    TotalVatAmount = row.TotalVatAmount,
                    TaxSnapshotStatus = row.TaxSnapshotStatus,
                    LineCount = row.Lines.Count
                })
                .ToListAsync();
        }

        public async Task<CustomerReturnHistoryDetailsDto?> GetReturnHistoryDetailsAsync(
            int returnHeaderId)
        {
            await using AppDbContext context =
                await _contextFactory.CreateDbContextAsync();

            CustomerReturnHeader? row = await context.CustomerReturnHeaders
                .AsNoTracking()
                .Include(header => header.OriginalSalesHeader)
                .Include(header => header.Lines)
                    .ThenInclude(line => line.SalesLine)
                .FirstOrDefaultAsync(header => header.Id == returnHeaderId);

            if (row == null)
                return null;

            return new CustomerReturnHistoryDetailsDto
            {
                Id = row.Id,
                ReturnNo = row.ReturnNo,
                CreditNoteNo = row.CreditNoteNo ?? row.ReturnNo,
                OriginalInvoiceNo = row.OriginalInvoiceNo ?? string.Empty,
                ReturnDate = row.ReturnDate,
                CustomerName = row.OriginalSalesHeader?.CustomerName ?? "Walk-In",
                CustomerCode = row.OriginalSalesHeader?.CustomerCode ?? string.Empty,
                CashierName = row.CashierName,
                TerminalNo = row.TerminalNo,
                AuthorizedBy = row.AuthorizedBy,
                RefundMethod = row.RefundMethod,
                TotalRefundAmount = row.TotalRefundAmount,
                AccountCreditAmount = row.AccountCreditAmount,
                GiftVoucherRefundAmount = row.GiftVoucherRefundAmount,
                ReplacementGiftVoucherNo = row.ReplacementGiftVoucherNo,
                CashRefundAmount = row.CashRefundAmount,
                TaxableAmountTotal = row.TaxableAmountTotal,
                TotalVatAmount = row.TotalVatAmount,
                StandardRatedAmount = row.StandardRatedAmount,
                ZeroRatedAmount = row.ZeroRatedAmount,
                ExemptAmount = row.ExemptAmount,
                OutOfScopeAmount = row.OutOfScopeAmount,
                TaxSnapshotStatus = row.TaxSnapshotStatus,
                Lines = row.Lines
                    .OrderBy(line => line.Id)
                    .Select(line => new CustomerReturnHistoryLineDto
                    {
                        Id = line.Id,
                        SalesLineId = line.SalesLineId,
                        ItemCode = line.SalesLine?.SkuCode ?? string.Empty,
                        ItemDescription = line.ItemDescription,
                        ItemType = line.ItemTypeSnapshot ?? string.Empty,
                        QuantityReturned = line.QuantityReturned,
                        RefundValue = line.RefundValue,
                        LineTotalRefund = line.LineTotalRefund,
                        ReturnReason = line.ReturnReason,
                        InventoryAction = line.InventoryAction,
                        TaxCategoryCode = line.TaxCategoryCodeSnapshot ?? string.Empty,
                        TaxRatePercent = line.TaxRatePercentSnapshot,
                        TaxableAmount = line.TaxableAmountSnapshot,
                        VatAmount = line.VatAmountSnapshot,
                        TaxInclusiveAmount = line.TaxInclusiveAmountSnapshot,
                        TaxSnapshotStatus = line.TaxSnapshotStatus
                    })
                    .ToList()
            };
        }

        public async Task<CustomerReturnHeader?> GetReturnDocumentAsync(int returnHeaderId)
        {
            await using AppDbContext context =
                await _contextFactory.CreateDbContextAsync();

            return await context.CustomerReturnHeaders
                .AsNoTracking()
                .Include(header => header.OriginalSalesHeader)
                .Include(header => header.Lines)
                    .ThenInclude(line => line.SalesLine)
                .FirstOrDefaultAsync(header => header.Id == returnHeaderId);
        }

        private static string ResolveItemType(SalesLine line)
        {
            if (string.Equals(
                    line.ItemTypeSnapshot,
                    ItemTypeCodes.StockItem,
                    StringComparison.Ordinal))
            {
                return ItemTypeCodes.StockItem;
            }

            if (string.Equals(
                    line.ItemTypeSnapshot,
                    ItemTypeCodes.Service,
                    StringComparison.Ordinal))
            {
                return ItemTypeCodes.Service;
            }

            // Historical rows may lack the type snapshot. The original batch link
            // remains authoritative for whether inventory must be restored.
            return line.ItemBatchId.HasValue
                ? ItemTypeCodes.StockItem
                : ItemTypeCodes.Service;
        }

        private static bool IsCategory(SalesLine line, string categoryCode) =>
            string.Equals(
                line.TaxCategoryCodeSnapshot,
                categoryCode,
                StringComparison.OrdinalIgnoreCase);

        private static string Normalize(string? value) =>
            (value ?? string.Empty).Trim();

        private static string Truncate(string value, int maxLength) =>
            value.Length <= maxLength
                ? value
                : value[..maxLength];

        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim()
            ?? string.Empty;

        private static decimal Money(decimal value) =>
            decimal.Round(value, 2, MidpointRounding.AwayFromZero);

        private static decimal RoundQuantity(decimal value) =>
            decimal.Round(value, 3, MidpointRounding.AwayFromZero);

        private sealed class ReturnAggregate
        {
            public decimal Quantity { get; init; }
            public decimal RefundAmount { get; init; }
            public decimal TaxableAmount { get; init; }
            public decimal VatAmount { get; init; }
            public decimal TaxInclusiveAmount { get; init; }
        }

        private sealed class ReturnPlan
        {
            public SalesLine SourceLine { get; init; } = null!;
            public string ItemType { get; init; } = string.Empty;
            public ReturnAggregate Prior { get; init; } = null!;
            public CustomerReturnAllocationResult Allocation { get; init; } = null!;
        }
    }
}
