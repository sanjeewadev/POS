using System;

namespace POS.Core.Models
{
    public class Expense
    {
        public int Id { get; set; }
        public int ShiftId { get; set; } // Links the expense to the active shift
        public required string Description { get; set; } // e.g., "Staff Tea"
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }
}