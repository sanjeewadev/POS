using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Configuration;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Models.DTOs;

namespace POS.Core.Repositories
{
    public class CashierCartRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public CashierCartRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<CashierCartSessionDto?> GetActiveAsync(CashierCartOwnerDto owner)
        {
            ValidateOwner(owner);

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            CashierCartSession? session = await context.CashierCartSessions
                .Include(s => s.Lines)
                .AsNoTracking()
                .Where(s =>
                    s.ShiftSessionId == owner.ShiftSessionId &&
                    s.TerminalNo == Normalize(owner.TerminalNo) &&
                    s.CashierName == Normalize(owner.CashierName) &&
                    s.Status == CashierCartStatusCodes.Active)
                .OrderByDescending(s => s.UpdatedAtUtc)
                .FirstOrDefaultAsync();

            return session == null ? null : ToDto(session);
        }

        public async Task<CashierCartSessionDto?> GetByTokenAsync(Guid cartToken)
        {
            if (cartToken == Guid.Empty)
                return null;

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            CashierCartSession? session = await context.CashierCartSessions
                .Include(s => s.Lines)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.CartToken == cartToken);

            return session == null ? null : ToDto(session);
        }

        public async Task<IReadOnlyList<CashierCartSessionDto>> ListHeldAsync(CashierCartOwnerDto owner)
        {
            ValidateOwner(owner);

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();

            List<CashierCartSession> sessions = await context.CashierCartSessions
                .Include(s => s.Lines)
                .AsNoTracking()
                .Where(s =>
                    s.ShiftSessionId == owner.ShiftSessionId &&
                    s.TerminalNo == Normalize(owner.TerminalNo) &&
                    s.CashierName == Normalize(owner.CashierName) &&
                    s.Status == CashierCartStatusCodes.Held)
                .OrderByDescending(s => s.HeldAtUtc ?? s.UpdatedAtUtc)
                .ToListAsync();

            return sessions.Select(ToDto).ToList();
        }

        public async Task<CashierCartSessionDto> SaveActiveAsync(CashierCartSaveRequest request)
        {
            ValidateSaveRequest(request);

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                CashierCartSession? session = await context.CashierCartSessions
                    .Include(s => s.Lines)
                    .FirstOrDefaultAsync(s => s.CartToken == request.CartToken);

                if (session == null)
                {
                    CashierCartSession? existingActive = await context.CashierCartSessions
                        .Include(s => s.Lines)
                        .FirstOrDefaultAsync(s =>
                            s.ShiftSessionId == request.Owner.ShiftSessionId &&
                            s.TerminalNo == Normalize(request.Owner.TerminalNo) &&
                            s.CashierName == Normalize(request.Owner.CashierName) &&
                            s.Status == CashierCartStatusCodes.Active);

                    if (existingActive != null)
                    {
                        if (existingActive.CartToken != request.CartToken)
                        {
                            throw new InvalidOperationException(
                                $"Active cart {existingActive.ReferenceNo} already exists for this cashier and shift.");
                        }

                        session = existingActive;
                    }
                    else
                    {
                        session = new CashierCartSession
                        {
                            CartToken = request.CartToken,
                            ReferenceNo = CreateReferenceNo(request.CartToken),
                            ShiftSessionId = request.Owner.ShiftSessionId,
                            TerminalNo = Normalize(request.Owner.TerminalNo),
                            CashierName = Normalize(request.Owner.CashierName),
                            Status = CashierCartStatusCodes.Active,
                            CreatedAtUtc = DateTime.UtcNow,
                            CreatedBy = Normalize(request.Owner.CashierName),
                            Revision = 0
                        };

                        await context.CashierCartSessions.AddAsync(session);
                    }
                }

                EnsureOwner(session, request.Owner);

                if (!session.Status.Equals(CashierCartStatusCodes.Active, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Cart {session.ReferenceNo} is {session.Status} and cannot be saved as active.");
                }

                ApplySnapshot(session, request);

                context.CashierCartLines.RemoveRange(session.Lines);
                session.Lines.Clear();

                int lineNumber = 1;
                foreach (CashierCartLineSnapshotDto line in request.Lines)
                {
                    line.LineNumber = lineNumber;

                    session.Lines.Add(new CashierCartLine
                    {
                        LineNumber = lineNumber,
                        LineType = NormalizeLineType(line.LineType),
                        ItemVariantId = line.ItemVariantId > 0 ? line.ItemVariantId : null,
                        ItemBatchId = line.ItemBatchId > 0 ? line.ItemBatchId : null,
                        Description = Truncate(line.Description, 250),
                        Quantity = Math.Round(line.Quantity, 3),
                        UnitPrice = Math.Round(line.UnitPrice, 2),
                        LineTotal = Math.Round(
                            line.IsGiftVoucherSale || line.IsFreeItem
                                ? Math.Max(0m, line.UnitPrice * line.Quantity)
                                : Math.Max(0m, line.TaxInclusiveAmount > 0m ? line.TaxInclusiveAmount : line.UnitPrice * line.Quantity),
                            2),
                        SnapshotJson = JsonSerializer.Serialize(line, JsonOptions)
                    });

                    lineNumber++;
                }

                session.ItemCount = request.Lines.Count;
                session.TotalQuantity = Math.Round(request.Lines.Sum(l => l.Quantity), 3);
                session.Revision++;
                session.UpdatedAtUtc = DateTime.UtcNow;
                session.UpdatedBy = Normalize(request.Owner.CashierName);

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ToDto(session);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<CashierCartSessionDto> SuspendAsync(Guid cartToken, CashierCartOwnerDto owner)
        {
            ValidateOwner(owner);

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                CashierCartSession session = await LoadForUpdateAsync(context, cartToken);
                EnsureOwner(session, owner);

                if (!session.Status.Equals(CashierCartStatusCodes.Active, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Cart {session.ReferenceNo} is not active.");

                if (session.Lines.Count == 0)
                    throw new InvalidOperationException("An empty cart cannot be suspended.");

                DateTime now = DateTime.UtcNow;
                session.Status = CashierCartStatusCodes.Held;
                session.HeldAtUtc = now;
                session.HeldBy = Normalize(owner.CashierName);
                session.UpdatedAtUtc = now;
                session.UpdatedBy = Normalize(owner.CashierName);
                session.Revision++;

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                return ToDto(session);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<CashierCartSessionDto> RecallAsync(int cartSessionId, CashierCartOwnerDto owner)
        {
            ValidateOwner(owner);

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                bool activeExists = await context.CashierCartSessions.AnyAsync(s =>
                    s.ShiftSessionId == owner.ShiftSessionId &&
                    s.TerminalNo == Normalize(owner.TerminalNo) &&
                    s.CashierName == Normalize(owner.CashierName) &&
                    s.Status == CashierCartStatusCodes.Active);

                if (activeExists)
                    throw new InvalidOperationException("Cancel, complete or suspend the current active cart before recalling another cart.");

                CashierCartSession? session = await context.CashierCartSessions
                    .Include(s => s.Lines)
                    .FirstOrDefaultAsync(s => s.Id == cartSessionId);

                if (session == null)
                    throw new InvalidOperationException("The selected held cart was not found.");

                EnsureOwner(session, owner);

                if (!session.Status.Equals(CashierCartStatusCodes.Held, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Cart {session.ReferenceNo} is no longer held.");

                if (session.Lines.Count == 0)
                    throw new InvalidOperationException("The selected held cart has no lines.");

                DateTime now = DateTime.UtcNow;
                session.Status = CashierCartStatusCodes.Active;
                session.RecalledAtUtc = now;
                session.RecalledBy = Normalize(owner.CashierName);
                session.RecallCount++;
                session.UpdatedAtUtc = now;
                session.UpdatedBy = Normalize(owner.CashierName);
                session.Revision++;

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                return ToDto(session);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<CashierCartSessionDto> CancelAsync(
            Guid cartToken,
            CashierCartOwnerDto owner,
            string reasonCode,
            string reasonText)
        {
            ValidateOwner(owner);
            ValidateCancellationReason(reasonCode, reasonText);

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            await using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                CashierCartSession session = await LoadForUpdateAsync(context, cartToken);
                EnsureOwner(session, owner);

                if (!session.Status.Equals(CashierCartStatusCodes.Active, StringComparison.OrdinalIgnoreCase) &&
                    !session.Status.Equals(CashierCartStatusCodes.Held, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Cart {session.ReferenceNo} cannot be cancelled from status {session.Status}.");
                }

                DateTime now = DateTime.UtcNow;
                session.Status = CashierCartStatusCodes.Cancelled;
                session.CancellationReasonCode = Truncate(reasonCode, 50);
                session.CancellationReasonText = Truncate(reasonText, 250);
                session.CancelledAtUtc = now;
                session.CancelledBy = Normalize(owner.CashierName);
                session.UpdatedAtUtc = now;
                session.UpdatedBy = Normalize(owner.CashierName);
                session.Revision++;

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
                return ToDto(session);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<CashierCartSessionDto> CancelHeldAsync(
            int cartSessionId,
            CashierCartOwnerDto owner,
            string reasonCode,
            string reasonText)
        {
            ValidateOwner(owner);
            ValidateCancellationReason(reasonCode, reasonText);

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            CashierCartSession? session = await context.CashierCartSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == cartSessionId);

            if (session == null)
                throw new InvalidOperationException("Held cart was not found.");

            return await CancelAsync(session.CartToken, owner, reasonCode, reasonText);
        }

        public async Task<bool> HasBlockingCartsAsync(CashierCartOwnerDto owner)
        {
            ValidateOwner(owner);

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            return await context.CashierCartSessions.AnyAsync(s =>
                s.ShiftSessionId == owner.ShiftSessionId &&
                s.TerminalNo == Normalize(owner.TerminalNo) &&
                s.CashierName == Normalize(owner.CashierName) &&
                (s.Status == CashierCartStatusCodes.Active || s.Status == CashierCartStatusCodes.Held));
        }

        private static async Task<CashierCartSession> LoadForUpdateAsync(AppDbContext context, Guid cartToken)
        {
            if (cartToken == Guid.Empty)
                throw new InvalidOperationException("Cart token is required.");

            CashierCartSession? session = await context.CashierCartSessions
                .Include(s => s.Lines)
                .FirstOrDefaultAsync(s => s.CartToken == cartToken);

            return session ?? throw new InvalidOperationException("Cashier cart was not found.");
        }

        private static void ApplySnapshot(CashierCartSession session, CashierCartSaveRequest request)
        {
            CustomerSearchDto? customer = request.Customer;

            session.CustomerMasterId = customer?.Id > 0 ? customer.Id : null;
            session.CustomerCodeSnapshot = Truncate(customer?.CustomerCode, 30);
            session.CustomerNameSnapshot = Truncate(customer?.DisplayName ?? "Walk-In", 150);
            session.CustomerTypeSnapshot = Truncate(customer?.DisplayCustomerType ?? "Walk-In", 30);
            session.CustomerSnapshotJson = customer == null
                ? string.Empty
                : JsonSerializer.Serialize(customer, JsonOptions);
            session.IsWholesaleMode = request.IsWholesaleMode;
            session.InvoiceDiscountAmount = Math.Round(Math.Max(0m, request.InvoiceDiscountAmount), 2);
            session.GrossTotal = Math.Round(Math.Max(0m, request.GrossTotal), 2);
            session.TotalDiscount = Math.Round(Math.Max(0m, request.TotalDiscount), 2);
            session.NetTotal = Math.Round(Math.Max(0m, request.NetTotal), 2);
        }

        private static CashierCartSessionDto ToDto(CashierCartSession session)
        {
            CustomerSearchDto? customer = null;
            if (!string.IsNullOrWhiteSpace(session.CustomerSnapshotJson))
            {
                try
                {
                    customer = JsonSerializer.Deserialize<CustomerSearchDto>(session.CustomerSnapshotJson, JsonOptions);
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException(
                        $"Cart {session.ReferenceNo} contains an invalid customer snapshot.",
                        ex);
                }
            }

            List<CashierCartLineSnapshotDto> lines = session.Lines
                .OrderBy(l => l.LineNumber)
                .Select(line => DeserializeLine(session.ReferenceNo, line))
                .ToList();

            return new CashierCartSessionDto
            {
                Id = session.Id,
                CartToken = session.CartToken,
                ReferenceNo = session.ReferenceNo,
                ShiftSessionId = session.ShiftSessionId,
                TerminalNo = session.TerminalNo,
                CashierName = session.CashierName,
                Customer = customer,
                IsWholesaleMode = session.IsWholesaleMode,
                InvoiceDiscountAmount = session.InvoiceDiscountAmount,
                GrossTotal = session.GrossTotal,
                TotalDiscount = session.TotalDiscount,
                NetTotal = session.NetTotal,
                ItemCount = session.ItemCount,
                TotalQuantity = session.TotalQuantity,
                Status = session.Status,
                Revision = session.Revision,
                CreatedAtUtc = session.CreatedAtUtc,
                UpdatedAtUtc = session.UpdatedAtUtc,
                HeldAtUtc = session.HeldAtUtc,
                CompletedAtUtc = session.CompletedAtUtc,
                CancelledAtUtc = session.CancelledAtUtc,
                CancellationReasonCode = session.CancellationReasonCode,
                CancellationReasonText = session.CancellationReasonText,
                RecallCount = session.RecallCount,
                SalesHeaderId = session.SalesHeaderId,
                Lines = lines
            };
        }

        private static CashierCartLineSnapshotDto DeserializeLine(string referenceNo, CashierCartLine line)
        {
            try
            {
                CashierCartLineSnapshotDto? snapshot = JsonSerializer.Deserialize<CashierCartLineSnapshotDto>(
                    line.SnapshotJson,
                    JsonOptions);

                if (snapshot == null)
                    throw new JsonException("Snapshot was empty.");

                snapshot.LineNumber = line.LineNumber;
                snapshot.LineType = line.LineType;
                return snapshot;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Cart {referenceNo} line {line.LineNumber} contains invalid recovery data.",
                    ex);
            }
        }

        private static void ValidateSaveRequest(CashierCartSaveRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.CartToken == Guid.Empty)
                throw new InvalidOperationException("Cart token is required.");

            ValidateOwner(request.Owner);

            if (request.Lines == null || request.Lines.Count == 0)
                throw new InvalidOperationException("Cannot save an empty active cart.");

            if (request.Lines.Any(line => line.Quantity <= 0m))
                throw new InvalidOperationException("Every cart line must have a positive quantity.");
        }

        private static void ValidateOwner(CashierCartOwnerDto owner)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            if (owner.ShiftSessionId <= 0)
                throw new InvalidOperationException("Open shift is required for cart persistence.");

            if (string.IsNullOrWhiteSpace(owner.TerminalNo))
                throw new InvalidOperationException("Terminal number is required for cart persistence.");

            if (string.IsNullOrWhiteSpace(owner.CashierName))
                throw new InvalidOperationException("Cashier name is required for cart persistence.");
        }

        private static void EnsureOwner(CashierCartSession session, CashierCartOwnerDto owner)
        {
            if (session.ShiftSessionId != owner.ShiftSessionId ||
                !session.TerminalNo.Equals(Normalize(owner.TerminalNo), StringComparison.OrdinalIgnoreCase) ||
                !session.CashierName.Equals(Normalize(owner.CashierName), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The cart belongs to a different terminal, shift or cashier.");
            }
        }

        private static void ValidateCancellationReason(string reasonCode, string reasonText)
        {
            if (string.IsNullOrWhiteSpace(reasonCode))
                throw new InvalidOperationException("Cancellation reason is required.");

            if (reasonCode.Equals(CashierCartCancellationReasons.Other, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(reasonText))
            {
                throw new InvalidOperationException("A note is required when cancellation reason is Other.");
            }
        }

        private static string CreateReferenceNo(Guid token)
        {
            return $"CART-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{token.ToString("N")[..6].ToUpperInvariant()}";
        }

        private static string NormalizeLineType(string? value)
        {
            string type = Normalize(value);
            return type switch
            {
                CashierCartLineTypeCodes.Service => CashierCartLineTypeCodes.Service,
                CashierCartLineTypeCodes.GiftVoucherSale => CashierCartLineTypeCodes.GiftVoucherSale,
                _ => CashierCartLineTypeCodes.StockItem
            };
        }

        private static string Normalize(string? value) => (value ?? string.Empty).Trim();

        private static string Truncate(string? value, int maxLength)
        {
            string normalized = Normalize(value);
            return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
        }
    }
}
