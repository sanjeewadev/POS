using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;

namespace POS.Core.Repositories
{
    public class SalesRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SalesRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<SalesHeader> ProcessCheckoutAsync(
            SalesHeader header,
            List<SalesLine> lines,
            List<SalesPayment>? payments = null)
        {
            if (header == null)
                throw new ArgumentNullException(nameof(header));

            if (lines == null || !lines.Any())
                throw new InvalidOperationException("Cannot checkout an empty cart.");

            if (payments == null || !payments.Any())
                throw new InvalidOperationException("At least one payment is required.");

            NormalizeSalesHeader(header);
            NormalizeSalesLines(lines);
            NormalizePayments(payments);

            bool sellingGiftVoucher = lines.Any(l => l.IsGiftVoucherSale);
            bool payingByGiftVoucher = payments.Any(IsGiftVoucherPayment);

            if (sellingGiftVoucher && payingByGiftVoucher)
                throw new InvalidOperationException("Gift voucher cannot be used to buy another gift voucher.");

            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                DateTime now = DateTime.Now;

                await ValidateShiftAsync(context, header.ShiftSessionId);
                await ApplyCustomerSnapshotAsync(context, header);

                RecalculateHeaderTotals(header, lines);
                ValidatePaymentTotals(header, payments);
                ValidateCashTendering(header, payments);

                var sequence = await GetOrCreateInvoiceSequenceAsync(context);

                header.InvoiceNo = $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString().PadLeft(sequence.PaddingLength, '0')}";
                sequence.NextSequenceNumber++;
                sequence.UpdatedAt = now;

                header.TransactionDate = now;
                header.Status = "Completed";
                header.IsVoided = false;

                await context.SalesHeaders.AddAsync(header);
                await context.SaveChangesAsync();

                foreach (var line in lines)
                {
                    line.SalesHeaderId = header.Id;
                    line.CreatedAt = now;

                    if (line.IsGiftVoucherSale)
                    {
                        PrepareGiftVoucherSaleLine(line);
                        await context.SalesLines.AddAsync(line);
                        continue;
                    }

                    if (!line.ItemBatchId.HasValue || line.ItemBatchId.Value <= 0)
                        throw new InvalidOperationException("Product sale line has no selected batch.");

                    var batch = await context.ItemBatches
                        .Include(b => b.ItemVariant)
                            .ThenInclude(v => v.ItemParent)
                        .FirstOrDefaultAsync(b => b.Id == line.ItemBatchId.Value);

                    if (batch == null)
                        throw new InvalidOperationException($"Item batch was not found. Batch ID: {line.ItemBatchId}");

                    ValidateBatchForSale(batch, line);
                    PrepareProductSaleLineForPersistence(batch, line);

                    batch.CurrentStock = Math.Round(batch.CurrentStock - line.Quantity, 3);

                    await context.SalesLines.AddAsync(line);
                }

                await context.SaveChangesAsync();

                await CreateDiscountAuditRowsAsync(context, header, lines, now);
                await context.SaveChangesAsync();

                foreach (var payment in payments)
                {
                    payment.SalesHeaderId = header.Id;
                    payment.CreatedAt = now;

                    if (payment.PaymentDate == null)
                        payment.PaymentDate = now;

                    await context.SalesPayments.AddAsync(payment);
                }

                await context.SaveChangesAsync();

                await ProcessSoldGiftVoucherLinesAsync(context, header, lines);
                await ProcessGiftVoucherRedemptionsAsync(context, header, payments);
                await ProcessFreeItemSupplierClaimsAsync(context, header, lines);

                await context.SaveChangesAsync();

                foreach (var line in lines.Where(l => !l.IsGiftVoucherSale))
                {
                    if (!line.ItemVariantId.HasValue || !line.ItemBatchId.HasValue)
                        throw new InvalidOperationException("Product sale line is missing item stock reference.");

                    var inventoryTransaction = new InventoryTransaction
                    {
                        ItemVariantId = line.ItemVariantId.Value,
                        ItemBatchId = line.ItemBatchId.Value,
                        TransactionDate = header.TransactionDate,
                        TransactionType = "SALE",
                        ReferenceDocument = header.InvoiceNo,
                        ReferenceLineId = line.Id,
                        Quantity = -line.Quantity,
                        UnitCost = line.CostPrice,
                        CreatedBy = header.CashierName,
                        Remarks = line.IsFreeItem
                            ? $"Free issue sale invoice {header.InvoiceNo} / Batch {line.BatchNo} / {line.FreeIssueType}"
                            : $"Sale invoice {header.InvoiceNo} / Batch {line.BatchNo}"
                    };

                    await context.InventoryTransactions.AddAsync(inventoryTransaction);
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return await LoadSavedReceiptAsync(context, header.Id);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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
        }

        // =========================================================
        // VALIDATION / SEQUENCES
        // =========================================================

        private static async Task ValidateShiftAsync(AppDbContext context, int shiftSessionId)
        {
            if (shiftSessionId <= 0)
                throw new InvalidOperationException("No active shift found.");

            bool exists = await context.ShiftSessions.AnyAsync(s => s.Id == shiftSessionId);

            if (!exists)
                throw new InvalidOperationException("Active shift session was not found.");
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

        private static void ValidateBatchForSale(ItemBatch batch, SalesLine line)
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

            if (batch.ExpiryDate.HasValue && batch.ExpiryDate.Value.Date < DateTime.Today)
                throw new InvalidOperationException($"Batch '{batch.BatchNo}' is expired and cannot be sold.");

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

            if (line.ItemVariantId.HasValue &&
                line.ItemVariantId.Value > 0 &&
                line.ItemVariantId.Value != batch.ItemVariantId)
            {
                throw new InvalidOperationException("Cart item variant does not match selected batch.");
            }
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

                if (!line.ItemBatchId.HasValue || line.ItemBatchId.Value <= 0)
                    throw new InvalidOperationException("Every product sale line must have a selected batch.");

                if (!line.ItemVariantId.HasValue || line.ItemVariantId.Value <= 0)
                    throw new InvalidOperationException("Every product sale line must have a selected item variant.");

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
                    line.SupplierClaimStatus = "Pending";
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

        private static string NormalizeFreeIssueType(string? value)
        {
            string text = NormalizeText(value);

            if (text.Equals("SupplierClaim", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("Supplier Recoverable", StringComparison.OrdinalIgnoreCase))
                return "SupplierClaim";

            return "ShopCost";
        }

        private static void PrepareProductSaleLineForPersistence(ItemBatch batch, SalesLine line)
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

            ValidateAndRecalculateNormalSaleLine(line);
            ValidateMinimumPriceForProductLine(batch, line);
        }

        private static void ValidateMinimumPriceForProductLine(ItemBatch batch, SalesLine line)
        {
            decimal minimumPrice = batch.ItemVariant?.MinimumPrice ?? 0m;

            if (minimumPrice <= 0m)
                return;

            if (line.Quantity <= 0m)
                throw new InvalidOperationException("Sale quantity must be greater than zero.");

            decimal effectiveUnitPrice = Math.Round(line.LineTotal / line.Quantity, 2);

            if (effectiveUnitPrice >= minimumPrice)
                return;

            if (line.IsPriceOverridden)
            {
                if (string.IsNullOrWhiteSpace(line.PriceOverrideApprovedBy))
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
                if (string.IsNullOrWhiteSpace(line.DiscountApprovedBy))
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
                    line.SupplierClaimStatus = "Pending";

                if (string.IsNullOrWhiteSpace(line.SupplierClaimReferenceNo))
                    line.SupplierClaimReferenceNo = $"FI-{DateTime.Now:yyyyMMddHHmmss}";
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
            if (!payments.Any())
                throw new InvalidOperationException("At least one payment is required.");

            foreach (var payment in payments)
            {
                payment.PaymentType = NormalizeText(payment.PaymentType);
                payment.ReferenceNo = NormalizeText(payment.ReferenceNo);
                payment.BankOrCardType = NormalizeText(payment.BankOrCardType);
                payment.GiftVoucherNo = NormalizeText(payment.GiftVoucherNo);
                payment.GiftVoucherBarcode = NormalizeText(payment.GiftVoucherBarcode);

                if (string.IsNullOrWhiteSpace(payment.PaymentType))
                    throw new InvalidOperationException("Payment type is required.");

                if (payment.Amount <= 0m)
                    throw new InvalidOperationException("Payment amount must be greater than zero.");

                if (IsGiftVoucherPayment(payment))
                    ValidateGiftVoucherPaymentBeforeRedeem(payment);
            }
        }

        // =========================================================
        // TOTALS / PAYMENTS
        // =========================================================

        private static void RecalculateHeaderTotals(SalesHeader header, List<SalesLine> lines)
        {
            header.GrossTotal = Math.Round(lines.Sum(l => l.GrossAmount), 2);
            header.TotalDiscount = Math.Round(lines.Sum(l => l.DiscountAmount), 2);
            header.NetTotal = Math.Round(lines.Sum(l => l.LineTotal), 2);

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
                DiscountAmount = line.DiscountAmount,

                OriginalUnitPrice = line.OriginalUnitPrice,
                Quantity = line.Quantity,
                GrossAmount = line.GrossAmount,
                LineTotalAfterDiscount = line.LineTotal,
                CostPrice = line.CostPrice,
                ProfitAfterDiscount = line.ProfitAmount,

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
                .Where(l => l.GiftVoucherId.HasValue && l.GiftVoucherId.Value > 0)
                .GroupBy(l => l.GiftVoucherId.Value)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicateVoucher != null)
                throw new InvalidOperationException("The same gift voucher cannot be sold more than once in the same invoice.");

            foreach (var line in voucherSaleLines)
            {
                ValidateGiftVoucherSaleLineBeforeActivation(line);

                int giftVoucherId = line.GiftVoucherId.Value;

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
                .Where(p => p.GiftVoucherId.HasValue && p.GiftVoucherId.Value > 0)
                .GroupBy(p => p.GiftVoucherId.Value)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicateVoucher != null)
                throw new InvalidOperationException("The same gift voucher cannot be used more than once in the same invoice.");

            foreach (var payment in giftVoucherPayments)
            {
                ValidateGiftVoucherPaymentBeforeRedeem(payment);

                await GiftVoucherRepository.MarkVoucherRedeemedAsync(
                    context,
                    payment.GiftVoucherId.Value,
                    payment.Amount,
                    payment.GiftVoucherForfeitedAmount,
                    header,
                    header.CashierName,
                    header.TerminalNo,
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

        private static void ValidateGiftVoucherSaleLineBeforeActivation(SalesLine line)
        {
            if (!line.GiftVoucherId.HasValue || line.GiftVoucherId.Value <= 0)
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
        }

        private static void ValidateGiftVoucherPaymentBeforeRedeem(SalesPayment payment)
        {
            if (!payment.GiftVoucherId.HasValue || payment.GiftVoucherId.Value <= 0)
                throw new InvalidOperationException("Gift voucher payment is missing voucher reference.");

            if (payment.Amount <= 0m)
                throw new InvalidOperationException("Gift voucher payment amount must be greater than zero.");

            if (payment.GiftVoucherAmount <= 0m)
                throw new InvalidOperationException("Gift voucher face value is missing or invalid.");

            if (payment.GiftVoucherForfeitedAmount < 0m)
                throw new InvalidOperationException("Gift voucher forfeited amount cannot be negative.");

            decimal totalUsed = Math.Round(payment.Amount + payment.GiftVoucherForfeitedAmount, 2);
            decimal voucherValue = Math.Round(payment.GiftVoucherAmount, 2);

            if (totalUsed > voucherValue)
                throw new InvalidOperationException("Gift voucher applied amount plus forfeited amount cannot exceed voucher value.");
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