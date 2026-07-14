using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Enums;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Services.Tax;

namespace POS.Core.Repositories
{
    public class SalesRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly SalesTaxService _salesTaxService = new();

        public SalesRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<SalesHeader> ProcessCheckoutAsync(
            SalesHeader header,
            List<SalesLine> lines,
            List<SalesPayment>? payments = null,
            Guid? checkoutToken = null)
        {
            if (header == null)
                throw new ArgumentNullException(nameof(header));

            if (lines == null || !lines.Any())
                throw new InvalidOperationException("Cannot checkout an empty cart.");

            payments ??= new List<SalesPayment>();

            NormalizeSalesHeader(header);
            NormalizeSalesLines(lines);
            NormalizePayments(payments);

            if (checkoutToken.HasValue && checkoutToken.Value != Guid.Empty)
                header.CheckoutToken = checkoutToken.Value;

            bool sellingGiftVoucher = lines.Any(line => line.IsGiftVoucherSale);
            bool payingByGiftVoucher = payments.Any(IsGiftVoucherPayment);

            if (sellingGiftVoucher && payingByGiftVoucher)
                throw new InvalidOperationException("Gift voucher cannot be used to buy another gift voucher.");

            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                DateTime now = DateTime.Now;
                CashierCartSession? cartSession = null;

                if (header.CheckoutToken.HasValue && header.CheckoutToken.Value != Guid.Empty)
                {
                    SalesHeader? existingSale = await context.SalesHeaders
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.CheckoutToken == header.CheckoutToken.Value);

                    if (existingSale != null)
                    {
                        await transaction.RollbackAsync();
                        return await LoadSavedReceiptAsync(context, existingSale.Id);
                    }
                }

                await ValidateShiftAsync(context, header.ShiftSessionId);

                if (header.CheckoutToken.HasValue && header.CheckoutToken.Value != Guid.Empty)
                {
                    cartSession = await context.CashierCartSessions
                        .Include(c => c.Lines)
                        .FirstOrDefaultAsync(c => c.CartToken == header.CheckoutToken.Value);

                    if (cartSession == null)
                        throw new InvalidOperationException("The active cashier cart could not be found for checkout.");

                    if (!cartSession.Status.Equals(CashierCartStatusCodes.Active, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"Cart {cartSession.ReferenceNo} is {cartSession.Status} and cannot be checked out.");

                    if (cartSession.ShiftSessionId != header.ShiftSessionId ||
                        !cartSession.TerminalNo.Equals(header.TerminalNo, StringComparison.OrdinalIgnoreCase) ||
                        !cartSession.CashierName.Equals(header.CashierName, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("The checkout cart belongs to a different terminal, shift or cashier.");
                    }
                }

                header.TransactionDate = now;
                header.Status = "Completed";
                header.IsVoided = false;

                await ApplyCustomerSnapshotAsync(context, header);
                await ApplyStoreTaxSnapshotAsync(context, header);
                await ValidateAndApplyFreeIssueRulesAsync(context, header, lines);
                await ValidateApprovalIdentitiesAsync(context, lines);

                RecalculateHeaderTotals(header, lines);

                int[] variantIds = lines
                    .Where(line => !line.IsGiftVoucherSale)
                    .Select(line => line.ItemVariantId ?? 0)
                    .Where(id => id > 0)
                    .Distinct()
                    .ToArray();

                Dictionary<int, SalesTaxProfile> taxProfiles =
                    await _salesTaxService.ResolveProfilesAsync(
                        context,
                        variantIds,
                        header.TransactionDate);

                ApplySalesTaxSnapshots(
                    header,
                    lines,
                    taxProfiles);

                ValidatePaymentTotals(header, payments);
                ValidateCashTendering(header, payments);
                CustomerCreditPlan? customerCreditPlan =
                    await ValidateCustomerCreditAsync(
                        context,
                        header,
                        payments);

                DocumentSequence sequence =
                    await GetOrCreateInvoiceSequenceAsync(context);

                header.InvoiceNo =
                    $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString().PadLeft(sequence.PaddingLength, '0')}";

                sequence.NextSequenceNumber++;
                sequence.UpdatedAt = now;

                await context.SalesHeaders.AddAsync(header);
                await context.SaveChangesAsync();

                foreach (SalesLine line in lines)
                {
                    line.SalesHeaderId = header.Id;
                    line.CreatedAt = now;

                    if (line.IsGiftVoucherSale)
                    {
                        PrepareGiftVoucherSaleLine(line);
                        await context.SalesLines.AddAsync(line);
                        continue;
                    }

                    if (!line.ItemVariantId.HasValue ||
                        line.ItemVariantId.Value <= 0)
                    {
                        throw new InvalidOperationException(
                            "Sale line has no selected item variant.");
                    }

                    if (!taxProfiles.TryGetValue(
                            line.ItemVariantId.Value,
                            out SalesTaxProfile? taxProfile))
                    {
                        throw new InvalidOperationException(
                            $"Tax profile was not resolved for item variant {line.ItemVariantId.Value}.");
                    }

                    if (string.Equals(
                            taxProfile.ItemType,
                            ItemTypeCodes.Service,
                            StringComparison.Ordinal))
                    {
                        if (line.ItemBatchId.HasValue &&
                            line.ItemBatchId.Value > 0)
                        {
                            throw new InvalidOperationException(
                                "Service sale line cannot contain a stock batch.");
                        }

                        ItemVariant? serviceVariant =
                            await context.ItemVariants
                                .Include(variant => variant.ItemParent)
                                .FirstOrDefaultAsync(
                                    variant =>
                                        variant.Id ==
                                        line.ItemVariantId.Value);

                        if (serviceVariant == null)
                        {
                            throw new InvalidOperationException(
                                $"Service item variant was not found. Variant ID: {line.ItemVariantId.Value}");
                        }

                        ValidateServiceForSale(
                            serviceVariant,
                            line);

                        PrepareServiceSaleLineForPersistence(
                            serviceVariant,
                            line,
                            header.IsWholesaleSale);

                        await context.SalesLines.AddAsync(line);
                        continue;
                    }

                    if (!string.Equals(
                            taxProfile.ItemType,
                            ItemTypeCodes.StockItem,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Unsupported sale item type '{taxProfile.ItemType}'.");
                    }

                    if (!line.ItemBatchId.HasValue ||
                        line.ItemBatchId.Value <= 0)
                    {
                        throw new InvalidOperationException(
                            "Stock Item sale line has no selected batch.");
                    }

                    ItemBatch? batch =
                        await context.ItemBatches
                            .Include(itemBatch =>
                                itemBatch.ItemVariant)
                                .ThenInclude(variant =>
                                    variant.ItemParent)
                            .FirstOrDefaultAsync(
                                itemBatch =>
                                    itemBatch.Id ==
                                    line.ItemBatchId.Value);

                    if (batch == null)
                    {
                        throw new InvalidOperationException(
                            $"Item batch was not found. Batch ID: {line.ItemBatchId}");
                    }

                    ValidateBatchForSale(batch, line);
                    PrepareProductSaleLineForPersistence(
                        batch,
                        line,
                        header.IsWholesaleSale);

                    batch.CurrentStock = Math.Round(
                        batch.CurrentStock -
                        line.Quantity,
                        3);

                    await context.SalesLines.AddAsync(line);
                }

                await context.SaveChangesAsync();

                await CreateDiscountAuditRowsAsync(
                    context,
                    header,
                    lines,
                    now);

                await context.SaveChangesAsync();

                foreach (SalesPayment payment in payments)
                {
                    payment.SalesHeaderId = header.Id;
                    payment.CreatedAt = now;
                    payment.EnteredBy = header.CashierName;
                    payment.TerminalNo = header.TerminalNo;

                    if (payment.TenderedAmount <= 0m)
                        payment.TenderedAmount = payment.Amount;

                    if (payment.ChangeAmount < 0m)
                        payment.ChangeAmount = 0m;

                    if (payment.PaymentDate == null)
                        payment.PaymentDate = now;

                    await context.SalesPayments.AddAsync(payment);
                }

                await context.SaveChangesAsync();

                if (customerCreditPlan != null)
                {
                    await PostCustomerCreditAsync(
                        context,
                        header,
                        customerCreditPlan,
                        now);
                }

                await ProcessSoldGiftVoucherLinesAsync(
                    context,
                    header,
                    lines);

                await ProcessGiftVoucherRedemptionsAsync(
                    context,
                    header,
                    payments);

                await ProcessFreeItemSupplierClaimsAsync(
                    context,
                    header,
                    lines);

                await context.SaveChangesAsync();

                foreach (SalesLine line in lines.Where(
                             line =>
                                 !line.IsGiftVoucherSale &&
                                 string.Equals(
                                     line.ItemTypeSnapshot,
                                     ItemTypeCodes.StockItem,
                                     StringComparison.Ordinal)))
                {
                    if (!line.ItemVariantId.HasValue ||
                        !line.ItemBatchId.HasValue)
                    {
                        throw new InvalidOperationException(
                            "Stock Item sale line is missing its stock reference.");
                    }

                    var inventoryTransaction =
                        new InventoryTransaction
                        {
                            ItemVariantId =
                                line.ItemVariantId.Value,
                            ItemBatchId =
                                line.ItemBatchId.Value,
                            TransactionDate =
                                header.TransactionDate,
                            TransactionType =
                                "SALE",
                            ReferenceDocument =
                                header.InvoiceNo,
                            ReferenceLineId =
                                line.Id,
                            Quantity =
                                -line.Quantity,
                            UnitCost =
                                line.CostPrice,
                            CreatedBy =
                                header.CashierName,
                            Remarks =
                                line.IsFreeItem
                                    ? $"Free issue sale invoice {header.InvoiceNo} / Batch {line.BatchNo} / {line.FreeIssueType}"
                                    : $"Sale invoice {header.InvoiceNo} / Batch {line.BatchNo}"
                        };

                    await context.InventoryTransactions.AddAsync(
                        inventoryTransaction);
                }

                if (cartSession != null)
                {
                    cartSession.Status = CashierCartStatusCodes.Completed;
                    cartSession.SalesHeaderId = header.Id;
                    cartSession.CompletedAtUtc = DateTime.UtcNow;
                    cartSession.UpdatedAtUtc = cartSession.CompletedAtUtc.Value;
                    cartSession.UpdatedBy = header.CashierName;
                    cartSession.Revision++;
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return await LoadSavedReceiptAsync(
                    context,
                    header.Id);
            }
            catch (DbUpdateException) when (header.CheckoutToken.HasValue && header.CheckoutToken.Value != Guid.Empty)
            {
                await transaction.RollbackAsync();

                await using AppDbContext retryContext = await _contextFactory.CreateDbContextAsync();
                SalesHeader? existingSale = await retryContext.SalesHeaders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.CheckoutToken == header.CheckoutToken.Value);

                if (existingSale != null)
                    return await LoadSavedReceiptAsync(retryContext, existingSale.Id);

                throw;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static async Task<CustomerCreditPlan?> ValidateCustomerCreditAsync(
            AppDbContext context,
            SalesHeader header,
            IReadOnlyCollection<SalesPayment> payments)
        {
            decimal creditAmount = Money(payments
                .Where(payment => CustomerCreditCodes.IsCustomerCreditPayment(payment.PaymentType))
                .Sum(payment => payment.Amount));

            if (creditAmount <= 0m)
                return null;

            if (!header.CustomerMasterId.HasValue || header.CustomerMasterId.Value <= 0)
                throw new InvalidOperationException("Customer Credit requires a selected customer account.");

            CustomerMaster customer = await context.CustomerMasters
                .FirstOrDefaultAsync(row => row.Id == header.CustomerMasterId.Value)
                ?? throw new InvalidOperationException("Selected customer account was not found.");

            if (!customer.IsActive)
                throw new InvalidOperationException("Selected customer account is inactive.");
            if (!customer.IsCreditEnabled)
                throw new InvalidOperationException("Credit is not enabled for the selected customer.");
            if (!string.Equals(customer.CreditStatus, "Active", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Customer credit status is {customer.CreditStatus}.");
            if (customer.IsCreditLocked)
                throw new InvalidOperationException("Customer credit account is locked.");
            if (customer.CreditLimit <= 0m)
                throw new InvalidOperationException("Customer credit limit is not configured.");

            decimal available = Money(Math.Max(0m, customer.CreditLimit - customer.CurrentBalance));
            if (creditAmount > available)
            {
                throw new InvalidOperationException(
                    $"Customer Credit amount exceeds available credit of Rs. {available:N2}.");
            }

            return new CustomerCreditPlan
            {
                Customer = customer,
                Amount = creditAmount,
                DueDate = header.TransactionDate.Date.AddDays(Math.Max(0, customer.CreditDays))
            };
        }

        private static async Task PostCustomerCreditAsync(
            AppDbContext context,
            SalesHeader header,
            CustomerCreditPlan plan,
            DateTime now)
        {
            bool alreadyPosted = await context.CustomerLedgers
                .AnyAsync(row => row.SalesHeaderId == header.Id);
            if (alreadyPosted)
                return;

            context.CustomerLedgers.Add(new CustomerLedger
            {
                CustomerMasterId = plan.Customer.Id,
                SalesHeaderId = header.Id,
                TransactionDate = header.TransactionDate,
                DocumentRef = header.InvoiceNo,
                TransactionType = CustomerCreditCodes.CreditSale,
                DebitAmount = plan.Amount,
                CreditAmount = 0m,
                DueDate = plan.DueDate,
                OriginalAmount = plan.Amount,
                AllocatedAmount = 0m,
                OutstandingAmount = plan.Amount,
                Status = plan.DueDate.Date < now.Date
                    ? CustomerCreditCodes.Overdue
                    : CustomerCreditCodes.Open,
                ProcessedBy = header.CashierName,
                Remarks = $"Customer Credit portion of invoice {header.InvoiceNo}"
            });

            plan.Customer.CurrentBalance = Money(plan.Customer.CurrentBalance + plan.Amount);
            plan.Customer.UpdatedAt = now;
            plan.Customer.UpdatedBy = header.CashierName;

            await context.SaveChangesAsync();
        }

        // =========================================================
        // CUSTOMER SNAPSHOT
        // =========================================================

        private static async Task ApplyCustomerSnapshotAsync(AppDbContext context, SalesHeader header)
        {
            if (!header.CustomerMasterId.HasValue || header.CustomerMasterId.Value <= 0)
            {
                header.CustomerMasterId = null;
                header.CustomerCode = string.Empty;

                header.CustomerName = string.IsNullOrWhiteSpace(header.CustomerName)
                    ? "Walk-In"
                    : NormalizeText(header.CustomerName);

                header.CustomerCompanyName = string.Empty;
                header.CustomerPhone = NormalizeText(header.CustomerPhone);
                header.CustomerType = "Walk-In";
                header.CustomerNicOrBrNumber = string.Empty;
                header.CustomerIsDiscountEligible = false;
                header.CustomerIsCreditEnabled = false;
                header.CustomerCreditStatus = "None";
                header.IsWholesaleSale = false;
                header.CustomerTinSnapshot = string.Empty;
                header.CustomerVatNoSnapshot = string.Empty;
                header.CustomerAddressSnapshot = string.Empty;

                return;
            }

            var customer = await context.CustomerMasters
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == header.CustomerMasterId.Value);

            if (customer == null)
                throw new InvalidOperationException("Selected customer was not found.");

            if (!customer.IsActive)
                throw new InvalidOperationException("Selected customer account is inactive.");

            bool isWholesale = customer.CustomerType.Equals("Wholesale", StringComparison.OrdinalIgnoreCase);

            string invoiceCustomerName = isWholesale && !string.IsNullOrWhiteSpace(customer.CompanyName)
                ? customer.CompanyName
                : customer.FullName;

            string nicOrBr = isWholesale
                ? customer.BusinessRegistrationNumber
                : customer.NicNumber;

            if (isWholesale && string.IsNullOrWhiteSpace(nicOrBr))
                nicOrBr = customer.VatRegistrationNumber;

            header.CustomerCode = NormalizeText(customer.CustomerCode);
            header.CustomerName = string.IsNullOrWhiteSpace(invoiceCustomerName)
                ? "Walk-In"
                : NormalizeText(invoiceCustomerName);

            header.CustomerCompanyName = NormalizeText(customer.CompanyName);
            header.CustomerPhone = NormalizeText(customer.Phone);
            header.CustomerType = isWholesale ? "Wholesale" : "Retail";
            header.CustomerNicOrBrNumber = NormalizeText(nicOrBr);
            header.CustomerIsDiscountEligible = customer.IsDiscountEligible;
            header.CustomerIsCreditEnabled = customer.IsCreditEnabled;
            header.CustomerCreditStatus = string.IsNullOrWhiteSpace(customer.CreditStatus)
                ? "None"
                : NormalizeText(customer.CreditStatus);

            header.IsWholesaleSale = isWholesale;
            header.CustomerTinSnapshot = string.Empty;
            header.CustomerVatNoSnapshot =
                NormalizeText(customer.VatRegistrationNumber);
            header.CustomerAddressSnapshot =
                NormalizeText(customer.Address);
        }

        private static async Task ApplyStoreTaxSnapshotAsync(
            AppDbContext context,
            SalesHeader header)
        {
            StoreSettings? storeSettings =
                await context.StoreSettings
                    .AsNoTracking()
                    .Where(settings => settings.IsActive)
                    .OrderBy(settings => settings.Id)
                    .FirstOrDefaultAsync();

            header.DocumentType = "Receipt";
            header.TaxInvoiceNo = null;

            if (storeSettings == null)
            {
                header.IsVatRegisteredSale = false;
                header.SupplierTinSnapshot = string.Empty;
                header.SupplierVatNoSnapshot = string.Empty;
                return;
            }

            header.IsVatRegisteredSale =
                storeSettings.IsVatRegistered;

            header.SupplierTinSnapshot =
                NormalizeText(
                    storeSettings.TaxpayerIdentificationNumber);

            header.SupplierVatNoSnapshot =
                NormalizeText(
                    storeSettings.VatRegistrationNumber);
        }

        private void ApplySalesTaxSnapshots(
            SalesHeader header,
            IReadOnlyList<SalesLine> lines,
            IReadOnlyDictionary<int, SalesTaxProfile> taxProfiles)
        {
            var normalLines = lines
                .Select((line, index) => new
                {
                    Line = line,
                    LineKey = index
                })
                .Where(row =>
                    !row.Line.IsGiftVoucherSale &&
                    !row.Line.IsFreeItem)
                .ToList();

            foreach (SalesLine line in lines)
            {
                if (line.IsGiftVoucherSale)
                {
                    ClearTaxSnapshotForSpecialLine(line);
                    continue;
                }

                if (!line.ItemVariantId.HasValue ||
                    line.ItemVariantId.Value <= 0)
                {
                    throw new InvalidOperationException(
                        "Sale item variant is required for tax calculation.");
                }

                if (!taxProfiles.TryGetValue(
                        line.ItemVariantId.Value,
                        out SalesTaxProfile? profile))
                {
                    throw new InvalidOperationException(
                        $"Tax profile was not resolved for item variant {line.ItemVariantId.Value}.");
                }

                line.ItemTypeSnapshot = profile.ItemType;

                if (line.IsFreeItem)
                {
                    ApplyZeroValueFreeIssueTaxSnapshot(
                        line,
                        profile);

                    continue;
                }
            }

            if (normalLines.Count == 0)
            {
                if (lines.All(line => line.IsGiftVoucherSale || line.IsFreeItem))
                {
                    header.TaxableAmountTotal = 0m;
                    header.TotalVatAmount = 0m;
                    header.StandardRatedAmount = 0m;
                    header.ZeroRatedAmount = 0m;
                    header.ExemptAmount = 0m;
                    header.OutOfScopeAmount = 0m;
                    header.TaxSnapshotStatus = TaxSnapshotStatuses.Complete;
                    return;
                }

                ClearHeaderTaxSnapshot(header);
                return;
            }

            var calculationInputs = normalLines
                .Select(row =>
                {
                    SalesLine line = row.Line;
                    SalesTaxProfile profile =
                        taxProfiles[line.ItemVariantId!.Value];

                    return new SalesTaxLineInput
                    {
                        LineKey = row.LineKey,
                        ItemVariantId =
                            line.ItemVariantId.Value,
                        Quantity =
                            line.Quantity,
                        VatInclusiveUnitPrice =
                            line.UnitPrice,
                        LineDiscountAmount =
                            line.DiscountAmount,
                        TaxProfile =
                            profile
                    };
                })
                .ToList();

            SalesTaxDocumentResult result =
                _salesTaxService.CalculateDocument(
                    calculationInputs,
                    invoiceDiscount:
                        header.InvoiceDiscountAmount,
                    isVatRegisteredSale:
                        header.IsVatRegisteredSale);

            foreach (SalesTaxLineResult lineResult in result.Lines)
            {
                SalesLine line =
                    lines[lineResult.LineKey];

                decimal expectedFinalLineTotal =
                    Math.Round(
                        line.LineTotal -
                        lineResult.InvoiceDiscountAllocation,
                        2);

                if (Math.Abs(
                        expectedFinalLineTotal -
                        lineResult.TaxInclusiveAmount) > 0.01m)
                {
                    throw new InvalidOperationException(
                        $"Saved line total does not reconcile with VAT calculation for '{line.ItemDescription}'.");
                }

                line.DiscountAmount = Math.Round(
                    lineResult.LineDiscountAmount +
                    lineResult.InvoiceDiscountAllocation,
                    2);

                line.LineTotal =
                    lineResult.TaxInclusiveAmount;

                line.ProfitAmount = Math.Round(
                    line.LineTotal -
                    (line.CostPrice * line.Quantity),
                    2);

                ApplyCompleteTaxSnapshot(
                    line,
                    lineResult);
            }

            decimal merchandiseGross = Math.Round(
                header.GrossTotal - header.GiftVoucherIssueTotal,
                2);
            decimal merchandiseNet = Math.Round(
                header.NetTotal - header.GiftVoucherIssueTotal,
                2);

            if (Math.Abs(merchandiseGross - result.GrossTotal) > 0.01m ||
                Math.Abs(header.TotalDiscount - result.TotalDiscount) > 0.01m ||
                Math.Abs(merchandiseNet - result.NetTotal) > 0.01m)
            {
                throw new InvalidOperationException(
                    "Merchandise totals do not reconcile with the shared VAT calculation.");
            }

            header.TaxableAmountTotal =
                result.TaxableAmountTotal;

            header.TotalVatAmount =
                result.TotalVat;

            header.StandardRatedAmount =
                result.StandardRatedAmount;

            header.ZeroRatedAmount =
                result.ZeroRatedAmount;

            header.ExemptAmount =
                result.ExemptAmount;

            header.OutOfScopeAmount =
                result.OutOfScopeAmount;

            header.TaxSnapshotStatus =
                TaxSnapshotStatuses.Complete;
        }

        private static void ApplyCompleteTaxSnapshot(
            SalesLine line,
            SalesTaxLineResult result)
        {
            SalesTaxProfile profile =
                result.TaxProfile;

            line.ItemTypeSnapshot =
                profile.ItemType;

            line.TaxCategoryId =
                profile.TaxCategoryId;

            line.TaxRateId =
                profile.TaxRateId;

            line.TaxCategoryCodeSnapshot =
                profile.TaxCategoryCode;

            line.TaxCodeSnapshot =
                profile.TaxCode;

            line.TaxNameSnapshot =
                profile.TaxName;

            line.TaxRatePercentSnapshot =
                profile.RatePercent;

            line.IsTaxInclusiveSnapshot =
                true;

            line.TaxableAmountSnapshot =
                result.TaxableAmount;

            line.VatAmountSnapshot =
                result.VatAmount;

            line.TaxInclusiveAmountSnapshot =
                result.TaxInclusiveAmount;

            line.TaxSnapshotStatus =
                TaxSnapshotStatuses.Complete;
        }

        private static void ApplyZeroValueFreeIssueTaxSnapshot(
            SalesLine line,
            SalesTaxProfile profile)
        {
            line.ItemTypeSnapshot =
                profile.ItemType;

            line.TaxCategoryId =
                profile.TaxCategoryId;

            line.TaxRateId =
                profile.TaxRateId;

            line.TaxCategoryCodeSnapshot =
                profile.TaxCategoryCode;

            line.TaxCodeSnapshot =
                profile.TaxCode;

            line.TaxNameSnapshot =
                profile.TaxName;

            line.TaxRatePercentSnapshot =
                profile.RatePercent;

            line.IsTaxInclusiveSnapshot =
                true;

            line.TaxableAmountSnapshot = 0m;
            line.VatAmountSnapshot = 0m;
            line.TaxInclusiveAmountSnapshot = 0m;
            line.TaxSnapshotStatus = TaxSnapshotStatuses.Complete;
        }

        private static void ClearTaxSnapshotForSpecialLine(
            SalesLine line)
        {
            line.ItemTypeSnapshot = null;
            line.TaxCategoryId = null;
            line.TaxRateId = null;
            line.TaxCategoryCodeSnapshot = null;
            line.TaxCodeSnapshot = null;
            line.TaxNameSnapshot = null;
            line.TaxRatePercentSnapshot = null;
            line.IsTaxInclusiveSnapshot = null;
            line.TaxableAmountSnapshot = null;
            line.VatAmountSnapshot = null;
            line.TaxInclusiveAmountSnapshot = null;
            line.TaxSnapshotStatus =
                TaxSnapshotStatuses.LegacyUnknown;
        }

        private static void ClearHeaderTaxSnapshot(
            SalesHeader header)
        {
            header.TaxableAmountTotal = null;
            header.TotalVatAmount = null;
            header.StandardRatedAmount = null;
            header.ZeroRatedAmount = null;
            header.ExemptAmount = null;
            header.OutOfScopeAmount = null;
            header.TaxSnapshotStatus =
                TaxSnapshotStatuses.LegacyUnknown;
        }

        // =========================================================
        // VALIDATION / SEQUENCES
        // =========================================================

        private static async Task ValidateShiftAsync(AppDbContext context, int shiftSessionId)
        {
            if (shiftSessionId <= 0)
                throw new InvalidOperationException("No active shift found.");

            ShiftSession? shift = await context.ShiftSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == shiftSessionId);

            if (shift == null)
                throw new InvalidOperationException("Active shift session was not found.");

            if (!string.Equals(
                    shift.Status,
                    ShiftStatusCodes.Open,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Shift {shift.Id} is {shift.Status} and cannot accept new sales.");
            }
        }

        private static async Task<DocumentSequence> GetOrCreateInvoiceSequenceAsync(AppDbContext context)
        {
            var sequence = await context.DocumentSequences
                .FirstOrDefaultAsync(s => s.DocumentType == "INV");

            if (sequence != null)
                return sequence;

            sequence = new DocumentSequence
            {
                DocumentType = "INV",
                Prefix = "INV-",
                NextSequenceNumber = 1,
                PaddingLength = 6,
                UpdatedAt = DateTime.Now
            };

            await context.DocumentSequences.AddAsync(sequence);

            return sequence;
        }

        private static void ValidateBatchForSale(
            ItemBatch batch,
            SalesLine line)
        {
            if (batch.IsDeactivated)
                throw new InvalidOperationException($"Batch '{batch.BatchNo}' is inactive.");

            if (batch.CurrentStock <= 0m)
                throw new InvalidOperationException($"Batch '{batch.BatchNo}' has no stock.");

            if (line.Quantity <= 0m)
                throw new InvalidOperationException("Sale quantity must be greater than zero.");

            if (batch.CurrentStock < line.Quantity)
            {
                throw new InvalidOperationException(
                    $"Not enough stock in batch '{batch.BatchNo}'. Available: {batch.CurrentStock:N3}, Required: {line.Quantity:N3}");
            }

            if (batch.ExpiryDate.HasValue &&
                batch.ExpiryDate.Value.Date < DateTime.Today)
            {
                throw new InvalidOperationException(
                    $"Batch '{batch.BatchNo}' is expired and cannot be sold.");
            }

            if (batch.ItemVariant == null)
                throw new InvalidOperationException("Item variant was not found for selected batch.");

            if (batch.ItemVariant.IsDeactivated)
                throw new InvalidOperationException("Selected item variant is inactive.");

            if (batch.ItemVariant.ItemParent == null)
                throw new InvalidOperationException("Item parent was not found for selected batch.");

            if (batch.ItemVariant.ItemParent.IsDeactivated)
                throw new InvalidOperationException("Selected item is inactive.");

            if (batch.ItemVariant.ItemParent.IsSaleLocked)
                throw new InvalidOperationException("Selected item is locked for sale.");

            if (!string.Equals(
                    batch.ItemVariant.ItemParent.ItemType,
                    ItemTypeCodes.StockItem,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Only a Stock Item can be sold from an inventory batch.");
            }

            if (line.ItemVariantId.HasValue &&
                line.ItemVariantId.Value > 0 &&
                line.ItemVariantId.Value != batch.ItemVariantId)
            {
                throw new InvalidOperationException(
                    "Cart item variant does not match selected batch.");
            }
        }

        private static void ValidateServiceForSale(
            ItemVariant variant,
            SalesLine line)
        {
            if (variant.IsDeactivated)
                throw new InvalidOperationException("Selected service variant is inactive.");

            if (variant.ItemParent == null)
                throw new InvalidOperationException("Service item parent was not found.");

            if (variant.ItemParent.IsDeactivated)
                throw new InvalidOperationException("Selected service is inactive.");

            if (variant.ItemParent.IsSaleLocked)
                throw new InvalidOperationException("Selected service is locked for sale.");

            if (!string.Equals(
                    variant.ItemParent.ItemType,
                    ItemTypeCodes.Service,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Selected item is not configured as a Service.");
            }

            if (line.ItemBatchId.HasValue &&
                line.ItemBatchId.Value > 0)
            {
                throw new InvalidOperationException(
                    "Service sale line cannot contain a stock batch.");
            }

            if (line.ItemVariantId.HasValue &&
                line.ItemVariantId.Value > 0 &&
                line.ItemVariantId.Value != variant.Id)
            {
                throw new InvalidOperationException(
                    "Cart service variant does not match the selected Service.");
            }
        }

        private sealed class CustomerCreditPlan
        {
            public CustomerMaster Customer { get; init; } = null!;
            public decimal Amount { get; init; }
            public DateTime DueDate { get; init; }
        }

        // =========================================================
        // NORMALIZATION
        // =========================================================

        private static void NormalizeSalesHeader(SalesHeader header)
        {
            header.TerminalNo = NormalizeText(header.TerminalNo);
            header.CashierName = NormalizeText(header.CashierName);

            header.CustomerCode = NormalizeText(header.CustomerCode);

            header.CustomerName = string.IsNullOrWhiteSpace(header.CustomerName)
                ? "Walk-In"
                : NormalizeText(header.CustomerName);

            header.CustomerCompanyName = NormalizeText(header.CustomerCompanyName);
            header.CustomerPhone = NormalizeText(header.CustomerPhone);

            header.CustomerType = string.IsNullOrWhiteSpace(header.CustomerType)
                ? "Walk-In"
                : NormalizeCustomerTypeSnapshot(header.CustomerType);

            header.CustomerNicOrBrNumber = NormalizeText(header.CustomerNicOrBrNumber);

            header.CustomerCreditStatus = string.IsNullOrWhiteSpace(header.CustomerCreditStatus)
                ? "None"
                : NormalizeText(header.CustomerCreditStatus);

            header.PaymentMethod = string.IsNullOrWhiteSpace(header.PaymentMethod)
                ? "Split"
                : NormalizeText(header.PaymentMethod);

            header.InvoiceDiscountAmount = Math.Round(
                header.InvoiceDiscountAmount,
                2);

            if (header.InvoiceDiscountAmount < 0m)
            {
                throw new InvalidOperationException(
                    "Invoice discount cannot be negative.");
            }

            if (string.IsNullOrWhiteSpace(header.TerminalNo))
                throw new InvalidOperationException("Terminal number is required.");

            if (string.IsNullOrWhiteSpace(header.CashierName))
                throw new InvalidOperationException("Cashier name is required.");
        }

        private static void NormalizeSalesLines(List<SalesLine> lines)
        {
            foreach (var line in lines)
            {
                if (line.IsGiftVoucherSale)
                {
                    NormalizeGiftVoucherSaleLine(line);
                    continue;
                }

                if (!line.ItemVariantId.HasValue || line.ItemVariantId.Value <= 0)
                    throw new InvalidOperationException("Every item or service sale line must have a selected item variant.");

                if (line.Quantity <= 0m)
                    throw new InvalidOperationException("Sale quantity must be greater than zero.");

                if (line.UnitPrice < 0m)
                    throw new InvalidOperationException("Unit price cannot be negative.");

                line.SkuCode = NormalizeText(line.SkuCode);
                line.Barcode = NormalizeText(line.Barcode);

                line.ItemDescription = string.IsNullOrWhiteSpace(line.ItemDescription)
                    ? "Unknown Item"
                    : NormalizeText(line.ItemDescription);

                line.BatchNo = NormalizeText(line.BatchNo);

                line.Uom = string.IsNullOrWhiteSpace(line.Uom)
                    ? "PCS"
                    : NormalizeText(line.Uom);

                if (line.IsFreeItem)
                {
                    NormalizeFreeIssueLine(line);
                    continue;
                }

                ValidateAndRecalculateNormalSaleLine(line);
            }
        }

        private static void ValidateAndRecalculateNormalSaleLine(SalesLine line)
        {
            if (line.IsGiftVoucherSale)
                throw new InvalidOperationException("Gift voucher sale line cannot be processed as normal product line.");

            if (line.IsFreeItem)
                throw new InvalidOperationException("Free item line cannot be processed as normal discount line.");

            if (line.Quantity <= 0m)
                throw new InvalidOperationException("Sale quantity must be greater than zero.");

            if (line.UnitPrice < 0m)
                throw new InvalidOperationException("Unit price cannot be negative.");

            if (line.DiscountPercentage < 0m || line.DiscountPercentage > 100m)
                throw new InvalidOperationException("Discount percentage must be between 0 and 100.");

            if (line.ManualDiscountAmount < 0m)
                throw new InvalidOperationException("Manual discount amount cannot be negative.");

            line.DiscountPercentage = Math.Round(line.DiscountPercentage, 2);
            line.ManualDiscountAmount = Math.Round(line.ManualDiscountAmount, 2);

            bool hasPercentDiscount = line.DiscountPercentage > 0m;
            bool hasAmountDiscount = line.ManualDiscountAmount > 0m;

            bool isRuleDiscount =
                line.IsRuleDiscount ||
                line.DiscountMode.Equals("Rule", StringComparison.OrdinalIgnoreCase);

            line.IsRuleDiscount = isRuleDiscount;

            if (hasPercentDiscount && hasAmountDiscount)
            {
                throw new InvalidOperationException(
                    "A sale line cannot have both rupee discount and percentage discount. Use one discount type only.");
            }

            if (line.IsPriceOverridden && (hasPercentDiscount || hasAmountDiscount || line.DiscountAmount > 0m || isRuleDiscount))
            {
                throw new InvalidOperationException(
                    "New Price and discount cannot be applied to the same sale line.");
            }

            if (isRuleDiscount)
            {
                ValidateRuleDiscountSnapshot(line);
            }
            else
            {
                ClearDiscountRuleSnapshot(line);
            }

            if (line.IsPriceOverridden)
            {
                if (line.OriginalUnitPrice <= 0m)
                    throw new InvalidOperationException("Original unit price is required for New Price override.");

                line.OriginalUnitPrice = Math.Round(line.OriginalUnitPrice, 2);
                line.PriceOverrideAmount = Math.Round(line.OriginalUnitPrice - line.UnitPrice, 2);
                line.PriceOverrideApprovedBy = NormalizeText(line.PriceOverrideApprovedBy);
            }
            else
            {
                if (line.OriginalUnitPrice <= 0m)
                    line.OriginalUnitPrice = Math.Round(line.UnitPrice, 2);

                line.PriceOverrideAmount = 0m;
                line.PriceOverrideApprovedBy = string.Empty;
                line.PriceOverrideApprovedAt = null;
            }

            line.GrossAmount = Math.Round(line.Quantity * line.UnitPrice, 2);

            decimal percentDiscountAmount = hasPercentDiscount
                ? Math.Round(line.GrossAmount * (line.DiscountPercentage / 100m), 2)
                : 0m;

            decimal totalDiscount = Math.Round(percentDiscountAmount + line.ManualDiscountAmount, 2);

            if (totalDiscount < 0m)
                totalDiscount = 0m;

            if (totalDiscount > line.GrossAmount)
            {
                throw new InvalidOperationException(
                    $"Discount cannot exceed line gross amount for item '{line.ItemDescription}'.");
            }

            line.DiscountAmount = totalDiscount;

            if (isRuleDiscount)
            {
                line.DiscountMode = "Rule";
                line.IsManualDiscount = true;
            }
            else if (hasAmountDiscount)
            {
                line.DiscountMode = "Amount";
                line.IsManualDiscount = true;
            }
            else if (hasPercentDiscount)
            {
                line.DiscountMode = "Percent";
                line.IsManualDiscount = true;
            }
            else
            {
                line.DiscountMode = "None";
                line.IsManualDiscount = false;
                line.DiscountAmount = 0m;
                line.ManualDiscountAmount = 0m;
                line.DiscountPercentage = 0m;
            }

            line.DiscountMode = NormalizeDiscountMode(line.DiscountMode);

            line.LineTotal = Math.Round(line.GrossAmount - line.DiscountAmount, 2);

            if (line.LineTotal < 0m)
                line.LineTotal = 0m;

            line.ProfitAmount = Math.Round(line.LineTotal - (line.CostPrice * line.Quantity), 2);
        }

        private static string NormalizeDiscountMode(string? value)
        {
            string mode = NormalizeText(value);

            if (mode.Equals("Amount", StringComparison.OrdinalIgnoreCase))
                return "Amount";

            if (mode.Equals("Percent", StringComparison.OrdinalIgnoreCase))
                return "Percent";

            if (mode.Equals("Rule", StringComparison.OrdinalIgnoreCase))
                return "Rule";

            return "None";
        }

        private static void ValidateRuleDiscountSnapshot(SalesLine line)
        {
            if (!line.IsRuleDiscount)
                return;

            if (line.IsGiftVoucherSale)
                throw new InvalidOperationException("Discount rule cannot be applied to gift voucher sale line.");

            if (line.IsFreeItem)
                throw new InvalidOperationException("Discount rule cannot be applied to free item line.");

            if (line.IsPriceOverridden)
                throw new InvalidOperationException("Discount rule cannot be applied after New Price.");

            if (!line.DiscountRuleId.HasValue || line.DiscountRuleId.Value <= 0)
                throw new InvalidOperationException("Discount rule id is required for rule discount.");

            if (string.IsNullOrWhiteSpace(line.DiscountRuleName))
                throw new InvalidOperationException("Discount rule name is required for rule discount.");

            if (!line.DiscountReasonId.HasValue || line.DiscountReasonId.Value <= 0)
                throw new InvalidOperationException("Discount reason id is required for rule discount.");

            if (string.IsNullOrWhiteSpace(line.DiscountReasonCode))
                throw new InvalidOperationException("Discount reason code is required for rule discount.");

            if (string.IsNullOrWhiteSpace(line.DiscountReasonName))
                throw new InvalidOperationException("Discount reason name is required for rule discount.");

            if (line.DiscountRequiresManagerApproval && string.IsNullOrWhiteSpace(line.DiscountApprovedBy))
                throw new InvalidOperationException("Manager approval is required for this discount rule.");

            if (line.DiscountRequiresAdminApproval && string.IsNullOrWhiteSpace(line.DiscountApprovedBy))
                throw new InvalidOperationException("Admin approval is required for this discount rule.");

            if ((line.DiscountRequiresManagerApproval || line.DiscountRequiresAdminApproval) &&
                !line.DiscountApprovedAt.HasValue)
            {
                line.DiscountApprovedAt = DateTime.Now;
            }

            line.DiscountRuleName = NormalizeText(line.DiscountRuleName);
            line.DiscountReasonCode = NormalizeText(line.DiscountReasonCode).ToUpperInvariant();
            line.DiscountReasonName = NormalizeText(line.DiscountReasonName);
            line.DiscountApprovedBy = NormalizeText(line.DiscountApprovedBy);
        }

        private static void ClearDiscountRuleSnapshot(SalesLine line)
        {
            if (line == null)
                return;

            line.IsRuleDiscount = false;

            line.DiscountRuleId = null;
            line.DiscountRuleName = string.Empty;

            line.DiscountReasonId = null;
            line.DiscountReasonCode = string.Empty;
            line.DiscountReasonName = string.Empty;

            line.DiscountRequiresManagerApproval = false;
            line.DiscountRequiresAdminApproval = false;

            line.DiscountApprovedBy = string.Empty;
            line.DiscountApprovedAt = null;
        }

        private static void NormalizeFreeIssueLine(SalesLine line)
        {
            if (!line.IsFreeItem)
                return;

            if (!line.FreeIssueRuleId.HasValue || line.FreeIssueRuleId.Value <= 0)
                throw new InvalidOperationException("Free issue rule is required for free item line.");

            line.FreeIssueRuleName = NormalizeText(line.FreeIssueRuleName);
            line.FreeIssueType = NormalizeFreeIssueType(line.FreeIssueType);
            line.FreeReasonCode = NormalizeText(line.FreeReasonCode).ToUpperInvariant();
            line.FreeReasonText = NormalizeText(line.FreeReasonText);
            line.FreeApprovedBy = NormalizeText(line.FreeApprovedBy);
            line.FreeApprovedRole = NormalizeText(line.FreeApprovedRole);
            line.FreeIssueAppliedBy = NormalizeText(line.FreeIssueAppliedBy);
            line.FreeIssueRuleSnapshotJson = line.FreeIssueRuleSnapshotJson?.Trim() ?? string.Empty;
            line.FreeIssueSnapshotStatus = string.IsNullOrWhiteSpace(line.FreeIssueSnapshotStatus)
                ? FreeIssueSnapshotStatusCodes.LegacyUnknown
                : NormalizeText(line.FreeIssueSnapshotStatus);
            line.SupplierName = NormalizeText(line.SupplierName);
            line.SupplierPromotionReference = NormalizeText(line.SupplierPromotionReference);
            line.SupplierClaimReferenceNo = NormalizeText(line.SupplierClaimReferenceNo);
            line.SupplierClaimStatus = NormalizeText(line.SupplierClaimStatus);

            line.IsSupplierRecoverable =
                line.FreeIssueType.Equals("SupplierClaim", StringComparison.OrdinalIgnoreCase);

            line.UnitPrice = 0m;

            line.DiscountPercentage = 0m;
            line.DiscountAmount = 0m;
            line.ManualDiscountAmount = 0m;
            line.DiscountMode = "None";
            line.IsManualDiscount = false;

            line.IsPriceOverridden = false;
            line.PriceOverrideAmount = 0m;
            line.PriceOverrideApprovedBy = string.Empty;
            line.PriceOverrideApprovedAt = null;

            ClearDiscountRuleSnapshot(line);

            line.GrossAmount = 0m;
            line.LineTotal = 0m;

            if (line.OriginalUnitPrice < 0m)
                line.OriginalUnitPrice = 0m;

            line.FreeIssueCostValue = Math.Round(line.FreeIssueCostValue, 2);
            line.FreeIssueSellingValue = Math.Round(line.FreeIssueSellingValue, 2);
            line.SupplierClaimValue = Math.Round(line.SupplierClaimValue, 2);

            if (line.IsSupplierRecoverable)
            {
                if (!line.SupplierId.HasValue || line.SupplierId.Value <= 0)
                    throw new InvalidOperationException("Supplier is required for supplier recoverable free item.");

                if (string.IsNullOrWhiteSpace(line.SupplierName))
                    throw new InvalidOperationException("Supplier name is required for supplier recoverable free item.");

                if (line.SupplierClaimValue <= 0m)
                    throw new InvalidOperationException("Supplier claim value must be greater than zero for supplier recoverable free item.");

                if (string.IsNullOrWhiteSpace(line.SupplierClaimStatus))
                    line.SupplierClaimStatus = SupplierClaimStatusCodes.Draft;
            }
            else
            {
                line.SupplierId = null;
                line.SupplierName = string.Empty;
                line.SupplierPromotionReference = string.Empty;
                line.SupplierClaimId = null;
                line.SupplierClaimStatus = string.Empty;
                line.SupplierClaimReferenceNo = string.Empty;
                line.SupplierClaimValue = 0m;
            }
        }

        private static string NormalizeFreeIssueType(string? value) =>
            FreeIssueTypeCodes.Normalize(value);

        private static async Task ValidateApprovalIdentitiesAsync(
            AppDbContext context,
            IReadOnlyCollection<SalesLine> lines)
        {
            string[] approvalNames = lines
                .Where(line => !line.IsGiftVoucherSale && !line.IsFreeItem)
                .SelectMany(line => new[]
                {
                    NormalizeText(line.PriceOverrideApprovedBy),
                    NormalizeText(line.DiscountApprovedBy)
                })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.ToUpperInvariant())
                .Distinct()
                .ToArray();

            Dictionary<string, User> usersByName = approvalNames.Length == 0
                ? new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase)
                : (await context.Users
                    .AsNoTracking()
                    .Where(user => approvalNames.Contains(user.Username.ToUpper()))
                    .ToListAsync())
                    .GroupBy(user => user.Username, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            foreach (SalesLine line in lines.Where(line => !line.IsGiftVoucherSale && !line.IsFreeItem))
            {
                string priceApproverName = NormalizeText(line.PriceOverrideApprovedBy);
                if (!string.IsNullOrWhiteSpace(priceApproverName))
                {
                    if (!usersByName.TryGetValue(priceApproverName, out User? approver) ||
                        approver == null ||
                        !approver.IsActive ||
                        (approver.Role != UserRole.Manager && approver.Role != UserRole.Admin))
                    {
                        throw new InvalidOperationException(
                            $"An active Manager or Administrator must approve the New Price for item '{line.ItemDescription}'.");
                    }

                    line.PriceOverrideApprovedBy = approver.Username;
                }

                if (!line.IsRuleDiscount ||
                    (!line.DiscountRequiresManagerApproval && !line.DiscountRequiresAdminApproval))
                {
                    continue;
                }

                string discountApproverName = NormalizeText(line.DiscountApprovedBy);
                if (!usersByName.TryGetValue(discountApproverName, out User? discountApprover) ||
                    discountApprover == null ||
                    !discountApprover.IsActive)
                {
                    string requiredRole = line.DiscountRequiresAdminApproval
                        ? "Administrator"
                        : "Manager or Administrator";
                    throw new InvalidOperationException(
                        $"An active {requiredRole} must approve the discount for item '{line.ItemDescription}'.");
                }

                bool roleAllowed = line.DiscountRequiresAdminApproval
                    ? discountApprover.Role == UserRole.Admin
                    : discountApprover.Role == UserRole.Manager || discountApprover.Role == UserRole.Admin;

                if (!roleAllowed)
                {
                    string requiredRole = line.DiscountRequiresAdminApproval
                        ? "Administrator"
                        : "Manager or Administrator";
                    throw new InvalidOperationException(
                        $"An active {requiredRole} must approve the discount for item '{line.ItemDescription}'.");
                }

                line.DiscountApprovedBy = discountApprover.Username;
            }
        }

        private static void PrepareProductSaleLineForPersistence(
            ItemBatch batch,
            SalesLine line,
            bool isWholesaleSale)
        {
            line.ItemVariantId = batch.ItemVariantId;
            line.ItemBatchId = batch.Id;
            line.BatchNo = batch.BatchNo;
            line.ExpiryDate = batch.ExpiryDate;
            line.CostPrice = batch.CostPrice;

            if (line.IsFreeItem)
            {
                PrepareFreeIssueProductLine(line);
                return;
            }

            FinalizePreparedNormalSaleLine(
                batch.ItemVariant,
                line,
                isWholesaleSale);
        }

        private static void PrepareServiceSaleLineForPersistence(
            ItemVariant variant,
            SalesLine line,
            bool isWholesaleSale)
        {
            line.ItemVariantId = variant.Id;
            line.ItemBatchId = null;
            line.BatchNo = string.Empty;
            line.ExpiryDate = null;
            line.CostPrice =
                variant.CostPrice > 0m
                    ? variant.CostPrice
                    : variant.AverageCost;

            if (line.IsFreeItem)
            {
                PrepareFreeIssueProductLine(line);
                return;
            }

            FinalizePreparedNormalSaleLine(
                variant,
                line,
                isWholesaleSale);
        }

        private static void FinalizePreparedNormalSaleLine(
            ItemVariant? variant,
            SalesLine line,
            bool isWholesaleSale)
        {
            ValidateCataloguePrice(
                variant,
                line,
                isWholesaleSale);

            if (line.TaxSnapshotStatus ==
                TaxSnapshotStatuses.Complete)
            {
                line.ProfitAmount = Math.Round(
                    line.LineTotal -
                    (line.CostPrice * line.Quantity),
                    2);

                ValidateMinimumPrice(
                    variant,
                    line);
                return;
            }

            ValidateAndRecalculateNormalSaleLine(line);
            ValidateMinimumPrice(
                variant,
                line);
        }

        private static void ValidateCataloguePrice(
            ItemVariant? variant,
            SalesLine line,
            bool isWholesaleSale)
        {
            if (variant == null)
                throw new InvalidOperationException("Current item price could not be verified.");

            decimal currentPrice = Math.Round(
                isWholesaleSale && variant.WholesalePrice > 0m
                    ? variant.WholesalePrice
                    : variant.RetailPrice,
                2);

            if (line.IsPriceOverridden)
            {
                decimal approvedOriginalPrice = Math.Round(line.OriginalUnitPrice, 2);
                if (Math.Abs(approvedOriginalPrice - currentPrice) > 0.01m)
                {
                    throw new InvalidOperationException(
                        $"Catalogue price changed for item '{line.ItemDescription}'. Re-enter and re-approve the New Price.");
                }

                return;
            }

            decimal submittedPrice = Math.Round(line.UnitPrice, 2);
            if (Math.Abs(submittedPrice - currentPrice) > 0.01m)
            {
                throw new InvalidOperationException(
                    $"Catalogue price changed for item '{line.ItemDescription}'. Current price is Rs. {currentPrice:N2}. Refresh the cart before checkout.");
            }
        }

        private static void ValidateMinimumPrice(
            ItemVariant? variant,
            SalesLine line)
        {
            decimal minimumPrice =
                variant?.MinimumPrice ?? 0m;

            if (minimumPrice <= 0m)
                return;

            if (line.Quantity <= 0m)
                throw new InvalidOperationException("Sale quantity must be greater than zero.");

            decimal effectiveUnitPrice = Math.Round(
                line.LineTotal / line.Quantity,
                2);

            if (effectiveUnitPrice >= minimumPrice)
                return;

            if (line.IsPriceOverridden)
            {
                if (string.IsNullOrWhiteSpace(
                        line.PriceOverrideApprovedBy))
                {
                    throw new InvalidOperationException(
                        $"Manager approval is required for price below minimum. Item: '{line.ItemDescription}', Minimum price: Rs. {minimumPrice:N2}.");
                }

                if (!line.PriceOverrideApprovedAt.HasValue)
                    line.PriceOverrideApprovedAt = DateTime.Now;

                return;
            }

            if (line.IsRuleDiscount)
            {
                if (string.IsNullOrWhiteSpace(
                        line.DiscountApprovedBy))
                {
                    throw new InvalidOperationException(
                        $"Manager approval is required because discount sells below minimum price. Item: '{line.ItemDescription}', Minimum price: Rs. {minimumPrice:N2}.");
                }

                if (!line.DiscountApprovedAt.HasValue)
                    line.DiscountApprovedAt = DateTime.Now;

                return;
            }

            throw new InvalidOperationException(
                $"Selling price is below minimum price for item '{line.ItemDescription}'. Minimum price: Rs. {minimumPrice:N2}.");
        }

        private static void PrepareFreeIssueProductLine(SalesLine line)
        {
            if (!line.IsFreeItem)
                return;

            line.FreeIssueType = NormalizeFreeIssueType(line.FreeIssueType);

            if (!line.FreeIssueRuleId.HasValue || line.FreeIssueRuleId.Value <= 0)
                throw new InvalidOperationException("Free issue rule is required for free item line.");

            if (line.Quantity <= 0m)
                throw new InvalidOperationException("Free item quantity must be greater than zero.");

            if (line.OriginalUnitPrice <= 0m)
                line.OriginalUnitPrice = Math.Round(line.UnitPrice, 2);

            line.UnitPrice = 0m;

            line.DiscountPercentage = 0m;
            line.DiscountAmount = 0m;
            line.ManualDiscountAmount = 0m;
            line.DiscountMode = "None";
            line.IsManualDiscount = false;

            line.IsPriceOverridden = false;
            line.PriceOverrideAmount = 0m;
            line.PriceOverrideApprovedBy = string.Empty;
            line.PriceOverrideApprovedAt = null;

            ClearDiscountRuleSnapshot(line);

            line.GrossAmount = 0m;
            line.LineTotal = 0m;

            line.FreeIssueCostValue = line.FreeIssueCostValue > 0m
                ? Math.Round(line.FreeIssueCostValue, 2)
                : Math.Round(line.CostPrice * line.Quantity, 2);

            line.FreeIssueSellingValue = line.FreeIssueSellingValue > 0m
                ? Math.Round(line.FreeIssueSellingValue, 2)
                : Math.Round(line.OriginalUnitPrice * line.Quantity, 2);

            line.ProfitAmount = Math.Round(0m - (line.CostPrice * line.Quantity), 2);

            line.IsSupplierRecoverable =
                line.FreeIssueType.Equals("SupplierClaim", StringComparison.OrdinalIgnoreCase);

            if (line.IsSupplierRecoverable)
            {
                if (!line.SupplierId.HasValue || line.SupplierId.Value <= 0)
                    throw new InvalidOperationException("Supplier is required for supplier recoverable free item.");

                if (line.SupplierClaimValue <= 0m)
                    line.SupplierClaimValue = Math.Round(line.CostPrice * line.Quantity, 2);

                if (string.IsNullOrWhiteSpace(line.SupplierClaimStatus))
                    line.SupplierClaimStatus = SupplierClaimStatusCodes.Draft;

            }
            else
            {
                line.SupplierId = null;
                line.SupplierName = string.Empty;
                line.SupplierPromotionReference = string.Empty;
                line.SupplierClaimId = null;
                line.SupplierClaimStatus = string.Empty;
                line.SupplierClaimReferenceNo = string.Empty;
                line.SupplierClaimValue = 0m;
            }
        }

        // =========================================================
        // GIFT VOUCHER SALE LINE
        // =========================================================

        private static void NormalizeGiftVoucherSaleLine(SalesLine line)
        {
            if (!line.GiftVoucherId.HasValue || line.GiftVoucherId.Value <= 0)
                throw new InvalidOperationException("Gift voucher sale line is missing voucher reference.");

            if (line.Quantity != 1m)
                throw new InvalidOperationException("Gift voucher sale quantity must be 1.");

            if (line.UnitPrice <= 0m)
                throw new InvalidOperationException("Gift voucher sale price must be greater than zero.");

            if (line.DiscountAmount != 0m ||
                line.DiscountPercentage != 0m ||
                line.ManualDiscountAmount != 0m ||
                line.IsManualDiscount ||
                line.IsRuleDiscount ||
                line.DiscountRuleId.HasValue ||
                line.IsPriceOverridden)
            {
                throw new InvalidOperationException("Discount or New Price cannot be applied to gift voucher sale line.");
            }

            line.ItemVariantId = null;
            line.ItemBatchId = null;

            line.SkuCode = "GV-SALE";
            line.Barcode = NormalizeText(line.Barcode);

            line.ItemDescription = string.IsNullOrWhiteSpace(line.ItemDescription)
                ? "Gift Voucher"
                : NormalizeText(line.ItemDescription);

            line.BatchNo = string.Empty;
            line.Uom = string.IsNullOrWhiteSpace(line.Uom)
                ? "VOU"
                : NormalizeText(line.Uom);

            line.CostPrice = 0m;
            line.GrossAmount = Math.Round(line.Quantity * line.UnitPrice, 2);
            line.DiscountPercentage = 0m;
            line.DiscountAmount = 0m;
            line.ManualDiscountAmount = 0m;
            line.DiscountMode = "None";
            line.IsManualDiscount = false;

            line.LineTotal = line.GrossAmount;
            line.ProfitAmount = line.LineTotal;

            line.GiftVoucherNo = NormalizeText(line.GiftVoucherNo);
            line.GiftVoucherBarcode = NormalizeText(line.GiftVoucherBarcode);

            line.IsFreeItem = false;
            line.IsSupplierRecoverable = false;

            line.OriginalUnitPrice = line.UnitPrice;
            line.IsPriceOverridden = false;
            line.PriceOverrideAmount = 0m;
            line.PriceOverrideApprovedBy = string.Empty;
            line.PriceOverrideApprovedAt = null;

            ClearDiscountRuleSnapshot(line);
        }

        private static void PrepareGiftVoucherSaleLine(SalesLine line)
        {
            NormalizeGiftVoucherSaleLine(line);
        }

        private static void NormalizePayments(List<SalesPayment> payments)
        {
            foreach (var payment in payments)
            {
                payment.PaymentType = NormalizeText(payment.PaymentType);
                payment.ReferenceNo = NormalizeText(payment.ReferenceNo);
                payment.BankOrCardType = NormalizeText(payment.BankOrCardType);
                payment.CardLastDigits = NormalizeCardLastDigits(payment.CardLastDigits);
                payment.EnteredBy = NormalizeText(payment.EnteredBy);
                payment.TerminalNo = NormalizeText(payment.TerminalNo);
                payment.TenderedAmount = Math.Round(payment.TenderedAmount, 2);
                payment.ChangeAmount = Math.Round(payment.ChangeAmount, 2);
                payment.GiftVoucherNo = NormalizeText(payment.GiftVoucherNo);
                payment.GiftVoucherBarcode = NormalizeText(payment.GiftVoucherBarcode);
                payment.GiftVoucherAuthorizedBy = NormalizeText(payment.GiftVoucherAuthorizedBy);

                if (string.IsNullOrWhiteSpace(payment.PaymentType))
                    throw new InvalidOperationException("Payment type is required.");

                if (payment.Amount <= 0m)
                    throw new InvalidOperationException("Payment amount must be greater than zero.");

                if (payment.PaymentType.Equals(PaymentTypeCodes.Card, StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrWhiteSpace(payment.ReferenceNo))
                {
                    throw new InvalidOperationException("Card payment reference is required.");
                }

                if (payment.PaymentType.Equals(PaymentTypeCodes.Cheque, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(payment.ReferenceNo))
                        throw new InvalidOperationException("Cheque payment reference is required.");

                    if (string.IsNullOrWhiteSpace(payment.BankOrCardType))
                        throw new InvalidOperationException("Cheque bank or branch is required.");
                }

                if (IsGiftVoucherPayment(payment))
                    ValidateGiftVoucherPaymentBeforeRedeem(payment);
            }
        }

        // =========================================================
        // TOTALS / PAYMENTS
        // =========================================================

        private static void RecalculateHeaderTotals(SalesHeader header, List<SalesLine> lines)
        {
            decimal grossTotal = Math.Round(
                lines.Sum(line => line.GrossAmount),
                2);

            decimal lineDiscountTotal = Math.Round(
                lines.Sum(line => line.DiscountAmount),
                2);

            decimal valueAfterLineDiscounts = Math.Round(
                lines.Sum(line => line.LineTotal),
                2);

            decimal invoiceDiscount = Math.Round(
                header.InvoiceDiscountAmount,
                2);

            if (invoiceDiscount > valueAfterLineDiscounts)
            {
                throw new InvalidOperationException(
                    "Invoice discount cannot be greater than sale value.");
            }

            header.GiftVoucherIssueTotal = Math.Round(
                lines.Where(line => line.IsGiftVoucherSale).Sum(line => line.LineTotal),
                2);

            header.GrossTotal = grossTotal;
            header.TotalDiscount = Math.Round(
                lineDiscountTotal + invoiceDiscount,
                2);
            header.NetTotal = Math.Round(
                valueAfterLineDiscounts - invoiceDiscount,
                2);

            if (header.NetTotal < 0m)
                header.NetTotal = 0m;
        }

        private static void ValidatePaymentTotals(SalesHeader header, List<SalesPayment> payments)
        {
            decimal paymentTotal = Math.Round(payments.Sum(p => p.Amount), 2);
            decimal netTotal = Math.Round(header.NetTotal, 2);

            if (Math.Abs(paymentTotal - netTotal) > 0.01m)
            {
                throw new InvalidOperationException(
                    $"Payment total must equal invoice net total. Payment: {paymentTotal:N2}, Net: {netTotal:N2}");
            }
        }

        private static void ValidateCashTendering(SalesHeader header, List<SalesPayment> payments)
        {
            decimal cashApplied = Math.Round(
                payments
                    .Where(p => p.PaymentType.Equals("Cash", StringComparison.OrdinalIgnoreCase))
                    .Sum(p => p.Amount),
                2);

            if (cashApplied <= 0m)
            {
                header.AmountTendered = 0m;
                header.BalanceReturned = 0m;
                return;
            }

            if (header.AmountTendered <= 0m)
                header.AmountTendered = cashApplied;

            if (header.AmountTendered < cashApplied)
            {
                throw new InvalidOperationException(
                    "Cash tendered amount cannot be lower than cash payment amount.");
            }

            header.BalanceReturned = Math.Round(header.AmountTendered - cashApplied, 2);
        }

        // =========================================================
        // DISCOUNT AUDIT
        // =========================================================

        private static async Task CreateDiscountAuditRowsAsync(
            AppDbContext context,
            SalesHeader header,
            List<SalesLine> lines,
            DateTime now)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (header == null)
                throw new ArgumentNullException(nameof(header));

            if (lines == null || !lines.Any())
                return;

            var ruleDiscountLines = lines
                .Where(l =>
                    l.IsRuleDiscount &&
                    !l.IsGiftVoucherSale &&
                    !l.IsFreeItem &&
                    l.DiscountAmount > 0m)
                .ToList();

            if (!ruleDiscountLines.Any())
                return;

            foreach (var line in ruleDiscountLines)
            {
                if (line.Id <= 0)
                    throw new InvalidOperationException("Sales line must be saved before creating discount audit.");

                var audit = BuildDiscountAudit(header, line, now);

                await context.SalesLineDiscountAudits.AddAsync(audit);
            }
        }

        private static SalesLineDiscountAudit BuildDiscountAudit(
            SalesHeader header,
            SalesLine line,
            DateTime now)
        {
            string discountType;
            decimal discountValue;

            if (line.ManualDiscountAmount > 0m)
            {
                discountType = "Amount";
                discountValue = Math.Round(line.ManualDiscountAmount, 2);
            }
            else
            {
                discountType = "Percent";
                discountValue = Math.Round(line.DiscountPercentage, 2);
            }

            decimal originalLineDiscount =
                CalculateOriginalLineDiscount(line);

            decimal lineTotalAfterOriginalDiscount =
                Math.Round(
                    line.GrossAmount -
                    originalLineDiscount,
                    2);

            return new SalesLineDiscountAudit
            {
                SalesHeaderId = header.Id,
                SalesLineId = line.Id,

                InvoiceNo = header.InvoiceNo,
                InvoiceDate = header.TransactionDate,
                CashierName = header.CashierName,
                TerminalNo = header.TerminalNo,

                DiscountRuleId = line.DiscountRuleId,
                DiscountRuleName = line.DiscountRuleName,

                DiscountReasonId = line.DiscountReasonId,
                ReasonCode = line.DiscountReasonCode,
                ReasonName = line.DiscountReasonName,

                DiscountType = discountType,
                DiscountValue = discountValue,
                DiscountAmount = originalLineDiscount,

                OriginalUnitPrice = line.OriginalUnitPrice,
                Quantity = line.Quantity,
                GrossAmount = line.GrossAmount,
                LineTotalAfterDiscount =
                    lineTotalAfterOriginalDiscount,
                CostPrice = line.CostPrice,
                ProfitAfterDiscount = Math.Round(
                    lineTotalAfterOriginalDiscount -
                    (line.CostPrice * line.Quantity),
                    2),

                ItemVariantId = line.ItemVariantId,
                ItemBatchId = line.ItemBatchId,
                Barcode = line.Barcode,
                SkuCode = line.SkuCode,
                ItemDescription = line.ItemDescription,
                BatchNo = line.BatchNo,
                Uom = line.Uom,

                RequiresManagerApproval = line.DiscountRequiresManagerApproval,
                RequiresAdminApproval = line.DiscountRequiresAdminApproval,
                ApprovedBy = line.DiscountApprovedBy,
                ApprovedAt = line.DiscountApprovedAt,

                CreatedAt = now,
                CreatedBy = header.CashierName,
                Remarks = "Created automatically from cashier discount rule."
            };
        }

        // =========================================================
        // FREE ISSUE CHECKOUT SUPPORT
        // =========================================================

        private static async Task ValidateAndApplyFreeIssueRulesAsync(
            AppDbContext context,
            SalesHeader header,
            IReadOnlyList<SalesLine> lines)
        {
            List<SalesLine> freeLines = lines
                .Where(line => line.IsFreeItem)
                .ToList();

            if (freeLines.Count == 0)
                return;

            int[] ruleIds = freeLines
                .Select(line => line.FreeIssueRuleId ?? 0)
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToArray();

            if (ruleIds.Length == 0 || freeLines.Any(line => !line.FreeIssueRuleId.HasValue || line.FreeIssueRuleId.Value <= 0))
                throw new InvalidOperationException("Every Free Issue line must have a valid rule.");

            // Acquire the SQLite write lock before reading aggregate usage. This keeps
            // daily limits safe when two terminals check out at the same time.
            foreach (int ruleId in ruleIds)
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE FreeIssueRules SET UpdatedAt = UpdatedAt WHERE Id = {ruleId}");
            }

            Dictionary<int, FreeIssueRule> rules = await context.FreeIssueRules
                .Where(rule => ruleIds.Contains(rule.Id))
                .ToDictionaryAsync(rule => rule.Id);

            if (rules.Count != ruleIds.Length)
                throw new InvalidOperationException("One or more Free Issue rules no longer exist.");

            int[] variantIds = freeLines
                .Select(line => line.ItemVariantId ?? 0)
                .Where(id => id > 0)
                .Distinct()
                .ToArray();

            Dictionary<int, ItemVariant> variants = await context.ItemVariants
                .AsNoTracking()
                .Include(variant => variant.ItemParent)
                .Include(variant => variant.ItemSuppliers)
                .Where(variant => variantIds.Contains(variant.Id))
                .ToDictionaryAsync(variant => variant.Id);

            int[] approverIds = freeLines
                .Select(line => line.FreeApprovedByUserId ?? 0)
                .Where(id => id > 0)
                .Distinct()
                .ToArray();
            Dictionary<int, User> approvers = await context.Users
                .AsNoTracking()
                .Where(user => approverIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id);

            int[] freeBatchIds = freeLines
                .Select(line => line.ItemBatchId ?? 0)
                .Where(id => id > 0)
                .Distinct()
                .ToArray();
            Dictionary<int, ItemBatch> freeBatches = await context.ItemBatches
                .AsNoTracking()
                .Where(batch => freeBatchIds.Contains(batch.Id))
                .ToDictionaryAsync(batch => batch.Id);

            var dailyUsage = new Dictionary<int, FreeIssueRuleUsageDto>();
            foreach (int ruleId in ruleIds)
            {
                dailyUsage[ruleId] = await FreeIssueRuleRepository.GetUsageAsync(
                    context,
                    ruleId,
                    header.TransactionDate.Date);
            }

            var invoiceQty = new Dictionary<int, decimal>();
            var invoiceValue = new Dictionary<int, decimal>();

            foreach (SalesLine line in freeLines)
            {
                int ruleId = line.FreeIssueRuleId!.Value;
                FreeIssueRule rule = rules[ruleId];

                if (!line.ItemVariantId.HasValue || !variants.TryGetValue(line.ItemVariantId.Value, out ItemVariant? variant))
                    throw new InvalidOperationException($"Free Issue item was not found for '{line.ItemDescription}'.");

                DateTime saleDate = header.TransactionDate.Date;
                if (!rule.IsActive)
                    throw new InvalidOperationException($"Free Issue rule '{rule.RuleName}' is inactive.");
                if (rule.ValidFrom.Date > saleDate)
                    throw new InvalidOperationException($"Free Issue rule '{rule.RuleName}' is not active yet.");
                if (rule.ValidTo.HasValue && rule.ValidTo.Value.Date < saleDate)
                    throw new InvalidOperationException($"Free Issue rule '{rule.RuleName}' has expired.");

                bool applies;
                if (string.Equals(rule.AppliesToType, "Supplier", StringComparison.OrdinalIgnoreCase))
                {
                    applies = variant.ItemSuppliers.Any(link =>
                        FreeIssueRuleRepository.DoesRuleApplyToItem(
                            rule,
                            variant.Id,
                            variant.ItemParentId,
                            variant.ItemParent.CategoryId,
                            variant.ItemParent.SubCategoryId,
                            link.SupplierId,
                            variant.SkuCode,
                            variant.Barcode));
                }
                else
                {
                    int? primarySupplierId = variant.ItemSuppliers
                        .OrderByDescending(link => link.IsPrimary)
                        .Select(link => (int?)link.SupplierId)
                        .FirstOrDefault();

                    applies = FreeIssueRuleRepository.DoesRuleApplyToItem(
                        rule,
                        variant.Id,
                        variant.ItemParentId,
                        variant.ItemParent.CategoryId,
                        variant.ItemParent.SubCategoryId,
                        primarySupplierId,
                        variant.SkuCode,
                        variant.Barcode);
                }

                if (!applies)
                    throw new InvalidOperationException($"Free Issue rule '{rule.RuleName}' does not apply to '{line.ItemDescription}'.");

                ItemBatch? freeBatch = null;
                if (string.Equals(variant.ItemParent.ItemType, ItemTypeCodes.StockItem, StringComparison.Ordinal))
                {
                    if (!line.ItemBatchId.HasValue ||
                        !freeBatches.TryGetValue(line.ItemBatchId.Value, out freeBatch) ||
                        freeBatch.ItemVariantId != variant.Id)
                    {
                        throw new InvalidOperationException($"Free Issue stock batch is invalid for '{line.ItemDescription}'.");
                    }

                    line.CostPrice = freeBatch.CostPrice;
                }
                else
                {
                    line.CostPrice = variant.CostPrice > 0m ? variant.CostPrice : variant.AverageCost;
                }

                decimal quantity = Math.Round(line.Quantity, 3);
                decimal originalUnitPrice = ResolveFreeIssueOriginalUnitPrice(
                    header,
                    variant,
                    freeBatch);
                line.OriginalUnitPrice = originalUnitPrice;

                if (quantity <= 0m)
                    throw new InvalidOperationException("Free Issue quantity must be greater than zero.");

                decimal sellingValue = Math.Round(quantity * originalUnitPrice, 2);
                decimal nextInvoiceQty = invoiceQty.GetValueOrDefault(ruleId) + quantity;
                decimal nextInvoiceValue = invoiceValue.GetValueOrDefault(ruleId) + sellingValue;
                FreeIssueRuleUsageDto used = dailyUsage[ruleId];

                if (rule.MaxQtyPerInvoice > 0m && nextInvoiceQty > rule.MaxQtyPerInvoice)
                    throw new InvalidOperationException($"Free Issue rule '{rule.RuleName}' exceeds its invoice quantity limit.");
                if (rule.MaxValuePerInvoice > 0m && nextInvoiceValue > rule.MaxValuePerInvoice)
                    throw new InvalidOperationException($"Free Issue rule '{rule.RuleName}' exceeds its invoice value limit.");
                if (rule.MaxQtyPerDay > 0m && used.TodayQty + nextInvoiceQty > rule.MaxQtyPerDay)
                    throw new InvalidOperationException($"Free Issue rule '{rule.RuleName}' exceeds its daily quantity limit.");
                if (rule.MaxValuePerDay > 0m && used.TodayValue + nextInvoiceValue > rule.MaxValuePerDay)
                    throw new InvalidOperationException($"Free Issue rule '{rule.RuleName}' exceeds its daily value limit.");

                (bool requiresManager, bool requiresAdmin) =
                    FreeIssueRuleRepository.ResolveApprovalRequirement(rule, sellingValue);

                if (requiresAdmin)
                {
                    if (!line.FreeApprovedByUserId.HasValue ||
                        !line.FreeApprovedAt.HasValue ||
                        !approvers.TryGetValue(line.FreeApprovedByUserId.Value, out User? approver) ||
                        !approver.IsActive ||
                        approver.Role != UserRole.Admin)
                    {
                        throw new InvalidOperationException($"Administrator approval is required for Free Issue rule '{rule.RuleName}'.");
                    }

                    line.FreeApprovedBy = approver.Username;
                    line.FreeApprovedRole = approver.Role.ToString();
                }
                else if (requiresManager)
                {
                    if (!line.FreeApprovedByUserId.HasValue ||
                        !line.FreeApprovedAt.HasValue ||
                        !approvers.TryGetValue(line.FreeApprovedByUserId.Value, out User? approver) ||
                        !approver.IsActive ||
                        (approver.Role != UserRole.Manager && approver.Role != UserRole.Admin))
                    {
                        throw new InvalidOperationException($"Manager approval is required for Free Issue rule '{rule.RuleName}'.");
                    }

                    line.FreeApprovedBy = approver.Username;
                    line.FreeApprovedRole = approver.Role.ToString();
                }
                else
                {
                    line.FreeApprovedBy = string.Empty;
                    line.FreeApprovedByUserId = null;
                    line.FreeApprovedRole = string.Empty;
                    line.FreeApprovedAt = null;
                }

                line.FreeIssueRuleName = rule.RuleName;
                line.FreeIssueType = FreeIssueTypeCodes.Normalize(rule.FreeIssueType);
                line.FreeReasonCode = rule.ReasonCode;
                line.FreeReasonText = string.IsNullOrWhiteSpace(rule.ReasonName) ? rule.RuleName : rule.ReasonName;
                line.FreeIssueAppliedBy = header.CashierName;
                line.FreeIssueAppliedAt = header.TransactionDate;
                line.FreeIssueRuleSnapshotJson = FreeIssueRuleSnapshot.FromRule(rule).ToJson();
                line.FreeIssueSnapshotStatus = FreeIssueSnapshotStatusCodes.Complete;
                line.FreeIssueSellingValue = sellingValue;
                line.FreeIssueCostValue = Math.Round(line.CostPrice * quantity, 2);
                line.IsSupplierRecoverable = line.FreeIssueType == FreeIssueTypeCodes.SupplierClaim;

                if (line.IsSupplierRecoverable)
                {
                    if (!rule.SupplierId.HasValue || rule.SupplierId.Value <= 0)
                        throw new InvalidOperationException($"Supplier-funded rule '{rule.RuleName}' has no supplier.");

                    line.SupplierId = rule.SupplierId;
                    line.SupplierName = rule.SupplierName;
                    line.SupplierPromotionReference = rule.SupplierPromotionReference;
                    line.SupplierClaimValue = FreeIssueRuleRepository.CalculateClaimValue(
                        rule,
                        quantity,
                        originalUnitPrice,
                        line.CostPrice);
                    if (line.SupplierClaimValue <= 0m)
                        throw new InvalidOperationException($"Supplier claim value for rule '{rule.RuleName}' must be greater than zero.");
                    line.SupplierClaimStatus = SupplierClaimStatusCodes.Draft;
                    line.SupplierClaimReferenceNo = string.Empty;
                }
                else
                {
                    line.SupplierId = null;
                    line.SupplierName = string.Empty;
                    line.SupplierPromotionReference = string.Empty;
                    line.SupplierClaimId = null;
                    line.SupplierClaimStatus = string.Empty;
                    line.SupplierClaimReferenceNo = string.Empty;
                    line.SupplierClaimValue = 0m;
                }

                invoiceQty[ruleId] = nextInvoiceQty;
                invoiceValue[ruleId] = nextInvoiceValue;
            }
        }


        private static decimal ResolveFreeIssueOriginalUnitPrice(
            SalesHeader header,
            ItemVariant variant,
            ItemBatch? batch)
        {
            decimal price;
            if (header.IsWholesaleSale)
            {
                price = variant.WholesalePrice > 0m
                    ? variant.WholesalePrice
                    : variant.RetailPrice > 0m
                        ? variant.RetailPrice
                        : batch?.WholesalePrice > 0m
                            ? batch.WholesalePrice
                            : batch?.RetailPrice ?? 0m;
            }
            else
            {
                price = variant.RetailPrice > 0m
                    ? variant.RetailPrice
                    : batch?.RetailPrice ?? 0m;
            }

            if (price <= 0m)
            {
                string mode = header.IsWholesaleSale ? "wholesale" : "retail";
                throw new InvalidOperationException(
                    $"A positive {mode} selling price is required for Free Issue item '{variant.FullDisplayName}'.");
            }

            return Math.Round(price, 2);
        }

        private static async Task ProcessFreeItemSupplierClaimsAsync(
            AppDbContext context,
            SalesHeader header,
            IEnumerable<SalesLine> lines)
        {
            var recoverableLines = lines
                .Where(l => l.IsFreeItem && l.IsSupplierRecoverable)
                .ToList();

            if (recoverableLines.Count == 0)
                return;

            if (header.Id <= 0)
                throw new InvalidOperationException("Sales header must be saved before creating free item supplier claims.");

            if (string.IsNullOrWhiteSpace(header.InvoiceNo))
                throw new InvalidOperationException("Invoice number is required before creating supplier claims.");

            foreach (var line in recoverableLines)
            {
                if (line.Id <= 0)
                    throw new InvalidOperationException("Sales line must be saved before creating supplier claim.");

                await FreeItemClaimRepository.CreateSupplierClaimFromSaleLineAsync(
                    context,
                    header,
                    line);
            }
        }

        // =========================================================
        // GIFT VOUCHER CHECKOUT SUPPORT
        // =========================================================

        private static async Task ProcessSoldGiftVoucherLinesAsync(
            AppDbContext context,
            SalesHeader header,
            IEnumerable<SalesLine> lines)
        {
            var voucherSaleLines = lines
                .Where(l => l.IsGiftVoucherSale)
                .ToList();

            if (voucherSaleLines.Count == 0)
                return;

            if (header.Id <= 0)
                throw new InvalidOperationException("Sales header must be saved before activating sold gift vouchers.");

            if (string.IsNullOrWhiteSpace(header.InvoiceNo))
                throw new InvalidOperationException("Invoice number is required before activating sold gift vouchers.");

            var duplicateVoucher = voucherSaleLines
                .Select(l => l.GiftVoucherId.GetValueOrDefault())
                .Where(id => id > 0)
                .GroupBy(id => id)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicateVoucher != null)
                throw new InvalidOperationException("The same gift voucher cannot be sold more than once in the same invoice.");

            foreach (var line in voucherSaleLines)
            {
                int giftVoucherId = ValidateGiftVoucherSaleLineBeforeActivation(line);

                var voucher = await context.GiftVouchers
                    .FirstOrDefaultAsync(v => v.Id == giftVoucherId);

                if (voucher == null)
                    throw new InvalidOperationException("Gift voucher was not found.");

                decimal lineAmount = Math.Round(line.LineTotal, 2);
                decimal voucherAmount = Math.Round(voucher.VoucherAmount, 2);

                if (lineAmount != voucherAmount)
                {
                    throw new InvalidOperationException(
                        $"Gift voucher sale amount mismatch. Voucher {voucher.VoucherNo} value is Rs. {voucherAmount:N2}, but sale line amount is Rs. {lineAmount:N2}.");
                }

                await GiftVoucherRepository.MarkVoucherSoldAsync(
                    context,
                    giftVoucherId,
                    header,
                    header.CashierName,
                    header.TerminalNo,
                    $"Gift voucher sold from invoice {header.InvoiceNo}.");
            }
        }

        private static async Task ProcessGiftVoucherRedemptionsAsync(
            AppDbContext context,
            SalesHeader header,
            IEnumerable<SalesPayment> payments)
        {
            var giftVoucherPayments = payments
                .Where(IsGiftVoucherPayment)
                .ToList();

            if (giftVoucherPayments.Count == 0)
                return;

            if (header.Id <= 0)
                throw new InvalidOperationException("Sales header must be saved before redeeming gift vouchers.");

            if (string.IsNullOrWhiteSpace(header.InvoiceNo))
                throw new InvalidOperationException("Invoice number is required before redeeming gift vouchers.");

            var duplicateVoucher = giftVoucherPayments
                .Select(p => p.GiftVoucherId.GetValueOrDefault())
                .Where(id => id > 0)
                .GroupBy(id => id)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicateVoucher != null)
                throw new InvalidOperationException("The same gift voucher cannot be used more than once in the same invoice.");

            foreach (var payment in giftVoucherPayments)
            {
                int giftVoucherId = ValidateGiftVoucherPaymentBeforeRedeem(payment);

                await GiftVoucherRepository.MarkVoucherRedeemedAsync(
                    context,
                    giftVoucherId,
                    payment.Amount,
                    payment.GiftVoucherForfeitedAmount,
                    header,
                    payment,
                    header.CashierName,
                    header.TerminalNo,
                    payment.GiftVoucherAuthorizedBy,
                    $"Gift voucher redeemed from invoice {header.InvoiceNo}.");
            }
        }

        private static bool IsGiftVoucherPayment(SalesPayment payment)
        {
            if (payment == null)
                return false;

            return payment.PaymentType.Equals("GiftVoucher", StringComparison.OrdinalIgnoreCase) ||
                   payment.PaymentType.Equals("Gift Voucher", StringComparison.OrdinalIgnoreCase);
        }

        private static int ValidateGiftVoucherSaleLineBeforeActivation(SalesLine line)
        {
            if (line.GiftVoucherId is not int giftVoucherId || giftVoucherId <= 0)
                throw new InvalidOperationException("Gift voucher sale line is missing voucher reference.");

            if (line.Quantity != 1m)
                throw new InvalidOperationException("Gift voucher sale quantity must be 1.");

            if (line.UnitPrice <= 0m)
                throw new InvalidOperationException("Gift voucher sale price must be greater than zero.");

            if (line.LineTotal <= 0m)
                throw new InvalidOperationException("Gift voucher sale line total must be greater than zero.");

            if (line.DiscountAmount != 0m ||
                line.DiscountPercentage != 0m ||
                line.ManualDiscountAmount != 0m ||
                line.IsManualDiscount ||
                line.IsRuleDiscount ||
                line.DiscountRuleId.HasValue ||
                line.IsPriceOverridden)
            {
                throw new InvalidOperationException("Discount or New Price cannot be applied to gift voucher sale line.");
            }

            return giftVoucherId;
        }

        private static int ValidateGiftVoucherPaymentBeforeRedeem(SalesPayment payment)
        {
            if (payment.GiftVoucherId is not int giftVoucherId || giftVoucherId <= 0)
                throw new InvalidOperationException("Gift voucher payment is missing voucher reference.");

            if (payment.Amount <= 0m)
                throw new InvalidOperationException("Gift voucher payment amount must be greater than zero.");

            if (payment.GiftVoucherAmount <= 0m)
                throw new InvalidOperationException("Gift voucher face value is missing or invalid.");

            if (payment.GiftVoucherForfeitedAmount < 0m)
                throw new InvalidOperationException("Gift voucher forfeited amount cannot be negative.");

            decimal totalUsed = Math.Round(payment.Amount + payment.GiftVoucherForfeitedAmount, 2);
            decimal voucherValue = Math.Round(payment.GiftVoucherAmount, 2);

            if (Math.Abs(totalUsed - voucherValue) > 0.01m)
            {
                throw new InvalidOperationException(
                    "A one-time gift voucher must be fully consumed by the applied and forfeited amounts.");
            }

            if (payment.GiftVoucherForfeitedAmount > 0m &&
                string.IsNullOrWhiteSpace(payment.GiftVoucherAuthorizedBy))
            {
                throw new InvalidOperationException(
                    "Manager authorization is required for gift voucher forfeiture.");
            }

            return giftVoucherId;
        }

        // =========================================================
        // RECEIPT LOAD
        // =========================================================

        private static async Task<SalesHeader> LoadSavedReceiptAsync(AppDbContext context, int salesHeaderId)
        {
            var receipt = await context.SalesHeaders
                .Include(h => h.CustomerMaster)
                .Include(h => h.SalesLines)
                .Include(h => h.SalesPayments)
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.Id == salesHeaderId);

            if (receipt == null)
                throw new InvalidOperationException("Saved receipt could not be loaded.");

            return receipt;
        }

        private static decimal CalculateOriginalLineDiscount(
            SalesLine line)
        {
            if (line.ManualDiscountAmount > 0m)
            {
                return Math.Round(
                    line.ManualDiscountAmount,
                    2);
            }

            if (line.DiscountPercentage > 0m)
            {
                return Math.Round(
                    line.GrossAmount *
                    (line.DiscountPercentage / 100m),
                    2);
            }

            return 0m;
        }


        private static decimal Money(decimal value) =>
            decimal.Round(value, 2, MidpointRounding.AwayFromZero);

        private static string NormalizeCardLastDigits(string? value)
        {
            string digits = new string((value ?? string.Empty)
                .Where(char.IsDigit)
                .TakeLast(6)
                .ToArray());

            return digits;
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string NormalizeCustomerTypeSnapshot(string? value)
        {
            string type = NormalizeText(value);

            if (type.Equals("Wholesale", StringComparison.OrdinalIgnoreCase))
                return "Wholesale";

            if (type.Equals("Retail", StringComparison.OrdinalIgnoreCase))
                return "Retail";

            return "Walk-In";
        }
    }
}
