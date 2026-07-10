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

        public async Task<ShiftSession?> GetActiveShiftAsync(
            string terminalNo)
        {
            string safeTerminalNo =
                (terminalNo ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(safeTerminalNo))
                return null;

            await using var context =
                await _contextFactory.CreateDbContextAsync();

            return await context.ShiftSessions
                .AsNoTracking()
                .Where(shift =>
                    shift.TerminalNo == safeTerminalNo &&
                    shift.Status == "Open")
                .OrderByDescending(shift => shift.StartTime)
                .ThenByDescending(shift => shift.Id)
                .FirstOrDefaultAsync();
        }

        // Compatibility overload. Startup no longer uses this method.
        public Task<ShiftSession> CreateNewShiftAsync(
            string terminalNo,
            string cashierName)
        {
            return CreateNewShiftAsync(
                terminalNo,
                cashierName,
                0m);
        }

        public async Task<ShiftSession> CreateNewShiftAsync(
            string terminalNo,
            string cashierName,
            decimal openingCash)
        {
            string safeTerminalNo =
                (terminalNo ?? string.Empty).Trim();

            string safeCashierName =
                (cashierName ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(safeTerminalNo))
            {
                throw new InvalidOperationException(
                    "Terminal number is required.");
            }

            if (string.IsNullOrWhiteSpace(safeCashierName))
            {
                throw new InvalidOperationException(
                    "Cashier name is required.");
            }

            if (openingCash < 0)
            {
                throw new InvalidOperationException(
                    "Opening cash cannot be negative.");
            }

            await using var context =
                await _contextFactory.CreateDbContextAsync();

            await using var transaction =
                await context.Database.BeginTransactionAsync();

            try
            {
                bool openShiftExists =
                    await context.ShiftSessions.AnyAsync(
                        shift =>
                            shift.TerminalNo == safeTerminalNo &&
                            shift.Status == "Open");

                if (openShiftExists)
                {
                    throw new InvalidOperationException(
                        $"Terminal '{safeTerminalNo}' already has an open shift.");
                }

                var newShift = new ShiftSession
                {
                    TerminalNo = safeTerminalNo,
                    CashierName = safeCashierName,
                    StartTime = DateTime.Now,
                    Status = "Open",
                    OpeningCash = decimal.Round(
                        openingCash,
                        2,
                        MidpointRounding.AwayFromZero)
                };

                await context.ShiftSessions.AddAsync(
                    newShift);

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return newShift;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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