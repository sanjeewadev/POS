using System;
using System.Collections.Generic;

namespace POS.Core.Models.DTOs
{
    public sealed class SecurityAuditSummaryDto
    {
        public int TotalEventCount { get; set; }
        public int FailedLoginCount { get; set; }
        public int CancelledCartCount { get; set; }
        public int CustomerReturnCount { get; set; }
        public int ApprovalEventCount { get; set; }
        public int DrawerFailureCount { get; set; }
        public int UnusualCashierCount { get; set; }
    }

    public sealed class SecurityAuditEventDto
    {
        public DateTime OccurredAt { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Severity { get; set; } = "Information";
        public string Actor { get; set; } = string.Empty;
        public string AuthorizedBy { get; set; } = string.Empty;
        public string TerminalNo { get; set; } = string.Empty;
        public string ReferenceNo { get; set; } = string.Empty;
        public decimal? Amount { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public sealed class CashierActivitySummaryDto
    {
        public string CashierName { get; set; } = string.Empty;
        public int CompletedSales { get; set; }
        public int CancelledCarts { get; set; }
        public int CustomerReturns { get; set; }
        public int ManualDiscounts { get; set; }
        public int PriceOverrides { get; set; }
        public int FreeIssues { get; set; }
        public decimal ReturnRatePercent => CompletedSales == 0
            ? 0m
            : Math.Round((decimal)CustomerReturns / CompletedSales * 100m, 2);
        public bool IsUnusual => CancelledCarts >= 5 || ReturnRatePercent > 10m || PriceOverrides >= 10;
        public string ReviewNote => IsUnusual ? "Review activity" : "Normal";
    }

    public sealed class SecurityAuditResultDto
    {
        public SecurityAuditSummaryDto Summary { get; set; } = new();
        public List<SecurityAuditEventDto> Events { get; set; } = new();
        public List<CashierActivitySummaryDto> CashierActivity { get; set; } = new();
    }
}
