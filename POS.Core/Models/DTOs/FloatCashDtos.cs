using System;

namespace POS.Core.DTOs
{
    // ==============================================================================
    // 1. MACRO LEVEL: The Shift Audit (Used for the Main Grid)
    // ==============================================================================
    public class ShiftAuditDto
    {
        public int ShiftId { get; set; }
        public string TerminalNo { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;

        public DateTime OpenTime { get; set; }
        public DateTime? CloseTime { get; set; }

        // Status: "Open", "Closed", or "Discrepancy"
        public string Status { get; set; } = string.Empty;

        // --- THE CASH LEDGER MATH ---
        public decimal OpeningFloat { get; set; }
        public decimal TotalCashIn { get; set; }  // Cash Sales + Manual Paid In
        public decimal TotalCashOut { get; set; } // Cash Refunds + Petty Cash/Paid Out

        // What SHOULD be in the drawer:
        public decimal ExpectedCash => OpeningFloat + TotalCashIn - TotalCashOut;

        // What was ACTUALLY counted by the manager/cashier at the end of the shift:
        public decimal ActualCash { get; set; }

        // The critical security metric: 
        // Negative = Shortage (Cash missing). Positive = Overage (Too much cash).
        public decimal Variance => CloseTime.HasValue ? (ActualCash - ExpectedCash) : 0m;

        // Helper for UI styling
        public bool HasDiscrepancy => CloseTime.HasValue && Math.Abs(Variance) > 0;
    }

    // ==============================================================================
    // 2. MICRO LEVEL: The Drill-Down Ledger (Minute-by-Minute actions)
    // ==============================================================================
    public class CashMovementDto
    {
        public int MovementId { get; set; }
        public DateTime Timestamp { get; set; }

        // e.g., "Opening Float", "Cash Sale", "Refund", "Petty Cash", "Drawer Skim"
        public string MovementType { get; set; } = string.Empty;

        // e.g., INV-0045, RTN-0012, or PC-001
        public string ReferenceNo { get; set; } = string.Empty;

        // Note: For UI clarity, IN amounts will be positive, OUT amounts will be negative.
        public decimal Amount { get; set; }

        public string Remarks { get; set; } = string.Empty;
    }
}