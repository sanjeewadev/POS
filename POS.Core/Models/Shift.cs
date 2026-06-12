using System;

namespace POS.Core.Models
{
    public class Shift
    {
        public int Id { get; set; }
        public int CashierId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; } // Nullable because it is empty while the shift is running
        public decimal StartingCash { get; set; } // The float (e.g., LKR 5000)
        public decimal ExpectedClosingCash { get; set; } // Calculated by the system (Start Cash + Sales - Expenses)
        public decimal? ActualClosingCash { get; set; } // Counted by the cashier at night
        public required string Status { get; set; } // "OPEN" or "CLOSED"
    }
}