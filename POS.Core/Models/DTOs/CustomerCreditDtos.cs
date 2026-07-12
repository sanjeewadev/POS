using System;
using System.Collections.Generic;

namespace POS.Core.Models.DTOs
{
    public sealed class CustomerCreditValidationDto
    {
        public int CustomerId { get; init; }
        public string CustomerCode { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public decimal CreditLimit { get; init; }
        public decimal CurrentBalance { get; init; }
        public decimal AvailableCredit { get; init; }
        public int CreditDays { get; init; }
        public bool CanUseCredit { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    public sealed class CustomerOpenInvoiceDto
    {
        public int LedgerId { get; init; }
        public int? SalesHeaderId { get; init; }
        public string InvoiceNo { get; init; } = string.Empty;
        public DateTime InvoiceDate { get; init; }
        public DateTime? DueDate { get; init; }
        public decimal OriginalAmount { get; init; }
        public decimal AllocatedAmount { get; init; }
        public decimal OutstandingAmount { get; init; }
        public string Status { get; init; } = string.Empty;
        public int OverdueDays { get; init; }
    }

    public sealed class CustomerLedgerStatementRowDto
    {
        public int LedgerId { get; init; }
        public DateTime TransactionDate { get; init; }
        public string DocumentRef { get; init; } = string.Empty;
        public string TransactionType { get; init; } = string.Empty;
        public DateTime? DueDate { get; init; }
        public decimal DebitAmount { get; init; }
        public decimal CreditAmount { get; init; }
        public decimal RunningBalance { get; init; }
        public decimal OutstandingAmount { get; init; }
        public string Status { get; init; } = string.Empty;
        public string Remarks { get; init; } = string.Empty;
        public string ProcessedBy { get; init; } = string.Empty;
    }

    public sealed class CustomerAccountSummaryDto
    {
        public int CustomerId { get; init; }
        public string CustomerCode { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public string CustomerType { get; init; } = string.Empty;
        public decimal CreditLimit { get; init; }
        public decimal CurrentBalance { get; init; }
        public decimal AvailableCredit { get; init; }
        public decimal OverdueAmount { get; init; }
        public decimal AgingCurrent { get; init; }
        public decimal Aging1To30 { get; init; }
        public decimal Aging31To60 { get; init; }
        public decimal Aging61To90 { get; init; }
        public decimal AgingOver90 { get; init; }
        public IReadOnlyList<CustomerOpenInvoiceDto> OpenInvoices { get; init; } = Array.Empty<CustomerOpenInvoiceDto>();
        public IReadOnlyList<CustomerLedgerStatementRowDto> StatementRows { get; init; } = Array.Empty<CustomerLedgerStatementRowDto>();
    }

    public sealed class CustomerPaymentRequest
    {
        public Guid ReceiptToken { get; set; } = Guid.NewGuid();
        public int CustomerId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public DateTime PaymentDate { get; set; } = DateTime.Now;
        public string ReferenceNo { get; set; } = string.Empty;
        public string BankOrCardType { get; set; } = string.Empty;
        public string DestinationAccount { get; set; } = string.Empty;
        public string ProcessedBy { get; set; } = string.Empty;
        public string TerminalNo { get; set; } = string.Empty;
        public int? ShiftSessionId { get; set; }
        public string Remarks { get; set; } = string.Empty;
    }

    public sealed class CustomerPaymentResultDto
    {
        public int ReceiptId { get; init; }
        public string ReceiptNo { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public decimal RemainingBalance { get; init; }
        public IReadOnlyList<CustomerLedgerAllocationDto> Allocations { get; init; } = Array.Empty<CustomerLedgerAllocationDto>();
    }

    public sealed class CustomerLedgerAllocationDto
    {
        public string InvoiceNo { get; init; } = string.Empty;
        public decimal Amount { get; init; }
    }
}
