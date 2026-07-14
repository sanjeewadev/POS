using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;

namespace POS.Core.Repositories
{
    public class TillRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public TillRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<ShiftSession?> GetActiveShiftAsync(string terminalNo)
        {
            string safeTerminalNo = Normalize(terminalNo).ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(safeTerminalNo))
                return null;

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            return await context.ShiftSessions
                .AsNoTracking()
                .Where(shift =>
                    shift.TerminalNo.ToUpper() == safeTerminalNo &&
                    (shift.Status.ToUpper() == "OPEN" ||
                     shift.Status.ToUpper() == "CLOSING"))
                .OrderByDescending(shift => shift.StartTime)
                .ThenByDescending(shift => shift.Id)
                .FirstOrDefaultAsync();
        }

        public Task<ShiftSession> CreateNewShiftAsync(string terminalNo, string cashierName)
        {
            return CreateNewShiftAsync(terminalNo, cashierName, 0m);
        }

        public async Task<ShiftSession> CreateNewShiftAsync(
            string terminalNo,
            string cashierName,
            decimal openingCash)
        {
            string safeTerminalNo = Normalize(terminalNo).ToUpperInvariant();
            string safeCashierName = Normalize(cashierName);
            decimal safeOpeningCash = RoundMoney(openingCash);

            if (string.IsNullOrWhiteSpace(safeTerminalNo))
                throw new InvalidOperationException("Terminal number is required.");
            if (string.IsNullOrWhiteSpace(safeCashierName))
                throw new InvalidOperationException("Cashier name is required.");
            if (safeOpeningCash < 0m)
                throw new InvalidOperationException("Opening cash cannot be negative.");

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                bool openShiftExists = await context.ShiftSessions.AnyAsync(shift =>
                    shift.TerminalNo.ToUpper() == safeTerminalNo &&
                    (shift.Status.ToUpper() == "OPEN" ||
                     shift.Status.ToUpper() == "CLOSING"));

                if (openShiftExists)
                    throw new InvalidOperationException($"Terminal '{safeTerminalNo}' already has an open shift.");

                var newShift = new ShiftSession
                {
                    TerminalNo = safeTerminalNo,
                    CashierName = safeCashierName,
                    StartTime = DateTime.Now,
                    Status = ShiftStatusCodes.Open,
                    OpeningCash = safeOpeningCash,
                    TotalCashSales = 0m,
                    ExpectedCash = safeOpeningCash,
                    ActualCash = 0m,
                    Variance = 0m
                };

                context.ShiftSessions.Add(newShift);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                return newShift;
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException(
                    $"Terminal '{safeTerminalNo}' already has an open shift.",
                    ex);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<decimal> GetCurrentFloatBalanceAsync(int shiftId)
        {
            ShiftCashSummaryDto? summary = await GetShiftCashSummaryAsync(shiftId);
            if (summary == null)
                return 0m;

            return CalculateCurrentFloat(summary);
        }

        public async Task<bool> InjectFloatAsync(int shiftId, decimal amount, string managerName)
        {
            CashMovement movement = await RegisterCashMovementDetailedAsync(
                new CashMovementRegistrationRequest
                {
                    ShiftSessionId = shiftId,
                    MovementType = CashMovementTypeCodes.PaidIn,
                    Amount = amount,
                    ReasonCategory = CashMovementReasonCodes.FloatIn,
                    Remarks = "Float added to drawer.",
                    CashierName = await GetShiftCashierNameAsync(shiftId),
                    AuthorizedBy = Normalize(managerName)
                });

            return movement.Id > 0;
        }

        public async Task<bool> WithdrawFloatAsync(int shiftId, decimal amount, string managerName)
        {
            CashMovement movement = await RegisterCashMovementDetailedAsync(
                new CashMovementRegistrationRequest
                {
                    ShiftSessionId = shiftId,
                    MovementType = CashMovementTypeCodes.PaidOut,
                    Amount = amount,
                    ReasonCategory = CashMovementReasonCodes.FloatOut,
                    Remarks = "Float removed from drawer.",
                    CashierName = await GetShiftCashierNameAsync(shiftId),
                    AuthorizedBy = Normalize(managerName)
                });

            return movement.Id > 0;
        }

        public Task<bool> RegisterCashMovementAsync(
            int shiftId,
            string movementType,
            decimal amount,
            string reasonCategory,
            string remarks,
            string cashierName)
        {
            return RegisterCashMovementCompatibilityAsync(
                shiftId,
                movementType,
                amount,
                reasonCategory,
                remarks,
                cashierName);
        }

        private async Task<bool> RegisterCashMovementCompatibilityAsync(
            int shiftId,
            string movementType,
            decimal amount,
            string reasonCategory,
            string remarks,
            string cashierName)
        {
            CashMovement movement = await RegisterCashMovementDetailedAsync(
                new CashMovementRegistrationRequest
                {
                    ShiftSessionId = shiftId,
                    MovementType = movementType,
                    Amount = amount,
                    ReasonCategory = reasonCategory,
                    Remarks = remarks,
                    CashierName = cashierName,
                    AuthorizedBy = cashierName
                });

            return movement.Id > 0;
        }

        public async Task<CashMovement> RegisterCashMovementDetailedAsync(
            CashMovementRegistrationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            string movementType = NormalizeMovementType(request.MovementType);
            decimal amount = RoundMoney(request.Amount);
            string reason = Truncate(Normalize(request.ReasonCategory), 100);
            string cashier = Truncate(Normalize(request.CashierName), 100);
            string authorizedBy = Truncate(Normalize(request.AuthorizedBy), 100);

            if (request.ShiftSessionId <= 0)
                throw new InvalidOperationException("An active shift is required.");
            if (amount <= 0m)
                throw new InvalidOperationException("Cash movement amount must be greater than zero.");
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("Cash movement reason is required.");
            if (string.IsNullOrWhiteSpace(cashier))
                throw new InvalidOperationException("Cashier name is required.");
            if (string.IsNullOrWhiteSpace(authorizedBy))
                throw new InvalidOperationException("Authorization identity is required.");

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                ShiftSession shift = await context.ShiftSessions
                    .FirstOrDefaultAsync(row => row.Id == request.ShiftSessionId)
                    ?? throw new InvalidOperationException("The active shift was not found.");

                EnsureOpenShift(shift);

                if (!string.Equals(shift.CashierName, cashier, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The cash movement cashier does not own this shift.");

                if (movementType == CashMovementTypeCodes.PaidOut)
                {
                    ShiftCashSummaryDto current = await BuildLiveSummaryAsync(context, shift);

                    if (CashMovementReasonCodes.IsFloatOut(reason))
                    {
                        decimal currentFloat = CalculateCurrentFloat(current);
                        if (amount > currentFloat)
                        {
                            throw new InvalidOperationException(
                                $"Cannot remove Rs. {amount:N2} as Float Out. The current float balance is only Rs. {currentFloat:N2}.");
                        }
                    }

                    if (amount > current.ExpectedCash)
                    {
                        throw new InvalidOperationException(
                            $"Cannot remove Rs. {amount:N2}. The calculated drawer cash is only Rs. {current.ExpectedCash:N2}.");
                    }
                }

                string sequenceType = movementType == CashMovementTypeCodes.PaidIn
                    ? ShiftDocumentSequenceCodes.PaidIn
                    : ShiftDocumentSequenceCodes.PaidOut;
                string prefix = movementType == CashMovementTypeCodes.PaidIn ? "PI-" : "POT-";
                string voucherNo = await AllocateDocumentNumberAsync(
                    context,
                    sequenceType,
                    prefix,
                    6);

                var movement = new CashMovement
                {
                    ShiftSessionId = shift.Id,
                    MovementType = movementType,
                    Amount = amount,
                    ReasonCategory = reason,
                    Remarks = Truncate(Normalize(request.Remarks), 255),
                    CashierName = cashier,
                    AuthorizedBy = authorizedBy,
                    Timestamp = DateTime.Now,
                    ReferenceVoucherNo = voucherNo
                };

                context.CashMovements.Add(movement);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                return movement;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<ShiftCashSummaryDto?> GetShiftCashSummaryAsync(
            int shiftId,
            bool preferClosedSnapshot = true)
        {
            if (shiftId <= 0)
                return null;

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            if (preferClosedSnapshot)
            {
                ShiftCloseSnapshot? snapshot = await context.ShiftCloseSnapshots
                    .AsNoTracking()
                    .FirstOrDefaultAsync(row => row.ShiftSessionId == shiftId);

                if (snapshot != null)
                    return FromSnapshot(snapshot);
            }

            ShiftSession? shift = await context.ShiftSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == shiftId);

            return shift == null
                ? null
                : await BuildLiveSummaryAsync(context, shift);
        }

        public async Task<ShiftSession?> GetShiftSummaryAsync(int shiftId)
        {
            ShiftCashSummaryDto? summary = await GetShiftCashSummaryAsync(shiftId);
            if (summary == null)
                return null;

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            ShiftSession? shift = await context.ShiftSessions
                .Include(row => row.CashMovements)
                .FirstOrDefaultAsync(row => row.Id == shiftId);

            if (shift == null)
                return null;

            shift.TotalCashSales = summary.CashTenderTotal;
            shift.ExpectedCash = summary.ExpectedCash;
            if (summary.ClosedAt.HasValue)
            {
                shift.ActualCash = summary.CountedCash;
                shift.Variance = summary.Variance;
            }

            await context.SaveChangesAsync();
            return shift;
        }

        public async Task<ShiftCashSummaryDto> CloseShiftSafelyAsync(ShiftCloseRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (request.ShiftSessionId <= 0)
                throw new InvalidOperationException("Shift is required.");
            if (request.CountedCash < 0m)
                throw new InvalidOperationException("Counted cash cannot be negative.");

            Guid closeToken = request.CloseToken == Guid.Empty
                ? Guid.NewGuid()
                : request.CloseToken;

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                ShiftCloseSnapshot? existing = await context.ShiftCloseSnapshots
                    .AsNoTracking()
                    .FirstOrDefaultAsync(row =>
                        row.ShiftSessionId == request.ShiftSessionId ||
                        row.CloseToken == closeToken);

                if (existing != null)
                {
                    await transaction.RollbackAsync();
                    return FromSnapshot(existing);
                }

                ShiftSession shift = await context.ShiftSessions
                    .FirstOrDefaultAsync(row => row.Id == request.ShiftSessionId)
                    ?? throw new InvalidOperationException("The shift was not found.");

                if (!string.Equals(shift.Status, ShiftStatusCodes.Open, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Shift {shift.Id} is {shift.Status} and cannot be closed.");

                string terminalNo = Normalize(request.TerminalNo);
                string cashierName = Normalize(request.CashierName);
                if (!string.Equals(shift.TerminalNo, terminalNo, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(shift.CashierName, cashierName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("The shift belongs to a different terminal or cashier.");
                }

                bool openCartExists = await context.CashierCartSessions.AnyAsync(cart =>
                    cart.ShiftSessionId == shift.Id &&
                    (cart.Status == CashierCartStatusCodes.Active ||
                     cart.Status == CashierCartStatusCodes.Held));

                if (openCartExists)
                    throw new InvalidOperationException("Complete or cancel all active and suspended carts before closing the shift.");

                shift.Status = ShiftStatusCodes.Closing;
                await context.SaveChangesAsync();

                // Re-check after the status transition while the write transaction is held.
                openCartExists = await context.CashierCartSessions.AnyAsync(cart =>
                    cart.ShiftSessionId == shift.Id &&
                    (cart.Status == CashierCartStatusCodes.Active ||
                     cart.Status == CashierCartStatusCodes.Held));
                if (openCartExists)
                    throw new InvalidOperationException("A cart became active while the shift was closing.");

                ShiftCashSummaryDto summary = await BuildLiveSummaryAsync(context, shift);
                decimal countedCash = RoundMoney(request.CountedCash);
                decimal variance = RoundMoney(countedCash - summary.ExpectedCash);
                string authorizedBy = Truncate(Normalize(request.AuthorizedBy), 100);

                if (variance != 0m && string.IsNullOrWhiteSpace(authorizedBy))
                    throw new InvalidOperationException("Manager authorization is required for a non-zero cash variance.");

                string zReportNo = await AllocateDocumentNumberAsync(
                    context,
                    ShiftDocumentSequenceCodes.ZReport,
                    "Z-",
                    6);
                DateTime closedAt = DateTime.Now;

                var snapshot = new ShiftCloseSnapshot
                {
                    ShiftSessionId = shift.Id,
                    CloseToken = closeToken,
                    ZReportNo = zReportNo,
                    TerminalNo = shift.TerminalNo,
                    CashierName = shift.CashierName,
                    OpenedAt = shift.StartTime,
                    ClosedAt = closedAt,
                    CompletedSaleCount = summary.CompletedSaleCount,
                    CustomerReturnCount = summary.CustomerReturnCount,
                    GrossSales = summary.GrossSales,
                    TotalDiscount = summary.TotalDiscount,
                    NetSales = summary.NetSales,
                    VatTotal = summary.VatTotal,
                    CashTenderTotal = summary.CashTenderTotal,
                    CardTenderTotal = summary.CardTenderTotal,
                    ChequeTenderTotal = summary.ChequeTenderTotal,
                    GiftVoucherTenderTotal = summary.GiftVoucherTenderTotal,
                    CustomerCreditTenderTotal = summary.CustomerCreditTenderTotal,
                    OtherTenderTotal = summary.OtherTenderTotal,
                    OpeningCash = summary.OpeningCash,
                    PaidInTotal = summary.PaidInTotal,
                    FloatInTotal = summary.FloatInTotal,
                    PaidOutTotal = summary.PaidOutTotal,
                    FloatOutTotal = summary.FloatOutTotal,
                    CashRefundTotal = summary.CashRefundTotal,
                    ExpectedCash = summary.ExpectedCash,
                    CountedCash = countedCash,
                    Variance = variance,
                    ClosedBy = Truncate(
                        FirstNonEmpty(request.ClosedBy, shift.CashierName),
                        100),
                    AuthorizedBy = authorizedBy,
                    VarianceNote = Truncate(Normalize(request.VarianceNote), 500),
                    CreatedAtUtc = DateTime.UtcNow
                };

                context.ShiftCloseSnapshots.Add(snapshot);

                shift.TotalCashSales = summary.CashTenderTotal;
                shift.ExpectedCash = summary.ExpectedCash;
                shift.ActualCash = countedCash;
                shift.Variance = variance;
                shift.EndTime = closedAt;
                shift.Status = ShiftStatusCodes.Closed;

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                return FromSnapshot(snapshot);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                ShiftCashSummaryDto? existing = await GetShiftCashSummaryAsync(request.ShiftSessionId);
                if (existing?.IsSnapshot == true)
                    return existing;
                throw;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> CloseShiftAsync(int shiftId, decimal actualCashCounted)
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            ShiftSession? shift = await context.ShiftSessions.AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == shiftId);
            if (shift == null)
                return false;

            ShiftCashSummaryDto preview = await GetShiftCashSummaryAsync(shiftId, false)
                ?? throw new InvalidOperationException("Shift summary could not be calculated.");
            decimal variance = RoundMoney(actualCashCounted - preview.ExpectedCash);

            await CloseShiftSafelyAsync(new ShiftCloseRequest
            {
                ShiftSessionId = shift.Id,
                TerminalNo = shift.TerminalNo,
                CashierName = shift.CashierName,
                CountedCash = actualCashCounted,
                ClosedBy = shift.CashierName,
                AuthorizedBy = variance == 0m ? string.Empty : shift.CashierName,
                VarianceNote = variance == 0m ? string.Empty : "Compatibility close authorization."
            });

            return true;
        }

        public async Task<CashDrawerEvent> RecordCashDrawerEventAsync(CashDrawerEventRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (request.ShiftSessionId <= 0)
                throw new InvalidOperationException("An active shift is required for drawer audit.");

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            ShiftSession shift = await context.ShiftSessions.AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == request.ShiftSessionId)
                ?? throw new InvalidOperationException("The shift was not found for drawer audit.");

            EnsureOpenShift(shift);

            if (!string.Equals(shift.TerminalNo, Normalize(request.TerminalNo), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(shift.CashierName, Normalize(request.CashierName), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The drawer event belongs to a different shift owner.");
            }

            var drawerEvent = new CashDrawerEvent
            {
                ShiftSessionId = shift.Id,
                TerminalNo = Truncate(shift.TerminalNo, 20),
                CashierName = Truncate(shift.CashierName, 100),
                EventType = Truncate(Normalize(request.EventType), 30),
                Reason = Truncate(FirstNonEmpty(request.Reason, request.EventType), 100),
                Note = Truncate(Normalize(request.Note), 500),
                AuthorizedBy = Truncate(Normalize(request.AuthorizedBy), 100),
                SalesHeaderId = request.SalesHeaderId,
                CashMovementId = request.CashMovementId,
                RequestedAtUtc = DateTime.UtcNow,
                Succeeded = request.Succeeded,
                FailureMessage = Truncate(Normalize(request.FailureMessage), 500)
            };

            context.CashDrawerEvents.Add(drawerEvent);
            await context.SaveChangesAsync();
            return drawerEvent;
        }

        public async Task<bool> HasOpenCartsAsync(int shiftId)
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            return await context.CashierCartSessions.AnyAsync(cart =>
                cart.ShiftSessionId == shiftId &&
                (cart.Status == CashierCartStatusCodes.Active ||
                 cart.Status == CashierCartStatusCodes.Held));
        }

        private async Task<string> GetShiftCashierNameAsync(int shiftId)
        {
            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            return await context.ShiftSessions
                .Where(row => row.Id == shiftId)
                .Select(row => row.CashierName)
                .FirstOrDefaultAsync()
                ?? string.Empty;
        }

        private static async Task<ShiftCashSummaryDto> BuildLiveSummaryAsync(
            AppDbContext context,
            ShiftSession shift)
        {
            List<SalesHeader> sales = await context.SalesHeaders
                .AsNoTracking()
                .Where(sale =>
                    sale.ShiftSessionId == shift.Id &&
                    sale.Status == "Completed" &&
                    !sale.IsVoided)
                .ToListAsync();

            int[] saleIds = sales.Select(row => row.Id).ToArray();
            List<SalesPayment> payments = saleIds.Length == 0
                ? new List<SalesPayment>()
                : await context.SalesPayments
                    .AsNoTracking()
                    .Where(payment => saleIds.Contains(payment.SalesHeaderId))
                    .ToListAsync();

            List<CashMovement> movements = await context.CashMovements
                .AsNoTracking()
                .Where(movement => movement.ShiftSessionId == shift.Id)
                .ToListAsync();

            int returnCount = await context.CustomerReturnHeaders
                .AsNoTracking()
                .CountAsync(row => row.ShiftSessionId == shift.Id);

            decimal cash = SumPayment(payments, PaymentTypeCodes.Cash);
            decimal card = SumPayment(payments, PaymentTypeCodes.Card);
            decimal cheque = SumPayment(payments, PaymentTypeCodes.Cheque);
            decimal giftVoucher = payments
                .Where(payment => IsPaymentType(payment.PaymentType, PaymentTypeCodes.GiftVoucher) ||
                                  IsPaymentType(payment.PaymentType, "Gift Voucher"))
                .Sum(payment => RoundMoney(payment.Amount));
            decimal customerCredit = payments
                .Where(payment => IsPaymentType(payment.PaymentType, PaymentTypeCodes.CustomerCredit) ||
                                  IsPaymentType(payment.PaymentType, "Customer Credit"))
                .Sum(payment => RoundMoney(payment.Amount));
            decimal knownTender = cash + card + cheque + giftVoucher + customerCredit;
            decimal allTender = payments.Sum(payment => RoundMoney(payment.Amount));

            decimal floatIn = movements
                .Where(row => IsMovement(row, CashMovementTypeCodes.PaidIn) &&
                              CashMovementReasonCodes.IsFloatIn(row.ReasonCategory))
                .Sum(row => RoundMoney(row.Amount));
            decimal otherPaidIn = movements
                .Where(row => IsMovement(row, CashMovementTypeCodes.PaidIn) &&
                              !CashMovementReasonCodes.IsFloatIn(row.ReasonCategory))
                .Sum(row => RoundMoney(row.Amount));
            decimal cashRefund = movements
                .Where(row => IsMovement(row, CashMovementTypeCodes.PaidOut) &&
                              CashMovementReasonCodes.IsCustomerRefund(row.ReasonCategory))
                .Sum(row => RoundMoney(row.Amount));
            decimal floatOut = movements
                .Where(row => IsMovement(row, CashMovementTypeCodes.PaidOut) &&
                              CashMovementReasonCodes.IsFloatOut(row.ReasonCategory))
                .Sum(row => RoundMoney(row.Amount));
            decimal otherPaidOut = movements
                .Where(row => IsMovement(row, CashMovementTypeCodes.PaidOut) &&
                              !CashMovementReasonCodes.IsFloatOut(row.ReasonCategory) &&
                              !CashMovementReasonCodes.IsCustomerRefund(row.ReasonCategory))
                .Sum(row => RoundMoney(row.Amount));

            decimal expected = RoundMoney(
                shift.OpeningCash +
                cash +
                floatIn +
                otherPaidIn -
                floatOut -
                otherPaidOut -
                cashRefund);

            return new ShiftCashSummaryDto
            {
                ShiftSessionId = shift.Id,
                TerminalNo = shift.TerminalNo,
                CashierName = shift.CashierName,
                OpenedAt = shift.StartTime,
                ClosedAt = shift.EndTime,
                Status = shift.Status,
                CompletedSaleCount = sales.Count,
                CustomerReturnCount = returnCount,
                GrossSales = RoundMoney(sales.Sum(row => row.GrossTotal)),
                TotalDiscount = RoundMoney(sales.Sum(row => row.TotalDiscount)),
                NetSales = RoundMoney(sales.Sum(row => row.NetTotal)),
                VatTotal = RoundMoney(sales.Sum(row => row.TotalVatAmount ?? 0m)),
                CashTenderTotal = RoundMoney(cash),
                CardTenderTotal = RoundMoney(card),
                ChequeTenderTotal = RoundMoney(cheque),
                GiftVoucherTenderTotal = RoundMoney(giftVoucher),
                CustomerCreditTenderTotal = RoundMoney(customerCredit),
                OtherTenderTotal = RoundMoney(Math.Max(0m, allTender - knownTender)),
                OpeningCash = RoundMoney(shift.OpeningCash),
                PaidInTotal = RoundMoney(otherPaidIn),
                FloatInTotal = RoundMoney(floatIn),
                PaidOutTotal = RoundMoney(otherPaidOut),
                FloatOutTotal = RoundMoney(floatOut),
                CashRefundTotal = RoundMoney(cashRefund),
                ExpectedCash = expected,
                CountedCash = RoundMoney(shift.ActualCash),
                Variance = RoundMoney(shift.Variance),
                IsSnapshot = false
            };
        }

        private static decimal CalculateCurrentFloat(ShiftCashSummaryDto summary)
        {
            return RoundMoney(
                summary.OpeningCash +
                summary.FloatInTotal -
                summary.FloatOutTotal);
        }

        private static ShiftCashSummaryDto FromSnapshot(ShiftCloseSnapshot snapshot)
        {
            return new ShiftCashSummaryDto
            {
                ShiftSessionId = snapshot.ShiftSessionId,
                TerminalNo = snapshot.TerminalNo,
                CashierName = snapshot.CashierName,
                OpenedAt = snapshot.OpenedAt,
                ClosedAt = snapshot.ClosedAt,
                Status = ShiftStatusCodes.Closed,
                ZReportNo = snapshot.ZReportNo,
                CompletedSaleCount = snapshot.CompletedSaleCount,
                CustomerReturnCount = snapshot.CustomerReturnCount,
                GrossSales = snapshot.GrossSales,
                TotalDiscount = snapshot.TotalDiscount,
                NetSales = snapshot.NetSales,
                VatTotal = snapshot.VatTotal,
                CashTenderTotal = snapshot.CashTenderTotal,
                CardTenderTotal = snapshot.CardTenderTotal,
                ChequeTenderTotal = snapshot.ChequeTenderTotal,
                GiftVoucherTenderTotal = snapshot.GiftVoucherTenderTotal,
                CustomerCreditTenderTotal = snapshot.CustomerCreditTenderTotal,
                OtherTenderTotal = snapshot.OtherTenderTotal,
                OpeningCash = snapshot.OpeningCash,
                PaidInTotal = snapshot.PaidInTotal,
                FloatInTotal = snapshot.FloatInTotal,
                PaidOutTotal = snapshot.PaidOutTotal,
                FloatOutTotal = snapshot.FloatOutTotal,
                CashRefundTotal = snapshot.CashRefundTotal,
                ExpectedCash = snapshot.ExpectedCash,
                CountedCash = snapshot.CountedCash,
                Variance = snapshot.Variance,
                ClosedBy = snapshot.ClosedBy,
                AuthorizedBy = snapshot.AuthorizedBy,
                VarianceNote = snapshot.VarianceNote,
                IsSnapshot = true
            };
        }

        private static async Task<string> AllocateDocumentNumberAsync(
            AppDbContext context,
            string documentType,
            string prefix,
            int padding)
        {
            DocumentSequence? sequence = await context.DocumentSequences
                .FirstOrDefaultAsync(row => row.DocumentType == documentType);

            if (sequence == null)
            {
                sequence = new DocumentSequence
                {
                    DocumentType = documentType,
                    Prefix = prefix,
                    NextSequenceNumber = 1,
                    PaddingLength = padding,
                    UpdatedAt = DateTime.Now
                };
                context.DocumentSequences.Add(sequence);
            }

            string number = $"{sequence.Prefix}{sequence.NextSequenceNumber.ToString().PadLeft(sequence.PaddingLength, '0')}";
            sequence.NextSequenceNumber++;
            sequence.UpdatedAt = DateTime.Now;
            return number;
        }

        private static decimal SumPayment(IEnumerable<SalesPayment> payments, string paymentType)
        {
            return payments
                .Where(payment => IsPaymentType(payment.PaymentType, paymentType))
                .Sum(payment => RoundMoney(payment.Amount));
        }

        private static bool IsPaymentType(string? actual, string expected)
        {
            return string.Equals(Normalize(actual).Replace(" ", string.Empty), expected.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMovement(CashMovement movement, string expected)
        {
            return string.Equals(Normalize(movement.MovementType), expected, StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureOpenShift(ShiftSession shift)
        {
            if (!string.Equals(shift.Status, ShiftStatusCodes.Open, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Shift {shift.Id} is {shift.Status} and does not accept cash movements.");
        }

        private static string NormalizeMovementType(string? value)
        {
            string normalized = Normalize(value);
            if (string.Equals(normalized, CashMovementTypeCodes.PaidIn, StringComparison.OrdinalIgnoreCase))
                return CashMovementTypeCodes.PaidIn;
            if (string.Equals(normalized, CashMovementTypeCodes.PaidOut, StringComparison.OrdinalIgnoreCase))
                return CashMovementTypeCodes.PaidOut;
            throw new InvalidOperationException("Cash movement type must be Paid In or Paid Out.");
        }

        private static decimal RoundMoney(decimal value) =>
            decimal.Round(value, 2, MidpointRounding.AwayFromZero);

        private static string Normalize(string? value) => (value ?? string.Empty).Trim();

        private static string Truncate(string value, int maxLength) =>
            value.Length <= maxLength ? value : value[..maxLength];

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
            return string.Empty;
        }
    }
}
