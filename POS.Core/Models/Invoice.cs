using System;

namespace POS.Core.Models
{
    public class Invoice
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int CashierId { get; set; }
        public DateTime Date { get; set; }
        public required string Status { get; set; }
        public decimal GrossTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal NetTotal { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
    }
}