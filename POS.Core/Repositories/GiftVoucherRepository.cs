using Microsoft.EntityFrameworkCore;
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

        public string Status { get; set; } = "Created";

        public string DisplayStatus { get; set; } = "Created";

        public string BatchNo { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime? ExpiryDate { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ActivatedAt { get; set; }

        public DateTime? RedeemedDate { get; set; }

        public string SoldInvoiceNo { get; set; } = string.Empty;

        public string RedeemedInvoiceNo { get; set; } = string.Empty;

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
            _contextFactory = contextFactory;
        }

        // =========================================================
        // ADMIN: CREATE VOUCHER BATCH
        // =========================================================

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

            if (voucherAmount <= 0m)
                throw new InvalidOperationException("Voucher amount must be greater than zero.");

            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                DateTime now = DateTime.Now;

                string safeCreatedBy = NormalizeText(createdBy);
                string safeBatchNo = NormalizeText(batchNo);

                if (string.IsNullOrWhiteSpace(safeBatchNo))
                    safeBatchNo = await GenerateBatchNoAsync(context);

                string safeDescription = string.IsNullOrWhiteSpace(description)
                    ? $"Gift Voucher Rs. {voucherAmount:N2}"
                    : NormalizeText(description);

                var generated = new List<GiftVoucher>();

                for (int i = 0; i < count; i++)
                {
                    string voucherNo = await GenerateVoucherNoAsync(context);

                    var voucher = new GiftVoucher
                    {
                        VoucherNo = voucherNo,
                        Barcode = voucherNo,
                        VoucherAmount = Math.Round(voucherAmount, 2),
                        Status = "Created",
                        ExpiryDate = expiryDate,
                        BatchNo = safeBatchNo,
                        Description = safeDescription,
                        CreatedAt = now,
                        CreatedBy = safeCreatedBy,
                        Remarks = "Generated from voucher batch."
                    };

                    context.GiftVouchers.Add(voucher);
                    generated.Add(voucher);
                }

                await context.SaveChangesAsync();

                foreach (var voucher in generated)
                {
                    await AddVoucherTransactionAsync(
                        context,
                        voucher,
                        "Created",
                        voucher.VoucherAmount,
                        0m,
                        0m,
                        null,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        safeCreatedBy,
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

        // =========================================================
        // SEARCH / INQUIRY
        // =========================================================

        public async Task<List<GiftVoucherSearchDto>> SearchVouchersAsync(
            string filter = "All",
            string searchTerm = "",
            int take = 200)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            string safeFilter = NormalizeText(filter);
            string safeTerm = NormalizeText(searchTerm).ToLower();

            if (take <= 0)
                take = 200;

            if (take > 1000)
                take = 1000;

            var query = context.GiftVouchers.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(safeFilter) &&
                !safeFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(v => v.Status == safeFilter);
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

            var vouchers = await query
                .OrderByDescending(v => v.CreatedAt)
                .ThenBy(v => v.VoucherNo)
                .Take(take)
                .ToListAsync();

            return vouchers
                .Select(BuildSearchDto)
                .ToList();
        }

        public async Task<GiftVoucher?> GetByBarcodeOrVoucherNoAsync(string barcodeOrVoucherNo)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            string code = NormalizeText(barcodeOrVoucherNo);

            if (string.IsNullOrWhiteSpace(code))
                return null;

            return await context.GiftVouchers
                .AsNoTracking()
                .FirstOrDefaultAsync(v =>
                    v.Barcode == code ||
                    v.VoucherNo == code);
        }

        public async Task<List<GiftVoucherTransaction>> GetVoucherHistoryAsync(int giftVoucherId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.GiftVoucherTransactions
                .AsNoTracking()
                .Where(t => t.GiftVoucherId == giftVoucherId)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();
        }

        // =========================================================
        // CASHIER: VALIDATE SELLING A VOUCHER
        // =========================================================

        public async Task<GiftVoucherSaleValidationResult> ValidateVoucherForSaleAsync(
            string barcodeOrVoucherNo)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            string code = NormalizeText(barcodeOrVoucherNo);

            if (string.IsNullOrWhiteSpace(code))
            {
                return new GiftVoucherSaleValidationResult
                {
                    IsValid = false,
                    Message = "Voucher barcode or number is required."
                };
            }

            var voucher = await context.GiftVouchers
                .AsNoTracking()
                .FirstOrDefaultAsync(v =>
                    v.Barcode == code ||
                    v.VoucherNo == code);

            if (voucher == null)
            {
                return new GiftVoucherSaleValidationResult
                {
                    IsValid = false,
                    Message = "Voucher was not found."
                };
            }

            if (IsExpiredByDate(voucher))
            {
                return BuildSaleValidationResult(
                    voucher,
                    false,
                    "Voucher is expired and cannot be sold.");
            }

            if (!voucher.Status.Equals("Created", StringComparison.OrdinalIgnoreCase))
            {
                return BuildSaleValidationResult(
                    voucher,
                    false,
                    $"Voucher cannot be sold. Current status: {voucher.Status}.");
            }

            if (voucher.VoucherAmount <= 0m)
            {
                return BuildSaleValidationResult(
                    voucher,
                    false,
                    "Voucher amount is invalid.");
            }

            return BuildSaleValidationResult(
                voucher,
                true,
                "Voucher can be sold.");
        }

        // =========================================================
        // CASHIER: VALIDATE REDEMPTION
        // =========================================================

        public async Task<GiftVoucherRedeemValidationResult> ValidateVoucherForRedemptionAsync(
            string barcodeOrVoucherNo,
            decimal balanceDue,
            bool allowForfeitWithManagerApproval = false)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            string code = NormalizeText(barcodeOrVoucherNo);

            if (string.IsNullOrWhiteSpace(code))
            {
                return new GiftVoucherRedeemValidationResult
                {
                    IsValid = false,
                    Message = "Voucher barcode or number is required."
                };
            }

            if (balanceDue <= 0m)
            {
                return new GiftVoucherRedeemValidationResult
                {
                    IsValid = false,
                    Message = "Invoice is already fully paid."
                };
            }

            var voucher = await context.GiftVouchers
                .AsNoTracking()
                .FirstOrDefaultAsync(v =>
                    v.Barcode == code ||
                    v.VoucherNo == code);

            if (voucher == null)
            {
                return new GiftVoucherRedeemValidationResult
                {
                    IsValid = false,
                    Message = "Voucher was not found."
                };
            }

            if (IsExpiredByDate(voucher))
            {
                return BuildRedeemValidationResult(
                    voucher,
                    false,
                    false,
                    "Voucher is expired.",
                    0m,
                    0m);
            }

            if (!voucher.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                return BuildRedeemValidationResult(
                    voucher,
                    false,
                    false,
                    $"Voucher cannot be redeemed. Current status: {voucher.Status}.",
                    0m,
                    0m);
            }

            if (voucher.VoucherAmount <= 0m)
            {
                return BuildRedeemValidationResult(
                    voucher,
                    false,
                    false,
                    "Voucher amount is invalid.",
                    0m,
                    0m);
            }

            decimal amountToApply;
            decimal forfeitedAmount;

            if (voucher.VoucherAmount > balanceDue)
            {
                amountToApply = Math.Round(balanceDue, 2);
                forfeitedAmount = Math.Round(voucher.VoucherAmount - balanceDue, 2);

                if (!allowForfeitWithManagerApproval)
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
                    $"Voucher approved. Rs. {forfeitedAmount:N2} will be forfeited.",
                    amountToApply,
                    forfeitedAmount);
            }

            amountToApply = Math.Round(voucher.VoucherAmount, 2);
            forfeitedAmount = 0m;

            return BuildRedeemValidationResult(
                voucher,
                true,
                false,
                "Voucher can be redeemed.",
                amountToApply,
                forfeitedAmount);
        }

        // =========================================================
        // SALES CHECKOUT SUPPORT: ACTIVATE SOLD VOUCHER
        // =========================================================

        public async Task MarkVoucherSoldAsync(
            int giftVoucherId,
            SalesHeader salesHeader,
            string cashierName,
            string terminalNo,
            string remarks = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                await MarkVoucherSoldAsync(
                    context,
                    giftVoucherId,
                    salesHeader,
                    cashierName,
                    terminalNo,
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

        public static async Task MarkVoucherSoldAsync(
            AppDbContext context,
            int giftVoucherId,
            SalesHeader salesHeader,
            string cashierName,
            string terminalNo,
            string remarks = "")
        {
            var voucher = await context.GiftVouchers
                .FirstOrDefaultAsync(v => v.Id == giftVoucherId);

            if (voucher == null)
                throw new InvalidOperationException("Gift voucher was not found.");

            if (IsExpiredByDate(voucher))
                throw new InvalidOperationException("Gift voucher is expired and cannot be sold.");

            if (!voucher.Status.Equals("Created", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Gift voucher cannot be sold. Current status: {voucher.Status}.");

            if (voucher.VoucherAmount <= 0m)
                throw new InvalidOperationException("Gift voucher amount is invalid.");

            DateTime now = DateTime.Now;

            voucher.Status = "Active";
            voucher.ActivatedAt = now;
            voucher.SoldDate = now;
            voucher.SoldSalesHeaderId = salesHeader.Id;
            voucher.SoldInvoiceNo = salesHeader.InvoiceNo;
            voucher.SoldCashierName = NormalizeText(cashierName);
            voucher.SoldTerminalNo = NormalizeText(terminalNo);
            voucher.UpdatedAt = now;
            voucher.UpdatedBy = NormalizeText(cashierName);

            await AddVoucherTransactionAsync(
                context,
                voucher,
                "Activated",
                voucher.VoucherAmount,
                0m,
                0m,
                salesHeader,
                salesHeader.InvoiceNo,
                cashierName,
                terminalNo,
                cashierName,
                string.IsNullOrWhiteSpace(remarks)
                    ? $"Voucher sold and activated from invoice {salesHeader.InvoiceNo}."
                    : remarks);
        }

        // =========================================================
        // SALES CHECKOUT SUPPORT: REDEEM VOUCHER
        // =========================================================

        public async Task MarkVoucherRedeemedAsync(
            int giftVoucherId,
            decimal appliedAmount,
            decimal forfeitedAmount,
            SalesHeader salesHeader,
            string cashierName,
            string terminalNo,
            string remarks = "")
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                await MarkVoucherRedeemedAsync(
                    context,
                    giftVoucherId,
                    appliedAmount,
                    forfeitedAmount,
                    salesHeader,
                    cashierName,
                    terminalNo,
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
            string cashierName,
            string terminalNo,
            string remarks = "")
        {
            var voucher = await context.GiftVouchers
                .FirstOrDefaultAsync(v => v.Id == giftVoucherId);

            if (voucher == null)
                throw new InvalidOperationException("Gift voucher was not found.");

            if (IsExpiredByDate(voucher))
                throw new InvalidOperationException("Gift voucher is expired.");

            if (!voucher.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Gift voucher cannot be redeemed. Current status: {voucher.Status}.");

            appliedAmount = Math.Round(appliedAmount, 2);
            forfeitedAmount = Math.Round(forfeitedAmount, 2);

            if (appliedAmount <= 0m)
                throw new InvalidOperationException("Gift voucher applied amount must be greater than zero.");

            if (appliedAmount > voucher.VoucherAmount)
                throw new InvalidOperationException("Applied amount cannot exceed voucher amount.");

            if (forfeitedAmount < 0m)
                forfeitedAmount = 0m;

            if (Math.Round(appliedAmount + forfeitedAmount, 2) > voucher.VoucherAmount)
                throw new InvalidOperationException("Applied plus forfeited amount cannot exceed voucher amount.");

            DateTime now = DateTime.Now;

            voucher.Status = "Redeemed";
            voucher.RedeemedDate = now;
            voucher.RedeemedSalesHeaderId = salesHeader.Id;
            voucher.RedeemedInvoiceNo = salesHeader.InvoiceNo;
            voucher.RedeemedCashierName = NormalizeText(cashierName);
            voucher.RedeemedTerminalNo = NormalizeText(terminalNo);
            voucher.RedeemedAmount = appliedAmount;
            voucher.ForfeitedAmount = forfeitedAmount;
            voucher.UpdatedAt = now;
            voucher.UpdatedBy = NormalizeText(cashierName);

            await AddVoucherTransactionAsync(
                context,
                voucher,
                "Redeemed",
                appliedAmount,
                appliedAmount,
                forfeitedAmount,
                salesHeader,
                salesHeader.InvoiceNo,
                cashierName,
                terminalNo,
                cashierName,
                string.IsNullOrWhiteSpace(remarks)
                    ? $"Voucher redeemed from invoice {salesHeader.InvoiceNo}."
                    : remarks);
        }

        // =========================================================
        // ADMIN ACTIONS
        // =========================================================

        public async Task BlockVoucherAsync(int giftVoucherId, string blockedBy, string reason)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var voucher = await context.GiftVouchers
                .FirstOrDefaultAsync(v => v.Id == giftVoucherId);

            if (voucher == null)
                throw new InvalidOperationException("Gift voucher was not found.");

            if (voucher.Status.Equals("Redeemed", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Redeemed voucher cannot be blocked.");

            if (voucher.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cancelled voucher cannot be blocked.");

            DateTime now = DateTime.Now;

            voucher.Status = "Blocked";
            voucher.BlockedAt = now;
            voucher.BlockedBy = NormalizeText(blockedBy);
            voucher.BlockReason = NormalizeText(reason);
            voucher.UpdatedAt = now;
            voucher.UpdatedBy = NormalizeText(blockedBy);

            await AddVoucherTransactionAsync(
                context,
                voucher,
                "Blocked",
                0m,
                0m,
                0m,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                blockedBy,
                voucher.BlockReason);

            await context.SaveChangesAsync();
        }

        public async Task CancelVoucherAsync(int giftVoucherId, string cancelledBy, string reason)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var voucher = await context.GiftVouchers
                .FirstOrDefaultAsync(v => v.Id == giftVoucherId);

            if (voucher == null)
                throw new InvalidOperationException("Gift voucher was not found.");

            if (voucher.Status.Equals("Redeemed", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Redeemed voucher cannot be cancelled.");

            DateTime now = DateTime.Now;

            voucher.Status = "Cancelled";
            voucher.CancelledAt = now;
            voucher.CancelledBy = NormalizeText(cancelledBy);
            voucher.CancelReason = NormalizeText(reason);
            voucher.UpdatedAt = now;
            voucher.UpdatedBy = NormalizeText(cancelledBy);

            await AddVoucherTransactionAsync(
                context,
                voucher,
                "Cancelled",
                0m,
                0m,
                0m,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                cancelledBy,
                voucher.CancelReason);

            await context.SaveChangesAsync();
        }

        // =========================================================
        // PRIVATE HELPERS
        // =========================================================

        private static GiftVoucherSearchDto BuildSearchDto(GiftVoucher voucher)
        {
            return new GiftVoucherSearchDto
            {
                Id = voucher.Id,
                VoucherNo = voucher.VoucherNo,
                Barcode = voucher.Barcode,
                VoucherAmount = voucher.VoucherAmount,
                Status = voucher.Status,
                DisplayStatus = IsExpiredByDate(voucher) ? "Expired" : voucher.Status,
                BatchNo = voucher.BatchNo,
                Description = voucher.Description,
                ExpiryDate = voucher.ExpiryDate,
                CreatedAt = voucher.CreatedAt,
                ActivatedAt = voucher.ActivatedAt,
                RedeemedDate = voucher.RedeemedDate,
                SoldInvoiceNo = voucher.SoldInvoiceNo,
                RedeemedInvoiceNo = voucher.RedeemedInvoiceNo,
                Remarks = voucher.Remarks
            };
        }

        private static GiftVoucherSaleValidationResult BuildSaleValidationResult(
            GiftVoucher voucher,
            bool isValid,
            string message)
        {
            return new GiftVoucherSaleValidationResult
            {
                IsValid = isValid,
                Message = message,
                GiftVoucherId = voucher.Id,
                VoucherNo = voucher.VoucherNo,
                Barcode = voucher.Barcode,
                VoucherAmount = voucher.VoucherAmount,
                Status = voucher.Status,
                ExpiryDate = voucher.ExpiryDate
            };
        }

        private static GiftVoucherRedeemValidationResult BuildRedeemValidationResult(
            GiftVoucher voucher,
            bool isValid,
            bool requiresManagerApproval,
            string message,
            decimal amountToApply,
            decimal forfeitedAmount)
        {
            return new GiftVoucherRedeemValidationResult
            {
                IsValid = isValid,
                RequiresManagerApproval = requiresManagerApproval,
                Message = message,
                GiftVoucherId = voucher.Id,
                VoucherNo = voucher.VoucherNo,
                Barcode = voucher.Barcode,
                VoucherAmount = voucher.VoucherAmount,
                AmountToApply = Math.Round(amountToApply, 2),
                ForfeitedAmount = Math.Round(forfeitedAmount, 2),
                Status = voucher.Status,
                ExpiryDate = voucher.ExpiryDate
            };
        }

        private static async Task AddVoucherTransactionAsync(
            AppDbContext context,
            GiftVoucher voucher,
            string transactionType,
            decimal amount,
            decimal appliedAmount,
            decimal forfeitedAmount,
            SalesHeader? salesHeader,
            string referenceInvoiceNo,
            string cashierName,
            string terminalNo,
            string createdBy,
            string remarks)
        {
            var transaction = new GiftVoucherTransaction
            {
                GiftVoucherId = voucher.Id,
                TransactionDate = DateTime.Now,
                TransactionType = NormalizeText(transactionType),
                VoucherNo = voucher.VoucherNo,
                Barcode = voucher.Barcode,
                VoucherAmount = voucher.VoucherAmount,
                Amount = Math.Round(amount, 2),
                AppliedAmount = Math.Round(appliedAmount, 2),
                ForfeitedAmount = Math.Round(forfeitedAmount, 2),
                StatusAfter = voucher.Status,
                SalesHeaderId = salesHeader?.Id,
                ReferenceInvoiceNo = NormalizeText(referenceInvoiceNo),
                CashierName = NormalizeText(cashierName),
                TerminalNo = NormalizeText(terminalNo),
                CreatedBy = NormalizeText(createdBy),
                Remarks = NormalizeText(remarks),
                CreatedAt = DateTime.Now
            };

            await context.GiftVoucherTransactions.AddAsync(transaction);
        }

        private static async Task<string> GenerateVoucherNoAsync(AppDbContext context)
        {
            var sequence = await GetOrCreateSequenceAsync(
                context,
                "GV",
                "GV-",
                6);

            int attempts = 0;

            while (attempts < 1000)
            {
                string voucherNo = $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString($"D{sequence.PaddingLength}")}";

                sequence.NextSequenceNumber++;
                sequence.UpdatedAt = DateTime.Now;

                bool exists = await context.GiftVouchers
                    .AnyAsync(v => v.VoucherNo == voucherNo || v.Barcode == voucherNo);

                if (!exists)
                    return voucherNo;

                attempts++;
            }

            throw new InvalidOperationException("Unable to generate a unique gift voucher number.");
        }

        private static async Task<string> GenerateBatchNoAsync(AppDbContext context)
        {
            var sequence = await GetOrCreateSequenceAsync(
                context,
                "GVB",
                "GVB-",
                6);

            string batchNo = $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString($"D{sequence.PaddingLength}")}";

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
            var sequence = await context.DocumentSequences
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

            await context.DocumentSequences.AddAsync(sequence);

            return sequence;
        }

        private static bool IsExpiredByDate(GiftVoucher voucher)
        {
            return voucher.ExpiryDate.HasValue &&
                   voucher.ExpiryDate.Value.Date < DateTime.Today &&
                   !voucher.Status.Equals("Redeemed", StringComparison.OrdinalIgnoreCase) &&
                   !voucher.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) &&
                   !voucher.Status.Equals("Blocked", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeText(string? value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}