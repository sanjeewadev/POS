using System;

namespace POS.Core.Models.DTOs
{
    public sealed class ShiftCashSummaryDto
    {
        public int ShiftSessionId { get; set; }
        public string TerminalNo { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;
        public DateTime OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ZReportNo { get; set; } = string.Empty;

        public int CompletedSaleCount { get; set; }
        public int CustomerReturnCount { get; set; }

        public decimal GrossSales { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal NetSales { get; set; }
        public decimal VatTotal { get; set; }

        public decimal CashTenderTotal { get; set; }
        public decimal CardTenderTotal { get; set; }
        public decimal ChequeTenderTotal { get; set; }
        public decimal GiftVoucherTenderTotal { get; set; }
        public decimal CustomerCreditTenderTotal { get; set; }
        public decimal OtherTenderTotal { get; set; }

        public decimal OpeningCash { get; set; }
        public decimal PaidInTotal { get; set; }
        public decimal FloatInTotal { get; set; }
        public decimal PaidOutTotal { get; set; }
        public decimal FloatOutTotal { get; set; }
        public decimal CashRefundTotal { get; set; }

        public decimal ExpectedCash { get; set; }
        public decimal CountedCash { get; set; }
        public decimal Variance { get; set; }
        public string ClosedBy { get; set; } = string.Empty;
        public string AuthorizedBy { get; set; } = string.Empty;
        public string VarianceNote { get; set; } = string.Empty;
        public bool IsSnapshot { get; set; }
    }

    public sealed class CashMovementRegistrationRequest
    {
        public int ShiftSessionId { get; set; }
        public string MovementType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string ReasonCategory { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;
        public string AuthorizedBy { get; set; } = string.Empty;
    }

    public sealed class ShiftCloseRequest
    {
        public int ShiftSessionId { get; set; }
        public string TerminalNo { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;
        public decimal CountedCash { get; set; }
        public string ClosedBy { get; set; } = string.Empty;
        public string AuthorizedBy { get; set; } = string.Empty;
        public string VarianceNote { get; set; } = string.Empty;
        public Guid CloseToken { get; set; } = Guid.NewGuid();
    }

    public sealed class CashDrawerEventRequest
    {
        public int ShiftSessionId { get; set; }
        public string TerminalNo { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string AuthorizedBy { get; set; } = string.Empty;
        public int? SalesHeaderId { get; set; }
        public int? CashMovementId { get; set; }
        public bool Succeeded { get; set; }
        public string FailureMessage { get; set; } = string.Empty;
    }
}
