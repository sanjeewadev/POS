using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Core.Repositories
{
    public class GiftVoucherRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private static readonly Random _random = new Random();

        public GiftVoucherRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =================================================================================
        // 1. ADMIN: Get Vouchers for Back-Office Grid
        // =================================================================================
        public async Task<List<GiftVoucherSummaryDto>> GetVoucherSummariesAsync(string statusFilter = "All")
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.GiftVouchers.AsNoTracking().AsQueryable();

            if (statusFilter != "All")
            {
                query = query.Where(v => v.Status == statusFilter);
            }

            return await query
                .OrderByDescending(v => v.CreatedDate)
                .Select(v => new GiftVoucherSummaryDto
                {
                    Id = v.Id,
                    Barcode = v.Barcode,
                    InitialAmount = v.InitialAmount,
                    CurrentBalance = v.CurrentBalance,
                    Status = v.Status,
                    CreatedDate = v.CreatedDate,
                    ActivationDate = v.ActivationDate,
                    ExpiryDate = v.ExpiryDate
                })
                .ToListAsync();
        }

        // =================================================================================
        // 2. ADMIN: Generate a Batch of Inactive Vouchers (For physical printing)
        // =================================================================================
        public async Task GenerateVoucherBatchAsync(int count, decimal amount)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var newVouchers = new List<GiftVoucher>();

            for (int i = 0; i < count; i++)
            {
                string newBarcode;
                bool isUnique;

                // Ensure the 12-digit barcode is absolutely unique in the database
                do
                {
                    newBarcode = GenerateSecureBarcode();
                    isUnique = !await context.GiftVouchers.AnyAsync(v => v.Barcode == newBarcode);
                } while (!isUnique);

                newVouchers.Add(new GiftVoucher
                {
                    Barcode = newBarcode,
                    InitialAmount = amount,
                    CurrentBalance = amount,
                    Status = "Inactive", // VERY IMPORTANT: Safe to put on shop shelves
                    CreatedDate = DateTime.Now
                });
            }

            context.GiftVouchers.AddRange(newVouchers);
            await context.SaveChangesAsync();
        }

        // =================================================================================
        // 3. CASHIER: Issue / Sell a Voucher to a Customer
        // =================================================================================
        public async Task<bool> IssueVoucherAsync(string scannedBarcode, string invoiceNo, string customerName, int validityMonths = 12)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var voucher = await context.GiftVouchers.FirstOrDefaultAsync(v => v.Barcode == scannedBarcode);

            if (voucher == null) throw new Exception("Voucher barcode not found in the system.");
            if (voucher.Status != "Inactive") throw new Exception($"Cannot issue this voucher. Current status is: {voucher.Status}");

            // Activate the liability
            voucher.Status = "Active";
            voucher.ActivationDate = DateTime.Now;
            voucher.ExpiryDate = DateTime.Now.AddMonths(validityMonths);
            voucher.SoldInInvoiceNo = invoiceNo;
            voucher.CustomerName = customerName;

            await context.SaveChangesAsync();
            return true;
        }

        // =================================================================================
        // 4. CASHIER: Validate Voucher before applying to a sale
        // =================================================================================
        public async Task<VoucherValidationResultDto> ValidateVoucherForPaymentAsync(string scannedBarcode)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var voucher = await context.GiftVouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Barcode == scannedBarcode);

            if (voucher == null) return new VoucherValidationResultDto { IsValid = false, ErrorMessage = "Invalid Barcode." };
            if (voucher.Status == "Inactive") return new VoucherValidationResultDto { IsValid = false, ErrorMessage = "Card has not been activated at the till." };
            if (voucher.Status == "Blocked") return new VoucherValidationResultDto { IsValid = false, ErrorMessage = "Card has been reported lost or stolen." };
            if (voucher.Status == "Exhausted") return new VoucherValidationResultDto { IsValid = false, ErrorMessage = "Card balance is zero." };

            if (voucher.ExpiryDate.HasValue && voucher.ExpiryDate.Value < DateTime.Now)
            {
                return new VoucherValidationResultDto { IsValid = false, ErrorMessage = "Card has expired." };
            }

            return new VoucherValidationResultDto
            {
                IsValid = true,
                AvailableBalance = voucher.CurrentBalance,
                VoucherId = voucher.Id
            };
        }

        // =================================================================================
        // 5. CASHIER: Redeem / Deduct Balance during Checkout
        // =================================================================================
        public async Task RedeemVoucherBalanceAsync(int voucherId, decimal amountToDeduct)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var voucher = await context.GiftVouchers.FindAsync(voucherId);
            if (voucher == null) throw new Exception("Voucher not found.");

            if (voucher.CurrentBalance < amountToDeduct)
                throw new Exception("Insufficient funds on the voucher.");

            voucher.CurrentBalance -= amountToDeduct;

            // Auto-Exhaust if empty
            if (voucher.CurrentBalance <= 0)
            {
                voucher.Status = "Exhausted";
            }

            await context.SaveChangesAsync();
        }

        // =================================================================================
        // 6. ADMIN: The Kill Switch (Block Stolen/Lost Cards)
        // =================================================================================
        public async Task BlockVoucherAsync(int voucherId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var voucher = await context.GiftVouchers.FindAsync(voucherId);

            if (voucher != null)
            {
                voucher.Status = "Blocked";
                await context.SaveChangesAsync();
            }
        }

        // --- Helper Method: Generates a 12-digit random string ---
        private string GenerateSecureBarcode()
        {
            // Example format: 489274819384
            string result = "";
            for (int i = 0; i < 12; i++)
            {
                result += _random.Next(0, 10).ToString();
            }
            return result;
        }

        // =================================================================================
        // CASHIER: Look up an Inactive card before adding to cart
        // =================================================================================
        public async Task<GiftVoucherSummaryDto?> GetInactiveVoucherAsync(string barcode)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var voucher = await context.GiftVouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Barcode == barcode);

            if (voucher == null || voucher.Status != "Inactive") return null;

            return new GiftVoucherSummaryDto
            {
                Id = voucher.Id,
                Barcode = voucher.Barcode,
                InitialAmount = voucher.InitialAmount,
                Status = voucher.Status
            };
        }

        // =================================================================================
        // 5. CASHIER: Redeem Single-Use Voucher (No Change Given / Breakage Profit)
        // =================================================================================
        public async Task<decimal> RedeemSingleUseVoucherAsync(int voucherId, decimal amountDue)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var voucher = await context.GiftVouchers.FindAsync(voucherId);
            if (voucher == null) throw new Exception("Voucher not found.");
            if (voucher.Status != "Active") throw new Exception($"Cannot redeem. Voucher status is {voucher.Status}.");
            if (voucher.CurrentBalance <= 0) throw new Exception("Voucher has zero balance.");

            // Calculate exactly how much of the bill this voucher can cover
            decimal amountApplied = Math.Min(amountDue, voucher.CurrentBalance);

            // SINGLE-USE RULE: The card is instantly drained to zero, regardless of the cart total.
            // If (amountApplied < CurrentBalance), the difference becomes instant store profit (Breakage).
            voucher.CurrentBalance = 0;
            voucher.Status = "Exhausted";

            await context.SaveChangesAsync();

            // We return the actual amount applied so the UI knows exactly how much to subtract from the bill
            return amountApplied;
        }
    }
}