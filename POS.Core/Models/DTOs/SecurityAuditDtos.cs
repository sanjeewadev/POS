using System;

namespace POS.Core.Models.DTOs
{
    // ==============================================================================
    // 1. MACRO SECURITY SUMMARY (For Top KPI Cards)
    // ==============================================================================
    public class SecurityAuditSummaryDto
    {
        public int TotalVoidCount { get; set; }
        public decimal TotalVoidAmount { get; set; }

        public int TotalReturnCount { get; set; }
        public decimal TotalReturnAmount { get; set; }

        public int SuspendedCartCount { get; set; }

        // Count of cashiers whose void/return ratios exceed normal thresholds
        public int HighRiskCashierCount { get; set; }
    }

    // ==============================================================================
    // 2. RETURN AUDIT RECORD (The Refund Ledger)
    // ==============================================================================
    public class ReturnAuditRecordDto
    {
        public string ReturnNo { get; set; } = string.Empty;
        public string OriginalInvoiceNo { get; set; } = string.Empty;
        public DateTime ReturnDate { get; set; }

        public string CashierName { get; set; } = string.Empty;
        public string TerminalNo { get; set; } = string.Empty;
        public string AuthorizedBy { get; set; } = string.Empty;

        public decimal RefundAmount { get; set; }
        public string RefundMethod { get; set; } = string.Empty;

        // UI Flag: If a cashier authorized their own high-value return, it is a severe security risk
        public bool IsSelfAuthorized => !string.IsNullOrWhiteSpace(AuthorizedBy) &&
                                        CashierName.Equals(AuthorizedBy, StringComparison.OrdinalIgnoreCase);
    }

    // ==============================================================================
    // 3. VOID & SUSPENDED RECORD (The Cancelled Sales Ledger)
    // ==============================================================================
    public class VoidAuditRecordDto
    {
        public string InvoiceNo { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string CashierName { get; set; } = string.Empty;
        public string TerminalNo { get; set; } = string.Empty;

        public decimal AttemptedAmount { get; set; }
        public string Status { get; set; } = string.Empty; // "Voided" or "Suspended"
    }

    // ==============================================================================
    // 4. CASHIER FRAUD RISK PROFILE (Behavioral Analytics)
    // ==============================================================================
    public class CashierFraudRiskDto
    {
        public string CashierName { get; set; } = string.Empty;

        public int TotalTransactions { get; set; }
        public int VoidCount { get; set; }
        public int ReturnCount { get; set; }

        // Ratios are far more important than raw numbers for catching fraud
        public decimal VoidRate => TotalTransactions == 0 ? 0 : Math.Round((decimal)VoidCount / TotalTransactions * 100, 2);
        public decimal ReturnRate => TotalTransactions == 0 ? 0 : Math.Round((decimal)ReturnCount / TotalTransactions * 100, 2);

        // Automatically flag a cashier if their Voids exceed 5% or Returns exceed 10% of their total traffic
        public bool IsHighRisk => VoidRate > 5m || ReturnRate > 10m;
    }
}