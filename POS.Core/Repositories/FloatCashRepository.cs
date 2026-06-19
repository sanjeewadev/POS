using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.DTOs;
using POS.Core.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Core.Repositories
{
    public class FloatCashRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public FloatCashRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ==============================================================================
        // 1. MACRO LEVEL: The Shift Audit Engine
        // ==============================================================================
        public async Task<List<ShiftAuditDto>> GetShiftAuditsAsync(DateTime startDate, DateTime endDate)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Fetch exact matches from your ShiftSession model
            var rawShifts = await context.ShiftSessions
                .AsNoTracking()
                .Where(s => s.StartTime.Date >= startDate.Date && s.StartTime.Date <= endDate.Date)
                .Select(s => new ShiftAuditDto
                {
                    ShiftId = s.Id,
                    TerminalNo = s.TerminalNo,
                    CashierName = s.CashierName,
                    OpenTime = s.StartTime,
                    CloseTime = s.EndTime,
                    Status = s.Status,

                    OpeningFloat = s.OpeningCash,
                    TotalCashIn = s.TotalCashSales, // Assuming this is purely cash sales
                    TotalCashOut = 0m, // We will calculate this from movements below
                    ActualCash = s.ActualCash
                })
                .ToListAsync();

            var shiftIds = rawShifts.Select(s => s.ShiftId).ToList();

            // Fetch specific manual Cash Movements (Payouts, Petty Cash, etc.)
            var rawLedger = await context.CashMovements
                .AsNoTracking()
                .Where(m => shiftIds.Contains(m.ShiftSessionId))
                .Select(m => new
                {
                    m.ShiftSessionId,
                    m.MovementType,
                    m.Amount
                })
                .ToListAsync();

            // Apply the manual cash movements (outflows) to the audit
            foreach (var shift in rawShifts)
            {
                var shiftLedger = rawLedger.Where(l => l.ShiftSessionId == shift.ShiftId).ToList();

                // Sum all negative movements (Paid Out, Petty Cash) as a positive "TotalCashOut" figure for UI reading
                shift.TotalCashOut = shiftLedger.Where(l => l.MovementType == "Paid Out" || l.Amount < 0)
                                                .Sum(l => Math.Abs(l.Amount));
            }

            return rawShifts.OrderByDescending(r => r.OpenTime).ToList();
        }

        // ==============================================================================
        // 2. MICRO LEVEL: The Minute-by-Minute Drawer Ledger
        // ==============================================================================
        public async Task<List<CashMovementDto>> GetShiftLedgerAsync(int shiftId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var movements = await context.CashMovements
                .AsNoTracking()
                .Where(m => m.ShiftSessionId == shiftId)
                .OrderBy(m => m.Timestamp)
                .Select(m => new CashMovementDto
                {
                    MovementId = m.Id,
                    Timestamp = m.Timestamp,
                    MovementType = m.MovementType,
                    ReferenceNo = m.ReferenceVoucherNo,
                    Amount = m.Amount,
                    Remarks = m.ReasonCategory + " - " + m.Remarks // Combine for UI details
                })
                .ToListAsync();

            return movements;
        }
    }
}