using System;

namespace POS.Core.DTOs
{
    public class ShiftAuditDto
    {
        public int ShiftId { get; set; }
        public string TerminalNo { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;
        public DateTime OpenTime { get; set; }
        public DateTime? CloseTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ZReportNo { get; set; } = string.Empty;

        public decimal OpeningFloat { get; set; }
        public decimal CashTenderTotal { get; set; }
        public decimal CardTenderTotal { get; set; }
        public decimal ChequeTenderTotal { get; set; }
        public decimal OtherTenderTotal { get; set; }
        public decimal TotalCashIn { get; set; }
        public decimal TotalCashOut { get; set; }
        public decimal CashRefundTotal { get; set; }
        public decimal ExpectedCash { get; set; }
        public decimal ActualCash { get; set; }
        public decimal Variance { get; set; }
        public string AuthorizedBy { get; set; } = string.Empty;
        public bool IsSnapshot { get; set; }

        public bool HasDiscrepancy => CloseTime.HasValue && Math.Abs(Variance) > 0m;
    }

    public class CashMovementDto
    {
        public int MovementId { get; set; }
        public DateTime Timestamp { get; set; }
        public string MovementType { get; set; } = string.Empty;
        public string ReferenceNo { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Remarks { get; set; } = string.Empty;
    }
}
