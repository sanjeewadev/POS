using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Interfaces;
using POS.Core.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Services
{
    public class CashMovementService : ICashMovementService
    {
        private readonly AppDbContext _context;

        public CashMovementService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> VerifyManagerPinAsync(string pin)
        {
            // In a real system, you hash the PIN and compare against the Users table.
            // For now, checking against a hardcoded highly-privileged role.
            var manager = await _context.Users
                .FirstOrDefaultAsync(u => u.PosPinHash == pin && u.Role == "Manager");

            return manager != null;
        }

        public async Task<decimal> GetCurrentDrawerBalanceAsync(int shiftSessionId)
        {
            // 1. Get Opening Float
            var openingFloat = await _context.CashMovements
                .Where(m => m.ShiftSessionId == shiftSessionId && m.MovementType == "Float")
                .SumAsync(m => m.Amount);

            // 2. Get Cash Sales (Assuming you have a Sales or Payments table)
            // var cashSales = await _context.Payments.Where(p => p.ShiftId == shiftSessionId && p.Method == "Cash").SumAsync(p => p.Amount);
            decimal cashSales = 0m; // Replace with actual sales table query

            // 3. Get Paid Ins
            var paidIns = await _context.CashMovements
                .Where(m => m.ShiftSessionId == shiftSessionId && m.MovementType == "Paid In")
                .SumAsync(m => m.Amount);

            // 4. Get Paid Outs
            var paidOuts = await _context.CashMovements
                .Where(m => m.ShiftSessionId == shiftSessionId && m.MovementType == "Paid Out")
                .SumAsync(m => m.Amount);

            // Formula: Float + Sales + PaidIns - PaidOuts
            return (openingFloat + cashSales + paidIns) - paidOuts;
        }

        public async Task<CashMovement> ProcessMovementAsync(
            int shiftSessionId,
            string movementType,
            decimal amount,
            string reasonCategory,
            string remarks,
            string cashierName,
            string authorizedBy)
        {
            // 1. Security Check: Block overdrafts on Paid Outs
            if (movementType == "Paid Out")
            {
                decimal currentBalance = await GetCurrentDrawerBalanceAsync(shiftSessionId);
                if (amount > currentBalance)
                {
                    throw new InvalidOperationException($"Insufficient funds. The drawer only contains Rs. {currentBalance:N2}. You cannot pay out Rs. {amount:N2}.");
                }
            }

            // 2. Generate the immutable Voucher Number (e.g., PI-20260601-001)
            string prefix = movementType == "Paid In" ? "PI" : movementType == "Paid Out" ? "PO" : "FL";
            string dateString = DateTime.Now.ToString("yyyyMMdd");
            string voucherNumber = await GenerateVoucherNumberAsync(prefix, dateString);

            // 3. Create the Database Record
            var movement = new CashMovement
            {
                ShiftSessionId = shiftSessionId,
                MovementType = movementType,
                Amount = amount,
                ReasonCategory = reasonCategory,
                Remarks = remarks,
                CashierName = cashierName,
                AuthorizedBy = authorizedBy,
                Timestamp = DateTime.Now,
                ReferenceVoucherNo = voucherNumber
            };

            _context.CashMovements.Add(movement);
            await _context.SaveChangesAsync();

            return movement;
        }

        private async Task<string> GenerateVoucherNumberAsync(string prefix, string dateString)
        {
            // Find all movements from today that share this prefix to calculate the next sequence number
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            int dailyCount = await _context.CashMovements
                .Where(m => m.ReferenceVoucherNo.StartsWith(prefix)
                         && m.Timestamp >= today
                         && m.Timestamp < tomorrow)
                .CountAsync();

            int nextSequence = dailyCount + 1;

            // Formats to PI-20260601-001
            return $"{prefix}-{dateString}-{nextSequence:D3}";
        }
    }
}