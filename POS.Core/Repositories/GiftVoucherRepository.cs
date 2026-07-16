using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Core.Repositories
{
    public class GiftVoucherSearchDto
    {
        public int Id { get; set; }
        public string VoucherNo { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public decimal VoucherAmount { get; set; }
        public decimal RedeemedAmount { get; set; }
        public decimal ForfeitedAmount { get; set; }
        public string Status { get; set; } = GiftVoucherStatusCodes.Created;
        public string DisplayStatus { get; set; } = GiftVoucherStatusCodes.Created;
        public string BatchNo { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime? ActivatedAt { get; set; }
        public DateTime? RedeemedDate { get; set; }
        public string SoldInvoiceNo { get; set; } = string.Empty;
        public string RedeemedInvoiceNo { get; set; } = string.Empty;
        public int PrintCount { get; set; }
        public DateTime? LastPrintedAt { get; set; }
        public string LastPrintedBy { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }

    public class GiftVoucherSaleValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public int GiftVoucherId { get; set; }
        public string VoucherNo { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public decimal VoucherAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
    }

    public class GiftVoucherRedeemValidationResult
    {
        public bool IsValid { get; set; }
        public bool RequiresManagerApproval { get; set; }
        public string Message { get; set; } = string.Empty;
        public int GiftVoucherId { get; set; }
        public string VoucherNo { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public decimal VoucherAmount { get; set; }
        public decimal AmountToApply { get; set; }
        public decimal ForfeitedAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
    }

    public class GiftVoucherRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public GiftVoucherRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<List<GiftVoucher>> GenerateVoucherBatchAsync(
            int count,
            decimal voucherAmount,
            DateTime? expiryDate,
            string createdBy,
            string batchNo = "",
            string description = "")
        {
            if (count <= 0)
                throw new InvalidOperationException("Voucher count must be greater than zero.");
            if (count > 1000)
                throw new InvalidOperationException("Cannot generate more than 1000 vouchers at once.");

            voucherAmount = Money(voucherAmount);
            if (voucherAmount <= 0m)
                throw new InvalidOperationException("Voucher amount must be greater than zero.");
            if (expiryDate.HasValue && expiryDate.Value.Date < DateTime.Today)
                throw new InvalidOperationException("Voucher expiry date cannot be in the past.");

            string safeCreatedBy = RequiredText(createdBy, "Created user is required.");

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                DateTime now = DateTime.Now;
                string safeBatchNo = NormalizeText(batchNo);
                if (string.IsNullOrWhiteSpace(safeBatchNo))
                    safeBatchNo = await GenerateBatchNoAsync(context);

                string safeDescription = string.IsNullOrWhiteSpace(description)
                    ? $"Gift Voucher Rs. {voucherAmount:N2}"
                    : NormalizeText(description);

                var generated = new List<GiftVoucher>();
                for (int index = 0; index < count; index++)
                {
                    string voucherNo = await GenerateVoucherNoAsync(context);
                    var voucher = new GiftVoucher
                    {
                        VoucherNo = voucherNo,
                        Barcode = voucherNo,
                        VoucherAmount = voucherAmount,
                        Status = GiftVoucherStatusCodes.Created,
                        ExpiryDate = expiryDate?.Date,
                        BatchNo = safeBatchNo,
                        Description = safeDescription,
                        CreatedAt = now,
                        CreatedBy = safeCreatedBy,
                        UpdatedAt = now,
                        UpdatedBy = safeCreatedBy,
                        Remarks = "Generated from voucher batch."
                    };

                    context.GiftVouchers.Add(voucher);
                    generated.Add(voucher);
                }

                await context.SaveChangesAsync();

                foreach (GiftVoucher voucher in generated)
                {
                    AddVoucherTransaction(
                        context,
                        voucher,
                        GiftVoucherTransactionCodes.Created,
                        voucher.VoucherAmount,
                        0m,
                        0m,
                        null,
                        null,
                        null,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        safeCreatedBy,
                        string.Empty,
                        null,
                        "Voucher generated.");
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                return generated;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<GiftVoucherSearchDto>> SearchVouchersAsync(
            string filter = "All",
            string searchTerm = "",
            int take = 200)
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            string safeFilter = NormalizeText(filter);
            string safeTerm = NormalizeText(searchTerm).ToLowerInvariant();
            take = Math.Clamp(take <= 0 ? 200 : take, 1, 1000);

            IQueryable<GiftVoucher> query = context.GiftVouchers.AsNoTracking();
            DateTime today = DateTime.Today;

            if (!string.IsNullOrWhiteSpace(safeFilter) &&
                !safeFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (GiftVoucherStatusCodes.Equals(safeFilter, GiftVoucherStatusCodes.Expired))
                {
                    query = query.Where(v =>
                        v.ExpiryDate.HasValue &&
                        v.ExpiryDate.Value < today &&
                        (v.Status == GiftVoucherStatusCodes.Created ||
                         v.Status == GiftVoucherStatusCodes.Active));
                }
                else if (GiftVoucherStatusCodes.IsVoided(safeFilter))
                {
                    query = query.Where(v =>
                        v.Status == GiftVoucherStatusCodes.Voided ||
                        v.Status == GiftVoucherStatusCodes.LegacyCancelled);
                }
                else
                {
                    query = query.Where(v => v.Status == safeFilter);
                }
            }

            if (!string.IsNullOrWhiteSpace(safeTerm))
            {
                query = query.Where(v =>
                    v.VoucherNo.ToLower().Contains(safeTerm) ||
                    v.Barcode.ToLower().Contains(safeTerm) ||
                    v.BatchNo.ToLower().Contains(safeTerm) ||
                    v.SoldInvoiceNo.ToLower().Contains(safeTerm) ||
                    v.RedeemedInvoiceNo.ToLower().Contains(safeTerm));
            }

            List<GiftVoucher> vouchers = await query
                .OrderByDescending(v => v.CreatedAt)
                .ThenBy(v => v.VoucherNo)
                .Take(take)
                .ToListAsync();

            return vouchers.Select(BuildSearchDto).ToList();
        }

        public async Task<GiftVoucher?> GetByBarcodeOrVoucherNoAsync(string barcodeOrVoucherNo)
        {
            string code = NormalizeText(barcodeOrVoucherNo);
            if (string.IsNullOrWhiteSpace(code))
                return null;

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            return await context.GiftVouchers
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Barcode == code || v.VoucherNo == code);
        }

        public async Task<List<GiftVoucherTransaction>> GetVoucherHistoryAsync(int giftVoucherId)
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            return await context.GiftVoucherTransactions
                .AsNoTracking()
                .Where(t => t.GiftVoucherId == giftVoucherId)
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.Id)
                .ToListAsync();
        }

        public async Task<GiftVoucherSaleValidationResult> ValidateVoucherForSaleAsync(string barcodeOrVoucherNo)
        {
            GiftVoucher? voucher = await GetByBarcodeOrVoucherNoAsync(barcodeOrVoucherNo);
            if (voucher == null)
            {
                return new GiftVoucherSaleValidationResult
                {
                    IsValid = false,
                    Message = string.IsNullOrWhiteSpace(barcodeOrVoucherNo)
                        ? "Voucher barcode or number is required."
                        : "Voucher was not found."
                };
            }

            if (IsExpiredByDate(voucher))
                return BuildSaleValidationResult(voucher, false, "Voucher is expired and cannot be sold.");
            if (!GiftVoucherStatusCodes.Equals(voucher.Status, GiftVoucherStatusCodes.Created))
                return BuildSaleValidationResult(voucher, false, $"Voucher cannot be sold. Current status: {DisplayStatus(voucher)}.");
            if (voucher.VoucherAmount <= 0m)
                return BuildSaleValidationResult(voucher, false, "Voucher amount is invalid.");

            return BuildSaleValidationResult(voucher, true, "Voucher can be sold.");
        }

        public async Task<GiftVoucherRedeemValidationResult> ValidateVoucherForRedemptionAsync(
            string barcodeOrVoucherNo,
            decimal balanceDue,
            bool allowForfeitWithManagerApproval = false)
        {
            balanceDue = Money(balanceDue);
            if (balanceDue <= 0m)
            {
                return new GiftVoucherRedeemValidationResult
                {
                    IsValid = false,
                    Message = "Invoice is already fully paid."
                };
            }

            GiftVoucher? voucher = await GetByBarcodeOrVoucherNoAsync(barcodeOrVoucherNo);
            if (voucher == null)
            {
                return new GiftVoucherRedeemValidationResult
                {
                    IsValid = false,
                    Message = string.IsNullOrWhiteSpace(barcodeOrVoucherNo)
                        ? "Voucher barcode or number is required."
                        : "Voucher was not found."
                };
            }

            if (IsExpiredByDate(voucher))
                return BuildRedeemValidationResult(voucher, false, false, "Voucher is expired.", 0m, 0m);
            if (!GiftVoucherStatusCodes.Equals(voucher.Status, GiftVoucherStatusCodes.Active))
                return BuildRedeemValidationResult(voucher, false, false, $"Voucher cannot be redeemed. Current status: {DisplayStatus(voucher)}.", 0m, 0m);
            if (voucher.VoucherAmount <= 0m)
                return BuildRedeemValidationResult(voucher, false, false, "Voucher amount is invalid.", 0m, 0m);

            decimal amountToApply = Money(Math.Min(voucher.VoucherAmount, balanceDue));
            decimal forfeitedAmount = Money(voucher.VoucherAmount - amountToApply);

            if (forfeitedAmount > 0m && !allowForfeitWithManagerApproval)
            {
                return BuildRedeemValidationResult(
                    voucher,
                    false,
                    true,
                    $"Voucher value is higher than balance due. Rs. {forfeitedAmount:N2} will be forfeited. Manager approval required.",
                    amountToApply,
                    forfeitedAmount);
            }

            return BuildRedeemValidationResult(
                voucher,
                true,
                false,
                forfeitedAmount > 0m
                    ? $"Voucher approved. Rs. {forfeitedAmount:N2} will be forfeited."
                    : "Voucher can be redeemed.",
                amountToApply,
                forfeitedAmount);
        }

        public async Task MarkVoucherSoldAsync(
            int giftVoucherId,
            SalesHeader salesHeader,
            string cashierName,
            string terminalNo,
            string remarks = "")
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                await MarkVoucherSoldAsync(context, giftVoucherId, salesHeader, cashierName, terminalNo, remarks);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public static async Task MarkVoucherSoldAsync(
            AppDbContext context,
            int giftVoucherId,
            SalesHeader salesHeader,
            string cashierName,
            string terminalNo,
            string remarks = "")
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (salesHeader == null || salesHeader.Id <= 0)
                throw new InvalidOperationException("Saved sales header is required to activate a gift voucher.");

            GiftVoucher voucher = await context.GiftVouchers
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == giftVoucherId)
                ?? throw new InvalidOperationException("Gift voucher was not found.");

            if (IsExpiredByDate(voucher))
                throw new InvalidOperationException("Gift voucher is expired and cannot be sold.");
            if (!GiftVoucherStatusCodes.Equals(voucher.Status, GiftVoucherStatusCodes.Created))
                throw new InvalidOperationException($"Gift voucher cannot be sold. Current status: {DisplayStatus(voucher)}.");
            if (voucher.VoucherAmount <= 0m)
                throw new InvalidOperationException("Gift voucher amount is invalid.");

            DateTime now = DateTime.Now;
            string safeCashier = RequiredText(cashierName, "Cashier name is required.");
            string safeTerminal = RequiredText(terminalNo, "Terminal number is required.");

            int affected = await context.GiftVouchers
                .Where(v => v.Id == giftVoucherId && v.Status == GiftVoucherStatusCodes.Created)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(v => v.Status, GiftVoucherStatusCodes.Active)
                    .SetProperty(v => v.ActivatedAt, now)
                    .SetProperty(v => v.SoldDate, now)
                    .SetProperty(v => v.SoldSalesHeaderId, salesHeader.Id)
                    .SetProperty(v => v.SoldInvoiceNo, salesHeader.InvoiceNo)
                    .SetProperty(v => v.SoldCashierName, safeCashier)
                    .SetProperty(v => v.SoldTerminalNo, safeTerminal)
                    .SetProperty(v => v.UpdatedAt, now)
                    .SetProperty(v => v.UpdatedBy, safeCashier));

            if (affected != 1)
                throw new InvalidOperationException("Gift voucher activation was rejected because its status changed.");

            voucher.Status = GiftVoucherStatusCodes.Active;
            voucher.ActivatedAt = now;
            voucher.SoldDate = now;
            voucher.SoldSalesHeaderId = salesHeader.Id;
            voucher.SoldInvoiceNo = salesHeader.InvoiceNo;
            voucher.SoldCashierName = safeCashier;
            voucher.SoldTerminalNo = safeTerminal;

            AddVoucherTransaction(
                context,
                voucher,
                GiftVoucherTransactionCodes.Activated,
                voucher.VoucherAmount,
                0m,
                0m,
                salesHeader,
                null,
                null,
                salesHeader.InvoiceNo,
                string.Empty,
                safeCashier,
                safeTerminal,
                safeCashier,
                string.Empty,
                $"ACTIVATE:{giftVoucherId}",
                string.IsNullOrWhiteSpace(remarks)
                    ? $"Voucher sold and activated from invoice {salesHeader.InvoiceNo}."
                    : remarks);
        }

        public async Task MarkVoucherRedeemedAsync(
            int giftVoucherId,
            decimal appliedAmount,
            decimal forfeitedAmount,
            SalesHeader salesHeader,
            string cashierName,
            string terminalNo,
            string authorizedBy = "",
            string remarks = "")
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                await MarkVoucherRedeemedAsync(
                    context,
                    giftVoucherId,
                    appliedAmount,
                    forfeitedAmount,
                    salesHeader,
                    null,
                    cashierName,
                    terminalNo,
                    authorizedBy,
                    remarks);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public static async Task MarkVoucherRedeemedAsync(
            AppDbContext context,
            int giftVoucherId,
            decimal appliedAmount,
            decimal forfeitedAmount,
            SalesHeader salesHeader,
            SalesPayment? salesPayment,
            string cashierName,
            string terminalNo,
            string authorizedBy = "",
            string remarks = "")
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (salesHeader == null || salesHeader.Id <= 0)
                throw new InvalidOperationException("Saved sales header is required to redeem a gift voucher.");

            GiftVoucher voucher = await context.GiftVouchers
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == giftVoucherId)
                ?? throw new InvalidOperationException("Gift voucher was not found.");

            if (IsExpiredByDate(voucher))
                throw new InvalidOperationException("Gift voucher is expired.");
            if (!GiftVoucherStatusCodes.Equals(voucher.Status, GiftVoucherStatusCodes.Active))
                throw new InvalidOperationException($"Gift voucher cannot be redeemed. Current status: {DisplayStatus(voucher)}.");

            appliedAmount = Money(appliedAmount);
            forfeitedAmount = Money(forfeitedAmount);
            string safeAuthorizedBy = NormalizeText(authorizedBy);

            if (appliedAmount <= 0m)
                throw new InvalidOperationException("Gift voucher applied amount must be greater than zero.");
            if (forfeitedAmount < 0m)
                throw new InvalidOperationException("Gift voucher forfeited amount cannot be negative.");
            if (forfeitedAmount > 0m && string.IsNullOrWhiteSpace(safeAuthorizedBy))
                throw new InvalidOperationException("Manager authorization is required for gift voucher forfeiture.");

            decimal totalConsumed = Money(appliedAmount + forfeitedAmount);
            if (Math.Abs(totalConsumed - Money(voucher.VoucherAmount)) > 0.01m)
            {
                throw new InvalidOperationException(
                    "A one-time gift voucher must be fully consumed by the applied and forfeited amounts.");
            }

            DateTime now = DateTime.Now;
            string safeCashier = RequiredText(cashierName, "Cashier name is required.");
            string safeTerminal = RequiredText(terminalNo, "Terminal number is required.");

            int affected = await context.GiftVouchers
                .Where(v => v.Id == giftVoucherId && v.Status == GiftVoucherStatusCodes.Active)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(v => v.Status, GiftVoucherStatusCodes.Redeemed)
                    .SetProperty(v => v.RedeemedDate, now)
                    .SetProperty(v => v.RedeemedSalesHeaderId, salesHeader.Id)
                    .SetProperty(v => v.RedeemedInvoiceNo, salesHeader.InvoiceNo)
                    .SetProperty(v => v.RedeemedCashierName, safeCashier)
                    .SetProperty(v => v.RedeemedTerminalNo, safeTerminal)
                    .SetProperty(v => v.RedeemedAmount, appliedAmount)
                    .SetProperty(v => v.ForfeitedAmount, forfeitedAmount)
                    .SetProperty(v => v.UpdatedAt, now)
                    .SetProperty(v => v.UpdatedBy, safeCashier));

            if (affected != 1)
                throw new InvalidOperationException("Gift voucher redemption was rejected because it was already used or its status changed.");

            voucher.Status = GiftVoucherStatusCodes.Redeemed;
            voucher.RedeemedDate = now;
            voucher.RedeemedSalesHeaderId = salesHeader.Id;
            voucher.RedeemedInvoiceNo = salesHeader.InvoiceNo;
            voucher.RedeemedCashierName = safeCashier;
            voucher.RedeemedTerminalNo = safeTerminal;
            voucher.RedeemedAmount = appliedAmount;
            voucher.ForfeitedAmount = forfeitedAmount;

            AddVoucherTransaction(
                context,
                voucher,
                GiftVoucherTransactionCodes.Redeemed,
                appliedAmount,
                appliedAmount,
                forfeitedAmount,
                salesHeader,
                salesPayment,
                null,
                salesHeader.InvoiceNo,
                string.Empty,
                safeCashier,
                safeTerminal,
                safeCashier,
                safeAuthorizedBy,
                $"REDEEM:{giftVoucherId}",
                string.IsNullOrWhiteSpace(remarks)
                    ? $"Voucher redeemed from invoice {salesHeader.InvoiceNo}."
                    : remarks);
        }

        public async Task MarkVoucherPrintedAsync(int giftVoucherId, string printedBy)
        {
            string safePrintedBy = RequiredText(printedBy, "Printed user is required.");
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            GiftVoucher voucher = await context.GiftVouchers
                .FirstOrDefaultAsync(v => v.Id == giftVoucherId)
                ?? throw new InvalidOperationException("Gift voucher was not found.");

            if (GiftVoucherStatusCodes.IsVoided(voucher.Status))
                throw new InvalidOperationException("Voided voucher cannot be printed.");

            DateTime now = DateTime.Now;
            bool isReprint = voucher.PrintCount > 0;
            voucher.PrintCount++;
            voucher.PrintedAt ??= now;
            voucher.PrintedBy = string.IsNullOrWhiteSpace(voucher.PrintedBy)
                ? safePrintedBy
                : voucher.PrintedBy;
            voucher.LastPrintedAt = now;
            voucher.LastPrintedBy = safePrintedBy;
            voucher.UpdatedAt = now;
            voucher.UpdatedBy = safePrintedBy;

            AddVoucherTransaction(
                context,
                voucher,
                isReprint ? GiftVoucherTransactionCodes.Reprinted : GiftVoucherTransactionCodes.Printed,
                voucher.VoucherAmount,
                0m,
                0m,
                null,
                null,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                safePrintedBy,
                string.Empty,
                null,
                isReprint ? "Voucher reprinted." : "Voucher printed.");

            await context.SaveChangesAsync();
        }

        public async Task BlockVoucherAsync(int giftVoucherId, string blockedBy, string reason)
        {
            string safeUser = RequiredText(blockedBy, "Authorized user is required.");
            string safeReason = RequiredText(reason, "Block reason is required.");

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            GiftVoucher voucher = await context.GiftVouchers
                .FirstOrDefaultAsync(v => v.Id == giftVoucherId)
                ?? throw new InvalidOperationException("Gift voucher was not found.");

            if (IsExpiredByDate(voucher))
                throw new InvalidOperationException("Expired voucher cannot be blocked.");
            if (GiftVoucherStatusCodes.Equals(voucher.Status, GiftVoucherStatusCodes.Redeemed))
                throw new InvalidOperationException("Redeemed voucher cannot be blocked.");
            if (GiftVoucherStatusCodes.IsVoided(voucher.Status))
                throw new InvalidOperationException("Voided voucher cannot be blocked.");
            if (GiftVoucherStatusCodes.Equals(voucher.Status, GiftVoucherStatusCodes.Blocked))
                throw new InvalidOperationException("Voucher is already blocked.");
            if (!GiftVoucherStatusCodes.Equals(voucher.Status, GiftVoucherStatusCodes.Created) &&
                !GiftVoucherStatusCodes.Equals(voucher.Status, GiftVoucherStatusCodes.Active))
                throw new InvalidOperationException($"Voucher cannot be blocked from status {DisplayStatus(voucher)}.");

            DateTime now = DateTime.Now;
            voucher.StatusBeforeBlock = voucher.Status;
            voucher.Status = GiftVoucherStatusCodes.Blocked;
            voucher.BlockedAt = now;
            voucher.BlockedBy = safeUser;
            voucher.BlockReason = safeReason;
            voucher.UpdatedAt = now;
            voucher.UpdatedBy = safeUser;

            AddVoucherTransaction(
                context, voucher, GiftVoucherTransactionCodes.Blocked,
                0m, 0m, 0m, null, null, null,
                string.Empty, string.Empty, string.Empty, string.Empty,
                safeUser, safeUser, null, safeReason);
            await context.SaveChangesAsync();
        }

        public async Task UnblockVoucherAsync(int giftVoucherId, string unblockedBy, string reason)
        {
            string safeUser = RequiredText(unblockedBy, "Authorized user is required.");
            string safeReason = RequiredText(reason, "Unblock reason is required.");

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            GiftVoucher voucher = await context.GiftVouchers
                .FirstOrDefaultAsync(v => v.Id == giftVoucherId)
                ?? throw new InvalidOperationException("Gift voucher was not found.");

            if (!GiftVoucherStatusCodes.Equals(voucher.Status, GiftVoucherStatusCodes.Blocked))
                throw new InvalidOperationException("Only a blocked voucher can be unblocked.");
            if (voucher.ExpiryDate.HasValue && voucher.ExpiryDate.Value.Date < DateTime.Today)
                throw new InvalidOperationException("Expired voucher cannot be unblocked.");

            string restoredStatus = GiftVoucherStatusCodes.Equals(
                    voucher.StatusBeforeBlock,
                    GiftVoucherStatusCodes.Active)
                ? GiftVoucherStatusCodes.Active
                : GiftVoucherStatusCodes.Created;

            DateTime now = DateTime.Now;
            voucher.Status = restoredStatus;
            voucher.StatusBeforeBlock = string.Empty;
            voucher.BlockedAt = null;
            voucher.BlockedBy = string.Empty;
            voucher.BlockReason = string.Empty;
            voucher.UpdatedAt = now;
            voucher.UpdatedBy = safeUser;

            AddVoucherTransaction(
                context, voucher, GiftVoucherTransactionCodes.Unblocked,
                0m, 0m, 0m, null, null, null,
                string.Empty, string.Empty, string.Empty, string.Empty,
                safeUser, safeUser, null, safeReason);
            await context.SaveChangesAsync();
        }

        public async Task VoidVoucherAsync(int giftVoucherId, string voidedBy, string reason)
        {
            string safeUser = RequiredText(voidedBy, "Authorized user is required.");
            string safeReason = RequiredText(reason, "Void reason is required.");

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            GiftVoucher voucher = await context.GiftVouchers
                .FirstOrDefaultAsync(v => v.Id == giftVoucherId)
                ?? throw new InvalidOperationException("Gift voucher was not found.");

            if (GiftVoucherStatusCodes.Equals(voucher.Status, GiftVoucherStatusCodes.Redeemed))
                throw new InvalidOperationException("Redeemed voucher cannot be voided.");
            if (GiftVoucherStatusCodes.IsVoided(voucher.Status))
                throw new InvalidOperationException("Voucher is already voided.");
            if (!GiftVoucherStatusCodes.Equals(voucher.Status, GiftVoucherStatusCodes.Created))
            {
                throw new InvalidOperationException(
                    "Only an unsold Created voucher can be voided. Block an issued Active voucher instead.");
            }

            DateTime now = DateTime.Now;
            voucher.Status = GiftVoucherStatusCodes.Voided;
            voucher.CancelledAt = now;
            voucher.CancelledBy = safeUser;
            voucher.CancelReason = safeReason;
            voucher.UpdatedAt = now;
            voucher.UpdatedBy = safeUser;

            AddVoucherTransaction(
                context, voucher, GiftVoucherTransactionCodes.Voided,
                0m, 0m, 0m, null, null, null,
                string.Empty, string.Empty, string.Empty, string.Empty,
                safeUser, safeUser, null, safeReason);
            await context.SaveChangesAsync();
        }

        public Task CancelVoucherAsync(int giftVoucherId, string cancelledBy, string reason) =>
            VoidVoucherAsync(giftVoucherId, cancelledBy, reason);

        public static async Task<GiftVoucher> CreateReturnVoucherAsync(
            AppDbContext context,
            CustomerReturnHeader returnHeader,
            decimal amount,
            string createdBy,
            string terminalNo,
            DateTime? expiryDate = null,
            string remarks = "")
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (returnHeader == null || returnHeader.Id <= 0)
                throw new InvalidOperationException("Saved customer return is required to issue a replacement voucher.");

            amount = Money(amount);
            if (amount <= 0m)
                throw new InvalidOperationException("Replacement gift voucher amount must be greater than zero.");

            string referenceKey = $"RETURN:{returnHeader.Id}";
            GiftVoucherTransaction? existing = await context.GiftVoucherTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.ReferenceKey == referenceKey);
            if (existing != null)
            {
                return await context.GiftVouchers
                    .FirstAsync(v => v.Id == existing.GiftVoucherId);
            }

            DateTime now = returnHeader.ReturnDate == default
                ? DateTime.Now
                : returnHeader.ReturnDate;
            string safeCreatedBy = RequiredText(createdBy, "Created user is required.");
            string safeTerminal = NormalizeText(terminalNo);
            string voucherNo = await GenerateVoucherNoAsync(context);

            var voucher = new GiftVoucher
            {
                VoucherNo = voucherNo,
                Barcode = voucherNo,
                VoucherAmount = amount,
                Status = GiftVoucherStatusCodes.Active,
                ExpiryDate = expiryDate?.Date,
                BatchNo = "RETURN",
                Description = $"Return Voucher {returnHeader.ReturnNo}",
                CreatedAt = now,
                CreatedBy = safeCreatedBy,
                ActivatedAt = now,
                UpdatedAt = now,
                UpdatedBy = safeCreatedBy,
                Remarks = string.IsNullOrWhiteSpace(remarks)
                    ? $"Issued against customer return {returnHeader.ReturnNo}."
                    : NormalizeText(remarks)
            };

            context.GiftVouchers.Add(voucher);
            await context.SaveChangesAsync();

            AddVoucherTransaction(
                context,
                voucher,
                GiftVoucherTransactionCodes.ReturnVoucherIssued,
                amount,
                amount,
                0m,
                null,
                null,
                returnHeader,
                returnHeader.OriginalInvoiceNo ?? string.Empty,
                returnHeader.ReturnNo,
                returnHeader.CashierName,
                safeTerminal,
                safeCreatedBy,
                returnHeader.AuthorizedBy,
                referenceKey,
                voucher.Remarks);

            return voucher;
        }

        private static GiftVoucherSearchDto BuildSearchDto(GiftVoucher voucher) =>
            new()
            {
                Id = voucher.Id,
                VoucherNo = voucher.VoucherNo,
                Barcode = voucher.Barcode,
                VoucherAmount = voucher.VoucherAmount,
                RedeemedAmount = voucher.RedeemedAmount,
                ForfeitedAmount = voucher.ForfeitedAmount,
                Status = voucher.Status,
                DisplayStatus = DisplayStatus(voucher),
                BatchNo = voucher.BatchNo,
                Description = voucher.Description,
                ExpiryDate = voucher.ExpiryDate,
                CreatedAt = voucher.CreatedAt,
                CreatedBy = voucher.CreatedBy,
                ActivatedAt = voucher.ActivatedAt,
                RedeemedDate = voucher.RedeemedDate,
                SoldInvoiceNo = voucher.SoldInvoiceNo,
                RedeemedInvoiceNo = voucher.RedeemedInvoiceNo,
                PrintCount = voucher.PrintCount,
                LastPrintedAt = voucher.LastPrintedAt,
                LastPrintedBy = voucher.LastPrintedBy,
                Remarks = voucher.Remarks
            };

        private static GiftVoucherSaleValidationResult BuildSaleValidationResult(
            GiftVoucher voucher,
            bool isValid,
            string message) =>
            new()
            {
                IsValid = isValid,
                Message = message,
                GiftVoucherId = voucher.Id,
                VoucherNo = voucher.VoucherNo,
                Barcode = voucher.Barcode,
                VoucherAmount = voucher.VoucherAmount,
                Status = DisplayStatus(voucher),
                ExpiryDate = voucher.ExpiryDate
            };

        private static GiftVoucherRedeemValidationResult BuildRedeemValidationResult(
            GiftVoucher voucher,
            bool isValid,
            bool requiresManagerApproval,
            string message,
            decimal amountToApply,
            decimal forfeitedAmount) =>
            new()
            {
                IsValid = isValid,
                RequiresManagerApproval = requiresManagerApproval,
                Message = message,
                GiftVoucherId = voucher.Id,
                VoucherNo = voucher.VoucherNo,
                Barcode = voucher.Barcode,
                VoucherAmount = voucher.VoucherAmount,
                AmountToApply = Money(amountToApply),
                ForfeitedAmount = Money(forfeitedAmount),
                Status = DisplayStatus(voucher),
                ExpiryDate = voucher.ExpiryDate
            };

        private static void AddVoucherTransaction(
            AppDbContext context,
            GiftVoucher voucher,
            string transactionType,
            decimal amount,
            decimal appliedAmount,
            decimal forfeitedAmount,
            SalesHeader? salesHeader,
            SalesPayment? salesPayment,
            CustomerReturnHeader? customerReturn,
            string referenceInvoiceNo,
            string referenceReturnNo,
            string cashierName,
            string terminalNo,
            string createdBy,
            string authorizedBy,
            string? referenceKey,
            string remarks)
        {
            context.GiftVoucherTransactions.Add(
                new GiftVoucherTransaction
                {
                    GiftVoucherId = voucher.Id,
                    TransactionDate = DateTime.Now,
                    TransactionType = NormalizeText(transactionType),
                    VoucherNo = voucher.VoucherNo,
                    Barcode = voucher.Barcode,
                    VoucherAmount = Money(voucher.VoucherAmount),
                    Amount = Money(amount),
                    AppliedAmount = Money(appliedAmount),
                    ForfeitedAmount = Money(forfeitedAmount),
                    StatusAfter = voucher.Status,
                    SalesHeaderId = salesHeader?.Id,
                    SalesPaymentId = salesPayment?.Id,
                    CustomerReturnHeaderId = customerReturn?.Id,
                    ReferenceInvoiceNo = NormalizeText(referenceInvoiceNo),
                    ReferenceReturnNo = NormalizeText(referenceReturnNo),
                    ReferenceKey = string.IsNullOrWhiteSpace(referenceKey)
                        ? null
                        : NormalizeText(referenceKey),
                    CashierName = NormalizeText(cashierName),
                    TerminalNo = NormalizeText(terminalNo),
                    CreatedBy = NormalizeText(createdBy),
                    AuthorizedBy = NormalizeText(authorizedBy),
                    Remarks = NormalizeText(remarks),
                    CreatedAt = DateTime.Now
                });
        }

        private static async Task<string> GenerateVoucherNoAsync(AppDbContext context)
        {
            DocumentSequence sequence = await GetOrCreateSequenceAsync(context, "GV", "GV-", 6);
            for (int attempts = 0; attempts < 1000; attempts++)
            {
                string voucherNo =
                    $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString($"D{sequence.PaddingLength}")}";
                sequence.NextSequenceNumber++;
                sequence.UpdatedAt = DateTime.Now;

                bool exists = await context.GiftVouchers
                    .AnyAsync(v => v.VoucherNo == voucherNo || v.Barcode == voucherNo);
                if (!exists)
                    return voucherNo;
            }

            throw new InvalidOperationException("Unable to generate a unique gift voucher number.");
        }

        private static async Task<string> GenerateBatchNoAsync(AppDbContext context)
        {
            DocumentSequence sequence = await GetOrCreateSequenceAsync(context, "GVB", "GVB-", 6);
            string batchNo =
                $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString($"D{sequence.PaddingLength}")}";
            sequence.NextSequenceNumber++;
            sequence.UpdatedAt = DateTime.Now;
            return batchNo;
        }

        private static async Task<DocumentSequence> GetOrCreateSequenceAsync(
            AppDbContext context,
            string documentType,
            string prefix,
            int paddingLength)
        {
            DocumentSequence? sequence = context.DocumentSequences.Local
                .FirstOrDefault(s => string.Equals(
                    s.DocumentType,
                    documentType,
                    StringComparison.OrdinalIgnoreCase));
            if (sequence != null)
                return sequence;

            sequence = await context.DocumentSequences
                .FirstOrDefaultAsync(s => s.DocumentType == documentType);
            if (sequence != null)
                return sequence;

            sequence = new DocumentSequence
            {
                DocumentType = documentType,
                Prefix = prefix,
                NextSequenceNumber = 1,
                PaddingLength = paddingLength,
                UpdatedAt = DateTime.Now
            };
            context.DocumentSequences.Add(sequence);
            return sequence;
        }

        private static bool IsExpiredByDate(GiftVoucher voucher) =>
            voucher.ExpiryDate.HasValue &&
            voucher.ExpiryDate.Value.Date < DateTime.Today &&
            !GiftVoucherStatusCodes.IsTerminal(voucher.Status) &&
            !GiftVoucherStatusCodes.Equals(voucher.Status, GiftVoucherStatusCodes.Blocked);

        private static string DisplayStatus(GiftVoucher voucher)
        {
            if (IsExpiredByDate(voucher))
                return GiftVoucherStatusCodes.Expired;
            if (GiftVoucherStatusCodes.IsVoided(voucher.Status))
                return GiftVoucherStatusCodes.Voided;
            return voucher.Status;
        }

        private static decimal Money(decimal value) =>
            Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private static string RequiredText(string? value, string message)
        {
            string safe = NormalizeText(value);
            if (string.IsNullOrWhiteSpace(safe))
                throw new InvalidOperationException(message);
            return safe;
        }

        private static string NormalizeText(string? value) =>
            (value ?? string.Empty).Trim();
    }
}
