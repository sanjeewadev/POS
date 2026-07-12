using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.DTOs;
using POS.Core.Models.DTOs;

namespace POS.Core.Repositories
{
    public class FloatCashRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly TillRepository _tillRepository;

        public FloatCashRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _tillRepository = new TillRepository(contextFactory);
        }

        public async Task<List<ShiftAuditDto>> GetShiftAuditsAsync(DateTime startDate, DateTime endDate)
        {
            DateTime start = startDate.Date;
            DateTime endExclusive = endDate.Date.AddDays(1);

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            List<int> shiftIds = await context.ShiftSessions
                .AsNoTracking()
                .Where(shift => shift.StartTime >= start && shift.StartTime < endExclusive)
                .OrderByDescending(shift => shift.StartTime)
                .Select(shift => shift.Id)
                .ToListAsync();

            var results = new List<ShiftAuditDto>();
            foreach (int shiftId in shiftIds)
            {
                ShiftCashSummaryDto? summary = await _tillRepository.GetShiftCashSummaryAsync(shiftId);
                if (summary == null)
                    continue;

                results.Add(new ShiftAuditDto
                {
                    ShiftId = summary.ShiftSessionId,
                    TerminalNo = summary.TerminalNo,
                    CashierName = summary.CashierName,
                    OpenTime = summary.OpenedAt,
                    CloseTime = summary.ClosedAt,
                    Status = summary.Status,
                    ZReportNo = summary.ZReportNo,
                    OpeningFloat = summary.OpeningCash,
                    CashTenderTotal = summary.CashTenderTotal,
                    CardTenderTotal = summary.CardTenderTotal,
                    ChequeTenderTotal = summary.ChequeTenderTotal,
                    OtherTenderTotal = summary.GiftVoucherTenderTotal +
                                       summary.CustomerCreditTenderTotal +
                                       summary.OtherTenderTotal,
                    TotalCashIn = summary.CashTenderTotal +
                                  summary.PaidInTotal +
                                  summary.FloatInTotal,
                    TotalCashOut = summary.PaidOutTotal +
                                   summary.FloatOutTotal +
                                   summary.CashRefundTotal,
                    CashRefundTotal = summary.CashRefundTotal,
                    ExpectedCash = summary.ExpectedCash,
                    ActualCash = summary.CountedCash,
                    Variance = summary.Variance,
                    AuthorizedBy = summary.AuthorizedBy,
                    IsSnapshot = summary.IsSnapshot
                });
            }

            return results;
        }

        public async Task<List<CashMovementDto>> GetShiftLedgerAsync(int shiftId)
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            var ledger = new List<CashMovementDto>();
            var shift = await context.ShiftSessions.AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == shiftId);
            if (shift == null)
                return ledger;

            ledger.Add(new CashMovementDto
            {
                MovementId = 0,
                Timestamp = shift.StartTime,
                MovementType = "Opening Cash",
                ReferenceNo = $"SHIFT-{shift.Id}",
                Amount = shift.OpeningCash,
                Remarks = "Opening drawer cash"
            });

            var cashTenders = await (
                from payment in context.SalesPayments.AsNoTracking()
                join sale in context.SalesHeaders.AsNoTracking()
                    on payment.SalesHeaderId equals sale.Id
                where sale.ShiftSessionId == shiftId &&
                      sale.Status == "Completed" &&
                      !sale.IsVoided &&
                      payment.PaymentType == PaymentTypeCodes.Cash
                orderby sale.TransactionDate, payment.Id
                select new CashMovementDto
                {
                    MovementId = -payment.Id,
                    Timestamp = payment.PaymentDate ?? payment.CreatedAt,
                    MovementType = "Cash Sale",
                    ReferenceNo = sale.InvoiceNo,
                    Amount = payment.Amount,
                    Remarks = "Completed cash tender"
                }).ToListAsync();

            ledger.AddRange(cashTenders);

            var movements = await context.CashMovements
                .AsNoTracking()
                .Where(movement => movement.ShiftSessionId == shiftId)
                .OrderBy(movement => movement.Timestamp)
                .Select(movement => new CashMovementDto
                {
                    MovementId = movement.Id,
                    Timestamp = movement.Timestamp,
                    MovementType = movement.MovementType,
                    ReferenceNo = movement.ReferenceVoucherNo,
                    Amount = movement.MovementType == CashMovementTypeCodes.PaidOut
                        ? -movement.Amount
                        : movement.Amount,
                    Remarks = movement.ReasonCategory +
                              (string.IsNullOrWhiteSpace(movement.Remarks)
                                  ? string.Empty
                                  : " - " + movement.Remarks)
                })
                .ToListAsync();

            ledger.AddRange(movements);

            List<CashDrawerEvent> drawerEvents = await context.CashDrawerEvents
                .AsNoTracking()
                .Where(drawerEvent => drawerEvent.ShiftSessionId == shiftId)
                .OrderBy(drawerEvent => drawerEvent.RequestedAtUtc)
                .ThenBy(drawerEvent => drawerEvent.Id)
                .ToListAsync();

            ledger.AddRange(drawerEvents.Select(drawerEvent => new CashMovementDto
            {
                MovementId = 1_000_000 + drawerEvent.Id,
                Timestamp = drawerEvent.RequestedAtUtc.ToLocalTime(),
                MovementType = $"Drawer: {drawerEvent.EventType}",
                ReferenceNo = drawerEvent.SalesHeaderId.HasValue
                    ? $"SALE-{drawerEvent.SalesHeaderId.Value}"
                    : drawerEvent.CashMovementId.HasValue
                        ? $"CASH-{drawerEvent.CashMovementId.Value}"
                        : string.Empty,
                Amount = 0m,
                Remarks = drawerEvent.Reason +
                          (string.IsNullOrWhiteSpace(drawerEvent.Note)
                              ? string.Empty
                              : " - " + drawerEvent.Note) +
                          (drawerEvent.Succeeded
                              ? " [Opened]"
                              : $" [Failed: {drawerEvent.FailureMessage}]")
            }));

            return ledger.OrderBy(row => row.Timestamp).ThenBy(row => row.MovementId).ToList();
        }
    }
}
