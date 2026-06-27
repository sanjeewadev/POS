using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data;

namespace POS.Core.Repositories
{
    public class TillRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public TillRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<ShiftSession?> GetActiveShiftAsync(string terminalNo)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.ShiftSessions.FirstOrDefaultAsync(s => s.TerminalNo == terminalNo && s.Status == "Open");
        }

        public async Task<ShiftSession> CreateNewShiftAsync(string terminalNo, string cashierName)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var newShift = new ShiftSession
            {
                TerminalNo = terminalNo,
                CashierName = cashierName,
                StartTime = DateTime.Now,
                Status = "Open",
                OpeningCash = 0m
            };
            context.ShiftSessions.Add(newShift);
            await context.SaveChangesAsync();
            return newShift;
        }

        public async Task<decimal> GetCurrentFloatBalanceAsync(int shiftId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var shift = await context.ShiftSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == shiftId);
            return shift?.OpeningCash ?? 0m;
        }

        public async Task<bool> InjectFloatAsync(int shiftId, decimal amount, string managerName)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var shift = await context.ShiftSessions.FindAsync(shiftId);
            if (shift == null || shift.Status != "Open") return false;

            shift.OpeningCash += amount;
            var movement = new CashMovement
            {
                ShiftSessionId = shiftId,
                MovementType = "Paid In",
                ReasonCategory = "Opening Float",
                Amount = amount,
                CashierName = shift.CashierName,
                AuthorizedBy = managerName,
                Timestamp = DateTime.Now
            };

            context.CashMovements.Add(movement);
            context.ShiftSessions.Update(shift);
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> WithdrawFloatAsync(int shiftId, decimal amount, string managerName)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var shift = await context.ShiftSessions.FindAsync(shiftId);
            if (shift == null || shift.Status != "Open") return false;

            if (shift.OpeningCash < amount)
                throw new InvalidOperationException($"Cannot withdraw Rs. {amount:N2}. Current float balance is only Rs. {shift.OpeningCash:N2}.");

            shift.OpeningCash -= amount;
            var movement = new CashMovement
            {
                ShiftSessionId = shiftId,
                MovementType = "Paid Out",
                ReasonCategory = "Float Out / Safe Drop",
                Amount = amount,
                CashierName = shift.CashierName,
                AuthorizedBy = managerName,
                Timestamp = DateTime.Now
            };

            context.CashMovements.Add(movement);
            context.ShiftSessions.Update(shift);
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RegisterCashMovementAsync(int shiftId, string movementType, decimal amount, string reasonCategory, string remarks, string cashierName)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var shift = await context.ShiftSessions.FindAsync(shiftId);
            if (shift == null || shift.Status != "Open") return false;

            string prefix = movementType == "Paid In" ? "PI" : "PO";
            string dateStr = DateTime.Now.ToString("yyyyMMdd");
            var today = DateTime.Today;
            int count = await context.CashMovements.CountAsync(m => m.ReferenceVoucherNo.StartsWith(prefix) && m.Timestamp >= today);
            string voucherNo = $"{prefix}-{dateStr}-{(count + 1):D3}";

            var movement = new CashMovement
            {
                ShiftSessionId = shiftId,
                MovementType = movementType,
                Amount = amount,
                ReasonCategory = reasonCategory,
                Remarks = remarks,
                CashierName = cashierName,
                AuthorizedBy = cashierName,
                Timestamp = DateTime.Now,
                ReferenceVoucherNo = voucherNo
            };

            context.CashMovements.Add(movement);
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<ShiftSession?> GetShiftSummaryAsync(int shiftId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var shift = await context.ShiftSessions.Include(s => s.CashMovements).FirstOrDefaultAsync(s => s.Id == shiftId);
            if (shift == null) return null;

            var cashSales = await context.SalesHeaders.Where(s => s.ShiftSessionId == shiftId && s.PaymentMethod == "Cash" && s.Status != "Voided").SumAsync(s => s.NetTotal);
            shift.TotalCashSales = cashSales;

            var extraPaidIns = shift.CashMovements.Where(m => m.MovementType == "Paid In" && m.ReasonCategory != "Opening Float").Sum(m => m.Amount);
            var paidOuts = shift.CashMovements.Where(m => m.MovementType == "Paid Out").Sum(m => m.Amount);

            shift.ExpectedCash = shift.OpeningCash + shift.TotalCashSales + extraPaidIns - paidOuts;
            context.ShiftSessions.Update(shift);
            await context.SaveChangesAsync();
            return shift;
        }

        public async Task<bool> CloseShiftAsync(int shiftId, decimal actualCashCounted)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var shift = await GetShiftSummaryAsync(shiftId);
            if (shift == null || shift.Status == "Closed") return false;

            context.ShiftSessions.Attach(shift);
            shift.ActualCash = actualCashCounted;
            shift.Variance = actualCashCounted - shift.ExpectedCash;
            shift.EndTime = DateTime.Now;
            shift.Status = "Closed";
            context.ShiftSessions.Update(shift);
            await context.SaveChangesAsync();
            return true;
        }
    }
}