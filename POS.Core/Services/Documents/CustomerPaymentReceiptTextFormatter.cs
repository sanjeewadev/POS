using System;
using System.Globalization;
using System.Linq;
using System.Text;
using POS.Core.Models.DTOs;

namespace POS.Core.Services.Documents
{
    public sealed class CustomerPaymentReceiptTextFormatter
    {
        public string Format(CustomerPaymentReceiptDocumentDto document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var text = new StringBuilder();
            text.AppendLine(FirstNonEmpty(document.StoreName, "STORE"));
            if (!string.IsNullOrWhiteSpace(document.StoreAddress))
                text.AppendLine(document.StoreAddress.Trim());
            if (!string.IsNullOrWhiteSpace(document.StorePhone))
                text.AppendLine($"Tel: {document.StorePhone.Trim()}");
            text.AppendLine();
            text.AppendLine("CUSTOMER PAYMENT RECEIPT");
            text.AppendLine(new string('-', 64));
            text.AppendLine($"Receipt No : {document.ReceiptNo}");
            text.AppendLine($"Payment Date: {document.PaymentDate:yyyy-MM-dd HH:mm}");
            text.AppendLine($"Customer    : {document.CustomerCode} - {document.CustomerName}");
            text.AppendLine($"Method      : {document.PaymentMethod}");
            if (!string.IsNullOrWhiteSpace(document.ReferenceNo))
                text.AppendLine($"Reference   : {document.ReferenceNo}");
            if (!string.IsNullOrWhiteSpace(document.BankOrCardType))
                text.AppendLine($"Bank/Card   : {document.BankOrCardType}");
            if (!string.IsNullOrWhiteSpace(document.DestinationAccount))
                text.AppendLine($"Destination : {document.DestinationAccount}");
            text.AppendLine($"Processed By: {document.ProcessedBy}");
            if (!string.IsNullOrWhiteSpace(document.TerminalNo))
                text.AppendLine($"Terminal    : {document.TerminalNo}");
            text.AppendLine(new string('-', 64));
            text.AppendLine($"AMOUNT RECEIVED: Rs. {Money(document.Amount)}");
            text.AppendLine($"REMAINING BALANCE: Rs. {Money(document.RemainingBalance)}");
            text.AppendLine(new string('-', 64));
            text.AppendLine("ALLOCATIONS");
            if (document.Allocations.Count == 0)
            {
                text.AppendLine("Unallocated / account-level payment");
            }
            else
            {
                foreach (CustomerLedgerAllocationDto allocation in document.Allocations)
                    text.AppendLine($"{allocation.InvoiceNo,-36} Rs. {allocation.Amount.ToString("N2", CultureInfo.InvariantCulture),12}");
            }
            decimal allocated = document.Allocations.Sum(row => row.Amount);
            text.AppendLine(new string('-', 64));
            text.AppendLine($"Allocated: Rs. {Money(allocated)}");
            if (!string.IsNullOrWhiteSpace(document.Remarks))
                text.AppendLine($"Remarks: {document.Remarks.Trim()}");
            text.AppendLine();
            text.AppendLine("This receipt confirms payment received against the customer account.");
            return text.ToString();
        }

        private static string Money(decimal value) =>
            value.ToString("N2", CultureInfo.InvariantCulture);

        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }
}
