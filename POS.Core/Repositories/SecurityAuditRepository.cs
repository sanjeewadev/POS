using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Core.Repositories
{
    public sealed class SecurityAuditRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public SecurityAuditRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<SecurityAuditResultDto> GetAuditAsync(
            DateTime startDate,
            DateTime endDate,
            string searchText)
        {
            if (startDate.Date > endDate.Date)
                throw new ArgumentException("Start date cannot be later than end date.");

            DateTime localStart = startDate.Date;
            DateTime localEndExclusive = endDate.Date.AddDays(1);
            DateTime utcStart = ToUtcBoundary(localStart);
            DateTime utcEndExclusive = ToUtcBoundary(localEndExclusive);
            string search = Normalize(searchText);

            await using AppDbContext context = await _contextFactory.CreateDbContextAsync();
            var events = new List<SecurityAuditEventDto>();

            var cartRows = await context.CashierCartSessions
                .AsNoTracking()
                .Where(row =>
                    (row.HeldAtUtc.HasValue && row.HeldAtUtc >= utcStart && row.HeldAtUtc < utcEndExclusive) ||
                    (row.RecalledAtUtc.HasValue && row.RecalledAtUtc >= utcStart && row.RecalledAtUtc < utcEndExclusive) ||
                    (row.CompletedAtUtc.HasValue && row.CompletedAtUtc >= utcStart && row.CompletedAtUtc < utcEndExclusive) ||
                    (row.CancelledAtUtc.HasValue && row.CancelledAtUtc >= utcStart && row.CancelledAtUtc < utcEndExclusive))
                .Select(row => new
                {
                    row.ReferenceNo,
                    row.TerminalNo,
                    row.CashierName,
                    row.NetTotal,
                    row.HeldAtUtc,
                    row.HeldBy,
                    row.RecalledAtUtc,
                    row.RecalledBy,
                    row.CompletedAtUtc,
                    row.CancelledAtUtc,
                    row.CancelledBy,
                    row.CancellationReasonCode,
                    row.CancellationReasonText,
                    InvoiceNo = row.SalesHeader == null ? string.Empty : row.SalesHeader.InvoiceNo
                })
                .ToListAsync();

            foreach (var row in cartRows)
            {
                if (InUtcRange(row.HeldAtUtc, utcStart, utcEndExclusive))
                {
                    events.Add(CreateEvent(
                        ToLocal(row.HeldAtUtc!.Value),
                        "Cart Held",
                        "Information",
                        FirstNonEmpty(row.HeldBy, row.CashierName),
                        string.Empty,
                        row.TerminalNo,
                        row.ReferenceNo,
                        row.NetTotal,
                        "Cashier cart was suspended for later recall."));
                }

                if (InUtcRange(row.RecalledAtUtc, utcStart, utcEndExclusive))
                {
                    events.Add(CreateEvent(
                        ToLocal(row.RecalledAtUtc!.Value),
                        "Cart Recalled",
                        "Information",
                        FirstNonEmpty(row.RecalledBy, row.CashierName),
                        string.Empty,
                        row.TerminalNo,
                        row.ReferenceNo,
                        row.NetTotal,
                        "A held cashier cart was recalled."));
                }

                if (InUtcRange(row.CompletedAtUtc, utcStart, utcEndExclusive))
                {
                    events.Add(CreateEvent(
                        ToLocal(row.CompletedAtUtc!.Value),
                        "Cart Completed",
                        "Information",
                        row.CashierName,
                        string.Empty,
                        row.TerminalNo,
                        FirstNonEmpty(row.InvoiceNo, row.ReferenceNo),
                        row.NetTotal,
                        "Persistent cart completed as a sale."));
                }

                if (InUtcRange(row.CancelledAtUtc, utcStart, utcEndExclusive))
                {
                    string reason = string.Join(" - ", new[]
                    {
                        row.CancellationReasonCode,
                        row.CancellationReasonText
                    }.Where(value => !string.IsNullOrWhiteSpace(value)));

                    events.Add(CreateEvent(
                        ToLocal(row.CancelledAtUtc!.Value),
                        "Cart Cancelled",
                        "Review",
                        FirstNonEmpty(row.CancelledBy, row.CashierName),
                        string.Empty,
                        row.TerminalNo,
                        row.ReferenceNo,
                        row.NetTotal,
                        string.IsNullOrWhiteSpace(reason) ? "Cashier cart was cancelled." : reason));
                }
            }

            var returns = await context.CustomerReturnHeaders
                .AsNoTracking()
                .Where(row => row.ReturnDate >= localStart && row.ReturnDate < localEndExclusive)
                .Select(row => new
                {
                    row.ReturnDate,
                    ReferenceNo = row.CreditNoteNo ?? row.ReturnNo,
                    row.OriginalInvoiceNo,
                    row.CashierName,
                    row.AuthorizedBy,
                    row.TerminalNo,
                    row.TotalRefundAmount,
                    row.RefundMethod
                })
                .ToListAsync();

            events.AddRange(returns.Select(row => CreateEvent(
                row.ReturnDate,
                "Customer Return",
                "Review",
                row.CashierName,
                row.AuthorizedBy,
                row.TerminalNo,
                row.ReferenceNo,
                row.TotalRefundAmount,
                $"Return against {FirstNonEmpty(row.OriginalInvoiceNo, "historical sale")} by {row.RefundMethod}.")));

            var discounts = await context.SalesLineDiscountAudits
                .AsNoTracking()
                .Where(row => row.CreatedAt >= localStart && row.CreatedAt < localEndExclusive)
                .Select(row => new
                {
                    row.CreatedAt,
                    row.InvoiceNo,
                    row.CashierName,
                    row.TerminalNo,
                    row.ApprovedBy,
                    row.DiscountAmount,
                    row.ReasonName,
                    row.Remarks
                })
                .ToListAsync();

            events.AddRange(discounts.Select(row => CreateEvent(
                row.CreatedAt,
                "Manual Discount",
                string.IsNullOrWhiteSpace(row.ApprovedBy) ? "Information" : "Approval",
                row.CashierName,
                row.ApprovedBy,
                row.TerminalNo,
                row.InvoiceNo,
                row.DiscountAmount,
                FirstNonEmpty(row.ReasonName, row.Remarks, "Manual line discount."))));

            var controlledLines = await context.SalesLines
                .AsNoTracking()
                .Where(line =>
                    line.SalesHeader.TransactionDate >= localStart &&
                    line.SalesHeader.TransactionDate < localEndExclusive &&
                    line.SalesHeader.Status == "Completed" &&
                    !line.SalesHeader.IsVoided &&
                    (line.IsPriceOverridden || line.IsFreeItem))
                .Select(line => new
                {
                    line.SalesHeader.TransactionDate,
                    line.SalesHeader.InvoiceNo,
                    line.SalesHeader.CashierName,
                    line.SalesHeader.TerminalNo,
                    line.ItemDescription,
                    line.IsPriceOverridden,
                    line.PriceOverrideAmount,
                    line.PriceOverrideApprovedBy,
                    line.IsFreeItem,
                    line.FreeIssueSellingValue,
                    line.FreeIssueRuleName,
                    line.FreeApprovedBy,
                    line.FreeIssueAppliedBy
                })
                .ToListAsync();

            foreach (var line in controlledLines)
            {
                if (line.IsPriceOverridden)
                {
                    events.Add(CreateEvent(
                        line.TransactionDate,
                        "Price Override",
                        "Approval",
                        line.CashierName,
                        line.PriceOverrideApprovedBy,
                        line.TerminalNo,
                        line.InvoiceNo,
                        line.PriceOverrideAmount,
                        $"Price override on {line.ItemDescription}."));
                }

                if (line.IsFreeItem)
                {
                    events.Add(CreateEvent(
                        line.TransactionDate,
                        "Free Issue",
                        string.IsNullOrWhiteSpace(line.FreeApprovedBy) ? "Information" : "Approval",
                        FirstNonEmpty(line.FreeIssueAppliedBy, line.CashierName),
                        line.FreeApprovedBy,
                        line.TerminalNo,
                        line.InvoiceNo,
                        line.FreeIssueSellingValue,
                        FirstNonEmpty(line.FreeIssueRuleName, line.ItemDescription)));
                }
            }

            var drawerRows = await context.CashDrawerEvents
                .AsNoTracking()
                .Where(row => row.RequestedAtUtc >= utcStart && row.RequestedAtUtc < utcEndExclusive &&
                    (row.EventType == "No Sale" || !row.Succeeded))
                .Select(row => new
                {
                    row.RequestedAtUtc,
                    row.EventType,
                    row.CashierName,
                    row.AuthorizedBy,
                    row.TerminalNo,
                    row.Succeeded,
                    row.Reason,
                    row.Note,
                    row.FailureMessage,
                    row.SalesHeaderId
                })
                .ToListAsync();

            events.AddRange(drawerRows.Select(row => CreateEvent(
                ToLocal(row.RequestedAtUtc),
                row.Succeeded ? row.EventType : "Drawer Failure",
                row.Succeeded ? "Approval" : "Warning",
                row.CashierName,
                row.AuthorizedBy,
                row.TerminalNo,
                row.SalesHeaderId.HasValue ? $"SALE-{row.SalesHeaderId.Value}" : string.Empty,
                null,
                FirstNonEmpty(row.FailureMessage, row.Note, row.Reason))));

            var loginRows = await context.LoginAuditEvents
                .AsNoTracking()
                .Where(row => row.EventTimeUtc >= utcStart && row.EventTimeUtc < utcEndExclusive &&
                    row.EventType != "Success")
                .Select(row => new
                {
                    row.EventTimeUtc,
                    row.EventType,
                    row.UsernameAttempted,
                    row.ApplicationName,
                    row.MachineName,
                    row.Message
                })
                .ToListAsync();

            events.AddRange(loginRows.Select(row => CreateEvent(
                ToLocal(row.EventTimeUtc),
                row.EventType.StartsWith("Approval", StringComparison.OrdinalIgnoreCase)
                    ? "Manager Approval " + row.EventType["Approval".Length..]
                    : "Login " + row.EventType,
                row.EventType.Contains("Success", StringComparison.OrdinalIgnoreCase)
                    ? "Approval"
                    : "Warning",
                row.UsernameAttempted,
                string.Empty,
                row.MachineName,
                row.ApplicationName,
                null,
                row.Message)));

            var documentRows = await context.SalesDocumentAudits
                .AsNoTracking()
                .Where(row => row.OccurredAtUtc >= utcStart && row.OccurredAtUtc < utcEndExclusive &&
                    (!row.IsSuccessful || row.EventType.Contains("Reprint")))
                .Select(row => new
                {
                    row.OccurredAtUtc,
                    row.DocumentType,
                    row.DocumentNumber,
                    row.EventType,
                    row.IsSuccessful,
                    row.PerformedBy,
                    row.TerminalNo,
                    row.PrinterName,
                    row.ErrorMessage
                })
                .ToListAsync();

            events.AddRange(documentRows.Select(row => CreateEvent(
                ToLocal(row.OccurredAtUtc),
                row.IsSuccessful ? "Document Reprint" : "Document Print Failure",
                row.IsSuccessful ? "Review" : "Warning",
                row.PerformedBy,
                string.Empty,
                row.TerminalNo,
                row.DocumentNumber,
                null,
                FirstNonEmpty(row.ErrorMessage, $"{row.DocumentType} {row.EventType} on {row.PrinterName}."))));

            if (!string.IsNullOrWhiteSpace(search))
            {
                events = events.Where(row =>
                    Contains(row.EventType, search) ||
                    Contains(row.Actor, search) ||
                    Contains(row.AuthorizedBy, search) ||
                    Contains(row.TerminalNo, search) ||
                    Contains(row.ReferenceNo, search) ||
                    Contains(row.Description, search)).ToList();
            }

            events = events
                .OrderByDescending(row => row.OccurredAt)
                .ThenBy(row => row.EventType)
                .ToList();

            var activity = await BuildCashierActivityAsync(
                context,
                localStart,
                localEndExclusive,
                utcStart,
                utcEndExclusive);

            return new SecurityAuditResultDto
            {
                Events = events,
                CashierActivity = activity,
                Summary = new SecurityAuditSummaryDto
                {
                    TotalEventCount = events.Count,
                    FailedLoginCount = loginRows.Count(row =>
                        !row.EventType.Contains("Success", StringComparison.OrdinalIgnoreCase)),
                    CancelledCartCount = cartRows.Count(row =>
                        InUtcRange(row.CancelledAtUtc, utcStart, utcEndExclusive)),
                    CustomerReturnCount = returns.Count,
                    ApprovalEventCount = events.Count(row => row.Severity == "Approval"),
                    DrawerFailureCount = drawerRows.Count(row => !row.Succeeded),
                    UnusualCashierCount = activity.Count(row => row.IsUnusual)
                }
            };
        }

        private static async Task<List<CashierActivitySummaryDto>> BuildCashierActivityAsync(
            AppDbContext context,
            DateTime localStart,
            DateTime localEndExclusive,
            DateTime utcStart,
            DateTime utcEndExclusive)
        {
            var sales = await context.SalesHeaders.AsNoTracking()
                .Where(row => row.Status == "Completed" && !row.IsVoided &&
                    row.TransactionDate >= localStart && row.TransactionDate < localEndExclusive)
                .GroupBy(row => row.CashierName)
                .Select(group => new NamedCountRow { Name = group.Key, Count = group.Count() })
                .ToListAsync();

            var cancelled = await context.CashierCartSessions.AsNoTracking()
                .Where(row => row.CancelledAtUtc.HasValue &&
                    row.CancelledAtUtc >= utcStart && row.CancelledAtUtc < utcEndExclusive)
                .GroupBy(row => row.CashierName)
                .Select(group => new NamedCountRow { Name = group.Key, Count = group.Count() })
                .ToListAsync();

            var returns = await context.CustomerReturnHeaders.AsNoTracking()
                .Where(row => row.ReturnDate >= localStart && row.ReturnDate < localEndExclusive)
                .GroupBy(row => row.CashierName)
                .Select(group => new NamedCountRow { Name = group.Key, Count = group.Count() })
                .ToListAsync();

            var discounts = await context.SalesLineDiscountAudits.AsNoTracking()
                .Where(row => row.CreatedAt >= localStart && row.CreatedAt < localEndExclusive)
                .GroupBy(row => row.CashierName)
                .Select(group => new NamedCountRow { Name = group.Key, Count = group.Count() })
                .ToListAsync();

            var overrides = await context.SalesLines.AsNoTracking()
                .Where(row => row.IsPriceOverridden &&
                    row.SalesHeader.TransactionDate >= localStart &&
                    row.SalesHeader.TransactionDate < localEndExclusive)
                .GroupBy(row => row.SalesHeader.CashierName)
                .Select(group => new NamedCountRow { Name = group.Key, Count = group.Count() })
                .ToListAsync();

            var freeIssues = await context.SalesLines.AsNoTracking()
                .Where(row => row.IsFreeItem &&
                    row.SalesHeader.TransactionDate >= localStart &&
                    row.SalesHeader.TransactionDate < localEndExclusive)
                .GroupBy(row => row.SalesHeader.CashierName)
                .Select(group => new NamedCountRow { Name = group.Key, Count = group.Count() })
                .ToListAsync();

            var names = sales.Select(row => row.Name)
                .Concat(cancelled.Select(row => row.Name))
                .Concat(returns.Select(row => row.Name))
                .Concat(discounts.Select(row => row.Name))
                .Concat(overrides.Select(row => row.Name))
                .Concat(freeIssues.Select(row => row.Name))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToList();

            return names.Select(name => new CashierActivitySummaryDto
            {
                CashierName = name,
                CompletedSales = Lookup(sales, name),
                CancelledCarts = Lookup(cancelled, name),
                CustomerReturns = Lookup(returns, name),
                ManualDiscounts = Lookup(discounts, name),
                PriceOverrides = Lookup(overrides, name),
                FreeIssues = Lookup(freeIssues, name)
            }).OrderByDescending(row => row.IsUnusual)
              .ThenByDescending(row => row.ReturnRatePercent)
              .ThenBy(row => row.CashierName)
              .ToList();
        }

        private static int Lookup(IEnumerable<NamedCountRow> rows, string name)
        {
            return rows.FirstOrDefault(row =>
                row.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Count ?? 0;
        }

        private sealed class NamedCountRow
        {
            public string Name { get; set; } = string.Empty;
            public int Count { get; set; }
        }

        private static SecurityAuditEventDto CreateEvent(
            DateTime occurredAt,
            string eventType,
            string severity,
            string actor,
            string authorizedBy,
            string terminalNo,
            string referenceNo,
            decimal? amount,
            string description)
        {
            return new SecurityAuditEventDto
            {
                OccurredAt = occurredAt,
                EventType = eventType,
                Severity = severity,
                Actor = actor,
                AuthorizedBy = authorizedBy,
                TerminalNo = terminalNo,
                ReferenceNo = referenceNo,
                Amount = amount,
                Description = description
            };
        }

        private static bool InUtcRange(DateTime? value, DateTime start, DateTime endExclusive) =>
            value.HasValue && value.Value >= start && value.Value < endExclusive;

        private static DateTime ToUtcBoundary(DateTime localDate) =>
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDate, DateTimeKind.Local));

        private static DateTime ToLocal(DateTime utcDate) =>
            DateTime.SpecifyKind(utcDate, DateTimeKind.Utc).ToLocalTime();

        private static string Normalize(string? value) => (value ?? string.Empty).Trim();
        private static bool Contains(string? value, string search) =>
            (value ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase);
        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }
}
