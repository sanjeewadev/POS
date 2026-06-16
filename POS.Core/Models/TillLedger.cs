using System;
using System.ComponentModel.DataAnnotations;

namespace POS.Core.Models
{
    public class TillLedger
    {
        public int Id { get; set; }

        public int ShiftSessionId { get; set; }
        public ShiftSession ShiftSession { get; set; } = null!;

        public DateTime TransactionDate { get; set; } = DateTime.Now;

        [MaxLength(30)]
        public string TransactionType { get; set; } = string.Empty; // e.g., "Sale", "PaidIn", "PaidOut", "Float"

        // Positive numbers mean cash went IN the drawer. Negative means cash went OUT.
        public decimal Amount { get; set; } = 0m;

        [MaxLength(50)]
        public string ReferenceNo { get; set; } = string.Empty; // Invoice No, or Reason for Paid Out

        [MaxLength(200)]
        public string Remarks { get; set; } = string.Empty;
    }
}