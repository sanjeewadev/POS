using System;

namespace POS.Core.EntModelsities
{
    public class CreditLedger
    {
        public int Id { get; set; }
        public int CreditorId { get; set; }
        public DateTime Date { get; set; }
        public int? InvoiceId { get; set; } // Nullable because a cash payment doesn't have an invoice
        public decimal Amount { get; set; }
        public required string TransactionType { get; set; } // "CHARGE" (Debt goes up) or "PAYMENT" (Debt goes down)
    }
}