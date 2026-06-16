using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Models;
using POS.Core.Data; // Assuming this is where your AppDbContext lives

namespace POS.Core.Repositories
{
    public class TillRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public TillRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ==========================================
        // 1. SHIFT LIFECYCLE
        // ==========================================
        public async Task<ShiftSession?> GetActiveShiftAsync(string terminalNo)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.ShiftSessions
                .FirstOrDefaultAsync(s => s.TerminalNo == terminalNo && s.Status == "Open");
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
                OpeningCash = 0m // Float is injected later by the manager
            };

            context.ShiftSessions.Add(newShift);
            await context.SaveChangesAsync();
            return newShift;
        }

        // ==========================================
        // 2. MANAGER INJECTIONS (CASH MOVEMENTS)
        // ==========================================
        public async Task<bool> InjectFloatAsync(int shiftId, decimal amount, string managerName)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var shift = await context.ShiftSessions.FindAsync(shiftId);
            if (shift == null || shift.Status != "Open") return false;

            // Update the master shift record
            shift.OpeningCash += amount;

            // Create the strict audit trail log
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

        // ==========================================
        // 3. THE MATH ENGINE (X-REPORT & Z-REPORT)
        // ==========================================
        public async Task<ShiftSession?> GetShiftSummaryAsync(int shiftId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var shift = await context.ShiftSessions
                .Include(s => s.CashMovements)
                .FirstOrDefaultAsync(s => s.Id == shiftId);

            if (shift == null) return null;

            // 1. Calculate Total Cash Sales (Ignoring Voids and Card payments)
            // 1. Calculate Total Cash Sales (Ignoring Voids and Card payments)
            var cashSales = await context.SalesHeaders
                .Where(s => s.ShiftSessionId == shiftId && s.PaymentMethod == "Cash" && s.Status != "Voided")
                .SumAsync(s => s.NetTotal);

            shift.TotalCashSales = cashSales;

            // 2. Calculate manual Manager Cash Drops or Payouts
            var extraPaidIns = shift.CashMovements
                .Where(m => m.MovementType == "Paid In" && m.ReasonCategory != "Opening Float")
                .Sum(m => m.Amount);

            var paidOuts = shift.CashMovements
                .Where(m => m.MovementType == "Paid Out")
                .Sum(m => m.Amount);

            // 3. The Ultimate Expected Cash Formula
            shift.ExpectedCash = shift.OpeningCash + shift.TotalCashSales + extraPaidIns - paidOuts;

            // Save the latest math snapshot to the database
            context.ShiftSessions.Update(shift);
            await context.SaveChangesAsync();

            return shift;
        }

        public async Task<bool> CloseShiftAsync(int shiftId, decimal actualCashCounted)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Re-run the math one final time to guarantee accuracy to the exact second of closure
            var shift = await GetShiftSummaryAsync(shiftId);
            if (shift == null || shift.Status == "Closed") return false;

            // Attach it to the current context so we can update it
            context.ShiftSessions.Attach(shift);

            // Lock in the final numbers
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